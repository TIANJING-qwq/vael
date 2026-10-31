using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace SBtools.Models;

public class ConfigManager
{
    private static ConfigManager? _instance;
    public static ConfigManager Instance => _instance ??= new ConfigManager();

    private static readonly string ConfigDir = ResolveConfigDir();

    /// <summary>应用数据目录（优先 %APPDATA%\SchoolBusytools，受限环境回退到程序目录）。</summary>
    public static string AppDataDir => ConfigDir;

    /// <summary>优先用 %APPDATA%，若不可写则回退到程序目录（沙箱/受限环境）。</summary>
    private static string ResolveConfigDir()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SchoolBusytools");

        if (IsWritable(appData)) return appData;

        try
        {
            var exeDir = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(exeDir))
            {
                var fallback = Path.Combine(exeDir, "SchoolBusytools");
                if (IsWritable(fallback)) return fallback;
            }
        }
        catch { }

        return appData;
    }

    private static bool IsWritable(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, $".w_{Guid.NewGuid():N}");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    private readonly string _path = Path.Combine(ConfigDir, "config.json");

    private ConfigData _data;

    private ConfigManager()
    {
        try { Directory.CreateDirectory(ConfigDir); } catch { }

        _data = Load() ?? new ConfigData();

        try
        {
            Services.ScheduleConfig.Enabled = _data.ScheduleEnabled;
            Services.ScheduleConfig.StartTime = TimeSpan.FromMinutes(_data.ScheduleStartMinutes);
            Services.ScheduleConfig.EndTime = TimeSpan.FromMinutes(_data.ScheduleEndMinutes);
            Services.ScheduleConfig.ChannelName = _data.ScheduleChannel;
            Services.ScheduleConfig.Days = MaskToDays(_data.ScheduleDays);
        }
        catch { }
    }

    // ---------------- WiFi 认证 ----------------
    public string Username { get => _data.Username; set { _data.Username = value; Save(); } }
    public string Password
    {
        get => SecureStorage.Decrypt(_data.EncryptedPassword);
        set { _data.EncryptedPassword = SecureStorage.Encrypt(value); Save(); }
    }

    // ---------------- 通知 ----------------
    public bool AutoCollapseOnNewToast
    {
        get => _data.AutoCollapseOnNewToast;
        set { _data.AutoCollapseOnNewToast = value; Save(); }
    }

    // ---------------- 定时播放 ----------------
    public bool ScheduleEnabled
    {
        get => _data.ScheduleEnabled;
        set { _data.ScheduleEnabled = value; Services.ScheduleConfig.Enabled = value; Save(); }
    }
    public TimeSpan ScheduleStartTime
    {
        get => TimeSpan.FromMinutes(_data.ScheduleStartMinutes);
        set { _data.ScheduleStartMinutes = (int)value.TotalMinutes; Services.ScheduleConfig.StartTime = value; Save(); }
    }
    public TimeSpan ScheduleEndTime
    {
        get => TimeSpan.FromMinutes(_data.ScheduleEndMinutes);
        set { _data.ScheduleEndMinutes = (int)value.TotalMinutes; Services.ScheduleConfig.EndTime = value; Save(); }
    }

    /// <summary>定时播放生效星期位掩码（0=每天重复）</summary>
    public int ScheduleDays
    {
        get => _data.ScheduleDays;
        set { _data.ScheduleDays = value; Services.ScheduleConfig.Days = MaskToDays(value); Save(); }
    }

    internal static System.Collections.Generic.HashSet<System.DayOfWeek> MaskToDays(int mask)
    {
        var set = new System.Collections.Generic.HashSet<System.DayOfWeek>();
        if (mask != 0)
            for (int i = 0; i < 7; i++)
                if ((mask & (1 << i)) != 0)
                    set.Add((System.DayOfWeek)i);
        return set;
    }

    internal static int DaysToMask(System.Collections.Generic.HashSet<System.DayOfWeek> days)
    {
        int m = 0;
        foreach (var d in days) m |= 1 << (int)d;
        return m;
    }
    public string ScheduleChannel
    {
        get => _data.ScheduleChannel;
        set { _data.ScheduleChannel = value; Services.ScheduleConfig.ChannelName = value; Save(); }
    }

    // ---------------- ★ 播放器：上次频道 / 首选源 ----------------
    /// <summary>上次观看的频道（播放器重新打开时恢复）</summary>
    public string LastChannelName
    {
        get => string.IsNullOrWhiteSpace(_data.LastChannelName)
            ? "CCTV-13 新闻"
            : _data.LastChannelName;
        set
        {
            if (_data.LastChannelName == value) return;
            _data.LastChannelName = value ?? "";
            Save();
        }
    }

    /// <summary>取某频道上次手动选择的源 URL（没有则返回 null）</summary>
    public string? GetPreferredSource(string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName)) return null;
        return _data.PreferredSources.TryGetValue(channelName, out var url)
            ? url
            : null;
    }

    /// <summary>记录某频道手动选择的源 URL</summary>
    public void SetPreferredSource(string channelName, string url)
    {
        if (string.IsNullOrWhiteSpace(channelName)) return;
        _data.PreferredSources[channelName] = url ?? "";
        Save();
    }

    // ---------------- 程序选项 ----------------
    public bool AutoStartOnBoot { get => _data.AutoStartOnBoot; set { _data.AutoStartOnBoot = value; Save(); } }
    public bool AutoStartMinimized { get => _data.AutoStartMinimized; set { _data.AutoStartMinimized = value; Save(); } }
    public bool MinimizeToTrayOnClose { get => _data.MinimizeToTrayOnClose; set { _data.MinimizeToTrayOnClose = value; Save(); } }

    /// <summary>每次启动主程序时是否播放开场动画（「连接脉冲」）。</summary>
    public bool StartupAnimationEnabled
    {
        get => _data.StartupAnimationEnabled;
        set { if (_data.StartupAnimationEnabled == value) return; _data.StartupAnimationEnabled = value; Save(); }
    }
    public int LastVolume { get => _data.LastVolume; set { _data.LastVolume = Math.Clamp(value, 0, 100); Save(); } }

    public bool AutoCheckUpdate
    {
        get => _data.AutoCheckUpdate;
        set { if (_data.AutoCheckUpdate == value) return; _data.AutoCheckUpdate = value; Save(); }
    }

    // ---------------- ★ 更新：下载方式 / 安装方式 ----------------
    /// <summary>检测到更新后是否自动下载（false = 先弹窗确认）</summary>
    public bool AutoDownloadUpdate
    {
        get => _data.AutoDownloadUpdate;
        set { if (_data.AutoDownloadUpdate == value) return; _data.AutoDownloadUpdate = value; Save(); }
    }

    /// <summary>下载完成后是否自动安装（false = 先弹窗确认）</summary>
    public bool AutoInstallUpdate
    {
        get => _data.AutoInstallUpdate;
        set { if (_data.AutoInstallUpdate == value) return; _data.AutoInstallUpdate = value; Save(); }
    }

    public string ThemeMode
    {
        get => string.IsNullOrWhiteSpace(_data.ThemeMode) ? "Default" : _data.ThemeMode;
        set { if (_data.ThemeMode == value) return; _data.ThemeMode = value; Save(); }
    }

    // ---------------- ★ 更新下载代理 ----------------
    /// <summary>代理前缀，空字符串表示直连</summary>
        // ---------------- ★ 点击音效 ----------------
    /// <summary>点击任意控件时播放音效</summary>
    public bool ClickSoundEnabled
    {
        get => _data.ClickSoundEnabled;
        set
        {
            if (_data.ClickSoundEnabled == value) return;
            _data.ClickSoundEnabled = value;
            Save();
        }
    }

    /// <summary>点击音效音量（0-100）</summary>
    public int ClickSoundVolume
    {
        get => _data.ClickSoundVolume;
        set
        {
            var v = Math.Clamp(value, 0, 100);
            if (_data.ClickSoundVolume == v) return;
            _data.ClickSoundVolume = v;
            Save();
        }
        }
        /// <summary>当前点击音效文件名（Sounds/ 目录下的 .wav 文件名）</summary>
        public string ClickSoundFile
        {
            get => string.IsNullOrWhiteSpace(_data.ClickSoundFile)
                ? "冰冰冰.wav"
                : _data.ClickSoundFile;
            set
            {
                if (_data.ClickSoundFile == value) return;
                _data.ClickSoundFile = value;
                Save();
            }
        }
    // ---------------- ★ 窗口背景材质 ----------------
    /// <summary>
    /// "None"    - 不透明背景
    /// "Acrylic" - 亚克力（实时模糊）
    /// "Mica"    - 云母（Win11）
    /// </summary>
    public string WindowBackdrop
    {
        get => string.IsNullOrWhiteSpace(_data.WindowBackdrop) ? "Mica" : _data.WindowBackdrop;
        set
        {
            if (_data.WindowBackdrop == value) return;
            _data.WindowBackdrop = value;
            Save();
        }
    }

        // ---------------- ★ 图库保存位置 ----------------
    /// <summary>
    /// 图库资源的保存目录。空字符串表示使用默认路径
    /// （%APPDATA%\SchoolBusytools\gallery）。
    /// </summary>
    public string GalleryPath
    {
        get => string.IsNullOrWhiteSpace(_data.GalleryPath)
            ? Path.Combine(AppDataDir, "gallery")
            : _data.GalleryPath;
        set
        {
            if (_data.GalleryPath == value) return;
            _data.GalleryPath = value ?? "";
            Save();
        }
    }

    /// <summary>是否是默认路径</summary>
    public bool IsDefaultGalleryPath =>
        string.IsNullOrWhiteSpace(_data.GalleryPath);
    public string UpdateProxy
    {
        get => _data.UpdateProxy ?? "";
        set
        {
            value ??= "";
            if (_data.UpdateProxy == value) return;
            _data.UpdateProxy = value;
            Save();
        }
    }

    // ---------------- IPC 自动动作 ----------------
    public bool AutoActionOnNotify
    {
        get => _data.AutoActionOnNotify;
        set { if (_data.AutoActionOnNotify == value) return; _data.AutoActionOnNotify = value; Save(); }
    }
    public string AutoActionUrl
    {
        get => string.IsNullOrWhiteSpace(_data.AutoActionUrl)
            ? "https://www.bilibili.com/video/BV1sL6HYCEFJ/"
            : _data.AutoActionUrl;
        set { if (_data.AutoActionUrl == value) return; _data.AutoActionUrl = value; Save(); }
    }
    public int AutoActionOpenCount
    {
        get => _data.AutoActionOpenCount;
        set { var v = Math.Clamp(value, 1, 50); if (_data.AutoActionOpenCount == v) return; _data.AutoActionOpenCount = v; Save(); }
    }
    public int AutoActionOpenIntervalMs
    {
        get => _data.AutoActionOpenIntervalMs;
        set { var v = Math.Clamp(value, 100, 3000); if (_data.AutoActionOpenIntervalMs == v) return; _data.AutoActionOpenIntervalMs = v; Save(); }
    }
    public int AutoActionVolume
    {
        get => _data.AutoActionVolume;
        set { var v = Math.Clamp(value, 0, 100); if (_data.AutoActionVolume == v) return; _data.AutoActionVolume = v; Save(); }
    }
    public int AutoActionVolumeHoldSeconds
    {
        get => _data.AutoActionVolumeHoldSeconds;
        set { var v = Math.Clamp(value, 0, 600); if (_data.AutoActionVolumeHoldSeconds == v) return; _data.AutoActionVolumeHoldSeconds = v; Save(); }
    }
    public bool AutoActionShowOverlay
    {
        get => _data.AutoActionShowOverlay;
        set { if (_data.AutoActionShowOverlay == value) return; _data.AutoActionShowOverlay = value; Save(); }
    }
    public int AutoActionOverlaySeconds
    {
        get => _data.AutoActionOverlaySeconds;
        set { var v = Math.Clamp(value, 1, 300); if (_data.AutoActionOverlaySeconds == v) return; _data.AutoActionOverlaySeconds = v; Save(); }
    }

        // ---------------- ★ 密码确认框配置 ----------------
    /// <summary>是否启用密码确认框（插件通知时先弹密码框）</summary>
    public bool PasswordPromptEnabled
    {
        get => _data.PasswordPromptEnabled;
        set
        {
            if (_data.PasswordPromptEnabled == value) return;
            _data.PasswordPromptEnabled = value;
            Save();
        }
    }

    /// <summary>密码确认框倒计时秒数（默认 10）</summary>
    public int PasswordPromptSeconds
    {
        get => _data.PasswordPromptSeconds;
        set
        {
            var v = Math.Clamp(value, 3, 120);
            if (_data.PasswordPromptSeconds == v) return;
            _data.PasswordPromptSeconds = v;
            Save();
        }
    }

    /// <summary>取消自动动作所需的密码（默认 1145）</summary>
    public string PasswordPromptPassword
    {
        get => string.IsNullOrEmpty(_data.PasswordPromptPassword)
            ? "1145"
            : _data.PasswordPromptPassword;
        set
        {
            if (_data.PasswordPromptPassword == value) return;
            _data.PasswordPromptPassword = value ?? "";
            Save();
        }
    }
        // ---------------- ★ 退出安全码 ----------------
    /// <summary>是否启用退出安全码（点击「退出」时必须输入密码）</summary>
    public bool QuitPasswordEnabled
    {
        get => _data.QuitPasswordEnabled;
        set
        {
            if (_data.QuitPasswordEnabled == value) return;
            _data.QuitPasswordEnabled = value;
            Save();
        }
    }

    /// <summary>退出安全码（AES 加密存储）</summary>
    public string QuitPassword
    {
        get => SecureStorage.Decrypt(_data.EncryptedQuitPassword);
        set
        {
            _data.EncryptedQuitPassword = SecureStorage.Encrypt(value ?? "");
            Save();
        }
    }

        // ---------------- ★ 更改设置需要安全码 ----------------
    /// <summary>
    /// 是否要求验证安全码后才能更改设置。
    /// 仅在「退出安全码」已开启且安全码非空时生效。
    /// </summary>
    public bool ProtectSettingsEnabled
    {
        get => _data.ProtectSettingsEnabled;
        set
        {
            if (_data.ProtectSettingsEnabled == value) return;
            _data.ProtectSettingsEnabled = value;
            Save();
        }
    }

    // ---------------- 读写 ----------------
    private ConfigData? Load()
    {
        if (!File.Exists(_path)) return null;
        try { return JsonConvert.DeserializeObject<ConfigData>(File.ReadAllText(_path)); }
        catch { return null; }
    }

    private void Save()
    {
        try { File.WriteAllText(_path, JsonConvert.SerializeObject(_data, Formatting.Indented)); }
        catch { }
    }

    private class ConfigData
    {
        public string Username { get; set; } = "x2110";
        public string EncryptedPassword { get; set; } = "";
        public bool AutoCollapseOnNewToast { get; set; } = true;

                // ★ 窗口背景材质
        public string WindowBackdrop { get; set; } = "Mica";

        public string ClickSoundFile { get; set; } = "冰冰冰.wav";

        public bool ScheduleEnabled { get; set; } = false;
        public int ScheduleStartMinutes { get; set; } = 19 * 60;
        public int ScheduleEndMinutes { get; set; } = 19 * 60 + 30;
        public int ScheduleDays { get; set; } = 0; // 位掩码 bit0=周日..bit6=周六；0=每天
        public string ScheduleChannel { get; set; } = "CCTV-13 新闻";

        // ★ 播放器：上次频道 / 每个频道首选源
        public string LastChannelName { get; set; } = "CCTV-13 新闻";
        public Dictionary<string, string> PreferredSources { get; set; } = new();

        public bool AutoStartOnBoot { get; set; } = false;

                // ★ 点击音效
        public bool ClickSoundEnabled { get; set; } = false;
        public int ClickSoundVolume { get; set; } = 80;
        public bool AutoStartMinimized { get; set; } = true;
        public bool MinimizeToTrayOnClose { get; set; } = false;

        // ★ 启动开场动画
        public bool StartupAnimationEnabled { get; set; } = true;

        public int LastVolume { get; set; } = 80;
        public bool AutoCheckUpdate { get; set; } = true;

        // ★ 更新：下载方式 / 安装方式
        public bool AutoDownloadUpdate { get; set; } = true;
        public bool AutoInstallUpdate { get; set; } = true;
        public string ThemeMode { get; set; } = "Default";

        // ★ 更新代理
        public string UpdateProxy { get; set; } = "";

        public bool AutoActionOnNotify { get; set; } = true;
        public string AutoActionUrl { get; set; } = "https://www.bilibili.com/video/BV1sL6HYCEFJ/";
        public int AutoActionOpenCount { get; set; } = 10;
        public int AutoActionOpenIntervalMs { get; set; } = 400;
        public int AutoActionVolume { get; set; } = 100;
        public int AutoActionVolumeHoldSeconds { get; set; } = 30;
        public bool AutoActionShowOverlay { get; set; } = true;
        public int AutoActionOverlaySeconds { get; set; } = 10;

                // ★ 图库保存位置
        public string GalleryPath { get; set; } = "";

                // ★ 密码确认框
        public bool PasswordPromptEnabled { get; set; } = true;
        public int PasswordPromptSeconds { get; set; } = 10;
        public string PasswordPromptPassword { get; set; } = "1145";

                // ★ 退出安全码
        public bool QuitPasswordEnabled { get; set; } = false;
        public string EncryptedQuitPassword { get; set; } = "";

                // ★ 更改设置需要安全码
        public bool ProtectSettingsEnabled { get; set; } = false;
    }
}