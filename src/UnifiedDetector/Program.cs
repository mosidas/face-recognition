using OpenCvSharp;
using Recognizer;
using System.CommandLine;
using System.CommandLine.Invocation;

namespace UnifiedDetector;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var objectModelOption = new Option<string?>(
            "--object-model",
            description: "Path to the YOLO object detection model (optional)");

        var faceDetectorModelOption = new Option<string>(
            "--face-detector",
            description: "Path to the face detection model (required)");

        var faceRecognizerModelOption = new Option<string>(
            "--face-recognizer", 
            description: "Path to the face recognition model (required)");

        var faceImagesOption = new Option<string?>(
            "--face-images",
            description: "Path to folder containing reference face images (optional)");

        var confidenceOption = new Option<float>(
            "--confidence",
            getDefaultValue: () => 0.5f,
            description: "Detection confidence threshold");

        var recognitionThresholdOption = new Option<float>(
            "--recognition-threshold",
            getDefaultValue: () => 0.6f,
            description: "Face recognition similarity threshold");

        var cameraIndexOption = new Option<int>(
            "--camera",
            getDefaultValue: () => 0,
            description: "Camera index (default: 0)");

        var enableObjectDetectionOption = new Option<bool>(
            "--enable-objects",
            getDefaultValue: () => true,
            description: "Enable object detection");

        var enableFaceRecognitionOption = new Option<bool>(
            "--enable-faces",
            getDefaultValue: () => true,
            description: "Enable face recognition");

        var rootCommand = new RootCommand("Unified object detection and face recognition system");
        rootCommand.AddOption(objectModelOption);
        rootCommand.AddOption(faceDetectorModelOption);
        rootCommand.AddOption(faceRecognizerModelOption);
        rootCommand.AddOption(faceImagesOption);
        rootCommand.AddOption(confidenceOption);
        rootCommand.AddOption(recognitionThresholdOption);
        rootCommand.AddOption(cameraIndexOption);
        rootCommand.AddOption(enableObjectDetectionOption);
        rootCommand.AddOption(enableFaceRecognitionOption);

        rootCommand.SetHandler(async (context) =>
        {
            var objectModelPath = context.ParseResult.GetValueForOption(objectModelOption);
            var faceDetectorPath = context.ParseResult.GetValueForOption(faceDetectorModelOption);
            var faceRecognizerPath = context.ParseResult.GetValueForOption(faceRecognizerModelOption);
            var faceImagesPath = context.ParseResult.GetValueForOption(faceImagesOption);
            var confidence = context.ParseResult.GetValueForOption(confidenceOption);
            var recognitionThreshold = context.ParseResult.GetValueForOption(recognitionThresholdOption);
            var cameraIndex = context.ParseResult.GetValueForOption(cameraIndexOption);
            var enableObjects = context.ParseResult.GetValueForOption(enableObjectDetectionOption);
            var enableFaces = context.ParseResult.GetValueForOption(enableFaceRecognitionOption);

            Environment.ExitCode = await RunUnifiedDetection(
                objectModelPath,
                faceDetectorPath!,
                faceRecognizerPath!,
                faceImagesPath,
                confidence,
                recognitionThreshold,
                cameraIndex,
                enableObjects,
                enableFaces);
        });

        return await rootCommand.InvokeAsync(args);
    }

    static async Task<int> RunUnifiedDetection(
        string? objectModelPath,
        string faceDetectorPath,
        string faceRecognizerPath,
        string? faceImagesPath,
        float confidence,
        float recognitionThreshold,
        int cameraIndex,
        bool enableObjects,
        bool enableFaces)
    {
        Console.WriteLine("=== 統合検出・認識システム ===");
        Console.WriteLine($"物体検出: {(enableObjects && !string.IsNullOrEmpty(objectModelPath) ? "有効" : "無効")}");
        Console.WriteLine($"顔認証: {(enableFaces ? "有効" : "無効")}");
        Console.WriteLine($"カメラ: {cameraIndex}");
        Console.WriteLine($"検出信頼度: {confidence}");
        Console.WriteLine($"認証閾値: {recognitionThreshold}");

        if (enableObjects && !string.IsNullOrEmpty(objectModelPath))
        {
            Console.WriteLine($"物体検出モデル: {objectModelPath}");
        }

        if (enableFaces)
        {
            Console.WriteLine($"顔検出モデル: {faceDetectorPath}");
            Console.WriteLine($"顔認証モデル: {faceRecognizerPath}");
            if (!string.IsNullOrEmpty(faceImagesPath))
            {
                Console.WriteLine($"参照顔画像: {faceImagesPath}");
            }
        }

        Console.WriteLine("終了するには 'q', 'Q', または ESC キーを押してください\n");

        VideoCapture? capture = null;
        YoloDetector? objectDetector = null;
        FaceRecognizer? faceRecognizer = null;
        UnifiedDetectorMain? unifiedDetector = null;

        try
        {
            // 必須モデルの検証
            if (enableFaces)
            {
                if (!File.Exists(faceDetectorPath))
                {
                    Console.WriteLine($"顔検出モデルが見つかりません: {faceDetectorPath}");
                    return 1;
                }

                if (!File.Exists(faceRecognizerPath))
                {
                    Console.WriteLine($"顔認証モデルが見つかりません: {faceRecognizerPath}");
                    return 1;
                }
            }

            // 物体検出器の初期化
            if (enableObjects && !string.IsNullOrEmpty(objectModelPath))
            {
                if (!File.Exists(objectModelPath))
                {
                    Console.WriteLine($"物体検出モデルが見つかりません: {objectModelPath}");
                    return 1;
                }

                Console.WriteLine("物体検出器を初期化中...");
                objectDetector = new YoloDetector(
                    objectModelPath,
                    CocoClassNames.Names,
                    confidence);
                Console.WriteLine("物体検出器の初期化完了");
            }

            // 顔認証器の初期化
            if (enableFaces)
            {
                Console.WriteLine("顔認証器を初期化中...");
                faceRecognizer = new FaceRecognizer(
                    faceDetectorPath,
                    faceRecognizerPath,
                    Constants.Thresholds.DefaultFaceDetectionThreshold,
                    recognitionThreshold);
                Console.WriteLine("顔認証器の初期化完了");
            }

            // 統合検出器の初期化
            unifiedDetector = new UnifiedDetectorMain(
                objectDetector,
                faceRecognizer!,
                enableObjectDetection: enableObjects,
                enableFaceRecognition: enableFaces);

            // 参照顔の読み込み
            if (enableFaces && !string.IsNullOrEmpty(faceImagesPath))
            {
                Console.WriteLine("参照顔を読み込み中...");
                await unifiedDetector.LoadReferenceFacesAsync(faceImagesPath);
            }

            // カメラの初期化
            Console.WriteLine("カメラを初期化中...");
            capture = new VideoCapture(cameraIndex);
            if (!capture.IsOpened())
            {
                Console.WriteLine($"カメラ {cameraIndex} を開けませんでした");
                Console.WriteLine("利用可能なカメラインデックスを試してください: 0, 1, 2...");
                return 1;
            }

            var frameWidth = (int)capture.Get(VideoCaptureProperties.FrameWidth);
            var frameHeight = (int)capture.Get(VideoCaptureProperties.FrameHeight);
            var fps = capture.Get(VideoCaptureProperties.Fps);

            Console.WriteLine($"カメラ解像度: {frameWidth}x{frameHeight} @ {fps} FPS");
            Console.WriteLine("統合検出・認識を開始します...\n");

            // 統合検出・認識の開始
            unifiedDetector.Start(capture);

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"エラー: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"内部エラー: {ex.InnerException.Message}");
            }
            Console.WriteLine($"スタックトレース: {ex.StackTrace}");
            return 1;
        }
        finally
        {
            unifiedDetector?.Dispose();
            faceRecognizer?.Dispose();
            objectDetector?.Dispose();
            capture?.Dispose();
            Cv2.DestroyAllWindows();

            Console.WriteLine("統合検出・認識システムを終了しました。");
        }
    }
}