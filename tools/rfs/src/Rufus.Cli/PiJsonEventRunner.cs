using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace Rufus.Cli.PiIntegration;

public sealed record PiJsonAskResult(
    bool Success,
    string Prompt,
    string Answer,
    string? ErrorMessage,
    string? Provider,
    string? Model);

public sealed record PiJsonStreamEvent(
    string Type,
    string? Id = null,
    string? Name = null,
    string? Text = null,
    string? Summary = null,
    string? Details = null,
    string? Message = null);

public sealed record PiJsonAgentDetailedResult(
    bool Success,
    string Task,
    string Answer,
    string? ErrorMessage,
    string? Provider,
    string? Model,
    IReadOnlyList<PiJsonEventRunner.PiJsonToolEvent> ToolEvents,
    string StdErr,
    int? ExitCode,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long DurationMs,
    bool TimedOut,
    bool Cancelled,
    bool FailedToStart,
    string WorkingDirectory,
    int PromptBytes);

public static class PiJsonEventRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);
    private const int StdinPromptThresholdChars = 32_000;

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

        var useStdinPrompt = trimmedPrompt.Length > StdinPromptThresholdChars;

        var startInfo = new ProcessStartInfo
        {
            FileName = "pi",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = useStdinPrompt,
            WorkingDirectory = workingDirectory
        };

        if (useStdinPrompt)
        {
            startInfo.StandardInputEncoding = Encoding.UTF8;
        }

        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("--no-session");
        startInfo.ArgumentList.Add("--no-tools");
        startInfo.ArgumentList.Add("--no-extensions");
        startInfo.ArgumentList.Add("--no-context-files");
        ApplyWorkspaceModel(startInfo, workspaceModel);
        if (!useStdinPrompt)
        {
            startInfo.ArgumentList.Add(trimmedPrompt);
        }

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

        var startedAt = DateTimeOffset.UtcNow;

        if (useStdinPrompt)
        {
            await process.StandardInput.WriteAsync(trimmedPrompt);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();
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
            var transportMode = useStdinPrompt ? "stdin" : "argv";
            var elapsed = (DateTimeOffset.UtcNow - startedAt).TotalSeconds;
            return new PiJsonAskResult(
                false,
                trimmedPrompt,
                string.Empty,
                CombineError($"Timed out waiting for Pi JSON response after {elapsed:F0}s (limit {DefaultTimeout.TotalSeconds:0}s, transport={transportMode}, promptLen={trimmedPrompt.Length}).", stderrText),
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

    // Prototype: run an agent task in Pi JSON event-stream mode with a restricted read-only toolset.
    public sealed record PiJsonToolEvent(string Type, string? Id, string? Name, string? Details, string? Summary);

    public sealed record PiJsonAgentResult(
        bool Success,
        string Task,
        string Answer,
        string? ErrorMessage,
        string? Provider,
        string? Model,
        IReadOnlyList<PiJsonToolEvent> ToolEvents);

    public static async Task<PiJsonAgentResult> RunAgentAsync(
        string workingDirectory,
        string task,
        string? workspaceModel,
        CancellationToken cancellationToken = default)
    {
        var detailed = await RunAgentDetailedAsync(workingDirectory, task, workspaceModel, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new PiJsonAgentResult(
            detailed.Success,
            detailed.Task,
            detailed.Answer,
            detailed.ErrorMessage,
            detailed.Provider,
            detailed.Model,
            detailed.ToolEvents);
    }

    public static async Task<PiJsonAgentDetailedResult> RunAgentDetailedAsync(
        string workingDirectory,
        string task,
        string? workspaceModel,
        Action<PiJsonStreamEvent>? eventReporter = null,
        CancellationToken cancellationToken = default)
    {
        var trimmedTask = task.Trim();
        var startedAt = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(trimmedTask))
        {
            var finishedAt = DateTimeOffset.UtcNow;
            return new PiJsonAgentDetailedResult(
                false,
                task,
                string.Empty,
                "Missing task.",
                null,
                null,
                Array.Empty<PiJsonToolEvent>(),
                string.Empty,
                null,
                startedAt,
                finishedAt,
                (long)(finishedAt - startedAt).TotalMilliseconds,
                false,
                false,
                false,
                workingDirectory,
                0);
        }

        var promptBytes = Encoding.UTF8.GetByteCount(trimmedTask);
        var useStdinPrompt = trimmedTask.Length > StdinPromptThresholdChars;

        var startInfo = new ProcessStartInfo
        {
            FileName = "pi",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = useStdinPrompt,
            WorkingDirectory = workingDirectory
        };

        if (useStdinPrompt)
        {
            startInfo.StandardInputEncoding = Encoding.UTF8;
        }

        // JSON event stream mode, headless. Enable a restricted set of read-only tools.
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("--no-session");
        // enable only read-only tools: read,grep,find,ls
        startInfo.ArgumentList.Add("--tools");
        startInfo.ArgumentList.Add("read,grep,find,ls");
        startInfo.ArgumentList.Add("--no-extensions");
        startInfo.ArgumentList.Add("--no-context-files");
        ApplyWorkspaceModel(startInfo, workspaceModel);
        if (!useStdinPrompt)
        {
            startInfo.ArgumentList.Add(trimmedTask);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                var finishedAt = DateTimeOffset.UtcNow;
                return new PiJsonAgentDetailedResult(
                    false,
                    trimmedTask,
                    string.Empty,
                    "Failed to start pi JSON process.",
                    null,
                    null,
                    Array.Empty<PiJsonToolEvent>(),
                    string.Empty,
                    null,
                    startedAt,
                    finishedAt,
                    (long)(finishedAt - startedAt).TotalMilliseconds,
                    false,
                    false,
                    true,
                    workingDirectory,
                    promptBytes);
            }
        }
        catch (Exception ex)
        {
            var finishedAt = DateTimeOffset.UtcNow;
            return new PiJsonAgentDetailedResult(
                false,
                trimmedTask,
                string.Empty,
                $"Failed to start pi JSON process: {ex.Message}",
                null,
                null,
                Array.Empty<PiJsonToolEvent>(),
                string.Empty,
                null,
                startedAt,
                finishedAt,
                (long)(finishedAt - startedAt).TotalMilliseconds,
                false,
                false,
                true,
                workingDirectory,
                promptBytes);
        }

        if (useStdinPrompt)
        {
            await process.StandardInput.WriteAsync(trimmedTask).ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
            process.StandardInput.Close();
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DefaultTimeout);

        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            var parsed = await ReadAgentEventsAsync(process.StandardOutput, eventReporter, timeoutCts.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            var stderrText = await stderrTask.ConfigureAwait(false);
            var finishedAt = DateTimeOffset.UtcNow;

            if (!parsed.Success)
            {
                return new PiJsonAgentDetailedResult(
                    false,
                    trimmedTask,
                    string.Empty,
                    CombineError(parsed.ErrorMessage, stderrText),
                    parsed.Provider,
                    parsed.Model,
                    parsed.ToolEvents,
                    stderrText,
                    process.HasExited ? process.ExitCode : null,
                    startedAt,
                    finishedAt,
                    (long)(finishedAt - startedAt).TotalMilliseconds,
                    false,
                    false,
                    false,
                    workingDirectory,
                    promptBytes);
            }

            if (process.ExitCode != 0)
            {
                return new PiJsonAgentDetailedResult(
                    false,
                    trimmedTask,
                    string.Empty,
                    CombineError($"pi JSON process exited with code {process.ExitCode}.", stderrText),
                    parsed.Provider,
                    parsed.Model,
                    parsed.ToolEvents,
                    stderrText,
                    process.ExitCode,
                    startedAt,
                    finishedAt,
                    (long)(finishedAt - startedAt).TotalMilliseconds,
                    false,
                    false,
                    false,
                    workingDirectory,
                    promptBytes);
            }

            if (string.IsNullOrWhiteSpace(parsed.Answer))
            {
                return new PiJsonAgentDetailedResult(
                    false,
                    trimmedTask,
                    string.Empty,
                    CombineError("Pi JSON mode completed without a final assistant answer.", stderrText),
                    parsed.Provider,
                    parsed.Model,
                    parsed.ToolEvents,
                    stderrText,
                    process.ExitCode,
                    startedAt,
                    finishedAt,
                    (long)(finishedAt - startedAt).TotalMilliseconds,
                    false,
                    false,
                    false,
                    workingDirectory,
                    promptBytes);
            }

            return new PiJsonAgentDetailedResult(
                true,
                trimmedTask,
                parsed.Answer,
                null,
                parsed.Provider,
                parsed.Model,
                parsed.ToolEvents,
                stderrText,
                process.ExitCode,
                startedAt,
                finishedAt,
                (long)(finishedAt - startedAt).TotalMilliseconds,
                false,
                false,
                false,
                workingDirectory,
                promptBytes);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            var stderrText = await AwaitStderrAfterKillAsync(stderrTask).ConfigureAwait(false);
            var finishedAt = DateTimeOffset.UtcNow;
            var cancelled = cancellationToken.IsCancellationRequested;
            var transportMode = useStdinPrompt ? "stdin" : "argv";
            var elapsed = (finishedAt - startedAt).TotalSeconds;
            return new PiJsonAgentDetailedResult(
                false,
                trimmedTask,
                string.Empty,
                cancelled
                    ? CombineError("Pi JSON execution was cancelled.", stderrText)
                    : CombineError($"Timed out waiting for Pi JSON response after {elapsed:F0}s (limit {DefaultTimeout.TotalSeconds:0}s, transport={transportMode}, taskLen={trimmedTask.Length}).", stderrText),
                null,
                null,
                Array.Empty<PiJsonToolEvent>(),
                stderrText,
                process.HasExited ? process.ExitCode : null,
                startedAt,
                finishedAt,
                (long)(finishedAt - startedAt).TotalMilliseconds,
                !cancelled,
                cancelled,
                false,
                workingDirectory,
                promptBytes);
        }
        catch (JsonException ex)
        {
            TryKill(process);
            var stderrText = await AwaitStderrAfterKillAsync(stderrTask).ConfigureAwait(false);
            var finishedAt = DateTimeOffset.UtcNow;
            return new PiJsonAgentDetailedResult(
                false,
                trimmedTask,
                string.Empty,
                CombineError($"Pi JSON mode returned invalid JSONL: {ex.Message}", stderrText),
                null,
                null,
                Array.Empty<PiJsonToolEvent>(),
                stderrText,
                process.HasExited ? process.ExitCode : null,
                startedAt,
                finishedAt,
                (long)(finishedAt - startedAt).TotalMilliseconds,
                false,
                false,
                false,
                workingDirectory,
                promptBytes);
        }
        catch (Exception ex)
        {
            TryKill(process);
            var stderrText = await AwaitStderrAfterKillAsync(stderrTask).ConfigureAwait(false);
            var finishedAt = DateTimeOffset.UtcNow;
            return new PiJsonAgentDetailedResult(
                false,
                trimmedTask,
                string.Empty,
                CombineError($"Pi JSON mode failed: {ex.Message}", stderrText),
                null,
                null,
                Array.Empty<PiJsonToolEvent>(),
                stderrText,
                process.HasExited ? process.ExitCode : null,
                startedAt,
                finishedAt,
                (long)(finishedAt - startedAt).TotalMilliseconds,
                false,
                false,
                false,
                workingDirectory,
                promptBytes);
        }
    }

    private static async Task<ParsedPiJsonResult> ReadEventsAsync(StreamReader stdout, CancellationToken cancellationToken)
    {
        var toolEvents = new List<PiJsonToolEvent>();
        var deltaBuilder = new StringBuilder();
        string? structuredAnswer = null;
        string? provider = null;
        string? model = null;
        string? explicitError = null;
        var completionObserved = false;
        var lineNumber = 0;
        var eventCount = 0;
        var messageStartSeen = false;
        var messageEndSeen = false;
        var messageUpdateCount = 0;
        var lastEventTypes = new List<string>();

        static void TrackEvent(ref int eventCount, List<string> lastEventTypes, string type)
        {
            eventCount++;
            if (lastEventTypes.Count >= 5)
            {
                lastEventTypes.RemoveAt(0);
            }
            lastEventTypes.Add(type);
        }

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
                    case "queue_update":
                    case "auto_retry_start":
                        TrackEvent(ref eventCount, lastEventTypes, type);
                        break;

                    case "message_start":
                        TrackEvent(ref eventCount, lastEventTypes, type);
                        messageStartSeen = true;
                        break;

                    case "tool_execution_start":
                        {
                            TrackEvent(ref eventCount, lastEventTypes, type);
                            var id = GetString(root, "id");
                            var name = GetString(root, "name");
                            string? details = null;
                            if (root.TryGetProperty("details", out var detailsEl) && detailsEl.ValueKind == JsonValueKind.String)
                            {
                                details = detailsEl.GetString();
                            }

                            toolEvents.Add(new PiJsonToolEvent("tool_execution_start", id, name, details, null));
                        }
                        break;

                    case "tool_execution_update":
                        TrackEvent(ref eventCount, lastEventTypes, type);
                        // ignore updates for prototype
                        break;

                    case "tool_execution_end":
                        {
                            TrackEvent(ref eventCount, lastEventTypes, type);
                            var id = GetString(root, "id");
                            var name = GetString(root, "name");
                            var summary = GetString(root, "summary");
                            toolEvents.Add(new PiJsonToolEvent("tool_execution_end", id, name, null, summary));
                        }
                        break;

                    case "message_update":
                        TrackEvent(ref eventCount, lastEventTypes, type);
                        messageUpdateCount++;
                        CaptureAssistantMetadata(root, ref provider, ref model);
                        AppendTextDelta(root, deltaBuilder);
                        break;

                    case "message_end":
                        TrackEvent(ref eventCount, lastEventTypes, type);
                        messageEndSeen = true;
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
                        TrackEvent(ref eventCount, lastEventTypes, type);
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
                        TrackEvent(ref eventCount, lastEventTypes, type);
                        completionObserved = true;
                        if (TryExtractLastAssistantText(root, ref provider, ref model, out var agentEndAnswer) && !string.IsNullOrWhiteSpace(agentEndAnswer))
                        {
                            structuredAnswer = agentEndAnswer;
                        }
                        break;

                    case "compaction_end":
                        TrackEvent(ref eventCount, lastEventTypes, type);
                        if (root.TryGetProperty("aborted", out var abortedElement)
                            && abortedElement.ValueKind == JsonValueKind.True
                            && (!root.TryGetProperty("willRetry", out var willRetryElement) || willRetryElement.ValueKind == JsonValueKind.False))
                        {
                            explicitError = GetString(root, "errorMessage") ?? "Pi JSON mode reported a compaction failure.";
                        }
                        break;

                    case "auto_retry_end":
                        TrackEvent(ref eventCount, lastEventTypes, type);
                        if (root.TryGetProperty("success", out var successElement)
                            && successElement.ValueKind == JsonValueKind.False)
                        {
                            explicitError = GetString(root, "finalError") ?? "Pi JSON mode exhausted retries.";
                        }
                        break;

                    default:
                        TrackEvent(ref eventCount, lastEventTypes, type);
                        break;
                }
            }
        }

        var answer = string.IsNullOrWhiteSpace(structuredAnswer)
            ? deltaBuilder.ToString().Trim()
            : structuredAnswer.Trim();

        if (string.IsNullOrWhiteSpace(answer))
        {
            var diagnostics = BuildEventDiagnostics(eventCount, lastEventTypes, messageStartSeen, messageEndSeen, messageUpdateCount, deltaBuilder);
            var errorMessage = explicitError
                ?? (completionObserved
                    ? $"Pi JSON stream completed without an assistant answer. {diagnostics}"
                    : $"Pi JSON stream ended before a final assistant answer was observed. {diagnostics}");

            return new ParsedPiJsonResult(false, errorMessage, string.Empty, provider, model);
        }

        return new ParsedPiJsonResult(true, null, answer, provider, model);
    }

    private static string BuildEventDiagnostics(int eventCount, List<string> lastEventTypes, bool messageStartSeen, bool messageEndSeen, int messageUpdateCount, StringBuilder deltaBuilder)
    {
        var parts = new List<string>();
        parts.Add($"events={eventCount}");

        if (lastEventTypes.Count > 0)
        {
            parts.Add($"lastTypes=[{string.Join(",", lastEventTypes)}]");
        }

        parts.Add(messageStartSeen ? "msgStart" : "noMsgStart");
        parts.Add(messageEndSeen ? "msgEnd" : "noMsgEnd");

        if (messageUpdateCount > 0)
        {
            parts.Add($"msgUpdates={messageUpdateCount}");
        }

        var deltaLength = deltaBuilder.Length;
        if (deltaLength > 0)
        {
            parts.Add($"deltaLen={deltaLength}");
        }

        return $"[{string.Join("; ", parts)}]";
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

    private static string AppendTextDelta(JsonElement eventElement, StringBuilder deltaBuilder)
    {
        if (!eventElement.TryGetProperty("assistantMessageEvent", out var assistantMessageEvent)
            || assistantMessageEvent.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (!string.Equals(GetString(assistantMessageEvent, "type"), "text_delta", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var delta = GetString(assistantMessageEvent, "delta");
        if (!string.IsNullOrEmpty(delta))
        {
            deltaBuilder.Append(delta);
            return delta;
        }

        return string.Empty;
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

    private sealed record ParsedPiJsonAgentResult(
        bool Success,
        string? ErrorMessage,
        string Answer,
        string? Provider,
        string? Model,
        IReadOnlyList<PiJsonToolEvent> ToolEvents);

    private static async Task<ParsedPiJsonAgentResult> ReadAgentEventsAsync(StreamReader stdout, Action<PiJsonStreamEvent>? eventReporter, CancellationToken cancellationToken)
    {
        var deltaBuilder = new StringBuilder();
        string? structuredAnswer = null;
        string? provider = null;
        string? model = null;
        string? explicitError = null;
        var completionObserved = false;
        var lineNumber = 0;
        var toolEvents = new List<PiJsonToolEvent>();
        var eventCount = 0;
        var messageStartSeen = false;
        var messageEndSeen = false;
        var messageUpdateCount = 0;
        var lastEventTypes = new List<string>();

        static void TrackAgentEvent(ref int eventCount, List<string> lastEventTypes, string type)
        {
            eventCount++;
            if (lastEventTypes.Count >= 5)
            {
                lastEventTypes.RemoveAt(0);
            }
            lastEventTypes.Add(type);
        }

        void ReportRuntimeEvent(PiJsonStreamEvent runtimeEvent)
        {
            eventReporter?.Invoke(runtimeEvent);
        }

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
                    case "queue_update":
                    case "auto_retry_start":
                        TrackAgentEvent(ref eventCount, lastEventTypes, type);
                        ReportRuntimeEvent(new PiJsonStreamEvent(type));
                        break;

                    case "message_start":
                        TrackAgentEvent(ref eventCount, lastEventTypes, type);
                        messageStartSeen = true;
                        ReportRuntimeEvent(new PiJsonStreamEvent(type));
                        break;

                    case "tool_execution_start":
                        {
                            TrackAgentEvent(ref eventCount, lastEventTypes, type);
                            var id = GetString(root, "id");
                            var name = GetString(root, "name");
                            string? details = null;
                            if (root.TryGetProperty("details", out var detailsEl) && detailsEl.ValueKind == JsonValueKind.String)
                            {
                                details = detailsEl.GetString();
                            }

                            toolEvents.Add(new PiJsonToolEvent("tool_execution_start", id, name, details, null));
                            ReportRuntimeEvent(new PiJsonStreamEvent(type, id, name, null, null, details, null));
                        }
                        break;

                    case "tool_execution_update":
                        TrackAgentEvent(ref eventCount, lastEventTypes, type);
                        // ignore updates for prototype
                        ReportRuntimeEvent(new PiJsonStreamEvent(type));
                        break;

                    case "tool_execution_end":
                        {
                            TrackAgentEvent(ref eventCount, lastEventTypes, type);
                            var id = GetString(root, "id");
                            var name = GetString(root, "name");
                            var summary = GetString(root, "summary");
                            toolEvents.Add(new PiJsonToolEvent("tool_execution_end", id, name, null, summary));
                            ReportRuntimeEvent(new PiJsonStreamEvent(type, id, name, null, summary, null, null));
                        }
                        break;

                    case "message_update":
                        TrackAgentEvent(ref eventCount, lastEventTypes, type);
                        messageUpdateCount++;
                        CaptureAssistantMetadata(root, ref provider, ref model);
                        var delta = AppendTextDelta(root, deltaBuilder);
                        ReportRuntimeEvent(new PiJsonStreamEvent(type, Text: string.IsNullOrWhiteSpace(delta) ? null : delta));
                        break;

                    case "message_end":
                        TrackAgentEvent(ref eventCount, lastEventTypes, type);
                        messageEndSeen = true;
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

                        ReportRuntimeEvent(new PiJsonStreamEvent(type));
                        break;

                    case "turn_end":
                        TrackAgentEvent(ref eventCount, lastEventTypes, type);
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

                        ReportRuntimeEvent(new PiJsonStreamEvent(type));
                        break;

                    case "agent_end":
                        TrackAgentEvent(ref eventCount, lastEventTypes, type);
                        completionObserved = true;
                        if (TryExtractLastAssistantText(root, ref provider, ref model, out var agentEndAnswer) && !string.IsNullOrWhiteSpace(agentEndAnswer))
                        {
                            structuredAnswer = agentEndAnswer;
                        }

                        ReportRuntimeEvent(new PiJsonStreamEvent(type));
                        break;

                    case "compaction_end":
                        TrackAgentEvent(ref eventCount, lastEventTypes, type);
                        if (root.TryGetProperty("aborted", out var abortedElement)
                            && abortedElement.ValueKind == JsonValueKind.True
                            && (!root.TryGetProperty("willRetry", out var willRetryElement) || willRetryElement.ValueKind == JsonValueKind.False))
                        {
                            explicitError = GetString(root, "errorMessage") ?? "Pi JSON mode reported a compaction failure.";
                        }

                        ReportRuntimeEvent(new PiJsonStreamEvent(type, Message: explicitError));
                        break;

                    case "auto_retry_end":
                        TrackAgentEvent(ref eventCount, lastEventTypes, type);
                        if (root.TryGetProperty("success", out var successElement)
                            && successElement.ValueKind == JsonValueKind.False)
                        {
                            explicitError = GetString(root, "finalError") ?? "Pi JSON mode exhausted retries.";
                        }

                        ReportRuntimeEvent(new PiJsonStreamEvent(type, Message: explicitError));
                        break;

                    default:
                        TrackAgentEvent(ref eventCount, lastEventTypes, type);
                        ReportRuntimeEvent(new PiJsonStreamEvent(type));
                        break;
                }
            }
        }

        var answer = string.IsNullOrWhiteSpace(structuredAnswer)
            ? deltaBuilder.ToString().Trim()
            : structuredAnswer.Trim();

        if (string.IsNullOrWhiteSpace(answer))
        {
            var diagnostics = BuildEventDiagnostics(eventCount, lastEventTypes, messageStartSeen, messageEndSeen, messageUpdateCount, deltaBuilder);
            var errorMessage = explicitError
                ?? (completionObserved
                    ? $"Pi JSON stream completed without an assistant answer. {diagnostics}"
                    : $"Pi JSON stream ended before a final assistant answer was observed. {diagnostics}");

            return new ParsedPiJsonAgentResult(false, errorMessage, string.Empty, provider, model, toolEvents);
        }

        return new ParsedPiJsonAgentResult(true, null, answer, provider, model, toolEvents);
    }
}
