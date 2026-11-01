using SBtools.Services;
using SBtools.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SBtools.Plugins;

public sealed class PluginManager
{
    private static readonly Lazy<PluginManager> _lazy = new(() => new PluginManager());
    public static PluginManager Instance => _lazy.Value;

    private readonly List<PluginInfo> _plugins = new();
    // 全部已知插件（含禁用的）：dll 路径 → 实例，供运行时启用
    private readonly Dictionary<string, IXiYuePlugin> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PluginContext> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _disabled = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<PluginInfo> Plugins => _plugins;

    public static string PluginsDir =>
        Path.Combine(AppContext.BaseDirectory, "Plugins");

    private PluginManager() { }

    private void LoadDisabledList()
    {
        try
        {
            var json = ConfigManager.Instance.DisabledPlugins;
            _disabled = string.IsNullOrWhiteSpace(json)
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : JsonConvert.DeserializeObject<HashSet<string>>(json)!.ToHashSet(StringComparer.OrdinalIgnoreCase)
                  ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch { _disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
    }

    private void SaveDisabledList()
    {
        try { ConfigManager.Instance.DisabledPlugins = JsonConvert.SerializeObject(_disabled.ToList()); } catch { }
    }

    public bool IsEnabled(string id) => !_disabled.Contains(id);

    /// <summary>一键启用/禁用（持久化，运行时立即生效）</summary>
    public void SetEnabled(string id, bool enable)
    {
        if (enable)
        {
            if (!_disabled.Remove(id)) return;
            SaveDisabledList();
            // 重新激活
            if (_known.TryGetValue(id, out var plugin))
                Activate(plugin, _plugins.FirstOrDefault(p => p.Id == id));
        }
        else
        {
            if (!_disabled.Add(id)) return;
            SaveDisabledList();
            // 立即停用
            if (_loaded.TryGetValue(id, out var ctx))
            {
                try
                {
                    _known[id]?.OnUnload();
                    ctx.RemoveUi();
                    ctx.Detach();
                }
                catch (Exception ex) { LogService.Log($"停用插件 {id} 失败: {ex.Message}", "插件"); }
                _loaded.Remove(id);
            }
            var info = _plugins.FirstOrDefault(p => p.Id == id);
            if (info != null) info.IsLoaded = false;
        }
        LogService.Log($"插件 {id} 已{(enable ? "启用" : "禁用")}", "插件");
    }

    public void LoadAll()
    {
        _plugins.Clear();
        _known.Clear();
        LoadDisabledList();

        // ★ 内置示例插件（演示：选项卡 + 小组件卡片 + 配置）
        RegisterBuiltIn(new BuiltinDemoPlugin());

        try
        {
            if (!Directory.Exists(PluginsDir))
            {
                Directory.CreateDirectory(PluginsDir);
                LogService.Log($"插件目录已创建: {PluginsDir}", "插件");
            }
            else
            {
                var dlls = Directory.GetFiles(PluginsDir, "*.dll", SearchOption.TopDirectoryOnly);
                LogService.Log($"扫描到 {dlls.Length} 个插件 DLL", "插件");
                foreach (var dll in dlls)
                    TryLoadPlugin(dll);
            }

            // 激活未禁用的（内置插件在注册时已激活，跳过）
            foreach (var info in _plugins.Where(p => !string.IsNullOrEmpty(p.Id)).ToList())
            {
                if (_loaded.ContainsKey(info.Id)) continue; // ★ 防止双重 OnLoad → 重复注册 UI
                if (IsEnabled(info.Id) && _known.TryGetValue(info.Id, out var plugin))
                    Activate(plugin, info);
            }

            LogService.Log($"插件加载完成：{_loaded.Count}/{_plugins.Count} 激活", "插件");
        }
        catch (Exception ex)
        {
            LogService.Log($"插件加载异常: {ex.Message}", "插件");
        }
    }

    private void RegisterBuiltIn(IXiYuePlugin plugin)
    {
        var info = new PluginInfo
        {
            Id = plugin.Id, Name = plugin.Name, Version = plugin.Version,
            Author = plugin.Author, Description = plugin.Description,
            IsBuiltIn = false,
        };
        _known[plugin.Id] = plugin;
        _plugins.Add(info);
        if (IsEnabled(plugin.Id)) Activate(plugin, info);
    }

    private void Activate(IXiYuePlugin plugin, PluginInfo? info)
    {
        try
        {
            var ctx = new PluginContext(plugin.Id);
            plugin.OnLoad(ctx);
            _loaded[plugin.Id] = ctx;
            if (info != null) { info.IsLoaded = true; info.ErrorMessage = null; }
            LogService.Log($"插件已加载: {plugin.Name} v{plugin.Version} by {plugin.Author}", "插件");
        }
        catch (Exception ex)
        {
            if (info != null) { info.IsLoaded = false; info.ErrorMessage = ex.Message; }
            LogService.Log($"插件初始化失败 [{plugin.Id}]: {ex.Message}", "插件");
        }
    }

    private void TryLoadPlugin(string dllPath)
    {
        var fileName = Path.GetFileName(dllPath);
        var info = new PluginInfo { DllPath = dllPath };

        try
        {
            var asm = System.Reflection.Assembly.LoadFrom(dllPath);
            var pluginTypes = asm.GetTypes()
                .Where(t => typeof(IXiYuePlugin).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .ToList();

            if (pluginTypes.Count == 0)
            {
                info.Name = fileName;
                info.ErrorMessage = "没有找到 IXiYuePlugin 实现类";
                _plugins.Add(info);
                return;
            }

            foreach (var type in pluginTypes)
            {
                try
                {
                    var plugin = (IXiYuePlugin)Activator.CreateInstance(type)!;
                    info.Id = plugin.Id;
                    info.Name = plugin.Name;
                    info.Version = plugin.Version;
                    info.Author = plugin.Author;
                    info.Description = plugin.Description;
                    _known[plugin.Id] = plugin;
                    if (!IsEnabled(plugin.Id))
                        info.IsLoaded = false; // 已禁用：记录但不激活
                }
                catch (Exception ex)
                {
                    info.IsLoaded = false;
                    info.ErrorMessage = ex.Message;
                    LogService.Log($"插件实例化失败 [{type.Name}]: {ex.Message}", "插件");
                }
                _plugins.Add(info);
            }
        }
        catch (Exception ex)
        {
            info.Name = fileName;
            info.ErrorMessage = ex.Message;
            _plugins.Add(info);
            LogService.Log($"加载 DLL 失败 [{fileName}]: {ex.Message}", "插件");
        }
    }

    public void UnloadAll()
    {
        foreach (var (id, ctx) in _loaded)
        {
            try
            {
                _known.TryGetValue(id, out var plugin);
                plugin?.OnUnload();
                ctx.RemoveUi();
                ctx.Detach();
            }
            catch (Exception ex)
            {
                LogService.Log($"卸载插件 {id} 失败: {ex.Message}", "插件");
            }
        }
        _loaded.Clear();
    }

    internal void BroadcastNetworkStatus(bool connected)
    {
        foreach (var (_, ctx) in _loaded)
            ctx.RaiseNetworkStatusChanged(connected);
    }
}
