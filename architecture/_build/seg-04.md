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
| `document.document` | `org_id` plus the owning entity's scope | The owning entity's containment applies; documents do not float free of their subject |
| `audit.audit_assignment` | `org_id`, `assignment_id`, `show_id`, date range | `ASSIGNMENT(assignment)` inside an active grant, or `ORG`/`SHOW` for a member auditor |
| `audit.audit_finding` | `org_id`, `assignment_id`, optional `show_id` | Assignment containment |
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
| Decision | `SELF` scope contains `user_1042`; permission `identity.user.update_self` (a `SELF`-only key) |
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
