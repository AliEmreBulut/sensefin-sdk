using MediatR;
using Microsoft.Extensions.Logging;
using SenseFin.Application.Common;
using SenseFin.Application.Interfaces;
using SenseFin.Domain.Aggregates.RiskProfile;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Application.Features.FraudEvaluation.Commands;

/// <summary>
/// Handles the full fraud evaluation pipeline for an incoming transaction:
///   1) Persist the transaction
///   2) Velocity check via Redis
///   3) AI analysis for high-value transactions
///   4) Update risk profile aggregate
/// </summary>
public sealed class AnalyzeTransactionHandler(
    ITransactionRepository transactionRepository,
    IRiskProfileRepository riskProfileRepository,
    IRiskAnalystService riskAnalystService,
    IVelocityService velocityService,
    ILogger<AnalyzeTransactionHandler> logger)
    : IRequestHandler<AnalyzeTransactionCommand, Result<AnalyzeTransactionResponse>>
{
    private const int VelocityLimit = 5;
    private const decimal AiThresholdAmount = 5_000m;

    public async Task<Result<AnalyzeTransactionResponse>> Handle(
        AnalyzeTransactionCommand request,
        CancellationToken cancellationToken)
    {
        // ────────────────── Step 1: Map to Domain & Persist ──────────────────

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

        // ────────────────── Step 2: Velocity Check (Redis) ──────────────────

        double riskScore;
        string aiReason;

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
            // ────────────────── Step 3: AI Analysis (Conditional) ──────────────────

            if (request.Amount > AiThresholdAmount)
            {
                logger.LogInformation(
                    "High-value transaction ({Amount} {Currency}) — invoking AI risk analysis.",
                    request.Amount, request.Currency);

                var analysisResult = await riskAnalystService.AnalyzeAsync(transaction, cancellationToken);

                // Gemini returns 0.0–1.0, domain uses 0–100
                riskScore = analysisResult.Score * 100;
                aiReason = analysisResult.Reason;
            }
            else
            {
                // Low-value transactions get a baseline score without AI overhead
                riskScore = 10;
                aiReason = "Low-value transaction — baseline risk score assigned without AI analysis.";
            }
        }

        // ────────────────── Step 4: Update Risk Profile ──────────────────

        var riskProfile = await riskProfileRepository.GetByAccountIdAsync(
            request.SenderAccountId, cancellationToken);

        if (riskProfile is null)
        {
            riskProfile = RiskProfileAggregate.Create(request.SenderAccountId);
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
            "Risk profile updated for account {AccountId}. Score: {Score}, Level: {Level}",
            request.SenderAccountId, riskScore, riskProfile.CurrentRiskLevel);

        // ────────────────── Build Response ──────────────────

        var response = new AnalyzeTransactionResponse(
            TransactionId: transaction.Id,
            RiskScore: riskScore,
            RiskLevel: riskProfile.CurrentRiskLevel,
            AiReason: aiReason,
            IsHighRisk: riskScore > 60);

        return Result<AnalyzeTransactionResponse>.Success(response);
    }
}
