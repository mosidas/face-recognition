using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using System.Drawing;
using System.Numerics;

namespace Recognizer;

public class FaceRecognizer : IDisposable
{
    private readonly InferenceSession _detectorSession;
    private readonly InferenceSession _recognizerSession;
    private readonly float _detectionThreshold;
    private readonly float _recognitionThreshold;

    public FaceRecognizer(
        string detectorModelPath,
        string recognizerModelPath,
        float detectionThreshold = Constants.Thresholds.DefaultFaceDetectionThreshold,
        float recognitionThreshold = Constants.Thresholds.DefaultFaceRecognitionThreshold)
    {
        _detectorSession = OnnxHelper.LoadModel(detectorModelPath);
        _recognizerSession = OnnxHelper.LoadModel(recognizerModelPath);
        _detectionThreshold = detectionThreshold;
        _recognitionThreshold = recognitionThreshold;
    }

    public async Task<List<FaceDetection>> DetectFacesAsync(string imagePath)
    {
        var result = await OnnxHelper.Run(_detectorSession, imagePath);
        return ParseFaceDetectorOutput(result);
    }

    public async Task<float[]> ExtractFaceEmbeddingAsync(string imagePath, Rectangle faceRegion)
    {
        // セキュリティ上の理由で一時ファイル経由で処理
        using var image = Cv2.ImRead(imagePath);
        using var face = ExtractFaceRegion(image, faceRegion);
        using var tempFile = new TempImageFile();
        Cv2.ImWrite(tempFile.Path, face);

        var result = await OnnxHelper.Run(_recognizerSession, tempFile.Path);
        return ExtractEmbedding(result);
    }

    public static float CompareFaces(float[] embedding1, float[] embedding2)
    {
        if (embedding1.Length != embedding2.Length)
            throw new ArgumentException("Embeddings must have the same dimension");

        // コサイン類似度の計算
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

        if (norm1 == 0 || norm2 == 0)
            return 0;

        return dotProduct / (norm1 * norm2);
    }

    public async Task<FaceVerificationResult> VerifyFaceAsync(
        string imagePath1,
        string imagePath2)
    {
        // 両方の画像から顔を検出
        var faces1 = await DetectFacesAsync(imagePath1);
        var faces2 = await DetectFacesAsync(imagePath2);

        if (faces1.Count == 0 || faces2.Count == 0)
        {
            return new FaceVerificationResult
            {
                IsMatch = false,
                Confidence = 0,
                Message = "顔が検出されませんでした"
            };
        }

        // 複数顔がある場合は最も確実な顔で比較
        var face1 = faces1.OrderByDescending(f => f.Confidence).First();
        var face2 = faces2.OrderByDescending(f => f.Confidence).First();

        // TODO: ログレベルに応じた出力制御が必要

        // 特徴ベクトルの抽出
        var embedding1 = await ExtractFaceEmbeddingAsync(imagePath1, face1.BBox);
        var embedding2 = await ExtractFaceEmbeddingAsync(imagePath2, face2.BBox);

        // 類似度の計算
        var similarity = CompareFaces(embedding1, embedding2);

        // 顔認証の閾値判定
        var isMatch = similarity >= _recognitionThreshold;

        return new FaceVerificationResult
        {
            IsMatch = isMatch,
            Confidence = similarity,
            Message = isMatch ? "同一人物です" : "別人です"
        };
    }

    private List<FaceDetection> ParseFaceDetectorOutput(InferenceResult result)
    {
        var detections = new List<FaceDetection>();
        var output = result.Outputs.First().Value;
        var predictions = output.Data;
        var shape = output.Shape;

        // YOLOv11-face特有の出力形式対応
        _ = shape[1]; // 5
        int numPredictions = shape[2]; // 8400

        var imageWidth = result.ImageSize.Width;
        var imageHeight = result.ImageSize.Height;

        // モデルの入力サイズ（通常640x640）
        var modelWidth = (float)Constants.ImageProcessing.YoloInputWidth;
        var modelHeight = (float)Constants.ImageProcessing.YoloInputHeight;

        // スケーリング係数
        var scaleX = imageWidth / modelWidth;
        var scaleY = imageHeight / modelHeight;

        for (int i = 0; i < numPredictions; i++)
        {
            // 信頼度を取得
            var confidence = predictions[4 * numPredictions + i]; // 5番目の要素
            if (confidence < _detectionThreshold) continue;

            // バウンディングボックスの座標を取得（モデル座標系）
            var cx = predictions[0 * numPredictions + i];  // center x
            var cy = predictions[1 * numPredictions + i];  // center y
            var w = predictions[2 * numPredictions + i];   // width
            var h = predictions[3 * numPredictions + i];   // height

            // 座標を元画像のサイズにスケーリング
            cx *= scaleX;
            cy *= scaleY;
            w *= scaleX;
            h *= scaleY;

            // バウンディングボックスの座標を計算
            var x1 = Math.Max(0, cx - w / 2);
            var y1 = Math.Max(0, cy - h / 2);
            var x2 = Math.Min(imageWidth, cx + w / 2);
            var y2 = Math.Min(imageHeight, cy + h / 2);

            // 有効な領域かチェック
            if (x2 <= x1 || y2 <= y1) continue;

            var bbox = Rectangle.FromLTRB((int)x1, (int)y1, (int)x2, (int)y2);

            detections.Add(new FaceDetection
            {
                BBox = bbox,
                Confidence = confidence,
                Landmarks = [] // ランドマークは使用しない
            });
        }

        // NMSを適用して重複する顔検出を除去
        var filteredDetections = new List<Detection>();
        foreach (var face in detections)
        {
            filteredDetections.Add(new Detection
            {
                ClassId = 0, // 顔クラス
                ClassName = "face",
                Confidence = face.Confidence,
                BBox = new RectangleF(face.BBox.X, face.BBox.Y, face.BBox.Width, face.BBox.Height)
            });
        }

        var nmsResults = OnnxHelper.ApplyNMS(filteredDetections, Constants.Thresholds.DefaultNmsThreshold);

        var finalDetections = new List<FaceDetection>();
        foreach (var nmsResult in nmsResults)
        {
            var bbox = Rectangle.FromLTRB((int)nmsResult.BBox.Left, (int)nmsResult.BBox.Top, (int)nmsResult.BBox.Right, (int)nmsResult.BBox.Bottom);
            // NOTE: 本来はログシステムで出力制御すべき

            finalDetections.Add(new FaceDetection
            {
                BBox = bbox,
                Confidence = nmsResult.Confidence,
                Landmarks = []
            });
        }

        return finalDetections;
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

        // 範囲外の場合は調整
        if (x + actualSize > image.Width) x = image.Width - actualSize;
        if (y + actualSize > image.Height) y = image.Height - actualSize;

        // FIXME: デバッグ出力の削除が必要

        var roi = new Rect(x, y, actualSize, actualSize);
        using var face = new Mat(image, roi);

        // 顔認証モデルの標準入力サイズに合わせる
        var resizedFace = new Mat();
        Cv2.Resize(face, resizedFace, new OpenCvSharp.Size(Constants.ImageProcessing.FaceRecognitionInputSize, Constants.ImageProcessing.FaceRecognitionInputSize));

        return resizedFace;
    }

    private static float[] ExtractEmbedding(InferenceResult result)
    {
        var output = result.Outputs.First().Value;
        var embedding = output.Data.ToArray(); // コピーを作成

        // 元のノルムを計算
        var norm = 0.0f;
        for (int i = 0; i < embedding.Length; i++)
        {
            norm += embedding[i] * embedding[i];
        }
        norm = MathF.Sqrt(norm);

        // NOTE: 正規化の有無は顔认识精度に大きく影響するため検証中
        return embedding;
    }

    public void Dispose()
    {
        _detectorSession?.Dispose();
        _recognizerSession?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class FaceDetection
{
    public Rectangle BBox { get; set; }
    public float Confidence { get; set; }
    public PointF[] Landmarks { get; set; } = [];
}

public class FaceVerificationResult
{
    public bool IsMatch { get; set; }
    public float Confidence { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class FaceDatabase
{
    private readonly Dictionary<string, FaceEntry> _database = [];

    public void RegisterFace(string personId, string name, float[] embedding)
    {
        _database[personId] = new FaceEntry
        {
            PersonId = personId,
            Name = name,
            Embedding = embedding,
            RegisteredAt = DateTime.Now
        };
    }

    public FaceIdentificationResult IdentifyFace(float[] queryEmbedding, float threshold = 0.6f)
    {
        var bestMatch = new FaceIdentificationResult
        {
            PersonId = null,
            Name = "Unknown",
            Confidence = 0
        };

        foreach (var entry in _database.Values)
        {
            var similarity = FaceRecognizer.CompareFaces(queryEmbedding, entry.Embedding);
            if (similarity > bestMatch.Confidence && similarity >= threshold)
            {
                bestMatch.PersonId = entry.PersonId;
                bestMatch.Name = entry.Name;
                bestMatch.Confidence = similarity;
            }
        }

        return bestMatch;
    }

}

public class FaceEntry
{
    public string PersonId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = [];
    public DateTime RegisteredAt { get; set; }
}

public class FaceIdentificationResult
{
    public string? PersonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public float Confidence { get; set; }
}

