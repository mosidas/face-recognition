#!/usr/bin/env python3
"""
ONNX Model Analyzer Tool
ONNXモデルのメタデータと仕様を分析するツール
"""

import onnx
import onnxruntime as ort
import numpy as np
import os
import sys
import argparse

def analyze_onnx_model(model_path):
    """ONNXモデルの詳細情報を分析"""
    print(f"=== Analyzing ONNX model: {model_path} ===")
    
    if not os.path.exists(model_path):
        print(f"ERROR: Model file not found: {model_path}")
        return False
    
    try:
        # ONNXモデルを読み込み
        model = onnx.load(model_path)
        
        # モデル情報を表示
        print(f"Producer: {model.producer_name}")
        print(f"Version: {model.producer_version}")
        print(f"Model Version: {model.model_version}")
        print(f"Doc String: {model.doc_string}")
        print()
        
        # 入力情報を表示
        print("=== INPUT INFORMATION ===")
        for i, input_tensor in enumerate(model.graph.input):
            print(f"Input {i}:")
            print(f"  Name: {input_tensor.name}")
            print(f"  Type: {input_tensor.type}")
            
            # 形状情報を取得
            if input_tensor.type.tensor_type.shape.dim:
                shape = []
                for dim in input_tensor.type.tensor_type.shape.dim:
                    if dim.dim_value:
                        shape.append(dim.dim_value)
                    elif dim.dim_param:
                        shape.append(dim.dim_param)
                    else:
                        shape.append("unknown")
                print(f"  Shape: {shape}")
            print()
        
        # 出力情報を表示
        print("=== OUTPUT INFORMATION ===")
        for i, output_tensor in enumerate(model.graph.output):
            print(f"Output {i}:")
            print(f"  Name: {output_tensor.name}")
            print(f"  Type: {output_tensor.type}")
            
            # 形状情報を取得
            if output_tensor.type.tensor_type.shape.dim:
                shape = []
                for dim in output_tensor.type.tensor_type.shape.dim:
                    if dim.dim_value:
                        shape.append(dim.dim_value)
                    elif dim.dim_param:
                        shape.append(dim.dim_param)
                    else:
                        shape.append("unknown")
                print(f"  Shape: {shape}")
            print()
        
        # ONNX Runtimeでセッション作成
        print("=== ONNX RUNTIME SESSION INFO ===")
        session = ort.InferenceSession(model_path)
        
        # 入力情報
        print("Runtime Input Info:")
        for input_meta in session.get_inputs():
            print(f"  Name: {input_meta.name}")
            print(f"  Shape: {input_meta.shape}")
            print(f"  Type: {input_meta.type}")
            print()
        
        # 出力情報
        print("Runtime Output Info:")
        for output_meta in session.get_outputs():
            print(f"  Name: {output_meta.name}")
            print(f"  Shape: {output_meta.shape}")
            print(f"  Type: {output_meta.type}")
            print()
        
        # テスト推論の実行
        print("=== TEST INFERENCE ===")
        input_name = session.get_inputs()[0].name
        input_shape = session.get_inputs()[0].shape
        
        # 動的次元を1に置き換え
        test_shape = []
        for dim in input_shape:
            if isinstance(dim, str) or dim == -1:
                test_shape.append(1)
            else:
                test_shape.append(dim)
        
        print(f"Test input shape: {test_shape}")
        
        # ダミーデータで推論実行
        dummy_input = np.random.rand(*test_shape).astype(np.float32)
        result = session.run(None, {input_name: dummy_input})
        
        print(f"Output shape: {result[0].shape}")
        print(f"Output dtype: {result[0].dtype}")
        print(f"Output range: [{result[0].min():.6f}, {result[0].max():.6f}]")
        print(f"Output mean: {result[0].mean():.6f}")
        print(f"Output std: {result[0].std():.6f}")
        
        # 形状からテンソル形式を推定
        if len(test_shape) == 4:
            if test_shape[1] == 3:
                print("Tensor format: NCHW (batch, channels, height, width)")
            elif test_shape[3] == 3:
                print("Tensor format: NHWC (batch, height, width, channels)")
            else:
                print("Tensor format: Unknown")
        
        # 正規化の推定
        print("\n=== NORMALIZATION ESTIMATION ===")
        if len(test_shape) == 4:
            # 異なる正規化で推論テスト
            normalized_inputs = {
                "0-1 normalization": np.random.rand(*test_shape).astype(np.float32),
                "[-1,1] normalization": (np.random.rand(*test_shape).astype(np.float32) - 0.5) * 2,
                "ImageNet normalization": (np.random.rand(*test_shape).astype(np.float32) - 0.485) / 0.229
            }
            
            for norm_name, norm_input in normalized_inputs.items():
                try:
                    norm_result = session.run(None, {input_name: norm_input})
                    print(f"{norm_name}: Output range [{norm_result[0].min():.6f}, {norm_result[0].max():.6f}], std: {norm_result[0].std():.6f}")
                except Exception as e:
                    print(f"{norm_name}: Failed - {e}")
        
        return True
        
    except Exception as e:
        print(f"ERROR: {e}")
        return False

def main():
    parser = argparse.ArgumentParser(description='ONNX Model Analyzer')
    parser.add_argument('model_paths', nargs='+', help='Path(s) to ONNX model file(s)')
    parser.add_argument('--compare', '-c', action='store_true', help='Compare multiple models')
    
    args = parser.parse_args()
    
    if len(args.model_paths) == 1:
        # 単一モデルの分析
        analyze_onnx_model(args.model_paths[0])
    else:
        # 複数モデルの比較分析
        for i, model_path in enumerate(args.model_paths):
            print(f"🔍 Analyzing model {i+1}/{len(args.model_paths)}: {os.path.basename(model_path)}")
            if analyze_onnx_model(model_path):
                print("✅ Analysis completed successfully\n")
            else:
                print("❌ Analysis failed\n")
            
            if i < len(args.model_paths) - 1:
                print("=" * 80)
                print()

if __name__ == "__main__":
    main()