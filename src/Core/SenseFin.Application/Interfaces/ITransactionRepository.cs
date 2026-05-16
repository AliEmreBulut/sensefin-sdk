using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Application.Interfaces;

// Transaction agregası için repository arayüzü.
public interface ITransactionRepository
{
    Task<TransactionAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(TransactionAggregate transaction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionAggregate>> GetBySenderAccountAsync(string senderAccountId, CancellationToken cancellationToken = default);
}
