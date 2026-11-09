using System;

namespace SlateIQ.Domain.Common.Primitives;

/// <summary>
/// Enforces tenant scoping invariant (INV-004, ADR-035).
/// Every tenant-owned domain entity must expose an OrgId.
/// </summary>
public interface ITenantScoped
{
    Guid OrgId { get; }
}

/// <summary>
/// Marker interface for in-process and transactional outbox domain events.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}
