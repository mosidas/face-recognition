using OpenCvSharp;
using Recognizer;

namespace RealTimeDetector;

/// <summary>
/// リアルタイム顔検出・ランドマーク表示
/// </summary>
public sealed class RealTimeFaceDetector(YoloFaceDetector detector, VideoCapture capture, string windowName = "Real-time Face Detection") : IDisposable
{
    private readonly YoloFaceDetector _detector = detector ?? throw new ArgumentNullException(nameof(detector));
    private readonly VideoCapture _capture = capture ?? throw new ArgumentNullException(nameof(capture));
    private readonly string _windowName = windowName;
    private bool _disposed = false;

    public void Start()
    {
        using Mat frame = new();
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

            var faces = DetectFaces(frame);
            FaceDetectionRenderer.DrawFaceDetections(frame, faces);
            Cv2.ImShow(_windowName, frame);

            // ユーザー体験向上のため定期的にパフォーマンス表示
            if (frameCount % 30 == 0)
            {
                var elapsed = DateTime.Now - startTime;
                var currentFps = frameCount / elapsed.TotalSeconds;
                Console.WriteLine($"Face Detection FPS: {currentFps:F1}, Faces: {faces.Count}");
            }

            var key = Cv2.WaitKey(1);
            if (key == 'q' || key == 'Q' || key == 27)
            {
                Console.WriteLine("Quit requested by user");
                break;
            }
        }
    }

    private List<FaceDetection> DetectFaces(Mat frame)
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
            Console.WriteLine($"Face detection error: {ex.Message}");
            return [];
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
