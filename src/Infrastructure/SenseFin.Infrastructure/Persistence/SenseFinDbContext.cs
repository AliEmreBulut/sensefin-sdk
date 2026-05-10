using Microsoft.EntityFrameworkCore;
using SenseFin.Domain.Aggregates.RiskProfile;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Infrastructure.Persistence;

/// <summary>
/// Main EF Core DbContext for SenseFin.
/// Configured for PostgreSQL via Npgsql provider.
/// </summary>
public sealed class SenseFinDbContext : DbContext
{
    public DbSet<TransactionAggregate> Transactions => Set<TransactionAggregate>();
    public DbSet<RiskProfileAggregate> RiskProfiles => Set<RiskProfileAggregate>();

    public SenseFinDbContext(DbContextOptions<SenseFinDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration implementations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SenseFinDbContext).Assembly);
    }
}
