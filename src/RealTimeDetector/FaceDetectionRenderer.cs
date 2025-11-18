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
        Rect rect = new(face.BBox.X, face.BBox.Y, face.BBox.Width, face.BBox.Height);
        var faceColor = GetConfidenceColor(face.Confidence);

        Cv2.Rectangle(frame, rect, faceColor, 2);
        DrawFaceLabel(frame, face, rect, faceColor);

        // ランドマーク描画（YOLOv8n-faceの場合）
        if (face.Landmarks != null)
        {
            DrawLandmarks(frame, face.Landmarks);
        }

        // 角度情報描画（YOLOv8n-faceの場合）
        if (face.Angles != null)
        {
            DrawFaceAngles(frame, face, rect);
        }
    }

    /// <summary>
    /// 顔ラベルの描画
    /// </summary>
    private static void DrawFaceLabel(Mat frame, FaceDetection face, Rect rect, Scalar color)
    {
        var label = $"Face: {face.Confidence:F2}";

        // 角度情報をラベルに追加
        if (face.Angles != null)
        {
            label += $" | R:{face.Angles.Roll:F0} P:{face.Angles.Pitch:F0} Y:{face.Angles.Yaw:F0}";
        }

        var font = HersheyFonts.HersheySimplex;
        var fontScale = 0.5; // 角度情報追加により少し小さくする
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
            new Scalar(255, 255, 255),
            thickness
        );
    }

    /// <summary>
    /// 顔の角度情報の描画
    /// </summary>
    private static void DrawFaceAngles(Mat frame, FaceDetection face, Rect faceRect)
    {
        if (face.Angles == null)
        {
            return;
        }

        var angles = face.Angles;

        // 角度情報のテキスト位置（顔の右下）
        Point angleTextPos = new(faceRect.Right + 5, faceRect.Bottom - 40);
        var font = HersheyFonts.HersheySimplex;
        var fontScale = 0.5f;
        var thickness = 1;
        Scalar textColor = new(255, 255, 255); // 白
        Scalar bgColor = new(0, 0, 0, 180); // 半透明黒

        // 角度情報テキスト
        var angleTexts = new[]
        {
            $"Roll: {angles.Roll:F1}",
            $"Pitch: {angles.Pitch:F1}",
            $"Yaw: {angles.Yaw:F1}"
        };

        // 背景矩形のサイズ計算
        var maxTextWidth = 0;
        var totalTextHeight = 0;
        var lineSpacing = 5;

        foreach (var text in angleTexts)
        {
            var textSize = Cv2.GetTextSize(text, font, fontScale, thickness, out var baseline);
            maxTextWidth = Math.Max(maxTextWidth, textSize.Width);
            totalTextHeight += textSize.Height + lineSpacing;
        }

        // 背景矩形描画
        Rect bgRect = new(
            angleTextPos.X - 5,
            angleTextPos.Y - totalTextHeight - 5,
            maxTextWidth + 10,
            totalTextHeight + 10
        );
        Cv2.Rectangle(frame, bgRect, bgColor, -1);

        // 各角度テキストを描画
        var currentY = angleTextPos.Y - totalTextHeight + 15;
        foreach (var text in angleTexts)
        {
            Cv2.PutText(frame, text, new Point(angleTextPos.X, currentY), font, fontScale, textColor, thickness);
            currentY += 18;
        }

        // 顔の向きを視覚的に表示（矢印で方向表示）
        DrawFaceOrientation(frame, face, faceRect);
    }

    /// <summary>
    /// 顔の向きを矢印で視覚的に表示
    /// </summary>
    private static void DrawFaceOrientation(Mat frame, FaceDetection face, Rect faceRect)
    {
        if (face.Angles == null)
        {
            return;
        }

        var angles = face.Angles;
        Point center = new(faceRect.X + faceRect.Width / 2, faceRect.Y + faceRect.Height / 2);

        // ヨー角による左右の向き（水平矢印）
        const float angleDisplayThreshold = 5.0f; // 5度以上で表示
        if (Math.Abs(angles.Yaw) > angleDisplayThreshold)
        {
            var yawLength = (int)(Math.Abs(angles.Yaw) * 1.5); // 角度に比例した長さ
            yawLength = Math.Min(yawLength, 50); // 最大長制限

            var yawEnd = angles.Yaw > 0
                ? new Point(center.X + yawLength, center.Y) // 右向き
                : new Point(center.X - yawLength, center.Y); // 左向き

            // ヨー矢印（青色）
            Cv2.ArrowedLine(frame, center, yawEnd, new Scalar(255, 0, 0), 2, tipLength: 0.3);
        }

        // ピッチ角による上下の向き（垂直矢印）
        if (Math.Abs(angles.Pitch) > angleDisplayThreshold)
        {
            var pitchLength = (int)(Math.Abs(angles.Pitch) * 1.5); // 角度に比例した長さ
            pitchLength = Math.Min(pitchLength, 50); // 最大長制限

            var pitchEnd = angles.Pitch > 0
                ? new Point(center.X, center.Y - pitchLength) // 上向き
                : new Point(center.X, center.Y + pitchLength); // 下向き

            // ピッチ矢印（緑色）
            Cv2.ArrowedLine(frame, center, pitchEnd, new Scalar(0, 255, 0), 2, tipLength: 0.3);
        }

        // ロール角による傾き（回転した線）
        if (Math.Abs(angles.Roll) > angleDisplayThreshold)
        {
            var rollLength = 30;
            var rollRadians = angles.Roll * Math.PI / 180.0;

            Point rollEnd = new(
                center.X + (int)(rollLength * Math.Cos(rollRadians)),
                center.Y + (int)(rollLength * Math.Sin(rollRadians))
            );

            // ロール線（赤色、少し太め）
            Cv2.Line(frame, center, rollEnd, new Scalar(0, 0, 255), 3);

            // ロール角度を表す小さな円弧（オプション）
            var arcRadius = 20;
            Scalar arcColor = new(0, 0, 255);
            Cv2.Ellipse(frame, center, new Size(arcRadius, arcRadius), 0, 0, angles.Roll, arcColor, 1);
        }
    }

    /// <summary>
    /// ランドマーク座標の描画
    /// </summary>
    private static void DrawLandmarks(Mat frame, FaceLandmarks landmarks)
    {
        // 目の描画（青色）
        Scalar eyeColor = new(255, 0, 0); // 青
        var eyeRadius = 3;
        Cv2.Circle(frame, new Point((int)landmarks.LeftEye.X, (int)landmarks.LeftEye.Y), eyeRadius, eyeColor, -1);
        Cv2.Circle(frame, new Point((int)landmarks.RightEye.X, (int)landmarks.RightEye.Y), eyeRadius, eyeColor, -1);

        // 鼻の描画（赤色）
        Scalar noseColor = new(0, 0, 255); // 赤
        var noseRadius = 3;
        Cv2.Circle(frame, new Point((int)landmarks.Nose.X, (int)landmarks.Nose.Y), noseRadius, noseColor, -1);

        // 口の描画（黄色）
        Scalar mouthColor = new(0, 255, 255); // 黄色
        var mouthRadius = 3;
        Cv2.Circle(frame, new Point((int)landmarks.LeftMouth.X, (int)landmarks.LeftMouth.Y), mouthRadius, mouthColor, -1);
        Cv2.Circle(frame, new Point((int)landmarks.RightMouth.X, (int)landmarks.RightMouth.Y), mouthRadius, mouthColor, -1);

        // ランドマーク間の線の描画（視覚的分かりやすさ向上）
        Scalar lineColor = new(128, 128, 128); // グレー
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
