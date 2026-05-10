using SenseFin.Domain.Common;

namespace SenseFin.Domain.Aggregates.Transaction;

/// <summary>
/// Transaction Aggregate Root — represents a single financial transaction
/// submitted for fraud analysis. This is the primary entry point for the
/// risk evaluation pipeline.
/// </summary>
public sealed class TransactionAggregate : AggregateRoot
{
    // ────────────────── Core Fields ──────────────────

    /// <summary>Monetary amount and currency of the transaction.</summary>
    public Money Money { get; private set; } = null!;

    /// <summary>Type/channel of the transaction (e.g., WireTransfer, CardPayment).</summary>
    public TransactionType TransactionType { get; private set; }

    // ────────────────── Device & Session ──────────────────

    /// <summary>Unique identifier of the sender's device (fingerprint or device ID).</summary>
    public string SenderDeviceId { get; private set; } = null!;

    /// <summary>IP address from which the transaction originated.</summary>
    public string? SenderIpAddress { get; private set; }

    // ────────────────── Account References ──────────────────

    /// <summary>Unique account ID of the sender.</summary>
    public string SenderAccountId { get; private set; } = null!;

    /// <summary>Unique account ID of the receiver.</summary>
    public string ReceiverAccountId { get; private set; } = null!;

    // ────────────────── Location ──────────────────

    /// <summary>Geographic location of the transaction, if available.</summary>
    public GeoLocation? Location { get; private set; }

    // ────────────────── Timestamps ──────────────────

    /// <summary>Timestamp when the transaction was initiated by the end user.</summary>
    public DateTime TransactionDate { get; private set; }

    // ────────────────── Metadata ──────────────────

    /// <summary>Optional merchant/vendor identifier for POS or online purchases.</summary>
    public string? MerchantId { get; private set; }

    /// <summary>Free-form description or reference note.</summary>
    public string? Description { get; private set; }

    // ────────────────── Constructor ──────────────────

    /// <summary>EF Core / serialization constructor.</summary>
    private TransactionAggregate() { }

    /// <summary>
    /// Factory method to create a new Transaction aggregate.
    /// </summary>
    public static TransactionAggregate Create(
        Money money,
        TransactionType transactionType,
        string senderDeviceId,
        string senderAccountId,
        string receiverAccountId,
        DateTime transactionDate,
        string? senderIpAddress = null,
        GeoLocation? location = null,
        string? merchantId = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(senderDeviceId))
            throw new ArgumentException("SenderDeviceId is required.", nameof(senderDeviceId));

        if (string.IsNullOrWhiteSpace(senderAccountId))
            throw new ArgumentException("SenderAccountId is required.", nameof(senderAccountId));

        if (string.IsNullOrWhiteSpace(receiverAccountId))
            throw new ArgumentException("ReceiverAccountId is required.", nameof(receiverAccountId));

        var transaction = new TransactionAggregate
        {
            Id = Guid.NewGuid(),
            Money = money,
            TransactionType = transactionType,
            SenderDeviceId = senderDeviceId,
            SenderAccountId = senderAccountId,
            ReceiverAccountId = receiverAccountId,
            TransactionDate = transactionDate,
            SenderIpAddress = senderIpAddress,
            Location = location,
            MerchantId = merchantId,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        // Raise domain event for downstream consumers
        transaction.RaiseDomainEvent(new Events.TransactionCreatedEvent(transaction.Id, transaction.TransactionDate));

        return transaction;
    }
}
