using System;
using System.Collections.Generic;

namespace SBtools.Services;

public static class ScheduleConfig
{
    public static bool Enabled { get; set; } = false;
    public static TimeSpan StartTime { get; set; } = new TimeSpan(19, 0, 0);
    public static TimeSpan EndTime { get; set; } = new TimeSpan(19, 30, 0);
    public static string ChannelName { get; set; } = "CCTV-13 新闻";

    /// <summary>生效的星期（空 = 每天重复）</summary>
    public static HashSet<DayOfWeek> Days { get; set; } = new();
}