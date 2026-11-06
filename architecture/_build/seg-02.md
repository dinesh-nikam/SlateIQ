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

**Referential rules.** `identity.user.home_org_id` and `tenancy.membership.org_id` must agree at MVP; the database enforces this with a composite foreign key or a deferred constraint trigger (Document 5 chooses the mechanism). A membership cannot exist for an erased identity. A session cannot exist for a deprovisioned identity; revocation is written before the status change commits.

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

The policy resolves RN-02: FR-TNT-008 requires minimum length and complexity, while Document 3 section 13.3 prohibits forced composition rules. Both are satisfied by requiring length and a choice of character classes, rejecting breached and predictable values, and never expiring a password on a calendar.

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
| Reset | Administrator with `identity.mfa.administer` and step-up, plus a second approver (SOD-09); all sessions revoked; user notified; re-enrollment required at next login |
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
