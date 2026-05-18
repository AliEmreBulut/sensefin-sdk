using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SenseFin.Domain.Aggregates.RiskProfile;
using SenseFin.Domain.Aggregates.Transaction;
using SenseFin.Domain.Aggregates.Blacklist;

namespace SenseFin.Infrastructure.Persistence;

public static class SenseFinDataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SenseFinDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SenseFinDataSeeder");

        try
        {
            int retryCount = 0;
            const int maxRetries = 10;
            bool connected = false;

            while (!connected && retryCount < maxRetries)
            {
                try
                {
                    logger.LogInformation("Veritabanı migration'ları kontrol ediliyor... (Deneme {Count}/{Max})", retryCount + 1, maxRetries);
                    await context.Database.MigrateAsync();
                    connected = true;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        logger.LogError(ex, "Veritabanına bağlanılamadı ve migration'lar uygulanamadı. Maksimum deneme sayısı aşıldı.");
                        throw;
                    }
                    logger.LogWarning("Veritabanı henüz hazır değil veya bağlantı kurulamadı. 2 saniye sonra tekrar denenecek... Hata: {Message}", ex.Message);
                    await Task.Delay(2000);
                }
            }

            if (await context.RiskProfiles.AnyAsync())
            {
                logger.LogInformation("Veritabanında halihazırda veri var, seed işlemi atlanıyor.");
                return;
            }

            logger.LogInformation("Senaryo tabanlı kurumsal test verileri tohumlanıyor...");

            var accounts = new Dictionary<string, (bool IsCorporate, string Name)>
            {
                { "CORP_TRENDYOL", (true, "Trendyol Anonim Şirketi") },
                { "CORP_SHOPIER", (true, "Shopier Ödeme Hizmetleri") },
                { "PERS_CLEAN_AHMET", (false, "Ahmet Yılmaz (Sıradan Temiz Kullanıcı)") },
                { "PERS_VICTIM_MEHMET", (false, "Mehmet Demir (Hesap Ele Geçirme Mağduru)") },
                { "PERS_VICTIM_AYSE", (false, "Ayşe Kaya (Ödeme İsteği Oltalama Mağduru)") },
                { "PERS_FRAUD_ZEYNEP", (false, "Zeynep Arslan (Organize Dolandırıcı & Mule Account)") }
            };

            foreach (var acc in accounts)
            {
                var profile = RiskProfileAggregate.Create(acc.Key, acc.Value.IsCorporate);
                context.RiskProfiles.Add(profile);
            }
            await context.SaveChangesAsync();

            var baseDate = DateTime.UtcNow.AddDays(-30);
            int txIdCounter = 1;

            // ──────────────────────────────────────────────────────────────────────────
            // 🎬 HİKAYE 1: AHMET YILMAZ
            // ──────────────────────────────────────────────────────────────────────────
            for (int day = 1; day <= 20; day++)
            {
                var tx = TransactionAggregate.Create(
                    Money.Create(new Random().Next(150, 1200), "TRY"),
                    TransactionType.Transfer, "DEV_AHMET_PHONE", "PERS_CLEAN_AHMET", "CORP_TRENDYOL",
                    baseDate.AddDays(day).AddHours(14), "192.168.1.45", null, "CORP_TRENDYOL", 
                    $"Trendyol Sipariş Bedeli #{txIdCounter}", "TR560001000200030004000501", 12, 10
                );
                context.Transactions.Add(tx);

                var p = await context.RiskProfiles.FirstAsync(x => x.AccountId == "PERS_CLEAN_AHMET");
                p.AddRiskScore(RiskScoreEntry.Create(5, "SenseFinDataSeeder", tx.Id, "Kayıtlı üye işyeri (Merchant) işlemi — güvenli kabul edildi.", tx.Timestamp.AddMilliseconds(15)));
                txIdCounter++;
            }

            // ──────────────────────────────────────────────────────────────────────────
            // 🎬 HİKAYE 2: MEHMET DEMİR (HESAP ELE GEÇİRME)
            // ──────────────────────────────────────────────────────────────────────────
            for (int day = 1; day <= 15; day++)
            {
                var tx = TransactionAggregate.Create(
                    Money.Create(new Random().Next(100, 800), "TRY"),
                    TransactionType.Transfer, "DEV_MEHMET_PHONE", "PERS_VICTIM_MEHMET", "CORP_SHOPIER",
                    baseDate.AddDays(day).AddHours(10), "176.234.90.11", null, "CORP_SHOPIER", 
                    "E-Ticaret Alışverişi", "TR560001000200030004000502", 22, 15
                );
                context.Transactions.Add(tx);

                var p = await context.RiskProfiles.FirstAsync(x => x.AccountId == "PERS_VICTIM_MEHMET");
                p.AddRiskScore(RiskScoreEntry.Create(12, "SenseFinDataSeeder", tx.Id, "Düşük tutarlı ve rutin görünen işlem.", tx.Timestamp.AddMilliseconds(15)));
                txIdCounter++;
            }
            
            var atoTx = TransactionAggregate.Create(
                Money.Create(65000, "TRY"),
                TransactionType.Transfer, "DEV_UNKNOWN_BOT", "PERS_VICTIM_MEHMET", "PERS_FRAUD_ZEYNEP",
                baseDate.AddDays(16).AddHours(23), "185.15.20.10", null, null, 
                "Borç ödemesi acil kapat", "TR990000000000000000000099", 95, 90
            );
            context.Transactions.Add(atoTx);
            var pMehmet = await context.RiskProfiles.FirstAsync(x => x.AccountId == "PERS_VICTIM_MEHMET");
            pMehmet.AddRiskScore(RiskScoreEntry.Create(92, "SenseFinDataSeeder", atoTx.Id, "Yüksek cihaz anomali puanları ve farklı IP adresi tespiti — Hesap Ele Geçirme (Account Takeover) şüphesi.", atoTx.Timestamp.AddMilliseconds(12)));
            pMehmet.SetRiskLevel("Critical"); // Domain model method for Bug #2

            // ──────────────────────────────────────────────────────────────────────────
            // 🎬 HİKAYE 3: AYŞE KAYA & ZEYNEP ARSLAN (ÖDEME İSTEĞİ VE ŞİRKET TAKLİDİ)
            // ──────────────────────────────────────────────────────────────────────────
            var scamTx = TransactionAggregate.Create(
                Money.Create(4500, "TRY"),
                TransactionType.PaymentRequest, "DEV_ZEYNEP_ATTACK", "PERS_FRAUD_ZEYNEP", "PERS_VICTIM_AYSE",
                baseDate.AddDays(18).AddHours(11), "95.9.44.12", null, null, 
                "işlemi onayladığınızda para hesabınıza geçecek hediye iade", "TR990000000000000000000099", 85, 80
            );
            context.Transactions.Add(scamTx);
            var pZeynepScam = await context.RiskProfiles.FirstAsync(x => x.AccountId == "PERS_FRAUD_ZEYNEP");
            pZeynepScam.AddRiskScore(RiskScoreEntry.Create(98, "SenseFinDataSeeder", scamTx.Id, "⚠️ ÖDEME İSTEĞİ DOLANDIRICILIK TESPİTİ: Bu bir 'ödeme isteği' (payment request) işlemidir — onaylandığında para size GELMEZ, karşı tarafa GİDER.", scamTx.Timestamp.AddMilliseconds(18)));

            // impTx: Zeynep'in kurguladığı sahte mağaza tuzağı — Ahmet mağdur oldu.
            // Risk skoru alıcı/dolandırıcı olan PERS_FRAUD_ZEYNEP profiline işleniyor.
            var impTx = TransactionAggregate.Create(
                Money.Create(18900, "TRY"),
                TransactionType.Transfer, "DEV_ZEYNEP_ATTACK", "PERS_CLEAN_AHMET", "PERS_FRAUD_ZEYNEP",
                baseDate.AddDays(19).AddHours(16), "95.9.44.12", null, null, 
                "Sipariş No: 89452 fatura bedeli", "TR990000000000000000000099", 40, 30
            );
            context.Transactions.Add(impTx);
            
            // Şirket taklidi risk skoru → dolandırıcı Zeynep'in profiline.
            var pZeynepImp = await context.RiskProfiles.FirstAsync(x => x.AccountId == "PERS_FRAUD_ZEYNEP");
            pZeynepImp.AddRiskScore(RiskScoreEntry.Create(88, "SenseFinDataSeeder", impTx.Id, "⚠️ ŞİRKET TAKLİDİ TESPİTİ (Semantic Identity Mismatch): İşlem açıklamasında kurumsal/ticari ifadeler tespit edildi, ancak alıcı hesap bireysel şahıs hesabıdır.", impTx.Timestamp.AddMilliseconds(20)));

            await context.SaveChangesAsync();

            // ──────────────────────────────────────────────────────────────────────────
            // 🔒 3. ADIM: KARA LİSTE ENTEGRASYONU
            // ──────────────────────────────────────────────────────────────────────────
            logger.LogInformation("Suçlu hesaplar resmi kara listeye (Blacklist) işleniyor...");
            
            var blacklist1 = SenseFin.Domain.Aggregates.Blacklist.BlacklistedAccount.Create(
                "PERS_FRAUD_ZEYNEP", BlacklistIdentifierType.AccountId, BlacklistReason.FraudConfirmed, 
                "System.Seeder", "Organize dolandırıcılık, oltalama ve şirket taklidi tespiti nedeniyle hesap engellenmiştir.");
                
            var blacklist2 = SenseFin.Domain.Aggregates.Blacklist.BlacklistedAccount.Create(
                "TR990000000000000000000099", BlacklistIdentifierType.Iban, BlacklistReason.RepeatedHighRisk, 
                "System.Seeder", "Dolandırıcılık faaliyetlerinde fon toplamak için kullanılan şüpheli havuz IBAN bloğu.");

            context.BlacklistedAccounts.AddRange(blacklist1, blacklist2);
            await context.SaveChangesAsync();

            logger.LogInformation("Gerçekçi, senaryo bazlı ve kronolojik test verileri başarıyla yüklendi! Kupaya bir adım daha yaklaştık.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Veritabanı seed edilirken mimari bir hata oluştu.");
        }
    }
}