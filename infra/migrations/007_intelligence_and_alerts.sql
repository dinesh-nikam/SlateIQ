-- ============================================================================
-- Migration: 007_intelligence_and_alerts.sql
-- Classification: expand
-- Description: Core Phase 1 tables for Variance Intelligence & Alerts (MOD-08)
-- Governing Documents: SLQ-ARCH-002, SLQ-ARCH-008 (§14.8), SLQ-ARCH-013
-- State Machines: SM-16 (Alert Instance)
-- Invariants: Canonical variance formula (Budget - Actual - Committed), Forced RLS
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. INTEL SCHEMA (MOD-08)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS intel.variance_snapshot (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    snapshot_id UUID NOT NULL DEFAULT gen_random_uuid(),
    show_id UUID NOT NULL,
    season_id UUID NOT NULL,
    department_id UUID,
    category_id UUID,
    as_of_date DATE NOT NULL,
    budget_amount NUMERIC(19,4) NOT NULL,
    committed_amount NUMERIC(19,4) NOT NULL,
    actual_amount NUMERIC(19,4) NOT NULL,
    variance_amount NUMERIC(19,4) NOT NULL,
    burn_rate_daily NUMERIC(19,4),
    projected_final_cost NUMERIC(19,4),
    currency CHAR(3) NOT NULL DEFAULT 'INR',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    PRIMARY KEY (org_id, snapshot_id)
);

CREATE TABLE IF NOT EXISTS intel.alert_rule (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    rule_id UUID NOT NULL DEFAULT gen_random_uuid(),
    show_id UUID,
    name TEXT NOT NULL,
    rule_code TEXT NOT NULL,
    threshold_percentage NUMERIC(5,2) NOT NULL CHECK (threshold_percentage >= 0),
    severity TEXT NOT NULL CHECK (severity IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, rule_id),
    CONSTRAINT uq_alert_rule_code UNIQUE NULLS NOT DISTINCT (org_id, show_id, rule_code)
);

CREATE TABLE IF NOT EXISTS intel.alert_instance (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    alert_id UUID NOT NULL DEFAULT gen_random_uuid(),
    rule_id UUID NOT NULL,
    show_id UUID NOT NULL,
    department_id UUID,
    category_id UUID,
    title TEXT NOT NULL,
    message TEXT NOT NULL,
    current_variance_amount NUMERIC(19,4) NOT NULL,
    severity TEXT NOT NULL CHECK (severity IN ('LOW', 'MEDIUM', 'HIGH', 'CRITICAL')),
    status TEXT NOT NULL DEFAULT 'TRIGGERED' CHECK (status IN ('TRIGGERED', 'ACKNOWLEDGED', 'RESOLVED', 'FALSE_POSITIVE', 'ESCALATED')),
    triggered_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    resolved_at TIMESTAMPTZ,
    resolution_note TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, alert_id),
    CONSTRAINT fk_alert_instance_rule FOREIGN KEY (org_id, rule_id) REFERENCES intel.alert_rule(org_id, rule_id) ON DELETE CASCADE,
    CONSTRAINT ck_alert_resolved CHECK (status NOT IN ('RESOLVED', 'FALSE_POSITIVE') OR resolution_note IS NOT NULL)
);

-- ----------------------------------------------------------------------------
-- 2. ROW-LEVEL SECURITY ENFORCEMENT & POLICIES (ADR-002, ADR-035)
-- ----------------------------------------------------------------------------

ALTER TABLE intel.variance_snapshot ENABLE ROW LEVEL SECURITY;
ALTER TABLE intel.variance_snapshot FORCE ROW LEVEL SECURITY;
CREATE POLICY p_intel_variance_snapshot_tenant_isolation ON intel.variance_snapshot
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE intel.alert_rule ENABLE ROW LEVEL SECURITY;
ALTER TABLE intel.alert_rule FORCE ROW LEVEL SECURITY;
CREATE POLICY p_intel_alert_rule_tenant_isolation ON intel.alert_rule
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE intel.alert_instance ENABLE ROW LEVEL SECURITY;
ALTER TABLE intel.alert_instance FORCE ROW LEVEL SECURITY;
CREATE POLICY p_intel_alert_instance_tenant_isolation ON intel.alert_instance
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA intel TO slateiq_app;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA intel TO slateiq_migrator;
