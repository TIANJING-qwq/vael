using Avalonia;
using LibVLCSharp.Shared;
using SBtools.Models;
using SBtools.Plugins;
using SBtools.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools;

class Program
{
    private const string MutexName = "Vael_SingleInstance_Mutex";
    private static Mutex? _mutex;

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogException("AppDomain", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogException("Task", e.Exception);
            e.SetObserved();
        };

        LogInfo($"=== 程序启动，参数: {string.Join(" ", args)} ===");
        LogInfo($"版本: {typeof(Program).Assembly.GetName().Version}");

        bool isAnotherInstanceActive = CheckExistingInstance();

        if (isAnotherInstanceActive)
        {
            LogInfo("已有活跃实例，尝试唤到前台后退出");
            BringExistingWindowToFront();
            return;
        }

        LogInfo("无活跃实例，启动新进程");

        try
        {
            Core.Initialize();
            LogInfo("VLC 初始化成功（点击音效）");
        }
        catch (Exception ex)
        {
            LogException("VLC", ex);
        }

        try
        {
            UpdateService.Instance.Initialize();
            LogInfo("更新服务已初始化（检查推迟到主窗口显示后）");
        }
        catch (Exception ex)
        {
            LogException("UpdateService", ex);
        }

        // ★ 加载插件
        try
        {
            PluginManager.Instance.LoadAll();
        }
        catch (Exception ex)
        {
            LogException("PluginManager", ex);
        }

        try
        {
            LogInfo("进入 Avalonia 主循环");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            LogInfo("Avalonia 主循环已退出");
        }
        catch (Exception ex)
        {
            LogException("Avalonia", ex);
            throw;
        }
        finally
        {
            try { PluginManager.Instance.UnloadAll(); } catch { }
            try { UpdateService.Instance.StopAutoCheck(); } catch { }
            try { _mutex?.ReleaseMutex(); } catch { }
            _mutex?.Dispose();
        }
    }

    private static bool CheckExistingInstance()
    {
        try
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);

            if (createdNew)
            {
                LogInfo("Mutex 创建成功，无其他实例");
                return false;
            }

            var current = Process.GetCurrentProcess();
            var others = Process.GetProcessesByName(current.ProcessName)
                .Where(p => p.Id != current.Id)
                .ToList();

            if (others.Count == 0)
            {
                LogInfo("Mutex 存在但无同名进程，视为残留，启动新进程");
                _mutex?.Dispose();
                _mutex = new Mutex(true, MutexName, out createdNew);
                return false;
            }

            foreach (var p in others)
            {
                try
                {
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        LogInfo($"发现活跃实例 PID={p.Id}，有窗口");
                        return true;
                    }
                }
                catch { }
            }

            LogInfo($"发现 {others.Count} 个同名进程但均无窗口，清理旧进程");

            foreach (var p in others)
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(2000);
                    LogInfo($"已终止残留进程 PID={p.Id}");
                }
                catch { }
            }

            Thread.Sleep(500);

            _mutex?.Dispose();
            _mutex = new Mutex(true, MutexName, out createdNew);
            LogInfo($"重新获取 Mutex: {createdNew}");
            return false;
        }
        catch (Exception ex)
        {
            LogException("MutexCheck", ex);
            return false;
        }
    }

    private static void LogInfo(string message)
    {
        try
        {
            var dir = Path.Combine(ConfigManager.AppDataDir, "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, $"app_{DateTime.Now:yyyyMMdd}.log"),
                $"{DateTime.Now:HH:mm:ss} [启动] {message}{Environment.NewLine}");
            Console.WriteLine(message);
        }
        catch { }
    }

    private static void LogException(string source, Exception? ex)
    {
        try
        {
            var dir = Path.Combine(ConfigManager.AppDataDir, "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, $"error_{DateTime.Now:yyyyMMdd}.log"),
                $"[{DateTime.Now:HH:mm:ss}] [{source}] {ex}\n\n");
        }
        catch { }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void BringExistingWindowToFront()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var current = Process.GetCurrentProcess();
                foreach (var p in Process.GetProcessesByName(current.ProcessName))
                {
                    if (p.Id == current.Id) continue;
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(p.MainWindowHandle, SW_RESTORE);
                        SetForegroundWindow(p.MainWindowHandle);
                        break;
                    }
                }
            }
        }
        catch { }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int SW_RESTORE = 9;
}