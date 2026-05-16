using Microsoft.EntityFrameworkCore;
using SenseFin.Application.Interfaces;
using SenseFin.Domain.Aggregates.Blacklist;

namespace SenseFin.Infrastructure.Persistence.Repositories;

// IBlacklistRepository arayüzünün EF Core implementasyonu.
public sealed class BlacklistRepository(SenseFinDbContext context) : IBlacklistRepository
{
    public async Task<BlacklistedAccount?> FindActiveAsync(
        string identifier,
        BlacklistIdentifierType identifierType,
        CancellationToken cancellationToken = default)
    {
        return await context.BlacklistedAccounts
            .Where(b => b.AccountIdentifier == identifier
                     && b.IdentifierType == identifierType
                     && b.IsActive
                     && (b.ExpiresAt == null || b.ExpiresAt > DateTime.UtcNow))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<BlacklistedAccount?> FindAnyMatchAsync(
        string? accountId,
        string? iban,
        string? deviceId,
        CancellationToken cancellationToken = default)
    {
        return await context.BlacklistedAccounts
            .Where(b => b.IsActive
                     && (b.ExpiresAt == null || b.ExpiresAt > DateTime.UtcNow)
                     && (
                         (accountId != null && b.AccountIdentifier == accountId && b.IdentifierType == BlacklistIdentifierType.AccountId) ||
                         (iban != null && b.AccountIdentifier == iban && b.IdentifierType == BlacklistIdentifierType.Iban) ||
                         (deviceId != null && b.AccountIdentifier == deviceId && b.IdentifierType == BlacklistIdentifierType.DeviceId)
                     ))
            .OrderByDescending(b => b.IncidentCount)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(BlacklistedAccount entry, CancellationToken cancellationToken = default)
    {
        await context.BlacklistedAccounts.AddAsync(entry, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BlacklistedAccount entry, CancellationToken cancellationToken = default)
    {
        context.BlacklistedAccounts.Update(entry);
        await context.SaveChangesAsync(cancellationToken);
    }
}
