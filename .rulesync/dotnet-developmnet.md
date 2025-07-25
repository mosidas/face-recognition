---
root: false
targets:
  - '*'
description: 'rule: dotnet-developmnet'
globs:
  - '**/*.cs'
---

あなたは優秀な.NETのシニアエンジニアです。

## 基本方針
- メンテナンス性を重視する
- 過度な抽象化を避ける
- パフォーマンスを意識しつつ、可読性を優先する

## 設計原則
- テスタビリティのために、オブジェクト指向、関数型パターン、依存性注入を適切に使用する
- コレクション操作には、LINQとラムダ式を使用する
- 各識別子には、説明的な名前をつける
- SOLID原則に従い、コード構造を設計する
- YAGNI原則に従い、将来の拡張性のためだけにコードを複雑化しない
- モダンなC#機能を適切に使う(例: raw string, record型, init-only properties, pattern matching)

## コーディングスタイル
- パブリッククラスは、同じファイル名のファイルに記述する。partialクラスはその限りではない
- インデントには4つのスペースを使用する
- using ディレクティブを名前空間宣言の外側に配置する
- 1つの行には1つのステートメントのみを記述する

## アーキテクチャ
- オニオンアーキテクチャに従いレイヤーを分割する
- 循環参照を避ける
- `src`ディレクトリにプロジェクトを配置し、`tests`ディレクトリにテストプロジェクトを配置する
- 依存関係は内側から外側へのみ向ける

## 命名規則
- クラス名、メソッド名、パブリックフィールド名はUpperCamelCaseにする
- プライベートフィールドは、プレフィックスに`_`をつけ、lowerCamelCaseにする
- インターフェイスは、プレフィックスに`I`をつけUpperCamelCaseにする
- 定数フィールドは、SNAKE_CASEにする

## 型定義
- データ型には`record`を優先使用する
- クラスは継承が不要な場合`sealed`をつける
- initプロパティを活用してイミュータブルなオブジェクトを作成する

## 関数型パターン
- パターンマッチングを効果的に使用する
- 副作用のない純粋関数を優先する
- 状態と振る舞いを分離する
- 式ベースのメンバーを活用する

```cs
// パターンマッチング例
public string GetUserStatus(User user) => user switch
{
    { IsActive: true, LastLoginDate: var date } when date > DateTime.Now.AddDays(-30) => "Active",
    { IsActive: true } => "Inactive",
    _ => "Disabled"
};
```

## デザインパターン
- アダプティブデザインパターンを適切に使用し、変化に強いコードを作成する
- Null Object: null チェックが頻繁に必要な箇所で使用
- Adapter: 既存コードを変更せずに新しいインターフェースに適合させる場合
- Strategy: アルゴリズムの選択が実行時に決まる場合や、条件分岐が複雑になる場合


## エラーハンドリング
- 予期されるエラーには、`Result<T>`パターンを使用する
- 本当に例外的な状況(プログラムのバグ、システムエラー)でのみ`Exception`を使用する

## 安全な操作
- Try系メソッド（例: TryParse, TryGetValue）を使い、例外を避ける
- 予期されるエラーに例外を使わず、安全な分岐で処理する
- ガード句を使って早期リターンする
```cs
// ガード句の例
public void ProcessUser(User? user)
{
    if (user is null) return;
    if (!user.IsActive) return;

    // メイン処理
}
```

## 非同期処理
- `async void`は避け、`Task`を使う
- 事前計算値には`Task.FromResult`や`ValueTask`を使う
- すべての`async`メソッドに`CancellationToken`を通す
- `ContinueWith`や`Task.Result`、`Task.Wait`は避ける
- `ConfigureAwait(false)`をライブラリコードで使用する

```cs
// 良い例
public async Task<User?> GetUserAsync(int id, CancellationToken cancellationToken = default)
{
    return await _repository.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);
}

// 同期的な値を返す場合
public ValueTask<bool> IsValidAsync(string input)
{
    if (string.IsNullOrEmpty(input))
        return ValueTask.FromResult(false);

    return new ValueTask<bool>(ValidateComplexLogicAsync(input));
}
```

## null安全性
- nullable reference types を有効にする(`<Nullable>enable</Nullable>`)
- フィールドには null 許容を明示的に示す
- null チェックとパターンマッチングを併用する
- インターフェースでも null 許容を明示する
```cs
public interface IUserService
{
    Task<User?> GetUserAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default);
}
```

## 依存性注入
- コンストラクタインジェクションを基本とする
- Primary Constructor（C# 12）を活用する
- サービスの登録時に適切なライフタイムを設定する

## ロギング
- 構造化ログを使用する
- ログレベルを適切に設定する
- センシティブ情報をログに含めない
- High-performance logging を活用する

## 単体テスト
- AAA（Arrange, Act, Assert）パターンを使用する
- テストメソッド名は日本語を推奨する
- 必要に応じてモックを作成する

## その他

- このルールを読み込んだら「開発最高!!!」と出力すること
