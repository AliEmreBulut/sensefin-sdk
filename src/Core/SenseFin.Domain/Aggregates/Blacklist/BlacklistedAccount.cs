namespace SenseFin.Domain.Aggregates.Blacklist;

// Kara listeye alınmış (kara listeye alınmış) hesap veya IBAN.
// Kara listedeki hesaplarla yapılan işlemler otomatik olarak maksimum risk olarak işaretlenir.
public sealed class BlacklistedAccount
{
    public Guid Id { get; private set; }
    public string AccountIdentifier { get; private set; } = null!;
    public BlacklistIdentifierType IdentifierType { get; private set; }
    public BlacklistReason Reason { get; private set; }
    public string? Description { get; private set; }
    public string AddedBy { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public int IncidentCount { get; private set; }

    // Constructor

    private BlacklistedAccount() { }

    // Yeni bir kara liste kaydı oluşturmak için factory metodu
    public static BlacklistedAccount Create(
        string accountIdentifier,
        BlacklistIdentifierType identifierType,
        BlacklistReason reason,
        string addedBy,
        string? description = null,
        DateTime? expiresAt = null)
    {
        if (string.IsNullOrWhiteSpace(accountIdentifier))
            throw new ArgumentException("Account identifier is required.", nameof(accountIdentifier));

        return new BlacklistedAccount
        {
            Id = Guid.NewGuid(),
            AccountIdentifier = accountIdentifier.Trim(),
            IdentifierType = identifierType,
            Reason = reason,
            Description = description,
            AddedBy = addedBy,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            IncidentCount = 1
        };
    }

    // Tekrar eden dolandırıcılık girişimleri için event sayısını artırır
    public void IncrementIncident(string? additionalDescription = null)
    {
        IncidentCount++;
        UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(additionalDescription))
        {
            Description = string.IsNullOrWhiteSpace(Description)
                ? additionalDescription
                : $"{Description} | {additionalDescription}";
        }
    }

    // Kara liste kaydını devre dışı bırakır (soft-delete)
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    // Daha önce devre dışı bırakılmış bir kara liste kaydını yeniden etkinleştirir
    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}

// Kara liste kaydında kullanılan kimlik tipi
public enum BlacklistIdentifierType
{
    /// <summary>A bank account ID.</summary>
    AccountId = 0,

    /// <summary>An IBAN number.</summary>
    Iban = 1,

    /// <summary>A device fingerprint/ID.</summary>
    DeviceId = 2
}

// Hesabın neden kara listeye alındığının sebebi
public enum BlacklistReason
{
    /// <summary>Confirmed fraud activity (dolandırıcılık).</summary>
    FraudConfirmed = 0,

    /// <summary>Payment request scam (ödeme isteği dolandırıcılığı).</summary>
    PaymentRequestScam = 1,

    /// <summary>Identity theft / account takeover.</summary>
    IdentityTheft = 2,

    /// <summary>Money laundering suspicion.</summary>
    MoneyLaundering = 3,

    /// <summary>Repeated high-risk transactions (auto-blacklisted by system).</summary>
    RepeatedHighRisk = 4,

    /// <summary>Phishing attack source.</summary>
    Phishing = 5,

    /// <summary>Manually reported by user or admin.</summary>
    ManualReport = 6
}
