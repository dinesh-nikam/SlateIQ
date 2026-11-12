-- ============================================================================
-- Migration: 006_audit_operations.sql
-- Classification: expand
-- Description: Core Phase 1 tables for Audit Operations & Findings (MOD-06)
-- Governing Documents: SLQ-ARCH-002, SLQ-ARCH-004, SLQ-ARCH-008 (§14.5)
-- State Machines: SM-08 (Audit Assignment), SM-10 (Audit Report), SM-11 (Audit Finding)
-- Invariants: Independent auditor mandates, visibility rules, Forced RLS
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. AUDITOPS SCHEMA (MOD-06)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS auditops.audit_team (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    team_id UUID NOT NULL DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    lead_user_id UUID NOT NULL,
    status TEXT NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'DISBANDED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, team_id),
    CONSTRAINT uq_audit_team_name UNIQUE (org_id, name)
);

CREATE TABLE IF NOT EXISTS auditops.audit_team_member (
    org_id UUID NOT NULL,
    team_id UUID NOT NULL,
    user_id UUID NOT NULL,
    team_role TEXT NOT NULL CHECK (team_role IN ('LEAD', 'MEMBER')),
    joined_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    left_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    PRIMARY KEY (org_id, team_id, user_id, joined_at),
    CONSTRAINT fk_team_member_team FOREIGN KEY (org_id, team_id) REFERENCES auditops.audit_team(org_id, team_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS auditops.audit_assignment (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    audit_assignment_id UUID NOT NULL DEFAULT gen_random_uuid(),
    show_id UUID NOT NULL,
    auditor_user_id UUID NOT NULL,
    team_id UUID,
    assignment_type TEXT NOT NULL CHECK (assignment_type IN ('INTERNAL', 'PLATFORM_APPOINTED', 'EXTERNAL')),
    appointing_party_id UUID,
    grant_id UUID,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    permission_level TEXT NOT NULL CHECK (permission_level IN ('READ_ONLY', 'CAPTURE', 'REVIEW')),
    status TEXT NOT NULL DEFAULT 'PLANNED' CHECK (status IN ('PLANNED', 'ACTIVE', 'COMPLETED', 'REVOKED')),
    handed_over_at TIMESTAMPTZ,
    revoked_by UUID,
    revoke_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, audit_assignment_id),
    CONSTRAINT uq_audit_assignment_denorm UNIQUE (org_id, show_id, audit_assignment_id),
    CONSTRAINT ck_audit_dates CHECK (end_date >= start_date),
    CONSTRAINT ck_audit_revoked CHECK (status <> 'REVOKED' OR revoke_reason IS NOT NULL)
);

CREATE TABLE IF NOT EXISTS auditops.audit_report (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    audit_report_id UUID NOT NULL DEFAULT gen_random_uuid(),
    audit_assignment_id UUID NOT NULL,
    show_id UUID NOT NULL,
    report_type TEXT NOT NULL CHECK (report_type IN ('DAILY', 'WEEKLY', 'FORTNIGHTLY', 'MONTHLY', 'CLOSING')),
    report_date DATE,
    period_start DATE,
    period_end DATE,
    version_no INT NOT NULL DEFAULT 1 CHECK (version_no >= 1),
    summary TEXT NOT NULL,
    totals_snapshot JSONB NOT NULL DEFAULT '{}'::jsonb,
    petty_cash_checked BOOLEAN NOT NULL DEFAULT false,
    petty_cash_variance NUMERIC(19,4),
    status TEXT NOT NULL DEFAULT 'DRAFT' CHECK (status IN ('DRAFT', 'SUBMITTED', 'ACCEPTED', 'RETURNED')),
    submitted_at TIMESTAMPTZ,
    accepted_by UUID,
    accepted_at TIMESTAMPTZ,
    returned_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, audit_report_id),
    CONSTRAINT fk_report_assignment FOREIGN KEY (org_id, show_id, audit_assignment_id) REFERENCES auditops.audit_assignment(org_id, show_id, audit_assignment_id) ON DELETE CASCADE,
    CONSTRAINT ck_report_accepted CHECK (status <> 'ACCEPTED' OR accepted_by IS NOT NULL)
);

CREATE TABLE IF NOT EXISTS auditops.audit_finding (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    finding_id UUID NOT NULL DEFAULT gen_random_uuid(),
    audit_assignment_id UUID NOT NULL,
    show_id UUID NOT NULL,
    department_id UUID,
    severity TEXT NOT NULL CHECK (severity IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')),
    finding_category TEXT NOT NULL,
    title TEXT NOT NULL,
    description TEXT NOT NULL,
    linked_entity_type TEXT CHECK (linked_entity_type IN ('EXPENSE', 'VENDOR', 'COMMITMENT')),
    linked_entity_id UUID,
    visibility TEXT NOT NULL DEFAULT 'SHARED_WITH_PRODUCTION' CHECK (visibility IN ('APPOINTING_PARTY_ONLY', 'SHARED_WITH_PRODUCTION')),
    due_date DATE,
    status TEXT NOT NULL DEFAULT 'DRAFT' CHECK (status IN ('DRAFT', 'OPEN', 'RESOLVED', 'CLOSED', 'WITHDRAWN')),
    closure_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, finding_id),
    CONSTRAINT fk_finding_assignment FOREIGN KEY (org_id, show_id, audit_assignment_id) REFERENCES auditops.audit_assignment(org_id, show_id, audit_assignment_id) ON DELETE CASCADE,
    CONSTRAINT ck_finding_closed CHECK (status <> 'CLOSED' OR closure_reason IS NOT NULL)
);

CREATE TABLE IF NOT EXISTS auditops.finding_response (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    response_id UUID NOT NULL DEFAULT gen_random_uuid(),
    finding_id UUID NOT NULL,
    responder_user_id UUID NOT NULL,
    response_text TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (org_id, response_id)
);

-- ----------------------------------------------------------------------------
-- 2. ROW-LEVEL SECURITY ENFORCEMENT & POLICIES (ADR-002, ADR-035)
-- ----------------------------------------------------------------------------

ALTER TABLE auditops.audit_team ENABLE ROW LEVEL SECURITY;
ALTER TABLE auditops.audit_team FORCE ROW LEVEL SECURITY;
CREATE POLICY p_auditops_audit_team_tenant_isolation ON auditops.audit_team
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE auditops.audit_team_member ENABLE ROW LEVEL SECURITY;
ALTER TABLE auditops.audit_team_member FORCE ROW LEVEL SECURITY;
CREATE POLICY p_auditops_audit_team_member_tenant_isolation ON auditops.audit_team_member
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE auditops.audit_assignment ENABLE ROW LEVEL SECURITY;
ALTER TABLE auditops.audit_assignment FORCE ROW LEVEL SECURITY;
CREATE POLICY p_auditops_audit_assignment_tenant_isolation ON auditops.audit_assignment
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE auditops.audit_report ENABLE ROW LEVEL SECURITY;
ALTER TABLE auditops.audit_report FORCE ROW LEVEL SECURITY;
CREATE POLICY p_auditops_audit_report_tenant_isolation ON auditops.audit_report
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE auditops.audit_finding ENABLE ROW LEVEL SECURITY;
ALTER TABLE auditops.audit_finding FORCE ROW LEVEL SECURITY;
CREATE POLICY p_auditops_audit_finding_tenant_isolation ON auditops.audit_finding
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE auditops.finding_response ENABLE ROW LEVEL SECURITY;
ALTER TABLE auditops.finding_response FORCE ROW LEVEL SECURITY;
CREATE POLICY p_auditops_finding_response_tenant_isolation ON auditops.finding_response
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA auditops TO slateiq_app;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA auditops TO slateiq_migrator;
