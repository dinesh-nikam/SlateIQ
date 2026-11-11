using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using SlateIQ.Domain.Ops;

namespace SlateIQ.Infrastructure.Ops;

public sealed class OutboxDispatcher : BackgroundService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(2);

    public OutboxDispatcher(NpgsqlDataSource dataSource, ILogger<OutboxDispatcher> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxDispatcher started. Polling interval: {Interval}s", _pollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processedCount = await ProcessBatchAsync(stoppingToken);
                if (processedCount == 0)
                {
                    await Task.Delay(_pollingInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in OutboxDispatcher loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("OutboxDispatcher stopped.");
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string selectSql = @"
            SELECT id, event_id, aggregate_type, aggregate_id, event_type,
                   org_id, payload::text, correlation_id, status, attempts, max_attempts
            FROM ops.outbox_event
            WHERE status IN ('PENDING', 'FAILED')
              AND scheduled_at <= now()
            ORDER BY scheduled_at ASC
            LIMIT 50
            FOR UPDATE SKIP LOCKED;";

        var eventsToDispatch = new List<OutboxEventRecord>();

        await using (var cmd = new NpgsqlCommand(selectSql, connection, transaction))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                eventsToDispatch.Add(new OutboxEventRecord(
                    Id: reader.GetGuid(0),
                    EventId: reader.GetGuid(1),
                    AggregateType: reader.GetString(2),
                    AggregateId: reader.GetGuid(3),
                    EventType: reader.GetString(4),
                    OrgId: reader.IsDBNull(5) ? null : reader.GetGuid(5),
                    Payload: reader.GetString(6),
                    CorrelationId: reader.IsDBNull(7) ? null : reader.GetString(7),
                    Status: Enum.Parse<OutboxStatus>(reader.GetString(8)),
                    Attempts: reader.GetInt32(9),
                    MaxAttempts: reader.GetInt32(10)
                ));
            }
        }

        if (eventsToDispatch.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        foreach (var record in eventsToDispatch)
        {
            try
            {
                _logger.LogDebug("Dispatching outbox event {EventId} [{EventType}] for aggregate {AggregateId}", 
                    record.EventId, record.EventType, record.AggregateId);

                // Event dispatch to in-process handlers/buses
                // On success, mark SUCCEEDED
                const string successSql = @"
                    UPDATE ops.outbox_event 
                    SET status = 'SUCCEEDED', processed_at = now(), last_error = NULL
                    WHERE id = @id;";

                await using var successCmd = new NpgsqlCommand(successSql, connection, transaction);
                successCmd.Parameters.AddWithValue("id", record.Id);
                await successCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                int nextAttempts = record.Attempts + 1;
                bool isDead = nextAttempts >= record.MaxAttempts;
                string newStatus = isDead ? "DEAD" : "FAILED";
                double backoffSeconds = Math.Pow(4, nextAttempts - 1) * 5;
                DateTimeOffset nextSchedule = DateTimeOffset.UtcNow.AddSeconds(backoffSeconds);

                _logger.LogWarning(ex, "Failed to dispatch outbox event {EventId}. Status: {Status}, Attempt: {Attempt}",
                    record.EventId, newStatus, nextAttempts);

                const string failSql = @"
                    UPDATE ops.outbox_event 
                    SET status = @status, 
                        attempts = @attempts,
                        last_error = @error,
                        scheduled_at = @scheduledAt
                    WHERE id = @id;";

                await using var failCmd = new NpgsqlCommand(failSql, connection, transaction);
                failCmd.Parameters.AddWithValue("id", record.Id);
                failCmd.Parameters.AddWithValue("status", newStatus);
                failCmd.Parameters.AddWithValue("attempts", nextAttempts);
                failCmd.Parameters.AddWithValue("error", ex.Message);
                failCmd.Parameters.AddWithValue("scheduledAt", nextSchedule);
                await failCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return eventsToDispatch.Count;
    }

    private sealed record OutboxEventRecord(
        Guid Id,
        Guid EventId,
        string AggregateType,
        Guid AggregateId,
        string EventType,
        Guid? OrgId,
        string Payload,
        string? CorrelationId,
        OutboxStatus Status,
        int Attempts,
        int MaxAttempts
    );
}
