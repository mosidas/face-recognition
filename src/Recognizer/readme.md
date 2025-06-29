# Recognizer

ONNXモデルを使用した物体検出と顔認証のライブラリ

## ビルド

```bash
dotnet build
```

## 使用例

```csharp
// 物体検出
using var detector = new YoloDetector("yolov5s.onnx", CocoClassNames.Names);
var detections = await detector.DetectAsync("image.jpg");

// 顔認証
var recognizer = new FaceRecognizer("face_detector.onnx", "face_recognizer.onnx");
var result = await recognizer.VerifyFaceAsync("face1.jpg", "face2.jpg");
```

## 主要クラス

- **YoloDetector**: 物体検出
- **FaceRecognizer**: 顔認証
- **OnnxHelper**: ONNX推論サポート
