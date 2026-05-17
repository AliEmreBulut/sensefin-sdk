using SenseFin.Domain.Common;

namespace SenseFin.Domain.Aggregates.RiskProfile;

// RiskProfile'a kaydedilen tek bir risk skoru kaydını temsil eden Value Object.
// Skoru, kaynağı ve zaman damgasını tutar.
public sealed class RiskScoreEntry : ValueObject
{
    public double Score { get; } // 0-100 arası risk puanı

    public string Source { get; } // Skoru üreten kural veya motorun adı

    public string? Reason { get; } // Açıklama metni

    public DateTime EvaluatedAt { get; } // Hesaplama zamanı

    public Guid TransactionId { get; } // İlgili işlem ID'si

    private RiskScoreEntry(double score, string source, Guid transactionId, string? reason, DateTime evaluatedAt)
    {
        Score = score;
        Source = source;
        TransactionId = transactionId;
        Reason = reason;
        EvaluatedAt = evaluatedAt;
    }

    public static RiskScoreEntry Create(double score, string source, Guid transactionId, string? reason = null, DateTime? evaluatedAt = null)
    {
        if (score is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 100.");

        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source is required.", nameof(source));

        return new RiskScoreEntry(score, source, transactionId, reason, evaluatedAt ?? DateTime.UtcNow);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Score;
        yield return Source;
        yield return TransactionId;
        yield return EvaluatedAt;
    }
}
