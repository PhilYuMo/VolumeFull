# VolumeFull 安装器编译脚本
# 用法：powershell -ExecutionPolicy Bypass -File build-setup.ps1
# 前置：先发布便携版到 ..\发布\便携版
# 注意：csc 4.0.30319 处理中文路径会乱码，所以在英文临时目录里编译；
#       版本号只写入临时副本，不改动 Setup.cs 源码

param(
    [string]$Version = "1.2.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Split-Path -Parent $root
$portable = Join-Path $proj "发布\便携版"
$appZip = Join-Path $root "app.zip"
$setup = Join-Path $root "VolumeFull-Setup.exe"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $portable)) { throw "缺少便携版目录: $portable" }
if (-not (Test-Path $csc)) { throw "找不到 csc.exe: $csc" }

# 1) 打包内嵌载荷
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $appZip) { Remove-Item $appZip -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($portable, $appZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Write-Host ("app.zip: {0:N2} MB" -f ((Get-Item $appZip).Length / 1MB))

# 2) 在英文临时目录编译（避开 csc 的中文路径乱码），版本号只替换临时副本
$work = Join-Path $env:TEMP "volumefull-setup-build"
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Path $work | Out-Null

$setupSrc = Join-Path $root "Setup.cs"
$text = [System.IO.File]::ReadAllText($setupSrc, [System.Text.Encoding]::UTF8)
$text = [System.Text.RegularExpressions.Regex]::Replace($text, 'AppVersion = "[^"]*"', ('AppVersion = "' + $Version + '"'))
[System.IO.File]::WriteAllText((Join-Path $work "Setup.cs"), $text, (New-Object System.Text.UTF8Encoding($true)))

Copy-Item (Join-Path $root "app.manifest") (Join-Path $work "app.manifest")
Copy-Item $appZip (Join-Path $work "app.zip")

& $csc /nologo /target:winexe /out:"$work\VolumeFull-Setup.exe" `
    /win32manifest:"$work\app.manifest" `
    /resource:"$work\app.zip",app.zip `
    /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll `
    "$work\Setup.cs"

$built = Join-Path $work "VolumeFull-Setup.exe"
if (-not (Test-Path $built)) { throw "编译失败" }
Copy-Item $built $setup -Force
Remove-Item $work -Recurse -Force
Write-Host ("VolumeFull-Setup.exe: {0:N2} MB" -f ((Get-Item $setup).Length / 1MB))
