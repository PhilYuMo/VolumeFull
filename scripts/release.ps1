<#
.SYNOPSIS
  VolumeFull 发布脚本：版本号、发布、签名、打包、安装器、留档一步完成。

.DESCRIPTION
  1. 把 csproj 的 Version / FileVersion / AssemblyVersion / InformationalVersion 更新为 -Version
  2. dotnet publish 便携版到 发布\便携版
  3. 自签名 VolumeFull.exe（-SkipSign 可跳过）
  4. 打包 发布\VolumeFull-v<版本>-portable.zip
  5. 编译安装器为 发布\VolumeFull-v<版本>-Setup.exe（-NoSetup 可跳过）
  6. 复制两份产物到 发布\历史版本\，并生成 发布\SHA256SUMS.txt
  7. -Deploy 部署到 C:\Apps\VolumeCtrlWpf；-Tag 创建本地 tag

  推送 tag 和创建 GitHub Release 不在脚本内（需要 PAT），脚本结束时会打印待执行命令。

.EXAMPLE
  ./scripts/release.ps1 -Version 1.2.2
  ./scripts/release.ps1 -Version 1.2.2 -Deploy -Tag
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [switch]$SkipBuild,
    [switch]$SkipSign,
    [switch]$NoSetup,
    [switch]$Deploy,
    [switch]$Tag
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'VolumeCtrlWpf.csproj'
$publishDir = Join-Path $root '发布'
$portableDir = Join-Path $publishDir '便携版'
$archiveDir = Join-Path $publishDir '历史版本'
$signScript = Join-Path $PSScriptRoot 'sign-exe.ps1'
$setupScript = Join-Path $root '安装器\build-setup.ps1'
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { throw '找不到 dotnet，请把 .NET SDK 加入 PATH（global.json 固定 9.0.321）' }

Write-Host "发布 VolumeFull v$Version" -ForegroundColor Cyan

# 1) 更新 csproj 版本号（唯一来源）
$text = [System.IO.File]::ReadAllText($proj, [System.Text.Encoding]::UTF8)
$text = [regex]::Replace($text, '<Version>[^<]*</Version>', "<Version>$Version</Version>")
$text = [regex]::Replace($text, '<FileVersion>[^<]*</FileVersion>', "<FileVersion>$Version.0</FileVersion>")
$text = [regex]::Replace($text, '<AssemblyVersion>[^<]*</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>")
$text = [regex]::Replace($text, '<InformationalVersion>[^<]*</InformationalVersion>', "<InformationalVersion>$Version</InformationalVersion>")
[System.IO.File]::WriteAllText($proj, $text, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "版本号已更新为 $Version" -ForegroundColor Green

# 2) 发布便携版
if (-not $SkipBuild) {
    if (Test-Path $portableDir) { Remove-Item $portableDir -Recurse -Force }
    & $dotnet publish $proj -c Release -r win-x64 --self-contained true -o $portableDir --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish 失败' }
    Write-Host "便携版已发布: $portableDir" -ForegroundColor Green
}
$exe = Join-Path $portableDir 'VolumeFull.exe'
if (-not (Test-Path $exe)) { throw "缺少 $exe" }

# 3) 签名便携版
if (-not $SkipSign) {
    & $signScript -ExePath $exe -SkipTrust
    Write-Host '便携版已签名' -ForegroundColor Green
}

# 4) 便携版 zip
New-Item -ItemType Directory -Force $publishDir | Out-Null
$zip = Join-Path $publishDir "VolumeFull-v$Version-portable.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $portableDir '*') -DestinationPath $zip -Force
Write-Host ("便携版 zip: {0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB)) -ForegroundColor Green

# 5) 安装器
$setupOut = $null
if (-not $NoSetup) {
    & $setupScript -Version $Version
    $setupOut = Join-Path $publishDir "VolumeFull-v$Version-Setup.exe"
    Copy-Item (Join-Path $root '安装器\VolumeFull-Setup.exe') $setupOut -Force
    if (-not $SkipSign) {
        & $signScript -ExePath $setupOut -SkipTrust
    }
    Write-Host ("安装器: {0} ({1:N1} MB)" -f $setupOut, ((Get-Item $setupOut).Length / 1MB)) -ForegroundColor Green
}

# 6) 留档 + 校验和（历史版本永久保留：同名文件存在时拒绝覆盖）
New-Item -ItemType Directory -Force $archiveDir | Out-Null
$archiveFiles = @($zip)
if ($setupOut) { $archiveFiles += $setupOut }
foreach ($f in $archiveFiles) {
    $dest = Join-Path $archiveDir (Split-Path $f -Leaf)
    if (Test-Path $dest) { throw "历史版本已存在，拒绝覆盖: $dest（确认要重发请先手动移走该文件）" }
    Copy-Item $f $dest
}
$sumFiles = @($zip)
if ($setupOut) { $sumFiles += $setupOut }
$sums = $sumFiles | ForEach-Object {
    $hash = (Get-FileHash $_ -Algorithm SHA256).Hash.ToLowerInvariant()
    "{0}  {1}" -f $hash, (Split-Path $_ -Leaf)
}
$sums | Set-Content (Join-Path $publishDir 'SHA256SUMS.txt') -Encoding ascii
Write-Host 'SHA256:' -ForegroundColor Cyan
$sums | ForEach-Object { Write-Host "  $_" }

# 7) 部署
if ($Deploy) {
    $running = Get-Process VolumeFull -ErrorAction SilentlyContinue
    if ($running) { throw 'VolumeFull 正在运行，请先退出再部署' }
    $target = 'C:\Apps\VolumeCtrlWpf'
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item (Join-Path $portableDir '*') $target -Recurse -Force
    Write-Host "已部署到 $target" -ForegroundColor Green
}

# 8) 本地 tag
if ($Tag) {
    $existing = git -C $root tag --list "v$Version"
    if ($existing) { throw "tag v$Version 已存在" }
    git -C $root tag "v$Version"
    if ($LASTEXITCODE -ne 0) { throw '创建 tag 失败' }
    Write-Host "已创建 tag v$Version" -ForegroundColor Green
}

Write-Host ''
Write-Host '完成。待手动执行（需要 PAT）：' -ForegroundColor Yellow
if ($Tag) { Write-Host "  git -C `"$root`" push origin v$Version" }
Write-Host "  在 GitHub 上创建 Release v$Version，上传 $(Split-Path $zip -Leaf) 与 $(if ($setupOut) { Split-Path $setupOut -Leaf } else { '(安装器已跳过)' })"
