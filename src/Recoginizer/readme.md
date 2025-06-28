# ONNX推論ライブラリ - 物体検出と顔認証

.NETでONNXモデルを使用した物体検出と顔認証を実行するためのライブラリです。

## 機能

- **物体検出**（YOLO）
  - YOLOv5/v8モデルのサポート
  - 複数物体の同時検出
  - NMS（Non-Maximum Suppression）による重複除去
  - バウンディングボックスの描画

- **顔認証**
  - 顔検出（RetinaFace、MTCNNなど）
  - 顔特徴抽出（FaceNet、ArcFaceなど）
  - 1:1照合（2つの顔画像の比較）
  - 1:N識別（データベースからの検索）

## 必要なパッケージ

```xml
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.20.1" />
<PackageReference Include="OpenCvSharp4" Version="4.10.0.20241108" />
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.6" />
```

## 使用方法

### 1. 物体検出（YOLO）

```csharp
// YOLODetectorの初期化
using var detector = new YoloDetector(
    "yolov5s.onnx",                    // モデルファイルパス
    CocoClassNames.Names,               // クラス名の配列
    confidenceThreshold: 0.5f,          // 信頼度の閾値
    nmsThreshold: 0.5f                  // NMSの閾値
);

// 物体検出の実行
var detections = await detector.DetectAsync("image.jpg");

// 結果の表示
foreach (var detection in detections)
{
    Console.WriteLine($"{detection.ClassName}: {detection.Confidence:F2}");
    Console.WriteLine($"位置: {detection.BBox}");
}
```

### 2. 顔認証

#### 顔照合（1:1）

```csharp
// FaceRecognizerの初期化
var recognizer = new FaceRecognizer(
    "face_detector.onnx",               // 顔検出モデル
    "face_recognizer.onnx",             // 顔認識モデル
    detectionThreshold: 0.7f,           // 検出閾値
    recognitionThreshold: 0.6f          // 認識閾値
);

// 2つの画像の顔を比較
var result = await recognizer.VerifyFaceAsync("face1.jpg", "face2.jpg");
Console.WriteLine($"同一人物: {result.IsMatch}");
Console.WriteLine($"類似度: {result.Confidence:F3}");
```

#### 顔識別（1:N）

```csharp
// データベースの作成
var database = new FaceDatabase();

// 顔の登録
var faces = await recognizer.DetectFacesAsync("person1.jpg");
if (faces.Count > 0)
{
    var embedding = await recognizer.ExtractFaceEmbeddingAsync(
        "person1.jpg", 
        faces[0].BBox
    );
    database.RegisterFace("001", "田中太郎", embedding);
}

// 顔の識別
var queryFaces = await recognizer.DetectFacesAsync("query.jpg");
if (queryFaces.Count > 0)
{
    var queryEmbedding = await recognizer.ExtractFaceEmbeddingAsync(
        "query.jpg", 
        queryFaces[0].BBox
    );
    var result = database.IdentifyFace(queryEmbedding);
    Console.WriteLine($"識別結果: {result.Name} (信頼度: {result.Confidence:F3})");
}
```

## サンプルプログラム

`Program.cs`にインタラクティブなサンプルプログラムが含まれています。

```bash
dotnet run
```

実行すると、物体検出または顔認証のデモを選択できます。

## モデルの入手方法

### YOLOモデル
- [YOLOv5](https://github.com/ultralytics/yolov5) - `export.py`でONNX形式にエクスポート
- [YOLOv8](https://github.com/ultralytics/ultralytics) - `model.export(format='onnx')`

### 顔認証モデル
- [ONNX Model Zoo](https://github.com/onnx/models) - 事前学習済みモデル
- [InsightFace](https://github.com/deepinsight/insightface) - ArcFaceなど

## カスタマイズ

### 画像の前処理

デフォルトでは0-255の値を0-1に正規化しますが、モデルによっては異なる正規化が必要な場合があります。
`OnnxHelper.PreprocessImage`メソッドをオーバーライドして、カスタム前処理を実装できます。

### 出力の後処理

モデルの出力形式に応じて、`ParseYoloOutput`や`ParseFaceDetectorOutput`メソッドをカスタマイズできます。

## 注意事項

- GPUを使用する場合は、CUDA対応のONNX Runtimeが必要です
- 大きな画像の場合、メモリ使用量に注意してください
- モデルファイルのサイズが大きい場合、初回読み込みに時間がかかることがあります

## ライセンス

このプロジェクトはMITライセンスで公開されています。
使用するONNXモデルのライセンスについては、各モデルのライセンスを確認してください。
