using OpenCvSharp;
using Recognizer;

namespace RealTimeDetector;

/// <summary>
/// 物体検出結果の描画を担当するクラス
/// </summary>
public static class ObjectDetectionRenderer
{
    /// <summary>
    /// フレームに検出結果を描画
    /// </summary>
    /// <param name="frame">描画対象のフレーム</param>
    /// <param name="detections">検出結果のリスト</param>
    public static void DrawDetections(Mat frame, List<Detection> detections)
    {
        foreach (var detection in detections)
        {
            DrawSingleDetection(frame, detection);
        }

        // 検出数の表示
        DrawDetectionCount(frame, detections.Count);
    }

    /// <summary>
    /// 単一の検出結果を描画
    /// </summary>
    /// <param name="frame">描画対象のフレーム</param>
    /// <param name="detection">検出結果</param>
    private static void DrawSingleDetection(Mat frame, Detection detection)
    {
        var rect = new Rect(
            (int)detection.BBox.X,
            (int)detection.BBox.Y,
            (int)detection.BBox.Width,
            (int)detection.BBox.Height
        );

        // バウンディングボックスの色を信頼度に基づいて決定
        var color = GetConfidenceColor(detection.Confidence);

        // バウンディングボックスを描画
        Cv2.Rectangle(frame, rect, color, 2);

        // ラベルと信頼度を描画
        DrawLabel(frame, detection, rect, color);
    }

    /// <summary>
    /// ラベルと信頼度を描画
    /// </summary>
    /// <param name="frame">描画対象のフレーム</param>
    /// <param name="detection">検出結果</param>
    /// <param name="rect">バウンディングボックス</param>
    /// <param name="color">描画色</param>
    private static void DrawLabel(Mat frame, Detection detection, Rect rect, Scalar color)
    {
        var label = $"{detection.ClassName}: {detection.Confidence:F2}";
        var font = HersheyFonts.HersheySimplex;
        var fontScale = 0.6;
        var thickness = 1;

        // テキストサイズを取得
        var textSize = Cv2.GetTextSize(label, font, fontScale, thickness, out int baseline);

        // テキスト背景の矩形を計算
        var textRect = new Rect(
            rect.X,
            rect.Y - textSize.Height - baseline - 5,
            textSize.Width + 10,
            textSize.Height + baseline + 5
        );

        // テキスト背景を描画
        Cv2.Rectangle(frame, textRect, color, -1);

        // テキストを描画（黒色）
        Cv2.PutText(
            frame,
            label,
            new Point(rect.X + 5, rect.Y - 5),
            font,
            fontScale,
            new Scalar(0, 0, 0), // 黒色
            thickness
        );
    }

    /// <summary>
    /// 検出数をフレームに描画
    /// </summary>
    /// <param name="frame">描画対象のフレーム</param>
    /// <param name="detectionCount">検出数</param>
    private static void DrawDetectionCount(Mat frame, int detectionCount)
    {
        var infoText = $"Detections: {detectionCount}";
        var font = HersheyFonts.HersheySimplex;
        var fontScale = 0.8;
        var thickness = 2;

        // 背景色（半透明の黒）
        var bgColor = new Scalar(0, 0, 0, 128);
        var textColor = new Scalar(255, 255, 255); // 白色

        // テキストサイズを取得
        var textSize = Cv2.GetTextSize(infoText, font, fontScale, thickness, out int baseline);

        // 背景矩形を描画
        var bgRect = new Rect(10, 10, textSize.Width + 20, textSize.Height + baseline + 10);
        Cv2.Rectangle(frame, bgRect, new Scalar(0, 0, 0), -1); // 不透明な黒背景

        // テキストを描画
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
    /// 信頼度に基づいて色を決定
    /// </summary>
    /// <param name="confidence">信頼度</param>
    /// <returns>描画色</returns>
    private static Scalar GetConfidenceColor(float confidence)
    {
        // 信頼度に基づいて色を変更
        // 高い信頼度: 緑色
        // 中程度の信頼度: 黄色
        // 低い信頼度: 赤色
        if (confidence >= 0.8f)
        {
            return new Scalar(0, 255, 0); // 緑色
        }
        else if (confidence >= 0.6f)
        {
            return new Scalar(0, 255, 255); // 黄色
        }
        else
        {
            return new Scalar(0, 0, 255); // 赤色
        }
    }
}
