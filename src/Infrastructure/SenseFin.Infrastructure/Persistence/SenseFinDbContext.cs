using Microsoft.EntityFrameworkCore;
using SenseFin.Domain.Aggregates.Blacklist;
using SenseFin.Domain.Aggregates.RiskProfile;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Infrastructure.Persistence;

// SenseFin ana veritabanı bağlamı (PostgreSQL).
public sealed class SenseFinDbContext : DbContext
{
    public DbSet<TransactionAggregate> Transactions => Set<TransactionAggregate>();
    public DbSet<RiskProfileAggregate> RiskProfiles => Set<RiskProfileAggregate>();
    public DbSet<BlacklistedAccount> BlacklistedAccounts => Set<BlacklistedAccount>();

    public SenseFinDbContext(DbContextOptions<SenseFinDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Bu assembly içindeki tüm konfigürasyonları uygula
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SenseFinDbContext).Assembly);
    }
}
