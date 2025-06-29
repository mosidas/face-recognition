using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.Drawing;

namespace Recognizer;

public class OnnxHelper
{
    public static InferenceSession LoadModel(string modelPath)
    {
        // SessionOptionsの設定
        var sessionOptions = new SessionOptions
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
            // フォールバック処理
        }

        // TODO: デバッグ情報の出力制御機能が必要
        var session = new InferenceSession(modelPath, sessionOptions);

        return session;
    }

    public static async Task<InferenceResult> Run(InferenceSession session, Mat inputImage)
    {
        return await Task.Run(() =>
        {
            // 入力メタデータの取得
            var inputMeta = session.InputMetadata.First();
            var inputName = inputMeta.Key;
            var inputShape = inputMeta.Value.Dimensions;

            // 画像の前処理
            var inputTensor = PreprocessImage(inputImage, inputShape);

            // 推論の実行
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            using var results = session.Run(inputs);

            // 結果の処理
            return ProcessResults(results, inputImage.Size());
        });
    }

    public static async Task<InferenceResult> Run(InferenceSession session, string inputPath)
    {
        return await Task.Run(() =>
        {
            using var image = Cv2.ImRead(inputPath);
            if (image.Empty())
            {
                throw new ArgumentException($"Failed to load image: {inputPath}");
            }

            return Run(session, image);
        });
    }

    private static DenseTensor<float> PreprocessImage(Mat image, int[] inputShape)
    {
        var batchSize = inputShape[0] == -1 ? 1 : inputShape[0];
        var (channels, height, width) = GetImageDimensions(inputShape);

        using var resized = new Mat();
        Cv2.Resize(image, resized, new OpenCvSharp.Size(width, height));

        // OpenCVのBGR形式をRGBに変換（ONNXモデルの標準）
        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);
        var tensor = new DenseTensor<float>(new[] { batchSize, channels, height, width });

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = rgb.At<Vec3b>(y, x);

                // 0-255を0-1に正規化（すべてのモデルで統一）
                tensor[0, 0, y, x] = pixel[0] / Constants.ImageProcessing.NormalizationMaxValue;
                tensor[0, 1, y, x] = pixel[1] / Constants.ImageProcessing.NormalizationMaxValue;
                tensor[0, 2, y, x] = pixel[2] / Constants.ImageProcessing.NormalizationMaxValue;
            }
        }



        return tensor;
    }

    private static (int channels, int height, int width) GetImageDimensions(int[] inputShape)
    {
        // NCHW形式 (batch, channels, height, width)
        if (inputShape.Length == 4 && (inputShape[1] == 3 || inputShape[1] == 1))
        {
            return (inputShape[1], inputShape[2], inputShape[3]);
        }
        // NHWC形式 (batch, height, width, channels)
        else if (inputShape.Length == 4 && (inputShape[3] == 3 || inputShape[3] == 1))
        {
            return (inputShape[3], inputShape[1], inputShape[2]);
        }
        else
        {
            throw new NotSupportedException($"Unsupported input shape: [{string.Join(", ", inputShape)}]");
        }
    }

    private static InferenceResult ProcessResults(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, OpenCvSharp.Size imageSize)
    {
        var inferenceResult = new InferenceResult
        {
            ImageSize = imageSize
        };

        foreach (var result in results)
        {
            var outputName = result.Name;
            var outputTensor = result.AsEnumerable<float>().ToArray();

            var tensorShape = result.AsTensor<float>().Dimensions.ToArray();

            inferenceResult.Outputs.Add(outputName, new OutputData
            {
                Name = outputName,
                Data = outputTensor,
                Shape = tensorShape
            });
        }

        return inferenceResult;
    }

    public static List<Detection> ApplyNMS(List<Detection> detections, float nmsThreshold = Constants.Thresholds.DefaultNmsThreshold)
    {
        if (detections.Count == 0) return detections;

        detections.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));

        var selected = new List<Detection>();
        var active = new bool[detections.Count];
        for (int i = 0; i < active.Length; i++) active[i] = true;

        for (int i = 0; i < detections.Count; i++)
        {
            if (!active[i]) continue;

            selected.Add(detections[i]);

            for (int j = i + 1; j < detections.Count; j++)
            {
                if (!active[j]) continue;
                if (detections[i].ClassId != detections[j].ClassId) continue;

                var iou = CalculateIoU(detections[i].BBox, detections[j].BBox);
                if (iou > nmsThreshold)
                {
                    active[j] = false;
                }
            }
        }

        return selected;
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

public class InferenceResult
{
    public OpenCvSharp.Size ImageSize { get; set; }
    public Dictionary<string, OutputData> Outputs { get; set; } = new Dictionary<string, OutputData>();
}

public class OutputData
{
    public string Name { get; set; } = string.Empty;
    public float[] Data { get; set; } = [];
    public int[] Shape { get; set; } = [];
}

public class Detection
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public RectangleF BBox { get; set; }
    public float[]? Embedding { get; set; } // 顔認証用の特徴ベクトル
}
