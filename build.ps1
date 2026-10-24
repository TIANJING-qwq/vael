$ErrorActionPreference = "Stop"

# 版本号从 csproj 读取
$csproj = Get-Content ".\SBtools.csproj" -Raw
$m = [regex]::Match($csproj, '<Version>([^<]+)</Version>')
$version = if ($m.Success) { $m.Groups[1].Value } else { "0.4.9" }

$rids = @("win-x64", "linux-x64", "osx-arm64", "osx-x64")

Write-Host "===== 1. 清理 =====" -ForegroundColor Cyan
dotnet clean -c Release

Write-Host "`n===== 2. 四平台发布 =====" -ForegroundColor Cyan
foreach ($rid in $rids) {
    Write-Host "`n--- $rid ---" -ForegroundColor Yellow
    dotnet publish -c Release -r $rid --self-contained false -o ".\publish\$rid"
    if ($LASTEXITCODE -ne 0) { Write-Host "X $rid 发布失败" -ForegroundColor Red; exit 1 }
    Write-Host "OK $rid" -ForegroundColor Green
}

Write-Host "`n===== 3. 验证 Windows VLC 依赖 =====" -ForegroundColor Cyan
$required = @(
    ".\publish\win-x64\libvlc.dll",
    ".\publish\win-x64\libvlccore.dll",
    ".\publish\win-x64\plugins"
)
$missing = @()
foreach ($item in $required) {
    if (-not (Test-Path $item)) { $missing += $item; Write-Host "X 缺失: $item" -ForegroundColor Red }
    else { Write-Host "OK 存在: $item" -ForegroundColor Green }
}
if ($missing.Count -gt 0) {
    $nugetVlc = "$env:USERPROFILE\.nuget\packages\videolan.libvlc.windows\3.0.21\build\x64"
    if (Test-Path $nugetVlc) {
        Copy-Item "$nugetVlc\*" -Destination ".\publish\win-x64\" -Recurse -Force
        Write-Host "OK 已从 NuGet 缓存复制 VLC" -ForegroundColor Green
    } else {
        Write-Host "X 找不到 NuGet 缓存，请执行 dotnet restore" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n===== 4. 生成压缩包（排除 pdb/xml，减小上传体积）=====" -ForegroundColor Cyan
foreach ($rid in $rids) {
    $zipName = "Vael_${rid}_v$version.zip"
    $zipPath = Join-Path (Get-Location) "publish\$zipName"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

    $srcDir = Resolve-Path ".\publish\$rid"
    $staging = ".\publish\.staging\$rid"
    if (Test-Path ".\publish\.staging") { Remove-Item ".\publish\.staging" -Recurse -Force }
    New-Item -ItemType Directory -Path $staging -Force | Out-Null

    $baseLen = $srcDir.Path.Length
    Get-ChildItem $srcDir -Recurse -File |
        Where-Object { $_.Extension -notin @('.pdb', '.xml') } |
        ForEach-Object {
            $rel = $_.FullName.Substring($baseLen).TrimStart('\', '/')
            $dest = Join-Path $staging $rel
            $parent = Split-Path $dest
            if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
            Copy-Item $_.FullName $dest -Force
        }

    Compress-Archive -Path "$staging\*" -DestinationPath $zipPath -Force
    $mb = (Get-Item $zipPath).Length / 1MB
    Write-Host ("OK publish\{0}  ({1:N1} MB)" -f $zipName, $mb) -ForegroundColor Green
}
Remove-Item ".\publish\.staging" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "`n===== 5. Windows 安装包（Inno Setup）=====" -ForegroundColor Cyan
$iscc = @(
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 7\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    & $iscc .\installer.iss
    Write-Host "`nOK 安装包：publish\installer\Vael_Setup_v$version.exe" -ForegroundColor Green
} else {
    Write-Host "X 未找到 Inno Setup 编译器 ISCC.exe（不是 ISIDE.exe）" -ForegroundColor Yellow
}

Write-Host "`n===== 6. 上传到 GitHub Release =====" -ForegroundColor Cyan
Write-Host "★ 上传慢且代理无效的根因：代理规则没覆盖上传域名。" -ForegroundColor Yellow
Write-Host "  请把以下域名也加入代理（或直接开 TUN / 全局模式）：" -ForegroundColor Gray
Write-Host "      uploads.github.com" -ForegroundColor Gray
Write-Host "      objects.githubusercontent.com" -ForegroundColor Gray
Write-Host "      release-assets.githubusercontent.com" -ForegroundColor Gray
Write-Host "  （github.com 能代理但这三个没代理，上传就会绕开代理走直连）" -ForegroundColor DarkGray
Write-Host ""
Write-Host "★ 最快方案：用 GitHub Actions 在云端构建+上传，本地无需上传" -ForegroundColor Yellow
Write-Host "    git tag v$version" -ForegroundColor Gray
Write-Host "    git push origin v$version" -ForegroundColor Gray
Write-Host "    （见 .github/workflows/release.yml）" -ForegroundColor DarkGray
Write-Host ""
Write-Host "★ 手动上传：只传压缩包，不要传整个目录" -ForegroundColor Yellow
Write-Host "    gh release create v$version .\publish\*.zip .\publish\installer\*.exe --clobber" -ForegroundColor Gray

Write-Host "`n===== 完成 =====" -ForegroundColor Cyan
Get-ChildItem ".\publish\*.zip" -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host ("  - {0}  ({1:N1} MB)" -f $_.Name, ($_.Length / 1MB)) -ForegroundColor White
}
if (Test-Path ".\publish\installer") {
    Get-ChildItem ".\publish\installer\*.exe" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host ("  - {0}  ({1:N1} MB)" -f $_.Name, ($_.Length / 1MB)) -ForegroundColor White
    }
}