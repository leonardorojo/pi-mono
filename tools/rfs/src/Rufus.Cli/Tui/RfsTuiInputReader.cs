using System.Text;

namespace Rufus.Cli.Tui;

internal static class RfsTuiInputReader
{
    private const string MoveCursorUpOneLine = "\u001b[1F";
    private const string ClearLine = "\u001b[2K";
    private const char ControlD = '\u0004';

    internal static string? ReadLine()
    {
        if (!CanUseLivePalette())
        {
            return Console.ReadLine();
        }

        try
        {
            return ReadInteractiveLine();
        }
        catch (InvalidOperationException)
        {
            return Console.ReadLine();
        }
        catch (IOException)
        {
            return Console.ReadLine();
        }
        catch (ArgumentOutOfRangeException)
        {
            return Console.ReadLine();
        }
    }

    internal static bool ShouldUseCommandPalette(string buffer)
        => buffer.Length > 0 && buffer[0] == '/';

    private static bool CanUseLivePalette()
        => RfsTuiTerminal.IsInteractive;

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
        for (var i = 0; i < lineCount; i++)
        {
            Console.Write(MoveCursorUpOneLine);
            Console.Write(ClearLine);
        }
    }
}
