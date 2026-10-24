using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SBtools.Controls;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class SettingsView : UserControl
{
    private readonly ConfigManager _config = ConfigManager.Instance;
    private bool _isInitializing;

    /// <summary>本次进入设置页是否已通过安全码验证</summary>
    private bool _isUnlocked;

    /// <summary>本次锁定是否已记录日志（避免重复进入时刷屏）</summary>
    private bool _lockLogged;

    public SettingsView()
    {
        InitializeComponent();
        _isInitializing = true;

        // WiFi 认证
        var usernameBox = this.FindControl<TextBox>("UsernameBox");
        var passwordBox = this.FindControl<TextBox>("PasswordBox");
        if (usernameBox != null) usernameBox.Text = _config.Username;
        if (passwordBox != null) passwordBox.Text = _config.Password;

        // 主题
        var themeCombo = this.FindControl<ComboBox>("ThemeModeComboBox");
        if (themeCombo != null)
        {
            themeCombo.SelectedIndex = _config.ThemeMode switch
            {
                "Light" => 0,
                "Dark"  => 1,
                _       => 2,
            };
        }

        // 窗口材质
        var backdropCombo = this.FindControl<ComboBox>("WindowBackdropComboBox");
        if (backdropCombo != null)
        {
            backdropCombo.SelectedIndex = _config.WindowBackdrop switch
            {
                "Mica"    => 0,
                "Acrylic" => 1,
                _         => 2,
            };
        }

        // 点击音效
        var clickSoundSwitch = this.FindControl<ToggleSwitch>("ClickSoundSwitch");
        if (clickSoundSwitch != null)
            clickSoundSwitch.IsChecked = _config.ClickSoundEnabled;

        var clickSoundSlider = this.FindControl<Slider>("ClickSoundVolumeSlider");
        if (clickSoundSlider != null)
            clickSoundSlider.Value = _config.ClickSoundVolume;

        var clickSoundText = this.FindControl<TextBlock>("ClickSoundVolumeText");
        if (clickSoundText != null)
            clickSoundText.Text = _config.ClickSoundVolume.ToString();

        LoadSoundFiles();

        // 更新代理
        var proxyCombo = this.FindControl<ComboBox>("UpdateProxyComboBox");
        if (proxyCombo != null)
        {
            proxyCombo.SelectedIndex = _config.UpdateProxy switch
            {
                "https://gh-proxy.com/"    => 1,
                "https://gh-proxy.org/"    => 2,
                "https://v4.gh-proxy.org/" => 3,
                "https://v6.gh-proxy.org/" => 4,
                _ => 0,
            };
        }

        // 更新：下载方式 / 安装方式
        var autoDlSwitch = this.FindControl<ToggleSwitch>("AutoDownloadSwitch");
        if (autoDlSwitch != null)
            autoDlSwitch.IsChecked = _config.AutoDownloadUpdate;

        var autoInSwitch = this.FindControl<ToggleSwitch>("AutoInstallSwitch");
        if (autoInSwitch != null)
            autoInSwitch.IsChecked = _config.AutoInstallUpdate;

        // 图库位置
        UpdateGalleryPathDisplay();

        // 通知
        var autoCollapseSwitch = this.FindControl<ToggleSwitch>("AutoCollapseSwitch");
        if (autoCollapseSwitch != null)
            autoCollapseSwitch.IsChecked = _config.AutoCollapseOnNewToast;
        ToastHost.AutoCollapseOnNew = _config.AutoCollapseOnNewToast;

        // 插件联动
        var autoActionSwitch = this.FindControl<ToggleSwitch>("AutoActionSwitch");
        if (autoActionSwitch != null)
            autoActionSwitch.IsChecked = _config.AutoActionOnNotify;

        var urlBox = this.FindControl<TextBox>("AutoActionUrlBox");
        if (urlBox != null) urlBox.Text = _config.AutoActionUrl;

        var countBox = this.FindControl<NumericUpDown>("AutoActionCountBox");
        if (countBox != null) countBox.Value = _config.AutoActionOpenCount;

        var intervalBox = this.FindControl<NumericUpDown>("AutoActionIntervalBox");
        if (intervalBox != null) intervalBox.Value = _config.AutoActionOpenIntervalMs;

        var volumeBox = this.FindControl<NumericUpDown>("AutoActionVolumeBox");
        if (volumeBox != null) volumeBox.Value = _config.AutoActionVolume;

        var holdBox = this.FindControl<NumericUpDown>("AutoActionHoldBox");
        if (holdBox != null) holdBox.Value = _config.AutoActionVolumeHoldSeconds;

        var overlaySwitch = this.FindControl<ToggleSwitch>("OverlaySwitch");
        if (overlaySwitch != null)
            overlaySwitch.IsChecked = _config.AutoActionShowOverlay;

        var overlaySecBox = this.FindControl<NumericUpDown>("OverlaySecondsBox");
        if (overlaySecBox != null) overlaySecBox.Value = _config.AutoActionOverlaySeconds;

        // 密码确认框
        var passwordPromptSwitch = this.FindControl<ToggleSwitch>("PasswordPromptSwitch");
        if (passwordPromptSwitch != null)
            passwordPromptSwitch.IsChecked = _config.PasswordPromptEnabled;

        var passwordPromptSecBox = this.FindControl<NumericUpDown>("PasswordPromptSecondsBox");
        if (passwordPromptSecBox != null)
            passwordPromptSecBox.Value = _config.PasswordPromptSeconds;

        var passwordPromptPwdBox = this.FindControl<TextBox>("PasswordPromptPasswordBox");
        if (passwordPromptPwdBox != null)
            passwordPromptPwdBox.Text = _config.PasswordPromptPassword;

        // 程序选项
        var autoStartSwitch = this.FindControl<ToggleSwitch>("AutoStartSwitch");
        var autoStartMinimizedSwitch = this.FindControl<ToggleSwitch>("AutoStartMinimizedSwitch");
        var minTraySwitch = this.FindControl<ToggleSwitch>("MinTraySwitch");

        if (autoStartSwitch != null)
            autoStartSwitch.IsChecked = _config.AutoStartOnBoot;
        if (autoStartMinimizedSwitch != null)
            autoStartMinimizedSwitch.IsChecked = _config.AutoStartMinimized;
        if (minTraySwitch != null)
            minTraySwitch.IsChecked = _config.MinimizeToTrayOnClose;

        var startupAnimSwitch = this.FindControl<ToggleSwitch>("StartupAnimationSwitch");
        if (startupAnimSwitch != null)
            startupAnimSwitch.IsChecked = _config.StartupAnimationEnabled;

        // ★ 退出安全码
        var quitPwdSwitch = this.FindControl<ToggleSwitch>("QuitPasswordSwitch");
        if (quitPwdSwitch != null)
            quitPwdSwitch.IsChecked = _config.QuitPasswordEnabled;

        var quitPwdBox = this.FindControl<TextBox>("QuitPasswordBox");
        if (quitPwdBox != null)
        {
            try { quitPwdBox.Text = _config.QuitPassword; } catch { }
        }

        // ★ 更改设置需要安全码
        var protectSwitch = this.FindControl<ToggleSwitch>("ProtectSettingsSwitch");
        if (protectSwitch != null)
            protectSwitch.IsChecked = _config.ProtectSettingsEnabled;

        _isInitializing = false;

        // ★ 每次进入结构树（即进入设置页）都重新上锁
        AttachedToVisualTree += (_, _) => OnEnter();
        OnEnter();
    }

    // ============================================================
    // ★ 设置锁（更改设置需要安全码）
    // ============================================================

    /// <summary>
    /// 进入设置页时调用：重置解锁状态，并按配置决定是否上锁。
    /// </summary>
    public void OnEnter()
    {
        _isUnlocked = false;
        ApplyLockState();
        LoadAdminStartState();
    }

    /// <summary>安全码是否已开启且已设置</summary>
    private static bool HasSecurityCode()
    {
        try
        {
            var c = ConfigManager.Instance;
            return c.QuitPasswordEnabled && !string.IsNullOrEmpty(c.QuitPassword);
        }
        catch { return false; }
    }

    /// <summary>「更改设置需要安全码」是否生效（必须已开启安全码）</summary>
    private static bool IsSettingsProtectionActive()
    {
        try
        {
            return ConfigManager.Instance.ProtectSettingsEnabled && HasSecurityCode();
        }
        catch { return false; }
    }

    /// <summary>按当前状态刷新锁：内容禁用 + 显示解锁面板</summary>
    private void ApplyLockState()
    {
        bool locked = IsSettingsProtectionActive() && !_isUnlocked;

        var overlay = this.FindControl<Border>("SettingsLockOverlay");
        var scroll = this.FindControl<ScrollViewer>("SettingsScrollViewer");
        var error = this.FindControl<TextBlock>("UnlockErrorText");
        var box = this.FindControl<TextBox>("UnlockCodeBox");

        if (overlay != null) overlay.IsVisible = locked;
        if (scroll != null) scroll.IsEnabled = !locked;
        if (error != null) error.IsVisible = false;

        if (locked)
        {
            if (box != null)
            {
                box.Text = "";
                try { box.Focus(); } catch { }
            }

            if (!_lockLogged)
            {
                LogService.Log("设置页已锁定（更改设置需要安全码）", "配置");
                _lockLogged = true;
            }
        }
        else
        {
            _lockLogged = false;
        }
    }

    private void UnlockCodeBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        TryUnlock();
    }

    private void UnlockButton_Click(object? sender, RoutedEventArgs e) => TryUnlock();

    private void TryUnlock()
    {
        var box = this.FindControl<TextBox>("UnlockCodeBox");
        var error = this.FindControl<TextBlock>("UnlockErrorText");
        var input = box?.Text ?? "";

        string correct;
        try { correct = ConfigManager.Instance.QuitPassword; }
        catch { correct = ""; }

        if (string.IsNullOrEmpty(correct) || input != correct)
        {
            if (error != null)
            {
                error.Text = "安全码错误";
                error.IsVisible = true;
            }

            if (box != null)
            {
                box.Text = "";
                try { box.Focus(); } catch { }
            }

            LogService.Log("设置页解锁失败：安全码错误", "配置");
            return;
        }

        _isUnlocked = true;
        LogService.Log("设置页已解锁", "配置");
        ApplyLockState();
    }

    /// <summary>弹出安全码验证对话框（用于保护安全码自身）</summary>
    private async Task<bool> VerifyCurrentCodeAsync(string title, string prompt)
    {
        try
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return false;

            var dialog = new QuitPasswordDialog(title, prompt, "确认", "安全码错误", "安全码");
            await dialog.ShowDialog(owner);
            return dialog.Verified;
        }
        catch (Exception ex)
        {
            LogService.Log($"安全码验证异常: {ex.Message}", "配置");
            return false;
        }
    }

    // ============================================================
    // 音效文件
    // ============================================================
    private void LoadSoundFiles()
    {
        try
        {
            var combo = this.FindControl<ComboBox>("ClickSoundFileComboBox");
            if (combo == null) return;

            var files = ClickSoundService.GetAvailableSounds();
            combo.ItemsSource = files;

            var current = _config.ClickSoundFile;
            var idx = Array.IndexOf(files, current);
            if (idx < 0)
            {
                idx = files.Length > 0 ? 0 : -1;
                if (idx >= 0) _config.ClickSoundFile = files[idx];
            }

            combo.SelectedIndex = idx;

            var hint = this.FindControl<TextBlock>("SoundsHintText");
            if (hint != null && files.Length == 0)
            {
                hint.Text = $"Sounds 目录里没有 .wav 文件\n路径: {ClickSoundService.SoundsDir}";
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"加载音效文件失败: {ex.Message}", "音效");
        }
    }

    private void ClickSoundFileComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        var combo = sender as ComboBox;
        if (combo?.SelectedItem is string fileName)
        {
            _config.ClickSoundFile = fileName;
            ClickSoundService.Instance.Reload();
            MainWindow.PushToast("点击音效", $"已切换到: {fileName}");
        }
    }

    private void RefreshSounds_Click(object? sender, RoutedEventArgs e)
    {
        var backup = _isInitializing;
        _isInitializing = true;
        LoadSoundFiles();
        _isInitializing = backup;

        var files = ClickSoundService.GetAvailableSounds();
        MainWindow.PushToast("音效列表", $"共找到 {files.Length} 个 .wav 文件");
    }

    private void PreviewSound_Click(object? sender, RoutedEventArgs e)
    {
        ClickSoundService.Instance.Preview();
    }

    // ============================================================
    // 图库位置
    // ============================================================
    private void UpdateGalleryPathDisplay()
    {
        try
        {
            var box = this.FindControl<TextBox>("GalleryPathBox");
            if (box == null) return;

            var cfg = ConfigManager.Instance;
            box.Text = cfg.GalleryPath;

            if (cfg.IsDefaultGalleryPath)
                box.Watermark = "默认路径 (%APPDATA%\\SchoolBusytools\\gallery)";
            else
                box.Watermark = "";
        }
        catch { }
    }

    private async void BrowseGalleryPath_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "选择图库保存位置",
                    AllowMultiple = false
                });

            if (folders == null || folders.Count == 0) return;

            var picked = folders[0];
            var path = picked.Path.LocalPath;
            if (string.IsNullOrEmpty(path)) return;

            ConfigManager.Instance.GalleryPath = path;
            UpdateGalleryPathDisplay();

            MainWindow.PushToast("图库位置", $"已切换到:\n{path}");
            LogService.Log($"图库位置已更改: {path}", "图库");
        }
        catch (Exception ex)
        {
            MainWindow.PushToast("选择失败", ex.Message);
        }
    }

    private void OpenGalleryPath_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var path = ConfigManager.Instance.GalleryPath;
            System.IO.Directory.CreateDirectory(path);

            if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", path);
            else
                System.Diagnostics.Process.Start("xdg-open", path);
        }
        catch (Exception ex)
        {
            MainWindow.PushToast("打开失败", ex.Message);
        }
    }

    private void ResetGalleryPath_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var defaultPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SchoolBusytools", "gallery");

            ConfigManager.Instance.GalleryPath = defaultPath;
            UpdateGalleryPathDisplay();

            MainWindow.PushToast("图库位置", "已重置为默认路径");
        }
        catch (Exception ex)
        {
            MainWindow.PushToast("重置失败", ex.Message);
        }
    }

    // ============================================================
    // WiFi 认证
    // ============================================================
    private void SaveCredentials_Click(object? sender, RoutedEventArgs e)
    {
        var usernameBox = this.FindControl<TextBox>("UsernameBox");
        var passwordBox = this.FindControl<TextBox>("PasswordBox");

        var u = usernameBox?.Text?.Trim() ?? "";
        var p = passwordBox?.Text ?? "";
        if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
        {
            MainWindow.PushToast("保存失败", "账号和密码不能为空");
            return;
        }

        _config.Username = u;
        _config.Password = p;
        MainWindow.PushToast("设置已保存", "认证凭据已更新并加密存储。");
    }

    // ============================================================
    // 主题
    // ============================================================
    private void ThemeModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var combo = sender as ComboBox;
        if (combo == null) return;

        string mode = combo.SelectedIndex switch
        {
            0 => "Light",
            1 => "Dark",
            _ => "Default",
        };

        _config.ThemeMode = mode;
        App.ApplyTheme(mode);
        WindowBackdropService.RefreshAll();

        string display = mode switch
        {
            "Light" => "浅色",
            "Dark"  => "深色",
            _       => "跟随系统",
        };
        MainWindow.PushToast("主题", $"已切换为{display}模式");
    }

    // ============================================================
    // 窗口材质
    // ============================================================
    private void WindowBackdropComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var combo = sender as ComboBox;
        if (combo == null) return;

        string mode = combo.SelectedIndex switch
        {
            0 => "Mica",
            1 => "Acrylic",
            _ => "None",
        };

        _config.WindowBackdrop = mode;

        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is Avalonia.Controls.Window main)
            {
                if (!WindowBackdropService.IsTransparentModeSupported(main) &&
                    (mode == "Mica" || mode == "Acrylic"))
                {
                    MainWindow.PushToast("窗口材质",
                        "云母/亚克力需要 Win11 + 深色模式，已回退到不透明背景");
                }

                WindowBackdropService.Apply(main, mode);
            }
        }

        string display = mode switch
        {
            "Mica"    => "云母",
            "Acrylic" => "亚克力",
            _         => "无",
        };
        MainWindow.PushToast("窗口材质", $"已切换为: {display}");
    }

    // ============================================================
    // 点击音效
    // ============================================================
    private void ClickSoundSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("ClickSoundSwitch");
        _config.ClickSoundEnabled = sw?.IsChecked == true;
        MainWindow.PushToast("点击音效",
            _config.ClickSoundEnabled ? "已开启" : "已关闭");
    }

    private void ClickSoundVolumeSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        int volume = (int)e.NewValue;
        _config.ClickSoundVolume = volume;

        var text = this.FindControl<TextBlock>("ClickSoundVolumeText");
        if (text != null) text.Text = volume.ToString();

        ClickSoundService.Instance.UpdateVolume(volume);
    }

    // ============================================================
    // 更新代理
    // ============================================================
    private void UpdateProxyComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        var combo = sender as ComboBox;
        if (combo == null) return;

        string proxy = combo.SelectedIndex switch
        {
            1 => "https://gh-proxy.com/",
            2 => "https://gh-proxy.org/",
            3 => "https://v4.gh-proxy.org/",
            4 => "https://v6.gh-proxy.org/",
            _ => "",
        };

        _config.UpdateProxy = proxy;

        string display = combo.SelectedIndex switch
        {
            1 => "gh-proxy.com",
            2 => "gh-proxy.org",
            3 => "v4.gh-proxy.org",
            4 => "v6.gh-proxy.org",
            _ => "直连",
        };
        MainWindow.PushToast("更新代理", $"已切换为: {display}");
    }

    // ============================================================
    // 更新：下载方式 / 安装方式
    // ============================================================
    private void AutoDownloadSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("AutoDownloadSwitch");
        if (sw == null) return;
        _config.AutoDownloadUpdate = sw.IsChecked == true;
        MainWindow.PushToast("更新",
            _config.AutoDownloadUpdate ? "已开启自动下载" : "检测到更新后将先询问");
    }

    private void AutoInstallSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("AutoInstallSwitch");
        if (sw == null) return;
        _config.AutoInstallUpdate = sw.IsChecked == true;
        MainWindow.PushToast("更新",
            _config.AutoInstallUpdate ? "已开启自动安装" : "下载完成后将先询问");
    }

    // ============================================================
    // 通知
    // ============================================================
    private void AutoCollapseSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("AutoCollapseSwitch");
        var value = sw?.IsChecked == true;
        ToastHost.AutoCollapseOnNew = value;
        _config.AutoCollapseOnNewToast = value;
    }

    // ============================================================
    // 插件联动
    // ============================================================
    private void AutoActionSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("AutoActionSwitch");
        _config.AutoActionOnNotify = sw?.IsChecked == true;
    }

    private void AutoActionUrlBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<TextBox>("AutoActionUrlBox");
        var text = box?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;
        _config.AutoActionUrl = text;
    }

    private void AutoActionCountBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<NumericUpDown>("AutoActionCountBox");
        if (box?.Value is decimal v) _config.AutoActionOpenCount = (int)v;
    }

    private void AutoActionIntervalBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<NumericUpDown>("AutoActionIntervalBox");
        if (box?.Value is decimal v) _config.AutoActionOpenIntervalMs = (int)v;
    }

    private void AutoActionVolumeBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<NumericUpDown>("AutoActionVolumeBox");
        if (box?.Value is decimal v) _config.AutoActionVolume = (int)v;
    }

    private void AutoActionHoldBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<NumericUpDown>("AutoActionHoldBox");
        if (box?.Value is decimal v) _config.AutoActionVolumeHoldSeconds = (int)v;
    }

    private void OverlaySwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("OverlaySwitch");
        _config.AutoActionShowOverlay = sw?.IsChecked == true;
    }

    private void OverlaySecondsBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<NumericUpDown>("OverlaySecondsBox");
        if (box?.Value is decimal v) _config.AutoActionOverlaySeconds = (int)v;
    }

    // ============================================================
    // 密码确认框
    // ============================================================
    private void PasswordPromptSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("PasswordPromptSwitch");
        _config.PasswordPromptEnabled = sw?.IsChecked == true;
        MainWindow.PushToast("密码确认框",
            _config.PasswordPromptEnabled ? "已启用" : "已禁用");
    }

    private void PasswordPromptSecondsBox_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<NumericUpDown>("PasswordPromptSecondsBox");
        if (box?.Value is decimal v) _config.PasswordPromptSeconds = (int)v;
    }

    private void PasswordPromptPasswordBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var box = this.FindControl<TextBox>("PasswordPromptPasswordBox");
        var text = box?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(text)) return;
        _config.PasswordPromptPassword = text;
        MainWindow.PushToast("密码确认框", "密码已更新");
    }

    // ============================================================
    // 程序选项
    // ============================================================
    private void AutoStartSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("AutoStartSwitch");
        var value = sw?.IsChecked == true;
        _config.AutoStartOnBoot = value;

        try
        {
            AutoStartManager.Apply(value);
            MainWindow.PushToast("开机自启动", value ? "已开启" : "已关闭");
        }
        catch (Exception ex)
        {
            MainWindow.PushToast("设置失败", ex.Message);
        }
    }

    private void AutoStartMinimizedSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("AutoStartMinimizedSwitch");
        var value = sw?.IsChecked == true;
        _config.AutoStartMinimized = value;

        MainWindow.PushToast("自启动隐藏",
            value ? "开机后自动隐藏到托盘" : "开机后显示主窗口");
    }

    // ============================================================
    // 以管理员身份启动（最高权限计划任务，免 UAC）
    // ============================================================
    private const string AdminTaskName = "VaelAdminStart";
    private bool _adminInit;

    private void AdminStartSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_adminInit || _isInitializing) return;
        _ = ApplyAdminStartAsync(AdminStartSwitch.IsChecked == true);
    }

    private async Task ApplyAdminStartAsync(bool enable)
    {
        try
        {
            var exePath = Environment.ProcessPath
                          ?? System.IO.Path.Combine(AppContext.BaseDirectory, "SBtools.exe");
            var args = enable
                ? $"/create /tn \"{AdminTaskName}\" /tr \"\\\"{exePath}\\\" --autostart\" /sc onlogon /rl highest /delay 0000:10 /f"
                : $"/delete /tn \"{AdminTaskName}\" /f";

            var (code, output) = await RunProcessAsync("schtasks", args);
            AdminStartStatusText.Text = enable
                ? (code == 0
                    ? "已启用：登录时以最高权限自动启动（无需 UAC 确认）"
                    : "创建失败，请以管理员身份运行程序后重试")
                : "未启用";
            LogService.Log($"管理员自启 {(enable ? "创建" : "删除")} 结果: {code} {output}", "设置");
        }
        catch (Exception ex)
        {
            AdminStartStatusText.Text = $"操作失败: {ex.Message}";
        }
    }

    private async void LoadAdminStartState()
    {
        try
        {
            var (code, _) = await RunProcessAsync("schtasks", $"/query /tn \"{AdminTaskName}\"");
            var exists = code == 0;
            Dispatcher.UIThread.Post(() =>
            {
                _adminInit = true;
                if (AdminStartSwitch != null) AdminStartSwitch.IsChecked = exists;
                if (AdminStartStatusText != null)
                    AdminStartStatusText.Text = exists
                        ? "已启用：登录时以最高权限自动启动（无需 UAC 确认）"
                        : "未启用";
                _adminInit = false;
            });
        }
        catch { }
    }

    private static async Task<(int code, string output)> RunProcessAsync(string file, string args)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        try
        {
            using var p = Process.Start(psi);
            if (p == null) return (-1, "");
            var output = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var error = await p.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await p.WaitForExitAsync().ConfigureAwait(false);
            return (p.ExitCode, output + error);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    private void MinTraySwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("MinTraySwitch");
        _config.MinimizeToTrayOnClose = sw?.IsChecked == true;
    }

    private void StartupAnimationSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("StartupAnimationSwitch");
        var value = sw?.IsChecked == true;
        _config.StartupAnimationEnabled = value;
        MainWindow.PushToast("启动动画", value ? "已开启" : "已关闭");
    }

    // ============================================================
    // ★ 退出安全码
    // ============================================================
    private async void QuitPasswordSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("QuitPasswordSwitch");
        if (sw == null) return;

        bool want = sw.IsChecked == true;

        // ★ 关闭安全码前必须先验证当前安全码
        if (!want && HasSecurityCode())
        {
            bool passed = await VerifyCurrentCodeAsync("关闭安全码", "请输入当前安全码以关闭：");
            if (!passed)
            {
                _isInitializing = true;
                sw.IsChecked = true;
                _isInitializing = false;

                MainWindow.PushToast("退出安全码", "未通过验证，保持开启");
                return;
            }
        }

        _config.QuitPasswordEnabled = want;
        MainWindow.PushToast("退出安全码", want ? "已开启" : "已关闭");

        // 安全码关闭后，「更改设置需要安全码」自动失效
        ApplyLockState();
    }

    // ============================================================
    // ★ 更改设置需要安全码
    // ============================================================
    private void ProtectSettingsSwitch_Changed(object? sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        var sw = this.FindControl<ToggleSwitch>("ProtectSettingsSwitch");
        if (sw == null) return;

        bool want = sw.IsChecked == true;

        // 未开启安全码时该选项无意义，回退并提示
        if (want && !HasSecurityCode())
        {
            _isInitializing = true;
            sw.IsChecked = false;
            _isInitializing = false;

            MainWindow.PushToast("更改设置需安全码", "请先开启并设置上方的安全码");
            return;
        }

        _config.ProtectSettingsEnabled = want;
        MainWindow.PushToast("更改设置需安全码",
            want ? "已开启，下次进入设置页需验证" : "已关闭");
    }

    private async void SaveQuitPassword_Click(object? sender, RoutedEventArgs e)
    {
        var box = this.FindControl<TextBox>("QuitPasswordBox");
        var text = box?.Text ?? "";

        // ★ 已有安全码时，修改或清除都必须先验证当前安全码
        if (HasSecurityCode())
        {
            string current;
            try { current = _config.QuitPassword; } catch { current = ""; }

            if (text != current)
            {
                bool passed = await VerifyCurrentCodeAsync("修改安全码", "请输入当前安全码以确认：");
                if (!passed)
                {
                    MainWindow.PushToast("退出安全码", "未通过验证，未修改");
                    return;
                }
            }
        }

        try
        {
            _config.QuitPassword = text;

            if (string.IsNullOrEmpty(text))
            {
                _config.QuitPasswordEnabled = false;
                var sw = this.FindControl<ToggleSwitch>("QuitPasswordSwitch");
                if (sw != null) sw.IsChecked = false;

                MainWindow.PushToast("退出安全码", "已清除");
            }
            else
            {
                MainWindow.PushToast("退出安全码", "已保存");
            }

            LogService.Log("退出安全码已更新", "配置");

            // 安全码被清除后，「更改设置需要安全码」应立即失效
            ApplyLockState();
        }
        catch (Exception ex)
        {
            MainWindow.PushToast("保存失败", ex.Message);
        }
    }
}