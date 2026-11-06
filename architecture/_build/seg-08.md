---

## 11. API Surface

### 11.1 Role of This Section

Document 6 owns the canonical OpenAPI 3.1 contract, the generated SDKs and the deprecation schedule. This section defines the identity, tenancy and access-control surface that the contract must expose: every endpoint, the permission that authorises it, the scope it evaluates, and the denial it returns. A route that exists in code but not here is an undocumented authority path, which is the class of defect an access review is least likely to find.

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

These conventions extend Document 3 Appendix A.1 and are binding on Document 6.

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
| POST | `/organizations/{orgId}/users/{userId}/mfa-reset` | `identity.mfa.administer` | `ORG` | Removes a lost factor after identity verification and a second approver. Step-up required. `409 MFA_RESET_NOT_ALLOWED` for self-service |
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
| GET | `/sod-rules` | `tenancy.sod.read` | `ORG` | The ten seeded rules with enforcement mode and description |
| GET | `/sod-exceptions` | `tenancy.sod.read` | `ORG` | Live and expired exceptions with compensating controls |
| POST | `/sod-exceptions` | `tenancy.sod.administer` | `ORG` | Two approvers, reason, compensating control, maximum 90 days. `409 SOD_EXCEPTION_OVERLAP` |
| POST | `/sod-exceptions/{exceptionId}/revoke` | `tenancy.sod.administer` | `ORG` | Revokes before expiry; audited at high severity |

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

The envelope extends Document 3 Appendix B.1. Fields marked optional are additive and do not change the version for existing consumers.

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

**`GrantCreated` from Document 3 is retired.** Appendix B.2 lists `GrantCreated` as a single event. The lifecycle has distinct creation, approval and activation moments, and collapsing them loses the evidence that the client approved before the auditor could read anything. Document 6 must map the old name to `GrantDrafted` and treat `GrantActivated` as the authority-begin event. This is recorded as change request D4-06 in Appendix G.

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
