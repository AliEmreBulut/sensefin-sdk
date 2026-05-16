namespace SenseFin.Domain.Common;

// Domain event'leri için işaretleyici (marker) arayüz.
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
