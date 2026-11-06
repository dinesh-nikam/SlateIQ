# Document 3: Actor, Permission, Tenancy & Identity Architecture

**Product:** SlateIQ — Production Cost Intelligence & Audit Monitoring Platform
**Document ID:** SLQ-ARCH-003
**Version:** 1.0.0 (Tenancy & Identity Baseline)
**Supersedes:** `_archive/03-actor-permission-architecture.v1.0.0.md`, the earlier Document 3 draft. Drafted as "Document 4" under the superseded numbering of Document 1 Appendix D; the re-baseline (D-023) is recorded in `00-foundation-index.md` section 2.
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
| 1.0.0 (publication) | 2026-11-06 | Architecture Team | Assembled and published as Document 3 under the re-baselined set (D-023): references renumbered, `COST_CONTROLLER` added, commissioner-hosted note (ADR-025), legacy permission keys normalised, section 17 and Appendices A, D, E and G completed. See Appendix G.2 |

### Related Documents

| Document | Relationship |
|----------|--------------|
| Document 1 — Product Vision & Business Requirements | Source of FR-TNT-001 to FR-TNT-024, the security and privacy NFRs, decisions D-003, D-013, D-018, D-019 and D-021, and personas P-01 to P-13 |
| Document 2 — Domain Model & Business Capability Map | Source of CAP-001 to CAP-003; version 2.0.0 applies D2-05 and owns ADR-025 (show hosting topology) |
| Document 4 — Core Business Workflows & State Machines | Consumes the SoD rules, approval limits and grant lifecycle |
| Document 5 — Business Rules & Invariants Catalogue | Consumes the BR-ACC and BR-AUD enforcement points |
| Document 6 — High-Level Solution Architecture | Binding inputs: sections 7.1 to 7.3, 13.3, 13.4, ADR-002, ADR-008, ADR-009, Appendix C and its C.8 handoff |
| Document 7 — Module & Dependency Architecture | Consumes the policy decision point contract and permission-driven UI rules |
| Document 8 — Database Conceptual Model & ERD | Consumes the schema, keys, indexes and RLS policy inventory (Appendix D) |
| Document 9 — API Architecture & Endpoint Catalogue | Consumes the API surface and event contracts in sections 11 and 12 |
| Document 10 — Security Architecture | Consumes the control commitments, threat model and test catalogue |
| Document 11 — Mobile & Offline Architecture | Consumes session, device, offline-grant and deactivation behaviour |
| Document 14 — Audit & Observability Architecture | Consumes security events and isolation telemetry |
| Document 15 — Infrastructure & Deployment Architecture | Consumes tenant provisioning, regional placement and break-glass tooling |
| Document 16 — Testing Strategy | Consumes the isolation suite and authorization matrix |
| Document 17 — Risk Register & ADRs | Holds ADR-016 to ADR-024 in full |
| Document 18 — Phased Implementation Roadmap | Consumes the build sequence implied by sections 10 and 17 |

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
| Document owner for Documents 4 to 18 | 9, 11, 12, Appendix G |

### Conventions

| Convention | Meaning |
|------------|---------|
| MUST, MUST NOT, SHOULD, MAY | Used with their RFC 2119 meanings. A MUST deviation requires an ADR. |
| `P-xx` | Persona identifier from Document 1 section 5.1 |
| `FR-TNT-xxx`, `NFR-xxx-xxx`, `BR-xxx-xxx`, `D-xxx` | Canonical identifiers from Document 1. Never renumbered here. |
| `CAP-xxx` | Capability identifier from Document 2 |
| `ADR-xxx` | Programme-wide decision record. This document owns ADR-016 to ADR-024; Document 6 owns ADR-001 to ADR-015. |
| `T4-xx` | Threat identifier local to this document. Document 6 uses `T-xx`; the two sets do not overlap. |
| `NT-xx` | Negative isolation test, continuing the catalogue started in Document 6 Appendix C.7 |
| `D4-xx` | Change request against Document 6 arising from this document |
| Permission key | `domain.resource.action`, for example `audit.access_grant.approve` |
| Scope token | `ORG`, `SHOW`, `SEASON`, `EPISODE`, `DEPARTMENT`, `ASSIGNMENT`, `SELF` |

### Authority and Conflict Resolution

This document is authoritative for the tenancy model, the identity model, the permission catalogue, the authorization algorithm, the RLS policy set, session behaviour and the cross-organization grant lifecycle. It is subordinate to Document 1 for requirements: where this document and a Document 1 requirement disagree, the requirement wins and the conflict is recorded in section 1.6.

Where this document contradicts Document 6, the conflict is recorded in section 1.6 and raised as a change request in Appendix G. Document 6 is not edited by this document; a defect in a published baseline is corrected by a new version of that baseline, never by silent divergence.

---

## 1. Purpose, Scope & Architectural Drivers

### 1.1 Purpose

Document 6 established that SlateIQ is a shared-infrastructure, multi-tenant platform in which PostgreSQL row-level security is the last line of defence. It deliberately stopped at the boundary of policy: it named the permission key grammar, the scope dimension and the negative tests, then handed the catalogue, the grant workflow, the session design and the policy set to this document.

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
| Physical schema DDL, index tuning, migration mechanics | Document 8 | This document specifies the logical model and the policy; Document 8 writes the DDL |
| HTTP contract detail, OpenAPI schemas, error catalogues | Document 9 | This document fixes the endpoint surface and required permission per endpoint |
| Control procedures, evidence collection, audit testing | Document 10 | This document states the control commitments; Document 10 tests them |
| Audit assignment scope semantics and coverage computation | Document 4 or the audit domain document | This document defines the grant that bounds an assignment, not the assignment workflow |
| Offline sync protocol and conflict resolution | Document 11 | This document defines the session and device rules the sync client obeys |
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
| Permission grammar | Document 6 Appendix C.1 | `domain.resource.action`; scope is a separate dimension; no wildcards in stored assignments |
| RLS session variables | Document 6 Appendix C.5 | `app.org_id`, `app.user_id`, `app.grant_ids`, `app.grant_scope`, set with `SET LOCAL` |
| Revocation budget | BR-ACC-003, BR-ACC-007 | Five minutes for grants and role changes; immediate for deactivation and device wipe |
| Grant ceiling | BR-AUD-002 | Exactly one client organization per grant; expiry no more than twelve months after activation |
| Audit model | ADR-012 | In-transaction append-only audit with a per-tenant hash chain |

### 1.5 Relationship to the Archived Draft

The file `_archive/03-actor-permission-architecture.v1.0.0.md` contained a useful first pass: a 14-actor catalogue, a resource-action matrix, a thirteen-role set, a JWT sketch and an RLS example. It was written against an earlier draft of the specification and diverged from the architecture baseline in four ways that could not be merged forward:

1. It embedded scope inside the permission key (`resource:action:scope`), which Document 6 Appendix C.1 prohibits because it multiplies the catalogue and makes revocation ambiguous.
2. It assumed a user belongs to exactly one organization with a single `org_id`, which cannot express an external auditor serving several clients under D-003.
3. It used a static permissions array in the access token, which cannot honour a five-minute revocation budget.
4. It introduced a `SYS_PLATFORM_AUDITOR` role without a home organization, which makes the auditor's own employer invisible to access review.

The draft's actor list, role intent and scope vocabulary survive in sections 5 and 8 and in Appendices A and B in corrected form. The draft itself is retired and must not be used as an implementation reference.

### 1.6 Reconciliation Notes

Six conflicts exist across the baselines. Each is resolved here explicitly, with the affected baseline named. None is resolved by silently picking one side.

| ID | Conflict | Sources | Resolution adopted here | Baseline action |
|----|----------|---------|--------------------------|-----------------|
| **RN-01** | Session idle timeout is stated as 30 minutes in one place and 8 hours in another | FR-TNT-022 says idle timeout defaults to 30 minutes and is configurable; Document 6 Appendix C.4 says 8 hours web | 30-minute default, organization-configurable in the range 15 minutes to 8 hours, with 8 hours as the ceiling and never the default. The mobile offline window is a separate concept at 7 days (NFR-MOB-004) | D4-01 corrects the C.4 wording |
| **RN-02** | Password composition is required by one baseline and forbidden by another | FR-TNT-008 says minimum 12 characters with complexity; Document 6 section 13.3 says no forced composition rules | Minimum 12 characters, breach-list rejection, identity-token rejection, and either three of four character classes or a 16-character passphrase. No forced periodic rotation. This satisfies FR-TNT-008 while following current guidance | D4-02 corrects the section 13.3 wording |
| **RN-03** | Refresh token lifetime is 14 days in one table and 30 days in another | Document 6 section 7.2 says 14 days; Appendix C.4 says 30 days | 14-day rotating refresh token, 30-day absolute session lifetime, re-authentication required at the absolute bound. Both numbers are preserved with distinct meanings | D4-03 corrects the C.4 row |
| **RN-04** | The domain model gives `User` a mandatory `org_id`, which prevents one identity serving several clients | Document 2 `User` entity; D-003 and D2-05 | Global `identity.user` with `home_org_id`, plus `tenancy.membership`, plus grants. Document 2 is not edited; D2-05 remains the change request that closes the gap | D2-05 already recorded in Document 1 Appendix E |
| **RN-05** | Region assignment is a Should requirement at Phase 1 but a structural property at MVP | FR-TNT-023; ADR-009 and NFR-PRV-009 | Region is assigned at provisioning and enforced by placement from the first tenant. The customer-facing region selector, region change request and migration tooling remain Phase 1 | D4-04 aligns the FR phase marker |
| **RN-06** | The scope list omits the assignment scope that the grant model depends on | Document 6 Appendix C.2 lists six scope types; FR-TNT-013 requires assignment scope; the archived draft used `ASSIGNMENT` | Seven scope types, with `ASSIGNMENT` added and defined as the scope of an audit assignment bounded by a grant | D4-05 adds the row to C.2 |
| **RN-07** | Permission key grammar differs between the draft and the baseline | Archived draft `resource:action:scope`; Document 6 Appendix C.1 `domain.resource.action` | Document 6 grammar wins. The draft's keys are not carried forward | None; the draft is retired |
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

---

## 3. Identity Model

### 3.1 Principal Classes

SlateIQ recognises two classes of principal that can hold authority, and one trust context that can constrain it. The distinction matters because each has a different lifecycle owner, a different revocation path and a different maximum lifetime.

| Class | `principal_type` | Authenticates by | Authority held as | Belongs to a tenant | Maximum continuous authority | Revocation path |
|-------|------------------|------------------|-------------------|---------------------|------------------------------|-----------------|
| Human member | `HUMAN` | Password plus TOTP, or federated assertion at Phase 1 | Role assignments inside the home organization | Yes, exactly one membership at MVP | 30-day absolute session, then re-authentication | Deactivation, session revoke, scope-version bump |
| External grantee | `HUMAN` | Password plus TOTP at the home organization | A grant issued by a client organization, never a role | Yes, exactly one membership in an audit firm | Grant end date; 30-day absolute session | Grant revoke, firm deactivation, session revoke |
| Service account | `SERVICE` | Client credentials, or workload identity for internal callers | Scoped API permission set | Yes, owned by one organization | 90-day credential rotation; no session | Secret rotation, account disable |
| Device context | none | Device key plus an existing human session | None; it narrows a human session | Inherits the user's tenant | Offline grant, 7 days maximum | Device revoke, wipe, session revoke |

**The device is not a principal.** A device holds no authority of its own and cannot act without a human or service principal behind it. Treating a device as a principal is how "remember this device" quietly becomes a permanent credential that nobody reviews. A device is a *context* recorded on a session, and it can only make a session narrower: it can require a key, restrict the offline window or force a wipe. It can never widen scope.

**Two classes, not three.** The archived draft distinguished internal staff, external auditors and platform super-administrators as separate species. That distinction is not architectural. A Platform Operations engineer is a human member of the `PLATFORM_INTERNAL` organization; an external auditor is a human member of an `AUDIT_FIRM` organization. What differs is the organization type, the roles available in it, and whether a grant is required to see another tenant's data. Modeling them as different principal types would duplicate the authentication path three times and produce three places for revocation to go wrong.

### 3.2 The Separation of Identity, Membership and Authority

Four relations are deliberately kept apart. Collapsing any two of them is the most common way a multi-tenant system develops a cross-tenant defect.

| Relation | Table | Answers the question | Owned by |
|----------|-------|----------------------|----------|
| Identity | `identity.user` | Who is this person, and which organization is accountable for them? | MOD-02 Identity |
| Membership | `tenancy.membership` | Which organization have they joined, and are they still active there? | MOD-01 Tenancy |
| Assignment | `tenancy.assignment` | What role do they hold, over which scope, inside that organization? | MOD-01 Tenancy |
| Grant | `tenancy.access_grant` | What may they see in an organization they have **not** joined? | MOD-01 Tenancy |

**Invariants.**

1. Every human has exactly one `home_org_id`. The home organization is accountable for the person's identity lifecycle, including deactivation and erasure.
2. At MVP, every active human has exactly one active membership, and it is in the home organization. Multi-organization membership is a Phase 1 capability (open question OQ-4-01), because it changes the meaning of an access review: no single administrator could certify a person's complete access.
3. A principal reaches a client organization through a grant **or** a membership, never both. An external auditor has no membership, no role and no directory entry in the client organization. A client cannot assign a role to an external auditor, and an auditor cannot be added to a show team.
4. `home_org_id` is immutable in normal operation. A change of employer is a controlled identity transfer: the releasing firm, the receiving firm and Platform Operations must all approve it, all sessions are revoked, and the transfer is audited. It is not a profile edit.
5. Every authorization decision names both the actor's home organization and the organization whose data is being touched. These are separate fields in the audit record (`actor_org_id`, `tenant_org_id`) even when they are the same value. A report that cannot distinguish them cannot investigate a cross-tenant incident.

**Why one membership at MVP.** A person who works for two production companies is a real case, but it is not a pilot case, and the workaround is worse than the limitation: either two identities for one human, or a membership model that no single administrator can review. The pilot states the limitation, models the second organization's access as a grant in Phase 1, and keeps access review answerable today. An organization that needs a contractor to work inside its tenant adds them as a named user with a role whose scope is one show; that is a membership, and it is reviewable.

### 3.3 Identity Attributes and Uniqueness

| Attribute | Rule | Personal data | Notes |
|-----------|------|---------------|-------|
| `user_id` | UUIDv7, immutable, never reused | No | Survives erasure as a pseudonym so audit records keep referential integrity |
| `home_org_id` | UUID, mandatory, immutable except by identity transfer | No | Drives accountability, access review and deactivation authority |
| `email` | Mandatory; normalised to lowercase for comparison; globally unique among non-erased identities | Yes | One human, one identity. Reuse of a freed address is blocked for 12 months |
| `email_verified_at` | Set only by possession proof | No | Invitation acceptance or verified email change |
| `display_name` | 1 to 120 characters; free text | Yes | Used in approvals and audit narratives |
| `phone` | Optional, E.164 | Yes | Notifications and account recovery contact only; never an authentication factor at MVP |
| `locale` | BCP 47, defaults to the home organization | No | Self-service (FR-TNT-005) |
| `timezone` | IANA zone, defaults to the home organization | No | Self-service |
| `status` | Closed enumeration, section 3.4 | No | |
| `created_at`, `activated_at`, `deprovisioned_at`, `erased_at` | `timestamptz` in UTC | No | |
| `last_login_at` | Updated at most once per minute | Yes, weakly | Shown to the user, not to other members |

**No self-registration.** There is no public sign-up route. An identity is created by invitation, by a grant acceptance at an audit firm, or by Platform Operations during provisioning. A sign-up form on a B2B financial system is an unauthenticated write path into a tenant directory.

**Email is the login identifier, not the person.** The platform never treats an email address as proof of identity beyond the moment it was verified. A change of email requires verification of both the old and the new address, revokes all refresh sessions, and notifies the previous address. If the old address is no longer reachable, the change becomes a support-assisted recovery (section 4.10) with the same evidence standard as an MFA reset.

**Data minimisation.** The identity record holds no date of birth, no postal address, no national identifier, no photograph and no free-text biography. Each of those would be a field that must be protected, exported, corrected and erased, in exchange for no functional benefit. NFR-PRV-005 is easier to satisfy by not collecting the field than by classifying it.

### 3.4 Identity Lifecycle

| State | Meaning | Can authenticate | Can be granted | Can hold a session | Exits to |
|-------|---------|------------------|----------------|--------------------|----------|
| `INVITED` | Invitation issued, not yet accepted | No | No | No | `ACTIVE`, `REVOKED` |
| `ACTIVE` | Normal | Yes, if the tenant is `ACTIVE` | Yes | Yes | `LOCKED`, `SUSPENDED`, `DEPROVISIONED` |
| `LOCKED` | Automatic lock after failed authentication | No | Yes, but unusable | No | `ACTIVE`, `DEPROVISIONED` |
| `SUSPENDED` | Administrative hold pending investigation | No | Suspended | No | `ACTIVE`, `DEPROVISIONED` |
| `DEPROVISIONED` | Removed from the organization; retained for attribution | No | No | No | `ERASURE_PENDING` |
| `ERASURE_PENDING` | Erasure approved; grace period running | No | No | No | `ERASED`, `DEPROVISIONED` |
| `ERASED` | Personal data destroyed; pseudonym retained | No | No | No | None |

| Transition | Trigger | Actor | Effects |
|------------|---------|-------|---------|
| `INVITED` to `ACTIVE` | Invitation accepted, password set, MFA enrolled where required | Invitee | Membership created; `UserInvited` closes; `UserActivated` emitted |
| `INVITED` to `REVOKED` | Expiry, resend, or administrator cancellation | System or inviter | Token invalidated; membership never created |
| `ACTIVE` to `LOCKED` | Five failed authentications within the lockout window | System | 15-minute lock (FR-TNT-007); notification to the user; no administrator action required to release |
| `LOCKED` to `ACTIVE` | Lockout expiry, successful password reset, or administrator unlock | System or Organization Administrator | Failure counter cleared; a password reset path also revokes sessions |
| `ACTIVE` to `SUSPENDED` | Security investigation, legal request or customer instruction | Organization Administrator with `identity.user.update` and step-up | Sessions revoked; grants suspended; approvals reassigned |
| `SUSPENDED` to `ACTIVE` | Investigation closed | Organization Administrator plus a second approver | Sessions are not restored; re-authentication required |
| `ACTIVE` to `DEPROVISIONED` | Employment ends, firm removes the auditor, or administrator deactivates (FR-TNT-006) | Organization Administrator with step-up | Sessions revoked immediately; grants suspended; open approvals reassigned; devices wiped; audit retained |
| `DEPROVISIONED` to `ERASURE_PENDING` | Erasure requested and no legal hold | Organization Administrator plus Platform Operations | 30-day grace; user notified where contact is lawful |
| `ERASURE_PENDING` to `ERASED` | Grace elapsed | System, verified | PII key destroyed; email released after 12 months; audit pseudonym retained |

**Lockout is not suspension.** `LOCKED` is an automatic, temporary, self-clearing response to failed authentication. `SUSPENDED` is a deliberate administrative act with a reason and an approver. Conflating them produces an incident where a help-desk unlock silently reverses a security suspension.

**Deprovisioning is immediate, erasure is not.** FR-TNT-006 requires access removal within five minutes. It does not require deletion, and deletion at the same moment would destroy the evidence needed to investigate why the person was removed. The identity is deprovisioned at once and erased on a separate, slower track that respects legal hold and audit retention.

**Erasure versus audit.** Audit records must survive for seven years (NFR-AUD-003, NFR-PRV-008), while GDPR and DPDP grant a right to erasure. The two are reconciled by splitting the identity into a pseudonymous record and a separately encrypted personal-data record:

| Store | Content | Erasure behaviour |
|-------|---------|-------------------|
| `identity.user` | `user_id`, `home_org_id`, status, timestamps, locale | Retained as a pseudonym; no direct identifier |
| `identity.user_pii` | Email, display name, phone, notification contacts | Encrypted with a per-user data key; key destroyed on erasure, rows purged |
| `identity.user_email_alias` | Historical addresses for lookup and reuse prevention | Hashed after erasure, retained to block address recycling |
| Audit records | `actor_user_id`, `actor_org_id`, action, time, IP | Retained; the pseudonym resolves to nothing once the PII key is destroyed |

This is cryptographic erasure of the personal data with retention of the accountability record. The data subject's access is removed, the auditor's evidence survives, and the platform can still answer "who approved this payment" with a stable, non-identifying reference.

### 3.5 Invitations

An invitation is a membership offer, not a credential delivery mechanism. It proves control of a mailbox at a point in time; it does not grant authority until it is accepted and, for privileged roles, until MFA is enrolled.

| Field | Rule |
|-------|------|
| `invitation_id` | UUIDv7 |
| `home_org_id` | The inviting organization; must equal the membership organization |
| `email` | Normalised; one open invitation per email per organization |
| `role_id` | Required; a system or custom role that exists and is active at acceptance time |
| `scope_type`, `scope_ids` | Required; cannot exceed the inviter's own scope |
| `token_hash` | SHA-256 of a 256-bit random token; the plaintext exists only in the delivered link |
| `issued_by`, `issued_at` | The inviting administrator |
| `expires_at` | `issued_at` plus 72 hours (FR-TNT-004) |
| `accepted_at`, `revoked_at`, `revoke_reason` | Lifecycle |
| `delivery_attempts` | Maximum five resends; each resend invalidates the previous token |

**Acceptance flow.**

1. The invitee opens the link. The server hashes the presented token and looks up an open, unexpired invitation.
2. If no identity exists for the email, the invitee sets a display name, password and locale. Email possession is proven by the token itself; no second verification email is sent, because a second message adds delay without adding proof.
3. If an identity already exists and is `ACTIVE`, the invitee must re-authenticate before accepting. This prevents an attacker who controls a mailbox from silently adding an existing user to another organization.
4. The invitee enrolls TOTP if the role is MFA-required (section 4.5).
5. The membership and assignment are created in one transaction; the invitation is marked accepted; `UserInvited` and `UserActivated` are emitted; the invitation token is destroyed.
6. The home organization's administrators are notified when an existing identity joins, and the new member's own organization is notified when one of its people is added elsewhere. Both notifications name the inviting organization and the role.

**Invitation cannot escalate.** An invitation can only create a membership in the inviting organization. It cannot move `home_org_id`, cannot create a grant, cannot grant `administer`, and cannot assign a scope the inviter does not hold. An invitation that names a role the inviter could not assign is rejected at creation, not at acceptance.

**Bulk invitation (Phase 1, FR-TNT-024).** A CSV upload is validated in full before any invitation is sent: maximum 1,000 rows, required columns, email syntax, role existence, scope resolution, duplicate detection within the file and against open invitations. The response reports per-row outcomes. Valid rows are invited in a single audited batch; invalid rows change nothing. Partial application on a malformed file is prohibited because it produces a half-provisioned team that nobody can reconcile.

### 3.6 External Auditor Identity

An external auditor is a human member of an audit firm tenant, with no membership in any client organization. The firm is a first-class tenant (section 2.4) for three reasons: the client must be able to see who employs the auditor, the firm must be able to remove the auditor without asking every client, and access review must be able to ask whether the auditor's employer is independent of the client.

| Record | Purpose | Key fields |
|--------|---------|------------|
| `tenancy.auditor_profile` | Professional standing and independence evidence | `user_id`, `firm_org_id`, `engagement_types`, `independence_declared_at`, `coi_declared_at`, `nda_version`, `firm_attested_by`, `status` |
| `tenancy.access_grant` | The only path to client data | See section 8 |
| `tenancy.grant_acknowledgment` | Evidence that the auditor accepted the terms | `grant_id`, `nda_version`, `coi_declaration_id`, `accepted_at`, `ip`, `user_agent_hash` |

**Onboarding.** A firm administrator invites the auditor, attests that the person is engaged by the firm, and records the professional body or registration where one exists. The auditor accepts, enrolls MFA, and accepts the platform NDA. The firm administrator's attestation is the control that makes the firm accountable; SlateIQ does not verify professional licences at MVP.

**Grant acceptance.** FR-TNT-016 requires MFA enrollment, NDA acknowledgment and a conflict-of-interest declaration before a grant activates. Each is a separate, versioned record:

| Gate | Evidence | Failure behaviour |
|------|----------|------------------|
| MFA enrolled | A confirmed TOTP factor | Grant stays `PENDING_ACCEPTANCE`; no data access |
| NDA acknowledged | NDA version, timestamp, IP, user agent hash | Grant stays `PENDING_ACCEPTANCE` |
| Conflict of interest declared | Declaration text version, answer, timestamp | A declared conflict blocks activation and routes to the client's Audit Manager and Organization Administrator for a documented decision |
| Identity attested by firm | `auditor_profile.firm_attested_by` | Grant cannot be created for an unattested auditor |

**Leaving the firm.** Firm-initiated deprovisioning revokes the auditor's sessions, suspends every active grant, notifies each client organization, and opens an access-review item. Suspension rather than deletion preserves the client's ability to see that access existed and when it stopped. The client may then revoke formally; the grant cannot be reactivated by the firm alone.

**Independence is declared, not inferred.** The platform records the declaration and enforces the client's decision. It does not attempt to compute independence from employment history, because the data required to do so would be a new collection of personal data with no consented purpose. The declaration is renewed annually and on every grant acceptance.

**Platform-appointed auditor (P-08).** The appointment is a client-side act: the appointing OTT platform or broadcaster creates the grant. The auditor still belongs to an audit firm tenant. An independent practitioner with no firm is onboarded into a SlateIQ-operated `AUDIT_FIRM` tenant with a single seat, so that there is always an employer to notify and an administrator to deprovision. The alternative, a home-less auditor, is precisely the `SYS_PLATFORM_AUDITOR` defect the archived draft introduced.

### 3.7 Service Identity

Service accounts exist for machine-to-machine access. They are not users with a flag, because a service account that shares the human table will eventually be given a human role, a password and a forgotten MFA exemption.

| Aspect | Internal workload | External machine client |
|--------|-------------------|-------------------------|
| Authentication | Workload identity from the platform's task role; no static secret | OAuth 2.0 client credentials with `client_id` and a 256-bit secret |
| Storage | No credential in the application database | `identity.service_account.secret_hash` (Argon2id); secret shown once |
| Audience | The specific internal service it calls | The public API only |
| Authority | Module-internal calls governed by the module contract, not by tenant permissions | Explicit permission set, read-only by default, bounded to one `org_id` |
| Rotation | Automatic with deployment | 90 days (NFR-SEC-006) or on demand; two active secrets during rotation |
| Expiry | Task lifetime | Mandatory `expires_at`, maximum 12 months |
| Owner | Platform Operations | A named human owner in the tenant, reviewed quarterly |
| Available | MVP | Phase 1, gated by the `api.enabled` entitlement |

**Hard limits.** A service account cannot hold a human role, cannot approve a financial transaction, cannot be a grantee, cannot hold `administer`, cannot sign in to the web application, and cannot be issued a refresh session. Its permission set is a subset of the read and submit actions in the catalogue; approval actions are excluded because an approval must have a human accountable for it.

**No shared secrets in configuration.** Internal service-to-service calls do not carry bearer tokens minted from a shared secret. They carry a short-lived signed token whose audience is the callee and whose subject is the calling workload. A leaked environment variable therefore yields nothing reusable outside the cluster.

### 3.8 Device Identity

| Attribute | Rule |
|-----------|------|
| `device_id` | UUIDv7, generated by the server at registration, immutable |
| `user_id` | Owner; a device is never shared between users |
| `platform` | `IOS`, `ANDROID`, `WEB` |
| `app_version`, `os_version` | Telemetry and minimum-version enforcement |
| `device_public_key` | Mobile only; used to sign refresh requests |
| `biometric_gate` | Whether a device passcode or biometric is required to unlock the offline store |
| `registered_at`, `last_seen_at` | Timestamps |
| `revoked_at`, `wipe_requested_at`, `wipe_acknowledged_at` | Revocation and wipe evidence |
| `trust_state` | `TRUSTED`, `STEP_UP_REQUIRED`, `REVOKED` |

**Limits.** Five active devices per user by default, organization-configurable between one and ten. A sixth registration requires revoking an existing device. Each new registration requires password plus MFA, and notifies the user by email and in-app.

**Registration.** The device generates a key pair in the platform keystore (iOS Secure Enclave or Android Keystore where available), sends the public key, and receives a `device_id`. The private key never leaves the device and is not exportable. Refresh requests from mobile are signed with that key; a copied refresh token presented from another device fails the signature check.

**Web devices are sessions, not registrations.** A browser cannot hold a non-exportable key without WebAuthn, which is Phase 1. The `device_id` on a web session is a server-issued correlation identifier stored in an HttpOnly cookie, not a hardware fingerprint. It supports "sign out this browser" and anomaly detection; it is not an authenticator and must never be described as one.

**Lost device.** The user revokes the device from their profile; an Organization Administrator can revoke it for them. Revocation ends the device's sessions immediately and queues a wipe command. On the next connectivity, the app destroys the SQLCipher key and the cached files, then acknowledges. If the device never connects again, the offline grant expires after seven days (NFR-MOB-004) and the remaining ciphertext is undecryptable without the keystore key. That residual window is stated in section 4.8 and accepted as a mobile-platform limitation.

### 3.9 Logical Identity Data Model

| Table | Key | Important columns and constraints | Owner |
|-------|-----|-----------------------------------|-------|
| `identity.user` | `user_id` | `home_org_id` FK, `email` unique where `erased_at is null`, `status`, `locale`, `timezone`, timestamps | MOD-02 |
| `identity.user_pii` | `user_id` | Encrypted `email`, `display_name`, `phone`; key reference; `key_destroyed_at` | MOD-02 |
| `identity.user_email_alias` | `alias_id` | `user_id`, `email_hash`, `released_at`; blocks address recycling for 12 months | MOD-02 |
| `identity.credential` | `credential_id` | `user_id` unique, `password_hash`, `algorithm`, `params_version`, `changed_at`, `must_change` | MOD-02 |
| `identity.mfa_factor` | `factor_id` | `user_id`, `factor_type`, `secret_ciphertext`, `confirmed_at`, `last_used_step`, `disabled_at` | MOD-02 |
| `identity.recovery_code` | `code_id` | `user_id`, `code_hash`, `used_at`; ten per generation | MOD-02 |
| `identity.refresh_session` | `session_id` | `user_id`, `org_id`, `device_id`, `family_id`, `token_hash`, `issued_at`, `expires_at`, `absolute_expires_at`, `rotated_at`, `revoked_at`, `revoke_reason`, `last_seen_at` | MOD-02 |
| `identity.device_registration` | `device_id` | `user_id`, `platform`, `public_key`, `trust_state`, `revoked_at`, wipe timestamps | MOD-02 |
| `identity.password_reset` | `reset_id` | `user_id`, `token_hash`, `expires_at` (30 minutes), `used_at`, `requested_ip` | MOD-02 |
| `identity.email_change` | `change_id` | `user_id`, `new_email_ciphertext`, `old_token_hash`, `new_token_hash`, `expires_at` | MOD-02 |
| `identity.service_account` | `service_account_id` | `org_id`, `name`, `secret_hash`, `permission_set`, `owner_user_id`, `expires_at`, `last_used_at` | MOD-02 |
| `identity.federated_identity` | `federation_id` | Phase 1: `user_id`, `idp_id`, `subject`, `tenant_claim` | MOD-02 |
| `tenancy.membership` | `membership_id` | `user_id`, `org_id`, `status`, `joined_at`, `left_at`; unique active pair | MOD-01 |
| `tenancy.invitation` | `invitation_id` | `org_id`, `email_hash`, `role_id`, `scope_type`, `scope_ids`, `token_hash`, `expires_at`, `accepted_at`, `revoked_at` | MOD-01 |
| `tenancy.auditor_profile` | `user_id` | `firm_org_id`, declarations, NDA version, attestation, status | MOD-01 |

**Referential rules.** `identity.user.home_org_id` and `tenancy.membership.org_id` must agree at MVP; the database enforces this with a composite foreign key or a deferred constraint trigger (Document 8 chooses the mechanism). A membership cannot exist for an erased identity. A session cannot exist for a deprovisioned identity; revocation is written before the status change commits.

### 3.10 Personal Data, Notice and Data Subject Rights

| Data | Purpose | Lawful basis | Retention | Erasure |
|------|---------|--------------|-----------|---------|
| Email, display name | Authentication, attribution, notifications | Contract | Life of identity plus 30 days | Crypto-shredded |
| Phone | Optional notifications | Consent | Life of identity | Crypto-shredded |
| IP address, user agent hash | Security investigation, fraud detection | Legitimate interest | 12 months in security logs, 7 years in audit | Pseudonymised with identity |
| Authentication events | SOC 2 evidence, incident response | Legal obligation | 7 years | Retained, pseudonymous |
| Device metadata | Session security, wipe | Contract | Life of device plus 90 days | Deleted with device |
| COI and NDA declarations | Audit independence evidence | Legal obligation | 7 years after grant end | Retained, pseudonymous |

**Notice.** The privacy notice is versioned. Acceptance is recorded at first login and re-requested when the version changes materially. A user who declines the new notice can export their data but cannot continue using the platform; the account is suspended, not silently continued.

**Access and portability.** A user can download a JSON bundle containing their profile, memberships, role assignments, active grants, device list and authentication history for 90 days. Secrets, recovery codes and token hashes are never included, because they are not the data subject's personal data in any useful sense and including them would create a new attack path.

**Rectification.** Display name, phone, locale and timezone are self-service (FR-TNT-005). Email change is verified on both sides. `home_org_id` and audit records are not rectifiable by the user; the first is an accountability fact, the second is evidence.

**Erasure.** A request is validated against legal hold, open investigations and retention obligations. If any applies, the request is partially fulfilled: direct marketing and optional fields are erased immediately, the retained records are documented, and the user is told what remains and why. The platform does not promise deletion it cannot deliver.

---

## 4. Authentication and Sessions

### 4.1 Method Matrix

| Method | MVP | Phase 1 | Used by | Notes |
|--------|-----|---------|---------|-------|
| Email and password | Yes | Yes | All human principals | Argon2id; policy in section 4.3 |
| TOTP | Yes | Yes | MFA-required roles and any user who opts in | RFC 6238; section 4.5 |
| Recovery codes | Yes | Yes | Users with TOTP | Ten single-use codes |
| WebAuthn / passkeys | No | Yes | Platform Operations first, then all users | Phishing-resistant; becomes the preferred factor |
| OIDC / SAML SSO | No | Yes | Enterprise tenants (FR-TNT-009) | Section 4.12 |
| SCIM provisioning | No | Yes | Enterprise tenants | Lifecycle automation |
| Client credentials | No | Yes | Machine clients | Section 3.7 |
| SMS OTP | No | No | None | SIM-swap risk; no roadmap commitment |
| Security questions | No | No | None | Knowledge-based authentication is not authentication |
| Magic links | No | No | None | Mailbox compromise becomes account compromise |

**MFA is mandatory, not encouraged, for:** Organization Administrator, Finance Head, Audit Manager, Platform Operations, every external auditor (NFR-SEC-003), and any custom role that contains an `administer` or `approve` action. The requirement is attached to the role, so a custom role cannot be built that quietly escapes it. Attempting to assign such a role to a user without a confirmed factor fails at assignment time, and the invitation flow forces enrollment before activation.

**MFA coverage measurement.** NFR-SEC-003 requires 100 percent coverage. The identity audit job runs daily: every active user holding an MFA-required role must have a confirmed factor, and every external auditor must have one. A single miss opens a P1 security finding, not a report line.

### 4.2 Credential Storage

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Algorithm | Argon2id | Memory-hard; resistant to GPU and ASIC attack |
| Memory | 64 MiB | Within the API host's per-request budget at pilot concurrency |
| Iterations | 3 | Tuned to approximately 250 ms on the target instance class |
| Parallelism | 4 | Matches the API vCPU allocation |
| Salt | 16 bytes, per credential, from a CSPRNG | Unique per hash |
| Output | 32 bytes | |
| Pepper | 32 bytes, versioned, stored in the managed secret store | Applied as HMAC-SHA-256 before hashing; rotates with rehash-on-login |
| Encoding | PHC string with algorithm and parameters | Allows per-credential parameter upgrades |
| Rehash on login | Yes, when parameters or pepper version change | Transparent upgrade without a reset campaign |

**Timing.** Verification runs in constant time with respect to the hash comparison. For an unknown email, the server still performs a dummy Argon2id verification against a fixed hash so that response time does not disclose account existence. The dummy hash uses the same parameters as a real one; a cheaper dummy is detectable.

**What is never stored or logged.** Plaintext passwords, password hashes in application logs, reset tokens, TOTP secrets in plaintext, recovery codes in plaintext, refresh tokens in plaintext. A log scrubber runs on the logging pipeline as a second defence, and a unit test asserts that the serialisers for these types emit `[REDACTED]`.

**Pepper rotation.** A new pepper version is introduced alongside the old one. On successful login, the credential is rehashed with the current version. Credentials not used within 180 days are rehashed by a forced reset at next login. The old pepper is destroyed only when no credential references it.

### 4.3 Password Policy

The policy resolves RN-02: FR-TNT-008 requires minimum length and complexity, while Document 6 section 13.3 prohibits forced composition rules. Both are satisfied by requiring length and a choice of character classes, rejecting breached and predictable values, and never expiring a password on a calendar.

| Rule | Value |
|------|-------|
| Minimum length | 12 characters |
| Maximum length | 128 characters (denial-of-service bound) |
| Composition | Either at least three of four classes (lowercase, uppercase, digit, symbol) **or** a passphrase of at least 16 characters |
| Unicode | Normalised to NFKC; all printable characters allowed; leading or trailing whitespace rejected rather than trimmed |
| Breach check | Pwned Passwords range API at set and change time; a match rejects the password |
| Predictability | Rejected: the email local part, display name tokens, organization slug, product name, `slateiq`, keyboard walks, more than three repeated characters, and sequences of four or more |
| Reuse | Last five password hashes rejected |
| Expiry | None. Forced change only on evidence of compromise, administrator reset, or pepper retirement |
| Reset token | 256-bit, hashed at rest, 30-minute expiry, single use (FR-TNT-008) |
| Reset effects | All refresh sessions revoked; scope version incremented; access tokens expire within 15 minutes; notification to the user |
| Administrator visibility | None. An administrator can trigger a reset; they can never see or set a password |

**Breach-check availability.** If the breach service is unreachable, the password is accepted, the event is flagged `breach_check_deferred`, and the check is retried asynchronously; a later match forces a reset at next login. Fail-closed was rejected because a third-party outage would block every password reset, including the reset an administrator needs during an incident. The compensating controls are the entropy rules, rate limiting and the deferred recheck.

**Why no forced rotation.** Mandatory 90-day rotation produces `Summer2026!` and its eleven successors. It is not a control; it is a ritual that weakens the credential and generates reset tickets. The platform instead forces a change on compromise evidence, administrator reset and pepper retirement, and it detects compromise through breach lists, reuse detection and anomaly signals.

**Rate limits.** Password verification is limited per account and per source address. Reset requests are limited to three per account per hour and ten per source address per hour, with a generic response that does not disclose whether the address exists. The reset endpoint is a favourite for account enumeration; its response body and timing are identical for known and unknown addresses.

### 4.4 Login and Authentication Flow

| Step | Behaviour | Security property |
|------|-----------|-------------------|
| 1 | Resolve tenant hint from the email domain or the last-used organization; never trust a client-supplied `org_id` | Prevents tenant selection from becoming an authorization input |
| 2 | Verify email and password with constant-time comparison and a dummy hash for unknown accounts | No account enumeration by timing |
| 3 | Apply progressive delay: 0.5 s, 1 s, 2 s, 4 s after successive failures | Raises the cost of online guessing without locking legitimate users out immediately |
| 4 | On the fifth failure within 15 minutes, lock the account for 15 minutes and return `429` with `Retry-After: 900` (FR-TNT-007) | Meets the acceptance criterion; the trade-off is disclosed below |
| 5 | If the credential is valid, check identity status and tenant status | A suspended tenant cannot authenticate even with a valid password |
| 6 | If a factor is enrolled, or the role requires one, issue an MFA challenge bound to a short-lived challenge token | The challenge token is single-use and bound to the session attempt |
| 7 | On success, create the refresh session and access token; record `last_login_at`, device, IP class and user agent hash | One session record is the authority for every later refresh |
| 8 | Emit `UserAuthenticated` with `amr`, `auth_time`, `device_id` and correlation ID | Audit and anomaly detection share one record |
| 9 | Redirect to the requested route only if it is an internal path; external return URLs are rejected | Prevents open-redirect credential theft |

**Lockout trade-off.** FR-TNT-007 mandates a 15-minute lock after five failures. That is also a denial-of-service vector: anyone who knows a victim's email can lock them out with five requests every fifteen minutes. The control is retained because it is a Must requirement, and it is mitigated by three measures: the lock is per account and does not reveal whether the password was correct, the user can clear it through a verified password reset, and the source address is throttled independently so a single attacker cannot sustain the cycle across many accounts. The residual risk is recorded as R4-03.

**Tenant state at login.** The check order is credential, then identity status, then tenant status, then MFA. A locked or suspended identity is rejected before MFA, so an attacker cannot use the MFA prompt as an oracle. A user whose tenant is suspended receives `403 TENANT_SUSPENDED` only after proving the password; this is deliberate, because the message is operationally necessary and the proof of possession justifies the disclosure.

**Session fixation.** A new `session_id` is issued at every privilege boundary: password verification to MFA challenge, MFA challenge to authenticated session, authenticated session to stepped-up session, and member context to grant context. A pre-authentication identifier is never promoted.

**Cookies.** Web refresh tokens are delivered in a `__Host-slateiq_rt` cookie: `Secure`, `HttpOnly`, `SameSite=Lax`, `Path=/`, no `Domain` attribute, 14-day expiry. Access tokens are held in memory only and sent as `Authorization: Bearer`. No token is written to `localStorage` or `sessionStorage`, because any XSS in a financial application would then become durable account takeover. State-changing requests require a double-submit CSRF token bound to the session; the refresh endpoint requires it as well.

### 4.5 Multi-Factor Authentication

| Aspect | MVP design |
|--------|-----------|
| Factor | TOTP, RFC 6238, SHA-1, 6 digits, 30-second step, ±1 step tolerance |
| Secret | 160-bit, generated server-side, encrypted with the user's data key, never returned after enrollment |
| Enrollment | Requires a fresh password check; the factor is inactive until a valid code is presented; `confirmed_at` is the activation |
| Replay | The last accepted time step is stored per factor; a code from the same or an earlier step is rejected |
| Recovery codes | Ten codes, 10 characters each, Argon2id-hashed, shown once, single use; regenerating invalidates all previous codes |
| Attempts | Five code attempts per challenge, then the challenge is discarded and the user must re-authenticate |
| Reset | Administrator with `identity.mfa_factor.administer` and step-up, plus a second approver (SOD-09); all sessions revoked; user notified; re-enrollment required at next login |
| Self-service reset | Not available. A lost factor is an administrator-assisted recovery with identity verification |
| Phase 1 | WebAuthn as the preferred factor; TOTP retained as fallback; Platform Operations required to use WebAuthn once available |

**Why TOTP at MVP.** It needs no hardware inventory, no per-user licence and no external service, and it is materially better than SMS. It is phishable, which is why WebAuthn is on the Phase 1 path and why step-up (section 4.9) is required for the actions that matter most.

**Enrollment cannot be skipped.** If a role requires MFA and the user has no factor, the post-password step is enrollment, not a skippable prompt. The user cannot reach any route other than enrollment, logout and support until a factor is confirmed. A "remind me later" link would convert a mandatory control into a suggestion.

**Recovery codes are a factor, not a bypass.** A recovery code satisfies the second factor and is consumed. It does not reset the password. Using a recovery code emits `RecoveryCodeUsed`, notifies the user, and is reviewed weekly by Security.

**Administrator reset is a sensitive action.** It requires step-up, a reason code, and a second approver when the target holds an `administer` permission. The reset does not choose a new secret; it removes the factor and forces re-enrollment, so the administrator never learns anything that could later be used to impersonate the user.

### 4.6 Session Model

Three lifetimes are deliberately distinct, and conflating them is how "remember me" becomes a permanent grant.

| Layer | Lifetime | Storage | Purpose | Revocable |
|-------|----------|---------|---------|-----------|
| Access token | 15 minutes | Client memory | Authorise an individual request | Indirectly, through the session denylist and scope version |
| Refresh token | 14 days, rotating | HttpOnly cookie (web), keystore (mobile) | Obtain a new access token without re-entering the password | Yes, immediately |
| Absolute session | 30 days from first authentication | Server record | Bound the total life of a session regardless of activity | Yes |
| Idle timeout | 30 minutes default; organization-configurable 15 minutes to 8 hours (RN-01) | Server record | Bound inactivity | Yes |
| Offline grant | 7 days (NFR-MOB-004) | Mobile secure storage | Permit capture without connectivity | At next sync or wipe |

**Access token claims.**

| Claim | Meaning | Why it is present |
|-------|---------|-------------------|
| `iss`, `aud` | Issuer and audience | Prevents token reuse across environments and services |
| `sub` | `user_id` or `service_account_id` | The actor |
| `org` | The organization whose data this token may touch | The tenant context; never accepted from the client |
| `sid` | Session identifier | Denylist key and audit correlation |
| `amr` | Authentication methods used | Distinguishes password-only from MFA sessions |
| `scope_version` | Monotonic version of the user's authority | Cache invalidation on role or grant change |
| `device_id` | Device context, where one exists | Binding and wipe |
| `iat`, `exp`, `jti`, `ver` | Issued at, expiry, token ID, key version | Standard validation and replay control |

**What the token does not contain.** Email, display name, roles, permissions, grant IDs, organization name, plan or entitlement. A token that carries permissions is a token that cannot be revoked faster than its expiry, which would break the five-minute budget. A token that carries a name is a token that leaks personal data into logs. The server resolves authority on every request from the session and the assignment or grant tables, cached under the `scope_version` key.

**Acting organization.** For a member, `org` is the home organization. For an external auditor entering a client workspace, a new access token is minted with `org` equal to the client organization and the same `sid` family; the server records which grant authorised the workspace. Only one `org` is active at a time. Switching workspace is an explicit action, audited as `WorkspaceEntered`, and it never widens the session's authority: the grant bounds it.

**Idle timeout enforcement.** `last_seen_at` is updated at most once per 60 seconds per session to avoid a write per request. Before serving any request, the middleware compares `now` with `last_seen_at` and with the organization's configured idle window. The client-side timer is a courtesy that shows a warning and offers to extend; it is not a control. An attacker replaying a stolen access token cannot extend a session, because extension requires a refresh, and refresh checks the absolute and idle bounds.

**Concurrent sessions.** Default five per user, configurable between one and ten. On the sixth login, the oldest session is revoked and the user is notified; the new login succeeds. Denying the new login was rejected because it lets an attacker lock a user out by opening sessions. Privileged roles default to three concurrent sessions. Every revocation is audited with the reason `CONCURRENCY_LIMIT`.

**Secure logout.** Logout revokes the refresh session, adds the `sid` to the denylist for the remaining access-token lifetime, clears the cookie and, on mobile, removes the tokens from the keystore. Logout of all devices revokes every session and increments `scope_version`. A logout that only clears the client is not a logout.

### 4.7 Refresh Rotation and Reuse Detection

| Step | Behaviour |
|------|-----------|
| 1 | The client presents the refresh token, the CSRF token and, on mobile, a signature from the device key |
| 2 | The server hashes the presented token and looks up the session by `token_hash`; the token format is `<token_id>.<secret>` so lookup is a single indexed read |
| 3 | Validate: session not revoked, not expired, absolute bound not exceeded, idle bound not exceeded, tenant `ACTIVE`, device matches, signature valid |
| 4 | Issue a new refresh token in the same `family_id`; mark the presented token `rotated_at`; return the new access token |
| 5 | If a token that was already rotated is presented again, treat it as reuse |

**Reuse response.** Reuse revokes the entire token family, revokes the session, increments `scope_version`, adds the `sid` to the denylist, emits `RefreshTokenReuseDetected` at high severity, notifies the user and opens a security review item. The legitimate user is forced to re-authenticate, which is the correct outcome: either the token was stolen or the client is replaying, and both warrant a fresh login.

**The 60-second grace window.** Mobile networks retry. A strict rotation scheme produces false reuse alarms when a response is lost and the client retries with the token it still holds. The server therefore permits the immediate predecessor token to be exchanged for the **same successor** within 60 seconds, provided the successor has never been used and the request fingerprint matches. This is idempotent replay, not a second rotation. After 60 seconds, or after the successor is used, reuse is treated as compromise. The window is a constant, not a configuration, because a tunable compromise window is a compromise window someone will widen.

**Refresh never extends the absolute bound.** Each refresh resets the idle clock, not the absolute clock. At 30 days the session ends and the user authenticates again, including MFA. This is what makes "30-day session" a bound rather than a hope.

**Refresh does not re-check authorization data from the client.** Roles and grants are read server-side. A client cannot ask for a narrower or wider scope at refresh.

### 4.8 Device Binding and Offline Sessions

| Surface | Binding | Anomaly response |
|---------|---------|------------------|
| Web | `device_id` cookie plus user-agent hash plus IP class (`/24` IPv4, `/48` IPv6) | Step-up if the IP class changes and the session is privileged; no hard block, because corporate NAT and mobile tethering change addresses legitimately |
| Mobile | `device_id` plus device public-key signature on every refresh | Refresh rejected if the signature does not verify; the device must re-register with password and MFA |
| Break-glass | Device plus operator identity plus approval ticket | Every request is logged and reviewed |
| Service | mTLS or signed workload token; no device concept | Audience mismatch rejects the call |

**Offline authentication.** The mobile app cannot call the server, so it carries a signed offline grant issued at the last successful online authentication:

| Field | Value |
|-------|-------|
| `user_id`, `org_id`, `device_id` | Bound to the device and user |
| `offline_until` | `min(last_online_auth + 7 days, absolute session expiry)` |
| `permission_snapshot_hash` | Hash of the permission set at issue time; the app uses the corresponding local snapshot |
| `signature` | Server signature over the grant, verified with a pinned public key |

The device requires a biometric or device passcode to unlock the grant, and the local database is SQLCipher-encrypted with a key held in the platform keystore. The seven-day window is a maximum, not a guarantee: if the organization's idle timeout is shorter, the offline window is shorter. The offline grant is invalidated early if the app receives a revocation or wipe command.

**Offline authorization is provisional.** Any record captured offline is re-authorised on sync. If the user's access was revoked while offline, the sync is rejected, the record is preserved locally for the user to export, and a security event records the attempted write. The server never accepts an offline decision as final, because the offline device cannot know about a revocation that happened after it disconnected.

**Residual risk.** A stolen, unlocked device with an unexpired offline grant can read the local data until the grant expires or the device connects and receives a wipe. This is a platform limitation of offline-first capture, mitigated by the seven-day bound, the biometric gate, SQLCipher, remote wipe and the ability to revoke the device and the user's grants. It is recorded as R4-04 and accepted for the pilot.

### 4.9 Revocation Architecture

| Trigger | Mechanism | Target latency | Evidence |
|---------|-----------|----------------|----------|
| User deactivation (FR-TNT-006) | Session rows revoked, `scope_version` incremented, `sid` denylisted, device wipe queued | Under 60 seconds; commitment is 5 minutes | `UserDeactivated`, `SessionRevoked` |
| Role change (FR-TNT-010) | `scope_version` incremented for every affected user | Under 60 seconds | `RoleChanged`, `ScopeVersionBumped` |
| Grant revocation (FR-TNT-016) | Grant status `REVOKED`, `scope_version` incremented, active grant workspace sessions revoked | Under 60 seconds; commitment is 5 minutes | `GrantRevoked`, `WorkspaceClosed` |
| Password reset | All refresh sessions revoked, `scope_version` incremented | Immediate | `PasswordResetCompleted` |
| MFA reset | All refresh sessions revoked, factor disabled | Immediate | `MfaReset` |
| Device revocation | Device sessions revoked, wipe queued | Immediate for server access; wipe at next connectivity | `DeviceRevoked`, `WipeQueued` |
| Tenant suspension | Organization status changed, all sessions for the tenant revoked, per-request status check | Under 60 seconds | `OrganizationSuspended` |
| Signing-key compromise | New `kid` published, old `kid` denylisted, all tokens with the old key rejected | Under 15 minutes | `SigningKeyRotated` |
| Emergency revoke-all | User-level or organization-level cascade | Under 60 seconds | `EmergencyRevocation` |

**Two-layer enforcement.** The session table is authoritative; Redis is a fast denylist. Every request validates the token signature, then checks the denylist, then checks the session record if the denylist lookup is unavailable. If Redis is down, the system degrades to database validation and stays available; it never fails open. The denylist entry lives for the remaining lifetime of the access token, so it can be small and self-expiring.

**Scope version.** `scope_version` is a monotonically increasing integer per user. It is embedded in the access token and used as a cache key for resolved permissions. A role or grant change increments it, which invalidates every cached decision for that user without hunting for cache entries. The cost is one integer comparison per request; the benefit is that revocation does not depend on cache eviction succeeding.

**Revocation is not deletion.** Revoked sessions remain for 90 days for investigation, then are purged. A revoked session that is deleted immediately cannot answer "was this token used after revocation", which is the first question in an incident review.

### 4.10 Step-Up Authentication

Step-up is required when the session's authentication strength is insufficient for the action, not merely when the action is "important". The check is server-side and action-specific.

| Action class | Requires step-up | Maximum age of strong authentication |
|--------------|------------------|--------------------------------------|
| Change password, email, MFA factor | Yes | 5 minutes |
| Create, widen or revoke a cross-organization grant | Yes | 5 minutes |
| Enter a client workspace as an external auditor | Yes, if the last strong authentication is older than 8 hours | 8 hours |
| Export more than 1,000 rows or download evidence archive | Yes | 15 minutes |
| Approve a payment above the approver's delegated limit | Yes | 15 minutes |
| Suspend or deprovision a user | Yes | 15 minutes |
| Change security policy or retention settings | Yes | 5 minutes |
| Start break-glass | Yes, plus second approver | 5 minutes |
| Bulk invite or bulk role change | Yes | 15 minutes |

**Mechanism.** The session record stores `last_strong_auth_at` and the methods used. A step-up challenge requires password plus a second factor and, at Phase 1, prefers WebAuthn. On success, `last_strong_auth_at` is updated and a short-lived step-up assertion is stored server-side, bound to the session and to an action class. The assertion is single-use for destructive actions and expires after the window in the table. No step-up data is placed in the access token, so a stolen token cannot claim a freshness it does not have.

**Why not re-use the login MFA.** A session that has been idle for six hours should not be able to revoke a grant because MFA was performed at login. Freshness is a property of the action, not of the session's existence.

### 4.11 Break-Glass and Support Access

Platform Operations sometimes needs to see a tenant's data to resolve an incident. That access must exist, must be narrow, and must be impossible to obtain quietly.

| Control | Design |
|---------|--------|
| Who | Members of `PLATFORM_INTERNAL` with the `SUPPORT_ENGINEER` role and a confirmed MFA factor |
| Approval | Two-person rule: one Platform Operations approver and one Security approver, recorded in `ops.support_access` |
| Ticket | A mandatory incident or support reference; free-text justification of at least 30 characters |
| Duration | Maximum 60 minutes, hard expiry, no renewal without a new approval |
| Scope | Read-only by default; write requires a separate approval naming the permission and the object |
| Tenant visibility | The tenant's Organization Administrators are notified at start and end; every action appears in the tenant audit trail attributed to `SlateIQ Support` |
| Token | Access token only, 15 minutes, `org` equal to the target tenant, `sid` bound to the support access record; no refresh token |
| Limits | Cannot change roles or grants, cannot reset another user's MFA, cannot disable audit, cannot export bulk data, cannot read credentials (which are not readable by design) |
| Review | Every use is reviewed monthly by Security; the review is SOC 2 evidence |

**Break-glass is not a grant.** It does not create a `tenancy.access_grant`, does not appear in the client's grant register, and cannot be issued by a tenant. It is a platform control with platform accountability. Modeling it as a grant would let a client revoke an incident response and would blur the distinction between customer-issued access and operator access.

**No standing access.** There is no always-on platform administrator account with cross-tenant read. The production database role used by the application has no `BYPASSRLS`; a support session sets `app.org_id` like any other request and is constrained by the same policies. The only way to bypass RLS is the migration role, which is not deployed to the application runtime and is audited separately.

### 4.12 Federation Roadmap

Federation is Phase 1 (ADR-008, FR-TNT-009). The MVP design must not make it a rewrite, so the following seams exist now even though they are unused.

| Capability | Phase 1 design | MVP seam |
|------------|----------------|----------|
| OIDC | Authorization code with PKCE; `nonce` and `state` validated; ID token signature and claims validated | `identity.federated_identity` table; `idp_id` on organization settings |
| SAML 2.0 | SP-initiated and IdP-initiated; signed assertions; audience and recipient validated; replay cache | Same table; certificate metadata store |
| JIT provisioning | First assertion creates a membership with the lowest role in the mapped set; no automatic admin | Invitation flow already creates identities; federation reuses it |
| Role mapping | Explicit group-to-role mapping per organization; no wildcard; unmapped groups yield no role | Roles are already stable, versioned identifiers |
| SCIM 2.0 | Create, update, deactivate; deactivation revokes sessions within 5 minutes | Deactivation path already exists |
| Domain verification | DNS TXT record proves control of an email domain before SSO is enforced | Email domain is already parsed for tenant hints |
| Break-glass local administrator | Exactly one local account per SSO tenant, MFA-protected, vaulted, reviewed quarterly | Local authentication remains available |
| Mixed mode | SSO for members, local credentials for external auditors if the firm has no IdP | Per-organization policy flag |

**Rules that survive federation.** A federated identity still has exactly one `home_org_id`. The IdP asserts who the person is; it does not assert which tenant they may read. Tenant authority remains a SlateIQ decision built from membership and grants. An IdP group named `platform-admins` cannot mint a Platform Operations role in another tenant, because role mapping is scoped to the organization that owns the IdP connection.

**No social login.** Google, Microsoft and Apple consumer identities are not accepted as workforce identities for a financial system. Enterprise OIDC and SAML are accepted because the customer's IdP carries employment and termination signals; a personal account does not.

### 4.13 Authentication Data Model

| Table | Key columns | Constraints and indexes |
|-------|-------------|-------------------------|
| `identity.credential` | `credential_id`, `user_id`, `password_hash`, `algorithm`, `params_version`, `pepper_version`, `changed_at`, `must_change` | Unique `user_id`; index on `params_version` for rehash campaigns |
| `identity.mfa_factor` | `factor_id`, `user_id`, `factor_type`, `secret_ciphertext`, `confirmed_at`, `last_used_step`, `disabled_at` | Partial unique index: one active TOTP factor per user |
| `identity.recovery_code` | `code_id`, `user_id`, `code_hash`, `used_at`, `generation` | Index `(user_id, generation)` |
| `identity.refresh_session` | `session_id`, `user_id`, `org_id`, `device_id`, `family_id`, `token_hash`, `issued_at`, `expires_at`, `absolute_expires_at`, `rotated_at`, `revoked_at`, `last_seen_at` | Unique `token_hash`; index `(user_id, revoked_at)`; index `(org_id, revoked_at)`; index `family_id` |
| `identity.device_registration` | `device_id`, `user_id`, `platform`, `public_key`, `trust_state`, `revoked_at` | Index `(user_id, revoked_at)`; limit enforced in the service layer |
| `identity.password_reset` | `reset_id`, `user_id`, `token_hash`, `expires_at`, `used_at` | Unique `token_hash`; purge after 24 hours |
| `identity.email_change` | `change_id`, `user_id`, `new_email_ciphertext`, `old_token_hash`, `new_token_hash`, `expires_at` | One open change per user |
| `identity.service_account` | `service_account_id`, `org_id`, `name`, `secret_hash`, `permission_set`, `owner_user_id`, `expires_at` | Unique `(org_id, name)`; index `expires_at` |
| `ops.support_access` | `support_access_id`, `operator_user_id`, `target_org_id`, `approver_one`, `approver_two`, `ticket_ref`, `reason`, `starts_at`, `expires_at`, `ended_at` | Index `(target_org_id, starts_at)`; index `expires_at` for the expiry job |

**Partitioning.** Session and authentication-event tables are partitioned monthly by `issued_at`, because they are high-volume and time-bounded. Old partitions are detached and archived after 90 days; the audit trail retains the security events for seven years.

**Deletion.** Expired refresh sessions are purged 90 days after expiry. Device registrations are purged 90 days after revocation. Password reset rows are purged 24 hours after use or expiry. Personal data is removed by the erasure workflow, not by these jobs.

### 4.14 Authentication Security Events

Every event below is emitted through the audit pipeline (ADR-012) with `event_id`, `occurred_at`, `actor_user_id`, `actor_org_id`, `tenant_org_id`, `ip`, `user_agent_hash`, `device_id`, `correlation_id`, `outcome` and `reason_code`. No event contains a password, token, secret, recovery code or full user agent string.

| Event | Severity | Key fields | Consumed by |
|-------|----------|-----------|-------------|
| `UserAuthenticated` | Info | `amr`, `auth_time`, `session_id` | Session analytics, anomaly detection |
| `AuthenticationFailed` | Info | `reason_code`, source address | Lockout, threat detection |
| `AccountLocked` | Warning | Failure count, window | Security dashboard, user notification |
| `MfaChallenged` | Info | `factor_type`, outcome | Coverage reporting |
| `MfaEnrolled` | Info | `factor_type` | NFR-SEC-003 evidence |
| `MfaReset` | High | Administrator, reason, approver | Security review |
| `RecoveryCodeUsed` | Warning | Remaining codes | Weekly security review |
| `PasswordChanged` | Info | Initiator (`SELF` or `RESET`) | Audit |
| `PasswordResetRequested` | Info | Source address | Abuse detection |
| `RefreshTokenReuseDetected` | Critical | `family_id`, `session_id` | Incident response, user notification |
| `SessionRevoked` | Info | Reason, initiator | Access review |
| `SessionConcurrencyExceeded` | Info | Revoked session | UX telemetry |
| `DeviceRegistered` | Info | Platform, app version | Device inventory |
| `DeviceRevoked` | Warning | Initiator, reason | Wipe tracking |
| `WipeQueued`, `WipeAcknowledged` | Warning | Device | NFR-SEC-011 evidence |
| `StepUpGranted` | Info | Action class, `amr` | Sensitive action audit |
| `BreakGlassStarted`, `BreakGlassEnded` | Critical | Ticket, approvers, target tenant | Monthly security review, SOC 2 |
| `InvitationAccepted`, `InvitationRevoked` | Info | Inviter, role | Access review |
| `UserActivated`, `UserDeactivated`, `UserSuspended` | Info | Initiator, reason | Access review, SOX evidence |
| `SigningKeyRotated` | High | Old and new `kid` | Platform audit |
| `EmergencyRevocation` | Critical | Scope, initiator, approver | Incident response |

**Correlation.** A login, its MFA challenge, the session creation and the first request share one `correlation_id` (NFR-AUD-004). An investigator can reconstruct the flow from four records without joining on timestamps.

### 4.15 Traceability for Sections 3 and 4

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| FR-TNT-004 | Sections 3.5, 4.4 | Invitation table, acceptance flow, expiry and reuse tests |
| FR-TNT-005 | Sections 3.3, 3.10 | Self-service profile fields; audit on change |
| FR-TNT-006 | Sections 3.4, 4.9 | Deactivation cascade; 60-second target against a 5-minute commitment |
| FR-TNT-007 | Sections 4.1, 4.4, 4.5 | MFA matrix; lockout after five failures for 15 minutes |
| FR-TNT-008 | Sections 4.2, 4.3 | Argon2id parameters; reset token 30 minutes; no logging |
| FR-TNT-009 | Section 4.12 | Federation seams and Phase 1 design |
| FR-TNT-022 | Sections 4.6, 4.7, 4.9 | Idle, absolute, concurrent limits; secure logout |
| FR-TNT-024 | Section 3.5 | Bulk invitation validation (Phase 1) |
| NFR-SEC-003 | Sections 4.1, 4.5 | 100 percent MFA coverage for privileged roles and external auditors |
| NFR-SEC-011 | Sections 3.8, 4.8 | SQLCipher, keystore key, remote wipe, 7-day bound |
| NFR-SEC-013 | Sections 4.3, 4.4 | Per-account and per-source rate limits |
| NFR-MOB-004 | Sections 4.8 | Offline grant capped at 7 days with biometric gate |
| NFR-PRV-003 | Sections 3.2, 4.9 | Attribution, revocation evidence, segregation enforced in section 5 |
| NFR-PRV-005 | Sections 3.3, 3.10 | Data minimisation and inventory |
| NFR-PRV-006 | Section 3.10 | DSR handling within 30 days |
| NFR-AUD-001, NFR-AUD-004 | Section 4.14 | Event catalogue and correlation ID |
| CAP-002 | Sections 4.1 to 4.14 | Authentication and session capability |

---

## 5. Authorization Model

### 5.1 Principles

| Principle | Rule | Consequence |
|-----------|------|-------------|
| **Deny by default** | A request is permitted only when an explicit permission, an in-scope resource and a satisfied veto all agree. Absence of a rule is a denial, never an allow. | A new endpoint that forgets to declare a permission fails closed. |
| **Least privilege** | A role contains the smallest permission set that completes its persona's job. Seed roles are reviewed at every release. | No seeded role contains `administer` outside the two administrator roles. |
| **Authority is resolved, not asserted** | The client never supplies a role, permission, tenant or scope. Those are read from server-side records using the authenticated session. | A crafted request cannot widen its own authority. |
| **Scope is a second dimension** | A permission answers "what action"; a scope answers "on which objects". Both must hold. | `expense.expense.approve` at `SHOW` scope cannot approve an expense in another show. |
| **Intersection, never union** | Effective scope is the intersection of assignment scope, grant scope and object containment. Adding authority can only narrow. | Delegation, grants and assignments cannot combine into a wider scope than any one of them. |
| **Separation of duties is a veto** | A permission that would otherwise be allowed is still denied when it completes a prohibited combination. | SoD is not a report; it is an eligibility gate in the decision. |
| **One decision point** | Every API and every UI action resolves through the same policy decision point. | There is no second, weaker authorization path for "internal" callers. |
| **Auditable decisions** | Every denial and every sensitive permit emits an event with the permission, scope, authority source and correlation ID. | An access dispute is answered from records, not memory. |

### 5.2 Building Blocks

| Concept | Definition | Table | Mutable |
|---------|------------|-------|---------|
| **Permission** | An atomic `domain.resource.action` key. The catalogue is closed and versioned with the release. | `tenancy.permission` | Only by migration |
| **Role** | A named bundle of permissions, scoped to one organization. Either a seeded system role (`is_system = true`) or a tenant-defined custom role. | `tenancy.role` | Custom roles yes; system roles no (FR-TNT-010) |
| **Role version** | An immutable snapshot of a role's permission set at each change, retained for audit and for explaining historical decisions. | `tenancy.role_version` | No |
| **Assignment** | The binding of a membership to a role, one scope type and a set of scope identifiers, with optional validity dates. | `tenancy.assignment` | Yes, audited |
| **Grant profile** | A fixed permission ceiling available to external principals. Not a role; it cannot be edited by a tenant. | `tenancy.grant_profile` | Only by release |
| **Grant** | A client-issued, time-bound authorisation naming one grantee, one profile, one scope and an engagement purpose. | `tenancy.access_grant` | Lifecycle only, section 8 |
| **Delegation** | A time-bound transfer of approval authority from one member to another, bounded by the delegator's own limit. | `tenancy.delegation` | Yes, audited |
| **SoD rule** | A prohibited combination of permissions, optionally bounded to the same scope. | `tenancy.sod_rule` | Only by release |
| **Approval limit** | The maximum amount a role or delegation may approve, in the organization's base currency. | `tenancy.approval_limit` | Yes, audited |

**Permission keys are flat.** `expense.expense.approve` does not imply `expense.expense.read`, and `administer` does not imply the other eight actions. Every action is granted explicitly. The alternative, a hierarchy, creates an invisible expansion that reviewers cannot see and that a single mistaken parent grant turns into broad access. Flat keys make a role's permission list equal to its authority.

**Nine actions, no more.** `read`, `create`, `update`, `delete`, `submit`, `approve`, `reject`, `export`, `administer`. A new verb requires a catalogue migration and a review of every role. Ad-hoc verbs such as `manage`, `view` or `edit` are rejected because they collapse distinct authorities into one word.

### 5.3 Permission Domains

The catalogue has fifteen domains, aligned one-to-one with the module schemas in Document 6 section 8.2. A domain owns its keys; no module writes into another module's namespace.

| Domain | Module | Representative resources | Typical actions | Notes |
|--------|--------|--------------------------|-----------------|-------|
| `tenancy` | MOD-01 | `organization`, `membership`, `role`, `assignment`, `invitation`, `access_review`, `delegation`, `sod_exception` | read, create, update, delete, administer | Tenant administration |
| `identity` | MOD-02 | `user`, `credential`, `mfa_factor`, `session`, `device`, `service_account` | read, update, delete, administer | No permission exposes a secret |
| `production` | MOD-03 | `show`, `season`, `episode`, `department`, `category`, `party`, `party_link` | read, create, update, delete, submit | |
| `budget` | MOD-04 | `budget`, `budget_version`, `budget_line`, `variation`, `forecast`, `approval` | read, create, update, submit, approve, reject, export | Approval is amount-bounded |
| `vendor` | MOD-05 | `vendor`, `contact`, `contract`, `rate_card`, `payment_term` | read, create, update, delete, approve, export | Vendor approval is separate from payment approval |
| `audit` | MOD-06 | `audit_plan`, `audit_assignment`, `audit_finding`, `audit_evidence`, `audit_report`, `access_grant` | read, create, update, submit, approve, reject, export, administer | `audit.access_grant.*` matches Document 6 Appendix C.1 |
| `expense` | MOD-07 | `expense`, `expense_line`, `advance`, `reimbursement`, `reconciliation` | read, create, update, delete, submit, approve, reject, export | |
| `intel` | MOD-08 | `cost_fact`, `variance`, `benchmark`, `anomaly`, `portfolio_view` | read, export | Read-mostly; writes are pipeline-owned |
| `document` | MOD-09 | `document`, `version`, `folder`, `signature` | read, create, update, delete, export | Download is `read`; bulk archive is `export` |
| `excel` | MOD-10 | `template`, `import_job`, `export_job`, `validation_error` | read, create, update, delete | Import commit requires `excel.import_job.create` plus the target domain's `create` |
| `notification` | MOD-11 | `notification`, `preference`, `digest`, `announcement` | read, update, create | Users manage their own preferences under `SELF` |
| `compliance` | MOD-12 | `audit_event`, `legal_hold`, `dsr_request`, `retention_policy`, `control`, `evidence_pack` | read, create, update, approve, export | `audit_event` is read-only to every role |
| `report` | MOD-13 | `report_definition`, `report_run`, `dashboard`, `schedule` | read, create, update, delete, export | Export of restricted reports needs step-up |
| `sync` | MOD-14 | `sync_batch`, `sync_conflict`, `checkpoint` | read, create, update, approve | Conflict approval is a human action |
| `ops` | MOD-15 | `tenant_region`, `provisioning_run`, `support_access`, `feature_flag`, `job`, `key`, `identity_transfer`, `service_account` | read, create, update, delete, approve, administer | No tenant role holds any `ops` permission |

**Catalog integrity.** The full catalogue is Appendix A. It is generated from a versioned manifest, and the build fails if an endpoint declares a permission that is absent from the manifest, if a manifest key is malformed, or if a role references an unknown key. FR-TNT-011's "unknown permission is rejected" is therefore enforced at compile time as well as at runtime.

**Resource naming.** Singular, snake_case, matching the aggregate or read model. `budget.budget_version.approve` is valid; `budget.budgets.approve` and `budget.version.approve` are not. A read model gets its own resource name rather than borrowing the aggregate's, so that read access to a projection does not silently become read access to the underlying records.

### 5.4 Scope Model

Scope is the second axis of every decision. It is stored as a type plus a set of identifiers on the assignment or grant, and it is never embedded in the permission key.

| Scope type | Identifier | Applies to | Containment | Allowed in a grant | Notes |
|------------|------------|------------|-------------|--------------------|-------|
| `ORG` | `org_id` | Every resource in the tenant | Contains all other scopes | Yes | Never crosses a tenant boundary |
| `SHOW` | `show_id` | Show, season, episode, department, budget, expense, vendor link, document | Contains `SEASON`, `DEPARTMENT`, `EPISODE` | Yes | The normal production unit |
| `SEASON` | `season_id` | Season, its episodes, season budget, season documents | Contains `EPISODE` within the season | Yes | Narrower than show |
| `EPISODE` | `episode_id` | Episode, episode budget, episode expenses and documents | Leaf | Yes | Narrowest production scope |
| `DEPARTMENT` | `department_id` | Department, its budget lines, expenses coded to it, members assigned to it | Within one show | Yes | Orthogonal to season/episode |
| `ASSIGNMENT` | `assignment_id` | An audit assignment, its findings, evidence and working papers | Exactly one show and one date range | **Required for external auditors** | Only valid inside an active grant |
| `SELF` | `user_id` | The actor's own profile, preferences, sessions, devices, own expenses and own delegations | Leaf | No | Never sufficient for an approval |

**Containment.** `ORG` contains `SHOW`; `SHOW` contains `SEASON`, `DEPARTMENT` and `EPISODE`; `SEASON` contains `EPISODE`. `DEPARTMENT` and `SEASON` are not comparable, so an assignment holding only `DEPARTMENT` cannot read a season-level budget unless the department scope is explicitly widened. `ASSIGNMENT` is a specialisation that carries an implicit show and date range; it never contains another scope and cannot be combined with `ORG` in the same grant.

**One scope type per assignment.** An assignment has exactly one `scope_type` and one or more identifiers of that type. A user who needs two shows receives two assignments, or one assignment naming both show IDs. Mixing a show and a department in one assignment is rejected, because the intersection semantics become ambiguous at the first object that belongs to both.

**Scope identifiers are validated at assignment.** A `show_id` must belong to the assigning organization. A `department_id` must belong to the named show. An `assignment_id` must belong to an active grant, name the same grantee, and fall inside the grant window. Invalid scope fails the assignment; it is never stored and filtered later.

**Scope is not a filter.** A resource outside the effective scope returns `403`, not an empty list. Filtering a list is a usability behaviour; authorisation is a decision. Returning an empty collection for an out-of-scope single-object request would leak the fact that the object exists and would hide a misconfigured assignment from the operator.

### 5.5 System Role Catalogue

Eighteen seeded roles cover the thirteen personas. A role applies only to the organization types named in the table; assigning an audit-firm role to a production house is rejected.

| Role key | Name | Persona | Organization types | MFA | Scope types | Authority summary | Hard exclusions |
|----------|------|---------|--------------------|-----|-------------|-------------------|-----------------|
| `ORG_ADMIN` | Organization Administrator | P-12 | OTT, broadcaster, production house, platform internal | Yes | ORG | Manage organization profile, users, roles, assignments, invitations, access reviews, security policy, entitlements view | Cannot approve financial transactions; cannot alter audit events; cannot grant cross-org access |
| `COMMISSIONING_ADMIN` | Commissioning Administrator | P-01, P-02 | OTT, broadcaster | Yes | ORG, SHOW | Create shows, approve budgets within limit, appoint auditors, manage grants, portfolio read | Cannot approve expenses; cannot administer roles |
| `PRODUCTION_ADMIN` | Production Administrator | P-03 | Production house | Yes | ORG, SHOW | Manage shows, departments, parties, budgets (submit), vendors, documents, production reports | Cannot approve own budget; cannot approve payments |
| `FINANCE_HEAD` | Finance Head | P-04 | OTT, broadcaster, production house | Yes | ORG, SHOW | Approve budgets, variations, expenses, payments, close periods, view all financial reports | Cannot create the expense they approve; cannot administer users |
| `COST_CONTROLLER` | Cost Controller | P-04 | OTT, broadcaster, production house | Yes | ORG, SHOW | Monitor variance, initiate budget revisions and reallocations, review commitments, configure alert rules, close and lock periods, approve expenses within limit, verify or dispute evidence | Cannot approve a budget version, a variation above threshold or a period reopen; cannot approve own expense or own revision; cannot administer users |
| `DEPARTMENT_HEAD` | Department Head | P-09 | Production house | Yes | SHOW, DEPARTMENT | Department budget, approve department expenses within limit, team read, department reports | Cannot approve above department limit; cannot approve own expense |
| `ACCOUNTS_EXECUTIVE` | Accounts Executive | P-10 | Production house | No | ORG, SHOW, DEPARTMENT | Vendor master, contracts, expense capture and submit, imports, reconciliation, exports | Cannot approve anything; cannot delete financial records |
| `AUDIT_MANAGER` | Audit Manager | P-05 | OTT, broadcaster, production house | Yes | ORG, SHOW, SEASON, EPISODE, ASSIGNMENT | Audit plan, assignments, findings, reports, grant creation and revocation, evidence review | Cannot approve grants alone; cannot edit audit events |
| `INTERNAL_AUDITOR` | Internal Auditor | P-06 | Production house, broadcaster | Yes | SHOW, ASSIGNMENT | Read scoped financial and production data, capture evidence, raise findings | No approval; no export outside assignment; no grant creation |
| `MANAGEMENT_VIEWER` | Management Viewer | P-11 | All data organizations | No | ORG | Read portfolio, budgets, variances, dashboards and reports | No writes; no exports of restricted evidence |
| `PRODUCTION_COORDINATOR` | Production Coordinator | P-03 support | Production house | No | SHOW, SEASON, EPISODE | Schedule, departments, parties, documents, call sheets | No financial approval; no vendor bank details |
| `VENDOR_COORDINATOR` | Vendor Coordinator | P-10 support | Production house | No | ORG, SHOW | Vendor onboarding, contracts, rate cards, payment terms | Cannot approve vendors; cannot see bank details after entry; cannot approve payments |
| `COMPLIANCE_OFFICER` | Compliance Officer | P-12 support | All | Yes | ORG | Retention, legal hold, DSR handling, control evidence, audit search | Cannot read restricted financial detail beyond `compliance` resources; cannot alter audit events |
| `ACCESS_REVIEWER` | Access Reviewer | P-12 support | All | Yes | ORG | Run and certify access review campaigns | Cannot certify own access; cannot modify roles |
| `AUDIT_FIRM_ADMIN` | Audit Firm Administrator | P-07 employer | Audit firm | Yes | ORG (firm only) | Manage firm users, attest auditors, view firm grants, deprovision, accept NDA on behalf of firm | No client data; cannot create client grants; cannot approve client access |
| `SUPPORT_ENGINEER` | Support Engineer | P-13 | Platform internal | Yes | ORG (none standing) | Request break-glass, view platform diagnostics, read redacted telemetry | No standing tenant read; no financial actions |
| `PLATFORM_OPERATIONS` | Platform Operations | P-13 | Platform internal | Yes | ORG (none standing) | Provision, suspend, archive, entitlements, feature flags, regional placement | Cannot read tenant financial data; cannot approve break-glass alone |
| `PLATFORM_SECURITY` | Platform Security | P-13 | Platform internal | Yes | ORG (none standing) | Approve break-glass, rotate keys, review security events, manage SoD exceptions | Cannot provision tenants; cannot approve business transactions |

**Administrator roles are not superusers.** `ORG_ADMIN` holds no financial approval and no `ops` permission. `PLATFORM_OPERATIONS` holds no tenant financial read and cannot pass the second approval for its own break-glass request. The two most dangerous capabilities in the product, moving money and reading another tenant's data, are deliberately absent from the roles that administer the system.

**Commissioner-hosted shows (ADR-025, provisional).** When an OTT platform or broadcaster hosts a commissioned show in its own tenant, `PRODUCTION_ADMIN`, `PRODUCTION_COORDINATOR`, `DEPARTMENT_HEAD`, `ACCOUNTS_EXECUTIVE` and `VENDOR_COORDINATOR` are also seeded in that tenant, assignable only at `SHOW`, `SEASON`, `EPISODE` or `DEPARTMENT` scope, never at `ORG`. The production-house staff who hold them are members of the hosting tenant for that engagement. Document 2 section 2.3 defines the two hosting patterns.

**System roles are immutable (FR-TNT-010).** A tenant cannot add a permission to `FINANCE_HEAD` or remove one from `AUDIT_MANAGER`. It can create a custom role that composes catalogue permissions, within the ceilings in section 5.6. Immutability is what makes the SoD matrix enforceable across tenants: if a tenant could edit `FINANCE_HEAD`, the matrix would hold only until the first customer asked.

### 5.6 Custom Roles

| Rule | Detail |
|------|--------|
| Creation | `ORG_ADMIN` with `tenancy.role.create` and step-up |
| Permission selection | Any subset of the catalogue except `ops.*`, `tenancy.role.administer`, `tenancy.sod_exception.*` and `compliance.audit_event.update` |
| Ceiling | A custom role cannot contain a permission the creator does not hold at `ORG` scope |
| Scope | A custom role is tenant-local and cannot be exported or imported |
| Editing | Creates a new `role_version`; assignments continue to reference the role, and the new version applies within 60 seconds (FR-TNT-010) |
| Retiring | A role with active assignments cannot be deleted; it is retired, and existing assignments must be reassigned or end |
| MFA propagation | Adding an `administer` or `approve` permission makes the role MFA-required; assignments to users without a factor are blocked until enrollment |
| SoD propagation | Adding a permission that violates an SoD rule for an existing assignee is blocked, or requires a recorded exception (section 5.8) |
| Audit | Creation, edit, retire and every assignment change emit events with before and after state |

**Why a custom role cannot contain `ops.*`.** Tenant-defined authority over platform operations would allow a customer to alter its own placement, entitlement enforcement or support access. Platform operations are not tenant-configurable, so they are not delegable.

**Why the creator's own authority is a ceiling.** Otherwise an Organization Administrator could construct a role containing `compliance.legal_hold.approve` or `audit.access_grant.administer` that they do not hold themselves, assign it to a colleague, and use that colleague as a proxy. The ceiling makes privilege construction a subset of existing privilege.

### 5.7 Grant Profiles for External Principals

A grant does not carry a role. It carries a grant profile: a fixed, release-managed permission ceiling. The client chooses the profile, the scope and the dates; it cannot add a permission beyond the profile, and the auditor cannot request one.

| Profile | Permission ceiling | Typical use | Maximum scope |
|---------|--------------------|-------------|---------------|
| `AUDIT_READ` | Read on `production`, `budget`, `expense`, `vendor`, `document`, `report`; `audit.audit_assignment.read`, `audit.audit_finding.read` | Statutory or lightweight review | One show, or one assignment |
| `AUDIT_EVIDENCE` | `AUDIT_READ` plus `document.export`, `audit.audit_evidence.*`, `audit.audit_finding.create/update/submit`, `audit.audit_report.read` | Fieldwork with evidence capture | One show, or one assignment |
| `AUDIT_FINANCE` | `AUDIT_EVIDENCE` plus `budget.read`, `expense.read`, `vendor.read`, `intel.variance.read`, `report.export` | Financial audit and cost investigation | One show, or one assignment |

**No `administer`, ever.** No grant profile contains `administer` on any resource, and no profile contains `tenancy.*`, `identity.*`, `ops.*` or `compliance.legal_hold.*`. An external principal cannot change a role, invite a user, alter retention or touch platform operations. This is enforced by a manifest assertion that fails the build if a grant profile references such a key.

**Profiles are ceilings, not defaults.** A grant may narrow the profile by restricting scope or by removing a permission through the `permissions_denied` list. It cannot widen it. The effective external permission set is `profile_permissions - denied_permissions`, intersected with grant scope. Storing the denial list makes the intent auditable: a reviewer can see that the client deliberately withheld vendor data rather than inferring it from an absence.

**One grant, one grantee, one client.** A grant names exactly one `grantee_user_id` and one `client_org_id` (BR-AUD-002). It cannot be issued to a group, a firm or an email domain. A firm with five auditors on an engagement receives five grants, each individually revocable and individually reviewable. Group grants are how an auditor who left six months ago still has access.

### 5.8 Segregation of Duties

SoD rules are evaluated twice: at assignment time, when a conflicting combination would be created, and at decision time, when the actor attempts the action. The second check is not redundant; it catches a combination created by delegation, a grant, or a role version change.

| Rule | Conflicting authority | Scope of conflict | Enforcement | Rationale |
|------|-----------------------|-------------------|-------------|-----------|
| **SOD-01** | `expense.expense.create` and `expense.expense.approve` | Same show | Hard block | The creator cannot approve their own or their team's expense |
| **SOD-02** | `expense.expense.submit` and `expense.expense.approve` | Same show | Hard block | Submission is preparation; approval must be independent |
| **SOD-03** | `budget.budget_version.create` and `budget.budget_version.approve` | Same show | Hard block | Budget preparer cannot be the approver |
| **SOD-04** | `vendor.vendor.create` and `vendor.vendor.approve` | Same org | Conditional | Allowed only with a recorded exception and a compensating review |
| **SOD-05** | `vendor.payment_term.update` and `expense.expense.approve` | Same org | Hard block | Bank-detail changes must not sit with payment approval |
| **SOD-06** | `tenancy.role.administer` and `tenancy.access_review.approve` | Same org | Hard block | The person who grants access cannot certify it |
| **SOD-07** | `audit.access_grant.create` and `audit.access_grant.approve` | Same org | Hard block | Grant creation and approval are two people |
| **SOD-08** | `ops.support_access.create` and `ops.support_access.approve` | Same org (platform) | Hard block | Break-glass requires two people |
| **SOD-09** | `identity.mfa_factor.delete` and `identity.user.update` on the same target | Same target user | Hard block | An administrator cannot both reset a factor and alter the account it protects without a second approver |
| **SOD-10** | `compliance.legal_hold.update` and `compliance.dsr_request.approve` | Same org | Conditional | Allowed only with Legal approval recorded on the hold |

**Self-approval is absolutely prohibited.** No exception, no delegation, no "small amount" threshold. An actor cannot approve an expense, budget, variation, payment or grant where they are the creator, the submitter, the beneficiary or the delegated approver of their own record. BR-ACC-004 is implemented as a hard predicate in the decision, not as a validation in the UI.

**SoD scope is deliberate.** SOD-01 applies per show, not per organization, because a finance head in a small production house may legitimately create a budget in one show and approve an expense in another. Extending the conflict to the whole organization would make the role unusable and drive customers to shared accounts, which is worse than the risk being controlled.

**Exceptions.** A conditional rule may be waived only through `tenancy.sod_exception`, which requires:

| Field | Rule |
|-------|------|
| Rule | The SoD rule being waived |
| Subjects | The user and the conflicting permissions |
| Scope | The show or organization |
| Reason | Minimum 50 characters, specific |
| Compensating control | Free text plus a named reviewer |
| Approvers | Organization Administrator and Finance Head; Platform Security when the rule is SOD-04 or SOD-10 |
| Validity | Maximum 90 days, no automatic renewal |
| Review | Listed in every quarterly access review and every audit evidence pack |

**Reporting.** Active exceptions are reported to the tenant's Audit Manager and included in the annual SOX-supporting evidence pack. An exception is not a secret; if it cannot be shown to an auditor, it is not a control.

### 5.9 Approval Authority and Delegation

Approval is a permission plus a limit. Holding `expense.expense.approve` does not authorise approving an unlimited amount.

| Layer | Source | Rule |
|-------|--------|------|
| Role limit | `tenancy.approval_limit` bound to the role | Maximum amount the role may approve in a scope |
| Personal limit | Optional per-user override | Cannot exceed the role limit |
| Delegated limit | `tenancy.delegation.max_amount` | Cannot exceed the delegator's personal limit |
| Organization ceiling | Tenant configuration | Absolute maximum for any single approval; cannot exceed the platform ceiling |
| Effective limit | Minimum of all four | Converted to base currency at the approval-date rate |

| Control | Rule |
|---------|------|
| Currency | Approval is recorded in the transaction currency and the base currency, with the rate and rate date. The limit test uses the base-currency amount. |
| Period | A limit is per transaction unless the policy states an aggregate; aggregate limits are evaluated against committed spend, not just approved spend. |
| Escalation | An amount above the approver's limit routes to the next authority; it is not silently rejected. |
| Multi-step approval | The budget module defines chains; this document defines who may occupy each step, not how many steps exist. |
| Attribution | Every approval stores `actor_user_id`, `on_behalf_of_user_id` when delegated, the limit applied, the role version and the decision timestamp. |

**Delegation (FR-TNT-020).**

| Field | Rule |
|-------|------|
| Delegator | A member with an approval permission and a personal limit |
| Delegate | A member of the same organization with the same permission, or with a role that can receive it |
| Scope | Equal to or narrower than the delegator's scope |
| Amount | Equal to or lower than the delegator's personal limit |
| Start and end | Mandatory; maximum 90 days; no open-ended delegation |
| Reason | Mandatory; shown in the approval record |
| Sub-delegation | Prohibited. A delegate cannot delegate onward |
| Self-delegation | Prohibited |
| Conflicts | Delegation is rejected if it would create an SoD conflict or allow the delegate to approve their own record |
| Revocation | Either party or an Organization Administrator may revoke at any time; revocation takes effect within 60 seconds |
| Expiry | Automatic at the end date; expired delegations are not resurrected by extending the role |
| Notification | Both parties and the Finance Head are notified at creation and revocation |
| Audit | `DelegationCreated`, `DelegationRevoked`, `DelegationExpired`; approvals record both identities |

**Delegation does not transfer the role.** The delegate's effective permission is the intersection of their own role, the delegation and the scope. A delegate who lacks the base permission cannot acquire it by delegation; the delegation narrows an existing authority, it does not create one. This prevents delegation from becoming a backdoor role-assignment mechanism.

### 5.10 Access Reviews

FR-TNT-021 requires quarterly reviews. The review is a control, not a report: an uncertified access survives only until the campaign closes.

| Aspect | Design |
|--------|--------|
| Frequency | Quarterly, plus an off-cycle campaign after a security incident, a major reorganisation or a tenant merger |
| Campaign scope | Every active membership, assignment, grant, service account, delegation and active SoD exception |
| Reviewers | The member's manager or the resource owner; an Organization Administrator for orphaned accounts; `ACCESS_REVIEWER` may run the campaign but cannot certify their own access |
| Decisions | `CERTIFY`, `REVOKE`, `REDUCE_SCOPE`, `ESCALATE` |
| Evidence | Reviewer, decision, timestamp, comment, prior and resulting authority |
| Orphans | A member with no active reviewer, no line manager and no recent activity is flagged and suspended at campaign close |
| Non-response | Reminder at day 7 and day 14; undecided items are flagged at close and suspended 7 days later |
| Revocation latency | A `REVOKE` decision takes effect within 60 seconds, not at campaign close |
| Reporting | Coverage percentage, decisions, revocations, orphan count, mean time to revoke, exceptions carried forward |
| Retention | Campaign records retained for 7 years as SOC 2 and SOX-supporting evidence |

**Reviewer independence.** A reviewer cannot certify their own membership, their own grant or an assignment that they created in the current campaign. The platform enforces this by excluding the reviewer's own rows and by requiring a second reviewer for any campaign where the reviewer is also the tenant's only Organization Administrator. A one-person tenant cannot self-certify; Platform Security acts as the second reviewer.

**Grants are reviewed by the client, not the firm.** The client's Audit Manager certifies that the engagement is still active and the scope is still appropriate. The firm's administrator certifies that the auditor is still employed. Both are required; either can revoke.

### 5.11 Denial Semantics and Information Disclosure

| Situation | Response | Body | Security event |
|-----------|----------|------|----------------|
| Authenticated, lacks the permission | `403` | `code: FORBIDDEN`, `permission`, `correlation_id` | `AuthorizationDenied` with permission and scope |
| Authenticated, has the permission, object outside scope | `403` | `code: OUT_OF_SCOPE`, `correlation_id` | `AuthorizationDenied` with scope reason |
| Authenticated, object belongs to another tenant | `404` | `code: NOT_FOUND` | `CrossTenantAccessAttempt` at high severity |
| Unauthenticated or expired token | `401` | `code: UNAUTHENTICATED` | `AuthenticationRequired` |
| Tenant suspended | `403` | `code: TENANT_SUSPENDED`, support reference | `TenantAccessBlocked` |
| Module not entitled | `403` | `code: MODULE_NOT_ENABLED` | `EntitlementDenied` |
| Step-up required | `403` | `code: STEP_UP_REQUIRED`, action class | `StepUpRequired` |
| SoD veto | `403` | `code: SEGREGATION_OF_DUTIES` | `SodViolation` at high severity |
| Rate limit | `429` | `Retry-After` | `RateLimited` |

**Why 404 for cross-tenant.** A `403` confirms that the object exists, which is itself a disclosure: an attacker can enumerate identifiers and learn which ones are real across the platform. A `404` is indistinguishable from a genuinely missing object. The internal event is high severity because a legitimate client should never produce this response; it means either a bug or an attack.

**No permission enumeration.** The denial body names the required permission only when the caller is a member of the tenant and the permission is in the public catalogue. It never names the owner, the tenant, the scope identifiers or the reason another tenant was involved.

**Denials are audited at the same fidelity as permits.** A denied approval, a denied export and a denied grant creation are exactly the events an investigator needs. Sampling only permits would hide the probing.

### 5.12 Policy Decision Point

| Aspect | Design |
|--------|--------|
| Location | In-process in MOD-01, called by every module through a single `IAuthorizationService`; no network hop at MVP (AD4-04) |
| Input | `principal` (from session), `permission`, `resource` (type and identifiers), `tenant context` (from token), optional `amount` and `required_step_up` |
| Output | `Decision { allow, reason_code, obligations[] }` where obligations include step-up, reason code, audit level and approval-limit check |
| Evaluation order | Tenant state, principal state, session validity, authority source, permission, scope, SoD, delegation and limit, entitlement, step-up |
| Failure mode | Fail closed. An exception in the PDP is a denial and a P1 alert, never an allow |
| Performance | Cached resolution, target p99 under 5 ms in-process; no database round trip on the hot path when the cache is warm |
| Cache key | `(user_id, org_id, scope_version)`; decisions are not cached across tenants or across scope versions |
| Invalidation | `scope_version` increment on role, assignment, grant, delegation, SoD or membership change; TTL 60 seconds as a backstop |
| Decision log | Permits for sensitive actions and all denials are written to the audit trail; high-volume reads are sampled at 1 percent with a per-endpoint cap |
| Testing | Every endpoint has a positive and a negative authorization test; the negative test asserts the exact status and code |

**Authority source is singular.** A request is authorised either by a membership (roles and assignments) or by a grant, never by a union of both. The session records which source is active for the current `org`. If a user is a member of the organization, the grant path is not evaluated; if the user is an external grantee, no membership is consulted. This eliminates the "grant plus role" escalation path in which a small grant and a stale role combine into broader access.

**Obligations are enforceable.** A decision that returns `allow` with a step-up obligation is not an allow until the obligation is satisfied. The caller cannot ignore an obligation; the authorization service exposes `AuthorizeAsync` and `AuthorizeAndRecordAsync`, and the latter rejects the action if the obligation is unmet. A permit without its obligations is a bug, and the contract test suite asserts that every sensitive endpoint uses the recording variant.

### 5.13 UI Projection

The frontend needs to know what to render. It does not get to decide what is allowed.

| Endpoint | Returns | Cache | Authority |
|----------|---------|-------|-----------|
| `GET /me/permissions?org_id=` | Effective permission keys, scope summary, obligations, `scope_version` | `ETag` equal to `scope_version`; 60-second client cache | Advisory only |
| `GET /me/workspaces` | Home organization plus active grant workspaces | 60 seconds | Advisory only |
| `GET /me/navigation` | Menu and route visibility derived from permissions | 60 seconds | Advisory only |

**Rules.**

1. Hiding a button is a usability decision, not a security control. Every action is authorised again on the server.
2. The permission projection contains no other user's permissions and no role internals.
3. A `scope_version` change invalidates the projection on the next request; the client refetches and re-renders.
4. A route guard that fails shows a `403` page with a support reference, not a blank screen.
5. Disabled actions state the missing permission in human terms ("Your role cannot approve expenses in this show"), not the raw key.

### 5.14 Traceability for Section 5

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| FR-TNT-010 | Sections 5.5, 5.6 | System role immutability; custom role versioning; 60-second propagation |
| FR-TNT-011 | Section 5.3, Appendix A | Closed catalogue, unique keys, unknown key rejected at build and runtime |
| FR-TNT-012 | Sections 5.5, 5.6, 5.8 | Least-privilege seeds, permission selection, SoD at assignment |
| FR-TNT-013 | Section 5.4, section 6 | Seven scope types; unassigned scope returns 403 |
| FR-TNT-014 | Sections 5.11, 5.12, 6.8 | Every endpoint declares a permission; negative tests |
| FR-TNT-020 | Section 5.9 | Delegation with dates, amount, attribution and revocation |
| FR-TNT-021 | Section 5.10 | Quarterly campaigns, certification, orphan suspension |
| NFR-SEC-004 | Sections 5.12, 6.1 | RBAC plus RLS plus request context in one decision |
| NFR-PRV-003 | Sections 5.8, 5.9, 5.10 | SoD, approval limits, access review evidence |
| BR-ACC-004 | Section 5.8 | Self-approval hard predicate |
| BR-ACC-006 | Section 5.8 | SoD matrix and exception register |
| CAP-003 | Sections 5.2 to 5.13 | Role-based access control capability |

---

## 6. Effective Permission Resolution

### 6.1 The Resolution Pipeline

Every request resolves authority through the same twelve steps, in the same order. The order matters: a cheaper check that fails early prevents a more expensive one, and a veto that runs after a permit can still deny.

| Step | Question | Source | Failure |
|------|----------|--------|---------|
| 1 | Is the token structurally valid, signed with a live key, and unexpired? | JWT validation | `401 UNAUTHENTICATED` |
| 2 | Is the session active, unrevoked, within idle and absolute bounds? | Session registry | `401 SESSION_EXPIRED` |
| 3 | Is the token's `scope_version` current for the user? | User authority record | `401 STALE_TOKEN`, client refreshes |
| 4 | Is the acting organization `ACTIVE`? | `tenancy.organization` | `403 TENANT_SUSPENDED` |
| 5 | Is the principal `ACTIVE` and permitted to act? | `identity.user`, membership or grant | `403 PRINCIPAL_INACTIVE` |
| 6 | Which authority source applies: membership, grant or support access? | Session + acting org | `403 NO_AUTHORITY` |
| 7 | Does the source contain the requested permission? | Role permissions or grant profile | `403 FORBIDDEN` |
| 8 | Does the requested object fall inside the source's scope? | Scope containment | `403 OUT_OF_SCOPE` |
| 9 | Does any SoD rule veto this actor and action on this object? | SoD rules and exceptions | `403 SEGREGATION_OF_DUTIES` |
| 10 | For an approval, is the amount within the effective limit, including delegation? | Approval limits, delegation | `403 LIMIT_EXCEEDED` |
| 11 | Is the module entitled for this tenant? | Entitlements | `403 MODULE_NOT_ENABLED` |
| 12 | Is a step-up obligation outstanding? | Session strong-auth record | `403 STEP_UP_REQUIRED` |

**Two independent gates.** The policy decision point answers "may this principal perform this action on this object". PostgreSQL row-level security answers "may this database role see this row in this transaction". Both must pass, and neither is allowed to assume the other succeeded. A PDP bug must not become a data leak; an RLS policy gap must not become an authorization bypass. Section 7 defines the RLS half.

**The object must be identified before the decision.** For a single-object request, the handler resolves the object's `org_id`, `show_id`, `season_id`, `episode_id`, `department_id` and `assignment_id` from the database before calling the PDP. It does not trust those identifiers from the request body. For a list request, the PDP returns a scope predicate that the query applies, and RLS applies again underneath.

### 6.2 Authorization Context

| Field | Source | Rule |
|-------|--------|------|
| `actor_user_id` | `sub` claim | Must match an active identity |
| `actor_org_id` | `identity.user.home_org_id` | Resolved server-side; never from the token |
| `tenant_org_id` | `org` claim | Must equal the organization being addressed; a mismatch is rejected |
| `authority_source` | Session record | `MEMBERSHIP`, `GRANT` or `SUPPORT`; exactly one |
| `authority_id` | Membership ID, grant ID or support access ID | Used for audit and revocation |
| `permission_set` | Expanded from roles or grant profile | Cached under `scope_version` |
| `scope_set` | Assignments or grant scope | Intersected, never unioned, across layers |
| `scope_version` | Token and user record | Must match, or the request is rejected as stale |
| `session_id`, `device_id` | Token and session | Bound to the decision for audit |
| `amount`, `currency` | Request, validated against the object | Only for approval actions |
| `resource` | Resolved object reference | Type plus identifiers, from the database |

**The client never supplies authority.** It may supply a resource identifier, a requested page size and an action payload. It may not supply `org_id` as an authorization input; if an `org_id` appears in a path, it is compared with the token's `org` and a mismatch is a `403` (same tenant) or `404` (different tenant), never a context switch. FR-TNT-018's prohibition on client-supplied tenant override is implemented here, and again in RLS.

### 6.3 Authority Source Selection

| Condition | Authority source | Roles consulted | Grants consulted | Notes |
|-----------|------------------|-----------------|------------------|-------|
| Active membership in the acting org | `MEMBERSHIP` | Yes | No | The normal path for members |
| No membership, active grant to the acting org | `GRANT` | No | One grant only | External auditor path |
| No membership, active support access to the acting org | `SUPPORT` | No | No | Break-glass, read-only by default |
| Membership suspended but grant active | `GRANT` | No | Yes | The membership is not silently bypassed; the grant is a separate authority |
| Membership active and grant active | `MEMBERSHIP` | Yes | No | A member does not accumulate grant permissions; the grant is dormant while membership exists |
| No membership, no grant, no support access | None | No | No | `403 NO_AUTHORITY` |

**The exclusivity rule.** A principal's authority for a given tenant comes from exactly one source. This is not an optimisation; it is the rule that prevents privilege accumulation. If memberships and grants could combine, a user with a narrow membership and a narrow grant could reach a scope that neither alone allows, and neither the tenant administrator nor the auditor's firm would see the combined authority. One source means one reviewable record.

**Entering a grant workspace.** An external auditor with grants to three clients sees three workspaces. Selecting one mints a new access token with `org` equal to that client and `authority_id` equal to the selected grant. The session record is updated to `GRANT` mode for that `org`. The auditor cannot hold two grant workspaces active in one token; switching is explicit and audited. A token minted for client A cannot be replayed against client B because the `org` claim and the RLS context both name A.

**Membership wins, and that is deliberate.** A platform engineer who is also a member of a customer tenant is a conflict of interest that should not exist. If it does, the membership path applies and the engineer's support access is not additionally available. The alternative, letting support access layer on top of membership, would let an operator silently exceed their membership scope.

### 6.4 Permission Expansion

```
resolve_permissions(principal, org):
    if authority_source == MEMBERSHIP:
        assignments = active_assignments(principal, org, now)
        roles = distinct(assignments.role_id)
        permissions = union(role_permissions(role) for role in roles)
    elif authority_source == GRANT:
        grant = active_grant(principal, org, now)
        permissions = grant_profile(grant.profile) - grant.permissions_denied
    elif authority_source == SUPPORT:
        permissions = support_access_permissions(principal, org, now)
    else:
        return EMPTY
    return permissions
```

**Role version is part of the cache key.** A role edit creates a new version; assignments continue to reference the role, and permission expansion reads the current version. The cached expansion is keyed by `(role_id, role_version)`, so an edit produces a new key rather than requiring an eager purge of every affected user. The user's `scope_version` is incremented in the same transaction, which invalidates the decision cache and forces a refresh of the access token within 60 seconds.

**Unions and intersections.** Permissions from multiple assignments are unioned; scopes from multiple assignments are unioned **within the same authority source**, then the result is intersected with the object's containment. A user with `SHOW` scope on S1 and `SHOW` scope on S2 can read both. A user with `SHOW` S1 and `DEPARTMENT` d1 (which belongs to S1) still cannot read S2. The intersection is applied per object, not once globally, because a global intersection of unrelated scopes would produce an empty set and break legitimate multi-show assignments.

**Deny is absence plus veto.** There is no `deny` permission in the catalogue. A permission is denied when it is absent, when the object is out of scope, or when a veto applies. Adding explicit deny rules would create ordering problems ("does deny beat allow?") and make the effective set impossible to explain. The SoD veto is the one explicit negative, and it is modeled as a separate rule rather than as a negative permission.

### 6.5 Object Containment

| Object | Required scope evidence | Containment test |
|--------|------------------------|------------------|
| `production.show` | `show_id` | `scope = ORG` or `SHOW` containing the show |
| `production.season` | `show_id`, `season_id` | `ORG`, `SHOW(show)`, or `SEASON(season)` |
| `production.episode` | `show_id`, `season_id`, `episode_id` | `ORG`, `SHOW`, `SEASON`, or `EPISODE` |
| `production.department` | `show_id`, `department_id` | `ORG`, `SHOW(show)`, or `DEPARTMENT(department)` |
| `budget.budget` | `org_id`, `show_id`, optional `season_id`, `department_id` | The budget's most specific scope must be contained |
| `expense.expense` | `org_id`, `show_id`, `department_id`, optional `episode_id` | All present dimensions must be contained |
| `vendor.vendor` | `org_id`, optional linked `show_id` | `ORG`, or `SHOW` when the vendor is show-linked |
| `docs.document` | `org_id` plus the owning entity's scope | The owning entity's containment applies; documents do not float free of their subject |
| `auditops.audit_assignment` | `org_id`, `assignment_id`, `show_id`, date range | `ASSIGNMENT(assignment)` inside an active grant, or `ORG`/`SHOW` for a member auditor |
| `auditops.audit_finding` | `org_id`, `assignment_id`, optional `show_id` | Assignment containment |
| `identity.user` | `user_id` | `SELF` for own profile; `ORG` for administration |
| `tenancy.role` | `org_id` | `ORG` only |
| `report.report_run` | `org_id`, scope of the report definition | The report definition's scope, narrowed by the runner's scope |
| `compliance.audit_event` | `org_id` | `ORG` read for Compliance Officer and Audit Manager; no write for any tenant role |

**Most-specific-scope rule.** When an object carries several scope dimensions, the actor must be contained by at least one applicable scope path, and every dimension that the object declares must be covered by that path. An expense coded to show S1 and department d1 is not reachable by a `DEPARTMENT` assignment for d1 in show S2. The containment test resolves the path, not just the identifiers.

**Aggregate read models.** A dashboard row that aggregates across shows must be filtered to the actor's scope, and the filter is applied before aggregation. Computing a global aggregate and then redacting rows leaks the redacted values through totals. This is a correctness rule for the query layer, tested by a negative test that compares a scoped total with the sum of the visible rows.

**SELF is not a wildcard.** `SELF` permits reading and updating the actor's own profile, preferences, sessions, devices, own expense drafts and own delegations. It never permits approving the actor's own expense, reading another user's profile, or exporting an organization report. The self-approval prohibition overrides any `SELF` permission.

### 6.6 Grant Resolution

The external path has additional gates because the principal has no membership, no role and no directory presence in the client organization.

```text
authorize_grant(principal, org, permission, resource):
    grant = grant_for(principal, org)
    require grant.status == ACTIVE
    require grant.start_at <= now <= grant.end_at
    require grant.acceptance_complete          # MFA, NDA, COI
    require grant.nda_version == current_nda
    require grant.conflict_state != DECLARED_UNRESOLVED
    require permission in grant_profile(grant.profile) - grant.permissions_denied
    require resource contained_by grant.scope
    require resource.show_id == grant.show_id  # for ASSIGNMENT scope
    return ALLOW with obligations [audit_level=HIGH]
```

| Gate | Failure code | Notes |
|------|--------------|-------|
| Grant not `ACTIVE` | `403 GRANT_INACTIVE` | `PENDING_ACCEPTANCE`, `SUSPENDED`, `REVOKED`, `EXPIRED` |
| Outside start or end date | `403 GRANT_EXPIRED` | Expiry is enforced at decision time, not only by a job |
| MFA not enrolled | `403 GRANT_ACCEPTANCE_INCOMPLETE` | Grant cannot activate without it |
| NDA not accepted or outdated | `403 GRANT_ACCEPTANCE_INCOMPLETE` | Version comparison, not just a boolean |
| Conflict declared and unresolved | `403 GRANT_CONFLICT_HOLD` | Requires client decision |
| Permission outside profile | `403 FORBIDDEN` | Profile is the ceiling |
| Resource outside scope | `403 OUT_OF_SCOPE` | |
| Assignment scope names another show | `403 OUT_OF_SCOPE` | The assignment must sit inside the grant |
| Grant revoked mid-session | `401 STALE_TOKEN` then `403 GRANT_INACTIVE` | `scope_version` bump forces refresh |

**Grants do not inherit from the firm.** An auditor's firm being a tenant does not give the auditor any authority over a client. The firm administrator can attest, suspend and deprovision, but cannot read client data and cannot create a client grant. The client's grant is the only path.

**One active grant per client per auditor.** If an auditor needs two profiles for the same client, the client issues two grants with non-overlapping scopes or a single grant with the broader profile. Overlapping grants for the same pair are rejected at creation because they make revocation ambiguous: revoking one leaves the other silently in force.

### 6.7 Approval Resolution

For an action with the `approve` verb, the permission and scope checks are necessary but not sufficient. The amount must be within the effective limit, and the actor must be independent of the record.

```text
authorize_approval(actor, org, permission, resource, amount):
    require authorize(actor, org, permission, resource).allow
    require actor != resource.created_by
    require actor != resource.submitted_by
    require actor != resource.beneficiary
    require not is_own_expense(actor, resource)
    role_limit = approval_limit(actor.role, resource.scope)
    personal_limit = min(role_limit, actor.personal_limit or role_limit)
    delegated = active_delegation(actor, resource.scope, now)
    effective = personal_limit
    if delegated:
        effective = min(effective, delegated.max_amount)
        require amount <= delegator.personal_limit
    require amount_base <= effective
    require amount_base <= org_ceiling
    return ALLOW with obligation record(on_behalf_of=delegated.delegator)
```

| Case | Result |
|------|--------|
| Amount within limit, actor independent | Allow; record the limit applied |
| Amount above limit | `403 LIMIT_EXCEEDED`; route to the next authority |
| Actor is creator, submitter or beneficiary | `403 SELF_APPROVAL`; no exception |
| Delegation expired | `403 DELEGATION_EXPIRED` |
| Delegation amount exceeded | `403 LIMIT_EXCEEDED`; route to the delegator |
| Delegator's own limit lower than the amount | `403 LIMIT_EXCEEDED` |
| Currency conversion unavailable | `503 FX_RATE_UNAVAILABLE`; fail closed, no approval on a guessed rate |

**The amount is resolved from the record, not the request.** An approver cannot approve a lower amount than the record states by sending a smaller number in the body. The handler compares the request amount with the persisted amount and rejects a mismatch. Approval is an act on the record as it exists at the decision.

**Delegation attribution is mandatory.** An approval made under delegation stores both `actor_user_id` (the delegate) and `on_behalf_of_user_id` (the delegator), plus the delegation ID. A report that shows only the delegate is incomplete; a report that shows only the delegator is false. Both appear in the audit record and in the approval history.

### 6.8 Vetoes

| Veto | Trigger | Overridable | Order |
|------|---------|-------------|-------|
| Tenant state | Organization not `ACTIVE` | No | Before permission |
| Principal state | Identity `SUSPENDED`, `DEPROVISIONED`, `LOCKED` or `ERASED` | No | Before permission |
| Session state | Revoked, idle-expired, absolute-expired or stale `scope_version` | No | Before permission |
| Authority source | None available for the acting org | No | Before permission |
| SoD | Prohibited permission combination for the same scope | Only through a recorded exception, where the rule is conditional | After scope, before limit |
| Self-approval | Actor is creator, submitter or beneficiary | No | After permission |
| Approval limit | Amount above the effective limit | No; escalation instead | After SoD |
| Entitlement | Module not enabled | No | After scope |
| Step-up | Strong authentication older than the action class allows | Satisfiable, not overridable | Last |
| Legal hold | Not an authorization veto; blocks deletion, not access | Not applicable | Separate domain rule |

**Ordering rationale.** State and session checks run first because they are cheap and because a suspended tenant must not leak whether a permission exists. SoD runs after scope because the rule is scoped; applying it globally would deny legitimate cross-show work. Limit checks run after SoD because a limit breach should be escalated, while an SoD breach is a control failure. Step-up runs last so the user is not challenged for an action that would be denied anyway.

**Entitlements are not permissions.** A tenant with the audit module disabled cannot use an audit permission even if a role contains it. The entitlement check is a tenant-level gate that produces a distinct code, so support can tell the difference between "your role lacks this" and "your plan does not include this".

### 6.9 Worked Examples

**Example 1 — Department head approves an expense.**

| Layer | Value |
|-------|-------|
| Principal | `user_1042`, member of production house `org_PH1` |
| Role | `DEPARTMENT_HEAD`, permission `expense.expense.approve`, limit 50,000 INR per transaction |
| Assignment | `DEPARTMENT` scope, `department_id = d_post_production`, show `S_ALPHA` |
| Resource | Expense `e_7781`, show `S_ALPHA`, department `d_post_production`, amount 42,000 INR, submitted by `user_1099` |
| Decision | Steps 1 to 8 pass; SOD-01 checks the actor is not creator or submitter and passes; limit 42,000 ≤ 50,000; entitlement enabled; step-up performed 4 minutes ago |
| Result | `ALLOW`, obligation `record_limit(50000, INR)` |

If the same approver attempts an 80,000 INR expense in the same department, the result is `403 LIMIT_EXCEEDED` with a route to the Finance Head. If the approver is the submitter, the result is `403 SELF_APPROVAL` regardless of amount.

**Example 2 — Accounts executive creates and tries to approve.**

| Layer | Value |
|-------|-------|
| Principal | `user_2011`, `ACCOUNTS_EXECUTIVE` |
| Permissions | `expense.expense.create`, `expense.expense.update`, `expense.expense.submit`; no `approve` |
| Action | `expense.expense.approve` on `e_8100` |
| Decision | Step 7 fails: permission absent. The SoD rule is not reached because the permission is already absent |
| Result | `403 FORBIDDEN` |

If the tenant creates a custom role that adds `approve` to the executive's role, the assignment is blocked by SOD-01 at assignment time unless an exception is recorded. If an exception is recorded, the decision-time check still blocks self-approval but allows approval of another user's expense within the limit. That is the intended shape: the exception relaxes the combination, not the self-dealing prohibition.

**Example 3 — Finance head with show scope crosses a show boundary.**

| Layer | Value |
|-------|-------|
| Principal | `user_3005`, `FINANCE_HEAD`, assignment `SHOW` scope for `S_BETA` |
| Request | Expense `e_9001` in `S_GAMMA`, same tenant |
| Decision | Step 8 fails: `S_GAMMA` is not contained by `SHOW(S_BETA)` |
| Result | `403 OUT_OF_SCOPE` |

If `e_9001` belongs to another tenant and the caller supplies its identifier, the handler's tenant resolution returns `404 NOT_FOUND` and emits `CrossTenantAccessAttempt`. The distinction between `403` and `404` is deliberate and is tested per endpoint.

**Example 4 — External auditor with an assignment-scoped grant.**

| Layer | Value |
|-------|-------|
| Principal | `user_7003`, member of audit firm `org_AF9`, no membership in `org_OTT2` |
| Grant | `g_55`, profile `AUDIT_FINANCE`, scope `ASSIGNMENT(a_12)`, show `S_DELTA`, start 2026-10-01, end 2026-12-31, NDA v3 accepted, COI clear |
| Request 1 | Read document `doc_441` attached to expense in `S_DELTA`, assignment `a_12` |
| Decision 1 | Grant active, profile contains `document.read`, containment passes, acceptance complete, step-up valid |
| Result 1 | `ALLOW` with `audit_level=HIGH` |
| Request 2 | Read the same document after the grant end date |
| Result 2 | `403 GRANT_EXPIRED`; the expiry job may not have run, but the decision does not depend on it |
| Request 3 | List roles in `org_OTT2` |
| Result 3 | `403 FORBIDDEN`; no grant profile contains `tenancy.role.read` |
| Request 4 | Read a document in `S_EPSILON` |
| Result 4 | `403 OUT_OF_SCOPE`; the assignment is bound to `S_DELTA` |

**Example 5 — Delegated approval.**

| Layer | Value |
|-------|-------|
| Delegator | `user_4001`, Finance Head, personal limit 500,000 INR, scope `ORG` |
| Delegate | `user_4002`, `FINANCE_HEAD` role, personal limit 200,000 INR |
| Delegation | 2026-11-01 to 2026-11-30, maximum 100,000 INR per transaction, scope `SHOW(S_ALPHA)`, reason "year-end close" |
| Request | Approve 90,000 INR in `S_ALPHA` |
| Decision | Permission present, scope contained, no SoD conflict, delegation active, 90,000 ≤ min(200,000, 100,000) and ≤ delegator 500,000 |
| Result | `ALLOW`, record `on_behalf_of = user_4001`, delegation ID |
| Request | Approve 120,000 INR in `S_ALPHA` |
| Result | `403 LIMIT_EXCEEDED`; the delegation cap is the binding limit, not the delegate's own 200,000 |

**Example 6 — Service account attempts an approval.**

| Layer | Value |
|-------|-------|
| Principal | `svc_import_01`, permission set `excel.import_job.create`, `expense.expense.create`, `vendor.vendor.read` |
| Request | `expense.expense.approve` |
| Decision | Permission absent; additionally the service principal is barred from every `approve` action by a catalogue assertion |
| Result | `403 FORBIDDEN` |

**Example 7 — Self-service profile update.**

| Layer | Value |
|-------|-------|
| Principal | `user_1042` |
| Request | Update own phone and timezone |
| Decision | `SELF` scope contains `user_1042`; permission `identity.user.update` held at `SELF` scope |
| Result | `ALLOW`; audit records the changed fields but not the values |

**Example 8 — Break-glass read.**

| Layer | Value |
|-------|-------|
| Principal | `user_9001`, `SUPPORT_ENGINEER`, `SUPPORT` authority for `org_OTT2` |
| Support access | `sa_77`, ticket `INC-4412`, two approvers, expires in 45 minutes, read-only |
| Request | Read expense `e_5510` in `org_OTT2` |
| Decision | Support access active, permission in the read-only support set, scope `ORG`, no SoD, audit level `CRITICAL` |
| Result | `ALLOW`; tenant administrators are notified; the action appears in the tenant audit as `SlateIQ Support` |
| Request | Change a role assignment |
| Result | `403 FORBIDDEN`; support permissions exclude every write to `tenancy.role` and `tenancy.assignment` |

### 6.10 Caching and Invalidation

| Cache | Key | TTL | Invalidation | Failure behaviour |
|-------|-----|-----|--------------|-------------------|
| Token validation keys | `kid` | 1 hour | Key rotation event | Refetch JWKS; unknown `kid` is rejected |
| Session state | `session_id` | 30 seconds | Revocation writes through | Read the session table; never fail open |
| Current `scope_version` | `user_id` | 30 seconds | Write-through on increment | Read the user record |
| Role expansion | `(role_id, role_version)` | 10 minutes | New version produces a new key | Read role permissions |
| Effective decision | `(user_id, org_id, scope_version)` | 60 seconds | Version bump | Recompute |
| Grant state | `(grant_id, updated_at)` | 30 seconds | Grant lifecycle event | Read the grant |

**The stale-token check is the revocation guarantee.** The token carries the `scope_version` at mint time. On every request the middleware compares it with the current value, which is cached for at most 30 seconds. If the token is older, the request returns `401 STALE_TOKEN`; the client refreshes, and the refresh reads the new authority. This is how a role change reaches an existing session within 60 seconds even though the access token is valid for 15 minutes.

**Cache partitioning.** The in-process cache is partitioned by `org_id` and the key includes it, so an entry created for tenant A can never be read by a request for tenant B. A shared cache, if introduced later, must use the same partition key and must not be keyed by permission alone. The current design uses in-process memory only, which removes the shared-cache poisoning class entirely.

**Negative caching.** A denial is cached for 30 seconds, except a cross-tenant `404`, which is not cached at all: the event is high severity and the next request should re-evaluate. Caching a `403` briefly protects against a client retry loop without extending the life of a stale denial after an access grant.

**Stampede control.** When a version bump invalidates a hot key, the first request recomputes and the rest wait on a single-flight promise. Without it, a role change in a large tenant produces a burst of identical queries.

### 6.11 Failure Modes

| Failure | Behaviour | Why |
|---------|-----------|-----|
| PDP exception | Deny, `500 AUTHORIZATION_UNAVAILABLE`, P1 alert | Fail closed; an allow on error is an outage that looks like a breach |
| Redis unavailable | Fall back to the session table and user record | Availability without weakening the decision |
| Database unavailable | `503`, no decision | There is no cached decision that can be trusted when the source of truth is unreachable |
| JWKS endpoint unavailable | Use the cached key set until its TTL; reject unknown `kid` | A cached key is safe; an unverified token is not |
| Entitlement service unavailable | Deny module-gated actions; allow core actions whose entitlement is cached | A billing outage must not stop expense capture |
| Clock skew | Reject tokens with `iat` more than 60 seconds in the future; allow 30 seconds of skew on `exp` | Bounded tolerance, not unlimited |
| Unknown permission key | Deny and emit `UnknownPermission` | A typo must not become a wildcard |
| Missing scope identifier | Deny and emit `MalformedScope` | Never default to `ORG` |

**No degraded authorization mode.** There is no "authorization bypass for maintenance" flag, no environment variable that disables the PDP, and no debug role. The only path to elevated access is a recorded support access or a recorded SoD exception, both with expiry and both audited. A system that can be made to skip authorization in an emergency will eventually be made to skip it for convenience.

### 6.12 Test Strategy

| Test class | What it proves | Example |
|------------|----------------|---------|
| Endpoint matrix | Every endpoint declares a permission and rejects an unauthorised caller | `POST /expenses/{id}/approve` returns `403` for `ACCOUNTS_EXECUTIVE` |
| Role matrix | Every seeded role has the expected allow and deny set | `FINANCE_HEAD` can approve an expense in scope, `MANAGEMENT_VIEWER` cannot |
| Scope matrix | Containment works at every boundary | `SHOW(S1)` cannot read `S2`; `EPISODE(e1)` cannot read `e2` |
| Tenant matrix | No role in tenant A can read anything in tenant B | Run as `slateiq_app`, assert zero rows and `404` |
| SoD matrix | Every hard rule blocks at assignment and at decision | `SOD-01` blocks self-approval and creator approval |
| Limit matrix | Amount boundaries are inclusive and currency-safe | 50,000 allows, 50,001 escalates |
| Delegation matrix | Dates, amount, scope and self-delegation | Expired delegation returns `403` |
| Grant matrix | Every acceptance gate and expiry path | Missing NDA keeps the grant inactive |
| Property tests | Monotonicity and non-escalation | Adding a permission never removes an allow; narrowing scope never widens |
| Fuzzing | Malformed scope and object identifiers | Random UUIDs never produce a `500` or a data row |

**Coverage requirement.** Every endpoint has at least one positive test, one negative permission test and one cross-tenant test. A new endpoint without the three fails the build. The matrix is generated from the endpoint manifest, so an undocumented endpoint is itself a failure.

**Decision table snapshot.** The expected decisions for the seeded roles against the core endpoints are committed as a fixture. A change to a role, a permission or a default scope that alters the fixture requires an explicit review, which makes accidental privilege creep visible in the diff.

### 6.13 Traceability for Section 6

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| FR-TNT-013 | Sections 6.4, 6.5 | Scope resolution and containment per resource |
| FR-TNT-014 | Sections 6.1, 6.12 | Pipeline plus endpoint matrix |
| FR-TNT-015 | Section 6.6 | Grant is the only external path; mandatory scope and dates |
| FR-TNT-016 | Sections 6.6, 6.8 | Acceptance gates and revocation latency |
| FR-TNT-018 | Sections 6.2, 7 | Token-bound tenant context; client override rejected |
| NFR-SEC-004 | Sections 6.1, 6.11 | PDP plus RLS plus context; fail-closed behaviour |
| NFR-SEC-005 | Section 6.12 | Tenant matrix and negative tests per release |
| BR-ACC-003 | Sections 6.10, 4.9 | Stale-token check and version invalidation |
| BR-ACC-004 | Section 6.7 | Self-approval predicate |
| BR-AUD-002 | Section 6.6 | One client, one grantee, one grant |
| CAP-003 | Sections 6.1 to 6.12 | Authorization capability |

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

The table carries `org_id` plus one or more of `show_id`, `season_id`, `episode_id`, `department_id`. Examples: `prod.show`, `budget.budget`, `expense.expense`, `docs.document`, `auditops.audit_finding`.

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
| `docs.document`, `docs.document_version` | Yes | Yes (evidence only) | No | No |
| `auditops.audit_finding` | Yes | Yes | Yes (own findings, pre-submit) | No |
| `auditops.audit_evidence` | Yes | Yes | No | No |
| `auditops.audit_report` | Yes | No | No | No |
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

The complete inventory is Appendix D. The summary below shows the families and the number of tables each covers at the logical-model level; Document 8 maps them to physical tables.

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

---

## 8. Cross-Organization Access Grants

### 8.1 What a Grant Is, and What It Is Not

A grant is the only mechanism by which a person who is not a member of an organization can see that organization's data. It is a client-issued, time-bound, scope-bound, individually revocable authorisation. It is not a role, not a membership, and not a partnership between tenants.

| A grant **is** | A grant **is not** |
|----------------|--------------------|
| Issued by one client organization to one named human | Issued to a firm, a team, a group or an email domain |
| Bounded by a grant profile (the "permission level" of FR-TNT-015) | A role that can be edited, extended or composed |
| Bounded by an explicit scope and a start and end date | Open-ended, renewable by default, or self-extending |
| Accepted by the grantee with MFA, NDA and conflict declaration | Activated by the client alone |
| Revocable by the client at any time, without the grantee's consent | Revocable only by the firm that employs the auditor |
| Recorded as its own object with its own audit trail | An entry in the auditor's role assignments |
| Stored in the client's region | Copied, cached or mirrored into the firm's region |

**Why a separate object.** The archived draft modelled external access as a role assignment with a flag, which produced two defects: the client could not see the assignment in its own directory, and the auditor accumulated permissions across engagements. A grant lives in the client's tenant, references the grantee by `user_id` without creating a membership, and dies on its own schedule. The client's grant register is therefore a complete answer to "who outside this organization can see our data", which is the question an auditor, a DPO and an incident responder all ask first.

**Why one grantee per grant.** BR-AUD-002 requires exactly one client organization. This document adds exactly one grantee. A firm-wide grant cannot be reviewed per person, cannot be revoked for the one auditor who left, and cannot name the individual accountable for an export. Five auditors on an engagement means five grants and five revocations, which is the correct amount of administrative work for the risk.

### 8.2 Grant Lifecycle

Eight states, one terminal outcome per path. The state machine is enforced in the service layer and mirrored by a database trigger, so a direct update cannot skip a gate.

| State | Meaning | Data readable | Sessions | Exits to |
|-------|---------|---------------|----------|----------|
| `DRAFT` | Being prepared; not yet submitted | No | None | `PENDING_APPROVAL`, deleted |
| `PENDING_APPROVAL` | Submitted; awaiting the second person | No | None | `PENDING_ACCEPTANCE`, `DECLINED` |
| `PENDING_ACCEPTANCE` | Approved; awaiting the grantee's gates | No | None | `ACTIVE`, `DECLINED` |
| `ACTIVE` | In force | Yes, within profile and scope | Grant workspace only | `SUSPENDED`, `REVOKED`, `EXPIRED` |
| `SUSPENDED` | Temporarily frozen | No | Revoked | `ACTIVE`, `REVOKED`, `EXPIRED` |
| `REVOKED` | Withdrawn by the client or the platform | No | Revoked | Terminal |
| `EXPIRED` | End date reached | No | Revoked | Terminal |
| `DECLINED` | Approver or grantee refused, or acceptance window lapsed | No | None | Terminal |

| Transition | Trigger | Actor | Guards | Side effects |
|------------|---------|-------|--------|--------------|
| `DRAFT` to `PENDING_APPROVAL` | Submit | Creator with `audit.access_grant.create` | Profile, scope, dates and owner present; grantee attested and active; no overlapping grant | `GrantSubmitted`; approver candidates notified |
| `DRAFT` to deleted | Discard | Creator | Never submitted | Physical delete; `GrantDraftDiscarded` |
| `PENDING_APPROVAL` to `PENDING_ACCEPTANCE` | Approve | A different human with `audit.access_grant.approve` and step-up | SOD-07 satisfied; Finance Head co-approval when profile is `AUDIT_FINANCE`; client region acknowledged when regions differ | `GrantApproved`; acceptance invitation sent to the grantee; 14-day acceptance window starts |
| `PENDING_APPROVAL` to `DECLINED` | Decline | Approver | Reason of at least 20 characters | `GrantDeclined`; creator notified |
| `PENDING_ACCEPTANCE` to `ACTIVE` | All gates complete and start date reached | Grantee | MFA confirmed; current NDA accepted; COI declared clear or resolved; cross-region acknowledgment where required; start date not in the future beyond 24 hours | `GrantAccepted`, `GrantActivated`; workspace available; grantee and firm admin notified |
| `PENDING_ACCEPTANCE` to `DECLINED` | Grantee declines, or window lapses | Grantee or system | Window is 14 days from approval | `GrantDeclined`; no data was ever readable |
| `ACTIVE` to `SUSPENDED` | Client suspension, firm deprovisioning, identity suspension, NDA supersession, unresolved conflict, platform security action | Client approver, firm admin, or system | Reason code mandatory | `GrantSuspended`; sessions revoked; workspace closed; exports cancelled |
| `SUSPENDED` to `ACTIVE` | Resume | Client approver plus fresh approval for security suspensions | Triggering condition cleared; NDA current; COI clear; dates still valid | `GrantResumed`; new acceptance record; new workspace session |
| `ACTIVE` to `REVOKED` | Client revokes, platform revokes, engagement ends | Client approver or Platform Security | Reason mandatory; no grantee consent needed | `GrantRevoked`; cascade in section 8.8 |
| `ACTIVE` or `SUSPENDED` to `EXPIRED` | `end_at` reached | System, evaluated at decision time and by job | None | `GrantExpired`; cascade in section 8.8 |
| `REVOKED`, `EXPIRED`, `DECLINED` | Any further transition | — | Rejected by trigger | — |

**Invariants.**

1. A grant in any state other than `ACTIVE` confers no authority. Section 6.6 enforces this at decision time; the job that flips the state is a convenience, not the control.
2. A grant never widens. Profile and scope are fixed at approval; every later change either narrows immediately or produces a new approval.
3. A grant never converts into a membership. There is no "convert to user" action, because that would move an external principal into the client's directory without an invitation.
4. A grantee with an `ACTIVE` grant to organization A and another to organization B holds one workspace token at a time. Switching is explicit and audited.
5. Deleting a `DRAFT` is the only physical delete in the lifecycle. Every other state is retained for seven years.

### 8.3 Creation and Approval

**Who may create.** FR-TNT-015 names "Client Administrators or Audit Managers". In the role catalogue these are `COMMISSIONING_ADMIN` (OTT platform and broadcaster), `PRODUCTION_ADMIN` (production house) and `AUDIT_MANAGER` (all data organizations). This section refines the authority summary in section 5.5 with the complete grant-permission matrix; Appendix B carries the same matrix per role.

| Role | `audit.access_grant.create` | `.approve` | `.delete` | `.read` | Notes |
|------|------------------------------|-----------|----------|---------|-------|
| `COMMISSIONING_ADMIN` | Yes | Yes | Yes | Yes | Cannot approve a grant they created (SOD-07) |
| `AUDIT_MANAGER` | Yes | No | Yes | Yes | Creates and revokes; never approves |
| `PRODUCTION_ADMIN` | Yes | Yes | Yes | Yes | Production houses only; same SOD-07 restriction |
| `FINANCE_HEAD` | No | Co-approval for `AUDIT_FINANCE` | No | Yes | Co-approval recorded separately from the approver |
| `ORG_ADMIN` | No | No | No | Yes | Sees the register for access review; cannot confer external access |
| `ACCESS_REVIEWER` | No | No | No | Yes | Certifies, cannot create |
| `AUDIT_FIRM_ADMIN` | No | No | No | Yes, own firm's grants | Can suspend its own auditor, which cascades |
| `PLATFORM_SECURITY` | No | No | Yes, emergency | Yes, all | Platform-level revocation with a ticket |

**Revocation uses `.delete`.** The permission action set is closed and contains no `revoke` verb (Document 6 Appendix C.1). `audit.access_grant.delete` therefore means "terminate this authority early"; the grant row is state-transitioned to `REVOKED`, not physically deleted, because section 8.14 retains it for seven years. Discarding a draft uses the same permission and is the only physical delete.

**Two-person rule.** The creator and the approver are different humans, enforced by `created_by <> approved_by` and by SOD-07. A single administrator in a small production house cannot self-approve; the platform requires a second person with the permission, and if none exists the grant cannot be created. That is an intended dead end: an organization with one administrator and no second approver has no independent control over external access, and inventing one by allowing self-approval would make the control fictional.

**Creation form.** The fields below are mandatory; the form cannot submit without them.

| Field | Rule |
|-------|------|
| `grantee_user_id` | An active human in an `AUDIT_FIRM` tenant, attested by that firm, with a confirmed MFA factor |
| `firm_org_id` | Derived from the grantee; the client cannot name an arbitrary firm |
| `owner_user_id` | The accountable business owner; defaults to the creator, may be another member with `audit.audit_assignment.read` |
| `profile_key` | One of `AUDIT_READ`, `AUDIT_EVIDENCE`, `AUDIT_FINANCE` |
| `permissions_denied` | Optional subset of the profile, used to withhold specific data |
| `scope_type`, `scope_ids` | `SHOW` or `ASSIGNMENT`; `ORG` is prohibited for external principals |
| `start_at`, `end_at` | `end_at > start_at`; `end_at <= start_at + 12 months` (BR-AUD-002) |
| `engagement_reference` | The client's engagement, contract or appointment reference |
| `purpose` | Minimum 30 characters; appears on the register and in the workspace banner |
| `region_acknowledgment` | Required when `firm_region <> client_region` |

**Scope for external principals.** A grant may name one show or one audit assignment. It may not name the whole organization, because "everything" is not a scope an external party can be given for a limited engagement. `ASSIGNMENT` is the preferred scope for statutory audits: it carries the show and the engagement date range, so the grant narrows itself when the assignment closes.

**Profile selection guidance.** `AUDIT_READ` for a review that needs to look; `AUDIT_EVIDENCE` when the auditor must capture evidence and raise findings; `AUDIT_FINANCE` when cost, variance and vendor data are in scope. The client chooses, not the auditor. An auditor who asks for `AUDIT_FINANCE` for a compliance review is asking for data the engagement does not need, and the approval step is the place that conversation happens.

**Validation at creation, not at acceptance.** An invalid `show_id`, an unattested auditor, a profile outside the catalogue, a denied permission outside the profile, or an end date beyond twelve months fails the draft. Deferring these to acceptance would produce a grant that the auditor is invited to accept and cannot.

### 8.4 Acceptance Gates

FR-TNT-016 requires MFA enrollment, NDA acknowledgment and a conflict-of-interest declaration before activation. Each is a separate record with its own evidence, because a single boolean cannot answer "which NDA, when, from which address" during a dispute.

| Gate | Evidence record | Requirement | Failure behaviour |
|------|-----------------|-------------|-------------------|
| **Identity attested** | `tenancy.auditor_profile.firm_attested_by` | The firm administrator confirms the person is engaged by the firm and names the engagement | Grant cannot be created |
| **MFA enrolled** | `identity.mfa_factor` confirmed | A confirmed TOTP factor at the home organization | Grant stays `PENDING_ACCEPTANCE`; the acceptance page routes to enrollment |
| **NDA acknowledged** | `tenancy.grant_acknowledgment.nda_version`, timestamp, IP, user-agent hash | The current platform NDA version, or the client's own NDA version when the client supplies one | Grant stays `PENDING_ACCEPTANCE`; a superseded NDA version forces re-acceptance before activation |
| **Conflict declared** | `tenancy.coi_declaration` | The current declaration question set, answered | A declared conflict blocks activation and routes to the client |
| **Client approval** | `tenancy.access_grant.approved_by`, `approved_at` | A second human with the approve permission, plus Finance Head co-approval for `AUDIT_FINANCE` | Grant never leaves `PENDING_APPROVAL` |
| **Cross-region acknowledgment** | `cross_region_acknowledged_at` | Only when the firm's region differs from the client's region | Grant stays `PENDING_ACCEPTANCE` |

**Conflict declaration is not a checkbox.** The declaration asks whether the auditor, their firm, or an immediate household member has a financial interest in the client, a current or recent engagement with the client outside this grant, or a personal relationship with a person who can influence the subject of the audit. A "yes" is not an automatic refusal; it is a hold that requires the client's Audit Manager and Organization Administrator to record a decision with reasoning. BR-AUD-005 then blocks the conflicted show until the client resolves the conflict in writing.

| Conflict outcome | Grant state | Further action |
|------------------|-------------|----------------|
| No conflict declared | Proceeds to `ACTIVE` at the start date | Declaration recorded with version and timestamp |
| Conflict declared, client accepts in writing | `ACTIVE`, `conflict_state = RESOLVED_ACCEPTED` | Resolution text, resolver, timestamp stored; show-level block lifted only for the named scope |
| Conflict declared, client declines | `DECLINED` | Auditor and firm notified; no data was readable |
| Conflict declared, no decision within 14 days | Remains `PENDING_ACCEPTANCE`, then `DECLINED` at window expiry | Escalated to Platform Security on day 7 |
| Conflict declared after activation | `SUSPENDED` immediately | Client decides; reactivation requires a fresh approval |

**The acceptance window.** Fourteen days from approval. After that the grant is `DECLINED` with reason `ACCEPTANCE_WINDOW_EXPIRED`, and the client must create a new grant. An open-ended acceptance window is a standing authorisation waiting to be picked up by whoever gains access to the mailbox.

**Acceptance is per grant, not per auditor.** An auditor with grants to three clients accepts each one separately. The NDA evidence may be shared when the version is identical, but the act of acceptance, the IP address and the timestamp are per grant. This is what makes "the auditor accepted access to our data on this date" a true statement.

### 8.5 Activation

Activation is the moment authority begins. It is a transaction, not a status field updated by a scheduled job.

| Step | Action | Failure |
|------|--------|---------|
| 1 | Re-read the grant `FOR UPDATE` | Concurrent cancellation wins; activation aborts |
| 2 | Verify state is `PENDING_ACCEPTANCE` and approval is intact | Abort |
| 3 | Verify all acceptance gates and the NDA version at this instant | Stay `PENDING_ACCEPTANCE` |
| 4 | Verify `start_at <= now`; if `start_at` is future, schedule activation and stop | No early access |
| 5 | Verify `end_at <= activated_at + 12 months` and `end_at > now` | Abort with `GRANT_WINDOW_INVALID` |
| 6 | Verify no other grant for this client and grantee is `ACTIVE` or `SUSPENDED` | Abort; renew instead of overlapping |
| 7 | Write the grant version snapshot | — |
| 8 | Set `state = ACTIVE`, `activated_at`, `scope_version` bump for the grantee | — |
| 9 | Emit `GrantActivated`; notify client owner, client administrators, grantee and firm administrator | — |
| 10 | Open the workspace route and mint the first grant-bound token on demand | — |

**Activation does not create a session.** The auditor still authenticates at the home organization with password and TOTP, then enters the workspace. A grant is authority, not a credential; treating activation as a login would create a session nobody authenticated.

**Start dates in the future.** A grant approved on 1 November with a 1 December start is `PENDING_ACCEPTANCE` until the auditor accepts, then waits in `PENDING_ACCEPTANCE` with `accepted_at` set until 1 December. The state does not become `ACTIVE` early, and the decision in section 6.6 checks `start_at` independently, so a clock or job error cannot open access early.

**Immediate start.** A start date up to 24 hours in the past is accepted, because time zones and approval delays make "now" a fuzzy boundary. Beyond 24 hours, the grant is rejected rather than backdated; a backdated grant would create access that appears to predate its approval.

### 8.6 Variation

A grant changes in one of three ways, and only one of them is silent.

| Change | Classification | Approval | Acceptance | Effect |
|--------|----------------|----------|------------|--------|
| Narrow the scope (remove a show or an assignment) | Reduction | None | None | Immediate; `GrantVariationApplied` |
| Remove a permission via `permissions_denied` | Reduction | None | None | Immediate |
| Shorten `end_at` | Reduction | None | None | Immediate |
| Widen the scope (add a show) | Extension | New approval by a different human | New acceptance | Effective only after both |
| Change profile to a broader one | Extension | New approval plus Finance Head when the target is `AUDIT_FINANCE` | New acceptance | Effective only after both |
| Extend `end_at` within the 12-month ceiling | Extension | New approval | New acceptance | Effective only after both |
| Change the owner or purpose text | Administrative | None | None | Audited, no authority change |
| Change the grantee | New grant | Full | Full | The old grant is revoked; a new grant is created |

**Reduction never needs the grantee's consent.** Reducing another party's access cannot harm them, and requiring consent would let an auditor block the removal of access the client no longer wants to give. The client can narrow or shorten at any time, including during an engagement.

**Extension is a new decision, not an edit.** The grant is versioned: `tenancy.access_grant_version` stores an immutable snapshot at each change with the actor, reason and timestamp. A reviewer can reconstruct what the grant permitted on any date, which is what an access dispute or a regulator's question requires.

**The 12-month ceiling applies to the grant, not to the relationship.** BR-AUD-002 caps expiry at twelve months after activation. A three-year engagement therefore produces three grants, each approved and accepted. Renewal is covered in section 8.10.

**Denied permissions are additive only.** A variation may add to `permissions_denied`; removing an entry is a widening and follows the extension path. This asymmetry prevents a quiet restoration of access that the client had deliberately withheld.

### 8.7 Suspension

Suspension is the reversible stop. It exists because the correct response to a question is not always permanent removal: a conflict under review, a firm investigating an auditor, or a client pausing an engagement all need access to stop now and possibly resume later.

| Trigger | Initiated by | Automatic | Resume requires |
|---------|--------------|-----------|-----------------|
| Client pauses the engagement | Client approver | No | Client approver, fresh approval |
| Conflict declared or discovered | System | Yes | Client decision recorded, fresh approval |
| NDA version superseded | System | Yes | Acceptance of the current NDA |
| Auditor deprovisioned by the firm | System, via identity cascade | Yes | Not resumable by the firm; the client must create a new grant for a new person |
| Auditor identity `SUSPENDED` or `LOCKED` | System | Yes | Identity restored, then client approval |
| Client tenant suspended | System | Yes | Tenant reactivated |
| Platform security action | Platform Security with ticket | No | Platform Security plus client approval |
| Suspicious access pattern | System, threshold-based | Yes | Security review, then client approval |

**Suspension revokes sessions immediately.** The grantee's workspace session is revoked, the `sid` is denylisted, `scope_version` is incremented, and any queued export or download job tied to the grant is cancelled. A suspended grant that leaves a session alive is not suspended.

**A suspended grant is still visible.** It appears in the register with its reason and the identity of the suspender. Hiding it would let a suspension look like a revocation, and the client needs to know the difference when it decides whether to resume.

**Resumption is not automatic.** When the triggering condition clears, the grant does not silently return to `ACTIVE`. A human with approve authority confirms, and the grant gets a fresh acceptance record. Automatic resumption would mean a conflict declaration that was cleared by a system job could restore access without anyone deciding it should.

**Firm suspension versus client revocation.** When a firm deprovisions an auditor, every grant to that auditor is suspended and every client is notified. The client then decides whether to revoke formally. The firm cannot delete the client's record of the access, and the client cannot be forced to keep it active. Both parties retain a veto, which is what independence requires.

### 8.8 Revocation and Expiry

| Aspect | Revocation | Expiry |
|--------|------------|--------|
| Trigger | Client decision, engagement end, policy breach, platform action | `end_at` reached |
| Actor | Client approver, or Platform Security with a ticket | System |
| Grantee consent | Not required and not sought | Not applicable |
| Effective | Under 60 seconds; committed 5 minutes (BR-AUD-003) | At `end_at`, enforced at decision time |
| Sessions | Revoked, `sid` denylisted, `scope_version` incremented | Same |
| Workspace | Closed; open tabs receive `401 STALE_TOKEN` on the next request | Same |
| Exports | Queued and running export jobs cancelled; completed downloads already delivered are not retrievable | Same |
| Devices | No device action; the auditor's device belongs to the firm | Same |
| Record | Retained seven years with reason, actor and timestamp | Retained seven years |
| Reversible | No | Only by a new grant |

**The cascade is deliberately broad.** Revocation does more than flip a status, because a status flip leaves live tokens, cached permissions, running exports and open browser tabs. The cascade is:

1. Grant state to `REVOKED` with `revoked_by`, `revoked_at`, `revoke_reason`.
2. Every session with `authority_id = grant_id` revoked and denylisted.
3. `scope_version` incremented for the grantee.
4. Permission cache entries for `(grantee, client_org)` invalidated.
5. Workspace route closed; in-flight requests fail with `401 STALE_TOKEN` and then `403 GRANT_INACTIVE`.
6. Export and report jobs referencing the grant cancelled.
7. `GrantRevoked` emitted at high severity, with the reason and the actor.
8. Notifications to the client owner, client administrators, grantee and firm administrator.
9. An access-review item opened to confirm the removal.

**Expiry is enforced twice.** The decision in section 6.6 compares `now` with `end_at` on every request, so an expired grant stops working even if the expiry job is late. The job exists to change the state, emit the event and notify, not to be the control. A system whose expiry depends on a cron job has an outage window equal to the job's delay.

**Revocation is not deletion.** The grant record, its versions, its acknowledgments and its access history remain for seven years. Deleting them would destroy the evidence that access existed, which is the first thing an incident review or a regulator asks for.

**Emergency platform revocation.** Platform Security can revoke any grant without client approval when there is evidence of compromise or a legal order. The client is notified within 15 minutes, the action is recorded with a ticket reference, and it is reviewed by the monthly security review. The platform can stop access; it cannot grant it, and it cannot restore a grant the client revoked.

### 8.9 Expiry Notification

FR-TNT-017 requires owners to be notified 14 days before expiry and requires a report of active grants.

| Stage | When | Recipients | Channel | Content |
|-------|------|-----------|---------|---------|
| T-14 | 14 days before `end_at` | Owner, client administrators, grantee, firm administrator | Email and in-app | Grant, scope, profile, expiry date, renewal link |
| T-7 | 7 days before | Owner, client administrators | Email and in-app | Reminder; renewal status |
| T-1 | 1 day before | Owner, client administrators, grantee | Email and in-app | Final notice; access ends tomorrow |
| T-0 | At expiry | Owner, grantee, firm administrator | In-app and audit | `GrantExpired`; access ended |
| T+1 | 1 day after | Owner | Email | Confirmation that access ended; renewal instructions |

**Delivery is recorded.** `tenancy.grant_expiry_notice` stores the stage, recipients, send time and delivery result. A notice that failed to deliver is retried and then escalated to the tenant's notification failure queue. "We sent an email" is not evidence that the owner was warned.

**Renewal link.** The T-14 notice includes a one-click path to create a renewal draft pre-filled with the existing profile and scope. It does not extend the grant; it opens a new approval. Pre-filling is a convenience, not a decision.

**The register is the report.** FR-TNT-017's report is the grant register, filtered and exportable:

| Column | Source |
|--------|--------|
| Grant ID and state | `access_grant` |
| Client organization | `client_org_id` |
| Grantee and firm | `grantee_user_id`, `firm_org_id` |
| Profile and denied permissions | `profile_key`, `permissions_denied` |
| Scope type and identifiers | `scope_type`, `scope_ids` |
| Owner | `owner_user_id` |
| Approved by, approved at | `approved_by`, `approved_at` |
| Start, end, days remaining | `start_at`, `end_at`, computed |
| Acceptance evidence | NDA version, COI state, MFA confirmed |
| Last access | `last_access_at` |
| Expiry notices sent | `grant_expiry_notice` |

The register exports as CSV under `audit.access_grant.export`, requires step-up when it exceeds 1,000 rows, and is included in the quarterly access review and the annual audit evidence pack.

**Dormant grants are a finding.** A grant that is `ACTIVE` with no access for 60 days is flagged to the owner. It is not revoked automatically, because a statutory audit may legitimately have quiet periods, but it appears on the register in amber and must be justified at the next access review.

### 8.10 Renewal and the Twelve-Month Ceiling

| Rule | Detail |
|------|--------|
| Maximum term | 12 months from activation (BR-AUD-002) |
| Renewal | A new grant that supersedes the old, with `supersedes_grant_id` |
| Renewal window | From 30 days before expiry to 30 days after; after that, a fresh grant with no history link |
| Approval | The same two-person rule; the approver cannot be the creator |
| Acceptance | Fresh acceptance record; MFA and NDA reused only when the evidence is current and the version unchanged; COI declared again |
| Continuity | The new grant activates at the old grant's `end_at`, or immediately when the old is revoked early and the new is already approved |
| Overlap | Zero. The partial unique index permits one `ACTIVE` or `SUSPENDED` grant per client and grantee |
| History | The chain `supersedes_grant_id` is visible on the register, so a three-year engagement reads as three grants, not one immortal grant |

**Why renew rather than extend.** An extension edit hides the fact that a human re-decided. A renewal makes the decision explicit every twelve months, resets the acceptance evidence, and produces a clean record for each audit period. The cost is one approval per year per grant; the benefit is that "access has been continuously justified" is true rather than assumed.

**Why not auto-renew.** Auto-renewal converts a time-bound grant into a standing entitlement with extra steps. The T-14 notice exists precisely to force the decision before expiry, and a lapsed grant that nobody renewed is the correct outcome when nobody is paying attention.

### 8.11 The Auditor Workspace

The workspace is the only surface where an external principal sees client data. It is a distinct route, a distinct token context and a distinct visual frame.

| Aspect | Design |
|--------|--------|
| Entry | `https://app.slateiq.com/workspace/{client_slug}` after explicit selection from the grant list |
| Visual frame | Persistent banner: client name, grant ID, profile, scope, expiry date, "External auditor" label |
| Token | New access token with `org = client_org_id`, `authority_id = grant_id`, 15-minute lifetime, no refresh token in the client context |
| Session | Same `sid` family, `authority_source = GRANT`; the home-organization session remains but cannot act on client data |
| Navigation | Only modules and routes permitted by the profile; no tenant administration, no identity, no settings beyond the auditor's own profile |
| Search | Scoped to the grant; no cross-tenant or cross-show results |
| Downloads | Watermarked PDF and CSV with auditor name, firm, grant ID and timestamp; each download audited |
| Uploads | Evidence only, written to the client's region under the client's `org_id` prefix |
| Exports | Bounded by the profile; step-up required above 1,000 rows; every export is a `GrantExportCompleted` event |
| Session end | Leaving the workspace closes the grant context; the token expires within 15 minutes; explicit exit is audited as `WorkspaceClosed` |
| Idle | The client's idle policy applies, not the firm's; a 15-minute client setting overrides a 60-minute firm setting |
| Device | The auditor's device registration belongs to the firm; the workspace does not create a second device |
| Notifications | The banner shows unread findings and assignments for this grant only |

**The banner is a control, not decoration.** Most cross-tenant incidents in multi-tenant products are a user acting in the wrong tenant. A permanent, unmissable label naming the client and the expiry is the cheapest defence against that class of error, and it is tested in the UX review.

**No mixed context.** The workspace never shows the auditor's own firm data beside client data. A combined view would require merging two tenants in one response, which is exactly the query the isolation model forbids. The auditor switches workspaces; they do not see two at once.

**Screenshots and local copies.** The platform cannot prevent a determined user from photographing a screen. It mitigates with watermarking, download audit, export limits, session recording at the metadata level and contractual terms in the NDA. The residual risk is stated in section 13 and accepted; claiming technical prevention would be false.

### 8.12 Cross-Region Grants

An audit firm may be homed in India while its client is homed in the EU. The grant must work without moving client data and without creating a second copy of the auditor's identity in the client region.

| Concern | Design |
|---------|--------|
| Data location | Client data is read and processed in the client's region only. No client record is replicated to the firm's region |
| Identity | The auditor authenticates in the home (firm) region. The client region receives a minimal projection, not a copy of the identity |
| Projection fields | `user_id` (pseudonym), display-name snapshot, firm name, `firm_org_id`, MFA-confirmed flag, NDA version, COI state, `grant_id` |
| Projection lifetime | Created at activation, refreshed on grant change, deleted 30 days after the grant terminates |
| Trust | The client region validates a short-lived signed assertion from the home region, audience-bound to the client region and to the grant; no shared session cookie spans regions |
| Audit | Grant lifecycle events are written in the client region; a metadata-only copy (`grant_id`, state, timestamps, actor pseudonym) is written in the firm region for the firm's own review |
| Session | A grant-bound session is created in the client region; the home-region session is not reused as an authorisation for client data |
| Revocation | The client region revokes locally; a revocation event propagates to the home region so the workspace disappears from the auditor's list |
| Support | Platform support follows the client's region; a support engineer in another region uses the same break-glass path, and the access is recorded in the client region |
| Backup | Backups stay in the client region; restoring a client backup in the firm region is technically prevented by key separation (section 7.6) |

**Why not replicate the identity.** Copying the auditor's email, phone and credential material into every client region would multiply the personal data, multiply the erasure paths, and create a stale copy that outlives the grant. The projection carries what the client needs to display and audit, and nothing that can authenticate.

**Cross-border transfer.** When the auditor's home region differs from the client's, the grant records `cross_region_acknowledged_at`, the transfer mechanism reference and the client's policy basis. The platform does not decide the lawfulness of the transfer; it records the client's decision and implements the technical boundary. A client whose policy forbids cross-border access sets `allow_cross_region_grants = false`, and the creation form rejects an auditor from another region with `CROSS_REGION_PROHIBITED`.

**Latency.** A cross-region workspace adds one assertion exchange at entry and then reads locally in the client region. It does not proxy every query through the home region, which would be slow and would make the firm's region a processing location for client data. The design keeps the data plane single-region even when the identity plane is not.

**Firm-region visibility.** The firm sees the grants its auditors hold, their state and their expiry. It does not see client data, client names beyond the grant record, or the auditor's activity detail. The firm's administrator needs enough to deprovision and to answer an independence question, and no more.

### 8.13 Independence and Platform-Appointed Auditors

BR-AUD-006 protects platform-appointed auditors from production-side pressure. The grant model implements it without a special case.

| Aspect | Rule |
|--------|------|
| Appointing party | The grant is created by the appointing OTT platform or broadcaster, not by the production house being audited |
| Revocation | Only the appointing party, its Organization Administrator, or Platform Security may revoke; the audited production house cannot revoke, because the appointment is not its to withdraw |
| Visibility | The production house sees that an auditor has access and to which scope; it does not see the appointment terms |
| Scope | The grant is scoped to the assignment, which names the appointing party |
| Dispute | A production house that believes the auditor is conflicted raises it with the appointing party and Platform Security; it cannot unilaterally remove access |
| Independence record | `auditor_profile.independence_declared_at` and the appointing party's reference are stored on the grant |

**Why the production house cannot revoke.** Allowing the audited party to remove its auditor converts the audit into a negotiation. The production house can see the access, can escalate a conflict, and can ask the appointing party to act; it cannot terminate the appointment. The restriction is narrow, documented in the UI, and itself audited.

**Independent practitioners.** An auditor with no firm is onboarded into a SlateIQ-operated `AUDIT_FIRM` tenant with a single seat (section 3.6). The grant then has a real firm to notify and a real administrator to deprovision, and the platform avoids the home-less principal that the archived draft created as `SYS_PLATFORM_AUDITOR`.

### 8.14 Grant Data Model

| Table | Key | Columns and constraints | Owner |
|-------|-----|-------------------------|-------|
| `tenancy.access_grant` | `grant_id` | `client_org_id`, `grantee_user_id`, `firm_org_id`, `owner_user_id`, `profile_key`, `permissions_denied text[]`, `scope_type`, `scope_ids uuid[]`, `start_at`, `end_at`, `state`, `created_by`, `approved_by`, `approved_at`, `accepted_at`, `activated_at`, `suspended_reason`, `revoked_by`, `revoked_at`, `revoke_reason`, `expires_notified_at`, `last_access_at`, `supersedes_grant_id`, `cross_region_acknowledged_at`, `version`, timestamps | MOD-01 |
| `tenancy.access_grant_version` | `grant_version_id` | `grant_id`, `version_no`, `snapshot jsonb`, `changed_by`, `changed_at`, `change_reason`; unique `(grant_id, version_no)` | MOD-01 |
| `tenancy.grant_acknowledgment` | `ack_id` | `grant_id`, `grant_version_no`, `nda_version`, `coi_declaration_id`, `mfa_factor_id`, `accepted_at`, `ip`, `user_agent_hash`; one per acceptance | MOD-01 |
| `tenancy.coi_declaration` | `declaration_id` | `user_id`, `grant_id`, `question_set_version`, `declared_conflict`, `details_ciphertext`, `declared_at`, `resolved_by`, `resolved_at`, `resolution`, `resolution_notes` | MOD-01 |
| `tenancy.grant_expiry_notice` | `notice_id` | `grant_id`, `stage`, `recipients`, `sent_at`, `delivery_result`; unique `(grant_id, stage)` | MOD-01 |
| `tenancy.auditor_profile` | `user_id` | `firm_org_id`, `engagement_types`, `independence_declared_at`, `coi_declared_at`, `nda_version`, `firm_attested_by`, `status` | MOD-01 |
| `tenancy.grant_projection` | `projection_id` | Cross-region minimal projection: `grant_id`, `client_region`, `user_id`, `display_name`, `firm_name`, `mfa_confirmed`, `nda_version`, `coi_state`, `expires_at` | MOD-01 |

**Constraints.**

| Constraint | Definition | Reason |
|------------|------------|--------|
| `grant_term_valid` | `end_at > start_at AND end_at <= start_at + interval '12 months'` | BR-AUD-002, checked at creation and again at activation |
| `grant_scope_present` | `scope_type IN ('SHOW','ASSIGNMENT') AND cardinality(scope_ids) > 0` | FR-TNT-015; no organization-wide external access |
| `grant_no_self` | `client_org_id <> firm_org_id` | A firm cannot audit itself through a grant; it uses its own membership |
| `grant_denied_subset` | `permissions_denied <@ profile_permissions(profile_key)` | A denial cannot name a permission outside the ceiling |
| `grant_one_active` | Partial unique index on `(client_org_id, grantee_user_id)` where `state IN ('ACTIVE','SUSPENDED','PENDING_ACCEPTANCE')` | Prevents overlapping authority and ambiguous revocation |
| `grant_approved_distinct` | `approved_by <> created_by` | SOD-07 |
| `grant_state_trigger` | Transition trigger on `state` | The state machine cannot be bypassed by a direct update |
| `grant_firm_match` | `firm_org_id = auditor_profile(user_id).firm_org_id` | The client cannot name a firm the auditor does not belong to |
| `grant_scope_owned` | Scope identifiers belong to `client_org_id` | An assignment-scoped grant cannot point at another tenant's show |

**Indexes.** `(client_org_id, state, end_at)` for the register and expiry job; `(grantee_user_id, state)` for the workspace list; `(firm_org_id, state)` for the firm view; `(end_at)` partial where `state = 'ACTIVE'` for the notice job.

**Retention.** Grants and their versions are retained seven years after the terminal state. `coi_declaration.details_ciphertext` is encrypted with the grantee's data key and is crypto-shredded on identity erasure while the declaration record, the answer and the resolution survive as pseudonymous evidence. A conflict declaration that vanishes when the auditor leaves is not evidence.

### 8.15 Grant Events

| Event | Severity | Key fields | Consumers |
|-------|----------|-----------|-----------|
| `GrantDrafted` | Info | `grant_id`, `client_org_id`, `grantee_user_id` | Register |
| `GrantDraftDiscarded` | Info | `grant_id`, actor | Hygiene reporting |
| `GrantSubmitted` | Info | `grant_id`, creator, approver candidates | Approval queue |
| `GrantApproved` | High | `grant_id`, approver, profile, scope, dates | Audit, owner notification |
| `GrantDeclined` | Info | `grant_id`, actor, reason code | Creator notification |
| `GrantAccepted` | High | `grant_id`, NDA version, COI state, IP class | BR-AUD-004 evidence |
| `GrantActivated` | High | `grant_id`, `activated_at`, `end_at` | Workspace, notices, audit |
| `GrantVariationApplied` | High | `grant_id`, version, change class, before and after | Audit, access review |
| `GrantSuspended` | High | `grant_id`, reason code, initiator | Session revocation, client notification |
| `GrantResumed` | High | `grant_id`, approver, cleared condition | Workspace, audit |
| `GrantRevoked` | Critical | `grant_id`, initiator, reason, cascade result | Incident review, client and firm notification |
| `GrantExpired` | High | `grant_id`, `end_at` | Register, notification |
| `GrantExpiryNoticeSent` | Info | `grant_id`, stage, recipients, delivery result | FR-TNT-017 evidence |
| `GrantRenewalCreated` | Info | `new_grant_id`, `supersedes_grant_id` | Register chain |
| `GrantWorkspaceEntered` | Info | `grant_id`, `session_id`, region | Cross-tenant monitoring |
| `GrantWorkspaceClosed` | Info | `grant_id`, `session_id`, duration | Session analytics |
| `GrantCrossRegionAcknowledged` | High | `grant_id`, regions, mechanism reference | DPO evidence |
| `GrantExportCompleted` | High | `grant_id`, resource type, row count, watermark ID | Data-loss monitoring |
| `GrantAccessDenied` | Warning | `grant_id`, permission, reason code | Threat detection |
| `EmergencyGrantRevocation` | Critical | `grant_id`, ticket, platform actor | Monthly security review |

**Correlation.** Creation, approval, acceptance, activation and the first workspace entry share one `correlation_id`, so the full grant can be reconstructed from a single identifier (NFR-AUD-004).

### 8.16 Open Questions for Section 8

| ID | Question | Current position | Needed by |
|----|----------|------------------|-----------|
| OQ-4-02 | May an audit firm supervisor hold a read-only grant to a client to review working papers? | No at MVP. Supervision happens through the firm's own quality review process, outside the client tenant. A supervisor grant would double the external principals per engagement for a benefit that is not yet evidenced | Pilot feedback |
| OQ-4-03 | Should grant access history be exportable by the client as a signed evidence pack? | Yes, Phase 1. At MVP the register and the audit events are exportable as CSV; a signed, hash-chained pack is deferred | Phase 1 design |
| OQ-4-04 | What is the maximum number of concurrent grants a single auditor may hold? | Twenty. Above that, the workspace list becomes unusable and the auditor is almost certainly holding stale grants; the limit is a soft warning, not a hard block | Pilot feedback |

### 8.17 Traceability for Section 8

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| FR-TNT-015 | Sections 8.2, 8.3, 8.14 | Mandatory profile, scope and dates; creation cannot proceed without them; `grant_term_valid` and `grant_scope_present` constraints |
| FR-TNT-016 | Sections 8.4, 8.5, 8.8 | MFA, NDA and COI gates; client approval; revocation cascade under 60 seconds against a 5-minute commitment |
| FR-TNT-017 | Sections 8.9, 8.14 | Register with grant, scope, owner and expiry; T-14, T-7, T-1 notices with delivery records; expiry enforced at decision time |
| FR-TNT-010 | Sections 8.6, 8.14 | Grant versioning; changes apply within 60 seconds through `scope_version` |
| NFR-SEC-003 | Section 8.4 | MFA mandatory for every external auditor before activation |
| NFR-AUD-001 | Section 8.15 | Full event catalogue for the grant lifecycle |
| NFR-AUD-004 | Section 8.15 | Correlation ID across creation, approval, acceptance and use |
| NFR-PRV-004 | Section 8.12 | Client-region processing; minimal identity projection; no client data in the firm region |
| NFR-PRV-009 | Sections 8.12, 7.6 | Regional keys and no cross-region restore |
| BR-AUD-001 | Sections 8.2, 8.3 | Mandatory start, end and scope; no open-ended grant |
| BR-AUD-002 | Sections 8.3, 8.10, 8.14 | One client, one grantee, explicit scope, 12-month ceiling enforced by constraint and activation check |
| BR-AUD-003 | Sections 8.7, 8.8 | Revocation and expiry invalidate sessions and refresh tokens |
| BR-AUD-004 | Sections 8.3, 8.4 | Activation gate requires MFA, NDA, COI and client approval |
| BR-AUD-005 | Section 8.4 | Declared conflict blocks the show until resolved in writing |
| BR-AUD-006 | Section 8.13 | Appointing party alone may revoke; production side cannot |
| BR-AUD-007 | Sections 8.6, 8.10 | Handover is a revocation plus a renewal with a `supersedes_grant_id` chain, timestamped and audited |
| CAP-002 | Sections 8.1 to 8.15 | External identity and access capability |
| CAP-003 | Sections 8.3, 8.7 | Role and grant authorisation capability |

---

---

## 9. Logical Data Model

### 9.1 Scope and Ownership

This section defines the logical model for the tenancy and identity domains, plus the rules that every other module must follow when it stores tenant-scoped data. It is not the full physical schema: Document 8 owns the DDL, and the domain modules own their own tables. What is fixed here is the structure that isolation, authorization and audit depend on.

| Schema | Owner | Contains | Defined here |
|--------|-------|----------|--------------|
| `tenancy` | MOD-01 Tenancy | Organizations, memberships, roles, assignments, grants, delegations, access reviews | Yes, in full |
| `identity` | MOD-02 Identity | Users, credentials, factors, sessions, devices, service accounts | Yes, in full |
| `ops` | MOD-15 Platform Ops | Provisioning runs, regions, support access, feature flags | Yes, in full |
| `prod`, `budget`, `expense`, `vendor`, `audit`, `document` | MOD-03 to MOD-09 | Domain aggregates | Only the tenant-column contract |
| `intel`, `excel`, `notification`, `compliance`, `report`, `sync` | MOD-08 to MOD-14 | Read models, jobs, audit, reports | Only the tenant-column contract and the audit table |

**One schema per module.** A module may read another module's table only through a published read model or a module API, never by joining across schemas in an ad-hoc query. The exception is `tenancy` and `identity`, which the authorization path reads directly because every request depends on them and an API hop would put a network round trip on the hot path. That exception is explicit, narrow and reviewed at each release.

### 9.2 Conventions

| Convention | Rule | Reason |
|------------|------|--------|
| Naming | Singular table names, `snake_case`; primary key `<entity>_id` | Consistent joins and generated code |
| Primary keys | UUIDv7, generated by the application, time-ordered | Global uniqueness without a sequence leak; index locality |
| Tenant column | `org_id` on every tenant-scoped table, `NOT NULL`, leading column of every composite index | RLS predicate and query performance |
| Timestamps | `created_at`, `updated_at` as `timestamptz` in UTC; `*_at` suffix | Unambiguous ordering across regions |
| Actor columns | `created_by`, `updated_by` as `user_id` or `service_account_id`; never a display name | Attribution survives a rename and an erasure |
| Money | `numeric(19,4)` amount plus `char(3)` currency plus `base_amount`, `base_currency`, `fx_rate`, `fx_rate_date` | No floating point in a financial system |
| Enumerations | PostgreSQL enum or a lookup table with a stable key; never free text | Closed sets that a migration can audit |
| Booleans | `is_` or `has_` prefix; `NOT NULL` with a default | Avoid three-valued logic in policies |
| Soft delete | Only where recovery is a requirement; a `deleted_at` column plus a partial index; never for audit or financial records | A soft delete that hides rows from RLS is a data leak waiting to happen |
| Hard delete | Only for drafts, expired sessions after retention, and purge | Everything else is retained or archived |
| Nullability | `NULL` means "not applicable", never "unknown" in a security decision; security fields are `NOT NULL` | A null in a policy predicate is a silent denial or a silent allow |
| Case | Emails and slugs lowercased before storage; names stored as entered | Comparison without `lower()` on the hot path |
| JSON | `jsonb` only for snapshots and extension points; never for anything that must be queried by RLS | A policy cannot index into an opaque blob |

**No natural keys in foreign keys.** A slug is unique but mutable in principle and human-chosen; an email is personal data and can change. Foreign keys reference UUIDs. The slug and email have their own unique indexes and are resolved at the edge.

### 9.3 Schema Map

| Table | Purpose | Rows at pilot scale | Partitioned |
|-------|---------|---------------------|-------------|
| `tenancy.organization` | Tenant directory | 500 | No |
| `tenancy.organization_setting` | Typed configuration values | 500 × 30 | No |
| `tenancy.organization_entitlement` | Plan and module entitlements | 500 × 12 | No |
| `tenancy.membership` | Person to organization | 50,000 | No |
| `tenancy.invitation` | Pending membership offers | 5,000 | No |
| `tenancy.role` | System and custom roles | 500 × 20 | No |
| `tenancy.role_permission` | Current permission set per role | 500 × 20 × 25 | No |
| `tenancy.role_version` | Immutable role snapshots | 500 × 20 × 6/year | No |
| `tenancy.assignment` | Role held by a member | 80,000 | No |
| `tenancy.assignment_scope` | Scope identifiers per assignment | 120,000 | No |
| `tenancy.access_grant` | External authority | 2,000 | No |
| `tenancy.access_grant_version` | Grant snapshots | 6,000 | No |
| `tenancy.grant_acknowledgment` | NDA, COI and MFA evidence | 4,000 | No |
| `tenancy.coi_declaration` | Conflict declarations | 4,000 | No |
| `tenancy.grant_expiry_notice` | Notice delivery records | 12,000 | No |
| `tenancy.auditor_profile` | Auditor standing and attestation | 1,000 | No |
| `tenancy.delegation` | Approval delegation | 2,000 | No |
| `tenancy.approval_limit` | Amount limits per role and user | 5,000 | No |
| `tenancy.sod_rule` | Prohibited combinations | 10 | No |
| `tenancy.sod_exception` | Recorded waivers | 200 | No |
| `tenancy.access_review_campaign` | Review cycles | 500/year | No |
| `tenancy.access_review_item` | Per-subject decisions | 50,000/year | Yes, by campaign |
| `identity.user` | Pseudonymous identity | 50,000 | No |
| `identity.user_pii` | Encrypted personal data | 50,000 | No |
| `identity.user_email_alias` | Historical addresses | 60,000 | No |
| `identity.credential` | Password hashes | 45,000 | No |
| `identity.mfa_factor` | TOTP factors | 45,000 | No |
| `identity.recovery_code` | Hashed recovery codes | 450,000 | No |
| `identity.refresh_session` | Refresh sessions | 2,000,000 | Monthly |
| `identity.device_registration` | Devices | 60,000 | No |
| `identity.password_reset` | Reset tokens | 10,000 | No |
| `identity.email_change` | Pending email changes | 1,000 | No |
| `identity.service_account` | Machine clients | 2,000 | No |
| `identity.federated_identity` | Phase 1 federation links | 50,000 | No |
| `identity.identity_transfer` | Controlled changes of employer | 500/year | No |
| `ops.provisioning_run` | Provisioning workflow state | 600 | No |
| `ops.tenant_region` | Tenant to region placement | 500 | No |
| `ops.support_access` | Break-glass records | 2,000/year | No |
| `ops.feature_flag` | Platform flags | 200 | No |

### 9.4 Relationship Map

```text
tenancy.organization (org_id)
  |-- 1:n tenancy.organization_setting
  |-- 1:n tenancy.organization_entitlement
  |-- 1:1 ops.tenant_region
  |-- 1:n tenancy.membership ---------- n:1 identity.user (home_org_id must agree)
  |-- 1:n tenancy.role ---------------- 1:n tenancy.role_permission
  |                          `--------- 1:n tenancy.role_version
  |-- 1:n tenancy.assignment ---------- 1:n tenancy.assignment_scope
  |-- 1:n tenancy.access_grant -------- 1:n tenancy.access_grant_version
  |                          |---------- 1:n tenancy.grant_acknowledgment
  |                          |---------- 1:n tenancy.grant_expiry_notice
  |                          `---------- n:1 tenancy.coi_declaration
  |-- 1:n tenancy.delegation
  |-- 1:n tenancy.approval_limit
  |-- 1:n tenancy.access_review_campaign -- 1:n tenancy.access_review_item
  `-- 1:n compliance.audit_event (append-only)

identity.user (user_id, home_org_id)
  |-- 1:1 identity.user_pii
  |-- 1:n identity.user_email_alias
  |-- 1:1 identity.credential
  |-- 1:n identity.mfa_factor
  |-- 1:n identity.recovery_code
  |-- 1:n identity.refresh_session
  |-- 1:n identity.device_registration
  |-- 1:n identity.federated_identity
  |-- 1:1 tenancy.auditor_profile (only for audit firm members)
  `-- 1:n tenancy.identity_transfer (from and to)
```

**Cardinality rules.**

| Relation | Cardinality | Enforced by |
|----------|-------------|-------------|
| User to home organization | Exactly one | `home_org_id NOT NULL` |
| User to membership at MVP | Exactly one active | Partial unique index on `(user_id)` where `status = 'ACTIVE'`, plus the application invariant |
| Membership to organization | Exactly one | `org_id NOT NULL` |
| Membership to assignment | Zero or more | No constraint beyond scope validity |
| Assignment to scope | One or more, same type | Service validation plus `assignment_scope` check |
| Grant to client | Exactly one | `client_org_id NOT NULL`, unique active pair |
| Grant to grantee | Exactly one | `grantee_user_id NOT NULL` |
| Grant to firm | Exactly one, derived | `firm_org_id` equals the grantee's profile firm |
| Auditor profile to user | Zero or one | Primary key on `user_id` |
| Session to organization | Exactly one acting organization | `org_id NOT NULL` on `refresh_session` |

### 9.5 Tenancy Tables

**`tenancy.organization`**

| Column | Type | Constraint |
|--------|------|------------|
| `org_id` | `uuid` | Primary key, UUIDv7 |
| `slug` | `citext` | Unique globally; `^[a-z0-9-]{3,40}$`; immutable after `ACTIVE` |
| `legal_name` | `text` | Not null, 1 to 200 characters |
| `display_name` | `text` | Not null, 1 to 120 characters |
| `organization_type` | enum | Not null; one of the five types |
| `status` | enum | Not null; the eight states |
| `default_locale` | `text` | Not null, BCP 47 |
| `default_timezone` | `text` | Not null, IANA zone |
| `base_currency` | `char(3)` | Not null, ISO 4217; immutable after the first approved transaction |
| `fiscal_calendar` | `jsonb` | Not null, default twelve monthly periods |
| `data_region` | enum | `IN` or `EU`; immutable at MVP |
| `parent_org_id` | `uuid` | Nullable; set only for a group structure in Phase 1; never used for authority |
| `created_at`, `activated_at`, `suspended_at`, `archived_at`, `purge_scheduled_at`, `purged_at` | `timestamptz` | Lifecycle |
| `purge_approver_one`, `purge_approver_two` | `uuid` | Required before `PURGING` |

`parent_org_id` exists so that a Phase 1 group structure does not require a migration, but it confers no authority. A parent organization cannot read a child's data; consolidated reporting is a separate grant-like mechanism that does not exist at MVP. Adding the column now without a behaviour is cheaper than adding it later, and the comment on the column says so.

**`tenancy.organization_setting`**

| Column | Type | Constraint |
|--------|------|------------|
| `org_id` | `uuid` | FK, part of the primary key |
| `key` | `text` | Part of the primary key; from a closed catalogue |
| `value` | `jsonb` | Validated against the key's schema |
| `updated_by`, `updated_at` | | Audit attribution |

Settings are typed and validated against a catalogue: `session.idle_minutes`, `session.concurrent_limit`, `device.max_per_user`, `password.min_length`, `password.breach_check`, `grant.allow_cross_region`, `retention.audit_years`, `notification.digest_time`, and similar. A key outside the catalogue is rejected. A free-form settings blob becomes a place where security controls are quietly disabled.

**`tenancy.organization_entitlement`**

| Column | Type | Constraint |
|--------|------|------------|
| `org_id` | `uuid` | FK |
| `entitlement_key` | `text` | FK to the global manifest |
| `enabled` | `boolean` | Not null |
| `source` | enum | `PLAN`, `OVERRIDE`, `TRIAL` |
| `limit_value` | `bigint` | Nullable; seats, storage, API calls |
| `valid_from`, `valid_until` | `timestamptz` | `valid_until` null for plan entitlements |

**`tenancy.membership`**

| Column | Type | Constraint |
|--------|------|------------|
| `membership_id` | `uuid` | Primary key |
| `org_id` | `uuid` | FK |
| `user_id` | `uuid` | FK |
| `status` | enum | `INVITED`, `ACTIVE`, `SUSPENDED`, `LEFT` |
| `joined_at`, `left_at` | `timestamptz` | `left_at` only when `LEFT` |
| `invited_by` | `uuid` | Nullable for the bootstrap membership |
| `created_at`, `updated_at` | | |

Indexes: unique `(org_id, user_id)` where `status <> 'LEFT'`; unique `(user_id)` where `status = 'ACTIVE'` at MVP; `(org_id, status)`.

**`tenancy.role`**

| Column | Type | Constraint |
|--------|------|------------|
| `role_id` | `uuid` | Primary key |
| `org_id` | `uuid` | FK; seeded roles are copied per tenant so that a grant references a tenant-owned row |
| `role_key` | `text` | Stable key; unique `(org_id, role_key)` |
| `name`, `description` | `text` | Display only |
| `is_system` | `boolean` | True for the eighteen seeded roles |
| `requires_mfa` | `boolean` | Derived from permissions, stored for fast checks |
| `status` | enum | `ACTIVE`, `RETIRED` |
| `current_version` | `integer` | Increments on each permission change |
| `created_at`, `updated_at` | | |

**`tenancy.role_permission`** — `(role_id, permission_key)` primary key, FK to `tenancy.permission`. The permission catalogue is a global table with `permission_key`, `domain`, `resource`, `action`, `description`, `introduced_in`, `retired_in`. A retired permission remains in the catalogue so historical role versions still resolve, but it cannot be assigned.

**`tenancy.role_version`** — `role_version_id`, `role_id`, `version_no`, `permissions text[]`, `changed_by`, `changed_at`, `change_reason`, `snapshot jsonb`. Unique `(role_id, version_no)`.

**`tenancy.assignment`**

| Column | Type | Constraint |
|--------|------|------------|
| `assignment_id` | `uuid` | Primary key |
| `org_id` | `uuid` | FK |
| `membership_id` | `uuid` | FK |
| `role_id` | `uuid` | FK; must belong to the same `org_id` |
| `scope_type` | enum | `ORG`, `SHOW`, `SEASON`, `EPISODE`, `DEPARTMENT`, `ASSIGNMENT`, `SELF` |
| `valid_from`, `valid_until` | `timestamptz` | `valid_until` nullable for permanent assignments |
| `created_by`, `created_at`, `updated_at` | | |

**`tenancy.assignment_scope`** — `(assignment_id, scope_id)` primary key, where `scope_id` is the identifier of the named scope type. A trigger verifies that the scope entity belongs to the same `org_id`; a cross-schema trigger is avoided by validating in the service and re-checking in the scope-validity job.

**`tenancy.permission`** — the global catalogue. `permission_key` primary key, `domain`, `resource`, `action`, `is_external_grantable`, `requires_step_up`, `introduced_in`, `retired_in`. The `is_external_grantable` flag is what the build assertion in section 5.7 reads.

**`tenancy.delegation`** — `delegation_id`, `org_id`, `delegator_user_id`, `delegate_user_id`, `permission_key`, `scope_type`, `scope_ids uuid[]`, `max_amount numeric(19,4)`, `currency`, `start_at`, `end_at`, `reason`, `revoked_at`, `revoked_by`, `created_at`. Constraints: `delegator_user_id <> delegate_user_id`, `end_at <= start_at + interval '90 days'`, both users are active members of `org_id`.

**`tenancy.approval_limit`** — `limit_id`, `org_id`, `role_id` null, `user_id` null, `scope_type`, `scope_ids uuid[]`, `max_amount`, `currency`, `effective_from`, `effective_until`. Exactly one of `role_id` or `user_id` is set. A user limit cannot exceed the role limit; enforced by the service on write and by a nightly consistency job.

**`tenancy.sod_rule`** — `rule_key`, `permission_a`, `permission_b`, `scope_kind` (`ORG`, `SHOW`, `TARGET_USER`), `enforcement` (`HARD`, `CONDITIONAL`), `description`, `effective_from`. Seeded, migration-only.

**`tenancy.sod_exception`** — `exception_id`, `org_id`, `rule_key`, `subject_user_id`, `scope_type`, `scope_ids uuid[]`, `reason`, `compensating_control`, `reviewer_user_id`, `approver_one`, `approver_two`, `valid_from`, `valid_until` (maximum 90 days), `revoked_at`. A partial unique index prevents two live exceptions for the same subject and rule.

### 9.6 Identity Tables

**`identity.user`**

| Column | Type | Constraint |
|--------|------|------------|
| `user_id` | `uuid` | Primary key, UUIDv7 |
| `home_org_id` | `uuid` | FK, not null, immutable except through `identity.identity_transfer` |
| `status` | enum | The seven identity states |
| `email_hash` | `bytea` | Deterministic hash for uniqueness without storing the address in the clear in this table |
| `locale`, `timezone` | `text` | Defaults from the home organization |
| `created_at`, `activated_at`, `deprovisioned_at`, `erased_at` | `timestamptz` | Lifecycle |
| `last_login_at` | `timestamptz` | Updated at most once per minute |

Unique `(email_hash)` where `erased_at IS NULL`. The plaintext email lives in `identity.user_pii`; the hash supports lookup and uniqueness while the PII row is encrypted. A deterministic hash of a low-entropy value is enumerable, so the hash is keyed with a server-side pepper held in the secret store, and the pepper is not the same one used for passwords.

**`identity.user_pii`**

| Column | Type | Constraint |
|--------|------|------------|
| `user_id` | `uuid` | Primary key, FK |
| `email_ciphertext` | `bytea` | AES-256-GCM with the user's data key |
| `display_name_ciphertext` | `bytea` | Same key |
| `phone_ciphertext` | `bytea` | Nullable |
| `key_id` | `uuid` | Reference to the user data key |
| `key_destroyed_at` | `timestamptz` | Set on erasure; the row is then unreadable |
| `updated_at` | | |

**`identity.credential`** — `credential_id`, `user_id` unique, `password_hash` (PHC string), `algorithm`, `params_version`, `pepper_version`, `changed_at`, `must_change`, `history jsonb` (last five hashes, Argon2id, never reversible).

**`identity.mfa_factor`** — `factor_id`, `user_id`, `factor_type` (`TOTP`, `WEBAUTHN` Phase 1), `secret_ciphertext`, `public_key` (WebAuthn), `confirmed_at`, `last_used_step`, `disabled_at`, `created_at`. Partial unique index: one active TOTP factor per user.

**`identity.recovery_code`** — `code_id`, `user_id`, `code_hash`, `generation`, `used_at`. Index `(user_id, generation)`.

**`identity.refresh_session`** — `session_id`, `user_id`, `org_id`, `device_id`, `family_id`, `authority_source`, `authority_id`, `token_hash`, `issued_at`, `expires_at`, `absolute_expires_at`, `last_seen_at`, `last_strong_auth_at`, `rotated_at`, `revoked_at`, `revoke_reason`, `ip_class`, `user_agent_hash`. Unique `token_hash`; indexes `(user_id, revoked_at)`, `(org_id, revoked_at)`, `(family_id)`, `(authority_id)` for grant revocation.

**`identity.device_registration`** — `device_id`, `user_id`, `platform`, `device_public_key`, `biometric_gate`, `trust_state`, `app_version`, `os_version`, `registered_at`, `last_seen_at`, `revoked_at`, `wipe_requested_at`, `wipe_acknowledged_at`.

**`identity.service_account`** — `service_account_id`, `org_id`, `name`, `secret_hash`, `permission_set text[]`, `owner_user_id`, `expires_at`, `last_used_at`, `rotated_at`, `disabled_at`. Unique `(org_id, name)`.

**`identity.identity_transfer`** — `transfer_id`, `user_id`, `from_org_id`, `to_org_id`, `requested_by`, `approved_by_releasing`, `approved_by_receiving`, `approved_by_platform`, `state` (`REQUESTED`, `APPROVED`, `COMPLETED`, `CANCELLED`), `effective_at`, `completed_at`, `reason`.

### 9.7 Grant Tables

Section 8.14 defines the grant tables in full. The model rules that apply to them and to every future table are:

| Rule | Application |
|------|-------------|
| The grant is tenant-owned | `client_org_id` is the RLS tenant column; the grantee's own organization cannot read the grant row except through the metadata projection in section 8.12 |
| The grant is not a role | No `role_id` column; `profile_key` references the global grant profile catalogue |
| The grant is versioned | Every change writes `access_grant_version` before the row is updated |
| The grant is retained | Seven years after the terminal state, regardless of identity erasure |
| The grant's evidence is separately keyed | COI details are encrypted with the grantee's data key; the record and the resolution survive erasure |

### 9.8 Platform Operations Tables

| Table | Key columns | Notes |
|-------|-------------|-------|
| `ops.provisioning_run` | `run_id`, `org_id` null until step 2, `slug`, `region`, `status` (`RUNNING`, `SUCCEEDED`, `FAILED`, `COMPENSATED`), `current_step`, `step_log jsonb`, `idempotency_key` unique, `requested_by`, timestamps | Not accessible to the tenant application role |
| `ops.tenant_region` | `org_id`, `region`, `cell`, `key_id`, `storage_bucket`, `created_at`, `moved_at` | One row per tenant; the source of truth for routing and key selection |
| `ops.support_access` | `support_access_id`, `operator_user_id`, `target_org_id`, `approver_one`, `approver_two`, `ticket_ref`, `reason`, `scope`, `starts_at`, `expires_at`, `ended_at`, `actions_count` | Two-person approval, maximum 60 minutes |
| `ops.feature_flag` | `flag_key`, `region`, `org_id` null, `enabled`, `rollout_percent`, `updated_by`, `updated_at` | Tenant overrides are explicit rows, not JSON |
| `ops.region_capacity` | `region`, `cell`, `tenant_count`, `storage_bytes`, `updated_at` | Capacity planning |
| `ops.key_rotation` | `key_id`, `region`, `purpose`, `activated_at`, `retired_at`, `destroyed_at` | Key lifecycle evidence |

**Why `ops` is a separate schema and role.** A tenant-scoped query can never accidentally read a provisioning run, because `slateiq_app` has no grant on `ops` at all. The platform service connects with `slateiq_ops`, which has no grant on financial tables. The split is enforced by PostgreSQL privileges, not by convention.

### 9.9 Cross-Module Reference Rules

| Rule | Detail |
|------|--------|
| Reference by ID plus tenant | A cross-module reference stores `(entity_id, org_id)`; the foreign key is to the entity table, and a check constraint or trigger asserts the `org_id` matches |
| No cross-schema joins in module code | Modules read each other through APIs or published views; the authorization path is the documented exception |
| No cross-tenant reference | Every reference between tenant-owned rows must carry the same `org_id`; a trigger rejects a mismatch |
| Reference data is global and read-only | Currencies, countries, permissions and grant profiles have no `org_id` and no write grant for the application role |
| Denormalised tenant column is mandatory | Child tables carry `org_id` even when the parent already has it, so RLS does not depend on a join |
| Deletion order follows dependency | Children before parents; the purge job walks a declared order, not `CASCADE` |

**Why denormalise `org_id` into children.** A policy that says `EXISTS (SELECT 1 FROM parent WHERE parent.org_id = ...)` runs a subquery for every row and can be bypassed by a query that does not join the parent. A direct column is one comparison, indexable, and impossible to avoid. The cost is a redundant column and a trigger that keeps it consistent; the benefit is that RLS is simple enough to review.

**Cross-tenant reference is a build failure.** A schema test enumerates every foreign key between tenant-owned tables and asserts that both sides carry `org_id` and that the constraint includes it. A composite foreign key `(parent_id, org_id) REFERENCES parent (parent_id, org_id)` makes the mismatch unrepresentable; where that is impractical, a trigger enforces it.

### 9.10 The Tenant-Scoped Table Contract

Every table in a tenant schema must satisfy the following. The build reads the catalogue and fails on a violation, so a new table cannot quietly opt out.

| Requirement | Check |
|-------------|-------|
| Has `org_id uuid NOT NULL` | `information_schema.columns` |
| Has RLS enabled and forced | `pg_class.relrowsecurity` and `relforcerowsecurity` |
| Has at least one `SELECT` policy and one write policy | `pg_policies` |
| Has a composite index beginning with `org_id` | `pg_indexes` |
| Has `created_at timestamptz NOT NULL DEFAULT now()` | Column check |
| Has no `BYPASSRLS` grant to the application role | `pg_roles` |
| Has a declared retention class | `compliance.retention_policy` reference in the table comment |
| Has no unlogged or temporary variant in production | `pg_class.relpersistence` |

**Exceptions.** Global reference tables, the `ops` schema and `compliance.audit_event` (which has RLS but an append-only write model) are listed in a manifest with a reason. The exception list is reviewed at every release; a table that appears on it without a reason fails the build.

### 9.11 Classification and Handling

| Class | Examples | At rest | In transit | In logs | In exports | Retention |
|-------|----------|---------|------------|---------|------------|-----------|
| **Restricted** | Credentials, TOTP secrets, recovery codes, service secrets, refresh tokens | Encrypted, separate key, never readable by the application in plaintext after write | TLS 1.2+ | Never | Never | Until rotation or expiry |
| **Sensitive personal** | Email, display name, phone, IP, user agent, COI details | Encrypted with a per-user key | TLS 1.2+ | Redacted or hashed | Included in the subject's own export | Life plus 30 days; COI 7 years |
| **Confidential financial** | Budgets, expenses, vendor bank details, contracts | Encrypted at rest | TLS 1.2+ | IDs only | Only under an authorised export | 7 years |
| **Confidential commercial** | Grants, roles, assignments, access reviews | Encrypted at rest | TLS 1.2+ | IDs and codes only | Authorised export | 7 years |
| **Internal** | Slugs, organization names, module names, configuration keys | Encrypted at rest | TLS 1.2+ | Allowed | Yes | Life of tenant |
| **Public** | Marketing content, help text | — | TLS | Allowed | — | — |

**Classification is a column comment.** Each table declares its highest class in a comment that the data-catalogue job reads. A column that holds personal data must be marked, and a test asserts that every marked column is either encrypted or on the approved exception list. Without that assertion, a new personal-data column is added in a migration and discovered during a subject access request.

### 9.12 Indexing Standard

| Index | Rule |
|-------|------|
| Tenant tables | Lead with `org_id`; then the selective predicate; then the sort column |
| RLS predicates | `org_id`, `show_id`, `department_id` indexes must exist before a table exceeds 10,000 rows |
| Unique keys | Partial where the uniqueness applies only to a subset (`status = 'ACTIVE'`, `erased_at IS NULL`) |
| Foreign keys | Every FK has an index on the referencing column; PostgreSQL does not create one |
| Soft-deleted rows | Partial index excludes `deleted_at IS NOT NULL` |
| Large read models | Cover the authorization predicate columns so the policy can be evaluated from the index |
| Unused indexes | Reviewed quarterly; an index with zero scans for 90 days is a write-cost tax with no benefit |

**Policy performance.** The RLS predicate is evaluated per row before the application sees it. A policy that cannot use an index turns a 5 ms query into a sequential scan on the tenant's largest table, which is a self-inflicted denial of service that looks like a database problem. The isolation suite includes `EXPLAIN` assertions for the ten hottest queries (section 7.8).

### 9.13 Partitioning and Retention

| Table | Partition key | Granularity | Online retention | Archive | Purge |
|-------|---------------|-------------|------------------|---------|-------|
| `identity.refresh_session` | `issued_at` | Monthly | 90 days after expiry | Not archived | Dropped partition |
| `identity.audit_event` (view of `compliance.audit_event`) | `occurred_at` | Monthly | 7 years | Object storage, Object Lock | After retention |
| `compliance.audit_event` | `occurred_at` | Monthly | 7 years | Object storage, Object Lock | After retention |
| `tenancy.access_review_item` | `campaign_id` | Per campaign | 7 years | Object storage | After retention |
| `notification.notification` | `created_at` | Monthly | 13 months | Not archived | Dropped partition |
| `intel.cost_fact` | `period_month` | Monthly | 7 years | Object storage | After retention |
| `sync.sync_batch` | `created_at` | Monthly | 90 days | Not archived | Dropped partition |

**Retention is per class, not per table.** A table with mixed classes carries the longest applicable retention for the rows that are retained, and the erasure job removes the personal fields early by destroying the key. The retention policy is a row in `compliance.retention_policy` with a class, a period and a legal basis, and the purge job reads that row rather than a hard-coded interval. Changing a retention period is a compliance decision, not a deployment.

**Partition maintenance.** A job creates the next three monthly partitions in advance, attaches them, and detaches expired ones. A missing partition causes an insert failure, which is loud and correct; an automatic default partition would silently absorb rows that then escape the retention sweep.

### 9.14 Deletion, Erasure and Legal Hold

| Object | Deletion path | Reversible | Evidence |
|--------|---------------|------------|----------|
| Draft grant | Hard delete | No | `GrantDraftDiscarded` |
| Revoked session | Purged 90 days after expiry | No | Revocation event retained |
| Device registration | Purged 90 days after revocation | No | `DeviceRevoked` retained |
| Invitation | Purged 30 days after expiry | No | `InvitationRevoked` retained |
| Membership | Soft `LEFT`, retained | Yes, by re-invitation (new membership) | `UserDeactivated` |
| Identity | Crypto-shred, pseudonym retained | No | `UserErased`, key destruction receipt |
| Financial record | Never deleted before retention; reversal is a new record | No | Audit trail |
| Audit event | Never updated or deleted before retention | No | Hash chain |
| Tenant | Purge workflow, key destroyed, tombstone retained | No | Certificate of destruction |

**Legal hold overrides every deletion path.** A hold row names the matter, the custodian, the scope (tenant, show, user or document set), the issuing authority and the release authority. The purge, retention and erasure jobs all consult it. Releasing a hold requires the named authority, not an administrator, and the release is audited with the same severity as the hold.

**Erasure order.** Destroy the user data key first, then delete the PII row, then hash the email alias, then retain the pseudonym. Destroying the key first makes any residue unreadable even if a later step fails; deleting rows first would leave a window in which a backup and a live row are both readable.

**Tombstones.** A purged tenant leaves a row with `org_id`, `slug`, `purged_at`, approvers and certificate hash. It contains no personal data and no business data. It exists so that support can answer "was this tenant deleted, by whom, and when" without retaining the tenant.

### 9.15 Migration Rules

| Rule | Detail |
|------|--------|
| Expand, migrate, contract | Add nullable, backfill, enforce, then remove in a later release; never a destructive change in one deployment |
| Policy changes are migrations | An RLS policy change is a versioned migration, reviewed like code, and drift is a build failure |
| No data migration in a request path | Backfills run as jobs with checkpoints; a request never waits for a backfill |
| Role changes are additive | A new permission is added to the catalogue and to roles by migration; removing one requires a deprecation cycle |
| Enum changes | Add the value first; remove the old value only after no row references it and no code path emits it |
| Index creation | `CONCURRENTLY` for tables above 100,000 rows; the migration runner does not wrap it in a transaction |
| Rollback | Every migration has a tested rollback or a documented forward-fix; a migration that cannot be reversed is flagged as such in review |
| Tenant impact | A migration that rewrites a tenant table is run per tenant with a progress record; a single statement over all tenants is prohibited |

**Why per-tenant migrations for large rewrites.** A single `UPDATE` across 50,000 memberships holds locks and can exhaust the connection pool. Running per tenant with a checkpoint makes the work resumable and bounds the blast radius. The migration job uses `slateiq_migrator`, which is not deployed to the runtime.

### 9.16 Data Invariants

| Invariant | Enforced by | Test |
|-----------|-------------|------|
| Every tenant table has `org_id` | Build check | Schema catalogue test |
| Every tenant row is visible only in its tenant | RLS | Per-table negative test |
| A membership's organization equals the user's home organization at MVP | Composite FK or deferred trigger | Invariant test |
| A role belongs to the same organization as its assignment | Composite FK | Invariant test |
| A grant's scope belongs to the client organization | Service plus scope-validity job | Cross-tenant scope test |
| A grant's firm equals the grantee's firm | Service plus trigger | Grant fixture test |
| One active membership per user at MVP | Partial unique index | Concurrency test |
| One active grant per client and grantee | Partial unique index | Concurrency test |
| A session's organization matches its token | Middleware plus session lookup | Token matrix test |
| An approval's actor is not the creator or submitter | Service predicate | SoD test |
| Audit events are append-only | Grant and trigger | Append-only test |
| A purged tenant has no rows | Post-purge probe | Purge drill |
| A permission key exists in the catalogue | FK from `role_permission` | Catalogue test |
| A grant profile permission is external-grantable | Build assertion | Manifest test |

**Service-enforced versus database-enforced.** The database enforces what can be expressed as a constraint without cross-schema queries: uniqueness, tenancy columns, state transitions, append-only audit. The service enforces what requires reading another module: scope validity, approval independence, SoD combinations. Every service-enforced invariant has a nightly consistency job that reports violations, because a code path that bypasses the service will eventually exist.

### 9.17 Seed Data and Catalogues

| Catalogue | Owner | Change process |
|-----------|-------|----------------|
| Permission catalogue | Architecture | Migration, versioned, build assertion |
| Role catalogue (18 system roles) | Architecture | Migration; a tenant receives a copy at provisioning |
| Grant profiles (3) | Architecture | Migration; build assertion on external-grantable keys |
| SoD rules (10) | Security | Migration; reviewed with Legal |
| Organization types (5) | Architecture | Migration |
| Retention policies | Compliance | Migration; approved by the DPO |
| Region catalogue | Platform Ops | Migration plus capacity record |
| Reference data (currencies, countries, categories) | Product | Migration; tenant may add categories but not currencies |

**Seeded roles are copies, not references.** Each tenant receives its own `tenancy.role` rows for the eighteen system roles, flagged `is_system = true`. The permissions are copied from the catalogue at provisioning and cannot be edited. Copying means a grant or assignment references a tenant-owned row, the tenant's role set can be exported with its data, and a future platform change can be rolled out per tenant with a visible version. A global role table shared by all tenants would make per-tenant role history impossible.

**Catalogue drift.** A job compares each tenant's system role permissions with the catalogue and reports drift. Drift should be impossible because system roles are immutable, but the check catches a direct database edit, a failed migration or a restored backup from an older version. Drift is a P2 finding and is corrected by re-seeding the affected role after review.

### 9.18 Traceability for Section 9

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| FR-TNT-001 | Sections 9.5, 9.8 | Organization table with slug, currency, timezone, region; provisioning run record |
| FR-TNT-002 | Sections 9.5, 9.3 | Profile fields and settings catalogue; base currency immutability |
| FR-TNT-011 | Sections 9.5, 9.17 | Global permission catalogue with unique keys and seed process |
| FR-TNT-013 | Sections 9.5, 9.9 | Assignment and scope tables; tenant-owned scope validation |
| FR-TNT-015 | Sections 9.7, 8.14 | Grant tables with profile, scope and dates |
| FR-TNT-018 | Sections 9.9, 9.10 | Tenant column contract and RLS coverage check |
| FR-TNT-023 | Sections 9.5, 9.8 | `data_region` and `ops.tenant_region` |
| NFR-PRV-004 | Sections 9.11, 9.13 | Classification and regional retention |
| NFR-PRV-005 | Section 9.11 | Minimisation and column classification |
| NFR-PRV-006 | Section 9.14 | Erasure path and pseudonym retention |
| NFR-AUD-002 | Sections 9.13, 9.14 | Append-only audit and seven-year retention |
| NFR-AUD-003 | Section 9.13 | Partitioned audit retention |
| CAP-001 | Sections 9.3 to 9.10 | Tenant isolation data model |
| CAP-002 | Sections 9.6 to 9.7 | Identity and grant data model |

---

## 10. Lifecycle and Provisioning Flows

### 10.1 Provisioning, End to End

Section 2.9.1 defines the ten provisioning steps and their compensations. This section defines how the workflow is driven, observed and recovered.

| Element | Design |
|---------|--------|
| Trigger | `POST /ops/tenants` by Platform Operations, or a contract-provisioning job from the commercial system |
| Idempotency | `Idempotency-Key` header, or a deterministic key from `(slug, region, requested_at)`; stored unique on `ops.provisioning_run` |
| Execution | Synchronous validation, then an asynchronous workflow in MOD-15; the API returns `202` with `run_id` and a status URL |
| State | `RUNNING` with `current_step` 1 to 10; `SUCCEEDED`; `FAILED`; `COMPENSATED` |
| Step log | Each step writes `{step, started_at, finished_at, outcome, error_code}` to `step_log` |
| Retry | Exponential backoff, five attempts per step, then `FAILED` and an alert |
| Resume | A failed run resumes from the first incomplete step; completed steps are not repeated |
| Timeout | Whole run 30 minutes; a step at 30 seconds is a warning, at 5 minutes a failure |
| Cancellation | Allowed before step 2; after step 2 the run must compensate |
| Observability | One trace per run, one span per step, `org_id` and `run_id` on every log line |
| Evidence | `step_log`, the isolation probe result and the activation event are retained for seven years |

**The workflow is not a database transaction.** Steps span PostgreSQL, object storage, KMS and the directory. A distributed transaction is not available, so each step is idempotent and has a compensation, and the run is a saga. Pretending otherwise produces a tenant that exists in the directory but has no storage, or has storage and no roles.

**Step detail beyond section 2.9.1.**

| Step | Additional operational detail |
|------|-------------------------------|
| 1 | Validates slug syntax and global uniqueness, region membership in the catalogue, organization type, plan existence and seat limit |
| 2 | Inserts the organization and `ops.tenant_region` in one transaction; the region row is the routing source of truth from this point |
| 3 | Copies the plan manifest into `organization_entitlement`; overrides are explicit rows with a reason and an expiry |
| 4 | Copies the eighteen system roles and their permissions at the current catalogue version; records `role_version` 1 for each |
| 5 | Seeds departments, expense categories, retention policy, notification defaults and fiscal calendar; all are editable except the retention policy |
| 6 | Creates the prefix `org/{org_id}/` and applies the bucket policy condition; enables versioning and Object Lock for evidence |
| 7 | Creates the tenant data key in the regional KMS with the region's key policy; records `ops.key_rotation` |
| 8 | Warms the tenant directory cache and registers the routing entry; a stale cache after activation is a P1 |
| 9 | Runs the probe as `slateiq_app`: write a probe row in the tenant, read it, read it as a second synthetic tenant, assert zero rows, delete it |
| 10 | Transitions to `ACTIVE`, creates the first administrator invitation, emits `OrganizationProvisioned` and `OrganizationActivated` |

**Why the probe is a hard gate.** A tenant activated without a probe is a tenant whose isolation has never been exercised. The probe catches a missing policy, a wrong role grant and a misconfigured connection before a customer sees the product. It runs as the application role, not as the migrator, because a probe that bypasses RLS proves nothing (section 7.8).

**Provisioning failure.** If a step fails after step 2, the workflow compensates in reverse order, marks the run `COMPENSATED`, sets the organization to `PROVISIONING_FAILED`, and leaves the slug reserved for 24 hours so a retry does not collide with a half-created tenant. After 24 hours the slug is released and the organization row is purged if it has no data.

**Manual provisioning at MVP.** FR-TNT-019 permits manual provisioning at MVP. The API exists, but the pilot uses an operator-driven run with the same steps and the same probe; "manual" means a human triggers it, not that the controls are skipped.

### 10.2 First Administrator Bootstrap

| Step | Action | Control |
|------|--------|---------|
| 1 | Platform Operations names the first administrator during provisioning | The person must be a named individual, not a shared mailbox |
| 2 | Invitation is issued with the `ORG_ADMIN` role and `ORG` scope | Token 256-bit, 72-hour expiry, single use |
| 3 | Invitee verifies email possession, sets a password, enrolls TOTP | No default credential; no skip |
| 4 | Membership and assignment are created in one transaction | `OrganizationAdminBootstrapped` event |
| 5 | The administrator is prompted to invite a second administrator | A soft prompt at first login, a hard reminder at day 7 |
| 6 | The platform records the bootstrap in the tenant audit trail | Visible to the tenant, not only to the platform |

**Why a second administrator is pushed early.** Almost every control in this document assumes two people: grant approval, MFA reset approval, suspension review, access review certification, SoD exceptions. A tenant with one administrator cannot use any of them, and the workaround is a shared account, which is worse than the gap. The prompt is soft because a second administrator may not exist yet; the gap is visible on the tenant's security posture page.

**Bootstrap identity is not privileged beyond its role.** The first administrator holds `ORG_ADMIN`, which cannot approve financial transactions, cannot grant external access, and cannot read audit events beyond the tenant's own view. The bootstrap creates a tenant administrator, not a platform deputy.

### 10.3 Invitation and Join

| Stage | Actor | Effect | Failure |
|-------|-------|--------|---------|
| Create | Administrator with `tenancy.invitation.create` | Row in `tenancy.invitation`, token hashed, email sent | Duplicate open invitation returns the existing one; invalid role or scope fails |
| Deliver | Notification service | Email with a single-use link | Bounce is recorded; the administrator sees `DELIVERY_FAILED` and can resend |
| Accept | Invitee | Token validated, identity created or matched, password set, MFA enrolled if required | Expired, used or revoked token returns `410 INVITATION_INVALID` |
| Join | System | Membership and assignment created in one transaction; invitation marked accepted | Partial creation is impossible; the transaction rolls back |
| Notify | System | Home organization administrators notified; inviter notified | Delivery failure is visible, not silent |
| Audit | System | `InvitationAccepted`, `UserActivated`, `MembershipCreated` | — |

**Expiry and resend.** An invitation expires after 72 hours (FR-TNT-004). A resend invalidates the previous token and issues a new one; the previous link returns `410` immediately. Five resends per invitation, then the invitation must be recreated, which forces the inviter to reconsider whether the address is correct.

**Existing identity.** If the email matches an existing active identity, the invitee must authenticate before accepting. The invitation then creates a membership in the inviting organization, subject to the one-membership rule at MVP: if the person already has an active membership elsewhere, the invitation is rejected with `MULTI_ORG_NOT_SUPPORTED` and the organization is told to use a grant or wait for Phase 1 (OQ-4-01).

**Bulk invitation (Phase 1).** The CSV is validated in full, then applied as one batch with a per-row outcome. The batch is a single audit event with a manifest hash, so an auditor can see exactly which file produced which memberships.

### 10.4 Suspension and Reactivation

| Phase | Action | Timing | Evidence |
|-------|--------|--------|----------|
| Request | Platform Operations or Security selects a reason code and writes a justification | — | `OrganizationSuspensionRequested` |
| Approval | Second approver for `SECURITY_INCIDENT` and `LEGAL_ORDER` | Before execution | Approval record |
| Execute | Status to `SUSPENDED`; all sessions revoked; jobs paused; cache entries invalidated | Within 60 seconds | `OrganizationSuspended` |
| Verify | Probe a member request and a grant request; both must return `403 TENANT_SUSPENDED` | Within 5 minutes | Probe result |
| Notify | Organization Administrators receive the reason category and support path | Within 15 minutes | Notification record |
| Hold | Data retained; retention clock continues; security and retention jobs run | Duration of suspension | Job log |
| Reactivate | Status to `ACTIVE`; no sessions restored; counters resume | Within one business day | `OrganizationReactivated` |
| Review | Post-incident review if the suspension was security-driven | Within 5 business days | Review record |

**Grant behaviour on suspension.** Every grant whose client is the suspended tenant becomes unusable at decision time because the tenant state check precedes the grant check (section 6.8). The grant rows are not changed to `SUSPENDED`, because that would conflate the tenant's state with the grant's state and would require a bulk update on reactivation. The decision refuses; the register shows the grant with a tenant-suspended annotation.

**Reactivation does not restore trust.** Sessions are not restored, devices remain registered but require re-authentication, and any grant suspended for cause stays suspended until separately resumed. Reactivation restores the tenant, not the previous level of access.

### 10.5 Archival and Export

| Step | Action | Output |
|------|--------|--------|
| 1 | Customer or Platform Operations requests archival | `OrganizationArchiveRequested` |
| 2 | Writes disabled; sessions revoked; tenant becomes read-only | `OrganizationArchived` |
| 3 | Export job builds a complete package: relational data as CSV and JSON, evidence objects with checksums, audit trail as JSONL, manifest | Export manifest |
| 4 | Manifest is signed and hashed; the package is encrypted and delivered to the customer's nominated destination | Delivery record |
| 5 | Export is verified by re-reading the manifest and sampling checksums | Verification report |
| 6 | Tenant remains archived until purge or reactivation | Archive certificate |

**Export completeness.** The manifest lists every table, row count, object count, checksum and the schema version. A table that exists in the schema but not in the manifest is a bug; the export job builds the table list from the catalogue, not from a hand-maintained list. An incomplete export discovered after purge is unrecoverable, which is why the manifest is verified before the purge is allowed.

**Export format.** CSV for tabular data, JSON for nested structures, JSONL for audit events, original bytes for documents. Formats are documented and versioned so that a customer can read the package without SlateIQ. A proprietary export format is not an export.

**Archived tenant access.** Only Platform Operations can read an archived tenant, through break-glass, and only for support or legal reasons. The customer's administrators cannot sign in to an archived tenant; they receive the export instead.

### 10.6 Deletion and Purge

| Phase | Actor | Action | Blocking conditions |
|-------|-------|--------|---------------------|
| Request | Customer administrator or Platform Operations | Record intent, scope and reason | Legal hold, open investigation, unpaid retention obligation |
| Validate | Compliance | Check legal hold, DSR obligations, contractual retention, audit retention | Any active hold blocks |
| Approve | Two approvers: Platform Operations and Security or Legal | Record approval | Approval cannot be the requester |
| Schedule | System | Set `PENDING_DELETION`, start the 30-day grace clock, notify administrators | — |
| Grace | System | Tenant read-only; export remains available; cancellation allowed | — |
| Freeze | System | Stop jobs, revoke sessions, snapshot the final state | — |
| Purge | System | Delete objects and versions, then child rows, then parent rows, then the organization, then the key, then caches | Any failure halts and alerts |
| Verify | System plus operator | Post-purge probe asserts zero rows and zero objects; backup expiry confirmed | A non-zero result reopens the run |
| Certify | Platform Operations | Certificate of destruction with approvers, timestamps and hash | — |
| Tombstone | System | Retain `org_id`, slug, purge date, approvers and certificate hash | — |

**Purge order and the key.** Objects and versions first, then rows, then the organization, then the key. Destroying the key last means that if any deletion step fails, the residual data is still encrypted and the run can be resumed. The audit narrative records the key destruction first because it is the step that makes the data irrecoverable.

**Backups.** Backups expire on their normal schedule; a purge does not attempt to rewrite immutable backups. The certificate states the backup expiry date, and the regional key is destroyed, which makes the backup ciphertext unreadable. This is the only honest way to handle immutable backups: you cannot delete them early, so you make them useless.

**Purge cancellation.** During the 30-day grace, cancellation returns the tenant to `ARCHIVED`. After the freeze, cancellation is not possible. The UI states the point of no return in plain language and requires the administrator to type the organization slug to confirm.

### 10.7 Region Change (Phase 1)

MVP fixes the region at provisioning (RN-05). Phase 1 adds a controlled move, and the design is recorded now so that the MVP schema does not need to change.

| Step | Action | Control |
|------|--------|---------|
| 1 | Request with target region, reason and window | Platform Operations and customer administrator |
| 2 | Approve | Two approvers, one from Platform Operations, one from Security |
| 3 | Freeze writes | Tenant read-only; jobs paused; sessions revoked |
| 4 | Snapshot | Consistent database snapshot plus object inventory with checksums |
| 5 | Replicate | Encrypted transfer to the target region's storage and database |
| 6 | Re-encrypt | Rewrap data keys with the target region's KMS key |
| 7 | Verify | Row counts, checksums, isolation probe in the target region |
| 8 | Switch | Update `ops.tenant_region`; invalidate routing caches; resume writes |
| 9 | Observe | 14-day dual-read verification window where the source remains read-only |
| 10 | Destroy source | Delete the source data and keys after the observation window; certificate |

**No live migration at MVP.** Region is structural: the bucket, the key and the database are provisioned per region, and the tenant is bound to one. A "region selector" in the UI at MVP would imply a capability that does not exist and would invite a customer to change a setting that cannot be honoured.

**Data residency during the move.** The source remains authoritative until the switch; the target is not readable by the tenant; both regions are recorded in the change record. The move is a processing event that the customer has approved, and the DPO signs off before step 5.

### 10.8 Identity Transfer Between Firms

An auditor changing employer is the case that breaks naive identity models: the person is the same, the accountable organization is different, and the grants were issued to the person on the strength of the old firm's attestation.

| Step | Actor | Action |
|------|-------|--------|
| 1 | Auditor or receiving firm | Request transfer, naming the receiving firm and the effective date |
| 2 | Releasing firm administrator | Approve release; confirm the person has left or is leaving |
| 3 | Receiving firm administrator | Approve acceptance; attest the new engagement |
| 4 | Platform Operations | Verify both approvals and the identity evidence; approve the transfer |
| 5 | System | Revoke all sessions; suspend every grant held by the auditor; close memberships |
| 6 | System | Update `home_org_id`; create the new membership at the receiving firm |
| 7 | System | Emit `IdentityTransferred`; notify every client with an affected grant |
| 8 | Client | Decide whether to issue a new grant to the auditor at the new firm |

**Grants do not travel.** A grant was issued to a person accountable to a specific firm. When the firm changes, the independence basis changes, and the client must decide again. Automatically carrying grants across would let an auditor move to a firm the client has never assessed and keep access.

**No self-service.** A transfer requires three approvals and is not a profile edit. The alternative, letting a user change their own organization, turns `home_org_id` into a client-controlled field and breaks every accountability assumption in section 3.2.

**Failure handling.** If the receiving firm rejects after the releasing firm has approved, the identity stays with the releasing firm and no membership is closed. The transfer is a saga with a compensation at each step, not a two-phase commit across two tenants.

### 10.9 Firm Deprovisioning and Grant Cascade

When an audit firm closes its SlateIQ tenant, or removes an auditor, the effect must reach every client without requiring each client to act.

| Step | Action | Result |
|------|--------|--------|
| 1 | Firm administrator deprovisions the auditor | `UserDeactivated` at the firm |
| 2 | System suspends every grant held by the auditor | `GrantSuspended` with reason `GRANTEE_DEPROVISIONED` |
| 3 | System revokes the auditor's sessions and devices | `SessionRevoked`, `WipeQueued` |
| 4 | System notifies every affected client | Owner, administrators and audit manager |
| 5 | Clients decide to revoke formally or leave suspended | Revocation is the normal outcome |
| 6 | Platform Operations reviews after 30 days | Suspended grants with a departed auditor are revoked by the platform if the client has not acted |

**The client's record survives.** The firm cannot delete the client's grant history. Deprovisioning stops access and preserves evidence of the access that existed, which is the correct shape for an audit relationship that ends.

**Firm closure.** A closing firm follows the same path for every auditor, then its own tenant moves to `ARCHIVED`. Any client grant that is still active is suspended and escalated. A firm cannot close while it holds an active grant whose client has not been notified; the closure workflow blocks on notification delivery.

### 10.10 Emergency Revocation Runbook

| Minute | Action | Owner |
|--------|--------|-------|
| 0 | Detect: alert on `RefreshTokenReuseDetected`, `CrossTenantAccessAttempt`, anomalous grant use or a customer report | Security on-call |
| 0 to 5 | Decide the scope: one session, one user, one grant, one firm or one tenant | Security on-call with Platform Operations |
| 5 to 10 | Execute: revoke sessions, suspend the grant, deactivate the identity or suspend the tenant | Platform Operations |
| 10 to 15 | Verify: attempt the revoked access and confirm denial; check for other live sessions | Security on-call |
| 15 to 30 | Notify: tenant administrators, firm administrator and DPO as applicable | Security and DPO |
| 30 to 60 | Assess: query the audit trail for the access window and the objects touched | Security |
| Day 1 | Contain residual: rotate keys if credential compromise is suspected; review other tenants with the same pattern | Platform Operations |
| Day 5 | Postmortem and regression test | Principal Architect |

**Revoke, do not delete.** The first action is always a reversible stop: suspend the grant, revoke the session, deactivate the identity. Deletion destroys the evidence needed for the assessment step and cannot be undone if the alert was a false positive.

**One command, one record.** The emergency revocation endpoint performs the cascade and writes a single `EmergencyRevocation` event with the scope, the actor, the approver and a ticket reference. A runbook that requires an operator to update five tables by hand is a runbook that will be executed incorrectly at 3 a.m.

### 10.11 Scheduled Jobs Touching Tenancy

| Job | Schedule | Purpose | Idempotent | Failure handling |
|-----|----------|---------|------------|------------------|
| Provisioning retry | Every 2 minutes | Resume failed runs | Yes, by `run_id` | Alert after five attempts |
| Isolation probe (new tenants) | On activation | Verify RLS before traffic | Yes | Block activation |
| Invitation expiry | Hourly | Mark expired invitations | Yes | Alert on repeated failure |
| Lockout release | On demand plus hourly sweep | Clear expired locks | Yes | Log only |
| Session purge | Daily | Remove sessions 90 days after expiry | Yes | Alert |
| Device purge | Daily | Remove devices 90 days after revocation | Yes | Alert |
| Grant expiry notice | Daily at 06:00 tenant local | Send T-14, T-7, T-1 notices | Yes, by `(grant_id, stage)` | Retry, then alert |
| Grant expiry | Every 5 minutes | Move `ACTIVE` grants past `end_at` to `EXPIRED` | Yes | Alert; decision-time check is the real control |
| Access review reminder | Daily | Day 7 and day 14 reminders; close campaigns | Yes | Alert |
| Retention sweep | Daily | Apply retention policy per class | Yes | Alert and halt on hold conflict |
| Legal hold check | Before every purge | Block purge on hold | Yes | Hard stop |
| Role drift check | Daily | Compare tenant system roles with the catalogue | Yes | P2 finding |
| Catalogue drift check | Daily | Compare permissions and grant profiles with the manifest | Yes | P2 finding |
| Scope validity check | Daily | Verify assignment and grant scope entities still exist and belong to the tenant | Yes | Revoke invalid scope and alert |
| Orphan access scan | Weekly | Find memberships without a reviewer or recent activity | Yes | Flag for access review |

**Jobs run per tenant, not across tenants.** A job that processes all tenants in one transaction is prohibited by section 7.2. Each tenant's work is a separate transaction with its own context, which also makes a failure isolated to one tenant.

**Job ownership.** Every job has a named owner module and an alert route. A job without an owner is deleted at the next review; unattended jobs are how stale data and silent failures accumulate.

### 10.12 Failure Matrix

| Failure | Detection | Behaviour | Recovery |
|---------|-----------|-----------|----------|
| Provisioning step fails | Step log and timeout | Run `FAILED`; organization `PROVISIONING_FAILED`; compensation starts | Resume or compensate; slug held 24 hours |
| Isolation probe fails | Probe result | Activation blocked; P1 alert | Fix policy or role grant; re-run probe |
| KMS unavailable | Step 7 error | Provisioning halts; no tenant without a key | Retry; escalate to platform on-call |
| Object storage unavailable | Step 6 error | Provisioning halts; no tenant without a bucket | Retry; check regional capacity |
| Invitation email bounces | Delivery result | Administrator sees `DELIVERY_FAILED`; resend allowed | Correct the address; recreate after five resends |
| Grant acceptance window lapses | Expiry job | Grant `DECLINED`; client notified | Create a new grant |
| Grant expiry job late | Monitoring lag | Access still denied at decision time | Investigate job; no data exposure |
| Revocation cascade partially fails | Cascade result in event | Event marked `PARTIAL`; P1 alert; repeated retry | Manual completion with evidence |
| Tenant suspension does not propagate | Probe after suspension | P1 alert; sessions revoked again | Investigate cache and connection pool |
| Purge blocked by legal hold | Hold check | Purge refuses; requester notified with the matter reference | Resolve the hold through its authority |
| Purge verification finds rows | Post-purge probe | Run reopens; certificate withheld | Re-run deletion; investigate FK or partition residue |
| Identity transfer approval missing | Transfer state | Transfer stays `REQUESTED`; no change | Obtain the missing approval or cancel |
| Firm closure with active grants | Closure workflow | Blocked until every client is notified | Notify, then suspend and proceed |

### 10.13 Traceability for Section 10

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| FR-TNT-001 | Sections 10.1, 10.2 | Ten-step provisioning, idempotency, probe and bootstrap |
| FR-TNT-002 | Sections 10.4, 10.5 | Profile and archival flows |
| FR-TNT-003 | Sections 10.4, 10.5, 10.6 | Suspension without deletion; archive; purge with retention |
| FR-TNT-004 | Section 10.3 | Invitation, 72-hour expiry, resend invalidation |
| FR-TNT-006 | Sections 10.9, 10.10 | Deprovisioning cascade and emergency revocation |
| FR-TNT-016 | Sections 10.9, 10.10 | Grant suspension and revocation on firm action |
| FR-TNT-017 | Section 10.11 | Expiry notice and expiry jobs |
| FR-TNT-019 | Section 10.1 | Manual provisioning path with the same controls |
| FR-TNT-021 | Section 10.11 | Access review reminders and orphan scan |
| FR-TNT-023 | Sections 10.1, 10.7 | Region at provisioning; controlled move in Phase 1 |
| NFR-PRV-006 | Sections 10.6, 10.8 | Erasure, purge and identity transfer |
| NFR-PRV-007 | Section 10.10 | Breach notification path |
| NFR-PRV-008 | Sections 10.5, 10.6 | Export and retention evidence |
| NFR-AUD-003 | Sections 10.6, 10.11 | Purge certificate and retention sweep |
| NFR-SEC-005 | Section 10.1 | Isolation probe as an activation gate |
| CAP-001 | Sections 10.1 to 10.7 | Tenant lifecycle capability |
| CAP-002 | Sections 10.2, 10.3, 10.8 | Identity lifecycle capability |

---

---

## 11. API Surface

### 11.1 Role of This Section

Document 9 owns the canonical OpenAPI 3.1 contract, the generated SDKs and the deprecation schedule. This section defines the identity, tenancy and access-control surface that the contract must expose: every endpoint, the permission that authorises it, the scope it evaluates, and the denial it returns. A route that exists in code but not here is an undocumented authority path, which is the class of defect an access review is least likely to find.

The surface covers six families:

| Family | Owner | Purpose |
|--------|-------|---------|
| Authentication and session | MOD-02 Identity | Prove who the caller is and mint revocable authority |
| Identity and device administration | MOD-02 Identity | Manage people, credentials, factors, sessions and devices |
| Membership and invitation | MOD-01 Tenancy | Bring a person into an organization |
| Role, permission and assignment | MOD-01 Tenancy | Define what a member may do, and where |
| Cross-organization grant | MOD-01 Tenancy | Admit an external auditor, bound in time and scope |
| Platform operations | MOD-15 Platform Ops | Provision, suspend, archive and purge tenants; break-glass |

### 11.2 Conventions

These conventions extend Document 6 Appendix A.1 and are binding on Document 9.

| Rule | Detail |
|------|--------|
| Base path | `/api/v1`; health endpoints at `/health/live` and `/health/ready`, outside the version prefix |
| Media type | `application/json; charset=utf-8`; evidence transfer uses presigned object-storage URLs, never multipart upload through the API |
| Authentication | Bearer access token, 15 minutes; rotating refresh token in an HttpOnly `Secure` `SameSite=Lax` cookie on web, platform keystore on mobile |
| Tenant context | The `org` claim is authoritative. `X-Org-Id`, when present, must equal it. A path `{orgId}` must equal it. No request may override the tenant |
| Authorization | Permission key plus scope, evaluated server-side on every request. There is no client-side authority |
| Idempotency | `Idempotency-Key` required on creation of grants, invitations, roles, assignments, provisioning runs, exports and purge requests; retained seven days |
| Concurrency | `ETag` on mutable resources; `If-Match` required on `PATCH`, `PUT` and state transitions; a missing header returns `428`, a stale value returns `409 CONCURRENCY_STALE` |
| Pagination | Opaque cursor, `limit` default 50 and maximum 200, stable sort key, no total for collections above 10,000 rows |
| Filtering | Whitelisted parameters per collection; unknown parameters are rejected with `422 FILTER_UNKNOWN`, never ignored |
| Errors | RFC 9457 `ProblemDetails` with `type`, `title`, `status`, `code`, `detail`, `traceId`, `errors[]`; no stack traces, SQL or internal identifiers |
| Long work | `202 Accepted` plus a job resource and `Location`; the job resource reports `status`, `progress`, `result` and `error` |
| Caching | Authenticated responses are `Cache-Control: no-store`; reference data may be cached with a versioned `ETag` |
| Tracing | `traceparent` accepted and returned; `traceId` appears in every error and audit event |
| Security headers | HSTS, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, a restrictive CSP on the SPA, no `Server` banner |

**Why the tenant claim wins.** A header that can select a tenant is a horizontal-privilege-escalation primitive. The header is retained only as a consistency assertion for clients that already send it, and a mismatch is a client defect, not an alternative context. FR-TNT-018 is satisfied by construction: the server never reads a tenant identifier from a source the caller can choose independently of the token.

**Why no totals on large collections.** A `COUNT(*)` over a tenant's audit or expense table is a second full scan on every page request and leaks volume information across a shared cache. The cursor and an explicit `has_more` flag are sufficient for every screen in the product.

### 11.3 Request Pipeline

Every request passes the same ordered pipeline. A step that fails short-circuits, and the failure is audited with the step name so that an incident review can distinguish "the token was stale" from "the policy said no".

| Step | Check | Failure |
|------|-------|---------|
| 1 | TLS terminated; request ID and `traceparent` assigned | `400` at the edge |
| 2 | Route exists and method is allowed | `404`, `405` |
| 3 | Access token signature, `iss`, `aud`, `exp`, `nbf`, `ver` | `401 TOKEN_INVALID` |
| 4 | `sid` not denylisted; session row exists and is not revoked | `401 SESSION_REVOKED` |
| 5 | Idle timeout and absolute session lifetime | `401 SESSION_EXPIRED` |
| 6 | Tenant status is `ACTIVE` (or `ARCHIVED` for the export-only allowlist) | `403 TENANT_SUSPENDED`, `403 TENANT_ARCHIVED` |
| 7 | `X-Org-Id` and path `{orgId}` agree with the token `org` | `400 TENANT_CONTEXT_MISMATCH`; path mismatch returns `404` |
| 8 | Membership is `ACTIVE`, or an `ACTIVE` grant exists for this org and principal | `403 MEMBERSHIP_INACTIVE`, `403 GRANT_INACTIVE` |
| 9 | Permission key is held through exactly one authority source, and its scope contains the target | `403 PERMISSION_DENIED`, `403 SCOPE_DENIED`; cross-tenant object returns `404` |
| 10 | SoD, approval-limit, step-up and delegation checks for the specific action | `403 SOD_CONFLICT`, `403 APPROVAL_LIMIT_EXCEEDED`, `403 STEP_UP_REQUIRED` |
| 11 | `SET LOCAL app.org_id`, `app.user_id`, `app.scope`, `app.grant_ids`, `app.authority_source` | `500 CONTEXT_UNSET`; never falls back to a default |
| 12 | Handler executes inside one transaction; business change, audit event and outbox event commit together | `500` with a correlation ID |

**Authority-source exclusivity is checked before permission.** A principal is either a member acting on membership authority or a grantee acting on grant authority for the current organization. Combining the two would let an auditor accumulate a member permission set and a grant ceiling, and the effective result would be the union rather than the intersection (section 6.4). The pipeline rejects a request that presents both.

**Step 7 is an assertion, not a selector.** A browser tab that still carries an old `X-Org-Id` after a workspace switch receives `400` and refreshes its context. It never receives data from the organization named in the header unless the token already authorises that organization.

**The pipeline runs before model binding.** A malformed body from an unauthorised caller must not reach the validator, because validator error messages can disclose field names and entity existence. Authorization precedes deserialisation for every route that names a resource.

### 11.4 Error Model and Denial Semantics

| HTTP | `code` | Meaning | Audit |
|------|--------|---------|-------|
| 400 | `TENANT_CONTEXT_MISMATCH` | Header, path and token disagree | Security event, warning |
| 401 | `TOKEN_INVALID` | Signature, issuer, audience or expiry failed | Security event |
| 401 | `TOKEN_EXPIRED` | Access token past `exp`; client refreshes | None |
| 401 | `SESSION_REVOKED` | Session revoked, denylisted or family burned | Security event if not a normal logout |
| 401 | `SESSION_EXPIRED` | Idle or absolute bound reached | None |
| 401 | `INVALID_CREDENTIALS` | Login failed; deliberately indistinguishable from unknown account | `LoginFailed` |
| 401 | `MFA_INVALID` | Wrong or reused TOTP code | `MfaChallengeFailed` |
| 403 | `PERMISSION_DENIED` | Permission key not held | `PermissionDenied` |
| 403 | `SCOPE_DENIED` | Permission held, target outside scope | `PermissionDenied` with scope reason |
| 403 | `MEMBERSHIP_INACTIVE` | Membership suspended, left or never active | Security event |
| 403 | `GRANT_INACTIVE` | Grant not `ACTIVE`, or authority source not `GRANT` | `GrantAccessDenied` |
| 403 | `GRANT_EXPIRED` | `end_at` reached, even if the expiry job is late | `GrantAccessDenied` |
| 403 | `TENANT_SUSPENDED` | Tenant is not active | `TenantAccessDenied` |
| 403 | `TENANT_ARCHIVED` | Tenant is read-only; only export endpoints are open | `TenantAccessDenied` |
| 403 | `STEP_UP_REQUIRED` | Action class requires fresh strong authentication | `StepUpRequired` |
| 403 | `SOD_CONFLICT` | Requested combination is prohibited | `SodBlocked` |
| 403 | `SELF_APPROVAL_PROHIBITED` | Actor is creator, submitter or beneficiary | `SodBlocked` |
| 403 | `APPROVAL_LIMIT_EXCEEDED` | Amount above the actor's effective limit | `ApprovalDenied` |
| 404 | `RESOURCE_NOT_FOUND` | Absent, or exists in another tenant; indistinguishable | Security event for cross-tenant probe |
| 409 | `CONCURRENCY_STALE` | `If-Match` did not match | None |
| 409 | `IDEMPOTENCY_KEY_REUSED` | Same key, different request payload | Security event if repeated |
| 409 | `GRANT_OVERLAP` | Another live grant exists for client and grantee | None |
| 409 | `GRANT_NOT_APPROVABLE` | State or independence check failed | `GrantApprovalDenied` |
| 409 | `ROLE_SYSTEM_IMMUTABLE` | Attempt to edit a seeded role | Security event |
| 409 | `ROLE_IN_USE` | Role still assigned at retirement | None |
| 409 | `MULTI_ORG_NOT_SUPPORTED` | Invitee already has an active membership at MVP | None |
| 409 | `LEGAL_HOLD_ACTIVE` | Deletion or purge blocked by a hold | `LegalHoldBlocked` |
| 410 | `INVITATION_INVALID` | Expired, used or superseded invitation | None |
| 410 | `RESET_TOKEN_INVALID` | Expired or used reset token | Security event on reuse |
| 410 | `CHALLENGE_EXPIRED` | MFA challenge past its five-minute life | None |
| 422 | `VALIDATION_FAILED` | Field-level errors in `errors[]` | None |
| 422 | `PASSWORD_POLICY_VIOLATION` | Length, class, breach or identity-token check failed | None |
| 422 | `GRANT_TERM_INVALID` | Dates absent, inverted or beyond twelve months | None |
| 422 | `GRANT_SCOPE_INVALID` | Scope type or identifiers invalid or foreign | Security event if foreign |
| 422 | `CROSS_REGION_PROHIBITED` | Client policy forbids cross-region grants | None |
| 423 | `ACCOUNT_LOCKED` | Five failures; fifteen-minute lock | `AccountLocked` |
| 429 | `RATE_LIMITED` | Limit exceeded; `Retry-After` present | Aggregate metric, sampled event |
| 500 | `CONTEXT_UNSET` | Database context missing; request refused | Critical alert |
| 503 | `ISOLATION_PROBE_FAILED` | Activation probe failed | Critical alert |
| 503 | `DEPENDENCY_UNAVAILABLE` | KMS, database or storage unavailable | Alert |

**403 and 404 are not interchangeable.** A member of the tenant who lacks a permission receives `403 PERMISSION_DENIED`; a caller who names a resource in another tenant receives `404 RESOURCE_NOT_FOUND`, because a `403` would confirm that the identifier exists. Both write a security event, and the `404` case is sampled into the cross-tenant probe metric. The rule is tested per endpoint (section 16.5).

**Errors never echo authority data.** The response does not list the permissions the caller holds, the role that would have allowed the action, or the owner of the target. The `detail` field names the action in business language and the next step, as NFR-UX-008 requires, without disclosing the policy graph.

**Rate-limit responses do not reveal account existence.** A login for an unknown account and a login for a known account with a wrong password return the same `401 INVALID_CREDENTIALS`, the same timing profile (a dummy Argon2id verification runs for unknown accounts), and the same lockout behaviour keyed by the submitted address hash.

### 11.5 Authentication Endpoints

| Method | Path | Authority | Scope | Behaviour and denial |
|--------|------|-----------|-------|----------------------|
| POST | `/auth/login` | Public | — | Email and password. Returns `200` with tokens, or `200` with `mfa_required`, `challenge_id` and `methods`. `401 INVALID_CREDENTIALS`, `403 ACCOUNT_SUSPENDED`, `403 TENANT_SUSPENDED`, `423 ACCOUNT_LOCKED`, `429 RATE_LIMITED` |
| POST | `/auth/mfa/verify` | Challenge | — | `challenge_id` plus TOTP. Returns tokens. `401 MFA_INVALID`, `410 CHALLENGE_EXPIRED`, `423 ACCOUNT_LOCKED` after five challenge failures |
| POST | `/auth/mfa/recovery` | Challenge | — | Single-use recovery code; consumes the code and increments the generation. `401 MFA_INVALID`, `410 CHALLENGE_EXPIRED` |
| POST | `/auth/refresh` | Refresh cookie | — | Rotates the refresh token. `401 SESSION_EXPIRED`, `401 TOKEN_REUSED` (family revoked), `403 TENANT_SUSPENDED` |
| POST | `/auth/logout` | Authenticated | `SELF` | Revokes the current session and denylists the `sid`. `204` |
| POST | `/auth/logout-all` | Authenticated | `SELF` | Revokes every session for the user, increments `scope_version`, queues device re-authentication. `204` |
| POST | `/auth/password/reset-request` | Public | — | Always `202`, whether or not the address exists. Token 256-bit, 30 minutes, single use |
| POST | `/auth/password/reset` | Reset token | — | `410 RESET_TOKEN_INVALID`, `422 PASSWORD_POLICY_VIOLATION`. Revokes all sessions on success |
| POST | `/auth/password/change` | Authenticated | `SELF` | Requires the current password and, for privileged roles, step-up. Revokes other sessions |
| POST | `/auth/step-up` | Authenticated | `SELF` | Password plus second factor; returns a single-use assertion bound to the session and an action class. `403 STEP_UP_REQUIRED` when the method is insufficient |
| POST | `/auth/device/register` | Authenticated mobile | `SELF` | Accepts the device public key and platform attestation; returns `device_id`. `409 DEVICE_LIMIT_REACHED` |
| POST | `/auth/device/verify` | Device key | `SELF` | Verifies a signed challenge during refresh or offline re-entry |

**Login does not disclose the organization.** The email is globally unique among active identities, so the client does not send an organization slug. A person with one membership at MVP has one possible tenant; Phase 1 multi-organization membership returns a tenant chooser after authentication, never before, because a pre-authentication tenant list leaks customer relationships.

**MFA challenge is a state, not an error.** Returning `200` with `mfa_required` avoids client code that treats the normal second step as a failure and avoids logging a false authentication error on every login.

**Refresh is the only endpoint that extends a session.** A stolen access token cannot extend its own life; it can only make requests until `exp`, and the session can be revoked underneath it. This is why the access token is short and the refresh token is the high-value secret (section 4.7).

**Federation is Phase 1.** The SAML 2.0 and OIDC endpoints (`/auth/sso/{idpId}/start`, `/auth/sso/callback`, `/auth/sso/metadata`) are reserved and return `404` at MVP. FR-TNT-009 is a Should requirement at Phase 1 and the MVP does not ship a half-configured identity provider integration.

### 11.6 Identity and Device Administration

All routes below are scoped to the active organization. `ORG` means the caller must hold the permission over the organization's member directory; `SELF` means the caller may act only on their own record.

| Method | Path | Permission | Scope | Behaviour and denial |
|--------|------|------------|-------|----------------------|
| GET | `/me` | Authenticated | `SELF` | Profile, active membership, locale, timezone, MFA state. No other tenants |
| PATCH | `/me` | `identity.user.update` | `SELF` | Display name, phone, locale, timezone, notification preferences. Email change starts a verification flow; it is not a direct update |
| GET | `/me/permissions` | Authenticated | `SELF` | Effective permission keys and scope summary for the active organization. `Cache-Control: no-store` |
| GET | `/me/access-explanations` | Authenticated | `SELF` | Explains a decision for one `permission` and `resource` query. Returns `allowed`, `reason_code`, `authority_source`, `scope_result`, `vetoes[]` |
| GET | `/organizations/{orgId}/users` | `identity.user.read` | `ORG` | Directory with status, roles and last activity. Filters: `status`, `roleId`, `q`, `mfaState` |
| GET | `/organizations/{orgId}/users/{userId}` | `identity.user.read` | `ORG` | Detail. Cross-tenant identifier returns `404` |
| PATCH | `/organizations/{orgId}/users/{userId}` | `identity.user.update` | `ORG` | Administrative fields only; the person's own preferences are `SELF` |
| POST | `/organizations/{orgId}/users/{userId}/deactivate` | `identity.user.delete` | `ORG` | FR-TNT-006. Revokes sessions within 60 seconds, suspends grants, reassigns or flags pending approvals. `409 LAST_ADMIN` if the target is the last active administrator |
| POST | `/organizations/{orgId}/users/{userId}/reactivate` | `identity.user.update` | `ORG` | Restores the membership; does not restore sessions or suspend grants |
| POST | `/organizations/{orgId}/users/{userId}/password-reset` | `identity.credential.administer` | `ORG` | Issues a reset link; the administrator never sees or sets a password. Step-up required |
| POST | `/organizations/{orgId}/users/{userId}/mfa-reset` | `identity.mfa_factor.administer` | `ORG` | Removes a lost factor after identity verification and a second approver. Step-up required. `409 MFA_RESET_NOT_ALLOWED` for self-service |
| GET | `/organizations/{orgId}/users/{userId}/sessions` | `identity.session.read` | `ORG` | Active sessions with device class and last seen. No token material |
| DELETE | `/organizations/{orgId}/users/{userId}/sessions/{sessionId}` | `identity.session.delete` | `ORG` | Revokes one session. `404` if the session is not the target's |
| GET | `/organizations/{orgId}/users/{userId}/devices` | `identity.device.read` | `ORG` | Registered devices, trust state and wipe state |
| DELETE | `/organizations/{orgId}/users/{userId}/devices/{deviceId}` | `identity.device.delete` | `ORG` | Revokes and queues a wipe. `202` until acknowledged |
| GET | `/organizations/{orgId}/users/{userId}/export` | `identity.user.export` | `ORG` | Subject access export. `202` plus a job; the package is encrypted and time-limited |
| POST | `/organizations/{orgId}/users/{userId}/erase` | `identity.user.delete` | `ORG` | Crypto-shred after legal-hold check. `409 LEGAL_HOLD_ACTIVE`; two approvers required |

**Administrators cannot read secrets.** No endpoint returns a password hash, TOTP secret, recovery code, refresh token or service secret. The only credential operation available to an administrator is a reset, which produces a one-time link delivered to the person, not a value displayed on screen.

**MFA reset is a high-risk action.** It removes a factor that protects the account, so it requires step-up, a documented reason, a second approver and a notification to the affected person. A help-desk process that resets MFA on a phone call is the most common account-takeover path in enterprise SaaS, and the approval is the control that makes the call insufficient on its own.

**Erasure is not deactivation.** Deactivation preserves the person's records for audit and reassignment. Erasure destroys the personal data key, retains the pseudonymous identifier and the audit trail, and is blocked while a legal hold or an open investigation exists.

### 11.7 Membership and Invitation

| Method | Path | Permission | Scope | Behaviour and denial |
|--------|------|------------|-------|----------------------|
| GET | `/organizations/{orgId}/members` | `tenancy.membership.read` | `ORG` | Members with status, join date and assignments. Excludes `LEFT` by default |
| POST | `/organizations/{orgId}/invitations` | `tenancy.invitation.create` | `ORG` | FR-TNT-004. Email, role and scope required. `201` with invitation ID; `409 DUPLICATE_INVITATION` returns the existing open invitation |
| GET | `/organizations/{orgId}/invitations` | `tenancy.invitation.read` | `ORG` | Pending, expired and accepted invitations with delivery state |
| DELETE | `/invitations/{invitationId}` | `tenancy.invitation.delete` | `ORG` | Revokes the token immediately; the link returns `410` |
| POST | `/invitations/{invitationId}/resend` | `tenancy.invitation.create` | `ORG` | Invalidates the previous token and issues a new one. Five resends maximum, then `409 RESEND_LIMIT` |
| GET | `/invitations/{token}` | Public | — | Preview: organization display name, inviter display name, role name, expiry. No member list, no data |
| POST | `/invitations/{token}/accept` | Public | — | Verifies the token, creates or matches the identity, sets the password, enrolls TOTP if required. `410 INVITATION_INVALID`, `409 MULTI_ORG_NOT_SUPPORTED` |
| PATCH | `/organizations/{orgId}/members/{userId}` | `tenancy.membership.update` | `ORG` | Status change between `ACTIVE` and `SUSPENDED`. Role changes go through assignments, not this route |
| DELETE | `/organizations/{orgId}/members/{userId}` | `tenancy.membership.delete` | `ORG` | Ends the membership, revokes sessions and suspends grants. `409 LAST_ADMIN` |
| POST | `/organizations/{orgId}/members/{userId}/leave` | Authenticated | `SELF` | Voluntary departure. Blocked for the last administrator |

**Invitation preview is deliberately thin.** It tells the invitee which organization is inviting them and who sent it, so a phishing invitation can be recognised. It does not confirm whether the address already has an account, and it does not list other members.

**Role and scope are validated when the invitation is created.** An invitation naming a retired role, a foreign show or a scope the inviter cannot confer fails at creation, not at acceptance. An invitation that cannot be accepted is a support ticket waiting to happen.

**Acceptance is a transaction.** Identity creation or match, membership creation, assignment creation and invitation consumption commit together. A partial acceptance would leave a person with a login and no membership, or a membership with no role.

### 11.8 Roles, Permissions and Assignments

| Method | Path | Permission | Scope | Behaviour and denial |
|--------|------|------------|-------|----------------------|
| GET | `/permissions` | `tenancy.permission.read` | `ORG` | Global catalogue, searchable by domain, resource and action. Includes retired keys with `retired_in` |
| GET | `/roles` | `tenancy.role.read` | `ORG` | Seeded and custom roles with permission counts and assignment counts |
| POST | `/roles` | `tenancy.role.create` | `ORG` | Custom role only; `is_system` is server-set and cannot be supplied. `422 UNKNOWN_PERMISSION` |
| GET | `/roles/{roleId}` | `tenancy.role.read` | `ORG` | Role detail and current version |
| PATCH | `/roles/{roleId}` | `tenancy.role.update` | `ORG` | Name and description only. `409 ROLE_SYSTEM_IMMUTABLE` for seeded roles |
| DELETE | `/roles/{roleId}` | `tenancy.role.delete` | `ORG` | Retires a custom role. `409 ROLE_IN_USE` while assignments reference it |
| GET | `/roles/{roleId}/permissions` | `tenancy.role.read` | `ORG` | Current permission keys |
| PUT | `/roles/{roleId}/permissions` | `tenancy.role.update` | `ORG` | Replaces the set in one transaction; bumps the role version and every holder's `scope_version`. `409 ROLE_SYSTEM_IMMUTABLE` |
| GET | `/roles/{roleId}/versions` | `tenancy.role.read` | `ORG` | Immutable snapshots with actor and reason |
| POST | `/roles/{roleId}/clone` | `tenancy.role.create` | `ORG` | Copies a system role into a custom role for controlled extension |
| GET | `/role-assignments` | `tenancy.assignment.read` | `ORG` | Filters by `userId`, `roleId`, `scopeType`, `scopeId`, `validAt` |
| POST | `/role-assignments` | `tenancy.assignment.create` | `ORG` | FR-TNT-013. Scope is mandatory. `403 SCOPE_DENIED` if the actor cannot confer that scope; `422 SCOPE_INVALID` if the target is foreign |
| DELETE | `/role-assignments/{assignmentId}` | `tenancy.assignment.delete` | `ORG` | Ends the assignment immediately and bumps `scope_version` |
| GET | `/users/{userId}/effective-permissions` | `tenancy.assignment.read` | `ORG` | Computed view with the assignment and scope that produced each key. Never cached across users |

**Permission replacement is a diff, not a delete-and-insert.** The service computes added and removed keys, writes a `role_version`, emits `RoleChanged` with both lists, and bumps `scope_version` for every holder. A replacement that fails midway leaves the old set intact.

**Assignments cannot escalate.** The actor may confer only permissions and scopes they hold themselves, and only within their own scope. An Organization Administrator with `ORG` scope can assign a show-scoped role to a member, but a show-scoped manager cannot assign an organization-wide role. This is the "no privilege amplification" rule in section 6.9, applied at write time as well as at decision time.

**Self-assignment is prohibited.** A person cannot create or extend their own assignment, even with the create permission. The check is `actor_user_id <> subject_user_id`, and it applies to role changes, scope widening and delegation.

### 11.9 Cross-Organization Grants

Section 8 defines the lifecycle; this table defines the surface. Grant authority is relationship-based for the grantee and permission-based for the client.

| Method | Path | Permission or relationship | Scope | Behaviour and denial |
|--------|------|----------------------------|-------|----------------------|
| GET | `/access-grants` | `audit.access_grant.read` | `ORG` | The register: state, grantee, firm, profile, scope, owner, dates, last access. Grantees see only their own grants through `GET /me/grants` |
| POST | `/access-grants` | `audit.access_grant.create` | `ORG` | Creates `DRAFT`. `422 GRANT_TERM_INVALID`, `422 GRANT_SCOPE_INVALID`, `422 CROSS_REGION_PROHIBITED`, `409 GRANT_OVERLAP` |
| GET | `/access-grants/{grantId}` | `audit.access_grant.read` or named grantee | `ORG` or `SELF` | Detail. A grantee from another firm receives `404` unless named on the grant |
| PATCH | `/access-grants/{grantId}` | `audit.access_grant.update` | `ORG` | Draft only. A widening on an approved grant returns `409 GRANT_VARIATION_REQUIRES_APPROVAL` |
| DELETE | `/access-grants/{grantId}` | `audit.access_grant.delete` | `ORG` | Draft discard only. A submitted or active grant returns `409 USE_REVOKE` |
| POST | `/access-grants/{grantId}/submit` | `audit.access_grant.create` | `ORG` | `DRAFT` to `PENDING_APPROVAL`. Validates the full form and the grantee's attestation |
| POST | `/access-grants/{grantId}/approve` | `audit.access_grant.approve` | `ORG` | `PENDING_APPROVAL` to `PENDING_ACCEPTANCE`. SOD-07, step-up, Finance Head co-approval for `AUDIT_FINANCE`. `403 SELF_APPROVAL_PROHIBITED`, `409 GRANT_NOT_APPROVABLE` |
| POST | `/access-grants/{grantId}/decline` | `audit.access_grant.approve` | `ORG` | Reason of at least 20 characters. `PENDING_APPROVAL` to `DECLINED` |
| POST | `/access-grants/{grantId}/accept` | Named grantee | `SELF` | `PENDING_ACCEPTANCE` to `ACTIVE` when every gate is satisfied. `409 MFA_REQUIRED`, `409 NDA_REQUIRED`, `409 COI_UNRESOLVED`, `409 GRANT_WINDOW_INVALID` |
| POST | `/access-grants/{grantId}/acknowledge` | Named grantee | `SELF` | Records NDA acceptance and conflict declaration for a specific grant version. Returns the next gate |
| POST | `/access-grants/{grantId}/revoke` | `audit.access_grant.delete` | `ORG` | Immediate. Cascades sessions, cache, exports and workspace. `202` with a cascade job ID; target under 60 seconds |
| POST | `/access-grants/{grantId}/suspend` | `audit.access_grant.update` | `ORG` | Reversible stop with a reason code. Revokes sessions immediately |
| POST | `/access-grants/{grantId}/resume` | `audit.access_grant.approve` | `ORG` | Fresh approval; new acceptance record; dates must still be valid |
| POST | `/access-grants/{grantId}/renew` | `audit.access_grant.create` | `ORG` | Creates a new draft with `supersedes_grant_id`. Does not extend the old grant |
| GET | `/access-grants/{grantId}/versions` | `audit.access_grant.read` | `ORG` | Immutable snapshots with actor, reason and timestamp |
| GET | `/access-grants/{grantId}/evidence` | `audit.access_grant.export` | `ORG` | Evidence pack: approval, acceptance, NDA version, COI state, notices, access log. Step-up above 1,000 rows |
| GET | `/access-grants/{grantId}/access-log` | `audit.access_grant.read` | `ORG` | Entry, exit, export and denied-action events for this grant |
| GET | `/access-grants/{grantId}/notices` | `audit.access_grant.read` | `ORG` | T-14, T-7, T-1 and T-0 delivery records |
| POST | `/access-grants/{grantId}/conflicts` | Named grantee | `SELF` | Declares a conflict; a declared conflict suspends an active grant and blocks an activating one |
| POST | `/access-grants/{grantId}/conflicts/{declarationId}/resolve` | `audit.access_grant.approve` | `ORG` | Records the client's written decision. `403 SOD_CONFLICT` if the resolver is the grantee |
| GET | `/me/grants` | Authenticated | `SELF` | The grantee's own grants across clients, with state and expiry. No client data |

**The grantee never holds a tenant permission.** `accept`, `acknowledge` and `conflicts` are authorised by the named-grantee relationship plus the grant state, not by a role. An external auditor with no client membership must still be able to accept, and inventing a client-side role for them would create exactly the membership the model forbids.

**Approval and acceptance are separate calls.** A single "activate" endpoint that performs both would let the client's approval and the auditor's consent collapse into one timestamp, destroying the evidence that two parties acted. FR-TNT-016 requires both.

**Revocation returns `202`, not `204`.** The cascade touches sessions, caches, exports and the workspace. The API returns a job ID and the target is under 60 seconds; the commitment to the customer remains five minutes (BR-AUD-003), which is met even if a downstream cache is slow.

**A grantee cannot enumerate clients.** `GET /me/grants` lists only grants where `grantee_user_id = sub`. There is no search by client name, slug or email, because a directory of clients is itself confidential.

### 11.10 Access Reviews, Delegation and Segregation of Duties

| Method | Path | Permission | Scope | Behaviour and denial |
|--------|------|------------|-------|----------------------|
| GET | `/access-reviews` | `tenancy.access_review.read` | `ORG` | Campaigns with period, reviewer, progress and state |
| POST | `/access-reviews` | `tenancy.access_review.create` | `ORG` | FR-TNT-021. Creates a campaign covering every member and every active grant. `202`; one campaign per quarter is enforced |
| GET | `/access-reviews/{reviewId}` | `tenancy.access_review.read` | `ORG` | Campaign detail with counts by decision |
| GET | `/access-reviews/{reviewId}/items` | `tenancy.access_review.read` | `ORG` | Subject, current access, last activity, proposed decision |
| POST | `/access-reviews/{reviewId}/decide` | `tenancy.access_review.approve` | `ORG` | Certify or revoke one item. `403 SELF_APPROVAL_PROHIBITED` when the reviewer reviews their own access; `409 CAMPAIGN_CLOSED` |
| POST | `/access-reviews/{reviewId}/close` | `tenancy.access_review.update` | `ORG` | Closes when every item is decided. `409 REVIEW_INCOMPLETE` otherwise; revocations execute on close |
| GET | `/approval-delegations` | `tenancy.delegation.read` | `ORG` | Delegations with delegator, delegate, permission, scope, dates and amount |
| POST | `/approval-delegations` | `tenancy.delegation.create` | `ORG` | FR-TNT-020. Maximum 90 days, no sub-delegation, amount at or below the delegator's limit. `422 DELEGATION_INVALID`, `403 APPROVAL_LIMIT_EXCEEDED` |
| DELETE | `/approval-delegations/{delegationId}` | `tenancy.delegation.delete` | `ORG` | Revokes immediately; actions already taken retain both identities |
| GET | `/approval-limits` | `tenancy.approval_limit.read` | `ORG` | Role and user limits with effective dates |
| PUT | `/approval-limits/{limitId}` | `tenancy.approval_limit.update` | `ORG` | A user limit may not exceed the role limit. `422 LIMIT_EXCEEDS_ROLE` |
| GET | `/sod-rules` | `tenancy.sod_rule.read` | `ORG` | The ten seeded rules with enforcement mode and description |
| GET | `/sod-exceptions` | `tenancy.sod_exception.read` | `ORG` | Live and expired exceptions with compensating controls |
| POST | `/sod-exceptions` | `tenancy.sod_exception.create` | `ORG` | Two approvers, reason, compensating control, maximum 90 days. `409 SOD_EXCEPTION_OVERLAP` |
| POST | `/sod-exceptions/{exceptionId}/revoke` | `tenancy.sod_exception.delete` | `ORG` | Revokes before expiry; audited at high severity |

**Self-review is prohibited.** The reviewer of an access campaign cannot certify their own membership, their own grants or their own role assignments. The item is reassigned to another reviewer or escalated to the Organization Administrator, and if neither exists the campaign cannot close, which is the correct visible failure.

**Delegation never transfers identity.** The delegate acts as themselves, and the approval records both `delegator_user_id` and `delegate_user_id`. The delegate cannot approve their own expense, cannot delegate further, and cannot exceed the delegator's amount or scope. An approval that says only "Finance Head" is not attributable when the Finance Head is on leave.

**SoD exceptions are time-boxed and reviewed.** An exception is not a permanent permission; it is a documented, compensating-controlled acceptance of a conflict for at most 90 days, approved by two people who are not the subject. It appears in the quarterly access review whether or not it is still live.

### 11.11 The Auditor Workspace

| Method | Path | Authority | Scope | Behaviour and denial |
|--------|------|-----------|-------|----------------------|
| POST | `/workspace/{clientSlug}/enter` | `audit.access_grant.read` plus an `ACTIVE` grant | `GRANT` | Mints a client-org access token bound to `grant_id`. `403 GRANT_INACTIVE`, `403 GRANT_EXPIRED`, `403 TENANT_SUSPENDED`, `404` for a client the caller has no grant to |
| POST | `/workspace/exit` | Grant-bound session | `GRANT` | Closes the grant context and returns to the home organization. `204`; audited as `WorkspaceClosed` |
| GET | `/workspace/context` | Grant-bound session | `GRANT` | Banner data: client display name, grant ID, profile, scope labels, expiry, region notice. No client business data |
| GET | `/workspace/notifications` | Grant-bound session | `GRANT` | Unread findings and assignments for this grant only |

**Workspace entry is not a second login.** The auditor authenticates once at the home organization and then enters a client workspace. The new token has `org = client_org_id` and the same `sid` family; the server records `authority_source = GRANT` and `authority_id = grant_id` on the session context. No client refresh token is issued, so leaving the workspace cannot leave a client-authorised refresh token on the auditor's device.

**Route shape is deliberately not `/organizations/{orgId}`.** An external principal using the member API would need a membership to pass the pipeline, and a membership is exactly what the grant model forbids. The workspace route makes the authority source explicit in the URL, the token and the audit trail.

**One workspace at a time.** A second `enter` call while another workspace is open returns `409 WORKSPACE_ALREADY_OPEN` and names the open client. Switching requires `exit` first. This is a usability cost that prevents the most common cross-tenant error: acting in the wrong client's context.

### 11.12 Platform Operations

These routes are not available to tenant members, are not reachable from the tenant hostname, and are excluded from tenant role templates. They require `PLATFORM_INTERNAL` membership plus a platform permission.

| Method | Path | Permission | Scope | Behaviour and denial |
|--------|------|------------|-------|----------------------|
| GET | `/admin/tenants` | `ops.tenant.read` | `PLATFORM` | Inventory with region, status, plan and last provisioning result |
| POST | `/admin/tenants` | `ops.tenant.create` | `PLATFORM` | Creates a provisioning request. `202` with `run_id`; idempotency key required |
| GET | `/admin/tenants/{orgId}` | `ops.tenant.read` | `PLATFORM` | Tenant detail, entitlements and region |
| POST | `/admin/tenants/{orgId}/provision` | `ops.tenant.administer` | `PLATFORM` | Starts or resumes the ten-step saga. `202`; `503 ISOLATION_PROBE_FAILED` if the probe fails |
| GET | `/admin/provisioning-runs/{runId}` | `ops.tenant.read` | `PLATFORM` | Step, status, step log and failure compensation |
| POST | `/admin/tenants/{orgId}/suspend` | `ops.tenant.administer` | `PLATFORM` | Reason code; second approver for security and legal reasons. `202`; effective within 60 seconds |
| POST | `/admin/tenants/{orgId}/reactivate` | `ops.tenant.administer` | `PLATFORM` | Restores the tenant; does not restore sessions |
| POST | `/admin/tenants/{orgId}/archive` | `ops.tenant.administer` | `PLATFORM` | Read-only plus export. `202` |
| POST | `/admin/tenants/{orgId}/purge` | `ops.tenant.administer` | `PLATFORM` | Two approvers, legal-hold check, 30-day grace. `409 LEGAL_HOLD_ACTIVE` |
| POST | `/admin/tenants/{orgId}/isolation-probe` | `ops.tenant.administer` | `PLATFORM` | Runs the cross-tenant read and write probe as `slateiq_app`. Returns the evidence record |
| POST | `/admin/support-access` | `ops.support_access.create` | `PLATFORM` | Requests time-boxed support access with a ticket and reason; maximum 60 minutes |
| POST | `/admin/support-access/{supportAccessId}/approve` | `ops.support_access.approve` | `PLATFORM` | Second approver; cannot be the requester |
| POST | `/admin/support-access/{supportAccessId}/end` | `ops.support_access.create` | `PLATFORM` | Ends early; the grant expires automatically at the deadline |
| GET | `/admin/identity-transfers` | `ops.identity_transfer.read` | `PLATFORM` | Transfers with the three approval states |
| POST | `/admin/identity-transfers/{transferId}/approve` | `ops.identity_transfer.approve` | `PLATFORM` | Third approval after both firms; executes the transfer saga |
| GET | `/admin/service-accounts` | `ops.service_account.read` | `PLATFORM` | Machine clients, owner, scope and expiry |
| POST | `/admin/service-accounts` | `ops.service_account.create` | `PLATFORM` | Creates a scoped account; the secret is shown once |
| POST | `/admin/service-accounts/{id}/rotate` | `ops.service_account.update` | `PLATFORM` | Rotates the secret with an overlap window |
| DELETE | `/admin/service-accounts/{id}` | `ops.service_account.delete` | `PLATFORM` | Disables immediately and revokes tokens |

**Support access does not impersonate.** The operator's token carries `sub = operator_user_id`, `org = target_org_id` and `authority_source = SUPPORT`, with a server-side `support_access_id`. The audit trail records the operator, the approver, the ticket and the actions. The operator never receives the customer's credentials, and the customer sees the access in its own audit view.

**Break-glass is bounded and loud.** A support session is at most 60 minutes, requires two people, is scoped to named read or write actions, and emits `SupportAccessGranted` and `SupportAccessEnded`. Any write action during support access opens a review item automatically. A silent back door is worse than no support tooling, because it is invisible to the customer's auditor.

**Purge and suspend require a second person.** The platform role that can suspend a tenant is powerful enough to cause an outage; the role that can purge is irreversible. Both require a second approver, and neither is available to a single on-call engineer at 3 a.m. without the escalation path in section 10.10.

### 11.13 Rate Limits and Abuse Controls

NFR-SEC-013 requires public endpoints to be rate-limited and anomaly-monitored. Limits are per identity where authenticated, per IP class where not, and are enforced at the edge and again in the service so that an internal caller cannot bypass them.

| Endpoint class | Limit | Window | Key | Response |
|----------------|-------|--------|-----|----------|
| Login | 10 | 1 minute | IP class | `429`, `Retry-After` |
| Login | 5 | 15 minutes | Account | `423 ACCOUNT_LOCKED` (FR-TNT-007) |
| MFA verify | 5 | 5 minutes | Challenge | `423`; challenge invalidated |
| Refresh | 60 | 1 minute | Session | `429` |
| Password reset request | 3 | 1 hour | Account and IP | `202` regardless; no enumeration |
| Invitation accept | 10 | 1 hour | IP class | `429` |
| Grant creation | 30 | 1 hour | Organization | `429` |
| Grant approval | 60 | 1 hour | User | `429` |
| Access review decision | 300 | 1 hour | User | `429` |
| Export | 10 | 1 hour | User | `429`; step-up above 1,000 rows |
| Admin mutation | 60 | 1 minute | User | `429` |
| Any authenticated route | 600 | 1 minute | User | `429` |

**Limits are not the lockout.** The account lockout in FR-TNT-007 is a credential control: five failures lock the account for fifteen minutes regardless of source address. The rate limit is a traffic control: it slows a distributed attempt that spreads failures across addresses. Both are needed; either alone is bypassable.

**Anomaly detection consumes the same events.** `LoginFailed`, `MfaChallengeFailed`, `RefreshTokenReuseDetected`, `PermissionDenied` and `CrossTenantAccessAttempt` feed the detection rules in section 15. A rate limit that fires without a correlated event is a blind spot, and an event without a limit is a log entry that arrives after the damage.

**No limit discloses existence.** A `429` is returned for a known and an unknown account alike, and the `Retry-After` value is computed from the key, not from account state.

### 11.14 Idempotency, Concurrency and Pagination

**Idempotency.** The `Idempotency-Key` header is required on the routes in the table below. The server stores a hash of the method, path, actor and canonical body with the response status and body. A replay with the same key and the same hash returns the stored response with `Idempotent-Replay: true`; a replay with a different hash returns `409 IDEMPOTENCY_KEY_REUSED`.

| Route | Retention | Reason |
|-------|-----------|--------|
| `POST /access-grants` | 7 days | A double-submitted grant creates two external authorities |
| `POST /access-grants/{id}/approve` | 7 days | A retried approval must not produce two approval records |
| `POST /access-grants/{id}/revoke` | 7 days | A retried revocation must not fail the second time and confuse the operator |
| `POST /organizations/{orgId}/invitations` | 7 days | A double invitation sends two emails and creates two tokens |
| `POST /role-assignments` | 24 hours | A double assignment duplicates authority |
| `POST /admin/tenants` | 7 days | A double provisioning run creates two tenants or two regions |
| `POST /admin/tenants/{orgId}/purge` | 7 days | A retried purge must not restart a completed destruction |
| `POST /access-reviews` | 24 hours | A double campaign doubles the review workload |

**Concurrency.** Mutable resources return `ETag`. The update must send `If-Match`; a missing header returns `428 PRECONDITION_REQUIRED`, a mismatch returns `409 CONCURRENCY_STALE` with the current `ETag`. Grant state transitions are additionally serialised by `SELECT ... FOR UPDATE` on the grant row, so two approvers clicking at the same time produce one approval and one `409 GRANT_NOT_APPROVABLE`.

**Pagination.** Collections use an opaque cursor encoding the last sort key and the tie-breaker ID. The default sort is `created_at DESC, id DESC` unless the collection declares another stable order (the grant register uses `end_at ASC, grant_id ASC` so the nearest expiry is first). `limit` above 200 returns `422 LIMIT_EXCEEDED`, not a silent clamp, because a client that asks for 10,000 rows and receives 200 will paginate incorrectly.

**Filtering is whitelisted.** The grant register accepts `state`, `granteeUserId`, `firmOrgId`, `profileKey`, `scopeType`, `scopeId`, `endsBefore`, `ownerUserId`. The member directory accepts `status`, `roleId`, `q`, `mfaState`. An unknown parameter returns `422 FILTER_UNKNOWN`. A filter on a field the caller cannot read returns `403`, not an empty list, so that the filter cannot be used to probe for hidden records.

**Export is asynchronous.** A request above 1,000 rows returns `202` with a job; the job produces a watermarked CSV or PDF, stores it under the tenant prefix, and returns a presigned URL valid for 15 minutes (NFR-SEC-008). The export event carries the row count and the watermark ID. The file is deleted after seven days.

### 11.15 Versioning and Deprecation

| Rule | Detail |
|------|--------|
| Version in path | `/api/v1`; a breaking change creates `/api/v2`, never a silent change to `v1` |
| Additive changes | New optional fields, new enum values in a response, and new endpoints are non-breaking |
| Breaking changes | Removing a field, changing a type, tightening validation, changing a default, or changing an error code |
| Deprecation | Announced at least six months before removal, with `Deprecation` and `Sunset` headers |
| Contract gate | The OpenAPI document is generated from code and diffed in CI; a breaking diff fails the build unless the version is bumped |
| Enum evolution | Clients must tolerate unknown enum values; the server never removes a value without a deprecation cycle |
| Error codes | Stable; a code is never reused for a different meaning. New codes are additive |

**Why the error code is part of the contract.** Clients branch on `code`, not on prose. Renaming `GRANT_INACTIVE` to `GRANT_NOT_ACTIVE` is a breaking change even though the status stays 403, and the versioning rule treats it as one.

### 11.16 API Test Obligations

| Obligation | Coverage |
|------------|----------|
| Every route has a permission declaration | Route inventory test; an undeclared route fails the build |
| Every route has an allowed and a denied test | Generated matrix from the permission catalogue; `403` for missing permission, `404` for cross-tenant object |
| Every route rejects a mismatched tenant header | `X-Org-Id` and path mismatch tests |
| Every state transition rejects an invalid state | Grant, invitation, membership and role transition tests |
| Every idempotent route replays correctly | Same key and body returns the stored response; different body returns `409` |
| Every mutable route enforces `If-Match` | Missing header `428`, stale `409` |
| Every error response is RFC 9457 | Schema validation on all error paths |
| Every denied action writes an audit event | Audit assertion per denial case |
| Every endpoint is covered by the OpenAPI contract | Contract diff in CI |

### 11.17 Traceability for Section 11

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| FR-TNT-001 | Sections 11.12, 10.1 | Tenant creation and provisioning endpoints |
| FR-TNT-002 | Sections 11.6, 11.7 | Organization profile and settings routes |
| FR-TNT-003 | Section 11.12 | Suspend, reactivate, archive and purge routes |
| FR-TNT-004 | Section 11.7 | Invitation creation, preview, acceptance and resend |
| FR-TNT-005 | Section 11.6 | `/me` profile routes |
| FR-TNT-006 | Section 11.6 | Deactivate route with session revocation |
| FR-TNT-007 | Section 11.5, 11.13 | Login, MFA, lockout and rate limits |
| FR-TNT-008 | Section 11.5 | Password change and reset routes; policy errors |
| FR-TNT-009 | Section 11.5 | SSO routes reserved for Phase 1 |
| FR-TNT-010 | Section 11.8 | Role create, update, retire and version routes |
| FR-TNT-011 | Sections 11.8, 9.5 | Permission catalogue endpoint and table |
| FR-TNT-012 | Section 11.8 | Permission replacement with versioning |
| FR-TNT-013 | Section 11.8 | Assignment routes with mandatory scope |
| FR-TNT-014 | Sections 11.3, 11.16 | Pipeline and per-route authorization tests |
| FR-TNT-015 | Section 11.9 | Grant creation with profile, scope and dates |
| FR-TNT-016 | Section 11.9 | Accept, acknowledge, revoke and cascade routes |
| FR-TNT-017 | Sections 11.9, 8.9 | Register, notices and access log routes |
| FR-TNT-018 | Sections 11.2, 11.3 | Token-authoritative tenant context and mismatch rejection |
| FR-TNT-019 | Section 11.12 | Entitlement and plan routes |
| FR-TNT-020 | Section 11.10 | Delegation routes with dates and amount |
| FR-TNT-021 | Section 11.10 | Access review campaign and decision routes |
| FR-TNT-022 | Sections 11.5, 4.6 | Session, logout and idle enforcement |
| FR-TNT-023 | Sections 11.12, 2.8 | Region at provisioning; no selector at MVP |
| FR-TNT-024 | Sections 11.7, 10.3 | Bulk invitation deferred to Phase 1 |
| NFR-SEC-003 | Sections 11.5, 11.6 | MFA login and MFA reset controls |
| NFR-SEC-004 | Section 11.3 | Permission, RLS and tenant context on every request |
| NFR-SEC-011 | Section 11.6 | Device revoke and wipe routes |
| NFR-SEC-013 | Section 11.13 | Rate limits and anomaly events |
| NFR-AUD-001 | Sections 11.4, 11.16 | Denial and state-change audit obligations |
| NFR-AUD-004 | Section 11.2 | `traceId` and `traceparent` propagation |
| NFR-AUD-005 | Sections 11.10, 9.12 | Query targets and index requirements |
| NFR-MOB-004 | Sections 11.5, 4.8 | Device registration and offline bounds |
| NFR-PRV-006 | Section 11.6 | Subject export and erasure routes |
| NFR-UX-008 | Section 11.4 | Error detail names the problem and next action |
| CAP-001 | Sections 11.7, 11.12 | Tenant membership and lifecycle API |
| CAP-002 | Sections 11.6, 11.9 | Identity and external access API |
| CAP-003 | Section 11.8 | Role and permission API |

---

## 12. Event Catalogue

### 12.1 Role and Principles

Events are the integration contract between modules and the evidence stream for security and audit. Identity and tenancy events have three properties that ordinary domain events do not:

1. **They are security evidence.** A denied authorization, a revoked session and a failed MFA challenge are material to an incident investigation even though no business object changed.
2. **They cross module boundaries without carrying authority.** A consumer learns that a role changed; it does not receive the caller's token, permission set or session.
3. **They are tenant-scoped.** Every event carries the `org_id` whose data or authority changed, and a consumer applies RLS with that value. An event that spans tenants is split into one event per tenant rather than emitted once with a list.

**One event, one aggregate.** An event describes a change to one aggregate. A bulk operation that changes 200 memberships emits 200 `RoleAssignmentCreated` events plus one `BulkOperationCompleted` summary, not one event with a 200-row payload. Ordering is per aggregate, and a multi-aggregate payload cannot be ordered.

### 12.2 Envelope

The envelope extends Document 6 Appendix B.1. Fields marked optional are additive and do not change the version for existing consumers.

| Field | Type | Required | Purpose |
|-------|------|----------|---------|
| `event_id` | UUIDv7 | Yes | Global deduplication key |
| `event_type` | string | Yes | Stable `PascalCase` name |
| `schema_version` | integer | Yes | Incremented only on a breaking payload change |
| `org_id` | UUID | Yes | Tenant whose state or authority changed |
| `aggregate_type` | string | Yes | `Organization`, `User`, `Membership`, `Role`, `Assignment`, `Grant`, `AccessReview`, `Session`, `Device` |
| `aggregate_id` | UUID | Yes | Ordering key and replay scope |
| `occurred_at` | timestamptz | Yes | Server commit time, UTC |
| `actor_id` | UUID or null | Yes | Human or service actor; null for system |
| `actor_type` | enum | Yes | `USER`, `SERVICE`, `SYSTEM`, `PLATFORM` |
| `actor_org_id` | UUID or null | Optional | The actor's home organization when it differs from `org_id` |
| `authority_source` | enum | Optional | `MEMBERSHIP`, `GRANT`, `SUPPORT`, `PLATFORM`, `SYSTEM` |
| `authority_id` | UUID or null | Optional | Assignment or grant that authorised the action |
| `correlation_id` | UUID | Yes | Ties the event to the request, audit rows and deliveries |
| `traceparent` | string | Yes | W3C trace context |
| `payload` | object | Yes | Event-specific fields; identifiers and codes, no personal data |
| `pii_class` | enum | Optional | Highest classification present in the payload: `NONE`, `INTERNAL`, `CONFIDENTIAL`, `PERSONAL` |

**Example.**

```json
{
  "event_id": "0193a7c2-6f1e-7c3a-9d2b-4a8f0c1e5b77",
  "event_type": "GrantRevoked",
  "schema_version": 1,
  "org_id": "0193a7c2-1a2b-7c3d-8e4f-5a6b7c8d9e0f",
  "aggregate_type": "Grant",
  "aggregate_id": "0193a7c2-2b3c-7d4e-9f5a-6b7c8d9e0f10",
  "occurred_at": "2026-11-05T09:14:22.481Z",
  "actor_id": "0193a7c2-3c4d-7e5f-8a6b-7c8d9e0f1a2b",
  "actor_type": "USER",
  "actor_org_id": "0193a7c2-1a2b-7c3d-8e4f-5a6b7c8d9e0f",
  "authority_source": "MEMBERSHIP",
  "authority_id": "0193a7c2-4d5e-7f6a-9b7c-8d9e0f1a2b3c",
  "correlation_id": "0193a7c2-5e6f-7a8b-9c8d-9e0f1a2b3c4d",
  "traceparent": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "pii_class": "NONE",
  "payload": {
    "grant_id": "0193a7c2-2b3c-7d4e-9f5a-6b7c8d9e0f10",
    "client_org_id": "0193a7c2-1a2b-7c3d-8e4f-5a6b7c8d9e0f",
    "firm_org_id": "0193a7c2-6f7a-7b8c-9d9e-0f1a2b3c4d5e",
    "reason_code": "ENGAGEMENT_ENDED",
    "sessions_revoked": 2,
    "cascade_status": "COMPLETE"
  }
}
```

**Why `authority_id` is in the envelope.** An access event that records only the actor cannot answer "under which grant did the auditor read this show". The authority identifier makes every event attributable to a membership or a grant, which is the first question in an audit-independence dispute.

**Why `pii_class` is present.** Consumers route events to storage with different retention and access rules. An event carrying an email would be personal data; the catalogue below forbids that, and the classifier is a check rather than a permission.

### 12.3 Delivery, Ordering and Idempotency

| Property | Rule |
|----------|------|
| Transactional outbox | The event is written to `ops.outbox_event` in the same transaction as the state change. A failed write aborts the change |
| Delivery | At least once; the relay retries with exponential backoff for 24 hours, then moves the event to the dead-letter queue and alerts |
| Deduplication | Consumers store `event_id` for at least seven days and ignore a duplicate |
| Ordering | Guaranteed per `aggregate_id`; no global order. A consumer that needs cross-aggregate order uses `occurred_at` and accepts gaps |
| Replay | Supported from the outbox and from the archive by `aggregate_id` or time range; replay is a privileged platform action and is itself audited |
| Poison messages | Three failed deliveries move the event to the DLQ; the DLQ has an owner and a weekly review |
| Consumer isolation | One consumer's failure does not block another; each subscription has its own cursor |
| Schema validation | Consumers reject an unknown major `schema_version` and alert; unknown optional fields are ignored |
| Clock | `occurred_at` is the database commit time, not the application host clock; ordering never relies on a client timestamp |

**Why no exactly-once claim.** Exactly-once delivery across a database and a broker is a distributed transaction in disguise. The outbox gives at-least-once with a stable `event_id`, and every consumer is idempotent. A design that claims exactly-once without that proof is a design that will duplicate a revocation or lose a notification.

**Revocation events are ordered by grant.** `GrantRevoked` must not be processed before `GrantActivated` for the same grant, or a consumer could re-open a workspace that was just closed. Per-aggregate ordering plus a state check at the consumer prevents both inversion and replay.

### 12.4 Tenancy Events

| Event | Aggregate | Key payload | Consumers | Notes |
|-------|-----------|-------------|-----------|-------|
| `OrganizationProvisioningStarted` | Organization | `org_id`, `slug`, `region`, `run_id`, `plan_key` | Platform Ops, notifications | First event of the saga |
| `OrganizationProvisioned` | Organization | `org_id`, `region`, `base_currency`, `plan_key`, `role_catalogue_version` | Identity, reports, platform | Emitted after step 9; activation follows |
| `OrganizationActivated` | Organization | `org_id`, `activated_at`, `first_admin_user_id` | All modules, billing | Tenant may receive traffic |
| `OrganizationSettingsChanged` | Organization | `org_id`, `key`, `old_value_hash`, `new_value_hash`, `changed_by` | Policy cache, notifications | Values are hashed in the event; the setting itself is read from the table |
| `OrganizationEntitlementChanged` | Organization | `org_id`, `entitlement_key`, `enabled`, `limit_value`, `source` | Module gating, billing | A disabled module returns `403 MODULE_NOT_ENTITLED` |
| `OrganizationSuspensionRequested` | Organization | `org_id`, `reason_code`, `requested_by`, `approver_one`, `approver_two` | Security, support | Approval precedes execution for security and legal reasons |
| `OrganizationSuspended` | Organization | `org_id`, `reason_code`, `effective_at`, `sessions_revoked` | All modules, notifications | Effective within 60 seconds |
| `OrganizationReactivated` | Organization | `org_id`, `reactivated_at`, `reason_code` | All modules, notifications | Sessions are not restored |
| `OrganizationArchiveRequested` | Organization | `org_id`, `requested_by`, `destination` | Export service | Starts read-only mode |
| `OrganizationArchived` | Organization | `org_id`, `archived_at`, `export_manifest_id` | All modules | Write routes closed |
| `OrganizationPurgeRequested` | Organization | `org_id`, `requested_by`, `approver_one`, `approver_two`, `scheduled_for` | Compliance, platform | 30-day grace begins |
| `OrganizationPurged` | Organization | `org_id`, `purged_at`, `certificate_hash`, `key_destroyed_at` | Compliance, audit | Tombstone remains |
| `TenantRegionAssigned` | Organization | `org_id`, `region`, `cell`, `key_id` | Routing, storage, backup | Source of truth for placement |
| `TenantRegionMoveStarted` | Organization | `org_id`, `from_region`, `to_region`, `change_id` | All modules | Phase 1 |
| `TenantRegionMoveCompleted` | Organization | `org_id`, `to_region`, `completed_at`, `verification_id` | All modules, DPO | Phase 1 |

**Suspension is not deletion.** The event set separates `OrganizationSuspended`, `OrganizationArchived` and `OrganizationPurged` because they have different customer promises and different recovery paths. A consumer that treats all three as "tenant unavailable" will eventually restore a purged tenant's cache.

**Settings events carry hashes, not values.** A setting may be a sensitive threshold or a cross-region permission. The event tells consumers that something changed and lets them re-read under their own authorization; broadcasting the value would leak it into every consumer's log.

### 12.5 Identity and Credential Events

| Event | Aggregate | Key payload | Consumers | Notes |
|-------|-----------|-------------|-----------|-------|
| `UserInvited` | Membership | `org_id`, `user_id`, `email_hash`, `role_id`, `expires_at` | Identity, notifications | The email itself is sent by the notification module from the invitation record, not from the event |
| `InvitationResent` | Membership | `org_id`, `invitation_id`, `resend_count`, `expires_at` | Notifications | Previous token invalidated |
| `InvitationRevoked` | Membership | `org_id`, `invitation_id`, `reason_code` | Notifications | Token returns `410` immediately |
| `InvitationAccepted` | Membership | `org_id`, `user_id`, `invitation_id`, `accepted_at` | Identity, audit | Membership and assignment already committed |
| `UserActivated` | User | `user_id`, `home_org_id`, `activated_at` | All modules | Identity is usable |
| `UserProfileUpdated` | User | `user_id`, `changed_fields[]`, `actor_id` | Reports, notifications | Field names only; no values |
| `UserEmailChanged` | User | `user_id`, `old_email_hash`, `new_email_hash`, `verified_at` | Identity, notifications | Both hashes, never plaintext |
| `UserDeactivated` | User | `org_id`, `user_id`, `reason_code`, `actor_id` | Identity, production, reports | Triggers session revocation and grant suspension |
| `UserReactivated` | User | `org_id`, `user_id`, `reactivated_at` | Identity | Does not restore sessions |
| `UserLocked` | User | `user_id`, `reason_code`, `locked_until`, `failure_count` | Security, notifications | Credential lockout |
| `UserUnlocked` | User | `user_id`, `unlocked_by`, `reason_code` | Security | Manual or timer |
| `UserErased` | User | `user_id`, `key_destroyed_at`, `certificate_hash` | All modules, compliance | Pseudonym remains |
| `IdentityTransferred` | User | `user_id`, `from_org_id`, `to_org_id`, `effective_at`, `transfer_id` | All modules, affected clients | Grants are suspended, not moved |
| `PasswordChanged` | User | `user_id`, `method` (`SELF`, `RESET`), `sessions_revoked` | Security | No password material |
| `PasswordResetRequested` | User | `user_id`, `email_hash`, `expires_at` | Notifications | Request is always accepted |
| `PasswordResetCompleted` | User | `user_id`, `reset_at`, `sessions_revoked` | Security | All sessions revoked |
| `MfaEnrolled` | User | `user_id`, `factor_type`, `factor_id` | Security, reports | TOTP at MVP |
| `MfaReset` | User | `user_id`, `factor_id`, `actor_id`, `approver_id`, `reason_code` | Security, notifications | High severity |
| `RecoveryCodesRegenerated` | User | `user_id`, `generation`, `count` | Security | Old codes invalid |
| `CredentialCompromised` | User | `user_id`, `source`, `action_taken` | Security, DPO | Breach-list or external notification |

**Email is never in an event.** A hash supports correlation and uniqueness checks without turning the event bus into a personal-data store. The notification module reads the address from `identity.user_pii` under its own service authority, decrypts it for delivery and discards it.

**User deletion is an event, not a cascade trigger alone.** `UserErased` tells consumers that the pseudonym remains valid but the personal fields are gone. A consumer that caches a display name must drop it; a consumer that stores only `user_id` needs no action. The event makes that distinction explicit.

### 12.6 Session, Authentication and Device Events

| Event | Aggregate | Key payload | Consumers | Notes |
|-------|-----------|-------------|-----------|-------|
| `UserAuthenticated` | Session | `user_id`, `org_id`, `method`, `amr[]`, `device_id`, `ip_class`, `session_id` | Security telemetry, audit | Not a business audit event by itself |
| `LoginFailed` | User | `email_hash`, `ip_class`, `reason_code`, `failure_count` | Security, detection | No distinction between unknown account and wrong password |
| `MfaChallengeIssued` | Session | `user_id`, `challenge_id`, `methods[]`, `expires_at` | Security | Challenge is server-side state |
| `MfaChallengeFailed` | Session | `user_id`, `challenge_id`, `reason_code`, `attempt_count` | Security, detection | Five failures lock the account |
| `RefreshTokenReuseDetected` | Session | `user_id`, `session_id`, `family_id`, `ip_class`, `action_taken` | Security, incident response | Critical; family revoked |
| `SessionRevoked` | Session | `user_id`, `session_id`, `reason_code`, `actor_id`, `authority_id` | All modules | Denylist and `scope_version` bump |
| `SessionExpired` | Session | `user_id`, `session_id`, `reason` (`IDLE`, `ABSOLUTE`) | Analytics | No security action |
| `LogoutAllCompleted` | User | `user_id`, `sessions_revoked`, `devices_requiring_reauth` | Security | Explicit user action |
| `DeviceRegistered` | Device | `user_id`, `device_id`, `platform`, `trust_state` | Security, mobile | Public key registered |
| `DeviceRevoked` | Device | `user_id`, `device_id`, `reason_code`, `actor_id` | Identity, mobile | Wipe may follow |
| `DeviceWipeRequested` | Device | `user_id`, `device_id`, `requested_by` | Mobile push | Queued until acknowledged |
| `DeviceWipeAcknowledged` | Device | `user_id`, `device_id`, `acknowledged_at` | Security, compliance | Closes the wipe record |
| `StepUpGranted` | Session | `user_id`, `session_id`, `action_class`, `method`, `expires_at` | Audit | Server-side assertion |
| `StepUpFailed` | Session | `user_id`, `session_id`, `action_class`, `reason_code` | Security, detection | Repeated failures are anomalous |
| `SupportAccessGranted` | SupportAccess | `support_access_id`, `operator_user_id`, `target_org_id`, `approver_id`, `ticket_ref`, `scope`, `expires_at` | Audit, notifications | Customer sees it |
| `SupportAccessEnded` | SupportAccess | `support_access_id`, `ended_at`, `actions_count`, `end_reason` | Audit, review | Write actions open a review item |

**`UserAuthenticated` is not a business audit event.** It is security telemetry with a short retention in the hot store and a longer archival copy. Mixing it into the business audit stream would bury a budget approval under millions of logins and make NFR-AUD-005 unachievable.

**Session events carry no token.** `session_id` is an identifier, not a credential. An event that contains a refresh token would replicate the secret into every consumer and every log sink, which defeats the point of storing only a hash in `identity.refresh_session`.

### 12.7 Authorization, Role and Assignment Events

| Event | Aggregate | Key payload | Consumers | Notes |
|-------|-----------|-------------|-----------|-------|
| `RoleCreated` | Role | `org_id`, `role_id`, `role_key`, `permission_count`, `created_by` | Policy cache, reports | Custom role only |
| `RoleChanged` | Role | `org_id`, `role_id`, `version_no`, `permissions_added[]`, `permissions_removed[]`, `changed_by` | Policy cache, all modules | Bumps `scope_version` for holders |
| `RoleRetired` | Role | `org_id`, `role_id`, `replacement_role_id`, `changed_by` | Policy cache, reports | No live assignments |
| `RoleAssignmentCreated` | Assignment | `org_id`, `assignment_id`, `user_id`, `role_id`, `scope_type`, `scope_ids[]`, `valid_from`, `valid_until` | Policy cache, reports | Scope identifiers only |
| `RoleAssignmentRemoved` | Assignment | `org_id`, `assignment_id`, `user_id`, `role_id`, `reason_code` | Policy cache, reports | Immediate |
| `ScopeVersionBumped` | User | `user_id`, `org_id`, `old_version`, `new_version`, `cause` | Policy cache | Cache invalidation signal |
| `PermissionDenied` | User | `user_id`, `org_id`, `permission_key`, `scope_result`, `route`, `reason_code` | Security, detection | Sampled above 100 per minute per user |
| `CrossTenantAccessAttempt` | User | `user_id`, `actor_org_id`, `target_org_id`, `resource_type`, `route` | Security, incident response | Critical; never contains the target identifier |
| `SodBlocked` | User | `user_id`, `org_id`, `rule_key`, `permission_a`, `permission_b`, `route` | Security, compliance | The action was refused |
| `SodExceptionGranted` | SodException | `org_id`, `exception_id`, `rule_key`, `subject_user_id`, `scope`, `approver_one`, `approver_two`, `valid_until` | Policy cache, audit | Maximum 90 days |
| `SodExceptionRevoked` | SodException | `org_id`, `exception_id`, `revoked_by`, `reason_code` | Policy cache, audit | Early removal |
| `DelegationCreated` | Delegation | `org_id`, `delegation_id`, `delegator_user_id`, `delegate_user_id`, `permission_key`, `scope`, `max_amount`, `start_at`, `end_at` | Policy cache, audit | No sub-delegation |
| `DelegationRevoked` | Delegation | `org_id`, `delegation_id`, `revoked_by`, `reason_code` | Policy cache, audit | Immediate |
| `ApprovalLimitChanged` | ApprovalLimit | `org_id`, `limit_id`, `subject`, `old_amount`, `new_amount`, `currency`, `changed_by` | Approval service, audit | Effective date respected |

**Denial events are sampled, not dropped.** `PermissionDenied` is emitted for every denial at the audit layer but published to the detection stream with sampling above a threshold. A user who triggers 10,000 denials in a minute is probing; a user who triggers one is clicking a button they do not have. Both are recorded; only the first needs a page.

**`CrossTenantAccessAttempt` deliberately omits the target identifier.** The security team needs to know that an actor tried to cross a boundary, from which tenant and on which route. Replicating the target's identifier into every security tool would spread the existence of another tenant's record further than the original request did.

### 12.8 Grant Events

Section 8.15 describes the business meaning and severity. This table is the normative delivery contract: payload fields and consumers. Event names are stable; a new field is additive within `schema_version` 1.

| Event | Key payload | Consumers | Delivery notes |
|-------|-------------|-----------|----------------|
| `GrantDrafted` | `grant_id`, `client_org_id`, `grantee_user_id`, `firm_org_id`, `profile_key`, `scope_type` | Register, audit | Not yet visible to the grantee |
| `GrantDraftDiscarded` | `grant_id`, `actor_id`, `reason_code` | Hygiene report | Physical delete follows |
| `GrantSubmitted` | `grant_id`, `creator_id`, `approver_candidates[]`, `profile_key`, `scope`, `start_at`, `end_at` | Approval queue, notifications | Approver candidates are users with `.approve` |
| `GrantApproved` | `grant_id`, `approver_id`, `co_approver_id`, `profile_key`, `scope`, `start_at`, `end_at`, `acceptance_deadline` | Audit, owner, grantee, firm | Starts the 14-day window |
| `GrantDeclined` | `grant_id`, `actor_id`, `actor_role` (`APPROVER`, `GRANTEE`, `SYSTEM`), `reason_code` | Creator, firm | Terminal |
| `GrantAccepted` | `grant_id`, `grantee_user_id`, `nda_version`, `coi_declaration_id`, `mfa_factor_id`, `accepted_at`, `ip_class` | Audit, client, firm | BR-AUD-004 evidence |
| `GrantActivated` | `grant_id`, `activated_at`, `end_at`, `scope_version` | Workspace, notifications, all modules | Authority begins |
| `GrantVariationApplied` | `grant_id`, `version_no`, `change_class` (`REDUCTION`, `EXTENSION`, `ADMINISTRATIVE`), `before_hash`, `after_hash`, `actor_id` | Audit, access review | Extension requires approval first |
| `GrantSuspended` | `grant_id`, `reason_code`, `initiator_id`, `sessions_revoked` | Session service, client, firm | Immediate |
| `GrantResumed` | `grant_id`, `approver_id`, `acceptance_id`, `resumed_at` | Workspace, audit | Fresh acceptance |
| `GrantRevoked` | `grant_id`, `initiator_id`, `reason_code`, `cascade_status`, `sessions_revoked`, `exports_cancelled` | Incident review, all modules, client, firm | Effective under 60 seconds |
| `GrantExpired` | `grant_id`, `end_at`, `expired_at`, `sessions_revoked` | Register, notifications | Decision-time check is the control |
| `GrantExpiryNoticeSent` | `grant_id`, `stage` (`T-14`, `T-7`, `T-1`, `T-0`, `T+1`), `recipients[]`, `delivery_result` | FR-TNT-017 evidence | Retried on failure |
| `GrantRenewalCreated` | `new_grant_id`, `supersedes_grant_id`, `client_org_id`, `grantee_user_id` | Register chain, audit | New approval required |
| `GrantWorkspaceEntered` | `grant_id`, `session_id`, `client_org_id`, `region` | Cross-tenant monitoring | One active workspace |
| `GrantWorkspaceClosed` | `grant_id`, `session_id`, `duration_seconds`, `end_reason` | Session analytics | Explicit or timeout |
| `GrantCrossRegionAcknowledged` | `grant_id`, `firm_region`, `client_region`, `mechanism_ref`, `acknowledged_by` | DPO evidence | Required only when regions differ |
| `GrantExportCompleted` | `grant_id`, `resource_type`, `row_count`, `watermark_id`, `job_id` | Data-loss monitoring, audit | Step-up above 1,000 rows |
| `GrantAccessDenied` | `grant_id`, `permission_key`, `reason_code`, `route` | Threat detection | Sampled |
| `EmergencyGrantRevocation` | `grant_id`, `ticket_ref`, `platform_actor_id`, `approver_id`, `reason_code` | Monthly security review | Critical |

**`GrantCreated` from Document 6 is retired.** Appendix B.2 lists `GrantCreated` as a single event. The lifecycle has distinct creation, approval and activation moments, and collapsing them loses the evidence that the client approved before the auditor could read anything. Document 9 must map the old name to `GrantDrafted` and treat `GrantActivated` as the authority-begin event. This is recorded as change request D4-06 in Appendix G.

**Revocation is one event with a cascade result.** A consumer should not have to assemble five events to learn whether sessions were actually revoked. `cascade_status` is `COMPLETE`, `PARTIAL` or `FAILED`, and a `PARTIAL` result opens an incident automatically. The detailed per-session revocations still arrive as `SessionRevoked`.

**Notices are events, not just emails.** `GrantExpiryNoticeSent` is the evidence that FR-TNT-017 was satisfied. A failed delivery is an event with `delivery_result = FAILED`, which is how the platform distinguishes "we tried" from "the owner was warned".

### 12.9 Access Review, Delegation and Compliance Events

| Event | Aggregate | Key payload | Consumers | Notes |
|-------|-----------|-------------|-----------|-------|
| `AccessReviewCampaignOpened` | AccessReview | `campaign_id`, `org_id`, `period`, `reviewer_ids[]`, `item_count`, `due_at` | Reviewers, audit | Quarterly by default |
| `AccessReviewItemDecided` | AccessReview | `campaign_id`, `item_id`, `subject_user_id` or `grant_id`, `decision` (`CERTIFY`, `REVOKE`), `reviewer_id`, `reason_code` | Audit, policy cache | Self-review blocked |
| `AccessReviewCertified` | AccessReview | `campaign_id`, `org_id`, `certified_at`, `certified_by`, `items_certified`, `items_revoked` | Compliance, audit | Sign-off record |
| `AccessReviewOverdue` | AccessReview | `campaign_id`, `org_id`, `days_overdue`, `open_items` | Management, compliance | Escalation trigger |
| `AccessReviewClosed` | AccessReview | `campaign_id`, `closed_at`, `revocations_executed` | Audit, reports | Revocations applied |
| `LegalHoldApplied` | LegalHold | `hold_id`, `org_id`, `scope`, `authority`, `matter_ref` | All deletion jobs | Blocks erasure and purge |
| `LegalHoldReleased` | LegalHold | `hold_id`, `released_by`, `authority`, `released_at` | All deletion jobs | Named authority only |
| `ErasureBlocked` | User | `user_id`, `hold_id`, `requested_by` | DPO, compliance | Erasure refused |
| `DsrReceived` | Dsr | `request_id`, `org_id`, `subject_user_id`, `request_type`, `due_at` | DPO, compliance | 30-day target |
| `DsrFulfilled` | Dsr | `request_id`, `fulfilled_at`, `package_id`, `redactions` | DPO, compliance | Evidence of completion |
| `DsrRejected` | Dsr | `request_id`, `reason_code`, `legal_basis` | DPO, compliance | With reasoning |

**Access review events are retained for seven years.** They are the evidence that the organization looked at its access and made a decision. A certification that exists only in a dashboard is not evidence.

**Legal hold is an event because it changes every deletion path.** A hold applied to one show must stop the purge of a tenant, the erasure of a user and the retention sweep of a document set. The event is consumed by every job that deletes anything.

### 12.10 Platform Operations Events

| Event | Aggregate | Key payload | Consumers | Notes |
|-------|-----------|-------------|-----------|-------|
| `ProvisioningStepFailed` | ProvisioningRun | `run_id`, `org_id`, `step`, `error_code`, `attempt`, `will_retry` | Platform on-call, audit | Step names are stable |
| `ProvisioningCompensated` | ProvisioningRun | `run_id`, `org_id`, `steps_compensated[]`, `final_state` | Platform Ops, audit | Tenant is `PROVISIONING_FAILED` |
| `IsolationProbeFailed` | Organization | `org_id`, `probe_id`, `failure_class`, `policy_or_role` | Security, platform | Blocks activation |
| `IsolationProbePassed` | Organization | `org_id`, `probe_id`, `tables_tested`, `duration_ms` | Audit, compliance | Activation evidence |
| `EmergencyRevocationExecuted` | User or Grant | `scope_type`, `scope_id`, `actor_id`, `approver_id`, `ticket_ref`, `actions[]` | Incident review, audit | One record per runbook execution |
| `KeyRotated` | Key | `key_id`, `region`, `purpose`, `new_version`, `rotated_at` | Security, compliance | No key material |
| `KeyDestroyed` | Key | `key_id`, `region`, `purpose`, `destroyed_at`, `certificate_hash` | Compliance, audit | Irreversibility evidence |
| `TenantQuotaWarning` | Organization | `org_id`, `metric`, `used`, `limit`, `percent` | Platform Ops, customer | 80% and 95% thresholds |
| `TenantQuotaExceeded` | Organization | `org_id`, `metric`, `used`, `limit` | Platform Ops, customer | Writes may be blocked |
| `JobFailed` | Job | `job_key`, `org_id`, `run_id`, `error_code`, `attempt` | Platform on-call | Every scheduled job has an owner |

**`IsolationProbePassed` is release evidence.** The probe is the only automated proof that a new tenant's policies work with real data. Storing the probe ID, the table count and the duration turns "we ran the probe" into a retrievable artefact for the SOC 2 examination.

### 12.11 Consumer Matrix

| Consumer | Tenancy | Identity | Session | Authorization | Grant | Review | Platform |
|----------|---------|----------|---------|---------------|-------|--------|----------|
| MOD-01 Tenancy | Yes | Yes | No | Yes | Yes | Yes | No |
| MOD-02 Identity | Yes | Yes | Yes | Yes | Yes | No | No |
| MOD-03 Production | Yes | Yes | No | Yes | Yes | No | No |
| MOD-04 Budget | Yes | Yes | No | Yes | Yes | No | No |
| MOD-05 Vendor | Yes | Yes | No | Yes | Yes | No | No |
| MOD-06 Audit | Yes | Yes | No | Yes | Yes | Yes | No |
| MOD-07 Expense | Yes | Yes | No | Yes | Yes | No | No |
| MOD-08 Intelligence | Yes | Yes | No | Yes | Yes | No | No |
| MOD-09 Document | Yes | Yes | No | Yes | Yes | No | No |
| MOD-10 Excel | Yes | Yes | No | Yes | Yes | No | No |
| MOD-11 Notification | Yes | Yes | Yes | No | Yes | Yes | No |
| MOD-12 Compliance | Yes | Yes | Yes | Yes | Yes | Yes | Yes |
| MOD-13 Reporting | Yes | Yes | No | Yes | Yes | Yes | No |
| MOD-14 Sync | Yes | Yes | Yes | Yes | Yes | No | No |
| MOD-15 Platform Ops | Yes | Yes | Yes | Yes | Yes | Yes | Yes |

**Every module subscribes to `OrganizationSuspended` and `OrganizationActivated`.** A module that keeps serving a suspended tenant because it missed the event is a module with a stale cache and a compliance defect. The subscription is mandatory, and the conformance test asserts it.

**Only MOD-02 and MOD-14 consume session events.** A domain module that reacts to `SessionRevoked` by deleting business data has confused authentication with authorization. The domain reads the current authority on each request; it does not maintain a shadow session state.

### 12.12 Payload Rules and Personal Data

| Rule | Detail |
|------|--------|
| Identifiers, not records | An event carries `user_id`, `org_id`, `role_id`, `grant_id`, not names, emails, amounts or document contents |
| Hashes for correlation | Email and IP are represented by keyed hashes or coarse classes (`/24`, `/48`) |
| No secrets | No password, token, TOTP secret, recovery code, service secret or private key |
| No free text | Reasons are closed `reason_code` values; the human-readable note lives in the audit record, not the event |
| Tenant scope | `org_id` is always the tenant whose data changed; a cross-tenant action emits one event per tenant |
| Classification | `pii_class` is set at publication and validated against the payload schema |
| Retention | Security events 13 months hot, 7 years archive; business audit 7 years; no event is deleted before its class retention |
| Redaction | A consumer that logs an event logs the envelope and the identifiers, never the full payload at `INFO` |

**Why reason codes instead of notes.** A free-text field in an event becomes an uncontrolled personal-data channel: an operator writes "auditor Jane Doe seemed conflicted" into a revocation reason, and the text is replicated to every consumer and archive. The note belongs in the audited record; the event carries the code.

### 12.13 Schema Evolution

| Change | Compatibility | Action |
|--------|---------------|--------|
| Add an optional field | Compatible | Publish within the current `schema_version` |
| Add a required field | Breaking | Increment `schema_version`; dual-publish for one release |
| Remove a field | Breaking | Deprecate for two releases, then remove with a version increment |
| Rename a field | Breaking | Add the new field, dual-write, migrate consumers, remove the old |
| Add an enum value | Compatible if consumers tolerate unknown values | Document in the catalogue; never reuse an old value |
| Change a type | Breaking | New field and version increment |
| Change semantics without changing shape | Breaking in practice | New event type; never silently redefine an existing one |

**Semantic changes are the dangerous ones.** Changing `GrantRevoked.cascade_status` from a boolean to an enum, or redefining `EXPIRED` to include suspended grants, breaks consumers without a schema diff. The rule is that a semantic change is a new event type, not a version bump on the old one.

**The catalogue is generated.** The event catalogue in this section is the source for a machine-readable manifest that generates consumer stubs, validates payloads in CI and detects a publisher that emits an event absent from the manifest. An event that exists only in code is an undocumented integration.

### 12.14 Traceability for Section 12

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| FR-TNT-001 | Section 12.4 | Provisioning and activation events |
| FR-TNT-003 | Section 12.4 | Suspension, archival and purge events |
| FR-TNT-004 | Section 12.5 | Invitation events with expiry |
| FR-TNT-006 | Sections 12.5, 12.6 | Deactivation and session revocation events |
| FR-TNT-007 | Section 12.6 | Login failure, lockout and MFA challenge events |
| FR-TNT-010 | Section 12.7 | Role change and scope version events |
| FR-TNT-013 | Section 12.7 | Assignment events with scope |
| FR-TNT-015 | Section 12.8 | Grant draft, submit and approve events |
| FR-TNT-016 | Section 12.8 | Acceptance, suspension and revocation events |
| FR-TNT-017 | Section 12.8 | Expiry notice events with delivery result |
| FR-TNT-018 | Section 12.7 | Cross-tenant access attempt and denial events |
| FR-TNT-020 | Section 12.7 | Delegation events with both identities |
| FR-TNT-021 | Section 12.9 | Access review campaign and certification events |
| FR-TNT-022 | Section 12.6 | Session revocation, expiry and logout events |
| NFR-AUD-001 | Sections 12.4 to 12.10 | Complete event catalogue |
| NFR-AUD-002 | Section 12.3 | Transactional outbox, append-only audit, replay |
| NFR-AUD-003 | Section 12.12 | Retention by class |
| NFR-AUD-004 | Section 12.2 | `correlation_id` in every envelope |
| NFR-SEC-005 | Section 12.10 | Isolation probe evidence events |
| NFR-SEC-013 | Section 12.6 | Abuse and rate-limit signals |
| NFR-PRV-006 | Sections 12.5, 12.9 | Erasure and DSR events |
| NFR-PRV-007 | Section 12.10 | Breach and emergency events |
| CAP-001 | Section 12.4 | Tenant lifecycle events |
| CAP-002 | Sections 12.5, 12.6 | Identity and session events |
| CAP-003 | Sections 12.7, 12.8 | Authorization and grant events |

---

---

## 13. Threat Model

### 13.1 Scope and Method

This threat model covers the identity, tenancy and access-control surface defined in sections 2 to 12: authentication, session management, authorization, isolation, cross-organization grants, provisioning and platform operations. Business-domain threats (duplicate invoice, collusive vendor, falsified evidence) belong to Documents 6 and 8, except where they are reached through an access-control weakness.

The method is STRIDE applied at each trust boundary, with two attack trees for the crown-jewel paths: reading another tenant's data, and exceeding an auditor's mandate. Every threat has a stable identifier, a preventative control, a detective control and a verification test, because a control that is not tested is a claim rather than a control. Threat identifiers are `T4-nn`; detection rules in section 15 are `DET-4-nn`; test obligations in section 16 are `ST-4-nn`.

Three rules govern the register:

1. **A threat is not closed by a policy statement.** It is closed when a test fails without the control, or when the control is structural and cannot be removed without a schema or code change that the test suite would reject.
2. **Every critical-impact threat has a detective control.** Prevention fails eventually. The question is how quickly the platform knows.
3. **Residual risk is recorded, not argued away.** Section 13.9 lists what remains after the controls, who accepts it and when it is reviewed.

**What is out of scope.** Physical access to the cloud provider's data centres, insider abuse at the cloud provider, and compromise of the customer's own email account before the first login. These are inherited, contractual or customer-side risks; the platform's obligation is to make the consequence bounded and visible, which the controls below do.

### 13.2 Assets

| Asset | Classification | Where it lives | Why an adversary wants it | Consequence of loss |
|-------|----------------|----------------|---------------------------|---------------------|
| Password verifier | Confidential | `identity.credential` | Offline cracking, credential reuse | Account takeover across tenants |
| TOTP secret and recovery codes | Confidential | `identity.mfa_factor` | Bypass the second factor | Account takeover |
| Refresh token | Secret | Client cookie or keystore; hash in `identity.refresh_session` | Long-lived impersonation | Persistent access until revoke |
| Access token | Secret | Client memory | Short impersonation, replay | Access for up to 15 minutes |
| Session and denylist state | Confidential | `identity.refresh_session`, Redis | Suppress revocation, replay a closed session | Revocation bypass |
| Grant record and evidence | Confidential | `tenancy.access_grant`, audit | Forge authority, hide a conflict | Undetectable external access |
| Tenant business data | Confidential | Module schemas | Competitive intelligence, financial manipulation | Contract loss, regulatory exposure |
| Personal data | Personal | `identity.user_pii`, domain tables | Identity theft, harassment | GDPR and DPDP breach |
| Audit trail | Confidential, integrity-critical | `compliance.audit_event` | Cover an intrusion or a fraudulent approval | Loss of evidential value |
| Signing keys and pepper | Secret | KMS, HSM-backed | Forge any identity in the region | Total identity compromise |
| Data encryption keys | Secret | KMS, per tenant | Decrypt a tenant's data at rest | Confidentiality loss |
| Platform operator authority | Privileged | `PLATFORM_INTERNAL` membership, break-glass vault | Silent access to every tenant | Maximum-impact insider event |

**The grant record is an asset in its own right.** It is the only object that converts an external person into an authorised reader of a client's data. An attacker who can create, widen or reactivate a grant does not need to steal a client password.

### 13.3 Adversaries

| Adversary | Capability | Motivation | Likely entry point | Primary control |
|-----------|------------|------------|--------------------|-----------------|
| Unauthenticated internet attacker | Automated scanning, credential lists, exploit kits | Data theft, ransom, notoriety | Login, invitation preview, password reset | Rate limits, lockout, Argon2id, uniform errors |
| Credential-stuffing operator | Large breached credential corpus, residential proxies | Account takeover, resale | Login | Breach-list rejection, MFA, anomaly detection |
| Malicious tenant member | Valid low-privilege account, knowledge of the domain | Financial gain, sabotage, curiosity | Object identifiers, scope boundaries | Permission plus scope, RLS, 404 semantics |
| Malicious external auditor | Valid grant, professional access to client data | Client poaching, insider trading, evidence leakage | Grant scope, export, workspace | Grant ceiling, export controls, watermark, access log |
| Colluding member and auditor | Two valid accounts on opposite sides of a grant | Conceal a conflict, approve an improper grant | Grant approval and acceptance | SOD-07, conflict declaration, independent review |
| Malicious platform operator | Infrastructure access, database credentials, KMS | Data theft, sabotage, favouritism | Support access, break-glass, `ops` schema | Two-person support access, no impersonation, loud audit |
| Compromised endpoint | Malware, stolen laptop, unlocked phone | Bulk exfiltration | Browser session, offline cache | Short tokens, device binding, offline bound, wipe |
| Supply-chain attacker | Build or dependency compromise | Persistent access | CI, package registry, container image | Lockfiles, SBOM, signed artefacts, provenance |
| Automated scraper | Valid account, high request volume | Competitive dataset | List and export endpoints | Rate limits, row caps, export step-up, volume detection |

**The insider is modelled as a platform operator, not as a tenant administrator.** A tenant administrator can harm one tenant, which is a customer-relationship and contract problem. A platform operator can harm every tenant, which is an existential problem, and the controls are correspondingly stronger.

### 13.4 Trust Boundaries

```
                        Internet
                           |
                 B1  TLS 1.2+, WAF, rate limit
                           |
                 +---------+----------+
                 |   Edge / ingress   |
                 +---------+----------+
                           |
                 B2  authenticated request, tenant claim
                           |
                 +---------+----------+
                 |      API host      |
                 |  authn -> authz    |
                 |  -> handler        |
                 +--+------+------+---+
                    |      |      |
          B3        | B4   | B5   | B6
                    |      |      |
          +---------+  +---+--+  +----+---------+
          |PostgreSQL|  |Redis |  |Object store |
          |  RLS     |  |cache |  |  Object Lock|
          +----+-----+  +------+  +-------------+
               |
       B7  KMS, keys, pepper
               |
       B8  outbox -> relay -> consumers
               |
       B9  platform operations -> tenant (support access only)
               |
       B10 region boundary (India / EU): no tenant data path
```

| Boundary | What crosses it | Threat focus |
|----------|-----------------|--------------|
| B1 | Unauthenticated HTTP | Denial of service, credential attacks, transport downgrade |
| B2 | A bearer token and a tenant claim | Token forgery, replay, tenant confusion |
| B3 | SQL with `app.org_id` context | Injection, RLS bypass, cross-tenant query |
| B4 | Cache keys and decision entries | Poisoning, stale authority, cross-user leakage |
| B5 | Presigned URLs and evidence objects | Exfiltration, URL sharing, retention |
| B6 | Key material and envelope encryption | Key compromise, decrypt without authority |
| B7 | Domain events | Personal-data leakage, forged events, replay |
| B8 | Operator session into a tenant | Impersonation, silent access, unreviewed write |
| B9/B10 | Replication and backup traffic | Residency breach, cross-tenant restore |

**B2 is the boundary that matters most.** Everything downstream trusts the tenant context that the API host sets. If the host can be made to set a context the caller is not entitled to, every other layer is defending the wrong tenant.

### 13.5 STRIDE Summary

| Category | Principal manifestation here | Primary defences | Residual |
|----------|------------------------------|------------------|----------|
| Spoofing | Credential stuffing, token theft, forged token, MFA reset fraud | Argon2id, MFA, signed tokens, device keys, approval for reset | R4-02, R4-03 |
| Tampering | Cross-tenant write, grant widening, role escalation, audit alteration | `WITH CHECK`, versioned grants, no amplification, hash chain | R4-01 |
| Repudiation | "I did not approve that grant", shared accounts | Named identities, no shared accounts, dual approval, immutable audit | None material |
| Information disclosure | Cross-tenant read, export exfiltration, log leakage, backup mixing | RLS, 404 semantics, watermark, payload rules, scoped restore | R4-06, R4-07 |
| Denial of service | Lockout abuse, auth flood, cache stampede | Per-IP throttle, autoscale, cache pre-warm, queue classes | R4-03 |
| Elevation of privilege | Assignment amplification, support access, break-glass | No amplification, two-person support, sealed credentials | R4-05 |

### 13.6 Threat Register

Likelihood and impact use Low, Medium, High and Critical. Impact is rated before controls; the residual column in section 13.9 is after controls.

| ID | STRIDE | Scenario | L | I | Preventive control | Detective control | Verification test |
|----|--------|----------|---|---|--------------------|-------------------|-------------------|
| **T4-01** | Spoofing | Credentials from a breach corpus are replayed across many accounts | High | High | Argon2id with pepper, breach-list check at set and change, 5-failure lockout, per-IP and per-account rate limits, MFA for privileged roles | `LoginFailed` burst rule, breach-list hit counter, distributed-source correlation | ST-4-01 credential-stuffing simulation; ASVS 2.2.1 |
| **T4-02** | Spoofing, Denial | An attacker locks a known victim out by submitting five wrong passwords every fifteen minutes | Medium | Medium | Lockout is per account, per-IP throttle is independent, verified reset clears the lock, no lock-state disclosure | Lockout count per address hash, repeated-lockout alert | ST-4-02 lockout abuse; R4-03 |
| **T4-03** | Spoofing | A phishing site clones the invitation or reset page and captures the token or the new password | High | High | 72-hour invitation and 30-minute reset lifetimes, single use, hashed tokens, no auto-login from a link, preview names the organization and inviter, MFA enrolment after acceptance | Token reuse, acceptance from a new IP class, password reset followed by immediate session | ST-4-03 token replay and wrong-recipient acceptance |
| **T4-04** | Spoofing | A caller convinces the help desk to reset MFA on an account they do not own | Medium | Critical | Self-service reset forbidden, step-up plus second approver, reason code, verification through an existing session or registered device, subject notification, Platform Security review for platform roles | `MfaReset` event, reset followed by login from a new device, approver-subject correlation | ST-4-04 single-approver and self-reset attempts |
| **T4-05** | Spoofing | A TOTP code is observed and replayed within its validity window | Low | High | Five-minute challenge, single-use code per time step, challenge binding, five attempts per challenge, step-up for high-risk actions | `MfaChallengeFailed`, reused-code counter | ST-4-05 code replay |
| **T4-06** | Spoofing | A refresh token is copied from a browser or device and replayed elsewhere | Medium | Critical | Rotation on every use, reuse detection burns the family, mobile refresh signed by a non-exportable key, HttpOnly `SameSite=Lax`, hashed at rest, 14-day and 30-day bounds | `RefreshTokenReuseDetected`, impossible-travel and IP-class change rules | ST-4-06 old-token replay revokes the family |
| **T4-07** | Spoofing | An access token is stolen through XSS or malware and used before expiry | Medium | High | 15-minute lifetime, thin claims, token held in memory only, strict CSP, `no-store`, session denylist, `scope_version`, device binding on mobile | Denial patterns per session, concurrent-geography rule, anomaly score | ST-4-07 revoked session rejected within 60 seconds |
| **T4-08** | Spoofing, Tampering | A signing key is compromised and tokens are forged for any user or tenant | Low | Critical | Asymmetric keys in KMS, non-exportable private key, 90-day rotation with `kid` overlap, `ver` claim, issuer and audience validation, separate keys per region and environment | Unknown `kid`, signature-failure spike, key-usage anomaly | ST-4-08 forged token and rotation drill |
| **T4-09** | Information disclosure | A member changes an object identifier in a URL and reads another tenant's record | Medium | Critical | Token-authoritative tenant, RLS on every tenant table, no tenant identifier from the client, UUIDv7 identifiers, `404` for foreign objects | `CrossTenantAccessAttempt`, 404 rate per user, isolation probe | ST-4-09 negative RLS suite and IDOR matrix |
| **T4-10** | Tampering | A caller supplies a foreign `org_id` in a body, header or path and writes into another tenant | Low | Critical | One tenant source, `SET LOCAL` only, `WITH CHECK` on every policy, composite foreign keys include `org_id`, authorization before model binding | `TENANT_CONTEXT_MISMATCH`, constraint-violation alert | ST-4-10 foreign-tenant write attempt |
| **T4-11** | Tampering, Information disclosure | A migration or hotfix disables a policy, adds `BYPASSRLS`, or creates a `SECURITY DEFINER` function that ignores context | Medium | Critical | Policies in version-controlled migrations, `FORCE ROW LEVEL SECURITY`, `slateiq_app` owns nothing and cannot DDL, default-deny when context is unset, isolation probe gates every deployment | Nightly `pg_policies` inventory diff, policy hash, probe failure alert | ST-4-11 policy inventory and drop-policy canary |
| **T4-12** | Information disclosure | An attacker enumerates tenant slugs or email addresses through response differences or timing | Medium | Medium | Uniform `404`, no public slug lookup, dummy Argon2id verification for unknown accounts, no pre-authentication tenant list, identical lockout behaviour | 404 and 401 distribution per source, enumeration pattern rule | ST-4-12 response-diff and timing-distribution test |
| **T4-13** | Elevation, Tampering | An approved grant is widened after approval by editing scope or dates | Medium | High | Approved version immutable, variation classified as reduction, extension or administrative, extension requires fresh approval, version hash chain, grants may not use `ORG` scope | `GrantVariationApplied` with `EXTENSION`, version hash mismatch | ST-4-13 widen-after-approval attempt returns `409` |
| **T4-14** | Elevation | An auditor reads shows or modules outside the assignment, or exports more than the profile allows | High | High | Grant profile ceiling, `ASSIGNMENT` scope, RLS grant boundary, export gated by profile and step-up, watermark on every export | `GrantAccessDenied`, export volume baseline, monthly access-log review | ST-4-14 grant profile matrix and scope-escape test |
| **T4-15** | Elevation | The creator approves their own grant, or two colluding users approve each other's | Low | High | SOD-07, approver must differ from creator, Finance Head co-approval for `AUDIT_FINANCE`, no self-acceptance, firm conflict declaration | `SodBlocked`, approval-timing correlation, same-IP approver rule | ST-4-15 self-approval and same-approver tests |
| **T4-16** | Elevation | An auditor leaves the firm or the client engagement ends, but the grant stays active | Medium | High | Identity transfer and firm deprovision suspend grants, user deactivation suspends grants, 12-month ceiling, T-14/T-7/T-1 notices, quarterly review | Daily orphaned-grant job, overdue-expiry alert | ST-4-16 deactivate grantee and assert denial within 60 seconds |
| **T4-17** | Elevation | A manager assigns a role or scope wider than their own authority | Medium | High | No amplification at write time, self-assignment prohibited, system roles immutable, role versions, SoD gate | `RoleAssignmentCreated` anomaly, scope-widening rule | ST-4-17 manager assigns organization-wide role |
| **T4-18** | Elevation, Information disclosure | A platform operator reads a tenant's data without a ticket or approval | Medium | Critical | `PLATFORM_INTERNAL` is a separate tenant, no default data access, support access is two-person, ticketed, 60 minutes and action-scoped, no impersonation, `ops` schema inaccessible to the application role | `SupportAccessGranted`, write-during-support review item, operator query-volume anomaly | ST-4-18 operator without support access receives `403` |
| **T4-19** | Elevation | Break-glass credentials are used outside an incident and never rotated | Low | Critical | Credentials sealed in KMS, two custodians, ticket and approval required, time-boxed, rotated after every use, every action audited with `authority_source = PLATFORM` | Break-glass use pages security, `EmergencyRevocationExecuted`, credential-access log | ST-4-19 tabletop and credential-access audit |
| **T4-20** | Tampering | Two provisioning requests race, or a run fails midway and leaves a half-created tenant | Medium | High | Idempotent ten-step saga, unique slug constraint, advisory lock per slug, state guards, compensation, isolation probe gate, 24-hour slug hold | `ProvisioningStepFailed`, duplicate-slug constraint, run-duration alert | ST-4-20 concurrent provision and kill-at-step-five |
| **T4-21** | Information disclosure | A user with legitimate read access exports a large dataset and shares it | High | High | Step-up above 1,000 rows, row caps, watermark, 15-minute URL, export event, profile gating for auditors | Export volume against baseline, off-hours and repeat-export rules, `GrantExportCompleted` | ST-4-21 export cap, watermark and URL expiry |
| **T4-22** | Information disclosure | A restore brings one tenant's data into a database serving another tenant | Low | Critical | Per-region backups, restore into an isolated account, tenant-scoped verification, per-region keys, no cross-region copy, two-person restore runbook | Restore verification job, key-region mismatch alert | ST-4-22 annual restore drill with cross-tenant assertion |
| **T4-23** | Tampering | A retried grant or invitation request creates two authorities | Medium | Medium | `Idempotency-Key` bound to method, path, actor and body hash, unique constraint per organization, stored response replay, different body returns `409` | `IDEMPOTENCY_KEY_REUSED`, duplicate-grant check | ST-4-23 double submit with same and different bodies |
| **T4-24** | Elevation | A stale permission cache keeps revoked authority alive past the commitment | Medium | High | Cache key includes `scope_version`, TTL at most 60 seconds, event-driven invalidation, no cross-user entries, fail-closed on cache error | Revocation-latency SLO, stale-decision canary | ST-4-24 role change and grant revoke propagation |
| **T4-25** | Information disclosure | Personal data appears in logs, traces or event payloads | Medium | High | Structured logging with an allowlist, no PII in events, email represented by keyed hash, reason codes instead of free text, redaction tests in CI | PII scanner over sampled logs, DLP alert | ST-4-25 log-capture assertion in every integration test |
| **T4-26** | Tampering | A compromised dependency or build step injects code into the API or worker image | Medium | Critical | Lockfiles, signed packages, SBOM, dependency scanning, image signing and provenance, no unreviewed transitive upgrade, least-privilege CI | SCA and provenance alerts, unexpected egress from build | ST-4-26 build-gate and SBOM diff |
| **T4-27** | Denial | An attacker floods login or a hot endpoint and starves legitimate traffic | Medium | Medium | Edge WAF and rate limits, per-tenant fairness, autoscale, circuit breakers, cache pre-warm, queue classes | 429 and 5xx rate, latency SLO burn, saturation | ST-4-27 3x peak load with attack mix |
| **T4-28** | Information disclosure | A lost mobile device with an offline cache is examined | Medium | High | SQLCipher, biometric gate, seven-day offline bound, remote wipe, device revoke, no offline grant data | Missing wipe acknowledgement, failed-unlock telemetry | ST-4-28 device-loss simulation; R4-04 |
| **T4-29** | Tampering | An auditor acts in one client's workspace while believing they are in another | Medium | High | One workspace at a time, explicit enter and exit, persistent banner, token `org` authoritative, workspace routes distinct from member routes | `GrantWorkspaceEntered` and `GrantWorkspaceClosed`, mismatch events | ST-4-29 concurrent enter returns `409` |
| **T4-30** | Tampering, Repudiation | An attacker with database access edits or deletes audit rows to remove evidence | Low | Critical | In-transaction append-only writes, no `UPDATE` or `DELETE` grant, hash chain, WORM archive, separate schema and role | Monthly chain verification, audit-write failure alert | ST-4-30 update and delete denied; chain verification |

### 13.7 Attack Trees

**AT-4-01: Read another tenant's data.**

```
Goal: read an object belonging to tenant B while authenticated to tenant A
├── 1. Obtain a valid session for tenant A
│   ├── 1.1 Use own legitimate account            -> blocked by permission + scope
│   ├── 1.2 Take over another account             -> T4-01, T4-03, T4-04, T4-06
│   └── 1.3 Forge a token                         -> T4-08
├── 2. Make the server believe the tenant is B
│   ├── 2.1 Set X-Org-Id to B                      -> 400 TENANT_CONTEXT_MISMATCH
│   ├── 2.2 Change path {orgId} to B               -> token org wins; 404
│   ├── 2.3 Change org_id in a request body        -> ignored by binder; WITH CHECK rejects
│   └── 2.4 Exploit a route that trusts a client tenant -> route inventory test
├── 3. Reach B's row with A's context
│   ├── 3.1 Guess an identifier                    -> UUIDv7, 404
│   ├── 3.2 Exploit a missing RLS policy           -> policy inventory + probe
│   ├── 3.3 Exploit a SECURITY DEFINER function    -> allowlist and review
│   └── 3.4 Use a reporting or export job          -> job sets context per tenant
└── 4. Read through a side channel
    ├── 4.1 Error or timing difference             -> uniform errors, dummy hash
    └── 4.2 Cache key collision                    -> org in every cache key
```

**AT-4-02: Exceed an auditor's mandate.**

```
Goal: read or retain client data beyond the grant
├── 1. Obtain a grant wider than the engagement
│   ├── 1.1 Create it yourself                     -> SOD-07, .create and .approve separated
│   ├── 1.2 Persuade a client admin                -> conflict declaration, review
│   └── 1.3 Widen after approval                   -> immutable version, extension re-approval
├── 2. Use the grant outside its scope
│   ├── 2.1 Read an unassigned show                -> ASSIGNMENT scope + RLS grant boundary
│   ├── 2.2 Switch to another client               -> one workspace, token org
│   └── 2.3 Call a member route                    -> authority-source exclusivity
├── 3. Retain data after the grant ends
│   ├── 3.1 Export before expiry                   -> watermark, profile gate, audit
│   ├── 3.2 Keep a session alive                   -> 60-second cascade, denylist
│   └── 3.3 Rely on cached data                    -> no-store, cache invalidation
└── 4. Avoid detection
    ├── 4.1 Act slowly                             -> monthly access-log review
    └── 4.2 Use a colleague's identity             -> named grants, no shared accounts
```

**AT-4-03: Take over a privileged account.**

```
Goal: control an Organization Administrator or Platform Security identity
├── 1. Credential attack        -> T4-01, T4-02
├── 2. Phish a reset or invite  -> T4-03
├── 3. Social-engineer MFA reset-> T4-04
├── 4. Steal a refresh token    -> T4-06
├── 5. Steal a device           -> T4-28
└── 6. Insider operator         -> T4-18, T4-19
```

### 13.8 Abuse-Case Walkthroughs

**Credential stuffing.** An attacker replays 200,000 email and password pairs from a 2023 breach against `/auth/login`. Roughly 60 per cent of attempts hit unknown addresses; the dummy Argon2id verification keeps response time within the normal distribution, so the attacker cannot build a valid-account list from timing. Known-account failures increment the per-account counter and the per-IP-class counter. At five failures the account locks for fifteen minutes, and the attacker cannot distinguish a locked account from a wrong password because both return the same status and body shape after the delay. Accounts with MFA never yield a session, which is why NFR-SEC-003 is a control and not a convenience. The detection rule fires on more than 50 failures from one IP class in five minutes, or more than 200 across the class in an hour, and the response is a temporary edge block plus a notification to affected account owners. The residual risk is a successful login on an account without MFA whose password was reused and not breached in a known corpus; that is R4-02's neighbourhood and is mitigated by mandatory MFA for every role that can approve, administer or export.

**Refresh token theft.** Malware reads a browser cookie store and copies the refresh token. The attacker presents it from another country. Rotation means the legitimate client's next refresh uses a token the attacker has already consumed, so the server sees a second presentation of a token whose `rotated_at` is set. The family is burned, every session in the family is revoked, `RefreshTokenReuseDetected` is emitted at critical severity, and the user is forced to authenticate again. If the attacker refreshes first, the legitimate user's next request fails and produces the same signal from the other direction. There is no sequence in which both parties continue to refresh quietly. The 60-second idempotent replay window exists for network retries, not for a second device: the replay must present the same `device_id` and the same request signature on mobile, and on web it must come from the same session cookie jar, which a copied token on another host does not have.

**MFA reset social engineering.** An attacker calls support, claims a lost phone, and knows the victim's name, email and organization. The first-line analyst can open the request but cannot approve it. Approval requires a second person with `identity.mfa_factor.administer`, step-up on the approver's own session, a reason code and a verification step that does not rely on the caller's knowledge: an existing authenticated session, a registered device, or a video call against the identity record. The subject receives an immediate notification on every channel on file. The reset emits `MfaReset` with both actor and approver, and a subsequent login from a new device within 24 hours raises a review item automatically. The control is intentionally slower than the fraud it prevents.

**Break-glass abuse.** Break-glass credentials are sealed in KMS and released only to two named custodians who authenticate with hardware keys. Every release requires a ticket reference and a second approver, and the resulting session is capped at 60 minutes with a named action list. The session emits `EmergencyRevocationExecuted` or the equivalent support-access events before the first action, not after. The security channel receives the alert at the moment of release. A post-incident review is mandatory even when the use was justified, because the alternative is a credential that is used quietly and rotated never.

**Provisioning race.** Two operators submit the same slug within a second of each other. The API accepts both requests because idempotency keys differ, but the database's unique index on `tenancy.organization.slug` permits one insert. The loser receives `409 SLUG_TAKEN` and its `ops.provisioning_run` is marked `COMPENSATED` with zero side effects. A run that fails at step six (bucket policy) retries the step three times, then compensates the earlier steps in reverse order and leaves the tenant in `PROVISIONING_FAILED` with the slug held for 24 hours. The isolation probe at step eight is a hard gate: a tenant whose probe fails does not become `ACTIVE`, no matter how many other steps succeeded.

**RLS policy drift.** A migration adds a table and forgets the policy. The policy inventory test enumerates every table with an `org_id` column and asserts that row-level security is enabled, forced, and covered by at least one policy for `SELECT`, `INSERT`, `UPDATE` and `DELETE`. The build fails before deployment. If a policy is dropped in production despite the gate, the nightly inventory diff detects the missing policy, the isolation probe fails on the next run, and the tenant's status is moved to `SUSPENDED` with a security incident opened. The design accepts that prevention can fail; it does not accept that failure can be silent.

### 13.9 Residual Risk Register

| ID | Residual risk | Threat | Why it remains | Mitigation and acceptance | Owner | Review |
|----|---------------|--------|----------------|---------------------------|-------|--------|
| **R4-01** | A shared-schema design depends on RLS correctness; a policy defect could expose rows before a test catches it | T4-09, T4-10, T4-11 | RLS is code, and code has defects | Four independent layers, per-table negative tests, policy inventory gate, isolation probe at provisioning and deploy; accepted for the pilot because dedicated stacks remain available for regulated tenants | Head of Engineering | Quarterly and after every isolation incident |
| **R4-02** | TOTP is phishable; a real-time relay can defeat it | T4-03, T4-05 | WebAuthn is Phase 1 | Mandatory MFA for privileged roles, step-up for high-risk actions, device binding, anomaly detection, short session bounds; accepted | CISO | Before GA and at Phase 1 planning |
| **R4-03** | Account lockout is a denial-of-service vector against a known victim | T4-02 | FR-TNT-007 mandates the lock | Per-IP throttle, verified reset clears the lock, no lock-state disclosure, monitoring; accepted | CISO | Quarterly |
| **R4-04** | A stolen unlocked device with an unexpired offline grant can read local data | T4-28 | Offline capture is a product requirement | SQLCipher, biometric gate, seven-day bound, remote wipe, device revoke; accepted | CISO | Quarterly |
| **R4-05** | A platform operator with approved support access can read tenant data | T4-18, T4-19 | Someone must be able to support and recover the platform | Two-person approval, ticket, 60-minute bound, action scope, no impersonation, customer-visible audit; accepted | CISO | Monthly sample review |
| **R4-06** | A cross-region grant places minimal identity metadata in the firm's region | T4-14 | Auditors work across borders | Minimal projection only, no client business data, acknowledgement recorded, DPO review; accepted | DPO | Per cross-region grant |
| **R4-07** | An authorised export cannot be recalled after download | T4-21 | Data leaves the platform by design | Watermark, row caps, step-up, export event, contractual terms, access review; accepted | CISO | Quarterly |
| **R4-08** | An auditor can memorise or reconstruct what they read | T4-14 | Human cognition cannot be controlled | NDA, conflict declaration, least-privilege profile, audit trail, contractual remedy; accepted | Legal | Annually |
| **R4-09** | Expiry notices may not reach the owner because email is unreliable | T4-16 | Email is an external dependency | Retries, in-app notice, register evidence, escalation at T-7 and T-1, grant expires regardless of delivery; accepted | Head of Customer Success | Quarterly |
| **R4-10** | A future identity provider becomes a trusted third party for authentication | T4-08 | Federation is Phase 1 and adds a dependency | Per-tenant IdP configuration, signed assertions, explicit trust registration, fallback local account policy; not applicable at MVP | CISO | Before Phase 1 GA |

**R4-01 is the defining residual risk of the architecture.** Everything else is bounded by a time window, a scope or a two-person rule. RLS correctness is bounded only by testing and review. That is why the negative suite, the policy inventory and the provisioning probe are release gates rather than quality suggestions.

### 13.10 Control Mapping to Binding Requirements

| Requirement | Threats addressed | Control evidence |
|-------------|-------------------|------------------|
| NFR-SEC-001 | T4-03, T4-06, T4-07 | TLS policy at the edge, HSTS preload, configuration scan |
| NFR-SEC-002 | T4-06, T4-22 | Encryption at rest, per-tenant keys, KMS audit |
| NFR-SEC-003 | T4-01, T4-04, T4-18 | MFA matrix, reset approval, platform-role enrolment report |
| NFR-SEC-004 | T4-09, T4-10, T4-11, T4-17 | Pipeline test, RLS suite, per-endpoint authorization matrix |
| NFR-SEC-005 | T4-09, T4-11, T4-22 | Per-build negative suite, annual penetration test, isolation probe |
| NFR-SEC-011 | T4-07, T4-28 | Device registration, revoke and wipe tests |
| NFR-SEC-013 | T4-01, T4-27 | Rate-limit tests, anomaly rules, attack-mix load test |
| NFR-PRV-009 | T4-22 | Region pinning, restore isolation, cross-region grant review |
| NFR-AUD-001 | T4-30 | Append-only audit, denial events, in-transaction writes |
| NFR-AUD-002 | T4-30 | Hash chain, WORM archive, verification job |
| NFR-AUD-004 | T4-18, T4-19 | Correlation ID across request, audit and event |
| NFR-AUD-005 | T4-21, T4-30 | Query targets, index coverage, 90-day search test |
| NFR-MOB-004 | T4-28 | Offline bound, wipe acknowledgement, device test |
| FR-TNT-007 | T4-01, T4-02 | Lockout and rate-limit tests |
| FR-TNT-010 | T4-24 | Role-change propagation within 60 seconds |
| FR-TNT-014 | T4-17, T4-24 | Authorization matrix on every endpoint |
| FR-TNT-016 | T4-13, T4-14, T4-15 | Grant gates, SOD-07, conflict declaration |
| FR-TNT-018 | T4-09, T4-10 | Tenant-context pipeline tests |
| FR-TNT-022 | T4-06, T4-07 | Session bound and revocation tests |

### 13.11 Threat Model Maintenance

The register is reviewed at four triggers: a new external endpoint, a new grant profile or scope type, a new region or identity provider, and any penetration-test finding rated high or critical. A threat that loses its control without a replacement is a release blocker, not a documentation update. The register is stored with the architecture repository so that a change to section 8 or section 11 that does not update section 13 is visible in review.

### 13.12 Traceability for Section 13

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| FR-TNT-006 | T4-16, section 10.6 | Deactivation cascade and orphaned-grant job |
| FR-TNT-007 | T4-01, T4-02, section 11.13 | Lockout and rate-limit controls |
| FR-TNT-008 | T4-03, section 4.3 | Credential policy and reset controls |
| FR-TNT-014 | T4-09, T4-10, T4-17 | Authorization and isolation threats |
| FR-TNT-015 | T4-13, T4-15 | Grant creation and approval threats |
| FR-TNT-016 | T4-14, T4-15, T4-16 | Grant misuse and expiry threats |
| FR-TNT-017 | T4-16, R4-09 | Notices and orphaned grants |
| FR-TNT-018 | T4-09, T4-10 | Tenant-context threats |
| FR-TNT-021 | T4-16, section 8.12 | Access review as a compensating control |
| FR-TNT-022 | T4-06, T4-07 | Session threats |
| NFR-SEC-003 | T4-01, T4-04 | MFA and reset threats |
| NFR-SEC-004 | T4-09, T4-10, T4-11 | Isolation threats |
| NFR-SEC-005 | T4-11, T4-22 | Probe and restore verification |
| NFR-SEC-011 | T4-28 | Device-loss threat |
| NFR-SEC-013 | T4-01, T4-27 | Abuse and denial threats |
| NFR-PRV-009 | T4-22, R4-06 | Residency and restore threats |
| NFR-AUD-001 | T4-30 | Audit integrity threat |
| NFR-AUD-002 | T4-30 | Hash chain and WORM |
| NFR-AUD-005 | T4-21, T4-30 | Export and audit query threats |
| NFR-MOB-004 | T4-28, R4-04 | Offline device threat |
| CAP-001 | T4-20 | Provisioning race |
| CAP-002 | T4-01 to T4-08, T4-28 | Identity threats |
| CAP-003 | T4-13 to T4-17, T4-24 | Authorization and grant threats |

---

## 14. Performance and Capacity

### 14.1 Principles

1. **A target without a budget is a wish.** Every latency target is decomposed across network, authentication, authorization, handler, data access, serialization and client render, following Document 6 section 11.3.
2. **Authorization is on the hot path of every request.** A design that costs 200 milliseconds per decision is a design that will be bypassed by a cache with no invalidation. The PDP budget is therefore single-digit milliseconds warm.
3. **RLS is measured, not assumed.** The policy predicate runs on every row of every tenant query. Its cost is tracked as a first-class performance metric, because a policy that defeats an index turns a 10-millisecond query into a 4-second scan.
4. **Tenant fairness beats average latency.** One tenant running a 50,000-row export must not raise another tenant's login latency. Queue classes, per-tenant concurrency caps and bulkheads provide that separation.
5. **Degrade in a defined order.** When a dependency fails, the platform chooses read-only, queued write or fail-closed deliberately, and the choice is documented in section 14.9 rather than discovered during an incident.

### 14.2 Targets

| Operation | Metric | Target | Source | Measurement |
|-----------|--------|--------|--------|-------------|
| Access token validation | p95 | 2 ms | Internal budget | APM span |
| Session and denylist lookup | p95 | 3 ms | Internal budget | APM span, Redis latency |
| Policy decision, warm cache | p95 / p99 | 8 ms / 20 ms | Internal budget | PDP histogram |
| Policy decision, cold | p95 / p99 | 25 ms / 60 ms | Internal budget | PDP histogram with cache-state label |
| Authentication and scope, standard CRUD | p95 | 30 ms | Document 6 section 11.3 | APM |
| Authentication and scope, dashboard | p95 | 40 ms | Document 6 section 11.3 | APM |
| Login with password and TOTP | p95 | 800 ms | Security cost of Argon2id | Login histogram |
| Refresh rotation | p95 | 150 ms | Internal budget | Endpoint histogram |
| Standard CRUD API | p95 | 500 ms | NFR-PERF-012 | Load test |
| Expense submission | p95 | 1 s | NFR-PERF-003 | Load test |
| Dashboard initial load | p95 | 2 s | NFR-PERF-001 | APM and RUM |
| Dashboard drill-down | p95 | 2 s | NFR-PERF-002 | Load test |
| Search | p95 | 1 s | NFR-PERF-008 | Load test |
| Grant register, 5,000 live and 50,000 total grants | p95 | 400 ms | Internal budget | Load test |
| Grant detail with scope labels | p95 | 250 ms | Internal budget | Load test |
| Grant activation transaction | p95 / p99 | 500 ms / 1 s | Internal budget | Endpoint histogram |
| Access review open, 50,000 items | elapsed | 120 s asynchronous | Internal budget | Job telemetry |
| Access review first page | p95 | 500 ms | Internal budget | Load test |
| Access review decision | p95 | 200 ms | Internal budget | Load test |
| Grant revocation cascade | p95 / commitment | 20 s / 5 min | BR-AUD-003, FR-TNT-016 | Cascade job telemetry |
| Tenant suspension effective | maximum | 60 s | Section 2.9 | Synthetic probe |
| Role change propagation | maximum | 60 s | FR-TNT-010 | Synthetic probe |
| Provisioning, ten steps | p95 / maximum | 6 min / 10 min | Internal budget | Run telemetry |
| Isolation probe | p95 | 90 s | Internal budget | Probe telemetry |
| Outbox dispatch | p95 | 5 s | Document 6 section 11.5 | Oldest-event age |
| Audit query over 90 days | p95 | 5 s | NFR-AUD-005 | Query telemetry |
| Report generation, 100,000 rows | p95 | 30 s | NFR-PERF-007 | Job telemetry |
| RLS overhead versus no-RLS baseline | p50 / p95 | +10% / +20% | Document 6 TR-001 | A/B benchmark |
| Availability, MVP | monthly | 99.5% | NFR-AVL-001 | SLO dashboard |
| Availability, Phase 1 | monthly | 99.9% | NFR-AVL-002 | SLO dashboard |

**Why login is allowed 800 milliseconds.** Argon2id at 64 MiB and three iterations is deliberately expensive. A login target of 100 milliseconds would force a weaker memory cost and trade a measurable user inconvenience for an unmeasurable security loss. The target is set so that a 400-millisecond hash plus database, network and MFA verification fits comfortably, and it is excluded from the general CRUD target for that reason.

**Why the cold-decision target is separate.** A cold decision reads membership, assignments, role permissions and grants from PostgreSQL. It happens at login and after a `scope_version` change, and the result is cached. Reporting it inside the 8-millisecond warm target would hide the cost of the query path, and reporting it inside the 30-millisecond CRUD budget would make an occasional but correct cost look like a regression. The two are measured separately, and the cold rate is alerted above 1 per cent of decisions.

### 14.3 Request Budget

The pipeline in section 11.3 is budgeted as follows for a standard CRUD request with a 30-millisecond authorization budget.

| Step | Component | Budget |
|------|-----------|--------|
| 1 | TLS termination, edge routing | 5 ms |
| 2 | Route resolution | 1 ms |
| 3 | Token signature and claim validation | 2 ms |
| 4 | Session and denylist lookup | 3 ms |
| 5 | Idle and absolute lifetime check | 1 ms |
| 6 | Tenant status check, cached | 1 ms |
| 7 | Tenant assertion comparison | 1 ms |
| 8 | Membership or grant state check, cached | 2 ms |
| 9 | Policy decision, warm | 8 ms |
| 10 | SoD, limit, step-up, delegation | 2 ms |
| 11 | Database context set | 1 ms |
| 12 | Handler, data access, serialization | remaining budget |
| | **Authorization subtotal** | **27 ms** |
| | **Margin** | **3 ms** |

**The budget is enforced in code.** Each stage emits a span, and a stage that exceeds its budget twice in a rolling hour raises a warning; three times raises an error. The purpose is attribution, not punishment: when p95 rises, the team should know whether the PDP, the denylist or the handler moved.

### 14.4 Query Budgets

| Rule | Value | Enforcement |
|------|-------|-------------|
| `statement_timeout`, API | 5 s | Connection string and role setting |
| `statement_timeout`, reports and jobs | 60 s | Separate role |
| `lock_timeout`, API | 2 s | Transaction begin |
| `lock_timeout`, migrations | 10 s, then retry | Migration runner |
| Maximum joins per request query | 5 | Query review and CI lint |
| Maximum rows examined by a list query | 10,000 | `EXPLAIN` gate |
| Sequential scan on a tenant table above 1,000 rows | Fails CI | `EXPLAIN` gate on top 25 queries |
| `OFFSET` on tables above 10,000 rows | Prohibited | Lint rule |
| N+1 queries per request | Zero | Query-count assertion in integration tests |
| Unbounded `COUNT(*)` on a list endpoint | Prohibited | API review and test |

**Tenant predicate must be index-eligible.** Every policy uses `org_id = app.current_org_id()`, where `app.current_org_id()` is a `STABLE` function reading `current_setting('app.org_id')`. A `STABLE` function can be evaluated once per statement rather than once per row, and an index on `(org_id, ...)` remains usable. A policy written as `org_id = current_setting('app.org_id')::uuid` is also index-eligible in PostgreSQL 17 but is easier to defeat with an expression that the planner cannot fold; the function form is the standard.

**Every tenant index leads with `org_id`.** A composite index on `(org_id, show_id, occurred_at DESC)` serves both the RLS predicate and the list filter. An index that does not lead with `org_id` forces the planner to choose between the policy and the filter, and the usual result is a bitmap heap scan over the whole tenant.

**Partition pruning is mandatory.** `compliance.audit_event` and `expense.expense` are partitioned monthly. A query without an `occurred_at` range scans every partition, which is the most common cause of an audit-query timeout. The query layer adds a default 90-day range and refuses an unbounded request with `422 RANGE_REQUIRED`.

### 14.5 Data Volume Model

| Entity | Pilot | GA year one | Year three | Growth driver |
|--------|-------|-------------|------------|---------------|
| Organizations | 25 | 500 | 1,500 | Sales, auditor firms |
| Users | 2,500 | 50,000 | 150,000 | Members plus auditors |
| Memberships | 2,500 | 50,000 | 150,000 | One active at MVP |
| Role assignments | 7,500 | 150,000 | 450,000 | Three per member average |
| Grants, all states | 2,000 | 50,000 | 150,000 | Engagements per year |
| Grants, active | 400 | 5,000 | 15,000 | Concurrent audits |
| Refresh sessions, active | 3,000 | 60,000 | 180,000 | Devices per user |
| Audit events per year | 15 million | 120 million | 360 million | Twelve events per expense plus security events |
| Outbox events per day | 200,000 | 1.5 million | 4.5 million | Domain event fan-out |
| Object storage | 50 GB | 5 TB | 15 TB | Evidence and exports |
| Database size, hot | 100 GB | 1.5 TB | 4 TB | Audit and expense partitions |

**The audit table dominates growth.** At 1 KB per row including indexes, 120 million events is roughly 120 GB per year. Hot retention is 24 months (Document 6 section 8.7), so the hot table is bounded at about 300 GB before partition archival, which is close to the 250 GB archival trigger. The trigger is therefore expected to fire in year two at GA volume, and archival must be operational before the first 250 GB partition is reached.

**Sessions are small but hot.** Sixty thousand active sessions at roughly 400 bytes each is 24 MB of table and a comparable cache footprint. The denylist is smaller: only revoked-but-unexpired sessions, which is a fraction of the active set.

### 14.6 Caching

| Cache | Key | TTL | Invalidation | Maximum staleness | Failure mode |
|-------|-----|-----|--------------|-------------------|--------------|
| Policy decision | `user_id`, `org_id`, `scope_version`, `resource_hash` | 60 s | `RoleChanged`, `ScopeVersionBumped`, `SessionRevoked`, grant state change | 60 s | Fail closed, recompute |
| Role definition | `org_id`, `role_id`, `version` | 5 min | `RoleChanged`, `RoleRetired` | 5 min | Read from database |
| Grant decision | `grant_id`, `version`, `state` | 60 s | Any grant transition | 60 s | Fail closed |
| Tenant status | `org_id` | 30 s | `OrganizationSuspended`, `OrganizationActivated`, `OrganizationArchived` | 30 s | Read from database |
| Session denylist | `sid` | Until token expiry | `SessionRevoked` | 5 s local snapshot | Fail closed after 60 s |
| JWKS | `kid` | 1 h | Key rotation | 1 h, overlap window | Use cached key until overlap ends |
| Permission catalogue | `org_id`, `catalogue_version` | 1 h | Deployment | 1 h | Read from database |

**What is never cached.** Authorization decisions are never shared across users, even when two users hold identical roles, because the cache key would have to include the full authority set and the risk of a key collision is a cross-user disclosure. Cross-tenant object existence is never cached; a `404` is cheap and a cached existence check is an oracle. Presigned URLs are never cached beyond their 15-minute life. A grantee's client list is never cached across requests.

**Cache sizing.** Fifty thousand users with three common scope combinations produce about 150,000 decision entries. At roughly 500 bytes per entry including keys and metadata, the working set is about 75 MB, which fits comfortably in the Redis instance with headroom for sessions and rate-limit counters. The cache is a performance layer, never a source of truth.

**Stampede control.** A cold cache after a deploy must not send 60,000 simultaneous decisions to PostgreSQL. The decision cache uses a per-key single-flight lock with a 50-millisecond wait; the loser waits for the winner's value rather than issuing a duplicate query. On a full flush, a 5 per cent random early refresh spreads the load over the TTL window.

### 14.7 Connections and Concurrency

| Concern | Decision |
|---------|----------|
| Pooling mode | PgBouncer transaction mode in front of PostgreSQL |
| API pool per instance | 40 connections, maximum 60 |
| Worker pool | 20 connections |
| Report pool | 10 connections, read replica |
| Maximum server connections | 400, with 100 reserved for operations |
| Context setting | `SET LOCAL` at the start of every transaction; no session-level `SET` |
| Prepared statements | Named statements disabled in transaction pooling; command-level caching only |
| Temporary tables | Prohibited in application transactions |
| Advisory locks | Only for provisioning and migration, with a bounded wait |
| Read replicas | Reports and exports only; never authentication or authorization reads |

**Why transaction pooling is compatible with RLS.** The tenant context is set with `SET LOCAL`, which is scoped to the transaction. In transaction pooling, a server connection is assigned for the duration of a transaction and returned afterwards, so the local setting cannot leak to the next client. This property holds only if the application never uses session-level `SET`, never relies on session state and never opens a transaction and leaves it idle across a client request. All three are prohibited and tested.

**Why authentication never reads a replica.** A replica can lag by hundreds of milliseconds. A session revocation written to the primary and read from a replica is a revocation that has not happened. Authentication, denylist checks and grant-state checks always read the primary or the cache that is invalidated by the primary write.

### 14.8 Load and Scale Scenarios

| ID | Scenario | Load | Duration | Pass criteria |
|----|----------|------|----------|---------------|
| LT-4-01 | Login storm | 200 requests per second, 30 per cent MFA | 15 min | p95 800 ms, lockout correct, no 5xx |
| LT-4-02 | Baseline mixed traffic | 350 requests per second, 3,000 users | 2 h | All p95 targets met |
| LT-4-03 | Peak mixed traffic | 1,200 requests per second, 10,000 users | 2 h | All p95 targets met, error rate below 0.1 per cent |
| LT-4-04 | Grant register at scale | 50,000 grants, 5,000 active, deep pagination | 30 min | p95 400 ms, no sequential scan |
| LT-4-05 | Access review at scale | 50,000 items, 20 reviewers | 2 h | Open under 120 s, decisions p95 200 ms |
| LT-4-06 | Revocation cascade | 500 sessions across 200 grants | 10 min | 99 per cent under 60 s, all under 5 min |
| LT-4-07 | Concurrent provisioning | 20 tenants, 5 with induced failures | 30 min | No duplicate slug, all failures compensated |
| LT-4-08 | RLS overhead A/B | Same query set with and without policies | 30 min | p50 increase at most 10 per cent, p95 at most 20 per cent |
| LT-4-09 | Cold cache stampede | Flush Redis, 600 requests per second | 10 min | Recovery within 2 min, p95 under 2 s after warm-up |
| LT-4-10 | Month-end audit query | 90-day range over 120 million rows | 15 min | p95 5 s, partition pruning confirmed |
| LT-4-11 | Attack mix | 3x peak with 20 per cent malicious login traffic | 30 min | Legitimate p95 within 20 per cent of baseline |
| LT-4-12 | Soak | Baseline for 24 h | 24 h | No memory growth above 5 per cent, no connection leak |

**Every scenario runs with RLS enabled and with production-shaped data.** A load test against a superuser role with policies disabled measures a system that does not exist.

### 14.9 Degradation Modes

| Dependency | Failure | Behaviour | User-visible result | Recovery |
|------------|---------|-----------|---------------------|----------|
| Redis | Unavailable | PDP falls back to database; denylist serves a local snapshot for 60 s, then fails closed | Latency rises, new logins may be refused | Automatic reconnect; cache warms |
| PostgreSQL primary | Unavailable | All writes and authentication fail; reads from replica are refused for authority | `503 DEPENDENCY_UNAVAILABLE` | Failover, 60–120 s |
| PostgreSQL replica | Unavailable | Reports queue; interactive traffic unaffected | Report delay | Replica rebuild |
| KMS | Unavailable | New logins and refreshes fail; existing access tokens remain valid until expiry; PII decryption fails closed | Login unavailable, existing sessions continue | KMS retry and escalation |
| Object storage | Unavailable | Evidence upload returns `503` with retry guidance; downloads fail | Upload and export unavailable | Retry with backoff |
| Outbox relay | Stopped | Business writes continue; events accumulate; projections lag | Dashboards stale, labelled by `as_of` | Restart; oldest-event alert |
| Event consumer | Stopped | Its projection or notification lags; no write impact | Feature-specific staleness | Replay from outbox |
| SMTP provider | Unavailable | Invitations and notices queue and retry; in-app notice still delivered | Email delay | Retry, alternate provider |
| Region | Unavailable | Tenant unavailable; DR runbook restores in the paired region | Outage | RTO 4 h, RPO 15 min |
| Search index | Unavailable | Search falls back to database `tsvector`; slower | p95 may exceed 1 s | Rebuild index |

**Fail-closed is a deliberate choice for authority.** If the denylist cannot be consulted beyond 60 seconds, the platform refuses requests rather than serving authority that may have been revoked. The 60-second window is the longest period for which a stale snapshot is defensible against the five-minute revocation commitment; beyond it, refusal is the safer failure. The trade-off is recorded in the availability review, not hidden in a retry policy.

### 14.10 Pilot Capacity Plan

| Component | Pilot specification | Headroom | Scale trigger |
|-----------|---------------------|----------|---------------|
| API hosts | 3 instances, 2 vCPU, 4 GB | 3x baseline | CPU above 60 per cent for 10 min |
| Worker hosts | 2 instances, 2 vCPU, 4 GB | 3x queue depth | Oldest event above 30 s |
| PostgreSQL | 1 primary, 1 replica, 4 vCPU, 16 GB, Multi-AZ | 2x pilot data | Connection saturation or p95 query regression |
| Redis | 1 primary, 1 replica, 2 GB | 4x working set | Eviction rate above zero |
| Object storage | S3-compatible, versioned, Object Lock | Effectively unbounded | Cost review at 5 TB |
| Regions | India and EU, one active per tenant | One region failure tolerated with DR | Tenant count per region above 250 |

**The pilot is sized for 25 tenants, not 500.** The capacity plan deliberately does not pre-build GA scale, because the load model will change once real grant and audit volumes are observed. The triggers define when to scale, and the load scenarios prove that scaling the API tier is horizontal while the database tier scales vertically to a point and then requires partitioning or a dedicated stack.

### 14.11 Rejected Patterns

| Pattern | Why it is rejected |
|---------|--------------------|
| Full permission list in the access token | Token size grows with the catalogue, revocation waits for expiry, and scope cannot be expressed |
| `roles` or `permissions` claims trusted by the resource server | The token becomes the authority instead of the database, and a role change cannot take effect within 60 s |
| Per-row policy function that queries another table | Turns a set query into N+1 queries inside the database; policies must be predicate-only |
| `BYPASSRLS` for reporting | One reporting bug becomes a cross-tenant breach; reports set context per tenant |
| `COUNT(*)` on list endpoints | Full scan per page and volume disclosure |
| `OFFSET` pagination | Cost grows linearly and results shift under concurrent writes |
| Session-level `SET app.org_id` | Leaks across pooled connections and survives a transaction |
| Caching a decision by role rather than by user and scope version | A role is not an authority; two users with the same role can have different scopes |
| Materialised views without `org_id` | The view cannot be protected by RLS and becomes an unaudited path |
| Read replica for session validation | Replication lag converts a revocation into a suggestion |

### 14.12 Traceability for Section 14

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| NFR-PERF-001 | Sections 14.2, 14.6 | Dashboard target and cache strategy |
| NFR-PERF-002 | Section 14.2 | Drill-down target |
| NFR-PERF-003 | Sections 14.2, 14.3 | Expense submission budget |
| NFR-PERF-004 | Section 14.2 | Projection freshness carried from Document 6 |
| NFR-PERF-007 | Sections 14.2, 14.9 | Report target and queue behaviour |
| NFR-PERF-008 | Section 14.2 | Search target and fallback |
| NFR-PERF-012 | Sections 14.2, 14.3 | CRUD target and authorization budget |
| NFR-AVL-001 | Section 14.2 | MVP availability |
| NFR-AVL-004 | Section 14.9 | Defined degradation modes |
| NFR-AVL-005 | Section 14.9 | Offline behaviour preserved |
| NFR-AVL-006 | Section 14.9 | RTO in region failure |
| NFR-AVL-007 | Section 14.9 | RPO in region failure |
| NFR-SCL-001 | Section 14.5 | Volume model |
| NFR-SCL-002 | Section 14.8 | Concurrent-user load scenario |
| NFR-SCL-003 | Section 14.5 | Object storage forecast |
| NFR-SCL-004 | Section 14.10 | Horizontal API scaling |
| NFR-SCL-005 | Section 14.8 | Peak and attack-mix scenarios |
| NFR-SCL-006 | Sections 14.4, 14.5 | Partitioning and archival |
| NFR-SEC-004 | Sections 14.4, 14.7 | RLS-aware query and connection model |
| NFR-AUD-005 | Sections 14.2, 14.4 | Audit query target and partition rule |
| FR-TNT-010 | Section 14.2 | Role propagation target |
| FR-TNT-016 | Section 14.2 | Revocation cascade target |
| FR-TNT-022 | Section 14.6 | Session cache and denylist |
| CAP-001 | Section 14.10 | Tenant capacity plan |
| CAP-002 | Section 14.6 | Identity cache model |
| CAP-003 | Section 14.2 | Decision latency target |

---

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
| NFR-OBS-003 | Section 15.2 | Health and readiness endpoints inherited from Document 6 |
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
| Actor | Member with each of the 18 seeded roles, custom role, grantee with each of 3 profiles, platform operator, service account, anonymous |
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

---

## 17. Decisions, Open Items and Downstream Obligations

### 17.1 Architecture Decisions Owned by This Document

This document owns ADR-016 to ADR-024. The full records (context, alternatives, consequences, downstream obligations) live in the programme ADR register, Document 17 section 4, so that every team reads decisions from one place. The table below is the index.

| ADR | Decision | Status | Driver |
|-----|----------|--------|--------|
| ADR-016 | Identity, membership, assignment and grant are four separate relations; one home organization per human; one membership at MVP | Accepted | AD4-02, AD4-09 |
| ADR-017 | The Cross-Organization Access Grant is a first-class aggregate with an eight-state lifecycle, two-person approval and grantee acceptance gates | Accepted | AD4-02, BR-AUD-002 to BR-AUD-004 |
| ADR-018 | Closed, flat permission catalogue `domain.resource.action` with nine actions and seven scope types; effective scope is an intersection | Accepted | AD4-01, BR-ACC-002, BR-ACC-003 |
| ADR-019 | In-process policy decision point with a `scope_version`-keyed cache; no external policy engine at MVP | Accepted | AD4-04, AD4-05 |
| ADR-020 | Segregation of duties is a decision-time veto backed by a time-boxed exception register | Accepted | AD4-03, BR-ACC-006 |
| ADR-021 | Thin access tokens, a session registry and a denylist deliver revocation within five minutes; 14-day rotating refresh, 30-day absolute session | Accepted | AD4-05, BR-ACC-007 |
| ADR-022 | Identity personal data is held under a per-user key and erased cryptographically; the pseudonymous accountability record survives | Accepted | NFR-PRV-001, NFR-PRV-002, NFR-AUD-003 |
| ADR-023 | A device is never a principal; offline validity is a bounded device grant of at most 7 days layered on a human session | Accepted | AD4-08, D-015 |
| ADR-024 | Tenant provisioning is an idempotent, compensated workflow that ends with a negative isolation probe | Accepted | AD4-10, FR-TNT-001 |

ADR-025 (show hosting topology) is owned by Document 2 and changes section 5.5 as described in Appendix G.

### 17.2 Open Items

Open questions raised in sections 3.2 (OQ-4-01, multi-organization membership) and 8.16 are consolidated, with owners and due dates, in the open-decision register of Document 17 section 5. None of them blocks the MVP build; OQ-4-01 blocks Phase 1 multi-membership.

### 17.3 Downstream Obligations

Appendix G lists the obligations every downstream document must consume. An obligation that cannot be met is raised as a change request against this document, never resolved locally.

---

## Appendix A — Permission Catalogue

### A.1 Rules

| Rule | Detail |
|------|--------|
| Grammar | `domain.resource.action`, lowercase snake_case, singular resource |
| Actions | Exactly nine: `read`, `create`, `update`, `delete`, `submit`, `approve`, `reject`, `export`, `administer` (section 5.2) |
| Flat | No action implies another; `approve` does not imply `read` |
| Scope | A key is granted at one or more of the seven scope types; the catalogue states the scopes at which a key is meaningful |
| Step-up | Keys marked in the Step-up column require a fresh MFA assertion (at most 15 minutes old) for each use |
| Release | Keys marked **P1** or **P2** exist in the manifest but are not seeded into any role until that release |
| Platform-only | Every `ops.*` key is held only by platform-internal roles; the manifest build fails if a tenant role or grant profile references one |
| `delete` semantics | For financial and identity records `delete` means deactivate, revoke, cancel or soft-delete. No permission authorizes physical deletion of a financial record (BR-FIN-009) |

Legend for actions: R read, C create, U update, D delete, S submit, A approve, J reject, E export, X administer.

### A.2 Catalogue

#### tenancy (MOD-01)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `organization` | R U X | ORG | X | `U` profile, branding, locale, fiscal calendar; `X` security policy, retention defaults |
| `membership` | R C U D | ORG | D | `D` deactivates; never a physical delete |
| `invitation` | R C D | ORG | — | 72-hour expiry (FR-TNT-004) |
| `role` | R C U D X | ORG | C U D X | System roles are immutable; `X` covers custom-role ceilings |
| `permission` | R | ORG | — | Catalogue is read-only to every tenant |
| `assignment` | R C U D | ORG | C U D | SoD is evaluated at assignment (section 5.8) |
| `approval_limit` | R U | ORG, SHOW | U | Cannot exceed the organization ceiling |
| `delegation` | R C D | ORG, SHOW, SELF | C | Section 5.9 |
| `sod_rule` | R | ORG | — | Rules change only by release |
| `sod_exception` | R C A D | ORG | C A D | Two approvers; maximum 90 days |
| `access_review` | R C U A | ORG | A | `A` certifies a campaign item |
| `auditor_profile` | R U A | ORG (firm) | A | `A` is firm attestation of an auditor |
| `entitlement` | R | ORG | — | Changed only through `ops.entitlement` |

#### identity (MOD-02)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `user` | R U D E | ORG, SELF | D E | `U` at SELF is profile self-service; `D` deprovisions; `E` produces the DSR identity bundle |
| `credential` | X | ORG | X | Administrative reset; never reveals a secret |
| `mfa_factor` | R D X | ORG, SELF | D X | `X` is the two-person reset (SOD-09) |
| `session` | R D | ORG, SELF | — | `D` revokes |
| `device` | R U D | ORG, SELF | D | `D` revokes and queues a wipe |
| `service_account` | R C U D | ORG | C U D | **P1** tenant integrations |
| `federated_identity` | R X | ORG | X | **P1** SSO and SCIM |

#### production (MOD-03)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `show` | R C U D S A | ORG, SHOW | A | `D` cancels a `PLANNING` show with no financial records; `S` requests closure; `A` approves closure or archive |
| `season` | R C U | SHOW, SEASON | — | |
| `episode` | R C U | SHOW, SEASON, EPISODE | — | |
| `department` | R C U | ORG, SHOW, DEPARTMENT | — | Hierarchy at most three levels (BR-PRD-006) |
| `category` | R C U | ORG | — | Organization taxonomy and department mapping |
| `party` | R C U | ORG, SHOW | — | Vendor-type party record (D-004) |
| `party_link` | R C A D | ORG | A | `A` accepts a sharing agreement; both organizations must approve |
| `comment` | R C | SHOW, SEASON, EPISODE, DEPARTMENT | — | Non-financial collaboration |

#### budget (MOD-04)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `budget` | R C | SHOW, SEASON | — | Container per show or season |
| `budget_version` | R C U S A J E | SHOW, SEASON | A J | Approval is amount-bounded (section 5.9) |
| `budget_line` | R | SHOW, SEASON, DEPARTMENT | — | Lines change only through `budget_version.update` on a draft |
| `variation` | R C A J | SHOW, SEASON | A | Reallocation between lines (BR-BUD-006) |
| `episode_allocation` | R U | SEASON, EPISODE | — | Ceiling rule BR-BUD-007 |
| `financial_period` | R U A | ORG, SHOW | U A | `U` closes and locks or requests reopen; `A` approves reopen or a close waiver |
| `approval_workflow` | R X | ORG, SHOW | X | Chain definitions for budget, expense, commitment, period and import approvals |
| `forecast` | R C U | SHOW, SEASON, DEPARTMENT | — | **P1** EFC (FR-CIN-003) |
| `comment` | R C | SHOW, SEASON | — | FR-BUD-015 |

#### vendor (MOD-05)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `vendor` | R C U A E | ORG, SHOW | A | `A` activates a vendor candidate, blocks or unblocks |
| `contact` | R C U | ORG | — | |
| `contract` | R C U | ORG, SHOW | — | **P1** attachments (FR-VND-020) |
| `payment_term` | R U | ORG | U | Includes bank references; masked on read without `U` (BR-VND-007) |
| `risk_rating` | R U | ORG | U | FR-VND-005 |
| `commitment` | R C U S A J E | SHOW, DEPARTMENT | A | Manual commitments at MVP |
| `purchase_order` | R C U S A J E | SHOW, DEPARTMENT | A | **P1** |
| `payment_reference` | R C U | ORG, SHOW | — | Reconciliation only; no payment execution |

#### audit (MOD-06)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `audit_plan` | R C U | ORG, SHOW | — | Coverage requirements |
| `audit_team` | R C U | ORG, SHOW | — | FR-AUD-001 |
| `audit_assignment` | R C U D A | ORG, SHOW, ASSIGNMENT | A D | `A` activates; `D` revokes |
| `auditor_work_profile` | R U | ORG, SELF | — | FR-AUD-005 professional profile |
| `audit_finding` | R C U S A J | SHOW, ASSIGNMENT | — | `A` closes a finding; `J` returns a response |
| `finding_response` | R C | SHOW | — | Production-side response to a shared finding |
| `audit_evidence` | R C | SHOW, ASSIGNMENT | — | Working papers |
| `audit_report` | R C U S A J E | SHOW, ASSIGNMENT | — | Daily and periodic audit reports |
| `access_grant` | R C U A D E X | ORG | C A D X | Section 8; SOD-07 |

#### expense (MOD-07)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `expense` | R C U D S A J E | SHOW, SEASON, EPISODE, DEPARTMENT, ASSIGNMENT, SELF | A J | `D` discards a draft or cancels a record; `A` and `J` are limit-bounded |
| `exception` | R C U A | SHOW, DEPARTMENT, ASSIGNMENT | A | `U` acknowledges, resolves or marks false positive; `A` approves a waiver |
| `comment` | R C | SHOW, DEPARTMENT, ASSIGNMENT | — | FR-EXP-027 |

#### intel (MOD-08)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `cost_fact` | R | ORG, SHOW, DEPARTMENT | — | Drill-down source |
| `variance` | R E | ORG, SHOW, SEASON, EPISODE, DEPARTMENT | — | |
| `portfolio_view` | R E | ORG | — | DSH-01 |
| `alert_rule` | R C U D | ORG, SHOW | — | FR-CIN-007 |
| `alert` | R U | ORG, SHOW, DEPARTMENT | — | Acknowledge, resolve, false positive |
| `benchmark` | R | ORG | — | **P1** |
| `anomaly` | R | ORG, SHOW | — | **P2** |

#### document (MOD-09)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `document` | R C U D E | SHOW, DEPARTMENT, ASSIGNMENT, SELF | E | `R` covers preview and signed-URL issuance; `E` bulk archive |
| `version` | R C | SHOW, ASSIGNMENT | — | |
| `evidence` | R A J | SHOW, DEPARTMENT, ASSIGNMENT | — | `A` verifies, `J` disputes (FR-DOC-007) |

#### excel (MOD-10)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `template` | R C U D | ORG | — | FR-XLS-001 |
| `import_job` | R C U D A | ORG, SHOW | A | `A` is the second-person commit approval where policy requires it; commit also requires the target domain's `create` |
| `export_job` | R C | ORG, SHOW, DEPARTMENT | — | Rows are limited to the requester's effective scope at generation time |
| `validation_error` | R | ORG, SHOW | — | |

#### notification (MOD-11)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `notification` | R U | SELF | — | `U` marks read |
| `preference` | R U | SELF | — | Within policy limits |
| `template` | R U | ORG | — | FR-NTF-011 |
| `announcement` | C | ORG, SHOW | — | **P1** |

#### compliance (MOD-12)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `audit_event` | R E | ORG, SHOW | E | No `U`, `D`, or `X` exists |
| `security_event` | R E | ORG | E | |
| `legal_hold` | R C U A | ORG | C U A | `U` requests release; `A` is the second approver (BR-DOC-003) |
| `retention_policy` | R C U A | ORG | U A | |
| `dsr_request` | R C U A | ORG | A | |
| `ropa_entry` | R U | ORG | — | |
| `breach_incident` | R C U A | ORG | A | |
| `control` | R U | ORG | — | |
| `evidence_pack` | R C E | ORG | E | |

#### report (MOD-13)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `report_definition` | R C U D | ORG | — | |
| `report_run` | R C E | ORG, SHOW, DEPARTMENT, ASSIGNMENT | E for restricted reports | |
| `dashboard` | R | ORG, SHOW, DEPARTMENT, ASSIGNMENT | — | |
| `saved_view` | R C U D | SELF | — | FR-CIN-023 |
| `schedule` | R C U D | ORG, SHOW | — | **P1** |

#### sync (MOD-14)

| Resource | Actions | Meaningful scopes | Step-up | Notes |
|----------|---------|-------------------|---------|-------|
| `sync_batch` | R C | SELF | — | Device push and pull |
| `sync_conflict` | R U A | SELF, SHOW, ASSIGNMENT | — | `A` is the manager resolution after 7 days (UC-05) |
| `checkpoint` | R | SELF | — | |

#### ops (MOD-15, platform-only)

| Resource | Actions | Notes |
|----------|---------|-------|
| `tenant` | R C U X | Provision, suspend, archive, purge workflows |
| `tenant_region` | R | Placement is set at provisioning |
| `provisioning_run` | R C | |
| `entitlement` | R U | Plan and override management |
| `support_access` | R C A D | SOD-08 |
| `feature_flag` | R U | |
| `job` | R U | `U` replays a dead-lettered job |
| `key` | X | Rotation and destruction |
| `identity_transfer` | R C A | Section 10.8 |
| `service_account` | R C U D | Platform-internal machine identities |

### A.3 Normalised Keys

Earlier sections of this document contained a small number of shorthand or legacy keys. They were normalised during assembly (Appendix G, item G-07); the manifest accepts only the canonical form.

| Legacy form | Canonical key |
|-------------|---------------|
| `identity.mfa.administer` | `identity.mfa_factor.administer` |
| `identity.user.update_self` | `identity.user.update` held at `SELF` |
| `tenancy.sod.read` | `tenancy.sod_rule.read` or `tenancy.sod_exception.read` |
| `tenancy.sod.administer` | `tenancy.sod_exception.create` or `tenancy.sod_exception.delete` |
| `compliance.legal_hold.release` | `compliance.legal_hold.update` (request) plus `compliance.legal_hold.approve` (second approver) |
| `document.read`, `document.export` (grant-profile shorthand) | `document.document.read`, `document.document.export` |

### A.4 System Role Grants

Unless stated, a role holds the listed keys at every scope type it may be assigned (section 5.5). Every member additionally holds the SELF bundle in A.4.19.

| # | Role | Granted keys (resource: actions) |
|---|------|----------------------------------|
| A.4.1 | `ORG_ADMIN` | tenancy: organization R U X; membership R C U D; invitation R C D; role R C U D X; permission R; assignment R C U D; approval_limit R; delegation R D; sod_rule R; sod_exception R C; access_review R C U; entitlement R. identity: user R U D; credential X; mfa_factor R X; session R D; device R D. production: show R; department R C U; category R C U. excel: template R C U D. notification: template R U. compliance: audit_event R; security_event R |
| A.4.2 | `COMMISSIONING_ADMIN` | production: show R C U S A; season R C U; episode R C U; department R; category R; party R C U; party_link R C A D. budget: budget R; budget_version R A J E; variation R; financial_period R. vendor: vendor R; commitment R. audit: audit_plan R C U; audit_assignment R C U D A; audit_report R; audit_finding R; access_grant R C U A D E. expense: expense R; exception R. intel: cost_fact R; variance R E; portfolio_view R E; alert R U. document: document R. report: dashboard R; report_run R C E |
| A.4.3 | `PRODUCTION_ADMIN` | production: show R C U S; season R C U; episode R C U; department R C U; category R; party R C U; party_link R C A. budget: budget R C; budget_version R C U S E; episode_allocation R U; variation R C; comment R C. vendor: vendor R C U E; contact R C U; contract R C U; commitment R C U S E; payment_reference R. audit: audit_finding R; finding_response R C. expense: expense R E; exception R U; comment R C. document: document R C U; version R C. excel: import_job R C U; export_job R C. intel: variance R. report: dashboard R; report_run R C E |
| A.4.4 | `FINANCE_HEAD` | production: show R U A; party R C U. budget: budget R C; budget_version R C U S A J E; variation R A J; episode_allocation R U; financial_period R U A; approval_workflow R X; forecast R C U; comment R C. tenancy: approval_limit R U; delegation R C D. vendor: vendor R A E; risk_rating R U; commitment R A J E; purchase_order R A J. expense: expense R A J E; exception R U A. intel: cost_fact R; variance R E; portfolio_view R E; alert_rule R C U D; alert R U. document: document R; evidence R. excel: import_job R A; export_job R C. report: dashboard R; report_run R C E. compliance: audit_event R |
| A.4.5 | `COST_CONTROLLER` | production: show R. budget: budget R; budget_version R C U S E; variation R C; financial_period R U; forecast R C U; comment R C. vendor: vendor R; commitment R A J E. expense: expense R A J E; exception R U. intel: cost_fact R; variance R E; portfolio_view R E; alert_rule R C U D; alert R U. document: document R; evidence R A J. excel: import_job R C U A; export_job R C. report: dashboard R; report_run R C E |
| A.4.6 | `DEPARTMENT_HEAD` | production: show R; department R. budget: budget_version R; budget_line R. vendor: vendor R; commitment R. expense: expense R C U S A J E; exception R U; comment R C. document: document R C; evidence R A J. intel: variance R. report: dashboard R; report_run R C E |
| A.4.7 | `ACCOUNTS_EXECUTIVE` | production: show R. budget: budget_version R. vendor: vendor R C U E; contact R C U; payment_term R U; payment_reference R C U; commitment R C U S E; purchase_order R C U S. expense: expense R C U D S E; exception R; comment R C. document: document R C U; version R C. excel: template R; import_job R C U D; export_job R C; validation_error R. report: dashboard R; report_run R C E |
| A.4.8 | `AUDIT_MANAGER` | production: show R. budget: budget_version R. vendor: vendor R. audit: audit_plan R C U; audit_team R C U; audit_assignment R C U D A; auditor_work_profile R U; audit_finding R C U S A J; audit_evidence R C; audit_report R C U A J E; access_grant R C U A D E. expense: expense R E; exception R C U. document: document R; evidence R A J. sync: sync_conflict R U A. intel: variance R. report: dashboard R; report_run R C E |
| A.4.9 | `INTERNAL_AUDITOR` | production: show R; season R; episode R; department R; category R. budget: budget_version R. vendor: vendor R C. expense: expense R C U D S; exception R C; comment R C. document: document R C; version R C; evidence R. audit: audit_assignment R; audit_finding R C U S; audit_evidence R C; audit_report R C U S. sync: sync_batch R C; sync_conflict R U; checkpoint R |
| A.4.10 | `MANAGEMENT_VIEWER` | production: show R; season R; episode R; department R. budget: budget_version R. vendor: vendor R. intel: cost_fact R; variance R E; portfolio_view R E; alert R. report: dashboard R; report_run R C E |
| A.4.11 | `PRODUCTION_COORDINATOR` | production: show R U; season R C U; episode R C U; department R; party R; comment R C. document: document R C U; version R C |
| A.4.12 | `VENDOR_COORDINATOR` | vendor: vendor R C U; contact R C U; contract R C U; payment_term R U; risk_rating R |
| A.4.13 | `COMPLIANCE_OFFICER` | compliance: audit_event R E; security_event R E; legal_hold R C U A; retention_policy R C U A; dsr_request R C U A; ropa_entry R U; breach_incident R C U A; control R U; evidence_pack R C E. tenancy: access_review R |
| A.4.14 | `ACCESS_REVIEWER` | tenancy: access_review R C U A; membership R; assignment R; role R; delegation R; sod_exception R. audit: access_grant R |
| A.4.15 | `AUDIT_FIRM_ADMIN` | tenancy: membership R C U D; invitation R C D; assignment R C U D; auditor_profile R U A. identity: user R U D; session R D; device R D. audit: access_grant R (grants held by the firm's auditors, metadata only) |
| A.4.16 | `SUPPORT_ENGINEER` | ops: support_access R C; tenant R; job R; feature_flag R |
| A.4.17 | `PLATFORM_OPERATIONS` | ops: tenant R C U X; tenant_region R; provisioning_run R C; entitlement R U; feature_flag R U; job R U; identity_transfer R C; service_account R C U D; support_access R C |
| A.4.18 | `PLATFORM_SECURITY` | ops: support_access R A D; key X; identity_transfer R A. tenancy: sod_exception A (SOD-04 and SOD-10 only). compliance: security_event R E (platform scope) |
| A.4.19 | SELF bundle (every member) | identity: user R U; mfa_factor R; session R D; device R D. notification: notification R U; preference R U. report: saved_view R C U D. tenancy: delegation R C D (only for keys the member already holds with `approve`). expense: expense R (own records). audit: auditor_work_profile R U (own profile, auditors only) |

**Assignment guards.** `INTERNAL_AUDITOR` may be assigned only at `SHOW` or `ASSIGNMENT` scope; `DEPARTMENT_HEAD` only at `SHOW` or `DEPARTMENT`. Both rules are enforced when the assignment is created and re-checked at every decision (section 6).

### A.5 Grant Profile Expansion

| Profile | Expanded keys |
|---------|---------------|
| `AUDIT_READ` | production: show, season, episode, department, category R. budget: budget_version R; budget_line R. expense: expense R. vendor: vendor R. document: document R; version R; evidence R. report: dashboard R; report_run R. audit: audit_assignment R; audit_finding R |
| `AUDIT_EVIDENCE` | `AUDIT_READ` plus document: document C E (evidence only, Family C); version C. audit: audit_evidence R C; audit_finding C U S; audit_report R |
| `AUDIT_FINANCE` | `AUDIT_EVIDENCE` plus vendor: commitment R. intel: variance R. report: report_run E. The `budget`, `expense` and `vendor` read keys named in section 5.7 are already present through `AUDIT_READ` |

### A.6 Mapping of the Governing Brief's Actors

The governing architecture brief names fourteen actors. Each maps to roles or grant profiles as follows.

| Brief actor | Realised as | Scope | Notes |
|-------------|-------------|-------|-------|
| SaaS Super Administrator | `PLATFORM_OPERATIONS`, `PLATFORM_SECURITY`, `SUPPORT_ENGINEER` | Platform | Deliberately split; no single platform role reads tenant data or approves its own break-glass (BR-ACC-008) |
| Organization Administrator | `ORG_ADMIN` | ORG | No financial approval |
| OTT/Broadcaster Administrator | `COMMISSIONING_ADMIN` | ORG, SHOW | Appointing party for platform auditors (section 8.13) |
| Finance Head | `FINANCE_HEAD` | ORG, SHOW | Final budget approval, period reopen, waivers |
| Cost Controller | `COST_CONTROLLER` | ORG, SHOW | Added at assembly (Appendix G, G-05) |
| Audit Manager | `AUDIT_MANAGER` | ORG to ASSIGNMENT | |
| Internal Auditor | `INTERNAL_AUDITOR` | SHOW, ASSIGNMENT | Captures expenses and evidence |
| External Auditor | Grant with `AUDIT_READ`, `AUDIT_EVIDENCE` or `AUDIT_FINANCE`; home organization of type `AUDIT_FIRM` | Grant scope | No role, no membership in the client |
| Platform-appointed Auditor | Grant created by the appointing party (section 8.13), or `INTERNAL_AUDITOR` when employed by the hosting commissioner | Grant or ASSIGNMENT | Revocable only by the appointing party (BR-AUD-006) |
| Production House Administrator | `PRODUCTION_ADMIN` at ORG | ORG | |
| Production Manager | `PRODUCTION_ADMIN` at SHOW, optionally with `PRODUCTION_COORDINATOR` | SHOW | Line-producer authority on one show |
| Department Head | `DEPARTMENT_HEAD` | SHOW, DEPARTMENT | |
| Accounts Executive | `ACCOUNTS_EXECUTIVE` | ORG, SHOW, DEPARTMENT | |
| Management Viewer | `MANAGEMENT_VIEWER` | ORG | Read and export of aggregates only |

### A.7 Notes

1. `MANAGEMENT_VIEWER` holds no write key, so alert acknowledgement (persona P-11 primary tasks) is performed by `COST_CONTROLLER` or `FINANCE_HEAD`. If the business owner requires viewers to acknowledge alerts, `intel.alert.update` is added to the role by a catalogue release; it does not affect SoD.
2. Approval keys (`budget.budget_version.approve`, `expense.expense.approve`, `vendor.commitment.approve`, `budget.variation.approve`) are always evaluated together with the approval-limit chain in section 5.9 and the SoD vetoes in section 5.8. Holding the key is necessary, never sufficient.

---

## Appendix D — Table and RLS Policy Inventory

Families are defined in section 7.4. Schema names follow Document 6 section 8.2. Column-level definitions are in Document 8; this appendix is the authority for each table's isolation family.

| Schema | Tables | Family | Notes |
|--------|--------|--------|-------|
| `tenancy` | `organization` | A (keyed on `org_id = id`) | Tenant directory |
| `tenancy` | `organization_setting`, `organization_entitlement`, `membership`, `invitation`, `role`, `role_permission`, `role_version`, `assignment`, `assignment_scope`, `delegation`, `approval_limit`, `sod_exception`, `access_review_campaign`, `access_review_item`, `auditor_profile` | A | Self-read policy on `membership`, `delegation` |
| `tenancy` | `access_grant`, `access_grant_version`, `grant_acknowledgment`, `coi_declaration`, `grant_expiry_notice` | A plus grantee self-read (C for `access_grant`) | Client-owned rows; the grantee reads own grants only |
| `tenancy` | `sod_rule`, `permission`, `grant_profile`, `plan`, `entitlement` | E | Global, read-only; `entitlement` is the plan manifest |
| `tenancy` | `usage_counter`, `grant_projection` | A | `grant_projection` is the Phase 1 cross-region minimal projection (section 8.12) |
| `identity` | `user` (keyed on `home_org_id`), `user_pii`, `user_email_alias`, `credential`, `mfa_factor`, `recovery_code`, `refresh_session`, `device_registration`, `password_reset`, `email_change`, `service_account`, `federated_identity`, `identity_transfer` | A | Secrets readable only by the identity service role |
| `prod` | `show`, `season`, `episode`, `department`, `category`, `category_department`, `party`, `show_party`, `party_organization_link`, `show_closure` | B (C allowlist for show, season, episode, department, category) | `party_organization_link` is visible to both linked organizations through a published view |
| `budget` | `budget`, `budget_version`, `budget_line`, `episode_allocation`, `variation`, `approval_workflow`, `approval_workflow_step`, `approval_instance`, `approval_step`, `approval_decision`, `financial_period`, `period_close_run`, `forecast` | B (C allowlist for budget, budget_version, budget_line) | `forecast` is Phase 1; approval tables hold instances for non-expense subjects (Document 8 RC-08-01) |
| `budget` | `fx_rate` | A | Organization rate table (D2-04, D-007); append-only |
| `vendor` | `vendor`, `vendor_tax_registration`, `vendor_contact`, `vendor_bank_reference`, `manual_commitment`, `commitment_amendment`, `contract`, `purchase_order`, `purchase_order_line`, `po_receipt`, `payment_reference` | A for vendor master tables; B for commitment and purchase-order tables (C allowlist for vendor, manual_commitment) | `vendor_bank_reference` is Restricted; column encryption |
| `auditops` | `audit_team`, `audit_team_member`, `audit_assignment`, `audit_scope`, `auditor_work_profile`, `coverage_requirement`, `audit_report`, `audit_finding`, `finding_response`, `audit_evidence` | B (C allowlist per section 7.4) | |
| `expense` | `expense`, `expense_line`, `expense_split`, `expense_tax`, `evidence_link`, `approval_instance`, `approval_step`, `approval_decision`, `expense_amendment`, `exception`, `exception_activity`, `expense_comment`, `cost_posting`, `expense_key`, `expense_number_counter` | B (C allowlist for expense, expense_line) | `cost_posting` is append-only (Document 8) |
| `intel` | `cost_fact`, `commitment_fact`, `variance_snapshot`, `alert_rule`, `alert_instance`, `saved_view`, `dashboard_definition`, `benchmark_aggregate`, `projector_checkpoint` | B; `benchmark_aggregate` is E (anonymised, cross-tenant by design, Phase 1) | Rebuildable |
| `docs` | `document`, `document_version`, `document_link`, `document_access_log`, `scan_result`, `upload_ticket`, `retention_policy` | B (C allowlist for document, document_version) | `document_access_log` is append-only |
| `xls` | `excel_template`, `template_version`, `value_mapping`, `import_batch`, `import_row`, `export_job` | A for templates; B for batches and jobs | |
| `notify` | `notification_template`, `notification`, `notification_preference`, `delivery_attempt`, `digest_run`, `escalation_policy`, `announcement` | A with recipient self-read | |
| `compliance` | `audit_event`, `audit_chain_head`, `audit_anchor`, `security_event` | D | Append-only trigger |
| `compliance` | `legal_hold`, `legal_hold_target`, `dsr_request`, `ropa_entry`, `breach_incident`, `control_evidence`, `rule_override` | A | `rule_override` is the override and exception register (Document 5) |
| `report` | `report_definition`, `report_run`, `report_output`, `report_schedule` | B | `report_schedule` Phase 1 |
| `sync` | `device_sync_state`, `sync_cursor`, `sync_batch`, `mutation_log`, `conflict_record`, `change_log` | A with device-owner self-read | `change_log` is the pull feed (Document 6 §9.3) |
| `ops` | `outbox_event`, `event_delivery`, `inbox_dedup`, `job`, `job_attempt`, `idempotency_key` | A (row carries `org_id`; platform rows use the platform tenant) | Written by `slateiq_app` in the business transaction; claimed across tenants by `slateiq_dispatch` (Document 8 RC-08-02) |
| `ops` | `tenant_region`, `provisioning_run`, `support_access`, `feature_flag`, `region_capacity`, `key_rotation`, `isolation_probe` | F | Not granted to `slateiq_app` |
| `ref` | `currency`, `country`, `tax_jurisdiction` | E | Global reference data |

**Inventory test.** A catalogue query in CI lists every table in every application schema and fails the build if a table is absent from this inventory, lacks `org_id` (except families E and F), lacks `ENABLE` and `FORCE ROW LEVEL SECURITY`, or lacks the family's policies (section 16.5).

---

## Appendix E — Control and Evidence Index

| Control area | Control | Evidence artefact | Produced by | Section |
|--------------|---------|-------------------|-------------|---------|
| Isolation | RLS on every tenant table; negative probe at provisioning | CI inventory report; provisioning run log | CI, MOD-15 | 7, 16.5, Appendix D |
| Authentication | MFA for privileged roles and grantees | MFA enrolment report; `MfaEnrolled` events | MOD-02 | 4 |
| Authorization | Deny-by-default PDP; per-endpoint negative tests | Authorization matrix test report | CI | 5.12, 16.6 |
| Segregation of duties | Decision-time veto; exception register | `SodViolation` events; exception register export | MOD-01 | 5.8 |
| Access governance | Quarterly access review | Campaign certification export | MOD-01 | 5.10 |
| External access | Grant approval, acceptance gates, expiry | Grant history, NDA and COI acknowledgments, expiry notices | MOD-01 | 8 |
| Revocation | Five-minute revocation | Revocation latency metric and alert history | Telemetry | 15.3 |
| Privileged access | Break-glass with two approvers, 8-hour bound | `support_access` records and session recordings | MOD-15 | 4, 10.10 |
| Change management | Signed images, reviewed migrations | Pipeline attestations and SBOMs | CI/CD | Document 15 |
| Audit integrity | Hash-chained audit events with daily anchors | Chain verification report; anchor objects | MOD-12 | Document 14 |

The full SOC 2 evidence plan is in section 15.11 and Document 10.

---

## Appendix G — Change Requests and Downstream Obligations

### G.1 Change Requests Raised by This Document

`D4-xx` identifiers are retained verbatim from the draft stage, when this document was numbered 4. Their target, then called Document 3, is now Document 6.

| ID | Target | Change | Origin | Status |
|----|--------|--------|--------|--------|
| D4-01 | Document 6 Appendix C.4 | Idle timeout default 30 minutes, configurable 15 minutes to 8 hours | RN-01 | Open |
| D4-02 | Document 6 section 13.3 | Password policy wording aligned to RN-02 (12 characters, breach list, three of four classes or 16-character passphrase, no forced rotation) | RN-02 | Open |
| D4-03 | Document 6 Appendix C.4 | Refresh token 14 days rotating; absolute session 30 days | RN-03 | Open |
| D4-04 | Document 1 FR-TNT-023 | Region is structural from MVP; selector and migration tooling remain Phase 1 | RN-05 | Open |
| D4-05 | Document 6 Appendix C.2 | Add `ASSIGNMENT` as the seventh scope type | RN-06 | Open |
| D4-06 | Document 6 Appendix B.2 | Replace the single `GrantCreated` event with the lifecycle events of section 12.8 | Section 12.8 | Open |

### G.2 Changes Applied at Assembly (2026-11-06)

| ID | Change | Reason |
|----|--------|--------|
| G-01 | Document renumbered from 4 to 3 and retitled "Actor, Permission, Tenancy & Identity Architecture"; document references renumbered to the re-baselined set | Governing brief document structure (D-023, `00-foundation-index.md`) |
| G-02 | Logical table names `document.*` and `audit.*` in sections 6 and 7 written as the physical schemas `docs.*` and `auditops.*` | Document 6 section 8.2 schema names are binding |
| G-03 | Section 17 and Appendices A, D, E and G completed | Draft tasks T7 and T8 |
| G-04 | Cross-module references: within a module schema, composite foreign keys `(org_id, id)` enforce same-tenant integrity; across module schemas, no foreign key exists and `org_id` equality is asserted by the owning module's contract on write and by nightly reconciliation | Reconciles section 9.9 with Document 6 section 5.4 and ADR-001; detailed in Document 8 |
| G-05 | `COST_CONTROLLER` added as an eighteenth system role; `FINANCE_HEAD` renamed "Finance Head" | Governing brief lists Cost Controller as a distinct actor; Document 1 assigns revision, reallocation and period lock to Cost Controllers and reopen approval to Finance Heads (FR-BUD-008, FR-BUD-010, FR-BUD-012) |
| G-06 | Production-side roles may be seeded in OTT and broadcaster tenants that host a commissioned show (section 5.5 note) | ADR-025, provisional, owned by Document 2 |
| G-07 | Legacy permission keys normalised (Appendix A.3) | Nine-action rule in section 5.2 |

### G.3 Downstream Obligations

| Document | Must consume | Must produce or honour |
|----------|--------------|------------------------|
| 4 Workflows & State Machines | Sections 5.8, 5.9, 8.2, Appendix A | Every transition names its permission key and SoD rule |
| 7 Module & Dependency | Section 5.12, Appendix A | Every module endpoint declares one catalogue key; client applications render from `GET /me/permissions` only as a hint |
| 8 Database Conceptual Model | Sections 7 and 9, Appendix D | Physical tables, composite keys, RLS policies per family, inventory test |
| 9 API Architecture | Sections 11 and 12, Appendix A | Permission and scope per endpoint; denial semantics of section 5.11 |
| 10 Security | Sections 4, 7, 13, Appendix E | Control catalogue and ASVS mapping |
| 11 Mobile & Offline | Sections 4.6 and 4.8, ADR-023 | Device grant, wipe and re-authentication behaviour |
| 14 Audit & Observability | Sections 12 and 15 | Security event taxonomy and revocation telemetry |
| 15 Infrastructure & Deployment | Sections 2.9, 7.6, 10 | Provisioning workflow, regional placement, break-glass tooling |
| 16 Testing Strategy | Section 16, Appendix D | Isolation suite, authorization matrix, grant lifecycle tests |
| 18 Roadmap | Sections 10 and 17 | Build order: tenancy and identity before any business module |

---

## Document Approval

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Principal Architect | | | |
| Security Architect | | | |
| Identity Architect | | | |
| Data Architect | | | |
| Product Owner | | | |
| Security & Compliance | | | |

*End of Document 3*
