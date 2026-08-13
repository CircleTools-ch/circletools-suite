namespace CircleTools.SharedKernel;

/// <summary>
/// Base class for aggregate roots. An aggregate root is the only entry point into an
/// aggregate and is responsible for recording <see cref="IDomainEvent"/>s that occur
/// while its invariants are enforced.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Gets the domain events raised by this aggregate that have not yet been dispatched.</summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Records a domain event to be dispatched after the aggregate is persisted.</summary>
    /// <param name="domainEvent">The domain event to record.</param>
    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Clears all recorded domain events. Called once they have been dispatched.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
