using SenseFin.Domain.Aggregates.RiskProfile;

namespace SenseFin.Application.Interfaces;

// Repository interface for the RiskProfile aggregate.
public interface IRiskProfileRepository
{
    Task<RiskProfileAggregate?> GetByAccountIdAsync(string accountId, CancellationToken cancellationToken = default);
    Task AddAsync(RiskProfileAggregate riskProfile, CancellationToken cancellationToken = default);
    Task UpdateAsync(RiskProfileAggregate riskProfile, CancellationToken cancellationToken = default);
}
