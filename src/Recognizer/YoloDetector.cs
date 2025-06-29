using Microsoft.ML.OnnxRuntime;
using System.Drawing;

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
        var detections = new List<Detection>();

        // YOLOv8/v11の特殊な出力形式に対応
        // 84 = 4 (x,y,w,h) + 80 classes, 8400 = 検出候補数
        var output = result.Outputs.First().Value;
        var predictions = output.Data;
        var shape = output.Shape;

        int numValues = shape[1]; // 84
        int numPredictions = shape[2]; // 8400
        int numClasses = numValues - Constants.YoloOutput.BoundingBoxDimensions; // 80 classes

        var imageWidth = result.ImageSize.Width;
        var imageHeight = result.ImageSize.Height;

        // YOLOモデルの標準入力サイズに正規化が必要
        var modelWidth = (float)Constants.ImageProcessing.YoloInputWidth;
        var modelHeight = (float)Constants.ImageProcessing.YoloInputHeight;

        // スケーリング係数
        var scaleX = imageWidth / modelWidth;
        var scaleY = imageHeight / modelHeight;

        for (int i = 0; i < numPredictions; i++)
        {
            var (maxClassScore, maxClassId) = GetMaxClassScore(predictions, numPredictions, numClasses, i);

            if (maxClassScore < _confidenceThreshold) continue;

            var boundingBox = CalculateBoundingBox(predictions, numPredictions, i, scaleX, scaleY, imageWidth, imageHeight);

            detections.Add(new Detection
            {
                ClassId = maxClassId,
                ClassName = GetClassName(maxClassId),
                Confidence = maxClassScore,
                BBox = boundingBox
            });
        }

        // 重複する検出結果を除去
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
        // バウンディングボックスの座標を取得（モデル座標系）
        var cx = predictions[0 * numPredictions + index] * scaleX;
        var cy = predictions[1 * numPredictions + index] * scaleY;
        var w = predictions[2 * numPredictions + index] * scaleX;
        var h = predictions[3 * numPredictions + index] * scaleY;

        // バウンディングボックスの座標を計算
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

