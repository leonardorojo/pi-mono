namespace Rufus.Cli.Tui;

internal static class RfsTuiTerminal
{
    internal static bool IsInteractive => !IsPlainOverrideEnabled() && !Console.IsInputRedirected && !Console.IsOutputRedirected;

    internal static bool UseColor => IsInteractive && IsAnsiDisabled() is false;

    internal static bool UseCursorControl => IsInteractive;

    internal static bool UseLivePalette => IsInteractive;

    internal static bool UseAnsiSgr => UseColor;

    internal static bool UseAnsiStyle => UseAnsiSgr;

    internal static void ClearIfInteractive()
    {
        if (!IsInteractive)
        {
            return;
        }

        Console.Clear();
    }

    private static bool IsAnsiDisabled()
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NO_COLOR"));

    private static bool IsPlainOverrideEnabled()
    {
        var value = Environment.GetEnvironmentVariable("RFS_TUI_PLAIN");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";
    }
}
