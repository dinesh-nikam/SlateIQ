-- ============================================================================
-- Script: 03-seed-reference-data.sql
-- Description: Seeds canonical roles, permissions, and development seed tenant
-- Governing Documents: SLQ-ARCH-003 §5, SLQ-ARCH-008 §4
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. Reference Roles Table (Global System Catalog)
-- ----------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS ref.system_role (
    code TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    category TEXT NOT NULL,
    mfa_required BOOLEAN NOT NULL DEFAULT false,
    description TEXT NOT NULL
);

INSERT INTO ref.system_role (code, name, category, mfa_required, description)
VALUES
    ('ORG_ADMIN', 'Organization Administrator', 'TENANT_ADMIN', true, 'Full administrative control of the tenant organization, members, and settings.'),
    ('COMMISSIONING_ADMIN', 'Commissioning Administrator', 'PLATFORM_ADMIN', true, 'Broadcaster/Platform commissioning lead overseeing greenlit shows and approvals.'),
    ('PRODUCTION_ADMIN', 'Production Administrator', 'PRODUCTION', true, 'Production house executive managing shows, budgets, and production teams.'),
    ('FINANCE_HEAD', 'Finance Head', 'FINANCE', true, 'Top-tier financial officer managing budget baselines, period close, and major approvals.'),
    ('COST_CONTROLLER', 'Cost Controller', 'FINANCE', false, 'Day-to-day production accountant tracking budgets, commitments, and variances.'),
    ('DEPARTMENT_HEAD', 'Department Head', 'CREW', false, 'Crew lead (Art, Camera, Direction) responsible for departmental expenses and approvals.'),
    ('ACCOUNTS_EXECUTIVE', 'Accounts Executive', 'FINANCE', false, 'Finance staff verifying vendor invoices, processing expenses, and tax reconciliation.'),
    ('AUDIT_MANAGER', 'Audit Manager', 'AUDIT', true, 'Audit lead managing assignments, reviewing findings, and accepting finalized audit reports.'),
    ('INTERNAL_AUDITOR', 'Internal Auditor', 'AUDIT', false, 'On-ground auditor capturing daily costs, performing receipt verification and field checks.'),
    ('MANAGEMENT_VIEWER', 'Management Viewer', 'EXECUTIVE', false, 'Executive read-only access to cross-show portfolio dashboards and variance trends.'),
    ('PRODUCTION_COORDINATOR', 'Production Coordinator', 'PRODUCTION', false, 'On-ground coordinator logging logistics expenses and coordinating PO commitments.'),
    ('VENDOR_COORDINATOR', 'Vendor Coordinator', 'PRODUCTION', false, 'Staff managing vendor onboarding, compliance documents, and contract archives.'),
    ('COMPLIANCE_OFFICER', 'Compliance Officer', 'COMPLIANCE', true, 'Compliance officer managing SoD rules, audit log verification, and legal holds.'),
    ('ACCESS_REVIEWER', 'Access Reviewer', 'IDENTITY', true, 'Identity administrator executing quarterly access reviews and membership audits.'),
    ('AUDIT_FIRM_ADMIN', 'Audit Firm Administrator', 'EXTERNAL', true, 'External partner admin managing external auditor firm engagement assignments.'),
    ('SUPPORT_ENGINEER', 'Support Engineer', 'PLATFORM_OPS', true, 'Platform support staff with operational diagnostic rights; zero access to financial data.'),
    ('PLATFORM_OPERATIONS', 'Platform Operations', 'PLATFORM_OPS', true, 'SRE and infrastructure engineering role managing system health, jobs, and migrations.'),
    ('PLATFORM_SECURITY', 'Platform Security', 'PLATFORM_OPS', true, 'Security operations role managing KMS keys, token revocations, and threat response.')
ON CONFLICT (code) DO UPDATE SET
    name = EXCLUDED.name,
    category = EXCLUDED.category,
    mfa_required = EXCLUDED.mfa_required,
    description = EXCLUDED.description;

-- Grant SELECT on ref to app
GRANT SELECT ON ALL TABLES IN SCHEMA ref TO slateiq_app;

-- ----------------------------------------------------------------------------
-- 2. Seed Baseline Development Organization & Administrator User
-- ----------------------------------------------------------------------------

DO $$
DECLARE
    dev_org_id UUID := '0193b2d4-0000-7000-8000-000000000001'::uuid;
    dev_user_id UUID := '0193b2d4-0000-7000-8000-000000000002'::uuid;
BEGIN
    -- 1. Create Dev Tenant ("Monsoon Media Works")
    INSERT INTO tenancy.organization (id, code, legal_name, organization_type, status, base_currency, tier, home_region)
    VALUES (
        dev_org_id,
        'MMW',
        'Monsoon Media Works Pvt Ltd',
        'PRODUCTION_HOUSE',
        'ACTIVE',
        'INR',
        'STANDARD',
        'IN'
    )
    ON CONFLICT (id) DO NOTHING;

    -- Map Tenant to Region
    INSERT INTO ops.tenant_region (org_id, region_code, cell_id)
    VALUES (dev_org_id, 'IN', 'shared-cell-01')
    ON CONFLICT (org_id) DO NOTHING;

    -- 2. Create Dev User ("Dev Administrator")
    INSERT INTO identity.user (id, home_org_id, email, full_name, status, mfa_enforced)
    VALUES (
        dev_user_id,
        dev_org_id,
        'admin@monsoonmedia.local',
        'Monsoon Dev Administrator',
        'ACTIVE',
        false
    )
    ON CONFLICT (id) DO NOTHING;

    -- 3. Set Membership as ORG_ADMIN
    INSERT INTO tenancy.membership (org_id, user_id, primary_role_code, status)
    VALUES (dev_org_id, dev_user_id, 'ORG_ADMIN', 'ACTIVE')
    ON CONFLICT (org_id, user_id) DO NOTHING;

    -- 4. Set Initial Password Hash (Password: "SlateIQ@2026!")
    -- Pre-calculated Argon2id hash for development seed
    INSERT INTO identity.credential (user_id, password_hash, algorithm, salt)
    VALUES (
        dev_user_id,
        '$argon2id$v=19$m=65536,t=3,p=4$c29tZXNhbHQxMjM0NTY3OA$B9q1vO2+zBvJcT8gL7qW1Y9jK5rT3uP8xV4mZ2nQ6aI',
        'argon2id',
        'somesalt12345678'
    )
    ON CONFLICT (user_id) DO NOTHING;

    -- 5. Seed Initial Genesis Audit Event
    INSERT INTO compliance.audit_event (
        org_id, actor_user_id, action, entity_type, entity_id,
        previous_state, new_state, metadata, correlation_id,
        previous_hash, current_hash
    )
    VALUES (
        dev_org_id,
        dev_user_id,
        'organization.provisioned',
        'Organization',
        dev_org_id,
        null,
        '{"code": "MMW", "legal_name": "Monsoon Media Works Pvt Ltd"}'::jsonb,
        '{"seed": true, "environment": "development"}'::jsonb,
        'genesis-correlation-00000001',
        '0000000000000000000000000000000000000000000000000000000000000000',
        encode(sha256('genesis-audit-block-monsoon-media-works-2026-11-09'::bytea), 'hex')
    );
END $$;
