using System.Drawing;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Recognizer;

public static class OnnxHelper
{
    public static InferenceSession LoadModel(string modelPath)
    {
        // var sessionOptions = new SessionOptions
        // {
        //     GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
        //     ExecutionMode = ExecutionMode.ORT_PARALLEL,
        //     EnableCpuMemArena = true,
        //     IntraOpNumThreads = Environment.ProcessorCount,
        //     InterOpNumThreads = 1
        // };
        SessionOptions sessionOptions = new()
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            EnableCpuMemArena = true
        };

        // CUDAが利用可能ならGPU推論を使用（パフォーマンス向上のため）
        try
        {
            sessionOptions.AppendExecutionProvider_CUDA(0);
        }
        catch
        {
            // CUDA利用不可時はCPU推論にフォールバック（環境依存エラー回避）
        }

        return new InferenceSession(modelPath, sessionOptions);
    }

    public static async Task<InferenceResult> Run(InferenceSession session, Mat inputImage, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            List<NamedOnnxValue> inputs = [];

            // Handle multiple inputs for YOLOv3
            if (session.InputMetadata.Count > 1)
            {
                // YOLOv3 format with input_1 and image_shape
                var imageInput = session.InputMetadata.First(x => x.Key == "input_1");
                var shapeInput = session.InputMetadata.First(x => x.Key == "image_shape");

                var inputTensor = PreprocessImage(inputImage, imageInput.Value.Dimensions);
                var imageSizeTensor = CreateImageShapeTensor(inputImage.Size());

                inputs.Add(NamedOnnxValue.CreateFromTensor(imageInput.Key, inputTensor));
                inputs.Add(NamedOnnxValue.CreateFromTensor(shapeInput.Key, imageSizeTensor));
            }
            else
            {
                // Standard single input for YOLOv8/v11
                var inputMeta = session.InputMetadata.First();
                var inputName = inputMeta.Key;
                var inputShape = inputMeta.Value.Dimensions;

                var inputTensor = PreprocessImage(inputImage, inputShape);
                inputs.Add(NamedOnnxValue.CreateFromTensor(inputName, inputTensor));
            }

            using var results = session.Run(inputs);
            return ProcessResults(results, inputImage.Size());
        }, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<InferenceResult> Run(InferenceSession session, string inputPath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            using var image = Cv2.ImRead(inputPath);
            return image.Empty()
          ? throw new ArgumentException($"Failed to load image: {inputPath}")
          : await Run(session, image, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static DenseTensor<float> PreprocessImage(Mat image, int[] inputShape)
    {
        var batchSize = inputShape[0] == -1 ? 1 : inputShape[0];
        var (channels, height, width) = GetImageDimensions(inputShape);
        var isNHWC = inputShape.Length == 4 && (inputShape[3] == 3 || inputShape[3] == 1);

        // Skip resize if dimensions are invalid (dynamic shapes)
        if (height <= 0 || width <= 0)
        {
            // Use default YOLO input size for dynamic shapes
            height = Constants.ImageProcessing.YoloInputHeight;
            width = Constants.ImageProcessing.YoloInputWidth;
            channels = 3;
        }

        using Mat resized = new();
        Cv2.Resize(image, resized, new OpenCvSharp.Size(width, height));

        // ONNXモデルはRGB入力が標準のためBGRから変換
        using Mat rgb = new();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        DenseTensor<float> tensor;

        if (isNHWC)
        {
            // NHWC形式 (batch, height, width, channels) - TensorFlow/ArcFace形式
            tensor = new DenseTensor<float>([batchSize, height, width, channels]);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var pixel = rgb.At<Vec3b>(y, x);

                    tensor[0, y, x, 0] = pixel[0] / Constants.ImageProcessing.NormalizationMaxValue;
                    tensor[0, y, x, 1] = pixel[1] / Constants.ImageProcessing.NormalizationMaxValue;
                    tensor[0, y, x, 2] = pixel[2] / Constants.ImageProcessing.NormalizationMaxValue;
                }
            }
        }
        else
        {
            // NCHW形式 (batch, channels, height, width) - PyTorch形式
            tensor = new DenseTensor<float>([batchSize, channels, height, width]);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var pixel = rgb.At<Vec3b>(y, x);

                    tensor[0, 0, y, x] = pixel[0] / Constants.ImageProcessing.NormalizationMaxValue;
                    tensor[0, 1, y, x] = pixel[1] / Constants.ImageProcessing.NormalizationMaxValue;
                    tensor[0, 2, y, x] = pixel[2] / Constants.ImageProcessing.NormalizationMaxValue;
                }
            }
        }

        return tensor;
    }

    private static (int channels, int height, int width) GetImageDimensions(int[] inputShape)
    {
        // PyTorchモデル等で一般的なNCHW形式 (batch, channels, height, width)
        if (inputShape.Length == 4 && (inputShape[1] == 3 || inputShape[1] == 1))
        {
            return (inputShape[1], inputShape[2], inputShape[3]);
        }
        // TensorFlowモデル等で一般的なNHWC形式 (batch, height, width, channels)
        else if (inputShape.Length == 4 && (inputShape[3] == 3 || inputShape[3] == 1))
        {
            return (inputShape[3], inputShape[1], inputShape[2]);
        }
        else
        {
            // Dynamic shapes or unsupported format - return invalid dimensions
            // This will be handled by the calling function with default values
            return (0, 0, 0);
        }
    }

    private static InferenceResult ProcessResults(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, OpenCvSharp.Size imageSize)
    {
        InferenceResult inferenceResult = new(imageSize);

        foreach (var result in results)
        {
            var outputName = result.Name;

            // Handle different output data types
            if (result.ElementType == TensorElementType.Float)
            {
                var outputTensor = result.AsEnumerable<float>().ToArray();
                var tensorShape = result.AsTensor<float>().Dimensions.ToArray();
                inferenceResult.Outputs.Add(outputName, new OutputData(outputName, outputTensor, tensorShape));
            }
            else if (result.ElementType == TensorElementType.Int32)
            {
                // Convert int32 to float for consistent processing
                var outputTensor = result.AsEnumerable<int>().Select(x => (float)x).ToArray();
                var tensorShape = result.AsTensor<int>().Dimensions.ToArray();
                inferenceResult.Outputs.Add(outputName, new OutputData(outputName, outputTensor, tensorShape));
            }
            else
            {
                // Try to handle as float by default
                var outputTensor = result.AsEnumerable<float>().ToArray();
                var tensorShape = result.AsTensor<float>().Dimensions.ToArray();
                inferenceResult.Outputs.Add(outputName, new OutputData(outputName, outputTensor, tensorShape));
            }
        }

        return inferenceResult;
    }

    public static List<Detection> ApplyNMS(List<Detection> detections, float nmsThreshold = Constants.Thresholds.DefaultNmsThreshold)
    {
        if (detections.Count == 0)
        {
            return detections;
        }

        detections.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));

        List<Detection> selected = [];
        var active = new bool[detections.Count];
        Array.Fill(active, true);

        for (int i = 0; i < detections.Count; i++)
        {
            if (!active[i])
            {
                continue;
            }

            selected.Add(detections[i]);

            for (int j = i + 1; j < detections.Count; j++)
            {
                if (!active[j] || detections[i].ClassId != detections[j].ClassId)
                {
                    continue;
                }

                var iou = CalculateIoU(detections[i].BBox, detections[j].BBox);
                if (iou > nmsThreshold)
                {
                    active[j] = false;
                }
            }
        }

        return selected;
    }

    private static DenseTensor<float> CreateImageShapeTensor(OpenCvSharp.Size imageSize)
    {
        DenseTensor<float> tensor = new([1, 2]);
        tensor[0, 0] = imageSize.Height;
        tensor[0, 1] = imageSize.Width;
        return tensor;
    }

    private static float CalculateIoU(RectangleF box1, RectangleF box2)
    {
        var x1 = Math.Max(box1.Left, box2.Left);
        var y1 = Math.Max(box1.Top, box2.Top);
        var x2 = Math.Min(box1.Right, box2.Right);
        var y2 = Math.Min(box1.Bottom, box2.Bottom);

        var intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        var area1 = box1.Width * box1.Height;
        var area2 = box2.Width * box2.Height;
        var union = area1 + area2 - intersection;

        return union > 0 ? intersection / union : 0;
    }
}

public record InferenceResult(OpenCvSharp.Size ImageSize)
{
    public Dictionary<string, OutputData> Outputs { get; init; } = [];
}

public record OutputData(string Name, float[] Data, int[] Shape);

public record Detection
{
    public required int ClassId { get; init; }
    public required string ClassName { get; init; }
    public required float Confidence { get; init; }
    public required RectangleF BBox { get; init; }
    public float[]? Embedding { get; init; }
}
