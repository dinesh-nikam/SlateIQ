# SlateIQ — Governing Architecture Brief

> Master Foundation Architect Prompt issued by the business owner and received 2026-11-06. This is the governing brief referenced throughout the architecture set (decision D-023). It is reproduced faithfully; only Markdown structure was added.

**Product:** SlateIQ — Production Cost Intelligence & Audit Monitoring Platform. **Tagline:** Intelligence Behind Every Production.

## 1. Role

Act as Principal Enterprise Solution Architect, Principal Software Architect, Domain Architect, Database Architect, Security Architect, DevOps Architect and Technical Product Strategist with 30+ years of equivalent experience. Design the complete technical and functional foundation of a real enterprise product to production engineering standards used by large OTT platforms, broadcasters, media companies and enterprise audit organizations. Not a prototype, generic CRUD application or demonstration. Think as an architect designing a commercial SaaS product across multiple organizations, shows, production houses and independent audit teams.

Priorities, in order: business correctness; domain integrity; security and authorization; data consistency; reliability; maintainability; scalability; observability; testability. Do not start coding until the foundation has been defined and reviewed.

## 2. Product Identity

Category: Production Cost Intelligence & Audit Monitoring Platform. Vision: a centralized, intelligent production cost monitoring and audit management ecosystem enabling OTT platforms, broadcasters, production houses and management to monitor production expenses, standardize audit operations, identify overruns early and make informed financial decisions. SlateIQ connects on-ground auditors, production teams, finance departments and senior management through a standardized digital platform.

## 3. Business Problem

Shows are audited by in-house auditors, OTT/platform-appointed auditors, broadcaster-appointed auditors, independent external auditors and production-house finance teams, each with their own Excel formats and methods, reporting fortnightly or monthly. Results: non-standard audit information; delayed visibility; inconsistent categorization; hard cross-show comparison; late overrun detection; limited vendor visibility; difficulty tracking approved versus unapproved expenses; manual consolidation; delayed corrective action; missed savings. SlateIQ must solve these without replacing auditors' professional roles; it is an automation, standardization, monitoring and intelligence platform. Existing Excel reporting must remain supported.

## 4. Product Objectives

1. Standardize audit data collection across shows and auditors. 2. Common mobile/tablet app for on-ground capture. 3. Timely or near-real-time synchronization. 4. Centralized cost intelligence. 5. Show-, department-, vendor- and category-wise visibility. 6. Budget versus actual and committed. 7. Early detection of unusual spend and overruns. 8. Evidence and audit trails. 9. Standardized management and audit reports. 10. Excel import and export. 11. Multi-show management dashboards. 12. Future predictive analytics and cost optimization.

## 5. Customers and Business Model

OTT platforms, broadcasters, media and entertainment companies, production houses, film and TV production organizations, independent audit firms, production finance and cost-control departments. Multi-tenant enterprise SaaS, with dedicated enterprise deployments when required. Do not assume customers share data; each organization has isolated, explicitly authorized access.

## 6. Domain-First Architecture

Identify and document: A organizations and tenants; B OTT platforms and broadcasters; C production houses; D shows, seasons, episodes; E production budgets; F departments and expense categories; G vendors and suppliers; H purchase orders and commitments; I expenses and transactions; J audit assignments; K auditors and audit teams; L daily audit reports; M supporting documents and evidence; N expense approvals and exceptions; O cost variances; P management dashboards; Q Excel imports and exports; R notifications and alerts; S audit history and business activity logs; T financial periods and reporting cycles. For every entity define business purpose, primary key, relationships, ownership, lifecycle, business rules, access permissions, validation rules, audit requirements and retention. No entity without a business purpose.

## 7. Actors and RBAC

Actors: SaaS Super Administrator; Organization Administrator; OTT/Broadcaster Administrator; Finance Head; Cost Controller; Audit Manager; Internal Auditor; External Auditor; Platform-appointed Auditor; Production House Administrator; Production Manager; Department Head; Accounts Executive; Management Viewer. Define roles, permissions, role-permission relationships, organization-, show-, department- and audit-assignment-level access, visibility boundaries, approval authority, read-only access, export and document access permissions. An auditor assigned to Show A must not gain access to Show B. An external auditor accesses only explicitly assigned organizations, shows and assignments. Never trust role, organization, tenant or permission information from the frontend; authorization is enforced by the backend.

## 8. Core Business Workflows

Organization onboarding; show creation; production-house onboarding; budget creation and approval; vendor registration; audit assignment; auditor onboarding; daily expense capture; supporting-document upload; expense validation; audit review; exception identification; expense approval or rejection; cost variance calculation; cost-overrun alerts; daily audit reporting; Excel import; Excel export; management dashboard updates; show closure and archival. For each important workflow document: initiating actor, preconditions, validation, authorization, state transitions, transaction boundary, concurrency handling, duplicate submission handling, success response, failure scenarios, retry behavior, audit events, notifications, recovery procedure.

## 9. Business Rules and Invariants

Define rules that must always hold, for example: every show belongs to an authorized organization; every expense has a valid show, an authorized creator or source and a valid category; every audit assignment has an assigned auditor and scope; approved budgets retain historical versions; budget changes are traceable; approved expenses are not silently modified; rejected expenses retain rejection history; every approval identifies the responsible actor; evidence is securely associated with its record; cost calculations use consistent currency and rounding; cross-tenant access is prevented; historical audit records do not disappear when users or assignments are deactivated. Define enforcement through application logic, database constraints and transaction management. Never rely only on frontend validation.

## 10. Lifecycles and State Machines

Organization; show; production budget; budget revision; vendor; audit assignment; expense; expense approval; audit report; Excel import batch; supporting document; cost exception; notification; show closure. For each: valid states, allowed and unauthorized transitions, actor permissions, preconditions, side effects, audit events, transaction boundaries, retry and recovery. No status field without a lifecycle definition.

## 11. Core Product Modules

Evaluate at minimum: tenant and organization; user, role and permission; show and production; budget and revision; department and category; vendor; audit assignment; auditor mobile backend; expense and daily cost capture; document and evidence; audit review and approval; cost variance intelligence; exception and alert; management BI dashboard; Excel import and export; reports and analytics; notifications; audit trail and activity history; system administration; integration management. For each module: purpose, entities, responsibilities, dependencies, business rules, permissions, APIs, database requirements, transactions, events and jobs, failure handling, audit requirements, testing requirements. Avoid giant modules and unnecessary duplication.

## 12. Mobile and On-Ground Audit

Fast entry; show and department selection; vendor selection; categorization; amount and currency; invoice photographs; document upload; remarks; date and time; location where permitted; drafts; offline capture; automatic sync; duplicate prevention; failed-sync recovery; auditor-specific assignments; secure local storage; device session management. Offline-first sync must handle poor connectivity, repeated submissions, app closure during submission, device restart, concurrent edits, conflicting updates, expired sessions and failed uploads. Never silently discard locally captured data.

## 13. Cost Intelligence and Analytics

Approved budget; revised budget; actual, committed, pending, approved and rejected expenses; forecast expenditure; estimated final cost; budget variance; department-, vendor-, category-, show-, season- and period-wise cost; benchmarking; exception identification. Define precise formulas and financial semantics; distinguish actual, committed and forecast. Decimal arithmetic; currency, rounding, tax treatment and reporting periods; configurable overrun thresholds. No calculation without business meaning.

## 14. Database Architecture

Evaluate PostgreSQL, relational integrity, multi-tenant isolation, normalization, history, indexing, foreign keys, unique and check constraints, isolation levels, row locking, soft deletion, archival, retention, migrations, backup and restore. For each table: name, purpose, columns and types, primary key, foreign keys, nullability, defaults, unique and check constraints, indexes, timestamps, deletion strategy, history requirements. Define tenant isolation enforcement; never rely on frontend filtering.

## 15. API Architecture

Versioned REST API with consistent naming: versioning, authentication, authorization, request validation, response structure, pagination, sorting, filtering, error handling, idempotency, rate limiting, correlation IDs, documentation. Representative endpoint contracts for every major module. Machine-readable errors. Never expose SQL, stack traces, credentials or infrastructure details.

## 16. Security Architecture

Secure authentication; password hashing; access tokens; refresh rotation; session revocation; RBAC; resource-level authorization; tenant isolation; API security; input validation; SQL injection, XSS and CSRF protection; CORS; security headers; rate limiting; secure document storage; file type validation; malware scanning; encryption in transit and at rest; secrets management; security event logging; backup security. Never store passwords, keys or tokens in plaintext; never log sensitive financial documents or secrets.

## 17. Excel and Legacy Reporting

Import: configurable templates, column mapping, validation, duplicate detection, preview, error reporting, partial failure handling, batch tracking, reconciliation, approval before commit. Export: standardized audit reports, daily expense reports, show-wise summaries, department-wise, vendor-wise, variance and management reports. Imported records preserve source and traceability. Imports never bypass authorization, validation or audit history.

## 18. System Architecture and Technology

Evaluate a modular monolith before microservices. Justify backend, frontend, mobile, database, object storage, background jobs, BI, authentication, API documentation, logging, monitoring, deployment and CI/CD. No Redis, Kafka, Kubernetes, Elasticsearch or microservices without clear justification. Backend, frontend and mobile completely separate; backend independently deployable.

## 19. Asynchronous Processing and Integrations

Notifications, Excel processing, document processing, dashboard aggregation, scheduled reports, cost alerts, data synchronization, external integrations. For each: producer, consumer, payload, retry policy, exponential backoff, idempotency, ordering, dead-letter handling, monitoring, recovery. Evaluate the transactional outbox.

## 20. Auditability and Observability

Separate business audit history, security audit history and operational logs. Business audit records capture actor, organization, action, entity, entity ID, previous state, new state, timestamp, correlation ID and source. Health and readiness checks, structured logging, metrics, tracing readiness.

## 21. Non-Functional Requirements

Measurable requirements for performance, availability, reliability, scalability, consistency, security, accessibility, mobile usability, offline capability, maintainability, disaster recovery, backup and restore, retention, monitoring. No unrealistic SLAs; identify assumptions and targets for validation.

## 22. Testing Strategy

Domain unit, application service, database integration, API, authentication, authorization, tenant isolation, workflow, concurrency, idempotency, offline sync, Excel, financial calculation, security, performance, migration, backup and restore, failure and recovery tests. Define critical scenarios that must pass before production release.

## 23. Development and Deployment Foundation

Repository structure; backend, frontend and mobile project structure; shared API contracts; environment configuration; development, testing, staging and production environments; migrations; CI/CD; Docker; secrets; monitoring; deployment and rollback; backup and disaster recovery. Provide a secure `.env.example` without secrets. Do not combine frontend and backend code.

## 24. Required Diagrams (Mermaid)

1 business context; 2 system context; 3 high-level solution architecture; 4 module dependency; 5 entity relationship; 6 tenant isolation model; 7 expense submission workflow; 8 audit approval workflow; 9 cost-overrun detection workflow; 10 mobile offline synchronization sequence; 11 Excel import workflow; 12 deployment architecture. Diagrams must match the written architecture and entities.

## 25. Required Deliverables

1 Product Vision and Business Requirements; 2 Domain Model and Business Capability Map; 3 Actor and Permission Architecture; 4 Core Business Workflows and State Machines; 5 Business Rules and Invariants Catalogue; 6 High-Level Solution Architecture; 7 Module and Dependency Architecture; 8 Database Conceptual Model and Initial ERD; 9 API Architecture and Endpoint Catalogue; 10 Security Architecture; 11 Mobile and Offline Architecture; 12 Excel Integration Architecture; 13 Analytics and Cost Calculation Architecture; 14 Audit and Observability Architecture; 15 Infrastructure and Deployment Architecture; 16 Testing Strategy; 17 Risk Register and Architecture Decision Records; 18 Phased Implementation Roadmap.

## 26. Architecture Quality Gate

Business requirements understood; entities identified; relationships defined; actors and permissions defined; tenant isolation designed; invariants documented; state machines defined; workflows documented; transaction boundaries defined; concurrency strategy defined; idempotency strategy defined; failure and recovery defined; database strategy; API standards; security architecture; mobile offline strategy; Excel strategy; financial calculations; audit history; observability; testing strategy; deployment architecture; no critical gaps. Do not declare production-ready if critical gaps remain.

## 27. Working Instructions

Understand the domain first; make reasonable assumptions and label those needing business-owner validation; ask only questions that materially affect architecture; no application code in this phase; no premature migrations or CRUD endpoints; no generic templates; do not omit difficult rules; no placeholders where a decision is possible; explain choices and trade-offs; keep every document and diagram consistent; record decisions; identify dependencies and sequence; treat future requirements as changes to an established architecture with impact analysis.

## 28. Final Objective

A comprehensive, internally consistent blueprint from which separate teams implement backend, web, auditor mobile/tablet app, database, BI dashboards, Excel integration, notifications, security, infrastructure, testing and deployment, all from the same domain model, API contracts, permission architecture and business rules. Start with Part 1 (vision, domain discovery, capabilities, actors, modules, rules, invariants, high-level architecture), then proceed through the remaining deliverables. Do not start implementation until the foundation passes its quality gate.
