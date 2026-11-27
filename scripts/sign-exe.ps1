# VolumeFull 自签名脚本
# 功能：创建自签名代码签名证书 → 签名 exe → 导出证书 → 导入受信任根/发布者 → 验证
#
# 用法：
#   普通用户（只签名 + 导出证书）：
#     powershell -ExecutionPolicy Bypass -File sign-exe.ps1 -SkipTrust
#   管理员（签名 + 导入本机信任，推荐）：
#     powershell -ExecutionPolicy Bypass -File sign-exe.ps1
#
# 参数：
#   -ExePath  要签名的 exe（默认自动查找 发布版/Release 输出）
#   -Subject  证书主题（默认 CN=VolumeFull Local Dev）

param(
    [string]$ExePath = "",
    [string]$Subject = "CN=VolumeFull Local Dev",
    [switch]$SkipTrust
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent $scriptDir

# 自动查找 exe
if ([string]::IsNullOrEmpty($ExePath)) {
    $candidates = @(
        (Join-Path $root "发布版\VolumeFull.exe"),
        (Join-Path $root "bin\Release\net8.0-windows\win-x64\VolumeFull.exe"),
        (Join-Path $root "VolumeFull.exe"),
        "C:\Apps\VolumeCtrlWpf\VolumeFull.exe"
    )
    $ExePath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $ExePath -or -not (Test-Path $ExePath)) {
    throw "找不到 exe，请用 -ExePath 指定路径"
}
Write-Host "目标: $ExePath" -ForegroundColor Cyan

# 1) 创建/复用代码签名证书
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Subject } | Select-Object -First 1
if (-not $cert) {
    Write-Host "创建自签名证书..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(10) `
        -KeyUsage DigitalSignature `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")
    Write-Host "证书已创建: $($cert.Thumbprint)" -ForegroundColor Green
} else {
    Write-Host "使用已有证书: $($cert.Thumbprint)" -ForegroundColor Green
}

# 2) 签名
$sig = Set-AuthenticodeSignature -FilePath $ExePath -Certificate $cert -HashAlgorithm SHA256
Write-Host "签名结果: $($sig.Status)" -ForegroundColor Cyan

# 3) 导出证书（方便其他电脑导入信任）
$cerPath = Join-Path $scriptDir "VolumeFull-Sign.cer"
Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null
Write-Host "证书已导出: $cerPath" -ForegroundColor Cyan

# 4) 导入受信任根/发布者（需要管理员）
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($SkipTrust) {
    Write-Host "已跳过导入信任（-SkipTrust）" -ForegroundColor Yellow
} elseif ($isAdmin) {
    certutil -f -addstore Root $cerPath | Out-Null
    certutil -f -addstore TrustedPublisher $cerPath | Out-Null
    Write-Host "已导入受信任根/受信任发布者（本机）" -ForegroundColor Green
} else {
    Write-Host "非管理员：跳过导入信任。" -ForegroundColor Yellow
    Write-Host "  方式1：右键本脚本 → 以管理员身份运行（自动导入）" -ForegroundColor Yellow
    Write-Host "  方式2：双击 $cerPath 手动安装到「受信任的根证书颁发机构」和「受信任的发布者」" -ForegroundColor Yellow
}

# 5) 验证
$verify = Get-AuthenticodeSignature -FilePath $ExePath
Write-Host "验证: $($verify.Status)" -ForegroundColor Cyan
Write-Host "签名者: $($verify.SignerCertificate.Subject)" -ForegroundColor Cyan
