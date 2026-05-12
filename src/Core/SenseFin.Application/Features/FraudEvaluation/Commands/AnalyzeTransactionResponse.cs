using SenseFin.Domain.Aggregates.RiskProfile;

namespace SenseFin.Application.Features.FraudEvaluation.Commands;

/// <summary>
/// Response returned after a transaction has been analyzed for fraud.
/// Contains the risk assessment details including Explainable AI reasoning.
/// </summary>
public sealed record AnalyzeTransactionResponse(
    /// <summary>Unique identifier of the persisted transaction.</summary>
    Guid TransactionId,

    /// <summary>Calculated risk score (0–100 scale, mapped from AI's 0.0–1.0).</summary>
    double RiskScore,

    /// <summary>Overall risk classification derived from the score.</summary>
    RiskLevel RiskLevel,

    /// <summary>Human-readable explanation from the AI or rule engine (XAI).</summary>
    string AiReason,

    /// <summary>Quick flag: true when RiskLevel is High or Critical.</summary>
    bool IsHighRisk
);
