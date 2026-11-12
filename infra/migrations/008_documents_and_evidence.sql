-- ============================================================================
-- Migration: 008_documents_and_evidence.sql
-- Classification: expand
-- Description: Core Phase 1 tables for Supporting Document Evidence (MOD-09)
-- Governing Documents: SLQ-ARCH-002, SLQ-ARCH-008 (§14.7), SLQ-ARCH-010
-- State Machines: SM-12 (Document Lifecycle)
-- Invariants: WORM immutability, Virus scan gate, Forced RLS
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. DOCS SCHEMA (MOD-09)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS docs.document (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    document_id UUID NOT NULL DEFAULT gen_random_uuid(),
    storage_bucket TEXT NOT NULL,
    storage_key TEXT NOT NULL,
    filename TEXT NOT NULL,
    content_type TEXT NOT NULL,
    file_size_bytes BIGINT NOT NULL CHECK (file_size_bytes > 0),
    sha256_hash CHAR(64) NOT NULL,
    scan_status TEXT NOT NULL DEFAULT 'PENDING' CHECK (scan_status IN ('PENDING', 'CLEAN', 'INFECTED', 'FAILED')),
    quarantine_reason TEXT,
    status TEXT NOT NULL DEFAULT 'UPLOAD_PENDING' CHECK (status IN ('UPLOAD_PENDING', 'PENDING_SCAN', 'AVAILABLE', 'QUARANTINED', 'SCAN_FAILED', 'SOFT_DELETED', 'PURGED')),
    uploaded_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    scanned_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, document_id),
    CONSTRAINT ck_document_quarantined CHECK (status <> 'QUARANTINED' OR quarantine_reason IS NOT NULL)
);

CREATE TABLE IF NOT EXISTS docs.upload_ticket (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    ticket_id UUID NOT NULL DEFAULT gen_random_uuid(),
    document_id UUID NOT NULL,
    storage_key TEXT NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    status TEXT NOT NULL DEFAULT 'ISSUED' CHECK (status IN ('ISSUED', 'CONSUMED', 'EXPIRED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    PRIMARY KEY (org_id, ticket_id),
    CONSTRAINT fk_ticket_doc FOREIGN KEY (org_id, document_id) REFERENCES docs.document(org_id, document_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS docs.document_access_log (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    log_id UUID NOT NULL DEFAULT gen_random_uuid(),
    document_id UUID NOT NULL,
    actor_user_id UUID NOT NULL,
    action TEXT NOT NULL CHECK (action IN ('UPLOAD', 'DOWNLOAD', 'VIEW', 'DELETE')),
    ip_address INET,
    user_agent TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (org_id, log_id)
);

-- ----------------------------------------------------------------------------
-- 2. ROW-LEVEL SECURITY ENFORCEMENT & POLICIES (ADR-002, ADR-035)
-- ----------------------------------------------------------------------------

ALTER TABLE docs.document ENABLE ROW LEVEL SECURITY;
ALTER TABLE docs.document FORCE ROW LEVEL SECURITY;
CREATE POLICY p_docs_document_tenant_isolation ON docs.document
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE docs.upload_ticket ENABLE ROW LEVEL SECURITY;
ALTER TABLE docs.upload_ticket FORCE ROW LEVEL SECURITY;
CREATE POLICY p_docs_upload_ticket_tenant_isolation ON docs.upload_ticket
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE docs.document_access_log ENABLE ROW LEVEL SECURITY;
ALTER TABLE docs.document_access_log FORCE ROW LEVEL SECURITY;
CREATE POLICY p_docs_document_access_log_tenant_isolation ON docs.document_access_log
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA docs TO slateiq_app;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA docs TO slateiq_migrator;
