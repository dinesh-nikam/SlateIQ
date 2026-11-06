---

## 8. Cross-Organization Access Grants

### 8.1 What a Grant Is, and What It Is Not

A grant is the only mechanism by which a person who is not a member of an organization can see that organization's data. It is a client-issued, time-bound, scope-bound, individually revocable authorisation. It is not a role, not a membership, and not a partnership between tenants.

| A grant **is** | A grant **is not** |
|----------------|--------------------|
| Issued by one client organization to one named human | Issued to a firm, a team, a group or an email domain |
| Bounded by a grant profile (the "permission level" of FR-TNT-015) | A role that can be edited, extended or composed |
| Bounded by an explicit scope and a start and end date | Open-ended, renewable by default, or self-extending |
| Accepted by the grantee with MFA, NDA and conflict declaration | Activated by the client alone |
| Revocable by the client at any time, without the grantee's consent | Revocable only by the firm that employs the auditor |
| Recorded as its own object with its own audit trail | An entry in the auditor's role assignments |
| Stored in the client's region | Copied, cached or mirrored into the firm's region |

**Why a separate object.** The archived draft modelled external access as a role assignment with a flag, which produced two defects: the client could not see the assignment in its own directory, and the auditor accumulated permissions across engagements. A grant lives in the client's tenant, references the grantee by `user_id` without creating a membership, and dies on its own schedule. The client's grant register is therefore a complete answer to "who outside this organization can see our data", which is the question an auditor, a DPO and an incident responder all ask first.

**Why one grantee per grant.** BR-AUD-002 requires exactly one client organization. This document adds exactly one grantee. A firm-wide grant cannot be reviewed per person, cannot be revoked for the one auditor who left, and cannot name the individual accountable for an export. Five auditors on an engagement means five grants and five revocations, which is the correct amount of administrative work for the risk.

### 8.2 Grant Lifecycle

Eight states, one terminal outcome per path. The state machine is enforced in the service layer and mirrored by a database trigger, so a direct update cannot skip a gate.

| State | Meaning | Data readable | Sessions | Exits to |
|-------|---------|---------------|----------|----------|
| `DRAFT` | Being prepared; not yet submitted | No | None | `PENDING_APPROVAL`, deleted |
| `PENDING_APPROVAL` | Submitted; awaiting the second person | No | None | `PENDING_ACCEPTANCE`, `DECLINED` |
| `PENDING_ACCEPTANCE` | Approved; awaiting the grantee's gates | No | None | `ACTIVE`, `DECLINED` |
| `ACTIVE` | In force | Yes, within profile and scope | Grant workspace only | `SUSPENDED`, `REVOKED`, `EXPIRED` |
| `SUSPENDED` | Temporarily frozen | No | Revoked | `ACTIVE`, `REVOKED`, `EXPIRED` |
| `REVOKED` | Withdrawn by the client or the platform | No | Revoked | Terminal |
| `EXPIRED` | End date reached | No | Revoked | Terminal |
| `DECLINED` | Approver or grantee refused, or acceptance window lapsed | No | None | Terminal |

| Transition | Trigger | Actor | Guards | Side effects |
|------------|---------|-------|--------|--------------|
| `DRAFT` to `PENDING_APPROVAL` | Submit | Creator with `audit.access_grant.create` | Profile, scope, dates and owner present; grantee attested and active; no overlapping grant | `GrantSubmitted`; approver candidates notified |
| `DRAFT` to deleted | Discard | Creator | Never submitted | Physical delete; `GrantDraftDiscarded` |
| `PENDING_APPROVAL` to `PENDING_ACCEPTANCE` | Approve | A different human with `audit.access_grant.approve` and step-up | SOD-07 satisfied; Finance Head co-approval when profile is `AUDIT_FINANCE`; client region acknowledged when regions differ | `GrantApproved`; acceptance invitation sent to the grantee; 14-day acceptance window starts |
| `PENDING_APPROVAL` to `DECLINED` | Decline | Approver | Reason of at least 20 characters | `GrantDeclined`; creator notified |
| `PENDING_ACCEPTANCE` to `ACTIVE` | All gates complete and start date reached | Grantee | MFA confirmed; current NDA accepted; COI declared clear or resolved; cross-region acknowledgment where required; start date not in the future beyond 24 hours | `GrantAccepted`, `GrantActivated`; workspace available; grantee and firm admin notified |
| `PENDING_ACCEPTANCE` to `DECLINED` | Grantee declines, or window lapses | Grantee or system | Window is 14 days from approval | `GrantDeclined`; no data was ever readable |
| `ACTIVE` to `SUSPENDED` | Client suspension, firm deprovisioning, identity suspension, NDA supersession, unresolved conflict, platform security action | Client approver, firm admin, or system | Reason code mandatory | `GrantSuspended`; sessions revoked; workspace closed; exports cancelled |
| `SUSPENDED` to `ACTIVE` | Resume | Client approver plus fresh approval for security suspensions | Triggering condition cleared; NDA current; COI clear; dates still valid | `GrantResumed`; new acceptance record; new workspace session |
| `ACTIVE` to `REVOKED` | Client revokes, platform revokes, engagement ends | Client approver or Platform Security | Reason mandatory; no grantee consent needed | `GrantRevoked`; cascade in section 8.8 |
| `ACTIVE` or `SUSPENDED` to `EXPIRED` | `end_at` reached | System, evaluated at decision time and by job | None | `GrantExpired`; cascade in section 8.8 |
| `REVOKED`, `EXPIRED`, `DECLINED` | Any further transition | — | Rejected by trigger | — |

**Invariants.**

1. A grant in any state other than `ACTIVE` confers no authority. Section 6.6 enforces this at decision time; the job that flips the state is a convenience, not the control.
2. A grant never widens. Profile and scope are fixed at approval; every later change either narrows immediately or produces a new approval.
3. A grant never converts into a membership. There is no "convert to user" action, because that would move an external principal into the client's directory without an invitation.
4. A grantee with an `ACTIVE` grant to organization A and another to organization B holds one workspace token at a time. Switching is explicit and audited.
5. Deleting a `DRAFT` is the only physical delete in the lifecycle. Every other state is retained for seven years.

### 8.3 Creation and Approval

**Who may create.** FR-TNT-015 names "Client Administrators or Audit Managers". In the role catalogue these are `COMMISSIONING_ADMIN` (OTT platform and broadcaster), `PRODUCTION_ADMIN` (production house) and `AUDIT_MANAGER` (all data organizations). This section refines the authority summary in section 5.5 with the complete grant-permission matrix; Appendix B carries the same matrix per role.

| Role | `audit.access_grant.create` | `.approve` | `.delete` | `.read` | Notes |
|------|------------------------------|-----------|----------|---------|-------|
| `COMMISSIONING_ADMIN` | Yes | Yes | Yes | Yes | Cannot approve a grant they created (SOD-07) |
| `AUDIT_MANAGER` | Yes | No | Yes | Yes | Creates and revokes; never approves |
| `PRODUCTION_ADMIN` | Yes | Yes | Yes | Yes | Production houses only; same SOD-07 restriction |
| `FINANCE_HEAD` | No | Co-approval for `AUDIT_FINANCE` | No | Yes | Co-approval recorded separately from the approver |
| `ORG_ADMIN` | No | No | No | Yes | Sees the register for access review; cannot confer external access |
| `ACCESS_REVIEWER` | No | No | No | Yes | Certifies, cannot create |
| `AUDIT_FIRM_ADMIN` | No | No | No | Yes, own firm's grants | Can suspend its own auditor, which cascades |
| `PLATFORM_SECURITY` | No | No | Yes, emergency | Yes, all | Platform-level revocation with a ticket |

**Revocation uses `.delete`.** The permission action set is closed and contains no `revoke` verb (Document 3 Appendix C.1). `audit.access_grant.delete` therefore means "terminate this authority early"; the grant row is state-transitioned to `REVOKED`, not physically deleted, because section 8.14 retains it for seven years. Discarding a draft uses the same permission and is the only physical delete.

**Two-person rule.** The creator and the approver are different humans, enforced by `created_by <> approved_by` and by SOD-07. A single administrator in a small production house cannot self-approve; the platform requires a second person with the permission, and if none exists the grant cannot be created. That is an intended dead end: an organization with one administrator and no second approver has no independent control over external access, and inventing one by allowing self-approval would make the control fictional.

**Creation form.** The fields below are mandatory; the form cannot submit without them.

| Field | Rule |
|-------|------|
| `grantee_user_id` | An active human in an `AUDIT_FIRM` tenant, attested by that firm, with a confirmed MFA factor |
| `firm_org_id` | Derived from the grantee; the client cannot name an arbitrary firm |
| `owner_user_id` | The accountable business owner; defaults to the creator, may be another member with `audit.audit_assignment.read` |
| `profile_key` | One of `AUDIT_READ`, `AUDIT_EVIDENCE`, `AUDIT_FINANCE` |
| `permissions_denied` | Optional subset of the profile, used to withhold specific data |
| `scope_type`, `scope_ids` | `SHOW` or `ASSIGNMENT`; `ORG` is prohibited for external principals |
| `start_at`, `end_at` | `end_at > start_at`; `end_at <= start_at + 12 months` (BR-AUD-002) |
| `engagement_reference` | The client's engagement, contract or appointment reference |
| `purpose` | Minimum 30 characters; appears on the register and in the workspace banner |
| `region_acknowledgment` | Required when `firm_region <> client_region` |

**Scope for external principals.** A grant may name one show or one audit assignment. It may not name the whole organization, because "everything" is not a scope an external party can be given for a limited engagement. `ASSIGNMENT` is the preferred scope for statutory audits: it carries the show and the engagement date range, so the grant narrows itself when the assignment closes.

**Profile selection guidance.** `AUDIT_READ` for a review that needs to look; `AUDIT_EVIDENCE` when the auditor must capture evidence and raise findings; `AUDIT_FINANCE` when cost, variance and vendor data are in scope. The client chooses, not the auditor. An auditor who asks for `AUDIT_FINANCE` for a compliance review is asking for data the engagement does not need, and the approval step is the place that conversation happens.

**Validation at creation, not at acceptance.** An invalid `show_id`, an unattested auditor, a profile outside the catalogue, a denied permission outside the profile, or an end date beyond twelve months fails the draft. Deferring these to acceptance would produce a grant that the auditor is invited to accept and cannot.

### 8.4 Acceptance Gates

FR-TNT-016 requires MFA enrollment, NDA acknowledgment and a conflict-of-interest declaration before activation. Each is a separate record with its own evidence, because a single boolean cannot answer "which NDA, when, from which address" during a dispute.

| Gate | Evidence record | Requirement | Failure behaviour |
|------|-----------------|-------------|-------------------|
| **Identity attested** | `tenancy.auditor_profile.firm_attested_by` | The firm administrator confirms the person is engaged by the firm and names the engagement | Grant cannot be created |
| **MFA enrolled** | `identity.mfa_factor` confirmed | A confirmed TOTP factor at the home organization | Grant stays `PENDING_ACCEPTANCE`; the acceptance page routes to enrollment |
| **NDA acknowledged** | `tenancy.grant_acknowledgment.nda_version`, timestamp, IP, user-agent hash | The current platform NDA version, or the client's own NDA version when the client supplies one | Grant stays `PENDING_ACCEPTANCE`; a superseded NDA version forces re-acceptance before activation |
| **Conflict declared** | `tenancy.coi_declaration` | The current declaration question set, answered | A declared conflict blocks activation and routes to the client |
| **Client approval** | `tenancy.access_grant.approved_by`, `approved_at` | A second human with the approve permission, plus Finance Head co-approval for `AUDIT_FINANCE` | Grant never leaves `PENDING_APPROVAL` |
| **Cross-region acknowledgment** | `cross_region_acknowledged_at` | Only when the firm's region differs from the client's region | Grant stays `PENDING_ACCEPTANCE` |

**Conflict declaration is not a checkbox.** The declaration asks whether the auditor, their firm, or an immediate household member has a financial interest in the client, a current or recent engagement with the client outside this grant, or a personal relationship with a person who can influence the subject of the audit. A "yes" is not an automatic refusal; it is a hold that requires the client's Audit Manager and Organization Administrator to record a decision with reasoning. BR-AUD-005 then blocks the conflicted show until the client resolves the conflict in writing.

| Conflict outcome | Grant state | Further action |
|------------------|-------------|----------------|
| No conflict declared | Proceeds to `ACTIVE` at the start date | Declaration recorded with version and timestamp |
| Conflict declared, client accepts in writing | `ACTIVE`, `conflict_state = RESOLVED_ACCEPTED` | Resolution text, resolver, timestamp stored; show-level block lifted only for the named scope |
| Conflict declared, client declines | `DECLINED` | Auditor and firm notified; no data was readable |
| Conflict declared, no decision within 14 days | Remains `PENDING_ACCEPTANCE`, then `DECLINED` at window expiry | Escalated to Platform Security on day 7 |
| Conflict declared after activation | `SUSPENDED` immediately | Client decides; reactivation requires a fresh approval |

**The acceptance window.** Fourteen days from approval. After that the grant is `DECLINED` with reason `ACCEPTANCE_WINDOW_EXPIRED`, and the client must create a new grant. An open-ended acceptance window is a standing authorisation waiting to be picked up by whoever gains access to the mailbox.

**Acceptance is per grant, not per auditor.** An auditor with grants to three clients accepts each one separately. The NDA evidence may be shared when the version is identical, but the act of acceptance, the IP address and the timestamp are per grant. This is what makes "the auditor accepted access to our data on this date" a true statement.

### 8.5 Activation

Activation is the moment authority begins. It is a transaction, not a status field updated by a scheduled job.

| Step | Action | Failure |
|------|--------|---------|
| 1 | Re-read the grant `FOR UPDATE` | Concurrent cancellation wins; activation aborts |
| 2 | Verify state is `PENDING_ACCEPTANCE` and approval is intact | Abort |
| 3 | Verify all acceptance gates and the NDA version at this instant | Stay `PENDING_ACCEPTANCE` |
| 4 | Verify `start_at <= now`; if `start_at` is future, schedule activation and stop | No early access |
| 5 | Verify `end_at <= activated_at + 12 months` and `end_at > now` | Abort with `GRANT_WINDOW_INVALID` |
| 6 | Verify no other grant for this client and grantee is `ACTIVE` or `SUSPENDED` | Abort; renew instead of overlapping |
| 7 | Write the grant version snapshot | — |
| 8 | Set `state = ACTIVE`, `activated_at`, `scope_version` bump for the grantee | — |
| 9 | Emit `GrantActivated`; notify client owner, client administrators, grantee and firm administrator | — |
| 10 | Open the workspace route and mint the first grant-bound token on demand | — |

**Activation does not create a session.** The auditor still authenticates at the home organization with password and TOTP, then enters the workspace. A grant is authority, not a credential; treating activation as a login would create a session nobody authenticated.

**Start dates in the future.** A grant approved on 1 November with a 1 December start is `PENDING_ACCEPTANCE` until the auditor accepts, then waits in `PENDING_ACCEPTANCE` with `accepted_at` set until 1 December. The state does not become `ACTIVE` early, and the decision in section 6.6 checks `start_at` independently, so a clock or job error cannot open access early.

**Immediate start.** A start date up to 24 hours in the past is accepted, because time zones and approval delays make "now" a fuzzy boundary. Beyond 24 hours, the grant is rejected rather than backdated; a backdated grant would create access that appears to predate its approval.

### 8.6 Variation

A grant changes in one of three ways, and only one of them is silent.

| Change | Classification | Approval | Acceptance | Effect |
|--------|----------------|----------|------------|--------|
| Narrow the scope (remove a show or an assignment) | Reduction | None | None | Immediate; `GrantVariationApplied` |
| Remove a permission via `permissions_denied` | Reduction | None | None | Immediate |
| Shorten `end_at` | Reduction | None | None | Immediate |
| Widen the scope (add a show) | Extension | New approval by a different human | New acceptance | Effective only after both |
| Change profile to a broader one | Extension | New approval plus Finance Head when the target is `AUDIT_FINANCE` | New acceptance | Effective only after both |
| Extend `end_at` within the 12-month ceiling | Extension | New approval | New acceptance | Effective only after both |
| Change the owner or purpose text | Administrative | None | None | Audited, no authority change |
| Change the grantee | New grant | Full | Full | The old grant is revoked; a new grant is created |

**Reduction never needs the grantee's consent.** Reducing another party's access cannot harm them, and requiring consent would let an auditor block the removal of access the client no longer wants to give. The client can narrow or shorten at any time, including during an engagement.

**Extension is a new decision, not an edit.** The grant is versioned: `tenancy.access_grant_version` stores an immutable snapshot at each change with the actor, reason and timestamp. A reviewer can reconstruct what the grant permitted on any date, which is what an access dispute or a regulator's question requires.

**The 12-month ceiling applies to the grant, not to the relationship.** BR-AUD-002 caps expiry at twelve months after activation. A three-year engagement therefore produces three grants, each approved and accepted. Renewal is covered in section 8.10.

**Denied permissions are additive only.** A variation may add to `permissions_denied`; removing an entry is a widening and follows the extension path. This asymmetry prevents a quiet restoration of access that the client had deliberately withheld.

### 8.7 Suspension

Suspension is the reversible stop. It exists because the correct response to a question is not always permanent removal: a conflict under review, a firm investigating an auditor, or a client pausing an engagement all need access to stop now and possibly resume later.

| Trigger | Initiated by | Automatic | Resume requires |
|---------|--------------|-----------|-----------------|
| Client pauses the engagement | Client approver | No | Client approver, fresh approval |
| Conflict declared or discovered | System | Yes | Client decision recorded, fresh approval |
| NDA version superseded | System | Yes | Acceptance of the current NDA |
| Auditor deprovisioned by the firm | System, via identity cascade | Yes | Not resumable by the firm; the client must create a new grant for a new person |
| Auditor identity `SUSPENDED` or `LOCKED` | System | Yes | Identity restored, then client approval |
| Client tenant suspended | System | Yes | Tenant reactivated |
| Platform security action | Platform Security with ticket | No | Platform Security plus client approval |
| Suspicious access pattern | System, threshold-based | Yes | Security review, then client approval |

**Suspension revokes sessions immediately.** The grantee's workspace session is revoked, the `sid` is denylisted, `scope_version` is incremented, and any queued export or download job tied to the grant is cancelled. A suspended grant that leaves a session alive is not suspended.

**A suspended grant is still visible.** It appears in the register with its reason and the identity of the suspender. Hiding it would let a suspension look like a revocation, and the client needs to know the difference when it decides whether to resume.

**Resumption is not automatic.** When the triggering condition clears, the grant does not silently return to `ACTIVE`. A human with approve authority confirms, and the grant gets a fresh acceptance record. Automatic resumption would mean a conflict declaration that was cleared by a system job could restore access without anyone deciding it should.

**Firm suspension versus client revocation.** When a firm deprovisions an auditor, every grant to that auditor is suspended and every client is notified. The client then decides whether to revoke formally. The firm cannot delete the client's record of the access, and the client cannot be forced to keep it active. Both parties retain a veto, which is what independence requires.

### 8.8 Revocation and Expiry

| Aspect | Revocation | Expiry |
|--------|------------|--------|
| Trigger | Client decision, engagement end, policy breach, platform action | `end_at` reached |
| Actor | Client approver, or Platform Security with a ticket | System |
| Grantee consent | Not required and not sought | Not applicable |
| Effective | Under 60 seconds; committed 5 minutes (BR-AUD-003) | At `end_at`, enforced at decision time |
| Sessions | Revoked, `sid` denylisted, `scope_version` incremented | Same |
| Workspace | Closed; open tabs receive `401 STALE_TOKEN` on the next request | Same |
| Exports | Queued and running export jobs cancelled; completed downloads already delivered are not retrievable | Same |
| Devices | No device action; the auditor's device belongs to the firm | Same |
| Record | Retained seven years with reason, actor and timestamp | Retained seven years |
| Reversible | No | Only by a new grant |

**The cascade is deliberately broad.** Revocation does more than flip a status, because a status flip leaves live tokens, cached permissions, running exports and open browser tabs. The cascade is:

1. Grant state to `REVOKED` with `revoked_by`, `revoked_at`, `revoke_reason`.
2. Every session with `authority_id = grant_id` revoked and denylisted.
3. `scope_version` incremented for the grantee.
4. Permission cache entries for `(grantee, client_org)` invalidated.
5. Workspace route closed; in-flight requests fail with `401 STALE_TOKEN` and then `403 GRANT_INACTIVE`.
6. Export and report jobs referencing the grant cancelled.
7. `GrantRevoked` emitted at high severity, with the reason and the actor.
8. Notifications to the client owner, client administrators, grantee and firm administrator.
9. An access-review item opened to confirm the removal.

**Expiry is enforced twice.** The decision in section 6.6 compares `now` with `end_at` on every request, so an expired grant stops working even if the expiry job is late. The job exists to change the state, emit the event and notify, not to be the control. A system whose expiry depends on a cron job has an outage window equal to the job's delay.

**Revocation is not deletion.** The grant record, its versions, its acknowledgments and its access history remain for seven years. Deleting them would destroy the evidence that access existed, which is the first thing an incident review or a regulator asks for.

**Emergency platform revocation.** Platform Security can revoke any grant without client approval when there is evidence of compromise or a legal order. The client is notified within 15 minutes, the action is recorded with a ticket reference, and it is reviewed by the monthly security review. The platform can stop access; it cannot grant it, and it cannot restore a grant the client revoked.

### 8.9 Expiry Notification

FR-TNT-017 requires owners to be notified 14 days before expiry and requires a report of active grants.

| Stage | When | Recipients | Channel | Content |
|-------|------|-----------|---------|---------|
| T-14 | 14 days before `end_at` | Owner, client administrators, grantee, firm administrator | Email and in-app | Grant, scope, profile, expiry date, renewal link |
| T-7 | 7 days before | Owner, client administrators | Email and in-app | Reminder; renewal status |
| T-1 | 1 day before | Owner, client administrators, grantee | Email and in-app | Final notice; access ends tomorrow |
| T-0 | At expiry | Owner, grantee, firm administrator | In-app and audit | `GrantExpired`; access ended |
| T+1 | 1 day after | Owner | Email | Confirmation that access ended; renewal instructions |

**Delivery is recorded.** `tenancy.grant_expiry_notice` stores the stage, recipients, send time and delivery result. A notice that failed to deliver is retried and then escalated to the tenant's notification failure queue. "We sent an email" is not evidence that the owner was warned.

**Renewal link.** The T-14 notice includes a one-click path to create a renewal draft pre-filled with the existing profile and scope. It does not extend the grant; it opens a new approval. Pre-filling is a convenience, not a decision.

**The register is the report.** FR-TNT-017's report is the grant register, filtered and exportable:

| Column | Source |
|--------|--------|
| Grant ID and state | `access_grant` |
| Client organization | `client_org_id` |
| Grantee and firm | `grantee_user_id`, `firm_org_id` |
| Profile and denied permissions | `profile_key`, `permissions_denied` |
| Scope type and identifiers | `scope_type`, `scope_ids` |
| Owner | `owner_user_id` |
| Approved by, approved at | `approved_by`, `approved_at` |
| Start, end, days remaining | `start_at`, `end_at`, computed |
| Acceptance evidence | NDA version, COI state, MFA confirmed |
| Last access | `last_access_at` |
| Expiry notices sent | `grant_expiry_notice` |

The register exports as CSV under `audit.access_grant.export`, requires step-up when it exceeds 1,000 rows, and is included in the quarterly access review and the annual audit evidence pack.

**Dormant grants are a finding.** A grant that is `ACTIVE` with no access for 60 days is flagged to the owner. It is not revoked automatically, because a statutory audit may legitimately have quiet periods, but it appears on the register in amber and must be justified at the next access review.

### 8.10 Renewal and the Twelve-Month Ceiling

| Rule | Detail |
|------|--------|
| Maximum term | 12 months from activation (BR-AUD-002) |
| Renewal | A new grant that supersedes the old, with `supersedes_grant_id` |
| Renewal window | From 30 days before expiry to 30 days after; after that, a fresh grant with no history link |
| Approval | The same two-person rule; the approver cannot be the creator |
| Acceptance | Fresh acceptance record; MFA and NDA reused only when the evidence is current and the version unchanged; COI declared again |
| Continuity | The new grant activates at the old grant's `end_at`, or immediately when the old is revoked early and the new is already approved |
| Overlap | Zero. The partial unique index permits one `ACTIVE` or `SUSPENDED` grant per client and grantee |
| History | The chain `supersedes_grant_id` is visible on the register, so a three-year engagement reads as three grants, not one immortal grant |

**Why renew rather than extend.** An extension edit hides the fact that a human re-decided. A renewal makes the decision explicit every twelve months, resets the acceptance evidence, and produces a clean record for each audit period. The cost is one approval per year per grant; the benefit is that "access has been continuously justified" is true rather than assumed.

**Why not auto-renew.** Auto-renewal converts a time-bound grant into a standing entitlement with extra steps. The T-14 notice exists precisely to force the decision before expiry, and a lapsed grant that nobody renewed is the correct outcome when nobody is paying attention.

### 8.11 The Auditor Workspace

The workspace is the only surface where an external principal sees client data. It is a distinct route, a distinct token context and a distinct visual frame.

| Aspect | Design |
|--------|--------|
| Entry | `https://app.slateiq.com/workspace/{client_slug}` after explicit selection from the grant list |
| Visual frame | Persistent banner: client name, grant ID, profile, scope, expiry date, "External auditor" label |
| Token | New access token with `org = client_org_id`, `authority_id = grant_id`, 15-minute lifetime, no refresh token in the client context |
| Session | Same `sid` family, `authority_source = GRANT`; the home-organization session remains but cannot act on client data |
| Navigation | Only modules and routes permitted by the profile; no tenant administration, no identity, no settings beyond the auditor's own profile |
| Search | Scoped to the grant; no cross-tenant or cross-show results |
| Downloads | Watermarked PDF and CSV with auditor name, firm, grant ID and timestamp; each download audited |
| Uploads | Evidence only, written to the client's region under the client's `org_id` prefix |
| Exports | Bounded by the profile; step-up required above 1,000 rows; every export is a `GrantExportCompleted` event |
| Session end | Leaving the workspace closes the grant context; the token expires within 15 minutes; explicit exit is audited as `WorkspaceClosed` |
| Idle | The client's idle policy applies, not the firm's; a 15-minute client setting overrides a 60-minute firm setting |
| Device | The auditor's device registration belongs to the firm; the workspace does not create a second device |
| Notifications | The banner shows unread findings and assignments for this grant only |

**The banner is a control, not decoration.** Most cross-tenant incidents in multi-tenant products are a user acting in the wrong tenant. A permanent, unmissable label naming the client and the expiry is the cheapest defence against that class of error, and it is tested in the UX review.

**No mixed context.** The workspace never shows the auditor's own firm data beside client data. A combined view would require merging two tenants in one response, which is exactly the query the isolation model forbids. The auditor switches workspaces; they do not see two at once.

**Screenshots and local copies.** The platform cannot prevent a determined user from photographing a screen. It mitigates with watermarking, download audit, export limits, session recording at the metadata level and contractual terms in the NDA. The residual risk is stated in section 13 and accepted; claiming technical prevention would be false.

### 8.12 Cross-Region Grants

An audit firm may be homed in India while its client is homed in the EU. The grant must work without moving client data and without creating a second copy of the auditor's identity in the client region.

| Concern | Design |
|---------|--------|
| Data location | Client data is read and processed in the client's region only. No client record is replicated to the firm's region |
| Identity | The auditor authenticates in the home (firm) region. The client region receives a minimal projection, not a copy of the identity |
| Projection fields | `user_id` (pseudonym), display-name snapshot, firm name, `firm_org_id`, MFA-confirmed flag, NDA version, COI state, `grant_id` |
| Projection lifetime | Created at activation, refreshed on grant change, deleted 30 days after the grant terminates |
| Trust | The client region validates a short-lived signed assertion from the home region, audience-bound to the client region and to the grant; no shared session cookie spans regions |
| Audit | Grant lifecycle events are written in the client region; a metadata-only copy (`grant_id`, state, timestamps, actor pseudonym) is written in the firm region for the firm's own review |
| Session | A grant-bound session is created in the client region; the home-region session is not reused as an authorisation for client data |
| Revocation | The client region revokes locally; a revocation event propagates to the home region so the workspace disappears from the auditor's list |
| Support | Platform support follows the client's region; a support engineer in another region uses the same break-glass path, and the access is recorded in the client region |
| Backup | Backups stay in the client region; restoring a client backup in the firm region is technically prevented by key separation (section 7.6) |

**Why not replicate the identity.** Copying the auditor's email, phone and credential material into every client region would multiply the personal data, multiply the erasure paths, and create a stale copy that outlives the grant. The projection carries what the client needs to display and audit, and nothing that can authenticate.

**Cross-border transfer.** When the auditor's home region differs from the client's, the grant records `cross_region_acknowledged_at`, the transfer mechanism reference and the client's policy basis. The platform does not decide the lawfulness of the transfer; it records the client's decision and implements the technical boundary. A client whose policy forbids cross-border access sets `allow_cross_region_grants = false`, and the creation form rejects an auditor from another region with `CROSS_REGION_PROHIBITED`.

**Latency.** A cross-region workspace adds one assertion exchange at entry and then reads locally in the client region. It does not proxy every query through the home region, which would be slow and would make the firm's region a processing location for client data. The design keeps the data plane single-region even when the identity plane is not.

**Firm-region visibility.** The firm sees the grants its auditors hold, their state and their expiry. It does not see client data, client names beyond the grant record, or the auditor's activity detail. The firm's administrator needs enough to deprovision and to answer an independence question, and no more.

### 8.13 Independence and Platform-Appointed Auditors

BR-AUD-006 protects platform-appointed auditors from production-side pressure. The grant model implements it without a special case.

| Aspect | Rule |
|--------|------|
| Appointing party | The grant is created by the appointing OTT platform or broadcaster, not by the production house being audited |
| Revocation | Only the appointing party, its Organization Administrator, or Platform Security may revoke; the audited production house cannot revoke, because the appointment is not its to withdraw |
| Visibility | The production house sees that an auditor has access and to which scope; it does not see the appointment terms |
| Scope | The grant is scoped to the assignment, which names the appointing party |
| Dispute | A production house that believes the auditor is conflicted raises it with the appointing party and Platform Security; it cannot unilaterally remove access |
| Independence record | `auditor_profile.independence_declared_at` and the appointing party's reference are stored on the grant |

**Why the production house cannot revoke.** Allowing the audited party to remove its auditor converts the audit into a negotiation. The production house can see the access, can escalate a conflict, and can ask the appointing party to act; it cannot terminate the appointment. The restriction is narrow, documented in the UI, and itself audited.

**Independent practitioners.** An auditor with no firm is onboarded into a SlateIQ-operated `AUDIT_FIRM` tenant with a single seat (section 3.6). The grant then has a real firm to notify and a real administrator to deprovision, and the platform avoids the home-less principal that the archived draft created as `SYS_PLATFORM_AUDITOR`.

### 8.14 Grant Data Model

| Table | Key | Columns and constraints | Owner |
|-------|-----|-------------------------|-------|
| `tenancy.access_grant` | `grant_id` | `client_org_id`, `grantee_user_id`, `firm_org_id`, `owner_user_id`, `profile_key`, `permissions_denied text[]`, `scope_type`, `scope_ids uuid[]`, `start_at`, `end_at`, `state`, `created_by`, `approved_by`, `approved_at`, `accepted_at`, `activated_at`, `suspended_reason`, `revoked_by`, `revoked_at`, `revoke_reason`, `expires_notified_at`, `last_access_at`, `supersedes_grant_id`, `cross_region_acknowledged_at`, `version`, timestamps | MOD-01 |
| `tenancy.access_grant_version` | `grant_version_id` | `grant_id`, `version_no`, `snapshot jsonb`, `changed_by`, `changed_at`, `change_reason`; unique `(grant_id, version_no)` | MOD-01 |
| `tenancy.grant_acknowledgment` | `ack_id` | `grant_id`, `grant_version_no`, `nda_version`, `coi_declaration_id`, `mfa_factor_id`, `accepted_at`, `ip`, `user_agent_hash`; one per acceptance | MOD-01 |
| `tenancy.coi_declaration` | `declaration_id` | `user_id`, `grant_id`, `question_set_version`, `declared_conflict`, `details_ciphertext`, `declared_at`, `resolved_by`, `resolved_at`, `resolution`, `resolution_notes` | MOD-01 |
| `tenancy.grant_expiry_notice` | `notice_id` | `grant_id`, `stage`, `recipients`, `sent_at`, `delivery_result`; unique `(grant_id, stage)` | MOD-01 |
| `tenancy.auditor_profile` | `user_id` | `firm_org_id`, `engagement_types`, `independence_declared_at`, `coi_declared_at`, `nda_version`, `firm_attested_by`, `status` | MOD-01 |
| `tenancy.grant_projection` | `projection_id` | Cross-region minimal projection: `grant_id`, `client_region`, `user_id`, `display_name`, `firm_name`, `mfa_confirmed`, `nda_version`, `coi_state`, `expires_at` | MOD-01 |

**Constraints.**

| Constraint | Definition | Reason |
|------------|------------|--------|
| `grant_term_valid` | `end_at > start_at AND end_at <= start_at + interval '12 months'` | BR-AUD-002, checked at creation and again at activation |
| `grant_scope_present` | `scope_type IN ('SHOW','ASSIGNMENT') AND cardinality(scope_ids) > 0` | FR-TNT-015; no organization-wide external access |
| `grant_no_self` | `client_org_id <> firm_org_id` | A firm cannot audit itself through a grant; it uses its own membership |
| `grant_denied_subset` | `permissions_denied <@ profile_permissions(profile_key)` | A denial cannot name a permission outside the ceiling |
| `grant_one_active` | Partial unique index on `(client_org_id, grantee_user_id)` where `state IN ('ACTIVE','SUSPENDED','PENDING_ACCEPTANCE')` | Prevents overlapping authority and ambiguous revocation |
| `grant_approved_distinct` | `approved_by <> created_by` | SOD-07 |
| `grant_state_trigger` | Transition trigger on `state` | The state machine cannot be bypassed by a direct update |
| `grant_firm_match` | `firm_org_id = auditor_profile(user_id).firm_org_id` | The client cannot name a firm the auditor does not belong to |
| `grant_scope_owned` | Scope identifiers belong to `client_org_id` | An assignment-scoped grant cannot point at another tenant's show |

**Indexes.** `(client_org_id, state, end_at)` for the register and expiry job; `(grantee_user_id, state)` for the workspace list; `(firm_org_id, state)` for the firm view; `(end_at)` partial where `state = 'ACTIVE'` for the notice job.

**Retention.** Grants and their versions are retained seven years after the terminal state. `coi_declaration.details_ciphertext` is encrypted with the grantee's data key and is crypto-shredded on identity erasure while the declaration record, the answer and the resolution survive as pseudonymous evidence. A conflict declaration that vanishes when the auditor leaves is not evidence.

### 8.15 Grant Events

| Event | Severity | Key fields | Consumers |
|-------|----------|-----------|-----------|
| `GrantDrafted` | Info | `grant_id`, `client_org_id`, `grantee_user_id` | Register |
| `GrantDraftDiscarded` | Info | `grant_id`, actor | Hygiene reporting |
| `GrantSubmitted` | Info | `grant_id`, creator, approver candidates | Approval queue |
| `GrantApproved` | High | `grant_id`, approver, profile, scope, dates | Audit, owner notification |
| `GrantDeclined` | Info | `grant_id`, actor, reason code | Creator notification |
| `GrantAccepted` | High | `grant_id`, NDA version, COI state, IP class | BR-AUD-004 evidence |
| `GrantActivated` | High | `grant_id`, `activated_at`, `end_at` | Workspace, notices, audit |
| `GrantVariationApplied` | High | `grant_id`, version, change class, before and after | Audit, access review |
| `GrantSuspended` | High | `grant_id`, reason code, initiator | Session revocation, client notification |
| `GrantResumed` | High | `grant_id`, approver, cleared condition | Workspace, audit |
| `GrantRevoked` | Critical | `grant_id`, initiator, reason, cascade result | Incident review, client and firm notification |
| `GrantExpired` | High | `grant_id`, `end_at` | Register, notification |
| `GrantExpiryNoticeSent` | Info | `grant_id`, stage, recipients, delivery result | FR-TNT-017 evidence |
| `GrantRenewalCreated` | Info | `new_grant_id`, `supersedes_grant_id` | Register chain |
| `GrantWorkspaceEntered` | Info | `grant_id`, `session_id`, region | Cross-tenant monitoring |
| `GrantWorkspaceClosed` | Info | `grant_id`, `session_id`, duration | Session analytics |
| `GrantCrossRegionAcknowledged` | High | `grant_id`, regions, mechanism reference | DPO evidence |
| `GrantExportCompleted` | High | `grant_id`, resource type, row count, watermark ID | Data-loss monitoring |
| `GrantAccessDenied` | Warning | `grant_id`, permission, reason code | Threat detection |
| `EmergencyGrantRevocation` | Critical | `grant_id`, ticket, platform actor | Monthly security review |

**Correlation.** Creation, approval, acceptance, activation and the first workspace entry share one `correlation_id`, so the full grant can be reconstructed from a single identifier (NFR-AUD-004).

### 8.16 Open Questions for Section 8

| ID | Question | Current position | Needed by |
|----|----------|------------------|-----------|
| OQ-4-02 | May an audit firm supervisor hold a read-only grant to a client to review working papers? | No at MVP. Supervision happens through the firm's own quality review process, outside the client tenant. A supervisor grant would double the external principals per engagement for a benefit that is not yet evidenced | Pilot feedback |
| OQ-4-03 | Should grant access history be exportable by the client as a signed evidence pack? | Yes, Phase 1. At MVP the register and the audit events are exportable as CSV; a signed, hash-chained pack is deferred | Phase 1 design |
| OQ-4-04 | What is the maximum number of concurrent grants a single auditor may hold? | Twenty. Above that, the workspace list becomes unusable and the auditor is almost certainly holding stale grants; the limit is a soft warning, not a hard block | Pilot feedback |

### 8.17 Traceability for Section 8

| Requirement | Where satisfied | Evidence |
|-------------|-----------------|----------|
| FR-TNT-015 | Sections 8.2, 8.3, 8.14 | Mandatory profile, scope and dates; creation cannot proceed without them; `grant_term_valid` and `grant_scope_present` constraints |
| FR-TNT-016 | Sections 8.4, 8.5, 8.8 | MFA, NDA and COI gates; client approval; revocation cascade under 60 seconds against a 5-minute commitment |
| FR-TNT-017 | Sections 8.9, 8.14 | Register with grant, scope, owner and expiry; T-14, T-7, T-1 notices with delivery records; expiry enforced at decision time |
| FR-TNT-010 | Sections 8.6, 8.14 | Grant versioning; changes apply within 60 seconds through `scope_version` |
| NFR-SEC-003 | Section 8.4 | MFA mandatory for every external auditor before activation |
| NFR-AUD-001 | Section 8.15 | Full event catalogue for the grant lifecycle |
| NFR-AUD-004 | Section 8.15 | Correlation ID across creation, approval, acceptance and use |
| NFR-PRV-004 | Section 8.12 | Client-region processing; minimal identity projection; no client data in the firm region |
| NFR-PRV-009 | Sections 8.12, 7.6 | Regional keys and no cross-region restore |
| BR-AUD-001 | Sections 8.2, 8.3 | Mandatory start, end and scope; no open-ended grant |
| BR-AUD-002 | Sections 8.3, 8.10, 8.14 | One client, one grantee, explicit scope, 12-month ceiling enforced by constraint and activation check |
| BR-AUD-003 | Sections 8.7, 8.8 | Revocation and expiry invalidate sessions and refresh tokens |
| BR-AUD-004 | Sections 8.3, 8.4 | Activation gate requires MFA, NDA, COI and client approval |
| BR-AUD-005 | Section 8.4 | Declared conflict blocks the show until resolved in writing |
| BR-AUD-006 | Section 8.13 | Appointing party alone may revoke; production side cannot |
| BR-AUD-007 | Sections 8.6, 8.10 | Handover is a revocation plus a renewal with a `supersedes_grant_id` chain, timestamped and audited |
| CAP-002 | Sections 8.1 to 8.15 | External identity and access capability |
| CAP-003 | Sections 8.3, 8.7 | Role and grant authorisation capability |

---
