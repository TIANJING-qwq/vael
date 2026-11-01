# Vael 开屏动画（Splash）接入说明

开屏不是视频，也不加载任何 mp4 / 媒体文件。它由 Avalonia 控件 + `DispatcherTimer` + `Stopwatch` 在每一帧实时计算，进度由真实启动节点驱动。

## 文件

| 文件 | 作用 |
|---|---|
| `Views/SplashWindow.axaml` | 920×640 无边框卡片；内部 920×640 设计画布放在 `Viewbox` 中，随窗口/DPI 等比缩放 |
| `Views/SplashWindow.axaml.cs` | 时间轴、进度平滑、模块 chip、READY、淡出；全部逐帧计算 |
| `App.axaml.cs` | 在真实启动节点调用 `SetProgress`，主窗口 Opened 后 `DismissAsync` |
| `Models/ConfigManager.cs` | 已有 `StartupAnimationEnabled` 开关，无需新增配置 |

## API

```csharp
// 显示（只应在 UI 线程 / 启动阶段调用）
SplashWindow.ShowSplash();

// 真实进度：0..1，可附带当前阶段文案；不调用也会按时间自动缓慢前进
SplashWindow.SetProgress(0.35, "载入桌面组件");

// 主窗口就绪：补到 100%，显示 READY，停留约 0.38s 后淡出关闭
await SplashWindow.DismissAsync();
```

`PlayTestAsync()` 供「实验室 → 测试动画」使用，会按完整时间轴播放一遍。

## 当前启动顺序（App.axaml.cs）

```text
ShowSplash()                 0.12  初始化运行环境
新建 MainWindow              0.35  载入桌面组件
MainWindow 构造完成          0.58  构建主界面
MainWindow.Opened            0.86  连接插件与通知服务
DismissAsync()               1.00  准备就绪 → READY → 淡出
```

动画端会把上报值和平滑兜底值取较大者，再以约 0.09/帧 逼近，因此：

- 某个阶段没上报，进度也不会停；
- 上报跳跃不会让进度条瞬移；
- 启动慢时开屏会一直保留，不会出现“动画播完了但程序没起来”的空窗。

## 自适应

- 窗口默认 920×640，`CanResize="True"`，`MinWidth/MinHeight` 640×450。
- 所有几何都在 920×640 画布内，外层 `Viewbox Stretch="Uniform"`：窗口尺寸、DPI、不同屏幕都会整体等比适配，不会裁切。
- 如果要改成“填满主窗口”的版本：把 `SplashWindow` 的 `Width/Height` 设为与 `MainWindow` 一致，并去掉卡片的圆角/外边距即可；布局不需要重写。

## 可调参数

`Views/SplashWindow.axaml.cs` 顶部：

```csharp
private const double MinDisplayMs = 1500;  // 最短展示
private const double ReadyHoldMs  = 380;   // READY 停留
private const double DismissMs    = 260;   // 淡出
```

时间轴节点集中在 `OnTick` 的 `Seg(t, a, b)` 调用里；文案在 `SetProgress` 的第二个参数或 `OnTick` 的状态分支中修改。

## 验证

```powershell
dotnet build SBtools.csproj -c Debug
dotnet bin/Debug/net8.0/SBtools.dll
```

构建 0 error；正常启动可见卡片式开屏 → 进度 → READY → 淡出，主窗口在卡片后方，结束后显示。
