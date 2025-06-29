using OpenCvSharp;
using Recognizer;
using System.CommandLine;

namespace RealTimeDetector;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var modelPathOption = new Option<string?>(
            "--model",
            description: "Path to the ONNX model file for object detection (optional)");

        var confidenceOption = new Option<float>(
            "--confidence",
            getDefaultValue: () => 0.5f,
            description: "Confidence threshold for object detection");

        var nmsOption = new Option<float>(
            "--nms",
            getDefaultValue: () => 0.4f,
            description: "NMS threshold for object detection");

        var cameraIndexOption = new Option<int>(
            "--camera",
            getDefaultValue: () => 0,
            description: "Camera index (default: 0)");

        var rootCommand = new RootCommand("Real-time object detection with camera using OpenCV");
        rootCommand.AddOption(modelPathOption);
        rootCommand.AddOption(confidenceOption);
        rootCommand.AddOption(nmsOption);
        rootCommand.AddOption(cameraIndexOption);

        rootCommand.SetHandler((string? modelPath, float confidence, float nms, int cameraIndex) =>
        {
            Environment.ExitCode = RunCameraTest(modelPath, confidence, nms, cameraIndex);
        }, modelPathOption, confidenceOption, nmsOption, cameraIndexOption);

        return await rootCommand.InvokeAsync(args);
    }

    static int RunCameraTest(string? modelPath, float confidenceThreshold, float nmsThreshold, int cameraIndex)
    {
        bool enableDetection = !string.IsNullOrEmpty(modelPath);

        if (enableDetection)
        {
            Console.WriteLine("Starting real-time object detection...");
            Console.WriteLine($"Model: {modelPath}");
            Console.WriteLine($"Confidence threshold: {confidenceThreshold}");
            Console.WriteLine($"NMS threshold: {nmsThreshold}");
        }
        else
        {
            Console.WriteLine("Starting camera test (no object detection)...");
        }

        Console.WriteLine($"Camera index: {cameraIndex}");
        Console.WriteLine("Press 'q' or ESC to quit");

        VideoCapture? capture = null;
        YoloDetector? detector = null;
        RealTimeObjectDetector? rtDetector = null;

        try
        {
            if (enableDetection)
            {
                if (!File.Exists(modelPath!))
                {
                    Console.WriteLine($"Model file not found: {modelPath}");
                    return 1;
                }

                detector = new YoloDetector(modelPath!, CocoClassNames.Names, confidenceThreshold, nmsThreshold);
                Console.WriteLine("YOLO detector initialized successfully.");
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

            if (enableDetection && detector != null)
            {
                Console.WriteLine("Starting real-time object detection...");
                rtDetector = new RealTimeObjectDetector(detector, capture);
                rtDetector.Start();
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
            rtDetector?.Dispose();
            detector?.Dispose();
            capture?.Dispose();
            Cv2.DestroyAllWindows();

            if (enableDetection)
            {
                Console.WriteLine("Object detection test completed.");
            }
            else
            {
                Console.WriteLine("Camera test completed.");
            }
        }
    }

    static void RunBasicCameraTest(VideoCapture capture)
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
