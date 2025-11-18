using System.Diagnostics;
using System.Drawing;
using OpenCvSharp;
using Recognizer;

namespace UnifiedDetector;

/// <summary>
/// 物体検出と顔認証を同時に実行する統合検出器
/// </summary>
public sealed class UnifiedDetectorMain(
  YoloDetector? objectDetector,
  FaceRecognizer faceRecognizer,
  string windowName = "Unified Detection & Recognition",
  bool enableObjectDetection = true,
  bool enableFaceRecognition = true) : IDisposable
{
    private readonly YoloDetector? _objectDetector = objectDetector;
    private readonly FaceRecognizer _faceRecognizer = faceRecognizer ?? throw new ArgumentNullException(nameof(faceRecognizer));
    private readonly FaceDatabase _faceDatabase = new();
    private readonly bool _enableObjectDetection = enableObjectDetection && objectDetector is not null;
    private readonly bool _enableFaceRecognition = enableFaceRecognition;
    private readonly string _windowName = windowName;
    private bool _disposed = false;
    private static readonly string[] SourceArray = [".jpg", ".jpeg", ".png", ".bmp"];

    /// <summary>
    /// 顔データベースに参照顔を登録
    /// </summary>
    public async Task LoadReferenceFacesAsync(string faceImagesPath)
    {
        if (!_enableFaceRecognition)
        {
            return;
        }

        Stopwatch totalStopwatch = Stopwatch.StartNew();

        if (!Directory.Exists(faceImagesPath))
        {
            throw new DirectoryNotFoundException($"顔画像フォルダが見つかりません: {faceImagesPath}");
        }

        List<string> imageFiles = [.. Directory.GetFiles(faceImagesPath, "*.*").Where(f => SourceArray.Contains(Path.GetExtension(f).ToLower()))];

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
        using Mat frame = new();
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

            Stopwatch frameStopwatch = Stopwatch.StartNew();

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
        var objectTask = _enableObjectDetection && _objectDetector is not null
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
        List<FaceRecognitionResult> results = [];

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
        // スマートホンの認証処理
        var authenticatedSmartphones = IdentifyAuthenticatedSmartphones(results);

        // 物体検出結果の描画
        DrawObjectDetections(frame, results.ObjectDetections, authenticatedSmartphones);

        // 顔認証結果の描画
        DrawFaceRecognitions(frame, results.FaceRecognitions);

        // 統計情報の描画
        DrawStatistics(frame, results);
    }

    /// <summary>
    /// 物体検出結果の描画
    /// </summary>
    private static void DrawObjectDetections(Mat frame, List<Detection> detections, HashSet<int> authenticatedSmartphones)
    {
        for (int i = 0; i < detections.Count; i++)
        {
            var detection = detections[i];
            Rect rect = new(
                (int)detection.BBox.X,
                (int)detection.BBox.Y,
                (int)detection.BBox.Width,
                (int)detection.BBox.Height
            );

            // スマートホンの認証状態に応じて色を変更
            Scalar color = new(0, 255, 0); // デフォルトは緑
            var label = $"{detection.ClassName}: {detection.Confidence:F2}";

            if (detection.ClassName.ToLower().Contains("cell phone") ||
                detection.ClassName.ToLower().Contains("phone") ||
                detection.ClassName.ToLower().Contains("smartphone"))
            {
                if (authenticatedSmartphones.Contains(i))
                {
                    color = new Scalar(255, 0, 255); // 認証済みスマートホンは紫
                    label += " [AUTH]";
                }
                else
                {
                    color = new Scalar(0, 165, 255); // 未認証スマートホンはオレンジ
                    label += " [UNAUTH]";
                }
            }

            Cv2.Rectangle(frame, rect, color, 2);

            OpenCvSharp.Point labelPos = new(rect.X, rect.Y - 5);
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
            Rect rect = new(
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
            if (recognition.Angles is not null)
            {
                label += $" | R:{recognition.Angles.Roll:F0} P:{recognition.Angles.Pitch:F0} Y:{recognition.Angles.Yaw:F0}";
            }

            OpenCvSharp.Point labelPos = new(rect.X, rect.Y - 25);

            // 背景付きテキスト
            var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheyDuplex, 0.6, 2, out var baseline);
            Rect textRect = new(
                labelPos.X,
                labelPos.Y - textSize.Height - baseline,
                textSize.Width, textSize.Height + baseline
              );
            Cv2.Rectangle(frame, textRect, new Scalar(0, 0, 0), -1);

            Cv2.PutText(frame, label, labelPos, HersheyFonts.HersheyDuplex, 0.6, color, 2);

            // ランドマークの描画
            if (recognition.Landmarks is { } landmarks)
            {
                DrawLandmarks(frame, landmarks);
            }
        }
    }

    /// <summary>
    /// ランドマークの描画
    /// </summary>
    private static void DrawLandmarks(Mat frame, FaceLandmarks landmarks)
    {
        Scalar eyeColor = new(255, 255, 0);  // シアン（目）
        Scalar noseColor = new(0, 0, 255);   // 赤（鼻）
        Scalar mouthColor = new(0, 255, 255); // 黄（口）

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

        Scalar textColor = new(255, 255, 255);
        Scalar bgColor = new(0, 0, 0);

        var textSize = Cv2.GetTextSize(stats, HersheyFonts.HersheyDuplex, 0.7, 2, out var baseline);
        Rect bgRect = new(
            10,
            frame.Height - textSize.Height - baseline - 20,
            textSize.Width + 20, textSize.Height + baseline + 10);

        Cv2.Rectangle(frame, bgRect, bgColor, -1);
        Cv2.PutText(
          frame,
          stats,
          new OpenCvSharp.Point(20, frame.Height - 20),
          HersheyFonts.HersheyDuplex,
          0.7,
          textColor,
          2);
    }

    /// <summary>
    /// 認証された顔に関連するスマートホンを特定
    /// </summary>
    private static HashSet<int> IdentifyAuthenticatedSmartphones(UnifiedResults results)
    {
        HashSet<int> authenticatedSmartphones = [];

        // 認証された顔のみを対象とする
        List<FaceRecognitionResult> authenticatedFaces = [.. results.FaceRecognitions.Where(face => face.Similarity >= 0.4f && face.Name != "Unknown")];

        if (authenticatedFaces.Count == 0)
        {
            return authenticatedSmartphones;
        }

        // 「person」ラベルの物体を取得
        var personDetections = results.ObjectDetections
            .Select((detection, index) => new { Detection = detection, Index = index })
            .Where(x => x.Detection.ClassName.ToLower() == "person")
            .ToList();

        // 各personについて処理
        foreach (var personData in personDetections)
        {
            Rectangle personRect = new(
                (int)personData.Detection.BBox.X,
                (int)personData.Detection.BBox.Y,
                (int)personData.Detection.BBox.Width,
                (int)personData.Detection.BBox.Height
            );

            // このpersonの矩形内にある認証された顔を検索
            List<FaceRecognitionResult> facesInPerson = [.. authenticatedFaces.Where(face => DoesRectanglesOverlap(personRect, face.BoundingBox))];

            // 認証された顔がperson内にある場合
            if (facesInPerson.Count > 0)
            {
                // このpersonの矩形内にあるスマートホンを検索
                for (int i = 0; i < results.ObjectDetections.Count; i++)
                {
                    var detection = results.ObjectDetections[i];

                    // スマートホンかどうかを判定
                    if (!IsSmartphone(detection.ClassName))
                    {
                        continue;
                    }

                    Rectangle smartphoneRect = new(
                        (int)detection.BBox.X,
                        (int)detection.BBox.Y,
                        (int)detection.BBox.Width,
                        (int)detection.BBox.Height
                    );

                    // スマートホンがpersonの矩形内にあるかを判定
                    if (DoesRectanglesOverlap(personRect, smartphoneRect))
                    {
                        _ = authenticatedSmartphones.Add(i);
                    }
                }
            }
        }

        return authenticatedSmartphones;
    }

    /// <summary>
    /// オブジェクトがスマートホンかどうかを判定
    /// </summary>
    private static bool IsSmartphone(string className)
    {
        var lowerName = className.ToLower();
        return lowerName.Contains("cell phone") ||
                lowerName.Contains("phone") ||
                lowerName.Contains("smartphone");
    }

    /// <summary>
    /// 二つの矩形が重複しているかどうかを判定
    /// </summary>
    private static bool DoesRectanglesOverlap(System.Drawing.Rectangle rect1, System.Drawing.Rectangle rect2)
    {
        return rect1.Left < rect2.Right &&
                rect1.Right > rect2.Left &&
                rect1.Top < rect2.Bottom &&
                rect1.Bottom > rect2.Top;
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
