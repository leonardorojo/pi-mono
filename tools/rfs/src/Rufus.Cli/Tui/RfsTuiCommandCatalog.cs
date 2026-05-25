using System.Collections.Generic;
using System.Linq;

namespace Rufus.Cli.Tui;

internal enum RfsTuiCommandKind
{
    Status,
    Log,
    ModelShow,
    ModelSet,
    Context,
    Trace,
    Hermes,
    Anchor,
    Help,
    Exit,
}

internal enum RfsTuiCommandMatchMode
{
    Exact,
    ExactOrArguments,
    ArgumentsOnly,
}

internal sealed record RfsTuiCommandInfo(
    RfsTuiCommandKind Kind,
    string Name,
    string Usage,
    string Description,
    RfsTuiCommandMatchMode MatchMode = RfsTuiCommandMatchMode.Exact,
    IReadOnlyList<string>? Aliases = null)
{
    public bool IsPrefixMatch(string input)
    {
        var normalized = Normalize(input);
        if (normalized.Length == 0 || !normalized.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        if (Name.StartsWith(normalized, StringComparison.Ordinal))
        {
            return true;
        }

        return Aliases?.Any(alias => $"/{alias}".StartsWith(normalized, StringComparison.Ordinal)) == true;
    }

    public bool MatchesExactInput(string input)
    {
        var normalized = Normalize(input);
        if (normalized.Length == 0)
        {
            return false;
        }

        return MatchMode switch
        {
            RfsTuiCommandMatchMode.Exact => MatchesExactToken(normalized),
            RfsTuiCommandMatchMode.ExactOrArguments => MatchesExactToken(normalized) || MatchesArguments(normalized),
            RfsTuiCommandMatchMode.ArgumentsOnly => MatchesArguments(normalized),
            _ => false,
        };
    }

    private bool MatchesExactToken(string normalized)
    {
        if (string.Equals(normalized, Name, StringComparison.Ordinal))
        {
            return true;
        }

        if (Aliases is null)
        {
            return false;
        }

        foreach (var alias in Aliases)
        {
            if (string.Equals(normalized, alias, StringComparison.Ordinal) || string.Equals(normalized, $"/{alias}", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesArguments(string normalized)
    {
        if (normalized.StartsWith(Name + " ", StringComparison.Ordinal))
        {
            return true;
        }

        if (Aliases is null)
        {
            return false;
        }

        foreach (var alias in Aliases)
        {
            if (normalized.StartsWith(alias + " ", StringComparison.Ordinal) || normalized.StartsWith($"/{alias} ", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string input)
        => input.Trim();
}

internal static class RfsTuiCommandCatalog
{
    private static readonly RfsTuiCommandInfo[] Commands =
    [
        new(RfsTuiCommandKind.Status, "/status", "/status", "Show session status"),
        new(RfsTuiCommandKind.Log, "/log", "/log", "Show recent RCK interactions"),
        new(RfsTuiCommandKind.ModelShow, "/model", "/model", "Open session model picker"),
        new(RfsTuiCommandKind.ModelSet, "/model", "/model <model>", "Set session model (temporary)", RfsTuiCommandMatchMode.ArgumentsOnly),
        new(RfsTuiCommandKind.Context, "/context", "/context", "Show last context summary"),
        new(RfsTuiCommandKind.Trace, "/trace", "/trace", "Show last TraceSlice summary"),
        new(RfsTuiCommandKind.Anchor, "/anchor", "/anchor \"name\"", "Create milestone anchor", RfsTuiCommandMatchMode.ExactOrArguments),
        new(RfsTuiCommandKind.Help, "/help", "/help", "Show this help"),
        new(RfsTuiCommandKind.Hermes, "/hermes", "/hermes", "Build Hermes handoff draft"),
        new(RfsTuiCommandKind.Exit, "/exit", "/exit", "Exit RFS", RfsTuiCommandMatchMode.Exact, ["exit"]),
    ];

    internal static IReadOnlyList<RfsTuiCommandInfo> GetHelpCommands()
        => Commands;

    internal static IReadOnlyList<RfsTuiCommandInfo> GetSuggestions(string input)
        => Commands.Where(command => command.IsPrefixMatch(input)).ToArray();

    internal static RfsTuiCommandInfo? FindExactMatch(string input)
        => Commands.FirstOrDefault(command => command.MatchesExactInput(input));
}
