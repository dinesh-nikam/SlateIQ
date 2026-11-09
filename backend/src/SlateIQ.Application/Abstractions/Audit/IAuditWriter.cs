using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SlateIQ.Application.Abstractions.Audit;

public sealed record AuditEventRecord(
    Guid OrgId,
    Guid? ActorUserId,
    string Action,
    string EntityType,
    Guid EntityId,
    JsonDocument? PreviousState,
    JsonDocument? NewState,
    JsonDocument? Metadata,
    string? CorrelationId,
    string? SourceIp,
    string? UserAgent
);

public interface IAuditWriter
{
    /// <summary>
    /// Writes a cryptographic hash-chained audit event within the active transaction (ADR-012).
    /// </summary>
    Task<string> WriteEventAsync(AuditEventRecord record, CancellationToken cancellationToken = default);
}
