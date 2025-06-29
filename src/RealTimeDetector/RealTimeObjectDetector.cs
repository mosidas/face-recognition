using OpenCvSharp;
using Recognizer;

namespace RealTimeDetector;

public sealed class RealTimeObjectDetector(YoloDetector detector, VideoCapture capture, string windowName = "Real-time Object Detection") : IDisposable
{
    private readonly YoloDetector _detector = detector ?? throw new ArgumentNullException(nameof(detector));
    private readonly VideoCapture _capture = capture ?? throw new ArgumentNullException(nameof(capture));
    private readonly string _windowName = windowName;
    private bool _disposed = false;

    public void Start()
    {
        using var frame = new Mat();
        var frameCount = 0;
        var startTime = DateTime.Now;

        while (true)
        {
            if (!_capture.Read(frame) || frame.Empty())
            {
                Console.WriteLine("Failed to read frame from camera");
                break;
            }

            frameCount++;

            var detections = DetectObjects(frame);
            DrawDetections(frame, detections);
            Cv2.ImShow(_windowName, frame);

            // ユーザー体験向上のため定期的にパフォーマンス表示
            if (frameCount % 30 == 0)
            {
                var elapsed = DateTime.Now - startTime;
                var currentFps = frameCount / elapsed.TotalSeconds;
                Console.WriteLine($"Detection FPS: {currentFps:F1}");
            }

            var key = Cv2.WaitKey(1);
            if (key == 'q' || key == 'Q' || key == 27)
            {
                Console.WriteLine("Quit requested by user");
                break;
            }
        }
    }

    private List<Detection> DetectObjects(Mat frame)
    {
        if (frame.Empty() || frame.IsDisposed)
        {
            return [];
        }

        try
        {
            // I/O処理削減によるリアルタイム性向上のためMat直接使用
            var detectionTask = _detector.DetectAsync(frame);
            var result = detectionTask.GetAwaiter().GetResult();

            return result ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Detection error: {ex.Message}");
            return [];
        }
    }

    private static void DrawDetections(Mat frame, List<Detection> detections)
    {
        foreach (var detection in detections)
        {
            var rect = new Rect(
                (int)detection.BBox.X,
                (int)detection.BBox.Y,
                (int)detection.BBox.Width,
                (int)detection.BBox.Height
            );

            Cv2.Rectangle(frame, rect, new Scalar(0, 255, 0), 2);

            var label = $"{detection.ClassName}: {detection.Confidence:F2}";
            Cv2.PutText(
                frame,
                label,
                new Point(rect.X, rect.Y - 5),
                HersheyFonts.HersheySimplex,
                0.5,
                new Scalar(0, 255, 0),
                1
            );
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
