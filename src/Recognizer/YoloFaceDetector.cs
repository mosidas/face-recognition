using System.Drawing;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace Recognizer;

/// <summary>
/// YOLO顔検出器（YOLOv8n-face / YOLOv11-face対応）
/// </summary>
public sealed class YoloFaceDetector(
    string modelPath,
    float confidenceThreshold = Constants.Thresholds.DefaultFaceDetectionThreshold,
    YoloFaceModelType modelType = YoloFaceModelType.Auto,
    bool enableDebug = false) : IDisposable
{
  private readonly InferenceSession _session = OnnxHelper.LoadModel(modelPath);
  private readonly float _confidenceThreshold = confidenceThreshold;
  private readonly YoloFaceModelType _modelType = modelType;
  private readonly bool _enableDebug = enableDebug;

  public async Task<List<FaceDetection>> DetectAsync(string imagePath, CancellationToken cancellationToken = default)
  {
    var result = await OnnxHelper.Run(_session, imagePath, cancellationToken).ConfigureAwait(false);
    return ParseOutput(result);
  }

  public async Task<List<FaceDetection>> DetectAsync(Mat inputImage, CancellationToken cancellationToken = default)
  {
    var result = await OnnxHelper.Run(_session, inputImage, cancellationToken).ConfigureAwait(false);
    return ParseOutput(result);
  }

  private List<FaceDetection> ParseOutput(InferenceResult result)
  {
    var output = result.Outputs.First().Value;
    var shape = output.Shape;
    var data = output.Data;

    if (_enableDebug)
    {
      Console.WriteLine($"\n=== YOLOv8n-face デバッグ情報 ===");
      Console.WriteLine($"出力形状: [{string.Join(", ", shape)}]");
      Console.WriteLine($"データ長: {data.Length}");
      Console.WriteLine($"画像サイズ: {result.ImageSize}");

      // 最初の10個のデータ値を表示
      Console.WriteLine($"最初の10個の値: [{string.Join(", ", data.Take(10).Select(x => x.ToString("F4")))}]");

      // 最後の10個のデータ値を表示
      Console.WriteLine($"最後の10個の値: [{string.Join(", ", data.Skip(Math.Max(0, data.Length - 10)).Select(x => x.ToString("F4")))}]");

      // データの統計情報
      Console.WriteLine($"最小値: {data.Min():F4}");
      Console.WriteLine($"最大値: {data.Max():F4}");
      Console.WriteLine($"平均値: {data.Average():F4}");
    }

    // モデルタイプ判別（自動の場合）
    var detectedModelType = _modelType == YoloFaceModelType.Auto
        ? DetectModelType(shape)
        : _modelType;

    if (_enableDebug)
    {
      Console.WriteLine($"検出されたモデルタイプ: {detectedModelType}");
    }

    return detectedModelType switch
    {
      YoloFaceModelType.Yolov8n => ParseYolov8nOutput(result),
      YoloFaceModelType.Yolov11 => ParseYolov11Output(result),
      _ => throw new NotSupportedException($"Unsupported model type: {detectedModelType}")
    };
  }

  /// <summary>
  /// 出力形状からモデルタイプを自動判別
  /// </summary>
  private static YoloFaceModelType DetectModelType(int[] shape)
  {
    if (shape.Length >= 3)
    {
      // YOLOv8n-face/YOLOv11-face共に転置出力: [1, features, predictions]
      // YOLOv11-face: [1, 5, 8400] 等
      if (shape[1] == Constants.YoloOutput.Yolov11FaceOutputFeatures && shape[2] > 1000)
      {
        return YoloFaceModelType.Yolov11;
      }
      // YOLOv8n-face: [1, 20, 8400] 等（転置出力、20特徴）
      else if (shape[1] > Constants.YoloOutput.Yolov11FaceOutputFeatures && shape[2] > 1000)
      {
        return YoloFaceModelType.Yolov8n;
      }
      // 従来の標準出力: [1, predictions, features]
      else if (shape[2] == Constants.YoloOutput.Yolov8FaceOutputFeatures)
      {
        return YoloFaceModelType.Yolov8n;
      }
    }

    // デフォルトはYOLOv8n形式
    return YoloFaceModelType.Yolov8n;
  }

  /// <summary>
  /// YOLOv8n-face出力のパース（転置形式）
  /// </summary>
  private List<FaceDetection> ParseYolov8nOutput(InferenceResult result)
  {
    var detections = new List<FaceDetection>();
    var output = result.Outputs.First().Value;
    var predictions = output.Data;
    var shape = output.Shape;

    // YOLOv8n-faceの実際の形式を判別
    if (shape[1] > 10) // [1, 20, 8400] 形式（転置出力）
    {
      return ParseYolov8nTransposedOutput(result);
    }
    else // [1, num_predictions, 5] 形式（標準出力）
    {
      return ParseYolov8nStandardOutput(result);
    }
  }

  /// <summary>
  /// YOLOv8n-face転置出力のパース [1, 20, 8400]
  /// </summary>
  private List<FaceDetection> ParseYolov8nTransposedOutput(InferenceResult result)
  {
    var detections = new List<FaceDetection>();
    var output = result.Outputs.First().Value;
    var predictions = output.Data;
    var shape = output.Shape;

    // YOLOv8n-face転置形式: [1, features, num_predictions]
    int numFeatures = shape[1]; // 通常20
    int numPredictions = shape[2]; // 通常8400

    var (scaleX, scaleY) = CalculateScale(result.ImageSize);

    if (_enableDebug)
    {
      Console.WriteLine($"\n=== YOLOv8n-face 転置パース情報 ===");
      Console.WriteLine($"特徴数: {numFeatures}");
      Console.WriteLine($"予測数: {numPredictions}");
      Console.WriteLine($"スケール: X={scaleX:F2}, Y={scaleY:F2}");
      Console.WriteLine($"信頼度閾値: {_confidenceThreshold}");
    }

    int validDetectionCount = 0;

    for (int i = 0; i < numPredictions; i++)
    {
      // バウンディングボックス座標とスコア取得
      var cx = predictions[0 * numPredictions + i];
      var cy = predictions[1 * numPredictions + i];
      var w = predictions[2 * numPredictions + i];
      var h = predictions[3 * numPredictions + i];
      var confidence = predictions[4 * numPredictions + i];

      // ランドマーク座標取得（5個のランドマーク × 3要素[x,y,confidence] = 15個）
      var leftEyeX = predictions[5 * numPredictions + i];
      var leftEyeY = predictions[6 * numPredictions + i];
      var rightEyeX = predictions[8 * numPredictions + i];
      var rightEyeY = predictions[9 * numPredictions + i];
      var noseX = predictions[11 * numPredictions + i];
      var noseY = predictions[12 * numPredictions + i];
      var leftMouthX = predictions[14 * numPredictions + i];
      var leftMouthY = predictions[15 * numPredictions + i];
      var rightMouthX = predictions[17 * numPredictions + i];
      var rightMouthY = predictions[18 * numPredictions + i];

      if (_enableDebug && validDetectionCount < 5)
      {
        Console.WriteLine($"検出 {i}: bbox=({cx:F2}, {cy:F2}, {w:F2}, {h:F2}), conf={confidence:F4}");
        Console.WriteLine($"  全特徴値 (20個):");
        for (int j = 0; j < 20; j++)
        {
          var value = predictions[j * numPredictions + i];
          Console.WriteLine($"    [{j}]: {value:F4}");
        }
        Console.WriteLine($"  ランドマーク抽出: 左目=({leftEyeX:F2}, {leftEyeY:F2}), 右目=({rightEyeX:F2}, {rightEyeY:F2})");
        Console.WriteLine($"              鼻=({noseX:F2}, {noseY:F2}), 左口=({leftMouthX:F2}, {leftMouthY:F2}), 右口=({rightMouthX:F2}, {rightMouthY:F2})");
      }

      if (confidence < _confidenceThreshold) continue;

      // スケール適用前の座標で境界チェック
      if (cx < 0 || cy < 0 || w <= 0 || h <= 0) continue;

      var scaledCx = cx * scaleX;
      var scaledCy = cy * scaleY;
      var scaledW = w * scaleX;
      var scaledH = h * scaleY;

      var boundingBox = CreateBoundingBox(scaledCx, scaledCy, scaledW, scaledH, result.ImageSize);

      // ランドマーク座標もスケール適用
      var landmarks = new FaceLandmarks(
          LeftEye: new PointF(leftEyeX * scaleX, leftEyeY * scaleY),
          RightEye: new PointF(rightEyeX * scaleX, rightEyeY * scaleY),
          Nose: new PointF(noseX * scaleX, noseY * scaleY),
          LeftMouth: new PointF(leftMouthX * scaleX, leftMouthY * scaleY),
          RightMouth: new PointF(rightMouthX * scaleX, rightMouthY * scaleY)
      );

      // ランドマークから顔の角度を計算
      var angles = FaceAngleCalculator.CalculateAngles(landmarks);

      if (_enableDebug && validDetectionCount < 5)
      {
        Console.WriteLine($"有効検出 {validDetectionCount}: スケール後座標=({scaledCx:F1}, {scaledCy:F1}, {scaledW:F1}, {scaledH:F1}), BBox={boundingBox}");
        Console.WriteLine($"  スケール後ランドマーク: 左目=({landmarks.LeftEye.X:F1}, {landmarks.LeftEye.Y:F1}), 右目=({landmarks.RightEye.X:F1}, {landmarks.RightEye.Y:F1})");
        Console.WriteLine($"  顔の角度: Roll={angles.Roll:F1}, Pitch={angles.Pitch:F1}, Yaw={angles.Yaw:F1}");
      }

      if (IsValidBoundingBox(boundingBox))
      {
        detections.Add(new FaceDetection(boundingBox, confidence, landmarks, angles));
        validDetectionCount++;
      }
    }

    if (_enableDebug)
    {
      Console.WriteLine($"有効な検出数: {validDetectionCount}");
    }

    return ApplyNMS(detections);
  }

  /// <summary>
  /// YOLOv8n-face標準出力のパース [1, num_predictions, 5]
  /// </summary>
  private List<FaceDetection> ParseYolov8nStandardOutput(InferenceResult result)
  {
    var detections = new List<FaceDetection>();
    var output = result.Outputs.First().Value;
    var predictions = output.Data;
    var shape = output.Shape;

    // YOLOv8n-face標準形式: [batch, num_predictions, 5]
    int numPredictions = shape[1];
    const int featuresPerPrediction = Constants.YoloOutput.Yolov8FaceOutputFeatures;

    var (scaleX, scaleY) = CalculateScale(result.ImageSize);

    if (_enableDebug)
    {
      Console.WriteLine($"\n=== YOLOv8n-face 標準パース情報 ===");
      Console.WriteLine($"予測数: {numPredictions}");
      Console.WriteLine($"特徴数: {featuresPerPrediction}");
      Console.WriteLine($"スケール: X={scaleX:F2}, Y={scaleY:F2}");
      Console.WriteLine($"信頼度閾値: {_confidenceThreshold}");
    }

    int validDetectionCount = 0;

    for (int i = 0; i < numPredictions; i++)
    {
      var baseIndex = i * featuresPerPrediction;

      var cx = predictions[baseIndex];
      var cy = predictions[baseIndex + 1];
      var w = predictions[baseIndex + 2];
      var h = predictions[baseIndex + 3];
      var confidence = predictions[baseIndex + 4];

      if (_enableDebug && validDetectionCount < 5)
      {
        Console.WriteLine($"検出 {i}: cx={cx:F2}, cy={cy:F2}, w={w:F2}, h={h:F2}, conf={confidence:F4}");
      }

      if (confidence < _confidenceThreshold) continue;

      if (cx < 0 || cy < 0 || w <= 0 || h <= 0) continue;

      var scaledCx = cx * scaleX;
      var scaledCy = cy * scaleY;
      var scaledW = w * scaleX;
      var scaledH = h * scaleY;

      var boundingBox = CreateBoundingBox(scaledCx, scaledCy, scaledW, scaledH, result.ImageSize);

      if (_enableDebug && validDetectionCount < 5)
      {
        Console.WriteLine($"有効検出 {validDetectionCount}: スケール後座標=({scaledCx:F1}, {scaledCy:F1}, {scaledW:F1}, {scaledH:F1}), BBox={boundingBox}");
      }

      if (IsValidBoundingBox(boundingBox))
      {
        detections.Add(new FaceDetection(boundingBox, confidence, null, null)); // 標準形式はランドマーク・角度なし
        validDetectionCount++;
      }
    }

    if (_enableDebug)
    {
      Console.WriteLine($"有効な検出数: {validDetectionCount}");
    }

    return ApplyNMS(detections);
  }

  /// <summary>
  /// YOLOv11-face出力のパース（転置形式）
  /// </summary>
  private List<FaceDetection> ParseYolov11Output(InferenceResult result)
  {
    var detections = new List<FaceDetection>();
    var output = result.Outputs.First().Value;
    var predictions = output.Data;
    var shape = output.Shape;

    // YOLOv11-face: [1, 5, num_predictions] 転置出力
    int numPredictions = shape[2];

    var (scaleX, scaleY) = CalculateScale(result.ImageSize);

    if (_enableDebug)
    {
      Console.WriteLine($"\n=== YOLOv11-face パース情報 ===");
      Console.WriteLine($"予測数: {numPredictions}");
      Console.WriteLine($"スケール: X={scaleX:F2}, Y={scaleY:F2}");
    }

    for (int i = 0; i < numPredictions; i++)
    {
      var confidence = predictions[4 * numPredictions + i];
      if (confidence < _confidenceThreshold) continue;

      var cx = predictions[0 * numPredictions + i] * scaleX;
      var cy = predictions[1 * numPredictions + i] * scaleY;
      var w = predictions[2 * numPredictions + i] * scaleX;
      var h = predictions[3 * numPredictions + i] * scaleY;

      var boundingBox = CreateBoundingBox(cx, cy, w, h, result.ImageSize);
      if (IsValidBoundingBox(boundingBox))
      {
        detections.Add(new FaceDetection(boundingBox, confidence, null, null)); // YOLOv11はランドマーク・角度なし
      }
    }

    return ApplyNMS(detections);
  }

  private static (float scaleX, float scaleY) CalculateScale(OpenCvSharp.Size imageSize)
  {
    var modelWidth = (float)Constants.ImageProcessing.YoloInputWidth;
    var modelHeight = (float)Constants.ImageProcessing.YoloInputHeight;

    return (imageSize.Width / modelWidth, imageSize.Height / modelHeight);
  }

  private static Rectangle CreateBoundingBox(float cx, float cy, float w, float h, OpenCvSharp.Size imageSize)
  {
    var x1 = (int)Math.Max(0, cx - w / 2);
    var y1 = (int)Math.Max(0, cy - h / 2);
    var x2 = (int)Math.Min(imageSize.Width, cx + w / 2);
    var y2 = (int)Math.Min(imageSize.Height, cy + h / 2);

    return Rectangle.FromLTRB(x1, y1, x2, y2);
  }

  private static bool IsValidBoundingBox(Rectangle boundingBox)
  {
    const int minSize = 10; // 最小サイズ制限を追加
    return boundingBox.Width >= minSize && boundingBox.Height >= minSize;
  }

  private static List<FaceDetection> ApplyNMS(List<FaceDetection> detections)
  {
    var filteredDetections = detections
        .Select(face => new Detection
        {
          ClassId = 0, // すべて同じクラス（顔）として扱う
          ClassName = "face",
          Confidence = face.Confidence,
          BBox = new RectangleF(face.BBox.X, face.BBox.Y, face.BBox.Width, face.BBox.Height)
        })
        .ToList();

    var nmsResults = OnnxHelper.ApplyNMS(filteredDetections, Constants.Thresholds.DefaultNmsThreshold);

    return [.. nmsResults
            .Select(nmsResult =>
            {
                var bbox = Rectangle.FromLTRB(
                    (int)nmsResult.BBox.Left,
                    (int)nmsResult.BBox.Top,
                    (int)nmsResult.BBox.Right,
                    (int)nmsResult.BBox.Bottom);

                // NMS後の結果に最も近い元の検出結果を見つけてランドマーク・角度情報を復元
                var originalDetection = detections
                    .Where(d => Math.Abs(d.Confidence - nmsResult.Confidence) < 0.001f)
                    .Where(d => Math.Abs(d.BBox.X - bbox.X) < 5 && Math.Abs(d.BBox.Y - bbox.Y) < 5)
                    .FirstOrDefault();

                return new FaceDetection(bbox, nmsResult.Confidence, originalDetection?.Landmarks, originalDetection?.Angles);
            })];
  }

  public void Dispose()
  {
    _session?.Dispose();
    GC.SuppressFinalize(this);
  }
}
