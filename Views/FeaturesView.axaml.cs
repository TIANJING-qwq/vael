using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SBtools.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class FeaturesView : UserControl
{
    private string? _mountedDriveLetter;
    private int _dailyQuoteIndex = -1;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        LoadRandomImage();
    }

    // ============================================================
    // 涩图合集（功能页卡片入口 + 主页随机一图）
    // ============================================================
    private void GalleryTile_Click(object? sender, RoutedEventArgs e) => MainWindow.ShowGallery();

    private void WidgetTile_Click(object? sender, RoutedEventArgs e) => MainWindow.ToggleWidget();

    private void LoadRandomImage()
    {
        try
        {
            if (!GalleryService.HasCache || RandomImage.Source != null) return;

            var dir = GalleryService.CacheDir;
            var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
            var imgs = Array.FindAll(files, f =>
                f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
            if (imgs.Length == 0) return;

            var pick = imgs[Random.Shared.Next(imgs.Length)];
            RandomImage.Source = new Avalonia.Media.Imaging.Bitmap(pick);
        }
        catch (Exception ex)
        {
            LogService.Log($"主页随机图加载失败: {ex.Message}", "功能");
        }
    }

    private static readonly string[] _dailyQuotes =
    {
        "行到水穷处，坐看云起时。",
        "日拱一卒，功不唐捐。",
        "路虽远，行则将至；事虽难，做则必成。",
        "不积跬步，无以至千里。",
        "凡是过往，皆为序章。",
        "山不让尘，川不辞盈。",
        "追风赶月莫停留，平芜尽处是春山。",
        "心之所向，素履以往。",
        "星光不负赶路人。",
        "保持热爱，奔赴山海。",
        "博观而约取，厚积而薄发。",
        "长风破浪会有时，直挂云帆济沧海。",
        "人生如逆旅，我亦是行人。",
        "努力尽今夕，少年犹可夸。",
        "道阻且长，行则将至。",
    };

    public FeaturesView()
    {
        InitializeComponent();
        LoadDailyQuote();
    }

    private void LoadDailyQuote()
    {
        try
        {
            if (_dailyQuoteIndex < 0)
                _dailyQuoteIndex = DateTime.Now.DayOfYear % _dailyQuotes.Length;
            DailyQuoteText.Text = _dailyQuotes[_dailyQuoteIndex];
        }
        catch { }
    }

    private void DailyQuoteCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            _dailyQuoteIndex = (_dailyQuoteIndex + 1) % _dailyQuotes.Length;
            DailyQuoteText.Text = _dailyQuotes[_dailyQuoteIndex];
        }
        catch { }
    }

    private void PlayCctvButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            MainWindow.PlayCctv();
        }
        catch (Exception ex)
        {
            LogService.Log($"播放 CCTV 异常: {ex.Message}", "播放");
        }
    }

    private void NetworkTile_Click(object? sender, RoutedEventArgs e)
        => MainWindow.NavigateTo("network");

    private void Win11Tile_Click(object? sender, RoutedEventArgs e)
    {
        HomeView.IsVisible = false;
        Win11Detail.IsVisible = true;
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        Win11Detail.IsVisible = false;
        HomeView.IsVisible = true;
    }

    // ============================================================
    // 选择 ISO 并挂载
    // ============================================================
    private async void Win11SelectIsoButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                UpdateStatus("❌ 此功能仅支持 Windows");
                return;
            }

            if (!IsAdministratorWindows())
            {
                UpdateStatus("❌ 需要管理员权限。请右键程序 → 「以管理员身份运行」");
                MainWindow.PushToast("权限不足", "此功能需要管理员权限");
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                UpdateStatus("❌ 无法获取窗口句柄");
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择 Windows 11 ISO 文件",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("ISO 镜像") { Patterns = new[] { "*.iso" } },
                    new FilePickerFileType("所有文件") { Patterns = new[] { "*.*" } }
                }
            });

            if (files == null || files.Count == 0)
            {
                UpdateStatus("已取消选择");
                return;
            }

            var isoPath = files[0].Path.LocalPath;
            if (!File.Exists(isoPath))
            {
                UpdateStatus($"❌ 文件不存在: {isoPath}");
                return;
            }

            UpdateStatus($"📀 正在挂载 ISO: {Path.GetFileName(isoPath)}...");
            LogService.Log($"挂载 ISO: {isoPath}", "Win11升级");

            var driveLetter = await MountIsoAsync(isoPath);
            if (string.IsNullOrEmpty(driveLetter))
            {
                UpdateStatus("❌ 挂载失败，请检查文件是否为有效 ISO");
                return;
            }

            _mountedDriveLetter = driveLetter;
            UpdateStatus($"✅ 已挂载到 {driveLetter}:，正在启动 setup.exe...");

            var success = await RunSetupAsync(driveLetter);
            if (success)
            {
                UpdateStatus($"✅ 已启动 Windows 11 安装程序（/product server 模式）\n" +
                             $"挂载盘符: {driveLetter}:\n" +
                             $"安装完成后请点击「卸载已挂载的 ISO」");
                MainWindow.PushToast("安装程序已启动", "请按屏幕提示完成升级");
            }
            else
            {
                UpdateStatus($"⚠️ 已挂载到 {driveLetter}:，但未能启动 setup.exe，请手动打开该盘符运行 setup.exe");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"❌ 异常: {ex.Message}");
            LogService.Log($"Win11 升级异常: {ex.Message}", "Win11升级");
        }
    }

    // ============================================================
    // 卸载 ISO
    // ============================================================
    private async void Win11UnmountButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                UpdateStatus("❌ 此功能仅支持 Windows");
                return;
            }

            if (string.IsNullOrEmpty(_mountedDriveLetter))
            {
                UpdateStatus("未记录挂载盘符，尝试卸载所有 ISO 镜像...");
            }

            var ok = await UnmountAllIsoAsync();
            _mountedDriveLetter = null;

            UpdateStatus(ok ? "✅ 已卸载所有 ISO 镜像" : "⚠️ 卸载完成（可能有残留）");
            LogService.Log($"卸载 ISO: {(ok ? "成功" : "部分成功")}", "Win11升级");
        }
        catch (Exception ex)
        {
            UpdateStatus($"❌ 卸载失败: {ex.Message}");
        }
    }

    // ============================================================
    // 挂载 ISO
    // ============================================================
    private static async Task<string?> MountIsoAsync(string isoPath)
    {
        var escapedPath = isoPath.Replace("'", "''");

        var script = $@"
$ErrorActionPreference = 'Stop'
$image = Mount-DiskImage -ImagePath '{escapedPath}' -PassThru
$volume = $image | Get-Volume
Write-Output $volume.DriveLetter
";

        var (exitCode, output, error) = await RunPowerShellAsync(script);
        if (exitCode != 0)
        {
            LogService.Log($"Mount-DiskImage 失败: {error}", "Win11升级");
            return null;
        }

        var match = Regex.Match(output ?? "", @"[A-Z]");
        return match.Success ? match.Value : null;
    }

    // ============================================================
    // 卸载所有 ISO
    // ============================================================
    private static async Task<bool> UnmountAllIsoAsync()
    {
        var script = @"
$ErrorActionPreference = 'SilentlyContinue'
$images = Get-DiskImage | Where-Object { $_.ImagePath -like '*.iso' -and $_.Attached }
foreach ($img in $images) {
    Dismount-DiskImage -ImagePath $img.ImagePath
}
Write-Output 'OK'
";

        var (exitCode, _, _) = await RunPowerShellAsync(script);
        return exitCode == 0;
    }

    // ============================================================
    // 启动 setup.exe /product server
    // ============================================================
    private static async Task<bool> RunSetupAsync(string driveLetter)
    {
        try
        {
            var setupPath = $"{driveLetter}:\\setup.exe";
            if (!File.Exists(setupPath))
            {
                LogService.Log($"找不到 {setupPath}", "Win11升级");
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = setupPath,
                Arguments = "/product server",
                UseShellExecute = true,
                Verb = "runas"
            };

            var proc = Process.Start(psi);
            LogService.Log($"已启动 setup.exe /product server (PID={proc?.Id})", "Win11升级");

            return await Task.FromResult(proc != null);
        }
        catch (Exception ex)
        {
            LogService.Log($"启动 setup.exe 失败: {ex.Message}", "Win11升级");
            return false;
        }
    }

    // ============================================================
    // PowerShell 执行辅助
    // ============================================================
    private static async Task<(int exitCode, string stdout, string stderr)> RunPowerShellAsync(string script)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return (-1, "", "进程启动失败");

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();

            await proc.WaitForExitAsync();

            return (proc.ExitCode, stdout, stderr);
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }

    // ============================================================
    // 管理员检查（只 Windows 有效）
    // ============================================================
    [SupportedOSPlatform("windows")]
    private static bool IsAdministratorWindows()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void UpdateStatus(string text)
    {
        var statusText = this.FindControl<TextBlock>("Win11StatusText");
        if (statusText != null)
            statusText.Text = text;

        LogService.Log(text, "Win11升级");
    }
}