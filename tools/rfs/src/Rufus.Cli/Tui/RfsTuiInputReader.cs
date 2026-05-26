using System.Diagnostics;
using System.Text;

namespace Rufus.Cli.Tui;

internal static class RfsTuiInputReader
{
    private const string MoveCursorUpOneLine = "\u001b[1F";
    private const string ClearLine = "\u001b[2K";
    private const char ControlD = '\u0004';
    private const int RedirectedBurstWindowMs = 24;
    private const int RedirectedPollSliceMs = 4;
    private const int LongInputThresholdChars = 1200;

    internal static string? ReadLine()
    {
        if (!CanUseLivePalette())
        {
            return ReadRedirectedLine();
        }

        try
        {
            return ReadInteractiveLine();
        }
        catch (InvalidOperationException)
        {
            return ReadRedirectedLine();
        }
        catch (IOException)
        {
            return ReadRedirectedLine();
        }
        catch (ArgumentOutOfRangeException)
        {
            return ReadRedirectedLine();
        }
    }

    internal static bool ShouldUseCommandPalette(string buffer)
        => buffer.Length > 0 && buffer[0] == '/';

    private static bool CanUseLivePalette()
        => RfsTuiTerminal.UseLivePalette;

    private static string? ReadRedirectedLine()
    {
        var firstLine = Console.ReadLine();
        if (firstLine is null)
        {
            return null;
        }

        if (firstLine.Length >= LongInputThresholdChars)
        {
            return firstLine;
        }

        if (!Console.IsInputRedirected)
        {
            return firstLine;
        }

        if (TryReadRedirectedBurst(firstLine, out var burst))
        {
            return burst;
        }

        return firstLine;
    }

    private static bool TryReadRedirectedBurst(string firstLine, out string burst)
    {
        var lines = new List<string> { firstLine };
        var sawAdditionalLine = false;
        var idleWindow = Stopwatch.StartNew();

        while (idleWindow.ElapsedMilliseconds < RedirectedBurstWindowMs)
        {
            if (Console.In.Peek() < 0)
            {
                Thread.Sleep(RedirectedPollSliceMs);
                continue;
            }

            var nextLine = Console.ReadLine();
            if (nextLine is null)
            {
                break;
            }

            lines.Add(nextLine);
            sawAdditionalLine = true;
            idleWindow.Restart();
        }

        burst = sawAdditionalLine ? string.Join('\n', lines) : firstLine;
        return sawAdditionalLine;
    }

    private static string? ReadInteractiveLine()
    {
        var buffer = new StringBuilder();
        var renderedLineCount = 1;
        var commandPaletteVisible = false;

        RfsTuiRenderer.WritePrompt();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.KeyChar == ControlD)
            {
                Console.WriteLine();
                return null;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return buffer.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                    var nextShouldUseCommandPalette = ShouldUseCommandPalette(buffer.ToString());
                    if (commandPaletteVisible || nextShouldUseCommandPalette)
                    {
                        renderedLineCount = Render(buffer, renderedLineCount);
                    }
                    else
                    {
                        Console.Write("\b \b");
                    }

                    commandPaletteVisible = nextShouldUseCommandPalette;
                }

                continue;
            }

            if (key.KeyChar == '\0' || char.IsControl(key.KeyChar) || char.IsSurrogate(key.KeyChar))
            {
                continue;
            }

            buffer.Append(key.KeyChar);
            var shouldUseCommandPalette = ShouldUseCommandPalette(buffer.ToString());
            if (commandPaletteVisible || shouldUseCommandPalette)
            {
                renderedLineCount = Render(buffer, renderedLineCount);
            }
            else
            {
                Console.Write(key.KeyChar);
            }

            commandPaletteVisible = shouldUseCommandPalette;
        }
    }

    private static int Render(StringBuilder buffer, int previousRenderedLineCount = 0)
    {
        if (!RfsTuiTerminal.UseCursorControl)
        {
            return 1;
        }

        if (previousRenderedLineCount > 0)
        {
            ClearRenderedBlock(previousRenderedLineCount);
        }

        var renderedLineCount = 1;
        var showCommandPalette = ShouldUseCommandPalette(buffer.ToString());

        RfsTuiRenderer.WritePrompt();
        Console.Write(buffer.ToString());

        if (showCommandPalette)
        {
            Console.WriteLine();
            renderedLineCount += RfsTuiRenderer.WriteCommandPalette(RfsTuiCommandCatalog.GetSuggestions(buffer.ToString()));
        }

        return renderedLineCount;
    }

    private static void ClearRenderedBlock(int lineCount)
    {
        if (!RfsTuiTerminal.UseCursorControl)
        {
            return;
        }

        for (var i = 0; i < lineCount; i++)
        {
            Console.Write(MoveCursorUpOneLine);
            Console.Write(ClearLine);
        }
    }
}
