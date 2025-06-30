using OpenCvSharp;
using Recognizer;

namespace RealTimeDetector;

/// <summary>
/// 顔検出結果の描画（ランドマーク対応）
/// </summary>
public static class FaceDetectionRenderer
{
    /// <summary>
    /// 顔検出結果を描画
    /// </summary>
    public static void DrawFaceDetections(Mat frame, List<FaceDetection> faces)
    {
        foreach (var face in faces)
        {
            DrawSingleFaceDetection(frame, face);
        }

        DrawFaceCount(frame, faces.Count);
    }

    /// <summary>
    /// 単一顔検出結果の描画
    /// </summary>
    private static void DrawSingleFaceDetection(Mat frame, FaceDetection face)
    {
        // バウンディングボックス描画
        var rect = new Rect(face.BBox.X, face.BBox.Y, face.BBox.Width, face.BBox.Height);
        var faceColor = GetConfidenceColor(face.Confidence);

        Cv2.Rectangle(frame, rect, faceColor, 2);
        DrawFaceLabel(frame, face, rect, faceColor);

        // ランドマーク描画（YOLOv8n-faceの場合）
        if (face.Landmarks != null)
        {
            DrawLandmarks(frame, face.Landmarks);
        }
    }

    /// <summary>
    /// 顔ラベルの描画
    /// </summary>
    private static void DrawFaceLabel(Mat frame, FaceDetection face, Rect rect, Scalar color)
    {
        var label = $"Face: {face.Confidence:F2}";
        var font = HersheyFonts.HersheySimplex;
        var fontScale = 0.6;
        var thickness = 1;

        var textSize = Cv2.GetTextSize(label, font, fontScale, thickness, out int baseline);

        var textRect = new Rect(
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
            new Scalar(255, 255, 255),
            thickness
        );
    }

    /// <summary>
    /// ランドマーク座標の描画
    /// </summary>
    private static void DrawLandmarks(Mat frame, FaceLandmarks landmarks)
    {
        // 目の描画（青色）
        var eyeColor = new Scalar(255, 0, 0); // 青
        var eyeRadius = 3;
        Cv2.Circle(frame, new Point((int)landmarks.LeftEye.X, (int)landmarks.LeftEye.Y), eyeRadius, eyeColor, -1);
        Cv2.Circle(frame, new Point((int)landmarks.RightEye.X, (int)landmarks.RightEye.Y), eyeRadius, eyeColor, -1);

        // 鼻の描画（赤色）
        var noseColor = new Scalar(0, 0, 255); // 赤
        var noseRadius = 3;
        Cv2.Circle(frame, new Point((int)landmarks.Nose.X, (int)landmarks.Nose.Y), noseRadius, noseColor, -1);

        // 口の描画（黄色）
        var mouthColor = new Scalar(0, 255, 255); // 黄色
        var mouthRadius = 3;
        Cv2.Circle(frame, new Point((int)landmarks.LeftMouth.X, (int)landmarks.LeftMouth.Y), mouthRadius, mouthColor, -1);
        Cv2.Circle(frame, new Point((int)landmarks.RightMouth.X, (int)landmarks.RightMouth.Y), mouthRadius, mouthColor, -1);

        // ランドマーク間の線の描画（視覚的分かりやすさ向上）
        var lineColor = new Scalar(128, 128, 128); // グレー
        var lineThickness = 1;

        // 目と鼻を結ぶ線
        Cv2.Line(frame,
            new Point((int)landmarks.LeftEye.X, (int)landmarks.LeftEye.Y),
            new Point((int)landmarks.Nose.X, (int)landmarks.Nose.Y),
            lineColor, lineThickness);
        Cv2.Line(frame,
            new Point((int)landmarks.RightEye.X, (int)landmarks.RightEye.Y),
            new Point((int)landmarks.Nose.X, (int)landmarks.Nose.Y),
            lineColor, lineThickness);

        // 口の両端を結ぶ線
        Cv2.Line(frame,
            new Point((int)landmarks.LeftMouth.X, (int)landmarks.LeftMouth.Y),
            new Point((int)landmarks.RightMouth.X, (int)landmarks.RightMouth.Y),
            lineColor, lineThickness);
    }

    /// <summary>
    /// 検出顔数の表示
    /// </summary>
    private static void DrawFaceCount(Mat frame, int faceCount)
    {
        var infoText = $"Faces: {faceCount}";
        var font = HersheyFonts.HersheySimplex;
        var fontScale = 0.8;
        var thickness = 2;

        var textColor = new Scalar(255, 255, 255);

        var textSize = Cv2.GetTextSize(infoText, font, fontScale, thickness, out int baseline);

        // 情報の視認性確保のため背景付き表示
        var bgRect = new Rect(10, 10, textSize.Width + 20, textSize.Height + baseline + 10);
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
            return new Scalar(0, 255, 0); // 緑
        }
        else if (confidence >= 0.6f)
        {
            return new Scalar(0, 255, 255); // 黄
        }
        else
        {
            return new Scalar(0, 0, 255); // 赤
        }
    }
}
