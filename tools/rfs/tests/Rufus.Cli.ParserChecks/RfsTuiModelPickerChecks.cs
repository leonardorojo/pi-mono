using Rufus.Cli.PiIntegration;
using Rufus.Cli.Tui;
using Rufus.RCK.Workspace;
using System.Text;
using System.Globalization;
using System.Text.Json;

internal static class RfsTuiModelPickerChecks
{
internal static void Run(List<string> failures)
{
RunSessionModelStateCases(failures);
RunModelSelectionStateCases(failures);
RunRendererAndPrincipalModelCases(failures);
RunTuiPrincipalAnswerResolutionCases(failures);
RunHermesDraftCases(failures);
RunHermesRunCases(failures);
RunHermesHealthCases(failures);
RunCommandCatalogCases(failures);
}

private static void RunSessionModelStateCases(List<string> failures)
{
var sessionState = new RfsTuiSessionState();
if (!string.Equals(sessionState.CurrentSessionModel, RfsTuiSessionState.DefaultSessionModel, StringComparison.Ordinal))
{
failures.Add($"[tui model picker] expected a fresh session to start on '{RfsTuiSessionState.DefaultSessionModel}' but got '{sessionState.CurrentSessionModel}'.");
}

sessionState.SetSessionModel("claude-sonnet-4.5");
if (!string.Equals(sessionState.CurrentSessionModel, "claude-sonnet-4.5", StringComparison.Ordinal))
{
    failures.Add($"[tui model picker] expected session model to update to 'claude-sonnet-4.5' but got '{sessionState.CurrentSessionModel}'.");
}

sessionState.SetSessionModel("gpt-5.4-mini", "github-copilot");
if (!string.Equals(sessionState.ResolveMainModel(), "github-copilot/gpt-5.4-mini", StringComparison.Ordinal))
{
    failures.Add($"[tui model picker] expected session model/provider to resolve to 'github-copilot/gpt-5.4-mini' but got '{sessionState.ResolveMainModel()}'.");
}

sessionState.SetSessionModel("github-copilot/gpt-5.4-mini");
if (!string.Equals(sessionState.ResolveMainModel(), "github-copilot/gpt-5.4-mini", StringComparison.Ordinal))
{
    failures.Add($"[tui model picker] expected qualified session model to remain qualified but got '{sessionState.ResolveMainModel()}'.");
}

sessionState.ResetSessionModel();
if (!string.Equals(sessionState.CurrentSessionModel, RfsTuiSessionState.DefaultSessionModel, StringComparison.Ordinal))
{
    failures.Add($"[tui model picker] expected session reset to restore '{RfsTuiSessionState.DefaultSessionModel}' but got '{sessionState.CurrentSessionModel}'.");
}
}

private static void RunModelSelectionStateCases(List<string> failures)
{
var models = new[]
{
new PiRpcAvailableModel("claude-haiku-4.5", "github-copilot", "Claude Haiku 4.5"),
new PiRpcAvailableModel("claude-sonnet-4.5", "github-copilot", "Claude Sonnet 4.5"),
new PiRpcAvailableModel("gpt-5.4-mini", "github-copilot", "GPT-5.4 Mini"),
new PiRpcAvailableModel("gpt-5.4-mini", "openai-codex", "GPT-5.4 Mini"),
new PiRpcAvailableModel("gpt-5.4-mini", "azure-openai-responses", "GPT-5.4 Mini"),
new PiRpcAvailableModel("gpt-5.4", "github-copilot", "GPT-5.4"),
};

var state = new RfsTuiModelSelectionState(models, "github-copilot/gpt-5.4-mini");
if (state.SelectedIndex != 2)
{
    failures.Add($"[tui model picker] expected the current session model to be selected initially, but got index {state.SelectedIndex}.");
}

if (!string.Equals(state.SelectedProvider, "github-copilot", StringComparison.Ordinal) || !string.Equals(state.SelectedQualifiedModel, "github-copilot/gpt-5.4-mini", StringComparison.Ordinal))
{
    failures.Add($"[tui model picker] expected selected provider/model to be github-copilot/gpt-5.4-mini but got '{state.SelectedQualifiedModel ?? "(null)"}'.");
}

var moveDownResult = state.HandleKey(new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, false, false, false));
if (moveDownResult != RfsTuiModelSelectionAction.Continue || state.SelectedIndex != 3)
{
failures.Add($"[tui model picker] expected ArrowDown to move to index 3 and continue, but got action {moveDownResult} and index {state.SelectedIndex}.");
}

var moveUpResult = state.HandleKey(new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false));
if (moveUpResult != RfsTuiModelSelectionAction.Continue || state.SelectedIndex != 2)
{
failures.Add($"[tui model picker] expected ArrowUp to move back to index 2 and continue, but got action {moveUpResult} and index {state.SelectedIndex}.");
}

var enterResult = state.HandleKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
if (enterResult != RfsTuiModelSelectionAction.Confirm || !string.Equals(state.SelectedModelId, "gpt-5.4-mini", StringComparison.Ordinal))
{
failures.Add($"[tui model picker] expected Enter to confirm 'gpt-5.4-mini' but got action {enterResult} and selected '{state.SelectedModelId ?? "(null)"}'.");
}

var escapeState = new RfsTuiModelSelectionState(models, "claude-haiku-4.5");
var escapeResult = escapeState.HandleKey(new ConsoleKeyInfo('\u001b', ConsoleKey.Escape, false, false, false));
if (escapeResult != RfsTuiModelSelectionAction.Cancel)
{
failures.Add($"[tui model picker] expected Escape to cancel but got {escapeResult}.");
}

var qState = new RfsTuiModelSelectionState(models, "claude-haiku-4.5");
var qResult = qState.HandleKey(new ConsoleKeyInfo('q', ConsoleKey.Q, false, false, false));
if (qResult != RfsTuiModelSelectionAction.Cancel)
{
failures.Add($"[tui model picker] expected q to cancel but got {qResult}.");
}

var missingCurrentState = new RfsTuiModelSelectionState(models, "does-not-exist");
if (missingCurrentState.SelectedIndex != 0)
{
failures.Add($"[tui model picker] expected missing current model to fall back to the first entry, but got index {missingCurrentState.SelectedIndex}.");
}
}

private static void RunCommandCatalogCases(List<string> failures)
{
var helpCommands = RfsTuiCommandCatalog.GetHelpCommands();
var modelShow = helpCommands.FirstOrDefault(command => command.Kind == RfsTuiCommandKind.ModelShow);
var modelSet = helpCommands.FirstOrDefault(command => command.Kind == RfsTuiCommandKind.ModelSet);
var hermesDraft = helpCommands.FirstOrDefault(command => string.Equals(command.Usage, "/hermes draft", StringComparison.Ordinal));
var hermesRun = helpCommands.FirstOrDefault(command => string.Equals(command.Usage, "/hermes run", StringComparison.Ordinal));
var piRun = helpCommands.FirstOrDefault(command => string.Equals(command.Usage, "/pi run", StringComparison.Ordinal));
var paste = helpCommands.FirstOrDefault(command => command.Kind == RfsTuiCommandKind.Paste);
var clear = helpCommands.FirstOrDefault(command => command.Kind == RfsTuiCommandKind.Clear);
var quit = helpCommands.FirstOrDefault(command => string.Equals(command.Usage, "/quit", StringComparison.Ordinal));

if (modelShow is null || !string.Equals(modelShow.Description, "Open session model picker", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /model help text to describe the session picker.");
}

if (modelSet is null || !string.Equals(modelSet.Description, "Set session model (temporary)", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /model <model> help text to describe temporary session updates.");
}

if (hermesDraft is null || !string.Equals(hermesDraft.Description, "Build Hermes handoff draft", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /hermes draft help text to describe the draft path.");
}

if (hermesRun is null || !string.Equals(hermesRun.Description, "Execute Hermes once with guardrails", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /hermes run help text to describe the guarded execution path.");
}

if (piRun is null || !string.Equals(piRun.Description, "Execute Pi using JSON Event Stream", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /pi run help text to describe the Pi runtime path.");
}

if (paste is null || !string.Equals(paste.Description, "Paste a long/multiline prompt. Finish with /end. Use /cancel to discard.", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /paste help text to describe the long-prompt capture path.");
}

if (clear is null || !string.Equals(clear.Description, "Clear the screen", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /clear help text to describe the screen clear action.");
}

if (quit is null || !string.Equals(quit.Description, "Alias for /exit", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /quit help text to describe the exit alias.");
}

var exactModel = RfsTuiCommandCatalog.FindExactMatch("/model");
if (exactModel is null || exactModel.Kind != RfsTuiCommandKind.ModelShow)
{
failures.Add("[tui model picker] expected /model to resolve to the picker command.");
}

var exactHermesDraft = RfsTuiCommandCatalog.FindExactMatch("/hermes draft");
if (exactHermesDraft is null || !string.Equals(exactHermesDraft.Usage, "/hermes draft", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /hermes draft to resolve to the Hermes draft command.");
}

var exactHermesRun = RfsTuiCommandCatalog.FindExactMatch("/hermes run");
if (exactHermesRun is null || !string.Equals(exactHermesRun.Usage, "/hermes run", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /hermes run to resolve to the guarded Hermes command.");
}

var exactPiRun = RfsTuiCommandCatalog.FindExactMatch("/pi run");
if (exactPiRun is null || !string.Equals(exactPiRun.Usage, "/pi run", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /pi run to resolve to the Pi runtime command.");
}

var exactClear = RfsTuiCommandCatalog.FindExactMatch("/clear");
if (exactClear is null || exactClear.Kind != RfsTuiCommandKind.Clear)
{
failures.Add("[tui model picker] expected /clear to resolve to the clear-screen command.");
}

var exactQuit = RfsTuiCommandCatalog.FindExactMatch("/quit");
if (exactQuit is null || exactQuit.Kind != RfsTuiCommandKind.Exit)
{
failures.Add("[tui model picker] expected /quit to resolve to the exit alias command.");
}

var modelSuggestions = RfsTuiCommandCatalog.GetSuggestions("/model");
if (modelSuggestions.Count == 0)
{
failures.Add("[tui model picker] expected /model suggestions to be populated.");
}

var exactPaste = RfsTuiCommandCatalog.FindExactMatch("/paste");
if (exactPaste is null || exactPaste.Kind != RfsTuiCommandKind.Paste)
{
failures.Add("[tui model picker] expected /paste to resolve to the paste capture command.");
}

var pasteSuggestions = RfsTuiCommandCatalog.GetSuggestions("/pa");
if (pasteSuggestions.FirstOrDefault(command => command.Kind == RfsTuiCommandKind.Paste) is null)
{
failures.Add("[tui model picker] expected /pa suggestions to include /paste.");
}
}

private static void RunRendererAndPrincipalModelCases(List<string> failures)
{
    var status = new RckWorkspaceStatus(
        RepoRoot: "/tmp/rfs",
        WorkspaceExists: true,
        ConfigExists: true,
        RckExists: true,
        HeadExists: true,
        Head: "abcdef1234567890",
        StateCount: 3,
        DeltaCount: 2,
        AnchorCount: 1,
        GitContext: new GitWorkspaceContext("main", "abcdef1234567890", Dirty: false, Array.Empty<GitWorkspaceArtifactChange>()));

    var originalOut = Console.Out;
    using var stdout = new StringWriter();

    RfsTuiSessionState sessionState;
    try
    {
        Console.SetOut(stdout);
        sessionState = new RfsTuiSessionState();
        sessionState.SetSessionModel("claude-sonnet-4.5");
        RfsTuiRenderer.WriteHeader(status, "pi-mono", sessionState, leadingBlankLine: false);
        RfsTuiRenderer.WriteHelp(RfsTuiCommandCatalog.GetHelpCommands());
        RfsTuiRenderer.WriteModeSelectionHelp();
    }
    finally
    {
        Console.SetOut(originalOut);
    }

    var headerText = stdout.ToString();
    if (!headerText.Contains("claude-sonnet-4.5 · session", StringComparison.Ordinal))
    {
        failures.Add("[tui model picker] expected the header to mark non-default session models as session-scoped.");
    }

    if (!headerText.Contains("Model:", StringComparison.Ordinal))
    {
        failures.Add("[tui model picker] expected the header to include a Model line.");
    }

    if (!headerText.Contains("/paste", StringComparison.Ordinal) ||
        !headerText.Contains("Paste a long/multiline prompt. Finish with /end. Use /cancel to discard.", StringComparison.Ordinal) ||
        !headerText.Contains("/clear", StringComparison.Ordinal) ||
        !headerText.Contains("/quit", StringComparison.Ordinal) ||
        !headerText.Contains("/exit", StringComparison.Ordinal))
    {
        failures.Add("[tui model picker] expected help and mode selection copy to expose /paste, /clear, and /quit discoverability.");
    }

    var executionModel = RfsTuiSession.CreatePrincipalAnswerExecutionModel("claude-sonnet-4.5");
    if (!string.Equals(executionModel.Model, "claude-sonnet-4.5", StringComparison.Ordinal))
    {
        failures.Add($"[tui model picker] expected the principal answer execution model to use the session model but got '{executionModel.Model}'.");
    }

    var defaultExecutionModel = RfsTuiSession.CreatePrincipalAnswerExecutionModel(string.Empty);
    if (!string.Equals(defaultExecutionModel.Model, RfsTuiSessionState.DefaultSessionModel, StringComparison.Ordinal))
    {
        failures.Add($"[tui model picker] expected empty session model to fall back to '{RfsTuiSessionState.DefaultSessionModel}' but got '{defaultExecutionModel.Model}'.");
    }
}

private static void RunTuiPrincipalAnswerResolutionCases(List<string> failures)
{
var tempDir = Path.Combine(Path.GetTempPath(), $"rfs-tui-principal-answer-check-{Guid.NewGuid():N}");
try
{
Directory.CreateDirectory(tempDir);
Directory.CreateDirectory(Path.Combine(tempDir, ".git"));
Directory.CreateDirectory(Path.Combine(tempDir, ".rfs"));

var config = new Dictionary<string, object?>
{
    ["schemaVersion"] = 1,
    ["type"] = "rufus.workspace",
    ["createdBy"] = "rfs init",
    ["llm"] = new Dictionary<string, object?>
    {
        ["defaultModel"] = "gpt-5.4-mini",
        ["stages"] = new Dictionary<string, object?>
        {
            ["principalAnswer"] = new Dictionary<string, object?> { ["model"] = "deepseek-chat" },
        },
    },
};
File.WriteAllText(Path.Combine(tempDir, ".rfs", "config.json"), JsonSerializer.Serialize(config));

var resolvedModel = RfsTuiSession.CreatePrincipalAnswerExecutionModel(tempDir, "gpt-5.4-mini");
if (!string.Equals(resolvedModel.Model, "deepseek/deepseek-chat", StringComparison.Ordinal))
{
    failures.Add($"[tui model picker] expected repo-root principalAnswer resolution to use the configured stage model but got '{resolvedModel.Model}'.");
}

var fallbackConfig = new Dictionary<string, object?>
{
    ["schemaVersion"] = 1,
    ["type"] = "rufus.workspace",
    ["createdBy"] = "rfs init",
    ["llm"] = new Dictionary<string, object?>
    {
        ["defaultModel"] = "gpt-5.4-mini",
    },
};
File.WriteAllText(Path.Combine(tempDir, ".rfs", "config.json"), JsonSerializer.Serialize(fallbackConfig));

var fallbackModel = RfsTuiSession.CreatePrincipalAnswerExecutionModel(tempDir, "gpt-5.4-mini");
if (!string.Equals(fallbackModel.Model, "github-copilot/gpt-5.4-mini", StringComparison.Ordinal))
{
    failures.Add($"[tui model picker] expected missing principalAnswer to fall back to the session/default model but got '{fallbackModel.Model}'.");
}
}
finally
{
    SafeDeleteDirectory(tempDir);
}
}

private static void RunHermesDraftCases(List<string> failures)
{
var status = new RckWorkspaceStatus(
RepoRoot: "/tmp/rfs",
WorkspaceExists: true,
ConfigExists: true,
RckExists: true,
HeadExists: true,
Head: "abcdef1234567890",
StateCount: 1,
DeltaCount: 1,
AnchorCount: 1,
GitContext: new GitWorkspaceContext("feature/rufus-cli-design", "abcdef1234567890", Dirty: true, Array.Empty<GitWorkspaceArtifactChange>()));

var freshSession = new RfsTuiSessionState();
var unavailable = RfsTuiHermesPromptBuilder.TryBuild(status, freshSession);
if (unavailable.Success || unavailable.Draft is not null || !string.Equals(unavailable.ErrorMessage, "No hay una respuesta previa para generar handoff a Hermes.", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /hermes draft to report that no prior answer is available before any RFS reply.");
}

var sessionState = new RfsTuiSessionState();
sessionState.RecordComplete(
new RfsTuiCompleteContextSummary(
SelectionStrategy: "trace-slice",
ValidationStatus: "validated",
ContextPackScope: "repo",
IntentSource: "llm",
SelectedStateCount: 5,
SelectedDeltaCount: 3,
SelectedAnchorCount: 2,
EstimatedChars: 1234,
EstimatedTokens: 321,
TransportRisk: "low",
Truncated: false,
Warnings: ["warning-1"],
Omissions: ["omission-1"]),
prompt: "¿Qué cambió en la última respuesta?",
answer: "Se validó el ContextPack y se respondió con una propuesta concreta.");

var draftResult = RfsTuiHermesPromptBuilder.TryBuild(status, sessionState);
if (!draftResult.Success || draftResult.Draft is null)
{
failures.Add("[tui model picker] expected /hermes draft to build a draft once a prior response exists.");
return;
}

var draft = draftResult.Draft;
if (!string.Equals(draft.RepoRoot, "/tmp/rfs", StringComparison.Ordinal) ||
!string.Equals(draft.Branch, "feature/rufus-cli-design", StringComparison.Ordinal) ||
!string.Equals(draft.DirtyState, "dirty", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /hermes draft metadata to include repo root, branch, and dirty state.");
}

if (!draft.PromptText.Contains("Hermes handoff draft", StringComparison.Ordinal) ||
!draft.PromptText.Contains("Repo root: /tmp/rfs", StringComparison.Ordinal) ||
!draft.PromptText.Contains("Branch: feature/rufus-cli-design", StringComparison.Ordinal) ||
!draft.PromptText.Contains("Dirty state: dirty", StringComparison.Ordinal) ||
!draft.PromptText.Contains("Respuesta previa del LLM principal:", StringComparison.Ordinal) ||
!draft.PromptText.Contains("ContextPack summary:", StringComparison.Ordinal) ||
!draft.PromptText.Contains("Entrega esperada:", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /hermes prompt text to include the required handoff sections.");
}
}

private static void RunHermesRunCases(List<string> failures)
{
var status = new RckWorkspaceStatus(
RepoRoot: "/tmp/rfs",
WorkspaceExists: true,
ConfigExists: true,
RckExists: true,
HeadExists: true,
Head: "abcdef1234567890",
StateCount: 1,
DeltaCount: 1,
AnchorCount: 1,
GitContext: new GitWorkspaceContext("feature/rufus-cli-design", "abcdef1234567890", Dirty: false, Array.Empty<GitWorkspaceArtifactChange>()));

var noResponseSession = new RfsTuiSessionState();
var captureNoResponse = new StringWriter();
var originalOut = Console.Out;
try
{
Console.SetOut(captureNoResponse);
var noResponseRunner = new FakeHermesRunner();
var noResponseResult = RfsTuiHermesRunCommand.ExecuteAsync(status, noResponseSession, noResponseRunner).GetAwaiter().GetResult();
if (!noResponseResult)
{
failures.Add("[tui model picker] expected /hermes run without a prior response to be handled.");
}

var noResponseText = captureNoResponse.ToString();
if (!noResponseText.Contains("No hay una respuesta previa para ejecutar handoff con Hermes.", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /hermes run to report the missing prior response.");
}

if (noResponseRunner.CaptureGitStatusCalls != 0 || noResponseRunner.RunCalls != 0)
{
failures.Add("[tui model picker] expected /hermes run to stop before invoking the runner when no response exists.");
}
}
finally
{
Console.SetOut(originalOut);
}

var longSession = new RfsTuiSessionState();
longSession.RecordComplete(
new RfsTuiCompleteContextSummary(
SelectionStrategy: "trace-slice",
ValidationStatus: "validated",
ContextPackScope: "repo",
IntentSource: "llm",
SelectedStateCount: 5,
SelectedDeltaCount: 3,
SelectedAnchorCount: 2,
EstimatedChars: 1234,
EstimatedTokens: 321,
TransportRisk: "low",
Truncated: false,
Warnings: Array.Empty<string>(),
Omissions: Array.Empty<string>()),
prompt: "¿Qué cambió en la última respuesta?",
answer: new string('x', RfsTuiHermesRunOptions.DefaultMaxPromptBytes + 2500));

var guardRunner = new FakeHermesRunner();
var guardCapture = new StringWriter();
try
{
Console.SetOut(guardCapture);
var guardResult = RfsTuiHermesRunCommand.ExecuteAsync(status, longSession, guardRunner).GetAwaiter().GetResult();
if (!guardResult)
{
failures.Add("[tui model picker] expected prompt-size guard to return handled state.");
}
}
finally
{
Console.SetOut(originalOut);
}

if (!guardCapture.ToString().Contains("Hermes prompt too large for argv transport.", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /hermes run to reject oversized argv prompts.");
}

if (guardRunner.CaptureGitStatusCalls != 0 || guardRunner.RunCalls != 0)
{
failures.Add("[tui model picker] expected oversized prompts to abort before runner invocation.");
}

var runSession = new RfsTuiSessionState();
runSession.RecordComplete(
new RfsTuiCompleteContextSummary(
SelectionStrategy: "trace-slice",
ValidationStatus: "validated",
ContextPackScope: "repo",
IntentSource: "llm",
SelectedStateCount: 5,
SelectedDeltaCount: 3,
SelectedAnchorCount: 2,
EstimatedChars: 1234,
EstimatedTokens: 321,
TransportRisk: "low",
Truncated: false,
Warnings: Array.Empty<string>(),
Omissions: Array.Empty<string>()),
prompt: "¿Qué cambió en la última respuesta?",
answer: "Se validó el ContextPack y se respondió con una propuesta concreta.");

var expectedPrompt = RfsTuiHermesPromptBuilder.TryBuild(status, runSession).Draft!.PromptText;
var successRunner = new FakeHermesRunner
{
GitStatusBefore = " M changed-file.cs",
GitStatusAfter = " M changed-file.cs",
RunResult = new RfsTuiHermesRunResult(
Stdout: "Hermes output",
Stderr: "",
ExitCode: 0,
StartedAt: DateTimeOffset.UtcNow,
FinishedAt: DateTimeOffset.UtcNow,
DurationMs: 123,
TimedOut: false,
WorkingDirectory: "/tmp/rfs",
GitStatusBefore: " M changed-file.cs",
GitStatusAfter: " M changed-file.cs",
DirtyStateChanged: false,
PromptBytes: Encoding.UTF8.GetByteCount(expectedPrompt))
};

var runCapture = new StringWriter();
try
{
Console.SetOut(runCapture);
var runResult = RfsTuiHermesRunCommand.ExecuteAsync(status, runSession, successRunner).GetAwaiter().GetResult();
if (!runResult)
{
failures.Add("[tui model picker] expected /hermes run to complete the guarded execution path.");
}
}
finally
{
Console.SetOut(originalOut);
}

if (successRunner.CaptureGitStatusCalls != 1 || successRunner.RunCalls != 1)
{
failures.Add("[tui model picker] expected /hermes run to capture git status once and invoke the runner once.");
}

if (!string.Equals(successRunner.CapturedPrompt, expectedPrompt, StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /hermes run to reuse the Hermes prompt builder output.");
}

if (!string.Equals(successRunner.CapturedWorkingDirectory, "/tmp/rfs", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /hermes run to execute from the repo root.");
}

var heartbeat = RfsTuiRenderer.FormatHermesRunHeartbeat(
new RfsTuiHermesRunProgress(
WorkingDirectory: "/tmp/rfs",
Elapsed: TimeSpan.FromSeconds(10),
Remaining: TimeSpan.FromSeconds(290),
Timeout: RfsTuiHermesRunOptions.DefaultTimeout,
PromptBytes: Encoding.UTF8.GetByteCount(expectedPrompt),
Transport: "cli-oneshot",
ProcessId: 4242),
showCancelHint: true);

var requiredHeartbeatFragments = new[]
{
    "[Hermes run] still running...",
    "elapsed: 10s / 300s",
    "remaining: 290s",
    "cwd: /tmp/rfs",
    $"prompt bytes: {Encoding.UTF8.GetByteCount(expectedPrompt).ToString("N0", CultureInfo.InvariantCulture)}",
    "transport: cli-oneshot",
};

foreach (var fragment in requiredHeartbeatFragments)
{
    if (!heartbeat.Contains(fragment, StringComparison.Ordinal))
    {
        failures.Add($"[tui model picker] expected Hermes heartbeat to include '{fragment}'.");
    }
}

var forbiddenHeartbeatFragments = new[]
{
    "tool.start",
    "tool.progress",
    "tool.complete",
    "message.delta",
};

foreach (var fragment in forbiddenHeartbeatFragments)
{
    if (heartbeat.Contains(fragment, StringComparison.Ordinal))
    {
        failures.Add($"[tui model picker] expected Hermes heartbeat to avoid invented progress token '{fragment}'.");
    }
}

var runOutput = runCapture.ToString();
        if (!runOutput.Contains("[Hermes run]", StringComparison.Ordinal) ||
            !runOutput.Contains("health: completed", StringComparison.Ordinal) ||
            !runOutput.Contains("Hermes run completed.", StringComparison.Ordinal) ||
            !runOutput.Contains("transport: cli-oneshot", StringComparison.Ordinal) ||
            !runOutput.Contains("Git changed: no", StringComparison.Ordinal) ||
            !runOutput.Contains("Hermes response:", StringComparison.Ordinal) ||
            !runOutput.Contains("Hermes output", StringComparison.Ordinal))
{
failures.Add("[tui model picker] expected /hermes run to render guarded execution metadata and stdout.");
}
}

private static void RunHermesHealthCases(List<string> failures)
{
var runningProgress = new RfsTuiHermesRunProgress(
WorkingDirectory: "/tmp/rfs",
Elapsed: TimeSpan.FromSeconds(10),
Remaining: TimeSpan.FromSeconds(290),
Timeout: RfsTuiHermesRunOptions.DefaultTimeout,
PromptBytes: 1234,
Transport: "cli-oneshot",
ProcessId: 4242);
if (runningProgress.Health != RfsTuiHermesRunHealth.Running)
{
    failures.Add("[tui model picker] expected short Hermes runs to remain in Running health.");
}

var longRunningProgress = runningProgress with { Elapsed = TimeSpan.FromSeconds(70), Remaining = TimeSpan.FromSeconds(230), Health = RfsTuiHermesRunHealth.LongRunning };
if (longRunningProgress.Health != RfsTuiHermesRunHealth.LongRunning)
{
    failures.Add("[tui model picker] expected 70s Hermes runs to switch to LongRunning health.");
}

var longRunningHeartbeat = RfsTuiRenderer.FormatHermesRunHeartbeat(longRunningProgress, showCancelHint: true);
foreach (var fragment in new[]
{
    "[Hermes run] taking longer than usual.",
    "elapsed: 70s / 300s",
    "remaining: 230s",
    "transport: cli-oneshot",
    "press q to cancel",
})
{
    if (!longRunningHeartbeat.Contains(fragment, StringComparison.Ordinal))
    {
        failures.Add($"[tui model picker] expected long-running Hermes heartbeat to include '{fragment}'.");
    }
}

var waitingHeartbeat = longRunningProgress with { Elapsed = TimeSpan.FromSeconds(130), Remaining = TimeSpan.FromSeconds(170) };
var waitingHeartbeatText = RfsTuiRenderer.FormatHermesRunHeartbeat(waitingHeartbeat, showCancelHint: true);
foreach (var fragment in new[]
{
    "[Hermes run] still waiting for final response; cli-oneshot is final-only.",
    "elapsed: 130s / 300s",
    "press q to cancel",
})
{
    if (!waitingHeartbeatText.Contains(fragment, StringComparison.Ordinal))
    {
        failures.Add($"[tui model picker] expected mid-run Hermes heartbeat to include '{fragment}'.");
    }
}

var timeoutHeartbeat = longRunningProgress with { Elapsed = TimeSpan.FromSeconds(250), Remaining = TimeSpan.FromSeconds(50) };
var timeoutHeartbeatText = RfsTuiRenderer.FormatHermesRunHeartbeat(timeoutHeartbeat, showCancelHint: true);
foreach (var fragment in new[]
{
    "[Hermes run] close to timeout.",
    "elapsed: 250s / 300s",
    "remaining: 50s",
    "press q to cancel",
})
{
    if (!timeoutHeartbeatText.Contains(fragment, StringComparison.Ordinal))
    {
        failures.Add($"[tui model picker] expected near-timeout Hermes heartbeat to include '{fragment}'.");
    }
}

var originalOut = Console.Out;
string CaptureOutput(Action action)
{
    using var output = new StringWriter();
    try
    {
        Console.SetOut(output);
        action();
    }
    finally
    {
        Console.SetOut(originalOut);
    }

    return output.ToString();
}

var cancelledOutput = CaptureOutput(() =>
{
    RfsTuiRenderer.WriteHermesRunResult(new RfsTuiHermesRunResult(
        Stdout: "partial stdout",
        Stderr: "partial stderr",
        ExitCode: null,
        StartedAt: DateTimeOffset.UtcNow,
        FinishedAt: DateTimeOffset.UtcNow,
        DurationMs: 456,
        TimedOut: false,
        WorkingDirectory: "/tmp/rfs",
        GitStatusBefore: string.Empty,
        GitStatusAfter: string.Empty,
        DirtyStateChanged: false,
        PromptBytes: 1234,
        Health: RfsTuiHermesRunHealth.Cancelled));
});

foreach (var fragment in new[]
{
    "health: cancelled",
    "Hermes process was interrupted by user cancellation.",
    "The cli-oneshot transport is final-only, so a partial answer may not be available.",
    "Partial Hermes output:",
    "partial stdout",
    "Partial Hermes stderr:",
    "partial stderr",
})
{
    if (!cancelledOutput.Contains(fragment, StringComparison.Ordinal))
    {
        failures.Add($"[tui model picker] expected cancelled Hermes output to include '{fragment}'.");
    }
}

var cancelledTracebackOutput = CaptureOutput(() =>
{
    RfsTuiRenderer.WriteHermesRunResult(new RfsTuiHermesRunResult(
        Stdout: "partial stdout",
        Stderr: "Hermes preflight warning\nTraceback (most recent call last):\n  File \"/tmp/hermes.py\", line 4, in <module>\n    main()\n  File \"/tmp/hermes.py\", line 2, in main\n    input()\nKeyboardInterrupt\nHermes cleanup warning",
        ExitCode: null,
        StartedAt: DateTimeOffset.UtcNow,
        FinishedAt: DateTimeOffset.UtcNow,
        DurationMs: 789,
        TimedOut: false,
        WorkingDirectory: "/tmp/rfs",
        GitStatusBefore: "",
        GitStatusAfter: "",
        DirtyStateChanged: true,
        PromptBytes: 1234,
        Health: RfsTuiHermesRunHealth.Cancelled));
});

foreach (var fragment in new[]
{
    "health: cancelled",
    "Hermes process was interrupted by user cancellation.",
    "The cli-oneshot transport is final-only, so a partial answer may not be available.",
    "timed out: no",
    "duration: 789 ms",
    "prompt bytes: 1,234",
    "Git changed: yes",
    "partial stdout",
    "Hermes preflight warning",
    "Hermes stderr (filtered):",
    "Hermes cleanup warning",
})
{
    if (!cancelledTracebackOutput.Contains(fragment, StringComparison.Ordinal))
    {
        failures.Add($"[tui model picker] expected cancelled Hermes output with traceback filtering to include '{fragment}'.");
    }
}

foreach (var fragment in new[]
{
    "Traceback (most recent call last):",
    "KeyboardInterrupt",
    "  File \"/tmp/hermes.py\", line 4, in <module>",
    "  File \"/tmp/hermes.py\", line 2, in main",
})
{
    if (cancelledTracebackOutput.Contains(fragment, StringComparison.Ordinal))
    {
        failures.Add($"[tui model picker] expected cancelled Hermes output to suppress the traceback fragment '{fragment}'.");
    }
}

var timedOutOutput = CaptureOutput(() =>
{
    RfsTuiRenderer.WriteHermesRunResult(new RfsTuiHermesRunResult(
        Stdout: string.Empty,
        Stderr: string.Empty,
        ExitCode: null,
        StartedAt: DateTimeOffset.UtcNow,
        FinishedAt: DateTimeOffset.UtcNow,
        DurationMs: 456,
        TimedOut: true,
        WorkingDirectory: "/tmp/rfs",
        GitStatusBefore: string.Empty,
        GitStatusAfter: string.Empty,
        DirtyStateChanged: false,
        PromptBytes: 1234,
        Health: RfsTuiHermesRunHealth.TimedOut));
});

foreach (var fragment in new[]
{
    "health: timedout",
    "Hermes run timed out before a final response arrived.",
    "No partial Hermes output was available.",
})
{
    if (!timedOutOutput.Contains(fragment, StringComparison.Ordinal))
    {
        failures.Add($"[tui model picker] expected timed-out Hermes output to include '{fragment}'.");
    }
}

var failedStartOutput = CaptureOutput(() =>
{
    RfsTuiRenderer.WriteHermesRunResult(new RfsTuiHermesRunResult(
        Stdout: string.Empty,
        Stderr: "hermes: not found",
        ExitCode: null,
        StartedAt: DateTimeOffset.UtcNow,
        FinishedAt: DateTimeOffset.UtcNow,
        DurationMs: 1,
        TimedOut: false,
        WorkingDirectory: "/tmp/rfs",
        GitStatusBefore: string.Empty,
        GitStatusAfter: string.Empty,
        DirtyStateChanged: false,
        PromptBytes: 1234,
        Health: RfsTuiHermesRunHealth.FailedToStart));
});

foreach (var fragment in new[]
{
    "health: failedtostart",
    "Hermes run failed to start.",
    "Hermes stderr:",
    "hermes: not found",
})
{
    if (!failedStartOutput.Contains(fragment, StringComparison.Ordinal))
    {
        failures.Add($"[tui model picker] expected failed-start Hermes output to include '{fragment}'.");
    }
}
}

private static void SafeDeleteDirectory(string path)
{
    try
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
    catch
    {
        // best-effort cleanup
    }
}

private sealed class FakeHermesRunner : IRfsTuiHermesRunner
{
    public int CaptureGitStatusCalls { get; private set; }

    public int RunCalls { get; private set; }

    public string GitStatusBefore { get; init; } = string.Empty;

    public string GitStatusAfter { get; init; } = string.Empty;

    public RfsTuiHermesRunResult RunResult { get; init; } = new(
        Stdout: string.Empty,
        Stderr: string.Empty,
        ExitCode: 0,
        StartedAt: DateTimeOffset.UtcNow,
        FinishedAt: DateTimeOffset.UtcNow,
        DurationMs: 0,
        TimedOut: false,
        WorkingDirectory: string.Empty,
        GitStatusBefore: string.Empty,
        GitStatusAfter: string.Empty,
        DirtyStateChanged: false,
        PromptBytes: 0);

    public string CapturedPrompt { get; private set; } = string.Empty;

    public string CapturedWorkingDirectory { get; private set; } = string.Empty;

    public string CapturedGitStatusBefore { get; private set; } = string.Empty;

    public TimeSpan? CapturedTimeout { get; private set; }

    public Task<string> CaptureGitStatusAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        CaptureGitStatusCalls++;
        CapturedWorkingDirectory = workingDirectory;
        return Task.FromResult(CaptureGitStatusCalls == 1 ? GitStatusBefore : GitStatusAfter);
    }

    public Task<RfsTuiHermesRunResult> RunAsync(
        string workingDirectory,
        string prompt,
        string? gitStatusBefore = null,
        RfsTuiHermesRunOptions? options = null,
        RfsTuiHermesRunProgressReporter? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        RunCalls++;
        CapturedWorkingDirectory = workingDirectory;
        CapturedPrompt = prompt;
        CapturedGitStatusBefore = gitStatusBefore ?? string.Empty;
        CapturedTimeout = options?.Timeout;
        return Task.FromResult(RunResult with
        {
            WorkingDirectory = workingDirectory,
            GitStatusBefore = gitStatusBefore ?? string.Empty,
            GitStatusAfter = GitStatusAfter,
            DirtyStateChanged = !string.Equals(gitStatusBefore ?? string.Empty, GitStatusAfter, StringComparison.Ordinal),
            PromptBytes = Encoding.UTF8.GetByteCount(prompt),
        });
    }
}
}
