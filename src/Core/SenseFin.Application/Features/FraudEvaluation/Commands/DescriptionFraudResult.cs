using SenseFin.Domain.Aggregates.Transaction;
namespace SenseFin.Application.Features.FraudEvaluation.Commands;

// Analiz sonucu
public sealed record DescriptionFraudResult(
    bool IsSuspicious,
    DescriptionFraudConfidence Confidence,
    string[] MatchedPatterns,
    double RecommendedRiskScore,
    string Reason)
{
    // Temiz (güvenli) sonuç döndürür
    public static DescriptionFraudResult Safe() => new(
        IsSuspicious: false,
        Confidence: DescriptionFraudConfidence.None,
        MatchedPatterns: [],
        RecommendedRiskScore: 0,
        Reason: string.Empty);
}