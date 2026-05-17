using SenseFin.Domain.Common;

namespace SenseFin.Domain.Aggregates.Transaction;

// Finansal işlemleri temsil eden Aggregate Root.
// Dolandırıcılık analizi için ana giriş noktasıdır.
public sealed class TransactionAggregate : AggregateRoot
{
    // Temel bilgiler

    public Money Money { get; private set; } = null!;

    public TransactionType TransactionType { get; private set; }

    // Cihaz ve oturum bilgileri

    public string SenderDeviceId { get; private set; } = null!;

    public string? SenderIpAddress { get; private set; }

    // Hesap referansları

    public string SenderAccountId { get; private set; } = null!;

    public string ReceiverAccountId { get; private set; } = null!;

    public string? ReceiverIban { get; private set; }

    // Konum bilgisi

    public GeoLocation? Location { get; private set; }

    // Zaman damgaları

    public DateTime TransactionDate { get; private set; }
    public DateTime Timestamp => TransactionDate;

    // Meta veriler


    public string? MerchantId { get; private set; }

    public string? Description { get; private set; }

    public double? TypingScore { get; private set; }

    public double? TremorScore { get; private set; }

    // Constructor

    private TransactionAggregate() { }

    // Yeni bir Transaction nesnesi oluşturur
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
        string? description = null,
        string? receiverIban = null,
        double? typingScore = null,
        double? tremorScore = null)
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
            ReceiverIban = receiverIban,
            TypingScore = typingScore,
            TremorScore = tremorScore,
            CreatedAt = DateTime.UtcNow
        };

        // Domain event fırlat
        transaction.RaiseDomainEvent(new Events.TransactionCreatedEvent(transaction.Id, transaction.TransactionDate));

        return transaction;
    }
}
