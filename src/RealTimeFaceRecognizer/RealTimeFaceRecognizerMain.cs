using OpenCvSharp;
using Recognizer;
using System.Diagnostics;
using System.Drawing;

namespace RealTimeFaceRecognizer;

public class RealTimeFaceRecognizerMain
{
  private readonly FaceRecognizer _faceRecognizer;
  private readonly string _faceImagesPath;
  private readonly List<ReferenceEmbedding> _referenceEmbeddings = new();
  private const string WindowName = "Real-time Face Recognition";

  public RealTimeFaceRecognizerMain(FaceRecognizer faceRecognizer, string faceImagesPath)
  {
    _faceRecognizer = faceRecognizer ?? throw new ArgumentNullException(nameof(faceRecognizer));
    _faceImagesPath = faceImagesPath ?? throw new ArgumentNullException(nameof(faceImagesPath));
  }

  public async Task LoadReferenceFacesAsync()
  {
    var totalStopwatch = Stopwatch.StartNew();

    if (!Directory.Exists(_faceImagesPath))
    {
      throw new DirectoryNotFoundException($"顔画像フォルダが見つかりません: {_faceImagesPath}");
    }

    var imageFiles = Directory.GetFiles(_faceImagesPath, "*.*")
        .Where(f => new[] { ".jpg", ".jpeg", ".png", ".bmp" }.Contains(Path.GetExtension(f).ToLower()))
        .ToList();

    if (imageFiles.Count == 0)
    {
      throw new InvalidOperationException($"顔画像フォルダに画像ファイルが見つかりません: {_faceImagesPath}");
    }

    Console.WriteLine($"[参照顔読み込み] {imageFiles.Count} 個の画像ファイルを処理します。");

    foreach (var imagePath in imageFiles)
    {
      try
      {
        var imageStopwatch = Stopwatch.StartNew();
        Console.WriteLine($"[参照画像] 処理中: {Path.GetFileName(imagePath)}");

        using var originalImage = Cv2.ImRead(imagePath);
        if (originalImage.Empty())
        {
          Console.WriteLine($"  警告: 画像を読み込めませんでした");
          continue;
        }

        Console.WriteLine($"  元画像サイズ: {originalImage.Width}x{originalImage.Height}");

        // 処理速度向上のため画像をダウンスケール
        using var downscaledImage = DownscaleImage(originalImage, out var scaleRatio);
        Console.WriteLine($"  ダウンスケール後: {downscaledImage.Width}x{downscaledImage.Height} (比率: {scaleRatio:F1})");

        var faces = await _faceRecognizer.DetectFacesAsync(downscaledImage);
        Console.WriteLine($"  検出された顔: {faces.Count} 個");

        if (faces.Count > 0)
        {
          // 最も矩形の大きい顔を選択（面積 = 幅 × 高さ）
          var largestFace = faces
              .OrderByDescending(f => f.BBox.Width * f.BBox.Height)
              .First();

          // 座標を元の解像度にアップスケール
          var upscaledBBox = UpscaleBoundingBox(largestFace.BBox, scaleRatio);

          Console.WriteLine($"  採用する顔 (ダウンスケール): {largestFace.BBox.Width}x{largestFace.BBox.Height}");
          Console.WriteLine($"  採用する顔 (元解像度): {upscaledBBox.Width}x{upscaledBBox.Height} (面積: {upscaledBBox.Width * upscaledBBox.Height})");

          // 元の解像度の画像から顔埋め込みベクトルを抽出
          var embedding = await _faceRecognizer.ExtractFaceEmbeddingAsync(originalImage, upscaledBBox);
          _referenceEmbeddings.Add(new ReferenceEmbedding
          {
            Embedding = embedding,
            SourceFile = Path.GetFileName(imagePath)
          });

          imageStopwatch.Stop();
          Console.WriteLine($"  処理時間: {imageStopwatch.ElapsedMilliseconds}ms");
        }
        else
        {
          imageStopwatch.Stop();
          Console.WriteLine($"  処理時間: {imageStopwatch.ElapsedMilliseconds}ms (顔検出なし)");
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"  エラー: {ex.Message}");
      }
    }

    if (_referenceEmbeddings.Count == 0)
    {
      throw new InvalidOperationException("参照顔の埋め込みベクトルを生成できませんでした。");
    }

    totalStopwatch.Stop();
    Console.WriteLine($"\n[参照顔読み込み完了] 合計 {_referenceEmbeddings.Count} 個の顔埋め込みベクトルを生成しました。");
    Console.WriteLine($"[参照顔読み込み完了] 総処理時間: {totalStopwatch.ElapsedMilliseconds}ms ({totalStopwatch.Elapsed.TotalSeconds:F1}秒)");
  }

  public void Start(VideoCapture capture)
  {
    using var frame = new Mat();
    var frameCount = 0;
    var startTime = DateTime.Now;

    Cv2.NamedWindow(WindowName);

    while (true)
    {
      if (!capture.Read(frame) || frame.Empty())
      {
        Console.WriteLine("カメラからフレームを読み取れませんでした");
        break;
      }

      frameCount++;

      // フレーム処理全体の時間計測
      var frameStopwatch = Stopwatch.StartNew();

      // 顔検出と認識
      var recognitionResults = ProcessFrame(frame).GetAwaiter().GetResult();

      // 結果の描画
      DrawRecognitionResults(frame, recognitionResults);

      // フレームの表示
      Cv2.ImShow(WindowName, frame);

      frameStopwatch.Stop();

      // 認識結果がある場合のみフレーム処理時間をログ出力
      if (recognitionResults.Count > 0)
      {
        Console.WriteLine($"[フレーム{frameCount}] 総処理時間: {frameStopwatch.ElapsedMilliseconds}ms");
      }

      // パフォーマンス情報の表示（30フレームごと）
      if (frameCount % 30 == 0)
      {
        var elapsed = DateTime.Now - startTime;
        var currentFps = frameCount / elapsed.TotalSeconds;
        Console.WriteLine($"[パフォーマンス] FPS: {currentFps:F1}, 処理フレーム数: {frameCount}");
      }

      // キー入力の確認
      var key = Cv2.WaitKey(1);
      if (key == 'q' || key == 'Q' || key == 27) // q, Q, ESC
      {
        Console.WriteLine("終了します...");
        break;
      }
    }

    Cv2.DestroyWindow(WindowName);
  }

  private async Task<List<FaceRecognitionResult>> ProcessFrame(Mat originalFrame)
  {
    var results = new List<FaceRecognitionResult>();
    var totalStopwatch = Stopwatch.StartNew();

    try
    {
      // 処理速度向上のため画像をダウンスケール
      using var downscaledFrame = DownscaleImage(originalFrame, out var scaleRatio);

      // 顔の検出（ダウンスケール画像で実行）
      var detectionStopwatch = Stopwatch.StartNew();
      var faces = await _faceRecognizer.DetectFacesAsync(downscaledFrame);
      detectionStopwatch.Stop();

      Console.WriteLine($"[顔検出] 検出時間: {detectionStopwatch.ElapsedMilliseconds}ms, 検出数: {faces.Count}");
      Console.WriteLine($"[画像解像度] 元: {originalFrame.Width}x{originalFrame.Height}, ダウンスケール: {downscaledFrame.Width}x{downscaledFrame.Height}");

      // 各顔に対して認識処理
      for (int i = 0; i < faces.Count; i++)
      {
        var face = faces[i];

        // 座標を元の解像度にアップスケール
        var upscaledBBox = UpscaleBoundingBox(face.BBox, scaleRatio);

        Console.WriteLine($"[顔{i + 1}] ダウンスケール位置: ({face.BBox.X}, {face.BBox.Y}), サイズ: {face.BBox.Width}x{face.BBox.Height}");
        Console.WriteLine($"[顔{i + 1}] 元解像度位置: ({upscaledBBox.X}, {upscaledBBox.Y}), サイズ: {upscaledBBox.Width}x{upscaledBBox.Height}, 検出信頼度: {face.Confidence:F3}");

        // 顔埋め込みベクトルの抽出（元の解像度の画像から）
        var embeddingStopwatch = Stopwatch.StartNew();
        var embedding = await _faceRecognizer.ExtractFaceEmbeddingAsync(originalFrame, upscaledBBox);
        embeddingStopwatch.Stop();

        Console.WriteLine($"[顔{i + 1}] 埋め込み抽出時間: {embeddingStopwatch.ElapsedMilliseconds}ms");

        // 参照顔との比較
        var comparisonStopwatch = Stopwatch.StartNew();
        var maxSimilarity = 0.0f;
        string bestMatchSource = "";
        var similarities = new List<float>();

        foreach (var refEmbedding in _referenceEmbeddings)
        {
          var similarity = FaceRecognizer.CompareFaces(embedding, refEmbedding.Embedding);
          similarities.Add(similarity);

          if (similarity > maxSimilarity)
          {
            maxSimilarity = similarity;
            bestMatchSource = refEmbedding.SourceFile;
          }
        }
        comparisonStopwatch.Stop();

        // 類似度統計の計算
        var avgSimilarity = similarities.Average();
        var minSimilarity = similarities.Min();

        Console.WriteLine($"[顔{i + 1}] 類似度計算時間: {comparisonStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"[顔{i + 1}] 類似度統計 - 最大: {maxSimilarity:F3}, 平均: {avgSimilarity:F3}, 最小: {minSimilarity:F3}");
        Console.WriteLine($"[顔{i + 1}] 最適合画像: {bestMatchSource}");

        // 判定結果のログ出力
        var matchLevel = maxSimilarity >= 0.6f ? "高信頼" : maxSimilarity >= 0.4f ? "中信頼" : "低信頼";
        Console.WriteLine($"[顔{i + 1}] 判定結果: {matchLevel} (類似度: {maxSimilarity:F3})");

        // 結果には元の解像度の座標を使用
        results.Add(new FaceRecognitionResult
        {
          BoundingBox = upscaledBBox,
          Confidence = face.Confidence,
          Similarity = maxSimilarity,
          MatchSource = bestMatchSource
        });
      }
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[エラー] フレーム処理エラー: {ex.Message}");
    }

    totalStopwatch.Stop();
    if (results.Count > 0)
    {
      Console.WriteLine($"[総合] フレーム処理完了時間: {totalStopwatch.ElapsedMilliseconds}ms, 処理顔数: {results.Count}");
      Console.WriteLine("----------------------------------------");
    }

    return results;
  }

  private static void DrawRecognitionResults(Mat frame, List<FaceRecognitionResult> results)
  {
    foreach (var result in results)
    {
      // バウンディングボックスの描画
      var rect = new Rect(
          result.BoundingBox.X,
          result.BoundingBox.Y,
          result.BoundingBox.Width,
          result.BoundingBox.Height
      );

      // 類似度に応じて色を変える（高い類似度は緑、低い類似度は赤）
      var color = result.Similarity >= 0.6f
          ? new Scalar(0, 255, 0)    // 緑
          : result.Similarity >= 0.4f
              ? new Scalar(0, 255, 255) // 黄
              : new Scalar(0, 0, 255);  // 赤

      Cv2.Rectangle(frame, rect, color, 2);

      // 類似度の表示
      var label = $"Sim: {result.Similarity:F3}";
      var labelPos = new OpenCvSharp.Point(rect.X, rect.Y - 10);

      // 背景付きテキスト描画（読みやすさのため）
      var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, 0.6, 1, out var baseline);
      var textRect = new Rect(
          labelPos.X,
          labelPos.Y - textSize.Height - baseline,
          textSize.Width,
          textSize.Height + baseline
      );

      Cv2.Rectangle(frame, textRect, new Scalar(0, 0, 0), -1);
      Cv2.PutText(frame, label, labelPos, HersheyFonts.HersheySimplex, 0.6, color, 1);
    }
  }

  /// <summary>
  /// 処理速度向上のため画像をダウンスケールする
  /// </summary>
  private static Mat DownscaleImage(Mat originalImage, out float scaleRatio)
  {
    scaleRatio = Constants.ImageProcessing.DownscaleRatio;
    var newWidth = (int)(originalImage.Width * scaleRatio);
    var newHeight = (int)(originalImage.Height * scaleRatio);

    var downscaled = new Mat();
    Cv2.Resize(originalImage, downscaled, new OpenCvSharp.Size(newWidth, newHeight));

    return downscaled;
  }

  /// <summary>
  /// ダウンスケールされた座標を元の解像度にアップスケールする
  /// </summary>
  private static Rectangle UpscaleBoundingBox(Rectangle downscaledBox, float scaleRatio)
  {
    var upscaleRatio = 1.0f / scaleRatio;
    return new Rectangle(
        (int)(downscaledBox.X * upscaleRatio),
        (int)(downscaledBox.Y * upscaleRatio),
        (int)(downscaledBox.Width * upscaleRatio),
        (int)(downscaledBox.Height * upscaleRatio)
    );
  }

  private class ReferenceEmbedding
  {
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public string SourceFile { get; set; } = string.Empty;
  }

  private class FaceRecognitionResult
  {
    public Rectangle BoundingBox { get; set; }
    public float Confidence { get; set; }
    public float Similarity { get; set; }
    public string MatchSource { get; set; } = string.Empty;
  }
}
