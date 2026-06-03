using System.Diagnostics;
using System.Text.Json;
using Rufus.Cli.PiIntegration;
using Rufus.Cli.Tui;
using Rufus.RCK.Workspace;

internal static class RfsTuiPiRunRecordingChecks
{
    internal static async Task RunAsync(List<string> failures)
    {
        await RunRecordingDeclineCaseAsync("pi run record prompt enter", "\n", expectPromptVisible: true, expectRecorded: false, health: RfsTuiPiRunHealth.Completed, failures: failures);
        await RunRecordingDeclineCaseAsync("pi run record prompt n", "n\n", expectPromptVisible: true, expectRecorded: false, health: RfsTuiPiRunHealth.Completed, failures: failures);
        await RunRecordingAcceptCaseAsync("pi run record prompt y", "y\n", failures);
        await RunFailedRunCaseAsync("pi run failure skips record prompt", "y\n", failures);
    }

    private static async Task RunRecordingDeclineCaseAsync(
        string name,
        string input,
        bool expectPromptVisible,
        bool expectRecorded,
        RfsTuiPiRunHealth health,
        List<string> failures)
    {
        var tempRoot = CreateTempRoot(name);
        try
        {
            if (!await InitializeTempGitRepoAndRckAsync(name, tempRoot, failures))
            {
                return;
            }

            var sessionState = CreateSessionState();
            var statusBefore = RckWorkspaceStatusReader.Read(tempRoot);
            var runner = new FakePiRunner(CreateRunnerResult(health));

            var output = await RunCommandAsync(tempRoot, input, statusBefore, sessionState, runner, failures, name);
            AssertNoStderr(name, output.Stderr, failures);
            AssertPromptVisibility(name, output.Stdout, expectPromptVisible, failures);
            AssertRecordedPromptOutcome(name, output.Stdout, expectRecorded: false, failures);
            AssertCountsUnchanged(name, statusBefore, RckWorkspaceStatusReader.Read(tempRoot), failures);
            AssertRunnerInputs(name, runner, tempRoot, sessionState, failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunRecordingAcceptCaseAsync(string name, string input, List<string> failures)
    {
        var tempRoot = CreateTempRoot(name);
        try
        {
            if (!await InitializeTempGitRepoAndRckAsync(name, tempRoot, failures))
            {
                return;
            }

            var sessionState = CreateSessionState();
            var statusBefore = RckWorkspaceStatusReader.Read(tempRoot);
            var runner = new FakePiRunner(CreateRunnerResult(RfsTuiPiRunHealth.Completed));

            var output = await RunCommandAsync(tempRoot, input, statusBefore, sessionState, runner, failures, name);
            AssertNoStderr(name, output.Stderr, failures);
            AssertPromptVisibility(name, output.Stdout, expectPromptVisible: true, failures: failures);
            AssertRecordedPromptOutcome(name, output.Stdout, expectRecorded: true, failures);
            AssertCountsIncreasedByOne(name, statusBefore, RckWorkspaceStatusReader.Read(tempRoot), failures);
            AssertRunnerInputs(name, runner, tempRoot, sessionState, failures);

            var stateId = ExtractRequiredToken(output.Stdout, "state:", name, failures);
            var deltaId = ExtractRequiredToken(output.Stdout, "delta:", name, failures);
            if (stateId is null || deltaId is null)
            {
                return;
            }

            AssertRecordedPayload(name, tempRoot, stateId, deltaId, runner.LastPrompt, runner.Result, failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunFailedRunCaseAsync(string name, string input, List<string> failures)
    {
        var tempRoot = CreateTempRoot(name);
        try
        {
            if (!await InitializeTempGitRepoAndRckAsync(name, tempRoot, failures))
            {
                return;
            }

            var sessionState = CreateSessionState();
            var statusBefore = RckWorkspaceStatusReader.Read(tempRoot);
            var runner = new FakePiRunner(CreateRunnerResult(RfsTuiPiRunHealth.ExitedWithError));

            var output = await RunCommandAsync(tempRoot, input, statusBefore, sessionState, runner, failures, name);
            AssertNoStderr(name, output.Stderr, failures);
            if (output.Stdout.Contains("Record Pi response into RCK? [y/N]:", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected failed runs to skip the record prompt.");
            }

            if (output.Stdout.Contains("Recorded Pi run State + Delta:", StringComparison.Ordinal) ||
                output.Stdout.Contains("Pi response was not recorded.", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected failed runs to avoid recording output entirely.");
            }

            AssertCountsUnchanged(name, statusBefore, RckWorkspaceStatusReader.Read(tempRoot), failures);
            AssertRunnerInputs(name, runner, tempRoot, sessionState, failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task<(string Stdout, string Stderr)> RunCommandAsync(
        string tempRoot,
        string input,
        RckWorkspaceStatus status,
        RfsTuiSessionState sessionState,
        FakePiRunner runner,
        List<string> failures,
        string name)
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var stdin = new StringReader(input);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetIn(stdin);
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var handled = await RfsTuiPiRunCommand.ExecuteAsync(status, sessionState, runner);
            if (!handled)
            {
                failures.Add($"[{name}] expected /pi run command to report handled=true.");
                return (string.Empty, string.Empty);
            }
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }

        return (stdout.ToString(), stderr.ToString());
    }

    private static void AssertPromptVisibility(string name, string stdout, bool expectPromptVisible, List<string> failures)
    {
        var promptVisible = stdout.Contains("Record Pi response into RCK? [y/N]:", StringComparison.Ordinal);
        if (promptVisible != expectPromptVisible)
        {
            failures.Add($"[{name}] expected record prompt visibility to be {expectPromptVisible} but got {promptVisible}.");
        }
    }

    private static void AssertNoStderr(string name, string stderr, List<string> failures)
    {
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {stderr.Trim()}.");
        }
    }

    private static void AssertRecordedPromptOutcome(string name, string stdout, bool expectRecorded, List<string> failures)
    {
        var recordedMessage = stdout.Contains("Recorded Pi run State + Delta:", StringComparison.Ordinal);
        var skippedMessage = stdout.Contains("Pi response was not recorded.", StringComparison.Ordinal);

        if (expectRecorded)
        {
            if (!recordedMessage)
            {
                failures.Add($"[{name}] expected recorded State + Delta output.");
            }

            if (skippedMessage)
            {
                failures.Add($"[{name}] expected no 'Pi response was not recorded.' message when recording.");
            }
        }
        else
        {
            if (recordedMessage)
            {
                failures.Add($"[{name}] expected no recorded State + Delta output.");
            }

            if (!skippedMessage)
            {
                failures.Add($"[{name}] expected 'Pi response was not recorded.' when declining the record prompt.");
            }
        }
    }

    private static void AssertCountsUnchanged(string name, RckWorkspaceStatus before, RckWorkspaceStatus after, List<string> failures)
    {
        if (after.StateCount != before.StateCount || after.DeltaCount != before.DeltaCount || after.AnchorCount != before.AnchorCount)
        {
            failures.Add($"[{name}] expected RCK counts to remain unchanged but got state {before.StateCount}->{after.StateCount}, delta {before.DeltaCount}->{after.DeltaCount}, anchor {before.AnchorCount}->{after.AnchorCount}.");
        }
    }

    private static void AssertCountsIncreasedByOne(string name, RckWorkspaceStatus before, RckWorkspaceStatus after, List<string> failures)
    {
        if (after.StateCount != before.StateCount + 1 || after.DeltaCount != before.DeltaCount + 1 || after.AnchorCount != before.AnchorCount)
        {
            failures.Add($"[{name}] expected RCK counts to increase by state +1 and delta +1 without anchors but got state {before.StateCount}->{after.StateCount}, delta {before.DeltaCount}->{after.DeltaCount}, anchor {before.AnchorCount}->{after.AnchorCount}.");
        }
    }

    private static void AssertRunnerInputs(string name, FakePiRunner runner, string tempRoot, RfsTuiSessionState sessionState, List<string> failures)
    {
        if (!string.Equals(runner.WorkingDirectory, tempRoot, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected runner working directory to be '{tempRoot}' but got '{runner.WorkingDirectory}'.");
        }

        if (!string.Equals(runner.WorkspaceModel, sessionState.ResolveMainModel(), StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected runner workspace model to match the session model.");
        }

        if (string.IsNullOrWhiteSpace(runner.LastPrompt))
        {
            failures.Add($"[{name}] expected runner to receive an operational Pi prompt.");
        }
    }

    private static void AssertRecordedPayload(
        string name,
        string tempRoot,
        string stateId,
        string deltaId,
        string prompt,
        RfsTuiPiRunResult result,
        List<string> failures)
    {
        var statePath = Path.Combine(tempRoot, ".rfs", "rck", "states", $"{stateId}.json");
        if (!File.Exists(statePath))
        {
            failures.Add($"[{name}] expected state file '{statePath}' to exist.");
            return;
        }

        var deltaPath = Path.Combine(tempRoot, ".rfs", "rck", "deltas", $"{deltaId}.json");
        if (!File.Exists(deltaPath))
        {
            failures.Add($"[{name}] expected delta file '{deltaPath}' to exist.");
            return;
        }

        using var stateDocument = JsonDocument.Parse(File.ReadAllText(statePath));
        var statePayloadJson = stateDocument.RootElement.GetProperty("payloadCanonicalJson").GetString() ?? string.Empty;
        using var statePayloadDocument = JsonDocument.Parse(statePayloadJson);
        var statePayload = statePayloadDocument.RootElement;

        var recordedAnswer = result.Stdout.Trim();
        AssertJsonString(name, statePayload.GetProperty("interaction"), "mode", "tui-direct", failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "prompt", prompt, failures);
        AssertJsonStringNormalizedWhitespace(name, statePayload.GetProperty("interaction"), "answer", recordedAnswer, failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "provider", result.Provider ?? string.Empty, failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "model", result.Model ?? string.Empty, failures);

        using var deltaDocument = JsonDocument.Parse(File.ReadAllText(deltaPath));
        var deltaValueJson = deltaDocument.RootElement.GetProperty("ops")[0].GetProperty("valueJson").GetString() ?? string.Empty;
        using var deltaPayloadDocument = JsonDocument.Parse(deltaValueJson);
        var deltaPayload = deltaPayloadDocument.RootElement;

        AssertJsonString(name, deltaPayload.GetProperty("cause"), "type", "llm-interaction", failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "mode", "tui-direct", failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "prompt", prompt, failures);
        AssertJsonStringNormalizedWhitespace(name, deltaPayload.GetProperty("cause"), "answer", recordedAnswer, failures);
    }

    private static void AssertJsonString(string name, JsonElement element, string propertyName, string expected, List<string> failures)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            failures.Add($"[{name}] expected JSON string property '{propertyName}'.");
            return;
        }

        var actual = property.GetString();
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected JSON property '{propertyName}' to be '{expected}' but got '{actual}'.");
        }
    }

    private static void AssertJsonStringNormalizedWhitespace(string name, JsonElement element, string propertyName, string expected, List<string> failures)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            failures.Add($"[{name}] expected JSON string property '{propertyName}'.");
            return;
        }

        var actual = property.GetString();
        if (!string.Equals(NormalizeWhitespace(actual), NormalizeWhitespace(expected), StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected JSON property '{propertyName}' to match normalized whitespace '{expected}' but got '{actual}'.");
        }
    }

    private static string NormalizeWhitespace(string? value)
        => string.Join(" ", (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? ExtractRequiredToken(string stdout, string label, string name, List<string> failures)
    {
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith(label, StringComparison.Ordinal))
            {
                continue;
            }

            var value = line[label.Length..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        failures.Add($"[{name}] expected stdout to contain a '{label}' line.");
        return null;
    }

    private static RfsTuiSessionState CreateSessionState()
    {
        var sessionState = new RfsTuiSessionState();
        sessionState.RecordComplete(
            new RfsTuiCompleteContextSummary(
                SelectionStrategy: "recent-chain-fallback",
                ValidationStatus: "accepted",
                ContextPackScope: "recent-chain-fallback",
                IntentSource: "complete",
                SelectedStateCount: 3,
                SelectedDeltaCount: 2,
                SelectedAnchorCount: 0,
                EstimatedChars: 512,
                EstimatedTokens: 128,
                TransportRisk: "low",
                Truncated: false,
                Warnings: Array.Empty<string>(),
                Omissions: Array.Empty<string>()),
            "Usando el Repo Snapshot generado por Pi en la interacción anterior, resumí el estado del repo.",
            "Complete prepared the operational prompt for Pi.");
        return sessionState;
    }

    private static RfsTuiPiRunResult CreateRunnerResult(RfsTuiPiRunHealth health)
        => new(
            Stdout: "Repo Snapshot:\n- tree clean\n- no anchors",
            Stderr: health == RfsTuiPiRunHealth.ExitedWithError ? "Pi exited with error." : string.Empty,
            ExitCode: health == RfsTuiPiRunHealth.Completed ? 0 : 1,
            StartedAt: DateTimeOffset.UtcNow.AddSeconds(-2),
            FinishedAt: DateTimeOffset.UtcNow,
            DurationMs: 200,
            TimedOut: health == RfsTuiPiRunHealth.TimedOut,
            Cancelled: health == RfsTuiPiRunHealth.Cancelled,
            FailedToStart: health == RfsTuiPiRunHealth.FailedToStart,
            WorkingDirectory: string.Empty,
            PromptBytes: 1234,
            Provider: "test-provider",
            Model: "test-model",
            ToolEvents: Array.Empty<PiJsonEventRunner.PiJsonToolEvent>(),
            Health: health);

    private static string CreateTempRoot(string name)
        => Path.Combine(Path.GetTempPath(), "rfs-pi-run-record-checks", name.Replace(' ', '-'), Guid.NewGuid().ToString("N"));

    private static async Task<bool> InitializeTempGitRepoAndRckAsync(string name, string tempRoot, List<string> failures)
    {
        Directory.CreateDirectory(tempRoot);
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return false;
        }

        var initResult = RckWorkspaceInitializer.Initialize(tempRoot);
        if (!initResult.Success)
        {
            failures.Add($"[{name}] expected RCK init to succeed but got: {initResult.ErrorMessage}");
            return false;
        }

        return true;
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(string workingDirectory, string fileName, string arguments)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {fileName} {arguments}.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class FakePiRunner : IRfsTuiPiRunner
    {
        public FakePiRunner(RfsTuiPiRunResult result)
        {
            Result = result;
        }

        public RfsTuiPiRunResult Result { get; }
        public string WorkingDirectory { get; private set; } = string.Empty;
        public string Prompt { get; private set; } = string.Empty;
        public string? WorkspaceModel { get; private set; }
        public string LastPrompt => Prompt;

        public Task<RfsTuiPiRunResult> RunAsync(
            string workingDirectory,
            string prompt,
            string? workspaceModel = null,
            Action<PiJsonStreamEvent>? eventReporter = null,
            CancellationToken cancellationToken = default)
        {
            WorkingDirectory = workingDirectory;
            Prompt = prompt;
            WorkspaceModel = workspaceModel;
            return Task.FromResult(Result);
        }
    }
}
