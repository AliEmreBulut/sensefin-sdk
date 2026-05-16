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

    // Kurucu

    private BlacklistedAccount() { }

    // Yeni bir kara liste kaydı oluşturmak için fabrika metodu
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

    // Tekrar eden dolandırıcılık girişimleri için olay sayısını artırır
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

public enum BlacklistIdentifierType
{
    AccountId = 0,
    Iban = 1,
    DeviceId = 2
}

// Hesabın neden kara listeye alındığının sebebi
public enum BlacklistReason
{
    FraudConfirmed = 0, // Kesinleşmiş dolandırıcılık
    PaymentRequestScam = 1, // Ödeme isteği dolandırıcılığı
    IdentityTheft = 2, // Kimlik hırsızlığı
    MoneyLaundering = 3, // Para aklama şüphesi
    RepeatedHighRisk = 4, // Sistem tarafından otomatik eklenen (üst üste riskli işlem)
    Phishing = 5, // Oltalama saldırısı kaynağı
    ManualReport = 6 // Manuel raporlama
}
