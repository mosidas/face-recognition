namespace WPFDetectorApp;

public class CommandLineArgs
{
  public string? YoloModelPath { get; set; }
  public string? FaceDetectionModelPath { get; set; }
  public string? FaceRecognitionModelPath { get; set; }
  public string? FaceImagesPath { get; set; }
  public double? TargetFps { get; set; }
  public bool? ObjectDetectionEnabled { get; set; }
  public bool? FaceRecognitionEnabled { get; set; }

  public static CommandLineArgs Parse(string[] args)
  {
    CommandLineArgs result = new();

    for (int i = 0; i < args.Length; i++)
    {
      string arg = args[i].ToLower();

      switch (arg)
      {
        case "--yolo-model":
        case "-y":
          if (i + 1 < args.Length)
          {
            result.YoloModelPath = args[++i];
          }

          break;

        case "--face-detector":
        case "-fd":
          if (i + 1 < args.Length)
          {
            result.FaceDetectionModelPath = args[++i];
          }

          break;

        case "--face-recognizer":
        case "-fr":
          if (i + 1 < args.Length)
          {
            result.FaceRecognitionModelPath = args[++i];
          }

          break;

        case "--face-images":
        case "-fi":
          if (i + 1 < args.Length)
          {
            result.FaceImagesPath = args[++i];
          }

          break;

        case "--fps":
        case "-f":
          if (i + 1 < args.Length && double.TryParse(args[++i], out double fps))
          {
            result.TargetFps = fps;
          }

          break;

        case "--disable-object-detection":
          result.ObjectDetectionEnabled = false;
          break;

        case "--disable-face-recognition":
          result.FaceRecognitionEnabled = false;
          break;

        case "--enable-object-detection":
          result.ObjectDetectionEnabled = true;
          break;

        case "--enable-face-recognition":
          result.FaceRecognitionEnabled = true;
          break;

        case "--help":
        case "-h":
          ShowHelp();
          Environment.Exit(0);
          break;
      }
    }

    return result;
  }

  private static void ShowHelp()
  {
    Console.WriteLine("WPF IR Camera Detector - Command Line Arguments");
    Console.WriteLine();
    Console.WriteLine("Usage: WPFDetectorApp.exe [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --yolo-model, -y <path>           Path to YOLO model file (.onnx)");
    Console.WriteLine("  --face-detector, -fd <path>       Path to face detection model file (.onnx)");
    Console.WriteLine("  --face-recognizer, -fr <path>     Path to face recognition model file (.onnx)");
    Console.WriteLine("  --face-images, -fi <path>         Path to face images folder");
    Console.WriteLine("  --fps, -f <number>                Target FPS (1-30)");
    Console.WriteLine("  --disable-object-detection        Disable object detection");
    Console.WriteLine("  --disable-face-recognition        Disable face recognition");
    Console.WriteLine("  --enable-object-detection         Enable object detection");
    Console.WriteLine("  --enable-face-recognition         Enable face recognition");
    Console.WriteLine("  --help, -h                        Show this help");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  WPFDetectorApp.exe --yolo-model models/yolo11n.onnx --fps 10");
    Console.WriteLine("  WPFDetectorApp.exe -y models/yolo.onnx -fd models/face_det.onnx -fr models/face_rec.onnx");
    Console.WriteLine("  WPFDetectorApp.exe --face-images reference_faces --disable-object-detection");
  }
}
