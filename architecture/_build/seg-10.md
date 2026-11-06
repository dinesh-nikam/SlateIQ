---

## 15. Observability and Security Monitoring

### 15.1 Purpose and Audiences

One telemetry pipeline serves three audiences with different questions:

| Audience | Question | Primary surface |
|----------|----------|-----------------|
| Operations | Is the platform healthy, and what is slow or failing? | SLO dashboards, traces, saturation metrics |
| Security | Is someone attacking, and did they succeed? | Detection rules, denial and cross-tenant metrics, incident queue |
| Audit and compliance | Can we prove the control operated? | Immutable audit events, probe evidence, access-review records |

The three are not separate stacks. Security detections consume the same events that produce the business audit trail, and the audit trail is the evidence that a detection was investigated. A second, parallel security tool that sees different data is a second source of truth, and the two will disagree during the incident when agreement matters most.

### 15.2 Telemetry Standard

| Signal | Standard | Retention | Notes |
|--------|----------|-----------|-------|
| Traces | OpenTelemetry, W3C `traceparent`, OTLP export | 7 days hot, 30 days sampled | 100 per cent of errors and denials, 10 per cent baseline |
| Metrics | OpenTelemetry metrics, Prometheus exposition | 13 months, 15-second resolution for 30 days | Recording rules for dashboards |
| Logs | Structured JSON to stdout, collected by the platform agent | 30 days hot, 13 months archive | No PII, no secrets, no tokens |
| Audit events | `compliance.audit_event`, append-only | 24 months hot, 7 years WORM | Separate from operational logs |
| Security events | Event stream to the detection pipeline | 13 months hot, 7 years archive | Same envelope as section 12 |

**Resource attributes.** Every span, metric and log carries `service.name`, `service.version`, `deployment.environment`, `cloud.region` and `deployment.cell`. Without `service.version` a regression cannot be tied to a release; without `cloud.region` a residency incident cannot be scoped.

**Span attributes for the identity path.** `endpoint.route`, `http.response.status_code`, `org.id`, `actor.id`, `authority.source`, `authority.id`, `permission.key`, `policy.decision`, `policy.reason`, `cache.state`, `grant.id`, `session.id`. These are identifiers, not personal data. `actor.id` is a pseudonymous UUID, never an email or a display name.

**No high-cardinality explosion.** `permission.key` is bounded by the catalogue, `endpoint.route` is the templated route rather than the concrete path, and `actor.id` is never used as a metric label. A metric labelled by user ID creates one time series per user and takes down the metrics backend before it takes down the platform.

**Log fields.** `timestamp`, `level`, `service`, `version`, `environment`, `region`, `trace_id`, `span_id`, `correlation_id`, `org_id`, `actor_id`, `event`, `message`, `error_code`, `duration_ms`. Additional fields are allowlisted per event. A field not on the allowlist is dropped with a counter increment rather than logged opportunistically.

### 15.3 Metric Catalogue

| Metric | Type | Labels | Purpose | Alert |
|--------|------|--------|---------|-------|
| `auth_login_total` | counter | `result`, `method`, `reason` | Login volume and failure mix | Failure ratio above 20 per cent for 5 min |
| `auth_login_duration_ms` | histogram | `method`, `mfa` | Argon2id and end-to-end cost | p95 above 800 ms for 10 min |
| `auth_lockout_total` | counter | `reason` | Lockout volume | Above 50 per 5 min |
| `auth_mfa_challenge_total` | counter | `result`, `method` | MFA health | Failure ratio above 30 per cent |
| `session_active` | gauge | `client_type` | Active sessions | Drop above 20 per cent in 5 min |
| `session_revocation_duration_ms` | histogram | `reason` | Revocation cascade | p95 above 60 s |
| `token_validation_total` | counter | `result` | Token health | Signature failures above 10 per min |
| `token_reuse_total` | counter | `client_type` | Refresh reuse | Any occurrence |
| `policy_decision_total` | counter | `decision`, `reason`, `cache` | Authorization outcomes | Denial ratio baseline shift |
| `policy_decision_duration_ms` | histogram | `cache`, `authority_source` | PDP latency | p95 above 20 ms warm |
| `policy_cache_hit_ratio` | gauge | — | Cache effectiveness | Below 95 per cent for 15 min |
| `permission_denied_total` | counter | `permission_key`, `reason` | Denial pressure | Above 100 per user per minute |
| `cross_tenant_attempt_total` | counter | `route`, `resource_type` | Isolation attacks | Any occurrence |
| `rls_context_unset_total` | counter | `service` | Missing tenant context | Any occurrence |
| `isolation_probe_total` | counter | `result`, `tenant_class` | Probe health | Any failure |
| `grant_state_total` | gauge | `state`, `profile` | Grant inventory | Active grants above forecast |
| `grant_access_denied_total` | counter | `reason`, `profile` | Grant boundary pressure | Burst above baseline |
| `grant_cascade_duration_ms` | histogram | `reason` | Revocation timing | p95 above 60 s |
| `grant_expiry_notice_total` | counter | `stage`, `result` | FR-TNT-017 evidence | Any `FAILED` at T-1 or T-0 |
| `provisioning_run_total` | counter | `result`, `failed_step` | Provisioning health | Any failure |
| `provisioning_duration_ms` | histogram | `plan` | Saga duration | p95 above 6 min |
| `outbox_oldest_age_seconds` | gauge | `queue_class` | Event freshness | Above 30 s |
| `audit_write_failure_total` | counter | `module` | Audit integrity | Any occurrence |
| `audit_chain_verify_total` | counter | `result` | Tamper evidence | Any failure |
| `export_rows_total` | counter | `resource_type`, `authority_source` | Exfiltration signal | Above 3x user baseline |
| `support_access_active` | gauge | `scope` | Operator access | Above 0 outside a ticket |
| `dependency_up` | gauge | `dependency` | Availability | Down for 30 s |
| `http_request_duration_ms` | histogram | `route`, `method`, `status_class` | Endpoint latency | p95 above target for 10 min |
| `http_requests_total` | counter | `route`, `method`, `status_class` | Traffic and errors | 5xx above 0.5 per cent |

**Metrics are derived from spans where possible.** A hand-incremented counter drifts from the request path; a span-derived metric cannot. Business-critical counters (`audit_write_failure_total`, `cross_tenant_attempt_total`) are incremented explicitly because they must fire even when the request is rejected before a span is completed.

### 15.4 Logging Rules

| Rule | Detail |
|------|--------|
| Level discipline | `ERROR` for a failed operation requiring attention, `WARN` for a denied or degraded operation, `INFO` for a completed state change, `DEBUG` disabled in production |
| No secrets | No password, token, TOTP secret, recovery code, key material or connection string, including in exception messages |
| No personal data | No email, phone, display name, address or free-text note; use `user_id` and `email_hash` |
| No request bodies | Bodies are not logged; field-level validation errors are logged by field name and code |
| Errors carry codes | `error_code` is the stable API code; the exception type is logged without its message when the message could contain data |
| Correlation | Every log line carries `trace_id` and `correlation_id` |
| Sampling | `INFO` logs for high-volume read paths are sampled at 1 per cent; security and state-change logs are never sampled |
| Redaction test | A CI test submits known PII patterns through every log call and fails on a match |

**An exception message is an uncontrolled string.** A database exception can contain a parameter value, and a parameter value can be an email. The logging layer records the exception type, the error code and the stack frame, not `ex.Message` for data-bearing exceptions. The detail stays in the trace, where access is narrower and retention is shorter.

### 15.5 Tracing the Authorization Path

Every request produces one trace with the following spans:

| Span | Parent | Key attributes |
|------|--------|----------------|
| `http.server` | — | route, method, status |
| `auth.token.validate` | `http.server` | `kid`, result |
| `auth.session.check` | `http.server` | `session.id`, denylist result, cache state |
| `tenancy.context.resolve` | `http.server` | `org.id`, authority source |
| `authz.decide` | `http.server` | permission, scope, decision, reason |
| `authz.pdp.membership` | `authz.decide` | membership state |
| `authz.pdp.assignment` | `authz.decide` | assignment count |
| `authz.pdp.grant` | `authz.decide` | grant id, profile |
| `authz.pdp.sod` | `authz.decide` | rule key |
| `db.query` | handler | statement name, rows, duration |
| `outbox.publish` | handler | event type, aggregate type |

**Denials are traced at 100 per cent.** A denial is the moment the system's understanding of authority diverges from the user's, and it is exactly the trace an engineer needs when a customer reports "it used to work". Sampling denials at 10 per cent guarantees that the interesting one is missing.

**Traces never carry the token.** The span records `kid`, `session.id` and the decision. It does not record the bearer value, the refresh token or the claims payload.

### 15.6 Security Detections

Each detection has a stable identifier, a signal, a threshold, a severity and a first response. A detection without an owner and a runbook is removed, because an unactioned alert trains the team to ignore the queue.

| ID | Detection | Signal and threshold | Severity | First response | Threat |
|----|-----------|----------------------|----------|----------------|--------|
| **DET-4-01** | Credential stuffing | More than 200 `LoginFailed` across an IP class in 1 hour, or above 50 from one class in 5 minutes | SEV-2 | Edge block, notify affected account owners, review breach-list hits | T4-01 |
| **DET-4-02** | Password spraying | Above 20 distinct accounts fail from one IP class in 15 minutes with fewer than 2 attempts each | SEV-2 | Edge block, force MFA challenge on targeted accounts | T4-01 |
| **DET-4-03** | Lockout wave | Above 50 `auth_lockout_total` in 5 minutes, or one account locked 3 times in an hour | SEV-3 | Confirm not an attack, offer reset, tune per-IP limit | T4-02 |
| **DET-4-04** | MFA failure burst | Above 10 `MfaChallengeFailed` for one user in 10 minutes | SEV-2 | Lock challenge, notify user, review session origin | T4-05 |
| **DET-4-05** | Refresh token reuse | Any `RefreshTokenReuseDetected` | SEV-1 | Confirm family revoked, notify user, preserve evidence | T4-06 |
| **DET-4-06** | Token anomaly | Signature failure above 10 per minute, or unknown `kid` | SEV-1 | Verify key rotation, check for forgery, review issuer config | T4-08 |
| **DET-4-07** | Cross-tenant probe | Any `CrossTenantAccessAttempt`, or above 5 foreign-object 404s per user per hour | SEV-1 | Identify actor, review all recent requests, notify tenant security contact if data may have been returned | T4-09, T4-10 |
| **DET-4-08** | Denial burst | Above 100 `PermissionDenied` per user per minute, or above 1,000 per tenant per hour | SEV-3 | Determine whether UI defect, misconfiguration or probing | T4-17 |
| **DET-4-09** | Authority escalation | A `RoleAssignmentCreated` widening scope, a custom role gaining `.administer`, or a self-assignment attempt | SEV-2 | Verify with the actor's manager, revert if unexplained | T4-17 |
| **DET-4-10** | Grant anomaly | More than 10 grants created by one user in an hour, `AUDIT_FINANCE` without co-approval, or a grant approved within 30 seconds of submission | SEV-2 | Review the register, confirm the engagement, suspend if unexplained | T4-15 |
| **DET-4-11** | Grant boundary pressure | Above 20 `GrantAccessDenied` for one grant in an hour, or access to an unassigned show | SEV-2 | Review access log with the client owner, consider suspension | T4-14 |
| **DET-4-12** | Export exfiltration | One user exports above 3x their 30-day baseline, or above 50,000 rows in a day | SEV-2 | Confirm business purpose, review watermark and recipients | T4-21 |
| **DET-4-13** | Support access write | Any write action during a support session | SEV-2 | Open review item, notify the tenant, verify the ticket | T4-18 |
| **DET-4-14** | Break-glass use | Any release of sealed credentials or `EmergencyRevocationExecuted` | SEV-1 | Page security, open incident, confirm ticket and approver | T4-19 |
| **DET-4-15** | Provisioning failure | Any `ProvisioningStepFailed` or run above 10 minutes | SEV-3 | Check compensation, confirm no partial tenant is reachable | T4-20 |
| **DET-4-16** | RLS drift | Policy inventory hash change outside a deployment, or any `IsolationProbeFailed` | SEV-1 | Suspend affected tenant, compare policies, roll back | T4-11 |
| **DET-4-17** | Audit chain break | `audit_chain_verify_total{result="FAIL"}` above zero | SEV-1 | Stop retention jobs, preserve the affected partitions, open incident | T4-30 |
| **DET-4-18** | Impossible travel | Two successful authentications from locations that cannot be reached in the elapsed time | SEV-2 | Step up the session, notify the user, review device | T4-07 |
| **DET-4-19** | SoD attack pattern | Above 3 `SodBlocked` for one user in an hour, or repeated self-approval attempts | SEV-3 | Review with the compliance officer, verify no exception exists | T4-15 |
| **DET-4-20** | Service-account anomaly | A service account used from an unexpected source, or outside its declared scope | SEV-1 | Disable the account, rotate the secret, review its actions | T4-19 |

**Severity definitions.** SEV-1 pages the security on-call and the incident commander immediately and opens an incident channel. SEV-2 pages the security on-call during business hours and the platform on-call at any time. SEV-3 creates a ticket for the next business day. SEV-4 is a weekly review item.

**Detection thresholds are configuration, not code.** They are tuned after the first thirty days of production data, and every change is recorded with the false-positive rate that motivated it. A threshold that is never tuned is either noisy or blind, and neither is visible from the alert count alone.

### 15.7 Service Level Objectives

| SLI | Definition | Target | Window | Burn alert |
|-----|------------|--------|--------|------------|
| Authentication availability | Successful or correctly rejected login responses divided by valid requests | 99.9% | 30 days | 2% budget in 1 hour |
| Authorization availability | PDP returns a decision, including a denial, rather than an error | 99.95% | 30 days | 2% budget in 1 hour |
| Authorization latency | Warm decision p95 under 20 ms | 99% of 5-minute windows | 30 days | 10% of windows breaching |
| Revocation latency | Revoked session or grant denied within 5 minutes | 100% | 30 days | Any breach |
| Grant expiry enforcement | Expired grant denied at decision time | 100% | 30 days | Any breach |
| Provisioning success | Runs reaching `ACTIVE` or fully compensated | 98% first attempt, 100% terminal | 30 days | Two consecutive failures |
| Audit write success | Business transactions with a committed audit row | 100% | 30 days | Any failure |
| Isolation probe | Probe passes before activation and on every deploy | 100% | 30 days | Any failure |
| Event freshness | Outbox oldest age under 30 seconds | 99% of 5-minute windows | 30 days | 10 minutes above threshold |

**Revocation has no error budget.** A target of 99.9 per cent would permit roughly 43 minutes of stale authority per month, which is not a commitment the product can make to a client auditor. The 100 per cent target is measured against the five-minute commitment, and the internal 60-second target provides the margin that makes the commitment credible.

**Error budget policy.** When a service exhausts its budget, feature work for that service stops until reliability is restored, and the exception requires the Head of Engineering. The policy exists so that the budget is a decision-making tool rather than a dashboard decoration.

### 15.8 Dashboards

| Dashboard | Audience | Panels |
|-----------|----------|--------|
| `OPS-DSH-4-01 Identity` | Operations | Login rate and failures, MFA completion, lockouts, session count, refresh reuse, token validation errors |
| `OPS-DSH-4-02 Authorization` | Operations, engineering | Decisions per second, allow and deny ratio, PDP latency by cache state, cache hit ratio, denials by permission |
| `OPS-DSH-4-03 Tenancy` | Operations | Tenants by state and region, provisioning duration, failed steps, suspension count, purge queue |
| `OPS-DSH-4-04 Grants` | Security, customer success | Active grants by profile, expiries in 14 days, cascade latency, denied access, notices by delivery result |
| `OPS-DSH-4-05 Isolation` | Security | Probe results, cross-tenant attempts, context-unset errors, RLS policy hash, foreign-key violations |
| `OPS-DSH-4-06 Security` | Security | Detection counts by severity, open incidents, support access, break-glass, export volume, impossible travel |
| `OPS-DSH-4-07 SLO` | Leadership, operations | Error budgets, burn rate, availability, latency against target |

**Every panel links to its runbook.** A dashboard that shows a red number without a next step is an invitation to improvise during an incident.

### 15.9 Alert Routing and Runbooks

| Severity | Channel | Response time | Escalation | Examples |
|----------|---------|---------------|------------|----------|
| SEV-1 | Page: security on-call and platform on-call | 5 minutes | Incident commander, CISO, Head of Engineering | Cross-tenant attempt, token reuse, RLS drift, audit chain break, break-glass |
| SEV-2 | Page: platform on-call; security notified | 15 minutes | Security lead | Credential stuffing, grant anomaly, export anomaly, support write |
| SEV-3 | Ticket: platform team | Next business day | Team lead | Lockout wave, provisioning failure, denial burst |
| SEV-4 | Weekly review | — | — | Threshold tuning, false-positive clusters |

**No alert without a runbook.** Each alert links to a runbook that names the first three actions, the evidence to preserve and the person to notify. The runbook for a cross-tenant alert begins with "do not delete anything", because the instinct to clean up destroys the evidence needed to determine scope.

### 15.10 Tenant-Aware Telemetry Without Tenant Data

NFR-OBS-004 requires monitoring to be tenant-aware without exposing tenant data. The line is drawn at identifiers:

| Allowed | Prohibited |
|---------|------------|
| `org_id` as a metric label on low-cardinality aggregates | Organization name, slug or domain |
| Per-tenant latency and error aggregates | Request or response bodies |
| Tenant state and region | Member lists or email addresses |
| Grant identifiers and profile keys | Grant scope labels containing show titles |
| Audit event counts | Audit event free text |

**Customer-facing status is aggregate.** A tenant administrator sees their own service health and their own audit events. They do not see platform-wide metrics, other tenants' identifiers or infrastructure topology. The status page reports regional health, never per-tenant detail.

### 15.11 Evidence for the SOC 2 Examination

AD4-07 requires every control to produce retrievable evidence without a manual reconstruction exercise. The evidence map is:

| Control area | Evidence | Source | Retention |
|--------------|----------|--------|-----------|
| Logical access | Access review campaigns, certifications, revocations | `compliance.audit_event`, review records | 7 years |
| Provisioning and deprovisioning | Provisioning runs, activation probes, deactivation events | `ops.provisioning_run`, audit | 7 years |
| Authentication | MFA enrolment coverage, lockout events, reset approvals | Audit, metric snapshots | 7 years |
| Authorization | Role change history, permission versions, SoD blocks | Audit, `tenancy.role_version` | 7 years |
| External access | Grant register, approvals, acceptances, notices, access logs | `tenancy.access_grant`, audit | 7 years |
| Monitoring | Detection rule inventory, alert records, incident reports | Detection configuration, incident system | 3 years |
| Change management | Build results, policy inventory diffs, penetration-test reports | CI records, security reports | 3 years |
| Isolation | Probe results per deployment and per activation | `ops.isolation_probe` | 7 years |

**Evidence is generated by the control, not by the auditor's request.** A control that requires an engineer to run a query and paste the result into a spreadsheet is a control that will be reconstructed inconsistently at the end of the audit period. The probe, the review and the approval write their own evidence as a side effect of operating.

### 15.12 Traceability for Section 15

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| NFR-OBS-001 | Sections 15.2 to 15.5 | Logs, metrics and traces for every service |
| NFR-OBS-002 | Sections 15.6, 15.9 | Detections and five-minute paging |
| NFR-OBS-003 | Section 15.2 | Health and readiness endpoints inherited from Document 3 |
| NFR-OBS-004 | Sections 15.3, 15.10 | Identifier-only labels and aggregate status |
| NFR-OBS-005 | Sections 15.6, 15.11 | Support-access detection and evidence |
| NFR-SEC-005 | Sections 15.6, 15.7 | Isolation probe and continuous testing |
| NFR-SEC-013 | Section 15.6 | Abuse detections |
| NFR-AUD-001 | Section 15.4 | Audit separation from operational logs |
| NFR-AUD-002 | Sections 15.6, 15.11 | Chain verification and WORM evidence |
| NFR-AUD-005 | Section 15.7 | Audit query SLO |
| NFR-PRV-007 | Sections 15.6, 15.9 | Breach detection and incident routing |
| FR-TNT-016 | Section 15.6 | Grant boundary and cascade detections |
| FR-TNT-017 | Sections 15.3, 15.6 | Notice delivery metrics and alerts |
| FR-TNT-018 | Section 15.6 | Cross-tenant probe detection |
| FR-TNT-021 | Section 15.11 | Access review evidence |
| CAP-001 | Section 15.8 | Tenancy dashboard |
| CAP-002 | Section 15.8 | Identity dashboard |
| CAP-003 | Sections 15.6, 15.8 | Authorization and grant telemetry |

---

## 16. Test Strategy

### 16.1 Principles

1. **Security properties are tested negatively.** A test that proves an authorized user can read their own data proves almost nothing. The test that matters proves a member of another tenant cannot, a grantee outside scope cannot, and a revoked session cannot.
2. **The database is real.** RLS, constraints, partitions and `SET LOCAL` behaviour are the controls under test. An in-memory provider or a mocked repository tests the mock, not the isolation.
3. **Authorization is tested per endpoint, not per role.** A role-based smoke test misses the endpoint that forgot to declare a permission. The matrix is generated from the route inventory, and an undeclared route fails the build.
4. **State machines are tested as matrices.** Every state and every event is a cell, including the illegal ones. The illegal cells are the security-relevant ones.
5. **A flaky security test is a failing security test.** Retries are permitted for infrastructure timeouts only; a test that is retried because it is order-dependent is quarantined and fixed, not ignored.
6. **Evidence is retained.** Test results, probe outputs and penetration-test reports are evidence for the SOC 2 examination (section 15.11).

### 16.2 Test Levels and Ownership

| Level | Scope | Owner | Runs |
|-------|-------|-------|------|
| Unit | Policy functions, scope algebra, state guards, claim validation, password policy | Module team | Every commit |
| Component | One module with a real database and Redis | Module team | Every pull request |
| Integration | API through the pipeline to PostgreSQL, Redis and the outbox | Platform team | Every pull request |
| Isolation | Negative RLS and cross-tenant suite | Security engineering | Every pull request and every deploy |
| Authorization matrix | Every route, every role, allowed and denied | Security engineering | Every pull request |
| Contract | OpenAPI diff, event schema, error codes | Platform team | Every pull request |
| State machine | Grant, invitation, membership, role and provisioning transitions | Module team | Every pull request |
| Performance | LT-4 scenarios | Performance engineering | Nightly and before release |
| Resilience | Dependency failure, region failover, duplicate delivery | Platform team | Weekly and before release |
| Penetration | External and internal, ASVS Level 2 | External firm | Annually and before a major release |
| Acceptance | Customer scenarios, auditor workspace | Product | Before release |

### 16.3 Unit Tests

| Area | What is tested | Coverage target |
|------|----------------|-----------------|
| Scope algebra | Intersection of `ORG`, `SHOW`, `SEASON`, `EPISODE`, `DEPARTMENT`, `ASSIGNMENT`, `SELF`; narrower never wider | 100 per cent branch |
| Grant state machine | Every legal transition and every illegal transition | 100 per cent transition matrix |
| Password policy | Length, classes, passphrase alternative, breach list, identity tokens | 100 per cent branch |
| Token claims | Exact claim set, `org` authority, `sid`, `amr`, `scope_version`, `device_id` | 100 per cent |
| Approval resolution | Role limit, user limit, delegation, amount, currency, self-approval | 100 per cent branch |
| SoD evaluator | Ten rules, exception handling, self-approval prohibition | 100 per cent rule coverage |
| Effective permission | Membership plus assignment plus scope plus vetoes | 100 per cent branch |
| Grant scope validation | Foreign identifiers, `ORG` rejection, date bounds, 12-month ceiling | 100 per cent branch |

**Mutation testing on the PDP.** Line coverage cannot distinguish a test that asserts `allowed = false` from a test that never runs the denial branch. The PDP and scope algebra are mutation-tested with a target of 85 per cent mutation score; a surviving mutant in a denial path is a release blocker.

### 16.4 Integration Tests

| Concern | Approach |
|---------|----------|
| Database | Real PostgreSQL 17 container with the production migration set and RLS enabled |
| Cache | Real Redis 7.4 container; no in-memory substitute |
| Keys | Test KMS with per-test key material; signing and encryption paths exercised |
| Object storage | MinIO or equivalent with versioning and Object Lock enabled |
| Events | Real outbox table, real relay process, consumer test harness |
| Clock | Injectable clock at the boundary; database time used for expiry assertions |
| Data | Deterministic synthetic tenants: A, B, auditor firm, platform internal |
| Cleanup | Transaction rollback per test where possible; truncate for state-machine suites |

**Three tenants are the minimum.** Tenant A is the actor's tenant, tenant B is the foreign tenant, and the auditor firm is the external principal. A suite with one tenant cannot express the failure that matters.

### 16.5 Isolation Test Suite

The isolation suite is generated from the schema, not hand-written per table. For every table with an `org_id` column it asserts:

| Assertion | Expected result |
|-----------|-----------------|
| `SELECT` as tenant A after setting `app.org_id = A` | Returns A rows only |
| `SELECT` as tenant A without setting context | Error `CONTEXT_UNSET`, never all rows |
| `SELECT` as tenant A with `app.org_id = B` | Returns zero rows |
| `INSERT` with `org_id = B` under A context | Policy violation |
| `UPDATE` a B row under A context | Zero rows affected |
| `DELETE` a B row under A context | Zero rows affected |
| `SELECT` as a grantee with `app.grant_ids` not containing the grant | Zero rows |
| `SELECT` as a grantee with the grant but outside `app.grant_scope` | Zero rows |
| `SELECT` as a grantee with the grant and inside scope | Only the scoped rows |
| `SELECT` as `slateiq_app` after `RESET ALL` | Error, not a default tenant |
| `SELECT` from a table with RLS disabled | Policy inventory test fails the build |
| Cross-tenant foreign key | Constraint rejects the row |

**Policy inventory.** The test enumerates `pg_class` and `pg_policies` and asserts that every table carrying `org_id` has row-level security enabled, forced, and at least one policy for each of `SELECT`, `INSERT`, `UPDATE` and `DELETE` (or an explicit, reviewed exception). The inventory hash is stored with each deployment, and a change outside a migration raises DET-4-16.

**Policy canary.** A staging environment deliberately drops one policy on a scratch table and runs the suite; the suite must fail. This tests the test, and it catches a suite that has silently stopped enumerating tables after a schema rename.

**Restore isolation.** The annual restore drill restores a backup into an isolated account, sets context for one tenant, and asserts that no row from another tenant is visible through any route, view or materialised view. A restore that passes functional checks but fails this assertion is a failed restore.

### 16.6 Authorization Matrix

| Dimension | Values |
|-----------|--------|
| Route | Every route in the OpenAPI document |
| Actor | Member with each of the 17 seeded roles, custom role, grantee with each of 3 profiles, platform operator, service account, anonymous |
| Tenant | Own tenant, foreign tenant, suspended tenant, archived tenant |
| Object | Own-scope object, out-of-scope object in the same tenant, foreign-tenant object, non-existent object |
| State | Active, suspended, expired, revoked, draft |
| Expected | `200`, `403` with a specific code, `404`, `409`, or `410` |

**The matrix is generated.** A route without a permission declaration fails the build. A role-permission change regenerates the expected matrix from the catalogue, so the test and the design cannot drift independently.

**Denial assertions are specific.** A test that accepts any `403` passes when the endpoint returns `PERMISSION_DENIED` for the wrong reason. The assertion names the `code`, and for cross-tenant objects it requires `404` rather than `403`.

### 16.7 Grant Lifecycle Tests

The grant has eight states and a guarded transition for each legal pair. The suite asserts the full matrix plus the following invariants:

| Invariant | Test |
|-----------|------|
| A grant is created only in `DRAFT` | Direct insert into another state fails |
| Approval requires a different human from the creator | Self-approval returns `403 SELF_APPROVAL_PROHIBITED` |
| `AUDIT_FINANCE` requires Finance Head co-approval | Approval without it returns `409 GRANT_NOT_APPROVABLE` |
| Acceptance requires MFA, NDA and no unresolved conflict | Each gate returns its specific `409` |
| Activation is atomic across the ten steps | Injected failure at each step leaves no partial authority |
| An active grant cannot be widened without a new approval | `PATCH` returns `409 GRANT_VARIATION_REQUIRES_APPROVAL` |
| Expiry is enforced at decision time | A grant past `end_at` is denied even with the expiry job disabled |
| Revocation cascades within the commitment | 99 per cent under 60 seconds, 100 per cent under 5 minutes |
| A revoked grant cannot be resumed | `resume` returns `409` |
| Renewal creates a new grant and does not extend the old one | Old `end_at` unchanged; `supersedes_grant_id` set |
| A grant never converts to a membership | No route accepts that transition |
| The seven-year record survives revocation | Row present, state `REVOKED`, evidence intact |

**Clock boundary tests.** Expiry is tested at `end_at - 1s`, `end_at`, `end_at + 1s`, and with a clock skew of plus and minus five minutes. The decision uses database time, and the test proves the application host clock cannot extend authority.

**Access review and delegation tests.**

| Test | Assertion |
|------|-----------|
| Campaign coverage | Opening a campaign creates an item for every active membership, every assignment and every active grant |
| Self-review block | A reviewer cannot certify their own membership, assignment or grant; the request returns `403 SELF_APPROVAL_PROHIBITED` |
| Closure | A campaign with an undecided item returns `409 REVIEW_INCOMPLETE`; closure executes pending revocations |
| Quarterly limit | A second campaign in the same quarter returns `409 CAMPAIGN_ALREADY_OPEN` |
| Overdue escalation | An open campaign past its due date emits `AccessReviewOverdue` and appears on the compliance dashboard |
| Delegation bounds | A delegation above 90 days, with sub-delegation, or above the delegator's amount returns `422 DELEGATION_INVALID` or `403 APPROVAL_LIMIT_EXCEEDED` |
| Delegation attribution | An action taken under delegation records both the delegator and the delegate |
| Delegation self-use | A delegate cannot approve their own expense or extend their own delegation |

### 16.8 Session and Credential Tests

| Test | Assertion |
|------|-----------|
| Refresh rotation | Old token invalid after rotation; new token works |
| Reuse detection | Replaying a rotated token revokes the family and emits `RefreshTokenReuseDetected` |
| Idempotent replay grace | Same device and same request within 60 seconds returns the same result; a different device does not |
| Idle timeout | 30 minutes default; 15 minutes and 8 hours configured bounds enforced |
| Absolute session | 30 days regardless of activity |
| Logout all | Every session revoked, `scope_version` bumped, devices require re-authentication |
| Device binding | Mobile refresh without the device signature fails |
| Lockout | Five failures lock for 15 minutes; a correct password during lock still fails |
| Password reset | Token single-use, 30 minutes, all sessions revoked on success |
| MFA replay | Reused TOTP code fails; code from the previous step fails |
| Step-up | Assertion bound to session and action class; expired assertion fails |
| Break-glass | Sealed credential release requires two custodians and writes evidence |

### 16.9 Contract Tests

| Contract | Test |
|----------|------|
| OpenAPI | Generated from code, diffed in CI; a breaking diff fails unless the version is bumped |
| Error codes | Every code in section 11.4 is emitted by at least one test and documented |
| Event schema | Payload validated against the catalogue manifest; an unknown event type fails the build |
| Event compatibility | Consumers run against the previous and current schema version |
| ProblemDetails | Every error path validates against the RFC 9457 schema |
| Pagination | Cursor round-trip, stable order, `limit` bounds, unknown filter rejection |
| Idempotency | Same key and body replays; different body returns `409` |
| ETag | Missing `If-Match` returns `428`; stale returns `409 CONCURRENCY_STALE` |

### 16.10 Performance Tests

Section 14.8 defines the scenarios. The gates are:

| Gate | Threshold |
|------|-----------|
| Any p95 target | No regression above 10 per cent against the previous baseline |
| RLS overhead | p50 within 10 per cent, p95 within 20 per cent |
| Revocation cascade | 99 per cent under 60 seconds |
| Cold cache recovery | Under 2 minutes |
| Soak | No memory growth above 5 per cent over 24 hours |
| Error rate at peak | Below 0.1 per cent |

**Performance tests run with production-shaped data and RLS enabled.** A test against an empty database with a superuser measures nothing that will be true in production.

### 16.11 Resilience Tests

| Failure | Expected behaviour | Test |
|---------|--------------------|------|
| Redis unavailable | PDP falls back to database; denylist snapshot then fail closed | Kill Redis during mixed traffic |
| PostgreSQL failover | Brief 5xx, automatic recovery, no data loss | Managed failover during writes |
| KMS unavailable | New logins fail, existing tokens valid, PII decryption fails closed | Block KMS endpoint |
| Outbox relay stopped | Writes continue, events accumulate, alert fires | Stop relay for 15 minutes |
| Duplicate event delivery | Consumer applies once, no duplicate side effect | Publish each event twice |
| Out-of-order delivery | Consumer rejects stale state transition | Publish revoke before activate |
| Region failure | DR runbook restores within RTO and RPO | Annual exercise |
| Clock skew | Expiry uses database time; no authority extension | Shift application clock by 5 minutes |
| Partial network | Retries are idempotent; no duplicate authority | Introduce 20 per cent packet loss |

### 16.12 Security Testing

| Activity | Cadence | Scope | Owner |
|----------|---------|-------|-------|
| SAST | Every pull request | All application code | Engineering |
| SCA and SBOM | Every build | Dependencies and images | Platform |
| Secret scanning | Every commit | Repository and build logs | Platform |
| Container scanning | Every image | Base image and packages | Platform |
| DAST | Nightly against staging | Authenticated and unauthenticated routes | Security |
| Authorization fuzzing | Nightly | Route and object matrix | Security |
| Penetration test | Annually and before a major release | External and internal, ASVS Level 2 | External firm |
| Isolation review | Quarterly | Policy set, migrations, role grants | Security engineering |
| Tabletop exercise | Semi-annually | Cross-tenant incident, break-glass, region loss | CISO |

**NFR-SEC-005 requires an annual penetration test and automated tests per release.** The annual test is not a substitute for the per-build suite, and the suite is not a substitute for an adversarial human. Both are required.

### 16.13 Test Data

| Tenant | Purpose | Contents |
|--------|---------|----------|
| `test-a` | Actor tenant | 50 members, 12 roles, 8 shows, 2,000 expenses, 500 audit events |
| `test-b` | Foreign tenant | Equivalent volume, distinct identifiers, no shared members |
| `test-firm` | Auditor firm | 10 auditors, 3 clients, grants in every state |
| `test-platform` | Platform internal | Operators, support access, provisioning runs |
| `test-suspended` | Suspended tenant | Full data, no access |
| `test-archived` | Archived tenant | Export-only access |

**No production data in test.** Synthetic data is generated deterministically from a seed so that a failure is reproducible. A production snapshot used for performance testing is anonymised and access-controlled, and it never enters a developer environment.

### 16.14 CI/CD Gates

| Gate | Blocks merge | Blocks deploy | Blocks release |
|------|--------------|---------------|----------------|
| Unit tests | Yes | Yes | Yes |
| Component and integration | Yes | Yes | Yes |
| Isolation suite | Yes | Yes | Yes |
| Authorization matrix | Yes | Yes | Yes |
| Contract diff | Yes | Yes | Yes |
| SAST and secret scan | Yes | Yes | Yes |
| SCA high or critical | Yes | Yes | Yes |
| Mutation score on PDP | No | Yes | Yes |
| Performance regression | No | No | Yes |
| Resilience suite | No | No | Yes |
| Penetration test open criticals | No | No | Yes |

**The isolation suite is a merge gate, not a release gate.** A policy defect that reaches `main` is a defect that can reach production through an unrelated hotfix. The cost of running the suite on every pull request is minutes; the cost of a cross-tenant defect is the company.

### 16.15 Test Evidence Retention

| Artefact | Retention | Storage |
|----------|-----------|---------|
| CI test results | 2 years | Build system |
| Isolation probe results | 7 years | `ops.isolation_probe` |
| Authorization matrix report | 2 years | Build system |
| Performance results | 2 years | Build system |
| Penetration-test report | 7 years | Compliance repository |
| Incident post-mortems | 7 years | Compliance repository |

**Evidence must be retrievable by control, not by build number.** The compliance repository indexes each report by control area and period, so the SOC 2 examination can sample a quarter without an engineer reconstructing which build corresponded to which week.

### 16.16 Traceability for Section 16

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| NFR-SEC-004 | Sections 16.5, 16.6 | Isolation and authorization suites |
| NFR-SEC-005 | Sections 16.5, 16.12 | Per-build suite and annual test |
| NFR-SEC-009 | Section 16.12 | ASVS Level 2 assessment |
| NFR-SEC-010 | Section 16.12 | Remediation SLAs tracked from findings |
| NFR-SEC-013 | Section 16.11 | Abuse and load scenarios |
| NFR-AUD-001 | Section 16.6 | Denial audit assertions |
| NFR-AUD-002 | Section 16.7 | Immutable record assertions |
| NFR-AUD-005 | Section 16.10 | Audit query performance |
| NFR-PRV-006 | Section 16.7 | Erasure and DSR tests |
| NFR-PRV-009 | Sections 16.5, 16.11 | Restore isolation and region failure |
| NFR-MOB-004 | Section 16.8 | Device binding and offline bound |
| NFR-AVL-004 | Section 16.11 | Graceful degradation |
| NFR-AVL-006 | Section 16.11 | RTO exercise |
| NFR-AVL-007 | Section 16.11 | RPO exercise |
| NFR-OBS-002 | Section 15.9 | Alert routing tests |
| FR-TNT-007 | Section 16.8 | Lockout tests |
| FR-TNT-010 | Section 16.6 | Propagation tests |
| FR-TNT-013 | Section 16.6 | Assignment scope tests |
| FR-TNT-014 | Section 16.6 | Per-endpoint matrix |
| FR-TNT-015 | Section 16.7 | Grant creation tests |
| FR-TNT-016 | Section 16.7 | Acceptance and revocation tests |
| FR-TNT-017 | Section 15.6 | Notice delivery detection |
| FR-TNT-018 | Section 16.5 | Tenant context and isolation |
| FR-TNT-020 | Sections 16.3, 16.7 | Delegation unit and lifecycle tests |
| FR-TNT-021 | Section 16.7 | Review campaign tests |
| FR-TNT-022 | Section 16.8 | Session tests |
| CAP-001 | Section 16.7 | Provisioning and lifecycle tests |
| CAP-002 | Section 16.8 | Identity tests |
| CAP-003 | Sections 16.5, 16.6 | Authorization tests |

---
