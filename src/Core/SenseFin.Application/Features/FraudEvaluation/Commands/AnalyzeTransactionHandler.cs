using MediatR;
using Microsoft.Extensions.Logging;
using SenseFin.Application.Common;
using SenseFin.Application.Interfaces;
using SenseFin.Domain.Aggregates.Blacklist;
using SenseFin.Domain.Aggregates.RiskProfile;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Application.Features.FraudEvaluation.Commands;

// İşlem analizi ana merkezi. Blacklist, limitler, açıklama kontrolü ve AI analizini sırayla koşturur.
public sealed class AnalyzeTransactionHandler(
    ITransactionRepository transactionRepository,
    IRiskProfileRepository riskProfileRepository,
    IRiskAnalystService riskAnalystService,
    IVelocityService velocityService,
    IBlacklistRepository blacklistRepository,
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
        // Önce işlemi kaydet

        var money = Money.Create(request.Amount, request.Currency);

        GeoLocation? location = null;
        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            location = GeoLocation.Create(
                request.Latitude.Value,
                request.Longitude.Value,
                request.Country,
                request.City);
        }

        var transaction = TransactionAggregate.Create(
            money,
            request.TransactionType,
            request.SenderDeviceId,
            request.SenderAccountId,
            request.ReceiverAccountId,
            request.TransactionDate,
            request.SenderIpAddress,
            location,
            request.MerchantId,
            request.Description,
            request.ReceiverIban,
            request.TypingScore,
            request.TremorScore);

        await transactionRepository.AddAsync(transaction, cancellationToken);
        logger.LogInformation("Transaction {TransactionId} persisted for account {AccountId}.",
            transaction.Id, request.SenderAccountId);

        // Kara liste kontrolü

        double riskScore;
        string aiReason;

        var blacklistMatch = await blacklistRepository.FindAnyMatchAsync(
            accountId: request.ReceiverAccountId,
            iban: request.ReceiverIban,
            deviceId: null,
            cancellationToken: cancellationToken);

        if (blacklistMatch != null)
        {
            logger.LogWarning(
                "BLACKLIST HIT — Receiver {Identifier} (Type: {Type}) is blacklisted. Reason: {Reason}, Incidents: {Count}",
                blacklistMatch.AccountIdentifier, blacklistMatch.IdentifierType,
                blacklistMatch.Reason, blacklistMatch.IncidentCount);

            // Increment incident count
            blacklistMatch.IncrementIncident(
                $"Tx {transaction.Id}: {request.SenderAccountId} → {request.ReceiverAccountId}");
            await blacklistRepository.UpdateAsync(blacklistMatch, cancellationToken);

            riskScore = 100;
            aiReason = $"KARA LİSTE: Alıcı hesap ({blacklistMatch.AccountIdentifier}) kara listede. " +
                       $"Sebep: {GetBlacklistReasonText(blacklistMatch.Reason)}. " +
                       $"Toplam olay sayısı: {blacklistMatch.IncidentCount}.";
        }
        else
        {
            // Gönderen cihazda da sıkıntı var mı diye bak
            var senderBlacklist = await blacklistRepository.FindAnyMatchAsync(
                accountId: request.SenderAccountId,
                iban: null,
                deviceId: request.SenderDeviceId,
                cancellationToken: cancellationToken);

            if (senderBlacklist != null)
            {
                logger.LogWarning(
                    "BLACKLIST HIT — Sender {Identifier} (Type: {Type}) is blacklisted. Reason: {Reason}",
                    senderBlacklist.AccountIdentifier, senderBlacklist.IdentifierType, senderBlacklist.Reason);

                senderBlacklist.IncrementIncident(
                    $"Tx {transaction.Id}: Blacklisted sender attempted transaction");
                await blacklistRepository.UpdateAsync(senderBlacklist, cancellationToken);

                riskScore = 100;
                aiReason = $"KARA LİSTE: Gönderen ({senderBlacklist.AccountIdentifier}) kara listede. " +
                           $"Sebep: {GetBlacklistReasonText(senderBlacklist.Reason)}.";
            }
            else
            {
                // Limit (Velocity) kontrolü - Redis üzerinden

                var velocityKey = $"velocity:{request.SenderAccountId}";
                var currentCount = await velocityService.IncrementAsync(velocityKey, TimeSpan.FromMinutes(1));

                if (currentCount > VelocityLimit)
                {
                    logger.LogWarning("Velocity limit exceeded for account {AccountId}. Count: {Count}",
                        request.SenderAccountId, currentCount);

                    riskScore = 95;
                    aiReason = $"Velocity limit exceeded: {currentCount} transactions in the last 60 seconds (limit: {VelocityLimit}).";
                }
                else
                {
                    // Açıklamada dolandırıcılık kelimeleri var mı?

                    var descriptionFraudResult = DescriptionFraudStrategy.Analyze(
                        request.Description, request.TransactionType);

                    if (descriptionFraudResult.IsSuspicious &&
                        descriptionFraudResult.Confidence >= DescriptionFraudConfidence.High)
                    {
                        logger.LogWarning(
                            "Description fraud detected for account {AccountId}. " +
                            "Confidence: {Confidence}, Patterns: [{Patterns}]",
                            request.SenderAccountId,
                            descriptionFraudResult.Confidence,
                            string.Join(", ", descriptionFraudResult.MatchedPatterns));

                        riskScore = descriptionFraudResult.RecommendedRiskScore;
                        aiReason = descriptionFraudResult.Reason;
                    }
                    else
                    {
                        // Alıcı IBAN'ı riskli mi?

                        string? receiverRiskContext = null;
                        var receiverProfile = await riskProfileRepository.GetByAccountIdAsync(
                            request.ReceiverAccountId, cancellationToken);

                        if (receiverProfile != null && receiverProfile.CurrentRiskLevel == RiskLevel.Critical)
                        {
                            logger.LogWarning(
                                "Receiver {ReceiverAccountId} (IBAN: {ReceiverIban}) is flagged as CRITICAL. Auto-assigning 100% risk.",
                                request.ReceiverAccountId, request.ReceiverIban);

                            riskScore = 100;
                            aiReason = $"Alıcı hesabı ({request.ReceiverAccountId}, IBAN: {request.ReceiverIban ?? "N/A"}) " +
                                       $"sistemde KRİTİK risk seviyesinde işaretlenmiştir (Ortalama Risk: {receiverProfile.AverageRiskScore:F1}, " +
                                       $"Toplam Değerlendirme: {receiverProfile.TotalEvaluations}). İşlem otomatik olarak reddedilmiştir.";
                        }
                        // Duruma göre AI analizine yolla (tutar yüksekse veya şüphe varsa)
                        else if (request.Amount > AiThresholdAmount ||
                                 request.TransactionType == TransactionType.PaymentRequest ||
                                 descriptionFraudResult.IsSuspicious)
                        {
                            logger.LogInformation(
                                "Transaction requires AI analysis — Amount: {Amount} {Currency}, Type: {Type}, DescriptionSuspicious: {Suspicious}",
                                request.Amount, request.Currency, request.TransactionType, descriptionFraudResult.IsSuspicious);

                            if (receiverProfile != null)
                            {
                                receiverRiskContext = $"Alıcı hesabın ({request.ReceiverAccountId}) mevcut risk seviyesi: {receiverProfile.CurrentRiskLevel}, " +
                                                      $"Ortalama Risk Skoru: {receiverProfile.AverageRiskScore:F1}/100, " +
                                                      $"Toplam Değerlendirme Sayısı: {receiverProfile.TotalEvaluations}, " +
                                                      $"IBAN: {request.ReceiverIban ?? "N/A"}";
                            }

                            var analysisResult = await riskAnalystService.AnalyzeAsync(transaction, receiverRiskContext, cancellationToken);

                            riskScore = analysisResult.Score * 100;
                            aiReason = analysisResult.Reason;

                            if (descriptionFraudResult.IsSuspicious && riskScore < descriptionFraudResult.RecommendedRiskScore)
                            {
                                logger.LogInformation(
                                    "Applying description fraud risk floor: AI score {AiScore} → floor {Floor}",
                                    riskScore, descriptionFraudResult.RecommendedRiskScore);
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
                }
            }
        }

        // Risk profilini güncelle

        var isPaymentRequestFraud = request.TransactionType == TransactionType.PaymentRequest
                                    && riskScore >= 60;

        var riskTargetAccountId = isPaymentRequestFraud
            ? request.ReceiverAccountId
            : request.SenderAccountId;

        var riskProfile = await riskProfileRepository.GetByAccountIdAsync(
            riskTargetAccountId, cancellationToken);

        if (riskProfile is null)
        {
            riskProfile = RiskProfileAggregate.Create(riskTargetAccountId);
            var entry = RiskScoreEntry.Create(riskScore, "SenseFin.Pipeline", transaction.Id, aiReason);
            riskProfile.AddRiskScore(entry);
            await riskProfileRepository.AddAsync(riskProfile, cancellationToken);
        }
        else
        {
            var entry = RiskScoreEntry.Create(riskScore, "SenseFin.Pipeline", transaction.Id, aiReason);
            riskProfile.AddRiskScore(entry);
            await riskProfileRepository.UpdateAsync(riskProfile, cancellationToken);
        }

        logger.LogInformation(
            "Risk profile updated for {TargetType} account {AccountId}. Score: {Score}, Level: {Level}",
            isPaymentRequestFraud ? "RECEIVER (scammer)" : "SENDER",
            riskTargetAccountId, riskScore, riskProfile.CurrentRiskLevel);

        // Çok yüksek riskliyse direkt kara listeye al

        if (riskScore >= AutoBlacklistThreshold)
        {
            await TryAutoBlacklistAsync(request, transaction.Id, riskScore, cancellationToken);
        }

        // Response oluştur

        var transactionRiskLevel = riskScore switch
        {
            >= 80 => RiskLevel.Critical,
            >= 60 => RiskLevel.High,
            >= 40 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        // Generate user-friendly message for the end user
        var userFriendlyMessage = GenerateUserFriendlyMessage(
            transactionRiskLevel, riskScore, request.TransactionType, request.Amount, request.Currency, aiReason);

        var response = new AnalyzeTransactionResponse(
            TransactionId: transaction.Id,
            RiskScore: riskScore,
            RiskLevel: transactionRiskLevel,
            AiReason: aiReason,
            UserFriendlyMessage: userFriendlyMessage,
            IsHighRisk: transactionRiskLevel is RiskLevel.High or RiskLevel.Critical);

        return Result<AnalyzeTransactionResponse>.Success(response);
    }

    // Otomatik kara liste mantığı

    // Emin olduğumuz dolandırıcıları direkt içeri alıyoruz
    private async Task TryAutoBlacklistAsync(
        AnalyzeTransactionCommand request,
        Guid transactionId,
        double riskScore,
        CancellationToken cancellationToken)
    {
        var targetAccountId = request.TransactionType == TransactionType.PaymentRequest
            ? request.ReceiverAccountId
            : request.SenderAccountId;

        var reason = request.TransactionType == TransactionType.PaymentRequest
            ? BlacklistReason.PaymentRequestScam
            : BlacklistReason.FraudConfirmed;

        // Check if already blacklisted
        var existing = await blacklistRepository.FindActiveAsync(
            targetAccountId, BlacklistIdentifierType.AccountId, cancellationToken);

        if (existing != null)
        {
            existing.IncrementIncident($"Tx {transactionId}: Risk {riskScore:F0}");
            await blacklistRepository.UpdateAsync(existing, cancellationToken);
            logger.LogWarning("Blacklist entry updated for {AccountId}. Incidents: {Count}",
                targetAccountId, existing.IncidentCount);
        }
        else
        {
            var entry = BlacklistedAccount.Create(
                accountIdentifier: targetAccountId,
                identifierType: BlacklistIdentifierType.AccountId,
                reason: reason,
                addedBy: "System.AutoBlacklist",
                description: $"Auto-blacklisted: Tx {transactionId}, Risk: {riskScore:F0}");
            await blacklistRepository.AddAsync(entry, cancellationToken);
            logger.LogWarning("Account {AccountId} AUTO-BLACKLISTED. Reason: {Reason}",
                targetAccountId, reason);
        }

        // Also blacklist IBAN if available and it's a payment request scam
        if (!string.IsNullOrWhiteSpace(request.ReceiverIban) &&
            request.TransactionType == TransactionType.PaymentRequest)
        {
            var ibanExists = await blacklistRepository.FindActiveAsync(
                request.ReceiverIban, BlacklistIdentifierType.Iban, cancellationToken);

            if (ibanExists != null)
            {
                ibanExists.IncrementIncident($"Tx {transactionId}: Risk {riskScore:F0}");
                await blacklistRepository.UpdateAsync(ibanExists, cancellationToken);
            }
            else
            {
                var ibanEntry = BlacklistedAccount.Create(
                    accountIdentifier: request.ReceiverIban,
                    identifierType: BlacklistIdentifierType.Iban,
                    reason: BlacklistReason.PaymentRequestScam,
                    addedBy: "System.AutoBlacklist",
                    description: $"Auto-blacklisted IBAN: Tx {transactionId}, Account: {targetAccountId}");
                await blacklistRepository.AddAsync(ibanEntry, cancellationToken);
                logger.LogWarning("IBAN {Iban} AUTO-BLACKLISTED.", request.ReceiverIban);
            }
        }
    }

    // Kullanıcıya gösterilecek uyarı mesajları

    // Teknik olmayan kullanıcılar için anlaşılır dilden mesaj üretir
    private static string GenerateUserFriendlyMessage(
        RiskLevel riskLevel,
        double riskScore,
        TransactionType transactionType,
        decimal amount,
        string currency,
        string aiReason)
    {
        string txTypeName = transactionType == TransactionType.PaymentRequest ? "ödeme isteği" : "işlem";

        // Clean up the reason text to make it flow better in the sentence
        string cleanReason = aiReason
            .Replace("KARA LİSTE: ", "")
            .Replace("🚨 ŞÜPHELİ AÇIKLAMA TESPİTİ: ", "")
            .Replace("🚨 ÖDEME İSTEĞİ DOLANDIRICILIK TESPİTİ: ", "")
            .Replace("?? ŞÜPHELİ AÇIKLAMA TESPİTİ: ", "")
            .Replace("?? ÖDEME İSTEĞİ DOLANDIRICILIK TESPİTİ: ", "")
            .Trim();

        if (riskLevel is RiskLevel.Critical or RiskLevel.High)
        {
            return $"🚨 DİKKAT! Bu {txTypeName}, sistem tarafından tespit edilen riskler ({cleanReason}) nedeniyle %{riskScore:F0} dolandırıcılık ihtimali taşımaktadır. İşlemi yapmadan önce lütfen dikkatli olunuz.";
        }
        else if (riskLevel == RiskLevel.Medium)
        {
            return $"⚠️ Bu {txTypeName} %{riskScore:F0} oranında risk taşıyor. Lütfen işlemi onaylamadan önce karşı tarafı tanıdığınızdan kesinlikle emin olun.";
        }
        else
        {
            return $"✅ Bu {txTypeName} güvenli görünüyor (Risk: %{riskScore:F0}). İşleme devam edebilirsiniz.";
        }
    }

    // Yardımcı metodlar

    private static string GetBlacklistReasonText(BlacklistReason reason) => reason switch
    {
        BlacklistReason.FraudConfirmed => "Doğrulanmış dolandırıcılık",
        BlacklistReason.PaymentRequestScam => "Ödeme isteği dolandırıcılığı",
        BlacklistReason.IdentityTheft => "Kimlik hırsızlığı",
        BlacklistReason.MoneyLaundering => "Kara para aklama şüphesi",
        BlacklistReason.RepeatedHighRisk => "Tekrarlanan yüksek riskli işlemler",
        BlacklistReason.Phishing => "Oltalama (phishing) saldırısı",
        BlacklistReason.ManualReport => "Manuel ihbar/rapor",
        _ => "Bilinmeyen sebep"
    };
}
