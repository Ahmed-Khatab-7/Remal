using MediatR;

namespace Remal.Domain.Common.Events;

/// <summary>Marker interface for domain events. Implements INotification so MediatR can dispatch them.</summary>
public interface IDomainEvent : INotification
{
    DateTime OccurredOn { get; }
}

public abstract class DomainEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>Entities that can raise events override this.</summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
