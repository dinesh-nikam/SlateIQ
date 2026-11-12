-- ============================================================================
-- Migration: 004_production_and_budgeting.sql
-- Classification: expand
-- Description: Core Phase 1 tables for Production (MOD-03) and Budgeting (MOD-04)
-- Governing Documents: SLQ-ARCH-002, SLQ-ARCH-004, SLQ-ARCH-008 (§14.2, §14.3)
-- State Machines: SM-02 (Show), SM-03 (Budget Container), SM-04 (Budget Version)
-- Invariants: INV-002 (Scale-4 Money sum conservation), RLS forced isolation
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. PRODUCTION SCHEMA (MOD-03)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS prod.show (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    show_id UUID NOT NULL DEFAULT gen_random_uuid(),
    code TEXT NOT NULL,
    title TEXT NOT NULL,
    show_type TEXT NOT NULL CHECK (show_type IN ('SERIES', 'FILM', 'REALITY', 'LIVE_EVENT', 'DOCUMENTARY', 'OTHER')),
    format TEXT,
    genre TEXT,
    language TEXT NOT NULL DEFAULT 'English',
    planned_start_date DATE,
    planned_end_date DATE,
    planned_shoot_days INT CHECK (planned_shoot_days IS NULL OR planned_shoot_days >= 0),
    hosting_pattern TEXT NOT NULL DEFAULT 'H1' CHECK (hosting_pattern IN ('H1', 'H2')),
    status TEXT NOT NULL DEFAULT 'PLANNING' CHECK (status IN ('PLANNING', 'ACTIVE', 'COMPLETED', 'CLOSING', 'CLOSED', 'ARCHIVED', 'CANCELLED')),
    closed_at TIMESTAMPTZ,
    archived_at TIMESTAMPTZ,
    cancelled_at TIMESTAMPTZ,
    cancel_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, show_id),
    CONSTRAINT uq_show_code UNIQUE (org_id, code),
    CONSTRAINT ck_show_dates CHECK (planned_end_date IS NULL OR planned_start_date IS NULL OR planned_end_date >= planned_start_date),
    CONSTRAINT ck_show_cancellation CHECK (status <> 'CANCELLED' OR cancel_reason IS NOT NULL)
);

CREATE TABLE IF NOT EXISTS prod.season (
    org_id UUID NOT NULL,
    season_id UUID NOT NULL DEFAULT gen_random_uuid(),
    show_id UUID NOT NULL,
    season_number INT NOT NULL CHECK (season_number >= 1),
    title TEXT,
    planned_episodes INT CHECK (planned_episodes IS NULL OR planned_episodes >= 0),
    planned_start_date DATE,
    planned_end_date DATE,
    status TEXT NOT NULL DEFAULT 'PLANNED' CHECK (status IN ('PLANNED', 'ACTIVE', 'COMPLETED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, season_id),
    CONSTRAINT fk_season_show FOREIGN KEY (org_id, show_id) REFERENCES prod.show(org_id, show_id) ON DELETE CASCADE,
    CONSTRAINT uq_season_number UNIQUE (org_id, show_id, season_number),
    CONSTRAINT uq_season_denorm UNIQUE (org_id, show_id, season_id)
);

CREATE TABLE IF NOT EXISTS prod.episode (
    org_id UUID NOT NULL,
    episode_id UUID NOT NULL DEFAULT gen_random_uuid(),
    show_id UUID NOT NULL,
    season_id UUID NOT NULL,
    episode_number INT NOT NULL CHECK (episode_number >= 1),
    title TEXT,
    shoot_start_date DATE,
    shoot_end_date DATE,
    status TEXT NOT NULL DEFAULT 'PLANNED' CHECK (status IN ('PLANNED', 'IN_PRODUCTION', 'SHOT', 'DELIVERED', 'DROPPED')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, episode_id),
    CONSTRAINT fk_episode_season FOREIGN KEY (org_id, show_id, season_id) REFERENCES prod.season(org_id, show_id, season_id) ON DELETE CASCADE,
    CONSTRAINT uq_episode_number UNIQUE (org_id, season_id, episode_number)
);

CREATE TABLE IF NOT EXISTS prod.department (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    department_id UUID NOT NULL DEFAULT gen_random_uuid(),
    code TEXT NOT NULL,
    name TEXT NOT NULL,
    parent_department_id UUID,
    show_id UUID,
    depth SMALLINT NOT NULL DEFAULT 1 CHECK (depth BETWEEN 1 AND 3),
    is_active BOOLEAN NOT NULL DEFAULT true,
    deactivated_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, department_id),
    CONSTRAINT uq_department_code UNIQUE NULLS NOT DISTINCT (org_id, show_id, code),
    CONSTRAINT ck_department_no_self_parent CHECK (parent_department_id IS NULL OR parent_department_id <> department_id)
);

CREATE TABLE IF NOT EXISTS prod.category (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    category_id UUID NOT NULL DEFAULT gen_random_uuid(),
    code TEXT NOT NULL,
    name TEXT NOT NULL,
    parent_category_id UUID,
    cost_type TEXT NOT NULL CHECK (cost_type IN ('ABOVE_THE_LINE', 'BELOW_THE_LINE', 'POST_PRODUCTION', 'OTHER')),
    is_leaf BOOLEAN NOT NULL DEFAULT true,
    input_tax_recoverable_default BOOLEAN NOT NULL DEFAULT false,
    evidence_policy TEXT NOT NULL DEFAULT 'ALWAYS' CHECK (evidence_policy IN ('ALWAYS', 'ABOVE_THRESHOLD', 'POLICY_DEFAULT')),
    evidence_threshold_amount NUMERIC(19,4),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, category_id),
    CONSTRAINT uq_category_code UNIQUE (org_id, code),
    CONSTRAINT ck_category_threshold CHECK (evidence_policy <> 'ABOVE_THRESHOLD' OR (evidence_threshold_amount IS NOT NULL AND evidence_threshold_amount >= 0))
);

CREATE TABLE IF NOT EXISTS prod.category_department (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    category_id UUID NOT NULL,
    department_id UUID NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    PRIMARY KEY (org_id, category_id, department_id),
    CONSTRAINT fk_catdept_category FOREIGN KEY (org_id, category_id) REFERENCES prod.category(org_id, category_id) ON DELETE CASCADE,
    CONSTRAINT fk_catdept_department FOREIGN KEY (org_id, department_id) REFERENCES prod.department(org_id, department_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS prod.show_party (
    org_id UUID NOT NULL,
    show_id UUID NOT NULL,
    party_name TEXT NOT NULL,
    party_role TEXT NOT NULL CHECK (party_role IN ('COMMISSIONER', 'PRODUCTION_HOUSE', 'CO_PRODUCER', 'APPOINTING_PARTY')),
    is_primary BOOLEAN NOT NULL DEFAULT false,
    effective_from DATE NOT NULL DEFAULT CURRENT_DATE,
    effective_to DATE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    PRIMARY KEY (org_id, show_id, party_name, party_role, effective_from),
    CONSTRAINT fk_show_party_show FOREIGN KEY (org_id, show_id) REFERENCES prod.show(org_id, show_id) ON DELETE CASCADE,
    CONSTRAINT ck_show_party_dates CHECK (effective_to IS NULL OR effective_to >= effective_from)
);

-- ----------------------------------------------------------------------------
-- 2. BUDGET SCHEMA (MOD-04)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS budget.budget (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    budget_id UUID NOT NULL DEFAULT gen_random_uuid(),
    show_id UUID NOT NULL,
    season_id UUID NOT NULL,
    status TEXT NOT NULL DEFAULT 'NOT_BASELINED' CHECK (status IN ('NOT_BASELINED', 'BASELINED', 'REVISION_IN_PROGRESS', 'FROZEN')),
    current_version_id UUID,
    base_currency CHAR(3) NOT NULL DEFAULT 'INR',
    frozen_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, budget_id),
    CONSTRAINT uq_budget_season UNIQUE (org_id, season_id),
    CONSTRAINT uq_budget_denorm UNIQUE (org_id, show_id, budget_id),
    CONSTRAINT ck_budget_baselined_version CHECK ((status = 'NOT_BASELINED') = (current_version_id IS NULL)),
    CONSTRAINT ck_budget_frozen CHECK (status <> 'FROZEN' OR frozen_at IS NOT NULL)
);

CREATE TABLE IF NOT EXISTS budget.budget_version (
    org_id UUID NOT NULL,
    budget_version_id UUID NOT NULL DEFAULT gen_random_uuid(),
    budget_id UUID NOT NULL,
    show_id UUID NOT NULL,
    version_no INT NOT NULL CHECK (version_no >= 1),
    version_type TEXT NOT NULL CHECK (version_type IN ('ORIGINAL', 'REVISION')),
    reason TEXT,
    effective_date DATE,
    total_amount NUMERIC(19,4) NOT NULL CHECK (total_amount >= 0),
    contingency_amount NUMERIC(19,4) NOT NULL DEFAULT 0 CHECK (contingency_amount >= 0),
    status TEXT NOT NULL DEFAULT 'DRAFT' CHECK (status IN ('DRAFT', 'SUBMITTED', 'CHANGES_REQUESTED', 'ACTIVE', 'SUPERSEDED', 'REJECTED', 'WITHDRAWN')),
    supersedes_version_id UUID,
    submitted_by UUID,
    submitted_at TIMESTAMPTZ,
    approved_by UUID,
    approved_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, budget_version_id),
    CONSTRAINT fk_version_budget FOREIGN KEY (org_id, show_id, budget_id) REFERENCES budget.budget(org_id, show_id, budget_id) ON DELETE CASCADE,
    CONSTRAINT uq_budget_version_no UNIQUE (org_id, budget_id, version_no),
    CONSTRAINT ck_version_type_no CHECK ((version_type = 'ORIGINAL' AND version_no = 1) OR (version_type = 'REVISION' AND version_no > 1))
);

-- Partial Unique Index: Only one ACTIVE version per budget (BR-BUD-001)
CREATE UNIQUE INDEX IF NOT EXISTS uq_budget_version_one_active 
    ON budget.budget_version (org_id, budget_id) 
    WHERE status = 'ACTIVE';

CREATE TABLE IF NOT EXISTS budget.budget_line (
    org_id UUID NOT NULL,
    budget_line_id UUID NOT NULL DEFAULT gen_random_uuid(),
    budget_version_id UUID NOT NULL,
    show_id UUID NOT NULL,
    department_id UUID,
    category_id UUID,
    cost_type TEXT NOT NULL CHECK (cost_type IN ('ABOVE_THE_LINE', 'BELOW_THE_LINE', 'POST_PRODUCTION', 'OTHER')),
    line_type TEXT NOT NULL DEFAULT 'OPERATIONAL' CHECK (line_type IN ('OPERATIONAL', 'CONTINGENCY')),
    amount NUMERIC(19,4) NOT NULL CHECK (amount >= 0),
    quantity NUMERIC(19,4),
    unit TEXT,
    rate NUMERIC(19,4),
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    PRIMARY KEY (org_id, budget_line_id),
    CONSTRAINT fk_line_version FOREIGN KEY (org_id, budget_version_id) REFERENCES budget.budget_version(org_id, budget_version_id) ON DELETE CASCADE,
    CONSTRAINT uq_budget_line_key UNIQUE NULLS NOT DISTINCT (org_id, budget_version_id, department_id, category_id, cost_type),
    CONSTRAINT ck_budget_line_refs CHECK (line_type = 'CONTINGENCY' OR (department_id IS NOT NULL AND category_id IS NOT NULL))
);

CREATE TABLE IF NOT EXISTS budget.contingency_bucket (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    contingency_id UUID NOT NULL DEFAULT gen_random_uuid(),
    budget_id UUID NOT NULL,
    initial_amount NUMERIC(19,4) NOT NULL CHECK (initial_amount >= 0),
    allocated_amount NUMERIC(19,4) NOT NULL DEFAULT 0 CHECK (allocated_amount >= 0),
    remaining_amount NUMERIC(19,4) NOT NULL CHECK (remaining_amount >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by UUID NOT NULL,
    row_version BIGINT NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    PRIMARY KEY (org_id, contingency_id),
    CONSTRAINT uq_contingency_budget UNIQUE (org_id, budget_id),
    CONSTRAINT ck_contingency_conservation CHECK (initial_amount = allocated_amount + remaining_amount)
);

CREATE TABLE IF NOT EXISTS budget.fx_rate (
    org_id UUID NOT NULL REFERENCES tenancy.organization(id) ON DELETE CASCADE,
    fx_rate_id UUID NOT NULL DEFAULT gen_random_uuid(),
    from_currency CHAR(3) NOT NULL,
    to_currency CHAR(3) NOT NULL,
    rate_date DATE NOT NULL,
    rate NUMERIC(19,6) NOT NULL CHECK (rate > 0),
    source TEXT NOT NULL CHECK (source IN ('ECB', 'OER', 'MANUAL')),
    revision_no INT NOT NULL DEFAULT 1 CHECK (revision_no >= 1),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by UUID NOT NULL,
    PRIMARY KEY (org_id, fx_rate_id),
    CONSTRAINT ck_fx_distinct_currencies CHECK (from_currency <> to_currency)
);

-- ----------------------------------------------------------------------------
-- 3. ROW-LEVEL SECURITY ENFORCEMENT & POLICIES (ADR-002, ADR-035)
-- ----------------------------------------------------------------------------

-- Enable and FORCE RLS on all 11 production and budgeting tables
ALTER TABLE prod.show ENABLE ROW LEVEL SECURITY;
ALTER TABLE prod.show FORCE ROW LEVEL SECURITY;
CREATE POLICY p_prod_show_tenant_isolation ON prod.show
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE prod.season ENABLE ROW LEVEL SECURITY;
ALTER TABLE prod.season FORCE ROW LEVEL SECURITY;
CREATE POLICY p_prod_season_tenant_isolation ON prod.season
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE prod.episode ENABLE ROW LEVEL SECURITY;
ALTER TABLE prod.episode FORCE ROW LEVEL SECURITY;
CREATE POLICY p_prod_episode_tenant_isolation ON prod.episode
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE prod.department ENABLE ROW LEVEL SECURITY;
ALTER TABLE prod.department FORCE ROW LEVEL SECURITY;
CREATE POLICY p_prod_department_tenant_isolation ON prod.department
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE prod.category ENABLE ROW LEVEL SECURITY;
ALTER TABLE prod.category FORCE ROW LEVEL SECURITY;
CREATE POLICY p_prod_category_tenant_isolation ON prod.category
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE prod.category_department ENABLE ROW LEVEL SECURITY;
ALTER TABLE prod.category_department FORCE ROW LEVEL SECURITY;
CREATE POLICY p_prod_category_department_tenant_isolation ON prod.category_department
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE prod.show_party ENABLE ROW LEVEL SECURITY;
ALTER TABLE prod.show_party FORCE ROW LEVEL SECURITY;
CREATE POLICY p_prod_show_party_tenant_isolation ON prod.show_party
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE budget.budget ENABLE ROW LEVEL SECURITY;
ALTER TABLE budget.budget FORCE ROW LEVEL SECURITY;
CREATE POLICY p_budget_budget_tenant_isolation ON budget.budget
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE budget.budget_version ENABLE ROW LEVEL SECURITY;
ALTER TABLE budget.budget_version FORCE ROW LEVEL SECURITY;
CREATE POLICY p_budget_budget_version_tenant_isolation ON budget.budget_version
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE budget.budget_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE budget.budget_line FORCE ROW LEVEL SECURITY;
CREATE POLICY p_budget_budget_line_tenant_isolation ON budget.budget_line
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE budget.contingency_bucket ENABLE ROW LEVEL SECURITY;
ALTER TABLE budget.contingency_bucket FORCE ROW LEVEL SECURITY;
CREATE POLICY p_budget_contingency_bucket_tenant_isolation ON budget.contingency_bucket
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

ALTER TABLE budget.fx_rate ENABLE ROW LEVEL SECURITY;
ALTER TABLE budget.fx_rate FORCE ROW LEVEL SECURITY;
CREATE POLICY p_budget_fx_rate_tenant_isolation ON budget.fx_rate
    FOR ALL TO slateiq_app
    USING (org_id = tenancy.current_org_id())
    WITH CHECK (org_id = tenancy.current_org_id());

-- Grant table privileges to application and migrator roles
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA prod TO slateiq_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA budget TO slateiq_app;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA prod TO slateiq_migrator;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA budget TO slateiq_migrator;
