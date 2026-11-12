# Document 17: Risk Register & Architecture Decision Records

**Product:** SlateIQ — Production Cost Intelligence & Audit Monitoring Platform
**Document ID:** SLQ-ARCH-017
**Version:** 1.1.0 (Consolidated Registers)
**Supersedes:** 1.0.0, archived at `_archive/17-risk-register-adrs.v1.0.0-unverified.md`
**Classification:** Internal — Architecture Foundation
**Status:** Draft — For Review
**Date:** 2026-11-12
**Authors:** Principal Enterprise Solution Architect
**Reviewers:** Principal Architect (TBD), Security & Compliance (TBD), Delivery Lead (TBD), Product Owner (TBD)

---

## Document Control

### Revision History

| Version | Date | Author | Change |
|---------|------|--------|--------|
| 1.0.0 | 2026-11-06 | Unrecorded | First draft. Its ADR registry did not match the owning documents (for example ADR-004 and ADR-005 carried other decisions' titles) and omitted ADR-016 to ADR-025. Archived, not used |
| 1.1.0 | 2026-11-12 | Architecture Team | Rebuilt from source: every ADR, risk and open question is extracted from its owning document, so this register cannot drift from them. Adds the blocking-decision list and the risk heat summary |

### How This Document Is Maintained

The registers in sections 3, 5 and 6 are generated from Documents 1 to 16 by extracting each ADR heading or table row, each risk row (`RSK-`, `TR-`, `R<doc>-`) and each open question row (`OQ-`, `AQ-`, `OD-`, `G-`). The owning document is authoritative; this document is the index. To change a decision, edit the owning document through the change process in Document 18 §10 and regenerate. Do not edit the generated tables by hand.

---

## 1. Purpose

One place to answer three questions: what has been decided and where it is justified; what could go wrong and who watches it; and what must still be decided before build or production.

## 2. Decision Governance

| Rule | Detail |
|------|--------|
| Ownership | Each ADR lives in the document whose topic it governs; the identifier blocks were allocated in advance so numbers never collide (ADR-001..015 Document 6; 016..024 Document 3; 025 Document 2; 026..029 Document 4; 030..031 Document 5; 032..034 Document 7; 035..039 Document 8; 040..043 Document 9; 044..047 Document 10; 048..051 Document 11; 052..054 Document 12; 055..058 Document 13; 059..061 Document 14; 062..065 Document 15; 066..067 Document 16) |
| Status values | `Accepted` (binding), `Proposed` (binding for design, confirmed at the owning document's approval), `Provisional` (binding until its named gate; a fallback is recorded) |
| Immutability | An accepted ADR is never edited; it is superseded by a new ADR that names it |
| Conflicts | A document that cannot comply raises a reconciliation note (`RC-…`) or change request; the foundation index §4 lists every one and its resolution |
| Review | The Principal Architect reviews `Proposed` ADRs at each document's approval; `Provisional` ADRs at their gates (G-01, G-02, OD-01) |

## 3. Architecture Decision Register

All 68 ADRs, ADR-001 to ADR-068, are defined. Four are provisional: ADR-013 (mobile framework, gate G-02), ADR-015 (cloud runtime, gate G-01), ADR-025 (show hosting topology, decision OD-01) and ADR-068 (zero-cost deployment profile, constraint C-01).

| ADR | Decision | Owner | Status |
|-----|----------|-------|--------|
| ADR-001 | Modular monolith with asynchronous workers and an isolated sync host | Document 6 §17.1 | Accepted |
| ADR-002 | Shared database with PostgreSQL row-level security and per-transaction tenant context | Document 6 §17.1 | Accepted |
| ADR-003 | Transactional outbox with `SKIP LOCKED` dispatch; no external broker at MVP | Document 6 §17.1 | Accepted |
| ADR-004 | Offline-first sync with server-authoritative conflict resolution | Document 6 §17.1 | Accepted |
| ADR-005 | Decimal money with an immutable FX snapshot per expense | Document 6 §17.1 | Accepted |
| ADR-006 | Object storage for evidence with scan-before-serve | Document 6 §17.1 | Accepted |
| ADR-007 | REST/JSON API versioned by path, with idempotency keys and ProblemDetails | Document 6 §17.1 | Accepted |
| ADR-008 | Built-in identity at MVP; OIDC/SAML and SCIM at Phase 1 | Document 6 §17.1 | Accepted |
| ADR-009 | One region per tenant; restore-based regional disaster recovery | Document 6 §17.1 | Accepted |
| ADR-010 | CQRS-lite read models for dashboards and reports; no warehouse at MVP | Document 6 §17.1 | Accepted |
| ADR-011 | Excel validation parity through the shared domain validation service | Document 6 §17.1 | Accepted |
| ADR-012 | In-transaction append-only audit with a per-tenant hash chain | Document 6 §17.1 | Accepted |
| ADR-013 | React Native with Expo and SQLCipher for the mobile client | Document 6 §17.1 | Provisional |
| ADR-014 | OpenTelemetry as the single telemetry standard | Document 6 §17.1 | Accepted |
| ADR-015 | Containers on managed Fargate with Terraform and GitHub Actions | Document 6 §17.1 | Provisional |
| ADR-016 | Identity, membership, assignment and grant are four separate relations; one home organization per human; one membership at MVP | Document 3 §17.1 | Accepted |
| ADR-017 | The Cross-Organization Access Grant is a first-class aggregate with an eight-state lifecycle, two-person approval and grantee acceptance ga… | Document 3 §17.1 | Accepted |
| ADR-018 | Closed, flat permission catalogue `domain.resource.action` with nine actions and seven scope types; effective scope is an intersection | Document 3 §17.1 | Accepted |
| ADR-019 | In-process policy decision point with a `scope_version`-keyed cache; no external policy engine at MVP | Document 3 §17.1 | Accepted |
| ADR-020 | Segregation of duties is a decision-time veto backed by a time-boxed exception register | Document 3 §17.1 | Accepted |
| ADR-021 | Thin access tokens, a session registry and a denylist deliver revocation within five minutes; 14-day rotating refresh, 30-day absolute sess… | Document 3 §17.1 | Accepted |
| ADR-022 | Identity personal data is held under a per-user key and erased cryptographically; the pseudonymous accountability record survives | Document 3 §17.1 | Accepted |
| ADR-023 | A device is never a principal; offline validity is a bounded device grant of at most 7 days layered on a human session | Document 3 §17.1 | Accepted |
| ADR-024 | Tenant provisioning is an idempotent, compensated workflow that ends with a negative isolation probe | Document 3 §17.1 | Accepted |
| ADR-025 | Show hosting topology: commissioner-hosted (H1) or producer-hosted (H2) | Document 2 §2.3 | Provisional |
| ADR-026 | One Approval Engine with Snapshot Chains | Document 4 §10 | Proposed |
| ADR-027 | Declarative Transition Tables with Database Guards | Document 4 §10 | Proposed |
| ADR-028 | Excel All-or-Nothing Through a Validation Gate and Compensation | Document 4 §10 | Proposed |
| ADR-029 | Dual-Trigger Rule Evaluation for Alerts and Budget Exceptions | Document 4 §10 | Proposed |
| ADR-030 | Layered Rule Enforcement with a Rule Registry | Document 5 §16 | Proposed |
| ADR-031 | Effective-Dated, Bounded Policy Store with Snapshots | Document 5 §16 | Proposed |
| ADR-032 | Two-Assembly Modules with Layered, Test-Enforced Contract Dependencies | Document 7 §16 | Proposed |
| ADR-033 | Approval-Chain Resolution Owned by Budget, Persistence Owned by the Subject | Document 7 §16 | Proposed |
| ADR-034 | Per-Subscription Delivery, Lanes and Retry Profiles on the Outbox | Document 7 §16 | Proposed |
| ADR-035 | Tenant-Leading Composite Keys and No Cross-Schema Foreign Keys | Document 8 §19 | Accepted |
| ADR-036 | State Columns as Text with CHECK Constraints and Transition Triggers | Document 8 §19 | Accepted |
| ADR-037 | Monthly Partitioning of Expense Tables with a Key Registry | Document 8 §19 | Accepted |
| ADR-038 | Read Committed with Explicit, Ordered Pessimistic Locks | Document 8 §19 | Accepted |
| ADR-039 | Per-Tenant Logical Restore from an Isolated PITR Clone | Document 8 §19 | Accepted |
| ADR-040 | Wire Format: snake_case JSON and Decimal-String Money | Document 9 §12 | Proposed |
| ADR-041 | Contract-First OpenAPI 3.1 in `contracts/` | Document 9 §12 | Proposed |
| ADR-042 | Stable Error-Code Catalogue on RFC 9457 | Document 9 §12 | Proposed |
| ADR-043 | Thin, Signed Webhooks with Pull-Back (Phase 1) | Document 9 §12 | Proposed |
| ADR-044 | Layered Rate Limiting with Redis GCRA and Local Fallback | Document 10 §21 | Accepted |
| ADR-045 | Application-Level Field Encryption with Per-Tenant Data Keys and Blind Indexes | Document 10 §21 | Accepted |
| ADR-046 | Content-Based File Validation Before Scanning, Without Content Disarm at MVP | Document 10 §21 | Accepted |
| ADR-047 | Multi-Clock Incident Notification and In-Region Security Log Retention | Document 10 §21 | Accepted, subject to legal validation o… |
| ADR-048 | Per-Profile Encrypted Local Store with a Transactional Outbox and Evidence-Gated Mutations | Document 11 §25 | See document |
| ADR-049 | Device Key Hierarchy with Presence-Bound Signing and a Server-Escrowed Recovery Wrap | Document 11 §25 | See document |
| ADR-050 | No Silent Discard: Held Items, Journalled Discards and Supervised Salvage Before Wipe | Document 11 §25 | See document |
| ADR-051 | Shared Devices as Isolated Per-User Profiles with a Profile PIN | Document 11 §25 | See document |
| ADR-052 | Imports Execute Through Owning-Module Commands from a Staged Batch | Document 12 §32 | See document |
| ADR-053 | All-or-Nothing Semantics over Chunked Commits | Document 12 §32 | See document |
| ADR-054 | Report-Definition Exports with Scope Snapshot, Watermark and Round-Trip Metadata | Document 12 §32 | See document |
| ADR-055 | Actual Cost Is Projected Only from the Cost Posting Ledger | Document 13 §19 | Proposed |
| ADR-056 | Commitment Consumption at the Commitment's Locked Rate | Document 13 §19 | Proposed |
| ADR-057 | Period Measures as Posting-Period Actual and Variance Movement | Document 13 §19 | Proposed |
| ADR-058 | Hybrid Alert Evaluation with Deduplication, Cooldown and Hysteresis | Document 13 §19 | Proposed |
| ADR-059 | Separate Business and Security Audit Streams with Independent Per-Tenant Chains | Document 14 §20 | Accepted |
| ADR-060 | Allow-List Structured Logging with Two-Stage Redaction and Opaque Tenant Identifiers | Document 14 §20 | Accepted |
| ADR-061 | SLO-Based Multi-Window Burn-Rate Alerting on Region-Local Telemetry | Document 14 §20 | Accepted |
| ADR-062 | Multi-Account Landing Zone with Region-Pinned Production Accounts | Document 15 §23 | Proposed |
| ADR-063 | Host Profiles from One Backend Image, with an Internal Ops Host | Document 15 §23 | Proposed (refines ADR-001) |
| ADR-064 | Restore-Based Disaster Recovery with Same-Region Locked Cross-Account Copies | Document 15 §23 | Proposed |
| ADR-065 | Dedicated Enterprise Deployment as a Cell of the Same Release | Document 15 §23 | Proposed |
| ADR-066 | Catalogue-Generated Test Suites for Routes, Policies, and State Machines | Document 16 §13 | Approved |
| ADR-067 | Strict Synthetic Production-Shaped Test Data Only (Zero Production Data in Non-Prod) | Document 16 §13 | Approved |
| ADR-068 | Zero-cost deployment profile (Docker Compose, open-source substitutes) alongside the managed profile | Document 0 §8.5 | Provisional |

## 4. Decisions Required Before Build or Production

These items block a named milestone. Everything else in section 6 can be resolved during the increment that implements it.

| ID | Decision | Blocks | Owner | Recommendation |
|----|----------|--------|-------|----------------|
| G-01 | Cloud provider and launch regions | Terraform, residency statement (Phase 0) | Executive sponsor, Platform Architect | AWS `ap-south-1` first; EU region at Phase 1 (Document 15) |
| G-02 | Mobile framework confirmed (ADR-013) | Mobile build (Phase 0) | Principal Architect, Mobile Lead | Confirm React Native + Expo + SQLCipher |
| OD-01 | Show hosting topology (ADR-025) | Role seeding and onboarding (end of M1) | Business owner | H1 commissioner-hosted for commissioned shows; H2 for producers' own shows |
| OQ-4-05 | May external auditors capture expenses and file audit reports? Grant profiles currently forbid capture | External auditor onboarding (M3) | Audit SME, Product Owner | Allow evidence and audit reports through `AUDIT_EVIDENCE`; keep expense capture with internal auditors unless the pilot customer requires otherwise |
| OQ-5-01 | Statutory basis and amount of the cash payment limit (POL-014) | Expense validation rules (M2) | Finance SME, tax advisor | Keep as configurable policy; validate wording before pilot |
| A2-04 | Input-tax recoverability defaults per category | Cost basis definition (M2) | Finance SME, tax advisor | Default by category, override per line with reason |
| OQ-13-01 | Do amendment reversals post to the original open period or the event period? | Period reports (M2) | Finance SME | Event period, consistent with BR-EXP-010 |
| G-03 | Retention and legal-hold policy | Pilot | Compliance, Legal | 7-year default (D-014), longer by jurisdiction |
| G-04 | Penetration-test scope and vendor | Pilot entry | Security & Compliance | Book at M4 start |
| G-06 | Support impersonation model | Pilot | Security & Compliance | Break-glass only, two approvers, 8-hour bound (BR-ACC-008) |
| C-01 | Zero-cost build and deployment: scope (production or evaluation), Android-only capture, free host for India data (OQ-0-01 to OQ-0-03) | Infrastructure profile (Phase 0) | Business owner | Adopt ADR-068: free profile for build, evaluation and small tenants; managed profile for production tenants needing NFR-AVL |
| — | CERT-In six-hour reporting and 180-day log retention applicability | Production in India | Legal | Treat as applicable; Document 10 already designs for it |

## 5. Risk Register

### 5.1 Top Cross-Cutting Risks

| Rank | Risk | Sources | Owner | Mitigation in the architecture |
|------|------|---------|-------|--------------------------------|
| 1 | Cross-tenant data disclosure | RSK-003, Document 3 §13, R8-05 | Security Architect | Four-layer isolation, forced RLS, inventory test, isolation probe, negative suites on every build (Document 16) |
| 2 | Offline capture loses or duplicates financial records | RSK-004, R11-xx, R18-01 | Mobile Lead | Client IDs, idempotent mutations, no-silent-discard rule (ADR-050), sync simulation tests |
| 3 | Financial figures disagree between screens, exports and reports | RSK-007, R13-xx | Data Architect | Single posting ledger (ADR-055), shared rounding (RC-01), reconciliation checks RCN-01..12 |
| 4 | Excel import bypasses controls or corrupts data | RSK-005, R12-xx | Integration Architect | Owning-module commands (ADR-052), all-or-nothing commit (ADR-053), approval before commit |
| 5 | Hosting topology decided late or wrongly | R2-01, R4-15, OD-01 | Product Owner | Both patterns supported; only seeding and onboarding change |
| 6 | Tax and statutory rules misinterpreted | OQ-5-01, A2-04 | Finance SME | Rules as versioned policies (ADR-031); advisor validation before pilot |
| 7 | Audit chain lock serialises writes per tenant | R8-01 | Data Architect | Measured in Phase 0 load test; fallback documented in Document 8 |
| 8 | Scope or schedule slip of the pilot | R18-01..05 | Delivery Lead | Walking skeleton, thin slices, early sync prototype |

### 5.2 Consolidated Register

Likelihood, impact and mitigation are as stated in each source document; where a source used a narrative column, its first columns are reproduced.

| ID | Source | Risk | Detail (likelihood, impact, mitigation as stated in source) |
|----|--------|------|------|
| RSK-001 | Doc 1 | Pilot customers do not provide clean budget/vendor data, delaying first value. | Adoption; 4; 4; 16 High |
| RSK-002 | Doc 1 | Auditors reject mobile capture as slower than paper. | Adoption; 3; 5; 15 High |
| RSK-003 | Doc 1 | Cross-organization grants leak data through an indirect path (search, export, document URL). | Security; 2; 5; 10 Medium |
| RSK-004 | Doc 1 | Offline sync conflicts cause duplicate or lost expenses. | Delivery; 3; 4; 12 High |
| RSK-005 | Doc 1 | Excel import silently bypasses a validation rule. | Compliance; 3; 5; 15 High |
| RSK-006 | Doc 1 | FX rate source unavailable or disputed, producing incorrect conversions. | Financial; 2; 4; 8 Medium |
| RSK-007 | Doc 1 | Budget and actual definitions diverge across reports. | Financial; 3; 4; 12 High |
| RSK-008 | Doc 1 | Scope creep pulls Phase 1 features into MVP and delays pilot. | Delivery; 4; 4; 16 High |
| RSK-009 | Doc 1 | Tenant isolation model fails customer security review. | Commercial; 3; 5; 15 High |
| RSK-010 | Doc 1 | Retention and legal hold requirements differ per customer, causing rework. | Compliance; 3; 3; 9 Medium |
| RSK-011 | Doc 1 | SOC 2 evidence is not captured early enough, delaying certification. | Compliance; 3; 4; 12 High |
| RSK-012 | Doc 1 | Alert noise causes users to ignore critical overruns. | Product; 4; 3; 12 High |
| RSK-013 | Doc 1 | External auditor onboarding is slower than promised due to identity verification. | Operational; 3; 3; 9 Medium |
| RSK-014 | Doc 1 | Evidence storage costs exceed model as photo volume grows. | Cost; 3; 3; 9 Medium |
| RSK-015 | Doc 1 | Category taxonomy inconsistency undermines benchmarking. | Data; 4; 3; 12 High |
| RSK-016 | Doc 1 | Key-person dependency in finance or audit SME availability. | Organizational; 3; 4; 12 High |
| RSK-017 | Doc 1 | Mobile app distribution blocked by customer IT policy. | Adoption; 2; 4; 8 Medium |
| RSK-018 | Doc 1 | Payment/reference data misinterpreted as payment execution capability. | Legal / Commercial; 2; 4; 8 Medium |
| RSK-019 | Doc 1 | Regulatory change (DPDP rules, GDPR interpretation) alters requirements mid-build. | Regulatory; 3; 4; 12 High |
| RSK-020 | Doc 1 | Downstream documents diverge from this BRD, producing inconsistent design. | Documentation; 4; 3; 12 High |
| R2-01 | Doc 2 | Production-house staff serving two H1 commissioners need two memberships, unavailable until Phase 1 | Workaround or earlier multi-membership; Product Owner; Risk, tracked in Document 17 |
| R4-01 | Doc 3 | A shared-schema design depends on RLS correctness; a policy defect could expose rows before a test catches it | T4-09, T4-10, T4-11; RLS is code, and code has defects; Four independent layers, per-table negative tests, policy inventory gate, isolation probe at provisioning and deploy; accepted for the pilot because dedicated stac… |
| R4-02 | Doc 3 | TOTP is phishable; a real-time relay can defeat it | T4-03, T4-05; WebAuthn is Phase 1; Mandatory MFA for privileged roles, step-up for high-risk actions, device binding, anomaly detection, short session bounds; accepted; CISO |
| R4-03 | Doc 3 | Account lockout is a denial-of-service vector against a known victim | T4-02; FR-TNT-007 mandates the lock; Per-IP throttle, verified reset clears the lock, no lock-state disclosure, monitoring; accepted; CISO |
| R4-04 | Doc 3 | A stolen unlocked device with an unexpired offline grant can read local data | T4-28; Offline capture is a product requirement; SQLCipher, biometric gate, seven-day bound, remote wipe, device revoke; accepted; CISO |
| R4-05 | Doc 3 | A platform operator with approved support access can read tenant data | T4-18, T4-19; Someone must be able to support and recover the platform; Two-person approval, ticket, 60-minute bound, action scope, no impersonation, customer-visible audit; accepted; CISO |
| R4-06 | Doc 3 | A cross-region grant places minimal identity metadata in the firm's region | T4-14; Auditors work across borders; Minimal projection only, no client business data, acknowledgement recorded, DPO review; accepted; DPO |
| R4-07 | Doc 3 | An authorised export cannot be recalled after download | T4-21; Data leaves the platform by design; Watermark, row caps, step-up, export event, contractual terms, access review; accepted; CISO |
| R4-08 | Doc 3 | An auditor can memorise or reconstruct what they read | T4-14; Human cognition cannot be controlled; NDA, conflict declaration, least-privilege profile, audit trail, contractual remedy; accepted; Legal |
| R4-09 | Doc 3 | Expiry notices may not reach the owner because email is unreliable | T4-16; Email is an external dependency; Retries, in-app notice, register evidence, escalation at T-7 and T-1, grant expires regardless of delivery; accepted; Head of Customer Success |
| R4-10 | Doc 3 | A future identity provider becomes a trusted third party for authentication | T4-08; Federation is Phase 1 and adds a dependency; Per-tenant IdP configuration, signed assertions, explicit trust registration, fallback local account policy; not applicable at MVP; CISO |
| R4-11 | Doc 4 | The shared approval engine (ADR-026) becomes a coupling hotspot across five modules | Medium; Medium; Narrow contract (resolve, decide, cancel); subject-specific guards stay in owning modules; architecture tests on dependencies |
| R4-12 | Doc 4 | Budget-based exceptions and alerts arrive up to 60 s after approval, so an over-budget expense may be approved first | High; Medium; Freshness-checked budget read at the final step (WF-10); OQ-4-09 blocking option |
| R4-13 | Doc 4 | Grant profiles block external capture and reporting at pilot | High; High; CAT-4-01 to CAT-4-03 decided before pilot onboarding (OQ-4-05) |
| R4-14 | Doc 4 | Import reversal stalls when created records were amended by others | Medium; Medium; Row-level blockers listed; resumable reversal; reversal rights limited to approvers |
| R4-15 | Doc 4 | H2 appointing-party grant path is undefined, weakening BR-AUD-006 in producer-hosted shows | Medium; High; Interim WF-06 path; OQ-4-06 resolved in Document 3 |
| R4-16 | Doc 4 | Open approvals and `REQUIRES_INFO` items accumulate and block period close and show closure | Medium; Medium; SLA reminders, digest, RPT-08 ageing, waivers with approver |
| R5-01 | Doc 5 | Rules that cross schemas (period guard, cross-module references) have no database backstop under RC-02; a coding defect could bypass them | Medium; High; Single contract per check; architecture test that every dated write calls `PeriodGuard`; RCN-07 and INT-03 nightly; concurrency tests |
| R5-02 | Doc 5 | Tenant policy misconfiguration weakens a control within bounds (for example a high evidence threshold) | Medium; Medium; Bounds, tighten-only levels, L2 values need a second approver, policy changes listed in the monthly override review |
| R5-03 | Doc 5 | Statutory values change (Income-tax Act 2025 replaced the 1961 Act from 1 April 2026) and seeded values become wrong | Medium; Medium; POL-014 owned by Tax Advisor; statutory policies are L1 with release-driven updates; OQ-5-01 |
| R5-04 | Doc 5 | Registry, error codes and tests drift from Document 1 as rules evolve | Medium; Medium; Registry generated from code and checked against Document 1 identifiers in CI; ADR-030 |
| R5-05 | Doc 5 | Advisory locks on duplicate and cash-limit checks become contention points at peak submission | Low; Medium; Lock keys are narrow (show, vendor, day); lock held only in the submission transaction; performance test in Document 16 |
| R5-06 | Doc 5 | Overrides mirrored from several tables leave gaps in the register | Low; High; Mirror written in the same transaction through one Compliance contract; CI test per override type |
| TR-001 | Doc 6 | RLS policy cost degrades dashboard and list queries at 3× volume | Medium; High; Indexed predicates, query plan gate, scope cache, load test with RLS enabled; p95 query time up 20%; sequential scans on tenant tables |
| TR-002 | Doc 6 | PostgreSQL outbox becomes a throughput bottleneck above 500 events/s | Low; High; Batched claims, partitioned outbox, worker autoscaling; broker extraction path defined; Oldest event age >30 s sustained |
| TR-003 | Doc 6 | Modular monolith drifts into a tightly coupled layered monolith | Medium; High; Architecture tests, schema ownership, contract-only access, extraction triggers; Cross-schema queries appear in review; cycle count rises |
| TR-004 | Doc 6 | Outbox and audit partitions grow faster than retention can prune | Medium; Medium; Monthly partitions, 3-month dispatch retention, archival job, capacity alert; Table size >150% of forecast |
| TR-005 | Doc 6 | A 50,000-row Excel import exhausts worker memory or starves interactive jobs | Medium; High; Streaming parser, 1,000-row chunks, memory cap, dedicated queue class; Worker OOM count; import queue age |
| TR-006 | Doc 6 | Headless Chromium increases attack surface and memory cost | Medium; Medium; Isolated renderer, no database route, read-only filesystem, pinned version, seccomp; Renderer restarts; unusual outbound connections |
| TR-007 | Doc 6 | Expo or React Native upgrade breaks native modules (SQLCipher, camera) | High; Medium; Pin SDK, quarterly upgrade window, development builds, regression device matrix; Build failures on upgrade branch |
| TR-008 | Doc 6 | SQLCipher key handling differs across Android vendors and OS versions | Medium; High; Keystore abstraction, device matrix, biometric fallback, wipe test; Decryption failure rate >0.5% |
| TR-009 | Doc 6 | Evidence storage cost grows faster than plan because of retention and WORM copies | Medium; Medium; Lifecycle tiers, compression, per-tenant quotas, retention enforcement; Storage cost per tenant >20% above model |
| TR-010 | Doc 6 | Transactional email lands in spam or is rate-limited by the provider | Medium; Medium; Authenticated domain (SPF, DKIM, DMARC), warm-up, secondary provider, in-app fallback; Bounce or complaint rate >2% |
| TR-011 | Doc 6 | FX source changes format, licence, or availability | Medium; High; Adapter isolation, secondary source, manual override, contract tests; Consecutive pull failures; schema mismatch |
| TR-012 | Doc 6 | Cloud region lacks a managed service or instance class needed at GA | Medium; Medium; Capability spike before region commitment, portable abstraction, IaC parameterization; Service unavailable in region |
| TR-013 | Doc 6 | Managed service quotas (connections, storage, requests) are reached during a peak | Medium; High; Quota review per release, pre-requested increases, load test at 3×, alarms at 75%; Quota utilization >75% |
| TR-014 | Doc 6 | Deep coupling to one cloud's storage or KMS blocks a region or exit | Medium; Medium; S3-compatible interface, KMS abstraction, no provider-specific payloads in domain code; Provider-specific code outside adapters |
| TR-015 | Doc 6 | Knowledge concentration in a small team creates a delivery and recovery risk | High; High; Pairing, runbooks, architecture decision records, on-call rotation, documented onboarding; Bus factor of one on a critical module |
| TR-016 | Doc 6 | A key open-source dependency is abandoned (ExcelDataReader, op-sqlite, Quartz) | Medium; Medium; Health review each quarter, fork option, abstraction at the boundary, licence check; No release in 12 months; unresolved CVE |
| TR-017 | Doc 6 | Terraform state drift or a manual change causes a region rebuild to fail | Medium; High; Daily plan, no console writes, state locking, quarterly rebuild drill; Drift detected; rebuild test failure |
| TR-018 | Doc 6 | Observability cardinality or retention cost exceeds budget | Medium; Medium; Label limits, sampling, tiered retention, cost dashboard, monthly review; Observability spend >15% of infrastructure |
| TR-019 | Doc 6 | Scanner false negatives allow a malicious file to be served | Low; High; Layered scanning, type allow-list, no execution, quarantine, periodic red-team upload; Post-scan infection report |
| TR-020 | Doc 6 | Multi-region operations exceed the team's capacity at GA | Medium; High; One region per tenant, shared control plane minimized, regional runbooks, phased rollout; Regional incident load; runbook gaps |
| R7-01 | Doc 7 | MOD-07 grows into a giant module as features accrete | Medium; Medium; Granularity review each quarter; split candidates pre-identified (exceptions, cost postings) behind existing contracts |
| R7-02 | Doc 7 | T3 edges couple edge modules to owning modules and block independent extraction | Medium; Medium; Allow-list in AT-05; extraction plan converts T3 to T5 sagas |
| R7-03 | Doc 7 | Audit chain-head lock becomes a per-tenant write bottleneck at peak | Low; High; Batch appends per transaction; measure in load tests; Document 14 to define sharded chains if needed |
| R7-04 | Doc 7 | Per-aggregate head-of-line blocking hides a dead delivery behind "slow" dashboards | Medium; Medium; Dead-delivery alert per consumer; stale labelling through `as_of` |
| R7-05 | Doc 7 | SPI implementations multiply and make closure or period checks slow | Low; Medium; Each SPI call budgeted at 50 ms; contract tests time them |
| R7-06 | Doc 7 | Developers bypass contracts with raw SQL across schemas | Medium; High; AT-08 SQL lint, database grants per schema, code review |
| R8-01 | Doc 8 | The per-tenant audit chain-head lock serialises the commit tail of every writing transaction in a tenant, worst during import commits | Medium; Medium; Lock taken last (§4.3); import commits in batches of at most 500 rows per transaction; lock-wait metric and alert; revisit ADR-012 with sharded chains if P95 wait exceeds 50 ms |
| R8-02 | Doc 8 | A missing future partition makes expense inserts fail (capture outage) | Low; High; Three partitions ahead, P2 alert below two, daily pipeline job, runbook; mobile capture continues offline |
| R8-03 | Doc 8 | Cross-schema references without foreign keys allow orphans through a code path that skips the contract | Medium; Medium; Same-transaction contract validation; nightly orphan report; schema test forbids cross-schema SQL in module code |
| R8-04 | Doc 8 | The per-tenant logical restore is complex and rarely exercised | Low; High; Scripted tooling; quarterly drill on staging with two synthetic tenants; two-person control |
| R8-05 | Doc 8 | RLS scope arrays and restrictive policies degrade hot queries as data grows | Medium; Medium; Scope-leading indexes; `EXPLAIN` assertions for the top queries in CI (Document 3 §7.8) |
| R8-06 | Doc 8 | Expense number counter contention during large import commits for one show | Low; Low; Range allocation in one statement; counter row locked last among financial locks |
| R8-07 | Doc 8 | `expense.cost_posting` growth (about 15 million rows a year) slows reconciliation and rebuilds | Medium; Medium; BRIN and dimension indexes; period-close snapshots; OQ-8-02 |
| R9-01 | Doc 9 | Contract-first OpenAPI drifts from implementation when teams edit generated code | Medium; High; Generated folders read-only; conformance and route-inventory tests in CI |
| R9-02 | Doc 9 | Error-code proliferation makes client handling inconsistent | Medium; Medium; Catalogue review in the architecture forum; codes added only with a rule reference |
| R9-03 | Doc 9 | Idempotency storage grows under heavy sync and import retries | Low; Medium; 7-day purge; partition `ops.idempotency_key` by day if volume requires |
| R9-04 | Doc 9 | Clients misuse action links as authority | Low; Medium; Documented as advisory; server re-authorises every request |
| R9-05 | Doc 9 | Webhook receivers fail to verify signatures | Medium; Medium; Reference verification snippets in the integration guide; test endpoint |
| R10-01 | Doc 10 | Fabricated or AI-generated receipts pass technical checks; authenticity depends on auditor review | High; High; Evidence policy, auditor verification and dispute, duplicate hashing, capture metadata retained, perceptual hashing and anomaly scoring at Phase 1 |
| R10-02 | Doc 10 | No content disarm and reconstruction at MVP; a zero-day in a PDF or image decoder could reach a reviewer | Low; High; Content validation, attachment disposition, sandbox CSP, pdf.js with scripting disabled, previews in an isolated renderer; revisit CDR at GA (OQ-10-05) |
| R10-03 | Doc 10 | Rate limits become per-replica approximations when Redis is lost, allowing a short abuse window | Medium; Low; Edge limits remain; lockout and revocation unaffected; Redis Multi-AZ; alert on fallback mode |
| R10-04 | Doc 10 | CERT-In and DPDP obligations are interpreted incorrectly, leading to a missed reporting deadline | Medium; High; Legal validation (OQ-10-01, OQ-10-02); clocks designed to the strictest reading; quarterly drill |
| R10-05 | Doc 10 | A purged tenant's data persists in encrypted backups for up to 35 days, and storage-level encryption is not per tenant | Certain; Medium; Field-level secrets crypto-shredded immediately; backups age out within 35 days; disclosed in the DPA; restore of a purged tenant prohibited by runbook |
| R10-06 | Doc 10 | Signature-based scanning misses novel malware | Medium; Medium; Structural checks before scan, rescan on signature update, safe serving rules; second engine evaluated (OQ-10-05) |
| R10-07 | Doc 10 | Customers require SOC 1 Type II and none is planned | Medium; Medium; Confirm early (OQ-10-04); SOX-supporting controls already produce evidence (section 9.5) |
| R10-08 | Doc 10 | Expense splitting below approval limits is not detected at MVP | Medium; Medium; Duplicate rule and `UNUSUAL_AMOUNT` exceptions at MVP; split-pattern detection at Phase 1; auditor sampling |
| R11-01 | Doc 11 | iOS grants too little background time to meet NFR-MOB-003 when the app is closed | High; Medium; Foreground sync on open and unlock; BGProcessing; queue-age reminders; server reports aged queues; OQ-11-01 |
| R11-02 | Doc 11 | Device-bound `K_db` lets an attacker controlling a running, once-unlocked device read local data within the grant window (extends Document 3 R4-04) | Low; High; Presence-bound `KP` for all network and UI authority; 7-day bound; background window; remote wipe; root detection signal |
| R11-03 | Doc 11 | An Expo SDK or native module upgrade breaks SQLCipher or keystore access, making local data unreadable | Medium; High; Pinned modules; upgrade migration tests with populated outboxes; recovery wrap `K_rec`; staged rollout; pre-migration database copy |
| R11-04 | Doc 11 | OEM Android keystore defects invalidate keys and strand an offline auditor until online sign-in | Medium; Medium; Recovery wrap; device matrix testing on pilot hardware; clear recovery messaging |
| R11-05 | Doc 11 | Auditors share profile PINs on shared devices, defeating attribution | Medium; Medium; PIN required per profile; attempts limit; access review signals on unusual profile switching; training |
| R11-06 | Doc 11 | Held and salvaged items accumulate unreviewed, or an uninstall destroys a queue | Medium; Medium; Held-item dashboard and 7-day escalation; seq-gap and no-contact detection reported to the Org Admin |
| R11-07 | Doc 11 | The 30-second target is missed on low-end Android devices | Medium; Medium; Native image worker; local indexes; telemetry on `capture.completed`; usability test gate |
| R11-08 | Doc 11 | Location capture is challenged as disproportionate monitoring of auditors | Low; Medium; Off by default; consent; coarse precision; capture only at save; OQ-11-02 |
| R12-01 | Doc 12 | Legacy formats vary so much that mapping each firm is slow, delaying onboarding | High; Medium; Mapping profiles, header synonyms, suggestions in preview, onboarding service, sample corpus per firm (OQ-12-02) |
| R12-02 | Doc 12 | Domain validation of complex expenses misses the 10 s per 1,000 rows target at 50,000 rows | Medium; Medium; Preloads, set-based lookups, parallel chunks, CI benchmark gate |
| R12-03 | Doc 12 | An all-or-nothing commit aborts after chunks committed and compensation produces confusing history | Low; Medium; Revalidation and lease make aborts rare; events held; alert on every abort; clear audit reason `COMMIT_ABORTED` |
| R12-04 | Doc 12 | Opening balances distort period analytics and trigger duplicate warnings | Medium; Medium; Brought-forward presentation (RC-12-04); batch-level justification; detail mode where periods allow |
| R12-05 | Doc 12 | A crafted workbook exploits the parser | Low; High; File gate before parse, malware scan, sandboxed worker, fuzzing, patch SLA |
| R12-06 | Doc 12 | Exported files leak outside the organization | Medium; High; Scope snapshot and intersection, watermarks, step-up for restricted reports, 15-minute links, download audit, exfiltration alert |
| R12-07 | Doc 12 | Spreadsheet double precision or user rounding causes reconciliation differences | Medium; Low; 15-digit normalisation, scale errors instead of rounding, text form for very large amounts, control totals |
| R13-01 | Doc 13 | Supersession reversals in the event period make open-period reports show offsetting entries (GX-06) and confuse reviewers | Medium; Medium; Economic-basis view; labelled period basis; OQ-13-01 |
| R13-02 | Doc 13 | Projection lag or out-of-order events show stale or inconsistent dashboards | Medium; High; Version guards, recompute-from-sums, checkpoints, freshness badges, nightly RCN checks, shadow rebuild |
| R13-03 | Doc 13 | Benchmarks re-identify an organization in thin cells | Low; High; Minimum 5 organizations, one value per organization, region-bound, consent (OQ-15 of Document 1) |
| R13-04 | Doc 13 | Pending claims against commitments reduce committed cost and flatter variance | Medium; Medium; Exposure (M-09) and remaining (M-12) shown beside variance; pending column on RPT-01 |
| R13-05 | Doc 13 | Per-component FX conversion differs from invoice-level conversion by 0.0001 amounts and triggers reviewer queries | Medium; Low; Documented rule, GX-04, tolerance POL-042 |
| R13-06 | Doc 13 | Alert fatigue from overlapping rules and volatile small lines | Medium; Medium; Cooldown, hysteresis, minimum budget floor, overlap warning, false-positive suppression |
| R14-01 | Doc 14 | Per-tenant chain-head locking throttles write throughput for a busy tenant during bulk import or period close | Medium; Medium; Pre-commit append, lock-wait metric, chain lanes (ADR-059), import chunking |
| R14-02 | Doc 14 | New code paths leak personal or financial data into telemetry despite the allow-list | Medium; High; Field registry, source and collector redaction, CI test, daily scanner, RB-SEC-07 |
| R14-03 | Doc 14 | Telemetry and audit retention costs grow faster than revenue, especially in-region duplication for two regions | Medium; Medium; Sampling, downsampling, Parquet archives, cost dashboard OPS-DSH-11, quarterly review |
| R14-04 | Doc 14 | Security events written outside the business transaction are lost in a crash between rejection and write | Low; Medium; Synchronous write before response, fallback log channel, reconciler, ALR-028 |
| R14-05 | Doc 14 | Alert fatigue from new SLOs and detections causes missed pages | Medium; High; Burn-rate alerting, 30-day hygiene review, synthetic tests, ownership rule |
| R14-06 | Doc 14 | Audit search misses its 5-second target at 10-year volume because of cross-partition scans | Low; Medium; Mandatory date range, tenant-leading indexes, archive offload beyond 24 months, SLO-15 |
| R15-01 | Doc 15 | Sustained loss of a provider region leaves its tenants unavailable beyond the 4-hour RTO, because ADR-009 forbids out-of-region copies | Low; High; Disclose in the service description (Document 6 RA-05); offline capture and Excel fallback (Document 6 §12.7); decide OQ-15-01 before GA |
| R15-02 | Doc 15 | In an account-compromise scenario (DS-4) the recoverable point is the last hourly copy, not 15 minutes | Low; High; Hourly locked copies; SCPs against backup deletion; device queues re-push; evaluate continuous copy when the provider supports it |
| R15-03 | Doc 15 | Region directory becomes a global dependency for sign-in | Medium; Medium; CDN-cached responses for known slugs, regional fallback URL in invitation links, mobile stores its region after first sign-in |
| R15-04 | Doc 15 | Per-tenant KMS keys and the ABAC signer role add request latency and quota pressure at scale | Medium; Medium; Bucket keys, session caching ≤15 minutes, quota alarms at 75%, load test LT scenarios in Document 16 |
| R15-05 | Doc 15 | Recovery accounts drift from production because they are rarely used | Medium; High; Same Terraform modules; daily drift plan; quarterly drill builds and verifies the stack |
| R15-06 | Doc 15 | Dedicated cells fall behind the release train and accumulate skew | Medium; Medium; Wave 3 with a one-release skew limit; cell deployments in the same workflow; skew alert |
| R15-07 | Doc 15 | Managed Redis engine differs from the 7.4 baseline (A15-03) | Medium; Low; Pinned engine parity across environments; compatibility tests |
| R15-08 | Doc 15 | Month-end freeze and regional windows slow critical fixes | Low; Medium; Hotfix path is exempt; error-budget policy prioritises reliability changes |
| R16-01 | Doc 16 | Test execution time explosion from combinatorial authorization matrix | Medium; Medium; Equivalence class pruning and parallel worker distribution in CI (Document 3 §16.6) |
| R16-02 | Doc 16 | Subtle rounding divergences between frontend JavaScript and backend .NET | Low; High; Frontend performs zero monetary math; all display values formatted from server decimal strings |
| R16-03 | Doc 16 | Flaky mobile end-to-end tests due to emulator timing inconsistencies | Medium; Medium; Isolate network via local mock server; inject deterministic step synchronization in Maestro |

## 6. Open Questions and Decisions Register

All open items raised by Documents 1 to 16. Items blocking a milestone are repeated in section 4.

| ID | Source | Question or decision | Owner / need-by / status as stated |
|----|--------|----------------------|------|
| OQ-01 | Doc 1 | Which pilot customers are contracted, and what are their data residency choices? | Determines infrastructure region and onboarding plan; Product Owner; Before MVP build start |
| OQ-02 | Doc 1 | Is per-auditor-seat pricing acceptable to buyers who employ many seasonal auditors? | Pricing model viability; Product Owner / Finance; Before GA pricing page |
| OQ-03 | Doc 1 | Do pilot customers require auditor-firm-level (not individual) grants in MVP? | Could pull Phase 2 access model forward; Audit SME; Before MVP design freeze |
| OQ-04 | Doc 1 | Which ERP systems must be supported at Phase 1, and in which formats? | Integration design and effort; Finance SME; Phase 1 planning |
| OQ-05 | Doc 1 | Should evidence files be stored with customer-managed encryption keys? | Enterprise procurement requirement; Security; Before GA |
| OQ-06 | Doc 1 | What is the maximum acceptable evidence file size per expense? | Storage cost and mobile capture design; Product / Platform; Before MVP build start |
| OQ-07 | Doc 1 | Are there jurisdiction-specific tax rules beyond GST/VAT that must be modelled in MVP? | Expense calculation correctness; Finance SME; Before MVP build start |
| OQ-08 | Doc 1 | Does any pilot customer require offline capture for more than 7 days? | Offline design and security exposure; Mobile Architect; Before MVP build start |
| OQ-09 | Doc 1 | Who is the accountable Data Protection Officer, and in which jurisdiction? | Privacy governance and DSR workflow; Legal; Before pilot go-live |
| OQ-10 | Doc 1 | Will the platform act as data processor or joint controller for customer auditor data? | Contractual and privacy obligations; Legal; Before pilot go-live |
| OQ-11 | Doc 1 | What is the approved retention for security logs versus financial records? | Storage design and compliance evidence; Security / Legal; Before GA |
| OQ-12 | Doc 1 | Is a mobile web app acceptable to customers who decline to install a native app? | Adoption and offline capability; Product Owner; Before MVP design freeze |
| OQ-13 | Doc 1 | Which languages are required at GA beyond English? | Localization effort; Product Owner; Phase 1 planning |
| OQ-14 | Doc 1 | What SLA and service credits will be offered at GA? | Contractual commitments and ops design; Product Owner / Legal; Before GA contracts |
| OQ-15 | Doc 1 | Who approves benchmark participation, and is opt-in required? | Privacy and competitive sensitivity; Legal / Product; Phase 1 planning |
| OD-01 | Doc 2 | Pilot hosting topology: H1 commissioner-hosted versus H2 producer-hosted (ADR-025) | Onboarding, role seeding and portfolio scope differ; Business owner; Open — needed before pilot onboarding |
| OQ-4-02 | Doc 3 | May an audit firm supervisor hold a read-only grant to a client to review working papers? | No at MVP. Supervision happens through the firm's own quality review process, outside the client tenant. A supervisor grant would double the external principal… |
| OQ-4-03 | Doc 3 | Should grant access history be exportable by the client as a signed evidence pack? | Yes, Phase 1. At MVP the register and the audit events are exportable as CSV; a signed, hash-chained pack is deferred; Phase 1 design |
| OQ-4-04 | Doc 3 | What is the maximum number of concurrent grants a single auditor may hold? | Twenty. Above that, the workspace list becomes unusable and the auditor is almost certainly holding stale grants; the limit is a soft warning, not a hard block… |
| G-01 | Doc 3 | Document renumbered from 4 to 3 and retitled "Actor, Permission, Tenancy & Identity Architecture"; document references renumbered to the re-baselined set | Governing brief document structure (D-023, `00-foundation-index.md`) |
| G-02 | Doc 3 | Logical table names `document.*` and `audit.*` in sections 6 and 7 written as the physical schemas `docs.*` and `auditops.*` | Document 6 section 8.2 schema names are binding |
| G-03 | Doc 3 | Section 17 and Appendices A, D, E and G completed | Draft tasks T7 and T8 |
| G-04 | Doc 3 | Cross-module references: within a module schema, composite foreign keys `(org_id, id)` enforce same-tenant integrity; across module schemas, no foreign key exists and `o… | Reconciles section 9.9 with Document 6 section 5.4 and ADR-001; detailed in Document 8 |
| G-05 | Doc 3 | `COST_CONTROLLER` added as an eighteenth system role; `FINANCE_HEAD` renamed "Finance Head" | Governing brief lists Cost Controller as a distinct actor; Document 1 assigns revision, reallocation and period lock to Cost Controllers and reopen approval to… |
| G-06 | Doc 3 | Production-side roles may be seeded in OTT and broadcaster tenants that host a commissioned show (section 5.5 note) | ADR-025, provisional, owned by Document 2 |
| G-07 | Doc 3 | Legacy permission keys normalised (Appendix A.3) | Nine-action rule in section 5.2 |
| OQ-4-05 | Doc 4 | Should external grantees capture expenses and submit audit reports (CAT-4-01 to 03), or capture only working papers? | Extend `AUDIT_EVIDENCE`; Business owner, Audit SME; Before pilot onboarding |
| OQ-4-06 | Doc 4 | How does an H2 appointing commissioner create a grant in the producer's tenant? | Interim path in WF-06; Document 3 §8.13 to specify; Security Architect; Before H2 pilot |
| OQ-4-07 | Doc 4 | Must every new vendor be activated by a `vendor.vendor.approve` holder, or may clean registrations auto-activate? | Approval required; Finance SME; Design freeze |
| OQ-4-08 | Doc 4 | Is cancelling an approved expense by one human holding both `delete` and `approve` sufficient, or is a second approver required? | One human, not the submitter, with step-up; Finance SME; Design freeze |
| OQ-4-09 | Doc 4 | Must the final approval step block on an `OVER_BUDGET` exception or only warn? | Warn; Finance Head configurable block; Finance SME; Design freeze |
| OQ-4-10 | Doc 4 | May a quarantined file be released after a confirmed false positive? | No at MVP; re-upload; Security Architect; Phase 1 |
| OQ-5-01 | Doc 5 | Confirm the statutory citation and value of the cash payment limit under the Income-tax Act 2025 effective 1 April 2026, the goods-carriage exception and Rule 6DD-equiva… | Wrong statutory default; Tax Advisor; Open — before pilot |
| OQ-5-02 | Doc 5 | Should a cancellation of an expense dated in a locked period require Finance Head approval in addition to normal cancellation permission (RC-05-03)? | Additional approval step; Finance SME; Open |
| OQ-5-03 | Doc 5 | Are per-day limits (POL-021) per submitter or per payee in pilot practice? | Limit evaluator key; Audit SME; Open |
| OQ-5-04 | Doc 5 | Must the monthly override review be signed by the Audit Manager, the Compliance Officer, or both? | Review workflow; Compliance; Open |
| AQ-01 | Doc 6 | Which cloud region serves EU tenants, and when does the second region go live? | Determines the ADR-009 provisioning order and the residency statement in contracts; Platform Architect; Before the GA design freeze |
| AQ-02 | Doc 6 | What tenant size or workload triggers a dedicated deployment? | Affects the RLS capacity model, noisy-neighbour controls, and pricing; Principal Architect; Phase 1 planning |
| AQ-03 | Doc 6 | May external auditors see evidence thumbnails, or only metadata plus an audited download? | Changes the scope model and the Doc 4 permission catalogue; Product Owner, Compliance; Before Doc 4 review |
| AQ-04 | Doc 6 | How long are rejected import batches and quarantined files retained? | Affects storage cost, DSR handling, and retention policy; Data Architect; Before Doc 14 review |
| AQ-05 | Doc 6 | Who may override an FX rate, and does an override above a threshold need dual approval? | A control gap would weaken the variance and audit story; Finance SME; Before pilot |
| AQ-06 | Doc 6 | Is the report renderer headless Chromium or a server-side PDF library? | Affects worker image size, attack surface, and TR-006; Platform Architect; Before Doc 13 design |
| AQ-07 | Doc 6 | Are tenant-configurable webhook subscriptions in MVP or Phase 1? | Determines whether INTG-08 ships at pilot and how the outbox is exposed; Product Owner; Before MVP scope lock |
| AQ-08 | Doc 6 | What consent model and anonymity threshold apply to cross-tenant benchmarking? | Benchmarking without consent is a privacy defect; small cohorts can re-identify; Product Owner, Legal; Before CAP-018 design |
| AQ-09 | Doc 6 | Does the app-store and audit context permit OTA JavaScript updates for a financial app? | A regulatory objection would force store-only releases and slow fixes; Mobile Lead, Compliance; Before mobile design freeze |
| AQ-10 | Doc 6 | What support impersonation model is permitted, and who approves it? | Support access is the highest-risk privileged path after break-glass; Security & Compliance; Before pilot |
| G-01 | Doc 6 | Cloud provider and launch regions committed | Executive sponsor, Platform Architect; Terraform workspaces, residency statement |
| G-02 | Doc 6 | Mobile framework confirmed or replaced | Principal Architect, Mobile Lead; Doc 15 start |
| G-03 | Doc 6 | Retention and legal-hold policy signed off | Compliance, Legal; Doc 14, object-storage lifecycle |
| G-04 | Doc 6 | Penetration-test scope and vendor selected | Security & Compliance; Pilot entry gate |
| G-05 | Doc 6 | DR exercise scheduled with pilot tenants informed | SRE Lead; GA readiness |
| G-06 | Doc 6 | Support impersonation model approved | Security & Compliance; Production access design |
| OQ-7-01 | Doc 7 | Should the same-layer edge MOD-11 → MOD-08 be replaced by digest content pushed through events? | Simpler graph versus heavier events; Architecture review |
| OQ-7-02 | Doc 7 | Is a dedicated `render` lane needed at MVP, or can PDF rendering share `bulk` until GA? | Cost versus isolation; Platform Lead |
| OQ-7-03 | Doc 7 | Should audit reports be submittable offline (Phase 1)? | Sync scope extends to MOD-06 commands, a new T3 edge; Product Owner, Audit SME |
| OQ-8-01 | Doc 8 | Is minutes-level archive retrieval acceptable to auditors reviewing closed shows older than 24 months? | Drives A8-09; Audit SME |
| OQ-8-02 | Doc 8 | Should `expense.cost_posting` be partitioned by posting year once it passes about 50 million rows? | Reversal FK must then include the partition key; Principal Architect at GA |
| OQ-8-03 | Doc 8 | Is `YY` in the expense number the calendar year or the organization fiscal year of first submission? | Counter key and number format; Finance SME |
| OQ-8-04 | Doc 8 | Is a per-tenant full rewind (§11.2) offered contractually, or only scoped repair? | DPA wording and restore tooling scope; Business owner, Legal |
| OQ-9-01 | Doc 9 | Should bulk approval be limited to expenses below a configurable amount? | Control strength versus approver productivity; Finance SME |
| OQ-9-02 | Doc 9 | Should dashboard responses support `currency_view=ORIGINAL`, or is base currency sufficient? | Additional projection columns; Product Owner |
| OQ-9-03 | Doc 9 | Is a public, customer-facing API (beyond webhooks and service accounts) in scope for Phase 1? | Developer portal, quotas, API keys; Product Owner |
| OQ-10-01 | Doc 10 | Do the CERT-In Directions apply to SlateIQ as a SaaS processor, and do they reach EU-region infrastructure serving Indian customers? | Legal; Before India pilot go-live |
| OQ-10-02 | Doc 10 | Under the DPDP Rules 2025, which log retention period and breach-reporting content apply to SlateIQ as Data Processor, and what must the DPA require? | Legal, DPO; Before pilot go-live |
| OQ-10-03 | Doc 10 | Should customer-managed keys (Document 1 OQ-05) be offered at GA? The per-tenant data key supports it; key custody and availability terms do not yet exist | Security, Product Owner; Before GA |
| OQ-10-04 | Doc 10 | Do pilot customers require a SOC 1 Type II report for ICFR reliance in addition to SOC 2? | Product Owner, Finance SME; Before GA contracts |
| OQ-10-05 | Doc 10 | Is a second malware engine or a detonation sandbox required for evidence from untrusted vendors? | Security; Before GA |
| OQ-10-06 | Doc 10 | When does the ASVS baseline move from 4.0.3 to 5.0? | Security; Before the GA assessment |
| OQ-10-07 | Doc 10 | Paid bug bounty at GA or vulnerability disclosure programme only? | CISO; Before GA |
| OQ-10-08 | Doc 10 | Should cumulative variations per budget version count against the direct-apply threshold (T10-08)? | Finance SME; Before MVP design freeze |
| OQ-11-01 | Doc 11 | Does NFR-MOB-003 (95% within 5 minutes of connectivity) apply when the app is not running, given iOS background limits? | Product Owner, Mobile Architect; Before MVP build start |
| OQ-11-02 | Doc 11 | Is location capture wanted for the pilot, and is the lawful basis consent or legitimate interest under GDPR and DPDP? | Legal, Product Owner; Before pilot go-live |
| OQ-11-03 | Doc 11 | Will pilot customers share devices, and should shared-device mode be the default for them? | Product Owner; Before pilot onboarding |
| OQ-11-04 | Doc 11 | Is a 14-day salvage window acceptable to audit and HR processes for departing auditors? | Audit SME, Compliance; Before MVP build start |
| OQ-11-05 | Doc 11 | Is RC-11-04 (idle timeout semantics for mobile) accepted by Security? | Security Architect; Before MVP build start |
| OQ-12-01 | Doc 12 | Will pilot customers enable partial commit, and is 5% the right default threshold? | Finance SME; Before MVP build start |
| OQ-12-02 | Doc 12 | Which legacy auditor formats must be mapped for the pilot? Sample workbooks are needed per firm | Audit SME, Product Owner; Before pilot onboarding |
| OQ-12-03 | Doc 12 | Is summary or detail the preferred opening-balance mode, and is ledger evidence acceptable (RC-12-03)? | Finance SME; Before pilot onboarding |
| OQ-12-04 | Doc 12 | Must the audit report export be signed (PDF signature) for appointing parties? | Audit SME; Before MVP build start |
| OQ-12-05 | Doc 12 | Do legacy formats require `.xls` (BIFF) support at pilot? | Product Owner; Before MVP build start |
| OQ-13-01 | Doc 13 | When a superseded revision's original period is still open, should the reversal post to that period instead of the event period? BR-EXP-010 as written requires the event… | Period reports, R13-01; Finance SME; Open — before MVP build |
| OQ-13-02 | Doc 13 | Is a display currency other than base needed at MVP, and at which rate? | Report layer and FX policy; Product Owner; Open |
| OQ-13-03 | Doc 13 | May alert emails contain absolute amounts by default (RC-13-06)? | Notification templates, privacy posture; Product Owner, Security; Open |
| OQ-13-04 | Doc 13 | Linear EFC progress: shoot days, calendar days or episodes delivered? | M-21 results; Finance SME; Open (Phase 1) |
| OQ-13-05 | Doc 13 | Must GSTIN validity be checked online (GSTN) before tax is treated as recoverable? | Recoverability accuracy; integration effort; Tax Advisor; Open |
| OQ-14-01 | Doc 14 | Which telemetry backend: self-hosted open-source stack per region or a regional managed service? | SRE Lead; Before MVP build start |
| OQ-14-02 | Doc 14 | Which SIEM receives the Phase 1 streaming export, and can it be region-local for India and EU? | CISO; Phase 1 planning |
| OQ-14-03 | Doc 14 | After contract exit and tenant purge, are audit archives retained by SlateIQ for 7 years, returned to the customer, or both? | Legal, DPO; Before pilot contracts |
| OQ-14-04 | Doc 14 | Should tenants receive a self-service audit-bundle verifier (open-source command-line tool) at GA? | Product Owner; Before GA |
| OQ-14-05 | Doc 14 | Approved retention for security logs versus financial records (Document 1 OQ-11) | Security, Legal; Before GA |
| OQ-15-01 | Doc 15 | Should an in-jurisdiction secondary region (Hyderabad for `IN`, a second EU region for `EU`) hold backup copies before GA, amending ADR-009? | Without it, DS-7 has no bounded RTO; Principal Architect, DPO, business owner; GA design freeze |
| OQ-15-02 | Doc 15 | Are the dedicated-deployment triggers of §14.4 acceptable (answers Document 6 AQ-02)? | Pricing and capacity model; Principal Architect, Product Owner; Phase 1 planning |
| OQ-15-03 | Doc 15 | Self-hosted scanner container or a managed in-region scanning service? | Operations load versus subprocessor and residency review; Security Architect; Before pilot |
| OQ-15-04 | Doc 15 | Which managed Redis engine version is available per region at commitment? | Engine parity with local and test; Platform Architect; G-01 |
| OQ-15-05 | Doc 15 | Is advanced DDoS protection required before GA? | Cost versus NFR-SEC-013 exposure; Security Architect; GA readiness |
| OQ-15-06 | Doc 15 | Which device farm provider, given that only synthetic data is used? | Mobile release gate (§11.4); Mobile Lead; Before pilot |
| OQ-16-01 | Doc 16 | Should performance soak tests run 72 hours continuously in weekly CI or monthly? | SRE Lead; Open; Pilot phase velocity review |
| OQ-16-02 | Doc 16 | Will device farm testing for mobile include on-device biometric hardware simulation? | Mobile Lead; Open; Phase 1 mobile testing baseline |

---

## Document Approval

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Principal Architect | | | |
| Security & Compliance | | | |
| Delivery Lead | | | |
| Product Owner | | | |

*End of Document 17*
