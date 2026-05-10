using Microsoft.EntityFrameworkCore;
using SenseFin.Application.Interfaces;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of ITransactionRepository.
/// </summary>
public sealed class TransactionRepository : ITransactionRepository
{
    private readonly SenseFinDbContext _dbContext;

    public TransactionRepository(SenseFinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TransactionAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task AddAsync(TransactionAggregate transaction, CancellationToken cancellationToken = default)
    {
        await _dbContext.Transactions.AddAsync(transaction, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionAggregate>> GetBySenderAccountAsync(
        string senderAccountId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .Where(t => t.SenderAccountId == senderAccountId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync(cancellationToken);
    }
}
