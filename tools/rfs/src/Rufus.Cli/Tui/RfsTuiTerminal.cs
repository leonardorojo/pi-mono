namespace Rufus.Cli.Tui;

internal static class RfsTuiTerminal
{
    internal static bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    internal static bool UseColor => IsInteractive && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NO_COLOR"));

    internal static bool UseCursorControl => IsInteractive;

    internal static bool UseLivePalette => IsInteractive;

    internal static void ClearIfInteractive()
    {
        if (!IsInteractive)
        {
            return;
        }

        Console.Clear();
    }
}