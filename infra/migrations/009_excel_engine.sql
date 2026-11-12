-- ============================================================================
-- Migration: 009_excel_engine.sql
-- Classification: expand
-- Description: Core Phase 1 tables for Excel Import & Validation Engine (MOD-10)
-- Governing Documents: SLQ-ARCH-002, SLQ-ARCH-008 (§14.10), SLQ-ARCH-012
-- State Machines: SM-17 (Import Batch)
-- Invariants: No partial commit without confirmation, Forced RLS
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. XLS SCHEMA (MOD-10)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS xls.import_batch (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    batch_id UUID NOT NULL DEFAULT gen_random_uuid(),
    show_id UUID NOT NULL,
    template_type TEXT NOT NULL CHECK (template_type IN ('BUDGET', 'EXPENSES', 'VENDORS', 'PAYROLL')),
    storage_document_id UUID NOT NULL,
    total_rows INT NOT NULL DEFAULT 0,
    valid_rows INT NOT NULL DEFAULT 0,
    error_rows INT NOT NULL DEFAULT 0,
    status TEXT NOT NULL DEFAULT 'UPLOADED' CHECK (status IN ('UPLOADED', 'SCANNING', 'PARSING', 'VALIDATING', 'VALIDATED', 'AWAITING_COMMIT_APPROVAL', 'COMMITTING', 'COMMITTED', 'PARTIALLY_COMMITTED', 'REJECTED', 'FAILED')),
    error_summary_json JSONB NOT NULL DEFAULT '[]'::jsonb,
    uploaded_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    committed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, batch_id)
);

CREATE TABLE IF NOT EXISTS xls.import_row_error (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    error_id UUID NOT NULL DEFAULT gen_random_uuid(),
    batch_id UUID NOT NULL,
    row_number INT NOT NULL,
    column_name TEXT NOT NULL,
    error_code TEXT NOT NULL,
    raw_value TEXT,
    error_message TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (org_id, error_id),
    CONSTRAINT fk_row_error_batch FOREIGN KEY (org_id, batch_id) REFERENCES xls.import_batch(org_id, batch_id) ON DELETE CASCADE
);

-- ----------------------------------------------------------------------------
-- 2. ROW-LEVEL SECURITY ENFORCEMENT & POLICIES (ADR-002, ADR-035)
-- ----------------------------------------------------------------------------

ALTER TABLE xls.import_batch ENABLE ROW LEVEL SECURITY;
ALTER TABLE xls.import_batch FORCE ROW LEVEL SECURITY;
CREATE POLICY p_xls_import_batch_tenant_isolation ON xls.import_batch
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE xls.import_row_error ENABLE ROW LEVEL SECURITY;
ALTER TABLE xls.import_row_error FORCE ROW LEVEL SECURITY;
CREATE POLICY p_xls_import_row_error_tenant_isolation ON xls.import_row_error
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA xls TO slateiq_app;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA xls TO slateiq_migrator;
