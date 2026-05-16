using SenseFin.Domain.Aggregates.RiskProfile;

namespace SenseFin.Application.Features.FraudEvaluation.Commands;

// İşlem analizi sonucunda dönen veriler.
// Risk skorunu ve yapay zeka açıklamasını içerir.
public sealed record AnalyzeTransactionResponse(
    Guid TransactionId,
    double RiskScore,
    RiskLevel RiskLevel,
    string AiReason,
    string UserFriendlyMessage,
    bool IsHighRisk
);
