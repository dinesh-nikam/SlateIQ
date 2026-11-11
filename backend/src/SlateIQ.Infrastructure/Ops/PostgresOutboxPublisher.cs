using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SlateIQ.Application.Abstractions.Ops;
using SlateIQ.Domain.Ops;

namespace SlateIQ.Infrastructure.Ops;

public sealed class PostgresOutboxPublisher : IOutboxPublisher
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresOutboxPublisher(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task PublishAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO ops.outbox_event (
                id, event_id, aggregate_type, aggregate_id, event_type,
                org_id, payload, correlation_id, status, attempts, max_attempts,
                scheduled_at, created_at
            ) VALUES (
                @id, @eventId, @aggregateType, @aggregateId, @eventType,
                @orgId, @payload::jsonb, @correlationId, @status, @attempts, @maxAttempts,
                @scheduledAt, @createdAt
            );";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", outboxEvent.Id);
        cmd.Parameters.AddWithValue("eventId", outboxEvent.EventId);
        cmd.Parameters.AddWithValue("aggregateType", outboxEvent.AggregateType);
        cmd.Parameters.AddWithValue("aggregateId", outboxEvent.AggregateId);
        cmd.Parameters.AddWithValue("eventType", outboxEvent.EventType);
        cmd.Parameters.AddWithValue("orgId", (object?)outboxEvent.OrgId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("payload", outboxEvent.PayloadJson);
        cmd.Parameters.AddWithValue("correlationId", (object?)outboxEvent.CorrelationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", outboxEvent.Status.ToString());
        cmd.Parameters.AddWithValue("attempts", outboxEvent.Attempts);
        cmd.Parameters.AddWithValue("maxAttempts", outboxEvent.MaxAttempts);
        cmd.Parameters.AddWithValue("scheduledAt", outboxEvent.ScheduledAt);
        cmd.Parameters.AddWithValue("createdAt", outboxEvent.CreatedAt);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class PostgresInboxDeduplicator : IInboxDeduplicator
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresInboxDeduplicator(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<bool> HasBeenProcessedAsync(string consumerName, Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT 1 FROM ops.inbox_dedup 
            WHERE consumer_name = @consumerName AND event_id = @eventId 
            LIMIT 1;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("consumerName", consumerName);
        cmd.Parameters.AddWithValue("eventId", eventId);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }

    public async Task MarkProcessedAsync(string consumerName, Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO ops.inbox_dedup (consumer_name, event_id, processed_at)
            VALUES (@consumerName, @eventId, now())
            ON CONFLICT (consumer_name, event_id) DO NOTHING;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("consumerName", consumerName);
        cmd.Parameters.AddWithValue("eventId", eventId);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
