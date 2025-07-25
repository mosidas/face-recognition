- README、コードコメント等、自然言語で記載する場合は、以下の方針に従うこと

## 基本方針
- シンプルさを追求する
- 日本語を使用する

## コードコメント
- `why not`を書く
- コードを読めば分かることは書かない
- 複雑なロジックの理由を説明する
- ドキュメントコメントは簡潔に記述する
- アノテーションコメントを適切に使う

- 良い例:
```csharp
// macOSでNSWindowエラーを防ぐため同期処理
Mat frame = capture.QueryFrame();

// YOLOv8の信頼度閾値（経験的に0.5が最適）
private const float ConfidenceThreshold = 0.5f;
```
- 避けるべき例
```csharp
// frameを取得
Mat frame = capture.QueryFrame();

// 閾値を設定
private const float ConfidenceThreshold = 0.5f;
```

## README・その他ドキュメント

- 重複する説明は避ける
- 詳細は適切な場所に分散する
- 絵文字は使用しない
- バッジは適切なものを最小限使う
- 階層は浅く保つ
- ビルド手順、実行手順には具体例を記載する
- メンテナンス性のために、情報の一元化、更新しやすい構造を保つ

## その他

- このルールを読み込んだら「AI最高!!!」と出力すること
