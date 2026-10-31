using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Threading.Tasks;

namespace SBtools.Views;

public enum CloseAction
{
    Cancel,
    MinimizeToTray,
    Quit
}

public partial class CloseConfirmDialog : Window
{
    private bool _allowClose;
    private bool _closingAnimated;
    private bool _quitPasswordRequired;

    public CloseConfirmDialog()
    {
        InitializeComponent();
        this.DismissOnDeactivate(CloseAction.Cancel); // 点弹窗外空白处自动取消（返回值必须是 CloseAction）

        // 判断是否需要安全码
        try
        {
            _quitPasswordRequired = ConfigManager.Instance.QuitPasswordEnabled
                && !string.IsNullOrEmpty(ConfigManager.Instance.QuitPassword);
        }
        catch
        {
            _quitPasswordRequired = false;
        }

        if (_quitPasswordRequired)
        {
            QuitPasswordPanel.IsVisible = true;
        }

        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        Opened += async (_, _) =>
        {
            await Task.Delay(30);
            RootCard.Opacity = 1;
            RootCard.RenderTransform = TransformOperations.Parse("scale(1.0)");

            if (_quitPasswordRequired)
            {
                try { QuitPasswordBox.Focus(); } catch { }
            }
        };

        Closing += async (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;

            if (_closingAnimated) return;
            _closingAnimated = true;

            RootCard.Opacity = 0;
            RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

            await Task.Delay(220);
            _allowClose = true;
            Close(CloseAction.Cancel);
        };
    }

    // ============================================================
    // 密码框回车 = 直接尝试退出
    // ============================================================
    private void QuitPasswordBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            QuitButton_Click(null, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    // ============================================================
    // 取消
    // ============================================================
    private async void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        LogService.Log("对话框：取消", "窗口");
        await CloseWithAnimationAsync(CloseAction.Cancel);
    }

    // ============================================================
    // 最小化到托盘
    // ============================================================
    private async void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        bool dontAsk = DontAskAgainCheckbox.IsChecked == true;
        LogService.Log($"对话框：最小化到托盘（不再询问={dontAsk}）", "窗口");

        if (dontAsk)
        {
            try
            {
                ConfigManager.Instance.MinimizeToTrayOnClose = true;
            }
            catch (Exception ex)
            {
                LogService.Log($"保存配置失败: {ex.Message}", "窗口");
            }
        }

        await CloseWithAnimationAsync(CloseAction.MinimizeToTray);
    }

    // ============================================================
    // 退出（需验证安全码）
    // ============================================================
    private async void QuitButton_Click(object? sender, RoutedEventArgs e)
    {
        // ★ 需要安全码时先验证
        if (_quitPasswordRequired)
        {
            var input = QuitPasswordBox.Text ?? "";

            string correct;
            try { correct = ConfigManager.Instance.QuitPassword; }
            catch { correct = ""; }

            if (input != correct)
            {
                // 密码错误 → 提示并清空
                QuitErrorText.Text = "安全码错误，无法退出";
                QuitErrorText.IsVisible = true;
                QuitPasswordBox.Text = "";
                try { QuitPasswordBox.Focus(); } catch { }

                LogService.Log("对话框：退出安全码错误", "窗口");

                // 3 秒后自动隐藏错误提示
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        try { QuitErrorText.IsVisible = false; } catch { }
                    });
                });

                return;
            }

            LogService.Log("对话框：退出安全码正确", "窗口");
        }

        bool dontAsk = DontAskAgainCheckbox.IsChecked == true;
        LogService.Log($"对话框：退出（不再询问={dontAsk}）", "窗口");

        await CloseWithAnimationAsync(CloseAction.Quit);
    }

    // ============================================================
    // 带退出动画的关闭
    // ============================================================
    private async Task CloseWithAnimationAsync(CloseAction result)
    {
        if (_allowClose) return;
        if (_closingAnimated) return;
        _closingAnimated = true;

        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        await Task.Delay(220);
        _allowClose = true;
        Close(result);
    }
}