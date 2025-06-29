using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using System.Drawing;

namespace Recognizer;

public sealed class FaceRecognizer(
    string detectorModelPath,
    string recognizerModelPath,
    float detectionThreshold = Constants.Thresholds.DefaultFaceDetectionThreshold,
    float recognitionThreshold = Constants.Thresholds.DefaultFaceRecognitionThreshold) : IDisposable
{
    private readonly InferenceSession _detectorSession = OnnxHelper.LoadModel(detectorModelPath);
    private readonly InferenceSession _recognizerSession = OnnxHelper.LoadModel(recognizerModelPath);
    private readonly float _detectionThreshold = detectionThreshold;
    private readonly float _recognitionThreshold = recognitionThreshold;

    public async Task<List<FaceDetection>> DetectFacesAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var result = await OnnxHelper.Run(_detectorSession, imagePath, cancellationToken).ConfigureAwait(false);
        return ParseFaceDetectorOutput(result);
    }

    public async Task<List<FaceDetection>> DetectFacesAsync(Mat inputImage, CancellationToken cancellationToken = default)
    {
        var result = await OnnxHelper.Run(_detectorSession, inputImage, cancellationToken).ConfigureAwait(false);
        return ParseFaceDetectorOutput(result);
    }

    public async Task<float[]> ExtractFaceEmbeddingAsync(string imagePath, Rectangle faceRegion, CancellationToken cancellationToken = default)
    {
        using var image = Cv2.ImRead(imagePath);
        return await ExtractFaceEmbeddingAsync(image, faceRegion, cancellationToken).ConfigureAwait(false);
    }

    public async Task<float[]> ExtractFaceEmbeddingAsync(Mat inputImage, Rectangle faceRegion, CancellationToken cancellationToken = default)
    {
        using var face = ExtractFaceRegion(inputImage, faceRegion);
        var result = await OnnxHelper.Run(_recognizerSession, face, cancellationToken).ConfigureAwait(false);
        return ExtractEmbedding(result);
    }

    public static float CompareFaces(float[] embedding1, float[] embedding2)
    {
        if (embedding1.Length != embedding2.Length)
            throw new ArgumentException("Embeddings must have the same dimension");

        var dotProduct = 0.0f;
        var norm1 = 0.0f;
        var norm2 = 0.0f;

        for (int i = 0; i < embedding1.Length; i++)
        {
            dotProduct += embedding1[i] * embedding2[i];
            norm1 += embedding1[i] * embedding1[i];
            norm2 += embedding2[i] * embedding2[i];
        }

        norm1 = MathF.Sqrt(norm1);
        norm2 = MathF.Sqrt(norm2);

        return norm1 == 0 || norm2 == 0 ? 0 : dotProduct / (norm1 * norm2);
    }

    public async Task<FaceVerificationResult> VerifyFaceAsync(
        Mat inputImage1,
        Mat inputImage2,
        CancellationToken cancellationToken = default)
    {
        var faces1 = await DetectFacesAsync(inputImage1, cancellationToken).ConfigureAwait(false);
        var faces2 = await DetectFacesAsync(inputImage2, cancellationToken).ConfigureAwait(false);

        if (faces1.Count == 0 || faces2.Count == 0)
        {
            return new FaceVerificationResult(false, 0, "顔が検出されませんでした");
        }

        var face1 = faces1.MaxBy(f => f.Confidence)!;
        var face2 = faces2.MaxBy(f => f.Confidence)!;

        var embedding1 = await ExtractFaceEmbeddingAsync(inputImage1, face1.BBox, cancellationToken).ConfigureAwait(false);
        var embedding2 = await ExtractFaceEmbeddingAsync(inputImage2, face2.BBox, cancellationToken).ConfigureAwait(false);

        var similarity = CompareFaces(embedding1, embedding2);
        var isMatch = similarity >= _recognitionThreshold;

        return new FaceVerificationResult(
            isMatch,
            similarity,
            isMatch ? "同一人物です" : "別人です");
    }

    private List<FaceDetection> ParseFaceDetectorOutput(InferenceResult result)
    {
        var detections = new List<FaceDetection>();
        var output = result.Outputs.First().Value;
        var predictions = output.Data;
        var shape = output.Shape;

        // YOLOv11-faceは転置出力のため従来のYOLOと処理が異なる
        _ = shape[1];
        int numPredictions = shape[2];

        var imageWidth = result.ImageSize.Width;
        var imageHeight = result.ImageSize.Height;

        var modelWidth = (float)Constants.ImageProcessing.YoloInputWidth;
        var modelHeight = (float)Constants.ImageProcessing.YoloInputHeight;

        var scaleX = imageWidth / modelWidth;
        var scaleY = imageHeight / modelHeight;

        for (int i = 0; i < numPredictions; i++)
        {
            var confidence = predictions[4 * numPredictions + i];
            if (confidence < _detectionThreshold) continue;

            var boundingBox = CalculateFaceBoundingBox(predictions, numPredictions, i, scaleX, scaleY, imageWidth, imageHeight);

            if (boundingBox.Width <= 0 || boundingBox.Height <= 0) continue;

            detections.Add(new FaceDetection(boundingBox, confidence));
        }

        return ApplyNMSToFaces(detections);
    }

    private static Rectangle CalculateFaceBoundingBox(float[] predictions, int numPredictions, int index, float scaleX, float scaleY, int imageWidth, int imageHeight)
    {
        var cx = predictions[0 * numPredictions + index] * scaleX;
        var cy = predictions[1 * numPredictions + index] * scaleY;
        var w = predictions[2 * numPredictions + index] * scaleX;
        var h = predictions[3 * numPredictions + index] * scaleY;

        var x1 = (int)Math.Max(0, cx - w / 2);
        var y1 = (int)Math.Max(0, cy - h / 2);
        var x2 = (int)Math.Min(imageWidth, cx + w / 2);
        var y2 = (int)Math.Min(imageHeight, cy + h / 2);

        return Rectangle.FromLTRB(x1, y1, x2, y2);
    }

    private static List<FaceDetection> ApplyNMSToFaces(List<FaceDetection> detections)
    {
        var filteredDetections = detections
            .Select(face => new Detection
            {
                ClassId = 0,
                ClassName = "face",
                Confidence = face.Confidence,
                BBox = new RectangleF(face.BBox.X, face.BBox.Y, face.BBox.Width, face.BBox.Height)
            })
            .ToList();

        var nmsResults = OnnxHelper.ApplyNMS(filteredDetections, Constants.Thresholds.DefaultNmsThreshold);

        return [.. nmsResults
            .Select(nmsResult =>
            {
                var bbox = Rectangle.FromLTRB(
                    (int)nmsResult.BBox.Left,
                    (int)nmsResult.BBox.Top,
                    (int)nmsResult.BBox.Right,
                    (int)nmsResult.BBox.Bottom);

                return new FaceDetection(bbox, nmsResult.Confidence);
            })];
    }

    private static Mat ExtractFaceRegion(Mat image, Rectangle faceRegion)
    {
        // 顔認証精度向上のため周辺情報を含む正方形で切り出し
        var padding = (int)(Math.Max(faceRegion.Width, faceRegion.Height) * Constants.ImageProcessing.FacePaddingRatio);
        var centerX = faceRegion.X + faceRegion.Width / 2;
        var centerY = faceRegion.Y + faceRegion.Height / 2;
        var size = Math.Max(faceRegion.Width, faceRegion.Height) + padding * 2;

        var x = Math.Max(0, centerX - size / 2);
        var y = Math.Max(0, centerY - size / 2);
        var actualSize = Math.Min(size, Math.Min(image.Width - x, image.Height - y));

        if (x + actualSize > image.Width) x = image.Width - actualSize;
        if (y + actualSize > image.Height) y = image.Height - actualSize;

        var roi = new Rect(x, y, actualSize, actualSize);
        using var face = new Mat(image, roi);

        var resizedFace = new Mat();
        Cv2.Resize(face, resizedFace, new OpenCvSharp.Size(Constants.ImageProcessing.FaceRecognitionInputSize, Constants.ImageProcessing.FaceRecognitionInputSize));

        return resizedFace;
    }

    private static float[] ExtractEmbedding(InferenceResult result)
    {
        var output = result.Outputs.First().Value;
        return output.Data.ToArray();
    }

    public void Dispose()
    {
        _detectorSession?.Dispose();
        _recognizerSession?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public record FaceDetection(Rectangle BBox, float Confidence)
{
    public PointF[] Landmarks { get; init; } = [];
}

public record FaceVerificationResult(bool IsMatch, float Confidence, string Message);

public sealed class FaceDatabase
{
    private readonly Dictionary<string, FaceEntry> _database = [];

    public void RegisterFace(string personId, string name, float[] embedding)
    {
        _database[personId] = new FaceEntry(personId, name, embedding, DateTime.Now);
    }

    public FaceIdentificationResult IdentifyFace(float[] queryEmbedding, float threshold = 0.6f)
    {
        var bestMatch = new FaceIdentificationResult(null, "Unknown", 0);

        foreach (var entry in _database.Values)
        {
            var similarity = FaceRecognizer.CompareFaces(queryEmbedding, entry.Embedding);
            if (similarity > bestMatch.Confidence && similarity >= threshold)
            {
                bestMatch = bestMatch with { PersonId = entry.PersonId, Name = entry.Name, Confidence = similarity };
            }
        }

        return bestMatch;
    }
}

public record FaceEntry(string PersonId, string Name, float[] Embedding, DateTime RegisteredAt);

public record FaceIdentificationResult(string? PersonId, string Name, float Confidence);

