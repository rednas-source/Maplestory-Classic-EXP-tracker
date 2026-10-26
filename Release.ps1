param([string]$OutputPath = (Join-Path $PSScriptRoot 'artifacts\Viddy-EXP-Only-Windows-x64.zip'))
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Build.ps1')
$stage = Join-Path $PSScriptRoot ('artifacts\release-' + [Guid]::NewGuid().ToString('N'))
$appFolder = Join-Path $stage 'app'
New-Item -ItemType Directory -Path $appFolder -Force | Out-Null
$bin = Join-Path $PSScriptRoot 'bin'
$names = @('ViddyExpOnly.exe','ViddyExpOnly.exe.config','Tesseract.dll','Microsoft.ML.OnnxRuntime.dll','onnxruntime.dll','onnxruntime_providers_shared.dll','System.Memory.dll','System.Buffers.dll','System.Numerics.Vectors.dll','System.Runtime.CompilerServices.Unsafe.dll')
foreach ($name in $names) { Copy-Item -LiteralPath (Join-Path $bin $name) -Destination $appFolder }
foreach ($folder in @('x64','tessdata','models')) { Copy-Item -LiteralPath (Join-Path $bin $folder) -Destination $appFolder -Recurse }
foreach ($folder in @('licenses','docs')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $folder) -Destination $stage -Recurse }
foreach ($name in @('Start Viddy EXP.cmd','README.md','LICENSE','THIRD-PARTY-NOTICES.md','DEPENDENCY-SHA256.txt','exp-table.csv')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $stage }
$parent = Split-Path -Parent $OutputPath
if ($parent) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
Compress-Archive -LiteralPath @(Get-ChildItem -LiteralPath $stage | ForEach-Object FullName) -DestinationPath $OutputPath -Force
$hash = Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256
($hash.Hash + '  ' + [System.IO.Path]::GetFileName($OutputPath)) | Set-Content -LiteralPath (Join-Path $parent 'SHA256SUMS.txt') -Encoding ascii
Write-Output ('Release ZIP: ' + $OutputPath)
$hash
