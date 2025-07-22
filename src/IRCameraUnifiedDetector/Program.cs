using IRCameraUnifiedDetector;
using Recognizer;
using System.CommandLine;
using System.IO;

var rootCommand = new RootCommand("IRカメラ対応統合検出・認証システム");

// オプション定義
var faceDetectorOption = new Option<string>(
    "--face-detector",
    "顔検出モデル（ONNX）のパス"
) { IsRequired = true };

var faceRecognizerOption = new Option<string>(
    "--face-recognizer",
    "顔認識モデル（ONNX）のパス"
) { IsRequired = true };

var objectModelOption = new Option<string?>(
    "--object-model",
    "物体検出モデル（ONNX）のパス"
);

var faceImagesOption = new Option<string>(
    "--face-images",
    "参照顔画像フォルダのパス"
) { IsRequired = true };

var recognitionThresholdOption = new Option<float>(
    "--recognition-threshold",
    () => 0.5f,
    "顔認識の類似度しきい値"
);

var disableObjectDetectionOption = new Option<bool>(
    "--disable-object-detection",
    () => false,
    "物体検出を無効にする"
);

var disableFaceRecognitionOption = new Option<bool>(
    "--disable-face-recognition",
    () => false,
    "顔認識を無効にする"
);

var windowNameOption = new Option<string>(
    "--window-name",
    () => "IR Camera Unified Detection & Recognition",
    "ウィンドウ名"
);

// オプションをコマンドに追加
rootCommand.AddOption(faceDetectorOption);
rootCommand.AddOption(faceRecognizerOption);
rootCommand.AddOption(objectModelOption);
rootCommand.AddOption(faceImagesOption);
rootCommand.AddOption(recognitionThresholdOption);
rootCommand.AddOption(disableObjectDetectionOption);
rootCommand.AddOption(disableFaceRecognitionOption);
rootCommand.AddOption(windowNameOption);

// ハンドラー設定
rootCommand.SetHandler(async (
    string faceDetectorPath,
    string faceRecognizerPath,
    string? objectModelPath,
    string faceImagesPath,
    float recognitionThreshold,
    bool disableObjectDetection,
    bool disableFaceRecognition,
    string windowName) =>
{
    try
    {
        Console.WriteLine("IRカメラ対応統合検出・認証システム");
        Console.WriteLine("=====================================");
        Console.WriteLine($"顔検出モデル: {faceDetectorPath}");
        Console.WriteLine($"顔認識モデル: {faceRecognizerPath}");
        Console.WriteLine($"物体検出モデル: {objectModelPath ?? "なし"}");
        Console.WriteLine($"参照顔画像フォルダ: {faceImagesPath}");
        Console.WriteLine($"認識しきい値: {recognitionThreshold}");
        Console.WriteLine($"物体検出: {(!disableObjectDetection ? "有効" : "無効")}");
        Console.WriteLine($"顔認識: {(!disableFaceRecognition ? "有効" : "無効")}");
        Console.WriteLine();

        // モデルファイルの存在確認
        if (!File.Exists(faceDetectorPath))
        {
            Console.WriteLine($"エラー: 顔検出モデルファイルが見つかりません: {faceDetectorPath}");
            return;
        }

        if (!File.Exists(faceRecognizerPath))
        {
            Console.WriteLine($"エラー: 顔認識モデルファイルが見つかりません: {faceRecognizerPath}");
            return;
        }

        if (!string.IsNullOrEmpty(objectModelPath) && !File.Exists(objectModelPath))
        {
            Console.WriteLine($"エラー: 物体検出モデルファイルが見つかりません: {objectModelPath}");
            return;
        }

        if (!Directory.Exists(faceImagesPath))
        {
            Console.WriteLine($"エラー: 参照顔画像フォルダが見つかりません: {faceImagesPath}");
            return;
        }

        // 顔認識器の初期化
        Console.WriteLine("顔認識器を初期化中...");
        using var faceRecognizer = new FaceRecognizer(faceDetectorPath, faceRecognizerPath);

        // 物体検出器の初期化
        YoloDetector? objectDetector = null;
        if (!disableObjectDetection && !string.IsNullOrEmpty(objectModelPath))
        {
            Console.WriteLine("物体検出器を初期化中...");
            objectDetector = new YoloDetector(objectModelPath, CocoClassNames.Names);
        }

        // ログサービスの初期化
        using var logger = new LoggingService();
        logger.Info("Program", "=====================================");
        logger.Info("Program", "IRカメラ対応統合検出・認証システム起動");
        logger.Info("Program", "=====================================");

        // 統合検出器の初期化
        using var irDetector = new IRUnifiedDetectorMain(
            objectDetector,
            faceRecognizer,
            windowName,
            !disableObjectDetection,
            !disableFaceRecognition,
            logger);

        // 参照顔の読み込み
        if (!disableFaceRecognition)
        {
            Console.WriteLine("参照顔画像を読み込み中...");
            await irDetector.LoadReferenceFacesAsync(faceImagesPath);
        }

        // 統合検出の開始
        Console.WriteLine("統合検出を開始します...");
        var success = await irDetector.StartAsync();

        if (success)
        {
            Console.WriteLine("統合検出が正常に終了しました。");
        }
        else
        {
            Console.WriteLine("統合検出の開始に失敗しました。");
        }

        // リソースの解放
        objectDetector?.Dispose();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"エラーが発生しました: {ex.Message}");
        Console.WriteLine($"詳細: {ex.StackTrace}");
    }
},
faceDetectorOption,
faceRecognizerOption,
objectModelOption,
faceImagesOption,
recognitionThresholdOption,
disableObjectDetectionOption,
disableFaceRecognitionOption,
windowNameOption);

// アプリケーション実行
return await rootCommand.InvokeAsync(args);
