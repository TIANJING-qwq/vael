using System;
using System.Collections.Generic;
using System.Timers;

namespace SBtools.Services;

public class PlaybackScheduler : IDisposable
{
    private readonly Timer _timer;
    private readonly Action _onStart;
    private readonly Action _onStop;

    private TimeSpan _startTime = new TimeSpan(19, 0, 0);
    private TimeSpan _endTime = new TimeSpan(19, 30, 0);
    private bool _enabled;

    // 记录上次触发的边界时间
    private DateTime _lastStartBoundary = DateTime.MinValue;
    private DateTime _lastStopBoundary = DateTime.MinValue;

    private readonly object _lock = new();

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (value)
            {
                // 启用时重置边界，允许立即触发
                _lastStartBoundary = DateTime.MinValue;
                _lastStopBoundary = DateTime.MinValue;
            }
            LogService.Log($"调度器 Enabled = {value}", "调度");
        }
    }

    public TimeSpan StartTime
    {
        get => _startTime;
        set { _startTime = value; LogService.Log($"调度器 StartTime = {value:hh\\:mm}", "调度"); }
    }

    public TimeSpan EndTime
    {
        get => _endTime;
        set { _endTime = value; LogService.Log($"调度器 EndTime = {value:hh\\:mm}", "调度"); }
    }

    public bool IsPlaying { get; private set; }

    /// <summary>生效的星期（空 = 每天）。跨午夜时段按开始日判断。</summary>
    public HashSet<DayOfWeek> Days { get; set; } = new();

    public PlaybackScheduler(Action onStart, Action onStop)
    {
        _onStart = onStart;
        _onStop = onStop;
        _timer = new Timer(5_000) { AutoReset = true };
        _timer.Elapsed += OnTick;
        _timer.Start();
        LogService.Log("调度器已启动（每 5 秒检查一次）", "调度");
    }

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        try
        {
            if (!_enabled) return;

            var now = DateTime.Now;

            lock (_lock)
            {
                // ★ 计算今天（或跨午夜时昨天/明天）的开始 / 结束边界
                var (startBoundary, endBoundary) = CalcBoundaries(now);

                // ★ 周几过滤：非生效日不触发（按开始边界所在日算，兼容跨午夜）
                if (Days.Count > 0 && !Days.Contains(startBoundary.DayOfWeek))
                {
                    // 防止边界时间戳停留在旧值，跨日回来后仍能触发
                    _lastStartBoundary = DateTime.MinValue;
                    _lastStopBoundary = DateTime.MinValue;
                    return;
                }

                // ============ 触发开始 ============
                // 条件：上次触发的开始边界 < 本次边界，且当前时间 >= 本次边界，且当前时间 < 结束边界
                if (_lastStartBoundary < startBoundary &&
                    now >= startBoundary &&
                    now < endBoundary)
                {
                    _lastStartBoundary = startBoundary;
                    IsPlaying = true;
                    LogService.Log($"★ 触发【开始】播放 {now:HH:mm:ss}（边界 {startBoundary:HH:mm}）", "调度");

                    try { _onStart?.Invoke(); }
                    catch (Exception ex) { LogService.Log($"开始回调异常: {ex.Message}", "调度"); }
                }

                // ============ 触发结束 ============
                // 条件：上次触发的结束边界 < 本次边界，且当前时间 >= 本次边界
                if (_lastStopBoundary < endBoundary &&
                    now >= endBoundary)
                {
                    _lastStopBoundary = endBoundary;
                    if (IsPlaying)
                    {
                        IsPlaying = false;
                        LogService.Log($"★ 触发【结束】播放 {now:HH:mm:ss}（边界 {endBoundary:HH:mm}）", "调度");

                        try { _onStop?.Invoke(); }
                        catch (Exception ex) { LogService.Log($"结束回调异常: {ex.Message}", "调度"); }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"调度器异常: {ex.Message}", "调度");
        }
    }

    /// <summary>
    /// 计算当前的开始 / 结束边界（处理跨午夜）
    /// </summary>
    private (DateTime start, DateTime end) CalcBoundaries(DateTime now)
    {
        var todayStart = now.Date + _startTime;
        var todayEnd = now.Date + _endTime;

        if (_startTime <= _endTime)
        {
            // 普通时段 [19:00, 19:30]，同一天
            return (todayStart, todayEnd);
        }
        else
        {
            // 跨午夜 [23:00, 01:00]
            if (now.TimeOfDay < _endTime)
            {
                // 当前是午夜后，开始时间是昨天
                var yesterdayStart = now.Date.AddDays(-1) + _startTime;
                return (yesterdayStart, todayEnd);
            }
            else
            {
                // 当前是午夜前，结束时间是明天
                var tomorrowEnd = now.Date.AddDays(1) + _endTime;
                return (todayStart, tomorrowEnd);
            }
        }
    }

    public void ForceStart()
    {
        IsPlaying = true;
        LogService.Log("手动强制【开始】", "调度");
        try { _onStart?.Invoke(); }
        catch (Exception ex) { LogService.Log($"强制开始异常: {ex.Message}", "调度"); }
    }

    public void ForceStop()
    {
        IsPlaying = false;
        LogService.Log("手动强制【结束】", "调度");
        try { _onStop?.Invoke(); }
        catch (Exception ex) { LogService.Log($"强制结束异常: {ex.Message}", "调度"); }
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}