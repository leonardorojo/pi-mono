using System.IO;
using Rufus.Cli.PiIntegration;
using Rufus.Cli.Tui;

internal static class RfsTuiPiRunRuntimeChecks
{
    internal static void Run(List<string> failures)
    {
        RunTextProgressAggregationCase(failures);
        RunToolEventSuppressionCase(failures);
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
        if (output.Contains("[Pi] text_delta", StringComparison.Ordinal) || output.Contains("text_delta:", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected compact text progress output instead of raw text_delta lines.");
        }

        foreach (var fragment in new[]
                 {
                     "[Pi] session started",
                     "[Pi] receiving text... 600 chars",
                     "[Pi] receiving text... 1,200 chars",
                     "[Pi] receiving text... 1,800 chars",
                 })
        {
            if (!output.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[pi run runtime] expected output fragment '{fragment}'.");
            }
        }

        if (output.Contains("[Pi] receiving text... 0 chars", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected empty text_delta to be suppressed.");
        }
    }

    private static void RunToolEventSuppressionCase(List<string> failures)
    {
        var originalOut = Console.Out;
        using var stdout = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("session"));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("tool_execution_start"));
            RfsTuiRenderer.WritePiRunRuntimeEvent(new PiJsonStreamEvent("tool_execution_start", Name: "read_file"));
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

        if (!output.Contains("[Pi] tool started: read_file", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected useful tool start events to be rendered compactly.");
        }

        if (!output.Contains("[Pi] tool completed: read_file · ok", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected useful tool completion events to be rendered compactly.");
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
        if (!output.Contains("[Pi] final answer received", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected final answer announcement.");
        }

        if (!output.Contains("[Pi] completed", StringComparison.Ordinal))
        {
            failures.Add("[pi run runtime] expected completion announcement.");
        }

        var finalAnswerCount = output.Split("[Pi] final answer received", StringSplitOptions.None).Length - 1;
        if (finalAnswerCount != 1)
        {
            failures.Add($"[pi run runtime] expected exactly one final answer announcement but found {finalAnswerCount}.");
        }
    }
}
