param([switch]$Test)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    $facade = Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\netstandard\v4.0_2.0.0.0__cc7b13ffcd2ddd51\netstandard.dll'
    if (!(Test-Path -LiteralPath $compiler) -or !(Test-Path -LiteralPath $facade)) { throw 'Install .NET Framework 4.8 to build this app.' }
    $bin = Join-Path $PSScriptRoot 'bin'
    New-Item -ItemType Directory -Path $bin -Force | Out-Null
    Get-ChildItem -LiteralPath 'app' -File -Filter '*.dll' | Copy-Item -Destination $bin -Force
    foreach ($folder in @('x64','tessdata','models')) { Copy-Item -LiteralPath (Join-Path 'app' $folder) -Destination $bin -Recurse -Force }
    foreach ($folder in @('licenses','docs')) { Copy-Item -LiteralPath $folder -Destination $bin -Recurse -Force }
    Copy-Item -LiteralPath 'app\ViddyExpOnly.exe.config' -Destination $bin -Force
    foreach ($file in @('README.md','LICENSE','THIRD-PARTY-NOTICES.md','exp-table.csv','DEPENDENCY-SHA256.txt')) { Copy-Item -LiteralPath $file -Destination $bin -Force }
    $sources = @(Get-ChildItem -LiteralPath 'src' -Filter '*.cs' | ForEach-Object FullName)
    $references = @('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll',"/r:$facade")
    foreach ($name in @('Tesseract','Microsoft.ML.OnnxRuntime','System.Memory','System.Buffers','System.Runtime.CompilerServices.Unsafe','System.Numerics.Vectors')) { $references += "/r:app\$name.dll" }
    & $compiler /nologo /codepage:65001 /optimize+ /platform:x64 /target:winexe /win32manifest:app.manifest /out:bin\ViddyExpOnly.exe $references $sources
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
    if ($Test) {
        $tests = @(Get-ChildItem -LiteralPath 'tests' -Filter '*.cs' | ForEach-Object FullName)
        & $compiler /nologo /codepage:65001 /optimize+ /platform:x64 /target:exe /main:ViddyExp.Tests /out:bin\TrackerTests.exe $references $sources $tests
        if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
        & .\bin\TrackerTests.exe
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    }
    Write-Output 'Built bin\ViddyExpOnly.exe. The prebuilt copy in app\ is unchanged.'
} finally { Pop-Location }
