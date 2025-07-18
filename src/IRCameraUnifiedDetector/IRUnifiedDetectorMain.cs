using OpenCvSharp;
using Recognizer;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace IRCameraUnifiedDetector;

/// <summary>
/// IRカメラ対応統合検出器
/// </summary>
public sealed class IRUnifiedDetectorMain : IDisposable
{
    private readonly LoggingService _logger;
    private readonly YoloDetector? _objectDetector;
    private readonly FaceRecognizer _faceRecognizer;
    private readonly FaceDatabase _faceDatabase;
    private readonly CameraService _cameraService;
    private readonly bool _enableObjectDetection;
    private readonly bool _enableFaceRecognition;
    private readonly string _windowName;
    private bool _disposed = false;
    private bool _isRunning = false;

    // フレーム処理統計
    private int _frameCount = 0;
    private DateTime _startTime = DateTime.Now;
    private readonly object _processLock = new();

    public IRUnifiedDetectorMain(
        YoloDetector? objectDetector,
        FaceRecognizer faceRecognizer,
        string windowName = "IR Camera Unified Detection & Recognition",
        bool enableObjectDetection = true,
        bool enableFaceRecognition = true,
        LoggingService? logger = null)
    {
        _logger = logger ?? new LoggingService();
        _objectDetector = objectDetector;
        _faceRecognizer = faceRecognizer ?? throw new ArgumentNullException(nameof(faceRecognizer));
        _faceDatabase = new FaceDatabase();
        _cameraService = new CameraService();
        _windowName = windowName;
        _enableObjectDetection = enableObjectDetection && objectDetector != null;
        _enableFaceRecognition = enableFaceRecognition;

        // カメラサービスのイベントを購読
        _cameraService.FrameArrived += OnFrameArrived;
        _cameraService.StatusChanged += OnStatusChanged;
        
        _logger.Info("IRUnifiedDetectorMain", "初期化完了");
        _logger.Info("IRUnifiedDetectorMain", $"物体検出: {_enableObjectDetection}, 顔認証: {_enableFaceRecognition}");
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
    /// IRカメラ統合検出を開始
    /// </summary>
    public async Task<bool> StartAsync()
    {
        if (_isRunning)
        {
            Console.WriteLine("既に実行中です");
            return false;
        }

        try
        {
            _logger.Info("StartAsync", "IRカメラ統合検出器を初期化中...");
            Console.WriteLine("IRカメラ統合検出器を初期化中...");

            // カメラサービスを初期化
            _logger.Debug("StartAsync", "カメラサービスを初期化開始");
            if (!await _cameraService.InitializeAsync())
            {
                _logger.Error("StartAsync", "カメラサービスの初期化に失敗しました");
                Console.WriteLine("カメラサービスの初期化に失敗しました");
                return false;
            }
            _logger.Info("StartAsync", "カメラサービスの初期化成功");

            // 利用可能なカメラを表示
            Console.WriteLine("利用可能なカメラソース:");
            for (int i = 0; i < _cameraService.AvailableSources.Count; i++)
            {
                var source = _cameraService.AvailableSources[i];
                Console.WriteLine($"  [{i}] {source.Description}");
            }

            // IRカメラが利用可能かチェック
            var hasIRCamera = _cameraService.AvailableSources.Any(s => s.SourceType == CameraSourceType.Infrared);
            if (!hasIRCamera)
            {
                _logger.Warning("StartAsync", "IRカメラが検出されませんでした。通常カメラで起動します。");
                Console.WriteLine("警告: IRカメラが検出されませんでした。通常カメラで起動します。");
            }

            // カメラストリーミングを開始
            _logger.Debug("StartAsync", "カメラストリーミングを開始");
            if (!await _cameraService.StartAsync())
            {
                _logger.Error("StartAsync", "カメラストリーミングの開始に失敗しました");
                Console.WriteLine("カメラストリーミングの開始に失敗しました");
                return false;
            }
            _logger.Info("StartAsync", "カメラストリーミング開始成功");
            
            // アクティブなカメラを表示
            var activeSource = _cameraService.ActiveSources.FirstOrDefault();
            if (activeSource != null)
            {
                var source = _cameraService.AvailableSources.FirstOrDefault(s => s.SourceId == activeSource);
                if (source != null)
                {
                    _logger.Info("StartAsync", $"アクティブカメラ: {source.Description}");
                    Console.WriteLine($"アクティブカメラ: {source.Description}");
                }
            }

            _isRunning = true;
            _startTime = DateTime.Now;

            Cv2.NamedWindow(_windowName, WindowFlags.AutoSize);

            Console.WriteLine($"IRカメラ統合検出器を開始しました");
            Console.WriteLine($"- 物体検出: {(_enableObjectDetection ? "有効" : "無効")}");
            Console.WriteLine($"- 顔認証: {(_enableFaceRecognition ? "有効" : "無効")}");
            Console.WriteLine("カメラ切り替え: 1=IR, 2=Color, 3=Depth");
            Console.WriteLine("終了: 'q', 'Q', または ESC");

            // メインループ
            await RunMainLoop();

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("StartAsync", $"開始中にエラーが発生しました: {ex.Message}", ex);
            Console.WriteLine($"開始中にエラーが発生しました: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// メインループ
    /// </summary>
    private async Task RunMainLoop()
    {
        _logger.Info("RunMainLoop", "メインループ開始");
        
        while (_isRunning)
        {
            var key = Cv2.WaitKey(1);
            
            // キー入力の処理
            if (key == 'q' || key == 'Q' || key == 27) // ESC
            {
                Console.WriteLine("終了します...");
                break;
            }
            else if (key == '1') // IR カメラに切り替え
            {
                await SwitchToCamera(CameraSourceType.Infrared);
            }
            else if (key == '2') // Color カメラに切り替え
            {
                await SwitchToCamera(CameraSourceType.Color);
            }
            else if (key == '3') // Depth カメラに切り替え
            {
                await SwitchToCamera(CameraSourceType.Depth);
            }

            await Task.Delay(10); // CPU負荷を下げるための短い待機
        }
    }

    /// <summary>
    /// 指定したタイプのカメラに切り替え
    /// </summary>
    private async Task SwitchToCamera(CameraSourceType targetType)
    {
        var targetSource = _cameraService.AvailableSources
            .FirstOrDefault(s => s.SourceType == targetType);

        if (targetSource != null)
        {
            Console.WriteLine($"カメラを切り替えます: {targetSource.Description}");
            await _cameraService.SwitchCameraSourceAsync(targetSource.SourceId);
        }
        else
        {
            Console.WriteLine($"指定されたタイプのカメラが見つかりません: {targetType}");
        }
    }

    /// <summary>
    /// 統合検出器を停止
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _isRunning = false;
        await _cameraService.StopAsync();
        Cv2.DestroyAllWindows();
        Console.WriteLine("IRカメラ統合検出器を停止しました");
    }

    /// <summary>
    /// フレーム到着時の処理
    /// </summary>
    private async void OnFrameArrived(object? sender, FrameData frameData)
    {
        if (!_isRunning) return;

        lock (_processLock)
        {
            _frameCount++;
        }

        _logger.Debug("OnFrameArrived", $"フレーム受信: {frameData.SourceType}, フレーム番号: {_frameCount}");

        try
        {
            using var perfScope = new PerformanceScope(_logger, "OnFrameArrived", "フレーム処理");
            var frameStopwatch = Stopwatch.StartNew();

            // フレームのコピーを作成（非同期処理用）
            var frameCopy = frameData.Frame.Clone();

            // 統合処理の実行
            var results = await ProcessFrameUnified(frameCopy);

            // 結果の描画
            DrawResults(frameCopy, results, frameData.SourceType);

            // カメラソース情報の描画
            DrawCameraInfo(frameCopy, frameData);

            // フレームの表示（UIスレッドでの処理）
            lock (_processLock)
            {
                try
                {
                    Cv2.ImShow(_windowName, frameCopy);
                }
                catch (Exception ex)
                {
                    _logger.Error("OnFrameArrived", $"OpenCV表示エラー: {ex.Message}", ex);
                }
            }

            frameStopwatch.Stop();

            // パフォーマンス情報の表示
            if (_frameCount % 60 == 0)
            {
                var elapsed = DateTime.Now - _startTime;
                var currentFps = _frameCount / elapsed.TotalSeconds;
                Console.WriteLine($"FPS: {currentFps:F1}, 物体: {results.ObjectDetections.Count}, 顔: {results.FaceRecognitions.Count}, カメラ: {frameData.SourceType}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("OnFrameArrived", $"フレーム処理エラー: {ex.Message}", ex);
            Console.WriteLine($"フレーム処理エラー: {ex.Message}");
        }
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
    private static void DrawResults(Mat frame, UnifiedResults results, CameraSourceType sourceType)
    {
        // IRカメラの場合は特別な描画処理
        if (sourceType == CameraSourceType.Infrared)
        {
            DrawIRSpecificResults(frame, results);
        }
        else
        {
            DrawStandardResults(frame, results);
        }

        // 統計情報の描画
        DrawStatistics(frame, results, sourceType);
    }

    /// <summary>
    /// IR専用の描画処理
    /// </summary>
    private static void DrawIRSpecificResults(Mat frame, UnifiedResults results)
    {
        // IR画像では輝度情報を強調
        var enhancedFrame = new Mat();
        Cv2.EqualizeHist(frame, enhancedFrame);

        // スマートホンの認証処理
        var authenticatedSmartphones = IdentifyAuthenticatedSmartphones(results);

        // 物体検出結果の描画（IRに適した色）
        DrawObjectDetections(frame, results.ObjectDetections, authenticatedSmartphones, true);

        // 顔認証結果の描画（IRに適した色）
        DrawFaceRecognitions(frame, results.FaceRecognitions, true);
    }

    /// <summary>
    /// 標準の描画処理
    /// </summary>
    private static void DrawStandardResults(Mat frame, UnifiedResults results)
    {
        // スマートホンの認証処理
        var authenticatedSmartphones = IdentifyAuthenticatedSmartphones(results);

        // 物体検出結果の描画
        DrawObjectDetections(frame, results.ObjectDetections, authenticatedSmartphones, false);

        // 顔認証結果の描画
        DrawFaceRecognitions(frame, results.FaceRecognitions, false);
    }

    /// <summary>
    /// 物体検出結果の描画
    /// </summary>
    private static void DrawObjectDetections(Mat frame, List<Detection> detections, HashSet<int> authenticatedSmartphones, bool isIR)
    {
        for (int i = 0; i < detections.Count; i++)
        {
            var detection = detections[i];
            var rect = new Rect(
                (int)detection.BBox.X,
                (int)detection.BBox.Y,
                (int)detection.BBox.Width,
                (int)detection.BBox.Height
            );

            // IRカメラの場合は明るい色を使用
            var color = isIR ? new Scalar(255, 255, 255) : new Scalar(0, 255, 0); // IRは白、通常は緑
            var label = $"{detection.ClassName}: {detection.Confidence:F2}";
            
            if (detection.ClassName.ToLower().Contains("cell phone") || 
                detection.ClassName.ToLower().Contains("phone") ||
                detection.ClassName.ToLower().Contains("smartphone"))
            {
                if (authenticatedSmartphones.Contains(i))
                {
                    color = isIR ? new Scalar(255, 128, 255) : new Scalar(255, 0, 255); // 認証済みスマートホン
                    label += " [AUTH]";
                }
                else
                {
                    color = isIR ? new Scalar(255, 200, 0) : new Scalar(0, 165, 255); // 未認証スマートホン
                    label += " [UNAUTH]";
                }
            }
            
            Cv2.Rectangle(frame, rect, color, 2);

            var labelPos = new OpenCvSharp.Point(rect.X, rect.Y - 5);
            Cv2.PutText(frame, label, labelPos, HersheyFonts.HersheyDuplex, 0.5, color, 1);
        }
    }

    /// <summary>
    /// 顔認証結果の描画
    /// </summary>
    private static void DrawFaceRecognitions(Mat frame, List<FaceRecognitionResult> recognitions, bool isIR)
    {
        foreach (var recognition in recognitions)
        {
            var rect = new Rect(
                recognition.BoundingBox.X,
                recognition.BoundingBox.Y,
                recognition.BoundingBox.Width,
                recognition.BoundingBox.Height
            );

            // IRカメラ用の色設定
            Scalar color;
            if (isIR)
            {
                color = recognition.Similarity >= 0.6f
                    ? new Scalar(255, 255, 255)      // 白（高信頼）
                    : recognition.Similarity >= 0.4f
                        ? new Scalar(200, 200, 200)  // 灰色（中信頼）
                        : new Scalar(128, 128, 128); // 暗い灰色（低信頼）
            }
            else
            {
                color = recognition.Similarity >= 0.6f
                    ? new Scalar(255, 0, 0)          // 青（高信頼）
                    : recognition.Similarity >= 0.4f
                        ? new Scalar(0, 255, 255)    // 黄（中信頼）
                        : new Scalar(0, 0, 255);     // 赤（低信頼）
            }

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
                DrawLandmarks(frame, recognition.Landmarks, isIR);
            }
        }
    }

    /// <summary>
    /// ランドマークの描画
    /// </summary>
    private static void DrawLandmarks(Mat frame, FaceLandmarks landmarks, bool isIR)
    {
        Scalar eyeColor, noseColor, mouthColor;
        
        if (isIR)
        {
            eyeColor = new Scalar(255, 255, 255);   // 白（目）
            noseColor = new Scalar(200, 200, 200);  // 灰色（鼻）
            mouthColor = new Scalar(180, 180, 180); // 薄灰色（口）
        }
        else
        {
            eyeColor = new Scalar(255, 255, 0);     // シアン（目）
            noseColor = new Scalar(0, 0, 255);      // 赤（鼻）
            mouthColor = new Scalar(0, 255, 255);   // 黄（口）
        }

        Cv2.Circle(frame, new OpenCvSharp.Point((int)landmarks.LeftEye.X, (int)landmarks.LeftEye.Y), 2, eyeColor, -1);
        Cv2.Circle(frame, new OpenCvSharp.Point((int)landmarks.RightEye.X, (int)landmarks.RightEye.Y), 2, eyeColor, -1);
        Cv2.Circle(frame, new OpenCvSharp.Point((int)landmarks.Nose.X, (int)landmarks.Nose.Y), 2, noseColor, -1);
        Cv2.Circle(frame, new OpenCvSharp.Point((int)landmarks.LeftMouth.X, (int)landmarks.LeftMouth.Y), 2, mouthColor, -1);
        Cv2.Circle(frame, new OpenCvSharp.Point((int)landmarks.RightMouth.X, (int)landmarks.RightMouth.Y), 2, mouthColor, -1);
    }

    /// <summary>
    /// カメラ情報の描画
    /// </summary>
    private static void DrawCameraInfo(Mat frame, FrameData frameData)
    {
        var cameraInfo = $"Camera: {frameData.SourceType} | Time: {frameData.Timestamp:HH:mm:ss.fff}";
        var color = new Scalar(255, 255, 255);
        var bgColor = new Scalar(0, 0, 0);

        var textSize = Cv2.GetTextSize(cameraInfo, HersheyFonts.HersheyDuplex, 0.6, 2, out var baseline);
        var bgRect = new Rect(10, 10, textSize.Width + 20, textSize.Height + baseline + 10);

        Cv2.Rectangle(frame, bgRect, bgColor, -1);
        Cv2.PutText(frame, cameraInfo, new OpenCvSharp.Point(20, 30), 
                   HersheyFonts.HersheyDuplex, 0.6, color, 2);
    }

    /// <summary>
    /// 統計情報の描画
    /// </summary>
    private static void DrawStatistics(Mat frame, UnifiedResults results, CameraSourceType sourceType)
    {
        var stats = $"Objects: {results.ObjectDetections.Count} | Faces: {results.FaceRecognitions.Count} | Mode: {sourceType}";
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

    /// <summary>
    /// 認証された顔に関連するスマートホンを特定
    /// </summary>
    private static HashSet<int> IdentifyAuthenticatedSmartphones(UnifiedResults results)
    {
        var authenticatedSmartphones = new HashSet<int>();

        // 認証された顔のみを対象とする
        var authenticatedFaces = results.FaceRecognitions
            .Where(face => face.Similarity >= 0.4f && face.Name != "Unknown")
            .ToList();

        if (authenticatedFaces.Count == 0)
            return authenticatedSmartphones;

        // 「person」ラベルの物体を取得
        var personDetections = results.ObjectDetections
            .Select((detection, index) => new { Detection = detection, Index = index })
            .Where(x => x.Detection.ClassName.ToLower() == "person")
            .ToList();

        // 各personについて処理
        foreach (var personData in personDetections)
        {
            var personRect = new System.Drawing.Rectangle(
                (int)personData.Detection.BBox.X,
                (int)personData.Detection.BBox.Y,
                (int)personData.Detection.BBox.Width,
                (int)personData.Detection.BBox.Height
            );

            // このpersonの矩形内にある認証された顔を検索
            var facesInPerson = authenticatedFaces
                .Where(face => DoesRectanglesOverlap(personRect, face.BoundingBox))
                .ToList();

            // 認証された顔がperson内にある場合
            if (facesInPerson.Count > 0)
            {
                // このpersonの矩形内にあるスマートホンを検索
                for (int i = 0; i < results.ObjectDetections.Count; i++)
                {
                    var detection = results.ObjectDetections[i];
                    
                    // スマートホンかどうかを判定
                    if (!IsSmartphone(detection.ClassName))
                        continue;

                    var smartphoneRect = new System.Drawing.Rectangle(
                        (int)detection.BBox.X,
                        (int)detection.BBox.Y,
                        (int)detection.BBox.Width,
                        (int)detection.BBox.Height
                    );

                    // スマートホンがpersonの矩形内にあるかを判定
                    if (DoesRectanglesOverlap(personRect, smartphoneRect))
                    {
                        authenticatedSmartphones.Add(i);
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

    /// <summary>
    /// ステータス変更時の処理
    /// </summary>
    private void OnStatusChanged(object? sender, string status)
    {
        Console.WriteLine($"[Camera] {status}");
    }

    public void Dispose()
    {
        if (_disposed) return;

        _logger.Info("Dispose", "リソースを解放中...");
        _isRunning = false;
        _cameraService?.Dispose();
        _objectDetector?.Dispose();
        _faceRecognizer?.Dispose();
        _logger?.Dispose();
        _disposed = true;
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