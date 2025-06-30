using Recognizer;
using static Recognizer.Constants;

internal sealed class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("ONNX推論サンプル");
        Console.WriteLine("================");

        Console.WriteLine("1. 物体検出 (YOLO)");
        Console.WriteLine("2. 顔認証 (YOLOv8n/v11-face対応)");
        Console.WriteLine("3. YOLOv8n-face専用検出器テスト");
        Console.Write("選択してください (1-3): ");
        var choice = Console.ReadLine();

        await (choice switch
        {
            "1" => RunObjectDetection(),
            "2" => RunFaceRecognition(),
            "3" => RunYolov8nFaceDetectionDemo(),
            _ => HandleInvalidChoice()
        });
    }

    private static async Task RunYolov8nFaceDetectionDemo()
    {
        Console.WriteLine("\n=== YOLOv8n-face専用検出器デモ ===");

        Console.Write("YOLOv8n-faceモデルファイルのパス (.onnx): ");
        var modelPath = Console.ReadLine() ?? "";

        Console.Write("検出する画像のパス: ");
        var imagePath = Console.ReadLine() ?? "";

        if (!File.Exists(modelPath) || !File.Exists(imagePath))
        {
            Console.WriteLine("ファイルが見つかりません");
            return;
        }

        try
        {
            // YOLOv8n-face専用検出器を使用
            using var faceDetector = new YoloFaceDetector(
                modelPath,
                Constants.Thresholds.DefaultFaceDetectionThreshold,
                YoloFaceModelType.Yolov8n // 明示的にYOLOv8n指定
            );

            Console.WriteLine("顔検出中...");
            var faces = await faceDetector.DetectAsync(imagePath).ConfigureAwait(false);

            Console.WriteLine($"\n検出された顔の数: {faces.Count}");

            for (int i = 0; i < faces.Count; i++)
            {
                var face = faces[i];
                Console.WriteLine($"顔 {i + 1}: " +
                                $"信頼度={face.Confidence:F3}, " +
                                $"位置=({face.BBox.X}, {face.BBox.Y}, " +
                                $"{face.BBox.Width}, {face.BBox.Height})");

                if (face.Landmarks != null)
                {
                    Console.WriteLine($"  ランドマーク:");
                    Console.WriteLine($"    左目: ({face.Landmarks.LeftEye.X:F1}, {face.Landmarks.LeftEye.Y:F1})");
                    Console.WriteLine($"    右目: ({face.Landmarks.RightEye.X:F1}, {face.Landmarks.RightEye.Y:F1})");
                    Console.WriteLine($"    鼻: ({face.Landmarks.Nose.X:F1}, {face.Landmarks.Nose.Y:F1})");
                    Console.WriteLine($"    口左: ({face.Landmarks.LeftMouth.X:F1}, {face.Landmarks.LeftMouth.Y:F1})");
                    Console.WriteLine($"    口右: ({face.Landmarks.RightMouth.X:F1}, {face.Landmarks.RightMouth.Y:F1})");
                }
            }

            await SaveFaceDetectionResult(imagePath, faces).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"エラー: {ex}");
        }
    }

    private static async Task SaveFaceDetectionResult(string imagePath, List<FaceDetection> faces)
    {
        Console.Write("\n結果画像を保存しますか？ (y/n): ");
        if (Console.ReadLine()?.ToLower() != "y")
        {
            return;
        }

        using var image = OpenCvSharp.Cv2.ImRead(imagePath);

        foreach (var face in faces)
        {
            var rect = new OpenCvSharp.Rect(
                face.BBox.X,
                face.BBox.Y,
                face.BBox.Width,
                face.BBox.Height
            );

            // 顔検出結果を緑色で描画
            OpenCvSharp.Cv2.Rectangle(image, rect, new OpenCvSharp.Scalar(0, 255, 0), 2);
            var label = $"Face: {face.Confidence:F2}";
            OpenCvSharp.Cv2.PutText(
                image,
                label,
                new OpenCvSharp.Point(rect.X, rect.Y - 5),
                OpenCvSharp.HersheyFonts.HersheySimplex,
                0.5,
                new OpenCvSharp.Scalar(0, 255, 0),
                1
            );

            // ランドマークを描画
            if (face.Landmarks != null)
            {
                var landmarks = face.Landmarks;
                var pointRadius = 3;
                var pointThickness = -1; // 塗りつぶし

                // 左目（青）
                OpenCvSharp.Cv2.Circle(image,
                    new OpenCvSharp.Point((int)landmarks.LeftEye.X, (int)landmarks.LeftEye.Y),
                    pointRadius, new OpenCvSharp.Scalar(255, 0, 0), pointThickness);

                // 右目（青）
                OpenCvSharp.Cv2.Circle(image,
                    new OpenCvSharp.Point((int)landmarks.RightEye.X, (int)landmarks.RightEye.Y),
                    pointRadius, new OpenCvSharp.Scalar(255, 0, 0), pointThickness);

                // 鼻（赤）
                OpenCvSharp.Cv2.Circle(image,
                    new OpenCvSharp.Point((int)landmarks.Nose.X, (int)landmarks.Nose.Y),
                    pointRadius, new OpenCvSharp.Scalar(0, 0, 255), pointThickness);

                // 口左（黄）
                OpenCvSharp.Cv2.Circle(image,
                    new OpenCvSharp.Point((int)landmarks.LeftMouth.X, (int)landmarks.LeftMouth.Y),
                    pointRadius, new OpenCvSharp.Scalar(0, 255, 255), pointThickness);

                // 口右（黄）
                OpenCvSharp.Cv2.Circle(image,
                    new OpenCvSharp.Point((int)landmarks.RightMouth.X, (int)landmarks.RightMouth.Y),
                    pointRadius, new OpenCvSharp.Scalar(0, 255, 255), pointThickness);
            }
        }

        var outputPath = Path.GetFileNameWithoutExtension(imagePath) + "_faces_result.jpg";
        OpenCvSharp.Cv2.ImWrite(outputPath, image);
        Console.WriteLine($"結果を保存しました: {outputPath}");
    }

    private static async Task RunObjectDetection()
    {
        Console.WriteLine("\n=== 物体検出デモ ===");

        Console.Write("YOLOモデルファイルのパス (.onnx): ");
        var modelPath = Console.ReadLine() ?? "";

        Console.Write("検出する画像のパス: ");
        var imagePath = Console.ReadLine() ?? "";

        if (!File.Exists(modelPath) || !File.Exists(imagePath))
        {
            Console.WriteLine("ファイルが見つかりません");
            return;
        }

        try
        {
            using var detector = new YoloDetector(
                modelPath,
                CocoClassNames.Names,
                confidenceThreshold: Constants.Thresholds.DefaultObjectDetectionThreshold,
                nmsThreshold: Constants.Thresholds.DefaultNmsThreshold
            );

            Console.WriteLine("検出中...");
            var detections = await detector.DetectAsync(imagePath).ConfigureAwait(false);
            Console.WriteLine($"\n検出された物体数: {detections.Count}");

            foreach (var detection in detections)
            {
                Console.WriteLine($"- {detection.ClassName}: " +
                                $"信頼度={detection.Confidence:F2}, " +
                                $"位置=({detection.BBox.X:F0}, {detection.BBox.Y:F0}, " +
                                $"{detection.BBox.Width:F0}, {detection.BBox.Height:F0})");
            }

            await SaveDetectionResult(imagePath, detections).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"エラー: {ex}");
        }
    }

    private static async Task RunFaceRecognition()
    {
        Console.WriteLine("\n=== 顔認証デモ（YOLOv8n/v11-face対応） ===");

        Console.Write("顔検出モデルのパス (.onnx): ");
        var detectorPath = Console.ReadLine() ?? "";

        Console.Write("顔認識モデルのパス (.onnx): ");
        var recognizerPath = Console.ReadLine() ?? "";

        if (!File.Exists(detectorPath) || !File.Exists(recognizerPath))
        {
            Console.WriteLine("モデルファイルが見つかりません");
            return;
        }

        // モデルタイプ選択
        Console.WriteLine("\nモデルタイプを選択してください:");
        Console.WriteLine("1. 自動判別（推奨）");
        Console.WriteLine("2. YOLOv8n-face");
        Console.WriteLine("3. YOLOv11-face");
        Console.Write("選択 (1-3): ");
        var modelChoice = Console.ReadLine();

        var modelType = modelChoice switch
        {
            "2" => YoloFaceModelType.Yolov8n,
            "3" => YoloFaceModelType.Yolov11,
            _ => YoloFaceModelType.Auto
        };

        Console.WriteLine("\n1. 顔照合（2つの画像を比較）");
        Console.WriteLine("2. 顔識別（データベースから検索）");
        Console.Write("選択してください (1 or 2): ");
        var task = Console.ReadLine();

        try
        {
            using var recognizer = new FaceRecognizer(
                detectorPath,
                recognizerPath,
                detectionThreshold: Constants.Thresholds.DefaultFaceDetectionThreshold,
                recognitionThreshold: Constants.Thresholds.DefaultFaceRecognitionThreshold,
                modelType: modelType
            );

            Console.WriteLine($"使用モデルタイプ: {modelType}");

            await (task switch
            {
                "1" => HandleFaceVerification(recognizer),
                "2" => RunFaceIdentification(recognizer),
                _ => HandleInvalidChoice()
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"エラー: {ex}");
        }
    }

    private static async Task HandleFaceVerification(FaceRecognizer recognizer)
    {
        Console.Write("1つ目の画像パス: ");
        var image1Path = Console.ReadLine() ?? "";

        Console.Write("2つ目の画像パス: ");
        var image2Path = Console.ReadLine() ?? "";

        if (!File.Exists(image1Path) || !File.Exists(image2Path))
        {
            Console.WriteLine("画像ファイルが見つかりません");
            return;
        }

        Console.WriteLine("照合中...");

        using var image1 = OpenCvSharp.Cv2.ImRead(image1Path);
        using var image2 = OpenCvSharp.Cv2.ImRead(image2Path);

        var result = await recognizer.VerifyFaceAsync(image1, image2).ConfigureAwait(false);

        Console.WriteLine($"\n結果: {result.Message}");
        Console.WriteLine($"類似度: {result.Confidence:F3}");
    }

    private static async Task RunFaceIdentification(FaceRecognizer recognizer)
    {
        var database = new FaceDatabase();

        Console.WriteLine("\n顔データベースに人物を登録します");
        Console.Write("登録する人数: ");

        if (!int.TryParse(Console.ReadLine(), out var count))
        {
            Console.WriteLine("無効な数値です");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Console.WriteLine($"\n人物 {i + 1}:");
            Console.Write("名前: ");
            var name = Console.ReadLine() ?? $"Person{i + 1}";

            Console.Write("画像パス: ");
            var imagePath = Console.ReadLine() ?? "";

            if (!File.Exists(imagePath)) continue;

            var faces = await recognizer.DetectFacesAsync(imagePath).ConfigureAwait(false);
            if (faces.Count > 0)
            {
                var face = faces.First();
                var embedding = await recognizer.ExtractFaceEmbeddingAsync(imagePath, face.BBox).ConfigureAwait(false);
                database.RegisterFace($"person_{i}", name, embedding);
                Console.WriteLine($"{name} を登録しました");
            }
            else
            {
                Console.WriteLine("顔が検出されませんでした");
            }
        }

        Console.Write("\n識別する画像のパス: ");
        var queryImage = Console.ReadLine() ?? "";

        if (!File.Exists(queryImage)) return;

        Console.WriteLine("識別中...");
        var queryFaces = await recognizer.DetectFacesAsync(queryImage).ConfigureAwait(false);

        if (queryFaces.Count > 0)
        {
            var face = queryFaces.First();
            var embedding = await recognizer.ExtractFaceEmbeddingAsync(queryImage, face.BBox).ConfigureAwait(false);
            var result = database.IdentifyFace(embedding);

            Console.WriteLine($"\n識別結果: {result.Name}");
            Console.WriteLine($"信頼度: {result.Confidence:F3}");
        }
        else
        {
            Console.WriteLine("顔が検出されませんでした");
        }
    }

    private static Task SaveDetectionResult(string imagePath, List<Detection> detections)
    {
        Console.Write("\n結果画像を保存しますか？ (y/n): ");
        if (Console.ReadLine()?.ToLower() != "y")
        {
            return Task.CompletedTask;
        }

        using var image = OpenCvSharp.Cv2.ImRead(imagePath);

        foreach (var detection in detections)
        {
            var rect = new OpenCvSharp.Rect(
                (int)detection.BBox.X,
                (int)detection.BBox.Y,
                (int)detection.BBox.Width,
                (int)detection.BBox.Height
            );

            OpenCvSharp.Cv2.Rectangle(image, rect, new OpenCvSharp.Scalar(0, 255, 0), 2);
            var label = $"{detection.ClassName}: {detection.Confidence:F2}";
            OpenCvSharp.Cv2.PutText(
                image,
                label,
                new OpenCvSharp.Point(rect.X, rect.Y - 5),
                OpenCvSharp.HersheyFonts.HersheySimplex,
                0.5,
                new OpenCvSharp.Scalar(0, 255, 0),
                1
            );
        }

        var outputPath = Path.GetFileNameWithoutExtension(imagePath) + Constants.Files.ResultImageSuffix;
        OpenCvSharp.Cv2.ImWrite(outputPath, image);
        Console.WriteLine($"結果を保存しました: {outputPath}");

        return Task.CompletedTask;
    }

    private static Task HandleInvalidChoice()
    {
        Console.WriteLine("無効な選択です");
        return Task.CompletedTask;
    }
}
