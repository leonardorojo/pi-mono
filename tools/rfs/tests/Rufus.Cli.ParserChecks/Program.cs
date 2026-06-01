using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Rufus.Cli.Intent;
using Rufus.Cli.PiIntegration;
using Rufus.Cli.TraceSlice;
using Rufus.Cli.Tui;
using Rufus.Cli.ParserChecks;
using Rufus.Agenting;
using Rufus.Agenting.Intent;
using Rufus.RCK.Workspace;

var failures = new List<string>();

await RunPartitionedChecksAsync(args, failures);

if (failures.Count > 0)
{
    foreach (var failure in failures)
    {
        Console.Error.WriteLine(failure);
    }

    return 1;
}

Console.WriteLine("ParserChecks passed.");
return 0;

var longPasteOnly = args.Contains("--long-paste-only", StringComparer.Ordinal);

if (longPasteOnly)
{
    await RfsTuiLongPasteChecks.RunAsync(failures);

    if (failures.Count > 0)
    {
        foreach (var failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        return 1;
    }

    Console.WriteLine("Long paste parser checks passed.");
    return 0;
}


await RunCaseAsync(
    name: "structured final answer",
    fixtureMode: "structured",
    expectedSuccess: true,
    expectedAnswer: "structured answer",
    expectedProvider: "test-provider",
    expectedModel: "test-model",
    expectedErrorContains: null,
    failures: failures);

await RunCaseAsync(
    name: "delta fallback with stderr separation",
    fixtureMode: "delta",
    expectedSuccess: true,
    expectedAnswer: "hello world",
    expectedProvider: null,
    expectedModel: null,
    expectedErrorContains: null,
    failures: failures);

await RunCaseAsync(
    name: "final answer beats prior delta",
    fixtureMode: "delta-then-final",
    expectedSuccess: true,
    expectedAnswer: "structured answer",
    expectedProvider: "test-provider",
    expectedModel: "test-model",
    expectedErrorContains: null,
    failures: failures);

await RunCaseAsync(
    name: "no answer fails explicitly",
    fixtureMode: "no-answer",
    expectedSuccess: false,
    expectedAnswer: null,
    expectedProvider: null,
    expectedModel: null,
    expectedErrorContains: "Pi JSON stream ended before a final assistant answer was observed",
    failures: failures);

await RunCaseAsync(
    name: "invalid jsonl",
    fixtureMode: "invalid",
    expectedSuccess: false,
    expectedAnswer: null,
    expectedProvider: null,
    expectedModel: null,
    expectedErrorContains: "Invalid JSONL on line 1",
    failures: failures);

await RunIntentInferenceCaseAsync(
    name: "intent inference success",
    task: new AgentTask(
        id: "task-1",
        kind: "infer-intent",
        goal: "Infer the operational intent from this prompt.",
        input: "Build a TraceSlice for the current diff and summarize evidence.",
        expectedOutput: "PromptIntent JSON"),
    expectedIntent: "build-trace-slice",
    failures: failures);

await RunIntentInferenceFailureCaseAsync(
    name: "intent inference rejects unsupported kind",
    task: new AgentTask(
        id: "task-2",
        kind: "summarize-evidence",
        goal: "Summarize evidence for the diff.",
        input: "Summarize the diff evidence.",
        expectedOutput: null),
    expectedErrorContains: "Kind='infer-intent'",
    failures: failures);

await RunPromptIntentJsonCodecCaseAsync(
    name: "prompt intent codec accepts lowercase llm json",
    json: "{\"intent\":\"implement-reset-board\",\"summary\":\"Implement the reset board action.\",\"entities\":[\"reset board\",\"board\"],\"constraints\":[\"do not write RCK\"]}",
    expectedIntent: "implement-reset-board",
    expectedSummary: "Implement the reset board action.",
    failures: failures);

await RunPromptIntentJsonCodecFailureCaseAsync(
    name: "prompt intent codec rejects invalid json",
    json: "{\"intent\":\"implement-reset-board\",\"summary\":\"missing brace\"",
    expectedErrorContains: "Invalid PromptIntent JSON",
    failures: failures);

await RunPiIntentInferenceAgentCaseAsync(
    name: "pi intent agent parses llm json and emits canonical prompt intent",
    task: new AgentTask(
        id: "task-llm-1",
        kind: "infer-intent",
        goal: "Infer the operational intent from this prompt.",
        input: "Implement reset board action",
        expectedOutput: "PromptIntent JSON"),
    llmAnswerJson: "{\"intent\":\"implement-reset-board\",\"summary\":\"Implement the reset board action.\",\"entities\":[\"reset board\"],\"constraints\":[]}",
    expectedIntent: "implement-reset-board",
    failures: failures);

await RunPiIntentInferenceAgentCaseAsync(
    name: "pi intent agent parses code-change intent",
    task: new AgentTask(
        id: "task-rfs-codechange",
        kind: "infer-intent",
        goal: "Infer the operational intent from this prompt.",
        input: "How do I fix this C# NullReferenceException in Program.cs?",
        expectedOutput: "PromptIntent JSON"),
    llmAnswerJson: "{\"intent\":\"code-change\",\"summary\":\"Fix NullReferenceException in Program.cs.\",\"entities\":[\"Program.cs\"],\"constraints\":[]}",
    expectedIntent: "code-change",
    failures: failures);

await RunPiIntentInferenceAgentCaseAsync(
    name: "pi intent agent parses repo-analysis intent",
    task: new AgentTask(
        id: "task-rfs-repoanalysis",
        kind: "infer-intent",
        goal: "Infer the operational intent from this prompt.",
        input: "Analiza este repo en modo read-only.",
        expectedOutput: "PromptIntent JSON"),
    llmAnswerJson: "{\"intent\":\"repo-analysis\",\"summary\":\"Analyze the repository in read-only mode.\",\"entities\":[],\"constraints\":[]}",
    expectedIntent: "repo-analysis",
    failures: failures);

await RunPiIntentInferenceAgentCaseAsync(
    name: "pi intent agent parses planning intent",
    task: new AgentTask(
        id: "task-rfs-planning",
        kind: "infer-intent",
        goal: "Infer the operational intent from this prompt.",
        input: "Planifiquemos la proxima fase de cobertura.",
        expectedOutput: "PromptIntent JSON"),
    llmAnswerJson: "{\"intent\":\"planning\",\"summary\":\"Plan the next coverage phase.\",\"entities\":[],\"constraints\":[]}",
    expectedIntent: "planning",
    failures: failures);

await RunPiIntentInferenceAgentCaseAsync(
    name: "pi intent agent parses rck-memory intent",
    task: new AgentTask(
        id: "task-rfs-rckmemory",
        kind: "infer-intent",
        goal: "Infer the operational intent from this prompt.",
        input: "Retomemos el anchor sobre continuidad conversacional.",
        expectedOutput: "PromptIntent JSON"),
    llmAnswerJson: "{\"intent\":\"rck-memory\",\"summary\":\"Resume anchor on conversational continuity.\",\"entities\":[\"anchor\"],\"constraints\":[]}",
    expectedIntent: "rck-memory",
    failures: failures);

await RunPiIntentInferenceAgentCaseAsync(
    name: "pi intent agent parses docs-update intent",
    task: new AgentTask(
        id: "task-rfs-docsupdate",
        kind: "infer-intent",
        goal: "Infer the operational intent from this prompt.",
        input: "Actualiza README.md para documentar agent-json.",
        expectedOutput: "PromptIntent JSON"),
    llmAnswerJson: "{\"intent\":\"docs-update\",\"summary\":\"Update README.md to document agent-json.\",\"entities\":[\"README.md\",\"agent-json\"],\"constraints\":[]}",
    expectedIntent: "docs-update",
    failures: failures);

await RunPiIntentInferenceAgentCaseAsync(
    name: "pi intent agent parses question intent",
    task: new AgentTask(
        id: "task-rfs-question",
        kind: "infer-intent",
        goal: "Infer the operational intent from this prompt.",
        input: "Cual es la capital de Japon?",
        expectedOutput: "PromptIntent JSON"),
    llmAnswerJson: "{\"intent\":\"question\",\"summary\":\"Ask about the capital of Japan.\",\"entities\":[\"Japon\"],\"constraints\":[]}",
    expectedIntent: "question",
    failures: failures);

await RunPiIntentInferenceAgentCaseAsync(
    name: "pi intent agent parses chat intent",
    task: new AgentTask(
        id: "task-rfs-chat",
        kind: "infer-intent",
        goal: "Infer the operational intent from this prompt.",
        input: "Hola, como estas?",
        expectedOutput: "PromptIntent JSON"),
    llmAnswerJson: "{\"intent\":\"chat\",\"summary\":\"Casual greeting.\",\"entities\":[],\"constraints\":[]}",
    expectedIntent: "chat",
    failures: failures);

await RunIntentCliLlmCaseAsync(
    name: "intent cli llm renders fixed lightweight model",
    prompt: "Implement reset board action",
    expectedIntent: "implement-reset-board",
    failures: failures);

await RunPiJsonRunnerWorkspaceModelCaseAsync(
    name: "pi json runner preserves workspace default for main llm",
    prompt: "Implement reset board action",
    expectedModel: "gpt-5.4-mini",
    failures: failures);

await RunRckRecordAskCaseAsync(
    name: "rck interaction recorder persists ask interactions",
    failures: failures);

await RunRckRecordAgentCaseAsync(
    name: "rck interaction recorder persists agent interactions with tool evidence",
    failures: failures);

PrincipalAnswerAgentContractChecks.Run(failures);
await PiPrincipalAnswerAgentChecks.RunAsync(failures);
await PiTraceSliceProposalAgentChecks.RunAsync(failures);
await PiConversationalMemoryAgentChecks.RunAsync(failures);
await RfsStageModelConfigChecks.RunAsync(failures);
await RfsCompleteModelProfileChecks.RunAsync(failures);
await RfsPromptDumpChecks.RunAsync(failures);

await RunPiIntentInferenceAgentFailureCaseAsync(
    name: "pi intent agent rejects invalid llm json",
    task: new AgentTask(
        id: "task-llm-2",
        kind: "infer-intent",
        goal: "Infer the operational intent from this prompt.",
        input: "Explain where Jujuy is located",
        expectedOutput: "PromptIntent JSON"),
    llmAnswerJson: "not-json",
    expectedErrorContains: "Invalid PromptIntent JSON",
    failures: failures);

await RunCompleteModePipelineWithAnchorSelectionLlmCaseAsync(
    name: "complete mode uses anchor-guided structural slicing and validates the expanded proposal",
    repoRoot: "/home/rufus/DEV/leonardorojo/ChessBoardApp",
    prompt: "Implement reset board action",
    llmAnswerJson: "{\"intent\":\"implement-reset-board\",\"summary\":\"Implement the reset board action.\",\"entities\":[\"reset board\"],\"constraints\":[\"do not write RCK\"]}",
    failures: failures);

await RunCompleteModePipelineWithAnchorSelectionFallbackCaseAsync(
    name: "complete mode makes fallback visible when anchor selection returns no anchors",
    repoRoot: "/home/rufus/DEV/leonardorojo/ChessBoardApp",
    prompt: "Implement reset board action",
    llmAnswerJson: "{\"intent\":\"implement-reset-board\",\"summary\":\"Implement the reset board action.\",\"entities\":[\"reset board\"],\"constraints\":[\"do not write RCK\"]}",
    failures: failures);

await RunCompleteModePipelineWithAnchorSelectionFailureCaseAsync(
    name: "complete mode fails before validation when anchor selection JSON is invalid",
    repoRoot: "/home/rufus/DEV/leonardorojo/ChessBoardApp",
    prompt: "Implement reset board action",
    llmAnswerJson: "{\"intent\":\"implement-reset-board\",\"summary\":\"Implement the reset board action.\",\"entities\":[\"reset board\"],\"constraints\":[\"do not write RCK\"]}",
    failures: failures);

await RunCompleteModePipelineWithIntentLlmFailureCaseAsync(
    name: "complete mode fails before proposal when intent JSON is invalid",
    repoRoot: "/home/rufus/DEV/leonardorojo/ChessBoardApp",
    prompt: "Implement reset board action",
    llmAnswerJson: "not-json",
    expectedErrorContains: "Complete mode failed while inferring intent.",
    failures: failures);

RunCompleteModeFailureRendererCase(
    name: "complete mode failure renderer does not report recorded state delta on intent failure",
    reason: "Complete mode failed while inferring intent.",
    failures: failures);

await RfsCompleteModeProposalObservabilityChecks.RunAsync(failures);

await RunIntentCliCaseAsync(
    name: "intent cli renders result",
    prompt: "Implement rfs show command",
    expectedIntent: "general-operational-intent",
    failures: failures);

await RunTraceSliceCliCaseAsync(
    name: "trace slice cli renders deterministic json",
    prompt: "Implement rfs show command",
    failures: failures);

await RunTraceSliceProposalCliCaseAsync(
    name: "trace slice proposal cli renders deterministic proposal json",
    prompt: "Implement rfs show command",
    failures: failures);

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
    expectedErrorContains: "invalid JSON from LLM");

await RunTraceSliceProposalLlmCliCaseAsync(
    name: "trace slice proposal llm cli rejects contaminated llm output",
    prompt: "Implement rfs show command",
    failures: failures,
    fixtureMode: "contaminated",
    expectSuccess: false,
    expectedErrorContains: "rationale entries must be objects");

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
    expectedErrorContains: "restricted materialization policy flags must be false");

await PiTraceSliceAnchorSelectionAgentChecks.RunAsync(failures);
await RunRckTraceSliceProposalValidatorCriticalCasesAsync(failures);
await RckAnchorExpansionServiceChecks.RunAsync(failures);
await RckDagQuickIndexV1BuilderChecks.RunAsync(failures);
await RckConversationalMemoryInputBuilderChecks.RunAsync(failures);
await RckSemanticChecks.RunAsync(failures);

await RunContextPackTraceSliceCliCaseAsync(
    name: "context-pack --trace-slice renders deterministic context pack JSON",
    prompt: "Implement rfs show command",
    failures: failures);

await RunContextPackTraceSliceValidatedCliCaseAsync(
    name: "context pack trace-slice-validated cli renders validated scoped json",
    prompt: "Implement rfs show command",
    failures: failures);

RunRfsTuiModeSelectionParserCases(failures);
RunRfsTuiCommandSuggestionCases(failures);
RfsTuiModelPickerChecks.Run(failures);
RfsTuiMarkdownLiteChecks.Run(failures);
await RfsTuiAnsiLeakChecks.Run(failures);
await RunRfsTuiCommandSuggestionSessionCaseAsync("tui slash suggestions are filtered and unknown commands are rejected", failures);

await RunRckTuiDirectRecordingCaseAsync(
    name: "tui direct recording stores direct pipeline summary",
    failures: failures);

await RunRckTuiCommitBoundaryAnchorCaseAsync(
    name: "tui recorder creates a commit-boundary anchor when git commit changes",
    failures: failures);

await RunRfsTuiAnchorUsageSessionCaseAsync(
    name: "bare rfs /anchor without a name prints usage and does not call the LLM",
    failures: failures);

await RunRfsTuiAnchorCommandSessionCaseAsync(
    name: "bare rfs /anchor creates a milestone anchor on current HEAD",
    failures: failures);

await RunRfsTuiSimpleModeRecordingSessionCaseAsync(
    name: "bare rfs prompt selects simple mode and records a simple interaction",
    prompt: "Implement reset board action",
    input: "Implement reset board action\n2\n/exit\n",
    failures: failures);

await RunRfsTuiPromptModeSelectionSessionCaseAsync(
    name: "bare rfs prompt selects complete mode real pipeline and records a complete interaction",
    prompt: "Implement reset board action",
    input: "Implement reset board action\n3\n/exit\n",
    expectedFragments: new[]
    {
        "[Complete]",
        "[1/5] Inferring intent...",
        "  intent:",
        "  summary:",
        "  source: pi-intent-inference",
        "[2/5] Building TraceSlice proposal...",
        "  proposal: pi-trace-slice-proposal",
        "  requested selection: 5 states · 5 deltas · 0 anchors",
        "  slicing: anchor-guided structural",
        "  anchors selected:",
        "  expansion:",
        "  fallback:",
        "[3/5] Validating proposal...",
        "  validation: accepted",
        "[4/5] Building ContextPack...",
        "  scope:",
        "  selected states/deltas/anchors:",
        "  estimated tokens:",
        "  transport:",
        "  transport risk:",
        "[5/5] Asking main LLM...",
        "  agent:",
        "  model:",
        "Respuesta:",
        "Recorded State + Delta:",
    },
    expectPromptEcho: false,
    forbiddenFragments: new[]
    {
        "Context:",
        "Context ready:",
    },
    expectedStateCountDelta: 1,
    expectedDeltaCountDelta: 1,
    expectedAnchorCountDelta: 0,
    failures: failures);

await RunRfsTuiPlanModeRecordingSessionCaseAsync(
    name: "bare rfs prompt selects plan mode and records a plan interaction",
    prompt: "Implement reset board action",
    input: "Implement reset board action\n4\n/exit\n",
    failures: failures);

await RunRfsTuiPromptModeSelectionSessionCaseAsync(
    name: "bare rfs prompt rejects invalid mode then cancels",
    prompt: "Implement reset board action",
    input: "Implement reset board action\nabc\n/cancel\n/exit\n",
    expectedFragments: new[]
    {
        "Invalid mode. Choose 1, 2, 3, 4, /cancel, or /exit.",
        "Prompt cancelled.",
    },
    expectPromptEcho: false,
    failures: failures);




await RunRfsTuiPromptModeSelectionSessionCaseAsync(
    name: "bare rfs prompt exits from mode selection",
    prompt: "Implement reset board action",
    input: "Implement reset board action\n/exit\n",
    expectedFragments: new[]
    {
        "¿Cómo querés procesarlo?",
        "  1 Direct    — sin contexto RCK",
        "Elegí 1-4, o /cancel:",
    },
    expectPromptEcho: false,
    failures: failures);

await RunRfsTuiInitializedSessionCaseAsync(
    name: "bare rfs enters tui and handles basic commands on initialized repo",
    failures: failures);

await RunRfsTuiInternalCommandsPolishSessionCaseAsync(
    name: "bare rfs internal commands polish session covers status log model context trace and help",
    failures: failures);


await RunRfsTuiAutoInitSessionCaseAsync(
    name: "bare rfs auto-initializes an empty repo and enters tui",
    failures: failures);

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

static async Task RunPartitionedChecksAsync(string[] args, List<string> failures)
{
    var longPasteOnly = args.Contains("--long-paste-only", StringComparer.Ordinal);
    var coreOnly = args.Contains("--core-only", StringComparer.Ordinal);
    var tuiOnly = args.Contains("--tui-only", StringComparer.Ordinal);
    var integrationOnly = args.Contains("--integration-only", StringComparer.Ordinal);
    var legacyOnly = args.Contains("--legacy-only", StringComparer.Ordinal);

    var selectedFlags = new[]
    {
        longPasteOnly ? "--long-paste-only" : null,
        coreOnly ? "--core-only" : null,
        tuiOnly ? "--tui-only" : null,
        integrationOnly ? "--integration-only" : null,
        legacyOnly ? "--legacy-only" : null,
    }.Where(flag => flag is not null).ToArray();

    Console.WriteLine("ParserChecks");

    if (selectedFlags.Length > 1)
    {
        failures.Add("ParserChecks accepts only one mode flag at a time: --core-only, --tui-only, --long-paste-only, --integration-only, or --legacy-only.");
        return;
    }

    var selectedMode = selectedFlags.Length == 0 ? "default" : selectedFlags[0];

    switch (selectedMode)
    {
        case "--long-paste-only":
            await RunLongPasteChecksAsync(failures);
            return;
        case "--core-only":
            await RunCoreChecksAsync(failures);
            Console.WriteLine("Core checks passed.");
            return;
        case "--tui-only":
            await RunTuiDeterministicChecksAsync(failures);
            Console.WriteLine("TUI deterministic checks passed.");
            return;
        case "--integration-only":
            await RunIntegrationChecksAsync(failures);
            Console.WriteLine("Integration checks completed.");
            return;
        case "--legacy-only":
            await RunLegacyChecksAsync(failures);
            Console.WriteLine("Legacy checks completed.");
            return;
        default:
            await RunCoreChecksAsync(failures);
            await RunTuiDeterministicChecksAsync(failures);
            Console.WriteLine("Core checks passed.");
            Console.WriteLine("TUI deterministic checks passed.");
            Console.WriteLine("Long paste checks skipped by default. Run with --long-paste-only.");
            Console.WriteLine("Integration checks skipped by default. Run with --integration-only.");
            Console.WriteLine("Legacy checks skipped by default. Run with --legacy-only.");
            return;
    }
}

static async Task RunCoreChecksAsync(List<string> failures)
{
    await RunCaseAsync(
        name: "structured final answer",
        fixtureMode: "structured",
        expectedSuccess: true,
        expectedAnswer: "structured answer",
        expectedProvider: "test-provider",
        expectedModel: "test-model",
        expectedErrorContains: null,
        failures: failures);

    await RunCaseAsync(
        name: "delta fallback with stderr separation",
        fixtureMode: "delta",
        expectedSuccess: true,
        expectedAnswer: "hello world",
        expectedProvider: null,
        expectedModel: null,
        expectedErrorContains: null,
        failures: failures);

    await RunCaseAsync(
        name: "final answer beats prior delta",
        fixtureMode: "delta-then-final",
        expectedSuccess: true,
        expectedAnswer: "structured answer",
        expectedProvider: "test-provider",
        expectedModel: "test-model",
        expectedErrorContains: null,
        failures: failures);

    await RunCaseAsync(
        name: "no answer fails explicitly",
        fixtureMode: "no-answer",
        expectedSuccess: false,
        expectedAnswer: null,
        expectedProvider: null,
        expectedModel: null,
        expectedErrorContains: "Pi JSON stream ended before a final assistant answer was observed",
        failures: failures);

    await RunCaseAsync(
        name: "invalid jsonl",
        fixtureMode: "invalid",
        expectedSuccess: false,
        expectedAnswer: null,
        expectedProvider: null,
        expectedModel: null,
        expectedErrorContains: "Invalid JSONL on line 1",
        failures: failures);

    await RunPiJsonRunnerRuntimeEventReportingCaseAsync(failures);

    await RunCaseAsync(
        name: "startup events without completion",
        fixtureMode: "startup-no-completion",
        expectedSuccess: false,
        expectedAnswer: null,
        expectedProvider: null,
        expectedModel: null,
        expectedErrorContains: "msgStart; noMsgEnd",
        failures: failures);

    await RunCaseAsync(
        name: "message end without assistant text",
        fixtureMode: "message-end-no-text",
        expectedSuccess: false,
        expectedAnswer: null,
        expectedProvider: null,
        expectedModel: null,
        expectedErrorContains: "msgEnd",
        failures: failures);

    await RunCaseAsync(
        name: "no events at all",
        fixtureMode: "no-events",
        expectedSuccess: false,
        expectedAnswer: null,
        expectedProvider: null,
        expectedModel: null,
        expectedErrorContains: "events=0; noMsgStart; noMsgEnd",
        failures: failures);

    await RunIntentInferenceCaseAsync(
        name: "intent inference success",
        task: new AgentTask(
            id: "task-1",
            kind: "infer-intent",
            goal: "Infer the operational intent from this prompt.",
            input: "Build a TraceSlice for the current diff and summarize evidence.",
            expectedOutput: "PromptIntent JSON"),
        expectedIntent: "build-trace-slice",
        failures: failures);

    await RunIntentInferenceFailureCaseAsync(
        name: "intent inference rejects unsupported kind",
        task: new AgentTask(
            id: "task-2",
            kind: "summarize-evidence",
            goal: "Summarize evidence for the diff.",
            input: "Summarize the diff evidence.",
            expectedOutput: null),
        expectedErrorContains: "Kind='infer-intent'",
        failures: failures);

    await RunPromptIntentJsonCodecCaseAsync(
        name: "prompt intent codec accepts lowercase llm json",
        json: "{\"intent\":\"implement-reset-board\",\"summary\":\"Implement the reset board action.\",\"entities\":[\"reset board\",\"board\"],\"constraints\":[\"do not write RCK\"]}",
        expectedIntent: "implement-reset-board",
        expectedSummary: "Implement the reset board action.",
        failures: failures);

    await RunPromptIntentJsonCodecFailureCaseAsync(
        name: "prompt intent codec rejects invalid json",
        json: "{\"intent\":\"implement-reset-board\",\"summary\":\"missing brace\"",
        expectedErrorContains: "Invalid PromptIntent JSON",
        failures: failures);

}

static async Task RunTuiDeterministicChecksAsync(List<string> failures)
{
    RunRfsTuiModeSelectionParserCases(failures);
    RunRfsTuiCommandSuggestionCases(failures);
    RfsTuiModelPickerChecks.Run(failures);
    RfsTuiMarkdownLiteChecks.Run(failures);
    RfsTuiPiRunRuntimeChecks.Run(failures);
    await RfsTuiAnsiLeakChecks.Run(failures);

    await RunRfsTuiInitializedSessionCaseAsync(
        name: "bare rfs enters tui and handles deterministic read-only commands on an initialized repo",
        failures: failures);

    await RunRfsTuiAnchorUsageSessionCaseAsync(
        name: "bare rfs /anchor without a name prints usage and does not call the LLM",
        failures: failures);

    await RunRfsTuiAnchorCommandSessionCaseAsync(
        name: "bare rfs /anchor creates a milestone anchor on current HEAD",
        failures: failures);

    await RunRfsTuiAutoInitSessionCaseAsync(
        name: "bare rfs auto-initializes an empty repo and enters tui",
        failures: failures);
}

static async Task RunIntegrationChecksAsync(List<string> failures)
{
    await RunIntentCliLlmCaseAsync(
        name: "intent cli llm renders canonical prompt intent json",
        prompt: "Implement reset board action",
        expectedIntent: "implement-reset-board",
        failures: failures);

    await RunTraceSliceProposalLlmCliCaseAsync(
        name: "trace slice proposal llm cli renders proposal json",
        prompt: "Implement rfs show command",
        failures: failures,
        fixtureMode: "valid");

    await RunTraceSliceProposalLlmCliCaseAsync(
        name: "trace slice proposal llm cli rejects contaminated llm output",
        prompt: "Implement rfs show command",
        failures: failures,
        fixtureMode: "contaminated",
        expectSuccess: false,
        expectedErrorContains: "rationale entries must be objects");

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
        expectedErrorContains: "restricted materialization policy flags must be false");

    await RunAskJsonCliCaseAsync(
        name: "ask-json cli renders structured answer",
        prompt: "Respond with a structured answer.",
        failures: failures);

    await RunAskJsonCliErrorCaseAsync(
        name: "ask-json cli fails when no answer in stream",
        prompt: "This prompt gets no answer.",
        failures: failures);

    await RunAskCliCaseAsync(
        name: "ask cli renders short answer without recording",
        prompt: "Respond with a short answer.",
        failures: failures);

    await RunAskRecordCliCaseAsync(
        name: "ask cli records interaction when --record is used",
        prompt: "Respond with a short answer.",
        failures: failures);

    await RunLegacyAskBridgeCliCaseAsync(
        name: "ask cli surfaces legacy fallback status when RFS_USE_LEGACY_ASK_BRIDGE=1 is set",
        prompt: "Respond with a short answer.",
        failures: failures);

    await RunAgentCliCaseAsync(
        name: "agent cli renders streamed output through node mock",
        task: "Inspect the repo read-only.",
        failures: failures);

    await RunAgentRecordCliCaseAsync(
        name: "agent cli records interaction when --record is used",
        task: "Inspect the repo read-only.",
        failures: failures);

    await RunAgentJsonCliCaseAsync(
        name: "agent-json cli renders agent output with tool actions",
        task: "Inspect the repo read-only.",
        failures: failures);

    await RunRfsTuiCommandSuggestionSessionCaseAsync(
        "tui slash suggestions are filtered and unknown commands are rejected",
        failures);
}

static async Task RunAskJsonCliCaseAsync(
    string name,
    string prompt,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-ask-json-cli-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    var scriptPath = Path.Combine(tempRoot, "pi");
    var script = "#!/usr/bin/env bash\n" +
                 "set -euo pipefail\n" +
                 "echo '{\"type\":\"session\"}'\n" +
                 "echo '{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":\"structured answer\"}]}}'\n" +
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
    try
    {
        Environment.SetEnvironmentVariable("PATH", tempRoot + Path.PathSeparator + (originalPath ?? string.Empty));

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
        startInfo.ArgumentList.Add("ask-json");
        startInfo.ArgumentList.Add(prompt);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            failures.Add($"[{name}] failed to start dotnet run for rfs ask-json.");
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

        if (!stdout.Contains("Rufus Ask JSON Prototype", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'Rufus Ask JSON Prototype' in stdout. stdout: {stdout}");
        }

        if (!stdout.Contains("Status: experimental diagnostic path.", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected ask-json status label in stdout. stdout: {stdout}");
        }

        if (!stdout.Contains("structured answer", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'structured answer' in stdout. stdout: {stdout}");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        Environment.SetEnvironmentVariable("PATH", originalPath);

        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static async Task RunAskJsonCliErrorCaseAsync(
    string name,
    string prompt,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-ask-json-cli-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    var scriptPath = Path.Combine(tempRoot, "pi");
    var script = "#!/usr/bin/env bash\n" +
                 "set -euo pipefail\n" +
                 "echo '{\"type\":\"session\"}'\n" +
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
    try
    {
        Environment.SetEnvironmentVariable("PATH", tempRoot + Path.PathSeparator + (originalPath ?? string.Empty));

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
        startInfo.ArgumentList.Add("ask-json");
        startInfo.ArgumentList.Add(prompt);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            failures.Add($"[{name}] failed to start dotnet run for rfs ask-json.");
            return;
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode == 0)
        {
            failures.Add($"[{name}] expected non-zero exit code but got 0. stdout: {stdout}");
            return;
        }

        if (!stderr.Contains("final assistant answer", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'final assistant answer' in stderr. stderr: {stderr}");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        Environment.SetEnvironmentVariable("PATH", originalPath);

        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}
static async Task RunAskCliCaseAsync(
    string name,
    string prompt,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-ask-cli-checks", Guid.NewGuid().ToString("N"));
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
        var result = await RunMockedAskCliAsync(name, cliProjectPath, tempRoot, prompt, answer: "short answer", recordInteraction: false, useLegacyBridge: false, failures);
        if (result.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {result.ExitCode}. stderr: {result.Stderr}");
            return;
        }

        if (!result.Stdout.Contains("Rufus Ask", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'Rufus Ask' in stdout. stdout: {result.Stdout}");
        }

        if (!result.Stdout.Contains("short answer", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'short answer' in stdout. stdout: {result.Stdout}");
        }

        if (result.Stdout.Contains("Rufus Ask JSON Prototype", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected ask output not to use the ask-json formatter. stdout: {result.Stdout}");
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        AssertRckCountDeltas(name, statusBefore, statusAfter, expectedStateDelta: 0, expectedDeltaDelta: 0, expectedAnchorDelta: 0, failures);
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        TryDeleteDirectory(tempRoot);
    }
}

static async Task RunAskRecordCliCaseAsync(
    string name,
    string prompt,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-ask-record-cli-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        if (!await InitializeTempGitRepoAndRckAsync(name, tempRoot, failures))
        {
            return;
        }

        var statusBefore = RckWorkspaceStatusReader.Read(tempRoot);
        var deltaFilesBefore = Directory.EnumerateFiles(Path.Combine(tempRoot, ".rfs", "rck", "deltas"), "*.json", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);

        var result = await RunMockedAskCliAsync(name, cliProjectPath, tempRoot, prompt, answer: "short answer", recordInteraction: true, useLegacyBridge: false, failures);
        if (result.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {result.ExitCode}. stderr: {result.Stderr}");
            return;
        }

        if (!result.Stdout.Contains("Rufus Ask", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'Rufus Ask' in stdout. stdout: {result.Stdout}");
        }

        if (!result.Stdout.Contains("short answer", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'short answer' in stdout. stdout: {result.Stdout}");
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        AssertRckCountDeltas(name, statusBefore, statusAfter, expectedStateDelta: 1, expectedDeltaDelta: 1, expectedAnchorDelta: 0, failures);

        if (string.IsNullOrWhiteSpace(statusAfter.Head))
        {
            failures.Add($"[{name}] expected HEAD to be updated.");
            return;
        }

        var stateFileName = $"{statusAfter.Head}.json";
        var stateFilePath = Path.Combine(tempRoot, ".rfs", "rck", "states", stateFileName);
        if (!File.Exists(stateFilePath))
        {
            failures.Add($"[{name}] expected state file '{stateFileName}' to exist.");
            return;
        }

        var statePayload = ReadRckStatePayload(stateFilePath);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "mode", "ask", failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "prompt", prompt, failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "answer", "short answer", failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "answerSummary", "short answer", failures);

        var deltaFilesAfter = Directory.EnumerateFiles(Path.Combine(tempRoot, ".rfs", "rck", "deltas"), "*.json", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        var newDeltaFiles = deltaFilesAfter.Except(deltaFilesBefore, StringComparer.Ordinal).ToArray();
        if (newDeltaFiles.Length != 1)
        {
            failures.Add($"[{name}] expected exactly one new delta file but found {newDeltaFiles.Length}.");
            return;
        }

        var deltaPayload = ReadRckDeltaOperationPayload(Path.Combine(tempRoot, ".rfs", "rck", "deltas", newDeltaFiles[0]));
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "type", "llm-interaction", failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "mode", "ask", failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "prompt", prompt, failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "answer", "short answer", failures);
        AssertJsonArrayLength(name, deltaPayload.GetProperty("evidence"), "tools", 0, failures);
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        TryDeleteDirectory(tempRoot);
    }
}

static async Task RunLegacyAskBridgeCliCaseAsync(
    string name,
    string prompt,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-legacy-ask-bridge-cli-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var result = await RunMockedAskCliAsync(
            name,
            cliProjectPath,
            tempRoot,
            prompt,
            answer: "short answer",
            recordInteraction: false,
            useLegacyBridge: true,
            failures);

        if (result.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {result.ExitCode}. stderr: {result.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {result.Stderr.Trim()}.");
        }

        if (!result.Stdout.Contains("Rufus Ask", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'Rufus Ask' in stdout. stdout: {result.Stdout}");
        }

        if (!result.Stdout.Contains("Status: legacy ask bridge fallback enabled by RFS_USE_LEGACY_ASK_BRIDGE.", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected legacy ask fallback status label in stdout. stdout: {result.Stdout}");
        }

        if (!result.Stdout.Contains("short answer", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'short answer' in stdout. stdout: {result.Stdout}");
        }

        if (Directory.Exists(Path.Combine(tempRoot, ".rfs")))
        {
            failures.Add($"[{name}] expected no .rfs directory to be created for non-record ask CLI.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        TryDeleteDirectory(tempRoot);
    }
}

static async Task RunAgentCliCaseAsync(
    string name,
    string task,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-agent-cli-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        var result = await RunMockedAgentCliAsync(name, cliProjectPath, tempRoot, task, recordInteraction: false, failures);
        if (result.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {result.ExitCode}. stderr: {result.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {result.Stderr.Trim()}.");
        }

        if (!result.Stdout.Contains("Rufus Agent", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'Rufus Agent' in stdout. stdout: {result.Stdout}");
        }

        if (!result.Stdout.Contains("Status: legacy active bridge (Node). Use agent-json only for experimental JSON Event Stream validation.", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected legacy agent status label in stdout. stdout: {result.Stdout}");
        }

        if (!result.Stdout.Contains("short answer", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'short answer' in stdout. stdout: {result.Stdout}");
        }

        if (!result.Stdout.Contains("read_file README.md", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected tool action 'read_file README.md' in stdout. stdout: {result.Stdout}");
        }

        if (!result.Stdout.Contains("✓ read_file README.md", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected completed tool line for 'read_file README.md' in stdout. stdout: {result.Stdout}");
        }

        if (Directory.Exists(Path.Combine(tempRoot, ".rfs")))
        {
            failures.Add($"[{name}] expected no .rfs directory to be created for non-record agent CLI.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        TryDeleteDirectory(tempRoot);
    }
}

static async Task RunAgentRecordCliCaseAsync(
    string name,
    string task,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-agent-record-cli-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        if (!await InitializeTempGitRepoAndRckAsync(name, tempRoot, failures))
        {
            return;
        }

        var statusBefore = RckWorkspaceStatusReader.Read(tempRoot);
        var deltaFilesBefore = Directory.EnumerateFiles(Path.Combine(tempRoot, ".rfs", "rck", "deltas"), "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);

        var result = await RunMockedAgentCliAsync(name, cliProjectPath, tempRoot, task, recordInteraction: true, failures);
        if (result.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {result.ExitCode}. stderr: {result.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {result.Stderr.Trim()}.");
        }

        if (!result.Stdout.Contains("Rufus Agent", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'Rufus Agent' in stdout. stdout: {result.Stdout}");
        }

        if (!result.Stdout.Contains("Status: legacy active bridge (Node) with RCK recording.", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected legacy agent recording status label in stdout. stdout: {result.Stdout}");
        }

        if (!result.Stdout.Contains("short answer", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'short answer' in stdout. stdout: {result.Stdout}");
        }

        if (!result.Stdout.Contains("read_file README.md", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected tool action 'read_file README.md' in stdout. stdout: {result.Stdout}");
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        AssertRckCountDeltas(name, statusBefore, statusAfter, expectedStateDelta: 1, expectedDeltaDelta: 1, expectedAnchorDelta: 0, failures);

        if (string.IsNullOrWhiteSpace(statusAfter.Head))
        {
            failures.Add($"[{name}] expected HEAD to be updated.");
            return;
        }

        var stateFileName = $"{statusAfter.Head}.json";
        var stateFilePath = Path.Combine(tempRoot, ".rfs", "rck", "states", stateFileName);
        if (!File.Exists(stateFilePath))
        {
            failures.Add($"[{name}] expected state file '{stateFileName}' to exist.");
            return;
        }

        var statePayload = ReadRckStatePayload(stateFilePath);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "mode", "agent", failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "prompt", task, failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "answer", "short answer", failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "answerSummary", "short answer", failures);

        var deltaFilesAfter = Directory.EnumerateFiles(Path.Combine(tempRoot, ".rfs", "rck", "deltas"), "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);
        var newDeltaFiles = deltaFilesAfter.Except(deltaFilesBefore, StringComparer.Ordinal).ToArray();
        if (newDeltaFiles.Length != 1)
        {
            failures.Add($"[{name}] expected exactly one new delta file but found {newDeltaFiles.Length}.");
            return;
        }

        var deltaPayload = ReadRckDeltaOperationPayload(Path.Combine(tempRoot, ".rfs", "rck", "deltas", newDeltaFiles[0]));
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "type", "llm-interaction", failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "mode", "agent", failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "prompt", task, failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "answer", "short answer", failures);
        AssertJsonArrayLength(name, deltaPayload.GetProperty("evidence"), "tools", 1, failures);

        var toolElement = deltaPayload.GetProperty("evidence").GetProperty("tools")[0];
        AssertJsonString(name, toolElement, "name", "read_file", failures);
        AssertJsonString(name, toolElement, "status", "completed", failures);
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        TryDeleteDirectory(tempRoot);
    }
}

static async Task<(int ExitCode, string Stdout, string Stderr)> RunMockedAgentCliAsync(
    string name,
    string cliProjectPath,
    string workingDirectory,
    string task,
    bool recordInteraction,
    List<string> failures)
{
    var tempToolRoot = Path.Combine(Path.GetTempPath(), "rfs-agent-cli-mock", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempToolRoot);

    var nodeScriptPath = Path.Combine(tempToolRoot, "node");
    var expectedTask = EscapeBashDoubleQuotedJson(task);
    var nodeScript = "#!/usr/bin/env bash\n" +
                     "set -euo pipefail\n" +
                     "helper_path=${1:-}\n" +
                     "received_task=${2:-}\n" +
                     $"expected_task=\"{expectedTask}\"\n" +
                     "if [[ -z \"$helper_path\" || -z \"$received_task\" ]]; then\n" +
                     "  echo 'missing node args' >&2\n" +
                     "  exit 97\n" +
                     "fi\n" +
                     "if [[ \"$helper_path\" != *\"rfs-agent.mjs\" ]]; then\n" +
                     "  echo \"unexpected helper path: $helper_path\" >&2\n" +
                     "  exit 98\n" +
                     "fi\n" +
                     "if [[ \"$received_task\" != \"$expected_task\" ]]; then\n" +
                     "  echo \"unexpected task: $received_task\" >&2\n" +
                     "  exit 99\n" +
                     "fi\n" +
                     "echo '[agent:start] mocked agent start'\n" +
                     "echo '[tool:start] id=tool-1 name=read_file path=README.md'\n" +
                     "echo '[tool:end] id=tool-1 name=read_file ok'\n" +
                     "echo '[assistant] short answer'\n" +
                     "echo '[agent:end]'\n" +
                     "exit 0\n";

    await File.WriteAllTextAsync(nodeScriptPath, nodeScript);
    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(
            nodeScriptPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    var originalPath = Environment.GetEnvironmentVariable("PATH");
    try
    {
        Environment.SetEnvironmentVariable("PATH", tempToolRoot + Path.PathSeparator + (originalPath ?? string.Empty));

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(cliProjectPath);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("agent");
        if (recordInteraction)
        {
            startInfo.ArgumentList.Add("--record");
        }

        startInfo.ArgumentList.Add(task);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            failures.Add($"[{name}] failed to start dotnet run for rfs agent.");
            return (-1, string.Empty, string.Empty);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
        return (-1, string.Empty, string.Empty);
    }
    finally
    {
        Environment.SetEnvironmentVariable("PATH", originalPath);

        try
        {
            Directory.Delete(tempToolRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static async Task<(int ExitCode, string Stdout, string Stderr)> RunMockedAskCliAsync(
    string name,
    string cliProjectPath,
    string workingDirectory,
    string prompt,
    string answer,
    bool recordInteraction,
    bool useLegacyBridge,
    List<string> failures)
{
    var tempToolRoot = Path.Combine(Path.GetTempPath(), "rfs-ask-cli-mock", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempToolRoot);

    var piScriptPath = Path.Combine(tempToolRoot, "pi");
    var piScript = "#!/usr/bin/env bash\n" +
                   "set -euo pipefail\n" +
                   "echo '{\"type\":\"session\"}'\n" +
                   $"echo '{{\"type\":\"message_end\",\"message\":{{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{{\"type\":\"text\",\"text\":\"{EscapeBashDoubleQuotedJson(answer)}\"}}]}}}}'\n" +
                   "exit 0\n";

    var nodeScriptPath = Path.Combine(tempToolRoot, "node");
    var nodeScript = useLegacyBridge
        ? "#!/usr/bin/env bash\n" +
          "set -euo pipefail\n" +
          $"echo '{EscapeBashDoubleQuotedJson(answer)}'\n" +
          "exit 0\n"
        : "#!/usr/bin/env bash\n" +
          "set -euo pipefail\n" +
          "echo 'legacy ask bridge should not be invoked' >&2\n" +
          "exit 99\n";

    await File.WriteAllTextAsync(piScriptPath, piScript);
    await File.WriteAllTextAsync(nodeScriptPath, nodeScript);
    if (!OperatingSystem.IsWindows())
    {
        var executableMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                              UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                              UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        File.SetUnixFileMode(piScriptPath, executableMode);
        File.SetUnixFileMode(nodeScriptPath, executableMode);
    }

    var originalPath = Environment.GetEnvironmentVariable("PATH");
    try
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.Environment["PATH"] = tempToolRoot + Path.PathSeparator + (originalPath ?? string.Empty);
        if (useLegacyBridge)
        {
            startInfo.Environment["RFS_USE_LEGACY_ASK_BRIDGE"] = "1";
        }
        else
        {
            startInfo.Environment.Remove("RFS_USE_LEGACY_ASK_BRIDGE");
        }

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(cliProjectPath);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("ask");
        if (recordInteraction)
        {
            startInfo.ArgumentList.Add("--record");
        }

        startInfo.ArgumentList.Add(prompt);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            failures.Add($"[{name}] failed to start dotnet run for rfs ask.");
            return (-1, string.Empty, string.Empty);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
        return (-1, string.Empty, string.Empty);
    }
    finally
    {
        try
        {
            Directory.Delete(tempToolRoot, recursive: true);
        }
        catch
        {
        }

        Environment.SetEnvironmentVariable("PATH", originalPath);
    }
}

static string EscapeBashDoubleQuotedJson(string text)
{
    return text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}

static JsonElement ReadRckStatePayload(string stateFilePath)
{
    using var stateDocument = JsonDocument.Parse(File.ReadAllText(stateFilePath));
    var payloadJson = stateDocument.RootElement.GetProperty("payloadCanonicalJson").GetString() ?? throw new InvalidDataException("state payloadCanonicalJson missing");
    using var payloadDocument = JsonDocument.Parse(payloadJson);
    return payloadDocument.RootElement.Clone();
}

static JsonElement ReadRckDeltaOperationPayload(string deltaFilePath)
{
    using var deltaDocument = JsonDocument.Parse(File.ReadAllText(deltaFilePath));
    var valueJson = deltaDocument.RootElement.GetProperty("ops")[0].GetProperty("valueJson").GetString() ?? throw new InvalidDataException("delta op valueJson missing");
    using var payloadDocument = JsonDocument.Parse(valueJson);
    return payloadDocument.RootElement.Clone();
}

static async Task RunAgentJsonCliCaseAsync(
    string name,
    string task,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-agent-json-cli-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    var scriptPath = Path.Combine(tempRoot, "pi");
    var script = "#!/usr/bin/env bash\n" +
                 "set -euo pipefail\n" +
                 "echo '{\"type\":\"session\"}'\n" +
                 "echo '{\"type\":\"tool_execution_start\",\"id\":\"tool-1\",\"name\":\"read\",\"details\":\"README.md\"}'\n" +
                 "echo '{\"type\":\"tool_execution_end\",\"id\":\"tool-1\",\"name\":\"read\",\"summary\":\"ok\"}'\n" +
                 "echo '{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":\"structured agent answer\"}]}}'\n" +
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
    try
    {
        Environment.SetEnvironmentVariable("PATH", tempRoot + Path.PathSeparator + (originalPath ?? string.Empty));

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
        startInfo.ArgumentList.Add("agent-json");
        startInfo.ArgumentList.Add(task);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            failures.Add($"[{name}] failed to start dotnet run for rfs agent-json.");
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

        if (!stderr.Contains("Experimental: relies on Pi --tools enforcement", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'Experimental: relies on Pi --tools enforcement' in stderr. stderr: {stderr}");
        }

        if (!stdout.Contains("Rufus Agent JSON Prototype", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'Rufus Agent JSON Prototype' in stdout. stdout: {stdout}");
        }

        if (!stdout.Contains("Status: experimental forward path.", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected agent-json status label in stdout. stdout: {stdout}");
        }

        if (!stdout.Contains("structured agent answer", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'structured agent answer' in stdout. stdout: {stdout}");
        }

        if (!stdout.Contains("read README.md", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected 'read README.md' tool action in stdout. stdout: {stdout}");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        Environment.SetEnvironmentVariable("PATH", originalPath);

        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static async Task RunLegacyChecksAsync(List<string> failures)
{
    await RunRfsTuiSimpleModeRecordingSessionCaseAsync(
        name: "bare rfs prompt selects simple mode and records a simple interaction",
        prompt: "Implement reset board action",
        input: "Implement reset board action\n2\n/exit\n",
        failures: failures);

    await RunRfsTuiCompleteModeRecordingSessionCaseAsync(
        name: "bare rfs prompt selects complete mode real pipeline and records a complete interaction",
        prompt: "Implement reset board action",
        input: "Implement reset board action\n3\n/exit\n",
        failures: failures);

    await RunRfsTuiPlanModeRecordingSessionCaseAsync(
        name: "bare rfs prompt selects plan mode and records a plan interaction",
        prompt: "Implement reset board action",
        input: "Implement reset board action\n4\n/exit\n",
        failures: failures);

    await RunRfsTuiPromptModeSelectionSessionCaseAsync(
        name: "bare rfs prompt rejects invalid mode then cancels",
        prompt: "Implement reset board action",
        input: "Implement reset board action\nabc\n/cancel\n/exit\n",
        expectedFragments: new[]
        {
            "Invalid mode. Choose 1, 2, 3, 4, /cancel, or /exit.",
            "Prompt cancelled.",
        },
        expectPromptEcho: false,
        failures: failures);

    await RunRfsTuiPromptModeSelectionSessionCaseAsync(
        name: "bare rfs prompt exits from mode selection",
        prompt: "Implement reset board action",
        input: "Implement reset board action\n/exit\n",
        expectedFragments: new[]
        {
            "¿Cómo querés procesarlo?",
            "  1 Direct    — sin contexto RCK",
            "Elegí 1-4, o /cancel:",
        },
        expectPromptEcho: false,
        failures: failures);

    await RunRfsTuiInternalCommandsPolishSessionCaseAsync(
        name: "bare rfs internal commands polish session covers status log model context trace and help",
        failures: failures);
}

static async Task RunLongPasteChecksAsync(List<string> failures)
{
    await RfsTuiLongPasteChecks.RunAsync(failures);
}

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
                 "  startup-no-completion)\n" +
                 "    echo '{\"type\":\"session\"}'\n" +
                 "    echo '{\"type\":\"agent_start\"}'\n" +
                 "    echo '{\"type\":\"turn_start\"}'\n" +
                 "    echo '{\"type\":\"message_start\"}'\n" +
                 "    ;;\n" +
                 "  message-end-no-text)\n" +
                 "    echo '{\"type\":\"session\"}'\n" +
                 "    echo '{\"type\":\"message_start\"}'\n" +
                 "    echo '{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"content\":[]}}'\n" +
                 "    ;;\n" +
                 "  no-events)\n" +
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
        failures.Add($"[{name}] threw {ex}");
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
        failures.Add($"[{name}] threw {ex}");
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
        failures.Add($"[{name}] threw {ex}");
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

static string BuildAnchorSelectionAnswer(
    IReadOnlyList<string> selectedAnchorIds,
    string fallbackStrategy,
    IReadOnlyList<(string Target, string Reason)> rationale,
    IReadOnlyList<string> warnings,
    double confidence,
    int schemaVersion,
    string type)
{
    var payload = new Dictionary<string, object?>
    {
        ["type"] = type,
        ["schemaVersion"] = schemaVersion,
        ["selectedAnchorIds"] = selectedAnchorIds,
        ["fallbackStrategy"] = fallbackStrategy,
        ["rationale"] = rationale.Select(item => new Dictionary<string, object?>
        {
            ["target"] = item.Target,
            ["reason"] = item.Reason,
        }).ToArray(),
        ["warnings"] = warnings,
        ["confidence"] = confidence,
    };

    return JsonSerializer.Serialize(payload);
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
        failures.Add($"[{name}] threw {ex}");
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
        failures.Add($"[{name}] threw {ex}");
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
        failures.Add($"[{name}] threw {ex}");
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
        failures.Add($"[{name}] threw {ex}");
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
        failures.Add($"[{name}] threw {ex}");
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
        failures.Add($"[{name}] threw {ex}");
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
        failures.Add($"[{name}] threw {ex}");
    }
}

static Task RunPromptIntentJsonCodecCaseAsync(
    string name,
    string json,
    string expectedIntent,
    string expectedSummary,
    List<string> failures)
{
    try
    {
        var parsed = PromptIntentJsonCodec.Parse(json);
        if (!string.Equals(parsed.Intent, expectedIntent, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected PromptIntent.Intent '{expectedIntent}' but got '{parsed.Intent}'.");
        }

        if (!string.Equals(parsed.Summary, expectedSummary, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected PromptIntent.Summary '{expectedSummary}' but got '{parsed.Summary}'.");
        }

        if (parsed.Entities.Count != 2)
        {
            failures.Add($"[{name}] expected 2 entities but got {parsed.Entities.Count}.");
        }

        if (parsed.Constraints.Count != 1)
        {
            failures.Add($"[{name}] expected 1 constraint but got {parsed.Constraints.Count}.");
        }

        using var document = JsonDocument.Parse(PromptIntentJsonCodec.Write(parsed));
        var root = document.RootElement;
        if (!root.TryGetProperty("Intent", out var intentProperty) || !string.Equals(intentProperty.GetString(), expectedIntent, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected canonical JSON Intent property.");
        }

        if (!root.TryGetProperty("Summary", out var summaryProperty) || !string.Equals(summaryProperty.GetString(), expectedSummary, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected canonical JSON Summary property.");
        }

        if (!root.TryGetProperty("Entities", out var entitiesProperty) || entitiesProperty.ValueKind != JsonValueKind.Array)
        {
            failures.Add($"[{name}] expected canonical JSON Entities array.");
        }

        if (!root.TryGetProperty("Constraints", out var constraintsProperty) || constraintsProperty.ValueKind != JsonValueKind.Array)
        {
            failures.Add($"[{name}] expected canonical JSON Constraints array.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }

    return Task.CompletedTask;
}

static Task RunPromptIntentJsonCodecFailureCaseAsync(
    string name,
    string json,
    string expectedErrorContains,
    List<string> failures)
{
    try
    {
        if (PromptIntentJsonCodec.TryParse(json, out var promptIntent, out var errorMessage))
        {
            failures.Add($"[{name}] expected parsing to fail but it succeeded with intent '{promptIntent?.Intent}'.");
        }
        else if (string.IsNullOrWhiteSpace(errorMessage) || !errorMessage.Contains(expectedErrorContains, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected error containing '{expectedErrorContains}' but got '{errorMessage ?? "(null)"}'.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }

    return Task.CompletedTask;
}

static async Task RunPiIntentInferenceAgentCaseAsync(
    string name,
    AgentTask task,
    string llmAnswerJson,
    string expectedIntent,
    List<string> failures)
{
    try
    {
        var transport = new FakeIntentLlmTransport(success: true, answerJson: llmAnswerJson);
        var agent = new PiIntentInferenceAgent(transport: transport);
        var result = await agent.ExecuteAsync(task);

        if (!string.Equals(agent.Descriptor.ExecutionModel.Provider, "pi", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected agent descriptor provider 'pi' but got '{agent.Descriptor.ExecutionModel.Provider}'.");
        }

        if (!string.Equals(agent.Descriptor.ExecutionModel.Model, "claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected agent descriptor model 'claude-haiku-4.5' but got '{agent.Descriptor.ExecutionModel.Model}'.");
        }

        if (result.Status != AgentTaskStatus.Succeeded)
        {
            failures.Add($"[{name}] expected Status=Succeeded but got {result.Status}.");
        }

        if (!string.Equals(result.TaskId, task.Id, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected TaskId '{task.Id}' but got '{result.TaskId}'.");
        }

        if (!string.Equals(result.AgentId, "pi-intent-inference", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected AgentId 'pi-intent-inference' but got '{result.AgentId}'.");
        }

        if (!string.Equals(result.ExecutionModel.Provider, "pi", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected execution provider 'pi' but got '{result.ExecutionModel.Provider}'.");
        }

        if (!string.Equals(result.ExecutionModel.Model, "claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected execution model 'claude-haiku-4.5' but got '{result.ExecutionModel.Model}'.");
        }

        if (transport.CallCount != 1)
        {
            failures.Add($"[{name}] expected the LLM transport to be called once but got {transport.CallCount} calls.");
        }

        if (!string.Equals(transport.LastModel, "claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected transport model 'claude-haiku-4.5' but got '{transport.LastModel}'.");
        }

        if (string.IsNullOrWhiteSpace(transport.LastPrompt) ||
            !transport.LastPrompt.Contains("Valid intent labels:", StringComparison.Ordinal) ||
            !transport.LastPrompt.Contains("Classify by operational intent, not grammatical form.", StringComparison.Ordinal) ||
            !transport.LastPrompt.Contains("code-change", StringComparison.Ordinal) ||
            !transport.LastPrompt.Contains("repo-analysis", StringComparison.Ordinal) ||
            !transport.LastPrompt.Contains("planning", StringComparison.Ordinal) ||
            !transport.LastPrompt.Contains("rck-memory", StringComparison.Ordinal) ||
            !transport.LastPrompt.Contains("docs-update", StringComparison.Ordinal) ||
            !transport.LastPrompt.Contains("chat", StringComparison.Ordinal) ||
            !transport.LastPrompt.Contains("question", StringComparison.Ordinal) ||
            !transport.LastPrompt.Contains(task.Input!, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected the generated LLM prompt to include valid intent labels, classification rules, few-shot examples, and the user prompt.");
        }

        if (string.IsNullOrWhiteSpace(result.Output))
        {
            failures.Add($"[{name}] expected Output to be populated.");
        }
        else
        {
            var parsed = PromptIntentJsonCodec.Parse(result.Output);
            if (!string.Equals(parsed.Intent, expectedIntent, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected PromptIntent.Intent '{expectedIntent}' but got '{parsed.Intent}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(result.Summary))
        {
            failures.Add($"[{name}] expected Summary to be populated.");
        }

        if (result.Evidence.Count == 0)
        {
            failures.Add($"[{name}] expected Evidence to be populated.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
}

static async Task RunPiIntentInferenceAgentFailureCaseAsync(
    string name,
    AgentTask task,
    string llmAnswerJson,
    string expectedErrorContains,
    List<string> failures)
{
    try
    {
        var transport = new FakeIntentLlmTransport(success: true, answerJson: llmAnswerJson);
        var agent = new PiIntentInferenceAgent(transport: transport);
        var result = await agent.ExecuteAsync(task);

        if (!string.Equals(agent.Descriptor.ExecutionModel.Model, "claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected agent descriptor model 'claude-haiku-4.5' but got '{agent.Descriptor.ExecutionModel.Model}'.");
        }

        if (result.Status != AgentTaskStatus.Failed)
        {
            failures.Add($"[{name}] expected Status=Failed but got {result.Status}.");
        }

        if (transport.CallCount != 1)
        {
            failures.Add($"[{name}] expected the LLM transport to be called once but got {transport.CallCount} calls.");
        }

        if (!string.Equals(transport.LastModel, "claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected transport model 'claude-haiku-4.5' but got '{transport.LastModel}'.");
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
        failures.Add($"[{name}] threw {ex}");
    }
}

static async Task RunCompleteModePipelineWithIntentLlmCaseAsync(
    string name,
    string repoRoot,
    string prompt,
    string llmAnswerJson,
    string expectedIntent,
    List<string> failures)
{
    var originalOut = Console.Out;
    using var stdout = new StringWriter();
    try
    {
        Console.SetOut(stdout);
        var transport = new FakeIntentLlmTransport(success: true, answerJson: llmAnswerJson);
        var intentAgent = new PiIntentInferenceAgent(repoRoot, transport: transport);
        var result = await RfsCompleteModePipeline.BuildAsync(prompt, repoRoot, 5, intentAgent, stageWriter: _ => { });

        var completeConsole = stdout.ToString();
        if (!completeConsole.Contains("model: claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected complete-mode console output to include model: claude-haiku-4.5 but it was missing.");
        }

        if (!string.Equals(intentAgent.Descriptor.ExecutionModel.Model, "claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected intent agent descriptor model 'claude-haiku-4.5' but got '{intentAgent.Descriptor.ExecutionModel.Model}'.");
        }

        if (!result.Success)
        {
            failures.Add($"[{name}] expected Success=true but got false. Error: {result.ErrorMessage}");
            return;
        }

        if (!string.Equals(result.IntentSource, "pi-intent-inference", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected IntentSource 'pi-intent-inference' but got '{result.IntentSource}'.");
        }

        if (!string.Equals(result.IntentKind, expectedIntent, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected IntentKind '{expectedIntent}' but got '{result.IntentKind}'.");
        }

        if (string.IsNullOrWhiteSpace(result.IntentSummary))
        {
            failures.Add($"[{name}] expected IntentSummary to be populated.");
        }

        if (!string.Equals(result.ProposalSource, "pi-trace-slice-proposal", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected ProposalSource 'pi-trace-slice-proposal' but got '{result.ProposalSource}'.");
        }

        if (string.IsNullOrWhiteSpace(result.ValidationStatus))
        {
            failures.Add($"[{name}] expected ValidationStatus to be populated.");
        }

        if (result.SelectedStateIds.Count == 0 || result.SelectedDeltaIds.Count == 0)
        {
            failures.Add($"[{name}] expected selected states/deltas to be populated.");
        }

        if (string.IsNullOrWhiteSpace(result.PromptToSend))
        {
            failures.Add($"[{name}] expected PromptToSend to be populated.");
        }
        else
        {
            var promptToSend = result.PromptToSend;
            var expectedPromptFragments = new[]
            {
                "Output formatting:",
                "Markdown-lite rendering",
                "compact text diagram",
                "Use text diagrams only when they materially improve clarity.",
                "Do not include diagrams for simple factual answers.",
                "At most one diagram unless explicitly requested.",
                "Do not use Mermaid unless the user explicitly asks for Mermaid.",
                "validated ContextPack as the authoritative structural project context",
                "ConversationalMemory only for recent conversational continuity",
                "Do not use ConversationalMemory to override validated structural facts",
                "prefer the validated ContextPack",
            };

            foreach (var fragment in expectedPromptFragments)
            {
                if (!promptToSend.Contains(fragment, StringComparison.Ordinal))
                {
                    failures.Add($"[{name}] expected PromptToSend to contain '{fragment}' but it was missing.");
                }
            }
        }

        if (transport.CallCount != 1)
        {
            failures.Add($"[{name}] expected the intent LLM to be called once but got {transport.CallCount} calls.");
        }

        if (!string.Equals(transport.LastModel, "claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected the intent transport model 'claude-haiku-4.5' but got '{transport.LastModel}'.");
        }

        var workspaceDefault = RckWorkspaceModelConfigStore.TryReadDefaultModel(repoRoot);
        if (!string.IsNullOrWhiteSpace(workspaceDefault) && !string.Equals(workspaceDefault, "claude-haiku-4.5", StringComparison.Ordinal) && string.Equals(transport.LastModel, workspaceDefault, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected intent transport model not to inherit workspace default '{workspaceDefault}'.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        Console.SetOut(originalOut);
    }
}

static async Task RunCompleteModePipelineWithAnchorSelectionLlmCaseAsync(
    string name,
    string repoRoot,
    string prompt,
    string llmAnswerJson,
    List<string> failures)
{
    var anchorSelectionJson = BuildAnchorSelectionAnswer(
        selectedAnchorIds: new[] { "anchor-a" },
        fallbackStrategy: "none",
        rationale: new[] { (Target: "anchor-a", Reason: "Best structural entry point for this slice.") },
        warnings: Array.Empty<string>(),
        confidence: 0.94,
        schemaVersion: 1,
        type: "rufus.anchor-selection");

    await RunCompleteModePipelineWithAnchorSelectionCaseAsync(
        name,
        repoRoot,
        prompt,
        llmAnswerJson,
        anchorSelectionJson,
        expectFailure: false,
        expectedErrorContains: null,
        expectedFallbackStrategy: null,
        failures);
}

static async Task RunCompleteModePipelineWithAnchorSelectionFallbackCaseAsync(
    string name,
    string repoRoot,
    string prompt,
    string llmAnswerJson,
    List<string> failures)
{
    var anchorSelectionJson = BuildAnchorSelectionAnswer(
        selectedAnchorIds: Array.Empty<string>(),
        fallbackStrategy: "recent-chain",
        rationale: Array.Empty<(string Target, string Reason)>(),
        warnings: new[] { "rfs anchor-selection: no relevant anchors; using recent-chain fallback." },
        confidence: 0.20,
        schemaVersion: 1,
        type: "rufus.anchor-selection");

    await RunCompleteModePipelineWithAnchorSelectionCaseAsync(
        name,
        repoRoot,
        prompt,
        llmAnswerJson,
        anchorSelectionJson,
        expectFailure: false,
        expectedErrorContains: null,
        expectedFallbackStrategy: "recent-chain-fallback",
        failures);
}

static async Task RunCompleteModePipelineWithAnchorSelectionFailureCaseAsync(
    string name,
    string repoRoot,
    string prompt,
    string llmAnswerJson,
    List<string> failures)
{
    await RunCompleteModePipelineWithAnchorSelectionCaseAsync(
        name,
        repoRoot,
        prompt,
        llmAnswerJson,
        anchorSelectionJson: "not-json",
        expectFailure: true,
        expectedErrorContains: "Complete mode failed while building anchor selection.",
        expectedFallbackStrategy: null,
        failures);
}

static async Task RunCompleteModePipelineWithAnchorSelectionCaseAsync(
    string name,
    string repoRoot,
    string prompt,
    string llmAnswerJson,
    string anchorSelectionJson,
    bool expectFailure,
    string? expectedErrorContains,
    string? expectedFallbackStrategy,
    List<string> failures)
{
    var originalOut = Console.Out;
    using var stdout = new StringWriter();
    try
    {
        Console.SetOut(stdout);
        var intentTransport = new FakeIntentLlmTransport(success: true, answerJson: llmAnswerJson);
        var intentAgent = new PiIntentInferenceAgent(repoRoot, transport: intentTransport);
        var proposalTransport = new FakeTraceSliceProposalLlmTransport(anchorSelectionJson);
        var proposalAgent = new PiTraceSliceProposalAgent(repoRoot, transport: proposalTransport);
        var result = await RfsCompleteModePipeline.BuildAsync(prompt, repoRoot, 5, intentAgent, proposalAgent);

        var completeConsole = stdout.ToString();
        if (!completeConsole.Contains("model: claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected complete-mode console output to include model: claude-haiku-4.5 but it was missing.");
        }

        if (!string.Equals(intentAgent.Descriptor.ExecutionModel.Model, "claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected intent agent descriptor model 'claude-haiku-4.5' but got '{intentAgent.Descriptor.ExecutionModel.Model}'.");
        }

        if (result.Success != !expectFailure)
        {
            failures.Add($"[{name}] expected Success={!expectFailure} but got {result.Success}.");
        }

        if (expectFailure)
        {
            if (result.ErrorMessage is null || !result.ErrorMessage.Contains(expectedErrorContains ?? string.Empty, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected ErrorMessage to contain '{expectedErrorContains}' but got '{result.ErrorMessage}'.");
            }

            if (!string.IsNullOrWhiteSpace(result.PromptToSend))
            {
                failures.Add($"[{name}] expected PromptToSend to be null/empty on failure.");
            }

            if (!string.IsNullOrWhiteSpace(result.IntentSource))
            {
                failures.Add($"[{name}] expected IntentSource to be empty on failure.");
            }

            if (!string.IsNullOrWhiteSpace(result.ValidationStatus))
            {
                failures.Add($"[{name}] expected ValidationStatus to be empty on failure.");
            }

            if (!string.IsNullOrWhiteSpace(result.ValidatedContextPackJson))
            {
                failures.Add($"[{name}] expected ValidatedContextPackJson to be empty on failure.");
            }

            if (intentTransport.CallCount != 1)
            {
                failures.Add($"[{name}] expected the intent LLM to be called once but got {intentTransport.CallCount} calls.");
            }

            if (proposalTransport.CallCount != 1)
            {
                failures.Add($"[{name}] expected the anchor-selection transport to be called once but got {proposalTransport.CallCount} calls.");
            }

            return;
        }

        if (!string.Equals(result.IntentSource, "pi-intent-inference", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected IntentSource 'pi-intent-inference' but got '{result.IntentSource}'.");
        }

        if (!string.Equals(result.IntentKind, "implement-reset-board", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected IntentKind 'implement-reset-board' but got '{result.IntentKind}'.");
        }

        if (string.IsNullOrWhiteSpace(result.IntentSummary))
        {
            failures.Add($"[{name}] expected IntentSummary to be populated.");
        }

        if (!string.Equals(result.ProposalSource, "pi-trace-slice-proposal", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected ProposalSource 'pi-trace-slice-proposal' but got '{result.ProposalSource}'.");
        }

        if (string.IsNullOrWhiteSpace(result.ValidationStatus))
        {
            failures.Add($"[{name}] expected ValidationStatus to be populated.");
        }

        if (result.SelectedStateIds.Count == 0 || result.SelectedDeltaIds.Count == 0 || result.SelectedAnchorIds.Count == 0)
        {
            failures.Add($"[{name}] expected selected states/deltas/anchors to be populated.");
        }

        if (string.IsNullOrWhiteSpace(result.PromptToSend))
        {
            failures.Add($"[{name}] expected PromptToSend to be populated.");
        }
        else
        {
            var promptToSend = result.PromptToSend;
            var expectedPromptFragments = new[]
            {
                "Output formatting:",
                "Markdown-lite rendering",
                "compact text diagram",
                "Use text diagrams only when they materially improve clarity.",
                "Do not include diagrams for simple factual answers.",
                "At most one diagram unless explicitly requested.",
                "Do not use Mermaid unless the user explicitly asks for Mermaid.",
                "validated ContextPack as the authoritative structural project context",
                "ConversationalMemory only for recent conversational continuity",
                "Do not use ConversationalMemory to override validated structural facts",
                "prefer the validated ContextPack",
            };

            foreach (var fragment in expectedPromptFragments)
            {
                if (!promptToSend.Contains(fragment, StringComparison.Ordinal))
                {
                    failures.Add($"[{name}] expected PromptToSend to contain '{fragment}' but it was missing.");
                }
            }
        }

        if (intentTransport.CallCount != 1)
        {
            failures.Add($"[{name}] expected the intent LLM to be called once but got {intentTransport.CallCount} calls.");
        }

        if (proposalTransport.CallCount != 1)
        {
            failures.Add($"[{name}] expected the anchor-selection transport to be called once but got {proposalTransport.CallCount} calls.");
        }

        if (!string.Equals(proposalTransport.LastModel, "claude-sonnet-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected the anchor-selection transport model 'claude-sonnet-4.5' but got '{proposalTransport.LastModel}'.");
        }

        if (!completeConsole.Contains("  slicing: anchor-guided structural", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected stage output to include anchor-guided structural slicing.");
        }

        if (!completeConsole.Contains("  anchors selected:", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected stage output to include anchors selected.");
        }

        if (!completeConsole.Contains("  expansion:", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected stage output to include expansion.");
        }

        if (!completeConsole.Contains("  fallback:", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected stage output to include fallback.");
        }

        if (!completeConsole.Contains("[3/5] Validating proposal...", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected validation stage output.");
        }

        if (!completeConsole.Contains("  validated selection:", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected validated selection stage detail.");
        }

        if (!string.IsNullOrWhiteSpace(expectedFallbackStrategy) && !completeConsole.Contains($"  fallback: {expectedFallbackStrategy}", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected fallback detail '{expectedFallbackStrategy}' but it was missing.");
        }

        if (string.IsNullOrWhiteSpace(result.MaterializationPolicySummary))
        {
            failures.Add($"[{name}] expected MaterializationPolicySummary to be populated.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        Console.SetOut(originalOut);
    }
}

static async Task RunCompleteModePipelineWithIntentLlmFailureCaseAsync(
    string name,
    string repoRoot,
    string prompt,
    string llmAnswerJson,
    string expectedErrorContains,
    List<string> failures)
{
    try
    {
        var transport = new FakeIntentLlmTransport(success: true, answerJson: llmAnswerJson);
        var intentAgent = new PiIntentInferenceAgent(repoRoot, transport: transport);
        var result = await RfsCompleteModePipeline.BuildAsync(prompt, repoRoot, 5, intentAgent);

        if (!string.Equals(intentAgent.Descriptor.ExecutionModel.Model, "claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected intent agent descriptor model 'claude-haiku-4.5' but got '{intentAgent.Descriptor.ExecutionModel.Model}'.");
        }

        if (result.Success)
        {
            failures.Add($"[{name}] expected Success=false but got true.");
        }

        if (result.ErrorMessage is null || !result.ErrorMessage.Contains(expectedErrorContains, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected ErrorMessage to contain '{expectedErrorContains}' but got '{result.ErrorMessage}'.");
        }

        if (!string.IsNullOrWhiteSpace(result.PromptToSend))
        {
            failures.Add($"[{name}] expected PromptToSend to be null/empty on failure.");
        }

        if (!string.IsNullOrWhiteSpace(result.IntentSource))
        {
            failures.Add($"[{name}] expected IntentSource to be empty on failure.");
        }

        if (transport.CallCount != 1)
        {
            failures.Add($"[{name}] expected the intent LLM to be called once but got {transport.CallCount} calls.");
        }

        if (!string.Equals(transport.LastModel, "claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected the intent transport model 'claude-haiku-4.5' but got '{transport.LastModel}'.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
}

static void RunCompleteModeFailureRendererCase(
    string name,
    string reason,
    List<string> failures)
{
    var originalOut = Console.Out;
    var originalErr = Console.Error;
    using var stdout = new StringWriter();
    using var stderr = new StringWriter();

    try
    {
        Console.SetOut(stdout);
        Console.SetError(stderr);
        RfsTuiRenderer.WriteCompleteFailure(reason);
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
        return;
    }
    finally
    {
        Console.SetOut(originalOut);
        Console.SetError(originalErr);
    }

    var stdoutText = stdout.ToString();
    var stderrText = stderr.ToString();

    if (!stdoutText.Contains("No State/Delta was recorded.", StringComparison.Ordinal))
    {
        failures.Add($"[{name}] expected stdout to contain 'No State/Delta was recorded.' but it was missing.");
    }

    if (stdoutText.Contains("Recorded State + Delta:", StringComparison.Ordinal))
    {
        failures.Add($"[{name}] expected stdout not to contain 'Recorded State + Delta:'.");
    }

    if (!stderrText.Contains("Complete mode failed while inferring intent.", StringComparison.Ordinal))
    {
        failures.Add($"[{name}] expected stderr to contain the intent-failure banner.");
    }

    if (!stderrText.Contains("Reason:", StringComparison.Ordinal))
    {
        failures.Add($"[{name}] expected stderr to contain 'Reason:'.");
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
        failures.Add($"[{name}] threw {ex}");
    }
}

static async Task RunIntentCliLlmCaseAsync(
    string name,
    string prompt,
    string expectedIntent,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-intent-llm-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    var modelFilePath = Path.Combine(tempRoot, "model.txt");
    var scriptPath = Path.Combine(tempRoot, "pi");
    var script = "#!/usr/bin/env bash\n" +
                 "set -euo pipefail\n" +
                 "MODEL=missing\n" +
                 "next=0\n" +
                 "for i in \"$@\"; do\n" +
                 "  if [ \"$next\" = 1 ]; then\n" +
                 "    MODEL=\"$i\"\n" +
                 "    break\n" +
                 "  fi\n" +
                 "  if [ \"$i\" = \"--model\" ]; then\n" +
                 "    next=1\n" +
                 "  fi\n" +
                 "done\n" +
                 $"printf '%s' \"$MODEL\" > \"{modelFilePath}\"\n" +
                 "echo '{\"type\":\"session\"}'\n" +
                 "cat <<EOF\n" +
                 "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"$MODEL\",\"content\":[{\"type\":\"text\",\"text\":\"{\\\"intent\\\":\\\"implement-reset-board\\\",\\\"summary\\\":\\\"Implement the reset board action.\\\",\\\"entities\\\":[\\\"reset board\\\"],\\\"constraints\\\":[]}\"}]}}\n" +
                 "EOF\n" +
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
    try
    {
        Environment.SetEnvironmentVariable("PATH", tempRoot + Path.PathSeparator + (originalPath ?? string.Empty));

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
        startInfo.ArgumentList.Add("--llm");
        startInfo.ArgumentList.Add(prompt);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            failures.Add($"[{name}] failed to start dotnet run for rfs intent --llm.");
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

        var modelEcho = await File.ReadAllTextAsync(modelFilePath);
        if (!string.Equals(modelEcho, "claude-haiku-4.5", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected the pi transport model file to contain 'claude-haiku-4.5' but got '{modelEcho}'.");
        }

        var requiredFragments = new[]
        {
            "{",
            "  \"Intent\": \"" + expectedIntent + "\"",
            "  \"Summary\": \"Implement the reset board action.\"",
            "  \"Entities\": [",
            "    \"reset board\"",
            "  ],",
            "  \"Constraints\": []",
            "}"
        };

        foreach (var fragment in requiredFragments)
        {
            if (!stdout.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
            }
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        Environment.SetEnvironmentVariable("PATH", originalPath);
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static async Task RunPiJsonRunnerWorkspaceModelCaseAsync(
    string name,
    string prompt,
    string expectedModel,
    List<string> failures)
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-pi-json-workspace-model-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    var scriptPath = Path.Combine(tempRoot, "pi");
    var script = "#!/usr/bin/env bash\n" +
                 "set -euo pipefail\n" +
                 "MODEL=missing\n" +
                 "next=0\n" +
                 "for i in \"$@\"; do\n" +
                 "  if [ \"$next\" = 1 ]; then\n" +
                 "    MODEL=\"$i\"\n" +
                 "    break\n" +
                 "  fi\n" +
                 "  if [ \"$i\" = \"--model\" ]; then\n" +
                 "    next=1\n" +
                 "  fi\n" +
                 "done\n" +
                 "echo '{\"type\":\"session\"}'\n" +
                 "cat <<EOF\n" +
                 "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"$MODEL\",\"content\":[{\"type\":\"text\",\"text\":\"structured answer\"}]}}\n" +
                 "EOF\n" +
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
    try
    {
        Environment.SetEnvironmentVariable("PATH", tempRoot + Path.PathSeparator + (originalPath ?? string.Empty));

        var result = await PiJsonEventRunner.RunAskAsync(tempRoot, prompt, expectedModel);

        if (!result.Success)
        {
            failures.Add($"[{name}] expected Success=true but got false. Error: {result.ErrorMessage}");
        }

        if (!string.Equals(result.Provider, "test-provider", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected provider 'test-provider' but got '{result.Provider}'.");
        }

        if (!string.Equals(result.Model, expectedModel, StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected model '{expectedModel}' but got '{result.Model}'.");
        }

        if (!string.Equals(result.Answer, "structured answer", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected answer 'structured answer' but got '{result.Answer}'.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        Environment.SetEnvironmentVariable("PATH", originalPath);
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static async Task RunRckRecordAskCaseAsync(
    string name,
    List<string> failures)
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-rck-record-ask-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        if (!await InitializeTempGitRepoAndRckAsync(name, tempRoot, failures))
        {
            return;
        }

        var statusBefore = RckWorkspaceStatusReader.Read(tempRoot);
        const string prompt = "Summarize the reset board change.";
        const string answer = "The reset board action clears the ChessBoardApp state safely.";
        var result = RckInteractionRecorder.RecordAsk(prompt, answer, tempRoot);
        if (!AssertRecorderResult(name, result, failures))
        {
            return;
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        AssertRckCountDeltas(name, statusBefore, statusAfter, expectedStateDelta: 1, expectedDeltaDelta: 1, expectedAnchorDelta: 0, failures);
        AssertHeadUpdated(name, result, failures);

        var statePayload = ReadStatePayload(result);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "mode", "ask", failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "prompt", prompt, failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "answer", answer, failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "answerSummary", answer, failures);

        var deltaPayload = ReadFirstDeltaOperationPayload(result);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "type", "llm-interaction", failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "mode", "ask", failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "prompt", prompt, failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "answer", answer, failures);
        AssertJsonArrayLength(name, deltaPayload.GetProperty("evidence"), "tools", 0, failures);
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        TryDeleteDirectory(tempRoot);
    }
}

static async Task RunRckRecordAgentCaseAsync(
    string name,
    List<string> failures)
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-rck-record-agent-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        if (!await InitializeTempGitRepoAndRckAsync(name, tempRoot, failures))
        {
            return;
        }

        var statusBefore = RckWorkspaceStatusReader.Read(tempRoot);
        const string task = "Inspect the repo read-only.";
        const string answer = "The agent inspected README.md and Program.cs without modifying files.";
        var tools = new[]
        {
            RckInteractionTool.Completed("read"),
            RckInteractionTool.Completed("grep"),
        };

        var result = RckInteractionRecorder.RecordAgent(task, answer, tools, tempRoot);
        if (!AssertRecorderResult(name, result, failures))
        {
            return;
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        AssertRckCountDeltas(name, statusBefore, statusAfter, expectedStateDelta: 1, expectedDeltaDelta: 1, expectedAnchorDelta: 0, failures);
        AssertHeadUpdated(name, result, failures);

        var statePayload = ReadStatePayload(result);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "mode", "agent", failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "prompt", task, failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "answer", answer, failures);
        AssertJsonString(name, statePayload.GetProperty("interaction"), "answerSummary", answer, failures);

        var deltaPayload = ReadFirstDeltaOperationPayload(result);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "type", "llm-interaction", failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "mode", "agent", failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "prompt", task, failures);
        AssertJsonString(name, deltaPayload.GetProperty("cause"), "answer", answer, failures);
        AssertJsonArrayLength(name, deltaPayload.GetProperty("evidence"), "tools", 2, failures);

        var toolElements = deltaPayload.GetProperty("evidence").GetProperty("tools").EnumerateArray().ToArray();
        AssertJsonString(name, toolElements[0], "name", "read", failures);
        AssertJsonString(name, toolElements[0], "status", "completed", failures);
        AssertJsonString(name, toolElements[1], "name", "grep", failures);
        AssertJsonString(name, toolElements[1], "status", "completed", failures);
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
    }
    finally
    {
        TryDeleteDirectory(tempRoot);
    }
}

static async Task<bool> InitializeTempGitRepoAndRckAsync(
    string name,
    string tempRoot,
    List<string> failures)
{
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

static bool AssertRecorderResult(
    string name,
    RckInteractionRecordResult result,
    List<string> failures)
{
    if (!result.Success)
    {
        failures.Add($"[{name}] expected recorder success but got: {result.ErrorMessage}");
        return false;
    }

    if (!result.StateCreated || !result.DeltaCreated || !result.HeadUpdated)
    {
        failures.Add($"[{name}] expected StateCreated/DeltaCreated/HeadUpdated to be true but got state={result.StateCreated}, delta={result.DeltaCreated}, head={result.HeadUpdated}.");
    }

    if (result.AnchorCreated || result.AnchorId is not null || result.AnchorLabel is not null)
    {
        failures.Add($"[{name}] expected no anchor creation but got AnchorCreated={result.AnchorCreated}, AnchorId={result.AnchorId}, AnchorLabel={result.AnchorLabel}.");
    }

    if (result.Paths is null || result.StateId is null || result.DeltaId is null)
    {
        failures.Add($"[{name}] expected recorder result to include paths/state/delta ids.");
        return false;
    }

    return true;
}

static void AssertRckCountDeltas(
    string name,
    RckWorkspaceStatus statusBefore,
    RckWorkspaceStatus statusAfter,
    int expectedStateDelta,
    int expectedDeltaDelta,
    int expectedAnchorDelta,
    List<string> failures)
{
    if (statusAfter.StateCount - statusBefore.StateCount != expectedStateDelta ||
        statusAfter.DeltaCount - statusBefore.DeltaCount != expectedDeltaDelta ||
        statusAfter.AnchorCount - statusBefore.AnchorCount != expectedAnchorDelta)
    {
        failures.Add($"[{name}] expected RCK count deltas state +{expectedStateDelta}, delta +{expectedDeltaDelta}, anchor +{expectedAnchorDelta} but got state {statusBefore.StateCount}->{statusAfter.StateCount}, delta {statusBefore.DeltaCount}->{statusAfter.DeltaCount}, anchor {statusBefore.AnchorCount}->{statusAfter.AnchorCount}.");
    }
}

static void AssertHeadUpdated(
    string name,
    RckInteractionRecordResult result,
    List<string> failures)
{
    var head = File.ReadAllText(result.Paths!.HeadPath).Trim();
    if (!string.Equals(head, result.StateId!.ToString(), StringComparison.Ordinal))
    {
        failures.Add($"[{name}] expected HEAD to be '{result.StateId}' but got '{head}'.");
    }
}

static JsonElement ReadStatePayload(RckInteractionRecordResult result)
{
    var statePath = Path.Combine(result.Paths!.StatesDirectory, $"{result.StateId}.json");
    using var stateDocument = JsonDocument.Parse(File.ReadAllText(statePath));
    var payloadJson = stateDocument.RootElement.GetProperty("payloadCanonicalJson").GetString() ?? throw new InvalidDataException("state payloadCanonicalJson missing");
    using var payloadDocument = JsonDocument.Parse(payloadJson);
    return payloadDocument.RootElement.Clone();
}

static JsonElement ReadFirstDeltaOperationPayload(RckInteractionRecordResult result)
{
    var deltaPath = Path.Combine(result.Paths!.DeltasDirectory, $"{result.DeltaId}.json");
    using var deltaDocument = JsonDocument.Parse(File.ReadAllText(deltaPath));
    var valueJson = deltaDocument.RootElement.GetProperty("ops")[0].GetProperty("valueJson").GetString() ?? throw new InvalidDataException("delta op valueJson missing");
    using var payloadDocument = JsonDocument.Parse(valueJson);
    return payloadDocument.RootElement.Clone();
}

static void AssertJsonString(
    string name,
    JsonElement element,
    string propertyName,
    string expected,
    List<string> failures)
{
    if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
    {
        failures.Add($"[{name}] expected JSON property '{propertyName}' to be a string.");
        return;
    }

    var actual = property.GetString();
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
    {
        failures.Add($"[{name}] expected JSON property '{propertyName}' to be '{expected}' but got '{actual}'.");
    }
}

static void AssertJsonArrayLength(
    string name,
    JsonElement element,
    string propertyName,
    int expectedLength,
    List<string> failures)
{
    if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
    {
        failures.Add($"[{name}] expected JSON property '{propertyName}' to be an array.");
        return;
    }

    var actualLength = property.GetArrayLength();
    if (actualLength != expectedLength)
    {
        failures.Add($"[{name}] expected JSON array '{propertyName}' length {expectedLength} but got {actualLength}.");
    }
}

static void TryDeleteDirectory(string path)
{
    try
    {
        Directory.Delete(path, recursive: true);
    }
    catch
    {
    }
}

static async Task RunPiJsonRunnerRuntimeEventReportingCaseAsync(List<string> failures)
{
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-pi-json-runtime-reporting-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    var scriptPath = Path.Combine(tempRoot, "pi");
    var script = "#!/usr/bin/env bash\n" +
                 "set -euo pipefail\n" +
                 "echo '{\"type\":\"session\"}'\n" +
                 "echo '{\"type\":\"message_start\"}'\n" +
                 "echo '{\"type\":\"message_update\",\"assistantMessageEvent\":{\"type\":\"text_delta\",\"delta\":\"hello \"}}'\n" +
                 "echo '{\"type\":\"tool_execution_start\",\"id\":\"tool-1\",\"name\":\"read\",\"details\":\"README.md\"}'\n" +
                 "echo '{\"type\":\"tool_execution_end\",\"id\":\"tool-1\",\"name\":\"read\",\"summary\":\"ok\"}'\n" +
                 "echo '{\"type\":\"message_update\",\"assistantMessageEvent\":{\"type\":\"text_delta\",\"delta\":\"world\"}}'\n" +
                 "echo '{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":\"structured answer\"}]}}'\n" +
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
    try
    {
        Environment.SetEnvironmentVariable("PATH", tempRoot + Path.PathSeparator + (originalPath ?? string.Empty));

        var runtimeEvents = new List<PiJsonStreamEvent>();
        var result = await PiJsonEventRunner.RunAgentDetailedAsync(
            tempRoot,
            "test prompt",
            null,
            eventReporter: runtimeEvents.Add);

        if (!result.Success)
        {
            failures.Add($"[pi runtime event reporting] expected Success=true but got false. Error: {result.ErrorMessage}");
        }

        if (!string.Equals(result.Answer, "structured answer", StringComparison.Ordinal))
        {
            failures.Add($"[pi runtime event reporting] expected answer 'structured answer' but got '{result.Answer}'.");
        }

        var eventTypes = runtimeEvents.Select(runtimeEvent => runtimeEvent.Type).ToArray();
        foreach (var expectedType in new[] { "session", "message_start", "message_update", "tool_execution_start", "tool_execution_end", "message_end" })
        {
            if (!eventTypes.Contains(expectedType, StringComparer.Ordinal))
            {
                failures.Add($"[pi runtime event reporting] missing runtime event type '{expectedType}'.");
            }
        }

        var firstDelta = runtimeEvents.FirstOrDefault(runtimeEvent => string.Equals(runtimeEvent.Type, "message_update", StringComparison.Ordinal));
        if (firstDelta is null || !string.Equals(firstDelta.Text, "hello ", StringComparison.Ordinal))
        {
            failures.Add($"[pi runtime event reporting] expected first message_update text 'hello ' but got '{firstDelta?.Text ?? "(null)"}'.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[pi runtime event reporting] threw {ex}");
    }
    finally
    {
        Environment.SetEnvironmentVariable("PATH", originalPath);

        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}

static void RunRfsTuiModeSelectionParserCases(List<string> failures)
{
    var cases = new (string Input, RfsTuiModeSelection Expected)[]
    {
        ("1", RfsTuiModeSelection.Direct),
        ("2", RfsTuiModeSelection.Simple),
        ("3", RfsTuiModeSelection.Complete),
        ("4", RfsTuiModeSelection.Plan),
        ("/cancel", RfsTuiModeSelection.Cancel),
        ("cancel", RfsTuiModeSelection.Cancel),
        ("/exit", RfsTuiModeSelection.Exit),
        ("x", RfsTuiModeSelection.Invalid),
    };

    foreach (var testCase in cases)
    {
        var actual = RfsTuiModeSelectionParser.ParseModeSelection(testCase.Input);
        if (actual != testCase.Expected)
        {
            failures.Add($"[tui mode parser] input '{testCase.Input}' expected {testCase.Expected} but got {actual}.");
        }
    }
}

static void RunRfsTuiCommandSuggestionCases(List<string> failures)
{
    var cases = new[]
    {
        new
        {
            Input = "/he",
            Expected = new[]
            {
                (Usage: "/help", Description: "Show this help"),
                (Usage: "/hermes draft", Description: "Build Hermes handoff draft"),
                (Usage: "/hermes run", Description: "Execute Hermes once with guardrails"),
            },
        },
        new
        {
            Input = "/pi",
            Expected = new[]
            {
                (Usage: "/pi run", Description: "Execute Pi using JSON Event Stream"),
            },
        },
        new
        {
            Input = "/mo",
            Expected = new[]
            {
                (Usage: "/model", Description: "Open session model picker"),
                (Usage: "/model <model>", Description: "Set session model (temporary)"),
            },
        },
        new
        {
            Input = "/con",
            Expected = new[]
            {
                (Usage: "/context", Description: "Show last context summary"),
            },
        },
        new
        {
            Input = "/tr",
            Expected = new[]
            {
                (Usage: "/trace", Description: "Show last TraceSlice summary"),
            },
        },
        new
        {
            Input = "/x",
            Expected = Array.Empty<(string Usage, string Description)>(),
        },
        new
        {
            Input = "hola",
            Expected = Array.Empty<(string Usage, string Description)>(),
        },
    };

    foreach (var testCase in cases)
    {
        var suggestions = RfsTuiCommandCatalog.GetSuggestions(testCase.Input).ToArray();
        if (suggestions.Length != testCase.Expected.Length)
        {
            failures.Add($"[tui command suggestions] input '{testCase.Input}' expected {testCase.Expected.Length} suggestions but got {suggestions.Length}.");
            continue;
        }

        for (var index = 0; index < testCase.Expected.Length; index++)
        {
            var expected = testCase.Expected[index];
            var actual = suggestions[index];
            if (!string.Equals(actual.Usage, expected.Usage, StringComparison.Ordinal))
            {
                failures.Add($"[tui command suggestions] input '{testCase.Input}' suggestion {index} expected usage '{expected.Usage}' but got '{actual.Usage}'.");
            }

            if (!string.Equals(actual.Description, expected.Description, StringComparison.Ordinal))
            {
                failures.Add($"[tui command suggestions] input '{testCase.Input}' suggestion {index} expected description '{expected.Description}' but got '{actual.Description}'.");
            }
        }
    }

    var paletteEligibilityCases = new[]
    {
        (Input: string.Empty, Expected: false),
        (Input: "hola", Expected: false),
        (Input: "hello/", Expected: false),
        (Input: "/", Expected: true),
        (Input: "/he", Expected: true),
    };

    foreach (var testCase in paletteEligibilityCases)
    {
        var actual = RfsTuiInputReader.ShouldUseCommandPalette(testCase.Input);
        if (actual != testCase.Expected)
        {
            failures.Add($"[tui command suggestions] expected palette eligibility for '{testCase.Input}' to be {testCase.Expected} but got {actual}.");
        }
    }

    var exactModel = RfsTuiCommandCatalog.FindExactMatch("/model")?.Usage;
    if (!string.Equals(exactModel, "/model", StringComparison.Ordinal))
    {
        failures.Add($"[tui command suggestions] expected exact '/model' to resolve to '/model' but got '{exactModel ?? "(null)"}'.");
    }

    var exactHelp = RfsTuiCommandCatalog.FindExactMatch("/help")?.Usage;
    if (!string.Equals(exactHelp, "/help", StringComparison.Ordinal))
    {
        failures.Add($"[tui command suggestions] expected exact '/help' to resolve to '/help' but got '{exactHelp ?? "(null)"}'.");
    }

    var helpCommands = RfsTuiCommandCatalog.GetHelpCommands().ToArray();
    var requiredHelpUsages = new[] { "/help", "/model", "/model <model>", "/context" };
    foreach (var usage in requiredHelpUsages)
    {
        if (!helpCommands.Any(command => string.Equals(command.Usage, usage, StringComparison.Ordinal)))
        {
            failures.Add($"[tui command suggestions] expected help catalog to include '{usage}'.");
        }
    }

    var originalIn = Console.In;
    var originalOut = Console.Out;
    try
    {
        using var redirectedInput = new StringReader("/help\n");
        using var redirectedOutput = new StringWriter();
        Console.SetIn(redirectedInput);
        Console.SetOut(redirectedOutput);

        var line = RfsTuiInputReader.ReadLine();
        if (!string.Equals(line, "/help", StringComparison.Ordinal))
        {
            failures.Add($"[tui command suggestions] expected redirected input fallback to return '/help' but got '{line ?? "(null)"}'.");
        }

        if (!string.IsNullOrWhiteSpace(redirectedOutput.ToString()))
        {
            failures.Add("[tui command suggestions] expected redirected input fallback to stay quiet when input/output are redirected.");
        }
    }
    finally
    {
        Console.SetIn(originalIn);
        Console.SetOut(originalOut);
    }
}

static async Task RunRfsTuiCommandSuggestionSessionCaseAsync(string name, List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-command-suggestions-checks", Guid.NewGuid().ToString("N"));
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
        var tuiResult = await RunProcessAsyncWithInput(tempRoot, "/he\n/mo\n/con\n/tr\n/xyz\n/help\n/exit\n", "dotnet", "run", "--project", cliProjectPath, "--");
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
            "Did you mean?",
            "/help",
            "/model",
            "/model <model>",
            "/context",
            "/trace",
            "/hermes draft",
            "/hermes run",
            "Unknown command: /xyz",
            "Type /help to show available commands.",
            "Commands:",
            "/anchor \"name\"",
            "/status",
        };

        foreach (var fragment in requiredFragments)
        {
            if (!tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
            }
        }

        var forbiddenFragments = new[]
        {
            "Write a prompt, then choose:",
            "  1 Direct",
            "  2 Simple",
            "  3 Complete",
            "  4 Plan",
            "Building lightweight context...",
            "Building governed context...",
            "Asking main LLM without RCK context...",
            "Recorded State + Delta:",
            "Respuesta:",
        };

        foreach (var fragment in forbiddenFragments)
        {
            if (tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected stdout not to contain '{fragment}'.");
            }
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        if (statusAfter.StateCount != statusBefore.StateCount || statusAfter.DeltaCount != statusBefore.DeltaCount || statusAfter.AnchorCount != statusBefore.AnchorCount)
        {
            failures.Add($"[{name}] expected slash command suggestions to leave RCK counts unchanged.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
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

static async Task RunRfsTuiPromptModeSelectionSessionCaseAsync(
    string name,
    string prompt,
    string input,
    string[] expectedFragments,
    bool expectPromptEcho,
    string[]? forbiddenFragments = null,
    int expectedStateCountDelta = 0,
    int expectedDeltaCountDelta = 0,
    int expectedAnchorCountDelta = 0,
    List<string>? failures = null)
{
    failures ??= new List<string>();
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-mode-selection-checks", Guid.NewGuid().ToString("N"));
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
        var tuiResult = await RunProcessAsyncWithInput(tempRoot, input, "dotnet", "run", "--project", cliProjectPath, "--");
        if (tuiResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {tuiResult.ExitCode}. stderr: {tuiResult.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(tuiResult.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {tuiResult.Stderr.Trim()}.");
        }

        foreach (var fragment in expectedFragments)
        {
            if (!tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
            }
        }

        if (forbiddenFragments is not null)
        {
            foreach (var fragment in forbiddenFragments)
            {
                if (tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
                {
                    failures.Add($"[{name}] expected stdout to not contain '{fragment}' but it was present.");
                }
            }
        }

        if (expectPromptEcho && !tuiResult.Stdout.Contains($"  {prompt}", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected stdout to echo the prompt text but it was missing.");
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        if (statusAfter.StateCount - statusBefore.StateCount != expectedStateCountDelta ||
            statusAfter.DeltaCount - statusBefore.DeltaCount != expectedDeltaCountDelta ||
            statusAfter.AnchorCount - statusBefore.AnchorCount != expectedAnchorCountDelta)
        {
            failures.Add($"[{name}] expected RCK count deltas to be state +{expectedStateCountDelta}, delta +{expectedDeltaCountDelta}, anchor +{expectedAnchorCountDelta} but got state {statusBefore.StateCount}->{statusAfter.StateCount}, delta {statusBefore.DeltaCount}->{statusAfter.DeltaCount}, anchor {statusBefore.AnchorCount}->{statusAfter.AnchorCount}.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
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
        var sessions = new[]
        {
            new
            {
                Input = "/status\n",
                Required = new[] { "RFS · ", "RCK:", "Git:", "Model:", "Session:" },
                Forbidden = new[] { "Workspace not initialized.", "Write a prompt, then choose:" }
            },
            new
            {
                Input = "/help\n",
                Required = new[] { "Commands:", "/status", "/anchor \"name\"", "/model <model>", "/exit" },
                Forbidden = new[] { "Workspace not initialized." }
            },
            new
            {
                Input = "/log\n",
                Required = new[] { "Recent interactions:", "genesis" },
                Forbidden = new[] { "Workspace not initialized." }
            },
            new
            {
                Input = "/context\n",
                Required = new[] { "No context has been built yet." },
                Forbidden = new[] { "Workspace not initialized." }
            },
            new
            {
                Input = "/trace\n",
                Required = new[] { "No TraceSlice has been built in this session yet." },
                Forbidden = new[] { "Workspace not initialized." }
            },
            new
            {
                Input = "/xyz\n",
                Required = new[] { "Unknown command: /xyz", "Type /help to show available commands." },
                Forbidden = new[] { "Workspace not initialized.", "Write a prompt, then choose:" }
            },
        };

        foreach (var session in sessions)
        {
            var tuiResult = await RunProcessAsyncWithInput(tempRoot, session.Input, "dotnet", "run", "--project", cliProjectPath, "--");
            if (tuiResult.ExitCode != 0)
            {
                failures.Add($"[{name}] expected exit code 0 but got {tuiResult.ExitCode}. stderr: {tuiResult.Stderr}");
                return;
            }

            if (!string.IsNullOrWhiteSpace(tuiResult.Stderr))
            {
                failures.Add($"[{name}] expected no stderr but got: {tuiResult.Stderr.Trim()}.");
            }

            foreach (var fragment in session.Required)
            {
                if (!tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
                {
                    failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
                }
            }

            foreach (var fragment in session.Forbidden)
            {
                if (tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
                {
                    failures.Add($"[{name}] expected stdout not to contain '{fragment}' but it was present.");
                }
            }
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        if (statusAfter.StateCount != statusBefore.StateCount || statusAfter.DeltaCount != statusBefore.DeltaCount || statusAfter.AnchorCount != statusBefore.AnchorCount)
        {
            failures.Add($"[{name}] expected bare session to leave RCK counts unchanged.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
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

static async Task RunRfsTuiInternalCommandsPolishSessionCaseAsync(string name, List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-internal-commands-checks", Guid.NewGuid().ToString("N"));
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
        var configBefore = RckWorkspaceModelConfigStore.Read(tempRoot);
        var tuiResult = await RunProcessAsyncWithInput(tempRoot, "/status\n/log\n/model\n/context\n/trace\n/model gpt-5.4-mini\n/model\n/help\n/exit\n", "dotnet", "run", "--project", cliProjectPath, "--");
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
            "RFS ·",
            "RCK:",
            "states/deltas/anchors:",
            "Git:",
            "Model:",
            "Session:",
            "Recent interactions:",
            "No context has been built yet.",
            "No TraceSlice has been built in this session yet.",
            "Session model updated:",
            "Current model:",
            "Commands:",
        };

        foreach (var fragment in requiredFragments)
        {
            if (!tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
            }
        }

        if (!tuiResult.Stdout.Contains("Source:", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected /model and /status output to include model source.");
        }

        if (!tuiResult.Stdout.Contains("current model:", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"[{name}] expected /status or /model output to include current model.");
        }

        var forbiddenFragments = new[]
        {
            "Write a prompt, then choose:",
            "  1 Direct",
            "  2 Simple",
            "  3 Complete",
            "  4 Plan",
        };

        foreach (var fragment in forbiddenFragments)
        {
            if (tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected informational slash commands to bypass the mode-selection menu, but found '{fragment}'.");
            }
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        if (statusAfter.StateCount != statusBefore.StateCount || statusAfter.DeltaCount != statusBefore.DeltaCount || statusAfter.AnchorCount != statusBefore.AnchorCount)
        {
            failures.Add($"[{name}] expected informational commands to leave RCK counts unchanged.");
        }

        var configAfter = RckWorkspaceModelConfigStore.Read(tempRoot);
        if (!configAfter.Success || configAfter.HasConfiguredDefaultModel)
        {
            failures.Add($"[{name}] expected /model gpt-5.4-mini to leave the workspace default model unmodified.");
        }

        if (configBefore.HasConfiguredDefaultModel)
        {
            failures.Add($"[{name}] expected the workspace to start without a configured default model.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
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
        failures.Add($"[{name}] threw {ex}");
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

static async Task RunRckTuiDirectRecordingCaseAsync(string name, List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-direct-recording-checks", Guid.NewGuid().ToString("N"));
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
        var recordResult = RckInteractionRecorder.RecordTui(
            new RckTuiInteractionRecordInput(
                "Respond with one short sentence confirming TUI direct mode works.",
                "Direct mode works.",
                provider: "test-provider",
                model: "test-model"),
            tempRoot);

        if (!recordResult.Success)
        {
            failures.Add($"[{name}] expected RecordTui to succeed but got error: {recordResult.ErrorMessage}");
            return;
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        if (statusAfter.StateCount != statusBefore.StateCount + 1)
        {
            failures.Add($"[{name}] expected state count to increase by 1 but changed from {statusBefore.StateCount} to {statusAfter.StateCount}.");
        }

        if (statusAfter.DeltaCount != statusBefore.DeltaCount + 1)
        {
            failures.Add($"[{name}] expected delta count to increase by 1 but changed from {statusBefore.DeltaCount} to {statusAfter.DeltaCount}.");
        }

        var headPath = Path.Combine(tempRoot, ".rfs", "rck", "HEAD");
        var headText = File.ReadAllText(headPath).Trim();
        if (!string.Equals(headText, recordResult.StateId?.ToString(), StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected HEAD to match the recorded state id but found '{headText}' and '{recordResult.StateId}'.");
        }

        var statePath = Path.Combine(tempRoot, ".rfs", "rck", "states", $"{recordResult.StateId}.json");
        var stateJson = File.ReadAllText(statePath);
        using var stateDocument = JsonDocument.Parse(stateJson);
        var stateRoot = stateDocument.RootElement;
        var payloadJson = stateRoot.GetProperty("payloadCanonicalJson").GetString() ?? string.Empty;
        using var payloadDocument = JsonDocument.Parse(payloadJson);
        var payloadRoot = payloadDocument.RootElement;
        var interaction = payloadRoot.GetProperty("interaction");

        AssertStringEqual(name, failures, "interaction.type", "rufus.interaction-state", payloadRoot.GetProperty("type").GetString());
        AssertStringEqual(name, failures, "interaction.mode", "tui-direct", interaction.GetProperty("mode").GetString());
        AssertStringEqual(name, failures, "interaction.prompt", "Respond with one short sentence confirming TUI direct mode works.", interaction.GetProperty("prompt").GetString());
        AssertStringEqual(name, failures, "interaction.answer", "Direct mode works.", interaction.GetProperty("answer").GetString());
        AssertStringEqual(name, failures, "interaction.answerSummary", "Direct mode works.", interaction.GetProperty("answerSummary").GetString());
        AssertStringEqual(name, failures, "interaction.pipelineSummary.kind", "direct", interaction.GetProperty("pipelineSummary").GetProperty("kind").GetString());
        AssertBooleanEqual(name, failures, "interaction.pipelineSummary.usesRckContext", false, interaction.GetProperty("pipelineSummary").GetProperty("usesRckContext").GetBoolean());
        AssertBooleanEqual(name, failures, "interaction.pipelineSummary.usesTraceSlice", false, interaction.GetProperty("pipelineSummary").GetProperty("usesTraceSlice").GetBoolean());
        AssertBooleanEqual(name, failures, "interaction.pipelineSummary.usesContextPack", false, interaction.GetProperty("pipelineSummary").GetProperty("usesContextPack").GetBoolean());

        if (!interaction.TryGetProperty("provider", out var providerElement) || providerElement.GetString() != "test-provider")
        {
            failures.Add($"[{name}] expected interaction.provider to be 'test-provider'.");
        }

        if (!interaction.TryGetProperty("model", out var modelElement) || modelElement.GetString() != "test-model")
        {
            failures.Add($"[{name}] expected interaction.model to be 'test-model'.");
        }

        var deltaPath = Path.Combine(tempRoot, ".rfs", "rck", "deltas", $"{recordResult.DeltaId}.json");
        var deltaJson = File.ReadAllText(deltaPath);
        using var deltaDocument = JsonDocument.Parse(deltaJson);
        var deltaRoot = deltaDocument.RootElement;
        var ops = deltaRoot.GetProperty("ops");
        var firstOp = ops[0];
        var valueJson = firstOp.GetProperty("valueJson").GetString() ?? string.Empty;
        using var deltaPayloadDocument = JsonDocument.Parse(valueJson);
        var deltaPayloadRoot = deltaPayloadDocument.RootElement;
        var cause = deltaPayloadRoot.GetProperty("cause");
        AssertStringEqual(name, failures, "delta.cause.mode", "tui-direct", cause.GetProperty("mode").GetString());
        AssertStringEqual(name, failures, "delta.cause.pipelineSummary.kind", "direct", cause.GetProperty("pipelineSummary").GetProperty("kind").GetString());
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
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

static async Task RunRckTuiCommitBoundaryAnchorCaseAsync(string name, List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-commit-boundary-anchor-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        var configNameResult = await RunProcessAsync(tempRoot, "git", "config", "user.name", "Rufus Test");
        var configEmailResult = await RunProcessAsync(tempRoot, "git", "config", "user.email", "rufus@test.local");
        if (configNameResult.ExitCode != 0 || configEmailResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to configure git identity for the temp repo.");
            return;
        }

        var seedPath = Path.Combine(tempRoot, "README.md");
        await File.WriteAllTextAsync(seedPath, "seed\n");
        var addResult = await RunProcessAsync(tempRoot, "git", "add", "README.md");
        if (addResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to stage the seed file: {addResult.Stderr}");
            return;
        }

        var commitResult = await RunProcessAsync(tempRoot, "git", "commit", "-m", "seed commit");
        if (commitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to create the seed commit: {commitResult.Stderr}");
            return;
        }

        var initResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj"), "--", "init");
        if (initResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected rfs init to succeed but got exit code {initResult.ExitCode}. stderr: {initResult.Stderr}");
            return;
        }

        var statusBefore = RckWorkspaceStatusReader.Read(tempRoot);
        var firstRecord = RckInteractionRecorder.RecordTui(
            new RckTuiInteractionRecordInput(
                "Record the first interaction.",
                "First interaction recorded.",
                provider: "test-provider",
                model: "test-model"),
            tempRoot);

        if (!firstRecord.Success)
        {
            failures.Add($"[{name}] expected first RecordTui to succeed but got error: {firstRecord.ErrorMessage}");
            return;
        }

        var statusAfterFirst = RckWorkspaceStatusReader.Read(tempRoot);
        if (statusAfterFirst.StateCount != statusBefore.StateCount + 1 ||
            statusAfterFirst.DeltaCount != statusBefore.DeltaCount + 1 ||
            statusAfterFirst.AnchorCount != statusBefore.AnchorCount)
        {
            failures.Add($"[{name}] expected first record to add only state+delta, not anchor.");
        }

        await File.AppendAllTextAsync(seedPath, "second change\n");
        var addSecondResult = await RunProcessAsync(tempRoot, "git", "add", "README.md");
        if (addSecondResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to stage the second change: {addSecondResult.Stderr}");
            return;
        }

        var commitSecondResult = await RunProcessAsync(tempRoot, "git", "commit", "-m", "second commit");
        if (commitSecondResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to create the second commit: {commitSecondResult.Stderr}");
            return;
        }

        var secondRecord = RckInteractionRecorder.RecordTui(
            new RckTuiInteractionRecordInput(
                "Record the second interaction after a commit boundary.",
                "Second interaction recorded.",
                provider: "test-provider",
                model: "test-model"),
            tempRoot);

        if (!secondRecord.Success)
        {
            failures.Add($"[{name}] expected second RecordTui to succeed but got error: {secondRecord.ErrorMessage}");
            return;
        }

        if (!secondRecord.AnchorCreated)
        {
            failures.Add($"[{name}] expected commit-boundary anchor to be created on the second record.");
        }

        var statusAfterSecond = RckWorkspaceStatusReader.Read(tempRoot);
        if (statusAfterSecond.StateCount != statusBefore.StateCount + 2 ||
            statusAfterSecond.DeltaCount != statusBefore.DeltaCount + 2 ||
            statusAfterSecond.AnchorCount != statusBefore.AnchorCount + 1)
        {
            failures.Add($"[{name}] expected commit-boundary record to add a single anchor on top of the state/delta pair.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
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

static async Task RunRfsTuiAnchorUsageSessionCaseAsync(string name, List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-anchor-usage-checks", Guid.NewGuid().ToString("N"));
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
        var tuiResult = await RunProcessAsyncWithInput(tempRoot, "/anchor\n", "dotnet", "run", "--project", cliProjectPath, "--");
        if (tuiResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {tuiResult.ExitCode}. stderr: {tuiResult.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(tuiResult.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {tuiResult.Stderr.Trim()}.");
        }

        if (!tuiResult.Stdout.Contains("Usage:", StringComparison.Ordinal) ||
            !tuiResult.Stdout.Contains("/anchor \"milestone-name\"", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected /anchor without a name to print usage.");
        }

        if (tuiResult.Stdout.Contains("Anchor created:", StringComparison.Ordinal) ||
            tuiResult.Stdout.Contains("Respuesta:", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected /anchor without a name to avoid LLM or anchor creation output.");
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        if (statusAfter.StateCount != statusBefore.StateCount ||
            statusAfter.DeltaCount != statusBefore.DeltaCount ||
            statusAfter.AnchorCount != statusBefore.AnchorCount)
        {
            failures.Add($"[{name}] expected /anchor without a name to leave RCK counts unchanged.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
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

static async Task RunRfsTuiAnchorCommandSessionCaseAsync(string name, List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-anchor-command-checks", Guid.NewGuid().ToString("N"));
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
        var headPath = Path.Combine(tempRoot, ".rfs", "rck", "HEAD");
        var headBefore = await File.ReadAllTextAsync(headPath);
        var tuiResult = await RunProcessAsyncWithInput(tempRoot, "/anchor manual-test-anchor\n", "dotnet", "run", "--project", cliProjectPath, "--");
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
            "Anchor created:",
            "name: manual-test-anchor",
            "state:",
            "id:",
        };

        foreach (var fragment in requiredFragments)
        {
            if (!tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
            }
        }

        if (tuiResult.Stdout.Contains("Respuesta:", StringComparison.Ordinal) ||
            tuiResult.Stdout.Contains("¿Cómo querés procesarlo?", StringComparison.Ordinal) ||
            tuiResult.Stdout.Contains("Write a prompt, then choose:", StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected /anchor to bypass the main LLM pipeline.");
        }

        var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
        if (statusAfter.StateCount != statusBefore.StateCount ||
            statusAfter.DeltaCount != statusBefore.DeltaCount ||
            statusAfter.AnchorCount != statusBefore.AnchorCount + 1)
        {
            failures.Add($"[{name}] expected /anchor to add exactly one anchor without changing HEAD/state/delta counts.");
        }

        var headAfter = await File.ReadAllTextAsync(headPath);
        if (!string.Equals(headBefore.Trim(), headAfter.Trim(), StringComparison.Ordinal))
        {
            failures.Add($"[{name}] expected /anchor to leave HEAD unchanged.");
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
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

static async Task RunRfsTuiSimpleModeRecordingSessionCaseAsync(string name, string prompt, string input, List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-simple-recording-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        var configNameResult = await RunProcessAsync(tempRoot, "git", "config", "user.name", "Rufus Test");
        var configEmailResult = await RunProcessAsync(tempRoot, "git", "config", "user.email", "rufus@test.local");
        if (configNameResult.ExitCode != 0 || configEmailResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to configure git identity for the temp repo.");
            return;
        }

        var seedPath = Path.Combine(tempRoot, "README.md");
        await File.WriteAllTextAsync(seedPath, "seed\n");
        var addResult = await RunProcessAsync(tempRoot, "git", "add", "README.md");
        if (addResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to stage the seed file: {addResult.Stderr}");
            return;
        }

        var commitResult = await RunProcessAsync(tempRoot, "git", "commit", "-m", "seed commit");
        if (commitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to create the seed commit: {commitResult.Stderr}");
            return;
        }

        var initResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "init");
        if (initResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected rfs init to succeed but got exit code {initResult.ExitCode}. stderr: {initResult.Stderr}");
            return;
        }

        var scriptPath = Path.Combine(tempRoot, "pi");
        var script = "#!/usr/bin/env bash\n" +
                     "set -euo pipefail\n" +
                     "cat <<EOF\n" +
                     "{\"type\":\"session\"}\n" +
                     "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":\"Simple mode works.\"}]}}\n" +
                     "EOF\n";
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
        try
        {
            Environment.SetEnvironmentVariable("PATH", tempRoot + Path.PathSeparator + (originalPath ?? string.Empty));

            var statusBefore = RckWorkspaceStatusReader.Read(tempRoot);
            var tuiResult = await RunProcessAsyncWithInput(tempRoot, input, "dotnet", "run", "--project", cliProjectPath, "--");
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
                "[Simple]",
                "Building lightweight context...",
                "Context:",
                "recent interactions:",
                "anchors:",
                "artifacts:",
                "estimated tokens:",
                "transport risk:",
                "truncated:",
                "Respuesta:",
                "Recorded State + Delta:",
            };

            foreach (var fragment in requiredFragments)
            {
                if (!tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
                {
                    failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
                }
            }

            if (tuiResult.Stdout.Contains("Mode execution will be implemented in PT6.", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected the simple mode stub message to be removed.");
            }

            if (tuiResult.Stdout.Contains("message_update", StringComparison.Ordinal) ||
                tuiResult.Stdout.Contains("diff --git", StringComparison.Ordinal) ||
                tuiResult.Stdout.Contains("stdout:", StringComparison.Ordinal) ||
                tuiResult.Stdout.Contains("stderr:", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected no raw JSONL/stdout/stderr/diff output in the TUI stream.");
            }

            var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
            if (statusAfter.StateCount != statusBefore.StateCount + 1)
            {
                failures.Add($"[{name}] expected state count to increase by 1 but changed from {statusBefore.StateCount} to {statusAfter.StateCount}.");
            }

            if (statusAfter.DeltaCount != statusBefore.DeltaCount + 1)
            {
                failures.Add($"[{name}] expected delta count to increase by 1 but changed from {statusBefore.DeltaCount} to {statusAfter.DeltaCount}.");
            }

            var headPath = Path.Combine(tempRoot, ".rfs", "rck", "HEAD");
            var headText = File.ReadAllText(headPath).Trim();
            if (string.IsNullOrWhiteSpace(headText))
            {
                failures.Add($"[{name}] expected HEAD to resolve after simple mode recording.");
                return;
            }

            var statePath = Path.Combine(tempRoot, ".rfs", "rck", "states", $"{headText}.json");
            if (!File.Exists(statePath))
            {
                failures.Add($"[{name}] expected state file for HEAD '{headText}' to exist.");
                return;
            }

            var stateJson = File.ReadAllText(statePath);
            using var stateDocument = JsonDocument.Parse(stateJson);
            var stateRoot = stateDocument.RootElement;
            var payloadJson = stateRoot.GetProperty("payloadCanonicalJson").GetString() ?? string.Empty;
            using var payloadDocument = JsonDocument.Parse(payloadJson);
            var payloadRoot = payloadDocument.RootElement;
            var interaction = payloadRoot.GetProperty("interaction");
            var pipelineSummary = interaction.GetProperty("pipelineSummary");

            AssertStringEqual(name, failures, "interaction.type", "rufus.interaction-state", payloadRoot.GetProperty("type").GetString());
            AssertStringEqual(name, failures, "interaction.mode", "tui-simple", interaction.GetProperty("mode").GetString());
            AssertStringEqual(name, failures, "interaction.prompt", prompt, interaction.GetProperty("prompt").GetString());
            AssertStringEqual(name, failures, "interaction.answerSummary", "Simple mode works.", interaction.GetProperty("answerSummary").GetString());
            AssertStringEqual(name, failures, "interaction.pipelineSummary.kind", "simple", pipelineSummary.GetProperty("kind").GetString());
            AssertBooleanEqual(name, failures, "interaction.pipelineSummary.usesRckContext", true, pipelineSummary.GetProperty("usesRckContext").GetBoolean());
            AssertBooleanEqual(name, failures, "interaction.pipelineSummary.usesTraceSlice", false, pipelineSummary.GetProperty("usesTraceSlice").GetBoolean());
            AssertBooleanEqual(name, failures, "interaction.pipelineSummary.usesContextPack", false, pipelineSummary.GetProperty("usesContextPack").GetBoolean());
            AssertBooleanEqual(name, failures, "interaction.pipelineSummary.truncated", false, pipelineSummary.GetProperty("truncated").GetBoolean());

            if (pipelineSummary.TryGetProperty("recentInteractionCount", out var recentInteractionCountElement) && recentInteractionCountElement.GetInt32() < 1)
            {
                failures.Add($"[{name}] expected recentInteractionCount to be at least 1.");
            }

            if (!pipelineSummary.TryGetProperty("selectedStateIds", out var selectedStateIdsElement) || selectedStateIdsElement.ValueKind != JsonValueKind.Array || selectedStateIdsElement.GetArrayLength() < 1)
            {
                failures.Add($"[{name}] expected selectedStateIds to contain at least one state id.");
            }

            if (!pipelineSummary.TryGetProperty("selectedAnchorIds", out var selectedAnchorIdsElement) || selectedAnchorIdsElement.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected selectedAnchorIds array to be present.");
            }

            if (!pipelineSummary.TryGetProperty("estimatedChars", out var estimatedCharsElement) || estimatedCharsElement.GetInt32() <= 0)
            {
                failures.Add($"[{name}] expected estimatedChars to be populated.");
            }

            if (!pipelineSummary.TryGetProperty("estimatedTokens", out var estimatedTokensElement) || estimatedTokensElement.GetInt32() <= 0)
            {
                failures.Add($"[{name}] expected estimatedTokens to be populated.");
            }

            if (!pipelineSummary.TryGetProperty("modelBudgetTokens", out var modelBudgetTokensElement) || modelBudgetTokensElement.ValueKind != JsonValueKind.Null)
            {
                failures.Add($"[{name}] expected modelBudgetTokens to be null when no budget source is available.");
            }

            if (!pipelineSummary.TryGetProperty("contextUsageRatio", out var contextUsageRatioElement) || contextUsageRatioElement.ValueKind != JsonValueKind.Null)
            {
                failures.Add($"[{name}] expected contextUsageRatio to be null when no budget source is available.");
            }

            if (!pipelineSummary.TryGetProperty("transportSizeChars", out var transportSizeCharsElement) || transportSizeCharsElement.GetInt32() <= 0)
            {
                failures.Add($"[{name}] expected transportSizeChars to be populated.");
            }

            if (!pipelineSummary.TryGetProperty("transportRisk", out var transportRiskElement) || transportRiskElement.GetString() is not ("low" or "medium" or "high"))
            {
                failures.Add($"[{name}] expected transportRisk to be one of low, medium, or high.");
            }

        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
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

static async Task RunRfsTuiCompleteModeRecordingSessionCaseAsync(string name, string prompt, string input, List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-complete-recording-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        var configNameResult = await RunProcessAsync(tempRoot, "git", "config", "user.name", "Rufus Test");
        var configEmailResult = await RunProcessAsync(tempRoot, "git", "config", "user.email", "rufus@test.local");
        if (configNameResult.ExitCode != 0 || configEmailResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to configure git identity for the temp repo.");
            return;
        }

        var seedPath = Path.Combine(tempRoot, "README.md");
        await File.WriteAllTextAsync(seedPath, "seed\n");
        var addResult = await RunProcessAsync(tempRoot, "git", "add", "README.md");
        if (addResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to stage the seed file: {addResult.Stderr}");
            return;
        }

        var commitResult = await RunProcessAsync(tempRoot, "git", "commit", "-m", "seed commit");
        if (commitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to create the seed commit: {commitResult.Stderr}");
            return;
        }

        var initResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "init");
        if (initResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected rfs init to succeed but got exit code {initResult.ExitCode}. stderr: {initResult.Stderr}");
            return;
        }

        // Mock Pi script — stateful, handles 4 sequential calls for the Complete pipeline:
        //   1) intent inference → PromptIntent JSON
        //   2) anchor selection → RckAnchorSelection JSON (fallback recent-chain)
        //   3) conversational memory → plain text
        //   4) main LLM answer → plain text
        var scriptPath = Path.Combine(tempRoot, "pi");
        var script = "#!/usr/bin/env bash\n" +
                     "set -euo pipefail\n" +
                     "COUNTER_FILE=\"$PWD/.rfs-mock-pi-counter\"\n" +
                     "if [ -f \"$COUNTER_FILE\" ]; then\n" +
                     "  COUNTER=$(cat \"$COUNTER_FILE\")\n" +
                     "else\n" +
                     "  COUNTER=0\n" +
                     "fi\n" +
                     "NEXT=$((COUNTER + 1))\n" +
                     "echo \"$NEXT\" > \"$COUNTER_FILE\"\n" +
                     "case $COUNTER in\n" +
                     "  0)\n" +
                     "    cat <<'EOF'\n" +
                     "{\"type\":\"session\"}\n" +
                     "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":\"{\\\"intent\\\":\\\"general-code-change\\\",\\\"summary\\\":\\\"Implement the reset board action.\\\",\\\"entities\\\":[\\\"reset board\\\"],\\\"constraints\\\":[\\\"do not write RCK\\\"]}\"}]}}\n" +
                     "EOF\n" +
                     "    ;;\n" +
                     "  1)\n" +
                     "    cat <<'EOF'\n" +
                     "{\"type\":\"session\"}\n" +
                     "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":\"{\\\"type\\\":\\\"rufus.anchor-selection\\\",\\\"schemaVersion\\\":1,\\\"selectedAnchorIds\\\":[],\\\"fallbackStrategy\\\":\\\"recent-chain\\\",\\\"rationale\\\":[{\\\"target\\\":\\\"recent-chain\\\",\\\"reason\\\":\\\"No relevant anchor found for this task.\\\"}],\\\"warnings\\\":[],\\\"confidence\\\":0.9}\"}]}}\n" +
                     "EOF\n" +
                     "    ;;\n" +
                     "  2)\n" +
                     "    cat <<'EOF'\n" +
                     "{\"type\":\"session\"}\n" +
                     "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":\"No relevant conversational history.\"}]}}\n" +
                     "EOF\n" +
                     "    ;;\n" +
                     "  3)\n" +
                     "    cat <<'EOF'\n" +
                     "{\"type\":\"session\"}\n" +
                     "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":\"Complete mode works. State + Delta recorded.\"}]}}\n" +
                     "EOF\n" +
                     "    ;;\n" +
                     "  *)\n" +
                     "    echo '{\"type\":\"session\"}' >&2\n" +
                     "    echo '{\"type\":\"error\",\"error\":{\"errorMessage\":\"Unexpected call count: '\"$COUNTER\"'\"}}'\n" +
                     "    exit 1\n" +
                     "    ;;\n" +
                     "esac\n";
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
        try
        {
            Environment.SetEnvironmentVariable("PATH", tempRoot + Path.PathSeparator + (originalPath ?? string.Empty));

            var statusBefore = RckWorkspaceStatusReader.Read(tempRoot);
            var tuiResult = await RunProcessAsyncWithInput(tempRoot, input, "dotnet", "run", "--project", cliProjectPath, "--");
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
                "[Complete]",
                "[1/5] Inferring intent...",
                "  intent:",
                "  summary:",
                "  source: pi-intent-inference",
                "[2/5] Building TraceSlice proposal...",
                "  proposal: pi-trace-slice-proposal",
                "  slicing: anchor-guided structural",
                "  anchors selected:",
                "  expansion:",
                "  fallback:",
                "[3/5] Validating proposal...",
                "  validation: accepted",
                "[4/5] Building ContextPack + ConversationalMemory...",
                "  scope:",
                "  selected states/deltas/anchors:",
                "  estimated tokens:",
                "  transport:",
                "  transport risk:",
                "[5/5] Asking main LLM...",
                "  agent:",
                "  model:",
                "Respuesta:",
                "Recorded State + Delta:",
            };

            foreach (var fragment in requiredFragments)
            {
                if (!tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
                {
                    failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
                }
            }

            if (tuiResult.Stdout.Contains("message_update", StringComparison.Ordinal) ||
                tuiResult.Stdout.Contains("diff --git", StringComparison.Ordinal) ||
                tuiResult.Stdout.Contains("stdout:", StringComparison.Ordinal) ||
                tuiResult.Stdout.Contains("stderr:", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected no raw JSONL/stdout/stderr/diff output in the TUI stream.");
            }

            var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
            if (statusAfter.StateCount != statusBefore.StateCount + 1)
            {
                failures.Add($"[{name}] expected state count to increase by 1 but changed from {statusBefore.StateCount} to {statusAfter.StateCount}.");
            }

            if (statusAfter.DeltaCount != statusBefore.DeltaCount + 1)
            {
                failures.Add($"[{name}] expected delta count to increase by 1 but changed from {statusBefore.DeltaCount} to {statusAfter.DeltaCount}.");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
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

static async Task RunRfsTuiPlanModeRecordingSessionCaseAsync(string name, string prompt, string input, List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-plan-recording-checks", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempRoot);

    try
    {
        var gitInitResult = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInitResult.Stderr}");
            return;
        }

        var configNameResult = await RunProcessAsync(tempRoot, "git", "config", "user.name", "Rufus Test");
        var configEmailResult = await RunProcessAsync(tempRoot, "git", "config", "user.email", "rufus@test.local");
        if (configNameResult.ExitCode != 0 || configEmailResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to configure git identity for the temp repo.");
            return;
        }

        var seedPath = Path.Combine(tempRoot, "README.md");
        await File.WriteAllTextAsync(seedPath, "seed\n");
        var addResult = await RunProcessAsync(tempRoot, "git", "add", "README.md");
        if (addResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to stage the seed file: {addResult.Stderr}");
            return;
        }

        var commitResult = await RunProcessAsync(tempRoot, "git", "commit", "-m", "seed commit");
        if (commitResult.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to create the seed commit: {commitResult.Stderr}");
            return;
        }

        var initResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "init");
        if (initResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected rfs init to succeed but got exit code {initResult.ExitCode}. stderr: {initResult.Stderr}");
            return;
        }

        var scriptPath = Path.Combine(tempRoot, "pi");
        var script = "#!/usr/bin/env bash\n" +
                     "set -euo pipefail\n" +
                     "cat <<EOF\n" +
                     "{\"type\":\"session\"}\n" +
                     "{\"type\":\"message_end\",\"message\":{\"role\":\"assistant\",\"provider\":\"test-provider\",\"model\":\"test-model\",\"content\":[{\"type\":\"text\",\"text\":\"Plan:\\n1. Reuse Simple Context.\\n2. Return a concise implementation plan only.\"}]}}\n" +
                     "EOF\n";
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
        try
        {
            Environment.SetEnvironmentVariable("PATH", tempRoot + Path.PathSeparator + (originalPath ?? string.Empty));

            var statusBefore = RckWorkspaceStatusReader.Read(tempRoot);
            var tuiResult = await RunProcessAsyncWithInput(tempRoot, input, "dotnet", "run", "--project", cliProjectPath, "--");
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
                "[Plan]",
                "Building planning context...",
                "Context:",
                "recent interactions:",
                "anchors:",
                "artifacts:",
                "estimated tokens:",
                "transport risk:",
                "truncated:",
                "Respuesta:",
                "Recorded State + Delta:",
            };

            foreach (var fragment in requiredFragments)
            {
                if (!tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
                {
                    failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
                }
            }

            if (tuiResult.Stdout.Contains("Mode execution will be implemented in PT8.", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected the plan mode stub message to be removed.");
            }

            if (tuiResult.Stdout.Contains("message_update", StringComparison.Ordinal) ||
                tuiResult.Stdout.Contains("diff --git", StringComparison.Ordinal) ||
                tuiResult.Stdout.Contains("stdout:", StringComparison.Ordinal) ||
                tuiResult.Stdout.Contains("stderr:", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected no raw JSONL/stdout/stderr/diff output in the TUI stream.");
            }

            var statusAfter = RckWorkspaceStatusReader.Read(tempRoot);
            if (statusAfter.StateCount != statusBefore.StateCount + 1)
            {
                failures.Add($"[{name}] expected state count to increase by 1 but changed from {statusBefore.StateCount} to {statusAfter.StateCount}.");
            }

            if (statusAfter.DeltaCount != statusBefore.DeltaCount + 1)
            {
                failures.Add($"[{name}] expected delta count to increase by 1 but changed from {statusBefore.DeltaCount} to {statusAfter.DeltaCount}.");
            }

            var headPath = Path.Combine(tempRoot, ".rfs", "rck", "HEAD");
            var headText = File.ReadAllText(headPath).Trim();
            if (string.IsNullOrWhiteSpace(headText))
            {
                failures.Add($"[{name}] expected HEAD to resolve after plan mode recording.");
                return;
            }

            var statePath = Path.Combine(tempRoot, ".rfs", "rck", "states", $"{headText}.json");
            if (!File.Exists(statePath))
            {
                failures.Add($"[{name}] expected state file for HEAD '{headText}' to exist.");
                return;
            }

            var stateJson = File.ReadAllText(statePath);
            using var stateDocument = JsonDocument.Parse(stateJson);
            var stateRoot = stateDocument.RootElement;
            var payloadJson = stateRoot.GetProperty("payloadCanonicalJson").GetString() ?? string.Empty;
            using var payloadDocument = JsonDocument.Parse(payloadJson);
            var payloadRoot = payloadDocument.RootElement;
            var interaction = payloadRoot.GetProperty("interaction");
            var pipelineSummary = interaction.GetProperty("pipelineSummary");

            AssertStringEqual(name, failures, "interaction.type", "rufus.interaction-state", payloadRoot.GetProperty("type").GetString());
            AssertStringEqual(name, failures, "interaction.mode", "tui-plan", interaction.GetProperty("mode").GetString());
            AssertStringEqual(name, failures, "interaction.prompt", prompt, interaction.GetProperty("prompt").GetString());
            AssertStringEqual(name, failures, "interaction.answerSummary", "Plan: 1. Reuse Simple Context. 2. Return a concise implementation plan only.", interaction.GetProperty("answerSummary").GetString());
            AssertStringEqual(name, failures, "interaction.pipelineSummary.kind", "plan", pipelineSummary.GetProperty("kind").GetString());
            AssertStringEqual(name, failures, "interaction.pipelineSummary.contextMode", "simple", pipelineSummary.GetProperty("contextMode").GetString());
            AssertBooleanEqual(name, failures, "interaction.pipelineSummary.usesRckContext", true, pipelineSummary.GetProperty("usesRckContext").GetBoolean());
            AssertBooleanEqual(name, failures, "interaction.pipelineSummary.usesTraceSlice", false, pipelineSummary.GetProperty("usesTraceSlice").GetBoolean());
            AssertBooleanEqual(name, failures, "interaction.pipelineSummary.usesContextPack", false, pipelineSummary.GetProperty("usesContextPack").GetBoolean());
            AssertBooleanEqual(name, failures, "interaction.pipelineSummary.truncated", false, pipelineSummary.GetProperty("truncated").GetBoolean());

            if (pipelineSummary.TryGetProperty("recentInteractionCount", out var recentInteractionCountElement) && recentInteractionCountElement.GetInt32() < 1)
            {
                failures.Add($"[{name}] expected recentInteractionCount to be at least 1.");
            }

            if (!pipelineSummary.TryGetProperty("selectedStateIds", out var selectedStateIdsElement) || selectedStateIdsElement.ValueKind != JsonValueKind.Array || selectedStateIdsElement.GetArrayLength() < 1)
            {
                failures.Add($"[{name}] expected selectedStateIds to contain at least one state id.");
            }

            if (!pipelineSummary.TryGetProperty("selectedDeltaIds", out var selectedDeltaIdsElement) || selectedDeltaIdsElement.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected selectedDeltaIds array to be present.");
            }

            if (!pipelineSummary.TryGetProperty("selectedAnchorIds", out var selectedAnchorIdsElement) || selectedAnchorIdsElement.ValueKind != JsonValueKind.Array)
            {
                failures.Add($"[{name}] expected selectedAnchorIds array to be present.");
            }

            if (!pipelineSummary.TryGetProperty("artifactRefCount", out var artifactRefCountElement) || artifactRefCountElement.GetInt32() < 0)
            {
                failures.Add($"[{name}] expected artifactRefCount to be populated.");
            }

            if (!pipelineSummary.TryGetProperty("estimatedChars", out var estimatedCharsElement) || estimatedCharsElement.GetInt32() <= 0)
            {
                failures.Add($"[{name}] expected estimatedChars to be populated.");
            }

            if (!pipelineSummary.TryGetProperty("estimatedTokens", out var estimatedTokensElement) || estimatedTokensElement.GetInt32() <= 0)
            {
                failures.Add($"[{name}] expected estimatedTokens to be populated.");
            }

            if (!pipelineSummary.TryGetProperty("modelBudgetTokens", out var modelBudgetTokensElement) || modelBudgetTokensElement.ValueKind != JsonValueKind.Null)
            {
                failures.Add($"[{name}] expected modelBudgetTokens to be null when no budget source is available.");
            }

            if (!pipelineSummary.TryGetProperty("contextUsageRatio", out var contextUsageRatioElement) || contextUsageRatioElement.ValueKind != JsonValueKind.Null)
            {
                failures.Add($"[{name}] expected contextUsageRatio to be null when no budget source is available.");
            }

            if (!pipelineSummary.TryGetProperty("transportSizeChars", out var transportSizeCharsElement) || transportSizeCharsElement.GetInt32() <= 0)
            {
                failures.Add($"[{name}] expected transportSizeChars to be populated.");
            }

            if (!pipelineSummary.TryGetProperty("transportRisk", out var transportRiskElement) || transportRiskElement.GetString() is not ("low" or "medium" or "high"))
            {
                failures.Add($"[{name}] expected transportRisk to be one of low, medium, or high.");
            }

            if (!pipelineSummary.TryGetProperty("modelBudgetTokens", out var planModelBudgetTokensElement) || planModelBudgetTokensElement.ValueKind != JsonValueKind.Null)
            {
                failures.Add($"[{name}] expected modelBudgetTokens to be null when no budget source is available.");
            }

            if (!pipelineSummary.TryGetProperty("contextUsageRatio", out var planContextUsageRatioElement) || planContextUsageRatioElement.ValueKind != JsonValueKind.Null)
            {
                failures.Add($"[{name}] expected contextUsageRatio to be null when no budget source is available.");
            }

            if (!pipelineSummary.TryGetProperty("transportSizeChars", out var planTransportSizeCharsElement) || planTransportSizeCharsElement.GetInt32() <= 0)
            {
                failures.Add($"[{name}] expected transportSizeChars to be populated.");
            }

            if (!pipelineSummary.TryGetProperty("transportRisk", out var planTransportRiskElement) || planTransportRiskElement.GetString() is not ("low" or "medium" or "high"))
            {
                failures.Add($"[{name}] expected transportRisk to be one of low, medium, or high.");
            }

            if (!interaction.TryGetProperty("provider", out var providerElement) || providerElement.GetString() != "test-provider")
            {
                failures.Add($"[{name}] expected interaction.provider to be 'test-provider'.");
            }

            if (!interaction.TryGetProperty("model", out var modelElement) || modelElement.GetString() != "test-model")
            {
                failures.Add($"[{name}] expected interaction.model to be 'test-model'.");
            }

            // The delta count change is verified above; the payload itself is covered by state assertions and external validation.
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
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

static void AssertStringEqual(string name, List<string> failures, string field, string expected, string? actual)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        failures.Add($"[{name}] expected {field} to equal '{expected}' but found '{actual}'.");
    }
}

static void AssertBooleanEqual(string name, List<string> failures, string field, bool expected, bool actual)
{
    if (expected != actual)
    {
        failures.Add($"[{name}] expected {field} to equal '{expected}' but found '{actual}'.");
    }
}

static async Task RunRfsTuiPasteCaptureSessionCaseAsync(
    string name,
    string input,
    bool expectTempPasteFile,
    string[] expectedFragments,
    string[] forbiddenFragments,
    List<string> failures)
{
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var cliProjectPath = Path.Combine(repoRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
    var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-paste-capture-checks", Guid.NewGuid().ToString("N"));
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

        var tuiResult = await RunProcessAsyncWithInput(tempRoot, input, "dotnet", "run", "--project", cliProjectPath, "--");
        if (tuiResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected exit code 0 but got {tuiResult.ExitCode}. stderr: {tuiResult.Stderr}");
            return;
        }

        if (!string.IsNullOrWhiteSpace(tuiResult.Stderr))
        {
            failures.Add($"[{name}] expected no stderr but got: {tuiResult.Stderr.Trim()}.");
        }

        foreach (var fragment in expectedFragments)
        {
            if (!tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
            }
        }

        foreach (var fragment in forbiddenFragments)
        {
            if (tuiResult.Stdout.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected stdout to not contain '{fragment}' but it was present.");
            }
        }

        var pasteDirectory = Path.Combine(tempRoot, ".rfs", "tmp", "pastes");
        if (expectTempPasteFile)
        {
            if (!Directory.Exists(pasteDirectory))
            {
                failures.Add($"[{name}] expected temp paste directory '{pasteDirectory}' to exist.");
            }
            else
            {
                var pasteFile = Directory.GetFiles(pasteDirectory, "*_paste.md", SearchOption.TopDirectoryOnly);
                if (pasteFile.Length != 1)
                {
                    failures.Add($"[{name}] expected exactly one temp paste file but found {pasteFile.Length}.");
                }
                else
                {
                    var pasteContent = File.ReadAllText(pasteFile[0]);
                    if (!pasteContent.Contains("paste line 1", StringComparison.Ordinal) || !pasteContent.Contains("paste line 2", StringComparison.Ordinal))
                    {
                        failures.Add($"[{name}] expected paste file to contain captured lines.");
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        failures.Add($"[{name}] threw {ex}");
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

static async Task RunRfsTuiPasteCaptureCancelSessionCaseAsync(
    string name,
    string input,
    string[] expectedFragments,
    string[] forbiddenFragments,
    List<string> failures)
{
    await RunRfsTuiPasteCaptureSessionCaseAsync(name, input, false, expectedFragments, forbiddenFragments, failures);
}

static async Task RunRfsTuiPromptModeSelectionBurstSessionCaseAsync(
    string name,
    string prompt,
    string input,
    string[] expectedFragments,
    string[] forbiddenFragments,
    List<string> failures)
{
    await RunRfsTuiPromptModeSelectionSessionCaseAsync(
        name,
        prompt,
        input,
        expectedFragments,
        expectPromptEcho: false,
        forbiddenFragments: forbiddenFragments,
        failures: failures);
}

sealed class FakeIntentLlmTransport : IIntentLlmTransport
{
    private readonly bool _success;
    private readonly string _answerJson;
    private readonly string? _errorMessage;

    public FakeIntentLlmTransport(bool success, string answerJson, string? errorMessage = null)
    {
        _success = success;
        _answerJson = answerJson;
        _errorMessage = errorMessage;
    }

    public int CallCount { get; private set; }

    public string? LastWorkingDirectory { get; private set; }

    public string? LastPrompt { get; private set; }

    public string? LastModel { get; private set; }

    public Task<PiJsonAskResult> AskAsync(string workingDirectory, string prompt, string model, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastWorkingDirectory = workingDirectory;
        LastPrompt = prompt;
        LastModel = model;
        return Task.FromResult(new PiJsonAskResult(_success, prompt, _answerJson, _errorMessage, "pi", model));
    }
}

sealed class FakeTraceSliceProposalLlmTransport : ITraceSliceProposalLlmTransport
{
    private readonly string _answerJson;

    public FakeTraceSliceProposalLlmTransport(string answerJson)
    {
        _answerJson = answerJson;
    }

    public int CallCount { get; private set; }

    public string? LastWorkingDirectory { get; private set; }

    public string? LastPrompt { get; private set; }

    public string? LastModel { get; private set; }

    public Task<PiJsonAskResult> AskAsync(string workingDirectory, string prompt, string model, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastWorkingDirectory = workingDirectory;
        LastPrompt = prompt;
        LastModel = model;
        return Task.FromResult(new PiJsonAskResult(true, prompt, _answerJson, null, "pi", model));
    }
}
