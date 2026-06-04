using System.Diagnostics;
using System.Text.Json;
using Rufus.Cli.Tui;
using Rufus.RCK.Workspace;

internal static class RfsTuiHermesRunRecordingChecks
{
    internal static async Task RunAsync(List<string> failures)
    {
        await RunRecordingSuccessCaseAsync("hermes run auto-record success", failures);
        await RunFailedRunCaseAsync("hermes run failure skips record", failures);
    }

    private static async Task RunRecordingSuccessCaseAsync(string name, List<string> failures)
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
            var runner = new FakeHermesRunner(CreateRunnerResult(RfsTuiHermesRunHealth.Completed));

            var output = await RunCommandAsync(statusBefore, sessionState, runner, failures, name);
            AssertNoStderr(name, output.Stderr, failures);
            AssertNoRecordingPrompt(name, output.Stdout, failures);
            AssertRecordedPromptOutcome(name, output.Stdout, expectRecorded: true, failures);
            AssertCountsIncreasedByOne(name, statusBefore, RckWorkspaceStatusReader.Read(tempRoot), failures);
            AssertRunnerInputs(name, runner, tempRoot, failures);

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

    private static async Task RunFailedRunCaseAsync(string name, List<string> failures)
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
            var runner = new FakeHermesRunner(CreateRunnerResult(RfsTuiHermesRunHealth.ExitedWithError));

            var output = await RunCommandAsync(statusBefore, sessionState, runner, failures, name);
            AssertNoStderr(name, output.Stderr, failures);
            AssertNoRecordingPrompt(name, output.Stdout, failures);
            AssertRecordedPromptOutcome(name, output.Stdout, expectRecorded: false, failures);
            AssertCountsUnchanged(name, statusBefore, RckWorkspaceStatusReader.Read(tempRoot), failures);
            AssertRunnerInputs(name, runner, tempRoot, failures);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task<(string Stdout, string Stderr)> RunCommandAsync(
        RckWorkspaceStatus status,
        RfsTuiSessionState sessionState,
        FakeHermesRunner runner,
        List<string> failures,
        string name)
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var stdin = new StringReader(string.Empty);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetIn(stdin);
            Console.SetOut(stdout);
            Console.SetError(stderr);

            var handled = await RfsTuiHermesRunCommand.ExecuteAsync(status, sessionState, runner);
            if (!handled)
            {
                failures.Add($"[{name}] expected /hermes run command to report handled=true.");
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

    private static void AssertNoRecordingPrompt(string name, string stdout, List<string> failures)
    {
        if (stdout.Contains("Record Hermes response into RCK?", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected /hermes run to avoid any recording prompt.");
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
        var recordedMessage = stdout.Contains("Recorded Hermes run State + Delta:", StringComparison.Ordinal);
        var skippedMessage = stdout.Contains("Hermes run did not produce a recordable final answer; State + Delta not recorded.", StringComparison.Ordinal);

        if (expectRecorded)
        {
            if (!recordedMessage)
            {
                failures.Add($"[{name}] expected recorded State + Delta output.");
            }

            if (skippedMessage)
            {
                failures.Add($"[{name}] expected no skipped-recording warning when recording.");
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
                failures.Add($"[{name}] expected the skipped-recording warning when the run was not recordable.");
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

    private static void AssertRunnerInputs(string name, FakeHermesRunner runner, string tempRoot, List<string> failures)
    {
        if (!string.Equals(runner.WorkingDirectory, tempRoot, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected runner working directory to be '{tempRoot}' but got '{runner.WorkingDirectory}'.");
        }

        if (string.IsNullOrWhiteSpace(runner.LastPrompt))
        {
            failures.Add($"[{name}] expected runner to receive an operational Hermes prompt.");
        }

        if (runner.GitStatusBefore is null)
        {
            failures.Add($"[{name}] expected runner to receive captured git status before the run.");
        }
    }

    private static void AssertRecordedPayload(
        string name,
        string tempRoot,
        string stateId,
        string deltaId,
        string prompt,
        RfsTuiHermesRunResult result,
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
        AssertJsonString(name, statePayload.GetProperty("interaction"), "mode", "tui-hermes-run", failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "prompt", prompt, failures);
        AssertJsonStringNormalizedWhitespace(name, statePayload.GetProperty("interaction"), "answer", recordedAnswer, failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "provider", "github-copilot", failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "model", "gpt-5.4-mini", failures);

        using var deltaDocument = JsonDocument.Parse(File.ReadAllText(deltaPath));
        var deltaValueJson = deltaDocument.RootElement.GetProperty("ops")[0].GetProperty("valueJson").GetString() ?? string.Empty;
        using var deltaPayloadDocument = JsonDocument.Parse(deltaValueJson);
        var deltaPayload = deltaPayloadDocument.RootElement;

        AssertJsonString(name, deltaPayload.GetProperty("cause"), "type", "llm-interaction", failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "mode", "tui-hermes-run", failures);
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
        sessionState.SetSessionModel("gpt-5.4-mini", "github-copilot");
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

    private static RfsTuiHermesRunResult CreateRunnerResult(RfsTuiHermesRunHealth health)
        => new(
            Stdout: "Hermes output",
            Stderr: health == RfsTuiHermesRunHealth.ExitedWithError ? "Hermes exited with error." : string.Empty,
            ExitCode: health == RfsTuiHermesRunHealth.Completed ? 0 : 1,
            StartedAt: DateTimeOffset.UtcNow.AddSeconds(-2),
            FinishedAt: DateTimeOffset.UtcNow,
            DurationMs: 200,
            TimedOut: health == RfsTuiHermesRunHealth.TimedOut,
            WorkingDirectory: string.Empty,
            GitStatusBefore: string.Empty,
            GitStatusAfter: string.Empty,
            DirtyStateChanged: false,
            PromptBytes: 1234,
            Health: health);

    private static string CreateTempRoot(string name)
        => Path.Combine(Path.GetTempPath(), "rfs-hermes-run-record-checks", name.Replace(' ', '-'), Guid.NewGuid().ToString("N"));

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

    private sealed class FakeHermesRunner : IRfsTuiHermesRunner
    {
        public FakeHermesRunner(RfsTuiHermesRunResult result)
        {
            Result = result;
        }

        public RfsTuiHermesRunResult Result { get; }
        public string WorkingDirectory { get; private set; } = string.Empty;
        public string Prompt { get; private set; } = string.Empty;
        public string? GitStatusBefore { get; private set; }
        public string? GitStatusAfter { get; private set; }
        public RfsTuiHermesRunOptions? Options { get; private set; }
        public string LastPrompt => Prompt;

        public Task<string> CaptureGitStatusAsync(string workingDirectory, CancellationToken cancellationToken = default)
        {
            WorkingDirectory = workingDirectory;
            GitStatusBefore = string.Empty;
            return Task.FromResult(string.Empty);
        }

        public Task<RfsTuiHermesRunResult> RunAsync(
            string workingDirectory,
            string prompt,
            string? gitStatusBefore = null,
            RfsTuiHermesRunOptions? options = null,
            RfsTuiHermesRunProgressReporter? progressReporter = null,
            CancellationToken cancellationToken = default)
        {
            WorkingDirectory = workingDirectory;
            Prompt = prompt;
            GitStatusBefore = gitStatusBefore;
            GitStatusAfter = gitStatusBefore;
            Options = options;
            return Task.FromResult(Result);
        }
    }
}
