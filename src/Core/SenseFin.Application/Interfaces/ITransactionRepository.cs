using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Application.Interfaces;

/// <summary>
/// Repository interface for the Transaction aggregate.
/// </summary>
public interface ITransactionRepository
{
    Task<TransactionAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(TransactionAggregate transaction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionAggregate>> GetBySenderAccountAsync(string senderAccountId, CancellationToken cancellationToken = default);
}
