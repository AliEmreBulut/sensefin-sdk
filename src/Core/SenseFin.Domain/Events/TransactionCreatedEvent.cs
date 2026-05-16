using SenseFin.Domain.Common;

namespace SenseFin.Domain.Events;

// Yeni bir işlem oluşturulduğunda fırlatılan domain event.
public sealed class TransactionCreatedEvent : IDomainEvent
{
    public Guid TransactionId { get; }
    public DateTime TransactionDate { get; }
    public DateTime OccurredOn { get; }

    public TransactionCreatedEvent(Guid transactionId, DateTime transactionDate)
    {
        TransactionId = transactionId;
        TransactionDate = transactionDate;
        OccurredOn = DateTime.UtcNow;
    }
}
