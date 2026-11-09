-- ============================================================================
-- Migration: 001_initial_foundation_tenancy_identity_ops_compliance.sql
-- Classification: expand
-- Description: Core Phase 0 tables for Tenancy, Identity, Ops, and Compliance
-- Governing Documents: SLQ-ARCH-003, SLQ-ARCH-008, SLQ-ARCH-010, SLQ-ARCH-014
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. TENANCY SCHEMA (MOD-01)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS tenancy.organization (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code TEXT NOT NULL UNIQUE,
    legal_name TEXT NOT NULL,
    organization_type TEXT NOT NULL CHECK (organization_type IN ('OTT_PLATFORM', 'BROADCASTER', 'PRODUCTION_HOUSE', 'AUDIT_FIRM', 'STUDIO')),
    status TEXT NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('PROVISIONING', 'PROVISIONING_FAILED', 'ACTIVE', 'SUSPENDED', 'ARCHIVED', 'PENDING_DELETION', 'PURGING', 'PURGED')),
    base_currency CHAR(3) NOT NULL DEFAULT 'INR',
    tier TEXT NOT NULL DEFAULT 'STANDARD' CHECK (tier IN ('PILOT', 'STANDARD', 'ENTERPRISE', 'DEDICATED')),
    home_region CHAR(2) NOT NULL DEFAULT 'IN' CHECK (home_region IN ('IN', 'EU')),
    settings_json JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    version INT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS tenancy.organization_setting (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    key TEXT NOT NULL,
    value TEXT NOT NULL,
    data_type TEXT NOT NULL DEFAULT 'STRING' CHECK (data_type IN ('STRING', 'NUMBER', 'BOOLEAN', 'JSON')),
    description TEXT,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by_user_id UUID,
    PRIMARY KEY (org_id, key)
);

CREATE TABLE IF NOT EXISTS tenancy.membership (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    user_id UUID NOT NULL,
    primary_role_code TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'SUSPENDED', 'REVOKED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    version INT NOT NULL DEFAULT 1,
    UNIQUE (org_id, user_id)
);

CREATE TABLE IF NOT EXISTS tenancy.invitation (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    email CITEXT NOT NULL,
    role_code TEXT NOT NULL,
    token_hash TEXT NOT NULL UNIQUE,
    invited_by_user_id UUID NOT NULL,
    status TEXT NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'ACCEPTED', 'EXPIRED', 'REVOKED')),
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    accepted_at TIMESTAMPTZ,
    version INT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS tenancy.delegation (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    delegator_user_id UUID NOT NULL,
    delegate_user_id UUID NOT NULL,
    role_code TEXT NOT NULL,
    starts_at TIMESTAMPTZ NOT NULL,
    ends_at TIMESTAMPTZ NOT NULL,
    reason TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'EXPIRED', 'REVOKED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    version INT NOT NULL DEFAULT 1,
    CONSTRAINT ck_delegation_duration CHECK (ends_at > starts_at AND ends_at <= starts_at + INTERVAL '90 days'),
    CONSTRAINT ck_delegation_no_self CHECK (delegator_user_id <> delegate_user_id)
);

CREATE TABLE IF NOT EXISTS tenancy.access_grant (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    grantee_org_id UUID NOT NULL REFERENCES tenancy.organization(id),
    profile_code TEXT NOT NULL CHECK (profile_code IN ('AUDIT_READ', 'AUDIT_EVIDENCE', 'AUDIT_FINANCE')),
    scope_type TEXT NOT NULL CHECK (scope_type IN ('SHOW', 'DEPARTMENT')),
    scope_id UUID NOT NULL,
    status TEXT NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('PENDING', 'ACTIVE', 'SUSPENDED', 'EXPIRED', 'REVOKED')),
    valid_from TIMESTAMPTZ NOT NULL,
    valid_to TIMESTAMPTZ NOT NULL,
    approved_by_user_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    version INT NOT NULL DEFAULT 1,
    CONSTRAINT ck_grant_validity CHECK (valid_to > valid_from AND valid_to <= valid_from + INTERVAL '365 days')
);

-- ----------------------------------------------------------------------------
-- 2. IDENTITY SCHEMA (MOD-02)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS identity.user (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    home_org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE RESTRICT,
    email CITEXT NOT NULL UNIQUE,
    full_name TEXT NOT NULL,
    phone_number TEXT,
    status TEXT NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('INVITED', 'ACTIVE', 'SUSPENDED', 'LOCKED', 'DEACTIVATED')),
    mfa_enforced BOOLEAN NOT NULL DEFAULT false,
    failed_login_attempts INT NOT NULL DEFAULT 0,
    lockout_until TIMESTAMPTZ,
    last_login_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    version INT NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS identity.credential (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL UNIQUE REFERENCES identity.user(id) ON DELETE CASCADE,
    password_hash TEXT NOT NULL,
    algorithm TEXT NOT NULL DEFAULT 'argon2id',
    salt TEXT NOT NULL,
    iterations INT NOT NULL DEFAULT 3,
    memory_kb INT NOT NULL DEFAULT 65536,
    parallelism INT NOT NULL DEFAULT 4,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS identity.mfa_factor (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES identity.user(id) ON DELETE CASCADE,
    factor_type TEXT NOT NULL DEFAULT 'TOTP' CHECK (factor_type IN ('TOTP', 'FIDO2')),
    secret_encrypted TEXT NOT NULL,
    is_verified BOOLEAN NOT NULL DEFAULT false,
    enrolled_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_used_at TIMESTAMPTZ,
    PRIMARY KEY (user_id, factor_type)
);

CREATE TABLE IF NOT EXISTS identity.refresh_session (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES identity.user(id) ON DELETE CASCADE,
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    token_family_id UUID NOT NULL,
    token_hash TEXT NOT NULL UNIQUE,
    device_fingerprint TEXT,
    ip_address INET,
    user_agent TEXT,
    expires_at TIMESTAMPTZ NOT NULL,
    revoked_at TIMESTAMPTZ,
    replaced_by_token_hash TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS identity.device_registration (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES identity.user(id) ON DELETE CASCADE,
    device_fingerprint TEXT NOT NULL,
    device_model TEXT NOT NULL,
    os_name TEXT NOT NULL,
    os_version TEXT NOT NULL,
    app_version TEXT NOT NULL,
    public_key_spki TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'SUSPENDED', 'REVOKED', 'WIPED')),
    registered_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_sync_at TIMESTAMPTZ,
    UNIQUE (user_id, device_fingerprint)
);

-- ----------------------------------------------------------------------------
-- 3. COMPLIANCE SCHEMA (MOD-12 - In-Transaction Audit Writer)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS compliance.audit_event (
    id UUID DEFAULT gen_random_uuid(),
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE RESTRICT,
    actor_user_id UUID,
    action TEXT NOT NULL,
    entity_type TEXT NOT NULL,
    entity_id UUID NOT NULL,
    previous_state JSONB,
    new_state JSONB,
    metadata JSONB NOT NULL DEFAULT '{}'::jsonb,
    correlation_id TEXT,
    source_ip INET,
    user_agent TEXT,
    previous_hash CHAR(64) NOT NULL,
    current_hash CHAR(64) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (created_at, id)
) PARTITION BY RANGE (created_at);

-- Initial Monthly Partitions for Audit Events (2026-11 to 2027-02)
CREATE TABLE IF NOT EXISTS compliance.audit_event_y2026m11 PARTITION OF compliance.audit_event
    FOR VALUES FROM ('2026-11-01 00:00:00+00') TO ('2026-12-01 00:00:00+00');
CREATE TABLE IF NOT EXISTS compliance.audit_event_y2026m12 PARTITION OF compliance.audit_event
    FOR VALUES FROM ('2026-12-01 00:00:00+00') TO ('2027-01-01 00:00:00+00');
CREATE TABLE IF NOT EXISTS compliance.audit_event_y2027m01 PARTITION OF compliance.audit_event
    FOR VALUES FROM ('2027-01-01 00:00:00+00') TO ('2027-02-01 00:00:00+00');
CREATE TABLE IF NOT EXISTS compliance.audit_event_y2027m02 PARTITION OF compliance.audit_event
    FOR VALUES FROM ('2027-02-01 00:00:00+00') TO ('2027-03-01 00:00:00+00');

-- Append-Only Invariant Trigger for Audit Events (Document 14 §13, ADR-012, ADR-060)
CREATE OR REPLACE FUNCTION compliance.trg_audit_event_append_only()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'compliance.audit_event is immutable; UPDATE and DELETE are prohibited'
        USING ERRCODE = '23505';
END;
$$;

DROP TRIGGER IF EXISTS trg_audit_event_no_modify ON compliance.audit_event;
CREATE TRIGGER trg_audit_event_no_modify
    BEFORE UPDATE OR DELETE ON compliance.audit_event
    FOR EACH ROW EXECUTE FUNCTION compliance.trg_audit_event_append_only();

-- ----------------------------------------------------------------------------
-- 4. PLATFORM OPERATIONS SCHEMA (MOD-15 - Outbox, Jobs, Locks)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS ops.outbox_event (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id UUID NOT NULL UNIQUE,
    aggregate_type TEXT NOT NULL,
    aggregate_id UUID NOT NULL,
    event_type TEXT NOT NULL,
    org_id UUID REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    payload JSONB NOT NULL,
    correlation_id TEXT,
    status TEXT NOT NULL DEFAULT 'PENDING' CHECK (status IN ('PENDING', 'PROCESSING', 'SUCCEEDED', 'FAILED', 'DEAD', 'CANCELLED')),
    attempts INT NOT NULL DEFAULT 0,
    max_attempts INT NOT NULL DEFAULT 5,
    last_error TEXT,
    scheduled_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    processed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS ops.inbox_dedup (
    consumer_name TEXT NOT NULL,
    event_id UUID NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (consumer_name, event_id)
);

CREATE TABLE IF NOT EXISTS ops.job_lock (
    lock_key TEXT PRIMARY KEY,
    locked_by TEXT NOT NULL,
    locked_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ NOT NULL,
    CONSTRAINT ck_lock_expiration CHECK (expires_at > locked_at)
);

CREATE TABLE IF NOT EXISTS ops.tenant_region (
    org_id UUID PRIMARY KEY REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    region_code CHAR(2) NOT NULL CHECK (region_code IN ('IN', 'EU')),
    cell_id TEXT NOT NULL DEFAULT 'shared-cell-01',
    db_cluster_endpoint TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS ops.feature_flag (
    flag_key TEXT PRIMARY KEY,
    description TEXT NOT NULL,
    is_enabled_globally BOOLEAN NOT NULL DEFAULT false,
    enabled_org_ids UUID[] DEFAULT ARRAY[]::UUID[],
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ----------------------------------------------------------------------------
-- 5. INDEXING STRATEGY
-- ----------------------------------------------------------------------------

CREATE INDEX IF NOT EXISTS ix_org_code ON tenancy.organization (code);
CREATE INDEX IF NOT EXISTS ix_membership_user ON tenancy.membership (user_id);
CREATE INDEX IF NOT EXISTS ix_membership_org_role ON tenancy.membership (org_id, primary_role_code);
CREATE INDEX IF NOT EXISTS ix_invitation_email ON tenancy.invitation (org_id, email) WHERE status = 'PENDING';
CREATE INDEX IF NOT EXISTS ix_delegation_active ON tenancy.delegation (org_id, delegate_user_id) WHERE status = 'ACTIVE';
CREATE INDEX IF NOT EXISTS ix_grant_grantee ON tenancy.access_grant (grantee_org_id, status);

CREATE INDEX IF NOT EXISTS ix_user_home_org ON identity.user (home_org_id);
CREATE INDEX IF NOT EXISTS ix_refresh_session_lookup ON identity.refresh_session (token_hash) WHERE revoked_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_refresh_session_family ON identity.refresh_session (token_family_id);

CREATE INDEX IF NOT EXISTS ix_audit_event_lookup ON compliance.audit_event (org_id, entity_type, entity_id);
CREATE INDEX IF NOT EXISTS ix_audit_event_actor ON compliance.audit_event (org_id, actor_user_id);

CREATE INDEX IF NOT EXISTS ix_outbox_pending ON ops.outbox_event (status, scheduled_at) WHERE status IN ('PENDING', 'FAILED');

-- ----------------------------------------------------------------------------
-- 6. ROW-LEVEL SECURITY ENFORCEMENT & POLICIES (ADR-002, ADR-035)
-- ----------------------------------------------------------------------------

-- Enable and FORCE RLS on all Tenancy tables
ALTER TABLE tenancy.organization ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.organization FORCE ROW LEVEL SECURITY;

ALTER TABLE tenancy.organization_setting ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.organization_setting FORCE ROW LEVEL SECURITY;

ALTER TABLE tenancy.membership ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.membership FORCE ROW LEVEL SECURITY;

ALTER TABLE tenancy.invitation ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.invitation FORCE ROW LEVEL SECURITY;

ALTER TABLE tenancy.delegation ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.delegation FORCE ROW LEVEL SECURITY;

ALTER TABLE tenancy.access_grant ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.access_grant FORCE ROW LEVEL SECURITY;

-- Enable and FORCE RLS on Identity tables
ALTER TABLE identity.user ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.user FORCE ROW LEVEL SECURITY;

ALTER TABLE identity.credential ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.credential FORCE ROW LEVEL SECURITY;

ALTER TABLE identity.mfa_factor ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.mfa_factor FORCE ROW LEVEL SECURITY;

ALTER TABLE identity.refresh_session ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.refresh_session FORCE ROW LEVEL SECURITY;

ALTER TABLE identity.device_registration ENABLE ROW LEVEL SECURITY;
ALTER TABLE identity.device_registration FORCE ROW LEVEL SECURITY;

-- Enable and FORCE RLS on Compliance tables
ALTER TABLE compliance.audit_event ENABLE ROW LEVEL SECURITY;
ALTER TABLE compliance.audit_event FORCE ROW LEVEL SECURITY;

-- Define Policies for `slateiq_app`

-- Organization: Can only see own tenant record
DROP POLICY IF EXISTS org_isolation_policy ON tenancy.organization;
CREATE POLICY org_isolation_policy ON tenancy.organization
    FOR ALL TO slateiq_app
    USING (id = tenancy.current_org_id())
    WITH CHECK (id = tenancy.current_org_id());

-- Organization Settings: Isolated by org_id
DROP POLICY IF EXISTS org_setting_isolation_policy ON tenancy.organization_setting;
CREATE POLICY org_setting_isolation_policy ON tenancy.organization_setting
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

-- Membership: Isolated by org_id
DROP POLICY IF EXISTS membership_isolation_policy ON tenancy.membership;
CREATE POLICY membership_isolation_policy ON tenancy.membership
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

-- Invitation: Isolated by org_id
DROP POLICY IF EXISTS invitation_isolation_policy ON tenancy.invitation;
CREATE POLICY invitation_isolation_policy ON tenancy.invitation
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

-- Delegation: Isolated by org_id
DROP POLICY IF EXISTS delegation_isolation_policy ON tenancy.delegation;
CREATE POLICY delegation_isolation_policy ON tenancy.delegation
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

-- Access Grant: Visible to grantor org OR grantee org
DROP POLICY IF EXISTS access_grant_isolation_policy ON tenancy.access_grant;
CREATE POLICY access_grant_isolation_policy ON tenancy.access_grant
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id() OR grantee_org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

-- User: Visible within home organization
DROP POLICY IF EXISTS user_isolation_policy ON identity.user;
CREATE POLICY user_isolation_policy ON identity.user
    FOR ALL TO slateiq_app
    USING (home_org_id = tenancy.current_org_id())
    WITH CHECK (home_org_id = tenancy.current_org_id());

-- Credential & MFA: Linked through user.home_org_id
DROP POLICY IF EXISTS credential_isolation_policy ON identity.credential;
CREATE POLICY credential_isolation_policy ON identity.credential
    FOR ALL TO slateiq_app
    USING (EXISTS (SELECT 1 FROM identity.user u WHERE u.id = credential.user_id AND u.home_org_id = tenancy.current_org_id()))
    WITH CHECK (EXISTS (SELECT 1 FROM identity.user u WHERE u.id = credential.user_id AND u.home_org_id = tenancy.current_org_id()));

DROP POLICY IF EXISTS mfa_factor_isolation_policy ON identity.mfa_factor;
CREATE POLICY mfa_factor_isolation_policy ON identity.mfa_factor
    FOR ALL TO slateiq_app
    USING (EXISTS (SELECT 1 FROM identity.user u WHERE u.id = mfa_factor.user_id AND u.home_org_id = tenancy.current_org_id()))
    WITH CHECK (EXISTS (SELECT 1 FROM identity.user u WHERE u.id = mfa_factor.user_id AND u.home_org_id = tenancy.current_org_id()));

-- Refresh Session: Isolated by org_id
DROP POLICY IF EXISTS refresh_session_isolation_policy ON identity.refresh_session;
CREATE POLICY refresh_session_isolation_policy ON identity.refresh_session
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

-- Device Registration: Linked through user.home_org_id
DROP POLICY IF EXISTS device_isolation_policy ON identity.device_registration;
CREATE POLICY device_isolation_policy ON identity.device_registration
    FOR ALL TO slateiq_app
    USING (EXISTS (SELECT 1 FROM identity.user u WHERE u.id = device_registration.user_id AND u.home_org_id = tenancy.current_org_id()))
    WITH CHECK (EXISTS (SELECT 1 FROM identity.user u WHERE u.id = device_registration.user_id AND u.home_org_id = tenancy.current_org_id()));

-- Audit Event: Read/Insert isolated by org_id
DROP POLICY IF EXISTS audit_event_isolation_policy ON compliance.audit_event;
CREATE POLICY audit_event_isolation_policy ON compliance.audit_event
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());
