using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using SlateIQ.Application.Abstractions.Persistence;

namespace SlateIQ.Api.Endpoints;

public sealed record IssueUploadTicketRequest(
    string Filename,
    string ContentType,
    long FileSizeBytes,
    string Sha256Hash);

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/documents");

        group.MapPost("/upload-ticket", IssueUploadTicketAsync);
        group.MapGet("/{id:guid}", GetDocumentStatusAsync);

        return endpoints;
    }

    private static async Task<IResult> IssueUploadTicketAsync(
        IssueUploadTicketRequest request,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        var docId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var storageKey = $"org/{orgId.Value}/evidence/{docId:N}_{request.Filename}";
        var presignedUrl = $"http://localhost:9000/slateiq-evidence/{storageKey}?ticket={ticketId:N}";
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        const string insertDocSql = @"
            INSERT INTO docs.document (
                org_id, document_id, storage_bucket, storage_key, filename, content_type,
                file_size_bytes, sha256_hash, scan_status, status, uploaded_at,
                created_at, created_by, updated_at, updated_by, row_version
            ) VALUES (
                @orgId, @docId, 'slateiq-evidence', @storageKey, @filename, @contentType,
                @size, @hash, 'CLEAN', 'AVAILABLE', now(),
                now(), @createdBy, now(), @createdBy, 1
            );";

        await using (var cmd = new NpgsqlCommand(insertDocSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("orgId", orgId.Value);
            cmd.Parameters.AddWithValue("docId", docId);
            cmd.Parameters.AddWithValue("storageKey", storageKey);
            cmd.Parameters.AddWithValue("filename", request.Filename);
            cmd.Parameters.AddWithValue("contentType", request.ContentType);
            cmd.Parameters.AddWithValue("size", request.FileSizeBytes);
            cmd.Parameters.AddWithValue("hash", request.Sha256Hash.ToLowerInvariant());
            cmd.Parameters.AddWithValue("createdBy", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        const string insertTicketSql = @"
            INSERT INTO docs.upload_ticket (
                org_id, ticket_id, document_id, storage_key, expires_at, status, created_at, created_by
            ) VALUES (
                @orgId, @ticketId, @docId, @storageKey, @expiresAt, 'ISSUED', now(), @createdBy
            );";

        await using (var cmd = new NpgsqlCommand(insertTicketSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("orgId", orgId.Value);
            cmd.Parameters.AddWithValue("ticketId", ticketId);
            cmd.Parameters.AddWithValue("docId", docId);
            cmd.Parameters.AddWithValue("storageKey", storageKey);
            cmd.Parameters.AddWithValue("expiresAt", expiresAt);
            cmd.Parameters.AddWithValue("createdBy", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        return Results.Ok(new
        {
            ticketId,
            documentId = docId,
            storageKey,
            presignedUrl,
            expiresAt
        });
    }

    private static async Task<IResult> GetDocumentStatusAsync(
        Guid id,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            SELECT document_id, filename, content_type, file_size_bytes, scan_status, status, uploaded_at
            FROM docs.document
            WHERE document_id = @id;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
        {
            return Results.NotFound(new { error = "ERR-090", message = "Document not found." });
        }

        return Results.Ok(new
        {
            documentId = reader.GetGuid(0),
            filename = reader.GetString(1),
            contentType = reader.GetString(2),
            fileSizeBytes = reader.GetInt64(3),
            scanStatus = reader.GetString(4),
            status = reader.GetString(5),
            uploadedAt = reader.GetDateTime(6)
        });
    }
}
