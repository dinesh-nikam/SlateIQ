using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using SlateIQ.Application.Abstractions.Audit;
using SlateIQ.Application.Abstractions.Persistence;
using SlateIQ.Domain.Common.ValueObjects;
using SlateIQ.Domain.Vendors;

namespace SlateIQ.Api.Endpoints;

public sealed record CreateVendorRequest(
    string Code,
    string LegalName,
    string VendorType,
    string? TradeName = null,
    string? PrimaryTaxIdentifier = null);

public sealed record VendorResponseDto(
    Guid VendorId,
    string Code,
    string LegalName,
    string VendorType,
    string Status,
    string? PrimaryTaxIdentifier,
    string RiskRating);

public sealed record CreateCommitmentRequest(
    string CommitmentNumber,
    Guid ShowId,
    Guid VendorId,
    Guid DepartmentId,
    Guid CategoryId,
    string CommitmentType,
    string Description,
    decimal Amount,
    DateOnly ValidFrom,
    DateOnly? ValidTo = null);

public static class VendorEndpoints
{
    public static IEndpointRouteBuilder MapVendorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/vendors");

        group.MapGet("/", ListVendorsAsync);
        group.MapPost("/", CreateVendorAsync);
        group.MapPost("/{id:guid}/approve", ApproveVendorAsync);

        endpoints.MapGet("/api/v1/commitments", ListCommitmentsAsync);
        endpoints.MapPost("/api/v1/commitments", CreateCommitmentAsync);

        return endpoints;
    }

    private static async Task<IResult> ListVendorsAsync(
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            SELECT vendor_id, code, legal_name, vendor_type, status, primary_tax_identifier, risk_rating
            FROM vendor.vendor
            ORDER BY created_at DESC;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<VendorResponseDto>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new VendorResponseDto(
                VendorId: reader.GetGuid(0),
                Code: reader.GetString(1),
                LegalName: reader.GetString(2),
                VendorType: reader.GetString(3),
                Status: reader.GetString(4),
                PrimaryTaxIdentifier: reader.IsDBNull(5) ? null : reader.GetString(5),
                RiskRating: reader.GetString(6)));
        }

        return Results.Ok(list);
    }

    private static async Task<IResult> CreateVendorAsync(
        CreateVendorRequest request,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        if (!Enum.TryParse<VendorType>(request.VendorType, true, out var vendorType))
        {
            return Results.BadRequest(new { error = "ERR-060", message = $"Invalid vendor type '{request.VendorType}'." });
        }

        var userId = Guid.NewGuid();
        var vendor = Vendor.Create(
            orgId: orgId.Value,
            code: request.Code,
            legalName: request.LegalName,
            vendorType: vendorType,
            createdBy: userId,
            tradeName: request.TradeName,
            primaryTaxIdentifier: request.PrimaryTaxIdentifier);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            INSERT INTO vendor.vendor (
                org_id, vendor_id, code, legal_name, normalized_legal_name, trade_name,
                vendor_type, primary_tax_identifier, status, risk_rating, created_at, created_by,
                updated_at, updated_by, row_version
            ) VALUES (
                @orgId, @vendorId, @code, @legalName, @normalized, @tradeName,
                @type, @taxId, @status, @risk, @createdAt, @createdBy,
                @updatedAt, @updatedBy, 1
            );";

        await using (var cmd = new NpgsqlCommand(sql, connection))
        {
            cmd.Parameters.AddWithValue("orgId", vendor.OrgId);
            cmd.Parameters.AddWithValue("vendorId", vendor.VendorId);
            cmd.Parameters.AddWithValue("code", vendor.Code);
            cmd.Parameters.AddWithValue("legalName", vendor.LegalName);
            cmd.Parameters.AddWithValue("normalized", vendor.NormalizedLegalName);
            cmd.Parameters.AddWithValue("tradeName", (object?)vendor.TradeName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("type", vendor.VendorType.ToString());
            cmd.Parameters.AddWithValue("taxId", (object?)vendor.PrimaryTaxIdentifier ?? DBNull.Value);
            cmd.Parameters.AddWithValue("status", vendor.Status.ToString());
            cmd.Parameters.AddWithValue("risk", vendor.RiskRating.ToString());
            cmd.Parameters.AddWithValue("createdAt", vendor.CreatedAt);
            cmd.Parameters.AddWithValue("createdBy", vendor.CreatedBy);
            cmd.Parameters.AddWithValue("updatedAt", vendor.UpdatedAt);
            cmd.Parameters.AddWithValue("updatedBy", vendor.UpdatedBy);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId.Value,
            ActorUserId: userId,
            Action: "vendor.created",
            EntityType: "vendor",
            EntityId: vendor.VendorId,
            Metadata: JsonSerializer.Serialize(new { code = vendor.Code, legalName = vendor.LegalName })), ct);

        return Results.Created($"/api/v1/vendors/{vendor.VendorId}", new VendorResponseDto(
            vendor.VendorId, vendor.Code, vendor.LegalName, vendor.VendorType.ToString(),
            vendor.Status.ToString(), vendor.PrimaryTaxIdentifier, vendor.RiskRating.ToString()));
    }

    private static async Task<IResult> ApproveVendorAsync(
        Guid id,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            UPDATE vendor.vendor
            SET status = 'ACTIVE', updated_at = now(), row_version = row_version + 1
            WHERE vendor_id = @id AND status = 'CANDIDATE';";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        int rows = await cmd.ExecuteNonQueryAsync(ct);

        if (rows == 0)
        {
            return Results.BadRequest(new { error = "ERR-061", message = "Vendor not found or not in CANDIDATE state." });
        }

        var userId = Guid.NewGuid();
        await auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId.Value,
            ActorUserId: userId,
            Action: "vendor.approved",
            EntityType: "vendor",
            EntityId: id), ct);

        return Results.Ok(new { message = "Vendor approved and active." });
    }

    private static async Task<IResult> ListCommitmentsAsync(
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            SELECT commitment_id, commitment_number, show_id, vendor_id, commitment_type,
                   description, amount, currency, valid_from, valid_to, status
            FROM vendor.manual_commitment
            ORDER BY created_at DESC;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<object>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new
            {
                CommitmentId = reader.GetGuid(0),
                CommitmentNumber = reader.GetString(1),
                ShowId = reader.GetGuid(2),
                VendorId = reader.GetGuid(3),
                CommitmentType = reader.GetString(4),
                Description = reader.GetString(5),
                Amount = reader.GetDecimal(6),
                Currency = reader.GetString(7),
                ValidFrom = DateOnly.FromDateTime(reader.GetDateTime(8)),
                ValidTo = reader.IsDBNull(9) ? null : (DateOnly?)DateOnly.FromDateTime(reader.GetDateTime(9)),
                Status = reader.GetString(10)
            });
        }

        return Results.Ok(list);
    }

    private static async Task<IResult> CreateCommitmentAsync(
        CreateCommitmentRequest request,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        if (!Enum.TryParse<CommitmentType>(request.CommitmentType, true, out var commitmentType))
        {
            return Results.BadRequest(new { error = "ERR-062", message = "Invalid commitment type." });
        }

        var userId = Guid.NewGuid();
        var commitment = ManualCommitment.Create(
            orgId: orgId.Value,
            commitmentNumber: request.CommitmentNumber,
            showId: request.ShowId,
            vendorId: request.VendorId,
            departmentId: request.DepartmentId,
            categoryId: request.CategoryId,
            commitmentType: commitmentType,
            description: request.Description,
            amount: Money.From(request.Amount, "INR"),
            validFrom: request.ValidFrom,
            createdBy: userId,
            validTo: request.ValidTo);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            INSERT INTO vendor.manual_commitment (
                org_id, commitment_id, commitment_number, show_id, vendor_id, department_id, category_id,
                commitment_type, description, amount, base_amount, currency, valid_from, valid_to,
                status, created_at, created_by, updated_at, updated_by, row_version
            ) VALUES (
                @orgId, @id, @number, @showId, @vendorId, @deptId, @catId,
                @type, @desc, @amount, @amount, @ccy, @validFrom, @validTo,
                'DRAFT', now(), @createdBy, now(), @createdBy, 1
            );";

        await using (var cmd = new NpgsqlCommand(sql, connection))
        {
            cmd.Parameters.AddWithValue("orgId", commitment.OrgId);
            cmd.Parameters.AddWithValue("id", commitment.CommitmentId);
            cmd.Parameters.AddWithValue("number", commitment.CommitmentNumber);
            cmd.Parameters.AddWithValue("showId", commitment.ShowId);
            cmd.Parameters.AddWithValue("vendorId", commitment.VendorId);
            cmd.Parameters.AddWithValue("deptId", commitment.DepartmentId);
            cmd.Parameters.AddWithValue("catId", commitment.CategoryId);
            cmd.Parameters.AddWithValue("type", commitment.CommitmentType.ToString());
            cmd.Parameters.AddWithValue("desc", commitment.Description);
            cmd.Parameters.AddWithValue("amount", commitment.Amount.Amount);
            cmd.Parameters.AddWithValue("ccy", commitment.Amount.Currency);
            cmd.Parameters.AddWithValue("validFrom", commitment.ValidFrom);
            cmd.Parameters.AddWithValue("validTo", (object?)commitment.ValidTo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("createdBy", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId.Value,
            ActorUserId: userId,
            Action: "commitment.created",
            EntityType: "commitment",
            EntityId: commitment.CommitmentId,
            Metadata: JsonSerializer.Serialize(new { number = commitment.CommitmentNumber, amount = commitment.Amount.Amount })), ct);

        return Results.Created($"/api/v1/commitments/{commitment.CommitmentId}", new { commitmentId = commitment.CommitmentId, number = commitment.CommitmentNumber });
    }
}
