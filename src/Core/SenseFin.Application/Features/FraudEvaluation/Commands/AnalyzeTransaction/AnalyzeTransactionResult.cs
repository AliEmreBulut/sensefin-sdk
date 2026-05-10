using SenseFin.Domain.Aggregates.RiskProfile;

namespace SenseFin.Application.Features.FraudEvaluation.Commands.AnalyzeTransaction;

/// <summary>
/// Result returned by the AnalyzeTransactionCommand handler.
/// </summary>
public sealed record AnalyzeTransactionResult
{
    /// <summary>The ID of the created transaction.</summary>
    public required Guid TransactionId { get; init; }

    /// <summary>The ID of the risk profile that was evaluated.</summary>
    public required Guid RiskProfileId { get; init; }

    /// <summary>The risk score assigned to this transaction.</summary>
    public required double RiskScore { get; init; }

    /// <summary>The current overall risk level of the account.</summary>
    public required RiskLevel CurrentRiskLevel { get; init; }

    /// <summary>Whether this transaction was flagged for further review.</summary>
    public required bool IsFlagged { get; init; }

    /// <summary>Human-readable evaluation summary.</summary>
    public string? EvaluationSummary { get; init; }
}
