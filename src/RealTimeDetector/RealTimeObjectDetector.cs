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

            // 物体検出の実行
            var detections = DetectObjects(frame);

            // 結果を描画
            DrawDetections(frame, detections);

            // フレームを表示
            Cv2.ImShow(_windowName, frame);

            // FPS計算と表示（30フレームごと）
            if (frameCount % 30 == 0)
            {
                var elapsed = DateTime.Now - startTime;
                var currentFps = frameCount / elapsed.TotalSeconds;
                Console.WriteLine($"Detection FPS: {currentFps:F1}");
            }

            // キー入力チェック
            var key = Cv2.WaitKey(1);
            if (key == 'q' || key == 'Q' || key == 27) // 'q' or ESC
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
            // 一時ファイル不要でMatを直接使用（パフォーマンス向上）
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

            // バウンディングボックスを描画
            Cv2.Rectangle(frame, rect, new Scalar(0, 255, 0), 2);

            // ラベルを描画
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
