using Microsoft.EntityFrameworkCore;
using SenseFin.Application.Interfaces;
using SenseFin.Domain.Aggregates.RiskProfile;

namespace SenseFin.Infrastructure.Persistence.Repositories;

// IRiskProfileRepository arayüzünün EF Core implementasyonu.
public sealed class RiskProfileRepository : IRiskProfileRepository
{
    private readonly SenseFinDbContext _dbContext;

    public RiskProfileRepository(SenseFinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RiskProfileAggregate?> GetByAccountIdAsync(
        string accountId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RiskProfiles
            .Include(r => r.RiskScores)
            .FirstOrDefaultAsync(r => r.AccountId == accountId, cancellationToken);
    }

    public async Task AddAsync(RiskProfileAggregate riskProfile, CancellationToken cancellationToken = default)
    {
        await _dbContext.RiskProfiles.AddAsync(riskProfile, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RiskProfileAggregate riskProfile, CancellationToken cancellationToken = default)
    {
        _dbContext.RiskProfiles.Update(riskProfile);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
