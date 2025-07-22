using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Media;
using IRCameraUnifiedDetector;
using OpenCvSharp;
using Recognizer;
using System.IO;
using Microsoft.Win32;

namespace WPFDetectorApp
{
    public partial class MainWindow : System.Windows.Window
    {
        private CameraService? _cameraService;
        private LoggingService? _logger;
        private YoloDetector? _objectDetector;
        private FaceRecognizer? _faceRecognizer;
        private FaceDatabase? _faceDatabase;

        private int _frameCount = 0;
        private int _totalReceivedFrames = 0; // 受信フレーム総数
        private bool _isCameraRunning = false;
        private bool _isDetectionRunning = false;
        private DateTime _startTime = DateTime.Now;
        private DateTime _lastProcessedTime = DateTime.MinValue;
        private double _targetFps = 5.0; // 可変FPS制限
        private TimeSpan _frameInterval = TimeSpan.FromMilliseconds(1000.0 / 5.0);
        private bool _isProcessingFrame = false;
        private bool _isInitialized = false;
        private CommandLineArgs? _commandLineArgs;

        public MainWindow() : this(null)
        {
        }

        public MainWindow(CommandLineArgs? commandLineArgs)
        {
            InitializeComponent();

            _commandLineArgs = commandLineArgs;

            // FPSテキストボックスのイベントハンドラーを設定
            FpsTextBox.TextChanged += FpsTextBox_TextChanged;

            _isInitialized = true;

            // コマンドライン引数でUIを初期化
            ApplyCommandLineArgs();

            InitializeServices();
        }

        private void ApplyCommandLineArgs()
        {
            if (_commandLineArgs == null) return;

            try
            {
                // モデルファイルパスを設定
                if (!string.IsNullOrEmpty(_commandLineArgs.YoloModelPath))
                {
                    YoloModelPathTextBox.Text = _commandLineArgs.YoloModelPath;
                }

                if (!string.IsNullOrEmpty(_commandLineArgs.FaceDetectionModelPath))
                {
                    FaceDetectionModelPathTextBox.Text = _commandLineArgs.FaceDetectionModelPath;
                }

                if (!string.IsNullOrEmpty(_commandLineArgs.FaceRecognitionModelPath))
                {
                    FaceRecognitionModelPathTextBox.Text = _commandLineArgs.FaceRecognitionModelPath;
                }

                if (!string.IsNullOrEmpty(_commandLineArgs.FaceImagesPath))
                {
                    FacePathTextBox.Text = _commandLineArgs.FaceImagesPath;
                }

                // FPSを設定
                if (_commandLineArgs.TargetFps.HasValue)
                {
                    var fps = _commandLineArgs.TargetFps.Value;
                    FpsTextBox.Text = fps.ToString("F1");
                }

                // 検出機能のオン/オフを設定
                if (_commandLineArgs.ObjectDetectionEnabled.HasValue)
                {
                    ObjectDetectionCheckBox.IsChecked = _commandLineArgs.ObjectDetectionEnabled.Value;
                }

                if (_commandLineArgs.FaceRecognitionEnabled.HasValue)
                {
                    FaceRecognitionCheckBox.IsChecked = _commandLineArgs.FaceRecognitionEnabled.Value;
                }

                _logger?.Info("MainWindow", "Command line arguments applied successfully");
            }
            catch (Exception ex)
            {
                _logger?.Error("MainWindow", $"Error applying command line arguments: {ex.Message}", ex);
            }
        }

        private async void InitializeServices()
        {
            try
            {
                _logger = new LoggingService();
                _cameraService = new CameraService();
                _faceDatabase = new FaceDatabase();

                _cameraService.FrameArrived += OnFrameArrived;
                _cameraService.StatusChanged += OnStatusChanged;

                StatusText.Text = "Services initialized - Load models manually";
                _logger.Info("MainWindow", "Services initialized - models not loaded yet");

                // Initialize camera
                if (await _cameraService.InitializeAsync())
                {
                    UpdateCameraComboBox();
                    StatusText.Text = "Services initialized successfully";
                    _logger.Info("MainWindow", "Services initialized successfully");
                    
                    // UIボタンの初期状態を同期
                    UpdateUIButtonStates();
                }
                else
                {
                    StatusText.Text = "Failed to initialize camera";
                    _logger.Error("MainWindow", "Failed to initialize camera");
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                _logger?.Error("MainWindow", $"Initialization error: {ex.Message}", ex);
            }
        }

        private void UpdateCameraComboBox()
        {
            if (_cameraService == null) return;

            CameraComboBox.Items.Clear();
            foreach (var source in _cameraService.AvailableSources)
            {
                CameraComboBox.Items.Add(source);
            }

            // Select IR camera if available, otherwise first camera
            var defaultSource = _cameraService.AvailableSources.FirstOrDefault(s => s.SourceType == CameraSourceType.Infrared) ??
                               _cameraService.AvailableSources.FirstOrDefault();

            if (defaultSource != null)
            {
                // SelectionChangedイベントを一時的に無効化してからデフォルト選択
                CameraComboBox.SelectionChanged -= CameraComboBox_SelectionChanged;
                CameraComboBox.SelectedItem = defaultSource;
                CameraComboBox.SelectionChanged += CameraComboBox_SelectionChanged;
            }
        }

        private void UpdateUIButtonStates()
        {
            if (_cameraService == null) return;

            // カメラサービスの実際の状態に基づいてUIボタンを更新
            var isCameraRunning = _isCameraRunning;
            var isDetectionRunning = _isDetectionRunning;

            _logger?.Debug("MainWindow", $"Updating UI button states - Camera: {isCameraRunning}, Detection: {isDetectionRunning}");

            // カメラボタンの状態
            StartCameraButton.IsEnabled = !isCameraRunning;
            StopCameraButton.IsEnabled = isCameraRunning;

            // 検出ボタンの状態（カメラが起動している場合のみ操作可能）
            StartDetectionButton.IsEnabled = isCameraRunning && !isDetectionRunning;
            StopDetectionButton.IsEnabled = isCameraRunning && isDetectionRunning;

            _logger?.Info("MainWindow", $"UI button states updated - StartCamera: {StartCameraButton.IsEnabled}, StopCamera: {StopCameraButton.IsEnabled}, StartDetection: {StartDetectionButton.IsEnabled}, StopDetection: {StopDetectionButton.IsEnabled}");
        }

        private async void StartCameraButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cameraService == null) return;

            try
            {
                _isCameraRunning = true;
                _startTime = DateTime.Now;
                _frameCount = 0;
                _totalReceivedFrames = 0; // 受信フレーム数もリセット
                _isProcessingFrame = false;
                _lastProcessedTime = DateTime.MinValue;

                _logger?.Info("MainWindow", "Starting camera");

                bool startResult;
                
                // ComboBoxで選択されているカメラがあればそれを使用、なければデフォルト動作
                if (CameraComboBox.SelectedItem is AvailableCameraSource selectedSource)
                {
                    startResult = await _cameraService.EnableCameraSourceAsync(selectedSource.SourceId);
                }
                else
                {
                    startResult = await _cameraService.StartAsync();
                }

                if (startResult)
                {
                    StatusText.Text = "Camera started";
                    NoVideoText.Visibility = Visibility.Collapsed;
                    UpdateUIButtonStates(); // UIボタン状態を統一管理

                    _logger?.Info("MainWindow", "Camera started");
                }
                else
                {
                    _isCameraRunning = false;
                    StatusText.Text = "Failed to start camera";
                    UpdateUIButtonStates(); // 失敗時も状態を更新
                    _logger?.Error("MainWindow", "Failed to start camera");
                }
            }
            catch (Exception ex)
            {
                _isCameraRunning = false;
                StatusText.Text = $"Error: {ex.Message}";
                UpdateUIButtonStates(); // エラー時も状態を更新
                _logger?.Error("MainWindow", $"Camera start error: {ex.Message}", ex);
            }
        }

        private async void StopCameraButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cameraService == null) return;

            try
            {
                await _cameraService.StopAsync();
                _isCameraRunning = false;
                _isDetectionRunning = false;

                StatusText.Text = "Camera stopped";
                CameraImage.Source = null;
                NoVideoText.Visibility = Visibility.Visible;
                UpdateUIButtonStates(); // UIボタン状態を統一管理

                _logger?.Info("MainWindow", "Camera stopped");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                _logger?.Error("MainWindow", $"Camera stop error: {ex.Message}", ex);
            }
        }

        private void StartDetectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCameraRunning)
            {
                StatusText.Text = "Please start camera first";
                return;
            }

            try
            {
                _isDetectionRunning = true;
                StatusText.Text = "Detection started";
                UpdateUIButtonStates(); // UIボタン状態を統一管理

                _logger?.Info("MainWindow", "Detection started");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                _logger?.Error("MainWindow", $"Detection start error: {ex.Message}", ex);
            }
        }

        private void StopDetectionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isDetectionRunning = false;
                StatusText.Text = "Detection stopped";
                UpdateUIButtonStates(); // UIボタン状態を統一管理

                _logger?.Info("MainWindow", "Detection stopped");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                _logger?.Error("MainWindow", $"Detection stop error: {ex.Message}", ex);
            }
        }

        private async void CameraComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 初期化中の場合は何もしない（カメラが起動していない場合はカメラ選択のみ更新）
            if (_cameraService == null || CameraComboBox.SelectedItem == null) return;

            var selectedSource = (AvailableCameraSource)CameraComboBox.SelectedItem;

            // カメラが起動している場合のみ実際に切り替える
            if (_isCameraRunning)
            {
                try
                {
                    await _cameraService.SwitchCameraSourceAsync(selectedSource.SourceId);
                    StatusText.Text = $"Switched to: {selectedSource.Description}";
                    _logger?.Info("MainWindow", $"Switched to camera: {selectedSource.Description}");
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error switching camera: {ex.Message}";
                    _logger?.Error("MainWindow", $"Switch error: {ex.Message}", ex);
                }
            }
            else
            {
                // カメラが起動していない場合は選択のみ記録
                StatusText.Text = $"Camera selected: {selectedSource.Description}";
                _logger?.Info("MainWindow", $"Camera selected (not running): {selectedSource.Description}");
            }
        }

        private void LoadYoloButton_Click(object sender, RoutedEventArgs e)
        {
            var modelPath = YoloModelPathTextBox.Text;
            if (string.IsNullOrEmpty(modelPath))
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Select YOLO Model File",
                    Filter = "ONNX files (*.onnx)|*.onnx|All files (*.*)|*.*",
                    CheckFileExists = true
                };

                if (dialog.ShowDialog() == true)
                {
                    modelPath = dialog.FileName;
                    YoloModelPathTextBox.Text = modelPath;
                }
            }

            if (!File.Exists(modelPath))
            {
                StatusText.Text = $"YOLO model file not found: {modelPath}";
                return;
            }

            try
            {
                StatusText.Text = "Loading YOLO model...";
                _objectDetector?.Dispose();
                _objectDetector = new YoloDetector(modelPath, CocoClassNames.Names);
                StatusText.Text = "YOLO model loaded successfully";
                _logger?.Info("MainWindow", $"YOLO model loaded: {modelPath}");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading YOLO model: {ex.Message}";
                _logger?.Error("MainWindow", $"YOLO model load error: {ex.Message}", ex);
            }
        }

        private void LoadFaceModelsButton_Click(object sender, RoutedEventArgs e)
        {
            var detectionModelPath = FaceDetectionModelPathTextBox.Text;
            var recognitionModelPath = FaceRecognitionModelPathTextBox.Text;

            if (string.IsNullOrEmpty(detectionModelPath) || string.IsNullOrEmpty(recognitionModelPath))
            {
                StatusText.Text = "Please specify both face detection and recognition model paths";
                return;
            }

            if (!File.Exists(detectionModelPath))
            {
                StatusText.Text = $"Face detection model not found: {detectionModelPath}";
                return;
            }

            if (!File.Exists(recognitionModelPath))
            {
                StatusText.Text = $"Face recognition model not found: {recognitionModelPath}";
                return;
            }

            try
            {
                StatusText.Text = "Loading face models...";
                _faceRecognizer?.Dispose();
                _faceRecognizer = new FaceRecognizer(detectionModelPath, recognitionModelPath);
                StatusText.Text = "Face models loaded successfully";
                _logger?.Info("MainWindow", $"Face models loaded: {detectionModelPath}, {recognitionModelPath}");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading face models: {ex.Message}";
                _logger?.Error("MainWindow", $"Face models load error: {ex.Message}", ex);
            }
        }

        private async void LoadFacesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_faceRecognizer == null || _faceDatabase == null)
            {
                StatusText.Text = "Face recognizer not available";
                return;
            }

            var folderPath = FacePathTextBox.Text;
            if (string.IsNullOrEmpty(folderPath))
            {
                var dialog = new OpenFileDialog
                {
                    Title = "Select Face Images Folder",
                    CheckFileExists = false,
                    CheckPathExists = true,
                    FileName = "Folder Selection"
                };

                if (dialog.ShowDialog() == true)
                {
                    folderPath = Path.GetDirectoryName(dialog.FileName) ?? "";
                }
            }

            if (!Directory.Exists(folderPath))
            {
                StatusText.Text = $"Folder not found: {folderPath}";
                return;
            }

            try
            {
                StatusText.Text = "Loading face images...";
                await LoadReferenceFacesAsync(folderPath);
                StatusText.Text = "Face images loaded successfully";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error loading faces: {ex.Message}";
                _logger?.Error("MainWindow", $"Load faces error: {ex.Message}", ex);
            }
        }

        private async Task LoadReferenceFacesAsync(string faceImagesPath)
        {
            if (_faceRecognizer == null || _faceDatabase == null) return;

            var imageFiles = Directory.GetFiles(faceImagesPath, "*.*")
                .Where(f => new[] { ".jpg", ".jpeg", ".png", ".bmp" }.Contains(Path.GetExtension(f).ToLower()))
                .ToList();

            if (imageFiles.Count == 0)
            {
                StatusText.Text = $"No image files found in: {faceImagesPath}";
                return;
            }

            foreach (var imagePath in imageFiles)
            {
                try
                {
                    var personName = Path.GetFileNameWithoutExtension(imagePath);
                    using var image = Cv2.ImRead(imagePath);

                    if (image.Empty()) continue;

                    var faces = await _faceRecognizer.DetectFacesAsync(image);
                    if (faces.Count > 0)
                    {
                        var largestFace = faces.OrderByDescending(f => f.BBox.Width * f.BBox.Height).First();
                        var embedding = await _faceRecognizer.ExtractFaceEmbeddingAsync(image, largestFace.BBox);
                        _faceDatabase.RegisterFace(personName, personName, embedding);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Error("MainWindow", $"Error loading face {imagePath}: {ex.Message}", ex);
                }
            }
        }

        private async void IRCameraButton_Click(object sender, RoutedEventArgs e) => await SwitchToCamera(CameraSourceType.Infrared);
        private async void ColorCameraButton_Click(object sender, RoutedEventArgs e) => await SwitchToCamera(CameraSourceType.Color);
        private async void DepthCameraButton_Click(object sender, RoutedEventArgs e) => await SwitchToCamera(CameraSourceType.Depth);

        private async Task SwitchToCamera(CameraSourceType targetType)
        {
            if (_cameraService == null) return;

            var targetSource = _cameraService.AvailableSources.FirstOrDefault(s => s.SourceType == targetType);
            if (targetSource != null)
            {
                await _cameraService.SwitchCameraSourceAsync(targetSource.SourceId);
                CameraComboBox.SelectedItem = targetSource;
            }
            else
            {
                StatusText.Text = $"Camera type not available: {targetType}";
            }
        }

        private void FpsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 初期化が完了していない場合は何もしない
            if (!_isInitialized || _logger == null || StatusText == null) return;

            try
            {
                if (FpsTextBox != null && !string.IsNullOrWhiteSpace(FpsTextBox.Text))
                {
                    if (double.TryParse(FpsTextBox.Text, out double fps))
                    {
                        // FPS値の範囲チェック（0.1～120の範囲で制限）
                        if (fps >= 0.1 && fps <= 120.0)
                        {
                            _targetFps = fps;
                            _frameInterval = TimeSpan.FromMilliseconds(1000.0 / fps);
                            StatusText.Text = $"FPS changed to: {fps:F1}";
                            _logger.Info("MainWindow", $"Frame rate changed to {fps:F1} FPS");

                            // テキストボックスの背景色を正常に戻す
                            FpsTextBox.Background = System.Windows.Media.Brushes.White;
                        }
                        else
                        {
                            // 範囲外の値の場合、背景色を変更して警告
                            FpsTextBox.Background = System.Windows.Media.Brushes.LightPink;
                            StatusText.Text = $"FPS value must be between 0.1 and 120.0";
                        }
                    }
                    else
                    {
                        // 無効な数値の場合、背景色を変更して警告
                        FpsTextBox.Background = System.Windows.Media.Brushes.LightPink;
                        StatusText.Text = "Invalid FPS value. Please enter a valid number.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("MainWindow", $"FPS change error: {ex.Message}", ex);
            }
        }


        private async void OnFrameArrived(object? sender, FrameData frameData)
        {
            // カメラが動作中の場合のみフレームを処理
            if (!_isCameraRunning)
            {
                return;
            }

            // 受信フレーム数をカウント
            _totalReceivedFrames++;
            _logger?.Debug("MainWindow", $"Frame arrived #{_totalReceivedFrames}: camera={_isCameraRunning}, detection={_isDetectionRunning}");

            // 前のフレームがまだ処理中の場合はスキップ
            if (_isProcessingFrame)
            {
                _logger?.Debug("MainWindow", "Frame skipped - previous frame still processing");
                return;
            }

            // フレーム番号ベースの制限（30FPS想定でターゲットFPSに合わせてスキップ）
            var frameSkipInterval = Math.Max(1, (int)Math.Round(30.0 / _targetFps));
            if (_totalReceivedFrames % frameSkipInterval != 0)
            {
                _logger?.Debug("MainWindow", $"Frame skipped due to frame-based rate limit (skip interval: {frameSkipInterval})");
                return;
            }

            // 時間ベースの制限チェック
            var now = DateTime.Now;
            if (now - _lastProcessedTime < _frameInterval)
            {
                _logger?.Debug("MainWindow", "Frame skipped due to time-based rate limit");
                return;
            }

            _isProcessingFrame = true;
            _logger?.Debug("MainWindow", $"Processing frame #{_frameCount + 1} (received #{_totalReceivedFrames})");

            // UIスレッドで処理（非同期）
            _ = Dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    _frameCount++;
                    _logger?.Debug("MainWindow", $"Frame #{_frameCount}: Starting processing");

                    using var frame = frameData.Frame.Clone();
                    _logger?.Debug("MainWindow", $"Frame #{_frameCount}: Frame cloned {frame.Width}x{frame.Height}");

                    // 検出処理は_isDetectionRunningがtrueの場合のみ実行
                    if (_isDetectionRunning)
                    {
                        // 統合検出処理
                        var results = await ProcessFrameUnified(frame);
                        _logger?.Debug("MainWindow", $"Frame #{_frameCount}: Detection completed, Objects: {results.ObjectDetections.Count}, Faces: {results.FaceRecognitions.Count}");

                        // 結果を描画
                        DrawResults(frame, results, frameData.SourceType);

                        // 統計情報を更新
                        UpdateStatistics(results, frameData.SourceType);
                    }
                    else
                    {
                        _logger?.Debug("MainWindow", $"Frame #{_frameCount}: Detection skipped (not running)");
                        // 検出なしの場合は基本的な統計情報のみ更新
                        UpdateBasicStatistics(frameData.SourceType);
                    }

                    // カメラ情報を描画
                    DrawCameraInfo(frame, frameData);
                    _logger?.Debug("MainWindow", $"Frame #{_frameCount}: Drawing completed");

                    // WPFで表示（常に表示）
                    var bitmapSource = MatToBitmapSource(frame);
                    CameraImage.Source = bitmapSource;
                    _logger?.Debug("MainWindow", $"Frame #{_frameCount}: Display updated");

                    // No video textを隠す
                    if (NoVideoText.Visibility == Visibility.Visible)
                    {
                        NoVideoText.Visibility = Visibility.Collapsed;
                    }
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Frame processing error: {ex.Message}";
                    _logger?.Error("MainWindow", $"Frame processing error: {ex.Message}", ex);
                }
                finally
                {
                    _isProcessingFrame = false;
                    _lastProcessedTime = DateTime.Now; // 処理完了時に更新
                    _logger?.Debug("MainWindow", $"Frame processing completed at {_lastProcessedTime:HH:mm:ss.fff}");
                }
            });
        }

        private async Task<UnifiedResults> ProcessFrameUnified(Mat frame)
        {
            // 検出処理用に解像度を下げる（パフォーマンス向上）
            var detectionFrame = frame;
            var scale = 1.0;

            if (frame.Width > 640)
            {
                scale = 640.0 / frame.Width;
                detectionFrame = new Mat();
                Cv2.Resize(frame, detectionFrame, new OpenCvSharp.Size((int)(frame.Width * scale), (int)(frame.Height * scale)));
            }

            var objectTask = ObjectDetectionCheckBox.IsChecked == true && _objectDetector != null
                ? _objectDetector.DetectAsync(detectionFrame)
                : Task.FromResult(new List<Detection>());

            var faceTask = FaceRecognitionCheckBox.IsChecked == true && _faceRecognizer != null
                ? ProcessFaceRecognition(detectionFrame)
                : Task.FromResult(new List<FaceRecognitionResult>());

            await Task.WhenAll(objectTask, faceTask);

            var objectResults = await objectTask;
            var faceResults = await faceTask;

            // スケールが適用されている場合は座標を元に戻す
            if (scale != 1.0)
            {
                var scaleBack = 1.0 / scale;
                objectResults = objectResults.Select(d => new Detection
                {
                    ClassId = d.ClassId,
                    ClassName = d.ClassName,
                    Confidence = d.Confidence,
                    BBox = new System.Drawing.RectangleF(
                        (float)(d.BBox.X * scaleBack),
                        (float)(d.BBox.Y * scaleBack),
                        (float)(d.BBox.Width * scaleBack),
                        (float)(d.BBox.Height * scaleBack)
                    ),
                    Embedding = d.Embedding
                }).ToList();

                faceResults = faceResults.Select(f => new FaceRecognitionResult(
                    new System.Drawing.Rectangle(
                        (int)(f.BoundingBox.X * scaleBack),
                        (int)(f.BoundingBox.Y * scaleBack),
                        (int)(f.BoundingBox.Width * scaleBack),
                        (int)(f.BoundingBox.Height * scaleBack)
                    ),
                    f.DetectionConfidence,
                    f.Similarity,
                    f.Name,
                    f.Landmarks,
                    f.Angles
                )).ToList();
            }

            if (detectionFrame != frame)
            {
                detectionFrame.Dispose();
            }

            return new UnifiedResults(objectResults, faceResults);
        }

        private async Task<List<FaceRecognitionResult>> ProcessFaceRecognition(Mat frame)
        {
            var results = new List<FaceRecognitionResult>();
            if (_faceRecognizer == null || _faceDatabase == null) return results;

            try
            {
                var faces = await _faceRecognizer.DetectFacesAsync(frame);
                foreach (var face in faces)
                {
                    var embedding = await _faceRecognizer.ExtractFaceEmbeddingAsync(frame, face.BBox);
                    var identification = _faceDatabase.IdentifyFace(embedding, 0.5f);

                    results.Add(new FaceRecognitionResult(
                        face.BBox,
                        face.Confidence,
                        identification.Confidence,
                        identification.Name,
                        face.Landmarks,
                        face.Angles
                    ));
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("MainWindow", $"Face recognition error: {ex.Message}", ex);
            }

            return results;
        }

        private void UpdateStatistics(UnifiedResults results, CameraSourceType sourceType)
        {
            var elapsed = DateTime.Now - _startTime;
            var processingFps = _frameCount / elapsed.TotalSeconds;
            var receivingFps = _totalReceivedFrames / elapsed.TotalSeconds;
            var skipRate = _totalReceivedFrames > 0 ? (double)(_totalReceivedFrames - _frameCount) / _totalReceivedFrames * 100 : 0;

            FrameInfoText.Text = $"Processed: {_frameCount} | Received: {_totalReceivedFrames} | Processing FPS: {processingFps:F1} | Target: {_targetFps:F1} | Skip: {skipRate:F1}% | Camera: {sourceType}";
            DetectionInfoText.Text = $"Objects: {results.ObjectDetections.Count} | Faces: {results.FaceRecognitions.Count}";

            var knownFaces = results.FaceRecognitions.Count(f => f.Name != "Unknown");
            if (knownFaces > 0)
            {
                DetectionInfoText.Text += $" | Known: {knownFaces}";
            }
        }

        private void UpdateBasicStatistics(CameraSourceType sourceType)
        {
            var elapsed = DateTime.Now - _startTime;
            var processingFps = _frameCount / elapsed.TotalSeconds;
            var receivingFps = _totalReceivedFrames / elapsed.TotalSeconds;
            var skipRate = _totalReceivedFrames > 0 ? (double)(_totalReceivedFrames - _frameCount) / _totalReceivedFrames * 100 : 0;

            FrameInfoText.Text = $"Processed: {_frameCount} | Received: {_totalReceivedFrames} | Processing FPS: {processingFps:F1} | Target: {_targetFps:F1} | Skip: {skipRate:F1}% | Camera: {sourceType}";
            DetectionInfoText.Text = "Detection: Off";
        }

        private void OnStatusChanged(object? sender, string status)
        {
            Dispatcher.BeginInvoke(() =>
            {
                StatusText.Text = status;
            });
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_cameraService != null)
            {
                await _cameraService.StopAsync();
                _cameraService.Dispose();
            }
            _objectDetector?.Dispose();
            _faceRecognizer?.Dispose();
            _logger?.Dispose();
        }

        // 以下、描画メソッドを追加（IRUnifiedDetectorMainから移植）
        private static void DrawResults(Mat frame, UnifiedResults results, CameraSourceType sourceType)
        {
            if (sourceType == CameraSourceType.Infrared)
            {
                DrawIRSpecificResults(frame, results);
            }
            else
            {
                DrawStandardResults(frame, results);
            }
        }

        private static void DrawIRSpecificResults(Mat frame, UnifiedResults results)
        {
            var authenticatedSmartphones = IdentifyAuthenticatedSmartphones(results);
            DrawObjectDetections(frame, results.ObjectDetections, authenticatedSmartphones, true);
            DrawFaceRecognitions(frame, results.FaceRecognitions, true);
        }

        private static void DrawStandardResults(Mat frame, UnifiedResults results)
        {
            var authenticatedSmartphones = IdentifyAuthenticatedSmartphones(results);
            DrawObjectDetections(frame, results.ObjectDetections, authenticatedSmartphones, false);
            DrawFaceRecognitions(frame, results.FaceRecognitions, false);
        }

        private static void DrawObjectDetections(Mat frame, List<Detection> detections, HashSet<int> authenticatedSmartphones, bool isIR)
        {
            for (int i = 0; i < detections.Count; i++)
            {
                var detection = detections[i];
                var rect = new OpenCvSharp.Rect(
                    (int)detection.BBox.X,
                    (int)detection.BBox.Y,
                    (int)detection.BBox.Width,
                    (int)detection.BBox.Height
                );

                var color = isIR ? new Scalar(255, 255, 255) : new Scalar(0, 255, 0);
                var label = $"{detection.ClassName}: {detection.Confidence:F2}";

                if (detection.ClassName.ToLower().Contains("cell phone") ||
                    detection.ClassName.ToLower().Contains("phone") ||
                    detection.ClassName.ToLower().Contains("smartphone"))
                {
                    if (authenticatedSmartphones.Contains(i))
                    {
                        color = isIR ? new Scalar(255, 128, 255) : new Scalar(255, 0, 255);
                        label += " [AUTH]";
                    }
                    else
                    {
                        color = isIR ? new Scalar(255, 200, 0) : new Scalar(0, 165, 255);
                        label += " [UNAUTH]";
                    }
                }

                Cv2.Rectangle(frame, rect, color, 2);
                var labelPos = new OpenCvSharp.Point(rect.X, rect.Y - 5);
                Cv2.PutText(frame, label, labelPos, HersheyFonts.HersheyDuplex, 0.5, color, 1);
            }
        }

        private static void DrawFaceRecognitions(Mat frame, List<FaceRecognitionResult> recognitions, bool isIR)
        {
            foreach (var recognition in recognitions)
            {
                var rect = new OpenCvSharp.Rect(
                    recognition.BoundingBox.X,
                    recognition.BoundingBox.Y,
                    recognition.BoundingBox.Width,
                    recognition.BoundingBox.Height
                );

                Scalar color;
                if (isIR)
                {
                    color = recognition.Similarity >= 0.6f
                        ? new Scalar(255, 255, 255)
                        : recognition.Similarity >= 0.4f
                            ? new Scalar(200, 200, 200)
                            : new Scalar(128, 128, 128);
                }
                else
                {
                    color = recognition.Similarity >= 0.6f
                        ? new Scalar(255, 0, 0)
                        : recognition.Similarity >= 0.4f
                            ? new Scalar(0, 255, 255)
                            : new Scalar(0, 0, 255);
                }

                Cv2.Rectangle(frame, rect, color, 3);

                var label = recognition.Name != "Unknown"
                    ? $"{recognition.Name}: {recognition.Similarity:F2}"
                    : $"Unknown: {recognition.Similarity:F2}";

                var labelPos = new OpenCvSharp.Point(rect.X, rect.Y - 25);
                var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheyDuplex, 0.6, 2, out var baseline);
                var textRect = new OpenCvSharp.Rect(labelPos.X, labelPos.Y - textSize.Height - baseline,
                                       textSize.Width, textSize.Height + baseline);
                Cv2.Rectangle(frame, textRect, new Scalar(0, 0, 0), -1);
                Cv2.PutText(frame, label, labelPos, HersheyFonts.HersheyDuplex, 0.6, color, 2);
            }
        }

        private static void DrawCameraInfo(Mat frame, FrameData frameData)
        {
            var cameraInfo = $"Camera: {frameData.SourceType} | Time: {frameData.Timestamp:HH:mm:ss.fff}";
            var color = new Scalar(255, 255, 255);
            var bgColor = new Scalar(0, 0, 0);

            var textSize = Cv2.GetTextSize(cameraInfo, HersheyFonts.HersheyDuplex, 0.6, 2, out var baseline);
            var bgRect = new OpenCvSharp.Rect(10, 10, textSize.Width + 20, textSize.Height + baseline + 10);

            Cv2.Rectangle(frame, bgRect, bgColor, -1);
            Cv2.PutText(frame, cameraInfo, new OpenCvSharp.Point(20, 30),
                       HersheyFonts.HersheyDuplex, 0.6, color, 2);
        }

        private static HashSet<int> IdentifyAuthenticatedSmartphones(UnifiedResults results)
        {
            var authenticatedSmartphones = new HashSet<int>();
            var authenticatedFaces = results.FaceRecognitions
                .Where(face => face.Similarity >= 0.4f && face.Name != "Unknown")
                .ToList();

            if (authenticatedFaces.Count == 0) return authenticatedSmartphones;

            var personDetections = results.ObjectDetections
                .Select((detection, index) => new { Detection = detection, Index = index })
                .Where(x => x.Detection.ClassName.ToLower() == "person")
                .ToList();

            foreach (var personData in personDetections)
            {
                var personRect = new System.Drawing.Rectangle(
                    (int)personData.Detection.BBox.X,
                    (int)personData.Detection.BBox.Y,
                    (int)personData.Detection.BBox.Width,
                    (int)personData.Detection.BBox.Height
                );

                var facesInPerson = authenticatedFaces
                    .Where(face => DoesRectanglesOverlap(personRect, face.BoundingBox))
                    .ToList();

                if (facesInPerson.Count > 0)
                {
                    for (int i = 0; i < results.ObjectDetections.Count; i++)
                    {
                        var detection = results.ObjectDetections[i];
                        if (!IsSmartphone(detection.ClassName)) continue;

                        var smartphoneRect = new System.Drawing.Rectangle(
                            (int)detection.BBox.X,
                            (int)detection.BBox.Y,
                            (int)detection.BBox.Width,
                            (int)detection.BBox.Height
                        );

                        if (DoesRectanglesOverlap(personRect, smartphoneRect))
                        {
                            authenticatedSmartphones.Add(i);
                        }
                    }
                }
            }

            return authenticatedSmartphones;
        }

        private static bool IsSmartphone(string className)
        {
            var lowerName = className.ToLower();
            return lowerName.Contains("cell phone") ||
                   lowerName.Contains("phone") ||
                   lowerName.Contains("smartphone");
        }

        private static bool DoesRectanglesOverlap(System.Drawing.Rectangle rect1, System.Drawing.Rectangle rect2)
        {
            return rect1.Left < rect2.Right &&
                   rect1.Right > rect2.Left &&
                   rect1.Top < rect2.Bottom &&
                   rect1.Bottom > rect2.Top;
        }

        private static BitmapSource MatToBitmapSource(Mat mat)
        {
            var bgraMat = new Mat();
            Cv2.CvtColor(mat, bgraMat, ColorConversionCodes.BGR2BGRA);

            var bitmap = new WriteableBitmap(bgraMat.Width, bgraMat.Height, 96, 96, PixelFormats.Bgra32, null);

            var dataSize = (int)(bgraMat.Total() * bgraMat.ElemSize());
            var buffer = new byte[dataSize];
            System.Runtime.InteropServices.Marshal.Copy(bgraMat.Data, buffer, 0, dataSize);

            bitmap.WritePixels(new Int32Rect(0, 0, bgraMat.Width, bgraMat.Height), buffer, (int)bgraMat.Step(), 0);

            bgraMat.Dispose();
            return bitmap;
        }
    }

    // 統合検出結果の定義
    public sealed record UnifiedResults(
        List<Detection> ObjectDetections,
        List<FaceRecognitionResult> FaceRecognitions
    );

    public sealed record FaceRecognitionResult(
        System.Drawing.Rectangle BoundingBox,
        float DetectionConfidence,
        float Similarity,
        string Name,
        FaceLandmarks? Landmarks = null,
        FaceAngles? Angles = null
    );
}
