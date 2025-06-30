# Recognizer

ONNXモデルを使用した物体検出と顔認証のライブラリ

## ビルド

```bash
dotnet build
```

## 使用例

### 物体検出
```csharp
using var detector = new YoloDetector("yolov5s.onnx", CocoClassNames.Names);
var detections = await detector.DetectAsync("image.jpg");
```

### 顔認証（YOLOv8n-face / YOLOv11-face対応）
```csharp
// 自動判別（推奨）
var recognizer = new FaceRecognizer("yolov8n-face.onnx", "face_recognizer.onnx");

// モデルタイプ指定
var recognizer = new FaceRecognizer(
    "yolov8n-face.onnx", 
    "face_recognizer.onnx",
    modelType: YoloFaceModelType.Yolov8n
);

// 顔認証実行
var result = await recognizer.VerifyFaceAsync(image1, image2);
```

### 専用顔検出器
```csharp
// YOLOv8n-face専用検出器
using var faceDetector = new YoloFaceDetector("yolov8n-face.onnx");
var faces = await faceDetector.DetectAsync("image.jpg");
```

## 主要クラス

- **YoloDetector**: 物体検出
- **FaceRecognizer**: 顔認証（YOLOv8n/v11対応）
- **YoloFaceDetector**: YOLO顔検出専用
- **OnnxHelper**: ONNX推論サポート

## 対応モデル

- **YOLOv8n-face**: 標準出力形式 [1, 25200, 5]
- **YOLOv11-face**: 転置出力形式 [1, 5, 8400]
- 自動判別によりシームレス切り替え
