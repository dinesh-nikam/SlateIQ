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
| `tenancy` | `sod_rule`, `permission`, `grant_profile` | E | Global, read-only |
| `identity` | `user` (keyed on `home_org_id`), `user_pii`, `user_email_alias`, `credential`, `mfa_factor`, `recovery_code`, `refresh_session`, `device_registration`, `password_reset`, `email_change`, `service_account`, `federated_identity`, `identity_transfer` | A | Secrets readable only by the identity service role |
| `prod` | `show`, `season`, `episode`, `department`, `category`, `category_department`, `party`, `show_party`, `party_organization_link`, `show_closure` | B (C allowlist for show, season, episode, department, category) | `party_organization_link` is visible to both linked organizations through a published view |
| `budget` | `budget`, `budget_version`, `budget_line`, `episode_allocation`, `variation`, `approval_workflow`, `approval_workflow_step`, `financial_period`, `period_close_run`, `forecast` | B (C allowlist for budget, budget_version, budget_line) | `forecast` is Phase 1 |
| `vendor` | `vendor`, `vendor_tax_registration`, `vendor_contact`, `vendor_bank_reference`, `manual_commitment`, `commitment_amendment`, `purchase_order`, `purchase_order_line`, `po_receipt`, `payment_reference` | A for vendor master tables; B for commitment and purchase-order tables (C allowlist for vendor, manual_commitment) | `vendor_bank_reference` is Restricted; column encryption |
| `auditops` | `audit_team`, `audit_team_member`, `audit_assignment`, `audit_scope`, `auditor_work_profile`, `coverage_requirement`, `audit_report`, `audit_finding`, `finding_response`, `audit_evidence` | B (C allowlist per section 7.4) | |
| `expense` | `expense`, `expense_line`, `expense_split`, `expense_tax`, `evidence_link`, `approval_instance`, `approval_decision`, `expense_amendment`, `exception`, `exception_activity`, `expense_comment`, `cost_posting` | B (C allowlist for expense, expense_line) | `cost_posting` is append-only (Document 8) |
| `intel` | `cost_fact`, `commitment_fact`, `variance_snapshot`, `alert_rule`, `alert_instance`, `saved_view`, `dashboard_definition`, `benchmark_aggregate`, `projector_checkpoint` | B; `benchmark_aggregate` is E (anonymised, cross-tenant by design, Phase 1) | Rebuildable |
| `docs` | `document`, `document_version`, `document_link`, `document_access_log`, `scan_result`, `upload_ticket`, `retention_policy` | B (C allowlist for document, document_version) | `document_access_log` is append-only |
| `xls` | `excel_template`, `template_version`, `value_mapping`, `import_batch`, `import_row`, `export_job` | A for templates; B for batches and jobs | |
| `notify` | `notification_template`, `notification`, `notification_preference`, `delivery_attempt`, `digest_run`, `escalation_policy`, `announcement` | A with recipient self-read | |
| `compliance` | `audit_event`, `audit_chain_head`, `audit_anchor`, `security_event` | D | Append-only trigger |
| `compliance` | `legal_hold`, `legal_hold_target`, `dsr_request`, `ropa_entry`, `breach_incident`, `control_evidence` | A | |
| `report` | `report_definition`, `report_run`, `report_output`, `report_schedule` | B | `report_schedule` Phase 1 |
| `sync` | `device_sync_state`, `sync_cursor`, `mutation_log`, `conflict_record` | A with device-owner self-read | |
| `ops` | `outbox_event`, `event_delivery`, `inbox_dedup`, `job`, `job_attempt`, `idempotency_key` | A (row carries `org_id`; platform rows use the platform tenant) | Workers set tenant context per row |
| `ops` | `tenant_region`, `provisioning_run`, `support_access`, `feature_flag` | F | Not granted to `slateiq_app` |
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
