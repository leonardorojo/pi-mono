namespace Rufus.RCK.Workspace;

public sealed record RckContextUsageReport(
    int EstimatedChars,
    int EstimatedTokens,
    int? ModelBudgetTokens,
    double? ContextUsageRatio,
    int TransportSizeChars,
    string TransportRisk,
    bool Truncated);

public static class RckContextUsageEstimator
{
    public static RckContextUsageReport Create(
        int estimatedChars,
        int estimatedTokens,
        int? modelBudgetTokens,
        bool truncated)
    {
        double? contextUsageRatio = modelBudgetTokens is > 0
            ? estimatedTokens / (double)modelBudgetTokens.Value
            : null;

        var transportRisk = estimatedChars <= 32_000
            ? "low"
            : estimatedChars <= 96_000
                ? "medium"
                : "high";

        return new RckContextUsageReport(
            estimatedChars,
            estimatedTokens,
            modelBudgetTokens,
            contextUsageRatio,
            estimatedChars,
            transportRisk,
            truncated);
    }
}
