using System.Runtime.InteropServices.WindowsRuntime;
using OpenCvSharp;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Mat = OpenCvSharp.Mat;

namespace WPFDetectorApp;

/// <summary>
/// カメラソースの種類
/// </summary>
public enum CameraSourceType
{
    Infrared,
    Depth,
    Color
}

/// <summary>
/// 利用可能なカメラソース情報
/// </summary>
public class AvailableCameraSource
{
    public string GroupId { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public CameraSourceType SourceType { get; set; }
    public string Description => $"{DisplayName} - {SourceType switch
    {
        CameraSourceType.Infrared => "赤外線",
        CameraSourceType.Depth => "深度",
        CameraSourceType.Color => "通常",
        _ => "不明"
    }}";
    public override string ToString() => Description;
}

/// <summary>
/// フレームデータ
/// </summary>
public class FrameData
{
    public Mat Frame { get; set; } = new();
    public CameraSourceType SourceType { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string SourceId { get; set; } = "";
}

/// <summary>
/// Depthフレームデータを格納するクラス
/// </summary>
public class DepthFrameData : FrameData
{
    public Windows.Media.Capture.Frames.DepthMediaFrameFormat? DepthFormat { get; set; }
    public uint MinReliableDepth { get; set; }
    public uint MaxReliableDepth { get; set; }
    public Windows.Media.Devices.Core.CameraIntrinsics? CameraIntrinsics { get; set; }
    public AvailableCameraSource? SourceInfo { get; set; }
}

public class CameraService : IDisposable
{
    private readonly Dictionary<string, MediaCapture> _mediaCaptures = [];
    private readonly Dictionary<string, MediaFrameReader> _frameReaders = [];
    private readonly Dictionary<string, DateTime> _lastFrameTime = [];
    private readonly System.Timers.Timer _healthCheckTimer;
    private readonly LoggingService? _logger;
    private bool _isInitialized = false;
    private bool _disposed = false;

    // 利用可能なカメラソース一覧
    public List<AvailableCameraSource> AvailableSources { get; private set; } = [];

    // 現在アクティブなカメラソース
    public HashSet<string> ActiveSources { get; private set; } = [];

    // イベント定義
    public event EventHandler<FrameData>? FrameArrived;
    public event EventHandler<DepthFrameData>? DepthFrameArrived;
    public event EventHandler<string>? StatusChanged;

    public CameraService(LoggingService? logger = null)
    {
        _logger = logger;

        // カメラの健全性チェック用タイマー（5秒間隔）
        _healthCheckTimer = new System.Timers.Timer(5000);
        _healthCheckTimer.Elapsed += OnHealthCheck;
        _healthCheckTimer.AutoReset = true;

        _logger?.Debug("CameraService", "CameraService initialized");
    }

    /// <summary>
    /// カメラサービスを初期化
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        try
        {
            OnStatusChanged("カメラデバイスを検索中...");

            // デバイスで現在利用可能な MediaFrameSourceGroup のリストを取得する
            var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();

            AvailableSources.Clear();

            foreach (var group in frameSourceGroups)
            {
                OnStatusChanged($"Group: {group.DisplayName} (ID: {group.Id})");

                foreach (var sourceInfo in group.SourceInfos)
                {
                    var sourceType = sourceInfo.SourceKind switch
                    {
                        MediaFrameSourceKind.Infrared => CameraSourceType.Infrared,
                        MediaFrameSourceKind.Depth => CameraSourceType.Depth,
                        MediaFrameSourceKind.Color => CameraSourceType.Color,
                        _ => (CameraSourceType?)null
                    };

                    if (sourceType.HasValue)
                    {
                        AvailableCameraSource availableSource = new()
                        {
                            GroupId = group.Id,
                            SourceId = sourceInfo.Id,
                            DisplayName = group.DisplayName,
                            SourceType = sourceType.Value
                        };

                        AvailableSources.Add(availableSource);
                        OnStatusChanged($"  追加: {availableSource.Description}");
                    }
                }
            }

            OnStatusChanged($"利用可能なカメラソース: {AvailableSources.Count}個");

            _isInitialized = true;
            OnStatusChanged("カメラサービスの初期化が完了しました");
            return true;
        }
        catch (Exception ex)
        {
            OnStatusChanged($"カメラサービスの初期化に失敗しました: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 指定されたカメラソースを有効化
    /// </summary>
    public async Task<bool> EnableCameraSourceAsync(string sourceId)
    {
        if (!_isInitialized)
        {
            OnStatusChanged("カメラサービスが初期化されていません");
            return false;
        }

        var source = AvailableSources.FirstOrDefault(s => s.SourceId == sourceId);
        if (source == null)
        {
            OnStatusChanged($"指定されたソースが見つかりません: {sourceId}");
            return false;
        }

        if (ActiveSources.Contains(sourceId))
        {
            OnStatusChanged($"既に有効化されています: {source.Description}");
            return true;
        }

        try
        {
            // MediaCaptureを作成（グループごとに分ける）
            if (!_mediaCaptures.ContainsKey(source.GroupId))
            {
                var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();
                var group = frameSourceGroups.FirstOrDefault(g => g.Id == source.GroupId);

                if (group == null)
                {
                    OnStatusChanged($"カメラグループが見つかりません: {source.GroupId}");
                    return false;
                }

                MediaCapture newMediaCapture = new();

                // 失敗イベントハンドラを追加
                newMediaCapture.Failed += (s, e) =>
                {
                    OnStatusChanged($"MediaCapture失敗: {e.Message} (Code: {e.Code})");

                    // 失敗したMediaCaptureを削除
                    _ = Task.Run(async () =>
                    {
                              await Task.Delay(2000); // 2秒待機してから復旧試行
                              if (_mediaCaptures.ContainsKey(source.GroupId))
                              {
                                  _mediaCaptures[source.GroupId]?.Dispose();
                                  _ = _mediaCaptures.Remove(source.GroupId);
                              }
                          });
                };

                await newMediaCapture.InitializeAsync(new MediaCaptureInitializationSettings()
                {
                    SourceGroup = group,
                    SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                    StreamingCaptureMode = StreamingCaptureMode.Video
                });

                _mediaCaptures[source.GroupId] = newMediaCapture;
            }

            // MediaFrameReaderを作成
            var mediaCapture = _mediaCaptures[source.GroupId];

            // 赤外線カメラの場合のみ、InfraredTorchControlを確認
            if (source.SourceType == CameraSourceType.Infrared)
            {
                try
                {
                    var infraredTorchControl = mediaCapture.VideoDeviceController.InfraredTorchControl;
                    if (infraredTorchControl.IsSupported)
                    {
                        OnStatusChanged($"赤外線照明制御がサポートされています: {source.Description}");
                        OnStatusChanged($"  現在のモード: {infraredTorchControl.CurrentMode}");
                        OnStatusChanged($"  電力範囲: {infraredTorchControl.MinPower} - {infraredTorchControl.MaxPower} (ステップ: {infraredTorchControl.PowerStep})");
                        OnStatusChanged($"  現在の電力: {infraredTorchControl.Power}");
                        OnStatusChanged($"  サポートモード: {string.Join(", ", infraredTorchControl.SupportedModes)}");
                    }
                    else
                    {
                        OnStatusChanged($"赤外線照明制御はサポートされていません: {source.Description}");
                    }
                }
                catch (Exception ex)
                {
                    // エラーを記録するが、処理は継続
                    OnStatusChanged($"赤外線照明制御の確認中にエラーが発生しました: {ex.Message}");
                }
            }

            var frameReader = await mediaCapture.CreateFrameReaderAsync(mediaCapture.FrameSources[sourceId]);

            // イベントハンドラを登録
            frameReader.FrameArrived += (sender, args) => OnFrameArrived(sender, args, source);

            _ = await frameReader.StartAsync();

            _frameReaders[sourceId] = frameReader;
            _ = ActiveSources.Add(sourceId);

            // フレーム受信時刻を初期化
            _lastFrameTime[sourceId] = DateTime.Now;

            OnStatusChanged($"有効化しました: {source.Description}");
            return true;
        }
        catch (Exception ex)
        {
            OnStatusChanged($"カメラソースの有効化に失敗しました: {source.Description} - {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 指定されたカメラソースを無効化
    /// </summary>
    public async Task<bool> DisableCameraSourceAsync(string sourceId)
    {
        if (!ActiveSources.Contains(sourceId))
        {
            return true; // 既に無効化されている
        }

        var source = AvailableSources.FirstOrDefault(s => s.SourceId == sourceId);
        if (source == null)
        {
            OnStatusChanged($"指定されたソースが見つかりません: {sourceId}");
            return false;
        }

        try
        {
            // まずActiveSourcesから削除してイベント処理を無効化
            _ = ActiveSources.Remove(sourceId);

            // フレーム時刻記録も削除
            _ = _lastFrameTime.Remove(sourceId);

            if (_frameReaders.TryGetValue(sourceId, out var frameReader))
            {
                // フレームリーダーの停止と削除
                await frameReader.StopAsync();

                // 少し待機してフレーム処理が完了するのを待つ
                await Task.Delay(50);

                frameReader.Dispose();
                _ = _frameReaders.Remove(sourceId);
            }

            OnStatusChanged($"無効化しました: {source.Description}");
            return true;
        }
        catch (Exception ex)
        {
            OnStatusChanged($"カメラソースの無効化に失敗しました: {source.Description} - {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// カメラソースの切り替え（他を無効化して指定ソースのみ有効化）
    /// </summary>
    public async Task<bool> SwitchCameraSourceAsync(string sourceId)
    {
        // 全てのソースを無効化
        await DisableAllSourcesAsync();

        // 少し待ってからフレームがクリアされるようにする
        await Task.Delay(100);

        // 指定されたソースのみを有効化
        return await EnableCameraSourceAsync(sourceId);
    }

    /// <summary>
    /// 全カメラソースを無効化
    /// </summary>
    public async Task DisableAllSourcesAsync()
    {
        List<string> activeSources = [.. ActiveSources];
        foreach (var sourceId in activeSources)
        {
            _ = await DisableCameraSourceAsync(sourceId);
        }
    }

    /// <summary>
    /// カメラストリーミングを開始（デフォルトで最初のカメラを開始）
    /// </summary>
    public async Task<bool> StartAsync()
    {
        if (!_isInitialized)
        {
            OnStatusChanged("カメラサービスが初期化されていません");
            return false;
        }

        if (!AvailableSources.Any())
        {
            OnStatusChanged("利用可能なカメラがありません");
            return false;
        }

        // ヘルスチェックタイマーを開始
        _healthCheckTimer.Start();

        // デフォルトで最初の利用可能なカメラを開始
        var firstSource = AvailableSources.FirstOrDefault(s => s.SourceType == CameraSourceType.Infrared) ??
                          AvailableSources.FirstOrDefault(s => s.SourceType == CameraSourceType.Color) ??
                          AvailableSources.First();

        return await EnableCameraSourceAsync(firstSource.SourceId);
    }

    /// <summary>
    /// カメラストリーミングを停止
    /// </summary>
    public async Task StopAsync()
    {
        await DisableAllSourcesAsync();
        OnStatusChanged("カメラストリーミングを停止しました");
    }

    /// <summary>
    /// フレームが到着した時の処理
    /// </summary>
    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args, AvailableCameraSource source)
    {
        // アクティブなソースかどうかをチェック
        if (!ActiveSources.Contains(source.SourceId))
        {
            return;
        }

        _logger?.Debug("CameraService", $"Frame arrived from {source.Description} at {DateTime.Now:HH:mm:ss.fff}");

        try
        {
            using var latestFrameReference = sender.TryAcquireLatestFrame();
            if (latestFrameReference?.VideoMediaFrame?.SoftwareBitmap == null)
            {
                _logger?.Warning("CameraService", $"Failed to acquire frame from {source.Description}");
                return;
            }

            var softwareBitmap = latestFrameReference.VideoMediaFrame.SoftwareBitmap;

            // WPF の Image コントロールで表示できるよう、BGRA8 のアルファ乗算済みに変換する
            if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
            {
                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }

            // フレーム受信時刻を記録
            _lastFrameTime[source.SourceId] = DateTime.Now;
            _logger?.Debug("CameraService", $"Frame time updated for {source.SourceId} at {DateTime.Now:HH:mm:ss.fff}");

            // SoftwareBitmapをOpenCVのMatに変換
            var mat = ConvertSoftwareBitmapToMat(softwareBitmap);

            // Depth情報がある場合は別処理
            if (source.SourceType == CameraSourceType.Depth && latestFrameReference.VideoMediaFrame.DepthMediaFrame != null)
            {
                var depthFrame = latestFrameReference.VideoMediaFrame.DepthMediaFrame;
                var intrinsics = latestFrameReference.VideoMediaFrame.CameraIntrinsics;

                DepthFrameData depthData = new()
                {
                    Frame = mat,
                    SourceType = source.SourceType,
                    Timestamp = DateTime.Now,
                    SourceId = source.SourceId,
                    DepthFormat = depthFrame.DepthFormat,
                    MinReliableDepth = depthFrame.MinReliableDepth,
                    MaxReliableDepth = depthFrame.MaxReliableDepth,
                    CameraIntrinsics = intrinsics,
                    SourceInfo = source
                };

                DepthFrameArrived?.Invoke(this, depthData);
            }
            else
            {
                // 通常のフレームデータ
                FrameData frameData = new()
                {
                    Frame = mat,
                    SourceType = source.SourceType,
                    Timestamp = DateTime.Now,
                    SourceId = source.SourceId
                };

                FrameArrived?.Invoke(this, frameData);
            }
        }
        catch (ObjectDisposedException)
        {
            // MediaFrameReaderが既に破棄されている場合は無視
            return;
        }
        catch (Exception ex)
        {
            OnStatusChanged($"フレーム処理でエラーが発生しました: {ex.Message}");
        }
    }

    /// <summary>
    /// SoftwareBitmapをOpenCVのMatに変換
    /// </summary>
    private static Mat ConvertSoftwareBitmapToMat(SoftwareBitmap softwareBitmap)
    {
        var width = softwareBitmap.PixelWidth;
        var height = softwareBitmap.PixelHeight;

        // SoftwareBitmapからバイト配列を取得
        var buffer = new byte[width * height * 4]; // BGRA8 format
        softwareBitmap.CopyToBuffer(buffer.AsBuffer());

        // MatTypeをBGRAに設定
        Mat mat = new(height, width, MatType.CV_8UC4);

        // バイト配列をMatにコピー
        var matData = mat.Data;
        System.Runtime.InteropServices.Marshal.Copy(buffer, 0, matData, buffer.Length);

        // BGRAからBGRに変換（OpenCVの標準フォーマット）
        Mat bgrMat = new();
        Cv2.CvtColor(mat, bgrMat, ColorConversionCodes.BGRA2BGR);

        return bgrMat;
    }

    /// <summary>
    /// カメラの健全性チェック
    /// </summary>
    private async void OnHealthCheck(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_isInitialized)
        {
            return;
        }

        var now = DateTime.Now;
        List<string> stoppedSources = [];

        foreach (var sourceId in ActiveSources.ToList())
        {
            // 最後のフレーム受信から60秒以上経過している場合は停止と判断（低FPS対応）
            if (_lastFrameTime.TryGetValue(sourceId, out var lastTime))
            {
                var elapsed = (now - lastTime).TotalSeconds;
                _logger?.Debug("CameraService", $"Health check for {sourceId}: {elapsed:F1}s since last frame");

                if (elapsed > 10)
                {
                    stoppedSources.Add(sourceId);
                    _logger?.Warning("CameraService", $"Camera source {sourceId} stopped (no frame for {elapsed:F1}s)");
                }
            }
        }

        // 停止したソースがあれば復旧を試行
        foreach (var sourceId in stoppedSources)
        {
            var source = AvailableSources.FirstOrDefault(s => s.SourceId == sourceId);
            if (source != null)
            {
                OnStatusChanged($"カメラストリームが停止しました。復旧を試行します: {source.Description}");

                // 一度無効化してから再有効化
                _ = await DisableCameraSourceAsync(sourceId);
                await Task.Delay(1000); // 1秒待機
                _ = await EnableCameraSourceAsync(sourceId);
            }
        }
    }

    /// <summary>
    /// ステータス変更を通知
    /// </summary>
    private void OnStatusChanged(string message)
    {
        _logger?.Info("CameraService", message);
        StatusChanged?.Invoke(this, message);
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

        // ヘルスチェックタイマーを停止
        _healthCheckTimer?.Stop();
        _healthCheckTimer?.Dispose();

        foreach (var frameReader in _frameReaders.Values)
        {
            frameReader?.Dispose();
        }
        _frameReaders.Clear();

        foreach (var mediaCapture in _mediaCaptures.Values)
        {
            mediaCapture?.Dispose();
        }
        _mediaCaptures.Clear();

        ActiveSources.Clear();
        _lastFrameTime.Clear();
        _isInitialized = false;
        _disposed = true;
    }
}
