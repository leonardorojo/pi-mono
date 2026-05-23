namespace Rufus.Cli.Tui;

public enum RfsTuiModeSelection
{
    Invalid = 0,
    Direct = 1,
    Simple = 2,
    Complete = 3,
    Plan = 4,
    Cancel = 5,
    Exit = 6,
}

public static class RfsTuiModeSelectionParser
{
    public static RfsTuiModeSelection ParseModeSelection(string input)
    {
        var normalized = input.Trim();
        if (normalized.Length == 0)
        {
            return RfsTuiModeSelection.Invalid;
        }

        if (string.Equals(normalized, "1", StringComparison.Ordinal))
        {
            return RfsTuiModeSelection.Direct;
        }

        if (string.Equals(normalized, "2", StringComparison.Ordinal))
        {
            return RfsTuiModeSelection.Simple;
        }

        if (string.Equals(normalized, "3", StringComparison.Ordinal))
        {
            return RfsTuiModeSelection.Complete;
        }

        if (string.Equals(normalized, "4", StringComparison.Ordinal))
        {
            return RfsTuiModeSelection.Plan;
        }

        if (string.Equals(normalized, "/cancel", StringComparison.Ordinal) || string.Equals(normalized, "cancel", StringComparison.Ordinal))
        {
            return RfsTuiModeSelection.Cancel;
        }

        if (string.Equals(normalized, "/exit", StringComparison.Ordinal) || string.Equals(normalized, "exit", StringComparison.Ordinal))
        {
            return RfsTuiModeSelection.Exit;
        }

        return RfsTuiModeSelection.Invalid;
    }
}
