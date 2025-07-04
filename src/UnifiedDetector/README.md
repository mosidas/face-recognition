# UnifiedDetector

物体検出と顔認証を同時に実行する統合アプリケーション

## 概要

RealTimeDetectorとRealTimeFaceRecognizerの機能を統合し、一つのアプリケーションで以下を同時に実行できます：

- **物体検出**: YOLOモデルによる一般物体検出
- **顔認証**: 顔検出 + 参照画像との照合・識別

## 主要機能

### 1. 統合処理
- 物体検出と顔認証を並列実行
- 異なる色分けで結果を区別表示
- リアルタイムパフォーマンス統計

### 2. 柔軟な設定
- 各機能の個別有効/無効切り替え
- 複数の信頼度閾値設定
- カメラインデックス指定

### 3. 高度な表示機能
- 物体検出結果（緑色）
- 顔認証結果（青/黄/赤で信頼度別色分け）
- ランドマーク表示
- 統計情報（画面下部）

## ビルド・実行

### ビルド
```bash
dotnet build src/UnifiedDetector/UnifiedDetector.csproj
```

### 基本実行
```bash
# 顔認証のみ（最小構成）
dotnet run --project src/UnifiedDetector -- \
  --face-detector models/yolov8n-face.onnx \
  --face-recognizer models/face_recognizer.onnx

# 物体検出も追加
dotnet run --project src/UnifiedDetector -- \
  --object-model models/yolov8n.onnx \
  --face-detector models/yolov8n-face.onnx \
  --face-recognizer models/face_recognizer.onnx

# 参照顔画像フォルダを指定
dotnet run --project src/UnifiedDetector -- \
  --object-model models/yolov8n.onnx \
  --face-detector models/yolov8n-face.onnx \
  --face-recognizer models/face_recognizer.onnx \
  --face-images ./reference_faces
```

### 高度なオプション
```bash
dotnet run --project src/UnifiedDetector -- \
  --object-model models/yolov8n.onnx \
  --face-detector models/yolov8n-face.onnx \
  --face-recognizer models/face_recognizer.onnx \
  --face-images ./reference_faces \
  --confidence 0.6 \
  --recognition-threshold 0.7 \
  --camera 1 \
  --enable-objects true \
  --enable-faces true
```

## コマンドラインオプション

| オプション | 説明 | デフォルト値 |
|-----------|------|-------------|
| `--object-model` | 物体検出モデルのパス（オプション） | なし |
| `--face-detector` | 顔検出モデルのパス（必須） | なし |
| `--face-recognizer` | 顔認証モデルのパス（必須） | なし |
| `--face-images` | 参照顔画像フォルダのパス（オプション） | なし |
| `--confidence` | 検出信頼度閾値 | 0.5 |
| `--recognition-threshold` | 顔認証類似度閾値 | 0.6 |
| `--camera` | カメラインデックス | 0 |
| `--enable-objects` | 物体検出の有効/無効 | true |
| `--enable-faces` | 顔認証の有効/無効 | true |

## 参照顔画像の準備

参照顔画像フォルダに人物の顔画像を配置します：

```
reference_faces/
├── 田中太郎.jpg
├── 佐藤花子.png
└── 山田次郎.jpeg
```

- ファイル名が人物名として使用されます
- 対応形式: .jpg, .jpeg, .png, .bmp
- 1ファイル1人（最も大きい顔を自動選択）

## 表示内容

### 物体検出結果
- **緑色の矩形**: 検出された物体
- **ラベル**: クラス名と信頼度

### 顔認証結果
- **青色の矩形**: 高信頼度の既知人物（類似度 ≥ 0.6）
- **黄色の矩形**: 中信頼度（類似度 ≥ 0.4）
- **赤色の矩形**: 低信頼度・未知人物
- **ランドマーク**: 目（シアン）、鼻（赤）、口（黄）
- **ラベル**: 人物名と類似度、顔の角度

### 統計情報
- 検出物体数
- 検出顔数
- 既知人物数

## 操作方法

- **'q' または 'Q'**: 終了
- **ESC**: 終了

## 技術仕様

### アーキテクチャ
- **並列処理**: 物体検出と顔認証を同時実行
- **非同期処理**: すべてのONNX推論でasync/await使用
- **統合描画**: 一つのウィンドウで全結果を表示

### パフォーマンス
- **GPU加速**: CUDA自動利用（利用可能時）
- **効率的描画**: 色分けによる視覚的識別
- **リアルタイム統計**: FPS、検出数の表示

### 拡張性
- 新しい検出モデルの追加が容易
- 表示スタイルのカスタマイズ可能
- 追加認証アルゴリズムの統合可能

## 使用例

### セキュリティ監視
```bash
# 人物識別 + 持ち物検出
dotnet run --project src/UnifiedDetector -- \
  --object-model security_yolo.onnx \
  --face-detector face_detector.onnx \
  --face-recognizer face_recognizer.onnx \
  --face-images ./authorized_personnel \
  --recognition-threshold 0.8
```

### 店舗分析
```bash
# 顧客識別 + 商品検出
dotnet run --project src/UnifiedDetector -- \
  --object-model product_yolo.onnx \
  --face-detector face_detector.onnx \
  --face-recognizer face_recognizer.onnx \
  --face-images ./customer_database \
  --confidence 0.7
```

'q'キーまたはESCキーで終了