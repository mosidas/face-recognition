# IRCameraUnifiedDetector

IRカメラ対応統合検出・認証システム

## 概要

Windows.Media.Captureを使用したIRカメラ対応のリアルタイム顔認証・物体検出システムです。通常のカメラ、IRカメラ、深度カメラを動的に切り替えながら、顔認証と物体検出を同時に実行できます。

## 主な機能

### カメラ機能
- **マルチカメラ対応**: 通常カメラ、IRカメラ、深度カメラの自動検出
- **動的切り替え**: 実行時にカメラソースを切り替え可能
- **IRカメラ最適化**: 赤外線カメラ用の専用描画処理
- **健全性チェック**: カメラストリームの自動復旧機能

### 検出・認証機能
- **顔検出**: YOLOv8n-face、YOLOv11n-face対応
- **顔認証**: ArcFace、JAPANESE_FACE_v1モデル対応
- **物体検出**: YOLOv3、YOLOv11n対応
- **スマートホン認証**: 認証済み人物のスマートホン使用許可判定
- **5点顔ランドマーク**: 目、鼻、口の位置検出
- **顔角度計算**: Roll、Pitch、Yaw角度表示

## 使用方法

### WPFアプリケーション（推奨）
```bash
# WPFアプリケーションの実行
dotnet run --project WPFDetectorApp/WPFDetectorApp.csproj
```

**WPFアプリケーション機能:**
- **カメラ制御**: Start Camera/Stop Camera - カメラストリームの独立制御
- **検出制御**: Start Detection/Stop Detection - 検出・認証処理の独立制御
- **モデル設定**: UIからモデルファイルパスを指定可能
- **リアルタイム表示**: 5FPS制限でパフォーマンス最適化
- **カメラ切り替え**: ドロップダウンメニューまたはショートカットキー

### コマンドライン実行
```bash
dotnet run --project IRCameraUnifiedDetector.csproj -- \
  --face-detector models/yolov8n-face.onnx \
  --face-recognizer models/arcface.onnx \
  --object-model models/yolo11n.onnx \
  --face-images reference_faces/
```

### 高度な設定
```bash
dotnet run --project IRCameraUnifiedDetector.csproj -- \
  --face-detector models/yolov11n-face.onnx \
  --face-recognizer models/arcface.onnx \
  --object-model models/yolo11n.onnx \
  --face-images reference_faces/ \
  --recognition-threshold 0.6 \
  --window-name "IR顔認証システム"
```

### 機能別実行
```bash
# 顔認証のみ
dotnet run [...] --disable-object-detection

# 物体検出のみ
dotnet run [...] --disable-face-recognition

# IRカメラ専用（物体検出なし）
dotnet run [...] --disable-object-detection
```

## 操作方法

### WPFアプリケーション操作
1. **カメラ操作**:
   - `Start Camera`: カメラストリーム開始
   - `Stop Camera`: カメラストリーム停止
   - カメラドロップダウン: カメラソース選択

2. **検出操作**:
   - `Start Detection`: 検出・認証処理開始（カメラ開始後に有効）
   - `Stop Detection`: 検出・認証処理停止（カメラ映像は継続）
   - チェックボックス: Object Detection / Face Recognition の有効/無効

3. **モデル設定**:
   - `Load YOLO`: YOLOモデルファイルの読み込み
   - `Load Face Models`: 顔検出・認証モデルの読み込み
   - `Load Faces`: 参照顔画像フォルダの読み込み

### キーボードショートカット（コマンドライン版）
- **1**: IRカメラに切り替え
- **2**: 通常カメラに切り替え
- **3**: 深度カメラに切り替え
- **q/Q/ESC**: アプリケーション終了

### 画面表示
- **青い枠**: 高信頼度認証（類似度 ≥ 0.6）
- **黄色い枠**: 中信頼度認証（類似度 0.4-0.6）
- **赤い枠**: 低信頼度認証（類似度 < 0.4）
- **紫色い枠**: 認証済みスマートホン [AUTH]
- **オレンジ枠**: 未認証スマートホン [UNAUTH]

## 必要なモデルファイル

### 顔検出モデル
- `yolov8n-face.onnx` - YOLOv8n顔検出
- `yolov11n-face.onnx` - YOLOv11n顔検出（推奨）

### 顔認証モデル
- `arcface.onnx` - ArcFace顔認証（推奨）
- `JAPANESE_FACE_v1.onnx` - 日本人顔特化モデル

### 物体検出モデル
- `yolo11n.onnx` - YOLOv11n物体検出（推奨）
- `yolov3-12-int8.onnx` - YOLOv3軽量版

## 参照顔画像の準備

```
reference_faces/
├── person1.jpg
├── person2.jpg
├── person3.png
└── person4.bmp
```

- サポート形式: JPG, JPEG, PNG, BMP
- ファイル名が人物名として使用されます
- 1ファイル1人物で、顔がはっきり写っている画像を使用してください

## システム要件

### 必須要件
- Windows 10 19041 (20H1) 以降
- .NET 8.0 ランタイム
- Windows.Media.Capture対応カメラ

### 推奨要件
- IRカメラ対応Webカメラ
- NVIDIA GPU（CUDA加速対応）
- 8GB以上のメモリ

## IRカメラ最適化

### 専用機能
- **輝度強調**: IRカメラ映像の自動輝度調整
- **IRカメラ検出**: 赤外線照明制御の自動検出
- **カラー調整**: IR映像用の白黒階調表示
- **ノイズ対策**: IR特有のノイズに対応した前処理

### 対応カメラ
- Intel RealSense シリーズ
- Microsoft Kinect シリーズ
- Logitech Brio 4K シリーズ
- その他Windows.Media.Capture対応IRカメラ

## パフォーマンス情報

### 処理能力
- **フレームレート**: 30fps（1080p）
- **レスポンス時間**: < 100ms（顔認証）
- **メモリ使用量**: < 500MB（通常動作）

### 最適化設定
- GPUアクセラレーション有効
- マルチスレッド処理
- フレームバッファリング最適化

## トラブルシューティング

### カメラが認識されない
1. カメラドライバーの更新
2. Windowsカメラアプリでの動作確認
3. 管理者権限での実行

### 認証精度が低い
1. 参照画像の品質確認
2. 認識しきい値の調整 (--recognition-threshold)
3. 照明条件の改善

### パフォーマンスが低い
1. GPU使用率の確認
2. 解像度の調整
3. 不要な検出機能の無効化

## 技術仕様

### アーキテクチャ
- **UI**: WPF (Windows Presentation Foundation)
- **カメラ**: Windows.Media.Capture API
- **画像処理**: OpenCV Sharp
- **機械学習**: ONNX Runtime

### 対応フォーマット
- **入力**: BGRA8, RGB24, Grayscale
- **出力**: BGR24 (OpenCV標準)
- **深度**: 16bit depth map

## ライセンス

MIT License - 詳細はLICENSEファイルを参照してください。