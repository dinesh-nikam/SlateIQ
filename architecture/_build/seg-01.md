# Document 4: Multi-Tenancy & Identity Architecture

**Product:** SlateIQ — Production Cost Intelligence & Audit Monitoring Platform
**Document ID:** SLQ-ARCH-004
**Version:** 1.0.0 (Tenancy & Identity Baseline)
**Supersedes:** Nothing. Absorbs and retires the access-control material preserved at `_archive/03-actor-permission-architecture.v1.0.0.md`.
**Classification:** Internal — Architecture Foundation
**Status:** Draft — For Review
**Date:** 2026-11-05
**Authors:** Principal Enterprise Solution Architect, Security Architect, Identity Architect, Data Architect
**Reviewers:** Product Owner (TBD), Security & Compliance (TBD), Data Protection Officer (TBD), Principal Architect (TBD)

---

## Document Control

### Approval Matrix

| Role | Name | Responsibility | Approval Required |
|------|------|----------------|-------------------|
| Principal Architect | TBD | Architectural integrity and cross-document consistency | Yes |
| Security Architect | TBD | Threat model, isolation guarantees, session and credential design | Yes |
| Identity Architect | TBD | Identity model, permission catalogue, grant lifecycle | Yes |
| Data Architect | TBD | Schema, RLS policy set, referential integrity | Yes |
| Data Protection Officer | TBD | Personal data minimisation, DSR and erasure behaviour | Advisory |
| Product Owner | TBD | Fit to MVP scope and persona needs | Yes |
| Security & Compliance | TBD | SOC 2, SOX-supporting and ISO 27001 control alignment | Yes |

### Revision History

| Version | Date | Author | Change |
|---------|------|--------|--------|
| 1.0.0 | 2026-11-05 | Architecture Team | First baseline. Defines tenancy, identity, authentication, authorization, isolation and cross-organization grants. Supersedes the archived actor and permission draft. |

### Related Documents

| Document | Relationship |
|----------|--------------|
| Document 1 — Product Vision & Business Requirements | Source of FR-TNT-001 to FR-TNT-024, the security and privacy NFRs, decisions D-003, D-013, D-018, D-019 and D-021, and the persona catalogue P-01 to P-13 |
| Document 2 — Domain Model & Business Capabilities | Source of CAP-001, CAP-002 and CAP-003; its `User` entity is corrected by D2-05, not by this document |
| Document 3 — System Architecture & Technology Stack | Binding inputs: sections 7.1 to 7.3, 13.3, 13.4, ADR-002, ADR-008, ADR-009, Appendix C and its C.8 handoff |
| Document 5 — Data Architecture & Database Design | Consumes the schema, keys, indexes and RLS policy inventory defined here |
| Document 6 — API & Integration Architecture | Consumes the API surface and event contracts in sections 11 and 12 |
| Document 7 — Security & Compliance | Consumes the control commitments, threat model and test catalogue |
| Document 9 — Expense Capture & Offline Sync | Consumes session, device and deactivation behaviour |
| Document 15 — Mobile Application | Consumes device registration, biometric gate and offline session rules |
| Document 16 — Frontend & UX | Consumes permission-driven UI rules and denial handling |
| Document 17 — Deployment, Operations & Observability | Consumes tenant provisioning, break-glass and isolation telemetry |
| Document 18 — Implementation Roadmap | Consumes the build sequence implied by sections 10 and 17 |

### Reading Guide

| Reader | Start here |
|--------|------------|
| Executive sponsor | 1, 2.2, 2.5, 8.2, 13.1 |
| Identity or security engineer | 3, 4, 5, 6, 7, 8 |
| Backend engineer | 2, 5, 6, 7, 9, 11 |
| Data engineer | 7.2, 7.3, 9, Appendix D |
| Platform or SRE engineer | 2.9, 7.9, 10, 14, 15 |
| Mobile engineer | 4.6, 4.8, 9.5, 15.4 |
| Frontend engineer | 5.9, 6.7, 11.6 |
| Auditor or compliance reviewer | 5.11, 8, 13, 16, Appendix E |
| Document owner for Docs 5 to 18 | 9, 11, 12, Appendix G |

### Conventions

| Convention | Meaning |
|------------|---------|
| MUST, MUST NOT, SHOULD, MAY | Used with their RFC 2119 meanings. A MUST deviation requires an ADR. |
| `P-xx` | Persona identifier from Document 1 section 5.1 |
| `FR-TNT-xxx`, `NFR-xxx-xxx`, `BR-xxx-xxx`, `D-xxx` | Canonical identifiers from Document 1. Never renumbered here. |
| `CAP-xxx` | Capability identifier from Document 2 |
| `ADR-xxx` | Programme-wide decision record. This document owns ADR-016 to ADR-024; Document 3 owns ADR-001 to ADR-015. |
| `T4-xx` | Threat identifier local to this document. Document 3 uses `T-xx`; the two sets do not overlap. |
| `NT-xx` | Negative isolation test, continuing the catalogue started in Document 3 Appendix C.7 |
| `D4-xx` | Change request against Document 3 arising from this document |
| Permission key | `domain.resource.action`, for example `audit.access_grant.approve` |
| Scope token | `ORG`, `SHOW`, `SEASON`, `EPISODE`, `DEPARTMENT`, `ASSIGNMENT`, `SELF` |

### Authority and Conflict Resolution

This document is authoritative for the tenancy model, the identity model, the permission catalogue, the authorization algorithm, the RLS policy set, session behaviour and the cross-organization grant lifecycle. It is subordinate to Document 1 for requirements: where this document and a Document 1 requirement disagree, the requirement wins and the conflict is recorded in section 1.6.

Where this document contradicts Document 3, the conflict is recorded in section 1.6 and raised as a change request in Appendix G. Document 3 is not edited by this document; a defect in a published baseline is corrected by a new version of that baseline, never by silent divergence.

---

## 1. Purpose, Scope & Architectural Drivers

### 1.1 Purpose

Document 3 established that SlateIQ is a shared-infrastructure, multi-tenant platform in which PostgreSQL row-level security is the last line of defence. It deliberately stopped at the boundary of policy: it named the permission key grammar, the scope dimension and the negative tests, then handed the catalogue, the grant workflow, the session design and the policy set to this document.

This document closes that boundary. It answers five questions that must be settled before a single endpoint can be written:

1. What exactly is a tenant, what states can it occupy, and what happens to its data at each state?
2. Who can hold an identity, how is that identity proven, and how is it removed?
3. What is the complete set of permissions, how are they bundled into roles, and how is scope resolved on every request?
4. How is tenant isolation enforced at each layer, and how is that enforcement proven rather than asserted?
5. How does an external auditor reach one client's data without ever reaching another's?

Every answer is expressed as a testable commitment. Where a commitment cannot be tested, it is not a control; it is a hope.

### 1.2 Scope

**In scope.**

| Area | Coverage |
|------|----------|
| Tenancy | Tenant definition, organization types, tenant states, provisioning, suspension, archival, export, purge, residency, entitlements, quotas |
| Identity | Human, external, service and device principals; identity lifecycle; invitations; credentials; MFA; recovery; federation roadmap |
| Sessions | Access and refresh tokens, rotation, binding, idle and absolute expiry, revocation, step-up, break-glass |
| Authorization | Permission catalogue, roles, assignments, seven scope types, effective-permission algorithm, segregation of duties, delegation, approval authority |
| Isolation | Request context, application filtering, RLS, cache, search, object storage, jobs, exports, telemetry, backups |
| Grants | Cross-organization access grant lifecycle, permission levels, acknowledgments, expiry, revocation, auditor workspace |
| Assurance | Threat model, negative test catalogue, access reviews, SOC 2 and SOX-supporting evidence |

**Out of scope.**

| Area | Owning document | Why it is excluded here |
|------|-----------------|-------------------------|
| Physical schema DDL, index tuning, migration mechanics | Document 5 | This document specifies the logical model and the policy; Document 5 writes the DDL |
| HTTP contract detail, OpenAPI schemas, error catalogues | Document 6 | This document fixes the endpoint surface and required permission per endpoint |
| Control procedures, evidence collection, audit testing | Document 7 | This document states the control commitments; Document 7 tests them |
| Audit assignment scope semantics and coverage computation | Document 8 or the audit domain document | This document defines the grant that bounds an assignment, not the assignment workflow |
| Offline sync protocol and conflict resolution | Document 9 | This document defines the session and device rules the sync client obeys |
| Billing, invoicing, revenue recognition | Not in product scope (D-019) | Manual entitlement provisioning at MVP |
| Customer-managed encryption keys | Open question OQ-05 | Referenced, not designed |

### 1.3 Architectural Drivers

| ID | Driver | Source | Consequence for this document |
|----|--------|--------|-------------------------------|
| **AD4-01** | A cross-tenant disclosure is the highest-severity failure the product can suffer | RSK-003, NFR-SEC-004, NFR-SEC-005 | Isolation is enforced at four independent layers and tested negatively on every build |
| **AD4-02** | External auditors serve several clients and must not accumulate standing access | D-003, FR-TNT-015 to FR-TNT-017 | One home organization per identity; access to a client exists only as a time-bound grant |
| **AD4-03** | Financial approvals must be attributable and free of self-approval | BR-ACC-004, BR-ACC-006, NFR-PRV-003 | Segregation of duties is an eligibility gate in the policy decision, not a UI convention |
| **AD4-04** | The pilot must operate with a small platform team | AD-01, AD-11 | In-process policy decision point, no external identity provider at MVP, no policy engine |
| **AD4-05** | Access must be removable within five minutes, including mid-session | BR-ACC-003, BR-ACC-007, TC-07 | Thin access tokens, per-request scope resolution, versioned cache, denylist |
| **AD4-06** | Data residency is a procurement condition in India and the EU | NFR-PRV-009, D-013, ADR-009 | Region is a provisioning attribute; tenant data never crosses a region boundary |
| **AD4-07** | The platform must survive a SOC 2 Type II examination within twelve months of GA | NFR-PRV-004 | Every control produces retrievable evidence without a manual reconstruction exercise |
| **AD4-08** | Offline capture is a first-class workflow, not a degraded mode | NFR-MOB-002, NFR-MOB-004 | Sessions and devices have an offline validity model with an explicit re-authentication bound |
| **AD4-09** | Auditors and customers share the same identity system without sharing a tenant | P-07, D-003 | Grants are the only cross-tenant primitive; there is no second tenant context |
| **AD4-10** | Tenant provisioning must be repeatable and reversible | FR-TNT-001 | Provisioning is an idempotent, compensated workflow with a recorded run |

### 1.4 Binding Inputs

This document treats the following as fixed. It does not re-litigate them.

| Input | Source | Binding statement |
|-------|--------|-------------------|
| Shared database with RLS | ADR-002 | One database per region; `org_id` on every tenant table; no `BYPASSRLS` for the application role |
| Built-in identity at MVP | ADR-008 | Local credentials and TOTP; OIDC, SAML and SCIM at Phase 1 |
| One region per tenant | ADR-009 | A tenant's database, objects, backups, logs and keys remain in its region |
| Permission grammar | Document 3 Appendix C.1 | `domain.resource.action`; scope is a separate dimension; no wildcards in stored assignments |
| RLS session variables | Document 3 Appendix C.5 | `app.org_id`, `app.user_id`, `app.grant_ids`, `app.grant_scope`, set with `SET LOCAL` |
| Revocation budget | BR-ACC-003, BR-ACC-007 | Five minutes for grants and role changes; immediate for deactivation and device wipe |
| Grant ceiling | BR-AUD-002 | Exactly one client organization per grant; expiry no more than twelve months after activation |
| Audit model | ADR-012 | In-transaction append-only audit with a per-tenant hash chain |

### 1.5 Relationship to the Archived Draft

The file `_archive/03-actor-permission-architecture.v1.0.0.md` contained a useful first pass: a 14-actor catalogue, a resource-action matrix, a thirteen-role set, a JWT sketch and an RLS example. It was written against an earlier draft of the specification and diverged from the architecture baseline in four ways that could not be merged forward:

1. It embedded scope inside the permission key (`resource:action:scope`), which Document 3 Appendix C.1 prohibits because it multiplies the catalogue and makes revocation ambiguous.
2. It assumed a user belongs to exactly one organization with a single `org_id`, which cannot express an external auditor serving several clients under D-003.
3. It used a static permissions array in the access token, which cannot honour a five-minute revocation budget.
4. It introduced a `SYS_PLATFORM_AUDITOR` role without a home organization, which makes the auditor's own employer invisible to access review.

The draft's actor list, role intent and scope vocabulary survive in sections 5 and 8 and in Appendices A and B in corrected form. The draft itself is retired and must not be used as an implementation reference.

### 1.6 Reconciliation Notes

Six conflicts exist across the baselines. Each is resolved here explicitly, with the affected baseline named. None is resolved by silently picking one side.

| ID | Conflict | Sources | Resolution adopted here | Baseline action |
|----|----------|---------|--------------------------|-----------------|
| **RN-01** | Session idle timeout is stated as 30 minutes in one place and 8 hours in another | FR-TNT-022 says idle timeout defaults to 30 minutes and is configurable; Document 3 Appendix C.4 says 8 hours web | 30-minute default, organization-configurable in the range 15 minutes to 8 hours, with 8 hours as the ceiling and never the default. The mobile offline window is a separate concept at 7 days (NFR-MOB-004) | D4-01 corrects the C.4 wording |
| **RN-02** | Password composition is required by one baseline and forbidden by another | FR-TNT-008 says minimum 12 characters with complexity; Document 3 section 13.3 says no forced composition rules | Minimum 12 characters, breach-list rejection, identity-token rejection, and either three of four character classes or a 16-character passphrase. No forced periodic rotation. This satisfies FR-TNT-008 while following current guidance | D4-02 corrects the section 13.3 wording |
| **RN-03** | Refresh token lifetime is 14 days in one table and 30 days in another | Document 3 section 7.2 says 14 days; Appendix C.4 says 30 days | 14-day rotating refresh token, 30-day absolute session lifetime, re-authentication required at the absolute bound. Both numbers are preserved with distinct meanings | D4-03 corrects the C.4 row |
| **RN-04** | The domain model gives `User` a mandatory `org_id`, which prevents one identity serving several clients | Document 2 `User` entity; D-003 and D2-05 | Global `identity.user` with `home_org_id`, plus `tenancy.membership`, plus grants. Document 2 is not edited; D2-05 remains the change request that closes the gap | D2-05 already recorded in Document 1 Appendix E |
| **RN-05** | Region assignment is a Should requirement at Phase 1 but a structural property at MVP | FR-TNT-023; ADR-009 and NFR-PRV-009 | Region is assigned at provisioning and enforced by placement from the first tenant. The customer-facing region selector, region change request and migration tooling remain Phase 1 | D4-04 aligns the FR phase marker |
| **RN-06** | The scope list omits the assignment scope that the grant model depends on | Document 3 Appendix C.2 lists six scope types; FR-TNT-013 requires assignment scope; the archived draft used `ASSIGNMENT` | Seven scope types, with `ASSIGNMENT` added and defined as the scope of an audit assignment bounded by a grant | D4-05 adds the row to C.2 |
| **RN-07** | Permission key grammar differs between the draft and the baseline | Archived draft `resource:action:scope`; Document 3 Appendix C.1 `domain.resource.action` | Document 3 grammar wins. The draft's keys are not carried forward | None; the draft is retired |
| **RN-08** | Actor identifiers differ between the draft and the requirements | Archived draft ACT-01 to ACT-14; Document 1 P-01 to P-13 | Document 1 personas are canonical. Roles map to personas; the platform super-administrator persona is P-13 Platform Operations, and there is no separate super-user identity | None; the draft is retired |

**Rule.** A reconciliation note is a decision, not a comment. Code reviews cite the note. A later change to any resolution requires a superseding ADR, not an edit to this table.

---

## 2. Tenancy Model

### 2.1 Vocabulary

| Term | Definition | Identifier |
|------|------------|------------|
| **Tenant** | The isolation boundary. Exactly one organization owns a tenant. All tenant data carries `org_id`. | `org_id` |
| **Organization** | The legal or operational entity that contracts with SlateIQ or receives access. A tenant is the technical shadow of an organization. | `tenancy.organization` |
| **Home organization** | The single organization that owns a human identity, employs the person or holds the engagement contract, and is responsible for that identity's lifecycle | `identity.user.home_org_id` |
| **Client organization** | The organization that owns the data being protected and that issues a grant | `tenancy.access_grant.client_org_id` |
| **Grantee** | The external principal that receives scoped, time-bound access through a grant | `tenancy.access_grant.grantee_user_id` |
| **Membership** | The relationship that makes a user a member of an organization with at least one role assignment | `tenancy.membership` |
| **Role assignment** | The binding of a role, a scope type and specific scope identifiers to a membership | `tenancy.assignment` |
| **Grant** | The only mechanism by which a principal reaches data outside its home organization | `tenancy.access_grant` |
| **Region** | The geographic placement of a tenant's data, keys, backups and logs | `ops.tenant_region` |
| **Principal** | Any authenticated actor: human member, external grantee, service account or registered device | `principal_type` |
| **Effective scope** | The computed set of resources a principal may reach in the current request, after intersecting assignment scope and grant scope | Derived per request |

**Naming rule.** "Tenant" and "organization" are used interchangeably in prose, but never in code. Code uses `org_id` and `organization`. A new synonym is a defect.

### 2.2 Tenant Topology

Four isolation topologies were evaluated against the pilot's constraints: a small platform team, a per-tenant data-residency obligation, a five-minute revocation budget, and a customer base that includes both production houses and audit firms.

| Option | Isolation strength | Migration cost per change | Operational cost at 500 tenants | Residency fit | Verdict |
|--------|--------------------|---------------------------|--------------------------------|---------------|---------|
| Database per tenant | Strongest; physical separation | One migration per database; 500 migration targets | High; connection fan-out, backup sprawl, per-database tuning | Good | Rejected as the default (ADR-002) |
| Schema per tenant | Strong logical separation | One migration per schema; search path per request | High; catalogue bloat, pooled connection reset risk | Good | Rejected (ADR-002) |
| Shared database, shared schema, RLS | Strong if policies are complete and tested | One migration | Low | Requires one database per region | **Selected** |
| Shared application, dedicated stack on request | Strongest for the tenant that pays for it | Two deployment paths to test | Low at MVP because no tenant has requested it | Good | **Offered as an enterprise option**, not built at MVP |

**Decision.** A tenant is a row, not a database. `tenancy.organization` is the tenant root; every tenant-owned table carries `org_id uuid not null`, indexed as the leading column of every composite index and present in every unique constraint that could collide across tenants. Isolation is enforced by RLS (section 7), not by deployment topology.

**Why this holds.** Database-per-tenant is often defended as "impossible to leak across tenants". It is not: the leak moves to the connection resolver, and a resolver bug is harder to test than a policy. RLS with a negative test on every tenant table is a smaller, more testable surface. The residual risk of shared infrastructure is accepted and mitigated by the isolation suite, per-tenant quotas and blast-radius controls.

**Dedicated option.** An enterprise tenant may be placed on a dedicated database and, optionally, a dedicated host pool. The application must not branch on this: the same code path runs, with a connection string resolved from `ops.tenant_region`. Dedicated placement is an infrastructure property, never an authorization property, and it must pass the same isolation suite.

### 2.3 Tenant Identity

| Attribute | Rule |
|-----------|------|
| `org_id` | UUIDv7, generated at provisioning, immutable, never reused, never exposed as a sequence |
| `slug` | Lowercase, 3 to 40 characters, `[a-z0-9-]`, globally unique across regions, immutable after activation |
| `legal_name` | Customer's registered name, up to 200 characters |
| `display_name` | Product-facing name, editable by the Organization Administrator |
| `organization_type` | Closed enumeration, see section 2.4 |
| `status` | Closed state machine, see section 2.5 |
| `default_locale` | BCP 47 tag; drives templates, date and number formatting |
| `default_timezone` | IANA zone; drives digest time, period boundaries and approval timestamps in the UI |
| `base_currency` | ISO 4217; immutable after the first approved financial transaction (BR-FIN-004) |
| `fiscal_calendar` | Twelve monthly periods by default; a custom calendar is a Phase 1 configuration |
| `data_region` | `IN` or `EU`; immutable in MVP, change requires a migration project (section 2.8) |
| `created_at`, `activated_at` | `timestamptz` in UTC |

**Slug namespace.** The slug is the only human-facing tenant identifier and appears in support conversations and sign-in hints. Two organizations must never share one, even across regions, because support staff reason about customers by slug. Uniqueness is enforced by a global unique index in the directory table, not by a per-region index.

**Identifier exposure.** `org_id` appears in URLs and API responses. It is not a secret and must not be treated as one. Authorization never derives from the presence of an identifier; it derives from the authenticated session and the resolved scope. A caller that supplies a valid `org_id` it has no relationship with receives `403` and a security event.

### 2.4 Organization Types

| Type | Purpose | Holds data | Can issue grants | Can receive grants | Notes |
|------|---------|------------|------------------|--------------------|-------|
| `OTT_PLATFORM` | Streaming platform commissioning productions | Yes | Yes | No | Personas P-01, P-04, P-05, P-11 |
| `BROADCASTER` | Television network commissioning productions | Yes | Yes | No | Personas P-02, P-04, P-05 |
| `PRODUCTION_HOUSE` | Production company delivering a commission | Yes | Yes | No | Personas P-03, P-06, P-09, P-10 |
| `AUDIT_FIRM` | Independent audit or assurance firm | Minimal | No | Yes | Persona P-07; closes D2-12 |
| `PLATFORM_INTERNAL` | SlateIQ operations | No tenant financial data | No | No | Persona P-13; exists for service ownership and break-glass attribution |

**Why audit firms are organizations.** An external auditor must have a home organization for three reasons: access review needs to ask "which firm does this person work for", deactivation must be executable by the firm when an auditor leaves, and the platform must be able to show that an auditor's employer is independent of the client. Making the auditor a guest row inside each client tenant would give the same person several identities, fragment the audit trail and make revocation dependent on every client acting.

**Lightweight provisioning.** An audit firm that is not a paying customer receives a tenant with no subscription, a seat limit of the number of named auditors, no financial modules enabled and no ability to create client data. It exists to hold identities, not to run productions. This keeps one identity per person without inventing a second identity store.

**Type immutability.** `organization_type` cannot change after activation. A production house that becomes a broadcaster requires a new tenant and a data migration; the alternative is a type change that silently alters grant and entitlement semantics.

### 2.5 Tenant State Machine

| State | Meaning | Members can authenticate | Data readable by members | Jobs running | Allowed next states |
|-------|---------|--------------------------|--------------------------|--------------|---------------------|
| `PROVISIONING` | Record created; seed work in progress | No | No | Provisioning only | `ACTIVE`, `PROVISIONING_FAILED` |
| `PROVISIONING_FAILED` | Seed work failed; compensation pending | No | No | Compensation only | `PROVISIONING`, `PURGING` |
| `ACTIVE` | Normal operation | Yes | Yes | Yes | `SUSPENDED`, `ARCHIVED`, `PENDING_DELETION` |
| `SUSPENDED` | Temporarily blocked by SlateIQ or by customer request | No | No | Paused except security and retention jobs | `ACTIVE`, `ARCHIVED` |
| `ARCHIVED` | Contract ended; data retained and read-only | No | No; export only through Platform Operations | Retention and verification only | `ACTIVE`, `PENDING_DELETION` |
| `PENDING_DELETION` | Deletion approved; grace period before purge | No | No | Retention and export only | `PURGING`, `ARCHIVED` |
| `PURGING` | Destructive workflow in progress | No | No | Purge only | `PURGED` |
| `PURGED` | Terminal; cryptographic erasure complete | No | No | None | None |

**Transition rules.**

| Transition | Trigger | Approver | Effects |
|------------|---------|----------|---------|
| `PROVISIONING` to `ACTIVE` | Provisioning run completes and the isolation probe passes | System | Emit `OrganizationActivated`; first admin invitation becomes usable |
| `ACTIVE` to `SUSPENDED` | Non-payment, security incident, abuse, legal order or customer request | Platform Operations with reason code | Revoke all sessions; block API with `TENANT_SUSPENDED`; pause dispatch except security and retention; notify all Organization Administrators |
| `SUSPENDED` to `ACTIVE` | Cause resolved or customer request | Platform Operations | Sessions are not restored; users sign in again; emit `OrganizationReactivated` |
| `ACTIVE` to `ARCHIVED` | Contract end or customer request | Platform Operations plus customer acknowledgement | Revoke sessions; disable writes; retain data per retention schedule |
| `ARCHIVED` to `ACTIVE` | Contract renewed within retention | Platform Operations | Restore entitlements; users sign in again |
| `ARCHIVED` to `PENDING_DELETION` | Retention expired or customer requests deletion | Two Platform Operations approvers | Start grace clock; emit `OrganizationDeletionScheduled` |
| `PENDING_DELETION` to `PURGING` | Grace period elapsed and no legal hold | Two approvers, one Security | Begin destructive workflow |
| `PENDING_DELETION` to `ARCHIVED` | Deletion cancelled within grace | Platform Operations | Emit `OrganizationDeletionCancelled` |
| `PURGING` to `PURGED` | Keys destroyed, rows deleted, objects deleted, backups expired | System, verified | Emit `OrganizationPurged`; retain a tombstone record only |

**Invariants.**

1. A tenant in any state other than `ACTIVE` cannot authenticate a member. The check is at authentication, not at each endpoint, so a suspended tenant cannot be reached by a token minted before suspension.
2. Suspension never deletes data and never shortens the retention clock.
3. `PURGED` is terminal. There is no restore from purge, and the platform must be able to prove that.
4. A legal hold blocks `PENDING_DELETION` to `PURGING` indefinitely. The hold check is a hard gate, not a warning.
5. State changes are audited with actor, reason code, prior state, new state and correlation ID.

### 2.6 Tenant Configuration

Configuration is split by who may change it and how quickly it must take effect.

| Configuration | Owner | Change mechanism | Effect latency | Guardrail |
|---------------|-------|------------------|----------------|-----------|
| Display name, branding, locale, timezone | Organization Administrator | Settings API | Immediate | Timezone change does not rewrite stored timestamps |
| Base currency | Organization Administrator | Settings API | Immediate | Blocked after the first approved financial transaction (BR-FIN-004) |
| Fiscal calendar | Organization Administrator | Settings API | Next period | Cannot overlap or skip periods |
| Security policy: idle timeout, MFA enforcement, session concurrency | Organization Administrator | Settings API | New sessions immediately; existing sessions at next refresh | Bounded by platform minimums; cannot weaken MFA for privileged roles |
| Retention defaults | Organization Administrator with Security acknowledgement | Settings API | Next retention evaluation | Cannot fall below the regulatory minimum |
| Approval authority limits | Finance Head | Settings API | New approvals only | Cannot exceed the platform ceiling |
| Entitlements, plan, seat limits | Platform Operations | Internal API | Immediate | Cannot reduce below current usage without a warning and a grace window |
| Data region | Platform Operations | Provisioning only | Immutable in MVP | Requires a migration project |
| Feature flags | Platform Operations | Flag service | 60 seconds | Per-tenant override is audited |

**Rule.** A tenant-configurable setting may tighten a platform control and may relax it only within a documented envelope. There is no configuration that disables RLS, disables audit, disables MFA for an individual privileged user, or permits cross-tenant reads.

### 2.7 Entitlements and Plan Limits

Document 1 defers subscription enforcement to Phase 1 (FR-TNT-019, D-019) while Document 2's D2-09 asks for the entities to exist. Both are satisfied by modelling entitlements now and enforcing them softly at MVP.

| Entity | Purpose | MVP behaviour |
|--------|---------|---------------|
| `tenancy.plan` | Named commercial package | Seeded with `PILOT`, `STANDARD`, `ENTERPRISE`, `AUDIT_FIRM_INTERNAL` |
| `tenancy.entitlement` | Feature and limit set attached to a plan | Seeded from a versioned manifest |
| `tenancy.organization_entitlement` | Per-tenant override | Manual at MVP; audited; expires |
| `tenancy.usage_counter` | Rolling counters per tenant and metric | Written on the event that consumes the metric; nightly reconciliation |

**Entitlement keys.** `seats.max`, `storage.gb`, `imports.month`, `exports.month`, `reports.concurrent`, `retention.years`, `modules.<name>`, `regions.allowed`, `sso.enabled`, `api.enabled`.

**Enforcement posture at MVP.**

| Dimension | MVP | Phase 1 |
|-----------|-----|---------|
| Seats | Soft limit; Platform Operations is alerted at 90 percent and 100 percent; invitation above the limit requires an override | Hard limit with an upgrade path in-product |
| Storage | Soft limit; upload above the limit returns `409 ENTITLEMENT_EXCEEDED` once the hard ceiling is reached | Metered and billed |
| Imports and exports per month | Counted, not blocked | Blocked at the limit |
| Modules | Enabled by flag; a disabled module returns `403 MODULE_NOT_ENABLED` | Same |
| SSO | Disabled at MVP because federation is Phase 1 | Enabled per entitlement |

**Why soft at MVP.** A pilot that stops inviting a user because a counter is wrong damages trust faster than an unbounded seat. The counters exist so that the Phase 1 hard limit is a configuration change, not a re-architecture.

**Counter integrity.** Usage counters are derived from immutable events (`UserInvited`, `DocumentUploaded`, `ImportCommitted`, `ExportCompleted`) and reconciled nightly. A counter is a cache of the event log, never the source of truth.

### 2.8 Data Residency

Tenant data is placed in exactly one region for its entire life. Two regions are supported at launch: `IN` (Mumbai) and `EU` (Frankfurt). The logical region code is what the application sees; the physical region mapping lives in infrastructure configuration.

**What "in region" covers.**

| Data class | Placement | Cross-region allowed |
|------------|-----------|----------------------|
| PostgreSQL primary and replicas | Tenant's region | No |
| Object storage buckets and replicas | Tenant's region | No |
| Backups and point-in-time recovery archives | Tenant's region | No |
| KMS keys and key material | Tenant's region | No |
| Application logs and traces containing tenant identifiers | Tenant's region | No; only redacted, aggregated metrics leave |
| Queue and outbox payloads | Tenant's region | No |
| Search index | Tenant's region | No |
| Support exports and diagnostic bundles | Tenant's region until downloaded by an authorised operator | No automatic transfer |
| Billing and contract records | Platform region | Yes; contains no tenant financial or personal data beyond the contracting entity |

**Enforcement.** Placement is not a policy that code can override. A tenant's connection string, bucket and key alias are resolved from `ops.tenant_region`, and the network path for a tenant's region admits only that region's application tier. A cross-region copy requires an infrastructure change, which requires an ADR.

**Region selection.** Region is chosen at contracting and set during provisioning. The product does not offer a region selector in the pilot. FR-TNT-023's customer-facing selection, region change request and migration workflow remain Phase 1 (RN-05).

**Region change.** There is no in-place region change. A change is a new tenant, an export, an import and a cutover, executed as a professional-services project with a documented chain of custody. This is deliberately expensive: the alternative is a silent copy path that breaks the residency promise.

**Backups.** Regional backups are encrypted with the tenant's regional key. A restore into another region is prohibited by policy and by key availability: the destination region cannot decrypt the backup. This is the technical control behind the policy.

### 2.9 Tenant Lifecycle

#### 2.9.1 Provisioning

Provisioning is an idempotent workflow owned by MOD-15 Platform Operations and recorded in `ops.provisioning_run`. Each step is independently retryable and has a compensating action.

| Step | Action | Failure compensation | Target |
|------|--------|----------------------|--------|
| 1 | Validate request: slug available, region valid, type valid, plan exists | None needed | 200 ms |
| 2 | Insert `tenancy.organization` in `PROVISIONING`; insert `ops.tenant_region` | Mark `PROVISIONING_FAILED` | 100 ms |
| 3 | Attach entitlements from plan manifest; apply overrides | Detach entitlements | 100 ms |
| 4 | Seed the system role set from the current role template version | Delete seeded roles | 200 ms |
| 5 | Seed reference data: departments, expense categories, currencies, retention policy, notification defaults | Delete seeded reference rows | 300 ms |
| 6 | Create object storage prefix and bucket policy for the tenant | Delete prefix | 5 s |
| 7 | Create or alias the tenant data key in the regional KMS | Schedule key deletion | 2 s |
| 8 | Warm reference caches; register the tenant in the directory | Evict caches | 1 s |
| 9 | Run the isolation probe: insert a probe row, read it as the tenant, read it as a foreign tenant and assert zero rows, delete it | Retry once, then fail | 2 s |
| 10 | Transition to `ACTIVE`; create the first administrator invitation; emit `OrganizationActivated` | None; the tenant stays `PROVISIONING_FAILED` | 100 ms |

**Targets.** p50 under 60 seconds, p95 under 5 minutes, no step longer than 30 seconds. The probe is mandatory: a tenant is never activated on the assumption that RLS works.

**Idempotency.** `provisioning_run_id` is supplied by the caller or derived from `(slug, region, requested_at)`. Re-running a completed run returns the existing tenant. Re-running a partially completed run resumes from the last committed step; it never duplicates roles, reference data or grants.

**First administrator.** The first administrator is a named individual, not a shared mailbox, and not the Platform Operations operator who ran provisioning. The invitation is valid for 72 hours, requires email verification, password creation and TOTP enrollment, and is audited as `OrganizationAdminBootstrapped`.

**No default password.** There is no seeded credential and no "welcome1" path. A tenant with no accepted administrator invitation is reachable only by Platform Operations through break-glass (section 4.9).

#### 2.9.2 Suspension and Reactivation

Suspension is a security and commercial control, not a deletion. It must be fast, reversible and loud.

| Aspect | Behaviour |
|--------|-----------|
| Reason codes | `NON_PAYMENT`, `SECURITY_INCIDENT`, `CUSTOMER_REQUEST`, `LEGAL_ORDER`, `ABUSE`, `PLATFORM_MIGRATION` |
| Actor | Platform Operations with a second approver for `SECURITY_INCIDENT` and `LEGAL_ORDER` |
| Speed | Access blocked within 60 seconds of the transition commit |
| Sessions | All sessions revoked; refresh tokens invalidated; device registrations retained but marked suspended |
| In-flight writes | Requests already in a transaction complete or roll back; the next request is rejected |
| Notifications | Security and legal notices still send; business notifications are suppressed |
| Jobs | All tenant jobs pause except retention, audit verification and security scanning |
| Data | Retained in full; retention clock continues |
| Customer visibility | All Organization Administrators receive an email with the reason category and the support path |
| Reactivation | Restores access within one business day; sessions are not restored; counters resume; a reactivation note is audited |

**Degradation rule.** A suspended tenant must fail closed and legibly: `403` with `code: TENANT_SUSPENDED`, a support reference and no data. It must not fail as a timeout or a generic error, because operators then debug the wrong subsystem.

#### 2.9.3 Archival, Export and Purge

| Phase | What happens | Evidence produced |
|-------|--------------|-------------------|
| Archive | Writes disabled, sessions revoked, data frozen; retention policy continues to apply | `OrganizationArchived` event; archive certificate |
| Export | Customer receives a complete export: relational data as CSV or JSON, evidence objects with checksums, audit trail as JSONL, manifest with row counts and hashes | Export manifest signed by the platform; delivery record |
| Deletion request | Customer or Platform Operations requests deletion; legal hold checked; DSR obligations checked | `OrganizationDeletionScheduled`; approval record |
| Grace period | 30 days, cancellable, during which the tenant is read-only and export remains available | Grace clock in `tenancy.organization` |
| Purge | Destroy the tenant data key, delete rows in dependency order, delete objects and versions, expire backups, invalidate caches | Purge run log; key destruction receipt; certificate of destruction |
| Verification | A post-purge probe confirms zero rows and zero objects for the `org_id`; backup expiry confirmed | Verification report retained for seven years |

**Purge order.** Object storage versions and delete markers, then child tables, then parent tables, then the organization row, then the key, then caches. The key is destroyed last so that any residue is unreadable even if a deletion is incomplete, and first in the audit narrative because it is the control that makes the rest irreversible.

**Legal hold.** A hold is a row in `compliance.legal_hold` with a matter reference, custodian, scope and release authority. Hold blocks purge, blocks retention expiry and blocks DSR erasure for the held records. Release requires the authority named on the hold, not merely an administrator.

**Tombstone.** After purge, a tombstone row retains `org_id`, slug, purge date, approvers and certificate hash. It contains no personal data and exists so that the platform can answer "was this tenant deleted, and by whom" without retaining the tenant.
