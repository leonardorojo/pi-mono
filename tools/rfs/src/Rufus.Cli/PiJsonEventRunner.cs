using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Rufus.Cli.PiIntegration;

public sealed record PiJsonAskResult(
    bool Success,
    string Prompt,
    string Answer,
    string? ErrorMessage,
    string? Provider,
    string? Model);

public static class PiJsonEventRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

    public static async Task<PiJsonAskResult> RunAskAsync(
        string workingDirectory,
        string prompt,
        string? workspaceModel,
        CancellationToken cancellationToken = default)
    {
        var trimmedPrompt = prompt.Trim();
        if (string.IsNullOrWhiteSpace(trimmedPrompt))
        {
            return new PiJsonAskResult(false, prompt, string.Empty, "Missing prompt.", null, null);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "pi",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory
        };

        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("--no-session");
        startInfo.ArgumentList.Add("--no-tools");
        startInfo.ArgumentList.Add("--no-extensions");
        startInfo.ArgumentList.Add("--no-context-files");
        ApplyWorkspaceModel(startInfo, workspaceModel);
        startInfo.ArgumentList.Add(trimmedPrompt);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return new PiJsonAskResult(false, trimmedPrompt, string.Empty, "Failed to start pi JSON process.", null, null);
            }
        }
        catch (Exception ex)
        {
            return new PiJsonAskResult(false, trimmedPrompt, string.Empty, $"Failed to start pi JSON process: {ex.Message}", null, null);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DefaultTimeout);

        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            var parsed = await ReadEventsAsync(process.StandardOutput, timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            var stderrText = await stderrTask;

            if (!parsed.Success)
            {
                return new PiJsonAskResult(
                    false,
                    trimmedPrompt,
                    string.Empty,
                    CombineError(parsed.ErrorMessage, stderrText),
                    parsed.Provider,
                    parsed.Model);
            }

            if (process.ExitCode != 0)
            {
                return new PiJsonAskResult(
                    false,
                    trimmedPrompt,
                    string.Empty,
                    CombineError($"pi JSON process exited with code {process.ExitCode}.", stderrText),
                    parsed.Provider,
                    parsed.Model);
            }

            if (string.IsNullOrWhiteSpace(parsed.Answer))
            {
                return new PiJsonAskResult(
                    false,
                    trimmedPrompt,
                    string.Empty,
                    CombineError("Pi JSON mode completed without a final assistant answer.", stderrText),
                    parsed.Provider,
                    parsed.Model);
            }

            return new PiJsonAskResult(true, trimmedPrompt, parsed.Answer, null, parsed.Provider, parsed.Model);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            var stderrText = await AwaitStderrAfterKillAsync(stderrTask);
            return new PiJsonAskResult(
                false,
                trimmedPrompt,
                string.Empty,
                CombineError($"Timed out waiting for Pi JSON response after {DefaultTimeout.TotalSeconds:0} seconds.", stderrText),
                null,
                null);
        }
        catch (JsonException ex)
        {
            TryKill(process);
            var stderrText = await AwaitStderrAfterKillAsync(stderrTask);
            return new PiJsonAskResult(
                false,
                trimmedPrompt,
                string.Empty,
                CombineError($"Pi JSON mode returned invalid JSONL: {ex.Message}", stderrText),
                null,
                null);
        }
        catch (Exception ex)
        {
            TryKill(process);
            var stderrText = await AwaitStderrAfterKillAsync(stderrTask);
            return new PiJsonAskResult(
                false,
                trimmedPrompt,
                string.Empty,
                CombineError($"Pi JSON mode failed: {ex.Message}", stderrText),
                null,
                null);
        }
    }

    private static async Task<ParsedPiJsonResult> ReadEventsAsync(StreamReader stdout, CancellationToken cancellationToken)
    {
        var deltaBuilder = new StringBuilder();
        string? structuredAnswer = null;
        string? provider = null;
        string? model = null;
        string? explicitError = null;
        var completionObserved = false;
        var lineNumber = 0;

        while (true)
        {
            var line = await stdout.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException ex)
            {
                throw new JsonException($"Invalid JSONL on line {lineNumber}: {ex.Message}");
            }

            using (document)
            {
                var root = document.RootElement;
                var type = GetString(root, "type") ?? throw new JsonException($"Event line {lineNumber} is missing a string 'type' property.");

                switch (type)
                {
                    case "session":
                    case "agent_start":
                    case "turn_start":
                    case "message_start":
                    case "tool_execution_start":
                    case "tool_execution_update":
                    case "tool_execution_end":
                    case "queue_update":
                    case "auto_retry_start":
                        break;

                    case "message_update":
                        CaptureAssistantMetadata(root, ref provider, ref model);
                        AppendTextDelta(root, deltaBuilder);
                        break;

                    case "message_end":
                        CaptureAssistantMetadata(root, ref provider, ref model);
                        if (TryGetAssistantMessage(root, "message", out var assistantMessageAtEnd))
                        {
                            var answerFromMessageEnd = ExtractTextFromMessage(assistantMessageAtEnd);
                            if (!string.IsNullOrWhiteSpace(answerFromMessageEnd))
                            {
                                structuredAnswer = answerFromMessageEnd;
                                completionObserved = true;
                            }
                        }
                        break;

                    case "turn_end":
                        completionObserved = true;
                        CaptureAssistantMetadata(root, ref provider, ref model);
                        if (TryGetAssistantMessage(root, "message", out var turnMessage))
                        {
                            var answerFromTurnEnd = ExtractTextFromMessage(turnMessage);
                            if (!string.IsNullOrWhiteSpace(answerFromTurnEnd))
                            {
                                structuredAnswer = answerFromTurnEnd;
                            }
                        }
                        break;

                    case "agent_end":
                        completionObserved = true;
                        if (TryExtractLastAssistantText(root, ref provider, ref model, out var agentEndAnswer) && !string.IsNullOrWhiteSpace(agentEndAnswer))
                        {
                            structuredAnswer = agentEndAnswer;
                        }
                        break;

                    case "compaction_end":
                        if (root.TryGetProperty("aborted", out var abortedElement)
                            && abortedElement.ValueKind == JsonValueKind.True
                            && (!root.TryGetProperty("willRetry", out var willRetryElement) || willRetryElement.ValueKind == JsonValueKind.False))
                        {
                            explicitError = GetString(root, "errorMessage") ?? "Pi JSON mode reported a compaction failure.";
                        }
                        break;

                    case "auto_retry_end":
                        if (root.TryGetProperty("success", out var successElement)
                            && successElement.ValueKind == JsonValueKind.False)
                        {
                            explicitError = GetString(root, "finalError") ?? "Pi JSON mode exhausted retries.";
                        }
                        break;

                    default:
                        break;
                }
            }
        }

        var answer = string.IsNullOrWhiteSpace(structuredAnswer)
            ? deltaBuilder.ToString().Trim()
            : structuredAnswer.Trim();

        if (string.IsNullOrWhiteSpace(answer))
        {
            var errorMessage = explicitError
                ?? (completionObserved
                    ? "Pi JSON stream completed without an assistant answer."
                    : "Pi JSON stream ended before a final assistant answer was observed.");

            return new ParsedPiJsonResult(false, errorMessage, string.Empty, provider, model);
        }

        return new ParsedPiJsonResult(true, null, answer, provider, model);
    }

    private static void ApplyWorkspaceModel(ProcessStartInfo startInfo, string? workspaceModel)
    {
        if (string.IsNullOrWhiteSpace(workspaceModel))
        {
            return;
        }

        var trimmedModel = workspaceModel.Trim();
        if (trimmedModel.Contains('/', StringComparison.Ordinal))
        {
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(trimmedModel);
            return;
        }

        startInfo.Environment["RUFUSCHAT_LLM_MODEL"] = trimmedModel;
    }

    private static void CaptureAssistantMetadata(JsonElement element, ref string? provider, ref string? model)
    {
        if (TryGetAssistantMessage(element, "message", out var assistantMessage))
        {
            provider ??= GetString(assistantMessage, "provider");
            model ??= GetString(assistantMessage, "model");
        }
    }

    private static void AppendTextDelta(JsonElement eventElement, StringBuilder deltaBuilder)
    {
        if (!eventElement.TryGetProperty("assistantMessageEvent", out var assistantMessageEvent)
            || assistantMessageEvent.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!string.Equals(GetString(assistantMessageEvent, "type"), "text_delta", StringComparison.Ordinal))
        {
            return;
        }

        var delta = GetString(assistantMessageEvent, "delta");
        if (!string.IsNullOrEmpty(delta))
        {
            deltaBuilder.Append(delta);
        }
    }

    private static bool TryGetAssistantMessage(JsonElement parent, string propertyName, out JsonElement assistantMessage)
    {
        assistantMessage = default;
        if (!parent.TryGetProperty(propertyName, out var messageElement) || messageElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!string.Equals(GetString(messageElement, "role"), "assistant", StringComparison.Ordinal))
        {
            return false;
        }

        assistantMessage = messageElement;
        return true;
    }

    private static bool TryExtractLastAssistantText(
        JsonElement agentEndElement,
        ref string? provider,
        ref string? model,
        out string? answer)
    {
        answer = null;
        if (!agentEndElement.TryGetProperty("messages", out var messagesElement) || messagesElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var messageElement in messagesElement.EnumerateArray().Reverse())
        {
            if (messageElement.ValueKind != JsonValueKind.Object
                || !string.Equals(GetString(messageElement, "role"), "assistant", StringComparison.Ordinal))
            {
                continue;
            }

            provider ??= GetString(messageElement, "provider");
            model ??= GetString(messageElement, "model");

            var text = ExtractTextFromMessage(messageElement);
            if (!string.IsNullOrWhiteSpace(text))
            {
                answer = text;
                return true;
            }
        }

        return false;
    }

    private static string ExtractTextFromMessage(JsonElement messageElement)
    {
        if (!messageElement.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var textBuilder = new StringBuilder();

        foreach (var item in contentElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !string.Equals(GetString(item, "type"), "text", StringComparison.Ordinal))
            {
                continue;
            }

            var text = GetString(item, "text");
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            textBuilder.Append(text);
        }

        return textBuilder.ToString();
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
        var trimmedPrimaryError = string.IsNullOrWhiteSpace(primaryError) ? "Pi JSON request failed." : primaryError.Trim();
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

    private sealed record ParsedPiJsonResult(
        bool Success,
        string? ErrorMessage,
        string Answer,
        string? Provider,
        string? Model);
}
