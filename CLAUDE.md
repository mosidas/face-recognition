# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## リポジトリ概要

ONNXベースの機械学習モデルを使用したリアルタイム顔検出、認識、物体検出を実装するC#プロジェクト群です。

## 主要プロジェクト

- **Recognizer** (`src/Recognizer/`): ONNXモデルを使用した物体検出・顔認識のコアライブラリ
- **ExampleCLI** (`src/ExampleCLI/`): YOLOv8n-face対応のコマンドラインインターフェース例
- **RealTimeDetector** (`src/RealTimeDetector/`): リアルタイム物体検出アプリケーション
- **RealTimeFaceRecognizer** (`src/RealTimeFaceRecognizer/`): roll/pitch/yaw角度表示付きリアルタイム顔認識
- **UnifiedDetector** (`src/UnifiedDetector/`): 顔認識とスマートフォン検出を統合したシステム
- **IRCameraUnifiedDetector** (`src/IRCameraUnifiedDetector/`): WPF UIを持つ赤外線カメラサポート

## 開発コマンド

### ビルド
```bash
# ソリューション全体のビルド
dotnet build face-recognition.sln

# 特定プロジェクトのビルド
dotnet build src/UnifiedDetector/UnifiedDetector.csproj

# クリーンビルド
dotnet clean && dotnet build
```

### アプリケーション実行
```bash
# 統合検出器（全機能）
dotnet run --project src/UnifiedDetector/UnifiedDetector.csproj -- \
  --face-detector models/yolo11n-face.onnx \
  --face-recognizer models/arcface.onnx \
  --object-model models/yolo11n.onnx \
  --face-images reference_faces/ \
  --camera 1 \
  --recognition-threshold 0.4

# リアルタイム顔認識
dotnet run --project src/RealTimeFaceRecognizer/RealTimeFaceRecognizer.csproj -- \
  --model models/yolo11n-face.onnx \
  --camera 0

# CLI例
dotnet run --project src/ExampleCLI/ExampleCLI.csproj -- \
  --model models/yolov8n-face.onnx \
  --input test_image.jpg

# 赤外線カメラ統合検出器
dotnet run --project src/IRCameraUnifiedDetector/IRCameraUnifiedDetector.csproj
```

## アーキテクチャ

### 技術スタック
- **.NET 8.0**: 主要開発フレームワーク
- **ONNX Runtime**: 機械学習モデル推論（CPU/GPU）
- **OpenCvSharp4**: コンピュータビジョン操作
- **SixLabors.ImageSharp**: 画像処理
- **NLog**: 構造化ログ
- **WPF**: Windowsデスクトップ UI（IRCameraUnifiedDetector）

### MLモデル
- **顔検出**: YOLOv8n-face、YOLOv11n-face（5点ランドマーク検出）
- **顔認識**: ArcFace、JAPANESE_FACE_v1
- **物体検出**: YOLOv3、YOLOv11n（スマートフォン検出用）

### プロジェクト依存関係
- すべてのアプリケーションが`Recognizer`コアライブラリに依存
- `Recognizer`がONNXモデルのロードと推論を処理
- 検出器と認識器の共通抽象化
- OpenCV経由でカメラキャプチャを処理

## コードアーキテクチャ

### 主要パターン
- **検出器パターン**: すべての検出モデル用の`IObjectDetector`インターフェース
- **認識パターン**: 顔埋め込みモデル用の`IFaceRecognizer`インターフェース
- **結果型**: 型安全な結果のための`DetectionResult`、`FaceRecognitionResult`
- **非同期処理**: すべての検出/認識メソッドはCancellationToken付きの非同期
- **リソース管理**: ONNXセッション用の適切なIDisposable実装

### 重要なクラス
- `YoloDetector`: YOLOモデル推論と後処理を処理
- `FaceRecognizer`: 顔埋め込みと類似度計算を管理
- `UnifiedDetectorEngine`: 顔と物体検出を統合
- `CameraCapture`: カメラ入力とフレーム処理を管理

## 開発ガイドライン

### コード規約
- .NETコーディング規約に従う
- null許容参照型を使用（`#nullable enable`）
- 予期されるエラーにはResult<T>パターンを実装
- コンテキスト情報を含む構造化ログを使用
- 過度な抽象化なしにSOLID原則を適用

### パフォーマンスの考慮事項
- CUDAプロバイダ経由でGPUアクセラレーション利用可能
- リアルタイムパフォーマンス用にフレーム処理を最適化
- 破棄パターンによるメモリ効率的な画像処理
- 精度/速度のトレードオフのための設定可能な検出閾値

### テストのベストプラクティス
- 顔認識精度のために複数の人物でテスト
- ポジティブ（同一人物）とネガティブ（異なる人物）の両方のテストケースを使用
- 異なる照明条件と角度で検証
- モデル評価のためのメトリクスとROC曲線を生成

## 重要な注意事項

- **モデルファイル**: リポジトリに含まれていないため、別途ダウンロードが必要
- **プラットフォーム**: カメラAPIと一部UIコンポーネントのためWindows固有
- **カメラアクセス**: Windowsのカメラ権限が必要
- **GPUサポート**: CUDAプロバイダはオプションだがパフォーマンスのため推奨
- **ログ**: 日次ローテーションで`logs/`ディレクトリに生成

## トラブルシューティング

### 一般的な問題
1. **モデルが見つからない**: ONNXモデルが正しいパスにあることを確認
2. **カメラアクセス拒否**: Windowsカメラプライバシー設定を確認
3. **GPUが利用されない**: CUDAランタイムをインストールし、GPU対応ONNX Runtimeを使用
4. **低FPS**: 検出閾値を調整するか、より小さいモデルを使用
5. **ビルドエラー**: .NET 8.0 SDKがインストールされていることを確認

### デバッグコマンド
```bash
# 詳細ログを有効化
dotnet run --project src/UnifiedDetector/UnifiedDetector.csproj -- --verbose

# 特定のカメラインデックスでテスト
dotnet run --project src/RealTimeDetector/RealTimeDetector.csproj -- --camera 0

# CPUのみの推論で実行
dotnet run --project src/UnifiedDetector/UnifiedDetector.csproj -- --use-cpu
```

## 参照ドキュメント

以下のドキュメントも必要に応じて参照してください：

| ドキュメント | 説明 | 対象ファイル |
|----------|-------------|---------------|
| @.claude/memories/document.md | ドキュメント作成ルール | **/* |
| @.claude/memories/dotnet-developmnet.md | .NET開発ルール | **/*.cs |