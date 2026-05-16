using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SenseFin.Domain.Aggregates.Blacklist;

namespace SenseFin.Infrastructure.Persistence.Configurations;

// BlacklistedAccount varlığı için EF Core konfigürasyonu.
public sealed class BlacklistedAccountConfiguration : IEntityTypeConfiguration<BlacklistedAccount>
{
    public void Configure(EntityTypeBuilder<BlacklistedAccount> builder)
    {
        builder.ToTable("BlacklistedAccounts");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .ValueGeneratedNever();

        builder.Property(b => b.AccountIdentifier)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(b => b.IdentifierType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(b => b.Reason)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(b => b.Description)
            .HasMaxLength(1000);

        builder.Property(b => b.AddedBy)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(b => b.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        builder.Property(b => b.UpdatedAt);

        builder.Property(b => b.ExpiresAt);

        builder.Property(b => b.IncidentCount)
            .HasDefaultValue(1);

        // İndeksler
        builder.HasIndex(b => new { b.AccountIdentifier, b.IdentifierType })
            .HasDatabaseName("IX_BlacklistedAccounts_Identifier");

        builder.HasIndex(b => b.IsActive)
            .HasDatabaseName("IX_BlacklistedAccounts_IsActive");

        builder.HasIndex(b => b.Reason)
            .HasDatabaseName("IX_BlacklistedAccounts_Reason");
    }
}
