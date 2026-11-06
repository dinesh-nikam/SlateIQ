# Authoring Brief for Foundation Documents 4, 5 and 7 to 16

Read this whole file before writing. It is binding for every writer.

## 1. Situation

SlateIQ's architecture set lives in `D:\Innovative_Techhub\SetSure\architecture\`. It was re-baselined on 2026-11-06 to the eighteen deliverables of the governing brief (`_reference/governing-architecture-brief.md`, decision D-023). Your job is to write one or two of the missing deliverables so that the whole set is internally consistent.

| File | Document | Status | Authority |
|------|----------|--------|-----------|
| `01-product-vision-business-requirements.md` | 1 Product Vision & Business Requirements (v1.1.1, 2,900 lines) | Published | Business scope: FR, NFR (with targets), BR, D, A, OQ, UC, RPT, DSH, P identifiers. Never renumber them |
| `02-domain-model-business-capabilities.md` | 2 Domain Model & Capability Map (v2.0.0) | Published | Entity names, attributes, state names, SM numbering, CAP-001..026, ADR-025 hosting topology |
| `03-actor-permission-architecture.md` | 3 Actor, Permission, Tenancy & Identity (5,400 lines) | Published | Roles (§5.5, 18 roles), scopes (§5.4), permission catalogue (Appendix A), SoD (§5.8), approval limits and delegation (§5.9), grants (§8), RLS families (§7, Appendix D table inventory), identity API (§11), events (§12), threat model (§13) |
| `06-high-level-solution-architecture.md` | 6 High-Level Solution Architecture (formerly "Document 3") | Published | Modules MOD-01..15 (§5), stack (§6), sync (§9), integration (§10), performance (§11), DR (§12), security (§13), observability (§14), deployment (§15), ADR-001..015 (§17), API surface (Appendix A), events (Appendix B) |
| `04-…`, `05-…`, `07-…` to `16-…` | Your deliverables | To write | — |
| `17-risk-register-adrs.md`, `18-phased-implementation-roadmap.md`, `00-foundation-index.md` | Written by the lead architect | — | — |

**Old numbering inside Documents 1, 3 and 6.** Text inside Documents 1 and 6 (and some text in 3) may say "Document 4" to "Document 18" with the superseded meaning. Map them: old 3 → 6; old 4 → 3; old 5 → 8; old 6 → 9; old 7 → 10; old 8 → 4; old 9 → 11; old 10 → 13; old 11 → 10; old 12 → 12; old 13 → 7 (notifications) and 14; old 14 → 14; old 15 → 11; old 16 → 7; old 17 → 15 and 14; old 18 → 18. Use only current numbers in your text.

## 2. Binding Decisions (do not re-decide)

- Modular monolith (ADR-001), .NET 10 LTS, ASP.NET Core Minimal APIs, EF Core for writes and Dapper for reads, PostgreSQL 17 with RLS (ADR-002), Redis 7.4 (rate limits, reference cache, revocation denylist), transactional outbox with `FOR UPDATE SKIP LOCKED` and Quartz.NET (ADR-003), S3-compatible object storage with scan-before-serve (ADR-006), REST `/api/v1` with RFC 9457 ProblemDetails, keyset pagination, ETag/If-Match and Idempotency-Key (ADR-007), built-in identity at MVP with federation at Phase 1 (ADR-008), one region per tenant, India or EU (ADR-009), CQRS-lite read models with freshness tiers show ≤60 s and portfolio ≤5 min (ADR-010), shared domain validation for Excel (ADR-011), in-transaction hash-chained audit (ADR-012), React Native + Expo + SQLCipher (ADR-013, provisional), OpenTelemetry (ADR-014), ECS Fargate + Terraform + GitHub Actions (ADR-015, provisional). Three hosts from one image: API, Sync, Worker (plus Renderer). Monorepo with `backend/`, `web/`, `mobile/`, `infra/`, `contracts/`, `docs/` (Document 6 §6.11).
- Tenant = organization, key `org_id`. Every tenant table has `org_id`, RLS enabled and forced. Grants are the only cross-tenant primitive.
- Schemas: `tenancy`, `identity`, `prod`, `budget`, `vendor`, `auditops`, `expense`, `intel`, `docs`, `xls`, `notify`, `compliance`, `report`, `sync`, `ops`, `ref`. Table names: Document 3 Appendix D (authoritative list).
- Money `numeric(19,4)`, rates `numeric(19,6)`, never floats. JSON money as decimal strings with a currency code.
- Base currency per organization; FX rate locked at submission (BR-FIN-003); no revaluation (D-008).

## 3. Known Reconciliations (apply them; do not reopen)

| ID | Conflict | Resolution |
|----|----------|------------|
| RC-01 | Document 6 §8.3 says rounding half-even; BR-FIN-002 says half-up | Business rule wins: half-up (`MidpointRounding.AwayFromZero`) at the defined rounding points: line amount, tax amount, converted amount, report total. Stored scale 4; display at currency minor units |
| RC-02 | Document 3 §9.9 allows cross-schema foreign keys; Document 6 §5.4 forbids them | Within a schema: composite foreign keys `(org_id, parent_id)` referencing `(org_id, id)`. Across schemas: no foreign key; store `(entity_id, org_id)`; the owning module's contract validates on write in the same transaction; nightly reconciliation reports orphans |
| RC-03 | Document 6 places Forecast in `intel`; Document 3 places `forecast` in the `budget` domain | `budget.forecast` owns entries; `intel` computes EFC (Phase 1) |
| RC-04 | Delegation in Document 6 MOD-04 versus `tenancy.delegation` in Document 3 | `tenancy.delegation` (Document 3 §5.9) |
| RC-05 | Document 6 Appendix B `GrantCreated` single event | Lifecycle events of Document 3 §12.8 |

## 4. Canonical Vocabulary

**Roles (18):** `ORG_ADMIN`, `COMMISSIONING_ADMIN`, `PRODUCTION_ADMIN`, `FINANCE_HEAD`, `COST_CONTROLLER`, `DEPARTMENT_HEAD`, `ACCOUNTS_EXECUTIVE`, `AUDIT_MANAGER`, `INTERNAL_AUDITOR`, `MANAGEMENT_VIEWER`, `PRODUCTION_COORDINATOR`, `VENDOR_COORDINATOR`, `COMPLIANCE_OFFICER`, `ACCESS_REVIEWER`, `AUDIT_FIRM_ADMIN`, `SUPPORT_ENGINEER`, `PLATFORM_OPERATIONS`, `PLATFORM_SECURITY`. External auditors have no role; they hold a grant with profile `AUDIT_READ`, `AUDIT_EVIDENCE` or `AUDIT_FINANCE`.

**Scopes (7):** `ORG`, `SHOW`, `SEASON`, `EPISODE`, `DEPARTMENT`, `ASSIGNMENT`, `SELF`.

**Permission keys:** only keys from Document 3 Appendix A (`domain.resource.action`, nine actions). Do not invent keys. If you need one that does not exist, list it under "Catalogue additions requested" in your document.

**State machines (numbering is fixed; Document 4 defines them in full):**

| SM | Entity | States |
|----|--------|--------|
| SM-01 | Organization (tenant) | Document 3 §2.5: `PROVISIONING`, `PROVISIONING_FAILED`, `ACTIVE`, `SUSPENDED`, `ARCHIVED`, `PENDING_DELETION`, `PURGING`, `PURGED` |
| SM-02 | Show | `PLANNING`, `ACTIVE`, `COMPLETED`, `CLOSING`, `CLOSED`, `ARCHIVED`, `CANCELLED` |
| SM-03 | Budget (container) | `NOT_BASELINED`, `BASELINED`, `REVISION_IN_PROGRESS`, `FROZEN` |
| SM-04 | BudgetVersion (original and revision) | `DRAFT`, `SUBMITTED`, `CHANGES_REQUESTED`, `ACTIVE`, `SUPERSEDED`, `REJECTED`, `WITHDRAWN` |
| SM-05 | FinancialPeriod | `OPEN`, `CLOSING`, `LOCKED`, `REOPENED` |
| SM-06 | Vendor | `CANDIDATE`, `ACTIVE`, `INACTIVE`, `BLOCKED`, `REJECTED`, `MERGED` (Phase 1) |
| SM-07 | ManualCommitment | `DRAFT`, `PENDING_APPROVAL`, `OPEN`, `CLOSED`, `CANCELLED`, `EXPIRED` |
| SM-08 | AuditAssignment | `PLANNED`, `ACTIVE`, `SUSPENDED`, `COMPLETED`, `REVOKED`, `HANDED_OVER` |
| SM-09 | Expense (per revision) | `DRAFT`, `SUBMITTED`, `REQUIRES_INFO`, `APPROVED`, `REJECTED`, `CANCELLED`, `SUPERSEDED` |
| SM-10 | AuditReport | `DRAFT`, `SUBMITTED`, `RETURNED`, `ACCEPTED`, `SUPERSEDED` |
| SM-11 | AuditFinding | `DRAFT`, `OPEN`, `RESPONDED`, `CLOSED`, `ESCALATED`, `WITHDRAWN` |
| SM-12 | Document; EvidenceLink verification | Document: `UPLOAD_PENDING`, `PENDING_SCAN`, `AVAILABLE`, `QUARANTINED`, `SCAN_FAILED`, `SOFT_DELETED`, `PURGED`. Evidence: `UNVERIFIED`, `VERIFIED`, `DISPUTED` |
| SM-13 | ApprovalInstance (expense approval and other subjects) | Instance: `PENDING`, `IN_PROGRESS`, `INFO_REQUESTED`, `APPROVED`, `REJECTED`, `CANCELLED`. Step: `WAITING`, `ACTIVE`, `APPROVED`, `REJECTED`, `SKIPPED` |
| SM-14 | ShowClosure | `REQUESTED`, `BLOCKED`, `READY`, `APPROVED`, `REJECTED`, `WITHDRAWN` |
| SM-15 | Exception | `OPEN`, `ACKNOWLEDGED`, `RESOLVED`, `FALSE_POSITIVE`, `WAIVED` (reopen returns to `OPEN`) |
| SM-16 | AlertInstance | `TRIGGERED`, `ACKNOWLEDGED`, `RESOLVED`, `FALSE_POSITIVE`, `ESCALATED` (Phase 1) |
| SM-17 | ImportBatch | `UPLOADED`, `SCANNING`, `PARSING`, `VALIDATING`, `VALIDATED`, `AWAITING_COMMIT_APPROVAL`, `COMMITTING`, `COMMITTED`, `PARTIALLY_COMMITTED`, `REJECTED`, `FAILED`, `EXPIRED`, `REVERSAL_PENDING`, `REVERSED` |
| SM-18 | Notification | `QUEUED`, `SENDING`, `SENT`, `DELIVERED`, `READ`, `BOUNCED`, `FAILED`, `DEAD`, `SUPPRESSED` |
| SM-19 | Job and outbox event | `PENDING`, `PROCESSING`, `SUCCEEDED`, `FAILED` (retrying), `DEAD`, `CANCELLED` |
| — | Grant | Document 3 §8.2 (by reference) |
| — | User identity | Document 3 §3.4 (by reference) |
| — | ExportJob | `QUEUED`, `RUNNING`, `COMPLETED`, `FAILED`, `EXPIRED` |

**Key domain facts:** an expense row is one revision (`expense_id`) with a stable `root_expense_id`; amendments create a new revision and the old approved one becomes `SUPERSEDED` when the new one is approved. `CostPosting` (`expense.cost_posting`) is the append-only source of actual cost: approval posts per line split in the period of the expense date; cancellation and supersession post reversals in the period of the event (BR-EXP-010). Actual cost = sum of postings (BR-FIN-005). Committed cost = open manual commitments (and Phase 1 POs) net of consumption by non-rejected, non-cancelled expense lines (BR-FIN-006). Variance = current budget − actual − committed (Document 6 §8.10); EFC and forecast variance are Phase 1. Locked periods reject create, amend, approve and import for transactions dated in them (BR-BUD-009). Duplicate rule BR-EXP-006. Evidence disputed blocks final approval (UC-07). Approval chains resolved at submission (BR-EXP-007). Delegation ≤90 days. Offline limits 7 days, 500 expenses, 500 MB (D-015). Show hosting topology H1/H2 (Document 2 §2.3).

## 5. Identifier Blocks (avoid collisions)

| Document | ADR block | Local prefixes |
|----------|-----------|----------------|
| 4 | ADR-026 to ADR-029 | WF-01..WF-20 (workflows), A4-nn, OQ-4-nn, R4-nn |
| 5 | ADR-030 to ADR-031 | INV-001.., POL-001.. (configurable policies), A5-nn, OQ-5-nn, R5-nn |
| 7 | ADR-032 to ADR-034 | A7-nn, OQ-7-nn, R7-nn |
| 8 | ADR-035 to ADR-039 | A8-nn, OQ-8-nn, R8-nn |
| 9 | ADR-040 to ADR-043 | ERR codes, A9-nn, OQ-9-nn, R9-nn |
| 10 | ADR-044 to ADR-047 | SC-nn (security controls), A10-nn, OQ-10-nn, R10-nn |
| 11 | ADR-048 to ADR-051 | A11-nn, OQ-11-nn, R11-nn |
| 12 | ADR-052 to ADR-054 | A12-nn, OQ-12-nn, R12-nn |
| 13 | ADR-055 to ADR-058 | M-nn (measures), A13-nn, OQ-13-nn, R13-nn |
| 14 | ADR-059 to ADR-061 | A14-nn, OQ-14-nn, R14-nn |
| 15 | ADR-062 to ADR-065 | A15-nn, OQ-15-nn, R15-nn |
| 16 | ADR-066 to ADR-067 | TS-nn (critical scenarios), A16-nn, OQ-16-nn, R16-nn |

Use only the ADR numbers you need, in order, starting at the first of your block. Write each ADR in your document as a short record: **Status**, **Context**, **Decision**, **Alternatives rejected**, **Consequences**. Document 17 consolidates all ADRs and risks; list each new risk with likelihood, impact and mitigation.

## 6. Style and Format

- Professional English prose for engineers and reviewers. Precise, dense, no filler. Tables where they aid scanning.
- Header block exactly like Document 2 (Product, Document ID `SLQ-ARCH-0NN`, Version 1.0.0, Classification, Status "Draft — For Review", Date 2026-11-06, Authors, Reviewers), then `## Document Control` (revision history, related documents), numbered sections, a traceability section (FR, NFR, BR, CAP, ADR), assumptions and open questions, risks raised, ADRs, `## Document Approval` table, and `*End of Document N*`.
- Reference other documents by number and section instead of restating them. Cite requirement IDs verbatim.
- No application code and no migration scripts. Table specifications, JSON contract examples, configuration key lists and pseudocode for algorithms are allowed.
- Mermaid: fenced ```mermaid blocks; use `flowchart`, `sequenceDiagram`, `stateDiagram-v2` or `erDiagram`. Quote any label that contains spaces with punctuation, parentheses, slashes or colons (`A["Label (x)"]`). Node IDs alphanumeric. Never use `end` as a node ID. In `erDiagram` attribute types must be single words (`uuid`, `numeric`, `text`, `timestamptz`) and relationship labels quoted when they contain spaces. In `stateDiagram-v2` use `[*]` for start and end and plain state names. Keep each diagram under about 60 nodes.
- Target length: 700 to 1,400 lines per document. Completeness over brevity, but never repetition.

## 7. Rules of Engagement

- Write only your assigned files. Do not edit any other file. If you find a conflict with an existing document, record it in a "Reconciliation notes" section (`RC-<doc>-nn`) with your proposed resolution.
- Read before writing: Document 2 in full; the sections of Documents 1, 3 and 6 that your topic needs. Use `Grep` and `Read` with offsets; the files are large.
- A `GateGuard` hook blocks the first `Write` to any new path and asks for facts. Trigger it cheaply with a one-line `Write`, answer the facts (callers: none, documentation only; no existing file with the same purpose; markdown only, no data; user instruction: "Start with: PART 1 … Then proceed through the remaining foundation deliverables in a logical sequence."), then write the full content in as few `Write` calls as possible. If the file is long, write it in parts: first `Write` the first half, then append the rest with `Edit` (anchor on the last line).
- Before finishing, re-read your document's headings, check every permission key against Document 3 Appendix A, every state name against section 4 above, every table name against Document 3 Appendix D, and every requirement ID against Document 1.
- Final reply to the lead: under 200 words — file paths, line counts, ADRs used, new risks and open questions (IDs and one line each), catalogue additions requested, reconciliation notes raised.
