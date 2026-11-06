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

The catalogue has fifteen domains, aligned one-to-one with the module schemas in Document 3 section 8.2. A domain owns its keys; no module writes into another module's namespace.

| Domain | Module | Representative resources | Typical actions | Notes |
|--------|--------|--------------------------|-----------------|-------|
| `tenancy` | MOD-01 | `organization`, `membership`, `role`, `assignment`, `invitation`, `access_review`, `delegation`, `sod_exception` | read, create, update, delete, administer | Tenant administration |
| `identity` | MOD-02 | `user`, `credential`, `mfa_factor`, `session`, `device`, `service_account` | read, update, delete, administer | No permission exposes a secret |
| `production` | MOD-03 | `show`, `season`, `episode`, `department`, `category`, `party`, `party_link` | read, create, update, delete, submit | |
| `budget` | MOD-04 | `budget`, `budget_version`, `budget_line`, `variation`, `forecast`, `approval` | read, create, update, submit, approve, reject, export | Approval is amount-bounded |
| `vendor` | MOD-05 | `vendor`, `contact`, `contract`, `rate_card`, `payment_term` | read, create, update, delete, approve, export | Vendor approval is separate from payment approval |
| `audit` | MOD-06 | `audit_plan`, `audit_assignment`, `audit_finding`, `audit_evidence`, `audit_report`, `access_grant` | read, create, update, submit, approve, reject, export, administer | `audit.access_grant.*` matches Document 3 Appendix C.1 |
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

Seventeen seeded roles cover the thirteen personas. A role applies only to the organization types named in the table; assigning an audit-firm role to a production house is rejected.

| Role key | Name | Persona | Organization types | MFA | Scope types | Authority summary | Hard exclusions |
|----------|------|---------|--------------------|-----|-------------|-------------------|-----------------|
| `ORG_ADMIN` | Organization Administrator | P-12 | OTT, broadcaster, production house, platform internal | Yes | ORG | Manage organization profile, users, roles, assignments, invitations, access reviews, security policy, entitlements view | Cannot approve financial transactions; cannot alter audit events; cannot grant cross-org access |
| `COMMISSIONING_ADMIN` | Commissioning Administrator | P-01, P-02 | OTT, broadcaster | Yes | ORG, SHOW | Create shows, approve budgets within limit, appoint auditors, manage grants, portfolio read | Cannot approve expenses; cannot administer roles |
| `PRODUCTION_ADMIN` | Production Administrator | P-03 | Production house | Yes | ORG, SHOW | Manage shows, departments, parties, budgets (submit), vendors, documents, production reports | Cannot approve own budget; cannot approve payments |
| `FINANCE_HEAD` | Finance Head / Cost Controller | P-04 | OTT, broadcaster, production house | Yes | ORG, SHOW | Approve budgets, variations, expenses, payments, close periods, view all financial reports | Cannot create the expense they approve; cannot administer users |
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
| **SOD-10** | `compliance.legal_hold.release` and `compliance.dsr_request.approve` | Same org | Conditional | Allowed only with Legal approval recorded on the hold |

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
