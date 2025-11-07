using System.Drawing;

namespace Recognizer;

/// <summary>
/// 顔の角度情報（オイラー角）
/// </summary>
public sealed record FaceAngles(
    float Roll,     // ロール角（Z軸回転、-180°～180°）
    float Pitch,    // ピッチ角（X軸回転、-90°～90°）
    float Yaw);     // ヨー角（Y軸回転、-90°～90°）

/// <summary>
/// 顔のランドマークから顔の角度を計算するクラス
/// </summary>
public static class FaceAngleCalculator
{
  /// <summary>
  /// ランドマークから顔の角度を計算
  /// </summary>
  /// <param name="landmarks">顔のランドマーク座標</param>
  /// <returns>計算された顔の角度</returns>
  public static FaceAngles CalculateAngles(FaceLandmarks landmarks)
  {
    var roll = CalculateRoll(landmarks);
    var pitch = CalculatePitch(landmarks);
    var yaw = CalculateYaw(landmarks);

    return new FaceAngles(roll, pitch, yaw);
  }

  /// <summary>
  /// ロール角の計算（頭の傾き、Z軸回転）
  /// 左右の目を結ぶ直線の水平からの傾きで計算
  /// </summary>
  private static float CalculateRoll(FaceLandmarks landmarks)
  {
    var leftEye = landmarks.LeftEye;
    var rightEye = landmarks.RightEye;

    // 目を結ぶベクトル
    PointF eyeVector = new(rightEye.X - leftEye.X, rightEye.Y - leftEye.Y);

    // 水平軸（X軸）との角度を計算
    var rollRadians = Math.Atan2(eyeVector.Y, eyeVector.X);
    var rollDegrees = (float)(rollRadians * 180.0 / Math.PI);

    // -180°～180°の範囲に正規化
    if (rollDegrees > 180)
    {
      rollDegrees -= 360;
    }

    if (rollDegrees < -180)
    {
      rollDegrees += 360;
    }

    return rollDegrees;
  }

  /// <summary>
  /// ピッチ角の計算（頭の上下の向き、X軸回転）
  /// 目と口の垂直位置関係から推定
  /// </summary>
  private static float CalculatePitch(FaceLandmarks landmarks)
  {
    // 両目の中点
    PointF eyeCenter = new(
        (landmarks.LeftEye.X + landmarks.RightEye.X) / 2,
        (landmarks.LeftEye.Y + landmarks.RightEye.Y) / 2);

    // 両口角の中点
    PointF mouthCenter = new(
        (landmarks.LeftMouth.X + landmarks.RightMouth.X) / 2,
        (landmarks.LeftMouth.Y + landmarks.RightMouth.Y) / 2);

    var nose = landmarks.Nose;

    // 目と鼻の距離
    var eyeToNoseDistance = Math.Sqrt(
        Math.Pow(nose.X - eyeCenter.X, 2) +
        Math.Pow(nose.Y - eyeCenter.Y, 2));

    // 鼻と口の距離
    var noseToMouthDistance = Math.Sqrt(
        Math.Pow(mouthCenter.X - nose.X, 2) +
        Math.Pow(mouthCenter.Y - nose.Y, 2));

    // 正面向きの場合の理想的な比率（経験値ベース）
    const float idealEyeNoseToNoseMouthRatio = 1.2f;

    if (eyeToNoseDistance > 0 && noseToMouthDistance > 0)
    {
      var currentRatio = (float)(eyeToNoseDistance / noseToMouthDistance);
      var ratioDeviation = currentRatio - idealEyeNoseToNoseMouthRatio;

      // 比率の変化をピッチ角に変換（経験値ベース）
      const float pitchScaleFactor = 30.0f;
      var pitchDegrees = ratioDeviation * pitchScaleFactor;

      // -90°～90°の範囲に制限
      //pitchDegrees = Math.Max(-90, Math.Min(90, pitchDegrees));

      return pitchDegrees;
    }

    return 0.0f; // 計算できない場合はニュートラル
  }

  /// <summary>
  /// ヨー角の計算（顔の左右の向き、Y軸回転）
  /// 目と鼻の位置関係から推定
  /// </summary>
  private static float CalculateYaw(FaceLandmarks landmarks)
  {
    var leftEye = landmarks.LeftEye;
    var rightEye = landmarks.RightEye;
    var nose = landmarks.Nose;

    // 両目の中点
    PointF eyeCenter = new(
        (leftEye.X + rightEye.X) / 2,
        (leftEye.Y + rightEye.Y) / 2);

    // 目の中点から鼻への横方向のオフセット
    var horizontalOffset = nose.X - eyeCenter.X;

    // 目の間の距離
    var eyeDistance = Math.Sqrt(
        Math.Pow(rightEye.X - leftEye.X, 2) +
        Math.Pow(rightEye.Y - leftEye.Y, 2));

    if (eyeDistance > 0)
    {
      // 横方向オフセットを目の間隔で正規化
      var normalizedOffset = horizontalOffset / eyeDistance;

      // 正規化されたオフセットをヨー角に変換（経験値ベース）
      const float yawScaleFactor = 60.0f;
      var yawDegrees = (float)(normalizedOffset * yawScaleFactor);

      // -90°～90°の範囲に制限
      //yawDegrees = Math.Max(-90, Math.Min(90, yawDegrees));

      return yawDegrees;
    }

    return 0.0f; // 計算できない場合はニュートラル
  }

  /// <summary>
  /// 角度をより詳細に計算（改良版）
  /// より多くのランドマーク情報を活用
  /// </summary>
  public static FaceAngles CalculateAdvancedAngles(FaceLandmarks landmarks)
  {
    var roll = CalculateAdvancedRoll(landmarks);
    var pitch = CalculateAdvancedPitch(landmarks);
    var yaw = CalculateAdvancedYaw(landmarks);

    return new FaceAngles(roll, pitch, yaw);
  }

  /// <summary>
  /// 改良版ロール角計算
  /// 目だけでなく口の角度も考慮
  /// </summary>
  private static float CalculateAdvancedRoll(FaceLandmarks landmarks)
  {
    // 目による角度
    var eyeRoll = CalculateRoll(landmarks);

    // 口による角度
    var leftMouth = landmarks.LeftMouth;
    var rightMouth = landmarks.RightMouth;
    PointF mouthVector = new(rightMouth.X - leftMouth.X, rightMouth.Y - leftMouth.Y);
    var mouthRollRadians = Math.Atan2(mouthVector.Y, mouthVector.X);
    var mouthRoll = (float)(mouthRollRadians * 180.0 / Math.PI);

    // 目と口の角度の重み付き平均（目の方が安定）
    const float eyeWeight = 0.7f;
    const float mouthWeight = 0.3f;
    var weightedRoll = eyeRoll * eyeWeight + mouthRoll * mouthWeight;

    // -180°～180°の範囲に正規化
    if (weightedRoll > 180)
    {
      weightedRoll -= 360;
    }

    if (weightedRoll < -180)
    {
      weightedRoll += 360;
    }

    return weightedRoll;
  }

  /// <summary>
  /// 改良版ピッチ角計算
  /// より精密な特徴点間距離分析
  /// </summary>
  private static float CalculateAdvancedPitch(FaceLandmarks landmarks)
  {
    var leftEye = landmarks.LeftEye;
    var rightEye = landmarks.RightEye;
    var nose = landmarks.Nose;
    var leftMouth = landmarks.LeftMouth;
    var rightMouth = landmarks.RightMouth;

    // 両目の中点
    PointF eyeCenter = new((leftEye.X + rightEye.X) / 2, (leftEye.Y + rightEye.Y) / 2);

    // 両口角の中点
    PointF mouthCenter = new((leftMouth.X + rightMouth.X) / 2, (leftMouth.Y + rightMouth.Y) / 2);

    // 縦方向の特徴点間距離
    var eyeToNoseY = Math.Abs(nose.Y - eyeCenter.Y);
    var noseToMouthY = Math.Abs(mouthCenter.Y - nose.Y);
    var eyeToMouthY = Math.Abs(mouthCenter.Y - eyeCenter.Y);

    // 全体の顔の高さ
    var faceHeight = eyeToMouthY;

    if (faceHeight > 0)
    {
      // 上部と下部の比率
      var upperRatio = eyeToNoseY / faceHeight;
      var lowerRatio = noseToMouthY / faceHeight;

      // 理想的な比率（正面向き時の経験値）
      const float idealUpperFaceRatio = 0.4f;
      const float idealLowerFaceRatio = 0.6f;

      // 比率の逸脱からピッチを推定
      var upperDeviation = upperRatio - idealUpperFaceRatio;
      var lowerDeviation = lowerRatio - idealLowerFaceRatio;

      // 上下の逸脱を統合してピッチ角を計算
      var pitchDegrees = (upperDeviation - lowerDeviation) * 100.0f;

      // -90°～90°の範囲に制限
      //return Math.Max(-90, Math.Min(90, pitchDegrees));
      return pitchDegrees;
    }

    return 0.0f;
  }

  /// <summary>
  /// 改良版ヨー角計算
  /// 非対称性分析による精度向上
  /// </summary>
  private static float CalculateAdvancedYaw(FaceLandmarks landmarks)
  {
    var leftEye = landmarks.LeftEye;
    var rightEye = landmarks.RightEye;
    var nose = landmarks.Nose;
    var leftMouth = landmarks.LeftMouth;
    var rightMouth = landmarks.RightMouth;

    // 顔の中心軸（目の中点と口の中点を結ぶ線）
    PointF eyeCenter = new((leftEye.X + rightEye.X) / 2, (leftEye.Y + rightEye.Y) / 2);
    PointF mouthCenter = new((leftMouth.X + rightMouth.X) / 2, (leftMouth.Y + rightMouth.Y) / 2);

    // 顔の中心線のX座標
    var faceCenterX = (eyeCenter.X + mouthCenter.X) / 2;

    // 鼻の位置による非対称性
    var noseOffset = nose.X - faceCenterX;

    // 目の非対称性（左右の目の見え方の違い）
    var leftEyeToCenter = Math.Abs(leftEye.X - faceCenterX);
    var rightEyeToCenter = Math.Abs(rightEye.X - faceCenterX);
    var eyeAsymmetry = rightEyeToCenter - leftEyeToCenter;

    // 口の非対称性
    var leftMouthToCenter = Math.Abs(leftMouth.X - faceCenterX);
    var rightMouthToCenter = Math.Abs(rightMouth.X - faceCenterX);
    var mouthAsymmetry = rightMouthToCenter - leftMouthToCenter;

    // 顔の幅（目の間の距離）
    var faceWidth = Math.Abs(rightEye.X - leftEye.X);

    if (faceWidth > 0)
    {
      // 各要素を顔の幅で正規化
      var normalizedNoseOffset = noseOffset / faceWidth;
      var normalizedEyeAsymmetry = eyeAsymmetry / faceWidth;
      var normalizedMouthAsymmetry = mouthAsymmetry / faceWidth;

      // 重み付き統合（鼻の位置が最も信頼性が高い）
      const float noseWeight = 0.5f;
      const float eyeAsymmetryWeight = 0.3f;
      const float mouthAsymmetryWeight = 0.2f;
      var combinedAsymmetry =
          normalizedNoseOffset * noseWeight +
          normalizedEyeAsymmetry * eyeAsymmetryWeight +
          normalizedMouthAsymmetry * mouthAsymmetryWeight;

      // ヨー角への変換（経験値ベース）
      const float advancedYawScaleFactor = 80.0f;
      var yawDegrees = combinedAsymmetry * advancedYawScaleFactor;

      // -90°～90°の範囲に制限
      //return Math.Max(-90, Math.Min(90, yawDegrees));
      return yawDegrees;
    }

    return 0.0f;
  }
}
