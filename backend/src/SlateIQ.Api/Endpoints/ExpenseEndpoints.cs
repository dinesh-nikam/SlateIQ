using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Npgsql;
using SlateIQ.Application.Abstractions.Audit;
using SlateIQ.Application.Abstractions.Ops;
using SlateIQ.Application.Abstractions.Persistence;
using SlateIQ.Domain.Common.ValueObjects;
using SlateIQ.Domain.Expenses;
using SlateIQ.Domain.Ops;

namespace SlateIQ.Api.Endpoints;

public sealed record CreateExpenseRequest(
    Guid ShowId,
    DateOnly ExpenseDate,
    string DocumentType,
    string Description,
    string Currency = "INR",
    Guid? SeasonId = null,
    Guid? DepartmentId = null,
    Guid? VendorId = null,
    string? DocumentNumber = null,
    string? PaymentMode = null);

public sealed record AddExpenseLineRequest(
    short LineNo,
    Guid CategoryId,
    Guid DepartmentId,
    string CostType,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    string? Description = null);

public sealed record ExpenseDto(
    Guid ExpenseId,
    string? ExpenseNumber,
    Guid ShowId,
    DateOnly ExpenseDate,
    string DocumentType,
    string Description,
    decimal TotalNetAmount,
    decimal TotalTaxAmount,
    decimal TotalGrossAmount,
    string Currency,
    string Status);

public static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/expenses");

        group.MapGet("/", ListExpensesAsync);
        group.MapPost("/", CreateExpenseAsync);
        group.MapPost("/{id:guid}/lines", AddLineAsync);
        group.MapPost("/{id:guid}/submit", SubmitExpenseAsync);
        group.MapPost("/{id:guid}/approve", ApproveExpenseAsync);

        return endpoints;
    }

    private static async Task<IResult> ListExpensesAsync(
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            SELECT expense_id, expense_number, show_id, expense_date, document_type,
                   description, total_net_amount, total_tax_amount, total_gross_amount,
                   currency, status
            FROM expense.expense
            ORDER BY created_at DESC;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<ExpenseDto>();
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ExpenseDto(
                ExpenseId: reader.GetGuid(0),
                ExpenseNumber: reader.IsDBNull(1) ? null : reader.GetString(1),
                ShowId: reader.GetGuid(2),
                ExpenseDate: DateOnly.FromDateTime(reader.GetDateTime(3)),
                DocumentType: reader.GetString(4),
                Description: reader.GetString(5),
                TotalNetAmount: reader.GetDecimal(6),
                TotalTaxAmount: reader.GetDecimal(7),
                TotalGrossAmount: reader.GetDecimal(8),
                Currency: reader.GetString(9),
                Status: reader.GetString(10)));
        }

        return Results.Ok(list);
    }

    private static async Task<IResult> CreateExpenseAsync(
        CreateExpenseRequest request,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        if (!Enum.TryParse<DocumentType>(request.DocumentType, true, out var docType))
        {
            return Results.BadRequest(new { error = "ERR-070", message = "Invalid document type." });
        }

        PaymentMode? pMode = null;
        if (!string.IsNullOrWhiteSpace(request.PaymentMode) && Enum.TryParse<PaymentMode>(request.PaymentMode, true, out var parsedMode))
        {
            pMode = parsedMode;
        }

        var userId = Guid.NewGuid();
        var expense = Expense.CreateDraft(
            orgId: orgId.Value,
            showId: request.ShowId,
            expenseDate: request.ExpenseDate,
            documentType: docType,
            description: request.Description,
            currency: request.Currency,
            createdBy: userId,
            seasonId: request.SeasonId,
            departmentId: request.DepartmentId,
            vendorId: request.VendorId,
            documentNumber: request.DocumentNumber,
            paymentMode: pMode);

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // 1. Insert expense_key anchor
        const string insertKeySql = @"
            INSERT INTO expense.expense_key (
                org_id, expense_id, root_expense_id, revision_no, show_id, created_at, created_by
            ) VALUES (
                @orgId, @expenseId, @rootId, 1, @showId, @createdAt, @createdBy
            );";

        await using (var cmd = new NpgsqlCommand(insertKeySql, connection, tx))
        {
            cmd.Parameters.AddWithValue("orgId", expense.OrgId);
            cmd.Parameters.AddWithValue("expenseId", expense.ExpenseId);
            cmd.Parameters.AddWithValue("rootId", expense.RootExpenseId);
            cmd.Parameters.AddWithValue("showId", expense.ShowId);
            cmd.Parameters.AddWithValue("createdAt", expense.CreatedAt);
            cmd.Parameters.AddWithValue("createdBy", expense.CreatedBy);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // 2. Insert into partitioned expense.expense
        const string insertExpenseSql = @"
            INSERT INTO expense.expense (
                created_at, org_id, expense_id, root_expense_id, revision_no, show_id,
                season_id, department_id, vendor_id, expense_date, document_type,
                document_number, payment_mode, description, currency, total_net_amount,
                total_tax_amount, total_gross_amount, status, created_by, updated_at, updated_by, row_version
            ) VALUES (
                @createdAt, @orgId, @expenseId, @rootId, 1, @showId,
                @seasonId, @deptId, @vendorId, @date, @docType,
                @docNum, @payMode, @desc, @ccy, 0,
                0, 0, 'DRAFT', @createdBy, @createdAt, @createdBy, 1
            );";

        await using (var cmd = new NpgsqlCommand(insertExpenseSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("createdAt", expense.CreatedAt);
            cmd.Parameters.AddWithValue("orgId", expense.OrgId);
            cmd.Parameters.AddWithValue("expenseId", expense.ExpenseId);
            cmd.Parameters.AddWithValue("rootId", expense.RootExpenseId);
            cmd.Parameters.AddWithValue("showId", expense.ShowId);
            cmd.Parameters.AddWithValue("seasonId", (object?)expense.SeasonId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("deptId", (object?)expense.DepartmentId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("vendorId", (object?)expense.VendorId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("date", expense.ExpenseDate);
            cmd.Parameters.AddWithValue("docType", expense.DocumentType.ToString());
            cmd.Parameters.AddWithValue("docNum", (object?)expense.DocumentNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("payMode", (object?)expense.PaymentMode?.ToString() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("desc", expense.Description);
            cmd.Parameters.AddWithValue("ccy", expense.Currency);
            cmd.Parameters.AddWithValue("createdBy", expense.CreatedBy);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        await auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId.Value,
            ActorUserId: userId,
            Action: "expense.draft_created",
            EntityType: "expense",
            EntityId: expense.ExpenseId,
            Metadata: JsonSerializer.Serialize(new { showId = expense.ShowId, desc = expense.Description })), ct);

        return Results.Created($"/api/v1/expenses/{expense.ExpenseId}", new { expenseId = expense.ExpenseId });
    }

    private static async Task<IResult> AddLineAsync(
        Guid id,
        AddExpenseLineRequest request,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // Fetch expense
        const string selectSql = @"
            SELECT show_id, currency, status FROM expense.expense
            WHERE expense_id = @id FOR UPDATE;";

        Guid showId;
        string currency;
        string status;

        await using (var cmd = new NpgsqlCommand(selectSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return Results.NotFound(new { error = "ERR-071", message = "Expense not found." });
            }
            showId = reader.GetGuid(0);
            currency = reader.GetString(1);
            status = reader.GetString(2);
        }

        if (status != "DRAFT")
        {
            return Results.BadRequest(new { error = "ERR-072", message = "Cannot add lines to non-draft expense." });
        }

        var userId = Guid.NewGuid();
        var line = ExpenseLine.Create(
            orgId: orgId.Value,
            expenseId: id,
            showId: showId,
            lineNo: request.LineNo,
            categoryId: request.CategoryId,
            departmentId: request.DepartmentId,
            costType: request.CostType,
            quantity: request.Quantity,
            unitPrice: Money.From(request.UnitPrice, currency),
            taxRate: request.TaxRate,
            createdBy: userId,
            description: request.Description);

        const string insertLineSql = @"
            INSERT INTO expense.expense_line (
                org_id, expense_line_id, expense_id, show_id, line_no, category_id,
                department_id, cost_type, description, quantity, unit_price, tax_rate,
                net_amount, tax_amount, gross_amount, cost_basis_amount, created_at, created_by
            ) VALUES (
                @orgId, @lineId, @expenseId, @showId, @lineNo, @catId,
                @deptId, @costType, @desc, @qty, @unitPrice, @taxRate,
                @net, @tax, @gross, @costBasis, now(), @createdBy
            );";

        await using (var cmd = new NpgsqlCommand(insertLineSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("orgId", line.OrgId);
            cmd.Parameters.AddWithValue("lineId", line.ExpenseLineId);
            cmd.Parameters.AddWithValue("expenseId", line.ExpenseId);
            cmd.Parameters.AddWithValue("showId", line.ShowId);
            cmd.Parameters.AddWithValue("lineNo", line.LineNo);
            cmd.Parameters.AddWithValue("catId", line.CategoryId);
            cmd.Parameters.AddWithValue("deptId", line.DepartmentId);
            cmd.Parameters.AddWithValue("costType", line.CostType);
            cmd.Parameters.AddWithValue("desc", (object?)line.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("qty", line.Quantity);
            cmd.Parameters.AddWithValue("unitPrice", line.UnitPrice.Amount);
            cmd.Parameters.AddWithValue("taxRate", line.TaxRate);
            cmd.Parameters.AddWithValue("net", line.NetAmount.Amount);
            cmd.Parameters.AddWithValue("tax", line.TaxAmount.Amount);
            cmd.Parameters.AddWithValue("gross", line.GrossAmount.Amount);
            cmd.Parameters.AddWithValue("costBasis", line.CostBasisAmount.Amount);
            cmd.Parameters.AddWithValue("createdBy", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Recalculate expense totals
        const string updateExpenseTotalsSql = @"
            UPDATE expense.expense
            SET total_net_amount = (SELECT COALESCE(SUM(net_amount), 0) FROM expense.expense_line WHERE expense_id = @id),
                total_tax_amount = (SELECT COALESCE(SUM(tax_amount), 0) FROM expense.expense_line WHERE expense_id = @id),
                total_gross_amount = (SELECT COALESCE(SUM(gross_amount), 0) FROM expense.expense_line WHERE expense_id = @id),
                updated_at = now(),
                row_version = row_version + 1
            WHERE expense_id = @id;";

        await using (var cmd = new NpgsqlCommand(updateExpenseTotalsSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return Results.Created($"/api/v1/expenses/{id}/lines/{line.ExpenseLineId}", new { lineId = line.ExpenseLineId });
    }

    private static async Task<IResult> SubmitExpenseAsync(
        Guid id,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        IOutboxPublisher outboxPublisher,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        var expenseNumber = $"EXP-2026-{id.ToString()[..6].ToUpperInvariant()}";
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        const string sql = @"
            UPDATE expense.expense
            SET status = 'SUBMITTED', expense_number = @num, submitted_at = now(), updated_at = now(), row_version = row_version + 1
            WHERE expense_id = @id AND status = 'DRAFT';";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("num", expenseNumber);
        int rows = await cmd.ExecuteNonQueryAsync(ct);

        if (rows == 0)
        {
            return Results.BadRequest(new { error = "ERR-073", message = "Expense must be in DRAFT state to submit." });
        }

        var userId = Guid.NewGuid();
        await auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId.Value,
            ActorUserId: userId,
            Action: "expense.submitted",
            EntityType: "expense",
            EntityId: id,
            Metadata: JsonSerializer.Serialize(new { expenseNumber })), ct);

        await outboxPublisher.PublishAsync(OutboxEvent.Create(
            aggregateType: "Expense",
            aggregateId: id,
            eventType: "ExpenseSubmitted",
            payload: new { expenseId = id, expenseNumber, orgId = orgId.Value },
            orgId: orgId.Value), ct);

        return Results.Ok(new { message = "Expense submitted for approval.", expenseNumber });
    }

    private static async Task<IResult> ApproveExpenseAsync(
        Guid id,
        NpgsqlDataSource dataSource,
        ITenantContext tenantContext,
        IAuditWriter auditWriter,
        IOutboxPublisher outboxPublisher,
        CancellationToken ct)
    {
        var orgId = tenantContext.CurrentOrgId;
        if (!orgId.HasValue) return Results.Unauthorized();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // 1. Approve expense
        const string approveSql = @"
            UPDATE expense.expense
            SET status = 'APPROVED', approved_at = now(), updated_at = now(), row_version = row_version + 1
            WHERE expense_id = @id AND status IN ('SUBMITTED', 'IN_REVIEW');";

        await using (var cmd = new NpgsqlCommand(approveSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            int rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows == 0)
            {
                return Results.BadRequest(new { error = "ERR-074", message = "Expense must be SUBMITTED to approve." });
            }
        }

        // 2. Fetch lines to create actual cost postings (D2-17, BR-EXP-010)
        const string fetchLinesSql = @"
            SELECT e.root_expense_id, l.expense_line_id, l.show_id, COALESCE(e.season_id, gen_random_uuid()),
                   l.department_id, l.category_id, l.gross_amount, e.currency, e.expense_date
            FROM expense.expense_line l
            JOIN expense.expense e ON l.expense_id = e.expense_id
            WHERE l.expense_id = @id;";

        var postings = new List<(Guid rootId, Guid lineId, Guid showId, Guid seasonId, Guid deptId, Guid catId, decimal amount, string ccy, DateOnly date)>();
        await using (var cmd = new NpgsqlCommand(fetchLinesSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                postings.Add((
                    reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3),
                    reader.GetGuid(4), reader.GetGuid(5), reader.GetDecimal(6), reader.GetString(7),
                    DateOnly.FromDateTime(reader.GetDateTime(8))));
            }
        }

        // 3. Insert cost postings
        const string insertPostingSql = @"
            INSERT INTO expense.cost_posting (
                org_id, cost_posting_id, posting_type, expense_id, root_expense_id,
                expense_line_id, split_seq, posting_date, show_id, season_id,
                department_id, category_id, amount, currency, created_at, created_by
            ) VALUES (
                @orgId, gen_random_uuid(), 'APPROVAL', @expId, @rootId,
                @lineId, 0, @date, @showId, @seasonId,
                @deptId, @catId, @amt, @ccy, now(), @createdBy
            );";

        var userId = Guid.NewGuid();
        foreach (var p in postings)
        {
            await using var cmd = new NpgsqlCommand(insertPostingSql, connection, tx);
            cmd.Parameters.AddWithValue("orgId", orgId.Value);
            cmd.Parameters.AddWithValue("expId", id);
            cmd.Parameters.AddWithValue("rootId", p.rootId);
            cmd.Parameters.AddWithValue("lineId", p.lineId);
            cmd.Parameters.AddWithValue("date", p.date);
            cmd.Parameters.AddWithValue("showId", p.showId);
            cmd.Parameters.AddWithValue("seasonId", p.seasonId);
            cmd.Parameters.AddWithValue("deptId", p.deptId);
            cmd.Parameters.AddWithValue("catId", p.catId);
            cmd.Parameters.AddWithValue("amt", p.amount);
            cmd.Parameters.AddWithValue("ccy", p.ccy);
            cmd.Parameters.AddWithValue("createdBy", userId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        await auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId.Value,
            ActorUserId: userId,
            Action: "expense.approved",
            EntityType: "expense",
            EntityId: id), ct);

        await outboxPublisher.PublishAsync(OutboxEvent.Create(
            aggregateType: "Expense",
            aggregateId: id,
            eventType: "CostPostingRecorded",
            payload: new { expenseId = id, postingCount = postings.Count, orgId = orgId.Value },
            orgId: orgId.Value), ct);

        return Results.Ok(new { message = "Expense approved and actual cost postings recorded." });
    }
}
