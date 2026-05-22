using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Rufus.Cli.PiIntegration;
using Rufus.Agenting;
using Rufus.Agenting.Intent;

var failures = new List<string>();

await RunCaseAsync(
    name: "structured final answer",
    fixtureMode: "structured",
    expectedSuccess: true,
    expectedAnswer: "structured answer",
    expectedProvider: "test-provider",
    expectedModel: "test-model",
    expectedErrorContains: null,
    failures);

await RunCaseAsync(
    name: "delta fallback with stderr separation",
    fixtureMode: "delta",
    expectedSuccess: true,
    expectedAnswer: "hello world",
    expectedProvider: null,
    expectedModel: null,
    expectedErrorContains: null,
    failures);

await RunCaseAsync(
    name: "invalid jsonl",
    fixtureMode: "invalid",
    expectedSuccess: false,
    expectedAnswer: null,
    expectedProvider: null,
    expectedModel: null,
    expectedErrorContains: "Invalid JSONL on line 1",
    failures);

await RunIntentInferenceCaseAsync(
    name: "intent inference success",
    task: new AgentTask(
        id: "task-1",
        kind: "infer-intent",
        goal: "Infer the operational intent from this prompt.",
        input: "Build a TraceSlice for the current diff and summarize evidence.",
        expectedOutput: "PromptIntent JSON"),
    expectedIntent: "build-trace-slice",
    failures);

await RunIntentInferenceFailureCaseAsync(
    name: "intent inference rejects unsupported kind",
    task: new AgentTask(
        id: "task-2",
        kind: "summarize-evidence",
        goal: "Summarize evidence for the diff.",
        input: "Summarize the diff evidence.",
        expectedOutput: null),
    expectedErrorContains: "Kind='infer-intent'",
    failures);

await RunIntentCliCaseAsync(
    name: "intent cli renders result",
    prompt: "Implement rfs show command",
    expectedIntent: "general-operational-intent",
    failures);

await RunTraceSliceCliCaseAsync(
    name: "trace slice cli renders deterministic json",
    prompt: "Implement rfs show command",
    failures);

await RunTraceSliceProposalCliCaseAsync(
    name: "trace slice proposal cli renders deterministic proposal json",
    prompt: "Implement rfs show command",
    failures);

await RunTraceSliceProposalLlmCliCaseAsync(
    name: "trace slice proposal llm cli renders proposal json",
    prompt: "Implement rfs show command",
    failures);

await RunTraceSliceValidateCliCaseAsync(
    name: "trace slice validate cli renders validated trace slice json",
    prompt: "Implement rfs show command",
    failures);

await RunTraceSliceValidateLlmCliCaseAsync(
    name: "trace slice validate llm cli renders validated trace slice json",
    prompt: "Implement rfs show command",
    failures);

await RunContextPackTraceSliceCliCaseAsync(
    name: "context pack trace-slice cli renders scoped json",
    prompt: "Implement rfs show command",
    failures);

await RunContextPackTraceSliceValidatedCliCaseAsync(
    name: "context pack trace-slice-validated cli renders validated scoped json",
    prompt: "Implement rfs show command",
    failures);

if (failures.Count > 0)
{
    foreach (var failure in failures)
    {
        Console.Error.WriteLine(failure);
    }

    return 1;
}

Console.WriteLine("PiJsonEventRunner parser checks passed.");
return 0;

static async Task RunCaseAsync(
    string name,
    string fixtureMode,
    bool expectedSuccess,
    string? expectedAnswer,
    string? expectedProvider,
    string? expectedModel,
    string? expectedErrorContains,
    List<string> failures)
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-pi-json-runner-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    var scriptPath = Path.Combine(tempRoot, "pi");
    var script = "#!/usr/bin/env bash\n" +
                 "set -euo pipefail\n" +
                 "case \"${PI_JSON_FIXTURE_MODE:-}\" in\n" +
                 "  structured)\n" +
                 "    cat <<'EOF'\n" +
                 "{\"type\":\"session\"}\n" +
                 "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":\"structured answer\"}]}}\n" +
                 "EOF\n" +
                 "    ;;\n" +
                 "  delta)\n" +
                 "    echo '{\"type\":\"session\"}'\n" +
                 "    echo '{\"type\":\"message_update\",\"assistantMessageEvent\":{\"type\":\"text_delta\",\"delta\":\"hello \"}}'\n" +
                 "    echo '{\"type\":\"message_update\",\"assistantMessageEvent\":{\"type\":\"text_delta\",\"delta\":\"world\"}}'\n" +
                 "    echo 'stderr line' >&2\n" +
                 "    ;;\n" +
                 "  invalid)\n" +
                 "    echo 'not-json'\n" +
                 "    ;;\n" +
                 "  *)\n" +
                 "    echo 'unexpected fixture mode' >&2\n" +
                 "    exit 1\n" +
                 "    ;;\n" +
                 "esac\n" +
                 "exit 0\n";

    await File.WriteAllTextAsync(scriptPath, script);
    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(
            scriptPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    var originalPath = Environment.GetEnvironmentVariable("PATH");
    var originalFixtureMode = Environment.GetEnvironmentVariable("PI_JSON_FIXTURE_MODE");

    try
    {
        Environment.SetEnvironmentVariable("PATH", tempRoot + Path.PathSeparator + (originalPath ?? string.Empty));
        Environment.SetEnvironmentVariable("PI_JSON_FIXTURE_MODE", fixtureMode);

        var result = await PiJsonEventRunner.RunAskAsync(tempRoot, "test prompt", null);

        if (result.Success != expectedSuccess)
        {
            failures.Add($"[{name}] expected Success={expectedSuccess} but got {result.Success}.");
        }

        if (expectedAnswer is null)
        {
            if (!string.IsNullOrEmpty(result.Answer))
            {
                failures.Add($"[{name}] expected empty answer but got '{result.Answer}'.");
            }
        }
        else if (!string.Equals(result.Answer, expectedAnswer, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected answer '{expectedAnswer}' but got '{result.Answer}'.");
        }

        if (!string.Equals(result.Provider, expectedProvider, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected provider '{expectedProvider ?? "(null)"}' but got '{result.Provider ?? "(null)"}'.");
        }

        if (!string.Equals(result.Model, expectedModel, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected model '{expectedModel ?? "(null)"}' but got '{result.Model ?? "(null)"}'.");
        }

        if (expectedErrorContains is null)
        {
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                failures.Add($"[{name}] expected no error message but got '{result.ErrorMessage}'.");
            }
        }
        else if (string.IsNullOrWhiteSpace(result.ErrorMessage) || !result.ErrorMessage.Contains(expectedErrorContains, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected error containing '{expectedErrorContains}' but got '{result.ErrorMessage ?? "(null)"}'.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        Environment.SetEnvironmentVariable("PATH", originalPath);
        Environment.SetEnvironmentVariable("PI_JSON_FIXTURE_MODE", originalFixtureMode);

        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static async Task RunTraceSliceCliCaseAsync(
    string name,
    string prompt,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-trace-slice-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        var initResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "init");
        if (initResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected rfs init to succeed but got exit code {initResult.ExitCode}. stderr: {initResult.Stderr}");
            return;
        }

        var traceSliceResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "trace-slice", prompt);
        if (traceSliceResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {traceSliceResult.ExitCode}. stderr: {traceSliceResult.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(traceSliceResult.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {traceSliceResult.Stderr.Trim()}.");
        }

        try
        {
            using var document = JsonDocument.Parse(traceSliceResult.Stdout);
            var root = document.RootElement;

            if (!string.Equals(root.GetProperty("type").GetString(), "rufus.trace-slice", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected type rufus.trace-slice.");
            }

            if (root.GetProperty("schemaVersion").GetInt32() != 1)
            {
                failures.Add($"[{name}] expected schemaVersion 1.");
            }

            var promptElement = root.GetProperty("prompt");
            if (!string.Equals(promptElement.GetProperty("text").GetString(), prompt, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] prompt.text did not round-trip.");
            }

            if (promptElement.GetProperty("isExcerpt").ValueKind != JsonValueKind.False)
            {
                failures.Add($"[{name}] expected prompt.isExcerpt to be false.");
            }

            var intent = root.GetProperty("intent");
            if (!string.Equals(intent.GetProperty("source").GetString(), "deterministic", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected deterministic intent source.");
            }

            var selection = root.GetProperty("selection");
            if (!selection.TryGetProperty("headStateId", out var headStateId) || headStateId.ValueKind != JsonValueKind.String)
            {
                failures.Add($"[{name}] expected selection.headStateId.");
            }

            if (!selection.TryGetProperty("stateIds", out var stateIds) || stateIds.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected selection.stateIds array.");
            }

            if (!selection.TryGetProperty("deltaIds", out var deltaIds) || deltaIds.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected selection.deltaIds array.");
            }

            if (!root.TryGetProperty("materializationPolicy", out var materializationPolicy))
            {
                failures.Add($"[{name}] expected materializationPolicy.");
            }
            else
            {
                if (materializationPolicy.GetProperty("includeArtifactContents").ValueKind != JsonValueKind.False)
                {
                    failures.Add($"[{name}] expected includeArtifactContents=false.");
                }

                if (materializationPolicy.GetProperty("includeGitDiffs").ValueKind != JsonValueKind.False)
                {
                    failures.Add($"[{name}] expected includeGitDiffs=false.");
                }
            }

            if (root.TryGetProperty("artifacts", out var artifacts) && artifacts.ValueKind == JsonValueKind.Array)
            {
                foreach (var artifact in artifacts.EnumerateArray())
                {
                    if (!string.Equals(artifact.GetProperty("includeMode").GetString(), "metadata-only", StringComparison.Ordinal))
                    {
                        failures.Add($"[{name}] expected artifacts to be metadata-only.");
                        break;
                    }
                }
            }

            var text = traceSliceResult.Stdout;
            foreach (var fragment in new[] { "diff --git", "AgentTaskResult" })
            {
                if (text.Contains(fragment, StringComparison.Ordinal))
                {
                    failures.Add($"[{name}] unexpected raw fragment '{fragment}' in trace-slice output.");
                }
            }
        }
        catch (JsonException ex)
        {
            failures.Add($"[{name}] trace-slice output was not valid JSON: {ex.Message}");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(string workingDirectory, params string[] commandLine)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = commandLine[0],
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    for (var i = 1; i < commandLine.Length; i++)
    {
        startInfo.ArgumentList.Add(commandLine[i]);
    }

    using var process = Process.Start(startInfo);
    if (process is null)
    {
        return (-1, string.Empty, "failed to start process");
    }

    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    return (process.ExitCode, await stdoutTask, await stderrTask);
}

static async Task RunTraceSliceProposalCliCaseAsync(
    string name,
    string prompt,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-trace-slice-proposal-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        var initResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "init");
        if (initResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected rfs init to succeed but got exit code {initResult.ExitCode}. stderr: {initResult.Stderr}");
            return;
        }

        var proposalResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "trace-slice-proposal", prompt);
        if (proposalResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {proposalResult.ExitCode}. stderr: {proposalResult.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(proposalResult.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {proposalResult.Stderr.Trim()}.");
        }

        try
        {
            using var document = JsonDocument.Parse(proposalResult.Stdout);
            var root = document.RootElement;

            if (!string.Equals(root.GetProperty("type").GetString(), "rufus.trace-slice-proposal", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected type rufus.trace-slice-proposal.");
            }

            if (root.GetProperty("schemaVersion").GetInt32() != 1)
            {
                failures.Add($"[{name}] expected schemaVersion 1.");
            }

            var promptElement = root.GetProperty("prompt");
            if (!string.Equals(promptElement.GetProperty("text").GetString(), prompt, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] prompt.text did not round-trip.");
            }

            if (promptElement.GetProperty("isExcerpt").ValueKind != JsonValueKind.False)
            {
                failures.Add($"[{name}] expected prompt.isExcerpt to be false.");
            }

            var intent = root.GetProperty("intent");
            if (!intent.TryGetProperty("kind", out var kindProperty) || kindProperty.ValueKind != JsonValueKind.String)
            {
                failures.Add($"[{name}] expected intent.kind.");
            }

            if (!intent.TryGetProperty("summary", out var summaryProperty) || summaryProperty.ValueKind != JsonValueKind.String)
            {
                failures.Add($"[{name}] expected intent.summary.");
            }

            if (!intent.TryGetProperty("source", out var sourceProperty) || sourceProperty.ValueKind != JsonValueKind.String)
            {
                failures.Add($"[{name}] expected intent.source.");
            }

            var selection = root.GetProperty("requestedSelection");
            if (!selection.TryGetProperty("stateIds", out var stateIds) || stateIds.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected requestedSelection.stateIds array.");
            }

            if (!selection.TryGetProperty("deltaIds", out var deltaIds) || deltaIds.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected requestedSelection.deltaIds array.");
            }

            if (!selection.TryGetProperty("anchorIds", out var anchorIds) || anchorIds.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected requestedSelection.anchorIds array.");
            }

            if (!selection.TryGetProperty("artifactRefs", out var artifactRefs) || artifactRefs.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected requestedSelection.artifactRefs array.");
            }

            if (!root.TryGetProperty("requestedMaterializationPolicy", out var materializationPolicy))
            {
                failures.Add($"[{name}] expected requestedMaterializationPolicy.");
            }
            else
            {
                if (materializationPolicy.GetProperty("includeArtifactContents").ValueKind != JsonValueKind.False)
                {
                    failures.Add($"[{name}] expected includeArtifactContents=false.");
                }

                if (materializationPolicy.GetProperty("includeGitDiffs").ValueKind != JsonValueKind.False)
                {
                    failures.Add($"[{name}] expected includeGitDiffs=false.");
                }

                if (materializationPolicy.GetProperty("includeStdoutStderr").ValueKind != JsonValueKind.False)
                {
                    failures.Add($"[{name}] expected includeStdoutStderr=false.");
                }

                if (materializationPolicy.GetProperty("includeJsonl").ValueKind != JsonValueKind.False)
                {
                    failures.Add($"[{name}] expected includeJsonl=false.");
                }
            }

            if (!root.TryGetProperty("rationale", out var rationale) || rationale.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected rationale array.");
            }

            if (!root.TryGetProperty("warnings", out var warnings) || warnings.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected warnings array.");
            }

            if (!root.TryGetProperty("confidence", out var confidence) || confidence.ValueKind != JsonValueKind.Number)
            {
                failures.Add($"[{name}] expected confidence number.");
            }

            var text = proposalResult.Stdout;
            foreach (var fragment in new[] { "diff --git", "AgentTaskResult", "assistantMessageEvent", "message_update", "message_end" })
            {
                if (text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"[{name}] unexpected raw fragment '{fragment}' in trace-slice-proposal output.");
                    break;
                }
            }
        }
        catch (JsonException ex)
        {
            failures.Add($"[{name}] trace-slice-proposal output was not valid JSON: {ex.Message}");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static async Task RunTraceSliceProposalLlmCliCaseAsync(
    string name,
    string prompt,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-trace-slice-proposal-llm-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        var initResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "init");
        if (initResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected rfs init to succeed but got exit code {initResult.ExitCode}. stderr: {initResult.Stderr}");
            return;
        }

        var proposalResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "trace-slice-proposal-llm", prompt);
        if (proposalResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {proposalResult.ExitCode}. stderr: {proposalResult.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(proposalResult.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {proposalResult.Stderr.Trim()}.");
        }

        try
        {
            using var document = JsonDocument.Parse(proposalResult.Stdout);
            var root = document.RootElement;

            if (!string.Equals(root.GetProperty("type").GetString(), "rufus.trace-slice-proposal", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected type rufus.trace-slice-proposal.");
            }

            if (root.GetProperty("schemaVersion").GetInt32() != 1)
            {
                failures.Add($"[{name}] expected schemaVersion 1.");
            }

            var promptElement = root.GetProperty("prompt");
            if (!string.Equals(promptElement.GetProperty("text").GetString(), prompt, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] prompt.text did not round-trip.");
            }

            if (promptElement.GetProperty("isExcerpt").ValueKind != JsonValueKind.False)
            {
                failures.Add($"[{name}] expected prompt.isExcerpt to be false.");
            }

            var selection = root.GetProperty("requestedSelection");
            if (!selection.TryGetProperty("stateIds", out var stateIds) || stateIds.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected requestedSelection.stateIds array.");
            }

            if (!selection.TryGetProperty("deltaIds", out var deltaIds) || deltaIds.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected requestedSelection.deltaIds array.");
            }

            if (!selection.TryGetProperty("anchorIds", out var anchorIds) || anchorIds.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected requestedSelection.anchorIds array.");
            }

            if (!selection.TryGetProperty("artifactRefs", out var artifactRefs) || artifactRefs.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected requestedSelection.artifactRefs array.");
            }

            if (!root.TryGetProperty("requestedMaterializationPolicy", out var materializationPolicy))
            {
                failures.Add($"[{name}] expected requestedMaterializationPolicy.");
            }
            else
            {
                if (materializationPolicy.GetProperty("includeArtifactContents").ValueKind != JsonValueKind.False)
                {
                    failures.Add($"[{name}] expected includeArtifactContents=false.");
                }

                if (materializationPolicy.GetProperty("includeGitDiffs").ValueKind != JsonValueKind.False)
                {
                    failures.Add($"[{name}] expected includeGitDiffs=false.");
                }

                if (materializationPolicy.GetProperty("includeStdoutStderr").ValueKind != JsonValueKind.False)
                {
                    failures.Add($"[{name}] expected includeStdoutStderr=false.");
                }

                if (materializationPolicy.GetProperty("includeJsonl").ValueKind != JsonValueKind.False)
                {
                    failures.Add($"[{name}] expected includeJsonl=false.");
                }
            }

            if (!root.TryGetProperty("rationale", out var rationale) || rationale.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected rationale array.");
            }

            if (!root.TryGetProperty("warnings", out var warnings) || warnings.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected warnings array.");
            }

            if (!root.TryGetProperty("confidence", out var confidence) || confidence.ValueKind != JsonValueKind.Number)
            {
                failures.Add($"[{name}] expected confidence number.");
            }

            var text = proposalResult.Stdout;
            foreach (var fragment in new[] { "diff --git", "AgentTaskResult", "assistantMessageEvent", "message_update", "message_end" })
            {
                if (text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"[{name}] unexpected raw fragment '{fragment}' in trace-slice-proposal-llm output.");
                    break;
                }
            }
        }
        catch (JsonException ex)
        {
            failures.Add($"[{name}] trace-slice-proposal-llm output was not valid JSON: {ex.Message}");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static async Task RunTraceSliceValidateCliCaseAsync(
    string name,
    string prompt,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-trace-slice-validate-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        var initResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "init");
        if (initResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected rfs init to succeed but got exit code {initResult.ExitCode}. stderr: {initResult.Stderr}");
            return;
        }

        var validateResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "trace-slice-validate", prompt);
        if (validateResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {validateResult.ExitCode}. stderr: {validateResult.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(validateResult.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {validateResult.Stderr.Trim()}.");
        }

        try
        {
            using var document = JsonDocument.Parse(validateResult.Stdout);
            var root = document.RootElement;

            if (!string.Equals(root.GetProperty("type").GetString(), "rufus.trace-slice", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected type rufus.trace-slice.");
            }

            if (root.GetProperty("schemaVersion").GetInt32() != 1)
            {
                failures.Add($"[{name}] expected schemaVersion 1.");
            }

            var promptElement = root.GetProperty("prompt");
            if (!string.Equals(promptElement.GetProperty("text").GetString(), prompt, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] prompt.text did not round-trip.");
            }

            var selection = root.GetProperty("selection");
            if (!string.Equals(selection.GetProperty("strategy").GetString(), "proposal-validated", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected selection.strategy=proposal-validated.");
            }

            if (selection.GetProperty("maxStates").GetInt32() != 5)
            {
                failures.Add($"[{name}] expected selection.maxStates=5.");
            }

            if (!root.TryGetProperty("validation", out var validation))
            {
                failures.Add($"[{name}] expected validation block.");
            }
            else
            {
                if (!validation.TryGetProperty("status", out var statusProperty) || statusProperty.ValueKind != JsonValueKind.String)
                {
                    failures.Add($"[{name}] expected validation.status.");
                }

                foreach (var propertyName in new[] { "accepted", "rejected", "downgraded", "reasons" })
                {
                    if (!validation.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
                    {
                        failures.Add($"[{name}] expected validation.{propertyName} array.");
                    }
                }
            }

            var materializationPolicy = root.GetProperty("materializationPolicy");
            foreach (var propertyName in new[] { "includeArtifactContents", "includeGitDiffs", "includeStdoutStderr", "includeJsonl" })
            {
                if (materializationPolicy.GetProperty(propertyName).ValueKind != JsonValueKind.False)
                {
                    failures.Add($"[{name}] expected {propertyName}=false.");
                }
            }

            if (root.TryGetProperty("artifacts", out var artifacts) && artifacts.ValueKind == JsonValueKind.Array)
            {
                foreach (var artifact in artifacts.EnumerateArray())
                {
                    if (!string.Equals(artifact.GetProperty("includeMode").GetString(), "metadata-only", StringComparison.Ordinal))
                    {
                        failures.Add($"[{name}] expected artifacts to be metadata-only.");
                        break;
                    }
                }
            }

            var text = validateResult.Stdout;
            foreach (var fragment in new[] { "diff --git", "assistantMessageEvent", "message_update", "message_end" })
            {
                if (text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"[{name}] unexpected raw fragment '{fragment}' in trace-slice-validate output.");
                    break;
                }
            }
        }
        catch (JsonException ex)
        {
            failures.Add($"[{name}] trace-slice-validate output was not valid JSON: {ex.Message}");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static async Task RunTraceSliceValidateLlmCliCaseAsync(
    string name,
    string prompt,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-trace-slice-validate-llm-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        var initResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "init");
        if (initResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected rfs init to succeed but got exit code {initResult.ExitCode}. stderr: {initResult.Stderr}");
            return;
        }

        var validateResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "trace-slice-validate-llm", prompt);
        if (validateResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {validateResult.ExitCode}. stderr: {validateResult.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(validateResult.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {validateResult.Stderr.Trim()}.");
        }

        try
        {
            using var document = JsonDocument.Parse(validateResult.Stdout);
            var root = document.RootElement;

            if (!string.Equals(root.GetProperty("type").GetString(), "rufus.trace-slice", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected type rufus.trace-slice.");
            }

            if (root.GetProperty("schemaVersion").GetInt32() != 1)
            {
                failures.Add($"[{name}] expected schemaVersion 1.");
            }

            var promptElement = root.GetProperty("prompt");
            if (!string.Equals(promptElement.GetProperty("text").GetString(), prompt, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] prompt.text did not round-trip.");
            }

            var selection = root.GetProperty("selection");
            if (!string.Equals(selection.GetProperty("strategy").GetString(), "proposal-validated", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected selection.strategy=proposal-validated.");
            }

            if (selection.GetProperty("maxStates").GetInt32() != 5)
            {
                failures.Add($"[{name}] expected selection.maxStates=5.");
            }

            if (!root.TryGetProperty("validation", out var validation))
            {
                failures.Add($"[{name}] expected validation block.");
            }
            else
            {
                if (!validation.TryGetProperty("status", out var statusProperty) || statusProperty.ValueKind != JsonValueKind.String)
                {
                    failures.Add($"[{name}] expected validation.status.");
                }

                foreach (var propertyName in new[] { "accepted", "rejected", "downgraded", "reasons" })
                {
                    if (!validation.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
                    {
                        failures.Add($"[{name}] expected validation.{propertyName} array.");
                    }
                }
            }

            var materializationPolicy = root.GetProperty("materializationPolicy");
            foreach (var propertyName in new[] { "includeArtifactContents", "includeGitDiffs", "includeStdoutStderr", "includeJsonl" })
            {
                if (materializationPolicy.GetProperty(propertyName).ValueKind != JsonValueKind.False)
                {
                    failures.Add($"[{name}] expected {propertyName}=false.");
                }
            }

            if (root.TryGetProperty("artifacts", out var artifacts) && artifacts.ValueKind == JsonValueKind.Array)
            {
                foreach (var artifact in artifacts.EnumerateArray())
                {
                    if (!string.Equals(artifact.GetProperty("includeMode").GetString(), "metadata-only", StringComparison.Ordinal))
                    {
                        failures.Add($"[{name}] expected artifacts to be metadata-only.");
                        break;
                    }
                }
            }

            var text = validateResult.Stdout;
            foreach (var fragment in new[] { "diff --git", "assistantMessageEvent", "message_update", "message_end" })
            {
                if (text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"[{name}] unexpected raw fragment '{fragment}' in trace-slice-validate-llm output.");
                    break;
                }
            }
        }
        catch (JsonException ex)
        {
            failures.Add($"[{name}] trace-slice-validate-llm output was not valid JSON: {ex.Message}");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static async Task RunContextPackTraceSliceCliCaseAsync(
    string name,
    string prompt,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-context-pack-trace-slice-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        var initResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "init");
        if (initResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected rfs init to succeed but got exit code {initResult.ExitCode}. stderr: {initResult.Stderr}");
            return;
        }

        var contextPackResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "context-pack", "--trace-slice", prompt);
        if (contextPackResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {contextPackResult.ExitCode}. stderr: {contextPackResult.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(contextPackResult.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {contextPackResult.Stderr.Trim()}.");
        }

        try
        {
            using var document = JsonDocument.Parse(contextPackResult.Stdout);
            var root = document.RootElement;

            if (!string.Equals(root.GetProperty("type").GetString(), "rck-dag-context-pack-v1", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected type rck-dag-context-pack-v1.");
            }

            if (!string.Equals(root.GetProperty("scope").GetString(), "trace-slice", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected scope=trace-slice.");
            }

            var traceSlice = root.GetProperty("traceSlice");
            if (!string.Equals(traceSlice.GetProperty("type").GetString(), "rufus.trace-slice", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected embedded traceSlice.type=rufus.trace-slice.");
            }

            var selection = traceSlice.GetProperty("selection");
            var maxStates = selection.GetProperty("maxStates").GetInt32();
            var stateIds = selection.GetProperty("stateIds");
            var deltaIds = selection.GetProperty("deltaIds");
            var anchorIds = selection.GetProperty("anchorIds");

            var states = root.GetProperty("states");
            var deltas = root.GetProperty("deltas");
            var anchors = root.GetProperty("anchors");
            var artifacts = root.GetProperty("artifacts");
            var materializationPolicy = root.GetProperty("materializationPolicy");

            if (states.GetArrayLength() != stateIds.GetArrayLength())
            {
                failures.Add($"[{name}] expected states length to match traceSlice.selection.stateIds length.");
            }

            if (deltas.GetArrayLength() != deltaIds.GetArrayLength())
            {
                failures.Add($"[{name}] expected deltas length to match traceSlice.selection.deltaIds length.");
            }

            if (anchors.GetArrayLength() != anchorIds.GetArrayLength())
            {
                failures.Add($"[{name}] expected anchors length to match traceSlice.selection.anchorIds length.");
            }

            if (states.GetArrayLength() > maxStates)
            {
                failures.Add($"[{name}] expected states length <= traceSlice.selection.maxStates.");
            }

            if (materializationPolicy.GetProperty("includeArtifactContents").ValueKind != JsonValueKind.False)
            {
                failures.Add($"[{name}] expected includeArtifactContents=false.");
            }

            if (materializationPolicy.GetProperty("includeGitDiffs").ValueKind != JsonValueKind.False)
            {
                failures.Add($"[{name}] expected includeGitDiffs=false.");
            }

            if (materializationPolicy.GetProperty("includeStdoutStderr").ValueKind != JsonValueKind.False)
            {
                failures.Add($"[{name}] expected includeStdoutStderr=false.");
            }

            if (materializationPolicy.GetProperty("includeJsonl").ValueKind != JsonValueKind.False)
            {
                failures.Add($"[{name}] expected includeJsonl=false.");
            }

            foreach (var artifact in artifacts.EnumerateArray())
            {
                if (!string.Equals(artifact.GetProperty("includeMode").GetString(), "metadata-only", StringComparison.Ordinal))
                {
                    failures.Add($"[{name}] expected artifacts to be metadata-only.");
                    break;
                }
            }

            var text = contextPackResult.Stdout;
            foreach (var fragment in new[] { "diff --git", "AgentTaskResult" })
            {
                if (text.Contains(fragment, StringComparison.Ordinal))
                {
                    failures.Add($"[{name}] unexpected raw fragment '{fragment}' in context-pack --trace-slice output.");
                    break;
                }
            }
        }
        catch (JsonException ex)
        {
            failures.Add($"[{name}] context-pack --trace-slice output was not valid JSON: {ex.Message}");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static async Task RunContextPackTraceSliceValidatedCliCaseAsync(
    string name,
    string prompt,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-context-pack-trace-slice-validated-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        var initResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "init");
        if (initResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected rfs init to succeed but got exit code {initResult.ExitCode}. stderr: {initResult.Stderr}");
            return;
        }

        var contextPackResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "context-pack", "--trace-slice-validated", prompt);
        if (contextPackResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {contextPackResult.ExitCode}. stderr: {contextPackResult.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(contextPackResult.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {contextPackResult.Stderr.Trim()}.");
        }

        try
        {
            using var document = JsonDocument.Parse(contextPackResult.Stdout);
            var root = document.RootElement;

            if (!string.Equals(root.GetProperty("type").GetString(), "rck-dag-context-pack-v1", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected type rck-dag-context-pack-v1.");
            }

            if (!string.Equals(root.GetProperty("scope").GetString(), "trace-slice-validated", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected scope=trace-slice-validated.");
            }

            var traceSlice = root.GetProperty("traceSlice");
            if (!string.Equals(traceSlice.GetProperty("type").GetString(), "rufus.trace-slice", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected embedded traceSlice.type=rufus.trace-slice.");
            }

            if (!string.Equals(traceSlice.GetProperty("selection").GetProperty("strategy").GetString(), "proposal-validated", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected traceSlice.selection.strategy=proposal-validated.");
            }

            if (!traceSlice.TryGetProperty("validation", out var validation))
            {
                failures.Add($"[{name}] expected traceSlice.validation block.");
            }
            else
            {
                if (!validation.TryGetProperty("status", out var statusProperty) || statusProperty.ValueKind != JsonValueKind.String)
                {
                    failures.Add($"[{name}] expected validation.status.");
                }

                foreach (var propertyName in new[] { "accepted", "rejected", "downgraded", "reasons" })
                {
                    if (!validation.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
                    {
                        failures.Add($"[{name}] expected validation.{propertyName} array.");
                    }
                }
            }

            var selection = traceSlice.GetProperty("selection");
            var maxStates = selection.GetProperty("maxStates").GetInt32();
            var stateIds = selection.GetProperty("stateIds");
            var deltaIds = selection.GetProperty("deltaIds");
            var anchorIds = selection.GetProperty("anchorIds");

            var states = root.GetProperty("states");
            var deltas = root.GetProperty("deltas");
            var anchors = root.GetProperty("anchors");
            var artifacts = root.GetProperty("artifacts");
            var materializationPolicy = root.GetProperty("materializationPolicy");

            if (states.GetArrayLength() != stateIds.GetArrayLength())
            {
                failures.Add($"[{name}] expected states length to match traceSlice.selection.stateIds length.");
            }

            if (deltas.GetArrayLength() != deltaIds.GetArrayLength())
            {
                failures.Add($"[{name}] expected deltas length to match traceSlice.selection.deltaIds length.");
            }

            if (anchors.GetArrayLength() != anchorIds.GetArrayLength())
            {
                failures.Add($"[{name}] expected anchors length to match traceSlice.selection.anchorIds length.");
            }

            if (states.GetArrayLength() > maxStates)
            {
                failures.Add($"[{name}] expected states length <= traceSlice.selection.maxStates.");
            }

            if (materializationPolicy.GetProperty("includeArtifactContents").ValueKind != JsonValueKind.False)
            {
                failures.Add($"[{name}] expected includeArtifactContents=false.");
            }

            if (materializationPolicy.GetProperty("includeGitDiffs").ValueKind != JsonValueKind.False)
            {
                failures.Add($"[{name}] expected includeGitDiffs=false.");
            }

            if (materializationPolicy.GetProperty("includeStdoutStderr").ValueKind != JsonValueKind.False)
            {
                failures.Add($"[{name}] expected includeStdoutStderr=false.");
            }

            if (materializationPolicy.GetProperty("includeJsonl").ValueKind != JsonValueKind.False)
            {
                failures.Add($"[{name}] expected includeJsonl=false.");
            }

            foreach (var artifact in artifacts.EnumerateArray())
            {
                if (!string.Equals(artifact.GetProperty("includeMode").GetString(), "metadata-only", StringComparison.Ordinal))
                {
                    failures.Add($"[{name}] expected artifacts to be metadata-only.");
                    break;
                }
            }

            var text = contextPackResult.Stdout;
            foreach (var fragment in new[] { "diff --git", "AgentTaskResult" })
            {
                if (text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"[{name}] unexpected raw fragment '{fragment}' in context-pack --trace-slice-validated output.");
                    break;
                }
            }
        }
        catch (JsonException ex)
        {
            failures.Add($"[{name}] context-pack --trace-slice-validated output was not valid JSON: {ex.Message}");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static async Task RunIntentInferenceCaseAsync(
    string name,
    AgentTask task,
    string expectedIntent,
    List<string> failures)
{
    try
    {
        var agent = new IntentInferenceAgent();
        var result = await agent.ExecuteAsync(task);

        if (result.Status != AgentTaskStatus.Succeeded)
        {
            failures.Add($"[{name}] expected Status=Succeeded but got {result.Status}.");
        }

        if (!string.Equals(result.TaskId, task.Id, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected TaskId '{task.Id}' but got '{result.TaskId}'.");
        }

        if (!string.Equals(result.AgentId, "intent-inference", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected AgentId 'intent-inference' but got '{result.AgentId}'.");
        }

        if (!string.Equals(result.ExecutionModel.Provider, "mock", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected execution provider 'mock' but got '{result.ExecutionModel.Provider}'.");
        }

        if (!string.Equals(result.ExecutionModel.Model, "deterministic-v1", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected execution model 'deterministic-v1' but got '{result.ExecutionModel.Model}'.");
        }

        if (string.IsNullOrWhiteSpace(result.Output))
        {
            failures.Add($"[{name}] expected Output to be populated.");
        }
        else
        {
            try
            {
                using var document = JsonDocument.Parse(result.Output);
                var root = document.RootElement;
                var actualIntent = root.TryGetProperty("Intent", out var intentElement) ? intentElement.GetString() : null;

                if (!string.Equals(actualIntent, expectedIntent, StringComparison.Ordinal))
                {
                    failures.Add($"[{name}] expected PromptIntent.Intent '{expectedIntent}' but got '{actualIntent ?? "(missing)"}'.");
                }

                if (!root.TryGetProperty("Summary", out var summaryElement) || string.IsNullOrWhiteSpace(summaryElement.GetString()))
                {
                    failures.Add($"[{name}] expected PromptIntent.Summary to be populated.");
                }

                if (!root.TryGetProperty("Entities", out var entitiesElement) || entitiesElement.ValueKind != JsonValueKind.Array)
                {
                    failures.Add($"[{name}] expected PromptIntent.Entities to be present.");
                }

                if (!root.TryGetProperty("Constraints", out var constraintsElement) || constraintsElement.ValueKind != JsonValueKind.Array)
                {
                    failures.Add($"[{name}] expected PromptIntent.Constraints to be present.");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"[{name}] output was not valid PromptIntent JSON: {ex.Message}");
            }
        }

        if (result.Evidence.Count == 0)
        {
            failures.Add($"[{name}] expected Evidence to be populated.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
    }
}

static async Task RunIntentInferenceFailureCaseAsync(
    string name,
    AgentTask task,
    string expectedErrorContains,
    List<string> failures)
{
    try
    {
        var agent = new IntentInferenceAgent();
        var result = await agent.ExecuteAsync(task);

        if (result.Status != AgentTaskStatus.Failed)
        {
            failures.Add($"[{name}] expected Status=Failed but got {result.Status}.");
        }

        if (!string.Equals(result.TaskId, task.Id, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected TaskId '{task.Id}' but got '{result.TaskId}'.");
        }

        if (!string.Equals(result.AgentId, "intent-inference", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected AgentId 'intent-inference' but got '{result.AgentId}'.");
        }

        if (!string.Equals(result.ExecutionModel.Provider, "mock", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected execution provider 'mock' but got '{result.ExecutionModel.Provider}'.");
        }

        if (!string.Equals(result.ExecutionModel.Model, "deterministic-v1", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected execution model 'deterministic-v1' but got '{result.ExecutionModel.Model}'.");
        }

        if (result.Errors.Count == 0)
        {
            failures.Add($"[{name}] expected Errors to be populated.");
        }
        else if (!result.Errors[0].Contains(expectedErrorContains, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected Errors to contain '{expectedErrorContains}' but got '{result.Errors[0]}'.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
    }
}

static async Task RunIntentCliCaseAsync(
    string name,
    string prompt,
    string expectedIntent,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");

    var startInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        WorkingDirectory = repoRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };

    startInfo.ArgumentList.Add("run");
    startInfo.ArgumentList.Add("--project");
    startInfo.ArgumentList.Add(cliProjectPath);
    startInfo.ArgumentList.Add("--");
    startInfo.ArgumentList.Add("intent");
    startInfo.ArgumentList.Add(prompt);

    try
    {
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            failures.Add($"[{name}] failed to start dotnet run for rfs intent.");
            return;
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {process.ExitCode}. stderr: {stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {stderr.Trim()}.");
        }

        var requiredFragments = new[]
        {
            "Rufus Intent",
            $"  {prompt}",
            "  Status: Succeeded",
            "  AgentId: intent-inference",
            "  ExecutionModel: mock/deterministic-v1",
            "  Summary:",
            "  Output:",
            $"\"Intent\":\"{expectedIntent}\"",
            "  Evidence:",
            "  Warnings:",
            "    (none)"
        };

        foreach (var fragment in requiredFragments)
        {
            if (!stdout.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
            }
        }

        if (stdout.Contains("  Errors:", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] did not expect an Errors section for successful intent inference.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
    }
}
