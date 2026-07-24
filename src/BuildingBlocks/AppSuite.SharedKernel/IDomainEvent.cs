namespace AppSuite.SharedKernel;

/// <summary>
/// Marker interface for domain events raised by aggregates. Domain events describe
/// something that happened in the domain and are dispatched after the aggregate is
/// persisted (see the application-layer pipeline).
/// </summary>
public interface IDomainEvent;
