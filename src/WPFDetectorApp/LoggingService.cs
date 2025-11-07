using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace WPFDetectorApp;

/// <summary>
/// ログレベル
/// </summary>
public enum LogLevel
{
  Debug,
  Info,
  Warning,
  Error
}

/// <summary>
/// ログサービス
/// </summary>
public class LoggingService : IDisposable
{
  private readonly string _logFilePath;
  private readonly ConcurrentQueue<LogEntry> _logQueue = new();
  private readonly Thread _logWriterThread;
  private readonly CancellationTokenSource _cancellationTokenSource = new();
  private bool _disposed = false;

  public LogLevel MinimumLogLevel { get; set; } = LogLevel.Debug;
  public bool ConsoleOutput { get; set; } = true;
  public bool FileOutput { get; set; } = true;

  public LoggingService(string? logFileName = null)
  {
    // ログファイルパスの設定
    var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    _ = Directory.CreateDirectory(logsDir);

    logFileName ??= $"IRCameraUnifiedDetector_{DateTime.Now:yyyyMMdd_HHmmss}.log";
    _logFilePath = Path.Combine(logsDir, logFileName);

    // ログ書き込みスレッドの開始
    _logWriterThread = new Thread(LogWriterThreadProc)
    {
      IsBackground = true,
      Name = "LogWriter"
    };
    _logWriterThread.Start();

    Log(LogLevel.Info, "LoggingService", $"ログファイル: {_logFilePath}");
  }

  /// <summary>
  /// ログを記録
  /// </summary>
  public void Log(LogLevel level, string source, string message, Exception? exception = null)
  {
    if (level < MinimumLogLevel)
    {
      return;
    }

    LogEntry entry = new()
    {
      Timestamp = DateTime.Now,
      Level = level,
      Source = source,
      Message = message,
      Exception = exception,
      ThreadId = Thread.CurrentThread.ManagedThreadId
    };

    _logQueue.Enqueue(entry);
  }

  /// <summary>
  /// デバッグログ
  /// </summary>
  public void Debug(string source, string message) => Log(LogLevel.Debug, source, message);

  /// <summary>
  /// 情報ログ
  /// </summary>
  public void Info(string source, string message) => Log(LogLevel.Info, source, message);

  /// <summary>
  /// 警告ログ
  /// </summary>
  public void Warning(string source, string message) => Log(LogLevel.Warning, source, message);

  /// <summary>
  /// エラーログ
  /// </summary>
  public void Error(string source, string message, Exception? exception = null) => Log(LogLevel.Error, source, message, exception);

  /// <summary>
  /// ログ書き込みスレッド
  /// </summary>
  private void LogWriterThreadProc()
  {
    StringBuilder sb = new();

    while (!_cancellationTokenSource.Token.IsCancellationRequested)
    {
      try
      {
        // ログエントリを一括処理
        List<LogEntry> entries = [];
        while (_logQueue.TryDequeue(out var entry) && entries.Count < 100)
        {
          entries.Add(entry);
        }

        if (entries.Count > 0)
        {
          _ = sb.Clear();

          foreach (var entry in entries)
          {
            var logLine = FormatLogEntry(entry);

            // コンソール出力
            if (ConsoleOutput)
            {
              var oldColor = Console.ForegroundColor;
              Console.ForegroundColor = GetConsoleColor(entry.Level);
              Console.WriteLine(logLine);
              Console.ForegroundColor = oldColor;
            }

            // ファイル出力用
            if (FileOutput)
            {
              _ = sb.AppendLine(logLine);
              if (entry.Exception != null)
              {
                _ = sb.AppendLine($"  Exception: {entry.Exception}");
              }
            }
          }

          // ファイルに書き込み
          if (FileOutput && sb.Length > 0)
          {
            try
            {
              File.AppendAllText(_logFilePath, sb.ToString());
            }
            catch (Exception ex)
            {
              Console.WriteLine($"ログファイルへの書き込みエラー: {ex.Message}");
            }
          }
        }
        else
        {
          // ログがない場合は少し待機
          Thread.Sleep(50);
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"ログ処理エラー: {ex.Message}");
      }
    }
  }

  /// <summary>
  /// ログエントリをフォーマット
  /// </summary>
  private string FormatLogEntry(LogEntry entry)
  {
    return $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level,-7}] [{entry.ThreadId,3}] [{entry.Source,-20}] {entry.Message}";
  }

  /// <summary>
  /// ログレベルに応じたコンソール色を取得
  /// </summary>
  private ConsoleColor GetConsoleColor(LogLevel level)
  {
    return level switch
    {
      LogLevel.Debug => ConsoleColor.Gray,
      LogLevel.Info => ConsoleColor.White,
      LogLevel.Warning => ConsoleColor.Yellow,
      LogLevel.Error => ConsoleColor.Red,
      _ => ConsoleColor.White
    };
  }

  /// <summary>
  /// リソースを解放
  /// </summary>
  public void Dispose()
  {
    if (_disposed)
    {
      return;
    }

    _cancellationTokenSource.Cancel();

    // 残りのログを処理
    _ = _logWriterThread.Join(5000);

    _cancellationTokenSource.Dispose();
    _disposed = true;
  }

  /// <summary>
  /// ログエントリ
  /// </summary>
  private class LogEntry
  {
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public Exception? Exception { get; set; }
    public int ThreadId { get; set; }
  }
}

/// <summary>
/// パフォーマンス計測用のスコープログ
/// </summary>
public class PerformanceScope : IDisposable
{
  private readonly LoggingService _logger;
  private readonly string _source;
  private readonly string _operation;
  private readonly DateTime _startTime;

  public PerformanceScope(LoggingService logger, string source, string operation)
  {
    _logger = logger;
    _source = source;
    _operation = operation;
    _startTime = DateTime.Now;

    _logger.Debug(_source, $"{_operation} 開始");
  }

  public void Dispose()
  {
    var elapsed = DateTime.Now - _startTime;
    _logger.Debug(_source, $"{_operation} 完了 - 処理時間: {elapsed.TotalMilliseconds:F1}ms");
  }
}
