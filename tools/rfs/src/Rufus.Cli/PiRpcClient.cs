using System.Diagnostics;
using System.Text.Json;

namespace Rufus.Cli.PiIntegration;

public sealed record PiRpcAvailableModel(
    string Id,
    string Provider,
    string? DisplayName);

public sealed record PiRpcModelListResult(
    bool Success,
    string? ErrorMessage,
    IReadOnlyList<PiRpcAvailableModel> Models);

public static class PiRpcClient
{
    private const string GetAvailableModelsCommand = "get_available_models";
    private const string RequestId = "rfs-model-list-1";
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public static async Task<PiRpcModelListResult> GetAvailableModelsAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pi",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory
        };

        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("rpc");
        startInfo.ArgumentList.Add("--no-session");
        startInfo.ArgumentList.Add("--no-extensions");
        startInfo.ArgumentList.Add("--no-context-files");

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return new PiRpcModelListResult(false, "Failed to start pi RPC process.", []);
            }
        }
        catch (Exception ex)
        {
            return new PiRpcModelListResult(false, $"Failed to start pi RPC process: {ex.Message}", []);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DefaultTimeout);

        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
        try
        {
            var requestJson = JsonSerializer.Serialize(new
            {
                id = RequestId,
                type = GetAvailableModelsCommand
            });

            await process.StandardInput.WriteLineAsync(requestJson);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            var response = await ReadModelListResponseAsync(process.StandardOutput, timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            var stderrText = await stderrTask;

            if (!response.Success)
            {
                return new PiRpcModelListResult(false, CombineError(response.ErrorMessage, stderrText), []);
            }

            if (process.ExitCode != 0)
            {
                return new PiRpcModelListResult(
                    false,
                    CombineError($"pi RPC process exited with code {process.ExitCode}.", stderrText),
                    []);
            }

            return new PiRpcModelListResult(true, null, response.Models);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            var stderrText = await AwaitStderrAfterKillAsync(stderrTask);
            return new PiRpcModelListResult(
                false,
                CombineError($"Timed out waiting for Pi RPC response after {DefaultTimeout.TotalSeconds:0} seconds.", stderrText),
                []);
        }
        catch (JsonException ex)
        {
            TryKill(process);
            var stderrText = await AwaitStderrAfterKillAsync(stderrTask);
            return new PiRpcModelListResult(
                false,
                CombineError($"Pi RPC returned invalid JSON: {ex.Message}", stderrText),
                []);
        }
        catch (Exception ex)
        {
            TryKill(process);
            var stderrText = await AwaitStderrAfterKillAsync(stderrTask);
            return new PiRpcModelListResult(false, CombineError($"Pi RPC failed: {ex.Message}", stderrText), []);
        }
    }

    private static async Task<PiRpcModelListResult> ReadModelListResponseAsync(
        StreamReader stdout,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await stdout.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return new PiRpcModelListResult(false, "Pi RPC closed without returning get_available_models response.", []);
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var typeElement) || !string.Equals(typeElement.GetString(), "response", StringComparison.Ordinal))
            {
                continue;
            }

            var responseId = GetString(root, "id");
            if (!string.IsNullOrWhiteSpace(responseId) && !string.Equals(responseId, RequestId, StringComparison.Ordinal))
            {
                continue;
            }

            var command = GetString(root, "command");
            if (string.IsNullOrWhiteSpace(command) || !string.Equals(command, GetAvailableModelsCommand, StringComparison.Ordinal))
            {
                continue;
            }

            if (!root.TryGetProperty("success", out var successElement) || successElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            {
                return new PiRpcModelListResult(false, "Pi RPC response did not include a valid success flag.", []);
            }

            if (!successElement.GetBoolean())
            {
                return new PiRpcModelListResult(
                    false,
                    GetString(root, "error") ?? "Pi RPC returned an error for get_available_models.",
                    []);
            }

            if (!root.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Object)
            {
                return new PiRpcModelListResult(false, "Pi RPC response did not include a data object.", []);
            }

            if (!dataElement.TryGetProperty("models", out var modelsElement) || modelsElement.ValueKind != JsonValueKind.Array)
            {
                return new PiRpcModelListResult(false, "Pi RPC response did not include a models array.", []);
            }

            var models = new List<PiRpcAvailableModel>();
            foreach (var modelElement in modelsElement.EnumerateArray())
            {
                if (modelElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = GetString(modelElement, "id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var provider = GetString(modelElement, "provider") ?? "(unknown-provider)";
                var displayName = GetString(modelElement, "name");
                models.Add(new PiRpcAvailableModel(id, provider, displayName));
            }

            return new PiRpcModelListResult(true, null, models);
        }
    }

    private static async Task<string> AwaitStderrAfterKillAsync(Task<string> stderrTask)
    {
        try
        {
            return await stderrTask;
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        catch
        {
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyValue) || propertyValue.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return propertyValue.GetString();
    }

    private static string CombineError(string? primaryError, string? stderrText)
    {
        var trimmedPrimaryError = string.IsNullOrWhiteSpace(primaryError) ? "Pi RPC request failed." : primaryError.Trim();
        if (string.IsNullOrWhiteSpace(stderrText))
        {
            return trimmedPrimaryError;
        }

        var singleLineStderr = string.Join(
            " | ",
            stderrText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return string.IsNullOrWhiteSpace(singleLineStderr)
            ? trimmedPrimaryError
            : $"{trimmedPrimaryError} stderr: {singleLineStderr}";
    }
}
