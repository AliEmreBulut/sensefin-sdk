using SenseFin.Domain.Aggregates.RiskProfile;

namespace SenseFin.Application.Interfaces;

/// <summary>
/// Repository interface for the RiskProfile aggregate.
/// </summary>
public interface IRiskProfileRepository
{
    Task<RiskProfileAggregate?> GetByAccountIdAsync(string accountId, CancellationToken cancellationToken = default);
    Task AddAsync(RiskProfileAggregate riskProfile, CancellationToken cancellationToken = default);
    Task UpdateAsync(RiskProfileAggregate riskProfile, CancellationToken cancellationToken = default);
}
