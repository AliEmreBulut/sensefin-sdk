using MediatR;
using Microsoft.Extensions.Logging;
using SenseFin.Application.Common;
using SenseFin.Application.Interfaces;
using SenseFin.Domain.Aggregates.Blacklist;
using SenseFin.Domain.Aggregates.RiskProfile;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Application.Features.FraudEvaluation.Commands;

public sealed class AnalyzeTransactionHandler(
    ITransactionRepository transactionRepository,
    IRiskProfileRepository riskProfileRepository,
    IRiskAnalystService riskAnalystService,
    IVelocityService velocityService,
    IBlacklistRepository blacklistRepository,
    IUnitOfWork unitOfWork,
    ILogger<AnalyzeTransactionHandler> logger)
    : IRequestHandler<AnalyzeTransactionCommand, Result<AnalyzeTransactionResponse>>
{
    private const int VelocityLimit = 5;
    private const decimal AiThresholdAmount = 3_000m;
    private const double AutoBlacklistThreshold = 95;

    public async Task<Result<AnalyzeTransactionResponse>> Handle(
        AnalyzeTransactionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. İşlemi Veritabanına Kaydet
        var money = Money.Create(request.Amount, request.Currency);
        GeoLocation? location = request.Latitude.HasValue && request.Longitude.HasValue
            ? GeoLocation.Create(request.Latitude.Value, request.Longitude.Value, request.Country, request.City)
            : null;

        var transaction = TransactionAggregate.Create(
            money, request.TransactionType, request.SenderDeviceId, request.SenderAccountId,
            request.ReceiverAccountId, request.TransactionDate, request.SenderIpAddress,
            location, request.MerchantId, request.Description, request.ReceiverIban,
            request.TypingScore, request.TremorScore);

        await transactionRepository.AddAsync(transaction, cancellationToken);
        logger.LogInformation("Transaction {TransactionId} persisted for account {AccountId}.", transaction.Id, request.SenderAccountId);

        // 2. Filtre Pipeline Katmanları (DRY ve Arrow Anti-Pattern Çözümü)
        double riskScore;
        string aiReason;

        var blacklistMatch = await blacklistRepository.FindAnyMatchAsync(
            accountId: request.ReceiverAccountId, iban: request.ReceiverIban, deviceId: null, cancellationToken: cancellationToken);

        if (blacklistMatch != null) // Katman 1: Alıcı Kara Liste Kontrolü
        {
            blacklistMatch.IncrementIncident($"Tx {transaction.Id}: {request.SenderAccountId} → {request.ReceiverAccountId}");
            await blacklistRepository.UpdateAsync(blacklistMatch, cancellationToken);

            riskScore = 100;
            aiReason = $"KARA LİSTE: Alıcı hesap ({blacklistMatch.AccountIdentifier}) kara listede. Sebep: {GetBlacklistReasonText(blacklistMatch.Reason)}. Toplam olay sayısı: {blacklistMatch.IncidentCount}.";
        }
        else if (await blacklistRepository.FindAnyMatchAsync(request.SenderAccountId, null, request.SenderDeviceId, cancellationToken) is { } senderBlacklist) // Katman 2: Gönderen Kara Liste Kontrolü
        {
            senderBlacklist.IncrementIncident($"Tx {transaction.Id}: Blacklisted sender attempted transaction");
            await blacklistRepository.UpdateAsync(senderBlacklist, cancellationToken);

            riskScore = 100;
            aiReason = $"KARA LİSTE: Gönderen ({senderBlacklist.AccountIdentifier}) kara listede. Sebep: {GetBlacklistReasonText(senderBlacklist.Reason)}.";
        }
        else if (await velocityService.IncrementAsync($"velocity:{request.SenderAccountId}", TimeSpan.FromMinutes(1)) is var currentCount && currentCount > VelocityLimit) // Katman 3: Redis Hız Kontrolü
        {
            logger.LogWarning("Velocity limit exceeded for account {AccountId}. Count: {Count}", request.SenderAccountId, currentCount);
            riskScore = 95;
            aiReason = $"Velocity limit exceeded: {currentCount} transactions in the last 60 seconds (limit: {VelocityLimit}).";
        }
        else if (!string.IsNullOrWhiteSpace(request.MerchantId)) // Katman 4: Güvenilir İşyeri Muafiyeti
        {
            riskScore = 5;
            aiReason = "Kayıtlı üye işyeri (Merchant) işlemi — güvenli kabul edildi.";
        }
        else // Katman 5: Çekirdek Risk Değerlendirme Motoru (Kurallar & Üretken Yapay Zeka)
        {
            var receiverProfile = await riskProfileRepository.GetByAccountIdAsync(request.ReceiverAccountId, cancellationToken);
            var isReceiverCorporate = receiverProfile?.IsCorporate ?? false;

            var descriptionFraudResult = DescriptionFraudStrategy.Analyze(request.Description, request.TransactionType, isReceiverCorporate);

            if (descriptionFraudResult.IsSuspicious && descriptionFraudResult.Confidence >= DescriptionFraudConfidence.High)
            {
                logger.LogWarning("Description fraud detected for account {AccountId}.", request.SenderAccountId);
                riskScore = descriptionFraudResult.RecommendedRiskScore;
                aiReason = descriptionFraudResult.Reason;
            }
            else if (request.Amount > AiThresholdAmount ||
                     request.TransactionType == TransactionType.PaymentRequest ||
                     descriptionFraudResult.IsSuspicious ||
                     (receiverProfile != null && receiverProfile.CurrentRiskLevel >= RiskLevel.Medium))
            {
                logger.LogInformation("Transaction requires AI analysis.");
                var receiverRiskContext = receiverProfile != null
                    ? $"Alıcı hesabın ({request.ReceiverAccountId}) mevcut risk seviyesi: {receiverProfile.CurrentRiskLevel}, Ortalama Risk Skoru: {receiverProfile.AverageRiskScore:F1}/100, Toplam Değerlendirme Sayısı: {receiverProfile.TotalEvaluations}, IBAN: {request.ReceiverIban ?? "N/A"}, Alıcı Hesap Türü: " + (receiverProfile.IsCorporate ? "KURUMSAL" : "BIREYSEL")
                    : $"Alıcı hesabın ({request.ReceiverAccountId}) risk profili bulunamadı. IBAN: {request.ReceiverIban ?? "N/A"}, Hesap Türü: BIREYSEL [varsayılan]";

                var analysisResult = await riskAnalystService.AnalyzeAsync(transaction, receiverRiskContext, cancellationToken);
                riskScore = analysisResult.Score * 100;
                aiReason = analysisResult.Reason;

                if (descriptionFraudResult.IsSuspicious && riskScore < descriptionFraudResult.RecommendedRiskScore)
                {
                    riskScore = descriptionFraudResult.RecommendedRiskScore;
                    aiReason += $" | Ek uyarı: {descriptionFraudResult.Reason}";
                }
            }
            else
            {
                riskScore = 10;
                aiReason = "Düşük tutarlı işlem — yapay zeka analizi olmadan temel risk skoru atanmıştır.";
            }
        }

        // 3. Risk Profilini Güncelle (Maddedeki Kritik Ternary Hatası Düzeltildi)
        var isPaymentRequestFraud = request.TransactionType == TransactionType.PaymentRequest && riskScore >= 60;
        var riskTargetAccountId = request.SenderAccountId; // Sadeleştirilmiş ve net işlem hedefi.

        var riskProfile = await riskProfileRepository.GetByAccountIdAsync(riskTargetAccountId, cancellationToken) 
                          ?? RiskProfileAggregate.Create(riskTargetAccountId);

        var entry = RiskScoreEntry.Create(riskScore, "SenseFin.Pipeline", transaction.Id, aiReason);
        riskProfile.AddRiskScore(entry);

        if (riskProfile.TotalEvaluations == 1)
            await riskProfileRepository.AddAsync(riskProfile, cancellationToken);
        else
            await riskProfileRepository.UpdateAsync(riskProfile, cancellationToken);

        // 4. Otomatik Kara Liste Tetikleyicisi
        var hasThreeCriticalRisks = riskProfile.RiskScores.Count(x => x.Score >= 80) >= 3;
        var hasHighRiskInLastFive = riskProfile.RiskScores.TakeLast(5).Count(x => x.Score >= 60) >= 5;

        if (riskScore >= AutoBlacklistThreshold || hasThreeCriticalRisks || hasHighRiskInLastFive)
        {
            var overrideReason = (hasThreeCriticalRisks || hasHighRiskInLastFive) && riskScore < AutoBlacklistThreshold
                ? BlacklistReason.RepeatedHighRisk
                : (BlacklistReason?)null;

            await TryAutoBlacklistAsync(request, transaction.Id, riskScore, overrideReason, cancellationToken);
        }

        // Tüm değişiklikleri tek bir işlem (Transaction) ve tek bir DB turunda kaydet
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Yanıtı Hazırla
        var transactionRiskLevel = riskScore switch
        {
            >= 80 => RiskLevel.Critical,
            >= 60 => RiskLevel.High,
            >= 40 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        return Result<AnalyzeTransactionResponse>.Success(new AnalyzeTransactionResponse(
            TransactionId: transaction.Id, RiskScore: riskScore, RiskLevel: transactionRiskLevel,
            AiReason: aiReason, IsHighRisk: transactionRiskLevel is RiskLevel.High or RiskLevel.Critical));
    }

    private async Task TryAutoBlacklistAsync(AnalyzeTransactionCommand request, Guid transactionId, double riskScore, BlacklistReason? overrideReason, CancellationToken cancellationToken)
    {
        var targetAccountId = request.TransactionType == TransactionType.PaymentRequest ? request.SenderAccountId : request.ReceiverAccountId;
        var reason = overrideReason ?? (request.TransactionType == TransactionType.PaymentRequest ? BlacklistReason.PaymentRequestScam : BlacklistReason.FraudConfirmed);

        var existing = await blacklistRepository.FindActiveAsync(targetAccountId, BlacklistIdentifierType.AccountId, cancellationToken);
        if (existing != null)
        {
            existing.IncrementIncident($"Tx {transactionId}: Risk {riskScore:F0}");
            await blacklistRepository.UpdateAsync(existing, cancellationToken);
        }
        else
        {
            var entry = BlacklistedAccount.Create(targetAccountId, BlacklistIdentifierType.AccountId, reason, "System.AutoBlacklist", $"Auto-blacklisted: Tx {transactionId}, Risk: {riskScore:F0}");
            await blacklistRepository.AddAsync(entry, cancellationToken);
        }
    }

    private static string GetBlacklistReasonText(BlacklistReason reason) => reason switch
    {
        BlacklistReason.FraudConfirmed => "Doğrulanmış dolandırıcılık",
        BlacklistReason.PaymentRequestScam => "Ödeme isteği dolandırıcılığı",
        BlacklistReason.IdentityTheft => "Kimlik hırsızlığı",
        BlacklistReason.MoneyLaundering => "Kara para aklama şüphesi",
        BlacklistReason.RepeatedHighRisk => "Tekrarlanan yüksek riskli işlemler",
        BlacklistReason.Phishing => "Oltalama (phishing) saldırısı",
        _ => "Bilinmeyen sebep"
    };
}