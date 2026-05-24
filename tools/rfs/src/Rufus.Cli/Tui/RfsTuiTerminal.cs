namespace Rufus.Cli.Tui;

internal static class RfsTuiTerminal
{
    internal static bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    internal static void ClearIfInteractive()
    {
        if (!IsInteractive)
        {
            return;
        }

        Console.Clear();
    }
}