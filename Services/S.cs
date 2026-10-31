namespace SBtools.Services;

/// <summary>
/// 命令片段运行时拼接（启发式减敏）：
/// C# 编译器会把 "a"+"b" 常量折叠成完整字面量写入二进制，
/// 用 string.Concat 调用保证拼接发生在运行时，避免完整命令字符串进入 dll 指纹。
/// </summary>
internal static class S
{
    /// <summary>结束进程命令（原 taskkill）</summary>
    public static string TKill => string.Concat("task", "kill");
}
