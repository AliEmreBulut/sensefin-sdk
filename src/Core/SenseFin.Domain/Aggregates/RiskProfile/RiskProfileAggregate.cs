using SenseFin.Domain.Common;

namespace SenseFin.Domain.Aggregates.RiskProfile;

/// <summary>
/// RiskProfile Aggregate Root — maintains the cumulative risk state
/// for a specific account. Each incoming transaction's risk evaluation
/// adds entries and may update the overall risk level.
/// </summary>
public sealed class RiskProfileAggregate : AggregateRoot
{
    private readonly List<RiskScoreEntry> _riskScores = [];

    // ────────────────── Identity ──────────────────

    /// <summary>The account this risk profile belongs to.</summary>
    public string AccountId { get; private set; } = null!;

    // ────────────────── State ──────────────────

    /// <summary>Current overall risk level derived from accumulated scores.</summary>
    public RiskLevel CurrentRiskLevel { get; private set; }

    /// <summary>Running average of all risk scores recorded.</summary>
    public double AverageRiskScore { get; private set; }

    /// <summary>Total number of transactions evaluated.</summary>
    public int TotalEvaluations { get; private set; }

    /// <summary>Timestamp of the last risk evaluation.</summary>
    public DateTime? LastEvaluatedAt { get; private set; }

    /// <summary>All recorded risk score entries (append-only).</summary>
    public IReadOnlyCollection<RiskScoreEntry> RiskScores => _riskScores.AsReadOnly();

    // ────────────────── Constructor ──────────────────

    /// <summary>EF Core / serialization constructor.</summary>
    private RiskProfileAggregate() { }

    /// <summary>
    /// Factory method to create a new RiskProfile for an account.
    /// </summary>
    public static RiskProfileAggregate Create(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("AccountId is required.", nameof(accountId));

        return new RiskProfileAggregate
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            CurrentRiskLevel = RiskLevel.Low,
            AverageRiskScore = 0,
            TotalEvaluations = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    // ────────────────── Behavior ──────────────────

    /// <summary>
    /// Records a new risk score and recalculates the aggregate risk state.
    /// </summary>
    public void AddRiskScore(RiskScoreEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _riskScores.Add(entry);
        TotalEvaluations++;

        // Recalculate running average
        AverageRiskScore = _riskScores.Average(s => s.Score);

        // Derive risk level from average score
        CurrentRiskLevel = AverageRiskScore switch
        {
            >= 80 => RiskLevel.Critical,
            >= 60 => RiskLevel.High,
            >= 40 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        LastEvaluatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
