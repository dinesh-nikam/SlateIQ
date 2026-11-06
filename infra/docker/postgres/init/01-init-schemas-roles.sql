-- ============================================================================
-- SlateIQ Local Development - Database Initialization Script
-- Sourced from: SLQ-ARCH-003 §7, SLQ-ARCH-008 §3, SLQ-ARCH-015 §6
-- ============================================================================

-- 1. Create Core Extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "citext";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

-- 2. Create All 15 Domain Schemas + Ref
CREATE SCHEMA IF NOT EXISTS tenancy;
CREATE SCHEMA IF NOT EXISTS identity;
CREATE SCHEMA IF NOT EXISTS prod;
CREATE SCHEMA IF NOT EXISTS budget;
CREATE SCHEMA IF NOT EXISTS vendor;
CREATE SCHEMA IF NOT EXISTS auditops;
CREATE SCHEMA IF NOT EXISTS expense;
CREATE SCHEMA IF NOT EXISTS intel;
CREATE SCHEMA IF NOT EXISTS docs;
CREATE SCHEMA IF NOT EXISTS xls;
CREATE SCHEMA IF NOT EXISTS notify;
CREATE SCHEMA IF NOT EXISTS compliance;
CREATE SCHEMA IF NOT EXISTS report;
CREATE SCHEMA IF NOT EXISTS sync;
CREATE SCHEMA IF NOT EXISTS ops;
CREATE SCHEMA IF NOT EXISTS ref;

-- 3. Create Application Database Roles (Document 3 §7.3, §7.7)
DO $$
BEGIN
    -- Application Runtime Role (Connects through PgBouncer, enforces RLS, no DDL)
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'slateiq_app') THEN
        CREATE ROLE slateiq_app WITH LOGIN PASSWORD 'slateiq_app_dev_pw' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
    END IF;

    -- Migration Role (Runs EF Core / DDL migrations, has DDL rights)
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'slateiq_migrator') THEN
        CREATE ROLE slateiq_migrator WITH LOGIN PASSWORD 'slateiq_migrator_dev_pw' NOSUPERUSER CREATEDB NOCREATEROLE;
    END IF;

    -- Platform Operations Role (Internal Ops Host, accesses ops schema)
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'slateiq_ops') THEN
        CREATE ROLE slateiq_ops WITH LOGIN PASSWORD 'slateiq_ops_dev_pw' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
    END IF;

    -- Read-Only Analytical Role
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'slateiq_ro') THEN
        CREATE ROLE slateiq_ro WITH LOGIN PASSWORD 'slateiq_ro_dev_pw' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
    END IF;
END $$;

-- 4. Schema Permissions & Grants
-- Migrator owns objects in schemas
GRANT ALL ON SCHEMA tenancy, identity, prod, budget, vendor, auditops, expense, intel, docs, xls, notify, compliance, report, sync, ops, ref TO slateiq_migrator;

-- App role gets usage and standard DML (SELECT, INSERT, UPDATE, DELETE)
GRANT USAGE ON SCHEMA tenancy, identity, prod, budget, vendor, auditops, expense, intel, docs, xls, notify, compliance, report, sync, ref TO slateiq_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA tenancy, identity, prod, budget, vendor, auditops, expense, intel, docs, xls, notify, compliance, report, sync, ref TO slateiq_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA tenancy, identity, prod, budget, vendor, auditops, expense, intel, docs, xls, notify, compliance, report, sync, ref
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO slateiq_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA tenancy, identity, prod, budget, vendor, auditops, expense, intel, docs, xls, notify, compliance, report, sync, ref
    GRANT USAGE, SELECT ON SEQUENCES TO slateiq_app;

-- Ops role gets access to ops schema
GRANT USAGE ON SCHEMA ops, tenancy, compliance TO slateiq_ops;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA ops TO slateiq_ops;
ALTER DEFAULT PRIVILEGES IN SCHEMA ops
    GRANT SELECT, INSERT, UPDATE ON TABLES TO slateiq_ops;

-- Read-only role gets SELECT
GRANT USAGE ON SCHEMA tenancy, identity, prod, budget, vendor, auditops, expense, intel, docs, xls, notify, compliance, report, sync, ref TO slateiq_ro;
GRANT SELECT ON ALL TABLES IN SCHEMA tenancy, identity, prod, budget, vendor, auditops, expense, intel, docs, xls, notify, compliance, report, sync, ref TO slateiq_ro;
ALTER DEFAULT PRIVILEGES IN SCHEMA tenancy, identity, prod, budget, vendor, auditops, expense, intel, docs, xls, notify, compliance, report, sync, ref
    GRANT SELECT ON TABLES TO slateiq_ro;

-- 5. Tenant Context Session Helper (Document 3 §7.2, Document 8 §3)
CREATE OR REPLACE FUNCTION tenancy.current_org_id()
RETURNS uuid
LANGUAGE plpgsql
STABLE
AS $$
DECLARE
    org_setting text;
BEGIN
    org_setting := current_setting('app.current_org_id', true);
    IF org_setting IS NULL OR org_setting = '' THEN
        RAISE EXCEPTION 'tenant_context_not_set: app.current_org_id is not set in session'
            USING ERRCODE = '42501';
    END IF;
    RETURN org_setting::uuid;
END;
$$;
