using System.Text;

namespace Rufus.Cli.Tui;

internal static class RfsTuiInputReader
{
    private const string MoveCursorUpOneLine = "\u001b[1F";
    private const string ClearLine = "\u001b[2K";

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

    private static bool CanUseLivePalette()
        => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    private static string? ReadInteractiveLine()
    {
        var buffer = new StringBuilder();
        var renderedLineCount = Render(buffer);

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.KeyChar == '\u0004')
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
                    renderedLineCount = Render(buffer, renderedLineCount);
                }

                continue;
            }

            if (key.KeyChar == '\0' || char.IsControl(key.KeyChar) || char.IsSurrogate(key.KeyChar))
            {
                continue;
            }

            buffer.Append(key.KeyChar);
            renderedLineCount = Render(buffer, renderedLineCount);
        }
    }

    private static int Render(StringBuilder buffer, int previousRenderedLineCount = 0)
    {
        if (previousRenderedLineCount > 0)
        {
            ClearRenderedBlock(previousRenderedLineCount);
        }

        var renderedLineCount = 1;

        RfsTuiRenderer.WritePrompt();
        Console.Write(buffer.ToString());
        Console.WriteLine();

        if (buffer.Length > 0 && buffer[0] == '/')
        {
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
