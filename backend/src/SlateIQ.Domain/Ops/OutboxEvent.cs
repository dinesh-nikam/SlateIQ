using System;

namespace SlateIQ.Domain.Ops;

/// <summary>
/// State machine SM-19 (Job and Outbox Event Lifecycle) per SLQ-ARCH-004 §4.19.
/// </summary>
public enum OutboxStatus
{
    PENDING = 1,
    PROCESSING = 2,
    SUCCEEDED = 3,
    FAILED = 4,
    DEAD = 5,
    CANCELLED = 6
}

public sealed class OutboxEvent
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string AggregateType { get; private set; }
    public Guid AggregateId { get; private set; }
    public string EventType { get; private set; }
    public Guid? OrgId { get; private set; }
    public string PayloadJson { get; private set; }
    public string? CorrelationId { get; private set; }
    public OutboxStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset ScheduledAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public OutboxEvent(
        Guid eventId,
        string aggregateType,
        Guid aggregateId,
        string eventType,
        Guid? orgId,
        string payloadJson,
        string? correlationId,
        DateTimeOffset? scheduledAt = null,
        int maxAttempts = 5)
    {
        Id = Guid.NewGuid();
        EventId = eventId == Guid.Empty ? Guid.NewGuid() : eventId;
        AggregateType = aggregateType ?? throw new ArgumentNullException(nameof(aggregateType));
        AggregateId = aggregateId;
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        OrgId = orgId;
        PayloadJson = payloadJson ?? throw new ArgumentNullException(nameof(payloadJson));
        CorrelationId = correlationId;
        Status = OutboxStatus.PENDING;
        Attempts = 0;
        MaxAttempts = maxAttempts > 0 ? maxAttempts : 5;
        ScheduledAt = scheduledAt ?? DateTimeOffset.UtcNow;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkProcessing()
    {
        Status = OutboxStatus.PROCESSING;
        Attempts++;
    }

    public void MarkSucceeded()
    {
        Status = OutboxStatus.SUCCEEDED;
        ProcessedAt = DateTimeOffset.UtcNow;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        LastError = error;
        if (Attempts >= MaxAttempts)
        {
            Status = OutboxStatus.DEAD;
        }
        else
        {
            Status = OutboxStatus.FAILED;
            // Exponential backoff: 5s, 20s, 80s, 320s
            double delaySeconds = Math.Pow(4, Attempts - 1) * 5;
            ScheduledAt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
        }
    }
}
