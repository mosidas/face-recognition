using System.CommandLine;
using OpenCvSharp;
using Recognizer;

namespace RealTimeDetector;

internal class Program
{
  private static async Task<int> Main(string[] args)
  {
    var modelPathOption = new Option<string?>(
        "--model",
        description: "Path to the ONNX model file (optional)");

    var modeOption = new Option<string>(
        "--mode",
        getDefaultValue: () => "objects",
        description: "Detection mode: 'objects' for object detection, 'faces' for face detection with landmarks");

    var confidenceOption = new Option<float>(
        "--confidence",
        getDefaultValue: () => 0.5f,
        description: "Confidence threshold for detection");

    var nmsOption = new Option<float>(
        "--nms",
        getDefaultValue: () => 0.4f,
        description: "NMS threshold for detection");

    var cameraIndexOption = new Option<int>(
        "--camera",
        getDefaultValue: () => 0,
        description: "Camera index (default: 0)");

    var rootCommand = new RootCommand("Real-time detection with camera using OpenCV (supports objects and faces with landmarks)");
    rootCommand.AddOption(modelPathOption);
    rootCommand.AddOption(modeOption);
    rootCommand.AddOption(confidenceOption);
    rootCommand.AddOption(nmsOption);
    rootCommand.AddOption(cameraIndexOption);

    rootCommand.SetHandler((string? modelPath, string mode, float confidence, float nms, int cameraIndex) =>
    {
      Environment.ExitCode = RunCameraTest(modelPath, mode, confidence, nms, cameraIndex);
    }, modelPathOption, modeOption, confidenceOption, nmsOption, cameraIndexOption);

    return await rootCommand.InvokeAsync(args);
  }

  private static int RunCameraTest(string? modelPath, string mode, float confidenceThreshold, float nmsThreshold, int cameraIndex)
  {
    bool enableDetection = !string.IsNullOrEmpty(modelPath);
    bool isFaceMode = mode.Equals("faces", StringComparison.OrdinalIgnoreCase);

    if (enableDetection)
    {
      var detectionType = isFaceMode ? "face detection with landmarks" : "object detection";
      Console.WriteLine($"Starting real-time {detectionType}...");
      Console.WriteLine($"Model: {modelPath}");
      Console.WriteLine($"Mode: {mode}");
      Console.WriteLine($"Confidence threshold: {confidenceThreshold}");
      Console.WriteLine($"NMS threshold: {nmsThreshold}");
    }
    else
    {
      Console.WriteLine("Starting camera test (no detection)...");
    }

    Console.WriteLine($"Camera index: {cameraIndex}");
    Console.WriteLine("Press 'q' or ESC to quit");

    VideoCapture? capture = null;
    YoloDetector? objectDetector = null;
    YoloFaceDetector? faceDetector = null;
    RealTimeObjectDetector? rtObjectDetector = null;
    RealTimeFaceDetector? rtFaceDetector = null;

    try
    {
      if (enableDetection)
      {
        if (!File.Exists(modelPath!))
        {
          Console.WriteLine($"Model file not found: {modelPath}");
          return 1;
        }

        if (isFaceMode)
        {
          faceDetector = new YoloFaceDetector(
              modelPath!,
              confidenceThreshold,
              YoloFaceModelType.Auto);
          Console.WriteLine("YOLOv8n-face detector initialized successfully.");
        }
        else
        {
          objectDetector = new YoloDetector(
              modelPath!,
              CocoClassNames.Names,
              confidenceThreshold,
              nmsThreshold);
          Console.WriteLine("YOLO object detector initialized successfully.");
        }
      }

      capture = new VideoCapture(cameraIndex);
      if (!capture.IsOpened())
      {
        Console.WriteLine($"Failed to open camera {cameraIndex}");
        Console.WriteLine("Available camera indices to try: 0, 1, 2...");
        return 1;
      }

      var frameWidth = (int)capture.Get(VideoCaptureProperties.FrameWidth);
      var frameHeight = (int)capture.Get(VideoCaptureProperties.FrameHeight);
      var fps = capture.Get(VideoCaptureProperties.Fps);

      Console.WriteLine($"Camera resolution: {frameWidth}x{frameHeight} @ {fps} FPS");

      if (enableDetection)
      {
        if (isFaceMode && faceDetector != null)
        {
          Console.WriteLine("Starting real-time face detection with landmarks...");
          rtFaceDetector = new RealTimeFaceDetector(faceDetector, capture);
          rtFaceDetector.Start();
        }
        else if (!isFaceMode && objectDetector != null)
        {
          Console.WriteLine("Starting real-time object detection...");
          rtObjectDetector = new RealTimeObjectDetector(objectDetector, capture);
          rtObjectDetector.Start();
        }
      }
      else
      {
        Console.WriteLine("Starting camera display...");
        RunBasicCameraTest(capture);
      }

      return 0;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error: {ex.Message}");
      Console.WriteLine($"Stack trace: {ex.StackTrace}");
      return 1;
    }
    finally
    {
      rtFaceDetector?.Dispose();
      rtObjectDetector?.Dispose();
      faceDetector?.Dispose();
      objectDetector?.Dispose();
      capture?.Dispose();
      Cv2.DestroyAllWindows();

      var completionMessage = enableDetection
          ? isFaceMode ? "Face detection test completed." : "Object detection test completed."
          : "Camera test completed.";
      Console.WriteLine(completionMessage);
    }
  }

  private static void RunBasicCameraTest(VideoCapture capture)
  {
    using var frame = new Mat();
    var frameCount = 0;
    var startTime = DateTime.Now;

    while (true)
    {
      if (!capture.Read(frame) || frame.Empty())
      {
        Console.WriteLine("Failed to read frame from camera");
        break;
      }

      frameCount++;

      Cv2.ImShow("Camera Test", frame);

      // デバッグとパフォーマンス確認のため定期的にFPS表示
      if (frameCount % 30 == 0)
      {
        var elapsed = DateTime.Now - startTime;
        var currentFps = frameCount / elapsed.TotalSeconds;
        Console.WriteLine($"Display FPS: {currentFps:F1}");
      }

      var key = Cv2.WaitKey(1);
      if (key == 'q' || key == 'Q' || key == 27)
      {
        Console.WriteLine("Quit requested by user");
        break;
      }
    }
  }
}
