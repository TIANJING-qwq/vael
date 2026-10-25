using SBtools.Models;
using System.Collections.Generic;
using System.Linq;

namespace SBtools.Services;

/// <summary>Windows 常见问题目录：100 个问题 × 12 大分类，每条含人工方法与自动动作。</summary>
public static class WindowsProblemCatalog
{
    private static WindowsProblem P(int id, string cat, string title, string symptom,
        string manual, params ProblemAction[] auto) =>
        new(id, cat, title, symptom, manual, auto);

    private static ProblemAction A(string label, ProblemActionKind kind, string? arg = null) =>
        new(label, kind, arg);

    public static readonly IReadOnlyList<WindowsProblem> All = new WindowsProblem[]
    {
        // ==================== 一、桌面与资源管理器（12）====================
        P(1, "桌面与资源管理器", "资源管理器无限重启（桌面任务栏反复闪断）",
            "桌面、任务栏每隔几秒黑一下又恢复，窗口标题栏闪烁",
            "控制面板 → 时钟和区域 → 日期和时间 → 点一下「更改时区」→ 不改动直接「确定」→ 再点「确定」即可修复。",
            A("打开控制面板", ProblemActionKind.OpenControlPanel),
            A("打开日期和时间", ProblemActionKind.OpenDateAndTime),
            A("重启资源管理器", ProblemActionKind.RestartExplorer)),

        P(2, "桌面与资源管理器", "桌面图标全部消失",
            "桌面空白，只剩壁纸，但任务栏正常",
            "右键桌面 → 查看 → 勾选「显示桌面图标」；若右键也无效则重启资源管理器。",
            A("重启资源管理器", ProblemActionKind.RestartExplorer)),

        P(3, "桌面与资源管理器", "桌面/任务栏图标变白或显示异常",
            "快捷方式图标变成白色空白或显示成别的程序图标",
            "关闭资源管理器 → 删除 %LocalAppData%\\IconCache.db → 重新打开资源管理器即可重建图标缓存。",
            A("重建图标缓存", ProblemActionKind.RebuildIconCache)),

        P(4, "桌面与资源管理器", "图片/视频缩略图不显示或错乱",
            "文件夹里只显示默认图标不显示缩略图，或缩略图和文件对不上",
            "关闭资源管理器 → 删除 %LocalAppData%\\Microsoft\\Windows\\Explorer 下的 thumbcache_*.db → 重启资源管理器。",
            A("清理缩略图缓存", ProblemActionKind.ThumbnailCache)),

        P(5, "桌面与资源管理器", "文件资源管理器打不开或闪退",
            "双击「此电脑」无反应，或打开后立刻关闭",
            "Ctrl+Shift+Esc 打开任务管理器 → 结束 Windows 资源管理器 → 文件→运行新任务→输入 explorer 回车。",
            A("重启资源管理器", ProblemActionKind.RestartExplorer)),

        P(6, "桌面与资源管理器", "复制粘贴（剪贴板）失效",
            "Ctrl+C/V 没反应，跨程序无法复制粘贴",
            "任务管理器结束 explorer.exe 和 rdpclip.exe（如有）→ 运行新任务 explorer 回车；仍不行则重启电脑。",
            A("重启资源管理器", ProblemActionKind.RestartExplorer)),

        P(7, "桌面与资源管理器", "右键菜单卡顿、转圈很久才弹出",
            "右键文件/桌面要等好几秒菜单才出现",
            "用 ShellExView 之类的工具禁用第三方右键扩展；或先重启资源管理器临时缓解，再排查最近装的软件。",
            A("重启资源管理器（缓解）", ProblemActionKind.RestartExplorer)),

        P(8, "桌面与资源管理器", "回收站损坏、打不开或清空异常",
            "双击回收站报错「回收站已损坏」，或删除文件不进回收站",
            "管理员命令行执行 rd /s /q C:\\$Recycle.Bin 删除损坏的回收站元数据，重启后系统自动重建（各盘符同理）。",
            A("重建回收站", ProblemActionKind.RunCommand, "rd /s /q C:\\$Recycle.Bin")),

        P(9, "桌面与资源管理器", "文件夹搜索没反应、搜不出结果",
            "资源管理器右上角搜索框输入后一直转圈或永远为空",
            "重启 Windows Search 服务：管理员命令行 net stop wsearch & net start wsearch；仍不行则重建索引（控制面板→索引选项→高级→重建）。",
            A("重启搜索服务", ProblemActionKind.RunCommand, "net stop wsearch & net start wsearch")),

        P(10, "桌面与资源管理器", "桌面壁纸改不了或变黑",
            "设置里换壁纸点了没反应，或桌面显示纯黑",
            "删除 %AppData%\\Microsoft\\Windows\\Themes 下的 TranscodedWallpaper 文件后重新设置壁纸。",
            A("打开壁纸缓存目录", ProblemActionKind.OpenFolder, "%AppData%\\Microsoft\\Windows\\Themes"),
            A("打开个性化设置", ProblemActionKind.OpenMsSettings, "personalization-background")),

        P(11, "桌面与资源管理器", "文件拖拽失效（拖不动文件）",
            "鼠标拖放文件没反应，无法移动/复制",
            "先重启资源管理器；仍不行检查是否有软件（如部分触摸板驱动、远程工具）劫持了拖拽，退出后再试。",
            A("重启资源管理器", ProblemActionKind.RestartExplorer)),

        P(12, "桌面与资源管理器", "资源管理器内存占用过高、持续上涨",
            "任务管理器里 explorer.exe 占几百 MB 甚至 1GB 以上且不下降",
            "关闭预览窗格和详细信息窗格（Alt+P / Alt+Shift+P）；减少缩略图；实在不行重启资源管理器释放内存。",
            A("重启资源管理器", ProblemActionKind.RestartExplorer)),

        // ==================== 二、任务栏与开始菜单（8）====================
        P(13, "任务栏与开始菜单", "任务栏无响应、点了没反应",
            "任务栏图标点击无效，鼠标放上去转圈",
            "Ctrl+Shift+Esc → 结束「Windows 资源管理器」→ 会自动重启；若不自动重启则手动运行新任务 explorer。",
            A("重启资源管理器", ProblemActionKind.RestartExplorer)),

        P(14, "任务栏与开始菜单", "开始菜单打不开",
            "按 Win 键或点开始按钮无反应",
            "重启资源管理器；Win11 可再结束 ShellExperienceHost 与 StartMenuExperienceHost（会自动重启）；仍不行运行 sfc /scannow。",
            A("重启资源管理器", ProblemActionKind.RestartExplorer),
            A("重启开始菜单宿主", ProblemActionKind.RunCommand, "taskkill /f /im ShellExperienceHost.exe & taskkill /f /im StartMenuExperienceHost.exe")),

        P(15, "任务栏与开始菜单", "任务栏图标错乱、重叠或残影",
            "图标显示错位、重叠，或有残留的旧图标",
            "重启资源管理器即可刷新任务栏图标布局。",
            A("重启资源管理器", ProblemActionKind.RestartExplorer)),

        P(16, "任务栏与开始菜单", "系统托盘图标（音量/网络）消失",
            "右下角音量、网络、输入法图标不见了",
            "设置 → 个性化 → 任务栏 → 打开系统图标；或先重启资源管理器刷新托盘。",
            A("重启资源管理器", ProblemActionKind.RestartExplorer),
            A("打开任务栏设置", ProblemActionKind.OpenMsSettings, "personalization-taskbar")),

        P(17, "任务栏与开始菜单", "任务栏自动隐藏失效",
            "设置了自动隐藏但任务栏一直显示，或反过来藏起来出不来",
            "设置 → 个性化 → 任务栏 → 关闭「自动隐藏任务栏」再重新打开；把鼠标移到屏幕底部即可唤出。",
            A("打开任务栏设置", ProblemActionKind.OpenMsSettings, "personalization-taskbar")),

        P(18, "任务栏与开始菜单", "通知中心 / 日历面板打不开",
            "点右下角时间或通知图标无反应",
            "重启资源管理器；Win11 再结束 ShellExperienceHost.exe（自动重启）；检查设置里通知是否被策略禁用。",
            A("重启资源管理器", ProblemActionKind.RestartExplorer)),

        P(19, "任务栏与开始菜单", "任务管理器被管理员禁用",
            "Ctrl+Shift+Esc 提示「任务管理器已被系统管理员停用」",
            "管理员命令行 reg add ... /v DisableTaskMgr /d 0 恢复；若是公司电脑请联系 IT。",
            A("恢复任务管理器", ProblemActionKind.RunCommand,
              "reg add HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System /v DisableTaskMgr /t REG_DWORD /d 0 /f")),

        P(20, "任务栏与开始菜单", "输入法图标消失、无法切换输入法",
            "任务栏看不到输入法图标，Win+空格切换无效",
            "设置 → 时间和语言 → 语言 → 键盘选项检查输入法；运行 ctfmon 重新拉起输入法服务。",
            A("拉起输入法服务", ProblemActionKind.RunCommand, "ctfmon"),
            A("打开语言设置", ProblemActionKind.OpenMsSettings, "regionlanguage")),

        // ==================== 三、系统更新（10）====================
        P(21, "系统更新", "更新后屏幕闪烁 / 显示不适配",
            "装完更新后屏幕不停闪，或分辨率错乱",
            "Shift+重启 进入恢复环境 → 疑难解答 → 高级选项 → 卸载更新 → 卸载最新的「质量更新」。",
            A("打开更新历史", ProblemActionKind.OpenUpdateHistory),
            A("打开恢复选项", ProblemActionKind.OpenMsSettings, "recovery")),

        P(22, "系统更新", "更新卡在「正在配置」非常久",
            "开机转圈提示正在配置更新 30% 长时间不动",
            "耐心等 1-2 小时（配置中强行关机风险大）；超过 2 小时再强制重启，一般会自动回滚。",
            A("打开 Windows 更新", ProblemActionKind.OpenMsSettings, "windowsupdate")),

        P(23, "系统更新", "更新失败并报错代码",
            "更新历史里显示失败，错误如 0x800f0922 等",
            "设置 → 系统 → 疑难解答 → 其他疑难解答 → Windows 更新 → 运行；再重试更新。",
            A("运行疑难解答", ProblemActionKind.OpenTroubleshoot)),

        P(24, "系统更新", "一直检查不到更新",
            "点「检查更新」永远转圈或提示最新但没有",
            "重启 Windows Update 服务：net stop wuauserv & net stop bits & net start bits & net start wuauserv 后再检查。",
            A("重启更新服务", ProblemActionKind.RunCommand, "net stop wuauserv & net stop bits & net start bits & net start wuauserv")),

        P(25, "系统更新", "更新文件占用磁盘太大",
            "C 盘被 Windows 更新缓存吃掉几十 GB",
            "磁盘清理 → 清理系统文件 → 勾选「Windows 更新清理」；或设置→存储→打开存储感知自动清理。",
            A("打开磁盘清理", ProblemActionKind.RunCommand, "cleanmgr"),
            A("打开存储感知", ProblemActionKind.OpenMsSettings, "storagesense")),

        P(26, "系统更新", "驱动被更新推送搞坏（声卡/网卡异常）",
            "Windows Update 自动装了驱动后设备失效",
            "设备管理器 → 对应设备 → 属性 → 驱动程序 → 回退驱动程序；再禁用该驱动的自动更新。",
            A("打开设备管理器", ProblemActionKind.RunCommand, "devmgmt.msc")),

        P(27, "系统更新", "更新反复失败进入循环",
            "每次开机都尝试安装同一更新又失败",
            "重命名更新缓存目录：net stop wuauserv & ren %windir%\\SoftwareDistribution SoftwareDistribution.old & net start wuauserv，再重新检查更新。",
            A("重置更新缓存", ProblemActionKind.RunCommand,
              "net stop wuauserv & ren %windir%\\SoftwareDistribution SoftwareDistribution.old & net start wuauserv")),

        P(28, "系统更新", "想卸载某个已安装的更新",
            "某个更新装完后出问题，想单独卸掉",
            "设置 → Windows 更新 → 更新历史记录 → 卸载更新 → 选中要卸载的更新卸载（也可用 wusa）。",
            A("打开更新历史", ProblemActionKind.OpenUpdateHistory)),

        P(29, "系统更新", "想暂停或彻底关闭自动更新",
            "不想让系统在关键时候自动重启更新",
            "设置 → Windows 更新 → 暂停更新最多 5 周；彻底关闭需组策略（gpedit.msc→计算机配置→管理模板→Windows 组件→Windows 更新）。",
            A("打开 Windows 更新", ProblemActionKind.OpenMsSettings, "windowsupdate")),

        P(30, "系统更新", "更新后设备丢失（没声音/没网）",
            "更新后声卡、网卡等设备直接消失",
            "设备管理器 → 操作 → 扫描检测硬件改动；或 pnputil /scan-devices 重新枚举设备。",
            A("扫描硬件改动", ProblemActionKind.RunCommand, "pnputil /scan-devices"),
            A("打开设备管理器", ProblemActionKind.RunCommand, "devmgmt.msc")),

        // ==================== 四、网络连接（12）====================
        P(31, "网络连接", "网页打不开但 QQ/微信能上（DNS 异常）",
            "能登录即时通讯软件，浏览器却全部打不开",
            "DNS 解析坏了：刷新 DNS 缓存，再把 DNS 手动改为 223.5.5.5 / 119.29.29.29。",
            A("刷新 DNS 缓存", ProblemActionKind.FlushDns),
            A("打开网卡设置", ProblemActionKind.OpenMsSettings, "network-status")),

        P(32, "网络连接", "完全没网（WiFi 图标异常/黄叹号）",
            "网络图标带黄色感叹号，诊断提示无法上网",
            "重置 Winsock：管理员命令行 netsh winsock reset 后重启电脑；多数网络栈损坏可修复。",
            A("重置网络栈", ProblemActionKind.NetshWinsockReset)),

        P(33, "网络连接", "有线显示「未识别的网络」",
            "网线已插好，却显示未识别网络无 Internet",
            "重置 TCP/IP：netsh int ip reset 后重启；再检查网口灯和路由器。",
            A("重置 TCP/IP", ProblemActionKind.NetshIpReset)),

        P(34, "网络连接", "WiFi 列表为空、搜不到无线网",
            "点 WiFi 图标列表一片空白或直接没有开关",
            "重启 WLAN 服务：net stop wlansvc & net start wlansvc；设备管理器里确认无线网卡未被禁用。",
            A("重启无线服务", ProblemActionKind.RunCommand, "net stop wlansvc & net start wlansvc"),
            A("打开设备管理器", ProblemActionKind.RunCommand, "devmgmt.msc")),

        P(35, "网络连接", "IP 地址冲突",
            "提示「IP 地址与网络上其他系统有冲突」频繁掉线",
            "释放并重新获取 IP：ipconfig /release & ipconfig /renew；路由器端可改用 DHCP 自动分配。",
            A("重新获取 IP", ProblemActionKind.RenewIp)),

        P(36, "网络连接", "网速慢、打开网页延迟高",
            "能上网但加载很慢，视频卡顿",
            "先刷新 DNS；再排查是否有人在蹭网、路由器过热；ping 网关对比延迟判断内网/外网问题。",
            A("刷新 DNS 缓存", ProblemActionKind.FlushDns),
            A("测试连通性", ProblemActionKind.RunCommand, "ping -t 223.5.5.5")),

        P(37, "网络连接", "代理设置异常导致全部没网",
            "之前挂过代理/VPN，关了之后反而上不了网",
            "设置 → 网络和 Internet → 代理 → 关闭「使用代理服务器」和「自动检测设置」。",
            A("打开代理设置", ProblemActionKind.OpenMsSettings, "network-proxy")),

        P(38, "网络连接", "只剩飞行模式，网络开关消失",
            "网络列表里只有飞行模式，WiFi/以太网选项没了",
            "关闭飞行模式；重启 wlansvc 服务；设备管理器扫描硬件改动找回网卡。",
            A("打开飞行模式设置", ProblemActionKind.OpenMsSettings, "network-airplanemode"),
            A("扫描硬件改动", ProblemActionKind.RunCommand, "pnputil /scan-devices")),

        P(39, "网络连接", "网络适配器从列表里消失",
            "设置里网卡整个不见了，只剩 VPN 项",
            "设备管理器 → 查看→显示隐藏的设备 → 找到网卡启用；或扫描硬件改动重装驱动。",
            A("扫描硬件改动", ProblemActionKind.RunCommand, "pnputil /scan-devices"),
            A("打开设备管理器", ProblemActionKind.RunCommand, "devmgmt.msc")),

        P(40, "网络连接", "宽带拨号报错 651 / 691",
            "拨号连接失败，提示错误代码",
            "651 多为网卡/线路问题：重启光猫和网卡；691 是账号密码错误：核对宽带账号或联系运营商。",
            A("打开网络状态", ProblemActionKind.OpenMsSettings, "network-status")),

        P(41, "网络连接", "局域网看不到其他电脑（共享失效）",
            "网上邻居/网络里找不到同工作组的主机",
            "启用网络发现和文件共享；启动功能发现服务：net start fdPHost & net start FDResPub；双方同一工作组。",
            A("启动发现服务", ProblemActionKind.RunCommand, "net start fdPHost & net start FDResPub"),
            A("打开高级共享设置", ProblemActionKind.OpenMsSettings, "network-advanced-sharing")),

        P(42, "网络连接", "hosts 被篡改、网页强制跳转",
            "打开正常网站却跳到别的页面，浏览器主页被锁",
            "检查 C:\\Windows\\System32\\drivers\\etc\\hosts 删除可疑行；再用杀毒软件全盘查杀劫持。",
            A("打开 hosts 目录", ProblemActionKind.OpenFolder, "%SystemRoot%\\System32\\drivers\\etc")),

        // ==================== 五、声音（6）====================
        P(43, "声音", "电脑完全没声音（喇叭带红叉）",
            "音量图标显示 ×，播放设备无效",
            "重启音频服务：net stop audiosrv & net stop AudioEndpointBuilder & net start audiosrv & net start AudioEndpointBuilder；再检查默认播放设备。",
            A("重启音频服务", ProblemActionKind.RestartAudio)),

        P(44, "声音", "音量图标消失",
            "任务栏看不到小喇叭，无法调音量",
            "重启资源管理器；设置→个性化→任务栏→打开音量系统图标。",
            A("重启资源管理器", ProblemActionKind.RestartExplorer),
            A("打开任务栏设置", ProblemActionKind.OpenMsSettings, "personalization-taskbar")),

        P(45, "声音", "只有某一个程序没声音",
            "其他程序有声，唯独某个软件静音",
            "右键音量图标→音量合成器，检查该程序是否被静音或调到 0；或重启该程序。",
            A("打开应用音量设置", ProblemActionKind.OpenMsSettings, "apps-volume")),

        P(46, "声音", "蓝牙耳机连上但没声音/频繁断连",
            "蓝牙已配对成功却不走声音，或不断断开",
            "设置→蓝牙删除设备后重新配对；关闭其他设备的蓝牙干扰；更新蓝牙驱动。",
            A("打开蓝牙设置", ProblemActionKind.OpenMsSettings, "bluetooth")),

        P(47, "声音", "麦克风没声音、对方听不到",
            "通话/录音时麦克风无输入",
            "设置→隐私→麦克风 允许应用访问；声音设置里选择正确输入设备并测试。",
            A("打开麦克风设置", ProblemActionKind.OpenMsSettings, "privacy-microphone")),

        P(48, "声音", "开机声音异常、爆音杂音",
            "开机或播放初期有爆音、电流声",
            "重启音频服务；更新声卡驱动；关闭「独占模式」（声音→设备属性→高级）。",
            A("重启音频服务", ProblemActionKind.RestartAudio)),

        // ==================== 六、显示与显卡（8）====================
        P(49, "显示与显卡", "屏幕闪烁不停",
            "屏幕内容反复黑一下又亮，无法正常使用",
            "多为驱动问题：回退/重装显卡驱动；WinRE 卸载最新质量更新；换一根视频线排除硬件。",
            A("打开设备管理器", ProblemActionKind.RunCommand, "devmgmt.msc"),
            A("打开恢复选项", ProblemActionKind.OpenMsSettings, "recovery")),

        P(50, "显示与显卡", "分辨率不对或被锁死",
            "屏幕模糊、比例不对，改不了分辨率",
            "设置→系统→屏幕→显示分辨率 选推荐值；灰色不可选则是驱动问题，先重装显卡驱动。",
            A("打开显示设置", ProblemActionKind.OpenMsSettings, "display")),

        P(51, "显示与显卡", "夜间模式关不掉/屏幕发黄",
            "屏幕一直偏黄，关了夜间模式也一样",
            "设置→系统→屏幕→夜间模式关闭；校准颜色（dccw）；检查显卡控制面板色彩设置。",
            A("打开夜间模式设置", ProblemActionKind.OpenMsSettings, "nightlight")),

        P(52, "显示与显卡", "高分辨率屏字体发虚（缩放模糊）",
            "高分屏上部分程序文字模糊或过小",
            "设置→系统→屏幕→缩放 改为推荐 125%/150%；老程序右键属性→兼容性→更改高 DPI 设置→由程序缩放。",
            A("打开显示设置", ProblemActionKind.OpenMsSettings, "display")),

        P(53, "显示与显卡", "外接显示器检测不到",
            "插了 HDMI/DP 但系统识别不到第二屏",
            "Win+P 切换投影模式；换线换接口；设备管理器扫描硬件；更新显卡驱动。",
            A("扫描硬件改动", ProblemActionKind.RunCommand, "pnputil /scan-devices"),
            A("打开设备管理器", ProblemActionKind.RunCommand, "devmgmt.msc")),

        P(54, "显示与显卡", "开 HDR 后黑屏/颜色异常",
            "开启 HDR 屏幕变黑或过曝失真",
            "等 15 秒让系统自动回滚确认；安全模式下关闭 HDR；更新显卡与显示器固件。",
            A("打开显示设置", ProblemActionKind.OpenMsSettings, "display")),

        P(55, "显示与显卡", "提示「显示驱动程序已停止响应并已恢复」",
            "玩游戏/看视频时屏幕黑一下，右下角弹驱动恢复提示",
            "TDR 超时：更新显卡驱动；降低超频；sfc+DISM 修复系统文件。",
            A("修复系统文件", ProblemActionKind.RunSfcDism),
            A("打开设备管理器", ProblemActionKind.RunCommand, "devmgmt.msc")),

        P(56, "显示与显卡", "屏幕方向被旋转/想锁定竖屏",
            "画面横过来了或想固定方向不被自动旋转",
            "右键桌面→显示设置→显示方向 选「横向」；或 Ctrl+Alt+方向键 快速纠正（部分显卡支持）。",
            A("打开显示设置", ProblemActionKind.OpenMsSettings, "display")),

        // ==================== 七、性能与卡顿（10）====================
        P(57, "性能与卡顿", "开机很慢（要等很久才进桌面）",
            "开机转圈时间长，进桌面后还要卡一阵",
            "任务管理器→启动应用 禁用不必要的自启项；关闭快速启动试试；机械盘建议换 SSD。",
            A("打开任务管理器", ProblemActionKind.RunCommand, "taskmgr")),

        P(58, "性能与卡顿", "系统整体卡顿、操作延迟大",
            "打开窗口、切换程序都明显卡",
            "先跑 sfc /scannow 和 DISM 修复系统；检查 CPU/内存占用是否异常；清理启动项。",
            A("修复系统文件", ProblemActionKind.RunSfcDism)),

        P(59, "性能与卡顿", "磁盘占用长期 100%（机械盘尤其）",
            "任务管理器磁盘一栏常年 100%，整机卡死",
            "关闭 SysMain 服务：net stop SysMain & sc config SysMain start= disabled；关闭 Windows Search 索引也能缓解。",
            A("关闭 SysMain", ProblemActionKind.RunCommand, "net stop SysMain & sc config SysMain start= disabled")),

        P(60, "性能与卡顿", "内存占用异常高",
            "没开什么程序内存就 80%+",
            "任务管理器按内存排序找出大户；检查是否有浏览器标签失控；杀毒扫描挖矿木马。",
            A("打开任务管理器", ProblemActionKind.RunCommand, "taskmgr")),

        P(61, "性能与卡顿", "玩游戏掉帧、不流畅",
            "帧数比以前低很多或忽高忽低",
            "设置→游戏→游戏模式 开启；更新显卡驱动；电源计划改「高性能」。",
            A("打开游戏模式", ProblemActionKind.OpenMsSettings, "gaming-game"),
            A("打开电源设置", ProblemActionKind.OpenMsSettings, "powersleep")),

        P(62, "性能与卡顿", "风扇狂转、机身很烫",
            "待机也高温，噪音大",
            "电源计划改「平衡」；任务管理器查高占用进程；清灰换硅脂（笔记本）。",
            A("打开电源设置", ProblemActionKind.OpenMsSettings, "powersleep"),
            A("打开任务管理器", ProblemActionKind.RunCommand, "taskmgr")),

        P(63, "性能与卡顿", "睡眠唤醒后卡死或极卡",
            "合盖再打开后系统假死半分钟以上",
            "关闭快速启动：powercfg /h off 或注册表 HiberbootEnabled=0；更新芯片组驱动。",
            A("关闭快速启动", ProblemActionKind.PowerFastStartupOff)),

        P(64, "性能与卡顿", "打字有延迟、按键反应慢",
            "键盘输入要等半秒才上屏",
            "设置→辅助功能→键盘 关闭筛选键；换 USB 接口；无线键盘换电池。",
            A("打开键盘辅助设置", ProblemActionKind.OpenMsSettings, "ease-of-access-keyboard")),

        P(65, "性能与卡顿", "右键「新建」菜单要等很久",
            "右键→新建 弹出子菜单卡好几秒",
            "多为某文件类型注册的 Shell New 损坏；用工具清理 Shell New 项；临时用重启资源管理器缓解。",
            A("重启资源管理器（缓解）", ProblemActionKind.RestartExplorer)),

        P(66, "性能与卡顿", "用久了越来越卡（临时文件堆积）",
            "系统使用数月后启动、运行都变慢",
            "设置→存储→打开存储感知定期清理；跑一次磁盘清理（含系统文件）；卸载不用的软件。",
            A("打开存储感知", ProblemActionKind.OpenMsSettings, "storagesense"),
            A("打开磁盘清理", ProblemActionKind.RunCommand, "cleanmgr")),

        // ==================== 八、启动与关机（8）====================
        P(67, "启动与关机", "关机很慢、卡在「正在关机」",
            "点关机后转圈很久甚至不动",
            "关闭快速启动可解决大部分关机卡死；任务管理器结束占用程序后再关机。",
            A("关闭快速启动", ProblemActionKind.PowerFastStartupOff)),

        P(68, "启动与关机", "开机黑屏只有鼠标指针",
            "登录后黑屏，只看得到鼠标",
            "Ctrl+Alt+Del → 任务管理器 → 运行新任务 explorer.exe；能进桌面说明是 Shell 启动项问题，排查自启软件。",
            A("重启资源管理器", ProblemActionKind.RestartExplorer)),

        P(69, "启动与关机", "开机卡 Logo 进不去系统",
            "转圈卡死或反复重启进不了桌面",
            "强制关机 3 次触发 WinRE → 疑难解答 → 高级选项 → 卸载更新 / 系统还原 / 安全模式排查驱动。",
            A("打开恢复选项", ProblemActionKind.OpenMsSettings, "recovery")),

        P(70, "启动与关机", "睡眠唤醒后没网",
            "待机回来 WiFi 图标在但上不了网",
            "设备管理器→网卡属性→电源管理 取消「允许计算机关闭此设备」；重置 Winsock；禁用网卡节能。",
            A("重置网络栈", ProblemActionKind.NetshWinsockReset),
            A("打开设备管理器", ProblemActionKind.RunCommand, "devmgmt.msc")),

        P(71, "启动与关机", "电脑不停自动重启",
            "刚到桌面或使用中无故重启循环",
            "关闭「自动重启」看到真实错误：wmic recoveros set AutoReboot=False；再按蓝屏代码排查。",
            A("关闭自动重启", ProblemActionKind.DisableAutoReboot)),

        P(72, "启动与关机", "休眠唤醒后文件丢失/未保存",
            "休眠回来程序状态丢了",
            "休眠文件损坏：powercfg /h off 关闭休眠（改用睡眠），或 powercfg /h on 重建休眠文件。",
            A("关闭休眠", ProblemActionKind.PowerHibernationOff)),

        P(73, "启动与关机", "双系统启动菜单不出现",
            "装了双系统但直接进其中一个",
            "Win+R 运行 msconfig → 引导 → 超时改为 30 秒；或 bcdedit /timeout 30。",
            A("打开系统配置", ProblemActionKind.RunCommand, "msconfig")),

        P(74, "启动与关机", "点关机却变成重启",
            "选关机后电脑自己又开机了",
            "关闭快速启动（最常见原因）；检查「唤醒定时器」和计划任务是否设置了自动开机。",
            A("关闭快速启动", ProblemActionKind.PowerFastStartupOff)),

        // ==================== 九、应用与兼容性（8）====================
        P(75, "应用与兼容性", "Microsoft Store 打不开/一直转圈",
            "商店图标点了没反应或加载失败报错",
            "Win+R 运行 wsreset.exe 重置商店缓存（会自动打开商店）；仍不行重新登录微软账户。",
            A("重置商店缓存", ProblemActionKind.ResetStoreCache)),

        P(76, "应用与兼容性", "UWP 应用（计算器/照片等）闪退",
            "系统自带应用打开即退或图标带感叹号",
            "先 wsreset 重置商店；设置→应用→对应应用→高级选项→修复/重置。",
            A("重置商店缓存", ProblemActionKind.ResetStoreCache),
            A("打开应用列表", ProblemActionKind.OpenMsSettings, "apps-features")),

        P(77, "应用与兼容性", "老程序报兼容性错误",
            "旧软件提示不兼容或启动报 DLL 错误",
            "右键程序→属性→兼容性→以兼容模式（Win7/XP）运行 + 管理员身份运行；仍不行考虑虚拟机。",
            A("打开疑难解答", ProblemActionKind.OpenTroubleshoot)),

        P(78, "应用与兼容性", "默认应用关联全部丢失",
            "文件双击不知道用什么打开，PDF/图片全乱",
            "设置→应用→默认应用→按文件类型逐个重设；或直接「重置为微软推荐的默认值」。",
            A("打开默认应用", ProblemActionKind.OpenMsSettings, "apps-default-apps")),

        P(79, "应用与兼容性", "安装应用报错 0x8007xxxx",
            "下载完安装阶段失败报错代码",
            "运行 Windows 更新与应用商店疑难解答；清理 C 盘空间；确认没有旧版本残留。",
            A("运行疑难解答", ProblemActionKind.OpenTroubleshoot)),

        P(80, "应用与兼容性", "输入法候选词面板不出现",
            "能打字但候选框不显示，只能盲打",
            "运行 ctfmon 重新拉起输入框架；设置→语言 添加/删除中文输入法刷新组件。",
            A("拉起输入法服务", ProblemActionKind.RunCommand, "ctfmon"),
            A("打开语言设置", ProblemActionKind.OpenMsSettings, "regionlanguage")),

        P(81, "应用与兼容性", "截图工具（Win+Shift+S）失效",
            "快捷键没反应，截图无提示",
            "设置→辅助功能→键盘 确认「使用打印屏幕键」；重启资源管理器；重装截图应用（可选功能）。",
            A("重启资源管理器", ProblemActionKind.RestartExplorer),
            A("打开键盘设置", ProblemActionKind.OpenMsSettings, "ease-of-access-keyboard")),

        P(82, "应用与兼容性", "浏览器主页被劫持",
            "打开浏览器跳到导航站，改了又弹回",
            "浏览器内改回主页并锁定；检查快捷方式目标栏是否被加了网址后缀；全盘杀毒。",
            A("打开默认应用", ProblemActionKind.OpenMsSettings, "apps-default-apps")),

        // ==================== 十、存储与磁盘（6）====================
        P(83, "存储与磁盘", "C 盘空间爆满（红色警告）",
            "系统盘只剩几百 MB，电脑卡顿",
            "磁盘清理→清理系统文件（更新清理/旧 Windows）；把大文件移到其他盘；关闭休眠可释放 hiberfil.sys。",
            A("打开磁盘清理", ProblemActionKind.RunCommand, "cleanmgr"),
            A("关闭休眠释放空间", ProblemActionKind.PowerHibernationOff)),

        P(84, "存储与磁盘", "系统提示「请检查磁盘」/反复chkdsk",
            "开机总要跑一遍磁盘检查",
            "让 chkdsk 完整跑完一次：chkdsk C: /f（重启执行）；跑完仍反复则硬盘可能有坏道，尽快备份。",
            A("检查磁盘", ProblemActionKind.CheckDisk)),

        P(85, "存储与磁盘", "SSD 用久了掉速",
            "固态硬盘读写明显变慢",
            "Win+R 运行 dfrgui → 选中 SSD → 优化（执行 Trim）；确认开启了 Trim：fsutil behavior query DisableDeleteNotify。",
            A("打开优化工具", ProblemActionKind.RunCommand, "dfrgui")),

        P(86, "存储与磁盘", "U 盘/移动硬盘插上没反应",
            "插入后无盘符、无提示音",
            "磁盘管理(diskmgmt.msc) 看是否需要分配盘符或导入；换接口/线；设备管理器卸载大容量存储控制器重插。",
            A("打开磁盘管理", ProblemActionKind.RunCommand, "diskmgmt.msc"),
            A("打开设备管理器", ProblemActionKind.RunCommand, "devmgmt.msc")),

        P(87, "存储与磁盘", "文件被占用无法删除",
            "删除时提示「操作无法完成，文件已在另一程序中打开」",
            "资源监视器(resmon)→CPU→关联的句柄→搜索文件名→结束占用进程；或重启后再删。",
            A("打开资源监视器", ProblemActionKind.RunCommand, "resmon")),

        P(88, "存储与磁盘", "回收站清空了想找回文件",
            "误清空回收站，需要恢复文件",
            "若开启了文件历史记录/OneDrive 可从备份还原；否则立即停止写入，用 Recuve/DiskGenius 等恢复软件抢救。",
            A("打开存储设置", ProblemActionKind.OpenMsSettings, "storagesense")),

        // ==================== 十一、蓝屏与稳定性（6）====================
        P(89, "蓝屏与稳定性", "蓝屏一闪而过看不清错误代码",
            "蓝屏 1 秒就重启，抓不到 stop code",
            "先关闭自动重启（wmic recoveros set AutoReboot=False），下次蓝屏会停在错误页，把 QR 码/代码记下来再对症下药。",
            A("关闭自动重启", ProblemActionKind.DisableAutoReboot)),

        P(90, "蓝屏与稳定性", "频繁蓝屏（每天多次）",
            "不定时蓝屏，错误代码还不一样",
            "内存条是头号嫌疑：Win+R 运行 mdsched 重启检测内存；再跑 sfc+DISM；最后怀疑硬盘/驱动。",
            A("检测内存", ProblemActionKind.RunCommand, "mdsched"),
            A("修复系统文件", ProblemActionKind.RunSfcDism)),

        P(91, "蓝屏与稳定性", "蓝屏代码 DPC_WATCHDOG_VIOLATION",
            "常见于 NVMe SSD 旧固件或驱动冲突",
            "更新 SSD 固件和存储驱动；设备管理器→IDE ATA 控制器 换标准 AHCI 驱动。",
            A("打开设备管理器", ProblemActionKind.RunCommand, "devmgmt.msc")),

        P(92, "蓝屏与稳定性", "怀疑内存条有问题",
            "随机花屏、蓝屏、装系统失败",
            "Windows 内存诊断：mdsched 立即重启检测（约 20 分钟）；更彻底用 MemTest86 跑 4 轮以上。",
            A("检测内存", ProblemActionKind.RunCommand, "mdsched")),

        P(93, "蓝屏与稳定性", "系统文件损坏（各种诡异报错）",
            "组件报错 0x8…、更新失败、程序异常",
            "管理员命令行依次运行 sfc /scannow → DISM /Online /Cleanup-Image /RestoreHealth → 再跑一次 sfc。",
            A("SFC + DISM 一键修复", ProblemActionKind.RunSfcDism)),

        P(94, "蓝屏与稳定性", "WMI 报错 / 脚本宿主报错 0x80041003",
            "事件日志里大量 WMI 错误，或开机弹脚本错误",
            "管理员命令行执行 winmgmt /verifyrepository 检查，不一致则 winmgmt /salvagerepository 修复仓库。",
            A("修复 WMI 仓库", ProblemActionKind.ResetWmi)),

        // ==================== 十二、账户、权限与激活（6）====================
        P(95, "账户、权限与激活", "软件提示权限不足、无法写入",
            "安装/运行程序提示拒绝访问",
            "右键→以管理员身份运行；或给当前用户加目录权限（属性→安全→编辑）。",
            A("打开用户账户设置", ProblemActionKind.RunCommand, "netplwiz")),

        P(96, "账户、权限与激活", "文件/文件夹拒绝访问（无权限）",
            "打开某些目录提示「您当前无权访问该文件夹」",
            "属性→安全→高级→所有者改为自己→勾选替换子容器所有者→确定后重新进入。",
            A("打开高级安全示例", ProblemActionKind.RunCommand, "icacls %SystemRoot%\\Temp")),

        P(97, "账户、权限与激活", "微软账户登录不上/转圈",
            "商店、邮件等提示登录失败 0x80192ee7",
            "设置→账户 检查账户状态；网络走代理时先关；重置商店缓存；DNS 改 4.2.2.1 试试。",
            A("打开账户信息", ProblemActionKind.OpenMsSettings, "yourinfo"),
            A("重置商店缓存", ProblemActionKind.ResetStoreCache)),

        P(98, "账户、权限与激活", "Windows 激活失效/提示未激活",
            "设置里显示「Windows 未激活」或数字权益失效",
            "设置→系统→激活→疑难解答 重新激活；确认登录了原来的微软账户（数字许可证绑定硬件+账户）。",
            A("打开激活设置", ProblemActionKind.OpenMsSettings, "activation")),

        P(99, "账户、权限与激活", "忘记本地账户密码",
            "开机密码忘了进不去",
            "密码重置盘（若之前创建）；或用 PE/安装介质替换 utilman 方式重置；微软账户则官网找回。仅限自己电脑，他人设备勿操作。",
            A("打开登录选项", ProblemActionKind.OpenMsSettings, "signinoptions")),

        P(100, "账户、权限与激活", "想回滚到之前的系统状态（还原点）",
            "系统改坏了想整体回到几天前",
            "Win+R 运行 rstrui 打开系统还原 → 选择还原点 → 确认回滚（不影响个人文件）。",
            A("打开系统还原", ProblemActionKind.RunCommand, "rstrui")),
    };

    /// <summary>全部分类名（按目录顺序）。</summary>
    public static IReadOnlyList<string> Categories =>
        All.Select(p => p.Category).Distinct().ToList();
}
