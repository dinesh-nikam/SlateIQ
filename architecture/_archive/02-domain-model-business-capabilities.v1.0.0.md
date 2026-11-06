# Document 2: Domain Model & Business Capability Map

**Product:** SlateIQ — Production Cost Intelligence & Audit Monitoring Platform  
**Version:** 1.0.0 (Architecture Foundation)  
**Classification:** Internal — Architecture Foundation  
**Status:** Draft — For Review  
**Date:** 2026-11-05  
**Authors:** Principal Enterprise Solution Architect, Domain Architect, Database Architect

---

## 2.1 Domain-Driven Design Approach

SlateIQ follows **Domain-Driven Design (DDD)** with **Bounded Contexts** aligned to business capabilities. Each context owns its data, enforces its invariants, and communicates via explicit contracts (domain events, API).

### 2.1.1 Bounded Contexts

| Context | Business Capability | Core Entities | Ownership |
|---------|---------------------|---------------|-----------|
| **Tenant & Identity** | Multi-tenancy, organizations, users, authentication, authorization | Organization, User, Role, Permission, AuditAssignment | Platform Team |
| **Production Management** | Shows, seasons, episodes, departments, categories, production hierarchy | Show, Season, Episode, Department, ExpenseCategory | Production Ops |
| **Budget Management** | Budget creation, revision, approval, versioning, lock/control | Budget, BudgetRevision, BudgetLine, ApprovalWorkflow | Finance |
| **Vendor & Procurement** | Vendor registry, purchase orders, commitments, vendor payments | Vendor, PurchaseOrder, POLine, Commitment | Procurement/Finance |
| **Audit Management** | Auditor assignments, audit scopes, audit teams, external auditor onboarding | AuditAssignment, AuditTeam, AuditScope, AuditorProfile | Audit Operations |
| **Expense & Cost Capture** | Daily expense entry, evidence attachment, validation, approval workflow | Expense, ExpenseLine, Evidence, ExpenseApproval, Exception | Auditors/Finance |
| **Cost Intelligence** | Variance calculation, forecasting, benchmarking, alerting, analytics | Variance, Forecast, CostBenchmark, Alert, DashboardMetric | Analytics/Finance |
| **Document & Evidence** | Secure storage, versioning, access control, malware scanning, retention | Document, DocumentVersion, AccessLog, RetentionPolicy | Platform/Compliance |
| **Excel Integration** | Template management, import validation, export generation, reconciliation | ExcelTemplate, ImportBatch, ImportRecord, ExportJob | Integration |
| **Notification & Alerting** | Real-time alerts, scheduled reports, escalation, multi-channel delivery | Notification, AlertRule, DeliveryChannel, EscalationPolicy | Platform |
| **Audit Trail & Compliance** | Business audit log, security audit log, immutable history, retention | BusinessAuditEvent, SecurityAuditEvent, ComplianceRecord | Compliance/Security |

### 2.1.2 Context Map (Integration Patterns)

```
┌─────────────────┐     ┌──────────────────┐     ┌────────────────────┐
│  Tenant &       │────▶│  Production      │────▶│  Budget            │
│  Identity       │     │  Management      │     │  Management        │
│  (Upstream)     │     │  (Downstream)    │     │  (Downstream)      │
└─────────────────┘     └────────┬─────────┘     └─────────┬──────────┘
                                 │                         │
                                 ▼                         ▼
                        ┌─────────────────┐     ┌──────────────────┐
                        │  Vendor &       │     │  Audit             │
                        │  Procurement    │     │  Management        │
                        └────────┬────────┘     └────────┬─────────┘
                                 │                      │
                                 ▼                      ▼
                        ┌─────────────────┐     ┌──────────────────┐
                        │  Expense &      │◀───▶│  Cost Intelligence │
                        │  Cost Capture   │     │  (Analytics)       │
                        └────────┬────────┘     └────────┬─────────┘
                                 │                      │
                    ┌────────────┴────────────┐         │
                    ▼                         ▼         ▼
          ┌───────────────┐         ┌───────────────┐ ┌──────────┐
          │  Document &   │         │  Excel        │ │ Alert &  │
          │  Evidence     │         │  Integration  │ │ Notif.   │
          └───────┬───────┘         └───────┬───────┘ └────┬─────┘
                  │                         │              │
                  └─────────────────────────┼──────────────┘
                                            ▼
                                 ┌─────────────────────┐
                                 │  Audit Trail &      │
                                 │  Compliance         │
                                 │  (Cross-cutting)    │
                                 └─────────────────────┘
```

**Integration Patterns:**
- **Upstream/Downstream**: Tenant & Identity → all contexts (tenant context propagation)
- **Shared Kernel**: Expense & Cost Capture ↔ Cost Intelligence (shared value objects: Money, Percentage, DateRange)
- **Published Language**: Domain events (ExpenseSubmitted, BudgetApproved, AlertTriggered) via transactional outbox
- **Anti-Corruption Layer**: Excel Integration → Expense/Cost contexts (template mapping, validation)
- **Conformist**: Document & Evidence → all contexts (standard document API)

---

## 2.2 Core Domain Entities (Per Context)

### 2.2.1 Tenant & Identity Context

| Entity | Purpose | Primary Key | Key Attributes |
|--------|---------|-------------|----------------|
| **Organization** | Tenant root; owns all data isolation boundary | `org_id` (UUID) | name, slug, status (ACTIVE/SUSPENDED/ARCHIVED), base_currency, timezone, settings_json, created_at, updated_at |
| **User** | Person with system access; belongs to one organization | `user_id` (UUID) | org_id (FK), email, password_hash, full_name, phone, status, mfa_enabled, last_login_at, created_at |
| **Role** | Named permission set; organization-scoped | `role_id` (UUID) | org_id (FK), name, description, is_system (bool), created_at |
| **Permission** | Atomic action (e.g., `expense:create`, `budget:approve`) | `perm_id` (UUID) | code (unique), resource, action, scope (ORG/SHOW/DEPT/ASSIGNMENT), description |
| **RolePermission** | Many-to-many link | `role_id`, `perm_id` (composite PK) | granted_at, granted_by |
| **UserRole** | User ↔ Role assignment (with optional scope restriction) | `user_id`, `role_id` (composite PK) | scope_type (ORG/SHOW/DEPT/ASSIGNMENT), scope_id (nullable), assigned_at, assigned_by |
| **AuditAssignment** | Links auditor to show/department with explicit scope | `assignment_id` (UUID) | org_id, auditor_user_id, show_id, department_id (nullable), start_date, end_date, status, created_by |

### 2.2.2 Production Management Context

| Entity | Purpose | Primary Key | Key Attributes |
|--------|---------|-------------|----------------|
| **Show** | Top-level production (series, film, event) | `show_id` (UUID) | org_id, title, slug, type (SERIES/FILM/EVENT/LIVE), status (PLANNING/ACTIVE/COMPLETED/ARCHIVED), start_date, end_date, production_house_id, broadcaster_id, ott_platform_id, created_at |
| **Season** | Season within a series | `season_id` (UUID) | show_id, season_number, title, status, start_date, end_date, episode_count_planned |
| **Episode** | Individual episode/segment | `episode_id` (UUID) | season_id (or show_id for non-series), episode_number, title, status, shoot_start_date, shoot_end_date, planned_budget |
| **Department** | Cost department (e.g., Camera, Art, VFX, Post-Production) | `dept_id` (UUID) | org_id, show_id (nullable for org-level), code, name, description, parent_dept_id (hierarchy), is_active |
| **ExpenseCategory** | Granular expense classification | `category_id` (UUID) | org_id, dept_id (FK), code, name, description, tax_treatment, is_active, sort_order |

### 2.2.3 Budget Management Context

| Entity | Purpose | Primary Key | Key Attributes |
|--------|---------|-------------|----------------|
| **Budget** | Approved financial plan for a show/season | `budget_id` (UUID) | org_id, show_id, season_id (nullable), version, status (DRAFT/SUBMITTED/APPROVED/REVISED/SUPERSEDED), total_approved_amount, currency, approved_at, approved_by, created_at |
| **BudgetRevision** | Immutable history of budget changes | `revision_id` (UUID) | budget_id, version, change_reason, changed_by, previous_total, new_total, status, created_at |
| **BudgetLine** | Line item: department × category × amount | `line_id` (UUID) | budget_id, dept_id, category_id, approved_amount, currency, notes, created_at |
| **ApprovalWorkflow** | Configurable approval chain per budget type | `workflow_id` (UUID) | org_id, name, trigger_entity (BUDGET/EXPENSE/PO), steps_json (ordered approvers), is_active |

### 2.2.4 Vendor & Procurement Context

| Entity | Purpose | Primary Key | Key Attributes |
|--------|---------|-------------|----------------|
| **Vendor** | Supplier/service provider registry | `vendor_id` (UUID) | org_id, code, legal_name, trade_name, tax_id, gst_number, pan, address_json, contact_json, payment_terms_days, status (ACTIVE/INACTIVE/BLOCKED), risk_rating, created_at |
| **PurchaseOrder** | Commitment to vendor for goods/services | `po_id` (UUID) | org_id, show_id, vendor_id, po_number, status (DRAFT/SENT/ACKNOWLEDGED/PARTIAL/FULL/CANCELLED), issue_date, expected_date, currency, total_amount, created_by |
| **POLine** | Line item within PO | `po_line_id` (UUID) | po_id, dept_id, category_id, description, quantity, unit_price, currency, tax_rate, committed_amount |
| **Commitment** | Aggregated committed spend per vendor/show/dept/category | `commitment_id` (UUID) | org_id, show_id, vendor_id, dept_id, category_id, committed_amount, currency, source (PO/CONTRACT/MANUAL), valid_from, valid_to |

### 2.2.5 Audit Management Context

| Entity | Purpose | Primary Key | Key Attributes |
|--------|---------|-------------|----------------|
| **AuditTeam** | Group of auditors working together | `team_id` (UUID) | org_id, name, lead_auditor_id, show_id, department_id (nullable), created_at |
| **AuditScope** | Defines what an auditor/team can access | `scope_id` (UUID) | assignment_id, show_id, dept_id (nullable), category_ids (array), permission_level (READ/WRITE/APPROVE), start_date, end_date |
| **AuditorProfile** | Auditor-specific settings, qualifications | `profile_id` (UUID) | user_id (FK), employee_id, qualification, specialization, hourly_rate, preferred_currency, notification_prefs_json |

### 2.2.6 Expense & Cost Capture Context

| Entity | Purpose | Primary Key | Key Attributes |
|--------|---------|-------------|----------------|
| **Expense** | Single expense submission (header) | `expense_id` (UUID) | org_id, show_id, season_id, episode_id, dept_id, vendor_id, auditor_id, status (DRAFT/SUBMITTED/UNDER_REVIEW/APPROVED/REJECTED/REQUIRES_INFO), source (MOBILE/WEB/EXCEL_IMPORT), submitted_at, submitted_by, total_amount, currency, exchange_rate, notes |
| **ExpenseLine** | Line item within expense | `line_id` (UUID) | expense_id, category_id, description, quantity, unit_price, currency, tax_rate, tax_amount, line_amount, evidence_required (bool) |
| **Evidence** | Supporting document (photo, PDF, invoice) | `evidence_id` (UUID) | org_id, expense_id (nullable), line_id (nullable), document_id (FK), evidence_type (INVOICE/RECEIPT/PHOTO/CONTRACT/OTHER), description, uploaded_at, uploaded_by, verification_status (UNVERIFIED/VERIFIED/DISPUTED) |
| **ExpenseApproval** | Approval/rejection record | `approval_id` (UUID) | expense_id, approver_user_id, action (APPROVE/REJECT/REQUEST_INFO), comment, previous_status, new_status, decided_at, ip_address |
| **Exception** | Cost anomaly or policy violation | `exception_id` (UUID) | org_id, expense_id (nullable), show_id, dept_id, vendor_id, type (OVER_BUDGET/DUPLICATE/POLICY_VIOLATION/UNAUTHORIZED_VENDOR/UNUSUAL_AMOUNT), severity (LOW/MEDIUM/HIGH/CRITICAL), description, detected_at, status (OPEN/ACKNOWLEDGED/RESOLVED/FALSE_POSITIVE), assigned_to |

### 2.2.7 Cost Intelligence Context

| Entity | Purpose | Primary Key | Key Attributes |
|--------|---------|-------------|----------------|
| **Variance** | Calculated variance (budget vs actual/committed/forecast) | `variance_id` (UUID) | org_id, show_id, dept_id, category_id, period_start, period_end, budget_amount, actual_amount, committed_amount, forecast_amount, variance_amount, variance_pct, calculated_at |
| **Forecast** | Projected final cost (EFC - Estimated Final Cost) | `forecast_id` (UUID) | org_id, show_id, dept_id, category_id, method (LINEAR/TREND/SEASONAL/MANUAL), projected_amount, confidence_level, assumptions_json, generated_at, generated_by |
| **CostBenchmark** | Historical benchmarks for comparison | `benchmark_id` (UUID) | org_id, category_id, show_type, metric (AVG_COST/MEDIAN_COST/PERCENTILE_25/PERCENTILE_75), value, currency, sample_size, period_from, period_to, updated_at |
| **Alert** | Threshold-based notification trigger | `alert_id` (UUID) | org_id, show_id (nullable), dept_id (nullable), rule_id (FK), triggered_value, threshold_value, severity, message, acknowledged_at, acknowledged_by, resolved_at |
| **DashboardMetric** | Pre-aggregated metrics for fast dashboard queries | `metric_id` (UUID) | org_id, show_id, dept_id, category_id, metric_type, period, value, currency, computed_at |

### 2.2.8 Document & Evidence Context

| Entity | Purpose | Primary Key | Key Attributes |
|--------|---------|-------------|----------------|
| **Document** | Stored file with metadata | `document_id` (UUID) | org_id, original_filename, stored_filename, storage_path, mime_type, file_size_bytes, checksum_sha256, malware_scan_status (PENDING/CLEAN/INFECTED/FAILED), uploaded_by, uploaded_at, retention_until |
| **DocumentVersion** | Version history for documents | `version_id` (UUID) | document_id, version_number, storage_path, checksum_sha256, changed_by, changed_at, change_reason |
| **AccessLog** | Audit trail for document access | `log_id` (UUID) | document_id, user_id, action (VIEW/DOWNLOAD/UPLOAD/DELETE), ip_address, user_agent, timestamp |
| **RetentionPolicy** | Configurable retention rules | `policy_id` (UUID) | org_id, entity_type, min_retention_years, max_retention_years, archive_after_years, delete_after_years, legal_hold (bool) |

### 2.2.9 Excel Integration Context

| Entity | Purpose | Primary Key | Key Attributes |
|--------|---------|-------------|----------------|
| **ExcelTemplate** | Organization-defined import/export template | `template_id` (UUID) | org_id, name, version, type (IMPORT/EXPORT), entity_target (EXPENSE/BUDGET/PO/VENDOR), column_mapping_json, validation_rules_json, is_default, created_by |
| **ImportBatch** | Single import operation | `batch_id` (UUID) | org_id, template_id, uploaded_by, status (UPLOADED/VALIDATING/VALIDATED/COMMITTED/REJECTED/PARTIAL), total_rows, valid_rows, error_rows, file_path, created_at, completed_at |
| **ImportRecord** | Row-level import result | `record_id` (UUID) | batch_id, row_number, status (VALID/INVALID/WARNING), entity_id (nullable), entity_type, error_json, warning_json, created_at |
| **ExportJob** | Export generation job | `job_id` (UUID) | org_id, template_id, requested_by, filters_json, status (QUEUED/PROCESSING/COMPLETED/FAILED), file_path, row_count, created_at, completed_at |

### 2.2.10 Notification & Alerting Context

| Entity | Purpose | Primary Key | Key Attributes |
|--------|---------|-------------|----------------|
| **Notification** | Individual notification instance | `notification_id` (UUID) | org_id, user_id (nullable for broadcast), channel (EMAIL/PUSH/IN_APP/WEBHOOK/SMS), template_id, payload_json, status (QUEUED/SENT/DELIVERED/FAILED/BOUNCED), sent_at, delivered_at, retry_count |
| **AlertRule** | Configurable threshold rule | `rule_id` (UUID) | org_id, name, scope (ORG/SHOW/DEPT/CATEGORY), metric (VARIANCE_PCT/COMMITTED_PCT/OVER_BUDGET/UNUSUAL_SPEND), condition (GT/LT/EQ/GTE/LTE), threshold_value, severity, cooldown_minutes, is_active |
| **DeliveryChannel** | Channel configuration per org | `channel_id` (UUID) | org_id, channel_type, config_json (SMTP, FCM, webhook URL, etc.), is_active, tested_at |
| **EscalationPolicy** | Multi-level escalation for critical alerts | `policy_id` (UUID) | org_id, alert_rule_id, level, delay_minutes, recipient_user_ids (array), channel_types (array), is_active |

### 2.2.11 Audit Trail & Compliance Context

| Entity | Purpose | Primary Key | Key Attributes |
|--------|---------|-------------|----------------|
| **BusinessAuditEvent** | Immutable business action log | `event_id` (ULID) | org_id, correlation_id, actor_user_id, actor_role, action (CREATE/UPDATE/DELETE/APPROVE/REJECT/IMPORT/EXPORT), entity_type, entity_id, previous_state_json, new_state_json, metadata_json, timestamp |
| **SecurityAuditEvent** | Authentication/authorization events | `event_id` (ULID) | org_id, user_id (nullable), event_type (LOGIN/LOGOUT/MFA_CHALLENGE/PASSWORD_CHANGE/ROLE_CHANGE/PERMISSION_DENIED/TOKEN_REFRESH), ip_address, user_agent, success (bool), failure_reason, timestamp |
| **ComplianceRecord** | Regulatory compliance evidence | `record_id` (UUID) | org_id, regulation (GDPR/SOX/IND_AS/OTHER), requirement, evidence_document_ids (array), status (COMPLIANT/NON_COMPLIANT/IN_PROGRESS), assessed_at, assessed_by, next_review_date |

---

## 2.3 Value Objects (Shared Kernel)

| Value Object | Purpose | Constraints |
|--------------|---------|-------------|
| **Money** | Monetary amount with currency | `amount: Decimal(19,4)`, `currency: ISO-4217`, immutable, value equality, arithmetic with rounding (banker's rounding, 4 decimal places) |
| **Percentage** | Percentage value | `value: Decimal(5,2)`, range 0-100 (or negative for variance), immutable |
| **DateRange** | Inclusive date range | `start: Date`, `end: Date`, `start <= end`, immutable |
| **ExchangeRate** | Currency conversion rate | `from_currency`, `to_currency`, `rate: Decimal(19,6)`, `effective_date`, `source` (MANUAL/ECB/OER/API) |
| **FileReference** | Secure document reference | `document_id`, `signed_url` (transient), `expires_at`, `permissions` (VIEW/DOWNLOAD) |
| **TenantContext** | Request-scoped tenant identity | `org_id`, `user_id`, `roles[]`, `permissions[]`, `show_scope[]`, `dept_scope[]`, `assignment_scope[]` |

---

## 2.4 Business Capability Map

| Capability ID | Capability Name | Bounded Context | Description | Priority |
|---------------|-----------------|-----------------|-------------|----------|
| **CAP-001** | Multi-Tenant Organization Management | Tenant & Identity | Create, configure, suspend, archive organizations | P0 |
| **CAP-002** | User Authentication & Session Management | Tenant & Identity | Login, MFA, token refresh, logout, session revocation | P0 |
| **CAP-003** | Role-Based Access Control (RBAC) | Tenant & Identity | Roles, permissions, resource-scoped assignments | P0 |
| **CAP-004** | Show/Season/Episode Lifecycle | Production Management | Create, update, status transitions, hierarchy | P0 |
| **CAP-005** | Department & Category Management | Production Management | Hierarchical departments, categorized expenses | P0 |
| **CAP-006** | Budget Creation & Versioning | Budget Management | Draft, submit, approve, revise, supersede, lock | P0 |
| **CAP-007** | Budget Approval Workflows | Budget Management | Configurable multi-step approval chains | P0 |
| **CAP-008** | Vendor Registry & Risk Profiling | Vendor & Procurement | Create, update, block, risk rating, tax compliance | P0 |
| **CAP-009** | Purchase Order Management | Vendor & Procurement | Create, send, acknowledge, track, close POs | P1 |
| **CAP-010** | Commitment Tracking | Vendor & Procurement | Aggregated committed spend visibility | P1 |
| **CAP-011** | Auditor Assignment & Scoping | Audit Management | Assign auditors to shows/depts with explicit scope | P0 |
| **CAP-012** | Mobile Offline Expense Capture | Expense & Cost Capture | Fast entry, photos, drafts, auto-sync, conflict resolution | P0 |
| **CAP-013** | Expense Validation & Approval | Expense & Cost Capture | Multi-level approval, rejection, request-info | P0 |
| **CAP-014** | Evidence Management | Document & Evidence | Upload, version, verify, access control, retention | P0 |
| **CAP-015** | Cost Variance Calculation | Cost Intelligence | Budget vs Actual vs Committed vs Forecast | P0 |
| **CAP-016** | Cost Overrun Detection & Alerting | Cost Intelligence | Threshold rules, real-time alerts, escalation | P0 |
| **CAP-017** | Estimated Final Cost (EFC) Forecasting | Cost Intelligence | Multiple methods, confidence intervals | P1 |
| **CAP-018** | Cross-Show Benchmarking | Cost Intelligence | Historical benchmarks, percentile analysis | P1 |
| **CAP-019** | Management Dashboards | Cost Intelligence | Portfolio view, drill-down, export | P0 |
| **CAP-020** | Excel Import with Validation | Excel Integration | Template mapping, row validation, preview, audit trail | P0 |
| **CAP-021** | Excel Export (Standardized Reports) | Excel Integration | Configurable templates, scheduled delivery | P0 |
| **CAP-022** | Business Audit Trail | Audit Trail & Compliance | Immutable action log, correlation IDs, search | P0 |
| **CAP-023** | Security Audit Trail | Audit Trail & Compliance | Auth events, permission denials, anomaly detection | P0 |
| **CAP-024** | Data Retention & Archival | Document & Evidence | Policy-driven, legal hold, tiered storage | P1 |
| **CAP-025** | Multi-Channel Notifications | Notification & Alerting | Email, push, in-app, webhook, SMS | P1 |

---

## 2.5 Key Domain Relationships (Conceptual)

```
Organization (1) ──────< User
Organization (1) ──────< Role
Organization (1) ──────< Show
Organization (1) ──────< Vendor
Organization (1) ──────< Budget
Organization (1) ──────< Department (org-level)
Organization (1) ──────< ExpenseCategory (org-level)

Show (1) ──────< Season
Show (1) ──────< Episode (non-series)
Show (1) ──────< Department (show-level)
Show (1) ──────< Budget
Show (1) ──────< Expense
Show (1) ──────< PurchaseOrder
Show (1) ──────< AuditAssignment
Show (1) ──────< AuditTeam

Season (1) ──────< Episode
Season (1) ──────< Budget

Episode (1) ──────< Expense

Department (1) ──────< ExpenseCategory
Department (1) ──────< BudgetLine
Department (1) ──────< Expense

Vendor (1) ──────< PurchaseOrder
Vendor (1) ──────< Commitment
Vendor (1) ──────< Expense

Budget (1) ──────< BudgetLine
Budget (1) ──────< BudgetRevision (history)
Budget (1) ──────< ApprovalWorkflow (trigger)

PurchaseOrder (1) ──────< POLine
PurchaseOrder (1) ──────< Commitment (derived)

Expense (1) ──────< ExpenseLine
Expense (1) ──────< Evidence
Expense (1) ──────< ExpenseApproval (history)
Expense (1) ──────< Exception (derived)

AuditAssignment (1) ──────< AuditScope
AuditAssignment (1) ──────> AuditorProfile (via User)
AuditTeam (1) ──────< AuditAssignment (members)

Document (1) ──────< DocumentVersion
Document (1) ──────< AccessLog
Expense (1) ──────< Document (via Evidence)

ExcelTemplate (1) ──────< ImportBatch
ImportBatch (1) ──────< ImportRecord
ExcelTemplate (1) ──────< ExportJob

AlertRule (1) ──────< Alert
AlertRule (1) ──────< EscalationPolicy
Notification (1) ──────> DeliveryChannel
```

---

## 2.6 Assumptions Requiring Validation

| # | Assumption | Context | Validation From |
|---|------------|---------|-----------------|
| D1 | **Department hierarchy depth**: Max 3 levels (Dept → Sub-dept → Cost center)? | Production | Production Manager |
| D2 | **Category granularity**: ~50-200 categories per show? Standard taxonomy per org? | Production | Cost Controller |
| D3 | **Budget line uniqueness**: One line per (dept, category) per budget? | Budget | Finance Head |
| D4 | **PO commitment accounting**: Commitment = PO total - invoiced? Separate manual commitments? | Procurement | Accounts Executive |
| D5 | **Auditor assignment exclusivity**: One auditor per (show, dept) or overlapping? | Audit | Audit Manager |
| D6 | **Expense line evidence requirement**: Per-category config or global? | Expense | Internal Auditor |
| D7 | **Forecast methods**: Which methods required at launch (Linear, Trend, Manual)? | Analytics | Cost Controller |
| D8 | **Benchmark anonymization**: Cross-org benchmarking requires anonymization? | Analytics | Legal/Compliance |
| D9 | **Document retention**: 7 years default; legal hold overrides? | Evidence | Legal/Compliance |
| D10 | **Excel template versioning**: Backward compatibility for imports? | Excel | Accounts Executive |

---
*End of Document 2*