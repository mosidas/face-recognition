namespace Recognizer;

public static class Constants
{
  public static class ImageProcessing
  {
    public const int YoloInputWidth = 640;
    public const int YoloInputHeight = 640;
    public const int FaceRecognitionInputSize = 224;
    public const float FacePaddingRatio = 0.2f;
    public const float NormalizationMaxValue = 255.0f;
    public const float DownscaleRatio = 0.333f; // 処理速度向上のための解像度縮小比率
  }

  public static class Thresholds
  {
    public const float DefaultFaceDetectionThreshold = 0.7f;
    public const float DefaultFaceRecognitionThreshold = 0.6f;
    public const float DefaultObjectDetectionThreshold = 0.5f;
    public const float DefaultNmsThreshold = 0.5f;
  }

  public static class YoloOutput
  {
    public const int BoundingBoxDimensions = 4;

    // YOLOv8n-face: [x, y, w, h, confidence]
    public const int Yolov8FaceOutputFeatures = 5;

    // YOLOv11-face: 転置出力形式
    public const int Yolov11FaceOutputFeatures = 5;
  }

  public static class Files
  {
    public const string ResultImageSuffix = "_result.jpg";
  }
}

/// <summary>
/// YOLOモデルタイプ
/// </summary>
public enum YoloFaceModelType
{
  /// <summary>
  /// 自動判別（推奨）
  /// </summary>
  Auto,

  /// <summary>
  /// YOLOv8n-face形式
  /// </summary>
  Yolov8n,

  /// <summary>
  /// YOLOv11-face形式（転置出力）
  /// </summary>
  Yolov11
}
