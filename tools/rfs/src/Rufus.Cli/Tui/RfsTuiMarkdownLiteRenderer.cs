using System.Text;
using System.Text.RegularExpressions;

namespace Rufus.Cli.Tui;

internal static class RfsTuiMarkdownLiteRenderer
{
    private static readonly Regex SimpleExponentRegex = new(@"\^(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static string Render(string? markdown, bool useAnsi)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n', StringSplitOptions.None);
        var output = new StringBuilder(normalized.Length + 32);
        var inCodeFence = false;

        foreach (var rawLine in lines)
        {
            if (IsFenceLine(rawLine))
            {
                inCodeFence = !inCodeFence;
                AppendLine(output, rawLine);
                continue;
            }

            if (inCodeFence)
            {
                AppendLine(output, rawLine);
                continue;
            }

            if (string.IsNullOrWhiteSpace(rawLine))
            {
                AppendLine(output, string.Empty);
                continue;
            }

            if (TryRenderHeading(rawLine, useAnsi, out var headingLine, out var underlineLine))
            {
                AppendLine(output, headingLine);
                if (underlineLine.Length > 0)
                {
                    AppendLine(output, underlineLine);
                }

                continue;
            }

            if (TryRenderBullet(rawLine, useAnsi, out var bulletLine))
            {
                AppendLine(output, bulletLine);
                continue;
            }

            if (TryRenderNumbered(rawLine, useAnsi, out var numberedLine))
            {
                AppendLine(output, numberedLine);
                continue;
            }

            AppendLine(output, RenderInline(rawLine, useAnsi));
        }

        return output.ToString().TrimEnd('\n');
    }

    private static bool IsFenceLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal);
    }

    private static bool TryRenderHeading(string line, bool useAnsi, out string headingLine, out string underlineLine)
    {
        headingLine = string.Empty;
        underlineLine = string.Empty;

        var span = line.AsSpan();
        var leading = 0;
        while (leading < span.Length && span[leading] == ' ' && leading < 3)
        {
            leading++;
        }

        var hashCount = 0;
        while (leading + hashCount < span.Length && hashCount < 6 && span[leading + hashCount] == '#')
        {
            hashCount++;
        }

        if (hashCount == 0)
        {
            return false;
        }

        var afterHashes = leading + hashCount;
        if (afterHashes >= span.Length || span[afterHashes] != ' ')
        {
            return false;
        }

        var content = span[(afterHashes + 1)..].ToString();
        content = RenderInline(content, useAnsi).Trim();
        if (content.Length == 0)
        {
            return false;
        }

        headingLine = useAnsi ? ApplyBold(content) : content;
        underlineLine = new string(GetHeadingUnderline(hashCount), content.Length);
        return true;
    }

    private static bool TryRenderBullet(string line, bool useAnsi, out string rendered)
    {
        rendered = string.Empty;
        var trimmed = line.TrimStart();
        var indentLength = line.Length - trimmed.Length;
        if (trimmed.Length < 2 || (trimmed[0] != '-' && trimmed[0] != '*') || trimmed[1] != ' ')
        {
            return false;
        }

        var content = RenderInline(trimmed[2..], useAnsi).TrimEnd();
        rendered = new string(' ', Math.Min(indentLength, 2)) + "• " + content;
        return true;
    }

    private static bool TryRenderNumbered(string line, bool useAnsi, out string rendered)
    {
        rendered = string.Empty;
        var trimmed = line.TrimStart();
        var indentLength = line.Length - trimmed.Length;

        var dotIndex = trimmed.IndexOf('.');
        if (dotIndex <= 0)
        {
            return false;
        }

        if (!int.TryParse(trimmed[..dotIndex], out var number))
        {
            return false;
        }

        if (dotIndex + 1 >= trimmed.Length || trimmed[dotIndex + 1] != ' ')
        {
            return false;
        }

        var content = RenderInline(trimmed[(dotIndex + 2)..], useAnsi).TrimEnd();
        rendered = new string(' ', Math.Min(indentLength, 2)) + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ". " + content;
        return true;
    }

    private static string RenderInline(string text, bool useAnsi)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == '`')
            {
                var closing = text.IndexOf('`', index + 1);
                if (closing > index)
                {
                    var code = text.Substring(index + 1, closing - index - 1);
                    builder.Append(useAnsi ? ApplyInlineCode(code) : code);
                    index = closing + 1;
                    continue;
                }
            }

            if (index + 1 < text.Length && text[index] == '*' && text[index + 1] == '*')
            {
                var closing = text.IndexOf("**", index + 2, StringComparison.Ordinal);
                if (closing > index)
                {
                    var bold = text.Substring(index + 2, closing - index - 2);
                    builder.Append(useAnsi ? ApplyBold(RenderInline(bold, useAnsi)) : RenderInline(bold, useAnsi));
                    index = closing + 2;
                    continue;
                }
            }

            builder.Append(text[index]);
            index++;
        }

        return NormalizeLatexSimple(builder.ToString());
    }

    private static string NormalizeLatexSimple(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var cleaned = text
            .Replace(@"\[", string.Empty, StringComparison.Ordinal)
            .Replace(@"\]", string.Empty, StringComparison.Ordinal)
            .Replace(@"\(", string.Empty, StringComparison.Ordinal)
            .Replace(@"\)", string.Empty, StringComparison.Ordinal)
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Trim();

        return SimpleExponentRegex.Replace(cleaned, match => match.Groups[1].Value.Length == 0 ? string.Empty : ConvertDigitsToSuperscript(match.Groups[1].Value));
    }

    private static string ConvertDigitsToSuperscript(string digits)
    {
        var builder = new StringBuilder(digits.Length);
        foreach (var digit in digits)
        {
            builder.Append(digit switch
            {
                '0' => '⁰',
                '1' => '¹',
                '2' => '²',
                '3' => '³',
                '4' => '⁴',
                '5' => '⁵',
                '6' => '⁶',
                '7' => '⁷',
                '8' => '⁸',
                '9' => '⁹',
                _ => digit,
            });
        }

        return builder.ToString();
    }

    private static void AppendLine(StringBuilder builder, string line)
    {
        builder.Append(line);
        builder.Append('\n');
    }

    private static char GetHeadingUnderline(int level)
        => level switch
        {
            1 => '─',
            2 => '─',
            _ => '─',
        };

    private static string ApplyBold(string text)
        => $"\u001b[1m{text}\u001b[0m";

    private static string ApplyInlineCode(string text)
        => $"\u001b[36m{text}\u001b[0m";
}
