using OpenCvSharp;
using RealTimeFaceRecognizer;
using Recognizer;

if (args.Length < 3)
{
  Console.WriteLine("使用方法: RealTimeFaceRecognizer <検出モデルパス> <認識モデルパス> <顔画像フォルダパス>");
  Console.WriteLine("例: RealTimeFaceRecognizer detector.onnx recognizer.onnx faces/");
  return 1;
}

var detectorModelPath = args[0];
var recognizerModelPath = args[1];
var faceImagesPath = args[2];

// カメラデバイスIDを引数で指定可能（デフォルトは0）
var cameraDeviceId = args.Length > 3 && int.TryParse(args[3], out var id) ? id : 0;

try
{
  Console.WriteLine($"検出モデル: {detectorModelPath}");
  Console.WriteLine($"認識モデル: {recognizerModelPath}");
  Console.WriteLine($"顔画像フォルダ: {faceImagesPath}");
  Console.WriteLine($"カメラデバイスID: {cameraDeviceId}");
  Console.WriteLine();

  // 顔認識器の初期化
  using var faceRecognizer = new FaceRecognizer(detectorModelPath, recognizerModelPath);

  // リアルタイム顔認識器の初期化
  var realTimeRecognizer = new RealTimeFaceRecognizerMain(faceRecognizer, faceImagesPath);

  // 参照顔の読み込み
  Console.WriteLine("参照顔画像を読み込んでいます...");
  await realTimeRecognizer.LoadReferenceFacesAsync();

  // カメラの初期化
  using var capture = new VideoCapture(cameraDeviceId);
  if (!capture.IsOpened())
  {
    Console.WriteLine("カメラを開けませんでした。");
    return 1;
  }

  // カメラの解像度設定（オプション）
  capture.Set(VideoCaptureProperties.FrameWidth, 1280);
  capture.Set(VideoCaptureProperties.FrameHeight, 720);

  Console.WriteLine("\nリアルタイム顔認識を開始します。終了するには 'q' キーを押してください。");

  // リアルタイム認識開始
  realTimeRecognizer.Start(capture);

  return 0;
}
catch (Exception ex)
{
  Console.WriteLine($"エラーが発生しました: {ex.Message}");
  return 1;
}
