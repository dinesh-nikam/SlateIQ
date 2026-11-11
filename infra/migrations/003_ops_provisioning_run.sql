-- ============================================================================
-- Migration: 003_ops_provisioning_run.sql
-- Classification: expand
-- Description: Platform Operations provisioning run tracking table (MOD-15 / MOD-01)
-- Governing Documents: SLQ-ARCH-003 §2.9.1, §10.1; SLQ-ARCH-004 §3.1 WF-01; ADR-002
-- ============================================================================

CREATE TABLE IF NOT EXISTS ops.provisioning_run (
    run_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id UUID REFERENCES tenancy.organization(id) ON DELETE SET NULL,
    slug TEXT NOT NULL,
    region CHAR(2) NOT NULL CHECK (region IN ('IN', 'EU')),
    status TEXT NOT NULL DEFAULT 'RUNNING' CHECK (status IN ('RUNNING', 'SUCCEEDED', 'FAILED', 'COMPENSATED')),
    current_step INT NOT NULL DEFAULT 1 CHECK (current_step BETWEEN 1 AND 10),
    step_log JSONB NOT NULL DEFAULT '[]'::jsonb,
    idempotency_key TEXT NOT NULL UNIQUE,
    requested_by UUID,
    started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at TIMESTAMPTZ,
    last_error TEXT
);

CREATE INDEX IF NOT EXISTS ix_provisioning_run_slug ON ops.provisioning_run (slug);
CREATE INDEX IF NOT EXISTS ix_provisioning_run_status ON ops.provisioning_run (status);

-- Grant privileges to platform operators and migrators
GRANT SELECT, INSERT, UPDATE ON ops.provisioning_run TO slateiq_ops;
GRANT ALL PRIVILEGES ON ops.provisioning_run TO slateiq_migrator;
