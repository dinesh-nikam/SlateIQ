using System;
using System.Threading;
using System.Threading.Tasks;
using SlateIQ.Domain.Ops;

namespace SlateIQ.Application.Abstractions.Ops;

public interface IOutboxPublisher
{
    /// <summary>
    /// Writes a domain event to ops.outbox_event within the active business transaction (ADR-003).
    /// </summary>
    Task PublishAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default);
}

public interface IInboxDeduplicator
{
    /// <summary>
    /// Asserts whether the event has already been processed by consumer (NFR-INTG-001).
    /// </summary>
    Task<bool> HasBeenProcessedAsync(string consumerName, Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records event execution into ops.inbox_dedup.
    /// </summary>
    Task MarkProcessedAsync(string consumerName, Guid eventId, CancellationToken cancellationToken = default);
}
