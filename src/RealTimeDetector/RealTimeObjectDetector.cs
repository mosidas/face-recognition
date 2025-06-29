using OpenCvSharp;
using Recognizer;

namespace RealTimeDetector;

public class RealTimeObjectDetector : IDisposable
{
    private readonly YoloDetector _detector;
    private readonly VideoCapture _capture;
    private readonly string _windowName;
    private bool _disposed = false;

    public RealTimeObjectDetector(YoloDetector detector, VideoCapture capture, string windowName = "Real-time Object Detection")
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _windowName = windowName;
    }

    public void Start()
    {
        Console.WriteLine("Starting real-time object detection...");
        Console.WriteLine("Press 'q' or ESC to quit");

        var frameCount = 0;
        var startTime = DateTime.Now;

        while (true)
        {
            using var frame = new Mat();
            using var displayFrame = new Mat();

            // フレームを読み取り
            if (!_capture.Read(frame) || frame.Empty())
            {
                Console.WriteLine("Failed to read frame from camera");
                break;
            }

            frameCount++;

            try
            {
                // フレームをコピーして表示用を準備
                frame.CopyTo(displayFrame);

                // 物体検出を実行（同期処理、一時ファイル不要）
                var detections = DetectObjects(frame);

                // 検出結果を描画
                if (detections.Count > 0)
                {
                    ObjectDetectionRenderer.DrawDetections(displayFrame, detections);
                }

                // FPS計算と表示（30フレームごと）
                if (frameCount % 30 == 0)
                {
                    var elapsed = DateTime.Now - startTime;
                    var currentFps = frameCount / elapsed.TotalSeconds;
                    Console.WriteLine($"Processing FPS: {currentFps:F1}, Detections: {detections.Count}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing frame {frameCount}: {ex.Message}");
            }

            // フレームを表示
            Cv2.ImShow(_windowName, displayFrame);

            // キー入力チェック
            var key = Cv2.WaitKey(1);
            if (key == 'q' || key == 'Q' || key == 27) // 'q' or ESC
            {
                Console.WriteLine("Quit requested by user");
                break;
            }
        }
    }

    /// <summary>
    private List<Detection> DetectObjects(Mat frame)
    {
        if (frame.Empty() || frame.IsDisposed)
        {
            return new List<Detection>();
        }

        try
        {
            // 一時ファイル不要でMatを直接使用（パフォーマンス向上）
            var detectionTask = _detector.DetectAsync(frame);
            var result = detectionTask.GetAwaiter().GetResult();

            return result ?? new List<Detection>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Detection error: {ex.Message}");
            return new List<Detection>();
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
