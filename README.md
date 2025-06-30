# face-recognition

![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![License](https://img.shields.io/badge/license-MIT-green)

ONNXモデルを使用したリアルタイム物体検出と顔認証システム

## 主要機能

- **YOLOv8n-face / YOLOv11-face対応**: 自動判別による柔軟な顔検出
- **リアルタイム物体検出**: YOLOモデルを活用したライブカメラ処理
- **顔認証システム**: 高精度な顔照合・識別機能
- **モダンC#実装**: Primary Constructor、record型、CancellationToken対応

## プロジェクト構成

- [RealTimeDetector](./src/RealTimeDetector/README.md) - リアルタイム物体検出
- [ExampleCLI](./src/ExampleCLI/README.md) - CLIサンプル（YOLOv8n-face対応）
- [Recognizer](./src/Recognizer/README.md) - 物体検出・顔認証ライブラリ

## クイックスタート

```csharp
// YOLOv8n-face使用例
using var faceDetector = new YoloFaceDetector("yolov8n-face.onnx");
var faces = await faceDetector.DetectAsync("image.jpg");

// 顔認証（自動判別）
var recognizer = new FaceRecognizer("face_detector.onnx", "face_recognizer.onnx");
var result = await recognizer.VerifyFaceAsync(image1, image2);
```

## ライセンス

MIT License
