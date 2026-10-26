# Third-party components

Original Viddy EXP Only code is MIT licensed, with attribution retained for the reused MapleLens EXP parser and reader. Each dependency below keeps its upstream license. Full license texts and third-party notices are in `licenses/`.

| Component | Version / source | License |
| --- | --- | --- |
| Tesseract .NET wrapper | [Charles Weld, 5.2.0](https://github.com/charlesw/tesseract) | Apache-2.0 |
| Tesseract native runtime | [5.2](https://github.com/tesseract-ocr/tesseract) | Apache-2.0 |
| Leptonica | [1.82](https://github.com/DanBloomberg/leptonica) | BSD-style |
| InteropDotNet, included in wrapper | [Andrey Akinshin](https://github.com/AndreyAkinshin/InteropDotNet) | MIT |
| English fast LSTM OCR model | [tessdata_fast](https://github.com/tesseract-ocr/tessdata_fast) | Apache-2.0 |
| ONNX Runtime, native and managed | [Microsoft, 1.18.1](https://github.com/microsoft/onnxruntime/tree/v1.18.1) | MIT, additional notices included |
| PP-OCRv4 recognition model | [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR), `ch_PP-OCRv4_rec_infer.onnx` from [rapidocr-onnxruntime 1.4.4](https://pypi.org/project/rapidocr-onnxruntime/1.4.4/) | Apache-2.0 |
| System.Memory | Microsoft NuGet 4.5.5 | MIT |
| System.Buffers | Microsoft NuGet 4.5.1 | MIT |
| System.Numerics.Vectors | Microsoft NuGet 4.5.0 | MIT |
| System.Runtime.CompilerServices.Unsafe | Microsoft NuGet 4.5.3 | MIT |

`app/models/digits.onnx` is the unchanged recognition model, renamed. Its BGR normalization and CTC decoding are implemented locally in C#. ONNX Runtime telemetry is explicitly disabled. The model is used only to cross-check EXP and level text when needed. No training or gameplay model is bundled.

EXP requirements are generated from numeric formulas published in [NiaMeowDB's Classic EXP table](https://meowdb.com/msclassic/guides/exp-table-level-1-to-100). They were checked October 26, 2026. No article text or site/game artwork is included.

`DEPENDENCY-SHA256.txt` records the hashes of the exact bundled OCR libraries and models for review. The repository bundles those dependencies so builds do not require package downloads.
