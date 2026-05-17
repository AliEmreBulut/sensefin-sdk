using SenseFin.Domain.Common;

namespace SenseFin.Domain.Aggregates.RiskProfile;

// Belirli bir hesap için birikimli risk durumunu tutan Aggregate Root.
// Her yeni işlem değerlendirmesi buraya eklenir ve genel risk seviyesini günceller.
public sealed class RiskProfileAggregate : AggregateRoot
{
    private readonly List<RiskScoreEntry> _riskScores = [];

    // Kimlik

    public string AccountId { get; private set; } = null!;

    // State

    // Current overall risk level derived from accumulated scores.
    public RiskLevel CurrentRiskLevel { get; private set; }

    // Running average of all risk scores recorded.
    public double AverageRiskScore { get; private set; }

    // Total number of transactions evaluated.
    public int TotalEvaluations { get; private set; }

    // Timestamp of the last risk evaluation.
    public DateTime? LastEvaluatedAt { get; private set; }

    // Hesabın kurumsal (ticari) mi yoksa bireysel (şahıs) mı olduğunu belirtir.
    public bool IsCorporate { get; private set; }

    // All recorded risk score entries (append-only).
    public IReadOnlyCollection<RiskScoreEntry> RiskScores => _riskScores.AsReadOnly();

    // Constructors

    // EF Core / serialization için
    private RiskProfileAggregate() { }

    // Hesap için yeni bir risk profili oluşturur
    public static RiskProfileAggregate Create(string accountId, bool isCorporate = false)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            throw new ArgumentException("AccountId is required.", nameof(accountId));

        return new RiskProfileAggregate
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            IsCorporate = isCorporate,
            CurrentRiskLevel = RiskLevel.Low,
            AverageRiskScore = 0,
            TotalEvaluations = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Behaviors

    // Yeni bir risk skoru ekler ve genel durumu tekrar hesaplar
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

    public void SetRiskLevel(string riskLevelStr)
    {
        if (Enum.TryParse<RiskLevel>(riskLevelStr, true, out var parsedLevel))
        {
            CurrentRiskLevel = parsedLevel;
            LastEvaluatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
