<div align="center">

<img src="Assets/app.png" width="140" alt="Vael Logo" />

# Vael

**轻量多功能系统工具箱 · 桌面小组件 · 系统优化 · 校园场景**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/Avalonia-11.3-8A2BE2)](https://avaloniaui.net/)
[![FluentAvalonia](https://img.shields.io/badge/FluentAvalonia-2.2-0078D4)](https://github.com/amwx/FluentAvalonia)
[![WebView2](https://img.shields.io/badge/WebView2-1.0-0078D4)](https://developer.microsoft.com/microsoft-edge/webview2/)
[![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)](#)
[![License](https://img.shields.io/badge/license-BSD--3-green)](LICENSE)

</div>

---

## 简介

**Vael** 是一款基于 **C# / .NET 8 + Avalonia UI** 的 Windows 桌面工具箱，Fluent Design 风格。

一个程序装下：**16 款桌面小组件**（时钟 / 倒计时 / 性能监控 / 抛硬币 / 随机点名 / 番茄钟 / 计算器……）、**114 项系统优化**、**115 条 Windows 问题自动修复**、CCTV 定时直播、涩图合集、通知系统、插件生态、自动更新。另有校园网 Portal 自动认证等实用小功能。

> 纯客户端工具，不收集任何用户数据，敏感凭据 AES 加密存储在本地。

---

## 功能总览

### 桌面小组件（v0.5.4 全新）

独立小窗悬浮桌面，**点击开启 / 再次点击关闭**，右键进入每个组件的详细配置：

| 类别 | 组件 |
|------|------|
| 悬浮窗 | 性能监控（CPU/内存/网速，自动换单位）、数字时钟、高考倒计时、自定义倒计时、随机涩图 |
| 趣味 | 骰子（右键改面数）、抛硬币（**上抛 + 空中旋转动画**）、一言卡片 |
| 校园 | 随机点名（**老虎机滚动动画**）、随机题号、番茄钟 |
| 实用 | 按键式计算器、电池、C 盘余量、内存 60 秒曲线图、休息提醒 |

特性：

- **材质与主界面同源**：云母 / 亚克力（下拉选择，实时切换全部组件）
- **会话记忆**：退出自动保存，重启后所有小组件原位置原样恢复（含置顶状态）
- **非线性动画**：开窗淡入缩放、关窗淡出缩退、抽奖平方减速
- 每个组件独立**置顶开关**、位置记忆、右键参数配置
- 涩图小组件支持历史回退（上一张按钮 / 中键）

### 系统优化大全（114 项）

一键或逐项执行的深度优化，覆盖：

- **界面**：动画加速、菜单延迟、任务栏透明、小组件禁用……
- **网络**：TCP 吞吐、网卡节能、DNS、网络限流解除……
- **性能**：后台应用、游戏调度、内存压缩、存储感知……
- **安全/隐私**：LSASS 保护、遥测、广告 ID、活动历史……

每项带说明与风险提示，可一键还原。

### Windows 问题自查修复（115 条）

电脑卡顿 / 无声 / 断网 / 蓝屏 / U 盘读不出……115 条常见问题：

- 按症状关键词搜索
- **手动修复步骤**说明
- **一键自动修复**（服务重启 / 注册表 / 网络栈重置等）

### CCTV 定时直播

- 基于 **Edge WebView2** 打开央视官方直播页（`tv.cctv.com`），复用官方播放器与 CDN，稳定不卡
- CCTV-1 综合 / CCTV-13 新闻
- 每日定时（支持跨午夜），到点自动全屏纯净播放

### 通知系统

- 亚克力半透明背景，自适应主题
- 非线性动画，多通知自动堆叠
- 主窗口隐藏时独立置顶显示

### 涩图合集

- 图片资源**不打包进程序**，从 GitHub Releases 按需下载（zip 流式读取，不占双倍空间）
- JPG / PNG / **GIF 动图** / WebP 等格式
- 网格缩略图 + 大图预览 + 小组件随机悬浮
- 支持代理下载

### 插件系统

- 第三方 .NET 8 类库动态加载
- 完整 API：日志、通知、系统操作、配置持久化、事件订阅
- 每插件独立数据目录，内置管理页
- 详见 **[插件开发文档](docs/PLUGIN_DEVELOPMENT.md)**

### 浏览器插件联动

- 内置 Chrome 扩展（`Extensions/browser-plugin/`，chrome://extensions 加载即用）
- 命中关键词 → 本地 IPC 通知 Vael → 自动动作：开浏览器窗口 / 调音量 / 置顶遮罩 / 密码确认

### 自动更新

- GitHub Releases + 静默安装（Inno Setup 安装包，规避杀软误报路径）
- 下载进度 + 实时速度 + 可取消，支持代理
- 独立「更新」页：版本、代号、版本日志时间线

### 界面与个性化

- **云母 / 亚克力**窗口材质（与小组件同源实现）
- 浅色 / 深色 / 跟随系统主题
- 全局点击音效（自定义 `.wav`）
- 非线性动画体系：启动、弹窗、下拉框、小组件开关
- 自定义标题栏、托盘、开机自启、单实例

### 网络工具

- 校园网 Portal 自动认证（自动识别 ikuai8 等网关，一键 / 全自动）
- 连通性检测、本地 / 公网 IP 显示

---

## 快速开始

### 环境要求

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 1809+（CCTV 需 Edge WebView2 运行时）

### 从源码运行

```bash
git clone https://github.com/TIANJING-qwq/Vael.git
cd Vael
dotnet restore
dotnet run
```

### 发布独立可执行文件

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./publish/win-x64
```

> CI 推送 tag 即自动构建四平台 ZIP + Windows 安装包并发布 Release（`.github/workflows/release.yml`）。

---

## 项目结构（核心）

```text
Vael/
├── Services/
│   ├── SystemOptimizer.cs            # 系统优化大全（114 项）
│   ├── WindowsProblemCatalog.cs      # 问题自查修复（115 条）
│   ├── GalleryService.cs             # 图库（zip 流式读取）
│   ├── UpdateService.cs              # 自动更新
│   └── LocalIpcServer.cs             # 浏览器插件 IPC
├── Views/
│   ├── WidgetView.axaml(.cs)         # 小组件管理页（16 款 + 材质下拉）
│   ├── SimpleWidgetWindow.axaml.cs   # 通用小组件窗（材质/会话/动画/配置）
│   ├── WidgetWindow.axaml            # 性能悬浮窗
│   ├── UpdateView.axaml              # 更新页（版本日志时间线）
│   ├── GalleryView.axaml             # 涩图合集
│   └── OptimizationView.axaml        # 系统优化
├── Extensions/browser-plugin/        # 内置浏览器扩展
├── Plugins/                          # 插件系统
└── .github/workflows/release.yml     # 一键发布流水线
```

---

## 配置文件位置

| 平台 | 路径 |
|------|------|
| Windows | `%APPDATA%\SchoolBusytools\` |

包含：`config.json`（含小组件会话）、`secure.key`、`logs/`、`gallery/`、`cover_cache/`（小组件封面图缓存）、`plugins/{id}/`。

---

## 技术栈

| 分类 | 技术 | 版本 |
|------|------|------|
| 语言 | C# | 12 |
| 运行时 | .NET | 8 |
| UI 框架 | Avalonia UI | 11.3 |
| 设计风格 | FluentAvalonia | 2.2 |
| 视频播放 | WebView2（Edge） | 1.0 |
| JSON | Newtonsoft.Json | 13.0 |
| 自动更新 | Updatum | 1.4 |
| 材质 | DWM（云母/亚克力）+ ACCENT 模糊 | - |

---

## 常见问题

**Q: 首次运行较慢？**
A: 单文件 exe 首次需解压，约 2～5 秒，之后正常。

**Q: CCTV 无法播放？**
A: 依赖 Edge WebView2 运行时，Win11 自带，Win10 到微软官网装 Evergreen。

**Q: 杀毒软件报毒？**
A: 未签名 exe 可能被误报（HEUR 类），请添加信任；已改用安装包静默升级降低误报率。

**Q: 小组件没恢复 / 没动画？**
A: 会话每 10 秒自动保存，退出后重启 3 秒内恢复；动画需要系统未关闭视觉特效。

**Q: 云母/亚克力没效果？**
A: 亚克力走 ACCENT 合成模糊（Win10 1803+ 可用），云母需 Win11；浅色模式自动回退。

**Q: 自动更新失败？**
A: 设置页选代理（gh-proxy.com 等），或检查 GitHub 连通性。

---

## 更新日志（节选）

### v0.5.4（Umi）
- **小组件管理页**：16 款自实现桌面小组件，点击开关、右键详细配置
- 抛硬币上抛旋转动画、点名老虎机滚动动画、开关窗非线性淡入淡出
- 云母 / 亚克力材质实时切换；会话记忆（位置/置顶/参数）
- 常见问题修复 115 条、优化大全 114 项
- 更新页重做（版本日志时间线）；合入浏览器扩展

### v0.5.2
- Windows 更新改安装包静默升级；CI 产出安装包

### v0.5.1
- 关于页彩蛋；下拉框非线性动画

完整历史见 **[更新日志](docs/CHANGELOG.md)**。

---

## 贡献

欢迎 Issue / PR：Fork → 特性分支 → 提交 → Pull Request。

## 许可证

**BSD-3 License**。

---

<div align="center">

**Vael · tianjing & deepseek**

⭐ 如果这个项目对你有帮助，欢迎点一个 Star！

</div>
