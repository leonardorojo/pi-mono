using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Rufus.Cli.PiIntegration;
using Rufus.Agenting;
using Rufus.Agenting.Intent;
using Rufus.RCK.Workspace;

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
    name: "final answer beats prior delta",
    fixtureMode: "delta-then-final",
    expectedSuccess: true,
    expectedAnswer: "structured answer",
    expectedProvider: "test-provider",
    expectedModel: "test-model",
    expectedErrorContains: null,
    failures);

await RunCaseAsync(
    name: "no answer fails explicitly",
    fixtureMode: "no-answer",
    expectedSuccess: false,
    expectedAnswer: null,
    expectedProvider: null,
    expectedModel: null,
    expectedErrorContains: "Pi JSON stream ended before a final assistant answer was observed",
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
    failures: failures,
    fixtureMode: "valid");

await RunTraceSliceProposalLlmCliCaseAsync(
    name: "trace slice proposal llm cli rejects invalid json",
    prompt: "Implement rfs show command",
    failures: failures,
    fixtureMode: "invalid-json",
    expectSuccess: false,
    expectedErrorContains: "invalid JSON from LLM");

await RunTraceSliceProposalLlmCliCaseAsync(
    name: "trace slice proposal llm cli rejects invalid shape",
    prompt: "Implement rfs show command",
    failures: failures,
    fixtureMode: "invalid-shape",
    expectSuccess: false,
    expectedErrorContains: "missing materialization policy field");

await RunTraceSliceProposalLlmCliCaseAsync(
    name: "trace slice proposal llm cli rejects contaminated llm output",
    prompt: "Implement rfs show command",
    failures: failures,
    fixtureMode: "contaminated",
    expectSuccess: false,
    expectedErrorContains: "forbidden content");

await RunTraceSliceValidateCliCaseAsync(
    name: "trace slice validate cli renders validated trace slice json",
    prompt: "Implement rfs show command",
    failures: failures);

await RunTraceSliceValidateLlmCliCaseAsync(
    name: "trace slice validate llm cli renders validated trace slice json",
    prompt: "Implement rfs show command",
    failures: failures,
    fixtureMode: "valid");

await RunTraceSliceValidateLlmCliCaseAsync(
    name: "trace slice validate llm cli rejects unsafe materialization policy",
    prompt: "Implement rfs show command",
    failures: failures,
    fixtureMode: "unsafe-policy",
    expectSuccess: false,
    expectedErrorContains: "expected restricted materialization policy flags to be false");

await RunRckTraceSliceProposalValidatorCriticalCasesAsync(failures);

await RunContextPackTraceSliceCliCaseAsync(
    name: "context pack trace-slice cli renders scoped json",
    prompt: "Implement rfs show command",
    failures);

await RunContextPackTraceSliceValidatedCliCaseAsync(
    name: "context pack trace-slice-validated cli renders validated scoped json",
    prompt: "Implement rfs show command",
    failures);

await RunRfsTuiInitializedSessionCaseAsync(
    name: "bare rfs enters tui and handles basic commands on initialized repo",
    failures);

await RunRfsTuiAutoInitSessionCaseAsync(
    name: "bare rfs auto-initializes an empty repo and enters tui",
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
                 "  delta-then-final)\n" +
                 "    cat <<'EOF'\n" +
                 "{\"type\":\"session\"}\n" +
                 "{\"type\":\"message_update\",\"assistantMessageEvent\":{\"type\":\"text_delta\",\"delta\":\"hello \"}}\n" +
                 "{\"type\":\"message_update\",\"assistantMessageEvent\":{\"type\":\"text_delta\",\"delta\":\"ignored delta\"}}\n" +
                 "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":\"structured answer\"}]}}\n" +
                 "EOF\n" +
                 "    ;;\n" +
                 "  no-answer)\n" +
                 "    echo '{\"type\":\"session\"}'\n" +
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

static Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(string workingDirectory, params string[] commandLine)
{
    return RunProcessAsyncWithInput(workingDirectory, null, commandLine);
}

static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsyncWithInput(string workingDirectory, string? standardInput, params string[] commandLine)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = commandLine[0],
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        RedirectStandardInput = standardInput is not null,
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

    Task? stdinTask = null;
    if (standardInput is not null)
    {
        stdinTask = process.StandardInput.WriteAsync(standardInput);
        process.StandardInput.Close();
    }

    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();
    if (stdinTask is not null)
    {
        await stdinTask;
    }

    await process.WaitForExitAsync();
    return (process.ExitCode, await stdoutTask, await stderrTask);
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

static string BuildPiFixtureScript(string prompt)
{
    var validAnswerJson = BuildTraceSliceProposalAnswer(prompt, includeUnsafePolicy: false, includeMissingPolicyField: false);
    var invalidShapeAnswerJson = BuildTraceSliceProposalAnswer(prompt, includeUnsafePolicy: false, includeMissingPolicyField: true);
    var unsafePolicyAnswerJson = BuildTraceSliceProposalAnswer(prompt, includeUnsafePolicy: true, includeMissingPolicyField: false);
    var contaminatedAnswerJson = BuildTraceSliceProposalContaminatedAnswer(prompt);
    var validAnswerLiteral = JsonSerializer.Serialize(validAnswerJson);
    var invalidShapeAnswerLiteral = JsonSerializer.Serialize(invalidShapeAnswerJson);
    var unsafePolicyAnswerLiteral = JsonSerializer.Serialize(unsafePolicyAnswerJson);
    var contaminatedAnswerLiteral = JsonSerializer.Serialize(contaminatedAnswerJson);

    return
        "#!/usr/bin/env bash\n" +
        "set -euo pipefail\n" +
        "case \"${RFS_PI_TRACE_SLICE_FIXTURE_MODE:-}\" in\n" +
        "  valid)\n" +
        "    cat <<'EOF'\n" +
        "{\"type\":\"session\"}\n" +
        "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":" + validAnswerLiteral + "}]}}\n" +
        "EOF\n" +
        "    ;;\n" +
        "  invalid-json)\n" +
        "    cat <<'EOF'\n" +
        "{\"type\":\"session\"}\n" +
        "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":\"not-json\"}]}}\n" +
        "EOF\n" +
        "    ;;\n" +
        "  invalid-shape)\n" +
        "    cat <<'EOF'\n" +
        "{\"type\":\"session\"}\n" +
        "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":" + invalidShapeAnswerLiteral + "}]}}\n" +
        "EOF\n" +
        "    ;;\n" +
        "  unsafe-policy)\n" +
        "    cat <<'EOF'\n" +
        "{\"type\":\"session\"}\n" +
        "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":" + unsafePolicyAnswerLiteral + "}]}}\n" +
        "EOF\n" +
        "    ;;\n" +
        "  contaminated)\n" +
        "    cat <<'EOF'\n" +
        "{\"type\":\"session\"}\n" +
        "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":" + contaminatedAnswerLiteral + "}]}}\n" +
        "EOF\n" +
        "    ;;\n" +
        "  *)\n" +
        "    echo 'unexpected fixture mode' >&2\n" +
        "    exit 1\n" +
        "    ;;\n" +
        "esac\n" +
        "exit 0\n";
}

static string BuildTraceSliceProposalAnswer(string prompt, bool includeUnsafePolicy, bool includeMissingPolicyField)
{
    var policy = new Dictionary<string, object?>
    {
        ["includeStatePayloads"] = true,
        ["includeDeltaDecodedOps"] = true,
        ["includeArtifactContents"] = includeUnsafePolicy,
        ["includeGitDiffs"] = includeUnsafePolicy,
        ["includeStdoutStderr"] = includeUnsafePolicy,
        ["includeJsonl"] = includeUnsafePolicy,
    };

    if (includeMissingPolicyField)
    {
        policy.Remove("includeJsonl");
    }

    var proposal = new Dictionary<string, object?>
    {
        ["type"] = "rufus.trace-slice-proposal",
        ["schemaVersion"] = 1,
        ["prompt"] = new Dictionary<string, object?>
        {
            ["text"] = prompt,
            ["isExcerpt"] = false,
        },
        ["intent"] = new Dictionary<string, object?>
        {
            ["kind"] = "build-trace-slice",
            ["summary"] = "Fixture proposal for trace-slice LLM hardening tests.",
            ["source"] = "intent-inference-agent",
        },
        ["requestedSelection"] = new Dictionary<string, object?>
        {
            ["stateIds"] = Array.Empty<string>(),
            ["deltaIds"] = Array.Empty<string>(),
            ["anchorIds"] = Array.Empty<string>(),
            ["artifactRefs"] = Array.Empty<string>(),
        },
        ["requestedMaterializationPolicy"] = policy,
        ["rationale"] = Array.Empty<string>(),
        ["confidence"] = 1.0,
        ["warnings"] = Array.Empty<string>(),
    };

    return JsonSerializer.Serialize(proposal);
}

static string BuildTraceSliceProposalContaminatedAnswer(string prompt)
{
    var proposal = new Dictionary<string, object?>
    {
        ["type"] = "rufus.trace-slice-proposal",
        ["schemaVersion"] = 1,
        ["prompt"] = new Dictionary<string, object?>
        {
            ["text"] = prompt + " ```json",
            ["isExcerpt"] = false,
        },
        ["intent"] = new Dictionary<string, object?>
        {
            ["kind"] = "build-trace-slice",
            ["summary"] = "Fixture proposal with diff --git and message_update contamination.",
            ["source"] = "intent-inference-agent",
        },
        ["requestedSelection"] = new Dictionary<string, object?>
        {
            ["stateIds"] = Array.Empty<string>(),
            ["deltaIds"] = Array.Empty<string>(),
            ["anchorIds"] = Array.Empty<string>(),
            ["artifactRefs"] = Array.Empty<string>(),
        },
        ["requestedMaterializationPolicy"] = new Dictionary<string, object?>
        {
            ["includeStatePayloads"] = false,
            ["includeDeltaDecodedOps"] = false,
            ["includeArtifactContents"] = false,
            ["includeGitDiffs"] = false,
            ["includeStdoutStderr"] = false,
            ["includeJsonl"] = false,
        },
        ["rationale"] = new[]
        {
            "diff --git a/a b/b",
            "message_update",
            "assistantMessageEvent",
            ".rfs/rck",
        },
        ["confidence"] = 0.1,
        ["warnings"] = new[]
        {
            "message_end",
            "stdout",
            "stderr",
        },
    };

    return JsonSerializer.Serialize(proposal);
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
    List<string> failures,
    string fixtureMode,
    bool expectSuccess = true,
    string? expectedErrorContains = null)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-trace-slice-proposal-llm-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    var piScriptPath = Path.Combine(tempRoot, "pi");
    var originalPath = Environment.GetEnvironmentVariable("PATH");
    var originalFixtureMode = Environment.GetEnvironmentVariable("RFS_PI_TRACE_SLICE_FIXTURE_MODE");

    try
    {
        await File.WriteAllTextAsync(piScriptPath, BuildPiFixtureScript(prompt));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                piScriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        Environment.SetEnvironmentVariable("PATH", tempRoot + Path.PathSeparator + (originalPath ?? string.Empty));
        Environment.SetEnvironmentVariable("RFS_PI_TRACE_SLICE_FIXTURE_MODE", fixtureMode);

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
        if (expectSuccess)
        {
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
        else
        {
            if (proposalResult.ExitCode == 0)
            {
                failures.Add($"[{name}] expected non-zero exit code for failing fixture mode '{fixtureMode}'.");
                return;
            }

            if (string.IsNullOrWhiteSpace(proposalResult.Stderr))
            {
                failures.Add($"[{name}] expected stderr for failing fixture mode '{fixtureMode}'.");
            }

            if (!string.IsNullOrWhiteSpace(expectedErrorContains) &&
                (string.IsNullOrWhiteSpace(proposalResult.Stderr) || !proposalResult.Stderr.Contains(expectedErrorContains, StringComparison.Ordinal)))
            {
                failures.Add($"[{name}] expected stderr containing '{expectedErrorContains}' but got: {proposalResult.Stderr.Trim()}.");
            }
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        Environment.SetEnvironmentVariable("PATH", originalPath);
        Environment.SetEnvironmentVariable("RFS_PI_TRACE_SLICE_FIXTURE_MODE", originalFixtureMode);

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
    List<string> failures,
    string fixtureMode,
    bool expectSuccess = true,
    string? expectedErrorContains = null)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-trace-slice-validate-llm-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    var piScriptPath = Path.Combine(tempRoot, "pi");
    var originalPath = Environment.GetEnvironmentVariable("PATH");
    var originalFixtureMode = Environment.GetEnvironmentVariable("RFS_PI_TRACE_SLICE_FIXTURE_MODE");

    try
    {
        await File.WriteAllTextAsync(piScriptPath, BuildPiFixtureScript(prompt));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                piScriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        Environment.SetEnvironmentVariable("PATH", tempRoot + Path.PathSeparator + (originalPath ?? string.Empty));
        Environment.SetEnvironmentVariable("RFS_PI_TRACE_SLICE_FIXTURE_MODE", fixtureMode);

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
        if (expectSuccess)
        {
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
        else
        {
            if (validateResult.ExitCode == 0)
            {
                failures.Add($"[{name}] expected non-zero exit code for failing fixture mode '{fixtureMode}'.");
                return;
            }

            if (string.IsNullOrWhiteSpace(validateResult.Stderr))
            {
                failures.Add($"[{name}] expected stderr for failing fixture mode '{fixtureMode}'.");
            }

            if (!string.IsNullOrWhiteSpace(expectedErrorContains) &&
                (string.IsNullOrWhiteSpace(validateResult.Stderr) || !validateResult.Stderr.Contains(expectedErrorContains, StringComparison.Ordinal)))
            {
                failures.Add($"[{name}] expected stderr containing '{expectedErrorContains}' but got: {validateResult.Stderr.Trim()}.");
            }
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        Environment.SetEnvironmentVariable("PATH", originalPath);
        Environment.SetEnvironmentVariable("RFS_PI_TRACE_SLICE_FIXTURE_MODE", originalFixtureMode);

        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}


static async Task RunRckTraceSliceProposalValidatorCriticalCasesAsync(List<string> failures)
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-trace-slice-validator-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[RckTraceSliceProposalValidator] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        CreateValidatorWorkspaceFixture(tempRoot);

        string BuildProposal(
            IReadOnlyList<string> stateIds,
            IReadOnlyList<string> deltaIds,
            IReadOnlyList<string> anchorIds,
            IReadOnlyList<string> artifactRefs,
            bool unsafePolicy)
        {
            var proposal = new Dictionary<string, object?>
            {
                ["type"] = "rufus.trace-slice-proposal",
                ["schemaVersion"] = 1,
                ["prompt"] = new Dictionary<string, object?>
                {
                    ["text"] = "Trace slice validation fixture prompt.",
                    ["isExcerpt"] = false,
                },
                ["intent"] = new Dictionary<string, object?>
                {
                    ["kind"] = "build-trace-slice",
                    ["summary"] = "Validator fixture",
                    ["source"] = "intent-inference-agent",
                },
                ["requestedSelection"] = new Dictionary<string, object?>
                {
                    ["stateIds"] = stateIds,
                    ["deltaIds"] = deltaIds,
                    ["anchorIds"] = anchorIds,
                    ["artifactRefs"] = artifactRefs,
                },
                ["requestedMaterializationPolicy"] = new Dictionary<string, object?>
                {
                    ["includeStatePayloads"] = true,
                    ["includeDeltaDecodedOps"] = true,
                    ["includeArtifactContents"] = unsafePolicy,
                    ["includeGitDiffs"] = unsafePolicy,
                    ["includeStdoutStderr"] = unsafePolicy,
                    ["includeJsonl"] = unsafePolicy,
                },
                ["rationale"] = Array.Empty<string>(),
                ["warnings"] = Array.Empty<string>(),
                ["confidence"] = 1.0,
            };

            return JsonSerializer.Serialize(proposal);
        }

        void AssertValidationCase(string name, string proposalJson, int maxStates, int maxDeltas, Action<JsonElement> verify)
        {
            try
            {
                var result = RckTraceSliceProposalValidator.Validate(proposalJson, tempRoot, maxStates, maxDeltas);
                if (!result.Success || string.IsNullOrWhiteSpace(result.Json))
                {
                    failures.Add($"[{name}] expected validation success but got failure: {result.ErrorMessage ?? "(null)"}.");
                    return;
                }

                using var document = JsonDocument.Parse(result.Json);
                verify(document.RootElement);
            }
            catch (Exception ex)
            {
                failures.Add($"[{name}] threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        static List<string> ExtractTargets(JsonElement root, string propertyName)
        {
            var validation = root.GetProperty("validation");
            return validation.GetProperty(propertyName)
                .EnumerateArray()
                .Select(item => item.GetProperty("target").GetString() ?? string.Empty)
                .ToList();
        }

        AssertValidationCase(
            name: "proposal accepted",
            proposalJson: BuildProposal(
                stateIds: new[] { "state-head" },
                deltaIds: new[] { "delta-main" },
                anchorIds: new[] { "anchor-head" },
                artifactRefs: Array.Empty<string>(),
                unsafePolicy: false),
            maxStates: 5,
            maxDeltas: 5,
            verify: root =>
            {
                var validation = root.GetProperty("validation");
                if (!string.Equals(validation.GetProperty("status").GetString(), "accepted", StringComparison.Ordinal))
                {
                    failures.Add("[proposal accepted] expected validation.status=accepted.");
                }

                foreach (var propertyName in new[] { "includeArtifactContents", "includeGitDiffs", "includeStdoutStderr", "includeJsonl" })
                {
                    if (root.GetProperty("materializationPolicy").GetProperty(propertyName).ValueKind != JsonValueKind.False)
                    {
                        failures.Add($"[proposal accepted] expected {propertyName}=false.");
                    }
                }

                if (!ExtractTargets(root, "accepted").Contains("state:state-head", StringComparer.Ordinal))
                {
                    failures.Add("[proposal accepted] expected accepted state:state-head.");
                }

                if (!ExtractTargets(root, "accepted").Contains("delta:delta-main", StringComparer.Ordinal))
                {
                    failures.Add("[proposal accepted] expected accepted delta:delta-main.");
                }

                if (!ExtractTargets(root, "accepted").Contains("anchor:anchor-head", StringComparer.Ordinal))
                {
                    failures.Add("[proposal accepted] expected accepted anchor:anchor-head.");
                }
            });

        AssertValidationCase(
            name: "missing state rejected",
            proposalJson: BuildProposal(
                stateIds: new[] { "missing-state" },
                deltaIds: Array.Empty<string>(),
                anchorIds: Array.Empty<string>(),
                artifactRefs: Array.Empty<string>(),
                unsafePolicy: false),
            maxStates: 5,
            maxDeltas: 5,
            verify: root =>
            {
                var validation = root.GetProperty("validation");
                if (!string.Equals(validation.GetProperty("status").GetString(), "rejected", StringComparison.Ordinal))
                {
                    failures.Add("[missing state rejected] expected validation.status=rejected.");
                }

                if (!ExtractTargets(root, "rejected").Contains("state:missing-state", StringComparer.Ordinal))
                {
                    failures.Add("[missing state rejected] expected rejected state:missing-state.");
                }
            });

        AssertValidationCase(
            name: "missing delta rejected",
            proposalJson: BuildProposal(
                stateIds: Array.Empty<string>(),
                deltaIds: new[] { "missing-delta" },
                anchorIds: Array.Empty<string>(),
                artifactRefs: Array.Empty<string>(),
                unsafePolicy: false),
            maxStates: 5,
            maxDeltas: 5,
            verify: root =>
            {
                var validation = root.GetProperty("validation");
                if (!string.Equals(validation.GetProperty("status").GetString(), "rejected", StringComparison.Ordinal))
                {
                    failures.Add("[missing delta rejected] expected validation.status=rejected.");
                }

                if (!ExtractTargets(root, "rejected").Contains("delta:missing-delta", StringComparer.Ordinal))
                {
                    failures.Add("[missing delta rejected] expected rejected delta:missing-delta.");
                }
            });

        AssertValidationCase(
            name: "unsafe policy downgraded",
            proposalJson: BuildProposal(
                stateIds: new[] { "state-head" },
                deltaIds: new[] { "delta-main" },
                anchorIds: new[] { "anchor-head" },
                artifactRefs: Array.Empty<string>(),
                unsafePolicy: true),
            maxStates: 5,
            maxDeltas: 5,
            verify: root =>
            {
                var validation = root.GetProperty("validation");
                if (!string.Equals(validation.GetProperty("status").GetString(), "partial", StringComparison.Ordinal))
                {
                    failures.Add("[unsafe policy downgraded] expected validation.status=partial.");
                }

                var downgradedTargets = ExtractTargets(root, "downgraded");
                foreach (var propertyName in new[] { "materializationPolicy.includeArtifactContents", "materializationPolicy.includeGitDiffs", "materializationPolicy.includeStdoutStderr", "materializationPolicy.includeJsonl" })
                {
                    if (!downgradedTargets.Contains(propertyName, StringComparer.Ordinal))
                    {
                        failures.Add($"[unsafe policy downgraded] expected downgraded {propertyName}.");
                    }

                    if (root.GetProperty("materializationPolicy").GetProperty(propertyName.Split('.')[1]).ValueKind != JsonValueKind.False)
                    {
                        failures.Add($"[unsafe policy downgraded] expected {propertyName} to be false.");
                    }
                }
            });

        AssertValidationCase(
            name: "limits reject overflow",
            proposalJson: BuildProposal(
                stateIds: new[] { "state-head", "state-base" },
                deltaIds: new[] { "delta-main", "delta-extra" },
                anchorIds: new[] { "anchor-head" },
                artifactRefs: Array.Empty<string>(),
                unsafePolicy: false),
            maxStates: 1,
            maxDeltas: 1,
            verify: root =>
            {
                var validation = root.GetProperty("validation");
                if (!string.Equals(validation.GetProperty("status").GetString(), "partial", StringComparison.Ordinal)
                    && !string.Equals(validation.GetProperty("status").GetString(), "rejected", StringComparison.Ordinal))
                {
                    failures.Add("[limits reject overflow] expected validation.status partial or rejected.");
                }

                if (!ExtractTargets(root, "accepted").Contains("state:state-head", StringComparer.Ordinal))
                {
                    failures.Add("[limits reject overflow] expected accepted state:state-head.");
                }

                if (!ExtractTargets(root, "rejected").Contains("state:state-base", StringComparer.Ordinal))
                {
                    failures.Add("[limits reject overflow] expected rejected state:state-base.");
                }

                if (!ExtractTargets(root, "accepted").Contains("delta:delta-main", StringComparer.Ordinal))
                {
                    failures.Add("[limits reject overflow] expected accepted delta:delta-main.");
                }

                if (!ExtractTargets(root, "rejected").Contains("delta:delta-extra", StringComparer.Ordinal))
                {
                    failures.Add("[limits reject overflow] expected rejected delta:delta-extra.");
                }
            });

        AssertValidationCase(
            name: "artifact exclusions reject protected paths",
            proposalJson: BuildProposal(
                stateIds: new[] { "state-head" },
                deltaIds: new[] { "delta-main" },
                anchorIds: new[] { "anchor-head" },
                artifactRefs: new[] { ".rfs/rck/HEAD", "bin/generated.txt", "obj/generated.txt", "notes/selected.md" },
                unsafePolicy: false),
            maxStates: 5,
            maxDeltas: 5,
            verify: root =>
            {
                var rejectedTargets = ExtractTargets(root, "rejected");
                foreach (var target in new[] { "artifact:.rfs/rck/HEAD", "artifact:bin/generated.txt", "artifact:obj/generated.txt" })
                {
                    if (!rejectedTargets.Contains(target, StringComparer.Ordinal))
                    {
                        failures.Add($"[artifact exclusions reject protected paths] expected rejected {target}.");
                    }
                }
            });
    }
    catch (Exception ex)
    {
        failures.Add($"[RckTraceSliceProposalValidator] threw {ex.GetType().Name}: {ex.Message}");
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

static void CreateValidatorWorkspaceFixture(string tempRoot)
{
    var rfsRoot = Path.Combine(tempRoot, ".rfs");
    var rckRoot = Path.Combine(rfsRoot, "rck");
    var statesRoot = Path.Combine(rckRoot, "states");
    var deltasRoot = Path.Combine(rckRoot, "deltas");
    var anchorsRoot = Path.Combine(rckRoot, "anchors");

    Directory.CreateDirectory(statesRoot);
    Directory.CreateDirectory(deltasRoot);
    Directory.CreateDirectory(anchorsRoot);

    File.WriteAllText(Path.Combine(rckRoot, "HEAD"), "state-head" + Environment.NewLine);

    var stateBasePayload = JsonSerializer.Serialize(new
    {
        type = "fixture.state",
        artifacts = Array.Empty<object>(),
    });

    var stateHeadPayload = JsonSerializer.Serialize(new
    {
        type = "fixture.state",
        artifacts = new[]
        {
            new
            {
                path = "notes/selected.md",
                changeType = "modified",
                source = "fixture",
            },
        },
    });

    File.WriteAllText(
        Path.Combine(statesRoot, "state-base.json"),
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["type"] = "rufus.rck.state",
            ["id"] = "state-base",
            ["payloadCanonicalJson"] = stateBasePayload,
            ["refs"] = Array.Empty<object>(),
            ["meta"] = new Dictionary<string, object?>
            {
                ["createdAtUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                ["CreatedBy"] = "fixture",
                ["Label"] = "base",
                ["Reason"] = "validator fixture",
            },
        }));

    File.WriteAllText(
        Path.Combine(statesRoot, "state-head.json"),
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["type"] = "rufus.rck.state",
            ["id"] = "state-head",
            ["payloadCanonicalJson"] = stateHeadPayload,
            ["refs"] = Array.Empty<object>(),
            ["meta"] = new Dictionary<string, object?>
            {
                ["createdAtUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                ["CreatedBy"] = "fixture",
                ["Label"] = "head",
                ["Reason"] = "validator fixture",
            },
        }));

    File.WriteAllText(
        Path.Combine(deltasRoot, "delta-main.json"),
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["type"] = "rufus.rck.delta",
            ["id"] = "delta-main",
            ["fromStateId"] = "state-base",
            ["toStateId"] = "state-head",
            ["ops"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["kind"] = "replace",
                    ["path"] = "notes/selected.md",
                    ["valueJson"] = JsonSerializer.Serialize(new { text = "selected" }),
                },
            },
            ["refs"] = Array.Empty<object>(),
            ["evidenceRefs"] = Array.Empty<object>(),
            ["meta"] = new Dictionary<string, object?>
            {
                ["createdAtUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                ["CreatedBy"] = "fixture",
                ["Label"] = "delta main",
                ["Reason"] = "validator fixture",
            },
        }));

    File.WriteAllText(
        Path.Combine(deltasRoot, "delta-extra.json"),
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["type"] = "rufus.rck.delta",
            ["id"] = "delta-extra",
            ["fromStateId"] = "state-head",
            ["toStateId"] = "state-base",
            ["ops"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["kind"] = "replace",
                    ["path"] = "notes/other.md",
                    ["valueJson"] = JsonSerializer.Serialize(new { text = "other" }),
                },
            },
            ["refs"] = Array.Empty<object>(),
            ["evidenceRefs"] = Array.Empty<object>(),
            ["meta"] = new Dictionary<string, object?>
            {
                ["createdAtUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                ["CreatedBy"] = "fixture",
                ["Label"] = "delta extra",
                ["Reason"] = "validator fixture",
            },
        }));

    File.WriteAllText(
        Path.Combine(anchorsRoot, "anchor-head.json"),
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["type"] = "rufus.rck.anchor",
            ["id"] = "anchor-head",
            ["stateId"] = "state-head",
            ["parentAnchorIds"] = Array.Empty<object>(),
            ["meta"] = new Dictionary<string, object?>
            {
                ["createdAtUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                ["CreatedBy"] = "fixture",
                ["Label"] = "anchor head",
                ["Reason"] = "validator fixture",
            },
        }));
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


static async Task RunRfsTuiInitializedSessionCaseAsync(string name, List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-initialized-checks", Guid.NewGuid().ToString("N"));
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

        var statusBefore = RckWorkspaceStatusReader.Read(tempRoot);
        var tuiResult = await RunProcessAsyncWithInput(tempRoot, "/status\n/help\n/exit\n", "dotnet", "run", "--project", cliProjectPath, "--");
        if (tuiResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {tuiResult.ExitCode}. stderr: {tuiResult.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(tuiResult.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {tuiResult.Stderr.Trim()}.");
        }

        var repoName = Path.GetFileName(Path.TrimEndingDirectorySeparator(tempRoot));
        var requiredFragments = new[]
        {
            $"RFS · {repoName}",
            "Model:",
            "RCK: states",
            "Git:",
            "rfs status",
            "Prompt processing modes will be implemented in later PT phases.",
        };

        foreach (var fragment in requiredFragments)
        {
            if (!tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
            }
        }

        if (tuiResult.Stdout.Contains("Workspace not initialized.", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected initialized session to skip auto-init messaging.");
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        if (statusAfter.StateCount != statusBefore.StateCount || statusAfter.DeltaCount != statusBefore.DeltaCount || statusAfter.AnchorCount != statusBefore.AnchorCount)
        {
            failures.Add($"[{name}] expected bare session to leave RCK counts unchanged.");
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

static async Task RunRfsTuiAutoInitSessionCaseAsync(string name, List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-autoinit-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        var statusBefore = RckWorkspaceStatusReader.Read(tempRoot);
        if (statusBefore.Initialized)
        {
            failures.Add($"[{name}] expected the repo to start without an RFS workspace.");
            return;
        }

        var tuiResult = await RunProcessAsyncWithInput(tempRoot, "/exit\n", "dotnet", "run", "--project", cliProjectPath, "--");
        if (tuiResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {tuiResult.ExitCode}. stderr: {tuiResult.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(tuiResult.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {tuiResult.Stderr.Trim()}.");
        }

        var requiredFragments = new[]
        {
            "Workspace not initialized.",
            "Initializing RFS workspace...",
            "✓ .rfs created",
            "✓ RCK initialized",
            "✓ genesis state created",
            "✓ genesis anchor created",
            "Entering RFS session.",
        };

        foreach (var fragment in requiredFragments)
        {
            if (!tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
            }
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        if (!statusAfter.Initialized || statusAfter.StateCount < 1 || statusAfter.AnchorCount < 1)
        {
            failures.Add($"[{name}] expected auto-init to create the RFS workspace and genesis objects.");
        }

        var rfsRoot = Path.Combine(tempRoot, ".rfs");
        var headPath = Path.Combine(rfsRoot, "rck", "HEAD");
        if (!Directory.Exists(rfsRoot) || !File.Exists(headPath))
        {
            failures.Add($"[{name}] expected .rfs and .rck/HEAD to be created.");
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
