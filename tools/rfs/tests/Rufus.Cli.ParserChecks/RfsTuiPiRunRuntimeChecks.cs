using System.IO;
using Rufus.Cli.PiIntegration;
using Rufus.Cli.Tui;

internal static class RfsTuiPiRunRuntimeChecks
{
    internal static void Run(List<string> failures)
    {
        RunTextProgressAggregationCase(failures);
        RunToolEventSummaryCase(failures);
        RunTurnStartSuppressionCase(failures);
        RunFinalAnswerAnnouncementCase(failures);
    }

    private static void RunTextProgressAggregationCase(List<string> failures)
    {
        var originalOut = Console.Out;
        using var stdout = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("session"));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("message_update", Text: new string('a', 600)));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("message_update", Text: new string('b', 600)));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("message_update", Text: new string('c', 600)));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("message_update", Text: string.Empty));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = stdout.ToString();
        if (output.Contains("[Pi run] text_delta", StringComparison.Ordinal) || output.Contains("text_delta:", StringComparison.Ordinal) || output.Contains("receiving text...", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected text_delta and chars-based progress to be suppressed.");
        }

        foreach (var fragment in new[]
                 {
                     "[Pi run] session started",
                     "[Pi run] processing...",
                 })
        {
            if (!output.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[pi run runtime] expected output fragment '{fragment}'.");
            }
        }
    }

    private static void RunToolEventSummaryCase(List<string> failures)
    {
        var originalOut = Console.Out;
        using var stdout = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("tool_execution_start"));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("tool_execution_start", Name: "read_file", Details: "README.md"));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("tool_execution_end", Name: "read_file", Summary: "ok"));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = stdout.ToString();
        if (output.Contains("tool_execution_start: (none)", StringComparison.Ordinal) || output.Contains("tool_execution_end: (none)", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected tool events without useful data to be suppressed.");
        }

        if (!output.Contains("[Pi run] tool started: read_file · README.md", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected useful tool start events to be rendered compactly.");
        }

        if (!output.Contains("[Pi run] tool completed: read_file · ok", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected useful tool completion events to be rendered compactly.");
        }

        if (output.Contains("[Pi run] tool updated:", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected no tool update lines in the compact summary path.");
        }
    }

    private static void RunTurnStartSuppressionCase(List<string> failures)
    {
        var originalOut = Console.Out;
        using var stdout = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("session"));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("agent_start"));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("turn_start"));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("turn_start"));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("turn_start"));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = stdout.ToString();
        var turnStartCount = output.Split("[Pi run] turn started", StringSplitOptions.None).Length - 1;
        if (turnStartCount != 0)
        {
            failures.Add($"[pi run runtime] expected turn_start to be suppressed, but saw {turnStartCount} lines.");
        }

        if (!output.Contains("[Pi run] session started", StringComparison.Ordinal) || !output.Contains("[Pi run] agent started", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected session and agent banners to remain visible.");
        }
    }

    private static void RunFinalAnswerAnnouncementCase(List<string> failures)
    {
        var originalOut = Console.Out;
        using var stdout = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("session"));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("message_update", Text: new string('x', 200)));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("message_end"));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("agent_end"));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = stdout.ToString();
        if (output.Contains("[Pi run] final answer received", StringComparison.Ordinal) || output.Contains("final answer received", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected final answer announcement to be suppressed.");
        }

        if (!output.Contains("[Pi run] completed", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected completion announcement.");
        }

        var finalAnswerCount = output.Split("[Pi run] final answer received", StringSplitOptions.None).Length - 1;
        if (finalAnswerCount != 0)
        {
            failures.Add($"[pi run runtime] expected no final answer announcement but found {finalAnswerCount}.");
        }
    }
}
