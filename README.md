# face-recognition

![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## 概要

ONNXモデルを使用したリアルタイム物体検出と顔認証システム

## プロジェクト構成

### コアライブラリ
- [Recognizer](./src/Recognizer/readme.md) - 物体検出・顔認証ライブラリ

### アプリケーション
- [ExampleCLI](./src/ExampleCLI/README.md) - CLIサンプル（YOLOv8n-face対応）
- [RealTimeDetector](./src/RealTimeDetector/README.md) - リアルタイム物体検出
- [RealTimeFaceRecognizer](./src/RealTimeFaceRecognizer/README.md) - リアルタイム顔認識
- [UnifiedDetector](./src/UnifiedDetector/README.md) - 統合検出・認証システム（スマートフォン認証機能付き）
- [WPFDetectorApp](./src/WPFDetectorApp/README.md) - WPF UI付き赤外線カメラ統合検出システム

## クイックスタート

### ビルド
```bash
dotnet build
```

### 統合システム実行例
```bash
# コンソール版統合検出
dotnet run --project src/UnifiedDetector/UnifiedDetector.csproj -- \
  --face-detector models/yolo11n-face.onnx \
  --face-recognizer models/face_recognition.onnx \
  --object-model models/yolo11n.onnx \
  --face-images reference_faces/ \
  --recognition-threshold 0.4

# WPF UI版統合検出
dotnet run --project src/WPFDetectorApp/WPFDetectorApp.csproj
```

```bash
dotnet run --project src/UnifiedDetector/UnifiedDetector.csproj --face-detector .local/models/yolov11n-face.onnx --face-recognizer .local/models/arcface.onnx --object-model .local/models/yolo11n.onnx --face-images .local/assets/face01 --camera 1

dotnet run --project src/UnifiedDetector/UnifiedDetector.csproj --face-detector .local/models/yolov11n-face.onnx --face-recognizer .local/models/arcface.onnx --object-model .local/models/yolov3-12-int8.onnx --face-images .local/assets/face01 --camera 0
```

### 単一ファイル配布
```bash
# WPF統合検出システムの単一ファイル配布
dotnet publish src/WPFDetectorApp/WPFDetectorApp.csproj -c Release
```

## ドキュメント

- [システム仕様書](./docs/system-specifications.md) - 詳細な技術仕様と機能説明

## 主要機能

- YOLOv8n-face/YOLOv11-face自動対応
- リアルタイム顔認識（roll, pitch, yaw角度表示）
- 人物ベーススマートフォン認証
- GPU加速対応（CUDA）
- 5点顔ランドマーク検出
- WPF UI付き赤外線カメラ対応
- 単一ファイル配布対応

## ライセンス

MIT License
