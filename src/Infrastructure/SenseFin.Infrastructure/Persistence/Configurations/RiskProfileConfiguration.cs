using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SenseFin.Domain.Aggregates.RiskProfile;

namespace SenseFin.Infrastructure.Persistence.Configurations;

// RiskProfile agregası için EF Core konfigürasyonu.
public sealed class RiskProfileConfiguration : IEntityTypeConfiguration<RiskProfileAggregate>
{
    public void Configure(EntityTypeBuilder<RiskProfileAggregate> builder)
    {
        // Tablo adı
        builder.ToTable("RiskProfiles");

        // PK
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        // Özellikler
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

        builder.Property(r => r.IsCorporate)
            .IsRequired()
            .HasDefaultValue(false);

        // Denetim alanları
        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt);

        // Risk skorları (Owned collection olarak tutulur)
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

            // İndeksler
            score.HasIndex("RiskProfileId")
                .HasDatabaseName("IX_RiskScoreEntries_RiskProfileId");

            score.HasIndex(s => s.TransactionId)
                .HasDatabaseName("IX_RiskScoreEntries_TransactionId");
        });

        // İndeksler
        builder.HasIndex(r => r.AccountId)
            .IsUnique()
            .HasDatabaseName("IX_RiskProfiles_AccountId");

        // Domain event'leri veritabanına yansıtma
        builder.Ignore(r => r.DomainEvents);
    }
}
