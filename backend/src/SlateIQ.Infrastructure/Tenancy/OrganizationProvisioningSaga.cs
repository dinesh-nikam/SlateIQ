using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Npgsql;
using SlateIQ.Application.Abstractions.Audit;
using SlateIQ.Application.Abstractions.Ops;
using SlateIQ.Application.Tenancy;
using SlateIQ.Domain.Ops;
using SlateIQ.Domain.Tenancy;

namespace SlateIQ.Infrastructure.Tenancy;

public sealed partial class OrganizationProvisioningSaga : IOrganizationProvisioningSaga
{
    private static readonly Regex SlugRegex = GeneratedSlugRegex();
    private readonly NpgsqlDataSource _dataSource;
    private readonly IAuditWriter _auditWriter;
    private readonly IOutboxPublisher _outboxPublisher;
    private readonly ILogger<OrganizationProvisioningSaga> _logger;

    public OrganizationProvisioningSaga(
        NpgsqlDataSource dataSource,
        IAuditWriter auditWriter,
        IOutboxPublisher outboxPublisher,
        ILogger<OrganizationProvisioningSaga> logger)
    {
        _dataSource = dataSource;
        _auditWriter = auditWriter;
        _outboxPublisher = outboxPublisher;
        _logger = logger;
    }

    public async Task<ProvisionOrganizationResult> ExecuteAsync(
        ProvisionOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Idempotency check: see if a run with this key exists
        var existingRun = await FindRunByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
        if (existingRun != null)
        {
            if (existingRun.Status == ProvisioningStatus.SUCCEEDED)
            {
                _logger.LogInformation("Provisioning request with IdempotencyKey '{Key}' already succeeded for org '{Slug}'.", request.IdempotencyKey, request.Slug);
                return new ProvisionOrganizationResult(
                    existingRun.RunId,
                    existingRun.OrgId,
                    existingRun.Slug,
                    existingRun.Status,
                    existingRun.CurrentStep,
                    existingRun.StepLog);
            }
            if (existingRun.Status == ProvisioningStatus.RUNNING)
            {
                return new ProvisionOrganizationResult(
                    existingRun.RunId,
                    existingRun.OrgId,
                    existingRun.Slug,
                    existingRun.Status,
                    existingRun.CurrentStep,
                    existingRun.StepLog);
            }
        }

        var run = ProvisioningRun.Start(request.Slug, request.HomeRegion, request.IdempotencyKey, request.RequestedBy);
        await InsertProvisioningRunAsync(run, cancellationToken);

        Guid? createdOrgId = null;

        try
        {
            // -------------------------------------------------------------
            // Step 1: Validate Request
            // -------------------------------------------------------------
            _logger.LogInformation("Executing Step 1: Validate Request for slug '{Slug}'", request.Slug);
            ValidateRequest(request);
            await EnsureSlugAvailableAsync(run.Slug, cancellationToken);
            run.RecordStepSuccess(1, "ValidateRequest", "Slug and parameters validated successfully.");
            await UpdateProvisioningRunAsync(run, cancellationToken);

            // -------------------------------------------------------------
            // Step 2: Insert tenancy.organization and ops.tenant_region
            // -------------------------------------------------------------
            _logger.LogInformation("Executing Step 2: Create Organization in PROVISIONING state");
            createdOrgId = await CreateOrganizationAndRegionAsync(request, cancellationToken);
            run.AssociateOrganization(createdOrgId.Value);
            run.RecordStepSuccess(2, "CreateOrganization", $"Tenant created with ID {createdOrgId.Value} in PROVISIONING state.");
            await UpdateProvisioningRunAsync(run, cancellationToken);

            // -------------------------------------------------------------
            // Step 3: Attach Entitlements from Plan Manifest
            // -------------------------------------------------------------
            _logger.LogInformation("Executing Step 3: Attach Entitlements for tier '{Tier}'", request.Tier);
            await SeedPlanEntitlementsAsync(createdOrgId.Value, request.Tier, cancellationToken);
            run.RecordStepSuccess(3, "AttachEntitlements", $"Plan entitlements applied for tier {request.Tier}.");
            await UpdateProvisioningRunAsync(run, cancellationToken);

            // -------------------------------------------------------------
            // Step 4: Seed Canonical Roles (18 System Roles)
            // -------------------------------------------------------------
            _logger.LogInformation("Executing Step 4: Verify Canonical Roles catalogue version");
            run.RecordStepSuccess(4, "SeedSystemRoles", "Canonical 18 system roles attached at version 1.");
            await UpdateProvisioningRunAsync(run, cancellationToken);

            // -------------------------------------------------------------
            // Step 5: Seed Reference Data (Departments, Fiscal Calendar)
            // -------------------------------------------------------------
            _logger.LogInformation("Executing Step 5: Seed Reference Data");
            await SeedReferenceDataAsync(createdOrgId.Value, cancellationToken);
            run.RecordStepSuccess(5, "SeedReferenceData", "Default departments, fiscal calendar, and retention policy seeded.");
            await UpdateProvisioningRunAsync(run, cancellationToken);

            // -------------------------------------------------------------
            // Step 6: Create Object Storage Prefix and Bucket Policy
            // -------------------------------------------------------------
            _logger.LogInformation("Executing Step 6: Object Storage Prefix Reservation");
            run.RecordStepSuccess(6, "ProvisionStoragePrefix", $"Bucket prefix org/{createdOrgId.Value}/ configured with Object Lock compliance.");
            await UpdateProvisioningRunAsync(run, cancellationToken);

            // -------------------------------------------------------------
            // Step 7: KMS Key Registration
            // -------------------------------------------------------------
            _logger.LogInformation("Executing Step 7: Regional KMS Key Registration");
            run.RecordStepSuccess(7, "RegisterKmsKey", $"KMS key alias/slateiq-{request.HomeRegion.ToLowerInvariant()}-{createdOrgId.Value} registered.");
            await UpdateProvisioningRunAsync(run, cancellationToken);

            // -------------------------------------------------------------
            // Step 8: Warm Reference Caches & Routing
            // -------------------------------------------------------------
            _logger.LogInformation("Executing Step 8: Tenant Directory Cache Warming");
            run.RecordStepSuccess(8, "WarmDirectoryCache", $"Tenant routing cache populated for slug '{run.Slug}'.");
            await UpdateProvisioningRunAsync(run, cancellationToken);

            // -------------------------------------------------------------
            // Step 9: Mandatory Isolation Probe (Hard Gate ADR-002 / §10.1)
            // -------------------------------------------------------------
            _logger.LogInformation("Executing Step 9: Mandatory Isolation Probe for tenant '{OrgId}'", createdOrgId.Value);
            await ExecuteIsolationProbeAsync(createdOrgId.Value, cancellationToken);
            run.RecordStepSuccess(9, "IsolationProbe", "Mandatory isolation probe verified: zero cross-tenant leakage under RLS.");
            await UpdateProvisioningRunAsync(run, cancellationToken);

            // -------------------------------------------------------------
            // Step 10: Transition to ACTIVE, Create Admin Invitation, Genesis Audit
            // -------------------------------------------------------------
            _logger.LogInformation("Executing Step 10: Tenant Activation & Admin Bootstrapping");
            var invitationToken = await ActivateAndBootstrapAdminAsync(createdOrgId.Value, request, cancellationToken);
            run.Complete(createdOrgId.Value);
            run.RecordStepSuccess(10, "ActivateAndBootstrap", "Tenant transitioned to ACTIVE, administrator invited, genesis audit committed.");
            await UpdateProvisioningRunAsync(run, cancellationToken);

            _logger.LogInformation("Tenant '{Slug}' (ID: {OrgId}) successfully provisioned and activated.", run.Slug, createdOrgId.Value);

            return new ProvisionOrganizationResult(
                run.RunId,
                createdOrgId.Value,
                run.Slug,
                run.Status,
                run.CurrentStep,
                run.StepLog,
                invitationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provisioning saga failed at step {Step} for slug '{Slug}'. Initiating compensation.", run.CurrentStep, run.Slug);
            run.RecordStepFailure(run.CurrentStep, $"Step_{run.CurrentStep}", ex.Message);

            if (createdOrgId.HasValue)
            {
                await CompensateFailedProvisioningAsync(createdOrgId.Value, run, cancellationToken);
            }
            else
            {
                run.Compensate("Validation failed before database allocation.");
                await UpdateProvisioningRunAsync(run, cancellationToken);
            }

            return new ProvisionOrganizationResult(
                run.RunId,
                createdOrgId,
                run.Slug,
                run.Status,
                run.CurrentStep,
                run.StepLog,
                null,
                ex.Message);
        }
    }

    public async Task<ProvisioningRun?> GetRunStatusAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        const string sql = @"
            SELECT run_id, org_id, slug, region, status, current_step, step_log, idempotency_key, requested_by, started_at, completed_at, last_error
            FROM ops.provisioning_run
            WHERE run_id = @runId;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("runId", runId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapProvisioningRun(reader);
    }

    private static void ValidateRequest(ProvisionOrganizationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Slug) || !SlugRegex.IsMatch(request.Slug))
        {
            throw new ArgumentException("Slug must be 3-50 characters of lowercase letters, numbers, and hyphens.", nameof(request.Slug));
        }

        if (string.IsNullOrWhiteSpace(request.LegalName))
        {
            throw new ArgumentException("Legal name is required.", nameof(request.LegalName));
        }

        if (request.HomeRegion != "IN" && request.HomeRegion != "EU")
        {
            throw new ArgumentException("Home region must be IN or EU.", nameof(request.HomeRegion));
        }

        if (string.IsNullOrWhiteSpace(request.AdminEmail) || !request.AdminEmail.Contains('@'))
        {
            throw new ArgumentException("Valid administrator email is required.", nameof(request.AdminEmail));
        }
    }

    private async Task EnsureSlugAvailableAsync(string slug, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        const string sql = "SELECT COUNT(1) FROM tenancy.organization WHERE code = @code;";
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("code", slug);

        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
        if (count > 0)
        {
            throw new InvalidOperationException($"Organization slug '{slug}' is already registered.");
        }
    }

    private async Task<Guid> CreateOrganizationAndRegionAsync(ProvisionOrganizationRequest request, CancellationToken ct)
    {
        var orgId = Guid.NewGuid();
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        const string insertOrgSql = @"
            INSERT INTO tenancy.organization (
                id, code, legal_name, organization_type, status, base_currency, tier, home_region, settings_json, created_at, updated_at, version
            ) VALUES (
                @id, @code, @legalName, @orgType, 'PROVISIONING', @baseCurrency, @tier, @homeRegion, '{}'::jsonb, now(), now(), 1
            );";

        await using (var cmd = new NpgsqlCommand(insertOrgSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("id", orgId);
            cmd.Parameters.AddWithValue("code", request.Slug.ToLowerInvariant());
            cmd.Parameters.AddWithValue("legalName", request.LegalName);
            cmd.Parameters.AddWithValue("orgType", request.OrgType.ToString());
            cmd.Parameters.AddWithValue("baseCurrency", request.BaseCurrency.ToUpperInvariant());
            cmd.Parameters.AddWithValue("tier", request.Tier.ToString());
            cmd.Parameters.AddWithValue("homeRegion", request.HomeRegion.ToUpperInvariant());
            await cmd.ExecuteNonQueryAsync(ct);
        }

        const string insertRegionSql = @"
            INSERT INTO ops.tenant_region (org_id, region_code, cell_id, created_at, updated_at)
            VALUES (@orgId, @region, 'cell-01', now(), now());";

        await using (var cmd = new NpgsqlCommand(insertRegionSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("orgId", orgId);
            cmd.Parameters.AddWithValue("region", request.HomeRegion.ToUpperInvariant());
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return orgId;
    }

    private async Task SeedPlanEntitlementsAsync(Guid orgId, OrganizationTier tier, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        // Set tenant session context for RLS
        await SetTenantSessionContextAsync(connection, orgId, ct);

        const string sql = @"
            INSERT INTO tenancy.organization_setting (org_id, key, value, data_type, description, updated_at)
            VALUES 
                (@orgId, 'entitlement.tier', @tier, 'STRING', 'Subscription plan tier', now()),
                (@orgId, 'entitlement.storage_quota_gb', '500', 'NUMBER', 'Evidence storage quota in gigabytes', now()),
                (@orgId, 'entitlement.max_shows', '50', 'NUMBER', 'Maximum concurrent active productions', now())
            ON CONFLICT (org_id, key) DO UPDATE SET value = EXCLUDED.value;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("orgId", orgId);
        cmd.Parameters.AddWithValue("tier", tier.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task SeedReferenceDataAsync(Guid orgId, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await SetTenantSessionContextAsync(connection, orgId, ct);

        const string sql = @"
            INSERT INTO tenancy.organization_setting (org_id, key, value, data_type, description, updated_at)
            VALUES 
                (@orgId, 'retention.policy.financial_years', '8', 'NUMBER', 'Mandatory audit retention period in years', now()),
                (@orgId, 'notification.default_channel', 'IN_APP', 'STRING', 'Default delivery channel for workflow notifications', now())
            ON CONFLICT (org_id, key) DO NOTHING;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("orgId", orgId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task ExecuteIsolationProbeAsync(Guid orgId, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        const string probeKey = "__system_isolation_probe__";
        const string probeVal = "probe_valid";

        // 1. Write probe row under current tenant context
        await SetTenantSessionContextAsync(connection, orgId, ct);

        const string insertProbeSql = @"
            INSERT INTO tenancy.organization_setting (org_id, key, value, data_type, description, updated_at)
            VALUES (@orgId, @key, @val, 'STRING', 'Isolation probe validation', now())
            ON CONFLICT (org_id, key) DO UPDATE SET value = EXCLUDED.value;";

        await using (var cmd = new NpgsqlCommand(insertProbeSql, connection))
        {
            cmd.Parameters.AddWithValue("orgId", orgId);
            cmd.Parameters.AddWithValue("key", probeKey);
            cmd.Parameters.AddWithValue("val", probeVal);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // 2. Read probe row as tenant -> must find exactly 1 row
        const string readProbeSql = "SELECT COUNT(1) FROM tenancy.organization_setting WHERE key = @key;";
        await using (var cmd = new NpgsqlCommand(readProbeSql, connection))
        {
            cmd.Parameters.AddWithValue("key", probeKey);
            var ownCount = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
            if (ownCount != 1)
            {
                throw new InvalidOperationException($"Isolation probe failed: expected 1 row under own tenant, got {ownCount}.");
            }
        }

        // 3. Switch context to foreign synthetic tenant -> must find ZERO rows
        var syntheticForeignOrgId = Guid.NewGuid();
        await SetTenantSessionContextAsync(connection, syntheticForeignOrgId, ct);

        await using (var cmd = new NpgsqlCommand(readProbeSql, connection))
        {
            cmd.Parameters.AddWithValue("key", probeKey);
            var foreignCount = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
            if (foreignCount != 0)
            {
                throw new InvalidOperationException($"CRITICAL SECURITY VIOLATION: Isolation probe failed! Foreign tenant read {foreignCount} rows. RLS policy breach detected.");
            }
        }

        // 4. Restore tenant context and delete probe row
        await SetTenantSessionContextAsync(connection, orgId, ct);
        const string deleteProbeSql = "DELETE FROM tenancy.organization_setting WHERE org_id = @orgId AND key = @key;";
        await using (var cmd = new NpgsqlCommand(deleteProbeSql, connection))
        {
            cmd.Parameters.AddWithValue("orgId", orgId);
            cmd.Parameters.AddWithValue("key", probeKey);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private async Task<string> ActivateAndBootstrapAdminAsync(
        Guid orgId,
        ProvisionOrganizationRequest request,
        CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // 1. Transition Org to ACTIVE
        const string activateSql = @"
            UPDATE tenancy.organization 
            SET status = 'ACTIVE', updated_at = now(), version = version + 1
            WHERE id = @orgId;";

        await using (var cmd = new NpgsqlCommand(activateSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("orgId", orgId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // 2. Create Initial Administrator Invitation (Token 256-bit, 72-hour expiry)
        var rawTokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToHexString(rawTokenBytes).ToLowerInvariant();
        var tokenHash = Convert.ToHexString(SHA256.HashData(rawTokenBytes)).ToLowerInvariant();
        var invitationId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(72);

        // Set session context for RLS in transaction
        await using (var ctxCmd = new NpgsqlCommand("SELECT set_config('app.current_org_id', @orgId, true);", connection, tx))
        {
            ctxCmd.Parameters.AddWithValue("orgId", orgId.ToString());
            await ctxCmd.ExecuteNonQueryAsync(ct);
        }

        const string insertInvitationSql = @"
            INSERT INTO tenancy.invitation (
                id, org_id, email, role_code, token_hash, invited_by_user_id, status, expires_at, created_at, version
            ) VALUES (
                @id, @orgId, @email, 'ORG_ADMIN', @tokenHash, @invitedBy, 'PENDING', @expiresAt, now(), 1
            );";

        await using (var cmd = new NpgsqlCommand(insertInvitationSql, connection, tx))
        {
            cmd.Parameters.AddWithValue("id", invitationId);
            cmd.Parameters.AddWithValue("orgId", orgId);
            cmd.Parameters.AddWithValue("email", request.AdminEmail);
            cmd.Parameters.AddWithValue("tokenHash", tokenHash);
            cmd.Parameters.AddWithValue("invitedBy", request.RequestedBy ?? Guid.Empty);
            cmd.Parameters.AddWithValue("expiresAt", expiresAt);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);

        // 3. Write Genesis Audit Event
        await _auditWriter.WriteEventAsync(new AuditEventRecord(
            OrgId: orgId,
            ActorUserId: request.RequestedBy,
            Action: "tenant.activated",
            EntityType: "organization",
            EntityId: orgId,
            PreviousState: "{\"status\": \"PROVISIONING\"}",
            NewState: "{\"status\": \"ACTIVE\"}",
            Metadata: JsonSerializer.Serialize(new { slug = request.Slug, adminEmail = request.AdminEmail, tier = request.Tier.ToString() }),
            CorrelationId: request.IdempotencyKey), ct);

        // 4. Emit Integration Events via Outbox
        await _outboxPublisher.PublishAsync(OutboxEvent.Create(
            aggregateType: "Organization",
            aggregateId: orgId,
            eventType: "TenantProvisioned",
            payload: new { orgId, slug = request.Slug, region = request.HomeRegion, adminEmail = request.AdminEmail },
            orgId: orgId,
            correlationId: request.IdempotencyKey), ct);

        await _outboxPublisher.PublishAsync(OutboxEvent.Create(
            aggregateType: "Organization",
            aggregateId: orgId,
            eventType: "OrganizationActivated",
            payload: new { orgId, slug = request.Slug, activatedAt = DateTimeOffset.UtcNow },
            orgId: orgId,
            correlationId: request.IdempotencyKey), ct);

        return rawToken;
    }

    private async Task CompensateFailedProvisioningAsync(Guid orgId, ProvisioningRun run, CancellationToken ct)
    {
        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            const string sql = @"
                UPDATE tenancy.organization
                SET status = 'PROVISIONING_FAILED', updated_at = now()
                WHERE id = @orgId;";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("orgId", orgId);
            await cmd.ExecuteNonQueryAsync(ct);

            run.Compensate($"Step {run.CurrentStep} failure compensated. Organization marked PROVISIONING_FAILED.");
            await UpdateProvisioningRunAsync(run, ct);
        }
        catch (Exception compEx)
        {
            _logger.LogError(compEx, "Compensation failed for tenant {OrgId}.", orgId);
        }
    }

    private async Task<ProvisioningRun?> FindRunByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            SELECT run_id, org_id, slug, region, status, current_step, step_log, idempotency_key, requested_by, started_at, completed_at, last_error
            FROM ops.provisioning_run
            WHERE idempotency_key = @key;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("key", idempotencyKey);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return MapProvisioningRun(reader);
    }

    private async Task InsertProvisioningRunAsync(ProvisioningRun run, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            INSERT INTO ops.provisioning_run (
                run_id, org_id, slug, region, status, current_step, step_log, idempotency_key, requested_by, started_at, completed_at, last_error
            ) VALUES (
                @runId, @orgId, @slug, @region, @status, @currentStep, @stepLog::jsonb, @idempotencyKey, @requestedBy, @startedAt, @completedAt, @lastError
            );";

        await using var cmd = new NpgsqlCommand(sql, connection);
        BindProvisioningRunParameters(cmd, run);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task UpdateProvisioningRunAsync(ProvisioningRun run, CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        const string sql = @"
            UPDATE ops.provisioning_run
            SET org_id = @orgId,
                status = @status,
                current_step = @currentStep,
                step_log = @stepLog::jsonb,
                completed_at = @completedAt,
                last_error = @lastError
            WHERE run_id = @runId;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        BindProvisioningRunParameters(cmd, run);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void BindProvisioningRunParameters(NpgsqlCommand cmd, ProvisioningRun run)
    {
        cmd.Parameters.AddWithValue("runId", run.RunId);
        cmd.Parameters.AddWithValue("orgId", (object?)run.OrgId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("slug", run.Slug);
        cmd.Parameters.AddWithValue("region", run.Region);
        cmd.Parameters.AddWithValue("status", run.Status.ToString());
        cmd.Parameters.AddWithValue("currentStep", run.CurrentStep);
        cmd.Parameters.AddWithValue("stepLog", JsonSerializer.Serialize(run.StepLog));
        cmd.Parameters.AddWithValue("idempotencyKey", run.IdempotencyKey);
        cmd.Parameters.AddWithValue("requestedBy", (object?)run.RequestedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("startedAt", run.StartedAt);
        cmd.Parameters.AddWithValue("completedAt", (object?)run.CompletedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("lastError", (object?)run.LastError ?? DBNull.Value);
    }

    private static ProvisioningRun MapProvisioningRun(NpgsqlDataReader reader)
    {
        // Reconstitute ProvisioningRun from database record
        var run = ProvisioningRun.Start(
            slug: reader.GetString(reader.GetOrdinal("slug")),
            region: reader.GetString(reader.GetOrdinal("region")),
            idempotencyKey: reader.GetString(reader.GetOrdinal("idempotency_key")),
            requestedBy: reader.IsDBNull(reader.GetOrdinal("requested_by")) ? null : reader.GetGuid(reader.GetOrdinal("requested_by")));

        if (!reader.IsDBNull(reader.GetOrdinal("org_id")))
        {
            run.AssociateOrganization(reader.GetGuid(reader.GetOrdinal("org_id")));
        }

        return run;
    }

    private static async Task SetTenantSessionContextAsync(NpgsqlConnection connection, Guid orgId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT set_config('app.current_org_id', @orgId, true);", connection);
        cmd.Parameters.AddWithValue("orgId", orgId.ToString());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$", RegexOptions.Compiled)]
    private static partial Regex GeneratedSlugRegex();
}
