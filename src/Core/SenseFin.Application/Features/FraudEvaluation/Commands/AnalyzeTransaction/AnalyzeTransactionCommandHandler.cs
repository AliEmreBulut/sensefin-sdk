using MediatR;
using Microsoft.Extensions.Logging;
using SenseFin.Application.Interfaces;
using SenseFin.Domain.Aggregates.RiskProfile;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Application.Features.FraudEvaluation.Commands.AnalyzeTransaction;

/// <summary>
/// Handles the AnalyzeTransactionCommand:
///   1. Creates a Transaction aggregate from the command data.
///   2. Persists the transaction.
///   3. Retrieves (or creates) the sender's RiskProfile.
///   4. Runs risk evaluation rules (skeleton) and records scores.
///   5. Returns the evaluation result.
/// </summary>
public sealed class AnalyzeTransactionCommandHandler
    : IRequestHandler<AnalyzeTransactionCommand, AnalyzeTransactionResult>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IRiskProfileRepository _riskProfileRepository;
    private readonly ILogger<AnalyzeTransactionCommandHandler> _logger;

    public AnalyzeTransactionCommandHandler(
        ITransactionRepository transactionRepository,
        IRiskProfileRepository riskProfileRepository,
        ILogger<AnalyzeTransactionCommandHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _riskProfileRepository = riskProfileRepository;
        _logger = logger;
    }

    public async Task<AnalyzeTransactionResult> Handle(
        AnalyzeTransactionCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting transaction analysis for account {SenderAccountId}, amount: {Amount} {Currency}",
            request.SenderAccountId, request.Amount, request.Currency);

        // ─── 1. Build the Transaction Aggregate ────────────────────────────

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
            money: money,
            transactionType: request.TransactionType,
            senderDeviceId: request.SenderDeviceId,
            senderAccountId: request.SenderAccountId,
            receiverAccountId: request.ReceiverAccountId,
            transactionDate: request.TransactionDate,
            senderIpAddress: request.SenderIpAddress,
            location: location,
            merchantId: request.MerchantId,
            description: request.Description);

        // ─── 2. Persist the Transaction ────────────────────────────────────

        await _transactionRepository.AddAsync(transaction, cancellationToken);

        _logger.LogInformation("Transaction {TransactionId} persisted.", transaction.Id);

        // ─── 3. Retrieve or Create the RiskProfile ────────────────────────

        var riskProfile = await _riskProfileRepository.GetByAccountIdAsync(
            request.SenderAccountId, cancellationToken);

        bool isNewProfile = false;
        if (riskProfile is null)
        {
            _logger.LogInformation(
                "No existing RiskProfile for account {AccountId}. Creating new profile.",
                request.SenderAccountId);

            riskProfile = RiskProfileAggregate.Create(request.SenderAccountId);
            isNewProfile = true;
        }

        // ─── 4. Risk Evaluation (Skeleton) ────────────────────────────────
        //
        //   TODO: Replace this placeholder with actual risk engine calls:
        //     - Rule-based engine (velocity checks, amount thresholds, geo-anomaly)
        //     - ML model scoring via IRiskScoringService
        //     - AI-powered behavioral analysis via IAiRiskAnalyzer
        //
        //   For now, we compute a simple heuristic score to demonstrate the flow.

        double riskScore = CalculatePreliminaryRiskScore(request);
        string riskSource = "PreliminaryHeuristicEngine";
        string? riskReason = BuildRiskReason(request, riskScore);

        var scoreEntry = RiskScoreEntry.Create(
            score: riskScore,
            source: riskSource,
            transactionId: transaction.Id,
            reason: riskReason);

        riskProfile.AddRiskScore(scoreEntry);

        // ─── 5. Persist the RiskProfile ───────────────────────────────────

        if (isNewProfile)
            await _riskProfileRepository.AddAsync(riskProfile, cancellationToken);
        else
            await _riskProfileRepository.UpdateAsync(riskProfile, cancellationToken);

        _logger.LogInformation(
            "RiskProfile {RiskProfileId} updated. Current level: {RiskLevel}, Average score: {AvgScore:F2}",
            riskProfile.Id, riskProfile.CurrentRiskLevel, riskProfile.AverageRiskScore);

        // ─── 6. Build & Return Result ─────────────────────────────────────

        bool isFlagged = riskProfile.CurrentRiskLevel >= RiskLevel.High;

        return new AnalyzeTransactionResult
        {
            TransactionId = transaction.Id,
            RiskProfileId = riskProfile.Id,
            RiskScore = riskScore,
            CurrentRiskLevel = riskProfile.CurrentRiskLevel,
            IsFlagged = isFlagged,
            EvaluationSummary = isFlagged
                ? $"⚠ Transaction flagged — Risk Level: {riskProfile.CurrentRiskLevel}, Score: {riskScore:F1}/100"
                : $"✓ Transaction cleared — Risk Level: {riskProfile.CurrentRiskLevel}, Score: {riskScore:F1}/100"
        };
    }

    // ────────────────── Placeholder Risk Heuristics ──────────────────

    /// <summary>
    /// Preliminary heuristic scoring — will be replaced by actual risk engines.
    /// </summary>
    private static double CalculatePreliminaryRiskScore(AnalyzeTransactionCommand request)
    {
        double score = 10; // Base score

        // High amount increases risk
        if (request.Amount > 10_000) score += 25;
        else if (request.Amount > 5_000) score += 15;
        else if (request.Amount > 1_000) score += 5;

        // Crypto transfers carry higher inherent risk
        if (request.TransactionType == TransactionType.CryptoTransfer)
            score += 20;

        // Wire transfers to unknown receivers
        if (request.TransactionType == TransactionType.WireTransfer)
            score += 10;

        // Missing location data is a weak signal
        if (!request.Latitude.HasValue || !request.Longitude.HasValue)
            score += 5;

        // Cap at 100
        return Math.Min(score, 100);
    }

    /// <summary>
    /// Builds a human-readable explanation of the risk score.
    /// </summary>
    private static string? BuildRiskReason(AnalyzeTransactionCommand request, double score)
    {
        var reasons = new List<string>();

        if (request.Amount > 10_000)
            reasons.Add($"High transaction amount ({request.Amount:N2} {request.Currency})");

        if (request.TransactionType == TransactionType.CryptoTransfer)
            reasons.Add("Crypto transfer channel");

        if (request.TransactionType == TransactionType.WireTransfer)
            reasons.Add("Wire transfer channel");

        if (!request.Latitude.HasValue)
            reasons.Add("Missing geolocation data");

        return reasons.Count > 0
            ? string.Join("; ", reasons)
            : null;
    }
}
