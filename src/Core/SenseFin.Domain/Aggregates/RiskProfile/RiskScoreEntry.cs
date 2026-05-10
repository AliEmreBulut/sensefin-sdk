using SenseFin.Domain.Common;

namespace SenseFin.Domain.Aggregates.RiskProfile;

/// <summary>
/// Value object representing a single risk score entry recorded
/// against a RiskProfile. Captures the score, source, and timestamp.
/// </summary>
public sealed class RiskScoreEntry : ValueObject
{
    /// <summary>Numeric risk score (0–100).</summary>
    public double Score { get; }

    /// <summary>Identifier of the rule/engine that produced this score.</summary>
    public string Source { get; }

    /// <summary>Human-readable reason or explanation.</summary>
    public string? Reason { get; }

    /// <summary>When this score was calculated.</summary>
    public DateTime EvaluatedAt { get; }

    /// <summary>Reference to the transaction that triggered this score.</summary>
    public Guid TransactionId { get; }

    private RiskScoreEntry(double score, string source, Guid transactionId, string? reason, DateTime evaluatedAt)
    {
        Score = score;
        Source = source;
        TransactionId = transactionId;
        Reason = reason;
        EvaluatedAt = evaluatedAt;
    }

    public static RiskScoreEntry Create(double score, string source, Guid transactionId, string? reason = null)
    {
        if (score is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 100.");

        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source is required.", nameof(source));

        return new RiskScoreEntry(score, source, transactionId, reason, DateTime.UtcNow);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Score;
        yield return Source;
        yield return TransactionId;
        yield return EvaluatedAt;
    }
}
