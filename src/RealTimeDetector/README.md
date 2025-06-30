# RealTimeDetector

YOLOモデルを使用したリアルタイム検出システム（物体検出・顔検出対応）

## 対応機能

- **物体検出**: YOLOv8/YOLOv11による一般物体検出
- **顔検出**: YOLOv8n-faceによる顔検出・ランドマーク表示

## ビルド・実行

```bash
# ビルド
dotnet build

# 物体検出モード
./bin/Debug/net8.0/RealTimeDetector --model /path/to/yolo.onnx --mode objects

# 顔検出モード（ランドマーク付き）
./bin/Debug/net8.0/RealTimeDetector --model /path/to/yolov8n-face.onnx --mode faces

# カメラテストのみ
./bin/Debug/net8.0/RealTimeDetector
```

## オプション

- `--model`: モデルファイル（.onnx）のパス
- `--mode`: 検出モード（`objects` または `faces`、デフォルト: objects）
- `--confidence`: 信頼度閾値（デフォルト: 0.5）
- `--nms`: NMS閾値（デフォルト: 0.4）
- `--camera`: カメラインデックス（デフォルト: 0）

## ランドマーク表示

顔検出モードでは以下のランドマークを表示：
- 🔵 目（青色）
- 🔴 鼻（赤色）
- 🟡 口（黄色）

'q'キーまたはESCキーで終了
