-- ============================================================================
-- Migration: 005_vendors_and_expenses.sql
-- Classification: expand
-- Description: Core Phase 1 tables for Vendors (MOD-05) and Expenses (MOD-07)
-- Governing Documents: SLQ-ARCH-002, SLQ-ARCH-004, SLQ-ARCH-008 (§14.4, §14.6)
-- State Machines: SM-06 (Vendor), SM-07 (Commitment), SM-09 (Expense)
-- Invariants: INV-001 (Scale-4 Money), INV-002 (Split & line sum conservation), Forced RLS
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. VENDOR SCHEMA (MOD-05)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS vendor.vendor (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    vendor_id UUID NOT NULL DEFAULT gen_random_uuid(),
    code TEXT NOT NULL,
    legal_name TEXT NOT NULL,
    normalized_legal_name TEXT NOT NULL,
    trade_name TEXT,
    vendor_type TEXT NOT NULL CHECK (vendor_type IN ('INDIVIDUAL', 'SOLE_PROPRIETOR', 'PARTNERSHIP', 'COMPANY', 'GOVERNMENT', 'OTHER')),
    primary_tax_identifier TEXT,
    status TEXT NOT NULL DEFAULT 'CANDIDATE' CHECK (status IN ('CANDIDATE', 'ACTIVE', 'INACTIVE', 'BLOCKED', 'REJECTED', 'MERGED')),
    risk_rating TEXT NOT NULL DEFAULT 'UNRATED' CHECK (risk_rating IN ('LOW', 'MEDIUM', 'HIGH', 'UNRATED')),
    blocked_reason TEXT,
    merged_into_vendor_id UUID,
    created_via TEXT NOT NULL DEFAULT 'MASTER' CHECK (created_via IN ('MASTER', 'ONE_TIME_PAYEE', 'IMPORT')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, vendor_id),
    CONSTRAINT uq_vendor_code UNIQUE (org_id, code),
    CONSTRAINT ck_vendor_merged CHECK ((status = 'MERGED') = (merged_into_vendor_id IS NOT NULL)),
    CONSTRAINT ck_vendor_blocked CHECK (status <> 'BLOCKED' OR blocked_reason IS NOT NULL)
);

CREATE TABLE IF NOT EXISTS vendor.vendor_tax_registration (
    org_id UUID NOT NULL,
    vendor_id UUID NOT NULL,
    scheme TEXT NOT NULL CHECK (scheme IN ('GSTIN', 'PAN', 'VAT', 'TIN', 'OTHER')),
    identifier TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'ACTIVE' CHECK (status IN ('ACTIVE', 'INACTIVE')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    PRIMARY KEY (org_id, vendor_id, scheme, identifier),
    CONSTRAINT fk_tax_vendor FOREIGN KEY (org_id, vendor_id) REFERENCES vendor.vendor(org_id, vendor_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS vendor.manual_commitment (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    commitment_id UUID NOT NULL DEFAULT gen_random_uuid(),
    commitment_number TEXT NOT NULL,
    show_id UUID NOT NULL,
    season_id UUID,
    vendor_id UUID NOT NULL,
    department_id UUID NOT NULL,
    category_id UUID NOT NULL,
    commitment_type TEXT NOT NULL CHECK (commitment_type IN ('DEAL_MEMO', 'RENTAL', 'CONTRACT', 'OTHER')),
    description TEXT NOT NULL,
    amount NUMERIC(19,4) NOT NULL CHECK (amount > 0),
    base_amount NUMERIC(19,4),
    currency CHAR(3) NOT NULL DEFAULT 'INR',
    valid_from DATE NOT NULL,
    valid_to DATE,
    status TEXT NOT NULL DEFAULT 'DRAFT' CHECK (status IN ('DRAFT', 'PENDING_APPROVAL', 'OPEN', 'CLOSED', 'CANCELLED', 'EXPIRED')),
    closed_at TIMESTAMPTZ,
    close_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, commitment_id),
    CONSTRAINT uq_commitment_number UNIQUE (org_id, commitment_number),
    CONSTRAINT fk_commitment_vendor FOREIGN KEY (org_id, vendor_id) REFERENCES vendor.vendor(org_id, vendor_id) ON DELETE RESTRICT,
    CONSTRAINT ck_commitment_dates CHECK (valid_to IS NULL OR valid_to >= valid_from),
    CONSTRAINT ck_commitment_closed CHECK (status <> 'CLOSED' OR close_reason IS NOT NULL)
);

-- ----------------------------------------------------------------------------
-- 2. EXPENSE SCHEMA (MOD-07)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS expense.expense_key (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE RESTRICT,
    expense_id UUID NOT NULL DEFAULT gen_random_uuid(),
    root_expense_id UUID NOT NULL,
    revision_no INT NOT NULL DEFAULT 1 CHECK (revision_no >= 1),
    expense_number TEXT,
    show_id UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    PRIMARY KEY (org_id, expense_id),
    CONSTRAINT uq_expense_root_rev UNIQUE (org_id, root_expense_id, revision_no)
);

CREATE TABLE IF NOT EXISTS expense.expense (
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE RESTRICT,
    expense_id UUID NOT NULL,
    root_expense_id UUID NOT NULL,
    revision_no INT NOT NULL DEFAULT 1 CHECK (revision_no >= 1),
    supersedes_expense_id UUID,
    expense_number TEXT,
    show_id UUID NOT NULL,
    season_id UUID,
    department_id UUID,
    vendor_id UUID,
    expense_date DATE NOT NULL,
    document_type TEXT NOT NULL CHECK (document_type IN ('TAX_INVOICE', 'BILL', 'RECEIPT', 'PETTY_CASH_VOUCHER', 'CREDIT_NOTE', 'OTHER')),
    document_number TEXT,
    payment_mode TEXT CHECK (payment_mode IN ('CASH', 'PETTY_CASH', 'BANK_TRANSFER', 'CARD', 'UPI', 'CHEQUE', 'OTHER')),
    description TEXT NOT NULL,
    source TEXT NOT NULL DEFAULT 'WEB' CHECK (source IN ('MOBILE', 'WEB', 'EXCEL_IMPORT', 'API')),
    currency CHAR(3) NOT NULL DEFAULT 'INR',
    total_net_amount NUMERIC(19,4) NOT NULL DEFAULT 0,
    total_tax_amount NUMERIC(19,4) NOT NULL DEFAULT 0,
    total_gross_amount NUMERIC(19,4) NOT NULL DEFAULT 0,
    status TEXT NOT NULL DEFAULT 'DRAFT' CHECK (status IN ('DRAFT', 'SUBMITTED', 'IN_REVIEW', 'APPROVED', 'POSTED', 'DISPUTED', 'REJECTED', 'VOID')),
    submitted_at TIMESTAMPTZ,
    approved_at TIMESTAMPTZ,
    cancel_reason TEXT,
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (created_at, org_id, expense_id),
    CONSTRAINT ck_expense_gross CHECK (total_gross_amount = total_net_amount + total_tax_amount),
    CONSTRAINT ck_expense_cancellation CHECK (status <> 'VOID' OR cancel_reason IS NOT NULL)
) PARTITION BY RANGE (created_at);

-- Monthly Partitions for Expenses
CREATE TABLE IF NOT EXISTS expense.expense_y2026m11 PARTITION OF expense.expense
    FOR VALUES FROM ('2026-11-01 00:00:00+00') TO ('2026-12-01 00:00:00+00');
CREATE TABLE IF NOT EXISTS expense.expense_y2026m12 PARTITION OF expense.expense
    FOR VALUES FROM ('2026-12-01 00:00:00+00') TO ('2027-01-01 00:00:00+00');
CREATE TABLE IF NOT EXISTS expense.expense_y2027m01 PARTITION OF expense.expense
    FOR VALUES FROM ('2027-01-01 00:00:00+00') TO ('2027-02-01 00:00:00+00');
CREATE TABLE IF NOT EXISTS expense.expense_y2027m02 PARTITION OF expense.expense
    FOR VALUES FROM ('2027-02-01 00:00:00+00') TO ('2027-03-01 00:00:00+00');

CREATE TABLE IF NOT EXISTS expense.expense_line (
    org_id UUID NOT NULL,
    expense_line_id UUID NOT NULL DEFAULT gen_random_uuid(),
    expense_id UUID NOT NULL,
    show_id UUID NOT NULL,
    line_no SMALLINT NOT NULL CHECK (line_no >= 1),
    category_id UUID NOT NULL,
    department_id UUID NOT NULL,
    cost_type TEXT NOT NULL CHECK (cost_type IN ('ABOVE_THE_LINE', 'BELOW_THE_LINE', 'POST_PRODUCTION', 'OTHER')),
    episode_id UUID,
    description TEXT,
    quantity NUMERIC(19,4) NOT NULL DEFAULT 1 CHECK (quantity > 0),
    unit_price NUMERIC(19,4) NOT NULL,
    tax_rate NUMERIC(5,2) NOT NULL DEFAULT 0 CHECK (tax_rate BETWEEN 0 AND 100),
    net_amount NUMERIC(19,4) NOT NULL,
    tax_amount NUMERIC(19,4) NOT NULL,
    gross_amount NUMERIC(19,4) NOT NULL,
    cost_basis_amount NUMERIC(19,4) NOT NULL,
    commitment_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    PRIMARY KEY (org_id, expense_line_id),
    CONSTRAINT uq_expense_line_no UNIQUE (org_id, expense_id, line_no),
    CONSTRAINT ck_expense_line_gross CHECK (gross_amount = net_amount + tax_amount)
);

CREATE TABLE IF NOT EXISTS expense.expense_split (
    org_id UUID NOT NULL,
    expense_line_id UUID NOT NULL,
    split_seq SMALLINT NOT NULL CHECK (split_seq >= 1),
    department_id UUID NOT NULL,
    category_id UUID NOT NULL,
    percentage NUMERIC(5,2) NOT NULL CHECK (percentage > 0 AND percentage <= 100),
    amount NUMERIC(19,4) NOT NULL CHECK (amount >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    PRIMARY KEY (org_id, expense_line_id, split_seq),
    CONSTRAINT fk_split_line FOREIGN KEY (org_id, expense_line_id) REFERENCES expense.expense_line(org_id, expense_line_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS expense.cost_posting (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE RESTRICT,
    cost_posting_id UUID NOT NULL DEFAULT gen_random_uuid(),
    posting_type TEXT NOT NULL CHECK (posting_type IN ('APPROVAL', 'REVERSAL')),
    expense_id UUID NOT NULL,
    root_expense_id UUID NOT NULL,
    expense_line_id UUID NOT NULL,
    split_seq SMALLINT NOT NULL DEFAULT 0,
    posting_date DATE NOT NULL,
    show_id UUID NOT NULL,
    season_id UUID NOT NULL,
    department_id UUID NOT NULL,
    category_id UUID NOT NULL,
    amount NUMERIC(19,4) NOT NULL,
    currency CHAR(3) NOT NULL DEFAULT 'INR',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    PRIMARY KEY (org_id, cost_posting_id)
);

-- ----------------------------------------------------------------------------
-- 3. ROW-LEVEL SECURITY ENFORCEMENT & POLICIES (ADR-002, ADR-035)
-- ----------------------------------------------------------------------------

ALTER TABLE vendor.vendor ENABLE ROW LEVEL SECURITY;
ALTER TABLE vendor.vendor FORCE ROW LEVEL SECURITY;
CREATE POLICY p_vendor_vendor_tenant_isolation ON vendor.vendor
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE vendor.vendor_tax_registration ENABLE ROW LEVEL SECURITY;
ALTER TABLE vendor.vendor_tax_registration FORCE ROW LEVEL SECURITY;
CREATE POLICY p_vendor_vendor_tax_registration_tenant_isolation ON vendor.vendor_tax_registration
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE vendor.manual_commitment ENABLE ROW LEVEL SECURITY;
ALTER TABLE vendor.manual_commitment FORCE ROW LEVEL SECURITY;
CREATE POLICY p_vendor_manual_commitment_tenant_isolation ON vendor.manual_commitment
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE expense.expense_key ENABLE ROW LEVEL SECURITY;
ALTER TABLE expense.expense_key FORCE ROW LEVEL SECURITY;
CREATE POLICY p_expense_expense_key_tenant_isolation ON expense.expense_key
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE expense.expense ENABLE ROW LEVEL SECURITY;
ALTER TABLE expense.expense FORCE ROW LEVEL SECURITY;
CREATE POLICY p_expense_expense_tenant_isolation ON expense.expense
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE expense.expense_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE expense.expense_line FORCE ROW LEVEL SECURITY;
CREATE POLICY p_expense_expense_line_tenant_isolation ON expense.expense_line
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE expense.expense_split ENABLE ROW LEVEL SECURITY;
ALTER TABLE expense.expense_split FORCE ROW LEVEL SECURITY;
CREATE POLICY p_expense_expense_split_tenant_isolation ON expense.expense_split
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE expense.cost_posting ENABLE ROW LEVEL SECURITY;
ALTER TABLE expense.cost_posting FORCE ROW LEVEL SECURITY;
CREATE POLICY p_expense_cost_posting_tenant_isolation ON expense.cost_posting
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

-- Grant table privileges to application and migrator roles
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA vendor TO slateiq_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA expense TO slateiq_app;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA vendor TO slateiq_migrator;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA expense TO slateiq_migrator;
