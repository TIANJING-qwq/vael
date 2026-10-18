using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using System;
using System.Text.RegularExpressions;

namespace SBtools.Controls;

/// <summary>
/// 轻量 Markdown 渲染器：把 Markdown 文本转成 Avalonia 控件。
/// 支持：# / ## / ### 标题、- / * 列表、**加粗**、`代码`、普通段落。
/// </summary>
public static class MarkdownRenderer
{
    private static readonly Regex InlinePattern =
        new(@"(\*\*(.+?)\*\*|`([^`]+)`)", RegexOptions.Compiled);

    public static Control Render(string? markdown)
    {
        var panel = new StackPanel { Spacing = 2 };
        if (string.IsNullOrWhiteSpace(markdown)) return panel;

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                panel.Children.Add(new Border { Height = 6 });
                continue;
            }

            if (line.StartsWith("### "))
            {
                panel.Children.Add(Heading(line[4..], 13, FontWeight.SemiBold));
                continue;
            }
            if (line.StartsWith("## "))
            {
                panel.Children.Add(Heading(line[3..], 14, FontWeight.SemiBold));
                continue;
            }
            if (line.StartsWith("# "))
            {
                panel.Children.Add(Heading(line[2..], 16, FontWeight.Bold));
                continue;
            }

            if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                panel.Children.Add(Bullet(line[2..]));
                continue;
            }

            panel.Children.Add(Paragraph(line));
        }

        return panel;
    }

    private static TextBlock Heading(string text, double size, FontWeight weight)
        => new TextBlock
        {
            Text = StripInline(text),
            FontSize = size,
            FontWeight = weight,
            Margin = new Thickness(0, 6, 0, 2),
            TextWrapping = TextWrapping.Wrap,
        };

    private static TextBlock Bullet(string text)
    {
        var tb = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(10, 1, 0, 1),
        };
        tb.Inlines = new InlineCollection { new Run("• ") };
        AddInlines(tb, text);
        return tb;
    }

    private static TextBlock Paragraph(string text)
    {
        var tb = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 1, 0, 1),
        };
        tb.Inlines = new InlineCollection();
        AddInlines(tb, text);
        return tb;
    }

    private static string StripInline(string s)
        => s.Replace("**", "").Replace("`", "");

    private static void AddInlines(TextBlock tb, string text)
    {
        var inlines = tb.Inlines ??= new InlineCollection();

        int last = 0;
        foreach (Match m in InlinePattern.Matches(text))
        {
            if (m.Index > last)
                inlines.Add(new Run(text[last..m.Index]));

            if (m.Groups[2].Success)
                inlines.Add(new Run(m.Groups[2].Value) { FontWeight = FontWeight.Bold });
            else if (m.Groups[3].Success)
                inlines.Add(new Run(m.Groups[3].Value)
                {
                    FontFamily = new FontFamily("Consolas,Menlo,monospace"),
                });

            last = m.Index + m.Length;
        }

        if (last < text.Length)
            inlines.Add(new Run(text[last..]));
    }
}
