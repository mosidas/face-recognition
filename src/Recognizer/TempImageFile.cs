namespace Recognizer;

/// <summary>
/// 一時画像ファイルの安全な管理クラス
/// </summary>
public sealed class TempImageFile : IDisposable
{
    private readonly string _tempPath;
    private bool _disposed = false;

    /// <summary>
    /// 一時ファイルのパス
    /// </summary>
    public string Path => _tempPath;

    /// <summary>
    /// セキュアな一時ファイルを作成
    /// </summary>
    public TempImageFile()
    {
        // セキュアな一時ディレクトリを取得
        var tempDir = System.IO.Path.GetTempPath();

        // ユニークなファイル名を生成
        var fileName = $"face_temp_{Guid.NewGuid():N}{Constants.Files.TempImageExtension}";

        _tempPath = System.IO.Path.Combine(tempDir, fileName);

        // ファイルが既に存在する場合の安全チェック
        if (File.Exists(_tempPath))
        {
            throw new InvalidOperationException($"Temporary file already exists: {_tempPath}");
        }
    }

    /// <summary>
    /// リソースの解放
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                if (File.Exists(_tempPath))
                {
                    File.Delete(_tempPath);
                }
            }
            catch (Exception ex)
            {
                // 一時ファイルの削除に失敗してもアプリケーションを停止させない
                Console.WriteLine($"Warning: Failed to delete temporary file {_tempPath}: {ex.Message}");
            }

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// ファイナライザ（念のため）
    /// </summary>
    ~TempImageFile()
    {
        Dispose();
    }
}
