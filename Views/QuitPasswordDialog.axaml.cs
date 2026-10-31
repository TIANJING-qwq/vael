using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Threading.Tasks;

namespace SBtools.Views;

/// <summary>
/// 安全码验证对话框。默认用于「退出」场景，也可自定义文案后复用于其他需要验证安全码的操作。
/// </summary>
public partial class QuitPasswordDialog : Window
{
    private bool _allowClose;
    private bool _closingAnimated;

    private readonly string _errorMessage;
    private readonly string _logTag;

    /// <summary>true = 通过验证</summary>
    public bool Verified { get; private set; }

    /// <summary>退出场景（默认文案）</summary>
    public QuitPasswordDialog()
        : this("退出Vael", "请输入退出安全码：", "退出",
               "安全码错误，无法退出", "退出安全码（托盘退出）")
    {
    }

    /// <summary>通用安全码验证对话框</summary>
    /// <param name="title">标题</param>
    /// <param name="prompt">说明文字</param>
    /// <param name="confirmText">确认按钮文字</param>
    /// <param name="errorMessage">验证失败时的提示</param>
    /// <param name="logTag">日志标签</param>
    public QuitPasswordDialog(string title, string prompt, string confirmText,
                              string errorMessage, string logTag)
    {
        _errorMessage = string.IsNullOrEmpty(errorMessage) ? "安全码错误" : errorMessage;
        InitializeComponent();
        this.DismissOnDeactivate(false); // 点弹窗外空白处自动取消
        _logTag = string.IsNullOrEmpty(logTag) ? "安全码" : logTag;

        InitializeComponent();

        try
        {
            Title = title;
            TitleText.Text = title;
            PromptText.Text = prompt;
            ConfirmButton.Content = confirmText;
        }
        catch { }

        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        Opened += async (_, _) =>
        {
            await Task.Delay(30);
            RootCard.Opacity = 1;
            RootCard.RenderTransform = TransformOperations.Parse("scale(1.0)");
            try { PasswordBox.Focus(); } catch { }
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
            Close();
        };
    }

    private void PasswordBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConfirmButton_Click(null, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private async void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Verified = false;
        await CloseWithAnimationAsync();
    }

    private async void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        var input = PasswordBox.Text ?? "";

        string correct;
        try { correct = ConfigManager.Instance.QuitPassword; }
        catch { correct = ""; }

        if (input != correct)
        {
            ErrorText.Text = _errorMessage;
            ErrorText.IsVisible = true;
            PasswordBox.Text = "";
            try { PasswordBox.Focus(); } catch { }

            LogService.Log($"{_logTag}验证失败", "窗口");

            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try { ErrorText.IsVisible = false; } catch { }
                });
            });

            return;
        }

        LogService.Log($"{_logTag}验证通过", "窗口");
        Verified = true;
        await CloseWithAnimationAsync();
    }

    private async Task CloseWithAnimationAsync()
    {
        if (_allowClose) return;
        if (_closingAnimated) return;
        _closingAnimated = true;

        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        await Task.Delay(220);
        _allowClose = true;
        Close();
    }
}