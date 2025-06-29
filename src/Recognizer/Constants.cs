namespace Recognizer;

/// <summary>
/// システム全体で使用する定数を定義
/// </summary>
public static class Constants
{
    /// <summary>
    /// 画像処理関連の定数
    /// </summary>
    public static class ImageProcessing
    {
        /// <summary>
        /// YOLOモデルの標準入力サイズ（幅）
        /// </summary>
        public const int YoloInputWidth = 640;

        /// <summary>
        /// YOLOモデルの標準入力サイズ（高さ）
        /// </summary>
        public const int YoloInputHeight = 640;

        /// <summary>
        /// 顔認識モデルの標準入力サイズ
        /// </summary>
        public const int FaceRecognitionInputSize = 224;

        /// <summary>
        /// 顔領域抽出時の余白の比率
        /// </summary>
        public const float FacePaddingRatio = 0.2f;

        /// <summary>
        /// 画像正規化の最大値（0-255の範囲を0-1に正規化）
        /// </summary>
        public const float NormalizationMaxValue = 255.0f;
    }

    /// <summary>
    /// 検出・認識の閾値
    /// </summary>
    public static class Thresholds
    {
        /// <summary>
        /// デフォルトの顔検出信頼度閾値
        /// </summary>
        public const float DefaultFaceDetectionThreshold = 0.7f;

        /// <summary>
        /// デフォルトの顔認識類似度閾値
        /// </summary>
        public const float DefaultFaceRecognitionThreshold = 0.6f;

        /// <summary>
        /// デフォルトの物体検出信頼度閾値
        /// </summary>
        public const float DefaultObjectDetectionThreshold = 0.5f;

        /// <summary>
        /// デフォルトのNMS閾値
        /// </summary>
        public const float DefaultNmsThreshold = 0.5f;
    }

    /// <summary>
    /// YOLOモデル出力の形状
    /// </summary>
    public static class YoloOutput
    {
        /// <summary>
        /// YOLOv11の予測数
        /// </summary>
        public const int PredictionCount = 8400;

        /// <summary>
        /// バウンディングボックスの座標数（x, y, w, h）
        /// </summary>
        public const int BoundingBoxDimensions = 4;

        /// <summary>
        /// COCOデータセットのクラス数
        /// </summary>
        public const int CocoClassCount = 80;

        /// <summary>
        /// 顔検出での特徴数（x, y, w, h, confidence）
        /// </summary>
        public const int FaceDetectionFeatureCount = 5;
    }

    /// <summary>
    /// ファイル関連の定数
    /// </summary>
    public static class Files
    {
        /// <summary>
        /// 一時ファイルの拡張子
        /// </summary>
        public const string TempImageExtension = ".jpg";

        /// <summary>
        /// 結果画像のサフィックス
        /// </summary>
        public const string ResultImageSuffix = "_result.jpg";
    }
}