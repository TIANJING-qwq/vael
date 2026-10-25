using Avalonia.Controls;
using Avalonia.Interactivity;
using SBtools.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace SBtools.Views;

/// <summary>工具条目：内置命令直接运行，外部工具打开官方下载/在线页。</summary>
public sealed class ToolboxItem
{
    public string Icon { get; init; } = "🔧";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "";
    public bool IsBuiltIn { get; init; }
    /// <summary>内置=要运行的命令；外部=要打开的网址。</summary>
    public string Target { get; init; } = "";
    public string ButtonText => IsBuiltIn ? "打开" : "前往";
    public string TagText => IsBuiltIn ? "内置" : "外部";
}

public partial class ToolboxView : UserControl
{
    private readonly ObservableCollection<ToolboxItem> _filtered = new();

    private static readonly ToolboxItem[] Tools =
    {
        // ===== CPU / 内存 =====
        new() { Icon="🧠", Name="CPU-Z", Category="CPU与内存", Target="https://www.cpuid.com/softwares/cpu-z.html",
                Description="CPU/主板/内存规格检测，装机验货必备" },
        new() { Icon="📊", Name="AIDA64", Category="CPU与内存", Target="https://www.aida64.com/downloads",
                Description="最全的硬件信息+传感器+烤机测试（付费，有试用）" },
        new() { Icon="🔥", Name="Prime95", Category="CPU与内存", Target="https://www.mersenne.org/download/",
                Description="CPU/内存稳定性拷机，超频验证标配" },
        new() { Icon="💾", Name="Windows 内存诊断", Category="CPU与内存", IsBuiltIn=true, Target="mdsched",
                Description="系统自带内存检测，重启后自动跑（约20分钟）" },
        new() { Icon="🧪", Name="MemTest86", Category="CPU与内存", Target="https://www.memtest86.com/",
                Description="U 盘启动的深度内存检测，查内存坏块" },

        // ===== 显卡 =====
        new() { Icon="🎮", Name="GPU-Z", Category="显卡与压力测试", Target="https://www.techpowerup.com/gpuz/",
                Description="显卡规格/传感器/BIOS 检测" },
        new() { Icon="🔥", Name="FurMark", Category="显卡与压力测试", Target="https://geeks3d.com/furmark/",
                Description="甜甜圈烤显卡，测温度上限与稳定性" },
        new() { Icon="🎥", Name="DirectX 诊断工具", Category="显卡与压力测试", IsBuiltIn=true, Target="dxdiag",
                Description="系统自带：显卡/声卡/DirectX 信息与简单测试" },

        // ===== 硬盘 =====
        new() { Icon="💿", Name="CrystalDiskInfo", Category="硬盘与存储", Target="https://crystalmark.info/downloads/",
                Description="硬盘 SMART 健康状态、通电时间、温度" },
        new() { Icon="⚡", Name="CrystalDiskMark", Category="硬盘与存储", Target="https://crystalmark.info/downloads/",
                Description="硬盘/SSD 读写速度测试" },
        new() { Icon="🛠", Name="DiskGenius", Category="硬盘与存储", Target="https://www.diskgenius.cn/download.php",
                Description="分区/数据恢复/坏道检测（图吧常客）" },
        new() { Icon="📈", Name="HD Tune Pro", Category="硬盘与存储", Target="http://www.hdtune.com/download.html",
                Description="硬盘基准测试+错误扫描查坏道" },
        new() { Icon="🗄", Name="磁盘管理", Category="硬盘与存储", IsBuiltIn=true, Target="diskmgmt.msc",
                Description="系统自带分区工具，无需第三方" },
        new() { Icon="♻️", Name="驱动器优化(Trim)", Category="硬盘与存储", IsBuiltIn=true, Target="dfrgui",
                Description="SSD Trim / HDD 碎片整理" },

        // ===== 屏幕与外设 =====
        new() { Icon="🖥", Name="屏幕坏点/漏光测试", Category="屏幕与外设", Target="https://www.eizo.be/monitor-test/",
                Description="在线全屏纯色测试，验坏点/坏线/漏光" },
        new() { Icon="⌨️", Name="键盘测试", Category="屏幕与外设", Target="https://keyboard-test.space/",
                Description="在线检测每个按键/连点是否正常" },
        new() { Icon="🖱", Name="鼠标测试", Category="屏幕与外设", Target="https://onlinemictest.com/mouse-test/",
                Description="在线检测按键/滚轮/双击" },
        new() { Icon="📷", Name="摄像头测试", Category="屏幕与外设", Target="https://onlinemictest.com/camera-test/",
                Description="在线检测摄像头画面" },
        new() { Icon="🎧", Name="麦克风测试", Category="屏幕与外设", Target="https://onlinemictest.com/",
                Description="在线检测麦克风输入" },
        new() { Icon="🔍", Name="屏幕放大镜", Category="屏幕与外设", IsBuiltIn=true, Target="magnify",
                Description="系统自带放大镜（Win + 加号）" },

        // ===== 网络 =====
        new() { Icon="🚀", Name="网速测试 Speedtest", Category="网络工具", Target="https://www.speedtest.cn/",
                Description="国内节点测上下行带宽" },
        new() { Icon="🌐", Name="Fast.com 测速", Category="网络工具", Target="https://fast.com/",
                Description="Netflix 一键测下载速度" },
        new() { Icon="📡", Name="IP 查询", Category="网络工具", Target="https://myip.ipip.net/",
                Description="查看本机出口 IP 与归属地" },
        new() { Icon="🔎", Name="Ping 检测", Category="网络工具", IsBuiltIn=true, Target="ping -t 223.5.5.5",
                Description="持续 ping 检查连通性与延迟抖动" },

        // ===== 系统信息 / 内置工具 =====
        new() { Icon="📋", Name="系统信息 msinfo32", Category="系统信息与内置工具", IsBuiltIn=true, Target="msinfo32",
                Description="系统自带完整软硬件信息报告" },
        new() { Icon="📈", Name="资源监视器", Category="系统信息与内置工具", IsBuiltIn=true, Target="resmon",
                Description="比任务管理器更细的 CPU/磁盘/网络占用" },
        new() { Icon="⚙️", Name="性能监视器", Category="系统信息与内置工具", IsBuiltIn=true, Target="perfmon",
                Description="专业计数器监控，排查性能问题" },
        new() { Icon="🔌", Name="设备管理器", Category="系统信息与内置工具", IsBuiltIn=true, Target="devmgmt.msc",
                Description="驱动/硬件管理，查黄色叹号" },
        new() { Icon="🧹", Name="磁盘清理", Category="系统信息与内置工具", IsBuiltIn=true, Target="cleanmgr",
                Description="系统自带垃圾清理（含系统文件）" },
        new() { Icon="🧰", Name="图吧工具箱官网", Category="系统信息与内置工具", Target="https://www.tbtool.cn/",
                Description="完整版图吧工具箱（离线整合包）" },
    };

    public ToolboxView()
    {
        InitializeComponent();

        CategoryCombo.Items.Add("全部分类");
        foreach (var c in Tools.Select(t => t.Category).Distinct())
            CategoryCombo.Items.Add(c);
        CategoryCombo.SelectedIndex = 0;

        ToolList.ItemsSource = _filtered;
        ApplyFilter();
        PopupAnimator.HookAll(this);
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void CategoryCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var cat = CategoryCombo.SelectedItem as string;
        var kw = (SearchBox.Text ?? "").Trim();

        var list = Tools.Where(t =>
            (string.IsNullOrEmpty(cat) || cat == "全部分类" || t.Category == cat) &&
            (kw.Length == 0 ||
             t.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
             t.Description.Contains(kw, StringComparison.OrdinalIgnoreCase)));

        _filtered.Clear();
        foreach (var t in list) _filtered.Add(t);

        CountText.Text = $"共 {_filtered.Count} 个";
        EmptyHint.IsVisible = _filtered.Count == 0;
    }

    private void ToolButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ToolboxItem t }) return;
        try
        {
            if (t.IsBuiltIn)
                Process.Start(new ProcessStartInfo("cmd.exe", "/c start \"\" " + t.Target) { UseShellExecute = true, CreateNoWindow = true });
            else
                Process.Start(new ProcessStartInfo(t.Target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            LogService.Log($"工具启动失败 {t.Name}: {ex.Message}", "工具箱");
        }
    }
}
