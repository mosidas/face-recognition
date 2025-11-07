using OpenCvSharp;
using Recognizer;

namespace RealTimeDetector;

/// <summary>
/// 物体検出結果の描画
/// </summary>
public static class ObjectDetectionRenderer
{
  /// <summary>
  /// 検出結果を描画
  /// </summary>
  public static void DrawDetections(Mat frame, List<Detection> detections)
  {
    foreach (var detection in detections)
    {
      DrawSingleDetection(frame, detection);
    }

    DrawDetectionCount(frame, detections.Count);
  }

  /// <summary>
  /// 単一検出結果の描画
  /// </summary>
  private static void DrawSingleDetection(Mat frame, Detection detection)
  {
    Rect rect = new(
        (int)detection.BBox.X,
        (int)detection.BBox.Y,
        (int)detection.BBox.Width,
        (int)detection.BBox.Height
    );

    // 視認性向上のため信頼度による色分け
    var color = GetConfidenceColor(detection.Confidence);

    Cv2.Rectangle(frame, rect, color, 2);
    DrawLabel(frame, detection, rect, color);
  }

  /// <summary>
  /// ラベルの描画
  /// </summary>
  private static void DrawLabel(Mat frame, Detection detection, Rect rect, Scalar color)
  {
    var label = $"{detection.ClassName}: {detection.Confidence:F2}";
    var font = HersheyFonts.HersheySimplex;
    var fontScale = 0.6;
    var thickness = 1;

    var textSize = Cv2.GetTextSize(label, font, fontScale, thickness, out int baseline);

    Rect textRect = new(
        rect.X,
        rect.Y - textSize.Height - baseline - 5,
        textSize.Width + 10,
        textSize.Height + baseline + 5
    );

    // 可読性向上のため背景付きテキスト
    Cv2.Rectangle(frame, textRect, color, -1);

    Cv2.PutText(
        frame,
        label,
        new Point(rect.X + 5, rect.Y - 5),
        font,
        fontScale,
        new Scalar(0, 0, 0),
        thickness
    );
  }

  /// <summary>
  /// 検出数の表示
  /// </summary>
  private static void DrawDetectionCount(Mat frame, int detectionCount)
  {
    var infoText = $"Detections: {detectionCount}";
    var font = HersheyFonts.HersheySimplex;
    var fontScale = 0.8;
    var thickness = 2;
    _ = new
    Scalar(0, 0, 0, 128);
    Scalar textColor = new(255, 255, 255);

    var textSize = Cv2.GetTextSize(infoText, font, fontScale, thickness, out int baseline);

    // 情報の視認性確保のため背景付き表示
    Rect bgRect = new(10, 10, textSize.Width + 20, textSize.Height + baseline + 10);
    Cv2.Rectangle(frame, bgRect, new Scalar(0, 0, 0), -1);

    Cv2.PutText(
        frame,
        infoText,
        new Point(20, 30 + textSize.Height),
        font,
        fontScale,
        textColor,
        thickness
    );
  }

  /// <summary>
  /// 信頼度による色分け
  /// </summary>
  private static Scalar GetConfidenceColor(float confidence)
  {
    // 直感的な信頼度表示のため色分け（緑=高、黄=中、赤=低）
    if (confidence >= 0.8f)
    {
      return new Scalar(0, 255, 0);
    }
    else if (confidence >= 0.6f)
    {
      return new Scalar(0, 255, 255);
    }
    else
    {
      return new Scalar(0, 0, 255);
    }
  }
}
