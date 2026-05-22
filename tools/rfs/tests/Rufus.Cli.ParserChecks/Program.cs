using System.Diagnostics;
using System.Runtime.InteropServices;
using Rufus.Cli.PiIntegration;

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
