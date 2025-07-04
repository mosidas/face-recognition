using OpenCvSharp;
using Recognizer;
using System.Diagnostics;
using System.Drawing;

namespace UnifiedDetector;

/// <summary>
/// 物体検出と顔認証を同時に実行する統合検出器
/// </summary>
public sealed class UnifiedDetectorMain : IDisposable
{
    private readonly YoloDetector? _objectDetector;
    private readonly FaceRecognizer _faceRecognizer;
    private readonly FaceDatabase _faceDatabase;
    private readonly bool _enableObjectDetection;
    private readonly bool _enableFaceRecognition;
    private readonly string _windowName;
    private bool _disposed = false;

    public UnifiedDetectorMain(
        YoloDetector? objectDetector,
        FaceRecognizer faceRecognizer,
        string windowName = "Unified Detection & Recognition",
        bool enableObjectDetection = true,
        bool enableFaceRecognition = true)
    {
        _objectDetector = objectDetector;
        _faceRecognizer = faceRecognizer ?? throw new ArgumentNullException(nameof(faceRecognizer));
        _faceDatabase = new FaceDatabase();
        _windowName = windowName;
        _enableObjectDetection = enableObjectDetection && objectDetector != null;
        _enableFaceRecognition = enableFaceRecognition;
    }

    /// <summary>
    /// 顔データベースに参照顔を登録
    /// </summary>
    public async Task LoadReferenceFacesAsync(string faceImagesPath)
    {
        if (!_enableFaceRecognition) return;

        var totalStopwatch = Stopwatch.StartNew();

        if (!Directory.Exists(faceImagesPath))
        {
            throw new DirectoryNotFoundException($"顔画像フォルダが見つかりません: {faceImagesPath}");
        }

        var imageFiles = Directory.GetFiles(faceImagesPath, "*.*")
            .Where(f => new[] { ".jpg", ".jpeg", ".png", ".bmp" }.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        if (imageFiles.Count == 0)
        {
            Console.WriteLine($"警告: 顔画像フォルダに画像ファイルが見つかりません: {faceImagesPath}");
            return;
        }

        Console.WriteLine($"[参照顔読み込み] {imageFiles.Count} 個の画像ファイルを処理します。");

        foreach (var imagePath in imageFiles)
        {
            try
            {
                var personName = Path.GetFileNameWithoutExtension(imagePath);
                Console.WriteLine($"[参照画像] 処理中: {personName}");

                using var image = Cv2.ImRead(imagePath);
                if (image.Empty())
                {
                    Console.WriteLine($"  警告: 画像を読み込めませんでした");
                    continue;
                }

                var faces = await _faceRecognizer.DetectFacesAsync(image);
                if (faces.Count > 0)
                {
                    var largestFace = faces
                        .OrderByDescending(f => f.BBox.Width * f.BBox.Height)
                        .First();

                    var embedding = await _faceRecognizer.ExtractFaceEmbeddingAsync(image, largestFace.BBox);
                    _faceDatabase.RegisterFace(personName, personName, embedding);

                    Console.WriteLine($"  登録完了: {personName}");
                }
                else
                {
                    Console.WriteLine($"  警告: 顔が検出されませんでした");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  エラー: {ex.Message}");
            }
        }

        totalStopwatch.Stop();
        Console.WriteLine($"[参照顔読み込み完了] 総処理時間: {totalStopwatch.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// リアルタイム検出・認識の開始
    /// </summary>
    public void Start(VideoCapture capture)
    {
        using var frame = new Mat();
        var frameCount = 0;
        var startTime = DateTime.Now;

        Cv2.NamedWindow(_windowName);

        Console.WriteLine($"統合検出器を開始します...");
        Console.WriteLine($"- 物体検出: {(_enableObjectDetection ? "有効" : "無効")}");
        Console.WriteLine($"- 顔認証: {(_enableFaceRecognition ? "有効" : "無効")}");
        Console.WriteLine("終了するには 'q'、'Q'、または ESC キーを押してください。");

        while (true)
        {
            if (!capture.Read(frame) || frame.Empty())
            {
                Console.WriteLine("カメラからフレームを読み取れませんでした");
                break;
            }

            frameCount++;

            var frameStopwatch = Stopwatch.StartNew();

            // 統合処理の実行
            var results = ProcessFrameUnified(frame).GetAwaiter().GetResult();

            // 結果の描画
            DrawResults(frame, results);

            // フレームの表示
            Cv2.ImShow(_windowName, frame);

            frameStopwatch.Stop();

            // パフォーマンス情報の表示
            if (frameCount % 60 == 0)
            {
                var elapsed = DateTime.Now - startTime;
                var currentFps = frameCount / elapsed.TotalSeconds;
                Console.WriteLine($"FPS: {currentFps:F1}, 物体: {results.ObjectDetections.Count}, 顔: {results.FaceRecognitions.Count}");
            }

            // キー入力の確認
            var key = Cv2.WaitKey(1);
            if (key == 'q' || key == 'Q' || key == 27)
            {
                Console.WriteLine("終了します...");
                break;
            }
        }

        Cv2.DestroyWindow(_windowName);
    }

    /// <summary>
    /// 物体検出と顔認証を統合して処理
    /// </summary>
    private async Task<UnifiedResults> ProcessFrameUnified(Mat frame)
    {
        var objectTask = _enableObjectDetection && _objectDetector != null
            ? _objectDetector.DetectAsync(frame)
            : Task.FromResult(new List<Detection>());

        var faceTask = _enableFaceRecognition
            ? ProcessFaceRecognition(frame)
            : Task.FromResult(new List<FaceRecognitionResult>());

        await Task.WhenAll(objectTask, faceTask);

        return new UnifiedResults(
            await objectTask,
            await faceTask
        );
    }

    /// <summary>
    /// 顔認証処理
    /// </summary>
    private async Task<List<FaceRecognitionResult>> ProcessFaceRecognition(Mat frame)
    {
        var results = new List<FaceRecognitionResult>();

        try
        {
            var faces = await _faceRecognizer.DetectFacesAsync(frame);

            foreach (var face in faces)
            {
                var embedding = await _faceRecognizer.ExtractFaceEmbeddingAsync(frame, face.BBox);
                var identification = _faceDatabase.IdentifyFace(embedding, 0.5f);

                results.Add(new FaceRecognitionResult(
                    face.BBox,
                    face.Confidence,
                    identification.Confidence,
                    identification.Name,
                    face.Landmarks,
                    face.Angles
                ));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"顔認証エラー: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// 検出・認証結果の描画
    /// </summary>
    private static void DrawResults(Mat frame, UnifiedResults results)
    {
        // 物体検出結果の描画
        DrawObjectDetections(frame, results.ObjectDetections);

        // 顔認証結果の描画
        DrawFaceRecognitions(frame, results.FaceRecognitions);

        // 統計情報の描画
        DrawStatistics(frame, results);
    }

    /// <summary>
    /// 物体検出結果の描画
    /// </summary>
    private static void DrawObjectDetections(Mat frame, List<Detection> detections)
    {
        foreach (var detection in detections)
        {
            var rect = new Rect(
                (int)detection.BBox.X,
                (int)detection.BBox.Y,
                (int)detection.BBox.Width,
                (int)detection.BBox.Height
            );

            // 物体検出は緑系の色で描画
            var color = new Scalar(0, 255, 0);
            Cv2.Rectangle(frame, rect, color, 2);

            var label = $"{detection.ClassName}: {detection.Confidence:F2}";
            var labelPos = new OpenCvSharp.Point(rect.X, rect.Y - 5);

            Cv2.PutText(frame, label, labelPos, HersheyFonts.HersheyDuplex, 0.5, color, 1);
        }
    }

    /// <summary>
    /// 顔認証結果の描画
    /// </summary>
    private static void DrawFaceRecognitions(Mat frame, List<FaceRecognitionResult> recognitions)
    {
        foreach (var recognition in recognitions)
        {
            var rect = new Rect(
                recognition.BoundingBox.X,
                recognition.BoundingBox.Y,
                recognition.BoundingBox.Width,
                recognition.BoundingBox.Height
            );

            // 認証結果に応じて色を変更
            var color = recognition.Similarity >= 0.6f
                ? new Scalar(255, 0, 0)      // 青（高信頼）
                : recognition.Similarity >= 0.4f
                    ? new Scalar(0, 255, 255) // 黄（中信頼）
                    : new Scalar(0, 0, 255);  // 赤（低信頼）

            Cv2.Rectangle(frame, rect, color, 3);

            // 認証情報のラベル
            var label = recognition.Name != "Unknown"
                ? $"{recognition.Name}: {recognition.Similarity:F2}"
                : $"Unknown: {recognition.Similarity:F2}";

            // 角度情報の追加
            if (recognition.Angles != null)
            {
                label += $" | R:{recognition.Angles.Roll:F0} P:{recognition.Angles.Pitch:F0} Y:{recognition.Angles.Yaw:F0}";
            }

            var labelPos = new OpenCvSharp.Point(rect.X, rect.Y - 25);

            // 背景付きテキスト
            var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheyDuplex, 0.6, 2, out var baseline);
            var textRect = new Rect(labelPos.X, labelPos.Y - textSize.Height - baseline, 
                                   textSize.Width, textSize.Height + baseline);
            Cv2.Rectangle(frame, textRect, new Scalar(0, 0, 0), -1);

            Cv2.PutText(frame, label, labelPos, HersheyFonts.HersheyDuplex, 0.6, color, 2);

            // ランドマークの描画
            if (recognition.Landmarks != null)
            {
                DrawLandmarks(frame, recognition.Landmarks);
            }
        }
    }

    /// <summary>
    /// ランドマークの描画
    /// </summary>
    private static void DrawLandmarks(Mat frame, FaceLandmarks landmarks)
    {
        var eyeColor = new Scalar(255, 255, 0);  // シアン（目）
        var noseColor = new Scalar(0, 0, 255);   // 赤（鼻）
        var mouthColor = new Scalar(0, 255, 255); // 黄（口）

        Cv2.Circle(frame, new OpenCvSharp.Point((int)landmarks.LeftEye.X, (int)landmarks.LeftEye.Y), 2, eyeColor, -1);
        Cv2.Circle(frame, new OpenCvSharp.Point((int)landmarks.RightEye.X, (int)landmarks.RightEye.Y), 2, eyeColor, -1);
        Cv2.Circle(frame, new OpenCvSharp.Point((int)landmarks.Nose.X, (int)landmarks.Nose.Y), 2, noseColor, -1);
        Cv2.Circle(frame, new OpenCvSharp.Point((int)landmarks.LeftMouth.X, (int)landmarks.LeftMouth.Y), 2, mouthColor, -1);
        Cv2.Circle(frame, new OpenCvSharp.Point((int)landmarks.RightMouth.X, (int)landmarks.RightMouth.Y), 2, mouthColor, -1);
    }

    /// <summary>
    /// 統計情報の描画
    /// </summary>
    private static void DrawStatistics(Mat frame, UnifiedResults results)
    {
        var stats = $"Objects: {results.ObjectDetections.Count} | Faces: {results.FaceRecognitions.Count}";
        var knownFaces = results.FaceRecognitions.Count(f => f.Name != "Unknown");
        if (knownFaces > 0)
        {
            stats += $" | Known: {knownFaces}";
        }

        var textColor = new Scalar(255, 255, 255);
        var bgColor = new Scalar(0, 0, 0);

        var textSize = Cv2.GetTextSize(stats, HersheyFonts.HersheyDuplex, 0.7, 2, out var baseline);
        var bgRect = new Rect(10, frame.Height - textSize.Height - baseline - 20, 
                             textSize.Width + 20, textSize.Height + baseline + 10);

        Cv2.Rectangle(frame, bgRect, bgColor, -1);
        Cv2.PutText(frame, stats, new OpenCvSharp.Point(20, frame.Height - 20), 
                   HersheyFonts.HersheyDuplex, 0.7, textColor, 2);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _objectDetector?.Dispose();
            _faceRecognizer?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// 統合検出結果
/// </summary>
public sealed record UnifiedResults(
    List<Detection> ObjectDetections,
    List<FaceRecognitionResult> FaceRecognitions
);

/// <summary>
/// 顔認証結果
/// </summary>
public sealed record FaceRecognitionResult(
    Rectangle BoundingBox,
    float DetectionConfidence,
    float Similarity,
    string Name,
    FaceLandmarks? Landmarks = null,
    FaceAngles? Angles = null
);