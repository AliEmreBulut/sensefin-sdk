namespace SenseFin.Domain.Common;

// Tüm agregat kökleri için temel sınıf.
// Domain event yönetimini sağlar.
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }

    // Fırlatılan domain event'leri tutar
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
