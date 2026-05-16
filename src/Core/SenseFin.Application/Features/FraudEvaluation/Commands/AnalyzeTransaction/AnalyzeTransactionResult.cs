using SenseFin.Domain.Aggregates.RiskProfile;

namespace SenseFin.Application.Features.FraudEvaluation.Commands.AnalyzeTransaction;

// İşlem analizi sonucunu temsil eden nesne.
public sealed record AnalyzeTransactionResult
{
    public required Guid TransactionId { get; init; }
    public required Guid RiskProfileId { get; init; }
    public required double RiskScore { get; init; }
    public required RiskLevel CurrentRiskLevel { get; init; }
    public required bool IsFlagged { get; init; }
    public string? EvaluationSummary { get; init; }
}
