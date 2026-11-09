using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SlateIQ.Application.Abstractions.Audit;

namespace SlateIQ.Infrastructure.Audit;

public sealed class HashChainedAuditWriter : IAuditWriter
{
    private const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";
    private readonly NpgsqlDataSource _dataSource;

    public HashChainedAuditWriter(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<string> WriteEventAsync(AuditEventRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // Ensure session tenant context is set for RLS compliance
        await using (var contextCmd = new NpgsqlCommand("SELECT set_config('app.current_org_id', @orgId, true);", connection))
        {
            contextCmd.Parameters.AddWithValue("orgId", record.OrgId.ToString());
            await contextCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // 1. Fetch latest hash for the tenant
        string previousHash = GenesisHash;
        const string fetchLastHashSql = @"
            SELECT current_hash 
            FROM compliance.audit_event 
            WHERE org_id = @orgId 
            ORDER BY created_at DESC 
            LIMIT 1 FOR UPDATE;";

        await using (var fetchCmd = new NpgsqlCommand(fetchLastHashSql, connection))
        {
            fetchCmd.Parameters.AddWithValue("orgId", record.OrgId);
            var result = await fetchCmd.ExecuteScalarAsync(cancellationToken);
            if (result != null && result != DBNull.Value)
            {
                previousHash = (string)result;
            }
        }

        // 2. Compute cryptographic current_hash
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string canonicalPayload = $"{previousHash}|{now:O}|{record.OrgId}|{record.ActorUserId}|{record.Action}|{record.EntityType}|{record.EntityId}|{record.PreviousState?.RootElement.ToString()}|{record.NewState?.RootElement.ToString()}|{record.CorrelationId}";
        
        string currentHash;
        using (var sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalPayload));
            currentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        // 3. Insert append-only audit row
        const string insertSql = @"
            INSERT INTO compliance.audit_event (
                org_id, actor_user_id, action, entity_type, entity_id,
                previous_state, new_state, metadata, correlation_id,
                source_ip, user_agent, previous_hash, current_hash, created_at
            ) VALUES (
                @orgId, @actorUserId, @action, @entityType, @entityId,
                @prevState::jsonb, @newState::jsonb, @metadata::jsonb, @correlationId,
                @sourceIp::inet, @userAgent, @prevHash, @currentHash, @createdAt
            );";

        await using (var insertCmd = new NpgsqlCommand(insertSql, connection))
        {
            insertCmd.Parameters.AddWithValue("orgId", record.OrgId);
            insertCmd.Parameters.AddWithValue("actorUserId", (object?)record.ActorUserId ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("action", record.Action);
            insertCmd.Parameters.AddWithValue("entityType", record.EntityType);
            insertCmd.Parameters.AddWithValue("entityId", record.EntityId);
            insertCmd.Parameters.AddWithValue("prevState", record.PreviousState != null ? record.PreviousState.RootElement.ToString() : (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("newState", record.NewState != null ? record.NewState.RootElement.ToString() : (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("metadata", record.Metadata != null ? record.Metadata.RootElement.ToString() : "{}" );
            insertCmd.Parameters.AddWithValue("correlationId", (object?)record.CorrelationId ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("sourceIp", (object?)record.SourceIp ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("userAgent", (object?)record.UserAgent ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("prevHash", previousHash);
            insertCmd.Parameters.AddWithValue("currentHash", currentHash);
            insertCmd.Parameters.AddWithValue("createdAt", now);

            await insertCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        return currentHash;
    }
}
