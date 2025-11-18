using System.Drawing;
using Microsoft.ML.OnnxRuntime;

namespace Recognizer;

public sealed class YoloDetector(
    string modelPath,
    string[] classNames,
    float confidenceThreshold = Constants.Thresholds.DefaultObjectDetectionThreshold,
    float nmsThreshold = Constants.Thresholds.DefaultNmsThreshold) : IDisposable
{
    private readonly InferenceSession _session = OnnxHelper.LoadModel(modelPath);
    private readonly string[] _classNames = classNames;
    private readonly float _confidenceThreshold = confidenceThreshold;
    private readonly float _nmsThreshold = nmsThreshold;

    public async Task<List<Detection>> DetectAsync(OpenCvSharp.Mat inputImage, CancellationToken cancellationToken = default)
    {
        var result = await OnnxHelper.Run(_session, inputImage, cancellationToken).ConfigureAwait(false);
        return ParseYoloOutput(result);
    }

    public async Task<List<Detection>> DetectAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var result = await OnnxHelper.Run(_session, imagePath, cancellationToken).ConfigureAwait(false);
        return ParseYoloOutput(result);
    }

    private List<Detection> ParseYoloOutput(InferenceResult result)
    {
        // Check if this is YOLOv3 format (3 outputs) or YOLOv8/v11 format (1 output)
        if (result.Outputs.Count == 3)
        {
            // YOLOv3 format: boxes, scores, indices
            return ParseYoloV3Output(result);
        }
        else
        {
            // YOLOv8/v11 format: single output tensor
            return ParseYoloV8Output(result);
        }
    }

    private List<Detection> ParseYoloV3Output(InferenceResult result)
    {
        List<Detection> detections = [];

        // YOLOv3 outputs: boxes [1, N, 4], scores [1, 80, N], indices [N, 3]
        var boxesOutput = result.Outputs["yolonms_layer_1/ExpandDims_1:0"];
        var scoresOutput = result.Outputs["yolonms_layer_1/ExpandDims_3:0"];
        var indicesOutput = result.Outputs["yolonms_layer_1/concat_2:0"];

        var boxes = boxesOutput.Data;
        var scores = scoresOutput.Data;
        var indices = indicesOutput.Data;
        _ = boxesOutput.Shape;
        var scoresShape = scoresOutput.Shape;
        var indicesShape = indicesOutput.Shape;
        _ = result.ImageSize.Width;
        _ = result.ImageSize.Height;

        var numDetections = indicesShape[0];

        for (int i = 0; i < numDetections; i++)
        {
            var batchIdx = (int)indices[i * 3];
            var classIdx = (int)indices[i * 3 + 1];
            var boxIdx = (int)indices[i * 3 + 2];

            if (batchIdx != 0 || classIdx < 0)
            {
                continue; // Only process first batch and valid classes
            }

            // Calculate score index: scores shape is [1, 80, N]
            var scoreIdx = classIdx * scoresShape[2] + boxIdx;
            if (scoreIdx >= scores.Length)
            {
                continue;
            }

            var confidence = scores[scoreIdx];
            if (confidence < _confidenceThreshold)
            {
                continue;
            }

            // Calculate box indices: boxes shape is [1, N, 4]
            var boxBaseIdx = boxIdx * 4;
            if (boxBaseIdx + 3 >= boxes.Length)
            {
                continue;
            }

            // Extract bounding box coordinates (already in image coordinates)
            var y1 = boxes[boxBaseIdx + 0];
            var x1 = boxes[boxBaseIdx + 1];
            var y2 = boxes[boxBaseIdx + 2];
            var x2 = boxes[boxBaseIdx + 3];

            // Ensure valid bounding box
            if (x2 <= x1 || y2 <= y1)
            {
                continue;
            }

            RectangleF boundingBox = new(x1, y1, x2 - x1, y2 - y1);

            detections.Add(new Detection
            {
                ClassId = classIdx,
                ClassName = GetClassName(classIdx),
                Confidence = confidence,
                BBox = boundingBox
            });
        }

        return detections;
    }

    private List<Detection> ParseYoloV8Output(InferenceResult result)
    {
        List<Detection> detections = [];

        // YOLOv8/v11の特殊な出力形式に対応
        var output = result.Outputs.First().Value;
        var predictions = output.Data;
        var shape = output.Shape;

        int numValues = shape[1];
        int numPredictions = shape[2];
        int numClasses = numValues - Constants.YoloOutput.BoundingBoxDimensions;

        var imageWidth = result.ImageSize.Width;
        var imageHeight = result.ImageSize.Height;

        // YOLOモデルの標準入力サイズに正規化が必要
        var modelWidth = (float)Constants.ImageProcessing.YoloInputWidth;
        var modelHeight = (float)Constants.ImageProcessing.YoloInputHeight;

        var scaleX = imageWidth / modelWidth;
        var scaleY = imageHeight / modelHeight;

        for (int i = 0; i < numPredictions; i++)
        {
            var (maxClassScore, maxClassId) = GetMaxClassScore(predictions, numPredictions, numClasses, i);

            if (maxClassScore < _confidenceThreshold)
            {
                continue;
            }

            var boundingBox = CalculateBoundingBox(predictions, numPredictions, i, scaleX, scaleY, imageWidth, imageHeight);

            detections.Add(new Detection
            {
                ClassId = maxClassId,
                ClassName = GetClassName(maxClassId),
                Confidence = maxClassScore,
                BBox = boundingBox
            });
        }

        return OnnxHelper.ApplyNMS(detections, _nmsThreshold);
    }

    private (float maxScore, int maxId) GetMaxClassScore(float[] predictions, int numPredictions, int numClasses, int predictionIndex)
    {
        var maxClassScore = 0.0f;
        var maxClassId = 0;

        for (int j = 0; j < numClasses; j++)
        {
            var classScore = predictions[(j + 4) * numPredictions + predictionIndex];
            if (classScore > maxClassScore)
            {
                maxClassScore = classScore;
                maxClassId = j;
            }
        }

        return (maxClassScore, maxClassId);
    }

    private static RectangleF CalculateBoundingBox(float[] predictions, int numPredictions, int index, float scaleX, float scaleY, int imageWidth, int imageHeight)
    {
        var cx = predictions[0 * numPredictions + index] * scaleX;
        var cy = predictions[1 * numPredictions + index] * scaleY;
        var w = predictions[2 * numPredictions + index] * scaleX;
        var h = predictions[3 * numPredictions + index] * scaleY;

        var x1 = Math.Max(0, cx - w / 2);
        var y1 = Math.Max(0, cy - h / 2);
        var x2 = Math.Min(imageWidth, cx + w / 2);
        var y2 = Math.Min(imageHeight, cy + h / 2);

        return new RectangleF(x1, y1, x2 - x1, y2 - y1);
    }

    private string GetClassName(int classId) =>
        classId < _classNames.Length ? _classNames[classId] : $"Class_{classId}";

    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public static class CocoClassNames
{
    public static readonly string[] Names =
    [
        "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat", "traffic light",
        "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat", "dog", "horse", "sheep", "cow",
        "elephant", "bear", "zebra", "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase", "frisbee",
        "skis", "snowboard", "sports ball", "kite", "baseball bat", "baseball glove", "skateboard", "surfboard",
        "tennis racket", "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
        "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair", "couch",
        "potted plant", "bed", "dining table", "toilet", "tv", "laptop", "mouse", "remote", "keyboard",
        "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock", "vase",
        "scissors", "teddy bear", "hair drier", "toothbrush"
    ];
}

