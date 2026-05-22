using System.Text.Json.Serialization;

namespace Rufus.Agenting.TraceSlice;

public sealed record TraceSliceProposal(
    string Type,
    int SchemaVersion,
    TraceSliceProposalPrompt Prompt,
    TraceSliceProposalIntent Intent,
    TraceSliceProposalSelection RequestedSelection,
    TraceSliceProposalMaterializationPolicy RequestedMaterializationPolicy,
    IReadOnlyList<TraceSliceProposalRationale> Rationale,
    double Confidence,
    IReadOnlyList<string> Warnings);

public sealed record TraceSliceProposalPrompt(
    string Text,
    bool IsExcerpt);

public sealed record TraceSliceProposalIntent(
    string Kind,
    string Summary,
    string Source);
