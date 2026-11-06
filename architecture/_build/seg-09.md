---

## 13. Threat Model

### 13.1 Scope and Method

This threat model covers the identity, tenancy and access-control surface defined in sections 2 to 12: authentication, session management, authorization, isolation, cross-organization grants, provisioning and platform operations. Business-domain threats (duplicate invoice, collusive vendor, falsified evidence) belong to Documents 3 and 8, except where they are reached through an access-control weakness.

The method is STRIDE applied at each trust boundary, with two attack trees for the crown-jewel paths: reading another tenant's data, and exceeding an auditor's mandate. Every threat has a stable identifier, a preventative control, a detective control and a verification test, because a control that is not tested is a claim rather than a control. Threat identifiers are `T4-nn`; detection rules in section 15 are `DET-4-nn`; test obligations in section 16 are `ST-4-nn`.

Three rules govern the register:

1. **A threat is not closed by a policy statement.** It is closed when a test fails without the control, or when the control is structural and cannot be removed without a schema or code change that the test suite would reject.
2. **Every critical-impact threat has a detective control.** Prevention fails eventually. The question is how quickly the platform knows.
3. **Residual risk is recorded, not argued away.** Section 13.9 lists what remains after the controls, who accepts it and when it is reviewed.

**What is out of scope.** Physical access to the cloud provider's data centres, insider abuse at the cloud provider, and compromise of the customer's own email account before the first login. These are inherited, contractual or customer-side risks; the platform's obligation is to make the consequence bounded and visible, which the controls below do.

### 13.2 Assets

| Asset | Classification | Where it lives | Why an adversary wants it | Consequence of loss |
|-------|----------------|----------------|---------------------------|---------------------|
| Password verifier | Confidential | `identity.credential` | Offline cracking, credential reuse | Account takeover across tenants |
| TOTP secret and recovery codes | Confidential | `identity.mfa_factor` | Bypass the second factor | Account takeover |
| Refresh token | Secret | Client cookie or keystore; hash in `identity.refresh_session` | Long-lived impersonation | Persistent access until revoke |
| Access token | Secret | Client memory | Short impersonation, replay | Access for up to 15 minutes |
| Session and denylist state | Confidential | `identity.refresh_session`, Redis | Suppress revocation, replay a closed session | Revocation bypass |
| Grant record and evidence | Confidential | `tenancy.access_grant`, audit | Forge authority, hide a conflict | Undetectable external access |
| Tenant business data | Confidential | Module schemas | Competitive intelligence, financial manipulation | Contract loss, regulatory exposure |
| Personal data | Personal | `identity.user_pii`, domain tables | Identity theft, harassment | GDPR and DPDP breach |
| Audit trail | Confidential, integrity-critical | `compliance.audit_event` | Cover an intrusion or a fraudulent approval | Loss of evidential value |
| Signing keys and pepper | Secret | KMS, HSM-backed | Forge any identity in the region | Total identity compromise |
| Data encryption keys | Secret | KMS, per tenant | Decrypt a tenant's data at rest | Confidentiality loss |
| Platform operator authority | Privileged | `PLATFORM_INTERNAL` membership, break-glass vault | Silent access to every tenant | Maximum-impact insider event |

**The grant record is an asset in its own right.** It is the only object that converts an external person into an authorised reader of a client's data. An attacker who can create, widen or reactivate a grant does not need to steal a client password.

### 13.3 Adversaries

| Adversary | Capability | Motivation | Likely entry point | Primary control |
|-----------|------------|------------|--------------------|-----------------|
| Unauthenticated internet attacker | Automated scanning, credential lists, exploit kits | Data theft, ransom, notoriety | Login, invitation preview, password reset | Rate limits, lockout, Argon2id, uniform errors |
| Credential-stuffing operator | Large breached credential corpus, residential proxies | Account takeover, resale | Login | Breach-list rejection, MFA, anomaly detection |
| Malicious tenant member | Valid low-privilege account, knowledge of the domain | Financial gain, sabotage, curiosity | Object identifiers, scope boundaries | Permission plus scope, RLS, 404 semantics |
| Malicious external auditor | Valid grant, professional access to client data | Client poaching, insider trading, evidence leakage | Grant scope, export, workspace | Grant ceiling, export controls, watermark, access log |
| Colluding member and auditor | Two valid accounts on opposite sides of a grant | Conceal a conflict, approve an improper grant | Grant approval and acceptance | SOD-07, conflict declaration, independent review |
| Malicious platform operator | Infrastructure access, database credentials, KMS | Data theft, sabotage, favouritism | Support access, break-glass, `ops` schema | Two-person support access, no impersonation, loud audit |
| Compromised endpoint | Malware, stolen laptop, unlocked phone | Bulk exfiltration | Browser session, offline cache | Short tokens, device binding, offline bound, wipe |
| Supply-chain attacker | Build or dependency compromise | Persistent access | CI, package registry, container image | Lockfiles, SBOM, signed artefacts, provenance |
| Automated scraper | Valid account, high request volume | Competitive dataset | List and export endpoints | Rate limits, row caps, export step-up, volume detection |

**The insider is modelled as a platform operator, not as a tenant administrator.** A tenant administrator can harm one tenant, which is a customer-relationship and contract problem. A platform operator can harm every tenant, which is an existential problem, and the controls are correspondingly stronger.

### 13.4 Trust Boundaries

```
                        Internet
                           |
                 B1  TLS 1.2+, WAF, rate limit
                           |
                 +---------+----------+
                 |   Edge / ingress   |
                 +---------+----------+
                           |
                 B2  authenticated request, tenant claim
                           |
                 +---------+----------+
                 |      API host      |
                 |  authn -> authz    |
                 |  -> handler        |
                 +--+------+------+---+
                    |      |      |
          B3        | B4   | B5   | B6
                    |      |      |
          +---------+  +---+--+  +----+---------+
          |PostgreSQL|  |Redis |  |Object store |
          |  RLS     |  |cache |  |  Object Lock|
          +----+-----+  +------+  +-------------+
               |
       B7  KMS, keys, pepper
               |
       B8  outbox -> relay -> consumers
               |
       B9  platform operations -> tenant (support access only)
               |
       B10 region boundary (India / EU): no tenant data path
```

| Boundary | What crosses it | Threat focus |
|----------|-----------------|--------------|
| B1 | Unauthenticated HTTP | Denial of service, credential attacks, transport downgrade |
| B2 | A bearer token and a tenant claim | Token forgery, replay, tenant confusion |
| B3 | SQL with `app.org_id` context | Injection, RLS bypass, cross-tenant query |
| B4 | Cache keys and decision entries | Poisoning, stale authority, cross-user leakage |
| B5 | Presigned URLs and evidence objects | Exfiltration, URL sharing, retention |
| B6 | Key material and envelope encryption | Key compromise, decrypt without authority |
| B7 | Domain events | Personal-data leakage, forged events, replay |
| B8 | Operator session into a tenant | Impersonation, silent access, unreviewed write |
| B9/B10 | Replication and backup traffic | Residency breach, cross-tenant restore |

**B2 is the boundary that matters most.** Everything downstream trusts the tenant context that the API host sets. If the host can be made to set a context the caller is not entitled to, every other layer is defending the wrong tenant.

### 13.5 STRIDE Summary

| Category | Principal manifestation here | Primary defences | Residual |
|----------|------------------------------|------------------|----------|
| Spoofing | Credential stuffing, token theft, forged token, MFA reset fraud | Argon2id, MFA, signed tokens, device keys, approval for reset | R4-02, R4-03 |
| Tampering | Cross-tenant write, grant widening, role escalation, audit alteration | `WITH CHECK`, versioned grants, no amplification, hash chain | R4-01 |
| Repudiation | "I did not approve that grant", shared accounts | Named identities, no shared accounts, dual approval, immutable audit | None material |
| Information disclosure | Cross-tenant read, export exfiltration, log leakage, backup mixing | RLS, 404 semantics, watermark, payload rules, scoped restore | R4-06, R4-07 |
| Denial of service | Lockout abuse, auth flood, cache stampede | Per-IP throttle, autoscale, cache pre-warm, queue classes | R4-03 |
| Elevation of privilege | Assignment amplification, support access, break-glass | No amplification, two-person support, sealed credentials | R4-05 |

### 13.6 Threat Register

Likelihood and impact use Low, Medium, High and Critical. Impact is rated before controls; the residual column in section 13.9 is after controls.

| ID | STRIDE | Scenario | L | I | Preventive control | Detective control | Verification test |
|----|--------|----------|---|---|--------------------|-------------------|-------------------|
| **T4-01** | Spoofing | Credentials from a breach corpus are replayed across many accounts | High | High | Argon2id with pepper, breach-list check at set and change, 5-failure lockout, per-IP and per-account rate limits, MFA for privileged roles | `LoginFailed` burst rule, breach-list hit counter, distributed-source correlation | ST-4-01 credential-stuffing simulation; ASVS 2.2.1 |
| **T4-02** | Spoofing, Denial | An attacker locks a known victim out by submitting five wrong passwords every fifteen minutes | Medium | Medium | Lockout is per account, per-IP throttle is independent, verified reset clears the lock, no lock-state disclosure | Lockout count per address hash, repeated-lockout alert | ST-4-02 lockout abuse; R4-03 |
| **T4-03** | Spoofing | A phishing site clones the invitation or reset page and captures the token or the new password | High | High | 72-hour invitation and 30-minute reset lifetimes, single use, hashed tokens, no auto-login from a link, preview names the organization and inviter, MFA enrolment after acceptance | Token reuse, acceptance from a new IP class, password reset followed by immediate session | ST-4-03 token replay and wrong-recipient acceptance |
| **T4-04** | Spoofing | A caller convinces the help desk to reset MFA on an account they do not own | Medium | Critical | Self-service reset forbidden, step-up plus second approver, reason code, verification through an existing session or registered device, subject notification, Platform Security review for platform roles | `MfaReset` event, reset followed by login from a new device, approver-subject correlation | ST-4-04 single-approver and self-reset attempts |
| **T4-05** | Spoofing | A TOTP code is observed and replayed within its validity window | Low | High | Five-minute challenge, single-use code per time step, challenge binding, five attempts per challenge, step-up for high-risk actions | `MfaChallengeFailed`, reused-code counter | ST-4-05 code replay |
| **T4-06** | Spoofing | A refresh token is copied from a browser or device and replayed elsewhere | Medium | Critical | Rotation on every use, reuse detection burns the family, mobile refresh signed by a non-exportable key, HttpOnly `SameSite=Lax`, hashed at rest, 14-day and 30-day bounds | `RefreshTokenReuseDetected`, impossible-travel and IP-class change rules | ST-4-06 old-token replay revokes the family |
| **T4-07** | Spoofing | An access token is stolen through XSS or malware and used before expiry | Medium | High | 15-minute lifetime, thin claims, token held in memory only, strict CSP, `no-store`, session denylist, `scope_version`, device binding on mobile | Denial patterns per session, concurrent-geography rule, anomaly score | ST-4-07 revoked session rejected within 60 seconds |
| **T4-08** | Spoofing, Tampering | A signing key is compromised and tokens are forged for any user or tenant | Low | Critical | Asymmetric keys in KMS, non-exportable private key, 90-day rotation with `kid` overlap, `ver` claim, issuer and audience validation, separate keys per region and environment | Unknown `kid`, signature-failure spike, key-usage anomaly | ST-4-08 forged token and rotation drill |
| **T4-09** | Information disclosure | A member changes an object identifier in a URL and reads another tenant's record | Medium | Critical | Token-authoritative tenant, RLS on every tenant table, no tenant identifier from the client, UUIDv7 identifiers, `404` for foreign objects | `CrossTenantAccessAttempt`, 404 rate per user, isolation probe | ST-4-09 negative RLS suite and IDOR matrix |
| **T4-10** | Tampering | A caller supplies a foreign `org_id` in a body, header or path and writes into another tenant | Low | Critical | One tenant source, `SET LOCAL` only, `WITH CHECK` on every policy, composite foreign keys include `org_id`, authorization before model binding | `TENANT_CONTEXT_MISMATCH`, constraint-violation alert | ST-4-10 foreign-tenant write attempt |
| **T4-11** | Tampering, Information disclosure | A migration or hotfix disables a policy, adds `BYPASSRLS`, or creates a `SECURITY DEFINER` function that ignores context | Medium | Critical | Policies in version-controlled migrations, `FORCE ROW LEVEL SECURITY`, `slateiq_app` owns nothing and cannot DDL, default-deny when context is unset, isolation probe gates every deployment | Nightly `pg_policies` inventory diff, policy hash, probe failure alert | ST-4-11 policy inventory and drop-policy canary |
| **T4-12** | Information disclosure | An attacker enumerates tenant slugs or email addresses through response differences or timing | Medium | Medium | Uniform `404`, no public slug lookup, dummy Argon2id verification for unknown accounts, no pre-authentication tenant list, identical lockout behaviour | 404 and 401 distribution per source, enumeration pattern rule | ST-4-12 response-diff and timing-distribution test |
| **T4-13** | Elevation, Tampering | An approved grant is widened after approval by editing scope or dates | Medium | High | Approved version immutable, variation classified as reduction, extension or administrative, extension requires fresh approval, version hash chain, grants may not use `ORG` scope | `GrantVariationApplied` with `EXTENSION`, version hash mismatch | ST-4-13 widen-after-approval attempt returns `409` |
| **T4-14** | Elevation | An auditor reads shows or modules outside the assignment, or exports more than the profile allows | High | High | Grant profile ceiling, `ASSIGNMENT` scope, RLS grant boundary, export gated by profile and step-up, watermark on every export | `GrantAccessDenied`, export volume baseline, monthly access-log review | ST-4-14 grant profile matrix and scope-escape test |
| **T4-15** | Elevation | The creator approves their own grant, or two colluding users approve each other's | Low | High | SOD-07, approver must differ from creator, Finance Head co-approval for `AUDIT_FINANCE`, no self-acceptance, firm conflict declaration | `SodBlocked`, approval-timing correlation, same-IP approver rule | ST-4-15 self-approval and same-approver tests |
| **T4-16** | Elevation | An auditor leaves the firm or the client engagement ends, but the grant stays active | Medium | High | Identity transfer and firm deprovision suspend grants, user deactivation suspends grants, 12-month ceiling, T-14/T-7/T-1 notices, quarterly review | Daily orphaned-grant job, overdue-expiry alert | ST-4-16 deactivate grantee and assert denial within 60 seconds |
| **T4-17** | Elevation | A manager assigns a role or scope wider than their own authority | Medium | High | No amplification at write time, self-assignment prohibited, system roles immutable, role versions, SoD gate | `RoleAssignmentCreated` anomaly, scope-widening rule | ST-4-17 manager assigns organization-wide role |
| **T4-18** | Elevation, Information disclosure | A platform operator reads a tenant's data without a ticket or approval | Medium | Critical | `PLATFORM_INTERNAL` is a separate tenant, no default data access, support access is two-person, ticketed, 60 minutes and action-scoped, no impersonation, `ops` schema inaccessible to the application role | `SupportAccessGranted`, write-during-support review item, operator query-volume anomaly | ST-4-18 operator without support access receives `403` |
| **T4-19** | Elevation | Break-glass credentials are used outside an incident and never rotated | Low | Critical | Credentials sealed in KMS, two custodians, ticket and approval required, time-boxed, rotated after every use, every action audited with `authority_source = PLATFORM` | Break-glass use pages security, `EmergencyRevocationExecuted`, credential-access log | ST-4-19 tabletop and credential-access audit |
| **T4-20** | Tampering | Two provisioning requests race, or a run fails midway and leaves a half-created tenant | Medium | High | Idempotent ten-step saga, unique slug constraint, advisory lock per slug, state guards, compensation, isolation probe gate, 24-hour slug hold | `ProvisioningStepFailed`, duplicate-slug constraint, run-duration alert | ST-4-20 concurrent provision and kill-at-step-five |
| **T4-21** | Information disclosure | A user with legitimate read access exports a large dataset and shares it | High | High | Step-up above 1,000 rows, row caps, watermark, 15-minute URL, export event, profile gating for auditors | Export volume against baseline, off-hours and repeat-export rules, `GrantExportCompleted` | ST-4-21 export cap, watermark and URL expiry |
| **T4-22** | Information disclosure | A restore brings one tenant's data into a database serving another tenant | Low | Critical | Per-region backups, restore into an isolated account, tenant-scoped verification, per-region keys, no cross-region copy, two-person restore runbook | Restore verification job, key-region mismatch alert | ST-4-22 annual restore drill with cross-tenant assertion |
| **T4-23** | Tampering | A retried grant or invitation request creates two authorities | Medium | Medium | `Idempotency-Key` bound to method, path, actor and body hash, unique constraint per organization, stored response replay, different body returns `409` | `IDEMPOTENCY_KEY_REUSED`, duplicate-grant check | ST-4-23 double submit with same and different bodies |
| **T4-24** | Elevation | A stale permission cache keeps revoked authority alive past the commitment | Medium | High | Cache key includes `scope_version`, TTL at most 60 seconds, event-driven invalidation, no cross-user entries, fail-closed on cache error | Revocation-latency SLO, stale-decision canary | ST-4-24 role change and grant revoke propagation |
| **T4-25** | Information disclosure | Personal data appears in logs, traces or event payloads | Medium | High | Structured logging with an allowlist, no PII in events, email represented by keyed hash, reason codes instead of free text, redaction tests in CI | PII scanner over sampled logs, DLP alert | ST-4-25 log-capture assertion in every integration test |
| **T4-26** | Tampering | A compromised dependency or build step injects code into the API or worker image | Medium | Critical | Lockfiles, signed packages, SBOM, dependency scanning, image signing and provenance, no unreviewed transitive upgrade, least-privilege CI | SCA and provenance alerts, unexpected egress from build | ST-4-26 build-gate and SBOM diff |
| **T4-27** | Denial | An attacker floods login or a hot endpoint and starves legitimate traffic | Medium | Medium | Edge WAF and rate limits, per-tenant fairness, autoscale, circuit breakers, cache pre-warm, queue classes | 429 and 5xx rate, latency SLO burn, saturation | ST-4-27 3x peak load with attack mix |
| **T4-28** | Information disclosure | A lost mobile device with an offline cache is examined | Medium | High | SQLCipher, biometric gate, seven-day offline bound, remote wipe, device revoke, no offline grant data | Missing wipe acknowledgement, failed-unlock telemetry | ST-4-28 device-loss simulation; R4-04 |
| **T4-29** | Tampering | An auditor acts in one client's workspace while believing they are in another | Medium | High | One workspace at a time, explicit enter and exit, persistent banner, token `org` authoritative, workspace routes distinct from member routes | `GrantWorkspaceEntered` and `GrantWorkspaceClosed`, mismatch events | ST-4-29 concurrent enter returns `409` |
| **T4-30** | Tampering, Repudiation | An attacker with database access edits or deletes audit rows to remove evidence | Low | Critical | In-transaction append-only writes, no `UPDATE` or `DELETE` grant, hash chain, WORM archive, separate schema and role | Monthly chain verification, audit-write failure alert | ST-4-30 update and delete denied; chain verification |

### 13.7 Attack Trees

**AT-4-01: Read another tenant's data.**

```
Goal: read an object belonging to tenant B while authenticated to tenant A
├── 1. Obtain a valid session for tenant A
│   ├── 1.1 Use own legitimate account            -> blocked by permission + scope
│   ├── 1.2 Take over another account             -> T4-01, T4-03, T4-04, T4-06
│   └── 1.3 Forge a token                         -> T4-08
├── 2. Make the server believe the tenant is B
│   ├── 2.1 Set X-Org-Id to B                      -> 400 TENANT_CONTEXT_MISMATCH
│   ├── 2.2 Change path {orgId} to B               -> token org wins; 404
│   ├── 2.3 Change org_id in a request body        -> ignored by binder; WITH CHECK rejects
│   └── 2.4 Exploit a route that trusts a client tenant -> route inventory test
├── 3. Reach B's row with A's context
│   ├── 3.1 Guess an identifier                    -> UUIDv7, 404
│   ├── 3.2 Exploit a missing RLS policy           -> policy inventory + probe
│   ├── 3.3 Exploit a SECURITY DEFINER function    -> allowlist and review
│   └── 3.4 Use a reporting or export job          -> job sets context per tenant
└── 4. Read through a side channel
    ├── 4.1 Error or timing difference             -> uniform errors, dummy hash
    └── 4.2 Cache key collision                    -> org in every cache key
```

**AT-4-02: Exceed an auditor's mandate.**

```
Goal: read or retain client data beyond the grant
├── 1. Obtain a grant wider than the engagement
│   ├── 1.1 Create it yourself                     -> SOD-07, .create and .approve separated
│   ├── 1.2 Persuade a client admin                -> conflict declaration, review
│   └── 1.3 Widen after approval                   -> immutable version, extension re-approval
├── 2. Use the grant outside its scope
│   ├── 2.1 Read an unassigned show                -> ASSIGNMENT scope + RLS grant boundary
│   ├── 2.2 Switch to another client               -> one workspace, token org
│   └── 2.3 Call a member route                    -> authority-source exclusivity
├── 3. Retain data after the grant ends
│   ├── 3.1 Export before expiry                   -> watermark, profile gate, audit
│   ├── 3.2 Keep a session alive                   -> 60-second cascade, denylist
│   └── 3.3 Rely on cached data                    -> no-store, cache invalidation
└── 4. Avoid detection
    ├── 4.1 Act slowly                             -> monthly access-log review
    └── 4.2 Use a colleague's identity             -> named grants, no shared accounts
```

**AT-4-03: Take over a privileged account.**

```
Goal: control an Organization Administrator or Platform Security identity
├── 1. Credential attack        -> T4-01, T4-02
├── 2. Phish a reset or invite  -> T4-03
├── 3. Social-engineer MFA reset-> T4-04
├── 4. Steal a refresh token    -> T4-06
├── 5. Steal a device           -> T4-28
└── 6. Insider operator         -> T4-18, T4-19
```

### 13.8 Abuse-Case Walkthroughs

**Credential stuffing.** An attacker replays 200,000 email and password pairs from a 2023 breach against `/auth/login`. Roughly 60 per cent of attempts hit unknown addresses; the dummy Argon2id verification keeps response time within the normal distribution, so the attacker cannot build a valid-account list from timing. Known-account failures increment the per-account counter and the per-IP-class counter. At five failures the account locks for fifteen minutes, and the attacker cannot distinguish a locked account from a wrong password because both return the same status and body shape after the delay. Accounts with MFA never yield a session, which is why NFR-SEC-003 is a control and not a convenience. The detection rule fires on more than 50 failures from one IP class in five minutes, or more than 200 across the class in an hour, and the response is a temporary edge block plus a notification to affected account owners. The residual risk is a successful login on an account without MFA whose password was reused and not breached in a known corpus; that is R4-02's neighbourhood and is mitigated by mandatory MFA for every role that can approve, administer or export.

**Refresh token theft.** Malware reads a browser cookie store and copies the refresh token. The attacker presents it from another country. Rotation means the legitimate client's next refresh uses a token the attacker has already consumed, so the server sees a second presentation of a token whose `rotated_at` is set. The family is burned, every session in the family is revoked, `RefreshTokenReuseDetected` is emitted at critical severity, and the user is forced to authenticate again. If the attacker refreshes first, the legitimate user's next request fails and produces the same signal from the other direction. There is no sequence in which both parties continue to refresh quietly. The 60-second idempotent replay window exists for network retries, not for a second device: the replay must present the same `device_id` and the same request signature on mobile, and on web it must come from the same session cookie jar, which a copied token on another host does not have.

**MFA reset social engineering.** An attacker calls support, claims a lost phone, and knows the victim's name, email and organization. The first-line analyst can open the request but cannot approve it. Approval requires a second person with `identity.mfa.administer`, step-up on the approver's own session, a reason code and a verification step that does not rely on the caller's knowledge: an existing authenticated session, a registered device, or a video call against the identity record. The subject receives an immediate notification on every channel on file. The reset emits `MfaReset` with both actor and approver, and a subsequent login from a new device within 24 hours raises a review item automatically. The control is intentionally slower than the fraud it prevents.

**Break-glass abuse.** Break-glass credentials are sealed in KMS and released only to two named custodians who authenticate with hardware keys. Every release requires a ticket reference and a second approver, and the resulting session is capped at 60 minutes with a named action list. The session emits `EmergencyRevocationExecuted` or the equivalent support-access events before the first action, not after. The security channel receives the alert at the moment of release. A post-incident review is mandatory even when the use was justified, because the alternative is a credential that is used quietly and rotated never.

**Provisioning race.** Two operators submit the same slug within a second of each other. The API accepts both requests because idempotency keys differ, but the database's unique index on `tenancy.organization.slug` permits one insert. The loser receives `409 SLUG_TAKEN` and its `ops.provisioning_run` is marked `COMPENSATED` with zero side effects. A run that fails at step six (bucket policy) retries the step three times, then compensates the earlier steps in reverse order and leaves the tenant in `PROVISIONING_FAILED` with the slug held for 24 hours. The isolation probe at step eight is a hard gate: a tenant whose probe fails does not become `ACTIVE`, no matter how many other steps succeeded.

**RLS policy drift.** A migration adds a table and forgets the policy. The policy inventory test enumerates every table with an `org_id` column and asserts that row-level security is enabled, forced, and covered by at least one policy for `SELECT`, `INSERT`, `UPDATE` and `DELETE`. The build fails before deployment. If a policy is dropped in production despite the gate, the nightly inventory diff detects the missing policy, the isolation probe fails on the next run, and the tenant's status is moved to `SUSPENDED` with a security incident opened. The design accepts that prevention can fail; it does not accept that failure can be silent.

### 13.9 Residual Risk Register

| ID | Residual risk | Threat | Why it remains | Mitigation and acceptance | Owner | Review |
|----|---------------|--------|----------------|---------------------------|-------|--------|
| **R4-01** | A shared-schema design depends on RLS correctness; a policy defect could expose rows before a test catches it | T4-09, T4-10, T4-11 | RLS is code, and code has defects | Four independent layers, per-table negative tests, policy inventory gate, isolation probe at provisioning and deploy; accepted for the pilot because dedicated stacks remain available for regulated tenants | Head of Engineering | Quarterly and after every isolation incident |
| **R4-02** | TOTP is phishable; a real-time relay can defeat it | T4-03, T4-05 | WebAuthn is Phase 1 | Mandatory MFA for privileged roles, step-up for high-risk actions, device binding, anomaly detection, short session bounds; accepted | CISO | Before GA and at Phase 1 planning |
| **R4-03** | Account lockout is a denial-of-service vector against a known victim | T4-02 | FR-TNT-007 mandates the lock | Per-IP throttle, verified reset clears the lock, no lock-state disclosure, monitoring; accepted | CISO | Quarterly |
| **R4-04** | A stolen unlocked device with an unexpired offline grant can read local data | T4-28 | Offline capture is a product requirement | SQLCipher, biometric gate, seven-day bound, remote wipe, device revoke; accepted | CISO | Quarterly |
| **R4-05** | A platform operator with approved support access can read tenant data | T4-18, T4-19 | Someone must be able to support and recover the platform | Two-person approval, ticket, 60-minute bound, action scope, no impersonation, customer-visible audit; accepted | CISO | Monthly sample review |
| **R4-06** | A cross-region grant places minimal identity metadata in the firm's region | T4-14 | Auditors work across borders | Minimal projection only, no client business data, acknowledgement recorded, DPO review; accepted | DPO | Per cross-region grant |
| **R4-07** | An authorised export cannot be recalled after download | T4-21 | Data leaves the platform by design | Watermark, row caps, step-up, export event, contractual terms, access review; accepted | CISO | Quarterly |
| **R4-08** | An auditor can memorise or reconstruct what they read | T4-14 | Human cognition cannot be controlled | NDA, conflict declaration, least-privilege profile, audit trail, contractual remedy; accepted | Legal | Annually |
| **R4-09** | Expiry notices may not reach the owner because email is unreliable | T4-16 | Email is an external dependency | Retries, in-app notice, register evidence, escalation at T-7 and T-1, grant expires regardless of delivery; accepted | Head of Customer Success | Quarterly |
| **R4-10** | A future identity provider becomes a trusted third party for authentication | T4-08 | Federation is Phase 1 and adds a dependency | Per-tenant IdP configuration, signed assertions, explicit trust registration, fallback local account policy; not applicable at MVP | CISO | Before Phase 1 GA |

**R4-01 is the defining residual risk of the architecture.** Everything else is bounded by a time window, a scope or a two-person rule. RLS correctness is bounded only by testing and review. That is why the negative suite, the policy inventory and the provisioning probe are release gates rather than quality suggestions.

### 13.10 Control Mapping to Binding Requirements

| Requirement | Threats addressed | Control evidence |
|-------------|-------------------|------------------|
| NFR-SEC-001 | T4-03, T4-06, T4-07 | TLS policy at the edge, HSTS preload, configuration scan |
| NFR-SEC-002 | T4-06, T4-22 | Encryption at rest, per-tenant keys, KMS audit |
| NFR-SEC-003 | T4-01, T4-04, T4-18 | MFA matrix, reset approval, platform-role enrolment report |
| NFR-SEC-004 | T4-09, T4-10, T4-11, T4-17 | Pipeline test, RLS suite, per-endpoint authorization matrix |
| NFR-SEC-005 | T4-09, T4-11, T4-22 | Per-build negative suite, annual penetration test, isolation probe |
| NFR-SEC-011 | T4-07, T4-28 | Device registration, revoke and wipe tests |
| NFR-SEC-013 | T4-01, T4-27 | Rate-limit tests, anomaly rules, attack-mix load test |
| NFR-PRV-009 | T4-22 | Region pinning, restore isolation, cross-region grant review |
| NFR-AUD-001 | T4-30 | Append-only audit, denial events, in-transaction writes |
| NFR-AUD-002 | T4-30 | Hash chain, WORM archive, verification job |
| NFR-AUD-004 | T4-18, T4-19 | Correlation ID across request, audit and event |
| NFR-AUD-005 | T4-21, T4-30 | Query targets, index coverage, 90-day search test |
| NFR-MOB-004 | T4-28 | Offline bound, wipe acknowledgement, device test |
| FR-TNT-007 | T4-01, T4-02 | Lockout and rate-limit tests |
| FR-TNT-010 | T4-24 | Role-change propagation within 60 seconds |
| FR-TNT-014 | T4-17, T4-24 | Authorization matrix on every endpoint |
| FR-TNT-016 | T4-13, T4-14, T4-15 | Grant gates, SOD-07, conflict declaration |
| FR-TNT-018 | T4-09, T4-10 | Tenant-context pipeline tests |
| FR-TNT-022 | T4-06, T4-07 | Session bound and revocation tests |

### 13.11 Threat Model Maintenance

The register is reviewed at four triggers: a new external endpoint, a new grant profile or scope type, a new region or identity provider, and any penetration-test finding rated high or critical. A threat that loses its control without a replacement is a release blocker, not a documentation update. The register is stored with the architecture repository so that a change to section 8 or section 11 that does not update section 13 is visible in review.

### 13.12 Traceability for Section 13

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| FR-TNT-006 | T4-16, section 10.6 | Deactivation cascade and orphaned-grant job |
| FR-TNT-007 | T4-01, T4-02, section 11.13 | Lockout and rate-limit controls |
| FR-TNT-008 | T4-03, section 4.3 | Credential policy and reset controls |
| FR-TNT-014 | T4-09, T4-10, T4-17 | Authorization and isolation threats |
| FR-TNT-015 | T4-13, T4-15 | Grant creation and approval threats |
| FR-TNT-016 | T4-14, T4-15, T4-16 | Grant misuse and expiry threats |
| FR-TNT-017 | T4-16, R4-09 | Notices and orphaned grants |
| FR-TNT-018 | T4-09, T4-10 | Tenant-context threats |
| FR-TNT-021 | T4-16, section 8.12 | Access review as a compensating control |
| FR-TNT-022 | T4-06, T4-07 | Session threats |
| NFR-SEC-003 | T4-01, T4-04 | MFA and reset threats |
| NFR-SEC-004 | T4-09, T4-10, T4-11 | Isolation threats |
| NFR-SEC-005 | T4-11, T4-22 | Probe and restore verification |
| NFR-SEC-011 | T4-28 | Device-loss threat |
| NFR-SEC-013 | T4-01, T4-27 | Abuse and denial threats |
| NFR-PRV-009 | T4-22, R4-06 | Residency and restore threats |
| NFR-AUD-001 | T4-30 | Audit integrity threat |
| NFR-AUD-002 | T4-30 | Hash chain and WORM |
| NFR-AUD-005 | T4-21, T4-30 | Export and audit query threats |
| NFR-MOB-004 | T4-28, R4-04 | Offline device threat |
| CAP-001 | T4-20 | Provisioning race |
| CAP-002 | T4-01 to T4-08, T4-28 | Identity threats |
| CAP-003 | T4-13 to T4-17, T4-24 | Authorization and grant threats |

---

## 14. Performance and Capacity

### 14.1 Principles

1. **A target without a budget is a wish.** Every latency target is decomposed across network, authentication, authorization, handler, data access, serialization and client render, following Document 3 section 11.3.
2. **Authorization is on the hot path of every request.** A design that costs 200 milliseconds per decision is a design that will be bypassed by a cache with no invalidation. The PDP budget is therefore single-digit milliseconds warm.
3. **RLS is measured, not assumed.** The policy predicate runs on every row of every tenant query. Its cost is tracked as a first-class performance metric, because a policy that defeats an index turns a 10-millisecond query into a 4-second scan.
4. **Tenant fairness beats average latency.** One tenant running a 50,000-row export must not raise another tenant's login latency. Queue classes, per-tenant concurrency caps and bulkheads provide that separation.
5. **Degrade in a defined order.** When a dependency fails, the platform chooses read-only, queued write or fail-closed deliberately, and the choice is documented in section 14.9 rather than discovered during an incident.

### 14.2 Targets

| Operation | Metric | Target | Source | Measurement |
|-----------|--------|--------|--------|-------------|
| Access token validation | p95 | 2 ms | Internal budget | APM span |
| Session and denylist lookup | p95 | 3 ms | Internal budget | APM span, Redis latency |
| Policy decision, warm cache | p95 / p99 | 8 ms / 20 ms | Internal budget | PDP histogram |
| Policy decision, cold | p95 / p99 | 25 ms / 60 ms | Internal budget | PDP histogram with cache-state label |
| Authentication and scope, standard CRUD | p95 | 30 ms | Document 3 section 11.3 | APM |
| Authentication and scope, dashboard | p95 | 40 ms | Document 3 section 11.3 | APM |
| Login with password and TOTP | p95 | 800 ms | Security cost of Argon2id | Login histogram |
| Refresh rotation | p95 | 150 ms | Internal budget | Endpoint histogram |
| Standard CRUD API | p95 | 500 ms | NFR-PERF-012 | Load test |
| Expense submission | p95 | 1 s | NFR-PERF-003 | Load test |
| Dashboard initial load | p95 | 2 s | NFR-PERF-001 | APM and RUM |
| Dashboard drill-down | p95 | 2 s | NFR-PERF-002 | Load test |
| Search | p95 | 1 s | NFR-PERF-008 | Load test |
| Grant register, 5,000 live and 50,000 total grants | p95 | 400 ms | Internal budget | Load test |
| Grant detail with scope labels | p95 | 250 ms | Internal budget | Load test |
| Grant activation transaction | p95 / p99 | 500 ms / 1 s | Internal budget | Endpoint histogram |
| Access review open, 50,000 items | elapsed | 120 s asynchronous | Internal budget | Job telemetry |
| Access review first page | p95 | 500 ms | Internal budget | Load test |
| Access review decision | p95 | 200 ms | Internal budget | Load test |
| Grant revocation cascade | p95 / commitment | 20 s / 5 min | BR-AUD-003, FR-TNT-016 | Cascade job telemetry |
| Tenant suspension effective | maximum | 60 s | Section 2.9 | Synthetic probe |
| Role change propagation | maximum | 60 s | FR-TNT-010 | Synthetic probe |
| Provisioning, ten steps | p95 / maximum | 6 min / 10 min | Internal budget | Run telemetry |
| Isolation probe | p95 | 90 s | Internal budget | Probe telemetry |
| Outbox dispatch | p95 | 5 s | Document 3 section 11.5 | Oldest-event age |
| Audit query over 90 days | p95 | 5 s | NFR-AUD-005 | Query telemetry |
| Report generation, 100,000 rows | p95 | 30 s | NFR-PERF-007 | Job telemetry |
| RLS overhead versus no-RLS baseline | p50 / p95 | +10% / +20% | Document 3 TR-001 | A/B benchmark |
| Availability, MVP | monthly | 99.5% | NFR-AVL-001 | SLO dashboard |
| Availability, Phase 1 | monthly | 99.9% | NFR-AVL-002 | SLO dashboard |

**Why login is allowed 800 milliseconds.** Argon2id at 64 MiB and three iterations is deliberately expensive. A login target of 100 milliseconds would force a weaker memory cost and trade a measurable user inconvenience for an unmeasurable security loss. The target is set so that a 400-millisecond hash plus database, network and MFA verification fits comfortably, and it is excluded from the general CRUD target for that reason.

**Why the cold-decision target is separate.** A cold decision reads membership, assignments, role permissions and grants from PostgreSQL. It happens at login and after a `scope_version` change, and the result is cached. Reporting it inside the 8-millisecond warm target would hide the cost of the query path, and reporting it inside the 30-millisecond CRUD budget would make an occasional but correct cost look like a regression. The two are measured separately, and the cold rate is alerted above 1 per cent of decisions.

### 14.3 Request Budget

The pipeline in section 11.3 is budgeted as follows for a standard CRUD request with a 30-millisecond authorization budget.

| Step | Component | Budget |
|------|-----------|--------|
| 1 | TLS termination, edge routing | 5 ms |
| 2 | Route resolution | 1 ms |
| 3 | Token signature and claim validation | 2 ms |
| 4 | Session and denylist lookup | 3 ms |
| 5 | Idle and absolute lifetime check | 1 ms |
| 6 | Tenant status check, cached | 1 ms |
| 7 | Tenant assertion comparison | 1 ms |
| 8 | Membership or grant state check, cached | 2 ms |
| 9 | Policy decision, warm | 8 ms |
| 10 | SoD, limit, step-up, delegation | 2 ms |
| 11 | Database context set | 1 ms |
| 12 | Handler, data access, serialization | remaining budget |
| | **Authorization subtotal** | **27 ms** |
| | **Margin** | **3 ms** |

**The budget is enforced in code.** Each stage emits a span, and a stage that exceeds its budget twice in a rolling hour raises a warning; three times raises an error. The purpose is attribution, not punishment: when p95 rises, the team should know whether the PDP, the denylist or the handler moved.

### 14.4 Query Budgets

| Rule | Value | Enforcement |
|------|-------|-------------|
| `statement_timeout`, API | 5 s | Connection string and role setting |
| `statement_timeout`, reports and jobs | 60 s | Separate role |
| `lock_timeout`, API | 2 s | Transaction begin |
| `lock_timeout`, migrations | 10 s, then retry | Migration runner |
| Maximum joins per request query | 5 | Query review and CI lint |
| Maximum rows examined by a list query | 10,000 | `EXPLAIN` gate |
| Sequential scan on a tenant table above 1,000 rows | Fails CI | `EXPLAIN` gate on top 25 queries |
| `OFFSET` on tables above 10,000 rows | Prohibited | Lint rule |
| N+1 queries per request | Zero | Query-count assertion in integration tests |
| Unbounded `COUNT(*)` on a list endpoint | Prohibited | API review and test |

**Tenant predicate must be index-eligible.** Every policy uses `org_id = app.current_org_id()`, where `app.current_org_id()` is a `STABLE` function reading `current_setting('app.org_id')`. A `STABLE` function can be evaluated once per statement rather than once per row, and an index on `(org_id, ...)` remains usable. A policy written as `org_id = current_setting('app.org_id')::uuid` is also index-eligible in PostgreSQL 17 but is easier to defeat with an expression that the planner cannot fold; the function form is the standard.

**Every tenant index leads with `org_id`.** A composite index on `(org_id, show_id, occurred_at DESC)` serves both the RLS predicate and the list filter. An index that does not lead with `org_id` forces the planner to choose between the policy and the filter, and the usual result is a bitmap heap scan over the whole tenant.

**Partition pruning is mandatory.** `compliance.audit_event` and `expense.expense` are partitioned monthly. A query without an `occurred_at` range scans every partition, which is the most common cause of an audit-query timeout. The query layer adds a default 90-day range and refuses an unbounded request with `422 RANGE_REQUIRED`.

### 14.5 Data Volume Model

| Entity | Pilot | GA year one | Year three | Growth driver |
|--------|-------|-------------|------------|---------------|
| Organizations | 25 | 500 | 1,500 | Sales, auditor firms |
| Users | 2,500 | 50,000 | 150,000 | Members plus auditors |
| Memberships | 2,500 | 50,000 | 150,000 | One active at MVP |
| Role assignments | 7,500 | 150,000 | 450,000 | Three per member average |
| Grants, all states | 2,000 | 50,000 | 150,000 | Engagements per year |
| Grants, active | 400 | 5,000 | 15,000 | Concurrent audits |
| Refresh sessions, active | 3,000 | 60,000 | 180,000 | Devices per user |
| Audit events per year | 15 million | 120 million | 360 million | Twelve events per expense plus security events |
| Outbox events per day | 200,000 | 1.5 million | 4.5 million | Domain event fan-out |
| Object storage | 50 GB | 5 TB | 15 TB | Evidence and exports |
| Database size, hot | 100 GB | 1.5 TB | 4 TB | Audit and expense partitions |

**The audit table dominates growth.** At 1 KB per row including indexes, 120 million events is roughly 120 GB per year. Hot retention is 24 months (Document 3 section 8.7), so the hot table is bounded at about 300 GB before partition archival, which is close to the 250 GB archival trigger. The trigger is therefore expected to fire in year two at GA volume, and archival must be operational before the first 250 GB partition is reached.

**Sessions are small but hot.** Sixty thousand active sessions at roughly 400 bytes each is 24 MB of table and a comparable cache footprint. The denylist is smaller: only revoked-but-unexpired sessions, which is a fraction of the active set.

### 14.6 Caching

| Cache | Key | TTL | Invalidation | Maximum staleness | Failure mode |
|-------|-----|-----|--------------|-------------------|--------------|
| Policy decision | `user_id`, `org_id`, `scope_version`, `resource_hash` | 60 s | `RoleChanged`, `ScopeVersionBumped`, `SessionRevoked`, grant state change | 60 s | Fail closed, recompute |
| Role definition | `org_id`, `role_id`, `version` | 5 min | `RoleChanged`, `RoleRetired` | 5 min | Read from database |
| Grant decision | `grant_id`, `version`, `state` | 60 s | Any grant transition | 60 s | Fail closed |
| Tenant status | `org_id` | 30 s | `OrganizationSuspended`, `OrganizationActivated`, `OrganizationArchived` | 30 s | Read from database |
| Session denylist | `sid` | Until token expiry | `SessionRevoked` | 5 s local snapshot | Fail closed after 60 s |
| JWKS | `kid` | 1 h | Key rotation | 1 h, overlap window | Use cached key until overlap ends |
| Permission catalogue | `org_id`, `catalogue_version` | 1 h | Deployment | 1 h | Read from database |

**What is never cached.** Authorization decisions are never shared across users, even when two users hold identical roles, because the cache key would have to include the full authority set and the risk of a key collision is a cross-user disclosure. Cross-tenant object existence is never cached; a `404` is cheap and a cached existence check is an oracle. Presigned URLs are never cached beyond their 15-minute life. A grantee's client list is never cached across requests.

**Cache sizing.** Fifty thousand users with three common scope combinations produce about 150,000 decision entries. At roughly 500 bytes per entry including keys and metadata, the working set is about 75 MB, which fits comfortably in the Redis instance with headroom for sessions and rate-limit counters. The cache is a performance layer, never a source of truth.

**Stampede control.** A cold cache after a deploy must not send 60,000 simultaneous decisions to PostgreSQL. The decision cache uses a per-key single-flight lock with a 50-millisecond wait; the loser waits for the winner's value rather than issuing a duplicate query. On a full flush, a 5 per cent random early refresh spreads the load over the TTL window.

### 14.7 Connections and Concurrency

| Concern | Decision |
|---------|----------|
| Pooling mode | PgBouncer transaction mode in front of PostgreSQL |
| API pool per instance | 40 connections, maximum 60 |
| Worker pool | 20 connections |
| Report pool | 10 connections, read replica |
| Maximum server connections | 400, with 100 reserved for operations |
| Context setting | `SET LOCAL` at the start of every transaction; no session-level `SET` |
| Prepared statements | Named statements disabled in transaction pooling; command-level caching only |
| Temporary tables | Prohibited in application transactions |
| Advisory locks | Only for provisioning and migration, with a bounded wait |
| Read replicas | Reports and exports only; never authentication or authorization reads |

**Why transaction pooling is compatible with RLS.** The tenant context is set with `SET LOCAL`, which is scoped to the transaction. In transaction pooling, a server connection is assigned for the duration of a transaction and returned afterwards, so the local setting cannot leak to the next client. This property holds only if the application never uses session-level `SET`, never relies on session state and never opens a transaction and leaves it idle across a client request. All three are prohibited and tested.

**Why authentication never reads a replica.** A replica can lag by hundreds of milliseconds. A session revocation written to the primary and read from a replica is a revocation that has not happened. Authentication, denylist checks and grant-state checks always read the primary or the cache that is invalidated by the primary write.

### 14.8 Load and Scale Scenarios

| ID | Scenario | Load | Duration | Pass criteria |
|----|----------|------|----------|---------------|
| LT-4-01 | Login storm | 200 requests per second, 30 per cent MFA | 15 min | p95 800 ms, lockout correct, no 5xx |
| LT-4-02 | Baseline mixed traffic | 350 requests per second, 3,000 users | 2 h | All p95 targets met |
| LT-4-03 | Peak mixed traffic | 1,200 requests per second, 10,000 users | 2 h | All p95 targets met, error rate below 0.1 per cent |
| LT-4-04 | Grant register at scale | 50,000 grants, 5,000 active, deep pagination | 30 min | p95 400 ms, no sequential scan |
| LT-4-05 | Access review at scale | 50,000 items, 20 reviewers | 2 h | Open under 120 s, decisions p95 200 ms |
| LT-4-06 | Revocation cascade | 500 sessions across 200 grants | 10 min | 99 per cent under 60 s, all under 5 min |
| LT-4-07 | Concurrent provisioning | 20 tenants, 5 with induced failures | 30 min | No duplicate slug, all failures compensated |
| LT-4-08 | RLS overhead A/B | Same query set with and without policies | 30 min | p50 increase at most 10 per cent, p95 at most 20 per cent |
| LT-4-09 | Cold cache stampede | Flush Redis, 600 requests per second | 10 min | Recovery within 2 min, p95 under 2 s after warm-up |
| LT-4-10 | Month-end audit query | 90-day range over 120 million rows | 15 min | p95 5 s, partition pruning confirmed |
| LT-4-11 | Attack mix | 3x peak with 20 per cent malicious login traffic | 30 min | Legitimate p95 within 20 per cent of baseline |
| LT-4-12 | Soak | Baseline for 24 h | 24 h | No memory growth above 5 per cent, no connection leak |

**Every scenario runs with RLS enabled and with production-shaped data.** A load test against a superuser role with policies disabled measures a system that does not exist.

### 14.9 Degradation Modes

| Dependency | Failure | Behaviour | User-visible result | Recovery |
|------------|---------|-----------|---------------------|----------|
| Redis | Unavailable | PDP falls back to database; denylist serves a local snapshot for 60 s, then fails closed | Latency rises, new logins may be refused | Automatic reconnect; cache warms |
| PostgreSQL primary | Unavailable | All writes and authentication fail; reads from replica are refused for authority | `503 DEPENDENCY_UNAVAILABLE` | Failover, 60–120 s |
| PostgreSQL replica | Unavailable | Reports queue; interactive traffic unaffected | Report delay | Replica rebuild |
| KMS | Unavailable | New logins and refreshes fail; existing access tokens remain valid until expiry; PII decryption fails closed | Login unavailable, existing sessions continue | KMS retry and escalation |
| Object storage | Unavailable | Evidence upload returns `503` with retry guidance; downloads fail | Upload and export unavailable | Retry with backoff |
| Outbox relay | Stopped | Business writes continue; events accumulate; projections lag | Dashboards stale, labelled by `as_of` | Restart; oldest-event alert |
| Event consumer | Stopped | Its projection or notification lags; no write impact | Feature-specific staleness | Replay from outbox |
| SMTP provider | Unavailable | Invitations and notices queue and retry; in-app notice still delivered | Email delay | Retry, alternate provider |
| Region | Unavailable | Tenant unavailable; DR runbook restores in the paired region | Outage | RTO 4 h, RPO 15 min |
| Search index | Unavailable | Search falls back to database `tsvector`; slower | p95 may exceed 1 s | Rebuild index |

**Fail-closed is a deliberate choice for authority.** If the denylist cannot be consulted beyond 60 seconds, the platform refuses requests rather than serving authority that may have been revoked. The 60-second window is the longest period for which a stale snapshot is defensible against the five-minute revocation commitment; beyond it, refusal is the safer failure. The trade-off is recorded in the availability review, not hidden in a retry policy.

### 14.10 Pilot Capacity Plan

| Component | Pilot specification | Headroom | Scale trigger |
|-----------|---------------------|----------|---------------|
| API hosts | 3 instances, 2 vCPU, 4 GB | 3x baseline | CPU above 60 per cent for 10 min |
| Worker hosts | 2 instances, 2 vCPU, 4 GB | 3x queue depth | Oldest event above 30 s |
| PostgreSQL | 1 primary, 1 replica, 4 vCPU, 16 GB, Multi-AZ | 2x pilot data | Connection saturation or p95 query regression |
| Redis | 1 primary, 1 replica, 2 GB | 4x working set | Eviction rate above zero |
| Object storage | S3-compatible, versioned, Object Lock | Effectively unbounded | Cost review at 5 TB |
| Regions | India and EU, one active per tenant | One region failure tolerated with DR | Tenant count per region above 250 |

**The pilot is sized for 25 tenants, not 500.** The capacity plan deliberately does not pre-build GA scale, because the load model will change once real grant and audit volumes are observed. The triggers define when to scale, and the load scenarios prove that scaling the API tier is horizontal while the database tier scales vertically to a point and then requires partitioning or a dedicated stack.

### 14.11 Rejected Patterns

| Pattern | Why it is rejected |
|---------|--------------------|
| Full permission list in the access token | Token size grows with the catalogue, revocation waits for expiry, and scope cannot be expressed |
| `roles` or `permissions` claims trusted by the resource server | The token becomes the authority instead of the database, and a role change cannot take effect within 60 s |
| Per-row policy function that queries another table | Turns a set query into N+1 queries inside the database; policies must be predicate-only |
| `BYPASSRLS` for reporting | One reporting bug becomes a cross-tenant breach; reports set context per tenant |
| `COUNT(*)` on list endpoints | Full scan per page and volume disclosure |
| `OFFSET` pagination | Cost grows linearly and results shift under concurrent writes |
| Session-level `SET app.org_id` | Leaks across pooled connections and survives a transaction |
| Caching a decision by role rather than by user and scope version | A role is not an authority; two users with the same role can have different scopes |
| Materialised views without `org_id` | The view cannot be protected by RLS and becomes an unaudited path |
| Read replica for session validation | Replication lag converts a revocation into a suggestion |

### 14.12 Traceability for Section 14

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| NFR-PERF-001 | Sections 14.2, 14.6 | Dashboard target and cache strategy |
| NFR-PERF-002 | Section 14.2 | Drill-down target |
| NFR-PERF-003 | Sections 14.2, 14.3 | Expense submission budget |
| NFR-PERF-004 | Section 14.2 | Projection freshness carried from Document 3 |
| NFR-PERF-007 | Sections 14.2, 14.9 | Report target and queue behaviour |
| NFR-PERF-008 | Section 14.2 | Search target and fallback |
| NFR-PERF-012 | Sections 14.2, 14.3 | CRUD target and authorization budget |
| NFR-AVL-001 | Section 14.2 | MVP availability |
| NFR-AVL-004 | Section 14.9 | Defined degradation modes |
| NFR-AVL-005 | Section 14.9 | Offline behaviour preserved |
| NFR-AVL-006 | Section 14.9 | RTO in region failure |
| NFR-AVL-007 | Section 14.9 | RPO in region failure |
| NFR-SCL-001 | Section 14.5 | Volume model |
| NFR-SCL-002 | Section 14.8 | Concurrent-user load scenario |
| NFR-SCL-003 | Section 14.5 | Object storage forecast |
| NFR-SCL-004 | Section 14.10 | Horizontal API scaling |
| NFR-SCL-005 | Section 14.8 | Peak and attack-mix scenarios |
| NFR-SCL-006 | Sections 14.4, 14.5 | Partitioning and archival |
| NFR-SEC-004 | Sections 14.4, 14.7 | RLS-aware query and connection model |
| NFR-AUD-005 | Sections 14.2, 14.4 | Audit query target and partition rule |
| FR-TNT-010 | Section 14.2 | Role propagation target |
| FR-TNT-016 | Section 14.2 | Revocation cascade target |
| FR-TNT-022 | Section 14.6 | Session cache and denylist |
| CAP-001 | Section 14.10 | Tenant capacity plan |
| CAP-002 | Section 14.6 | Identity cache model |
| CAP-003 | Section 14.2 | Decision latency target |

---
