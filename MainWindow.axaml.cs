using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Platform;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using SBtools.Controls;
using SBtools.Models;
using SBtools.Services;
using SBtools.Views;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SBtools;

public partial class MainWindow : Window
{
    private static MainWindow? _instance;
    private static PlaybackScheduler? _scheduler;
    private TrayIcon? _trayIcon;

    private bool _reallyQuit;
    private bool _closingDialogShown;
    private bool _isHiddenToTray;

    private Window? _playerWindow;

    private static bool _isAutoChecking = true;
    private bool _updateInProgress;

    private bool _windowShown;

    public MainWindow() : this(false) { }

    public MainWindow(bool startMinimized)
    {
        InitializeComponent();
        _instance = this;

        ApplyPlatformAvailability();

        WindowBackdropService.Apply(this, ConfigManager.Instance.WindowBackdrop);

        if (startMinimized)
        {
            Opacity = 1;
            if (RootGrid != null)
                RootGrid.RenderTransform = TransformOperations.Parse("scale(1.0)");
            _windowShown = false;
        }
        else
        {
            Opacity = 0;
            if (RootGrid != null)
                RootGrid.RenderTransform = TransformOperations.Parse("scale(0.97)");
            _windowShown = true;
        }

        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(FeaturesView));

        // ★ Frame 真正换页后再扫描挂钩下拉动画
        // （Frame.Navigate 延迟换内容，SelectionChanged 时 Content 还是旧页）
        ContentFrame.GetObservable(ContentProperty)
            .Subscribe(page => PopupAnimator.HookAll(page as Visual));

        _scheduler = new PlaybackScheduler(OnScheduleStart, OnScheduleStop);
        ApplyScheduleConfig();

        // 桌面小组件（延迟到启动动画结束后再显示，避免启动期黑框/无响应）
        if (ConfigManager.Instance.DesktopWidgetEnabled)
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                await System.Threading.Tasks.Task.Delay(3000);
                try { if (ConfigManager.Instance.DesktopWidgetEnabled && WidgetWindow.Instance == null) new WidgetWindow().Show(); } catch { }
            }, Avalonia.Threading.DispatcherPriority.Loaded);

        this.AddHandler(
            PointerReleasedEvent,
            OnGlobalPointerReleased,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        SetupTrayIcon();
        InitNetworkMonitor();
        InitUpdateService();
        LocalIpcServer.Instance.Start();

        Closing += OnWindowClosing;

        Opened += async (_, _) =>
        {
            // 窗口已显示、句柄可用时，重新应用一次背景材质（确保 DWM 属性生效）
            WindowBackdropService.Apply(this, ConfigManager.Instance.WindowBackdrop);

            if (!startMinimized || !_windowShown)
            {
                _windowShown = true;

                await Task.Delay(30);

                Opacity = 1;
                if (RootGrid != null)
                    RootGrid.RenderTransform = TransformOperations.Parse("scale(1.0)");

                LogService.Log("程序已启动", "启动");
            }

            if (_isAutoChecking && _windowShown)
            {
                StartUpdateCheckDelayed();
            }
        };

        if (startMinimized)
        {
            StartUpdateCheckDelayed();
        }
    }

    // ============================================================
    // 全局点击音效
    // ============================================================
    private void OnGlobalPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        try
        {
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                ClickSoundService.Instance.Play();
            }
        }
        catch { }
    }

    // ============================================================
    // 延迟检查更新
    // ============================================================
    private void StartUpdateCheckDelayed()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1500);

                // ★ 若有上次下载好但未安装的更新，先提示安装
                if (UpdateService.Instance.HasPendingUpdate)
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        try
                        {
                            if (_isHiddenToTray || !IsVisible)
                                RestoreMainWindow();

                            var dlg = new UpdateAvailableDialog(
                                "已下载", "检测到上次已下载好的更新，是否立即安装？",
                                "安装更新", "立即安装");

                            if (await dlg.ShowDialog<bool>(this))
                                await InstallWithProgressAsync();
                        }
                        catch (Exception ex)
                        {
                            LogService.Log($"待安装更新提示异常: {ex.Message}", "更新");
                        }
                    });
                    return;
                }

                // ★ 启动时检查一次，走后台自动更新流程
                var hasUpdate = await UpdateService.Instance.CheckForUpdatesAsync();
                await Dispatcher.UIThread.InvokeAsync(() => _ = HandleAutoUpdateAsync(hasUpdate));

                if (ConfigManager.Instance.AutoCheckUpdate)
                    UpdateService.Instance.StartAutoCheck();
            }
            catch (Exception ex)
            {
                LogService.Log($"自动检查更新失败: {ex.Message}", "更新");
            }
        });
    }

    public static PlaybackScheduler? Scheduler => _scheduler;

    public static void SetAutoChecking(bool value) => _isAutoChecking = value;

    public static void ApplyScheduleConfig()
    {
        if (_scheduler == null) return;
        _scheduler.Enabled = ScheduleConfig.Enabled;
        _scheduler.StartTime = ScheduleConfig.StartTime;
        _scheduler.EndTime = ScheduleConfig.EndTime;
        _scheduler.Days = ScheduleConfig.Days;
        string daysText = ScheduleConfig.Days.Count == 0
            ? "每天"
            : string.Join(",", ScheduleConfig.Days
                .OrderBy(d => (int)d == 0 ? 7 : (int)d)
                .Select(d => "周" + "日一二三四五六"[(int)d]));
        LogService.Log(
            $"调度配置同步: 启用={ScheduleConfig.Enabled}, " +
            $"时段={ScheduleConfig.StartTime:hh\\:mm}-{ScheduleConfig.EndTime:hh\\:mm}, " +
            $"星期={daysText}, " +
            $"频道={ScheduleConfig.ChannelName}",
            "调度");
    }

    // ============================================================
    // 导航
    // ============================================================
    private void NavView_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is NavigationViewItem item)
        {
            switch (item.Tag?.ToString())
            {
                case "features":     ContentFrame.Navigate(typeof(FeaturesView));     break;
                case "network":      ContentFrame.Navigate(typeof(NetworkView));      break;
                case "optimization": ContentFrame.Navigate(typeof(OptimizationView)); break;
                case "problems":     ContentFrame.Navigate(typeof(WindowsProblemsView)); break;
                case "backup":       ContentFrame.Navigate(typeof(BackupView));        break;
                case "tools":        ContentFrame.Navigate(typeof(SystemToolsView));  break;
                case "lab":          ContentFrame.Navigate(typeof(LabView));          break;
                case "update":       ContentFrame.Navigate(typeof(UpdateView));       break;
                case "gallery":      ContentFrame.Navigate(typeof(GalleryView));      break;
                case "plugins":      ContentFrame.Navigate(typeof(PluginsView));      break;
                case "settings":
                    ContentFrame.Navigate(typeof(SettingsView));
                    (ContentFrame.Content as SettingsView)?.OnEnter();
                    break;
                case "about":        ContentFrame.Navigate(typeof(AboutView));        break;
            }

            AnimateContentIn();
        }
    }

    /// <summary>全局页面进入动画：淡入 + 上浮</summary>
    private void AnimateContentIn()
    {
        try
        {
            if (ContentFrame.Content is not Control c) return;

            c.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(240),
                    Easing = new ExponentialEaseOut(),
                },
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(320),
                    Easing = new BackEaseOut(),
                },
            };

            c.Opacity = 0;
            c.RenderTransform = TransformOperations.Parse("translateY(16px)");

            Dispatcher.UIThread.Post(() =>
            {
                c.Opacity = 1;
                c.RenderTransform = TransformOperations.Parse("translateY(0px)");
            }, DispatcherPriority.Background);
        }
        catch { }
    }

    /// <summary>供其它页面跳转到指定导航页（tag 与 MainWindow.axaml 菜单 Tag 一致）。</summary>
    public static void NavigateTo(string tag)
    {
        if (_instance == null) return;
        var item = _instance.NavView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => i.Tag?.ToString() == tag);
        if (item != null)
            _instance.NavView.SelectedItem = item;
    }

    // ============================================================
    // 平台能力：按平台隐藏不可用的导航项
    // ============================================================
    private void ApplyPlatformAvailability()
    {
        if (PlatformInfo.IsWindows) return;

        HideNavItem("optimization");
        HideNavItem("problems");
    }

    private void HideNavItem(string tag)
    {
        var item = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => i.Tag?.ToString() == tag);
        if (item != null)
            NavView.MenuItems.Remove(item);
    }

    // ============================================================
    // 导航返回（NavigationView 自带返回按钮）
    // ============================================================
    private void NavView_BackRequested(object? sender, NavigationViewBackRequestedEventArgs e)
    {
        try
        {
            LogService.Log("点击返回按钮", "导航");

            // 1) 同步侧边栏选中项（由 SelectionChanged 触发导航）
            var featuresItem = NavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(i => i.Tag?.ToString() == "features");
            if (featuresItem != null)
                NavView.SelectedItem = featuresItem;

            // 2) 兜底：若内容还不是主页，直接强制导航（不依赖 SelectedItem 事件）
            if (ContentFrame.Content is not FeaturesView)
                ContentFrame.Navigate(typeof(FeaturesView));
        }
        catch (Exception ex)
        {
            LogService.Log($"返回失败: {ex.Message}", "导航");
        }
    }

    // ============================================================
    // 网络监控
    // ============================================================
    private void InitNetworkMonitor()
    {
        try
        {
            NetworkMonitor.Instance.Start(15);

            NetworkMonitor.Instance.StatusChanged += (connected) =>
            {
                LogService.Log(
                    $"网络状态变化: {(connected ? "已连接" : "断开")}",
                    "网络");
            };

            LogService.Log("全局网络监控已启动", "网络");
        }
        catch (Exception ex)
        {
            LogService.Log($"网络监控启动失败: {ex.Message}", "网络");
        }
    }

    public static void RefreshAuthenticator()
    {
        try
        {
            NetworkMonitor.Instance.ReloadCredentials();
            LogService.Log("认证凭据已刷新", "网络");
        }
        catch { }
    }

    // ============================================================
    // 自动更新
    // ============================================================
    private void InitUpdateService()
    {
        try
        {
            var updater = UpdateService.Instance;

            // ★ 后台自动更新（定时检查）：发现新版本后按设置决定是否静默下载/安装
            updater.AutoCheckCompleted += hasUpdate =>
            {
                Dispatcher.UIThread.Post(() => _ = HandleAutoUpdateAsync(hasUpdate));
            };

            updater.InstallCompleted += success =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (success)
                        PushToast("更新安装已开始", "应用即将自动重启。");
                    else
                        PushToast("更新失败", UpdateService.Instance.LastError ?? "请稍后重试。");
                });
            };

            LogService.Log("更新事件已订阅", "更新");
        }
        catch (Exception ex)
        {
            LogService.Log($"订阅更新事件失败: {ex.Message}", "更新");
        }
    }

    // ============================================================
    // ★ 后台自动更新流程（与手动「检查更新」分开）
    // ============================================================
    private async Task HandleAutoUpdateAsync(bool hasUpdate)
    {
        if (!hasUpdate) return;
        if (_updateInProgress) return;
        _updateInProgress = true;

        try
        {
            var version = UpdateService.Instance.LatestVersion ?? "";
            bool autoDownload = ConfigManager.Instance.AutoDownloadUpdate;
            bool autoInstall  = ConfigManager.Instance.AutoInstallUpdate;

            if (!autoDownload)
            {
                // 下载方式=手动：弹窗确认是否下载
                if (_isHiddenToTray || !IsVisible) RestoreMainWindow();
                var dialog = new UpdateAvailableDialog(version, UpdateService.Instance.GetChangelog());
                if (!await dialog.ShowDialog<bool>(this))
                {
                    LogService.Log($"用户忽略新版本 {version}", "更新");
                    return;
                }
            }
            else
            {
                PushToast("发现新版本", $"正在自动下载 v{version} …");
            }

            bool fullyAuto = autoDownload && autoInstall;
            var progress = fullyAuto ? null : new UpdateDownloadDialog();
            if (progress != null) progress.Show(this);

            bool downloaded = await UpdateService.Instance.DownloadAsync();
            if (progress != null) { try { await progress.CloseWithAnimationAsync(); } catch { } }

            if (!downloaded)
            {
                PushToast("更新失败", UpdateService.Instance.LastError ?? "下载失败，请稍后重试。");
                return;
            }

            if (autoInstall)
            {
                // 安装方式=自动：静默替换并重启（右下角进度弹窗）
                await InstallWithProgressAsync();
            }
            else
            {
                // 安装方式=手动：弹窗确认
                if (_isHiddenToTray || !IsVisible) RestoreMainWindow();
                var dialog = new UpdateAvailableDialog(version, "新版本已下载完成，是否立即安装？", "安装更新", "立即安装");
                if (await dialog.ShowDialog<bool>(this))
                    await InstallWithProgressAsync();
                else
                    PushToast("更新已下载", "稍后会再次提示安装。");
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"自动更新流程异常: {ex.Message}", "更新");
        }
        finally
        {
            _updateInProgress = false;
        }
    }

    // ============================================================
    // 隐藏 / 恢复
    // ============================================================
    private void HideToTray()
    {
        try
        {
            _isHiddenToTray = true;
            Hide();
            ShowInTaskbar = false;
            LogService.Log("窗口已隐藏到托盘", "窗口");
        }
        catch { }
    }

    private void RestoreMainWindow()
    {
        try
        {
            LogService.Log($"恢复窗口：IsVisible={IsVisible}, _windowShown={_windowShown}", "窗口");

            _isHiddenToTray = false;

            Opacity = 1;
            if (RootGrid != null)
                RootGrid.RenderTransform = TransformOperations.Parse("scale(1.0)");

            ShowInTaskbar = true;
            WindowState = WindowState.Normal;

            if (!IsVisible)
            {
                Show();
            }

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    Activate();
                    Topmost = true;
                    Topmost = false;
                }
                catch { }
            }, DispatcherPriority.Background);

            LogService.Log("窗口已恢复", "窗口");
        }
        catch (Exception ex)
        {
            LogService.Log($"恢复窗口失败: {ex.Message}", "窗口");
        }
    }

    // ============================================================
    // 关闭
    // ============================================================
    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_reallyQuit) return;

        if (_isHiddenToTray)
        {
            LogService.Log("内部隐藏导致的 Closing，放行", "窗口");
            return;
        }

        if (_closingDialogShown) { e.Cancel = true; return; }

        if (ConfigManager.Instance.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        e.Cancel = true;
        _closingDialogShown = true;

        try
        {
            Opacity = 1;

            if (!IsVisible)
            {
                RestoreMainWindow();
                await Task.Delay(150);
            }

            var dialog = new CloseConfirmDialog();
            var result = await dialog.ShowDialog<CloseAction>(this);

            switch (result)
            {
                case CloseAction.Quit:
                    _reallyQuit = true;
                    QuitApplication();
                    break;
                case CloseAction.MinimizeToTray:
                    HideToTray();
                    PushToast("已最小化", "Vael正在后台运行，双击托盘图标可恢复。");
                    break;
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"关闭询问异常: {ex.Message}", "窗口");
        }
        finally { _closingDialogShown = false; }
    }

    // ============================================================
    // ★ 从托盘触发退出（带安全码验证）
    // ============================================================
    private async Task TryQuitFromTrayAsync()
    {
        try
        {
            bool needPassword = false;
            try
            {
                needPassword = ConfigManager.Instance.QuitPasswordEnabled
                    && !string.IsNullOrEmpty(ConfigManager.Instance.QuitPassword);
            }
            catch { }

            if (!needPassword)
            {
                _reallyQuit = true;
                QuitApplication();
                return;
            }

            if (_isHiddenToTray || !IsVisible)
                RestoreMainWindow();

            await Task.Delay(100);

            var dialog = new QuitPasswordDialog();
            await dialog.ShowDialog(this);

            if (!dialog.Verified)
            {
                LogService.Log("托盘退出被取消（安全码验证失败或未通过）", "托盘");
                return;
            }

            LogService.Log("托盘退出验证通过，开始退出", "托盘");
            _reallyQuit = true;
            QuitApplication();
        }
        catch (Exception ex)
        {
            LogService.Log($"托盘退出异常: {ex.Message}", "托盘");
        }
    }

    // ============================================================
    // 退出
    // ============================================================
    private void QuitApplication()
    {
        try
        {
            LogService.Log("准备退出程序", "退出");

            ClosePlayerWindow();

            try { NetworkMonitor.Instance.Stop(); } catch { }

            _scheduler?.Dispose();
            _scheduler = null;

            try { UpdateService.Instance.StopAutoCheck(); } catch { }
            try { LocalIpcServer.Instance.Stop(); } catch { }
            try { ClickSoundService.Instance.Dispose(); } catch { }

            if (_trayIcon != null)
            {
                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                lifetime.Shutdown();
        }
        catch { Environment.Exit(0); }
    }

    // ============================================================
    // 调度器
    // ============================================================
    private void OnScheduleStart()
        => StartPlayer(ScheduleConfig.ChannelName, "定时播放", "调度");

    /// <summary>托盘「测试播放」：恢复上次观看的频道</summary>
    private void TestPlayLastChannel()
        => StartPlayer(ConfigManager.Instance.LastChannelName, "测试播放", "播放");

    /// <summary>供其它页面调用的「播放 CCTV」入口（恢复上次频道）</summary>
    public static void PlayCctv() => _instance?.TestPlayLastChannel();

    /// <summary>功能页卡片入口：进入涩图合集（侧边栏已隐藏该项）</summary>
    public static void ShowGallery()
    {
        if (_instance == null) return;
        _instance.ContentFrame.Navigate(typeof(GalleryView));
        PopupAnimator.HookAll(_instance.ContentFrame.Content as Visual);
    }

    /// <summary>功能页卡片入口：进入小组件页</summary>
    public static void ShowWidgetPage()
    {
        if (_instance == null) return;
        _instance.ContentFrame.Navigate(typeof(WidgetView));
        PopupAnimator.HookAll(_instance.ContentFrame.Content as Visual);
    }

    /// <summary>桌面性能小组件开关</summary>
    public static void ToggleWidget()
    {
        if (ConfigManager.Instance.DesktopWidgetEnabled)
        {
            ConfigManager.Instance.DesktopWidgetEnabled = false;
            WidgetWindow.Instance?.Close();
        }
        else
        {
            ConfigManager.Instance.DesktopWidgetEnabled = true;
            new WidgetWindow().Show();
        }
    }

    private void StartPlayer(string channelName, string toastTitle, string logSource)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                ClosePlayerWindow();

                if (!PlatformInfo.CanUseCctv)
                {
                    PushToast(toastTitle, "CCTV 直播仅支持 Windows（依赖 WebView2）");
                    return;
                }

                var name = CctvChannels.Resolve(channelName);
                var url = CctvWebPlayerWindow.GetCctvLiveUrl(name)
                          ?? "https://tv.cctv.com/live/cctv13/";

                LogService.Log($"{toastTitle}: {name}", logSource);
                await Task.Delay(150);

                _playerWindow = new CctvWebPlayerWindow(url);
                _playerWindow.Closed += (_, _) =>
                {
                    LogService.Log("播放窗口已关闭", "播放");
                    _playerWindow = null;
                };
                _playerWindow.Show();

                PushToast(toastTitle, $"正在播放 {name}");
            }
            catch (Exception ex)
            {
                LogService.Log($"{toastTitle}异常: {ex.Message}", logSource);
            }
        });
    }

    private void OnScheduleStop()
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                LogService.Log("定时结束，关闭播放窗口", "调度");
                ClosePlayerWindow();
                PushToast("定时播放", "播放时段结束，已关闭");
            }
            catch { }
        });
    }

    private void ClosePlayerWindow()
    {
        try
        {
            if (_playerWindow != null)
            {
                _playerWindow.Close();
                _playerWindow = null;
            }
        }
        catch { }
    }

    // ============================================================
    // 托盘
    // ============================================================
    private void SetupTrayIcon()
    {
        try
        {
            var icon = new WindowIcon(AssetLoader.Open(new Uri("avares://SBtools/Assets/app.ico")));

            _trayIcon = new TrayIcon
            {
                Icon = icon,
                ToolTipText = "Vael",
                IsVisible = true
            };

            _trayIcon.Clicked += (_, _) =>
            {
                Dispatcher.UIThread.Post(() => RestoreMainWindow(), DispatcherPriority.Background);
            };

            var menu = new NativeMenu();

            var showItem = new NativeMenuItem("显示主窗口");
            showItem.Click += (_, _) =>
            {
                Dispatcher.UIThread.Post(() => RestoreMainWindow(), DispatcherPriority.Background);
            };
            menu.Add(showItem);
            menu.Add(new NativeMenuItemSeparator());

            var connectItem = new NativeMenuItem("立即连接");
            connectItem.Click += async (_, _) =>
            {
                try
                {
                    var ok = await NetworkMonitor.Instance.ForceAuthenticateAsync();
                    PushToast(ok ? "认证成功" : "认证失败",
                              ok ? "校园网已连接" : "请检查账号密码或网络");
                }
                catch (Exception ex)
                {
                    PushToast("认证异常", ex.Message);
                }
            };
            menu.Add(connectItem);

            var testPlayItem = new NativeMenuItem("测试播放频道");
            testPlayItem.Click += (_, _) => TestPlayLastChannel();
            menu.Add(testPlayItem);

            var stopPlayItem = new NativeMenuItem("关闭播放");
            stopPlayItem.Click += (_, _) => OnScheduleStop();
            menu.Add(stopPlayItem);
            menu.Add(new NativeMenuItemSeparator());

            // ★ 退出菜单项：走安全码验证
            var quitItem = new NativeMenuItem("退出");
            quitItem.Click += (_, _) =>
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    await TryQuitFromTrayAsync();
                }, DispatcherPriority.Background);
            };
            menu.Add(quitItem);

            _trayIcon.Menu = menu;
            LogService.Log("托盘图标已创建", "托盘");
        }
        catch (Exception ex)
        {
            LogService.Log($"托盘创建失败: {ex.Message}", "托盘");
        }
    }

    // ============================================================
    // 通知
    // ============================================================
    public static void PushToast(string title, string message, int durationSeconds = 5)
    {
        if (_instance == null) return;

        void Dispatch()
        {
            try
            {
                var window = _instance;
                if (window == null) return;

                if (window.IsVisible && !window._isHiddenToTray)
                {
                    var host = window.FindControl<ToastHost>("GlobalToastHost");
                    if (host != null)
                    {
                        var toast = new NotificationToast { Title = title, Message = message };
                        toast.Closed += (_, _) => { try { host.Children.Remove(toast); } catch { } };
                        host.Children.Add(toast);
                        _ = SafeShowAsync(toast, durationSeconds);
                        return;
                    }
                }

                ShowStandaloneToast(title, message, durationSeconds);
            }
            catch { }
        }

        if (Dispatcher.UIThread.CheckAccess())
            Dispatch();
        else
            Dispatcher.UIThread.Post(Dispatch);
    }

    // ============================================================
    // 更新安装进度（右下角小弹窗 + 进度条）
    // ============================================================
    private static NotificationToast? _updateProgressToast;

    /// <summary>在右下角显示一个常驻的更新进度弹窗。</summary>
    public static void PushUpdateProgress(string title, string message)
    {
        if (_instance == null) return;

        void Dispatch()
        {
            try
            {
                var window = _instance;
                if (window == null) return;

                var host = window.FindControl<ToastHost>("GlobalToastHost");
                if (host == null) return;

                try { _updateProgressToast?.HideAsync(); } catch { }
                _updateProgressToast = null;

                var toast = new NotificationToast { Title = title, Message = message };
                toast.SetProgress(0);
                toast.Closed += (_, _) => { try { host.Children.Remove(toast); } catch { } };
                host.Children.Add(toast);
                _ = toast.ShowPersistentAsync();
                _updateProgressToast = toast;
            }
            catch { }
        }

        if (Dispatcher.UIThread.CheckAccess())
            Dispatch();
        else
            Dispatcher.UIThread.Post(Dispatch);
    }

    /// <summary>更新进度（0~1），可选更新副标题。</summary>
    public static void SetUpdateProgress(double percent, string? message = null)
    {
        if (_instance == null) return;

        void Dispatch()
        {
            try
            {
                _updateProgressToast?.SetProgress(percent);
                if (message != null && _updateProgressToast != null)
                    _updateProgressToast.Message = message;
            }
            catch { }
        }

        if (Dispatcher.UIThread.CheckAccess())
            Dispatch();
        else
            Dispatcher.UIThread.Post(Dispatch);
    }

    public static void CloseUpdateProgress()
    {
        if (_instance == null) return;

        void Dispatch()
        {
            try
            {
                _updateProgressToast?.HideAsync();
                _updateProgressToast = null;
            }
            catch { }
        }

        if (Dispatcher.UIThread.CheckAccess())
            Dispatch();
        else
            Dispatcher.UIThread.Post(Dispatch);
    }

    /// <summary>带右下角进度弹窗的安装。</summary>
    public static async Task InstallWithProgressAsync()
    {
        PushUpdateProgress("正在安装更新", "准备中…");
        SetUpdateProgress(0.05, "检查更新包…");

        var progress = new Progress<double>(p =>
        {
            SetUpdateProgress(p, p switch
            {
                < 0.30 => "检查更新包…",
                < 0.60 => "生成替换脚本…",
                < 0.90 => "启动更新程序…",
                _ => "即将重启…",
            });
        });

        try
        {
            await UpdateService.Instance.InstallAsync(progress);
        }
        catch
        {
            CloseUpdateProgress();
            throw;
        }
    }

    private static async Task SafeShowAsync(NotificationToast toast, int durationSeconds)
    {
        try { await toast.ShowAsync(durationSeconds); }
        catch (OperationCanceledException) { }
        catch { }
    }

    private static void ShowStandaloneToast(string title, string message, int durationSeconds)
    {
        try
        {
            var toast = new NotificationToast { Title = title, Message = message };

            var win = new Window
            {
                SystemDecorations = SystemDecorations.None,
                Background = Brushes.Transparent,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                CanResize = false,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Content = toast
            };

            win.Opened += (_, _) =>
            {
                var screen = win.Screens.ScreenFromWindow(win) ?? win.Screens.Primary;
                if (screen != null)
                {
                    var area = screen.WorkingArea;
                    int w = 380;
                    win.Position = new PixelPoint(area.Right - w - 24, area.Y + 20);
                }
            };

            toast.Closed += (_, _) => { try { win.Close(); } catch { } };
            win.Show();
            _ = SafeShowAsync(toast, durationSeconds);
        }
        catch { }
    }

    // ============================================================
    // 清理
    // ============================================================
    protected override void OnClosed(EventArgs e)
    {
        try
        {
            ClosePlayerWindow();

            try { NetworkMonitor.Instance.Stop(); } catch { }

            if (_trayIcon != null)
            {
                _trayIcon.IsVisible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }
        catch { }

        _scheduler?.Dispose();
        _scheduler = null;
        _instance = null;

        base.OnClosed(e);
    }
}