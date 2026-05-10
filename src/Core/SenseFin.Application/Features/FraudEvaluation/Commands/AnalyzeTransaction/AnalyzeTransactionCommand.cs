using MediatR;
using SenseFin.Domain.Aggregates.Transaction;

namespace SenseFin.Application.Features.FraudEvaluation.Commands.AnalyzeTransaction;

/// <summary>
/// Command to analyze a financial transaction for fraud risk.
/// This is the primary entry point for the risk evaluation pipeline.
/// </summary>
public sealed record AnalyzeTransactionCommand : IRequest<AnalyzeTransactionResult>
{
    // ────────────────── Core Fields ──────────────────

    /// <summary>Transaction amount.</summary>
    public required decimal Amount { get; init; }

    /// <summary>ISO 4217 currency code (e.g., USD, EUR, TRY).</summary>
    public required string Currency { get; init; }

    /// <summary>Type/channel of the transaction.</summary>
    public required TransactionType TransactionType { get; init; }

    // ────────────────── Device & Session ──────────────────

    /// <summary>Unique identifier of the sender's device.</summary>
    public required string SenderDeviceId { get; init; }

    /// <summary>IP address of the sender, if available.</summary>
    public string? SenderIpAddress { get; init; }

    // ────────────────── Account References ──────────────────

    /// <summary>Sender account identifier.</summary>
    public required string SenderAccountId { get; init; }

    /// <summary>Receiver account identifier.</summary>
    public required string ReceiverAccountId { get; init; }

    // ────────────────── Location ──────────────────

    /// <summary>Latitude of the transaction origin.</summary>
    public double? Latitude { get; init; }

    /// <summary>Longitude of the transaction origin.</summary>
    public double? Longitude { get; init; }

    /// <summary>Country of the transaction origin.</summary>
    public string? Country { get; init; }

    /// <summary>City of the transaction origin.</summary>
    public string? City { get; init; }

    // ────────────────── Metadata ──────────────────

    /// <summary>Timestamp of the transaction.</summary>
    public required DateTime TransactionDate { get; init; }

    /// <summary>Optional merchant identifier.</summary>
    public string? MerchantId { get; init; }

    /// <summary>Optional description/reference.</summary>
    public string? Description { get; init; }
}
