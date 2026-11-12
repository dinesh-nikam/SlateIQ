using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using SlateIQ.Application.Abstractions.Persistence;

namespace SlateIQ.Api.Endpoints;

public sealed record SubmitImportBatchRequest(
    Guid ShowId,
    string TemplateType,
    Guid StorageDocumentId,
    int TotalRows);

public static class ExcelEndpoints
{
    public static IEndpointRouteBuilder MapExcelEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/excel");

        group.MapGet("/batches", ListBatchesAsync);
        group.MapPost("/batches", SubmitBatchAsync);
        group.MapGet("/batches/{id:guid}", GetBatchAsync);

        return endpoints;
    }

    private static async Task<IResult> ListBatchesAsync(
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            SELECT batch_id, show_id, template_type, total_rows, valid_rows, error_rows, status, uploaded_at
            FROM xls.import_batch
            ORDER BY uploaded_at DESC;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<object>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new
            {
                BatchId = reader.GetGuid(0),
                ShowId = reader.GetGuid(1),
                TemplateType = reader.GetString(2),
                TotalRows = reader.GetInt32(3),
                ValidRows = reader.GetInt32(4),
                ErrorRows = reader.GetInt32(5),
                Status = reader.GetString(6),
                UploadedAt = reader.GetDateTime(7)
            });
        }

        return Results.Ok(list);
    }

    private static async Task<IResult> SubmitBatchAsync(
        SubmitImportBatchRequest request,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        var batchId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            INSERT INTO xls.import_batch (
                org_id, batch_id, show_id, template_type, storage_document_id,
                total_rows, valid_rows, error_rows, status, uploaded_at,
                created_at, created_by, updated_at, updated_by, row_version
            ) VALUES (
                @orgId, @batchId, @showId, @type, @docId,
                @total, @total, 0, 'VALIDATED', now(),
                now(), @createdBy, now(), @createdBy, 1
            );";

        await using (var cmd = new NpgsqlCommand(sql, connection))
        {
            cmd.Parameters.AddWithValue("orgId", orgId.Value);
            cmd.Parameters.AddWithValue("batchId", batchId);
            cmd.Parameters.AddWithValue("showId", request.ShowId);
            cmd.Parameters.AddWithValue("type", request.TemplateType.ToUpperInvariant());
            cmd.Parameters.AddWithValue("docId", request.StorageDocumentId);
            cmd.Parameters.AddWithValue("total", request.TotalRows);
            cmd.Parameters.AddWithValue("createdBy", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return Results.Created($"/api/v1/excel/batches/{batchId}", new { batchId, status = "VALIDATED" });
    }

    private static async Task<IResult> GetBatchAsync(
        Guid id,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            SELECT batch_id, show_id, template_type, total_rows, valid_rows, error_rows, status, uploaded_at
            FROM xls.import_batch
            WHERE batch_id = @id;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
        {
            return Results.NotFound(new { error = "ERR-100", message = "Import batch not found." });
        }

        return Results.Ok(new
        {
            batchId = reader.GetGuid(0),
            showId = reader.GetGuid(1),
            templateType = reader.GetString(2),
            totalRows = reader.GetInt32(3),
            validRows = reader.GetInt32(4),
            errorRows = reader.GetInt32(5),
            status = reader.GetString(6),
            uploadedAt = reader.GetDateTime(7)
        });
    }
}
