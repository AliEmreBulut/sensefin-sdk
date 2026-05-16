using SenseFin.Domain.Aggregates.Blacklist;

namespace SenseFin.Application.Interfaces;

// Kara listeye alınmış hesaplar için repository arayüzü.
public interface IBlacklistRepository
{
    // Hesap ID, IBAN veya cihaz ID'sinin kara listede olup olmadığını kontrol eder.
    // Sadece aktif ve süresi dolmamış kayıtları döner.
    Task<BlacklistedAccount?> FindActiveAsync(
        string identifier,
        BlacklistIdentifierType identifierType,
        CancellationToken cancellationToken = default);

    // Birden fazla kimliği aynı anda kontrol eder (Hesap ID + IBAN + Cihaz ID).
    // Bulunan ilk aktif eşleşmeyi döner.
    Task<BlacklistedAccount?> FindAnyMatchAsync(
        string? accountId,
        string? iban,
        string? deviceId,
        CancellationToken cancellationToken = default);

    // Yeni bir kara liste kaydı ekler
    Task AddAsync(BlacklistedAccount entry, CancellationToken cancellationToken = default);

    // Mevcut bir kara liste kaydını günceller
    Task UpdateAsync(BlacklistedAccount entry, CancellationToken cancellationToken = default);
}
