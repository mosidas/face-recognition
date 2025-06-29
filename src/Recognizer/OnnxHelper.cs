using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.Drawing;

namespace Recognizer;

public class OnnxHelper
{
  /// <summary>
  /// モデルを読み込む
  /// </summary>
  /// <param name="modelPath">ONNXモデルファイルのパス</param>
  /// <returns>推論セッション</returns>
  public static InferenceSession LoadModel(string modelPath)
  {
    // SessionOptionsの設定
    var sessionOptions = new SessionOptions
    {
      GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
      ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
      EnableCpuMemArena = true
    };

    // GPUが利用可能な場合はGPUを使用
    try
    {
      sessionOptions.AppendExecutionProvider_CUDA(0);
    }
    catch
    {
      // CUDAが利用できない場合はCPUを使用
      Console.WriteLine("CUDA is not available. Using CPU provider.");
    }

    // モデルの読み込み
    var session = new InferenceSession(modelPath, sessionOptions);

    // 入力と出力の情報を表示
    Console.WriteLine("Model loaded successfully.");
    Console.WriteLine("Input metadata:");
    foreach (var input in session.InputMetadata)
    {
      Console.WriteLine($"  Name: {input.Key}");
      Console.WriteLine($"  Shape: [{string.Join(", ", input.Value.Dimensions)}]");
      Console.WriteLine($"  Type: {input.Value.ElementType}");
    }

    Console.WriteLine("Output metadata:");
    foreach (var output in session.OutputMetadata)
    {
      Console.WriteLine($"  Name: {output.Key}");
      Console.WriteLine($"  Shape: [{string.Join(", ", output.Value.Dimensions)}]");
      Console.WriteLine($"  Type: {output.Value.ElementType}");
    }

    return session;
  }

  /// <summary>
  /// 推論を実行する(画像ファイルから)
  /// </summary>
  /// <param name="session">推論セッション</param>
  /// <param name="inputPath">入力画像のパス</param>
  /// <returns>推論結果</returns>
  public static async Task<InferenceResult> Run(InferenceSession session, string inputPath)
  {
    return await Task.Run(() =>
    {
      // 画像の読み込み
      using var image = Cv2.ImRead(inputPath);
      if (image.Empty())
      {
        throw new ArgumentException($"Failed to load image: {inputPath}");
      }

      // 入力メタデータの取得
      var inputMeta = session.InputMetadata.First();
      var inputName = inputMeta.Key;
      var inputShape = inputMeta.Value.Dimensions;

      // 画像の前処理
      var inputTensor = PreprocessImage(image, inputShape);

      // 推論の実行
      var inputs = new List<NamedOnnxValue>
      {
        NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
      };

      using var results = session.Run(inputs);

      // 結果の処理
      return ProcessResults(results, image.Size());
    });
  }

  /// <summary>
  /// 画像の前処理
  /// </summary>
  private static DenseTensor<float> PreprocessImage(Mat image, int[] inputShape)
  {
    // 入力形状の解析 (batch_size, channels, height, width) または (batch_size, height, width, channels)
    var batchSize = inputShape[0] == -1 ? 1 : inputShape[0];
    var (channels, height, width) = GetImageDimensions(inputShape);

            // 画像のリサイズ
        using var resized = new Mat();
        Cv2.Resize(image, resized, new OpenCvSharp.Size(width, height));

        // BGRからRGBへの変換
        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        // 正規化とテンソル化
        var tensor = new DenseTensor<float>(new[] { batchSize, channels, height, width });

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = rgb.At<Vec3b>(y, x);

                // 一般的な正規化 (0-255 -> 0-1) - すべてのモデルで統一
                tensor[0, 0, y, x] = pixel[0] / Constants.ImageProcessing.NormalizationMaxValue;  // R
                tensor[0, 1, y, x] = pixel[1] / Constants.ImageProcessing.NormalizationMaxValue;  // G
                tensor[0, 2, y, x] = pixel[2] / Constants.ImageProcessing.NormalizationMaxValue;  // B
            }
        }

        // Matオブジェクトの解放は自動で実行される

    return tensor;
  }

  /// <summary>
  /// 入力形状から画像の次元を取得
  /// </summary>
  private static (int channels, int height, int width) GetImageDimensions(int[] inputShape)
  {
    // NCHW形式 (batch, channels, height, width)
    if (inputShape.Length == 4 && (inputShape[1] == 3 || inputShape[1] == 1))
    {
      return (inputShape[1], inputShape[2], inputShape[3]);
    }
    // NHWC形式 (batch, height, width, channels)
    else if (inputShape.Length == 4 && (inputShape[3] == 3 || inputShape[3] == 1))
    {
      return (inputShape[3], inputShape[1], inputShape[2]);
    }
    else
    {
      throw new NotSupportedException($"Unsupported input shape: [{string.Join(", ", inputShape)}]");
    }
  }

  /// <summary>
  /// 推論結果の処理
  /// </summary>
  private static InferenceResult ProcessResults(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, OpenCvSharp.Size imageSize)
  {
    var inferenceResult = new InferenceResult
    {
      ImageSize = imageSize
    };

    foreach (var result in results)
    {
      var outputName = result.Name;
      var outputTensor = result.AsEnumerable<float>().ToArray();

      // 出力の形状を取得
      var tensorShape = result.AsTensor<float>().Dimensions.ToArray();

      inferenceResult.Outputs.Add(outputName, new OutputData
      {
        Name = outputName,
        Data = outputTensor,
        Shape = tensorShape
      });

      Console.WriteLine($"Output '{outputName}' shape: [{string.Join(", ", tensorShape)}]");
    }

    return inferenceResult;
  }

  /// <summary>
  /// バウンディングボックスのNMS（Non-Maximum Suppression）処理
  /// </summary>
  public static List<Detection> ApplyNMS(List<Detection> detections, float nmsThreshold = Constants.Thresholds.DefaultNmsThreshold)
  {
    if (detections.Count == 0) return detections;

    // 信頼度でソート（降順）
    detections.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));

    var selected = new List<Detection>();
    var active = new bool[detections.Count];
    for (int i = 0; i < active.Length; i++) active[i] = true;

    for (int i = 0; i < detections.Count; i++)
    {
      if (!active[i]) continue;

      selected.Add(detections[i]);

      for (int j = i + 1; j < detections.Count; j++)
      {
        if (!active[j]) continue;
        if (detections[i].ClassId != detections[j].ClassId) continue;

        var iou = CalculateIoU(detections[i].BBox, detections[j].BBox);
        if (iou > nmsThreshold)
        {
          active[j] = false;
        }
      }
    }

    return selected;
  }

  /// <summary>
  /// IoU（Intersection over Union）の計算
  /// </summary>
  private static float CalculateIoU(RectangleF box1, RectangleF box2)
  {
    var x1 = Math.Max(box1.Left, box2.Left);
    var y1 = Math.Max(box1.Top, box2.Top);
    var x2 = Math.Min(box1.Right, box2.Right);
    var y2 = Math.Min(box1.Bottom, box2.Bottom);

    var intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
    var area1 = box1.Width * box1.Height;
    var area2 = box2.Width * box2.Height;
    var union = area1 + area2 - intersection;

    return union > 0 ? intersection / union : 0;
  }
}

/// <summary>
/// 推論結果
/// </summary>
public class InferenceResult
{
  public OpenCvSharp.Size ImageSize { get; set; }
  public Dictionary<string, OutputData> Outputs { get; set; } = new Dictionary<string, OutputData>();
}

/// <summary>
/// 出力データ
/// </summary>
public class OutputData
{
  public string Name { get; set; } = string.Empty;
  public float[] Data { get; set; } = [];
  public int[] Shape { get; set; } = [];
}

/// <summary>
/// 検出結果
/// </summary>
public class Detection
{
  public int ClassId { get; set; }
  public string ClassName { get; set; } = string.Empty;
  public float Confidence { get; set; }
  public RectangleF BBox { get; set; }
  public float[]? Embedding { get; set; } // 顔認証用の特徴ベクトル
}
