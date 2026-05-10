using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SenseFin.Domain.Aggregates.RiskProfile;

namespace SenseFin.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the RiskProfile aggregate.
/// RiskScoreEntry is mapped as an Owned Collection.
/// </summary>
public sealed class RiskProfileConfiguration : IEntityTypeConfiguration<RiskProfileAggregate>
{
    public void Configure(EntityTypeBuilder<RiskProfileAggregate> builder)
    {
        // ─── Table ───────────────────────────────────────────────
        builder.ToTable("RiskProfiles");

        // ─── Primary Key ─────────────────────────────────────────
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        // ─── Scalar Properties ───────────────────────────────────
        builder.Property(r => r.AccountId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(r => r.CurrentRiskLevel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.AverageRiskScore)
            .HasColumnType("double precision");

        builder.Property(r => r.TotalEvaluations);

        builder.Property(r => r.LastEvaluatedAt);

        // ─── Audit Fields ────────────────────────────────────────
        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt);

        // ─── RiskScores (Owned Collection) ───────────────────────
        builder.OwnsMany(r => r.RiskScores, score =>
        {
            score.ToTable("RiskScoreEntries");

            score.WithOwner().HasForeignKey("RiskProfileId");

            score.Property<int>("Id")
                .ValueGeneratedOnAdd();
            score.HasKey("Id");

            score.Property(s => s.Score)
                .HasColumnType("double precision")
                .IsRequired();

            score.Property(s => s.Source)
                .HasMaxLength(128)
                .IsRequired();

            score.Property(s => s.Reason)
                .HasMaxLength(1000);

            score.Property(s => s.EvaluatedAt)
                .IsRequired();

            score.Property(s => s.TransactionId)
                .IsRequired();

            // ─── Indexes ─────────────────────────────────────
            score.HasIndex("RiskProfileId")
                .HasDatabaseName("IX_RiskScoreEntries_RiskProfileId");

            score.HasIndex(s => s.TransactionId)
                .HasDatabaseName("IX_RiskScoreEntries_TransactionId");
        });

        // ─── Indexes ─────────────────────────────────────────────
        builder.HasIndex(r => r.AccountId)
            .IsUnique()
            .HasDatabaseName("IX_RiskProfiles_AccountId");

        // ─── Ignore DomainEvents (not persisted) ─────────────────
        builder.Ignore(r => r.DomainEvents);
    }
}
