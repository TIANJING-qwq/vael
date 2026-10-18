<#
.SYNOPSIS
    Vael 一键打包发布脚本
.DESCRIPTION
    自动完成：版本号更新 → 构建 → ZIP 打包 → Inno Setup 安装包 → GitHub Release → 清理
.PARAMETER Version
    版本号（格式 x.y.z，如 0.3.1）。不传则使用 csproj 中的当前版本。
.PARAMETER SkipPublish
    跳过 GitHub 发布步骤，仅本地构建打包。
.PARAMETER Notes
    Release 说明文本。不传则自动生成。
.PARAMETER KeepArtifacts
    保留 publish/ 目录里的构建产物（默认会清理）。
.EXAMPLE
    .\publish.ps1 -Version 0.3.1
.EXAMPLE
    .\publish.ps1 -Version 0.3.1 -SkipPublish
.EXAMPLE
    .\publish.ps1 -Version 0.3.1 -KeepArtifacts
#>

[CmdletBinding()]
param(
    [string]$Version = "",
    [switch]$SkipPublish,
    [string]$Notes = "",
    [switch]$KeepArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ============================================================
# 配置区
# ============================================================
$RepoOwner    = "TIANJING-qwq"
$RepoName     = "Vael"
$CsprojPath   = "SBtools.csproj"
$InstallerIss = "installer.iss"
$PublishDir   = ".\publish\win-x64"
$ZipOutputDir = ".\publish"
$InnoSetupExe = "C:\Program Files\Inno Setup 7\ISCC.exe"
$Runtime      = "win-x64"

# ============================================================
# 工具函数
# ============================================================
function Write-Step($msg) { Write-Host "`n===== $msg =====" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "  [!!] $msg" -ForegroundColor Yellow }
function Write-Err($msg)  { Write-Host "  [XX] $msg" -ForegroundColor Red }

# ============================================================
# 0. 版本号处理
# ============================================================
Write-Step "0. 版本号处理"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $content = Get-Content $CsprojPath -Raw
    if ($content -match '<Version>([\d\.]+)</Version>') {
        $Version = $Matches[1]
        Write-Ok "使用 csproj 当前版本: $Version"
    } else {
        Write-Err "无法从 $CsprojPath 读取版本号"
        exit 1
    }
} else {
    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        Write-Err "版本号格式错误，应为 x.y.z（如 0.3.1）"
        exit 1
    }
    Write-Ok "目标版本: $Version"

    $content = Get-Content $CsprojPath -Raw
    $content = $content -replace '<Version>[\d\.]+</Version>', "<Version>$Version</Version>"
    $content = $content -replace '<AssemblyVersion>[\d\.]+</AssemblyVersion>', "<AssemblyVersion>$Version</AssemblyVersion>"
    $content = $content -replace '<FileVersion>[\d\.]+</FileVersion>', "<FileVersion>$Version</FileVersion>"
    $content = $content -replace '<InformationalVersion>.*?</InformationalVersion>', "<InformationalVersion>Dev$Version</InformationalVersion>"
    $content | Set-Content $CsprojPath -NoNewline -Encoding UTF8
    Write-Ok "csproj 版本号已更新"

    if (Test-Path $InstallerIss) {
        $issContent = Get-Content $InstallerIss -Raw
        $issContent = $issContent -replace 'AppVersion=.*', "AppVersion=$Version"
        $issContent = $issContent -replace 'OutputBaseFilename=.*', "OutputBaseFilename=Vael_Setup_v$Version"
        $issContent | Set-Content $InstallerIss -NoNewline -Encoding UTF8
        Write-Ok "installer.iss 版本号已更新"
    }
}

$Tag     = "v$Version"
$ZipName = "Vael_win-x64_v$Version.zip"
$ZipPath = Join-Path $ZipOutputDir $ZipName
$InstallerPath = ".\publish\installer\Vael_Setup_v$Version.exe"

Write-Host "`n  发布版本: $Version" -ForegroundColor White
Write-Host "  Git Tag : $Tag" -ForegroundColor White

# ============================================================
# 1. 清理旧构建
# ============================================================
Write-Step "1. 清理旧构建"
dotnet clean -c Release --nologo
if (Test-Path ".\publish") { Remove-Item ".\publish" -Recurse -Force }
Write-Ok "清理完成"

# ============================================================
# 2. 发布
# ============================================================
Write-Step "2. 发布 ($Runtime)"
dotnet publish -c Release -r $Runtime --self-contained false -o $PublishDir --nologo
if ($LASTEXITCODE -ne 0) { Write-Err "dotnet publish 失败"; exit 1 }
Write-Ok "发布完成"

# ============================================================
# 3. 验证 VLC 依赖
# ============================================================
Write-Step "3. 验证 VLC 依赖"
$required = @(
    "$PublishDir\libvlc.dll",
    "$PublishDir\libvlccore.dll",
    "$PublishDir\plugins"
)

$missing = @()
foreach ($item in $required) {
    if (-not (Test-Path $item)) {
        $missing += $item
        Write-Err "缺失: $item"
    } else {
        Write-Ok "存在: $item"
    }
}

if ($missing.Count -gt 0) {
    Write-Warn "尝试从 NuGet 缓存复制 VLC..."
    $nugetVlc = "$env:USERPROFILE\.nuget\packages\videolan.libvlc.windows\3.0.21\build\x64"
    if (Test-Path $nugetVlc) {
        Copy-Item "$nugetVlc\*" -Destination $PublishDir -Recurse -Force
        Write-Ok "已从 $nugetVlc 复制"
    } else {
        Write-Err "找不到 NuGet 缓存，请执行: dotnet nuget locals all --clear ; dotnet restore"
        exit 1
    }
}

# ============================================================
# 4. 生成便携版 ZIP
# ============================================================
Write-Step "4. 生成便携版 ZIP"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -Force
$zipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
Write-Ok "便携版: $ZipName ($zipSize MB)"

# ============================================================
# 5. Inno Setup 安装包
# ============================================================
Write-Step "5. Inno Setup 安装包"

if (Test-Path $InnoSetupExe) {
    Write-Host "  正在编译 installer.iss ..." -ForegroundColor Gray
    & $InnoSetupExe $InstallerIss
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Inno Setup 编译失败（退出码 $LASTEXITCODE）"
    } elseif (Test-Path $InstallerPath) {
        $instSize = [math]::Round((Get-Item $InstallerPath).Length / 1MB, 1)
        Write-Ok "安装包: Vael_Setup_v$Version.exe ($instSize MB)"
    } else {
        Write-Warn "Inno Setup 已执行，但未找到输出文件：$InstallerPath"
    }
} else {
    Write-Warn "未找到 Inno Setup 编译器: $InnoSetupExe"
    Write-Warn "跳过安装包生成，仅发布便携版 ZIP"
}

# ============================================================
# 6. 发布到 GitHub Releases
# ============================================================
if ($SkipPublish) {
    Write-Step "6. 跳过 GitHub 发布 (-SkipPublish)"
    Write-Host "`n本地构建产物：" -ForegroundColor Yellow
    Write-Host "  $ZipPath" -ForegroundColor White
    if (Test-Path $InstallerPath) {
        Write-Host "  $InstallerPath" -ForegroundColor White
    }
} else {
    Write-Step "6. 发布到 GitHub Releases"

    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        Write-Err "未找到 GitHub CLI (gh)，请先安装: https://cli.github.com/"
        exit 1
    }

    if ($env:GITHUB_TOKEN) {
        Write-Warn "检测到 GITHUB_TOKEN 环境变量，本次忽略它"
        Remove-Item Env:GITHUB_TOKEN -ErrorAction SilentlyContinue
    }

    gh auth status 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Err "未登录 GitHub CLI，请执行: gh auth login"
        exit 1
    }
    Write-Ok "GitHub CLI 已登录"

    if ([string]::IsNullOrWhiteSpace($Notes)) {
        $Notes = "## Vael v$Version`n`n### 下载`n- 安装包: Vael_Setup_v$Version.exe`n- 便携版: Vael_win-x64_v$Version.zip"
    }

    $assets = @()
    if (Test-Path $InstallerPath) { $assets += $InstallerPath }
    if (Test-Path $ZipPath)       { $assets += $ZipPath }

    if ($assets.Count -eq 0) {
        Write-Err "没有找到可上传的构建产物"
        exit 1
    }

    Write-Host "`n  即将上传以下资产：" -ForegroundColor White
    foreach ($a in $assets) {
        $size = [math]::Round((Get-Item $a).Length / 1MB, 1)
        Write-Host ("    - {0} ({1} MB)" -f (Split-Path $a -Leaf), $size) -ForegroundColor Gray
    }

    $tagExists = $false
    try {
        gh release view $Tag --repo "${RepoOwner}/${RepoName}" 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { $tagExists = $true }
    } catch { }

    if ($tagExists) {
        Write-Warn "Release $Tag 已存在，将覆盖上传同名资产"
        foreach ($a in $assets) {
            gh release upload $Tag $a --repo "${RepoOwner}/${RepoName}" --clobber
            Write-Ok "已上传: $(Split-Path $a -Leaf)"
        }
    } else {
        Write-Host "`n  正在创建 Release $Tag ..." -ForegroundColor Gray
        $notesFile = [System.IO.Path]::GetTempFileName()
        $Notes | Set-Content $notesFile -Encoding UTF8

        gh release create $Tag `
            --repo "${RepoOwner}/${RepoName}" `
            --title "Vael $Tag" `
            --notes-file $notesFile `
            --latest `
            $assets

        Remove-Item $notesFile -Force -ErrorAction SilentlyContinue

        if ($LASTEXITCODE -eq 0) {
            Write-Ok "Release $Tag 创建成功"
        } else {
            Write-Err "Release 创建失败"
            exit 1
        }
    }
}

# ============================================================
# 7. 构建后清理
# ============================================================
Write-Step "7. 构建后清理"

if ($KeepArtifacts) {
    Write-Ok "指定了 -KeepArtifacts，保留 publish/ 目录"
} else {
    # 删除 publish/（含 win-x64 中间产物、ZIP、installer）
    if (Test-Path ".\publish") {
        Remove-Item ".\publish" -Recurse -Force -ErrorAction SilentlyContinue
        Write-Ok "已删除 .\publish\"
    }

    # 删除 obj/ 和 bin/（dotnet 中间产物）
    foreach ($dir in @(".\obj", ".\bin")) {
        if (Test-Path $dir) {
            Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
            Write-Ok "已删除 $dir"
        }
    }

    # 可选：清理 NuGet 缓存（默认关闭，避免影响下次构建速度）
    # dotnet nuget locals http-cache --clear | Out-Null
    # Write-Ok "已清理 NuGet http-cache"
}

# ============================================================
# 完成
# ============================================================
Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "  发布完成！" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
if (-not $SkipPublish) {
    Write-Host ""
    Write-Host "  Release 页面: https://github.com/${RepoOwner}/${RepoName}/releases/tag/$Tag" -ForegroundColor White
}
Write-Host ""