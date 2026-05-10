using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SenseFin.Domain.Common;

namespace SenseFin.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that automatically populates
/// CreatedAt and UpdatedAt audit fields on AggregateRoot entities.
/// </summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ApplyAuditFields(DbContext? context)
    {
        if (context is null)
            return;

        var utcNow = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<AggregateRoot>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(nameof(AggregateRoot.CreatedAt)).CurrentValue = utcNow;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(AggregateRoot.UpdatedAt)).CurrentValue = utcNow;
                    // Prevent overwriting the original CreatedAt
                    entry.Property(nameof(AggregateRoot.CreatedAt)).IsModified = false;
                    break;
            }
        }
    }
}
