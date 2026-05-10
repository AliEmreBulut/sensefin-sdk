using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Transaction aggregate.
/// Money and GeoLocation are mapped as Owned Entities.
/// </summary>
public sealed class TransactionConfiguration : IEntityTypeConfiguration<TransactionAggregate>
{
    public void Configure(EntityTypeBuilder<TransactionAggregate> builder)
    {
        // ─── Table ───────────────────────────────────────────────
        builder.ToTable("Transactions");

        // ─── Primary Key ─────────────────────────────────────────
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        // ─── Money (Owned Entity) ────────────────────────────────
        builder.OwnsOne(t => t.Money, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("Amount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3)
                .IsUnicode(false)
                .IsRequired();
        });

        // ─── GeoLocation (Owned Entity, optional) ────────────────
        builder.OwnsOne(t => t.Location, location =>
        {
            location.Property(l => l.Latitude)
                .HasColumnName("Latitude")
                .HasColumnType("double precision");

            location.Property(l => l.Longitude)
                .HasColumnName("Longitude")
                .HasColumnType("double precision");

            location.Property(l => l.Country)
                .HasColumnName("LocationCountry")
                .HasMaxLength(100);

            location.Property(l => l.City)
                .HasColumnName("LocationCity")
                .HasMaxLength(200);
        });

        // ─── Scalar Properties ───────────────────────────────────
        builder.Property(t => t.TransactionType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.SenderDeviceId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.SenderIpAddress)
            .HasMaxLength(45); // IPv6 max length

        builder.Property(t => t.SenderAccountId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(t => t.ReceiverAccountId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(t => t.TransactionDate)
            .IsRequired();

        builder.Property(t => t.MerchantId)
            .HasMaxLength(128);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        // ─── Audit Fields ────────────────────────────────────────
        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt);

        // ─── Indexes ─────────────────────────────────────────────
        builder.HasIndex(t => t.SenderAccountId)
            .HasDatabaseName("IX_Transactions_SenderAccountId");

        builder.HasIndex(t => t.ReceiverAccountId)
            .HasDatabaseName("IX_Transactions_ReceiverAccountId");

        builder.HasIndex(t => t.TransactionDate)
            .HasDatabaseName("IX_Transactions_TransactionDate");

        // ─── Ignore DomainEvents (not persisted) ─────────────────
        builder.Ignore(t => t.DomainEvents);
    }
}
