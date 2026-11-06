# Document 3: System Architecture & Technology Stack

**Product:** SlateIQ — Production Cost Intelligence & Audit Monitoring Platform
**Document ID:** SLQ-ARCH-003
**Version:** 1.0.0 (System Architecture Baseline)
**Supersedes:** `03-actor-permission-architecture.md` v1.0.0 — archived at `_archive/03-actor-permission-architecture.v1.0.0.md`; its access-control content is an input to Document 4
**Classification:** Internal — Architecture Foundation
**Status:** Draft — For Review
**Date:** 2026-11-05
**Authors:** Principal Enterprise Solution Architect, Platform Architect, Security Architect, Data Architect
**Reviewers:** Product Owner (TBD), Security & Compliance (TBD), Finance SME (TBD), Principal Architect (TBD)

---

## Document Control

### Approval Matrix

| Role | Name | Responsibility | Approval Required |
|------|------|----------------|-------------------|
| Principal Architect | TBD | Architectural integrity and cross-document consistency | Yes |
| Platform Architect | TBD | Runtime, deployment, and technology selection | Yes |
| Security & Compliance | TBD | Threat model, isolation, encryption, audit | Yes |
| Data Architect | TBD | Data ownership, residency, retention, reporting models | Yes |
| Finance SME | TBD | Financial arithmetic, FX, variance semantics | Yes |
| Product Owner | TBD | Scope alignment and release boundaries | Yes |

### Revision History

| Version | Date | Author | Change |
|---------|------|--------|--------|
| 1.0.0 | 2026-11-05 | Architecture Team | Superseded access-control draft archived; system architecture and technology baseline established |

### Review & Sign-off Status

Draft for review. No section is approved until the approval matrix is complete. Decisions marked **Provisional** require confirmation before Phase 1 design freeze; see §19.

### Reading Guide

| Reader | Start here |
|--------|------------|
| Executive sponsor | §1, §4.1, §12.1, §15.3, §16 |
| Platform / backend engineer | §3, §5, §7, §8, §9, §11 |
| Frontend engineer | §3, §6.4, §7.4, §9 |
| Mobile engineer | §6.5, §9, §13.2 |
| Security reviewer | §2, §7.1–7.3, §13, Appendix C |
| Data / analytics engineer | §5.4, §7.5–7.7, §8, §11 |
| SRE / operations | §3.5, §11, §12, §14, §15 |
| Document owner (Docs 4–18) | §5, §7, §17, Appendix E |

### Conventions

| Convention | Definition |
|------------|------------|
| Requirement references | `FR-*`, `NFR-*`, `BR-*`, `D-###`, `CAP-###` resolve to Document 1 and Document 2 |
| Architecture decisions | `ADR-###` — §17; immutable once accepted, superseded by a new ADR |
| Technology risks | `TR-###` — §16 |
| Quality scenarios | `QA-###` — §2.3 |
| Principles | `AP-###` — §2.1 |
| Module | A code-level boundary inside the deployable application, owning its schema and public contract |
| Service | An independently deployable process with its own scaling and failure domain |
| "Shall" | Binding architectural requirement; "should" is a strong recommendation; "may" is optional |
| Money | `numeric(19,4)` in the storage currency; never floating point; formatted only at the presentation edge |
| Tenant | An organization (`org_id`) — the isolation boundary for all tenant-owned data |

---

## 1. Purpose, Scope & Architectural Drivers

### 1.1 Purpose

This document converts the business requirements in Document 1 and the domain model in Document 2 into a buildable system architecture. It defines:

- the architectural style and the reasoning behind it;
- the module and service decomposition, with owned data and dependency rules;
- the runtime, deployment, and technology baseline;
- the mechanisms that satisfy the non-functional requirements;
- the integration, offline-sync, security, observability, and delivery architecture;
- the decisions, risks, and open questions that constrain Documents 4–18.

It is the **binding technical baseline** for the remaining architecture set. Where a downstream document proposes a mechanism that conflicts with an accepted ADR, the conflict must be raised through the change process in Document 1 §18.2 rather than resolved locally.

### 1.2 Scope

| In scope | Out of scope (owned elsewhere) |
|----------|-------------------------------|
| System decomposition, module boundaries, dependency rules | Field-level API contracts and schemas — Document 6 |
| Runtime topology, deployment units, scaling model | Physical DDL, indexes, partition definitions — Document 5 |
| Technology selection with alternatives and rationale | Control catalog, ASVS mapping, DPIA — Document 7 |
| Data ownership, consistency model, residency approach | Detailed retention workflows — Documents 11, 14 |
| Offline sync protocol and conflict semantics | Mobile screen design and local UX — Documents 15, 16 |
| Cross-cutting mechanisms: tenancy, authZ enforcement, audit, events, caching | Infrastructure runbooks and on-call procedures — Document 17 |
| Performance, availability, and DR mechanisms | Delivery schedule, staffing, cost model — Document 18 |
| CI/CD, environments, release strategy | Commercial terms, pricing, contracts |

### 1.3 Position in the Document Set

| Direction | Document | What this document takes or gives |
|-----------|----------|-----------------------------------|
| Upstream | Doc 1 — Product Vision & Business Requirements | 216 FRs, 90 NFRs, 83 BRs, 22 decisions, release boundary |
| Upstream | Doc 2 — Domain Model & Business Capabilities | 11 bounded contexts, 25 capabilities, entities, value objects |
| Downstream | Doc 4 — Multi-Tenancy & Identity Architecture | Consumes §7.1–7.3, §13; owns identity detail, grant lifecycle, RLS policy specification |
| Downstream | Doc 5 — Data Architecture & Database Design | Consumes §8; owns schemas, indexes, partitions, migrations |
| Downstream | Doc 6 — API & Integration Architecture | Consumes §7.4, §10; owns contracts, error catalogue, webhook schemas |
| Downstream | Doc 7 — Security & Compliance Architecture | Consumes §13; owns threat model detail, control mapping, privacy controls |
| Downstream | Docs 8–13 | Consume module boundaries and event contracts |
| Downstream | Docs 14–17 | Consume audit, retention, mobile, frontend, and operations mechanisms |
| Downstream | Doc 18 — Implementation Roadmap | Consumes §4.5, §15, §16, §19 |

### 1.4 Architectural Drivers

| ID | Driver | Source | Architectural consequence |
|----|--------|--------|---------------------------|
| **AD-01** | A focused pilot MVP, not the full original scope | D-011, §13.2 | One deployable application, managed services, no speculative platform components |
| **AD-02** | Tenant isolation enforced at the data layer | D-018, NFR-SEC-004, NFR-SEC-005, TC-03 | PostgreSQL row-level security, per-transaction tenant context, continuous isolation tests |
| **AD-03** | External auditors access client data through cross-organization grants | D-003, FR-TNT-015–017, FR-AUD-007–009 | Grant-scoped authorization, session revocation, access-location logging |
| **AD-04** | Offline capture is a primary workflow, not a fallback | NFR-MOB-001–006, NFR-AVL-005 | Device-local store, deterministic sync protocol, conflict matrix, server-authoritative status |
| **AD-05** | Financial arithmetic must be decimal-exact | TC-02, NFR-DAT-003, BR-FIN-003 | `numeric` storage, decimal-only domain types, single rounding policy, FX snapshot per record |
| **AD-06** | Cost visibility is near-real-time | NFR-PERF-004, NFR-PERF-005, D-010 | Outbox-driven projectors, aggregate tables, 60 s / 5 min freshness tiers |
| **AD-07** | Excel is a first-class interface with validation parity | D-017, FR-XLS-001–017 | Server-side import engine sharing the domain validation service with the API |
| **AD-08** | Data residency is selectable per organization | D-013, NFR-PRV-009 | Independent regional stacks; no cross-region storage; region pinned per tenant |
| **AD-09** | Audit evidence must be tamper-evident and retained | D-020, NFR-AUD-001–005 | In-transaction audit writes, per-tenant hash chain, WORM export, 7-year retention |
| **AD-10** | Evidence volume grows faster than transaction volume | NFR-SCL-003, TC-04 | Object storage with lifecycle tiers, presigned access, quotas, asynchronous scanning |
| **AD-11** | A small team must operate the platform | BC-03, D-019, §17.4 | Modular monolith, managed data services, minimal infrastructure surface, strong automation |
| **AD-12** | Availability steps up from pilot to GA | NFR-AVL-001, NFR-AVL-002, NFR-AVL-006, NFR-AVL-007 | Multi-AZ from MVP; tested DR by GA; no architectural rewrite between releases |

### 1.5 Stakeholders

| Stakeholder | Concern | How this document addresses it |
|-------------|---------|-------------------------------|
| Executive sponsor | Predictable delivery and cost | Boring stack, managed services, explicit extraction triggers (§4.5) |
| Product Owner | MVP scope integrity | Release-aligned deployment topology and explicit deferrals (§3.5, §19) |
| Security reviewer | Isolation, encryption, audit | Trust boundaries, RLS, hash chain, STRIDE summary (§13) |
| Finance SME | Correct money and variance | Decimal policy, FX snapshot, single calculation service (§8.3, §7.5) |
| Audit SME | Evidence integrity and access traceability | Evidence pipeline, access logs, grant enforcement (§7.8, §9.4) |
| SRE | Operability and recovery | SLOs, failure matrix, DR design, runbook handoff (§11–§15) |
| Downstream architects | Stable contracts | Module ownership and event catalogue (§5, Appendix B) |

### 1.6 Architecture Requirements Language

This document uses **shall** for binding architecture requirements that downstream documents must satisfy. Each binding statement is traceable to a Document 1 requirement or decision. Where this document makes a **provisional** choice, it is labelled and listed in §19.

---

## 2. Architecture Principles & Quality Attributes

### 2.1 Principles

| ID | Principle | Rationale | Trade-off accepted |
|----|-----------|-----------|--------------------|
| **AP-01** | The tenant boundary is a data-layer invariant, not a convention | Application-layer filters fail silently; RLS fails closed | Slightly higher query complexity; connection-pool discipline required |
| **AP-02** | Modular monolith until measured pressure proves otherwise | One deployable minimizes operational load for a small team and keeps transactions simple | Coupling risk mitigated by schema-per-module and architecture tests |
| **AP-03** | One validation truth shared by UI, API, and Excel | Prevents the import path from bypassing controls (RSK-005) | Validation must be callable server-side; no client-only rules |
| **AP-04** | Money is decimal and converted at the edge | Binary floating point cannot represent currency exactly | Slightly slower arithmetic; strict type discipline |
| **AP-05** | Every material change emits an audit event in the same transaction | Audit gaps are unrecoverable; retrofitting is not credible | Write amplification on hot paths, mitigated by append-only partitioning |
| **AP-06** | Slow or unreliable work is asynchronous | User latency and third-party failures must not couple | Eventual consistency where explicitly documented |
| **AP-07** | Every message and job is idempotent | Retries are inevitable; duplicate effects are not | Idempotency keys and dedupe storage on every consumer |
| **AP-08** | Offline is a first-class state | Field capture happens where connectivity does not | Sync complexity, conflict UX, device security surface |
| **AP-09** | Configuration over code forks | One codebase for all tenants (NFR-MNT-004) | Configuration surface must be validated and versioned |
| **AP-10** | Prefer boring, managed technology | Operability is a feature; novelty is a cost | Less architectural novelty; vendor dependency managed by abstraction |
| **AP-11** | Observability is part of the feature definition | SLOs and audit evidence require telemetry from day one | Instrumentation effort on every module |
| **AP-12** | Security and privacy by construction | SOC 2 evidence must exist from MVP, not be retrofitted (RC-04) | Up-front design cost; fewer late surprises |

### 2.2 Inherited Constraints

| Constraint | Source | Architectural implication |
|------------|--------|---------------------------|
| Intermittent 2G/3G on location | TC-01 | Compact payloads, resumable uploads, incremental sync, no chatty APIs |
| Decimal-exact financial arithmetic | TC-02 | `numeric(19,4)`; decimal domain type; explicit rounding policy (§8.3) |
| Data-layer tenant isolation | TC-03 | RLS on every tenant table; `SET LOCAL` per transaction; no superuser app role |
| 100,000 evidence files per organization per year | TC-04 | Object storage, quotas, lifecycle tiers, asynchronous scanning |
| Excel files up to 50,000 rows | TC-05 | Streaming parser, staged validation, chunked commit, worker isolation |
| Append-only, verifiable audit | TC-06 | Insert-only role, per-tenant hash chain, WORM export, verification job |
| Auditor revocation within 5 minutes | TC-07 | Central session registry, short access tokens, revocation event, cache invalidation |
| Residency including backups | TC-08 | Region-scoped stacks; backups and keys never leave the region |
| GDPR and India DPDP | RC-01, RC-02 | Purpose limitation, DSR tooling, erasure with legal-hold precedence, region pinning |
| SOX-supporting controls | RC-03 | Segregation of duties, change traceability, access review, immutable audit |
| SOC 2 Type II within 12 months of GA | RC-04, NFR-PRV-004 | Controls instrumented from MVP; evidence retention and owner assignment |
| Customer audit rights and subprocessor notice | RC-05 | Subprocessor register, DPA metadata, auditable access logs |

### 2.3 Quality Attribute Scenarios

| ID | Source | Stimulus | Expected response | Measure | NFR |
|----|--------|----------|-------------------|---------|-----|
| **QA-01** | Internal auditor, on location | Submits an expense with two evidence photos while offline | Expense is persisted locally, queued, and confirmed to the user | Entry completes in ≤30 s median; zero data loss | NFR-UX-001, NFR-MOB-001 |
| **QA-02** | Mobile device | Reconnects after 3 days offline with 180 queued expenses | Queue drains incrementally without blocking capture; conflicts surfaced | ≥95% synced within 5 min; conflicts deterministic | NFR-MOB-002, NFR-MOB-003 |
| **QA-03** | Finance Head, month end | Opens a portfolio dashboard at 3× pilot volume | Cached aggregates serve the request; freshness label shown | P95 ≤2 s; aggregates ≤5 min old | NFR-PERF-001, NFR-PERF-005 |
| **QA-04** | External auditor with a grant for Show A | Requests Show B by manipulating an identifier | Request denied; denial audited; no data in response | 100% denial; zero leakage | NFR-SEC-004, NFR-SEC-005 |
| **QA-05** | Audit Manager | Revokes a grant during an active session | Subsequent requests fail within the revocation window | ≤5 minutes | TC-07, FR-AUD-009 |
| **QA-06** | Infrastructure | An availability zone fails during peak capture | Surviving zone serves traffic; no committed data lost | RTO ≤4 h for full region loss; no loss for AZ loss | NFR-AVL-004, NFR-AVL-006 |
| **QA-07** | Operations | Restores the primary database after corruption | Point-in-time restore to within 15 minutes of failure | RPO ≤15 min; restore tested quarterly | NFR-DAT-001, NFR-DAT-002 |
| **QA-08** | Accounts Executive | Imports a 50,000-row month-end workbook | Streaming validation; preview with row errors; no UI blocking | ≤10 s per 1,000 rows validated; commit idempotent | NFR-PERF-006, FR-XLS-006 |
| **QA-09** | Cost Controller | Generates a 100,000-row cost report | Asynchronous job; notification on completion; signed download | ≤30 s generation | NFR-PERF-007 |
| **QA-10** | Finance | FX source is unavailable at 06:00 | Latest published rate used and marked `FALLBACK` | Expense shows rate source; no silent default | BR-FIN-003, INTG-05 |
| **QA-11** | Insider with database write access | Alters a historical audit row | Chain verification detects the alteration; alert raised | Verification job fails; alert ≤5 min | NFR-SEC-012, NFR-AUD-002 |
| **QA-12** | Seasonal peak | Volume triples for 30 days | Horizontal scale absorbs load with stable latency | 3× baseline; targets unchanged | NFR-SCL-005 |

### 2.4 Trade-offs Accepted

| Trade-off | Benefit | Cost | Mitigation |
|-----------|---------|------|------------|
| Shared database with RLS (D-018) | SaaS economics, one migration path, low ops | Blast-radius and noisy-neighbour risk | RLS tests, per-tenant quotas, dedicated-deployment option at GA |
| Modular monolith (ADR-001) | Simple transactions, one deployment, fast delivery | Coupling, coarse scaling | Schema-per-module, architecture tests, extraction triggers |
| Aggregate tables instead of an OLAP engine | Lower cost and operational surface at pilot scale | Pre-computed freshness limits ad-hoc slicing | Defined refresh tiers; revisit at 3× GA volume |
| Server-side Excel parsing | Validation parity and security control | CPU and memory pressure in workers | Dedicated worker pool, streaming parser, file-size and row caps |
| Built-in identity at MVP (ADR-008) | No IdP dependency before GA | We own credential and MFA security | Argon2id, TOTP, rotation, penetration test; OIDC/SAML at Phase 1 |
| One region per tenant (ADR-009) | Simple residency guarantee | No cross-region failover for a tenant | Regional DR within the region; explicit RTO/RPO |
| SSE for in-app notifications | Simpler than WebSocket, works through proxies | One-way only; connection limits | Polling fallback; push at Phase 1 |
| Append-only audit in the same transaction | No audit gaps | Hot-path write amplification | Monthly partitions, asynchronous WORM export, separate tablespace |

---

## 3. Architecture Overview

### 3.1 System Context (C4 Level 1)

```
                         ┌──────────────────────────────────────────────┐
                         │                 SlateIQ Platform             │
                         │  Production cost capture, budget control,    │
                         │  audit evidence, and cost intelligence       │
                         └──────────────────────────────────────────────┘
        ┌──────────────┐          ▲            ▲            ▲          ┌──────────────┐
        │ Internal     │  mobile  │            │   web      │          │ Management   │
        │ Auditor      │──────────┘            │            └──────────│ Viewer       │
        │ (P-06)       │                       │                       │ (P-11)       │
        └──────────────┘                       │                       └──────────────┘
        ┌──────────────┐                       │                       ┌──────────────┐
        │ Finance Head │───────────────────────┤───────────────────────│ External     │
        │ Cost Control │        web            │        web            │ Auditor      │
        │ (P-04, P-05) │                       │                       │ (P-07)       │
        └──────────────┘                       │                       └──────────────┘
        ┌──────────────┐                       │                       ┌──────────────┐
        │ Org Admin    │───────────────────────┘───────────────────────│ Platform Ops │
        │ (P-12)       │        web                                    │ (P-13)       │
        └──────────────┘                                               └──────────────┘
                                               │
              ┌────────────────────────────────┼────────────────────────────────┐
              ▼                                ▼                                ▼
      ┌──────────────┐                 ┌──────────────┐                 ┌──────────────┐
      │ Email        │                 │ FX rate      │                 │ Customer IdP │
      │ provider     │                 │ source       │                 │ (Phase 1)    │
      │ (INTG-02)    │                 │ (INTG-05)    │                 │ (INTG-06)    │
      └──────────────┘                 └──────────────┘                 └──────────────┘
              ▲                                ▲                                ▲
              │                                │                                │
      ┌──────────────┐                 ┌──────────────┐                 ┌──────────────┐
      │ ERP / BI     │                 │ Object       │                 │ Malware      │
      │ (Phase 1)    │                 │ storage      │                 │ scanning     │
      │ (INTG-07/09) │                 │ (INTG-03)    │                 │ (INTG-04)    │
      └──────────────┘                 └──────────────┘                 └──────────────┘
```

**Boundaries.** The platform owns production budgets, captured cost, evidence, commitments, and audit history. It does not own the general ledger, payments, payroll, or the customer's identity master (Doc 1 §12.3).

### 3.2 Container View (C4 Level 2)

```
 ┌─────────────────────┐        ┌─────────────────────┐        ┌─────────────────────┐
 │ Web SPA             │        │ Mobile App          │        │ Customer systems    │
 │ React + TypeScript  │        │ React Native + SQLC │        │ ERP / BI / IdP      │
 │ served from CDN     │        │ offline-first       │        │ (Phase 1)           │
 └──────────┬──────────┘        └──────────┬──────────┘        └──────────┬──────────┘
            │ HTTPS                        │ HTTPS                        │ HTTPS
            ▼                              ▼                              ▼
 ┌───────────────────────────────────────────────────────────────────────────────────┐
 │  Edge: CDN + WAF + TLS termination + rate limiting                                │
 └───────────────────────────────────┬───────────────────────────────────────────────┘
                                     ▼
 ┌───────────────────────────────────────────────────────────────────────────────────┐
 │  API Host (ASP.NET Core, stateless, N replicas)                                   │
 │  REST /api/v1 · SSE /api/v1/stream · signed-URL issuance · authorization PEP       │
 └───────┬───────────────────┬───────────────────┬───────────────────┬───────────────┘
         │                   │                   │                   │
         ▼                   ▼                   ▼                   ▼
 ┌───────────────┐   ┌───────────────┐   ┌───────────────┐   ┌───────────────────┐
 │ PostgreSQL 17 │   │ Redis 7.4     │   │ Object store  │   │ Identity provider │
 │ primary +     │   │ cache, rate   │   │ S3-compatible │   │ built-in MVP      │
 │ read replica  │   │ limits, locks │   │ + Object Lock │   │ OIDC/SAML Phase 1 │
 │ RLS enforced  │   │               │   │               │   │                   │
 └───────┬───────┘   └───────────────┘   └───────┬───────┘   └───────────────────┘
         │                                       │
         │ outbox / job tables                   │ presigned PUT/GET
         ▼                                       ▼
 ┌───────────────────────────────────────────────────────────────────────────────────┐
 │  Worker Host (same codebase, different host configuration, M replicas)            │
 │  outbox dispatch · imports · exports · reports · notifications · FX · retention    │
 │  · aggregation projectors · scan orchestration · digest scheduling                 │
 └───────────────────────────────┬───────────────────────────────────────────────────┘
                                 ▼
 ┌───────────────────────────────────────────────────────────────────────────────────┐
 │  Report Renderer (headless Chromium, isolated)  →  PDF/XLSX to object storage      │
 └───────────────────────────────────────────────────────────────────────────────────┘
                                 │
                                 ▼
 ┌───────────────────────────────────────────────────────────────────────────────────┐
 │  Telemetry: OpenTelemetry SDK → Collector → metrics / logs / traces (regional)     │
 └───────────────────────────────────────────────────────────────────────────────────┘
```

**Deployment note.** API, Sync endpoints, and Worker are **one codebase** with three host configurations. Sync is hosted separately so that a backlog of large evidence uploads cannot starve interactive API traffic. The report renderer is isolated because headless Chromium has a different memory and security profile.

### 3.3 Container Responsibilities

| Container | Technology | Responsibility | Scaling unit | Durable state |
|-----------|------------|----------------|--------------|---------------|
| Web SPA | React 19, TypeScript, Vite | Role-scoped UI, forms, dashboards, admin | CDN edge | None (token in memory; refresh in secure storage) |
| Mobile App | React Native + Expo, SQLCipher | Offline capture, evidence, queue, sync | Device | Encrypted local SQLite + file store |
| API Host | ASP.NET Core 10 | Synchronous commands/queries, authZ, SSE, signed URLs | Horizontal, stateless | None |
| Sync Host | ASP.NET Core 10 (sync module) | Batch push/pull, conflict responses, resumable upload tickets | Horizontal, stateless | None (cursor state in DB) |
| Worker Host | .NET 10 Worker Service | Outbox consumers, jobs, projections, digests, retention | Horizontal by queue partition | Job/outbox tables |
| Report Renderer | Chromium + HTML templates | PDF rendering; XLSX assembly offloaded to worker | Horizontal, memory-bound | None |
| PostgreSQL | PostgreSQL 17 | System of record, RLS, outbox, audit, read models | Vertical + read replica | All relational data |
| Redis | Redis 7.4 | Cache, rate limits, distributed locks, revocation set | Horizontal with replica | Ephemeral |
| Object storage | S3-compatible, SSE-KMS, Object Lock | Evidence, exports, audit WORM copies | Managed | Files |
| Telemetry | OTel Collector + regional backends | Metrics, logs, traces, SLO burn | Managed | Retention per policy |

### 3.4 Key Runtime Flows

#### 3.4.1 Online expense submission

1. SPA or mobile sends `POST /api/v1/expenses` with `Idempotency-Key`.
2. Edge terminates TLS, applies WAF and rate limits, forwards to API.
3. API resolves the session, builds `TenantContext`, and opens a transaction with `SET LOCAL app.org_id` and `app.user_id`.
4. Authorization evaluates role, scope, grant, period lock, and segregation-of-duties rules.
5. Domain service validates lines, category rules, evidence requirements, FX snapshot, and budget availability.
6. Expense, lines, evidence links, audit event, and outbox event commit atomically.
7. API returns `201` with the expense resource and `ETag`; the outbox dispatcher later projects the cost fact.
8. Dashboards reflect the expense within 60 seconds (NFR-PERF-004).

#### 3.4.2 Offline capture and sync

1. Mobile writes the expense and evidence metadata to encrypted local SQLite with a client-generated UUIDv7.
2. Evidence files are compressed and stored in the app sandbox with a content hash.
3. On connectivity, the sync engine calls `POST /api/v1/sync/push` with a batch (default 25 mutations) and a device-scoped idempotency key.
4. Server applies mutations in order, records per-item results (`APPLIED`, `CONFLICT`, `REJECTED`, `RETRY`), and returns the authoritative versions.
5. Evidence upload uses a presigned PUT with a server-issued ticket; completion confirms hash and scan status.
6. Client then calls `GET /api/v1/sync/pull?cursor=` for changes to reference data and its own records.
7. Conflicts are resolved per §9.3; the user sees only conflicts requiring a decision.

#### 3.4.3 Dashboard read path

1. SPA requests a dashboard projection (`/api/v1/dashboards/{id}`) with scope filters.
2. API authorizes scope and reads from `intel` aggregate tables, never from raw expense tables.
3. Response includes `as_of` and `freshness_tier`; the UI labels stale data.
4. Aggregates are maintained by outbox-driven projectors: show-level ≤60 s, cross-show ≤5 min.

#### 3.4.4 Report generation

1. User requests a report (RPT-01–18) with filters.
2. API validates scope and inserts an `export_job` row, returning `202` with a job ID.
3. Worker claims the job, streams rows in keyset-paginated batches, writes XLSX or HTML, and renders PDF if required.
4. Output is stored in object storage under the tenant prefix; a signed URL with ≤15-minute TTL is issued.
5. Completion is delivered in-app and by email; job status is queryable.

#### 3.4.5 External auditor access through a grant

1. External auditor authenticates to their home organization and receives a token scoped to their home tenant.
2. A client-initiated `CrossOrganizationAccessGrant` names the auditor, client org, shows/departments, permission level, and validity window.
3. The API resolves the grant into an `EffectiveScope` with `grant_id`, client `org_id`, and allowed show/department IDs.
4. RLS admits rows only when `org_id` matches the tenant context or a valid grant scope; application filters enforce show/department/category limits.
5. Every grant-scoped request writes an access event with grant ID, client org, resource, and source location.
6. Revocation invalidates the grant, publishes a revocation event, evicts cached scopes, and blocks new requests within 5 minutes.

#### 3.4.6 Daily digest

1. Quartz triggers per organization timezone at 07:00 local (D-010).
2. Worker computes the role-scoped digest from aggregates, renders the template, and enqueues deliveries.
3. Email provider accepts the message; in-app copy is written to the notification table.
4. Failures retry with backoff; the digest is marked `DEGRADED` if aggregates are stale beyond the freshness tier.

### 3.5 Deployment Topology by Release

| Concern | MVP (Pilot) | Phase 1 (GA) | Phase 2 |
|---------|-------------|--------------|---------|
| Regions | One region per pilot tenant (India or EU) | India + EU, tenant-pinned | Additional regions on demand |
| Compute | Managed containers (ECS Fargate reference) | Same, plus HPA and dedicated worker pools | Kubernetes if extraction triggers are met |
| Database | PostgreSQL Multi-AZ, one primary + one read replica | Larger instance class, PITR, read replicas per workload | Partitioned archival, possible dedicated clusters |
| Cache | Single Redis with replica | Redis cluster mode | Same |
| Object storage | S3 with SSE-KMS, versioning, lifecycle | + Object Lock for audit WORM | + cross-account replication within region |
| DR | Multi-AZ; restore tested before go-live | Regional DR runbook and quarterly exercise | Automated regional rebuild |
| Observability | OTel + managed backends | SLO dashboards and burn alerts | Cross-region operational view (metadata only) |
| Environments | dev, test, staging, prod | + regional staging, ephemeral preview | + performance environment |

---

## 4. Architectural Style & Decisions

### 4.1 Options Considered

Scoring: 1 = poor, 5 = strong. Weights reflect pilot constraints (AD-11 dominates).

| Criterion | Weight | Layered monolith | Modular monolith | Microservices | Serverless functions |
|-----------|:------:|:----------------:|:----------------:|:-------------:|:--------------------:|
| Time to pilot | 25% | 4 | 5 | 2 | 3 |
| Operational simplicity | 25% | 4 | 5 | 2 | 4 |
| Tenant isolation and audit control | 20% | 3 | 5 | 3 | 3 |
| Evolvability at 3× GA | 15% | 3 | 4 | 5 | 3 |
| Cost efficiency at pilot scale | 10% | 4 | 4 | 2 | 5 |
| Team cognitive load | 5% | 4 | 4 | 1 | 3 |
| **Weighted score** | **100%** | **3.65** | **4.75** | **2.55** | **3.60** |

**Rejected options.**

- **Layered monolith** — no enforced boundaries; the audit and reporting concerns would bleed into transaction paths.
- **Microservices** — 25 capabilities across a small team would create distributed transactions, duplicated authorization, and an operational burden that contradicts AD-11.
- **Serverless functions** — good for spiky edges, poor for long-running imports, PDF rendering, and connection-heavy RLS transactions; cold starts harm the 30-second capture target.

### 4.2 Selected Style

**Modular monolith with asynchronous workers and an isolated sync host.**

| Element | Definition |
|---------|------------|
| Single application image | One build artefact, three host configurations (API, Sync, Worker) |
| Schema per module | Each module owns a PostgreSQL schema; cross-schema reads are prohibited |
| In-process contracts | Module public interfaces only; internal types are not referenced across modules |
| Asynchronous integration | Outbox events for cross-module side effects; no distributed transactions |
| Independent edges | Sync and report rendering scale and fail independently of interactive API |
| Extraction-ready | A module can be moved to its own service when §4.5 triggers are met, without changing its public contract |

### 4.3 Module Dependency Rules

```
                         ┌──────────────┐
                         │   Tenancy    │  (org, user, role, grant, session)
                         └──────┬───────┘
        ┌───────────────┬───────┼────────┬───────────────┬──────────────┐
        ▼               ▼       ▼        ▼               ▼              ▼
  ┌───────────┐   ┌──────────┐ ┌──────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
  │ Production│   │  Budget  │ │Vendor│ │  Audit   │ │ Expense  │ │  Docs    │
  └─────┬─────┘   └────┬─────┘ └──┬───┘ └────┬─────┘ └────┬─────┘ └────┬─────┘
        │              │          │          │            │            │
        └──────────────┴──────────┴──────────┴────────────┴────────────┘
                                      │
                    ┌─────────────────┼──────────────────┐
                    ▼                 ▼                  ▼
             ┌────────────┐   ┌────────────┐    ┌──────────────┐
             │    Intel   │   │    XLS     │    │   Notify     │
             └────────────┘   └────────────┘    └──────────────┘
                    ▲
             ┌──────┴───────┐
             │  Compliance  │  (audit store, retention, DSR, holds)
             └──────────────┘
```

| Rule | Detail | Enforcement |
|------|--------|-------------|
| R1 | Dependencies point downward or to Tenancy; never upward | Architecture tests (NetArchTest) in CI |
| R2 | No cycles between modules | Build fails on cycle |
| R3 | A module exposes only its `Contracts` assembly/namespace | Internal types marked `internal`; public API reviewed |
| R4 | Cross-module data access goes through a contract or a read model, never a foreign schema query | SQL lint + code review |
| R5 | Cross-module side effects use the outbox, not synchronous calls | Event catalogue review |
| R6 | Shared kernel is limited to money, identifiers, time, and results | No domain entities in the shared kernel |
| R7 | Compliance audit writes are a platform capability invoked in-transaction | Single audit writer; no direct table access |

### 4.4 Transaction and Consistency Model

| Operation | Transaction scope | Consistency | Rationale |
|-----------|-------------------|-------------|-----------|
| Expense submit | Expense + lines + audit + outbox | Immediate within module; projections eventual | Atomic capture with no audit gap |
| Budget approval | Budget + version + audit + outbox | Immediate | Financial control point |
| Grant creation/revocation | Grant + audit + outbox + revocation marker | Immediate; cached scopes evicted | TC-07 requires ≤5-minute revocation |
| Excel import commit | Staging batch + chunk of records + audit | Per-chunk atomic; batch resumable | 50,000 rows cannot be one transaction |
| Dashboard projection | Projector checkpoint + aggregate upsert | Idempotent, at-least-once | Replay-safe |
| Notification delivery | Notification row + delivery attempt | At-least-once | Duplicate delivery acceptable; loss is not |
| Report generation | Job row → object → job completion | Idempotent by job ID | Retry-safe |
| Audit WORM export | Batch read → object write → checkpoint | At-least-once, hash-verified | Tamper evidence |

**Event delivery guarantee.** The outbox dispatcher provides **at-least-once** delivery. Every consumer must deduplicate on `event_id` and be safe to replay. Ordering is guaranteed per aggregate ID, not globally.

### 4.5 Service Extraction Triggers

| Candidate | Trigger | First step | Contract preserved |
|-----------|---------|------------|--------------------|
| Sync host | >40% of API CPU or >200 req/s sustained | Already separate host; promote to separate deployment | Sync API v1 |
| Import/export | >30% of worker time or p95 queue wait >10 min | Dedicated worker pool with separate queue | Job contract |
| Report rendering | >25% of worker memory or OOM events | Separate renderer pool (already isolated) | Job contract |
| Notifications | >100,000 deliveries/day or provider rate limits | Dedicated dispatcher service | Event contract |
| Search | Postgres FTS p95 >1 s at 10M documents | OpenSearch with CDC projector | Search API |
| Cost intelligence | Aggregate refresh >5 min at 3× GA | Columnar store (ClickHouse) fed by outbox | Metric contract |
| Any module | Team >25 engineers or independent release cadence required | Extract module with its schema | Module public contract |

### 4.6 Evolution Path

| Stage | Architecture | Exit criteria |
|-------|--------------|---------------|
| MVP | Single region, modular monolith, managed containers | Pilot gates G1–G12 |
| GA | Two regions, read replicas, SLOs, regional DR | GA checklist §17.4 of Doc 1 |
| Scale | Extracted sync/import/reporting, Redis cluster, partitioned archival | Sustained 3× GA volume |
| Expansion | Optional analytics store, additional regions, dedicated deployment tier | Business case per capability |

---

## 5. Module & Service Decomposition

### 5.1 Module Catalogue

The application is organized into **twelve domain modules** and **three platform modules**. Each module owns a PostgreSQL schema, a set of aggregates, and a public contract. Module names are stable; physical deployment may change per §4.5.

| Module | Schema | Capabilities | Owns (aggregates) | Publishes | Consumes | Release |
|--------|--------|--------------|-------------------|-----------|----------|---------|
| **MOD-01 Tenancy & Access** | `tenancy` | CAP-001, CAP-003 | Organization, Membership, Role, Permission, RoleAssignment, CrossOrganizationAccessGrant, AccessReview | OrganizationProvisioned, UserInvited, UserDeactivated, RoleChanged, GrantCreated, GrantRevoked | — | MVP |
| **MOD-02 Identity** | `identity` | CAP-002 | Credential, MfaFactor, RefreshSession, DeviceRegistration, PasswordReset | UserAuthenticated, MfaEnrolled, SessionRevoked | UserDeactivated, GrantRevoked | MVP |
| **MOD-03 Production** | `prod` | CAP-004, CAP-005 | Show, Season, Episode, Department, Category, Party, PartyOrganizationLink | ShowCreated, ShowStatusChanged, PartyLinked, CategoryChanged | OrganizationSuspended | MVP |
| **MOD-04 Budget** | `budget` | CAP-006, CAP-007 | Budget, BudgetVersion, BudgetLine, Contingency, EpisodeAllocation, ApprovalWorkflow, ApprovalStep, ApprovalDelegation, FinancialPeriod | BudgetSubmitted, BudgetApproved, BudgetRevised, PeriodLocked, PeriodReopened, DelegationChanged | ShowCreated, ShowStatusChanged | MVP |
| **MOD-05 Vendor & Commitments** | `vendor` | CAP-008, CAP-009, CAP-010 | Vendor, VendorDocument, ManualCommitment, PurchaseOrder, PurchaseOrderLine, PaymentReference | VendorBlocked, CommitmentCreated, CommitmentAmended, CommitmentClosed, PurchaseOrderChanged | ShowCreated | MVP (PO Phase 1) |
| **MOD-06 Audit Operations** | `auditops` | CAP-011 | AuditTeam, AuditAssignment, AuditScope, AuditorProfile, CoverageRequirement | AuditorAssigned, AssignmentRevoked, CoverageUpdated | GrantRevoked, ShowStatusChanged | MVP |
| **MOD-07 Expense** | `expense` | CAP-012, CAP-013 | Expense, ExpenseLine, ExpenseSplit, EvidenceLink, ExpenseApproval, ExpenseAmendment, Exception, FxSnapshot | ExpenseSubmitted, ExpenseApproved, ExpenseRejected, ExpenseAmended, ExpenseCancelled, ExceptionRaised | PeriodLocked, CategoryChanged, GrantRevoked | MVP |
| **MOD-08 Cost Intelligence** | `intel` | CAP-015, CAP-016, CAP-017, CAP-018, CAP-019 | CostFact, CommitmentFact, VarianceSnapshot, AlertRule, AlertInstance, DashboardDefinition, BenchmarkAggregate, Forecast | AlertTriggered, VarianceBreached, ProjectionCheckpoint | Expense*, Commitment*, Budget*, CategoryChanged | MVP (forecast/benchmark later) |
| **MOD-09 Documents** | `docs` | CAP-014, CAP-024 | Document, DocumentVersion, DocumentAccessLog, ScanResult, UploadTicket, RetentionPolicy | DocumentUploaded, DocumentScanned, DocumentQuarantined | LegalHoldApplied, LegalHoldReleased | MVP |
| **MOD-10 Excel** | `xls` | CAP-020, CAP-021 | ExcelTemplate, TemplateVersion, ImportBatch, ImportRow, ExportJob | ImportCommitted, ImportRejected, ExportCompleted | — | MVP |
| **MOD-11 Notifications** | `notify` | CAP-025 | NotificationTemplate, Notification, NotificationPreference, DeliveryAttempt, EscalationPolicy, DigestRun | NotificationDelivered, NotificationFailed | Domain events (all) | MVP (email + in-app) |
| **MOD-12 Compliance** | `compliance` | CAP-022, CAP-023, CAP-024 | AuditEvent, AuditChainHead, SecurityEvent, LegalHold, DsrRequest, RopaEntry, BreachIncident, ControlEvidence | LegalHoldApplied, LegalHoldReleased, DsrCompleted, BreachDeclared | Domain events (audit via in-transaction writer) | MVP |
| **MOD-13 Reporting** | `report` | CAP-019, CAP-021 | ReportDefinition, ReportRun, ReportOutput | ReportCompleted, ReportFailed | Expense*, Budget*, Commitment* | MVP |
| **MOD-14 Sync** | `sync` | CAP-012 (support) | DeviceRegistry, SyncCursor, MutationLog, ConflictRecord | SyncConflictDetected, DeviceWiped | GrantRevoked, UserDeactivated | MVP |
| **MOD-15 Platform Operations** | `ops` | Cross-cutting | OutboxEvent, InboxDedup, Job, JobAttempt, IdempotencyKey, FeatureFlag, TenantRegion, ProvisioningRun | JobFailed, TenantProvisioned | — | MVP |

### 5.2 Capability Coverage

| Capability | Module | Capability | Module |
|------------|--------|------------|--------|
| CAP-001 | MOD-01 | CAP-014 | MOD-09 |
| CAP-002 | MOD-02 | CAP-015 | MOD-08 |
| CAP-003 | MOD-01 | CAP-016 | MOD-08 |
| CAP-004 | MOD-03 | CAP-017 | MOD-08 |
| CAP-005 | MOD-03 | CAP-018 | MOD-08 |
| CAP-006 | MOD-04 | CAP-019 | MOD-08, MOD-13 |
| CAP-007 | MOD-04 | CAP-020 | MOD-10 |
| CAP-008 | MOD-05 | CAP-021 | MOD-10, MOD-13 |
| CAP-009 | MOD-05 | CAP-022 | MOD-12 |
| CAP-010 | MOD-05 | CAP-023 | MOD-12 |
| CAP-011 | MOD-06 | CAP-024 | MOD-09, MOD-12 |
| CAP-012 | MOD-07, MOD-14 | CAP-025 | MOD-11 |
| CAP-013 | MOD-07 | — | — |

All 25 capabilities are assigned to exactly one primary module; CAP-012, CAP-019, CAP-021, and CAP-024 have a supporting module noted.

### 5.3 Module Invariants

| Module | Invariant | Consequence if violated |
|--------|-----------|-------------------------|
| Tenancy | Every request has exactly one tenant context; a grant scope never widens the tenant boundary beyond its named shows/departments | Cross-tenant exposure (RSK-003) |
| Identity | Refresh tokens rotate on every use; reuse revokes the whole session family | Session hijack persists |
| Production | A show belongs to exactly one organization; a party link requires an accepted sharing agreement | Cross-tenant party leakage (D2-02) |
| Budget | One active approved version per budget scope; episode allocations cannot exceed the season ceiling | Conflicting approvals, variance ambiguity (D-005) |
| Vendor | A manual commitment has one owner, one currency, and a lifecycle that cannot skip closure approval | Phantom or duplicated commitments |
| Audit Ops | An assignment is time-bound and cannot exceed the grant that authorizes it | Standing access (D-021) |
| Expense | Header total equals the sum of rounded lines; FX snapshot is immutable after submission | Financial misstatement (BR-FIN-005) |
| Cost Intelligence | Every fact traces to a source record and a projection checkpoint | Unreconcilable dashboards (RSK-007) |
| Documents | A document is never served before a clean scan; signed URLs never exceed 15 minutes | Malware exposure, link leakage |
| Excel | Import commit uses the same domain validation service as the API | Control bypass (RSK-005) |
| Notifications | Every delivery attempt is recorded; user preferences are evaluated before send | Notification fatigue, audit gap |
| Compliance | Audit chain is append-only and verified daily; legal hold overrides deletion | Unlawful deletion, inadmissible evidence |
| Reporting | A report run is reproducible from its parameters and data snapshot | Disputed figures |
| Sync | Server timestamps and versions are authoritative; client retries are idempotent | Duplicate or lost expenses (RSK-004) |
| Platform Ops | Every job and event has an idempotency key and a terminal state | Retry storms, stuck queues |

### 5.4 Data Ownership Rules

| Rule | Detail |
|------|--------|
| **Ownership** | A module is the only writer of its schema. Other modules read through contracts or derived read models. |
| **References** | Cross-module references use immutable IDs. Foreign keys exist only inside a module schema; cross-module integrity is validated on write and reconciled nightly. |
| **Tenant column** | Every tenant-owned table carries `org_id uuid not null` as the first column of its primary isolation index and is covered by an RLS policy. |
| **Global tables** | Only `ops.tenant_region`, `tenancy.organization`, and reference taxonomy tables may be global; they contain no personal data beyond the organization record. |
| **Derived data** | Everything in `intel` and `report` is rebuildable from source modules plus the event log. |
| **Deletion** | Hard deletion is permitted only through the compliance erasure workflow and only when no legal hold or retention obligation applies. |
| **Schema changes** | Expand/contract migrations only; a dropped column requires two releases and a documented backfill. |

### 5.5 Module Release Mapping

| Release | Modules and scope |
|---------|-------------------|
| **MVP** | MOD-01 through MOD-15 at pilot scope: manual commitments, in-app/email notifications, variance and alerts, Excel import/export, audit and legal hold, sync |
| **Phase 1** | PO lifecycle, OIDC/SAML + SCIM, push notifications, ERP posting, BI extract, forecast methods, OpenSearch (if triggered), regional DR |
| **Phase 2** | Benchmarking at scale, SMS, HRIS, advanced forecasting, additional regions, dedicated deployment tier |

---

## 6. Technology Stack

### 6.1 Selection Criteria

| Criterion | Weight | What "good" means |
|-----------|:------:|-------------------|
| Financial correctness | 20% | Native decimal, strong typing, deterministic rounding |
| Operability by a small team | 20% | Managed services, few moving parts, mature tooling |
| Security and isolation | 15% | RLS support, mature auth libraries, supply-chain tooling |
| Performance at target load | 15% | Meets NFR-PERF with headroom at 3× |
| Ecosystem for Excel, PDF, mobile | 10% | First-class libraries without custom plumbing |
| Hiring and maintainability | 10% | Mainstream skills, long-term support |
| Residency and portability | 10% | Runs in India and EU regions; no mandatory US-only dependency |

### 6.2 Stack Summary

| Layer | Choice | Version policy | Why | Alternatives rejected |
|-------|--------|----------------|-----|-----------------------|
| Backend runtime | .NET 10 LTS | Pinned LTS; upgrade within 6 months of next LTS | Native `decimal`, strong typing, mature async and background hosting | Node.js (decimal and CPU-heavy work), Java (heavier footprint for this team) |
| API framework | ASP.NET Core Minimal APIs | Tracks runtime | Low ceremony, endpoint filters, OpenAPI | MVC (more ceremony), FastEndpoints (extra dependency) |
| Data access | EF Core for writes; Dapper for read models | Tracks runtime | Rich mapping plus explicit SQL for reporting | EF Core only (poor for heavy reads) |
| Database | PostgreSQL 17 | Managed service version | RLS, partitioning, JSONB, `numeric`, mature PITR | MySQL (weaker RLS), SQL Server (cost, residency), MongoDB (financial integrity) |
| Cache | Redis 7.4 | Managed service | Rate limits, reference cache, locks, revocation set | In-memory only (no cross-replica state) |
| Queue and jobs | PostgreSQL outbox + `SKIP LOCKED`; Quartz.NET scheduler | In-house library | No broker to operate at pilot volume; transactional consistency | RabbitMQ/Kafka (operational cost), SQS-only (local dev and portability) |
| Object storage | S3-compatible with SSE-KMS and Object Lock | Managed | Presigned URLs, lifecycle, WORM audit copies | Database BLOBs (cost, backup size), NFS (no durability) |
| Search | PostgreSQL FTS + `pg_trgm`; OpenSearch if triggered | — | Meets NFR-PERF-008 at pilot volume | Elasticsearch (ops), dedicated search SaaS (residency) |
| Web framework | React 19 + TypeScript 5 | Current stable, pinned | Ecosystem, accessibility libraries, hiring | Angular (heavier), Vue (smaller enterprise ecosystem) |
| Web build | Vite | Current stable | Fast builds, simple static output | Next.js (SSR not needed for an authenticated SPA) |
| Server state | TanStack Query | Current stable | Caching, retries, optimistic updates | Redux-only (boilerplate), SWR (fewer features) |
| Routing | TanStack Router | Current stable | Type-safe routes and search params | React Router (weaker type safety) |
| Forms and validation | React Hook Form + Zod | Current stable | Performance, schema reuse, clear errors | Formik (slower), Yup (weaker TS inference) |
| UI system | Tailwind CSS 4 + Radix primitives | Current stable | Accessible primitives, design tokens, no lock-in | MUI (opinionated theming), Bootstrap (dated) |
| Charts | Recharts for standard; Visx for bespoke | Current stable | Accessible, composable, no canvas dependency | Chart.js (less React-native), D3-only (effort) |
| Mobile | React Native + Expo SDK | Pinned; upgrade quarterly | Shared TypeScript, OTA updates, one mobile team | Flutter (second language), native dual build (cost) |
| Mobile storage | SQLite via op-sqlite with SQLCipher | Pinned | Encrypted local store at rest | AsyncStorage (no queries), Realm (licensing and migration risk) |
| Mobile secure storage | iOS Keychain / Android Keystore | Platform | Keys never in app sandbox | EncryptedSharedPreferences only (weaker) |
| PDF rendering | Headless Chromium via Playwright .NET | Pinned, isolated container | HTML/CSS templates, deterministic output | QuestPDF (layout control but more code), wkhtmltopdf (unmaintained) |
| XLSX | ExcelDataReader (import), OpenXML SDK (export) | Pinned | Streaming import, precise export, no Office dependency | ClosedXML (memory-heavy at 50k rows), commercial libraries (cost) |
| Email | Provider-agnostic adapter (SES reference) | — | Residency and failover options | Hard-coded provider |
| FX rates | ECB/OER daily feed + manual override | — | Matches D-007 | Paid market feed (cost) |
| Secrets | Cloud secret manager with KMS | — | Rotation, audit, no secrets in code | Vault (extra ops at pilot) |
| IaC | Terraform | Current stable | Multi-region, reviewable plans | ClickOps (not auditable), CDK (language lock-in) |
| CI/CD | GitHub Actions | — | Ecosystem, OIDC to cloud, required checks | Jenkins (ops), GitLab CI (not the chosen SCM) |
| Containers | OCI images on ECS Fargate; EKS if triggered | — | Low ops at pilot; Kubernetes only when justified | Kubernetes from day one (ops burden) |
| Telemetry | OpenTelemetry SDK + Collector | — | Vendor-neutral, traces/metrics/logs | Proprietary agents (lock-in) |
| Error tracking | Sentry-compatible, region-hosted | — | Stack traces with residency control | US-only SaaS (residency conflict) |
| Feature flags | Database-backed flags in `ops` | — | No external dependency, auditable changes | SaaS flags (cost, residency) |
| Unit/integration tests | xUnit, Testcontainers, Verify | — | Real PostgreSQL in tests, snapshot safety | In-memory DB (RLS untested) |
| End-to-end tests | Playwright | — | Browser and API coverage, trace viewer | Cypress (weaker multi-tab/API) |
| Load tests | k6 | — | Scriptable, CI-friendly thresholds | JMeter (heavy) |
| SAST/SCA | CodeQL, Dependabot/Renovate, Trivy, Gitleaks | — | Layered supply-chain controls | Single scanner (coverage gaps) |
| Signing/SBOM | cosign + Syft | — | Provenance and inventory for SOC 2 | Unsigned images |

### 6.3 Backend Platform

| Aspect | Decision |
|--------|----------|
| Module structure | Vertical slices per module: `Api`, `Application`, `Domain`, `Infrastructure`, `Contracts` |
| Endpoint style | Minimal APIs grouped per module; endpoint filters for authentication, authorization, idempotency, and audit |
| Validation | FluentValidation at the edge; domain invariants inside aggregates; shared validator invoked by Excel |
| Error model | RFC 9457 `ProblemDetails` with stable `code`, `traceId`, and field-level errors |
| Serialization | `System.Text.Json`; explicit contracts; no domain entities on the wire |
| Resilience | Polly timeouts, retries with jitter, circuit breakers for email, FX, and storage |
| Background hosting | `IHostedService` consumers with graceful drain; Quartz.NET for cron |
| Configuration | Strongly typed options, validated at startup; per-tenant settings in database |
| Logging | Structured logs via Serilog → OpenTelemetry; no PII or financial payloads |
| API docs | OpenAPI generated at build; client SDK generated for the SPA |
| Money type | `readonly record struct Money(decimal Amount, string Currency)` with explicit operations |
| Time | `DateTimeOffset` in UTC at rest; IANA timezone per organization for display and digests |
| IDs | UUIDv7 for sortable, client-generatable identifiers; ULID for audit events |

### 6.4 Web Frontend

| Aspect | Decision |
|--------|----------|
| Rendering | Client-side SPA; no SSR requirement for authenticated workflows |
| Code splitting | Route-level lazy loading; dashboard and admin bundles separated |
| API access | Generated TypeScript client from OpenAPI; TanStack Query for cache and retries |
| Auth tokens | Access token in memory; refresh token in `HttpOnly`, `Secure`, `SameSite=Lax` cookie; CSRF double-submit |
| Authorization UX | Capability-driven rendering; server remains authoritative; no sensitive data in hidden DOM |
| Forms | React Hook Form + Zod; server errors mapped to fields; optimistic updates only for reversible actions |
| Tables | Virtualized rows for large result sets; server-side pagination and sorting |
| Accessibility | WCAG 2.1 AA (NFR-UX-002); focus management; keyboard paths; axe checks in CI |
| i18n | i18next with locale files; no hard-coded strings (NFR-L10N-001) |
| Security | Strict CSP, Trusted Types where supported, no `dangerouslySetInnerHTML` for user content |
| Offline web | Read-only cached shell; capture is mobile-only in MVP |

### 6.5 Mobile Application

| Aspect | Decision |
|--------|----------|
| Framework | React Native + Expo, development build (not Expo Go) for SQLCipher and native modules |
| Local database | SQLite with SQLCipher; schema versioned; migrations tested on upgrade paths |
| Local files | App sandbox with AES-GCM envelope encryption; keys in Keychain/Keystore |
| Sync engine | Custom push/pull protocol (§9); background task with OS limits respected |
| Capture | Camera with compression to ≤2 MB; EXIF stripped except orientation and timestamp |
| Auth | Short-lived access token; refresh token in secure storage; biometric or passcode gate (NFR-MOB-004) |
| Offline limits | 7 days, 500 expenses, 500 MB evidence (D-015); warnings at 80% |
| Distribution | App Store / Play Store; MDM guidance for enterprise fleets; OTA JS updates only for non-native changes |
| Telemetry | Crash and performance events without financial payloads; device ID hashed |
| Remote wipe | Server-issued wipe command on deactivation; local data erased on next launch |

### 6.6 Data and Storage

| Concern | Decision |
|---------|----------|
| Primary database | PostgreSQL 17, Multi-AZ, one primary per region |
| Read scaling | One read replica at MVP; separate replicas for reporting at GA |
| Connection pooling | PgBouncer in transaction mode; `SET LOCAL` tenant context per transaction |
| RLS | Enabled and forced on every tenant table; application role is not table owner and has no `BYPASSRLS` |
| Partitioning | Monthly range partitions for `expense`, `compliance.audit_event`, `notify.notification`, `ops.outbox_event` |
| Object storage | Bucket per region; tenant prefix; SSE-KMS; versioning; lifecycle to infrequent access after 90 days |
| WORM | Audit anchors and monthly audit archives in Object Lock (compliance mode) |
| Backups | PITR 15 minutes; daily snapshots 35 days; quarterly restore exercise |
| Archival | Parquet exports to archival storage after 24 months; aggregates retained |
| Residency | Region-pinned stacks; no cross-region replication of tenant data |
| Key management | Per-region KMS; envelope encryption; key rotation annually |

### 6.7 Asynchronous Processing

| Concern | Decision |
|---------|----------|
| Event publication | Transactional outbox written in the same transaction as the business change |
| Dispatch | Worker claims batches with `FOR UPDATE SKIP LOCKED`; at-least-once |
| Ordering | Per aggregate ID via partition key; no global ordering |
| Retry | Exponential backoff with jitter; max 8 attempts; then dead-letter |
| Dead letter | `ops.outbox_event` status `DEAD`; operator view and replay action |
| Deduplication | `ops.inbox_dedup(event_id, consumer)` unique constraint |
| Scheduling | Quartz.NET with clustered job store; misfire policy `fire once` |
| Long jobs | Resumable checkpoints in `ops.job`; max runtime 30 minutes; heartbeat |
| Backpressure | Queue-depth metrics; worker autoscaling on oldest-job age, not CPU alone |
| Poison messages | Payload hash and error captured; replay after fix; no infinite retry |

### 6.8 Reporting and Excel

| Concern | Decision |
|---------|----------|
| Import parse | Streaming `ExcelDataReader`; file ≤50 MB and ≤50,000 rows; formula cells rejected unless allow-listed |
| Import validation | Shared domain validator; row-level errors with cell references; preview before commit |
| Import commit | Chunked (1,000 rows) transactions; batch resumable; idempotent by file hash + template version |
| Import security | ZIP bomb and macro rejection; CSV injection neutralization on export; malware scan before parse |
| Export format | XLSX via OpenXML SDK; CSV for machine exchange; PDF for signed reports |
| Report rendering | HTML template → Chromium PDF; page headers with tenant, period, and generation timestamp |
| Reproducibility | Report run stores parameters, data watermark, template version, and output hash |
| Delivery | Signed URL ≤15 minutes; optional scheduled email with link, never attachment for confidential reports |

### 6.9 Identity and Security Tooling

| Concern | Decision |
|---------|----------|
| Password hashing | Argon2id with per-user salt and tuned memory cost |
| MFA | TOTP at MVP; WebAuthn at Phase 1; mandatory for privileged roles and external auditors |
| Tokens | JWT access token 15 minutes; opaque refresh token 14 days with rotation and reuse detection |
| Revocation | Session registry plus Redis denylist; grant revocation event evicts scopes within 5 minutes |
| Service identity | Workload identity from the container platform; no static cloud keys |
| Secrets | Managed secret store; 90-day rotation; no secrets in images, logs, or client bundles |
| Encryption | TLS 1.2+ in transit; AES-256 at rest; field-level encryption for MFA seeds and device keys |
| Scanning | Trivy for images, CodeQL for code, Gitleaks for secrets, ZAP baseline for APIs |
| Pen testing | Independent test before pilot go-live and annually |

### 6.10 Observability Tooling

| Concern | Decision |
|---------|----------|
| Instrumentation | OpenTelemetry for ASP.NET Core, HTTP, EF Core, Redis, and background jobs |
| Traces | W3C Trace Context propagated through API, outbox, and workers via `traceparent` in event metadata |
| Metrics | RED for services, USE for infrastructure, business metrics for capture and approval |
| Logs | Structured JSON with `traceId`, `org_id` (hashed where required), and no financial payloads |
| Dashboards | SLO burn, queue age, sync success, import throughput, projection freshness |
| Alerting | Burn-rate alerts for SLOs; symptom-based paging; no raw CPU alerts as pages |
| Retention | Logs 30 days hot, 13 months cold; traces 7 days; metrics 13 months |

### 6.11 Build and Supply Chain

| Concern | Decision |
|---------|----------|
| Repository | Monorepo: `backend/`, `web/`, `mobile/`, `infra/`, `contracts/`, `docs/` |
| Branching | Trunk-based with short-lived branches and required checks |
| Build | Deterministic builds; lockfiles committed; central package management |
| Images | Multi-stage, distroless/runtime-deps base, non-root user, read-only filesystem |
| Provenance | SBOM (Syft), image signing (cosign), attestations retained |
| Dependency policy | Renovate with grouped updates; no unmaintained packages; license allow-list |
| Environments | dev, test, staging, prod per region; ephemeral preview for UI changes |
| Migrations | EF Core migrations reviewed as SQL; expand/contract; no destructive change in the same release |

### 6.12 Rejected Technologies

| Technology | Reason rejected |
|------------|-----------------|
| Kubernetes at MVP | Operational burden exceeds pilot needs (AD-11) |
| Kafka | Throughput needs are orders of magnitude below its operating cost |
| MongoDB | Financial integrity and relational reporting are weaker |
| DynamoDB | Access patterns are relational and reporting-heavy |
| Microservices | Distributed transactions and duplicated authorization for a small team |
| Next.js SSR | Authenticated SPA does not benefit; adds hosting complexity |
| Flutter | Second language and separate hiring pipeline |
| ClosedXML for 50k-row import | Memory profile violates worker limits |
| Client-side Excel validation only | Control bypass (RSK-005) |
| US-only SaaS observability | Residency conflict (NFR-PRV-009) |

---

## 7. Core Platform Architecture

### 7.1 Multi-Tenancy Runtime

| Mechanism | Design |
|-----------|--------|
| Tenant identifier | `org_id` UUID on every tenant-owned row; immutable |
| Request context | Middleware resolves the session and writes `app.org_id`, `app.user_id`, `app.grant_ids` for the transaction |
| RLS policy shape | `org_id = current_setting('app.org_id')::uuid` **or** a valid grant predicate on `org_id` + `show_id` |
| Grant predicate | `EXISTS (SELECT 1 FROM tenancy.active_grant_scope g WHERE g.grant_id = ANY(current_setting('app.grant_ids')::uuid[]) AND g.org_id = row.org_id AND (g.show_id IS NULL OR g.show_id = row.show_id))` |
| Connection pooling | PgBouncer transaction mode; `SET LOCAL` inside each transaction; no session-level state |
| Application role | `slateiq_app` — no `BYPASSRLS`, no table ownership, no DDL |
| Migration role | Separate `slateiq_migrator`; used only by CI/CD, never by the application |
| Testing | Testcontainers PostgreSQL; a dedicated suite runs as `slateiq_app` and asserts zero rows for foreign tenants on every tenant table |
| Monitoring | Query tags include module and tenant hash; slow-query alerts exclude raw tenant data |
| Dedicated option | A dedicated database or stack can be provisioned without code change (enterprise tier) |

**Grant scope resolution.** Application code computes `EffectiveScope` once per request: home organization, role permissions, active grants, show/department filters, and validity window. The scope is cached for ≤60 seconds keyed by `user_id + session_id + grant_version`. Revocation increments `grant_version` and publishes an event that evicts the cache.

**Column-scoped sensitivity.** A grant may include a permission level (`READ`, `WRITE`, `APPROVE`) and a category filter. Category filters are applied in application queries; RLS enforces the organization and show boundary. This is deliberate: putting arbitrary category arrays into RLS policies would make plans unstable and policies unmaintainable. The security test suite verifies both layers.

### 7.2 Identity and Sessions

| Aspect | Design |
|--------|--------|
| Account model | A user has one **home organization** and may receive grants to other organizations (D-003) |
| Invitation | Signed, single-use, expiring invitation; email ownership verified |
| MFA | TOTP enrollment with recovery codes; mandatory for Org Admin, Finance Head, Audit Manager, Platform Ops, and all external auditors |
| Access token | JWT, 15 minutes, `sub`, `org_id`, `session_id`, `roles`, `scope_hash`; no permissions list (avoids stale broad tokens) |
| Refresh token | Opaque, hashed at rest, 14 days, rotation on every use, reuse detection revokes the family |
| Device binding | Mobile refresh tokens bound to a device registration; new device requires re-authentication |
| Revocation | Session row status + Redis denylist; access tokens expire naturally within 15 minutes; high-risk actions re-check session status |
| Step-up | Re-authentication required for grant creation, role change, retention override, and export of restricted data |
| Service accounts | Platform Ops only; scoped, short-lived, audited; no interactive login |
| Brute force | Per-account and per-IP throttling; progressive delays; alert on credential stuffing patterns |

### 7.3 Authorization

```
 Request → Authentication → EffectiveScope builder → Policy decision → Query filter + RLS → Response
                                   │                        │
                                   ├── roles/permissions    ├── deny → 403 + audit
                                   ├── grants               └── allow → handler
                                   ├── show/department scope
                                   └── period lock / SoD
```

| Layer | Responsibility |
|-------|----------------|
| Permission catalog | Atomic `resource:action` codes, seeded and versioned; no ad-hoc strings |
| Role templates | System roles per Doc 1 personas; custom roles may compose permissions but cannot exceed the org boundary |
| Policy decision | Central evaluator combining RBAC, scope, grant, period state, and segregation-of-duties rules |
| Enforcement point | Endpoint filter for coarse checks; handler for resource-level checks; RLS for tenant boundary |
| Denial handling | `403` with stable code; security event recorded; no resource existence disclosure |
| Caching | Permission sets cached per role version; scope cached per session/grant version; never cache decisions containing row data |
| Review | Quarterly access review generated from role assignments and grants (FR-CMP-013) |

**Segregation of duties.** The policy evaluator rejects any approval where the approver is the submitter, the creator of the budget line, or a delegate of either. Delegation is bounded to 90 days, non-chained, and audited (D-021).

### 7.4 API Layer

| Aspect | Decision |
|--------|----------|
| Style | REST over HTTPS, JSON; resource-oriented; verbs only for non-CRUD actions |
| Versioning | Path `/api/v1`; additive changes preferred; breaking change requires new version and ≥6 months notice (NFR-MNT-001) |
| Idempotency | `Idempotency-Key` required for POST that creates money, evidence, imports, or grants; stored 7 days |
| Pagination | Cursor-based for large collections; `limit` ≤200; stable sort key |
| Filtering | Allow-listed fields; explicit indexes; no arbitrary query language |
| Concurrency | `ETag`/`If-Match` on mutable financial records; stale write returns `409` |
| Errors | RFC 9457 with `code`, `detail`, `traceId`, `errors[]`; no stack traces or SQL |
| Rate limits | Per tenant, per user, per endpoint class; `429` with `Retry-After`; separate budgets for sync and reports |
| Payload limits | JSON ≤1 MB; evidence via presigned upload, never through the API |
| Timeouts | Client 30 s; server handler 10 s; long work returns `202` with job ID |
| OpenAPI | Generated, linted, and diffed in CI; breaking-change detection blocks merge |
| Deprecation | `Sunset` and `Deprecation` headers; usage telemetry before removal |

### 7.5 Events and Background Processing

| Element | Design |
|---------|--------|
| Outbox record | `event_id`, `aggregate_type`, `aggregate_id`, `event_type`, `payload`, `metadata`, `occurred_at`, `status`, `attempts`, `available_at` |
| Metadata | `traceparent`, `org_id`, `actor_id`, `correlation_id`, `schema_version` |
| Dispatcher | Claims up to 200 events per batch; per-aggregate ordering; marks `DISPATCHED` after all consumers succeed |
| Consumers | Idempotent handlers registered by event type; failures isolated per consumer |
| Retry | 1 s, 5 s, 30 s, 2 min, 10 min, 30 min, 2 h, 6 h; jitter ±20% |
| Dead letter | After 8 attempts; operator replay with a new attempt counter; original payload retained |
| Scheduler | Quartz clustered; jobs are idempotent and store their last successful run |
| Projections | Checkpoint per projector; rebuild command; lag metric per tenant |
| Transactional boundary | No event is published before commit; no commit succeeds without its outbox row |

### 7.6 Caching

| Cache | Content | TTL | Invalidation |
|-------|---------|-----|--------------|
| Reference data | Categories, departments, currencies, permission catalog | 15 min | Version bump on change |
| Session scope | EffectiveScope per session | 60 s | Grant/session event |
| Dashboard | Aggregate response fragments | 30 s | Projection watermark |
| Rate limits | Counters | Window | Natural expiry |
| Locks | Import, report, digest per tenant/key | Job lifetime | Release or expiry |
| Revocation | Revoked session/grant IDs | 15 min | Natural expiry |

**Never cached:** expense rows, evidence content, personal data beyond IDs, authorization decisions tied to row data, signed URLs (generated per request).

### 7.7 Evidence Pipeline

```
 Mobile/Web → request upload ticket → API validates scope + quota + type
        → presigned PUT (≤15 min, content-length + SHA-256 bound)
        → object stored in QUARANTINE prefix
        → scan job (malware, ZIP bomb, MIME sniff)
        → CLEAN: move to tenant prefix, create DocumentVersion, emit DocumentScanned
        → INFECTED/FAILED: retain in quarantine, emit DocumentQuarantined, notify uploader
        → link to expense line; evidence completeness recalculated
```

| Control | Design |
|---------|--------|
| Type allow-list | JPEG, PNG, HEIC, PDF, XLSX, DOCX; executables and archives rejected |
| Size | ≤25 MB per file (OQ-06 pending); per-tenant quota enforced |
| Hash | SHA-256 computed client-side and server-side; mismatch rejects |
| Access | Signed GET ≤15 minutes; `Content-Disposition: attachment`; no public buckets |
| Preview | Thumbnails generated only after clean scan |
| Retention | Object lifecycle respects retention policy and legal hold |
| Audit | Every view/download writes `DocumentAccessLog` with actor, IP, grant ID |

### 7.8 Search

| Aspect | MVP | Trigger for Phase 1 |
|--------|-----|---------------------|
| Engine | PostgreSQL `tsvector` + `pg_trgm` | p95 >1 s at 10M documents |
| Scope | Expenses, vendors, shows, documents, audit events | Same |
| Isolation | Every query includes tenant predicate and RLS; no global index | Same |
| Ranking | Recency + exact match + field weighting | Relevance tuning with click feedback |
| Indexing | Transactional update on write; rebuild command | CDC to OpenSearch |
| Sensitive fields | Not indexed (evidence content, bank references) | Same |

### 7.9 Notification Dispatch

| Element | Design |
|---------|--------|
| Channels | MVP: in-app (database + SSE), email; Phase 1: push; Phase 2: SMS, webhooks |
| Templates | Versioned, localized, with required and optional variables; preview and test send |
| Preferences | Per user, per category, per channel; mandatory security notices cannot be disabled |
| Routing | Event → rule → recipients → preference filter → channel adapter |
| Escalation | Critical alerts escalate after 15 minutes if unacknowledged (FR-NTF-012) |
| Digest | 07:00 organization local time; role-scoped sections; skipped when empty |
| Delivery | At-least-once; provider message ID stored; bounce and complaint handling |
| SSE | `/api/v1/stream` with heartbeat; reconnect with `Last-Event-ID`; polling fallback every 60 s |
| Suppression | Cooldowns per rule/scope; duplicate suppression within 10 minutes |

---

## 8. Data Architecture (Application View)

### 8.1 Data Classification and Ownership

| Class | Examples | Owner module | Storage | Retention |
|-------|----------|--------------|---------|-----------|
| Restricted | Credentials, MFA seeds, device keys, bank references | Identity, Vendor | Encrypted columns / secret store | Life of account + 30 days |
| Confidential | Expenses, evidence, budgets, audit events, grants | Expense, Documents, Budget, Compliance | PostgreSQL + object storage | 7 years (D-014) |
| Internal | Aggregates, dashboards, templates, job metadata | Intel, Report, XLS, Ops | PostgreSQL | 2 years unless derived |
| Public | Product metadata, taxonomy labels | Production | PostgreSQL | Indefinite |

### 8.2 Schema Strategy and Naming

| Rule | Detail |
|------|--------|
| Schema per module | `tenancy`, `identity`, `prod`, `budget`, `vendor`, `auditops`, `expense`, `intel`, `docs`, `xls`, `notify`, `compliance`, `report`, `sync`, `ops` |
| Table naming | Singular snake_case with module prefix only where ambiguity exists (for example `expense.expense`, `intel.cost_fact`) |
| Primary keys | `uuid` (UUIDv7) for business entities; `bigint` identity for high-volume append-only rows |
| Tenant column | `org_id uuid not null` on every tenant table; indexed as the leading column |
| Timestamps | `created_at`, `updated_at` as `timestamptz` in UTC; `occurred_at` for business time |
| Soft delete | `deleted_at` only where recovery is required; financial records are never soft-deleted, they are cancelled or amended |
| Enumerations | Lookup tables for configurable values; PostgreSQL enums only for closed, code-coupled states |
| JSONB | Only for flexible metadata, never for amounts, statuses, or relationships |
| Naming of constraints | `pk_`, `fk_`, `uq_`, `ck_`, `ix_` prefixes for reviewability |

### 8.3 Money, Currency, and FX

| Concern | Design |
|---------|--------|
| Storage | `numeric(19,4)` for amounts; `numeric(19,6)` for rates; ISO 4217 currency code |
| Rounding | Half-even to 4 decimal places at line level; header totals sum rounded lines; display rounds to currency minor units |
| Base currency | One per organization; set at provisioning and changeable only with a controlled migration |
| FX snapshot | `original_amount`, `original_currency`, `rate`, `rate_date`, `rate_source`, `base_amount` stored on the expense/commitment |
| Rate source | Organization rate table populated daily (ECB/OER) with manual override; latest published rate as fallback (D-007) |
| Lock point | Rate locked at submission; amendments retain the original rate unless the amendment changes the transaction date and policy allows re-lock |
| Revaluation | None in MVP or Phase 1 (D-008); reports use the locked rate |
| Precision tests | Property-based tests for sum, conversion, and rounding invariants |

### 8.4 Temporal and Versioning Model

| Entity | Versioning approach | History retained |
|--------|---------------------|------------------|
| Budget | Immutable `BudgetVersion` per approval; one active version | All versions, forever |
| Budget line | Belongs to a version; never updated in place after approval | All versions |
| Expense | Mutable until submission; amendments create a new revision with `supersedes_id` | All revisions |
| Approval | Append-only decisions with actor, timestamp, and comment | Forever |
| Assignment/Grant | `valid_from`/`valid_to`; revocation is a state change with reason | Forever |
| Retention policy | Versioned; changes require approval | Forever |
| Reference data | Effective-dated rows where history affects reporting | Forever |

**Bitemporal scope.** Full bitemporality is limited to audit events and budget versions. Other entities use application time plus immutable audit history; this avoids join complexity that the reporting requirements do not justify.

### 8.5 Audit Event Model

| Field | Type | Purpose |
|-------|------|---------|
| `event_id` | ULID | Time-sortable unique ID |
| `org_id` | UUID | Tenant isolation |
| `chain_seq` | bigint | Per-tenant sequence |
| `event_type` | text | Stable taxonomy (`expense.approved`, `grant.revoked`) |
| `actor_id` | UUID | User or service |
| `actor_type` | text | `USER`, `SERVICE`, `SYSTEM` |
| `entity_type`, `entity_id` | text, UUID | Subject |
| `action` | text | `CREATE`, `UPDATE`, `DELETE`, `APPROVE`, `REJECT`, `ACCESS` |
| `before_state`, `after_state` | jsonb | Minimal diff, no secrets |
| `metadata` | jsonb | Correlation, grant, source IP, user agent |
| `occurred_at` | timestamptz | Business time |
| `prev_hash`, `hash` | bytea | SHA-256 chain |
| `schema_version` | int | Event schema evolution |

**Chain rule.** `hash = SHA-256(prev_hash || canonical_json(event_without_hash))`. A per-tenant `audit_chain_head` row is locked during append. Daily anchors (last hash + count) are written to Object Lock storage. A verification job recomputes the chain per tenant per day and alerts on mismatch (QA-11).

**Write path.** The compliance module exposes an in-transaction `IAuditWriter` used by all modules. It writes to `compliance.audit_event` within the caller's transaction. A failed audit write fails the business transaction — audit is not best-effort.

### 8.6 Read Models and Projections

| Read model | Source events | Refresh | Consumer |
|------------|---------------|---------|----------|
| `intel.cost_fact` | ExpenseSubmitted, ExpenseApproved, ExpenseAmended, ExpenseCancelled | ≤60 s | Dashboards, variance |
| `intel.commitment_fact` | CommitmentCreated/Amended/Closed | ≤60 s | Variance |
| `intel.variance_snapshot` | Cost and commitment facts + BudgetApproved | ≤60 s show; ≤5 min portfolio | Dashboards, alerts |
| `intel.benchmark_aggregate` | Nightly from cost facts, k-anonymity ≥5 orgs | Nightly | Benchmark (Phase 2) |
| `report.*` | On-demand query over source/read models | Per request | Reports |
| `notify.*` | Domain events | ≤60 s | In-app, email |

**Rebuild.** Every projector supports a full rebuild from source tables plus the outbox history. A rebuild runs in a shadow schema, is verified against reconciliation totals, then swapped.

### 8.7 Partitioning, Archival, and Retention

| Table | Partition | Retention hot | Archive |
|-------|-----------|---------------|---------|
| `expense.expense` | Monthly by `created_at` | 24 months | Parquet, 7 years |
| `expense.expense_line` | Monthly by `created_at` | 24 months | Parquet, 7 years |
| `compliance.audit_event` | Monthly by `occurred_at` | 24 months | WORM + Parquet, 7 years |
| `notify.notification` | Monthly by `created_at` | 6 months | 13 months |
| `ops.outbox_event` | Monthly by `occurred_at` | 3 months | 13 months |
| `intel.cost_fact` | Monthly by `cost_date` | 36 months | Aggregates retained indefinitely |

**Legal hold** overrides deletion at the row and object level. Retention jobs run daily, produce an action report, and require approval for any destructive step in the first 12 months of operation.

### 8.8 Residency and Key Management

| Concern | Design |
|---------|--------|
| Region assignment | `ops.tenant_region(org_id, region, status)`; global mapping contains no personal data |
| Data plane | Each region has independent PostgreSQL, Redis, object storage, KMS, and telemetry backends |
| Backups | Region-local, encrypted with region KMS key; no cross-region copy |
| Keys | Per-region CMK; per-tenant data key wrapped by CMK; annual rotation |
| Cross-region grants | Not supported in MVP; Phase 1 uses token exchange and serves data from the owning region |
| Operator access | Break-glass only, region-bound, session-recorded, and audited |
| Deletion | Crypto-shredding of the tenant data key after lawful deletion |

### 8.9 Backup and Recovery

| Control | Target |
|---------|--------|
| PITR | 15 minutes (NFR-DAT-001, NFR-AVL-007) |
| Full snapshot | Daily, 35-day retention |
| Object storage | Versioning + cross-AZ durability; Object Lock for audit |
| Restore test | Quarterly, timed, evidence retained (NFR-DAT-002) |
| RTO | 4 hours for regional recovery (NFR-AVL-006) |
| Runbook | Document 17 owns the step-by-step procedure; this document defines the sequence and acceptance criteria |

### 8.10 Data Quality Controls

| Check | Frequency | Action on failure |
|-------|-----------|-------------------|
| Expense header = sum of lines | Every write + nightly | Block write; alert finance ops |
| Approved actual = sum of approved lines | Nightly | Alert; freeze affected dashboard |
| Variance = budget − actual − committed | Every projection | Recompute; alert if still mismatched |
| Every fact has a source record | Nightly | Quarantine orphan facts; alert |
| FX rate present and sourced | Every write | Reject submission or mark fallback |
| Audit chain verifies | Daily | Alert security; freeze exports |
| Cross-module reference integrity | Nightly | Report orphans; no silent repair |
| Benchmark sample ≥5 organizations | Every publication | Suppress benchmark |

---

## 9. Offline & Sync Architecture

### 9.1 Why Offline Is Architectural

Field capture happens on production locations where connectivity is intermittent (TC-01). If capture depends on the network, the platform loses the only data it exists to collect. Offline is therefore a **first-class state**, not an error path: the device is the system of capture, and the server is the system of record (AP-08, AD-04).

| Requirement | Architectural consequence |
|-------------|---------------------------|
| NFR-AVL-005: full capture for 7 days offline | Local schema covers every MVP capture field; no server round-trip required |
| NFR-MOB-001: capture in ≤30 s median | Local write completes in one transaction; evidence queued separately |
| NFR-MOB-002: 180 queued items drain in ≤5 min | Batched push, parallel evidence uploads, resumable transfers |
| NFR-MOB-003: deterministic conflicts | Explicit conflict matrix (§9.4); no implicit last-write-wins on financial fields |
| NFR-MOB-004: encrypted, wipeable device | SQLCipher, keystore-backed keys, server-issued wipe |
| NFR-MOB-005: background sync | OS background task with bounded execution and checkpointed cursor |
| NFR-MOB-006: visible sync state | Per-record status and a queue summary surfaced in the UI |

### 9.2 Device Data Model

| Local table | Purpose | Key columns |
|-------------|---------|-------------|
| `local_scope` | Cached user, roles, permitted shows/departments, grant validity | `user_id`, `org_id`, `scope_json`, `valid_to` |
| `local_reference` | Shows, seasons, episodes, departments, categories, currencies, vendors | `id`, `version`, `payload`, `effective_from` |
| `local_expense` | Expense header and status mirror | `id`, `server_version`, `base_version`, `status`, `sync_state` |
| `local_expense_line` | Line items with tombstone flag | `id`, `expense_id`, `amount`, `currency`, `deleted` |
| `local_evidence` | Evidence metadata and local file path | `id`, `expense_id`, `sha256`, `bytes`, `upload_state` |
| `local_mutation` | Ordered outbox of pending changes | `mutation_id`, `seq`, `type`, `payload`, `attempts`, `state` |
| `local_conflict` | Conflicts awaiting a user decision | `conflict_id`, `entity_id`, `server_value`, `client_value`, `resolution` |
| `local_sync_state` | Pull cursor, last handshake, device ID | `cursor`, `server_time`, `last_success_at` |

**Identifiers.** Every client-created record uses a **UUIDv7** generated on the device. The server accepts client IDs, so retries and duplicate deliveries collapse onto the same row rather than creating a second expense.

**Versioning.** Every server aggregate carries `version bigint`, incremented on each accepted mutation. The device stores `base_version` — the version it last saw — and sends it with every mutation. A mismatch is the trigger for conflict evaluation (§9.4), not an automatic rejection.

**Local ordering.** `local_mutation.seq` is a monotonic per-device counter. The server applies mutations in `seq` order within a batch and refuses to skip a gap; a gap forces a handshake before the batch is accepted.

### 9.3 Sync Protocol

```
Device                                   Sync Host
  │  POST /sync/handshake                  │
  │  device_id, app_version, cursor        │
  │◄───────────────────────────────────────│  server_time, scope_version,
  │                                        │  min_supported_version, cursor_valid
  │  POST /sync/push                       │
  │  batch_id, device_seq_range,           │
  │  mutations[25]                         │
  │───────────────────────────────────────►│  validate → authorize → apply
  │◄───────────────────────────────────────│  results[], server_time
  │  POST /sync/pull?cursor=...            │
  │───────────────────────────────────────►│  change log since cursor
  │◄───────────────────────────────────────│  changes[], next_cursor, has_more
  │  POST /sync/ack                        │
  │  cursor, applied_mutation_ids          │
  │───────────────────────────────────────►│  prune device queue
```

| Endpoint | Purpose | Limits |
|----------|---------|--------|
| `POST /api/v1/sync/handshake` | Validate device, app version, scope version, cursor age | 1 per 5 minutes per device |
| `POST /api/v1/sync/push` | Apply a batch of mutations | ≤25 mutations, ≤512 KB |
| `GET /api/v1/sync/pull` | Fetch changes since cursor | ≤5 MB per page, 500 records |
| `POST /api/v1/sync/ack` | Confirm applied cursor and mutation IDs | ≤500 IDs |
| `POST /api/v1/sync/conflicts/{id}/resolve` | Submit a user decision | 1 per conflict |
| `POST /api/v1/sync/evidence-ticket` | Obtain presigned upload ticket | ≤50 per batch |
| `POST /api/v1/sync/evidence-complete` | Confirm hash and finalize evidence | 1 per file |

**Mutation envelope.**

| Field | Required | Purpose |
|-------|----------|---------|
| `mutation_id` | Yes | UUIDv7 generated on the device; idempotency key |
| `device_id` | Yes | Bound to the session; used for replay defence |
| `entity_type`, `entity_id` | Yes | Target aggregate |
| `operation` | Yes | `CREATE`, `UPDATE`, `DELETE_EVIDENCE`, `SUBMIT`, `AMEND` |
| `base_version` | Yes | Version the client last observed; `0` for create |
| `client_occurred_at` | Yes | Device clock, used for display and audit, never for ordering |
| `payload` | Yes | Changed fields only, schema-versioned |
| `client_hash` | No | Content hash for evidence-bearing mutations |

**Push processing rules.**

1. Authenticate the session and confirm the device is active and not wiped.
2. Reject the batch if `device_seq_range` contains a gap or overlaps an already-processed range.
3. Deduplicate on `mutation_id`; a repeated mutation returns its original result.
4. Apply mutations sequentially. A `RETRY` result stops the batch so later mutations cannot overtake it.
5. For each mutation, open one transaction with tenant context, authorize, evaluate conflicts, apply, write audit, write outbox.
6. Return a per-item result: `APPLIED`, `CONFLICT`, `REJECTED`, or `RETRY` with a stable reason code.
7. Commit the batch checkpoint so a dropped connection resumes at the next unapplied `seq`.

**Pull processing rules.**

1. `sync.change_log` records `(server_seq bigint, org_id, entity_type, entity_id, version, op, changed_at)`.
2. The cursor is an opaque, signed encoding of `server_seq` plus the tenant and device.
3. Pull returns only data the device's scope permits: reference data, the user's own records, and records in shows the user can currently access.
4. Scope changes force a `scope_version` bump; the device performs a scoped full resync of reference data.
5. The change log is retained **30 days**. A cursor older than retention returns `CURSOR_EXPIRED`, and the device performs a full resync of open records.

**Full resync.** Reference data first, then open expenses for the current and previous period, then evidence metadata. Closed and approved periods are fetched on demand when the user opens them; this keeps the initial sync under the 500-expense local limit.

### 9.4 Conflict Matrix

Conflict policy codes:

| Code | Policy | Meaning |
|------|--------|---------|
| **C1** | Server-authoritative | Server value wins; client is informed and its change discarded |
| **C2** | Field-level last-writer-wins | Server receive time decides; only for non-financial descriptive fields |
| **C3** | Merge | Disjoint changes combine; deletions are tombstones |
| **C4** | Manual | `ConflictRecord` created; user chooses; nothing is silently lost |
| **C5** | Reject | Mutation refused with a stable code; user must act online |

| Entity / field | Editable offline | Conflict condition | Policy | User experience |
|----------------|:---------------:|--------------------|:------:|-----------------|
| Expense description, merchant, notes | Yes | Both sides changed since `base_version` | C2 | Saved; "updated on another device" badge |
| Expense `transaction_date` | Yes | Target period is locked | C5 | Field error `PERIOD_LOCKED`; date must move |
| Expense line amount, quantity, rate | Yes | Same line changed on both sides | C4 | Conflict screen with both values and totals |
| Expense line added | Yes | Concurrent additions to the same expense | C3 | Both lines kept, IDs distinct |
| Expense line removed | Yes | Line has evidence or approval on the server | C4 | User confirms removal or restores the line |
| Expense status draft → submitted | Yes | Already submitted or approved | C1 | Server state shown; duplicate submit is a no-op |
| Expense status submitted → approved | **No** | — | C5 | Approve is disabled offline |
| Expense cancel or void | **No** | — | C5 | Disabled offline; requires current period and approval state |
| Expense amendment | **No** | — | C5 | Requires latest version and approval context |
| Evidence attach | Yes, queued | Expense already approved | C4 | Evidence attached with `REVIEW_REQUIRED` flag |
| Evidence delete | Yes, queued | Evidence already reviewed | C5 | Rejected; a superseding document is required |
| Category | Yes | Category deactivated or not permitted for the user | C5 | Picker refreshes; user reselects |
| Department | Yes | Department removed from the show | C5 | Picker refreshes; user reselects |
| Vendor | Yes | Vendor blocked | C1 | Blocked vendor shown; expense flagged |
| Currency | Yes | Organization base currency changed | C1 | Server currency applied; user warned |
| FX rate | **No** | — | C1 | Rate is locked at submission by the server |
| Budget or commitment reference | **No** | — | C1 | Server-managed reference |
| Approval decision | **No** | — | C5 | Online only (segregation of duties and period state) |
| Role, scope, grant | **No** | — | C1 | Scope refresh forced by `scope_version` |

**Why approvals are online-only.** An approval requires a current view of segregation-of-duties state, delegation validity, period lock, and budget version. Caching those on a device would create a decision that is correct locally and wrong globally. Capture is optimized for offline; **control points are optimized for correctness**.

**Conflict volume control.** Manual conflicts are rare by design: financial fields are only editable by the submitter before submission, and approvals never happen offline. The expected conflict rate is below 1% of mutations; alerting fires at 3% (§9.7).

### 9.5 Sync Guarantees

| Guarantee | Mechanism | Verified by |
|-----------|-----------|-------------|
| No duplicate expenses | Client UUIDv7 plus `mutation_id` dedupe | Duplicate-delivery test |
| No lost mutations | Local queue persists until `ack`; batch checkpoint | Kill-connection test |
| Per-device ordering | `seq` gap detection and stop-on-`RETRY` | Property-based test |
| At-least-once delivery | Retries with exponential backoff | Chaos test |
| Exactly-once effect | Idempotent apply keyed by mutation and entity ID | Replay test |
| Bounded device storage | 7 days, 500 expenses, 500 MB; warnings at 80% | Device test |
| Deterministic outcome | Conflict matrix, no server-side randomness | Conflict suite |

**Retry schedule on the device.** 5 s, 15 s, 60 s, 5 min, 15 min, 1 hour, then every 6 hours while the app is foregrounded. Permanent failures (`REJECTED`) move to a user-visible "needs attention" list and stop retrying.

**Clock skew.** The server returns `server_time` on every response. The device computes a skew estimate for display only; ordering always uses the server sequence. A skew above 10 minutes is logged and shown as a warning.

### 9.6 Sync Security

| Control | Design |
|---------|--------|
| Local encryption | SQLCipher with AES-256; key wrapped by iOS Keychain or Android Keystore |
| App gate | Biometric or device passcode before opening the app (NFR-MOB-004) |
| Token binding | Refresh token bound to `device_id`; a new device requires re-authentication |
| Transport | TLS 1.2+ with certificate pinning for the API host |
| Replay defence | `mutation_id` uniqueness plus `device_seq_range` monotonicity |
| Wipe | `DeviceWiped` event; the app erases the database, files, and keys on next launch and refuses to sync |
| Lost device | Admin deactivates the device; the session is revoked immediately; the wipe is queued |
| Screen capture | Android `FLAG_SECURE` on approval and evidence screens; iOS blur on backgrounding |
| Local logs | No amounts or personal data; crash reports exclude payloads |
| Root/jailbreak | Detected and reported as a risk signal; not a hard block at MVP |

### 9.7 Sync Observability

| Metric | Target | Alert |
|--------|--------|-------|
| Push success rate | ≥99.5% | <98% for 15 min |
| Median queue drain after reconnect | ≤60 s | p95 >5 min |
| Conflict rate | <1% of mutations | >3% for 1 hour |
| Devices with queue age >24 h | <0.5% | >2% |
| Evidence upload failure rate | <2% | >5% for 15 min |
| `CURSOR_EXPIRED` rate | <1% of pulls | >5% |
| Wipe commands unacknowledged >7 days | 0 | Any |

### 9.8 Sync Test Strategy

| Test | Method | Pass criterion |
|------|--------|----------------|
| 180-item backlog | Simulated 3-day offline device | ≥95% applied within 5 minutes (QA-02) |
| Interrupted batch | Drop connection mid-push at each mutation index | No duplicate, no loss, resume correct |
| Duplicate delivery | Replay every mutation three times | One record, identical result |
| Concurrent edit | Two devices edit the same expense | Matrix policy applied exactly |
| Clock skew | Device clock ±6 hours | Server order preserved |
| Scope revocation mid-sync | Grant revoked between push and pull | Subsequent pull returns only permitted data |
| Wipe during queue | Wipe issued with 50 pending items | Local data erased; no sync after wipe |
| Schema upgrade | Upgrade app with pending queue | Queue preserved and applied |
| Long offline | 7 days, 500 expenses | Capture still works; warnings at limits |

---

## 10. Integration Architecture

### 10.1 Integration Principles Realized

| Principle (Doc 1 §12.1) | Architectural mechanism |
|--------------------------|-------------------------|
| IP-01 SlateIQ owns cost intelligence, not the ledger | ERP integration posts summarized journals only; no payment execution |
| IP-02 Idempotent by design | Every inbound file has a hash key; every outbound message an idempotency key; delivery records are unique |
| IP-03 Explicit contracts | Versioned JSON Schema or OpenAPI per integration; contract tests in CI |
| IP-04 Least data | Field allow-lists per contract; outbound payloads validated against the schema |
| IP-05 Observable | Delivery attempts, lag, error rate, and replay are first-class records and metrics |
| IP-06 Tenant-aware | Credentials, mappings, and delivery state are scoped by `org_id` |

### 10.2 Integration Runtime

```
Domain event / schedule / user action
        │
        ▼
┌───────────────────────┐      ┌───────────────────────┐
│ ops.outbox_event      │─────►│ Integration worker     │
│ (transactional)       │      │ claim → route → map    │
└───────────────────────┘      └───────┬───────────────┘
                                       ▼
                        ┌──────────────────────────────┐
                        │ Adapter (provider-specific)   │
                        │ email · storage · scanner ·   │
                        │ FX · ERP · webhook · IdP      │
                        └───────┬──────────────────────┘
                                ▼
                  ┌──────────────────────────────┐
                  │ ops.integration_delivery      │
                  │ attempt · status · error ·    │
                  │ provider_id · next_attempt_at │
                  └──────────────────────────────┘
```

| Component | Responsibility |
|-----------|----------------|
| Event router | Maps event types to integration subscriptions; no adapter code in domain modules |
| Mapping layer | Translates domain payloads to the contract; rejects unmapped required fields |
| Adapter | Provider protocol, authentication, timeout, and error classification |
| Delivery store | `ops.integration_delivery` with attempt count, provider message ID, and terminal state |
| Credential resolver | Fetches per-tenant credentials from the secret manager; caches ≤5 minutes; never logs values |
| Replay console | Operator action to requeue a `FAILED` delivery with a new attempt counter |
| Circuit breaker | Per provider and per tenant; opens after 10 consecutive failures in 60 s |

**Error classification.** `TRANSIENT` (timeout, 5xx, throttling) retries with backoff. `PERMANENT` (400, schema violation, revoked credential) stops and alerts. `UNKNOWN` retries three times, then alerts. This prevents both silent loss and retry storms.

### 10.3 MVP Integrations

#### 10.3.1 INTG-01 — Excel import and export

| Aspect | Design |
|--------|--------|
| Inbound | User uploads a template version; file hash and template version form the idempotency key |
| Validation | Streamed parse, schema check, domain validator, row-level errors with cell references |
| Commit | 1,000-row chunks; resumable; a batch is `STAGED` until the user confirms |
| Outbound | Generated XLSX from a report or export job; signed URL ≤15 minutes |
| Security | Macro rejection, ZIP-bomb limits, CSV-injection neutralization, malware scan before parse |
| Contract | Template ID plus version; a breaking change creates a new template version |
| Failure | Batch moves to `FAILED` with a downloadable error file; no partial visible commit until confirmed |

#### 10.3.2 INTG-02 — Transactional email

| Aspect | Design |
|--------|--------|
| Trigger | `Notification` row committed, then delivery attempted by the worker |
| Adapter | Provider-agnostic interface; SES reference implementation; secondary provider configurable |
| Idempotency | `notification_id` is the provider idempotency key where supported; duplicates suppressed for 10 minutes |
| Contract | Template ID, locale, variables; rendered server-side |
| Failure | Retry 1 m, 5 m, 30 m, 2 h, 6 h; after 24 h mark `FAILED`; critical alerts remain in-app |
| Compliance | No confidential report attachments; links only; unsubscribe rules for non-mandatory mail |

#### 10.3.3 INTG-03 — Object storage

| Aspect | Design |
|--------|--------|
| Direction | Internal, bidirectional |
| Access | Presigned PUT and GET only; no public bucket; no storage credentials in the app |
| Binding | Ticket binds content length, content type, SHA-256, tenant prefix, and expiry |
| Encryption | SSE-KMS with a per-region key; TLS in transit |
| Lifecycle | Standard → infrequent access at 90 days → archive at 24 months; legal hold suspends |
| Failure | Upload blocked with retry guidance; local draft preserved on mobile (Doc 1 §12.5) |

#### 10.3.4 INTG-04 — Malware scanning

| Aspect | Design |
|--------|--------|
| Trigger | Object write to the `quarantine/` prefix emits a scan job |
| Scan | Signature engine plus MIME sniffing and archive inspection; no execution |
| Outcome | `CLEAN` promotes to the tenant prefix; `INFECTED` or `FAILED` stays quarantined |
| Availability | If the scanner is unavailable, the file remains `PENDING_SCAN` and is never served |
| Idempotency | Job keyed by object version; rescanning is safe |
| Alerting | Any `INFECTED` result raises a security event and notifies the uploader and security ops |

#### 10.3.5 INTG-05 — FX rates

| Aspect | Design |
|--------|--------|
| Schedule | Daily at 16:30 CET after the ECB publication; retry hourly until success |
| Source | ECB reference rates through an adapter; OER or a secondary source as fallback |
| Contract | `base`, `quote`, `rate`, `rate_date`, `source`, `fetched_at` |
| Idempotency | Unique on `(rate_date, base, quote, source)`; re-fetch updates only if the source changed |
| Override | Finance Head may set a manual rate with reason and expiry; override wins for its validity window |
| Fallback | If no rate exists for the date, the latest published rate is used and marked `FALLBACK` (QA-10) |
| Storage | `budget.fx_rate` and snapshots on each expense; historical rates are never overwritten |

#### 10.3.6 Internal event integrations

| Aspect | Design |
|--------|--------|
| Transport | `ops.outbox_event` plus `SKIP LOCKED` claims; no external broker at MVP |
| Contract | Event type, `schema_version`, payload schema in `contracts/events/` |
| Compatibility | Additive fields only within a version; a breaking change publishes a new event type |
| Ordering | Per aggregate ID; consumers never assume global order |
| Replay | Consumers must be replay-safe; projections support full rebuild |
| Dead letter | After 8 attempts; operator view with payload hash and error; replay after fix |

### 10.4 Phase 1 and Phase 2 Integrations

| ID | Integration | Protocol | Idempotency | Design notes |
|----|-------------|----------|-------------|--------------|
| INTG-06 | Identity provider (OIDC/SAML + SCIM) | Standards-based federation | SCIM `externalId`; JIT keyed by issuer + subject | Built-in identity remains as break-glass; SCIM deprovisioning disables the user and revokes sessions |
| INTG-07 | ERP / accounting | Batch file or API | Batch ID plus journal hash | Summarized journals only; mapping table per tenant; a failure leaves the whole batch `FAILED`, never partial |
| INTG-08 | Webhooks | Signed HTTP POST | `event_id` plus timestamp; receiver dedupes | HMAC-SHA256 signature; retries to 24 h; auto-disable after 20 consecutive failures; replay endpoint |
| INTG-09 | BI / data warehouse | Scheduled extract or CDC | Extract watermark | Read replica only; column allow-list; no evidence content; tenant-scoped extracts |
| INTG-10 | Push notifications | Provider API | `notification_id` | Device tokens stored per user; invalid tokens pruned on provider feedback |
| INTG-11 | SMS gateway | Provider API | `notification_id` | Critical alerts only; region allow-list; opt-in where required |
| INTG-12 | HRIS / payroll | API or file | Employee ID plus effective date | Crew and approver master data only; never payroll amounts |

**Why ERP posting is Phase 1.** The MVP must prove capture and control value. Posting to a customer ledger requires per-customer account mapping, cut-off rules, and reconciliation ownership — a program of work, not a connector. Exporting a validated XLSX satisfies the pilot without pretending to be an accounting system (IP-01).

### 10.5 Contract and Version Management

| Rule | Detail |
|------|--------|
| Source of truth | `contracts/openapi/`, `contracts/events/`, `contracts/templates/` in the monorepo |
| Versioning | SemVer per contract; the major version is part of the path or event type |
| Compatibility gate | CI diffs the generated schema against the last released version; a breaking change fails the build |
| Contract tests | Provider and consumer tests run against recorded fixtures; a schema change requires fixture updates |
| Deprecation | Minimum 6 months for external contracts (NFR-MNT-001); `Sunset` header and usage telemetry |
| Documentation | Generated reference plus a plain-language integration guide per external integration |
| Ownership | Each integration has a named owner in the integration catalog; Doc 6 owns the detailed contract |

### 10.6 Integration Security

| Control | Design |
|---------|--------|
| Credentials | Per-tenant, encrypted in the secret manager, never in the database in plaintext |
| Rotation | 90-day rotation; dual-credential overlap for zero-downtime change |
| Egress | Allow-list of provider hostnames per adapter; no arbitrary URL fetch |
| SSRF | Webhook URLs validated against public IP ranges; private and link-local addresses rejected |
| Inbound authenticity | Webhook signatures, file hashes, and OAuth token validation |
| Outbound payloads | Schema-validated; field allow-list; no internal IDs beyond the documented contract |
| Audit | Every credential change and every manual replay is an audit event |
| Least privilege | Adapter credentials scoped to the minimum provider permission |

### 10.7 Failure Handling and Operator Visibility

| Failure | Detection | Behaviour | Operator action |
|---------|-----------|-----------|-----------------|
| Email provider outage | 5 consecutive failures | Queue and retry; secondary provider if configured | Switch provider; drain queue |
| FX source unavailable | Scheduled pull fails | Latest rate with `FALLBACK` marker | Manual rate entry |
| Object storage unavailable | Upload error rate >5% | Block upload, preserve local draft | Provider status; retry |
| Scanner unavailable | Scan queue age >10 min | Hold as `PENDING_SCAN`; never serve | Escalate to security |
| ERP posting failure | Batch `FAILED` | No partial posting; alert finance ops | Fix mapping, replay batch |
| Webhook endpoint failing | 20 consecutive failures | Disable endpoint, notify org admin | Re-enable after receiver fix |
| SCIM provisioning failure | Error responses from IdP | Retain manual user management | Review sync error report |
| Outbox backlog | Oldest event age >5 min | Autoscale workers; alert | Inspect dead letters |

**Acceptance mapping.** IAC-01 is met by the idempotency keys in §10.3 and §10.4. IAC-02 by the alert thresholds in §10.7 and §14. IAC-03 by §10.6. IAC-04 by schema validation in §10.5. IAC-05 by `correlation_id` propagation from the originating audit event through every delivery attempt.

---

## 11. Performance & Scalability

### 11.1 Workload Model

The model derives from NFR-SCL-001 (500 organizations, 50,000 users, 5 million expenses per year) and assumes a media-production calendar: work concentrates on shooting days and month-end, with a seasonal peak of 3× for 30 days (NFR-SCL-005).

| Metric | Average | Peak | Design target (3× peak) |
|--------|--------:|-----:|------------------------:|
| Concurrent interactive users | 3,000 | 10,000 | 30,000 (test only) |
| API requests | 350 req/s | 1,200 req/s | 3,600 req/s |
| Expense writes | 1.6/s | 10/s | 30/s |
| Evidence uploads | 0.8/s | 8/s | 24/s |
| Sync push batches | 40/s | 250/s | 750/s |
| Dashboard requests | 120 req/s | 600 req/s | 1,800 req/s |
| Outbox events | 20/s | 150/s | 450/s |
| Report jobs | 40/day | 300/day | 900/day |
| Email deliveries | 10/s | 60/s | 180/s |

**Derivation.** 5,000,000 expenses ÷ 250 working days = 20,000/day. At 3× seasonal peak = 60,000/day. With 40% of daily volume in a 4-hour window, that is 6,000/hour ≈ 1.7/s, and a peak minute at roughly 5× the hourly average gives ≈8.3/s; 10/s is used. Each expense write produces about six API calls (reference lookups, draft save, submit, ticket, confirm), so 10/s of writes contributes ≈60 req/s; reads and dashboards dominate the rest.

### 11.2 Capacity Model

Planning estimates for the **GA envelope**; they are validated by the load tests in §11.8 and are not a substitute for measurement.

| Resource | Year 1 | Year 3 | Basis |
|----------|-------:|-------:|-------|
| PostgreSQL hot data | 350 GB | 900 GB | Expenses, lines, audit, notifications, cost facts, indexes, 40% headroom |
| PostgreSQL IOPS | 3,000 baseline / 12,000 burst | 6,000 / 20,000 | Write-heavy capture plus dashboard reads |
| Read replica lag | ≤2 s | ≤2 s | Asynchronous streaming replication |
| Redis | 2 GB | 8 GB | Scope cache, rate counters, locks, revocation set |
| Object storage | 5 TB | 15 TB | NFR-SCL-003 envelope plus exports and WORM copies |
| Object count | 10M | 50M | 2 evidence files per expense, 100 KB average after compression |
| Backup storage | 1.5 TB | 4 TB | 35 daily snapshots plus PITR WAL |
| Logs and traces | 300 GB | 1 TB | 30-day hot logs, 7-day traces, 13-month cold logs |

| Tier | MVP | GA | Scaling method |
|------|-----|----|----------------|
| API | 4 × 2 vCPU / 4 GB | 6–12 × 2 vCPU / 4 GB | Horizontal, target 65% CPU at peak |
| Sync | 2 × 2 vCPU / 4 GB | 4 × 2 vCPU / 4 GB | Horizontal, separate queue budget |
| Worker | 3 × 2 vCPU / 4 GB | 6–10 × 2 vCPU / 4 GB | Queue-depth and oldest-job-age autoscaling |
| Renderer | 1 × 2 vCPU / 4 GB | 2 × 4 vCPU / 8 GB | Memory-bound, isolated pool |
| PostgreSQL | 4 vCPU / 16 GB, Multi-AZ | 8 vCPU / 32 GB, Multi-AZ, 1 replica | Vertical first, then read replicas |
| Redis | 1 node + replica, 2 GB | Cluster mode, 3 shards | Horizontal sharding |

**Connection budgeting.** PgBouncer in transaction mode: 400 client connections per API task set, 80 server connections to PostgreSQL at MVP, 200 at GA. Each transaction sets tenant context with `SET LOCAL`; no connection is held across a user think time.

### 11.3 Latency Budgets

Each NFR target is decomposed so that a regression can be attributed to a layer rather than discovered at the end.

| Operation | Target (P95) | Network | Auth and scope | Handler and validation | Data access | Serialization | Client render |
|-----------|-------------:|--------:|---------------:|-----------------------:|------------:|--------------:|--------------:|
| Dashboard load (NFR-PERF-001) | 2,000 ms | 400 | 40 | 60 | 500 | 100 | 900 |
| Dashboard drill-down (NFR-PERF-002) | 2,000 ms | 350 | 30 | 70 | 550 | 100 | 900 |
| Expense submit (NFR-PERF-003) | 1,000 ms | 250 | 40 | 80 | 250 | 60 | 320 |
| Standard CRUD (NFR-PERF-012) | 500 ms | 150 | 30 | 40 | 120 | 40 | 120 |
| Search (NFR-PERF-008) | 1,000 ms | 250 | 40 | 30 | 450 | 130 | 100 |
| Excel validation (NFR-PERF-006) | 10 s per 1,000 rows | — | — | 6,000 ms | 2,500 ms parse and stage | — | — |
| Report generation (NFR-PERF-007) | 30 s per 100,000 rows | — | — | — | 20,000 ms read | 6,000 ms render | 4,000 ms upload and overhead |
| Evidence upload (NFR-PERF-011) | 15 s per 10 MB | 10,000 ms transfer at 1 MB/s | 200 | 300 | 4,500 ms hash, scan queue, confirm | — | — |

**Rule.** No synchronous endpoint performs work whose budget exceeds 10 seconds. Anything longer becomes a job with `202 Accepted` and a status resource (NFR-PERF-007).

### 11.4 Data Access Strategy

| Concern | Approach |
|---------|----------|
| Read models | Dashboards and variance read `intel` aggregates only; never join raw expenses at request time |
| Keyset pagination | `(created_at, id)` or `(server_seq)` cursors; `OFFSET` is prohibited on large tables |
| Covering indexes | Every list query has an index matching its filter and sort; verified with `EXPLAIN` in CI for the top 20 queries |
| Projection freshness | Show-level ≤60 s; portfolio ≤5 min; the API returns `as_of` and `freshness_tier` |
| N+1 prevention | Explicit projection queries; EF Core lazy loading disabled; query count asserted in integration tests |
| Aggregation | Incremental upserts from events, not `SUM` over raw rows per request |
| JSONB | Metadata only; never filtered or sorted on hot paths without a generated column and index |
| Full-text search | `tsvector` generated column with GIN; `pg_trgm` for fuzzy names; tenant predicate always present |
| Connection scope | Read-only transactions for queries; `statement_timeout` 5 s for API, 60 s for reports |
| Partition pruning | All queries on partitioned tables include the time range; a missing predicate fails query review |

### 11.5 Asynchronous Throughput

| Stage | Target | Backpressure |
|-------|--------|--------------|
| Outbox dispatch latency | p95 ≤5 s | Autoscale on oldest-event age |
| Cost fact projection | p95 ≤60 s from commit | Per-projector lag metric |
| Portfolio aggregate | ≤5 min | Scheduled incremental refresh |
| Import validation | 1,000 rows in ≤10 s | Dedicated worker pool, chunked |
| Report job queue wait | p95 ≤2 min | Separate queue class from imports |
| Notification dispatch | p95 ≤30 s | Provider rate limiter |
| Alert evaluation | Critical ≤5 min, standard ≤15 min | Priority queue for critical rules |

**Queue classes.** `interactive` (alerts, notifications), `bulk` (imports, exports), `render` (PDF), `maintenance` (retention, reindex). Each has its own concurrency limit so a 50,000-row import cannot delay a critical alert.

### 11.6 Scaling Model

| Stage | Trigger | Action | Expected effect |
|-------|---------|--------|-----------------|
| Vertical DB | CPU >70% for 15 min or IOPS >80% of provisioned | Increase instance class | Immediate headroom, no code change |
| Read replicas | Read CPU >60% or replica lag stable and low | Add replica; route reports and BI | Offloads analytics |
| API horizontal | Request concurrency >65% per task | Add tasks (min 4, max 20) | Linear to connection limits |
| Worker horizontal | Oldest job age >5 min | Add workers by queue class | Throughput scales per queue |
| Redis cluster | Memory >70% or ops >50% of capacity | Enable cluster mode | Shard cache and counters |
| Partition archival | Hot table >250 GB | Detach partitions older than 24 months to Parquet | Keeps index height stable |
| Service extraction | §4.5 triggers | Promote module to its own deployment | Isolates scaling and failure |

**Linear-scale evidence.** NFR-SCL-004 requires linear scale to 3× baseline. The application tier is stateless, sessions live in the database and Redis, and no request affinity is required. The database is the only vertical component; the capacity model keeps it below 70% at 3× peak, and read replicas absorb reporting before vertical limits are reached.

### 11.7 Performance Test Plan

| Scenario | Load | Duration | Pass criteria |
|----------|------|----------|---------------|
| Baseline | 350 req/s, 3,000 users | 2 h | All P95 targets met |
| Peak | 1,200 req/s, 10,000 users | 2 h | All P95 targets met; error rate <0.1% |
| Seasonal soak | 3× peak, accelerated | 24 h | No memory growth; no queue divergence; targets met |
| Spike | 0 → 3,600 req/s in 60 s | 15 min | Autoscale within 3 min; error rate <1% during ramp |
| Sync backlog | 500 devices × 180 mutations | 30 min | ≥95% drained in 5 min |
| Import | 50,000 rows, 50 MB | — | ≤10 s per 1,000 rows; memory <1.5 GB per worker |
| Report | 100,000 rows | — | ≤30 s; output hash reproducible |
| Dashboard cold cache | 600 req/s with empty Redis | 10 min | P95 ≤2 s after warm-up; no stampede |
| Failure injection | Kill primary, AZ, Redis, scanner | — | Degradation per §12.2; no data loss |
| Data volume | 10-year synthetic data set | — | Targets unchanged (NFR-SCL-006) |

**Environment.** Performance tests run against a production-like staging stack with the same instance classes, Multi-AZ, and RLS enabled. Testing with RLS disabled is prohibited because it changes plans and hides policy cost.

**Exit gate.** A release cannot ship if any P95 target regresses by more than 10% against the previous baseline without an approved exception recorded in §19.

### 11.8 Known Performance Risks

| Risk | Symptom | Mitigation |
|------|---------|------------|
| RLS policy cost on hot tables | Sequential scans, high `shared_blks_read` | Policy predicates on indexed `org_id`; `EXPLAIN` gate; cache scope |
| Aggregate refresh contention | Lock waits during month-end | Incremental upserts, partitioned facts, staggered refresh per tenant |
| Large import memory | Worker OOM at 50,000 rows | Streaming parser, 1,000-row chunks, memory cap, dedicated pool |
| Report query over raw tables | Report timeouts | Reports read read models; `statement_timeout`; async only |
| Dashboard stampede | Cache miss storm after deploy | Request coalescing, stale-while-revalidate, jittered TTL |
| Connection exhaustion | Pool timeouts under spike | PgBouncer, pool per workload, `429` before saturation |
| Outbox table bloat | Dispatch slows over months | Monthly partitions, prune `DISPATCHED` after 3 months |
| Audit chain contention | Hot row on `audit_chain_head` | One chain per tenant; append-only insert; short lock hold |

---

## 12. Availability, Resilience & Disaster Recovery

### 12.1 Service Level Objectives

| SLO | MVP target | GA target | Measurement |
|-----|-----------:|----------:|-------------|
| Platform availability | 99.5% monthly | 99.9% monthly | Synthetic probes plus real-user success rate |
| API success rate | ≥99.5% | ≥99.9% | Non-5xx responses excluding client errors |
| Dashboard latency | P95 ≤2 s | P95 ≤2 s | Server and RUM telemetry |
| Expense submit latency | P95 ≤1 s | P95 ≤1 s | APM |
| Projection freshness | 99% ≤60 s | 99% ≤60 s | Event timestamp to fact timestamp |
| Sync drain | 95% ≤5 min | 95% ≤5 min | Queue age at reconnect |
| Report success | ≥99% | ≥99.5% | Job terminal state |
| Alert evaluation | Critical ≤5 min | Critical ≤5 min | Rule evaluation timestamp |

**Error budget policy.** At 50% budget consumption, feature work pauses for reliability review. At 100%, only reliability and security changes ship until the budget recovers. This is a supporting control for SOC 2 change management and is recorded in the operations log.

### 12.2 Failure Mode Matrix

| Component failure | Detection | User impact | Behaviour | Recovery |
|-------------------|-----------|-------------|-----------|----------|
| Availability zone loss | Health checks, AZ metrics | None if Multi-AZ healthy | Load balancer drains the zone; database fails over | Automatic; verify within 15 min |
| PostgreSQL primary failure | Connection errors, failover event | 30–90 s of write errors | Managed failover promotes the standby; clients retry | Automatic; RPO ≤15 min |
| Read replica lag | Lag metric >30 s | Dashboards may be stale | Route reads to primary above threshold | Automatic |
| Redis loss | Connection errors | Scope and cache misses; rate limits degrade to in-memory | Rebuild cache; fail closed on revocation lookups | Automatic within 5 min |
| Object storage degradation | Upload and GET error rate | Evidence upload blocked; existing evidence still served if cached | Queue local drafts; show retry | Provider recovery |
| Malware scanner unavailable | Scan queue age | New evidence `PENDING_SCAN`, not served | Hold and notify | Resume scan; no bypass |
| Email provider outage | Delivery failures | External email delayed; in-app unaffected | Retry and secondary provider | Provider recovery |
| FX source outage | Scheduled pull failure | Submission uses latest rate marked `FALLBACK` | Continue with fallback | Manual override available |
| Outbox dispatcher stopped | Oldest event age | Dashboards and notifications lag | Restart; workers claim backlog | Replay idempotent |
| Worker backlog | Queue age SLO breach | Reports and imports delayed | Autoscale; prioritize interactive queue | Drain |
| Full region loss | All regional probes fail | Tenant unavailable | Restore from region-local backups into a new region stack | RTO ≤4 h, RPO ≤15 min |
| KMS unavailable | Encryption calls fail | Uploads and secret reads fail; existing sessions continue | Fail closed for new writes | Provider recovery |
| Bad deployment | Error rate spike within 10 min | Partial or total outage | Automatic rollback to previous task definition | ≤15 min |
| Data corruption | Reconciliation or audit verification failure | Affected reports untrusted | Freeze affected module; restore to point in time | RTO depends on scope |
| Insider database alteration | Audit chain verification fails | Audit evidence suspect | Freeze exports; forensic snapshot; rotate credentials | Investigation |
| Quota exhaustion | Storage or row limits | New uploads rejected | Alert and expand; per-tenant quotas prevent cross-tenant impact | Capacity action |

### 12.3 Degradation Ladder

| Mode | Trigger | Behaviour | User message |
|------|---------|-----------|--------------|
| `NORMAL` | All dependencies healthy | Full functionality | — |
| `STALE_INTEL` | Projection lag >5 min | Dashboards serve last aggregate with a freshness warning | "Data as of HH:MM; refreshing" |
| `READ_ONLY` | Primary database in failover or write path impaired | Reads served; writes return `503` with `Retry-After`; mobile queues locally | "Temporarily read-only; your work is saved on this device" |
| `CAPTURE_ONLY` | API write path impaired but sync host and storage healthy | Capture and sync accept drafts; approvals and reports disabled | "Capture available; approvals paused" |
| `MAINTENANCE` | Planned window | Read-only or unavailable per announcement | Banner with end time |

**Design rule.** Capture never depends on dashboards, notifications, or reports. A failure in analytics must not stop an auditor from recording an expense (AD-04, NFR-AVL-004).

### 12.4 Multi-AZ Topology

| Layer | MVP | GA |
|-------|-----|----|
| Edge | CDN with global anycast, WAF, TLS | Same plus regional failover |
| API and sync | Tasks across ≥2 AZs behind one load balancer | Same, with ≥3 AZs |
| Workers | Tasks across ≥2 AZs; queue claims are safe from any AZ | Same |
| PostgreSQL | Multi-AZ managed instance with automatic failover | Multi-AZ plus read replica in a third AZ |
| Redis | Primary plus replica in a different AZ | Cluster mode across AZs |
| Object storage | Regional, replicated by the provider across AZs | Same plus versioning and Object Lock |
| Telemetry | Regional collector, buffered and retried | Same plus cross-AZ collector |

**No single-AZ dependency.** A task must not hold local state required to complete a request. Uploads go directly to object storage; jobs are claimed from the database; sessions are validated against the database and Redis.

### 12.5 Backup and Restore

| Control | Design |
|---------|--------|
| PITR | Continuous WAL archiving; restore to any second within 35 days |
| Snapshots | Daily automated snapshot at 02:00 tenant-local off-peak; 35-day retention |
| Object storage | Versioning enabled; delete markers retained; Object Lock for audit archives |
| Configuration | Infrastructure as code; secrets recoverable from the secret manager backup |
| Restore isolation | Restores go to a new instance, are verified, then promoted; never restore over production |
| Verification | Row counts, checksum of financial totals, audit chain verification, and a smoke test before promotion |
| Evidence | Restore report retained as SOC 2 evidence (NFR-DAT-002) |

**Restore sequence.**

1. Declare the incident and freeze writes.
2. Provision a new database instance from PITR at the last known good timestamp.
3. Restore object storage references; verify a sample of evidence hashes.
4. Run reconciliation: expense totals, budget versions, audit chain continuity.
5. Promote the instance, update the connection secret, and restart API and worker tasks.
6. Confirm capture, approval, dashboard, and report smoke tests.
7. Record actual RTO and RPO; publish a post-incident review.

### 12.6 Regional Disaster Recovery

The residency decision (D-013, AD-08) forbids replicating tenant data to another region. DR is therefore **restore-based within the tenant's region**, not active-active across regions.

| Aspect | Design |
|--------|--------|
| Scope | Region-level loss (all AZs), not AZ loss |
| Strategy | Backup and restore into a pre-provisioned recovery stack in the same region |
| RTO | ≤4 hours (NFR-AVL-006) |
| RPO | ≤15 minutes (NFR-AVL-007) |
| Prerequisites | IaC stack ready, secrets replicated within region, DNS switch prepared, runbook tested |
| Frequency | Quarterly full exercise; monthly restore spot check |
| Evidence | Timed runbook, restore verification report, action items |
| Tenant communication | Status page and direct notice for pilot tenants; RTO stated in the DPA |

**Why not cross-region warm standby.** A warm standby in another region would violate the residency guarantee unless the second region is in the same legal jurisdiction, and even then it doubles cost for a pilot. The MVP accepts a 4-hour restore target; Phase 2 may add an in-jurisdiction secondary region for enterprise tenants.

### 12.7 Business Continuity for the Pilot

| Scenario | Manual fallback |
|----------|-----------------|
| Platform unavailable >4 h | Sites continue capture on the offline mobile app; desktop users use the Excel template |
| Sync host unavailable | Devices retain the queue; no data loss; capture continues |
| Reports unavailable | Finance uses the last generated report plus the Excel export from the previous period |
| Email unavailable | In-app notifications and direct operator calls |
| Audit access needed during outage | Read-only restored copy in an isolated environment with break-glass approval |

The pilot runbook names an on-call owner, a customer contact, and a decision threshold for invoking the fallback. Document 17 owns the operational detail.

### 12.8 Chaos and Game-Day Testing

| Experiment | Frequency | Success criterion |
|------------|-----------|-------------------|
| Kill an API task during peak | Monthly | No failed request beyond the retry budget |
| Fail over PostgreSQL primary | Quarterly | Writes resume ≤90 s; no committed data lost |
| Remove Redis | Quarterly | Revocation still enforced; cache rebuilds |
| Stop the outbox dispatcher for 30 min | Quarterly | Backlog drains; no duplicate effects |
| Make the scanner unavailable | Quarterly | No file served unscanned; queue recovers |
| Restore a full region from backup | Quarterly | RTO ≤4 h; RPO ≤15 min; verification passes |
| Corrupt an audit row in a test copy | Quarterly | Verification detects it and alerts (QA-11) |
| Revoke a grant mid-session | Per release | Access blocked within 5 min (QA-05) |
| Deploy a bad build | Per release drill | Automatic rollback ≤15 min |

### 12.9 Data Integrity Under Failure

| Mechanism | Guarantee |
|-----------|-----------|
| Single transaction for business change, audit, and outbox | No change without an audit record or an event |
| Idempotent consumers | Replay after a crash produces the same state |
| Reconciliation job | Detects divergence between source records and projections nightly |
| Audit chain verification | Detects tampering or truncation daily |
| Unique constraints on business keys | Prevents duplicate expenses, approvals, and grants |
| Optimistic concurrency | Prevents lost updates on financial records |
| Immutable approved versions | A failure cannot silently rewrite an approved budget |
| Dead-letter review | Nothing is dropped without an operator-visible record |

### 12.10 Incident Management Handoff

This document defines **what** must survive a failure; Document 17 defines **how** it is operated.

| Artifact | Owner | Consumed by |
|----------|-------|-------------|
| SLOs and error budget policy | Architecture | Doc 17 |
| Failure matrix and degradation modes | Architecture | Doc 17 |
| Restore sequence and acceptance criteria | Architecture and Data | Doc 17 |
| Alert thresholds and routing | Architecture | Doc 17 |
| Post-incident review template | Security and Operations | Doc 17 |

---

## 13. Security Architecture

<!-- CHUNK-5 -->

## 13. Security Architecture

### 13.1 Security Principles

Security is a property of the architecture, not a layer added later. Controls are specified in Document 7; this section fixes the architectural commitments that Document 7 elaborates.

| # | Commitment | Realizes |
|---|------------|----------|
| SC-1 | Tenant isolation is enforced at the database (RLS) and at every application entry point | NFR-SEC-001, NFR-TNT |
| SC-2 | Authentication is delegated to a standards-based IdP; the platform never stores passwords | NFR-SEC-002 |
| SC-3 | Authorization is evaluated server-side from a versioned permission model; no client-side trust | NFR-SEC-003 |
| SC-4 | All data in transit uses TLS 1.2+; all data at rest uses AES-256 | NFR-SEC-004 |
| SC-5 | Secrets are managed by a vault; none are in source or configuration files | NFR-SEC-005 |
| SC-6 | Evidence files are stored with server-side encryption and accessed only through signed, time-limited URLs | NFR-SEC-006 |
| SC-7 | Security-relevant events are captured in an append-only security audit log | NFR-SEC-007, CAP-023 |
| SC-8 | Tenant data residency is configurable per organization and enforced by region | NFR-SEC-008 |
| SC-9 | Vulnerability scanning and dependency review gate every release | NFR-SEC-009 |
| SC-10 | Break-glass access is provisioned, time-limited, and audited | NFR-SEC-010 |

### 13.2 Threat Model (STRIDE summary)

| Threat | Target | Mitigation | Owner |
|--------|--------|------------|-------|
| Spoofing | User identity | MFA, SSO, short-lived tokens, refresh rotation | Identity service |
| Tampering | Financial records | Append-only versions, optimistic concurrency, audit chain | Budget/Expense modules |
| Repudiation | Approvals and grants | Immutable audit events with actor, time, and correlation ID | Audit module |
| Information disclosure | Cross-tenant data | RLS, scoped queries, signed URLs, field-level masking | All modules |
| Denial of service | APIs and sync | Rate limits, quotas, autoscaling, WAF | Platform |
| Elevation of privilege | Role/permission changes | Server-side evaluation, review workflow, audit | Identity service |

### 13.3 Tenant Isolation Architecture

- **Database**: PostgreSQL Row-Level Security on every tenant-scoped table; `org_id` is a non-nullable column on all such tables; policies filter by `current_setting('app.current_org_id')`.
- **Application**: a tenant context is resolved from the authenticated session and injected into every request; repository interfaces require an `org_id` parameter; a lint rule forbids queries without a tenant predicate.
- **Storage**: evidence objects are keyed by `org_id` prefix; signed URLs are scoped to a single object and expire within 15 minutes.
- **Cache**: cache keys include `org_id`; no shared cached value spans tenants.
- **Search**: index partitions or filtered queries by `org_id`; results are filtered server-side.
- **Jobs**: background jobs carry `org_id` in their payload and re-establish tenant context before execution.

### 13.4 Identity and Authentication

- Authentication is delegated to Keycloak (self-hosted) or an external IdP via OIDC/SAML; MFA is mandatory for privileged roles.
- Access tokens are JWT, 15-minute TTL, signed with RS256; refresh tokens are rotated on use and stored in HTTP-only cookies (web) or encrypted keystore (mobile).
- Revocation uses a Redis blacklist for `jti` with TTL equal to remaining access-token lifetime; organization suspension revokes all sessions in the tenant.
- Service-to-service calls use mTLS and short-lived tokens issued by the platform.

### 13.5 Authorization

- Permissions follow the `{resource}:{action}:{scope}` model (Document 4); scopes are `ORG`, `SHOW`, `DEPT`, `ASSIGNMENT`.
- Evaluation is server-side; the client receives only the resulting allowed actions for the current resource.
- Grants are versioned and auditable; quarterly review is required (NFR-SEC review cycle).
- Cross-organization grants (auditor access) are time-boxed and scoped to an assignment.

### 13.6 Data Protection

- Encryption at rest: AES-256 for database volumes, object storage, and backups.
- Encryption in transit: TLS 1.2+ everywhere, including internal service calls.
- Field-level protection: PII and financial identifiers are masked in logs and non-production environments.
- Key management: per-tenant data-encryption keys wrapped by a KMS; rotation is supported without re-encrypting all data.
- Secrets: stored in HashiCorp Vault; injected at runtime; never committed.

### 13.7 Evidence and Document Security

- Uploads are scanned for malware before becoming available; unscanned files are never served.
- File type and size are validated server-side; magic-number checks supplement extension checks.
- Access is gated by the permission model and produces an access-log entry.
- Retention and legal hold are enforced by policy; deletion is blocked while a hold is active.

### 13.8 Network and Edge Security

- WAF at the edge with OWASP ruleset and rate limiting.
- DDoS protection via the cloud provider.
- Private subnets for data and compute; public exposure limited to the edge and load balancers.
- Egress allow-list for integration adapters; no arbitrary outbound fetches.

### 13.9 Secure Development

- SAST and dependency scanning in CI; critical/high findings block release.
- Container image scanning before deployment.
- Secret scanning in repositories.
- Security review required for changes touching identity, authorization, or financial arithmetic.

---
### 13.1 Security Objectives

| Objective | Requirement source | Architectural commitment |
|-----------|--------------------|--------------------------|
| Tenant isolation that fails closed | NFR-SEC-004, NFR-SEC-005, TC-03 | RLS on every tenant table; no application role with `BYPASSRLS`; continuous isolation suite |
| Least-privilege access | NFR-SEC-003, FR-CMP-013 | Permission catalog plus scope plus grant; MFA for privileged and external users |
| Tamper-evident accountability | NFR-SEC-012, NFR-AUD-001–005 | In-transaction audit, per-tenant hash chain, WORM anchors, daily verification |
| Confidentiality of financial and personal data | NFR-SEC-001, NFR-SEC-002, NFR-SEC-014 | TLS 1.2+, AES-256 at rest, field-level encryption for secrets, region-pinned keys |
| Safe file handling | NFR-SEC-007, NFR-SEC-008 | Quarantine, scan-before-serve, type allow-list, short-lived signed URLs |
| Secure delivery | NFR-SEC-009, NFR-SEC-010 | ASVS Level 2 baseline, layered scanning, severity SLAs, signed artifacts |
| Privacy by design | NFR-PRV-001–010 | Minimization, purpose limitation, DSR tooling, residency, retention with legal-hold precedence |
| Operable assurance | NFR-OBS-005, NFR-PRV-004 | Every administrative action audited; controls instrumented from MVP for SOC 2 |

**Control frameworks.** OWASP ASVS 4.x Level 2 (NFR-SEC-009), OWASP Top 10 as a design checklist, CIS benchmarks for containers and database configuration, and the SOC 2 Trust Services Criteria as the control taxonomy. Document 7 owns the detailed control catalogue and test procedures; this section defines the mechanisms those controls rely on.

### 13.2 Trust Boundaries

```
 TB-01 Internet (untrusted)
   │
   ▼
┌──────────────────────────────────────────────────────────────┐
│ TB-02 Edge: CDN, WAF, TLS, DDoS protection, rate limiting    │
<!-- CHUNK-6 -->

## 14. Observability

### 14.1 Telemetry Standards

- **Logs**: structured JSON with `timestamp`, `level`, `service`, `org_id` (hashed), `trace_id`, `span_id`, `message`, and contextual fields. No PII or raw financial amounts in logs.
- **Metrics**: Prometheus-compatible; RED (Rate, Errors, Duration) for services, USE (Utilization, Saturation, Errors) for resources.
- **Traces**: OpenTelemetry, sampled at 10% baseline with 100% sampling for errors and slow requests.
- **Correlation**: every request carries a `trace_id` propagated across services, jobs, and events.

### 14.2 SLOs (architectural view)

| SLO | Target | Measurement |
|-----|--------|-------------|
| API availability | 99.5% MVP / 99.9% Phase 1 | Synthetic + RUM |
| API latency P95 (CRUD) | ≤500 ms | APM |
| Dashboard load P95 | ≤2 s | RUM |
| Sync success rate | ≥99.9% | Sync telemetry |
| Alert evaluation latency (critical) | ≤5 min | Alert telemetry |
| Evidence upload throughput | ≥1 MB/s | Network test |

### 14.3 Health and Readiness

- Every service exposes `/health` (liveness) and `/ready` (readiness); `/ready` reflects dependency connectivity.
- Kubernetes probes use these endpoints; traffic is routed only to ready instances.
- Graceful shutdown drains in-flight requests within 30 seconds.

### 14.4 Dashboards and Alerts

- Per-service and per-tenant dashboards; tenant views show aggregates only (NFR-OBS-004).
- Alerts route by severity to on-call; critical alerts page within 5 minutes (NFR-OBS-002).
- Integration, sync, and scan queues have dedicated dashboards.

### 14.5 Audit and Compliance Observability

- Business and security audit logs are queryable by authorized roles with correlation IDs.
- Anomaly detection runs on authentication and permission events.
- Retention and archival jobs report success/failure and lag.

---

## 15. Deployment & Environments

### 15.1 Environment Strategy

| Environment | Purpose | Data | Refresh |
|-------------|---------|------|---------|
| Local | Developer | Synthetic | On demand |
| Dev | Integration | Synthetic + scrubbed | Continuous |
| Staging | Pre-release validation | Scrubbed prod-like | Weekly |
| Production | Live tenants | Real | N/A |

### 15.2 Release Topology

- **MVP**: single region, multi-AZ, modular monolith container plus managed PostgreSQL, Redis, object storage, and message broker.
- **Phase 1**: add read replicas, CDN, and a second region for DR.
- **Phase 2**: extract hot modules (sync, reporting) into separate services if triggers are met (§4.5).

### 15.3 CI/CD Pipeline

| Stage | Gate |
|-------|------|
| Build | Unit tests, lint, type check, SAST |
| Test | Integration tests, contract tests, migration dry-run |
| Security | Dependency scan, image scan, secret scan |
| Deploy (staging) | Automatic on main branch |
| Deploy (production) | Manual approval + canary + rollback |
| Post-deploy | Smoke tests, SLO check, migration verification |

### 15.4 Database Migrations

- Migrations are forward-only, reviewed, and tested against a production-size copy.
- Backward-compatible changes only in a release; breaking changes require a two-phase migration.
- Rollback is by forward-fix; destructive migrations are gated by a manual approval.

### 15.5 Configuration Management

- Configuration is environment-specific and stored outside the codebase.
- Feature flags control rollout and tenant entitlements.
- No tenant-specific code branches; variation is by configuration (NFR-MNT-004).

### 15.6 Infrastructure as Code

- All infrastructure is defined as code and reviewed; drift is detected and corrected.
- Secrets are injected at runtime from the vault.

---
└───────────────────────────┬──────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ TB-03 Application plane: API, Sync, Worker (private subnets) │
│ workload identity · no inbound internet · egress allow-list  │
└───────┬───────────────────────────────┬──────────────────────┘
        │                               │
        ▼                               ▼
┌───────────────────────┐      ┌───────────────────────────────┐
│ TB-04 Data plane      │      │ TB-05 External providers      │
│ PostgreSQL, Redis,    │      │ email, FX, scanner, storage,  │
│ object storage, KMS   │      │ IdP, ERP, webhooks            │
│ no public endpoints   │      │ signed or authenticated calls │
└───────────────────────┘      └───────────────────────────────┘

Logical boundaries:
 TB-06 Tenant ↔ tenant        enforced by RLS + tenant context
 TB-07 Auditor home org ↔ client org   enforced by grant scope
 TB-08 Operator ↔ production  break-glass, recorded, time-bound
 TB-09 Device ↔ server        untrusted device, encrypted local store
```

<!-- CHUNK-7 -->

## 16. Technology Risk Register

| ID | Risk | Likelihood | Impact | Mitigation | Owner |
|----|------|------------|--------|------------|-------|
| TR-001 | Offline sync conflict complexity exceeds estimate | Medium | High | Prototype conflict matrix early; ship conservative merge rules | Platform |
| TR-002 | PostgreSQL RLS performance at 500 tenants / 5M expenses | Medium | High | Partition by org and date; load test before GA | Data |
| TR-003 | Mobile offline storage limits on low-end devices | Medium | Medium | Cap local cache; evict read-only data; test on mid-range devices | Mobile |
| TR-004 | Evidence scan queue becomes a bottleneck | Low | Medium | Autoscale scanner; hold-and-serve policy | Platform |
| TR-005 | Integration provider API changes break adapters | Medium | Medium | Contract tests; version pinning; adapter isolation | Integration |
| TR-006 | Keycloak operational burden | Medium | Medium | Managed IdP option; runbooks; Phase 1 evaluation | Security |
| TR-007 | Excel import parsing edge cases | High | Medium | Fuzz testing; strict validation; clear error reporting | Integration |
| TR-008 | Cost of multi-region DR exceeds budget | Medium | Medium | Pilot single-region with documented RTO; revisit at Phase 1 | Finance/Platform |
| TR-009 | Audit chain verification false positives | Low | High | Deterministic hashing; staged rollout; manual review path | Compliance |
| TR-010 | Vendor lock-in on cloud provider | Low | Medium | Prefer portable services; IaC; documented migration | Architecture |
| TR-011 | Talent availability for chosen stack | Low | Medium | Prefer mainstream technologies; document decisions | Architecture |
| TR-012 | Performance regression from observability overhead | Low | Low | Sampling; budget for telemetry cost | Platform |

---

## 17. Architecture Decision Records

### ADR-001 — Modular monolith for MVP

- **Status:** Accepted
- **Context:** Need to ship MVP within budget while preserving future extraction.
- **Decision:** Ship a single deployable with bounded-context modules; extract only on trigger.
- **Consequences:** Simpler ops and transactions; requires module discipline; extraction later.

### ADR-002 — PostgreSQL as primary data store

- **Status:** Accepted
- **Context:** Financial data requires ACID, referential integrity, partitioning, and point-in-time recovery.
- **Decision:** PostgreSQL with RLS for tenant isolation; read replicas for reporting.
- **Consequences:** Operational familiarity; scaling via partitioning and replicas.

### ADR-003 — React and React Native for clients

- **Status:** Accepted
- **Context:** Need web and mobile with shared component model and offline support.
- **Decision:** React (web) and React Native (mobile) with shared design tokens.
- **Consequences:** Single frontend skill set; native modules where needed.

### ADR-004 — Offline sync via outbox and CRDT-lite merge

- **Status:** Accepted
- **Context:** Auditors work offline for up to 7 days; conflicts must resolve deterministically.
- **Decision:** Event-sourced local outbox; server-side merge with a documented conflict matrix.
- **Consequences:** Deterministic reconciliation; complexity in conflict handling.

### ADR-005 — S3-compatible object storage for evidence

- **Status:** Accepted
- **Context:** Evidence volume up to 5 TB total; signed URLs and retention required.
- **Decision:** S3-compatible storage with server-side encryption and lifecycle policies.
- **Consequences:** Scalable and cost-effective; vendor API dependency.

### ADR-006 — Transactional outbox with message broker

- **Status:** Accepted
- **Context:** Integrations must be idempotent, retried, and observable.
- **Decision:** Outbox table in the same transaction as business changes; broker dispatches with retry and DLQ.
- **Consequences:** Reliable delivery; operational overhead for broker.

### ADR-007 — Keycloak for identity (provisional)

- **Status:** Provisional
- **Context:** Need standards-based auth with MFA and SSO; managed option to be evaluated.
- **Decision:** Keycloak self-hosted for MVP; revisit managed IdP at Phase 1.
- **Consequences:** Operational burden; full control.

### ADR-008 — Redis for caching and revocation

- **Status:** Accepted
- **Context:** Need low-latency revocation and caching.
- **Decision:** Redis cluster; blacklist for token revocation; cache with tenant-scoped keys.
- **Consequences:** Additional component; graceful degradation if unavailable.

### ADR-009 — OpenSearch for search

- **Status:** Accepted
- **Context:** Full-text search across expenses, vendors, and documents with tenant isolation.
- **Decision:** OpenSearch with tenant-filtered indices.
- **Consequences:** Operational overhead; strong search capabilities.

### ADR-010 — Object storage lifecycle for retention

- **Status:** Accepted
- **Context:** Retention and legal hold must be enforced.
- **Decision:** Lifecycle policies plus legal-hold flags blocking deletion.
- **Consequences:** Compliance-aligned; requires policy administration.

---
| Boundary | Crossing control |
|----------|------------------|
| TB-01 → TB-02 | TLS 1.2+, WAF managed rules, DDoS absorption, IP reputation, bot controls |
| TB-02 → TB-03 | Only published routes; request size limits; authentication before business logic |
| TB-03 → TB-04 | Private networking, security groups, IAM auth, TLS to the database, no shared credentials |
| TB-03 → TB-05 | Egress allow-list per adapter, TLS, signed requests, timeout and circuit breaker |
| TB-06 | RLS, tenant context, isolation test suite, no cross-tenant joins |
| TB-07 | `EffectiveScope`, grant predicate, access-location audit, 5-minute revocation |
| TB-08 | Break-glass approval, session recording, no standing production access, post-review |
| TB-09 | Device registration, token binding, SQLCipher, remote wipe, certificate pinning |

### 13.3 Identity, Authentication, and Session Security

| Control area | Design |
|--------------|--------|
| Human identity | Built-in identity at MVP; OIDC/SAML federation at Phase 1 (INTG-06) |
| Account lifecycle | Invitation, verification, activation, deactivation, erasure; deactivation revokes sessions immediately |
| Password policy | Minimum 12 characters, breach-list check, no forced composition rules, Argon2id hashing |
| MFA | TOTP at MVP, WebAuthn at Phase 1; mandatory for Org Admin, Finance Head, Audit Manager, Platform Ops, and all external auditors (NFR-SEC-003) |
| Recovery | Single-use recovery codes; recovery resets are audited and trigger a security notification |
| Access token | JWT, 15 minutes, asymmetric signing, `kid` rotation, no sensitive claims |
| Refresh token | Opaque 256-bit, hashed at rest, 14 days, rotation, reuse detection revokes the family |
| Session binding | Web sessions bound to a device fingerprint and IP class; mobile to `device_id`; step-up on anomaly |
| Revocation | Session registry plus Redis denylist; grant revocation propagates within 5 minutes (TC-07) |
| Service identity | Workload identity from the container platform; short-lived credentials; no static cloud keys |
| Break-glass | Named accounts, hardware-key MFA, time-bound elevation, full session recording, mandatory review within 24 hours |
| Directory hygiene | Quarterly access review, leavers within 24 hours, privileged access recertified quarterly (FR-CMP-013) |

**Authentication abuse controls.** Per-account and per-IP throttling, progressive delay after 5 failures, CAPTCHA only after repeated failures, credential-stuffing detection on distributed IP patterns, and alerts on impossible-travel and new-device anomalies. Lockout is time-based, not permanent, to avoid a denial-of-service against a legitimate user.

### 13.4 Authorization Enforcement

| Layer | Control | Failure behaviour |
|-------|---------|-------------------|
| Route | Authentication required by default; explicit `AllowAnonymous` reviewed in code review | `401` |
| Endpoint filter | Permission code plus scope check | `403` with stable code |
| Handler | Resource-level ownership, state, period lock, segregation of duties | `403` or `409` |
| Query | Scope predicate applied even when RLS exists (defence in depth) | Empty result |
| Database | RLS with tenant and grant predicate; application role has no bypass | Zero rows |
| Object storage | Signed URL bound to tenant prefix and content hash | `403` from storage |
| Export and report | Scope re-evaluated at generation, not only at request | Job fails with audit event |

**Authorization invariants tested on every build.**

1. A user of tenant A receives zero rows for tenant B on every tenant table.
2. An external auditor with a grant for Show A receives zero rows for Show B, including via search, export, report, and dashboard.
3. A revoked grant is ineffective within 5 minutes across all API replicas.
4. A role cannot grant a permission its author does not hold, and cannot exceed the organization boundary.
5. A user cannot approve their own submission or a submission they created a budget line for.
6. A deactivated user cannot refresh a token or complete an in-flight sync batch.
7. A signed URL cannot be reused after expiry or by a different tenant.

### 13.5 STRIDE Analysis

| ID | STRIDE | Threat scenario | Boundary | Primary controls | Verification | Residual |
|----|--------|-----------------|----------|------------------|--------------|----------|
| T-01 | Spoofing | Credential stuffing against built-in login | TB-01, TB-02 | MFA, breach-list, throttling, anomaly detection | Auth test, red team | Medium until federation at Phase 1 |
| T-02 | Spoofing | Stolen refresh token replayed on another device | TB-09 | Rotation, reuse detection, device binding, session revocation | Token replay suite | Low |
| T-03 | Spoofing | Forged service or workload identity | TB-03 | Platform workload identity, short-lived credentials, no static keys | IAM configuration audit | Low |
| T-04 | Spoofing | Forged outbound webhook received by a customer | TB-05 | HMAC-SHA256 signature, timestamp, `event_id`; documented verification | Contract test | Low |
| T-05 | Tampering | Client alters amounts, totals, or status before sync | TB-09 | Server recomputes totals, decimal validation, state machine, audit | Property-based tests | Low |
| T-06 | Tampering | Insider with database credentials edits a financial row | TB-08 | No standing write access, break-glass, audit chain, reconciliation | Quarterly access review | Medium |
| T-07 | Tampering | Historical audit rows altered or deleted | TB-04 | Append-only role, hash chain, WORM anchors, daily verification | Tamper simulation (QA-11) | Low |
| T-08 | Tampering | Evidence object replaced in storage | TB-04 | SHA-256 binding, versioning, Object Lock for audit copies | Hash comparison test | Low |
| T-09 | Repudiation | User denies submitting or approving an expense | All | Immutable audit with actor, time, IP, device, correlation ID | Audit completeness test | Low |
| T-10 | Repudiation | Administrator denies a role or grant change | TB-08 | Audit event plus access review evidence | Control test | Low |
| T-11 | Disclosure | Cross-tenant read by manipulating an identifier | TB-06 | RLS, object-level authorization, uniform `404` or `403` | Isolation suite per release | Low |
| T-12 | Disclosure | Grant scope escape to an out-of-scope show | TB-07 | `EffectiveScope`, show filter, RLS, access-location log | QA-04 scenario | Low |
| T-13 | Disclosure | Signed evidence URL shared outside the organization | TB-02 | 15-minute TTL, single object, access log, no public bucket | URL expiry test | Medium (human behaviour) |
| T-14 | Disclosure | Error responses reveal schema, SQL, or existence | TB-02 | ProblemDetails with stable codes; no stack traces | Error review, DAST | Low |
| T-15 | Disclosure | Logs or traces contain amounts, tokens, or personal data | TB-03 | Allow-list logging, redaction, PII tests, no payload logging | Log inspection in CI | Low |
| T-16 | Disclosure | Backup or key copied outside the tenant's region | TB-04 | Region-pinned KMS and buckets; policy denies cross-region copy | Infrastructure audit | Low |
| T-17 | Disclosure | Lost or stolen mobile device exposes local data | TB-09 | SQLCipher, keystore keys, biometric gate, remote wipe | Device security test | Medium until wipe-on-launch confirmed |
| T-18 | Denial of service | API flood or expensive report requests exhaust capacity | TB-01, TB-02 | WAF, per-tenant and per-user limits, async reports, queue isolation | Load and abuse test | Medium |
| T-19 | Denial of service | ZIP bomb or 50,000-row workbook exhausts a worker | TB-03 | Size and row caps, streaming parse, memory limit, dedicated pool | Malicious file test | Low |
| T-20 | Denial of service | Duplicate sync replay storm after a network flap | TB-09 | `mutation_id` dedupe, batch limits, per-device rate limit | Replay test | Low |
| T-21 | Elevation | Custom role composes permissions beyond the author's own | TB-03 | Permission allow-list, subset rule, approval, audit | Role escalation test | Low |
| T-22 | Elevation | Auditor creates or extends their own grant | TB-07 | Grant creation restricted to client admins; self-grant denied; SoD check | Policy test | Low |
| T-23 | Elevation | Break-glass used for convenience rather than incident | TB-08 | Time-bound, reason required, recording, 24-hour review, alert | Access review | Medium |
| T-24 | Elevation | Supply-chain compromise of a base image or package | TB-03 | Signed images, SBOM, Trivy, pinned digests, dependency review | CI supply-chain gate | Medium |

**Threat model maintenance.** The model is reviewed at each major release and whenever a new trust boundary, external integration, or data class is introduced. Findings are tracked in the risk register (§16) or the security backlog; accepted residual risks require Security & Compliance sign-off recorded in §19.

### 13.6 Cryptographic Design

| Purpose | Algorithm | Key management |
|---------|-----------|----------------|
| Transport | TLS 1.2 minimum, TLS 1.3 preferred; AEAD cipher suites only | Managed certificates, 90-day rotation, HSTS |
| Data at rest | AES-256-GCM or provider-equivalent | Per-region CMK, per-tenant data key, envelope encryption |
| Field-level secrets | AES-256-GCM with authenticated context | Separate key, restricted decrypt permission |
| Password storage | Argon2id, per-user salt, memory 64 MB, iterations 3, parallelism 4 | Parameters versioned for rehash on login |
| Integrity | SHA-256 for content and audit chain | No secret; chain anchored to WORM |
| Webhook signing | HMAC-SHA256 | Per-endpoint secret, rotated on demand |
| Tokens | 256-bit CSPRNG; opaque refresh token hashed with SHA-256 | Never logged; stored hashed |
| Mobile local store | SQLCipher AES-256 | Key in iOS Keychain or Android Keystore |
| Backup encryption | Provider-managed AES-256 with region CMK | Key never exported |

**Rules.** No custom cryptography. No static IVs. No secrets in environment variables when a secret manager is available. Key rotation is annual or on suspected compromise. Tenant deletion performs **crypto-shredding** of the tenant data key after lawful erasure, in addition to row and object deletion.

### 13.7 Network and Infrastructure Security

| Layer | Control |
|-------|---------|
| Edge | WAF managed plus custom rules, DDoS protection, geo/IP reputation, TLS termination, request size limits |
| Public ingress | Only the load balancer; no public database, cache, or object bucket |
| Private subnets | API, sync, worker, and renderer run in private subnets with no inbound internet route |
| Egress | Allow-list per adapter; DNS resolution restricted; no arbitrary outbound calls |
| Segmentation | Separate security groups for edge, application, data, and renderer; renderer has no database route |
| Administrative access | No bastion; session-managed access with MFA, time-bound role assumption, full recording |
| Container hardening | Non-root, read-only root filesystem, no privileged mode, seccomp profile, minimal base image |
| Host and runtime | Managed platform patching; image rebuild at least monthly and within 7 days of a critical CVE |
| Database | Private endpoint, IAM or rotating credential auth, TLS required, no superuser for the app |
| Storage | Block public access, TLS-only policy, versioning, Object Lock for audit |
| DNS | Registrar lock, DNSSEC where supported, MFA on the registrar, monitored record changes |

### 13.8 Application Security Controls

| Risk | Control |
|------|---------|
| Injection | Parameterized SQL only; ORM or Dapper with parameters; no string concatenation; `EXPLAIN` and code review |
| Broken access control | Deny by default, central policy, RLS, object-level checks, isolation tests |
| Authentication failures | Argon2id, MFA, throttling, session rotation, secure cookies |
| Insecure design | Threat model per feature, abuse-case review, SoD encoded in the policy engine |
| Security misconfiguration | IaC with policy checks, no default credentials, hardening baselines, CSP, security headers |
| Vulnerable components | SCA on every build, Renovate, license allow-list, severity SLAs |
| Identification failures | Stable audit identity, no shared accounts, service accounts inventoried |
| Integrity failures | Signed images, SBOM, lockfiles, immutable artifacts, migration review |
| Logging failures | Structured security events, alerting, tamper-evident audit, no sensitive payloads |
| SSRF | No arbitrary URL fetch; webhook and integration hosts validated; metadata endpoints blocked |
| XSS | React escaping by default; no raw HTML for user content; strict CSP; Trusted Types where supported |
| CSRF | `SameSite` cookies, double-submit token, origin check on state-changing requests |
| File upload | Type allow-list, size caps, hash binding, quarantine, scan before serve, no execution |
| Deserialization | `System.Text.Json` with explicit types; no polymorphic deserialization of untrusted input |
| XXE | XML parsing disabled for external entities; XLSX parsed with a safe reader |
| Open redirect | No user-controlled redirect targets; allow-list for post-login navigation |
| Clickjacking | `frame-ancestors 'none'`; no embedding of authenticated pages |
| Cache leakage | `Cache-Control: no-store` on authenticated responses; no shared proxy caching |

**Security headers.** HSTS with preload, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy` minimised, and a CSP with nonce-based scripts and no `unsafe-eval`.

### 13.9 Data Protection and Privacy Engineering

| Obligation | Mechanism |
|------------|-----------|
| Lawful basis and notice (GDPR, DPDP) | Consent and notice records at organization and user level; versioned privacy notice |
| Minimization (NFR-PRV-005) | Field-level data inventory; collection justified per field; no free-text collection of sensitive data |
| Purpose limitation | Data classified by purpose; benchmark data aggregated and k-anonymized (≥5 organizations) |
| DSR access and portability (NFR-PRV-006) | DSR workflow exports the subject's data in a structured bundle within 30 days |
| Erasure | Erasure workflow with legal-hold and retention precedence; crypto-shredding when applicable |
| Breach notification (NFR-PRV-007) | Incident workflow with 72-hour regulatory clock, notification templates, and evidence log |
| Retention (NFR-PRV-008) | Policy-driven retention with legal hold; destructive steps require approval in year one |
| Residency (NFR-PRV-009) | Region-pinned stack, keys, backups, and telemetry; no cross-region tenant replication |
| Subprocessors (NFR-PRV-010) | Register with purpose, location, DPA reference, and notice history |
| DPIA | Required for new processing, new region, or new sensitive data class; Doc 7 owns the template |

**Personal data in telemetry.** Traces carry `org_id` and `user_id` only as opaque identifiers. Names, emails, amounts, and evidence content are prohibited. Logs use allow-listed fields; an automated test scans a sample of staging logs for patterns resembling emails, card numbers, and large decimals.

### 13.10 Audit and Non-Repudiation

| Element | Design |
|---------|--------|
| Coverage | Authentication, authorization changes, financial writes, approvals, grants, exports, document access, configuration, retention, break-glass |
| Write path | `IAuditWriter` inside the business transaction; failure aborts the transaction |
| Chain | Per-tenant SHA-256 chain with a locked head row and daily WORM anchor |
| Verification | Daily job recomputes the chain and compares anchors; mismatch pages security |
| Search | 90-day window ≤5 seconds (NFR-AUD-005) through partitioned indexes and tenant scoping |
| Export | Signed bundle with manifest and hashes for auditors; access itself audited |
| SIEM | Phase 1 streaming export; MVP supports scheduled export |
| Retention | 7 years, monthly partitions, WORM archive, legal hold override |

### 13.11 Security Assurance and Testing

| Activity | Frequency | Gate |
|----------|-----------|------|
| Threat model review | Per epic and per release | Security reviewer sign-off |
| SAST (CodeQL) | Every pull request | No new high or critical |
| SCA and license check | Every build | No unresolved critical; approved licences only |
| Secret scanning (Gitleaks) | Every commit | Build fails on any finding |
| Container scan (Trivy) | Every image | No critical with a fix available |
| IaC scan | Every infrastructure change | No public storage, open security group, or unencrypted volume |
| DAST (ZAP baseline) | Nightly and pre-release | No high findings |
| Authorization and RLS suite | Every build | 100% pass |
| Dependency and API fuzzing | Weekly | No crash or unhandled 5xx |
| Penetration test | Pre-pilot and annually | Critical and high remediated before go-live |
| Restore and DR test | Quarterly | RTO and RPO met |
| Access review | Quarterly | 100% of privileged accounts reviewed |

**Remediation SLAs (NFR-SEC-010).** Critical ≤7 days, high ≤30 days, medium ≤90 days, low ≤180 days. An exception requires a documented compensating control and Security & Compliance approval.

### 13.12 Incident Response and Breach Handling

| Severity | Definition | Response | Notification |
|----------|------------|----------|--------------|
| SEV-1 | Confirmed data breach, cross-tenant exposure, or total outage | Immediate page, incident commander, containment | Customer and regulator per legal timeline (≤72 h where required) |
| SEV-2 | Major function unavailable or audit integrity suspect | 15-minute response | Customer status page, hourly updates |
| SEV-3 | Degraded performance or single-tenant impact | 1-hour response | Affected tenant |
| SEV-4 | Minor defect or cosmetic issue | Next business day | Normal support channel |

**Breach workflow.** Detect, contain, preserve evidence, assess scope and data classes, notify per jurisdiction, remediate, and publish a post-incident review within 10 business days. Forensic snapshots are taken before remediation when feasible. The breach register is retained as SOC 2 and regulatory evidence.

**Insider threat.** Production access is break-glass only; database changes go through migrations; audit chain verification detects direct alteration; separation of duties prevents a single person from creating and approving a financial record.

### 13.13 Mobile and Endpoint Security

| Control | Design |
|---------|--------|
| Local encryption | SQLCipher with keystore-wrapped key; files encrypted with AES-GCM |
| Authentication | Biometric or passcode gate on launch and after 5 minutes backgrounded |
| Offline session | ≤7 days, then re-authentication required even offline (NFR-MOB-004) |
| Device registration | Device record with model, OS, app version, and last seen; admin can revoke |
| Remote wipe | Queued on deactivation; erased on next launch; local keys destroyed |
| Screenshots | Blocked on Android for approval and evidence screens; iOS blurred in the app switcher |
| Clipboard | Financial values not copyable from read-only views |
| MDM | Supported through standard OS management; no custom MDM required for pilot |
| Update policy | Minimum supported app version enforced by the handshake |
| Root/jailbreak | Detected, reported, and shown to admins as a risk signal |

### 13.14 SOC 2 Control Hooks

| TSC area | Criterion | Architectural evidence |
|----------|-----------|------------------------|
| Security | CC6.1 logical access | RBAC plus RLS plus MFA; access review reports |
| Security | CC6.6 boundary protection | WAF, private subnets, segmentation, egress allow-list |
| Security | CC6.7 transmission and mobility | TLS, signed URLs, mobile encryption and wipe |
| Security | CC7.2 monitoring | OTel telemetry, SLO alerts, security events |
| Security | CC7.3 incident evaluation | Severity matrix, breach workflow, post-incident reviews |
| Security | CC8.1 change management | PR checks, approvals, signed artifacts, deployment audit |
| Availability | A1.2 backup and recovery | PITR, quarterly restore, DR exercise evidence |
| Confidentiality | C1.1 confidentiality | Classification, encryption, least privilege, residency |
| Processing integrity | PI1.1 processing integrity | Decimal arithmetic, reconciliation, idempotency, audit chain |
| Privacy | P4.2 retention and disposal | Retention policies, legal hold, erasure workflow, DSR log |

---

## 14. Observability & Operations

### 14.1 Observability Model

| Signal | Purpose | Backend | Retention |
|--------|---------|---------|-----------|
| Traces | Request and job flow across API, sync, outbox, worker, database | OTel Collector to regional tracing backend | 7 days |
| Metrics | RED for services, USE for infrastructure, business and pipeline metrics | Regional metrics store | 13 months |
| Logs | Structured events, errors, security events, audit references | Regional log store | 30 days hot, 13 months cold |
| Audit events | Compliance record, not operational telemetry | PostgreSQL plus WORM | 7 years |
| Business events | Capture, approval, sync, import outcomes | Metrics and dashboards | 13 months |

**Correlation.** Every request has a `trace_id`; every outbox event carries `traceparent` and `correlation_id`; every audit event carries the same `correlation_id`. A single identifier links a user action, its business change, its audit record, and every downstream delivery.

### 14.2 Telemetry Standards

| Concern | Standard |
|---------|----------|
| Naming | `slateiq.<domain>.<subject>.<unit>` for metrics; snake_case log fields |
| Resource attributes | `service.name`, `service.version`, `deployment.environment`, `cloud.region`, `tenant.hash` |
| Tenant data | Hashed or opaque `org_id` only; never names, amounts, or evidence content |
| Cardinality | No user ID, expense ID, or URL path with IDs as a metric label |
| Sampling | 100% of errors and slow traces; 10% of successful traces; tail sampling keeps all outliers |
| Log levels | `Information` for business milestones, `Warning` for recoverable, `Error` for action required; no `Debug` in production |
| Exceptions | Captured with stack trace server-side; client sees only a correlation ID |
| Health | `/health/live` and `/health/ready` on every host; readiness checks database, Redis, and storage |
| Build info | Version, commit, and build time exposed on an internal endpoint and in every trace |

### 14.3 Instrumentation Coverage

| Module | Key spans and metrics |
|--------|-----------------------|
| Tenancy | Scope resolution duration, grant cache hit ratio, revocation propagation time |
| Identity | Login success and failure, MFA challenge rate, token refresh, session revocations |
| Production | Show and party lookups, reference cache hit ratio |
| Budget | Approval duration, version creation, period lock operations |
| Vendor | Commitment lifecycle, vendor block events |
| Audit Ops | Assignment creation, coverage calculation |
| Expense | Submit latency, validation failures by code, amendment rate, exception rate |
| Cost Intelligence | Projection lag per projector, fact throughput, alert evaluation latency |
| Documents | Upload success, scan latency, quarantine rate, signed URL issuance |
| Excel | Rows per second, validation error rate, chunk retries, memory per worker |
| Notifications | Delivery success by channel, provider latency, bounce and complaint rate |
| Compliance | Audit write latency, chain verification result, DSR cycle time, hold operations |
| Reporting | Queue wait, render duration, output size, failure rate |
| Sync | Push and pull latency, applied and conflict counts, queue age, cursor expiry |
| Platform Ops | Outbox age, dead-letter count, job success, autoscale events |

### 14.4 Dashboards

| ID | Dashboard | Audience | Core panels |
|----|-----------|----------|-------------|
| OPS-DSH-01 | Service health | On-call | Request rate, error rate, p95 latency, saturation per host |
| OPS-DSH-02 | SLO burn | Engineering and product | Burn rate per SLO, error budget remaining, top contributors |
| OPS-DSH-03 | Capture and sync | Product and support | Submissions per hour, sync success, queue age, conflict rate |
| OPS-DSH-04 | Pipeline freshness | Data and finance | Outbox age, projection lag, reconciliation status |
| OPS-DSH-05 | Database | SRE | Connections, CPU, IOPS, replication lag, slow queries, locks |
| OPS-DSH-06 | Imports and reports | Operations | Queue depth, throughput, failure reasons, memory |
| OPS-DSH-07 | Notifications | Operations | Delivery rate by channel, provider errors, bounce rate |
| OPS-DSH-08 | Security | Security | Failed logins, MFA failures, denied authorizations, break-glass, quarantines |
| OPS-DSH-09 | Audit integrity | Compliance | Chain verification, anchor status, audit volume, retention actions |
| OPS-DSH-10 | Tenant health | Support | Per-tenant error rate, quota usage, sync lag (aggregated, no content) |
| OPS-DSH-11 | Cost | Engineering leadership | Spend by service, storage growth, data transfer, idle capacity |
| OPS-DSH-12 | Release | Engineering | Deployment frequency, change failure rate, rollback count, lead time |

### 14.5 Alert Catalogue

| ID | Alert | Condition | Severity | Runbook |
|----|-------|-----------|----------|---------|
| ALR-001 | Availability SLO burn | 2% budget in 1 hour or 10% in 6 hours | SEV-2 | RB-001 |
| ALR-002 | API error rate | 5xx >1% for 5 minutes | SEV-2 | RB-002 |
| ALR-003 | API latency | p95 >2× target for 10 minutes | SEV-3 | RB-002 |
| ALR-004 | Database failover | Failover event or primary unreachable | SEV-1 | RB-003 |
| ALR-005 | Replication lag | >30 seconds for 5 minutes | SEV-3 | RB-003 |
| ALR-006 | Connection saturation | Pool >85% for 5 minutes | SEV-3 | RB-003 |
| ALR-007 | Outbox backlog | Oldest undispatched event >5 minutes | SEV-2 | RB-004 |
| ALR-008 | Projection lag | Cost facts >5 minutes behind | SEV-2 | RB-004 |
| ALR-009 | Dead-letter growth | Any new dead letter | SEV-3 | RB-004 |
| ALR-010 | Sync failure rate | Push success <98% for 15 minutes | SEV-2 | RB-005 |
| ALR-011 | Sync queue age | p95 drain >5 minutes | SEV-3 | RB-005 |
| ALR-012 | Conflict spike | Conflict rate >3% for 1 hour | SEV-3 | RB-005 |
| ALR-013 | Evidence scan backlog | Oldest pending scan >10 minutes | SEV-2 | RB-006 |
| ALR-014 | Infected file | Any `INFECTED` scan result | SEV-2 | RB-006 |
| ALR-015 | Audit chain failure | Verification mismatch or missing anchor | SEV-1 | RB-007 |
| ALR-016 | Unauthorized access spike | Denied authorizations >3× baseline | SEV-3 | RB-008 |
| ALR-017 | Break-glass use | Any elevation | SEV-3 | RB-008 |
| ALR-018 | Import failure rate | >10% of batches failing in 1 hour | SEV-3 | RB-009 |
| ALR-019 | Report backlog | p95 queue wait >10 minutes | SEV-3 | RB-009 |
| ALR-020 | Storage quota | Tenant >80% or platform >75% | SEV-3 | RB-010 |
| ALR-021 | Email failure | Delivery failure >5% for 15 minutes | SEV-3 | RB-011 |
| ALR-022 | FX missing | No rate for the current date by 08:00 local | SEV-3 | RB-012 |
| ALR-023 | Certificate expiry | <21 days to expiry | SEV-3 | RB-013 |
| ALR-024 | Backup failure | Any failed snapshot or WAL gap | SEV-1 | RB-014 |
| ALR-025 | Cost anomaly | Daily spend >130% of forecast | SEV-4 | RB-015 |

**Alert hygiene.** Every page must map to a user-visible symptom or a control obligation. CPU above 85% alone is a dashboard signal, not a page. Alerts that fire without action for 30 days are reviewed and either automated or removed.

### 14.6 Runbooks

| ID | Runbook | Owner |
|----|---------|-------|
| RB-001 | SLO burn and availability degradation | SRE |
| RB-002 | API errors and latency | Platform |
| RB-003 | Database failover, locks, and connections | Data |
| RB-004 | Outbox and projection recovery | Platform |
| RB-005 | Sync backlog and conflict surge | Mobile and Platform |
| RB-006 | Evidence scanning and quarantine | Security |
| RB-007 | Audit chain verification failure | Compliance and Security |
| RB-008 | Security event and break-glass review | Security |
| RB-009 | Import and report backlog | Platform |
| RB-010 | Storage quota and lifecycle | Platform |
| RB-011 | Email deliverability failure | Platform |
| RB-012 | FX rate fallback and override | Finance Ops |
| RB-013 | Certificate and secret rotation | SRE |
| RB-014 | Backup, restore, and regional recovery | Data and SRE |
| RB-015 | Cost anomaly response | Engineering leadership |

Runbooks live in the operations repository, are linked from alerts, and are tested in game days. Document 17 owns their detailed content.

### 14.7 Operational Calendar

| Cadence | Activity |
|---------|----------|
| Continuous | SLO monitoring, alert triage, error budget tracking, security event review |
| Daily | Backup verification, audit chain check, failed job review, quarantine review, FX rate confirmation |
| Weekly | Dependency and vulnerability review, capacity trend, dead-letter review, sync health review |
| Monthly | Access review spot check, restore spot check, cost review, patch cycle, alert hygiene |
| Quarterly | Full access recertification, DR exercise, penetration test remediation review, tabletop incident |
| Annually | Penetration test, SOC 2 evidence cycle, key rotation, architecture review, BCP test |

### 14.8 Incident Severity and Escalation

| Level | Response time | Escalation | Communication |
|-------|---------------|------------|---------------|
| SEV-1 | Immediate page, 5-minute acknowledgement | Incident commander, CTO, Security, Account owner | Status page plus direct customer call |
| SEV-2 | 15 minutes | Engineering lead, Product, Support | Status page, hourly update |
| SEV-3 | 1 hour | Team on-call | Ticket and affected-tenant notice |
| SEV-4 | Next business day | Backlog | Normal support |

**Post-incident review.** Required for SEV-1 and SEV-2, and for any audit integrity or data loss event. The review is blameless, includes a timeline, contributing factors, detection quality, and action items with owners and dates. Action items are tracked to closure and reported in the monthly reliability review.

### 14.9 Change Management and Feature Flags

| Control | Design |
|---------|--------|
| Change record | Every production change links to a pull request, an approval, and a deployment ID |
| Approval | One reviewer for normal change; two for security-sensitive or financial logic |
| Feature flags | Database-backed, tenant-scoped, audited; default off for new behaviour |
| Flag lifecycle | Owner and expiry required; stale flags reviewed monthly |
| Emergency change | Break-glass deploy with post-hoc review within 24 hours |
| Configuration change | Treated as a change; versioned in IaC or audited in the admin console |
| Rollback | Previous task definition retained; database changes must be backward compatible |

### 14.10 Operational Readiness for Pilot

| Item | Exit criterion |
|------|----------------|
| Dashboards | OPS-DSH-01 to OPS-DSH-10 live before pilot |
| Alerts | All SEV-1 and SEV-2 alerts tested with a synthetic trigger |
| Runbooks | RB-001 to RB-014 reviewed and accessible from alerts |
| On-call | Named rotation, escalation contacts, and a tested paging path |
| Backups | One successful restore in staging with evidence |
| Security | Penetration test critical and high findings closed |
| Audit | Chain verification job running daily with a clean result |
| Support | Pilot tenant contacts, support hours, and escalation agreed |
| Fallback | Manual capture and Excel fallback documented and rehearsed |

---

## 15. Deployment, Environments & CI/CD

### 15.1 Environment Topology

| Environment | Purpose | Data | Access | Lifetime |
|-------------|---------|------|--------|----------|
| `dev` | Feature development and integration | Synthetic | Engineering | Persistent |
| `test` | Automated integration, RLS, and contract tests | Synthetic | CI and engineering | Persistent |
| `staging` | Production-like validation, performance, UAT | Synthetic or anonymized | Engineering, Product, Security | Persistent |
| `prod-in` | India tenant production | Real | Break-glass only | Persistent |
| `prod-eu` | EU tenant production | Real | Break-glass only | Persistent |
| `dr` | Recovery stack per region | Restored from backup | Break-glass only | Warm configuration, activated on disaster |
| `preview` | Per-pull-request UI and API preview | Synthetic | Reviewers | Ephemeral |

**Isolation rule.** Production accounts are separate from non-production accounts. A non-production environment has no network path to production data, no production credentials, and no shared KMS key. Anonymized data used in staging is irreversibly transformed and approved by the Data Architect.

### 15.2 Account and Region Layout

| Scope | Contents |
|-------|----------|
| Shared services | Container registry, CI identity, artifact signing, DNS, central logging for operational metadata |
| Region `in` | VPC, ECS cluster, PostgreSQL, Redis, S3, KMS, telemetry, secrets |
| Region `eu` | Same, fully independent |
| DR | Pre-created IaC stack per region, no running data services until invoked |

**No cross-region tenant data.** The shared services account holds no tenant content. Registry images and build artifacts are not tenant data. Operational telemetry may be aggregated across regions only after removing tenant identifiers.

### 15.3 Build and Promotion

```
Pull request
   │  build · unit · contract · SAST · SCA · secret scan
   ▼
Immutable image + SBOM + signature  ──►  registry (digest pinned)
   │
   ▼
dev ──► test ──► staging ──► prod-in / prod-eu
        (same artifact promoted by digest; configuration differs only)
```

| Rule | Detail |
|------|--------|
| One artifact | The exact image digest tested in staging is deployed to production |
| No rebuild | Environment differences come from configuration and secrets, never from a rebuild |
| Signed | cosign signature verified by the deployment platform |
| Immutable | Tag is informational; deploy by digest |
| Traceable | Deployment record links image digest, commit, PR, approver, and migration version |
| Reproducible | Lockfiles, pinned SDK, deterministic build settings |

### 15.4 Pipeline Stages and Gates

| Stage | Checks | Blocking | Evidence |
|-------|--------|----------|----------|
| 1. Validate | Format, lint, type check, architecture tests | Yes | CI log |
| 2. Build | Compile, unit tests, coverage ≥80% on critical paths | Yes | Coverage report |
| 3. Contract | OpenAPI diff, event schema compatibility, template version check | Yes | Contract report |
| 4. Security | CodeQL, SCA, Gitleaks, license allow-list | Yes | Scan report |
| 5. Integration | Testcontainers PostgreSQL, RLS suite, migration up and down | Yes | Test report |
| 6. Package | Multi-stage image, SBOM, cosign signature | Yes | SBOM and signature |
| 7. Deploy dev | Automatic on merge | — | Deployment record |
| 8. Deploy test | Automatic; full integration and E2E | Yes | Test report |
| 9. Deploy staging | Manual approval; smoke, performance sample, DAST | Yes | Staging sign-off |
| 10. Deploy production | Manual approval; canary; SLO watch | Yes | Release record |
| 11. Post-deploy | Smoke, synthetic journey, error budget check | Automatic rollback on failure | Deployment and rollback record |

**Required reviewers.** One engineering reviewer for normal change; two for authentication, authorization, money, audit, or infrastructure. A change to an RLS policy requires a Security reviewer.

### 15.5 Branching and Release Cadence

| Aspect | Decision |
|--------|----------|
| Model | Trunk-based; short-lived branches (≤2 days) |
| Merge requirement | Green pipeline plus review; squash merge |
| Release cadence | Continuous to staging; production at least weekly, on demand for fixes |
| Versioning | Semantic version for the platform; API version independent |
| Release notes | Generated from conventional commits plus a curated summary |
| Freeze | Only for declared change freezes; emergency path remains available |
| Hotfix | Branch from the production tag, same pipeline, expedited approval |

### 15.6 Database Migration Process

| Step | Rule |
|------|------|
| Authoring | EF Core migration reviewed as SQL; no generated SQL merged unread |
| Expand | Add nullable columns, new tables, and new indexes concurrently |
| Backfill | Batched, resumable, throttled, and observable |
| Contract | Drop or rename only after two releases and a verified no-use check |
| Lock safety | `CREATE INDEX CONCURRENTLY`; no long `ALTER TABLE` locks on hot tables |
| Rollback | Forward-fix preferred; destructive migrations require a tested restore plan |
| Approval | Data Architect review for partitioning, retention, and RLS changes |
| Execution | Migration job runs before the application deployment; the app tolerates both schemas during rollout |
| Verification | Row counts, constraint validation, and a smoke query after migration |

**Prohibited.** Dropping a column in the same release that stops writing it. Renaming a column in place. Adding a `NOT NULL` column without a default or backfill. Running DDL as the application role.

### 15.7 Deployment Strategies

| Strategy | Use | Mechanism | Rollback |
|----------|-----|-----------|----------|
| Rolling | Default for API, sync, and worker | Replace tasks in batches with readiness gates | Redeploy previous digest |
| Canary | Risky changes and major releases | 5% → 25% → 100% with SLO watch | Shift traffic back |
| Blue/green | Breaking infrastructure or runtime change | Parallel stack, switch at the load balancer | Switch back |
| Shadow | Performance-sensitive changes | Mirror read traffic to the new version | Discard shadow |
| Migration-first | Schema changes | Expand migration, deploy app, contract later | Forward-fix |

**Worker deployment.** Workers drain gracefully: stop claiming, finish in-flight jobs up to 60 seconds, release claims, then exit. A job that exceeds the drain window is retried by another worker; idempotency makes this safe.

### 15.8 Configuration and Secrets

| Concern | Design |
|---------|--------|
| Configuration | Strongly typed options validated at startup; fail fast on missing or invalid values |
| Per-tenant settings | Database rows with schema validation, audited changes, and cache invalidation |
| Secrets | Secret manager with automatic rotation; injected at runtime, never baked into images |
| Environment parity | Same configuration keys across environments; only values differ |
| Feature flags | Database-backed, audited, tenant-scoped, default off |
| Drift | IaC plan runs daily; any drift raises a ticket and blocks the next deploy until reconciled |
| Emergency config | Break-glass change audited and reviewed within 24 hours |

### 15.9 Infrastructure as Code

| Aspect | Design |
|--------|--------|
| Tool | Terraform with remote state, locking, and per-environment workspaces |
| Structure | `modules/` for network, compute, data, storage, observability, security; `envs/` per region and environment |
| Policy | Plan output reviewed; policy checks block public buckets, open security groups, unencrypted volumes, and missing tags |
| State | Encrypted, access-controlled, versioned; no secrets in state |
| Change flow | Pull request, plan comment, approval, apply through CI identity |
| Drift | Daily plan; manual changes prohibited and detected |
| Tagging | `env`, `region`, `service`, `owner`, `cost-center`, `data-class` required |
| Disaster rebuild | A region stack can be created from code with only secrets and DNS supplied |

### 15.10 Tenant Provisioning

| Step | Action | Verification |
|------|--------|--------------|
| 1 | Create organization record and assign region | Region matches the contract and residency election |
| 2 | Create tenant data key wrapped by the regional CMK | Key alias resolves; no cross-region key |
| 3 | Seed reference data and permission catalog | Counts match the release baseline |
| 4 | Create the first Org Admin with mandatory MFA | Invitation accepted; MFA enrolled |
| 5 | Apply plan limits and quotas | Quota record present and audited |
| 6 | Configure retention and legal-hold defaults | Policy version recorded |
| 7 | Run smoke checks for capture, approval, dashboard, and export | All pass |
| 8 | Record provisioning evidence | Audit event and onboarding checklist |

**Deprovisioning.** Suspend access, export on request, apply retention, then erase with legal-hold precedence and crypto-shred the tenant key. Evidence of each step is retained.

### 15.11 Production Access and Release Checklist

| Control | Requirement |
|---------|-------------|
| Standing access | None; all access is time-bound and approved |
| Approval | Two-person rule for production data access |
| Session | Recorded, with command and query logging |
| Data handling | No production data copied to laptops; no screenshots of personal data |
| Break-glass | MFA hardware key, reason, ticket reference, 24-hour review |

**Release checklist.**

1. Pipeline green and artifact signed.
2. Migration reviewed and rehearsed on a staging copy.
3. Feature flags default off and documented.
4. Dashboards and alerts cover the new behaviour.
5. Rollback path identified and tested where feasible.
6. Security review completed for changed trust boundaries.
7. Release notes and support brief prepared.
8. Post-deploy smoke and synthetic journey pass.
9. Error budget healthy; no active SEV-1 or SEV-2.
10. Deployment record and evidence retained.

---

## 16. Technology Risks

| ID | Risk | Likelihood | Impact | Mitigation | Early warning | Owner |
|----|------|:----------:|:------:|------------|---------------|-------|
| **TR-001** | RLS policy cost degrades dashboard and list queries at 3× volume | Medium | High | Indexed predicates, query plan gate, scope cache, load test with RLS enabled | p95 query time up 20%; sequential scans on tenant tables | Data Architect |
| **TR-002** | PostgreSQL outbox becomes a throughput bottleneck above 500 events/s | Low | High | Batched claims, partitioned outbox, worker autoscaling; broker extraction path defined | Oldest event age >30 s sustained | Platform Architect |
| **TR-003** | Modular monolith drifts into a tightly coupled layered monolith | Medium | High | Architecture tests, schema ownership, contract-only access, extraction triggers | Cross-schema queries appear in review; cycle count rises | Principal Architect |
| **TR-004** | Outbox and audit partitions grow faster than retention can prune | Medium | Medium | Monthly partitions, 3-month dispatch retention, archival job, capacity alert | Table size >150% of forecast | Data Architect |
| **TR-005** | A 50,000-row Excel import exhausts worker memory or starves interactive jobs | Medium | High | Streaming parser, 1,000-row chunks, memory cap, dedicated queue class | Worker OOM count; import queue age | Platform Architect |
| **TR-006** | Headless Chromium increases attack surface and memory cost | Medium | Medium | Isolated renderer, no database route, read-only filesystem, pinned version, seccomp | Renderer restarts; unusual outbound connections | Security Architect |
| **TR-007** | Expo or React Native upgrade breaks native modules (SQLCipher, camera) | High | Medium | Pin SDK, quarterly upgrade window, development builds, regression device matrix | Build failures on upgrade branch | Mobile Lead |
| **TR-008** | SQLCipher key handling differs across Android vendors and OS versions | Medium | High | Keystore abstraction, device matrix, biometric fallback, wipe test | Decryption failure rate >0.5% | Mobile Lead |
| **TR-009** | Evidence storage cost grows faster than plan because of retention and WORM copies | Medium | Medium | Lifecycle tiers, compression, per-tenant quotas, retention enforcement | Storage cost per tenant >20% above model | Platform Architect |
| **TR-010** | Transactional email lands in spam or is rate-limited by the provider | Medium | Medium | Authenticated domain (SPF, DKIM, DMARC), warm-up, secondary provider, in-app fallback | Bounce or complaint rate >2% | Platform Architect |
| **TR-011** | FX source changes format, licence, or availability | Medium | High | Adapter isolation, secondary source, manual override, contract tests | Consecutive pull failures; schema mismatch | Finance SME |
| **TR-012** | Cloud region lacks a managed service or instance class needed at GA | Medium | Medium | Capability spike before region commitment, portable abstraction, IaC parameterization | Service unavailable in region | Platform Architect |
| **TR-013** | Managed service quotas (connections, storage, requests) are reached during a peak | Medium | High | Quota review per release, pre-requested increases, load test at 3×, alarms at 75% | Quota utilization >75% | SRE Lead |
| **TR-014** | Deep coupling to one cloud's storage or KMS blocks a region or exit | Medium | Medium | S3-compatible interface, KMS abstraction, no provider-specific payloads in domain code | Provider-specific code outside adapters | Principal Architect |
| **TR-015** | Knowledge concentration in a small team creates a delivery and recovery risk | High | High | Pairing, runbooks, architecture decision records, on-call rotation, documented onboarding | Bus factor of one on a critical module | Engineering Lead |
| **TR-016** | A key open-source dependency is abandoned (ExcelDataReader, op-sqlite, Quartz) | Medium | Medium | Health review each quarter, fork option, abstraction at the boundary, licence check | No release in 12 months; unresolved CVE | Principal Architect |
| **TR-017** | Terraform state drift or a manual change causes a region rebuild to fail | Medium | High | Daily plan, no console writes, state locking, quarterly rebuild drill | Drift detected; rebuild test failure | SRE Lead |
| **TR-018** | Observability cardinality or retention cost exceeds budget | Medium | Medium | Label limits, sampling, tiered retention, cost dashboard, monthly review | Observability spend >15% of infrastructure | SRE Lead |
| **TR-019** | Scanner false negatives allow a malicious file to be served | Low | High | Layered scanning, type allow-list, no execution, quarantine, periodic red-team upload | Post-scan infection report | Security Architect |
| **TR-020** | Multi-region operations exceed the team's capacity at GA | Medium | High | One region per tenant, shared control plane minimized, regional runbooks, phased rollout | Regional incident load; runbook gaps | Engineering Lead |

**Risk review.** The register is reviewed monthly by the architecture group and at every release gate. A risk whose early-warning indicator fires becomes an agenda item in the weekly reliability review. Risk acceptance requires the owner and the Principal Architect, and is recorded in §19.

---

<!-- CHUNK-5 -->

