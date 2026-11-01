# Vael 插件开发指南

> **版本**：2.0
> **适用 Vael 版本**：v0.5.6+
> **最后更新**：2026-11

Vael 提供完整的插件系统：一个 .NET 8 类库 DLL，实现 `IXiYuePlugin` 接口，放进 `Plugins/` 目录即可运行。

插件可以：**创建侧边栏选项卡**、**注册小组件卡片**、**弹独立窗口**、读写自己的配置、发通知、写日志、订阅关键词/网络事件，并支持在插件页**一键启用/禁用**。

---

## 快速开始

### 1. 创建插件工程

```bash
dotnet new classlib -n MyPlugin
cd MyPlugin
```

`.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <!-- 引用 Vael 主程序获得插件接口（路径按实际位置调整） -->
  <ItemGroup>
    <Reference Include="SBtools">
      <HintPath>..\SBtools.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <!-- 创建界面需要 Avalonia（版本与主程序一致：11.3.x） -->
    <PackageReference Include="Avalonia" Version="11.3.12" />
  </ItemGroup>
</Project>
```

> `<Private>false</Private>` 很重要：避免把主程序 DLL 打包进插件目录造成冲突。

### 2. 实现入口

```csharp
using SBtools.Plugins;

public class MyPlugin : IXiYuePlugin
{
    public string Id => "com.example.myplugin";   // 唯一 ID
    public string Name => "我的插件";
    public string Version => "1.0.0";
    public string Author => "Your Name";
    public string Description => "插件描述";

    private IPluginContext _ctx = null!;

    public void OnLoad(IPluginContext context)
    {
        _ctx = context;
        context.Log("插件已加载");
        context.Toast("我的插件", "Hello Vael!");
    }

    public void OnUnload() { }
}
```

### 3. 部署

```bash
dotnet build -c Release
copy bin\Release\net8.0\MyPlugin.dll  <Vael目录>\Plugins\
```

启动 Vael，「插件」页即可看到。**一键禁用/启用**立即生效并持久化。

---

## API 总览（IPluginContext）

### 信息

| API | 说明 |
|-----|------|
| `PluginId` | 当前插件 ID |
| `AppVersion` | 主程序版本 |
| `DataDirectory` | 插件独立数据目录（自动创建） |

### 日志与通知

| API | 说明 |
|-----|------|
| `Log(msg)` | 写日志（实验室可见 + 文件） |
| `Toast(title, msg, sec)` | 右下角通知 |

### 系统操作

| API | 说明 |
|-----|------|
| `OpenUrl(url)` | 默认浏览器打开 |
| `PlaySound(wav)` | 播放 Sounds 目录音效 |
| `SetSystemVolume(0-100)` | 系统音量 |

### 配置（自动持久化到插件目录 config.json）

| API | 说明 |
|-----|------|
| `GetConfig / SetConfig` | 字符串 |
| `GetConfigBool / SetConfigBool` | 布尔 |
| `GetConfigInt / SetConfigInt` | 整数 |

### 事件

| 事件 | 说明 |
|------|------|
| `KeywordMatched(string)` | 浏览器插件命中关键词 |
| `NetworkStatusChanged(bool)` | 网络连接/断开 |

### UI 扩展（v0.5.6+）★

```csharp
// 1. 侧边栏选项卡：buildPage 每次进入页面时调用，返回任意 Avalonia Control
context.RegisterTab("我的页面", () => BuildMyPage());

// 2. 小组件页卡片：icon 用 Segoe Fluent Icons 字形（如 "\uE8A5"），点击触发回调
context.RegisterWidgetCard("每日签到", "点击签到打卡", "\uE8A7", () => { /* ... */ });

// 3. 独立窗口（自动滚动内容 + 居中弹出）
context.ShowWindow("我的窗口", BuildMyPage());
```

界面构建示例（纯代码，无需 XAML）：

```csharp
private Avalonia.Controls.Control BuildMyPage() => new Avalonia.Controls.StackPanel
{
    Margin = new Avalonia.Thickness(24, 16),
    Spacing = 10,
    Children =
    {
        new Avalonia.Controls.TextBlock { Text = "Hello from plugin!", FontSize = 22 },
        new Avalonia.Controls.Button { Content = "点我" },
    }
};
```

禁用插件时，它注册的选项卡和卡片会被**自动移除**；重新启用即恢复。

---

## 内置示例

Vael 自带「示例插件」（源码：`Plugins/BuiltinDemoPlugin.cs`），演示上述全部能力：

- 侧边栏「示例插件页」选项卡（配置读写 + 日志 + 通知按钮）
- 小组件页「插件演示卡」（点击弹独立窗口）
- 网络事件订阅

在「插件」页禁用它即可整体消失（选项卡、卡片一并移除），启用后恢复——可直接作为开发模板。

---

## 调试技巧

1. **看日志**：实验室 → 日志列表（或 `%APPDATA%\SchoolBusytools\logs\`）
2. **加载失败**：插件页会显示错误信息（缺依赖 / 版本不匹配 / 无入口类）
3. **依赖 DLL**：随插件一起放进 `Plugins/`
4. **改完代码**：插件页 → 重新加载（或重启 Vael）

## 注意事项

- `OnLoad` 不要阻塞太久（超过 2 秒会影响启动）
- 插件抛出的异常会被捕获并记录，不会拖垮主程序
- 卸载（`OnUnload`）时请停掉自己开的线程/定时器
- 同一进程内插件 DLL 无法真正卸载，「禁用」= 停止激活并移除全部 UI
