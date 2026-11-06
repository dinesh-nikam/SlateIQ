---

## 7. Isolation Architecture

### 7.1 Defence Layers

Tenant isolation is not a policy; it is a set of independent barriers, each of which must fail before data crosses a boundary. The layers are ordered from the outermost to the innermost, and each has its own test.

| Layer | Control | What it stops | Failure mode | Test |
|-------|---------|---------------|--------------|------|
| **L1 Edge** | Host and route resolution; no tenant identifier in the host name at MVP | Accidental cross-tenant routing | Single shared host, so no route confusion | Route inventory |
| **L2 Authentication** | Session bound to one acting `org`; token `org` claim signed | Token replay across tenants | Forged token fails signature | Token matrix |
| **L3 Policy decision** | PDP resolves authority from server-side records | Client-supplied role, scope or tenant | Deny and P1 alert | Endpoint matrix |
| **L4 Query layer** | Every query carries an explicit `org_id` predicate derived from context | A missing predicate in a new query | Query returns nothing or is rejected by RLS | Static analysis plus RLS |
| **L5 Row-level security** | `ENABLE` and `FORCE` RLS on every tenant table | Any query that reaches PostgreSQL without a tenant predicate | Zero rows, or an error if context is unset | Per-table negative test |
| **L6 Object storage** | Prefix per tenant, bucket policy condition, signed URL bound to tenant | Guessable object key | Access denied at the storage layer | Bucket policy test |
| **L7 Cache** | Keys partitioned by `org_id`; no cross-tenant shared entries | Key collision or missing prefix | Recomputation, not leakage | Cache key audit |
| **L8 Search** | RLS applies to the search read model at MVP | Global index leaking rows | Zero rows | Search negative test |
| **L9 Jobs** | Worker sets tenant context per message and asserts it | A batch job that processes all tenants in one context | Job fails loudly | Job harness test |
| **L10 Exports** | Export generated under tenant context, manifest names the `org_id` | Mixed-tenant export | Export job aborts | Export fixture test |
| **L11 Telemetry** | Tenant identifier allowed; financial and personal data redacted | Sensitive data in logs | Redaction test | Log sample audit |
| **L12 Backups** | Regional encryption; restore requires the regional key | Cross-region restore | Decryption fails | Restore drill |

**The layers are not substitutes.** Application filtering without RLS fails on the first forgotten predicate. RLS without application scope checks fails on the first over-broad grant. RLS without object-storage prefixing fails on the first presigned URL. Each layer is tested separately because a test that exercises all layers at once cannot tell you which one is holding.

### 7.2 Tenant Context Propagation

| Stage | Action | Rule |
|-------|--------|------|
| Request entry | Authenticate the token; extract `org`, `sub`, `sid` | The `org` claim is the only accepted tenant source |
| Session validation | Load the session; confirm `session.org_id = token.org` | A mismatch is a forged or stale token |
| Tenant validation | Load the organization; require `status = ACTIVE` | Cached for 30 seconds |
| Authority resolution | Load membership or grant; confirm it names the acting `org` | No authority means no context |
| Transaction begin | `BEGIN` | One transaction per request or per message |
| Context set | `SET LOCAL app.org_id = ...`, `app.user_id`, `app.grant_ids`, `app.scope` | `SET LOCAL` only; never session-level `SET` |
| Work | Execute queries through the module repository | Every query runs as `slateiq_app` |
| Commit or rollback | `COMMIT` / `ROLLBACK` | Context dies with the transaction |
| Connection return | PgBouncer returns the connection to the pool | No session state survives |

**`SET LOCAL` is mandatory.** A session-level `SET` would leak the last tenant's context into the next request that borrows the pooled connection. PgBouncer runs in transaction mode, and the application never issues a session-level `SET`. A code review rule and a static analysis check reject `SET` without `LOCAL` in repository code.

**Unset context is an error, not a null.** Policies call `current_setting('app.org_id')` without `missing_ok`, so a query that reaches PostgreSQL without context raises `unrecognized configuration parameter` rather than comparing against `NULL`. A `NULL` comparison would silently return zero rows, which looks like an empty result and hides the bug. An error is louder and is caught by the first test.

**Workers.** A worker consumes a message that names `org_id`. It opens a transaction, sets the same context, asserts that `app.org_id` equals the message's `org_id`, processes, and commits. A worker that processes a batch across tenants must open one transaction per tenant; a single transaction spanning two tenants is rejected by the assertion. The outbox row carries `org_id`, so the dispatcher cannot lose it.

**Scheduled jobs.** A Quartz job that operates per tenant is parameterised by `org_id` and runs once per tenant. A global job that legitimately spans tenants (for example, a retention sweep that must find expired tenants) runs as a separate platform role with no access to tenant financial tables; it reads only the tenancy directory. There is no job that reads tenant data across tenants.

**Trace propagation.** The `org_id` is attached to the OpenTelemetry span as a resource attribute and to every log line as a structured field. A trace can therefore be filtered by tenant, and a log line without a tenant is a defect. Personal data is never attached to a span.

### 7.3 RLS Design Rules

| Rule | Detail | Reason |
|------|--------|--------|
| Every tenant table has `org_id` | Even child tables that could join to a parent | A policy that depends on a join can be bypassed by a query that avoids the join; a direct column cannot |
| RLS is `ENABLE`d and `FORCE`d | `ALTER TABLE ... ENABLE ROW LEVEL SECURITY; ALTER TABLE ... FORCE ROW LEVEL SECURITY;` | `FORCE` applies the policy to the table owner too, which closes the accidental-owner path |
| The application role is not the owner | `slateiq_app` has no table ownership, no `BYPASSRLS`, no `SUPERUSER`, no DDL | Ownership is a policy bypass in PostgreSQL |
| One policy per command per table family | `SELECT`, `INSERT`, `UPDATE`, `DELETE` are separate | `WITH CHECK` is not applied to `SELECT`, and a single `ALL` policy hides that distinction |
| Permissive policies are ORed, restrictive policies are ANDed | Tenant policy and grant policy are permissive; the write guard is restrictive | PostgreSQL semantics; using the wrong type silently widens access |
| No policy calls a user-defined function that is not audited | Inline predicates only for the tenant check | A `SECURITY DEFINER` function is a privilege boundary and a performance trap |
| Every policy predicate starts with an indexed column | `org_id` is the leading column of every composite index | A policy that scans is a denial-of-service vector |
| Test role mirrors the application role | `slateiq_test_app` has the same grants and no bypass | Tests that run as a superuser prove nothing |

**The policy set is versioned.** Policies are migration artefacts. A drift test compares `pg_policies` with the expected manifest; a policy changed outside a migration fails the build. This is how an emergency `GRANT ALL` or a hastily added `USING (true)` is caught.

### 7.4 Policy Templates by Table Family

#### Family A — Tenant-direct

The table carries `org_id` and no finer scope. Examples: `tenancy.organization`, `tenancy.membership`, `identity.user`, `vendor.vendor`, `report.report_run`, `compliance.legal_hold`.

```sql
ALTER TABLE tenancy.membership ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.membership FORCE ROW LEVEL SECURITY;

CREATE POLICY membership_tenant_read ON tenancy.membership
  FOR SELECT
  USING (org_id = current_setting('app.org_id')::uuid);

CREATE POLICY membership_tenant_write ON tenancy.membership
  FOR ALL
  USING (org_id = current_setting('app.org_id')::uuid)
  WITH CHECK (org_id = current_setting('app.org_id')::uuid);
```

`identity.user` is special: its tenant column is `home_org_id`. The policy is `home_org_id = current_setting('app.org_id')::uuid`, plus a self-read policy `user_id = current_setting('app.user_id')::uuid`. A client organization cannot read an auditor's identity row; the grant list shows a name snapshot recorded on the grant.

#### Family B — Scope-bearing

The table carries `org_id` plus one or more of `show_id`, `season_id`, `episode_id`, `department_id`. Examples: `prod.show`, `budget.budget`, `expense.expense`, `document.document`, `audit.audit_finding`.

```sql
CREATE POLICY expense_tenant_read ON expense.expense
  FOR SELECT
  USING (org_id = current_setting('app.org_id')::uuid);

CREATE POLICY expense_scope_read ON expense.expense
  AS RESTRICTIVE
  FOR SELECT
  USING (
    current_setting('app.scope', true) IS NULL
    OR current_setting('app.scope', true) = 'ORG'
    OR (current_setting('app.scope', true) = 'SHOW'
        AND show_id = ANY(current_setting('app.show_ids')::uuid[]))
    OR (current_setting('app.scope', true) = 'DEPARTMENT'
        AND department_id = ANY(current_setting('app.department_ids')::uuid[]))
  );
```

The `app.scope` variables are set by the middleware from the resolved effective scope. They are an additional gate, not the primary one: the PDP remains authoritative, and a missing scope variable is treated as `ORG` only when the principal's authority source is `MEMBERSHIP` with `ORG` scope. For grants, the grant policy is restrictive and always applies, so a missing member scope variable cannot widen a grant.

**Why `AS RESTRICTIVE` for scope.** Restrictive policies are ANDed with permissive ones. The tenant policy permits rows in the tenant; the scope policy narrows them to the actor's scope. If the scope policy were permissive, it would OR with the tenant policy and have no effect.

**Scope array limits.** `app.show_ids` and `app.department_ids` carry at most 200 identifiers. A user with more than 200 show assignments is an administrative outlier; the resolver groups them into an `ORG`-equivalent sentinel only when the assignment set genuinely covers every show, and otherwise rejects the request with `403 SCOPE_TOO_BROAD`. A 10,000-element array in a session variable is a performance and memory risk.

#### Family C — Grant-readable, grant-writable allowlist

External principals can read most tenant data and write a narrow set. The grant policy is permissive only on the allowlisted tables.

| Table | Grant SELECT | Grant INSERT | Grant UPDATE | Grant DELETE |
|-------|--------------|--------------|--------------|--------------|
| `prod.show`, `prod.season`, `prod.episode`, `prod.department` | Yes | No | No | No |
| `budget.budget`, `budget.budget_version`, `budget.budget_line` | Yes | No | No | No |
| `expense.expense`, `expense.expense_line` | Yes | No | No | No |
| `vendor.vendor`, `vendor.contract` | Yes | No | No | No |
| `document.document`, `document.version` | Yes | Yes (evidence only) | No | No |
| `audit.audit_finding` | Yes | Yes | Yes (own findings, pre-submit) | No |
| `audit.audit_evidence` | Yes | Yes | No | No |
| `audit.audit_report` | Yes | No | No | No |
| `tenancy.access_grant` | Own grants only | No | No | No |
| `identity.user` | No | No | No | No |
| `tenancy.role`, `tenancy.assignment` | No | No | No | No |
| `compliance.audit_event` | No | No | No | No |
| `ops.*` | No | No | No | No |

The grant policy is a single predicate that checks the grant scope, not a per-table rule. The table allowlist is enforced by the SQL grant itself: `slateiq_app` has `SELECT` on the readable tables and `INSERT/UPDATE` only on the allowlisted evidence tables. RLS cannot grant a table privilege that the role does not have, and the role cannot have a privilege that the migration manifest does not include. Two independent lists must agree.

#### Family D — Append-only audit

`compliance.audit_event` is append-only. `slateiq_app` has `INSERT` and `SELECT`, no `UPDATE`, no `DELETE`. A trigger rejects any update or delete even from a role that somehow has the privilege. The hash chain is computed in the insert trigger, and a verification job walks the chain per tenant.

```sql
CREATE POLICY audit_event_tenant_read ON compliance.audit_event
  FOR SELECT
  USING (org_id = current_setting('app.org_id')::uuid);

CREATE POLICY audit_event_tenant_insert ON compliance.audit_event
  FOR INSERT
  WITH CHECK (org_id = current_setting('app.org_id')::uuid);
```

Audit reads are themselves audited at a sampled rate for `compliance.audit_event.read`; reading the audit trail is a sensitive action, not a neutral one.

#### Family E — Global reference data

Currencies, country codes, the permission catalogue and the role catalogue are global. They carry no `org_id`, RLS is not enabled, and `slateiq_app` has `SELECT` only. Writes are migration-only. A tenant cannot create a currency or a permission.

#### Family F — Platform operations

`ops.*` tables are not granted to `slateiq_app` at all. They are accessed by `slateiq_ops`, a separate role used only by the platform operations service, which runs in a separate deployment with its own credentials. The tenant application cannot read provisioning runs, regions, support access or feature flags through the ordinary data path. Break-glass reads tenant tables through `slateiq_app` with a support context, not through `slateiq_ops`.

### 7.5 Policy Inventory

The complete inventory is Appendix D. The summary below shows the families and the number of tables each covers at the logical-model level; Document 5 maps them to physical tables.

| Family | Example tables | Policies | Special handling |
|--------|----------------|----------|------------------|
| A Tenant-direct | `organization`, `membership`, `user`, `vendor`, `legal_hold` | Tenant read, tenant write, self-read | `user` keyed on `home_org_id` |
| B Scope-bearing | `show`, `season`, `episode`, `department`, `budget`, `expense`, `document`, `finding` | Tenant read, scope read, tenant write | `app.scope` restrictive policy |
| C Grant-readable | Same as B plus `access_grant` | Grant read, grant evidence write | Table allowlist enforced by SQL grants |
| D Audit | `audit_event` | Tenant read, tenant insert | Append-only trigger and hash chain |
| E Reference | `permission`, `currency`, `country`, `role_catalogue` | None | Read-only grant |
| F Platform | `provisioning_run`, `tenant_region`, `support_access`, `feature_flag` | Platform role only | Not granted to the application role |

**Tables that must never carry `org_id`.** The permission catalogue, the role catalogue and reference data are global by design. A table that carries `org_id` but is not in the inventory is a build failure; a table that should carry it and does not is also a build failure. The coverage test reads the schema catalogue, not a hand-maintained list.

### 7.6 Special Cases

| Object | Isolation design | Residual risk |
|--------|------------------|---------------|
| Object storage | Key prefix `org/{org_id}/`; bucket policy denies `s3:GetObject` outside the caller's prefix; presigned URLs include a tenant claim and expire in 15 minutes (NFR-SEC-008) | A leaked URL is valid for its 15-minute window; mitigated by short expiry and download audit |
| Signed URL generation | The signer resolves the object's `org_id` from the database, not from the key string | A key traversal attempt fails at the prefix check |
| File upload | Stored under a quarantine prefix until malware scan completes (NFR-SEC-007); the scan result is tenant-scoped | A scan bypass would expose malware, not another tenant |
| Search | At MVP, PostgreSQL full-text search over RLS-protected tables; no separate index | A future external index must implement per-tenant filtering or document-level security before it is enabled |
| Cache | Key format `org:{org_id}:...`; the cache client asserts the prefix on every call | A developer who forgets the prefix gets a miss, not a cross-tenant hit, because the value is keyed by the full key |
| Export | Export job runs under the tenant context; the manifest records `org_id`, row counts and hashes; a mixed-tenant result aborts the job | A misconfigured export is visible in the manifest |
| Email and notifications | Templates render only the recipient's tenant data; the notification service receives a rendered payload, not a query handle | A template bug could include another tenant's data only if the payload were wrong; the payload is built under RLS |
| Telemetry | `org_id` allowed as a resource attribute; no amounts, names, emails or document titles | Log aggregation in the tenant's region; redaction tested |
| Backups | Encrypted with the tenant's regional key; restore drill asserts that a backup from region A cannot be restored in region B | Key availability is the control; a key copied across regions would break it, so key policy is audited |
| Support bundles | Generated under support access; contain only the named tenant; expire after 7 days | Manual handling risk; bundles are watermarked and access-logged |
| Error messages | Never include a foreign identifier, SQL, stack trace or row count | A generic error is less helpful and more secure |
| AI or vector features | Not in MVP scope; any future feature must pass the same tenant-context test before enablement | Deferred, not exempt |

### 7.7 Connection and Role Model

| Role | Purpose | Privileges | Used by |
|------|---------|------------|---------|
| `slateiq_app` | Application runtime | `SELECT/INSERT/UPDATE/DELETE` on granted tables; no DDL; no `BYPASSRLS`; no ownership | API, sync host, workers |
| `slateiq_ops` | Platform operations | Access to `ops.*` and the tenancy directory; no financial tables | Provisioning and platform service |
| `slateiq_migrator` | Schema migrations | DDL, policy changes, ownership; not `SUPERUSER` | CI/CD only, never deployed |
| `slateiq_test_app` | Test suite | Same as `slateiq_app` | Integration and isolation tests |
| `slateiq_readonly` | Analytics (Phase 1) | `SELECT` on approved views only; RLS still applies | Future reporting replica |

**PgBouncer.** Transaction pooling, `pool_mode = transaction`, `server_reset_query = DISCARD ALL` as a second defence, maximum pool size per service. Because `SET LOCAL` dies at commit, `DISCARD ALL` is redundant but harmless; it protects against a future session-level mistake.

**Connection string separation.** The application, the platform service and migrations use different credentials and different secrets. A compromise of the application credential does not yield DDL or `ops` access. The migration credential exists only in the deployment pipeline's secret scope.

**No shared superuser.** There is no human-used database superuser in production. Break-glass database access is through the platform's audited support path, not through a shared `postgres` password. The managed database's emergency account is vaulted, requires two approvals, and its use pages Security.

### 7.8 RLS Verification Suite

| Test | Method | Pass criterion | Frequency |
|------|--------|----------------|-----------|
| Policy coverage | Query `pg_class` for tables with `org_id`; assert `relrowsecurity` and `relforcerowsecurity` and at least one policy | 100 percent of tenant tables | Every build |
| Policy drift | Compare `pg_policies` with the migration manifest | No difference | Every build |
| Tenant read isolation | Insert as tenant A; select as tenant B for every tenant table | Zero rows | Every build |
| Tenant write isolation | Insert or update a row with tenant B's `org_id` while context is A | Error or zero rows | Every build |
| Unset context | Run a tenant query without `app.org_id` | Error, not empty result | Every build |
| Grant read | Grant scope S1; read S1 and S2 | S1 rows, zero S2 rows | Every build |
| Grant write allowlist | Attempt write to each non-allowlisted table | Permission denied | Every build |
| Grant expiry | Set `end_at` in the past; read | Zero rows | Every build |
| Role escalation | Run as `slateiq_app`; attempt `SET ROLE slateiq_migrator` | Denied | Every build |
| Policy performance | `EXPLAIN` the ten hottest tenant queries | Index scan on `org_id`; no sequential scan on a large table | Every release |
| Restore isolation | Restore a backup into a different region | Decryption fails | Quarterly |
| Penetration test | External firm attempts cross-tenant access | No finding above medium | Annually (NFR-SEC-005) |

**The isolation suite runs as the application role.** A test that connects as the owner or a superuser bypasses RLS and proves nothing. The suite's connection string is the same shape as production, with the same grants, and the test asserts `current_user = 'slateiq_app'` before it starts.

**Test data.** Two tenants are seeded with identical-looking data, including identical slugs in different regions and colliding business keys, so that a query which forgets `org_id` returns rows rather than an empty set. A test that passes because the other tenant has no data is worse than no test.

**Continuous execution.** The suite runs on every pull request against a Testcontainers PostgreSQL instance, on every deployment against a staging clone, and nightly against a production-shaped dataset. NFR-SEC-005 requires automated tests per release plus an annual penetration test; the nightly run is the evidence that the automated part is real.

### 7.9 Isolation Incident Response

| Phase | Action | Owner | Evidence |
|-------|--------|-------|----------|
| Detect | Alert on `CrossTenantAccessAttempt`, RLS policy drift, anomalous grant scope or a support access anomaly | Security on-call | Alert ID, trace, event |
| Contain | Revoke the session, suspend the principal, freeze the grant, or suspend the tenant | Security on-call with Platform Operations | Revocation records |
| Assess | Determine which rows or objects were read, by whom, and for how long | Security and Data Protection Officer | Audit query, object access log |
| Notify | Follow the breach notification runbook; 72 hours where required (NFR-PRV-007) | DPO and Legal | Notification record |
| Remediate | Fix the policy, the query or the grant; add a regression test | Engineering | Pull request, test evidence |
| Review | Blameless postmortem; update the threat model and the isolation suite | Principal Architect | Postmortem, action items |

**Freeze before fix.** The first action is to stop the exposure, not to understand it. A grant can be frozen without being revoked, a session can be revoked without deprovisioning the user, and a tenant can be suspended without deleting data. Reversible containment is always preferred, and every containment action is itself audited.

### 7.10 Traceability for Section 7

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| FR-TNT-018 | Sections 7.2, 7.3 | Context from token only; unset context errors |
| NFR-SEC-004 | Sections 7.1, 7.3 | RLS as one of four layers |
| NFR-SEC-005 | Section 7.8 | Per-build isolation suite plus annual pen test |
| NFR-SEC-007 | Section 7.6 | Quarantine prefix and scan gate |
| NFR-SEC-008 | Section 7.6 | 15-minute signed URLs |
| NFR-PRV-009 | Sections 7.6, 2.8 | Regional keys and backup restore test |
| NFR-AUD-002 | Section 7.4 | Append-only audit table and trigger |
| ADR-002 | Sections 7.3, 7.4 | Shared schema with RLS |
| CAP-001 | Sections 7.1 to 7.8 | Tenant isolation capability |
