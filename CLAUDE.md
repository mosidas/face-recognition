# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a C# .NET 8 real-time face recognition system using ONNX models. The project supports both object detection and face recognition with YOLOv8n-face/YOLOv11-face models.

## Key Commands

### Build and Run
```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/ProjectName/ProjectName.csproj

# Run ExampleCLI
dotnet run --project src/ExampleCLI/ExampleCLI.csproj

# Run RealTimeDetector with options
dotnet run --project src/RealTimeDetector/RealTimeDetector.csproj -- --model path/to/model.onnx --mode faces

# Run RealTimeFaceRecognizer
dotnet run --project src/RealTimeFaceRecognizer/RealTimeFaceRecognizer.csproj
```

### Project Structure
```
src/
├── Recognizer/           # Core library for detection and recognition
├── ExampleCLI/          # CLI sample application
├── RealTimeDetector/    # Real-time object/face detection
└── RealTimeFaceRecognizer/ # Real-time face recognition
```

## Architecture

### Core Components

**Recognizer Library** (`src/Recognizer/`):
- `FaceRecognizer` - Main face recognition class with verification capabilities
- `YoloFaceDetector` - Face detection using YOLOv8n-face/YOLOv11-face models
- `YoloDetector` - General object detection using YOLO models
- `OnnxHelper` - ONNX model inference utilities with GPU acceleration support
- `FaceAngleCalculator` - Calculate face angles (roll, pitch, yaw) from landmarks
- `Constants` - Configuration constants for thresholds and image processing

**Model Support**:
- Automatic detection between YOLOv8n-face and YOLOv11-face formats
- YOLOv8n-face: Standard [1, 25200, 5] and transposed [1, 20, 8400] outputs
- YOLOv11-face: Transposed [1, 5, 8400] outputs
- Face landmarks extraction (5 points: eyes, nose, mouth corners)

**Applications**:
- `ExampleCLI`: Command-line interface for testing detection and recognition
- `RealTimeDetector`: Live camera processing with object/face detection modes
- `RealTimeFaceRecognizer`: Real-time face recognition with database matching

### Key Features

1. **Flexible Model Support**: Auto-detects YOLOv8n-face vs YOLOv11-face model types
2. **Real-time Processing**: Optimized for live camera feeds with GPU acceleration
3. **Face Verification**: Compare two faces with cosine similarity
4. **Face Identification**: Search faces in a database
5. **Landmark Detection**: Extract 5-point facial landmarks
6. **Angle Calculation**: Compute face orientation (roll, pitch, yaw)
7. **NMS Processing**: Non-Maximum Suppression for duplicate removal

### Dependencies

- Microsoft.ML.OnnxRuntime - ONNX model inference
- OpenCvSharp4 - Computer vision operations
- System.CommandLine - CLI argument parsing
- System.Numerics.Tensors - Tensor operations
- SixLabors.ImageSharp - Image processing

### Configuration

Key thresholds in `Constants.cs`:
- Face detection: 0.7 confidence threshold
- Face recognition: 0.6 similarity threshold
- Object detection: 0.5 confidence threshold
- NMS: 0.5 IoU threshold

### Common Development Patterns

1. **Model Loading**: Use `OnnxHelper.LoadModel()` for consistent session creation
2. **Image Processing**: All images converted to RGB format for ONNX compatibility
3. **Async Operations**: All inference operations use async/await patterns
4. **Resource Management**: Proper disposal of ONNX sessions and OpenCV Mat objects
5. **Error Handling**: Graceful fallback from GPU to CPU inference

### Testing

The project includes sample applications for testing:
- Use `ExampleCLI` for quick testing of detection/recognition functions
- Use `RealTimeDetector` for live camera testing
- Debug output available via `enableDebug` parameter in face detector

### Performance Optimization

- GPU acceleration automatically enabled when CUDA is available
- Image downscaling option available for faster processing
- Efficient tensor operations using System.Numerics.Tensors
- NMS processing for duplicate removal