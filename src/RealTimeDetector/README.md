# RealTimeDetector

YOLOモデルを使用したリアルタイム物体検出システム

## ビルド・実行

```bash
# ビルド
dotnet build

# 物体検出実行
./bin/Debug/net8.0/RealTimeDetector --model /path/to/model.onnx

# カメラテストのみ
./bin/Debug/net8.0/RealTimeDetector
```

## オプション

- `--model`: YOLOモデルファイル（.onnx）のパス
- `--confidence`: 信頼度閾値（デフォルト: 0.5）
- `--nms`: NMS閾値（デフォルト: 0.4）
- `--camera`: カメラインデックス（デフォルト: 0）

'q'キーまたはESCキーで終了
