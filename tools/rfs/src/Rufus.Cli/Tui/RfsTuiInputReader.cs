using System.Text;

namespace Rufus.Cli.Tui;

internal static class RfsTuiInputReader
{
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
        var hasRendered = false;

        Render(buffer, appendSeparator: false);
        hasRendered = true;

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
                    Render(buffer, appendSeparator: hasRendered);
                    hasRendered = true;
                }

                continue;
            }

            if (key.KeyChar == '\0' || char.IsControl(key.KeyChar) || char.IsSurrogate(key.KeyChar))
            {
                continue;
            }

            buffer.Append(key.KeyChar);
            Render(buffer, appendSeparator: hasRendered);
            hasRendered = true;
        }
    }

    private static void Render(StringBuilder buffer, bool appendSeparator)
    {
        if (appendSeparator)
        {
            Console.WriteLine();
        }

        RfsTuiRenderer.WritePrompt();
        Console.Write(buffer.ToString());
        Console.WriteLine();

        if (buffer.Length == 0 || buffer[0] != '/')
        {
            return;
        }

        var suggestions = RfsTuiCommandCatalog.GetSuggestions(buffer.ToString());
        RfsTuiRenderer.WriteCommandPalette(suggestions);
    }
}
