# Document 9: API Architecture & Endpoint Catalogue

**Product:** SlateIQ — Production Cost Intelligence & Audit Monitoring Platform
**Document ID:** SLQ-ARCH-009
**Version:** 1.0.0
**Classification:** Internal — Architecture Foundation
**Status:** Draft — For Review
**Date:** 2026-11-06
**Authors:** Principal Software Architect, API Lead, Security Architect
**Reviewers:** Principal Architect (TBD), Frontend Lead (TBD), Mobile Lead (TBD), Integration Lead (TBD)

---

## Document Control

### Revision History

| Version | Date | Author | Change |
|---------|------|--------|--------|
| 1.0.0 | 2026-11-06 | Architecture Team | First issue. Fixes API conventions, the error-code catalogue, the endpoint catalogue for every business module, representative JSON contracts and the Phase 1 webhook contract. Supersedes Document 6 Appendix A as the canonical surface (Document 6 Appendix A states that this document owns it) |

### Related Documents

| Document | Relationship |
|----------|--------------|
| Document 1 — Product Vision & Business Requirements | FR, NFR and BR identifiers cited here |
| Document 2 — Domain Model & Capability Map | Resource names follow entity names |
| Document 3 — Actor, Permission, Tenancy & Identity | Binding conventions (§11.2), request pipeline (§11.3), identity, tenancy and grant endpoints (§11.5–§11.12), rate limits (§11.13), idempotency (§11.14), versioning (§11.15), permission catalogue (Appendix A) |
| Document 4 — Core Business Workflows & State Machines | States named in request and response bodies |
| Document 6 — High-Level Solution Architecture | API layer (§7.4), sync protocol (§9), integrations (§10), Appendix A surface |
| Document 7 — Module & Dependency Architecture | Module ownership of every route; asynchronous processing behind `202` responses |
| Document 10 — Security Architecture | Security controls referenced by the conventions |
| Document 11 — Mobile & Offline Architecture | Client behaviour for sync endpoints |
| Document 12 — Excel Integration Architecture | Import and export semantics behind section 4.9 |
| Document 16 — Testing Strategy | API test obligations |

### Conventions

| Convention | Meaning |
|------------|---------|
| `ERR-nnn` | Error catalogue entry (section 3) |
| Idem column | `K` = `Idempotency-Key` required; `C` = client-generated `id` accepted; `M` = `If-Match` required; `—` = none |
| Scope column | Scope types at which the permission is evaluated (Document 3 §5.4); `PLATFORM` as in Document 3 §11.12 |
| Paths | Relative to `/api/v1` unless shown otherwise |
| P1, P2 | Route reserved for Phase 1 or Phase 2; returns `404` until released |

---

## 1. Purpose and Scope

This document is the canonical API contract description for SlateIQ. The machine-readable source of truth is the OpenAPI 3.1 document in `contracts/openapi/` (section 2.18); this document explains its conventions, catalogues every route with its authority, and gives representative payloads that the generated schemas must accept.

| In scope | Out of scope (owner) |
|----------|----------------------|
| Conventions for every route | Identity, tenancy and grant route details (Document 3 §11, referenced, not restated) |
| Error-code catalogue | Event payloads (Document 14; Document 6 Appendix B; Document 7 §4.16) |
| Endpoint catalogue for MOD-03 to MOD-15 | Field-level schemas (generated from `contracts/openapi/`) |
| Representative JSON contracts | Excel template layouts (Document 12) |
| Phase 1 signed webhooks | Mobile client internals (Document 11) |

Governing brief §15 requires a versioned REST API with consistent naming, authentication, authorization, validation, response structure, pagination, sorting, filtering, error handling, idempotency, rate limiting, correlation IDs and documentation, with machine-readable errors that never expose SQL, stack traces, credentials or infrastructure details. Section 2 covers each of these.

---

## 2. Conventions

### 2.1 Base Path, Hosts and Media Types

| Rule | Detail |
|------|--------|
| Base path | `/api/v1`; health at `/health/live` and `/health/ready` outside the version prefix (Document 3 §11.2) |
| Hosts | One regional hostname per data region (ADR-009). The load balancer routes `/api/v1/sync/*` to the Sync host and everything else to the API host (Document 7 §9). A client never chooses a region by header |
| Media type | `application/json; charset=utf-8` for requests and responses; `application/problem+json` for errors; `text/event-stream` for the notification stream |
| Files | Never through the API. Evidence, workbooks and outputs move through presigned object-storage URLs issued by the API (ADR-006) |
| Payload limit | JSON bodies ≤1 MB; sync push ≤512 KB (Document 6 §7.4, §9.3); larger returns `413 PAYLOAD_TOO_LARGE` |
| Timeouts | Handler budget 10 s; work that may exceed it returns `202` (section 2.16) |
| Resource naming | Plural kebab-case nouns (`/budget-versions`); identifiers in the path; nesting at most one level below a parent when the child cannot exist without it (`/expenses/{expenseId}/comments`) |
| Actions | State transitions are `POST` to a verb sub-resource (`/submit`, `/approve`, `/cancel`); `PATCH` never changes lifecycle state |

### 2.2 Versioning and Deprecation

Document 3 §11.15 is binding and summarised here.

| Rule | Detail |
|------|--------|
| Major version in path | `/api/v1`; a breaking change creates `/api/v2` |
| Non-breaking | New optional request fields, new response fields, new enum values in responses, new endpoints, new error codes |
| Breaking | Removing or renaming a field, changing a type, tightening validation, changing a default, changing or reusing an error code |
| Deprecation | At least six months' notice (NFR-MNT-001); `Deprecation` and `Sunset` response headers plus `Link: <…>; rel="successor-version"`; usage telemetry per client before removal |
| Client tolerance | Clients ignore unknown response fields and tolerate unknown enum values |
| Mobile floor | The sync handshake returns `min_supported_version`; an older app receives `426 APP_VERSION_UNSUPPORTED` on sync routes and must upgrade (Document 6 §9.3) |

### 2.3 Authentication: Web Session versus Mobile Bearer

Access tokens are always presented as `Authorization: Bearer`. What differs is how the client keeps and renews them.

| Client | Access credential | Renewal | CSRF exposure | Binding |
|--------|-------------------|---------|---------------|---------|
| Web SPA (web session) | 15-minute access token held in memory only | Refresh token in an `HttpOnly`, `Secure`, `SameSite=Lax` cookie scoped to `Path=/api/v1/auth`, sent only to `/auth/refresh` and `/auth/logout` | Cookie-authenticated routes require the double-submit header `X-CSRF-Token`; no resource route accepts a cookie, so there is no ambient authority to forge | Session `sid`; idle and absolute limits server-side (Document 3 §4.6) |
| Mobile app | 15-minute access token with a `device_id` claim | Refresh token in the platform keystore, sent in the request body with a device-key signature (`/auth/device/verify`) | None (no cookies) | Device-bound; offline grant 7 days (NFR-MOB-004); the Sync host rejects tokens without `device_id` |
| Auditor workspace | Token minted by `POST /workspace/{clientSlug}/enter` with `org` = client and `authority_source = GRANT` | No client refresh token; re-enter from the home session | As web | Grant-bound (Document 3 §11.11) |
| Service account (P1) | OAuth 2.0 client-credentials bearer token | Re-issue | None | Scoped to named permissions; `identity.service_account.*` |

Authentication endpoints are specified in Document 3 §11.5 and are not repeated here.

### 2.4 Tenant Context (FR-TNT-018)

| Rule | Detail |
|------|--------|
| Source | The access token `org` claim is the only source of tenant context |
| `X-Org-Id` | Optional consistency assertion; must equal the claim or the request fails with `400 TENANT_CONTEXT_MISMATCH` |
| Path `{orgId}` | Must equal the claim; a mismatch returns `404` (Document 3 §11.3 step 7) |
| Body fields | No request body field selects a tenant. A body that contains `org_id` is rejected with `422 VALIDATION_FAILED` (field `org_id`, code `FIELD_NOT_ALLOWED`) |
| Cross-tenant objects | An identifier from another tenant returns `404 RESOURCE_NOT_FOUND`, indistinguishable from a missing object (Document 3 §5.11) |
| Switching | Members have one organization at MVP; auditors switch through the workspace endpoints, one workspace at a time |

### 2.5 Authorization Declaration

Every operation in the OpenAPI document carries vendor extensions that the server enforces and the route-inventory test checks (Document 3 §11.16):

| Extension | Example | Meaning |
|-----------|---------|---------|
| `x-permission` | `expense.expense.approve` | Key from Document 3 Appendix A, or `Authenticated` for `SELF` routes |
| `x-scopes` | `[SHOW, DEPARTMENT, ASSIGNMENT]` | Scope types at which the target is evaluated |
| `x-step-up` | `true` | Fresh MFA assertion required (Document 3 Appendix A Step-up column) |
| `x-idempotency` | `required` | `Idempotency-Key` mandatory |
| `x-owner` | `MOD-07` | Owning module (Document 7) |
| `x-release` | `MVP`, `P1`, `P2` | Release gate |

Approval keys are always evaluated with approval limits and SoD vetoes (Document 3 Appendix A.7 note 2). A route that names several keys requires all of them.

### 2.6 Request Validation

| Stage | Check | Failure |
|-------|-------|---------|
| 1 | Pipeline steps 1–10 of Document 3 §11.3 (authentication, tenant, authority, permission, scope) run before the body is deserialised | `401`, `403`, `404` |
| 2 | Content type, size, JSON syntax | `415`, `413`, `400 MALFORMED_JSON` |
| 3 | Schema validation against the generated contract: required fields, types, formats, enum membership, string lengths, decimal-string format; unknown request fields rejected | `422 VALIDATION_FAILED` with `errors[]` |
| 4 | Edge validators (FluentValidation): cross-field rules that need no data (date ranges, split percentages summing to 100) | `422 VALIDATION_FAILED` |
| 5 | Domain validation inside the transaction: references, state, period lock, business rules | `409` or `422` with a domain code (section 3) |

Validation messages name the problem and the next action (NFR-UX-008) and never echo authority data or another tenant's existence.

### 2.7 Wire Format

| Element | Rule (ADR-040) |
|---------|----------------|
| Property names | `snake_case` in bodies and query parameters, matching Document 6 §9 and the event envelope. ProblemDetails extension members keep the names fixed by Document 3 §11.2 (`code`, `traceId`, `errors`) |
| Identifiers | UUID strings; UUIDv7 for client-generated IDs |
| Money | Object `{ "amount": "1234.5000", "currency": "INR" }`; `amount` is a decimal string with exactly four fraction digits, matching `numeric(19,4)`; never a JSON number (Authoring Brief §2) |
| Rates and percentages | Decimal strings: FX rates six fraction digits (`"84.125000"`), percentages four (`"18.0000"`) |
| Dates | `YYYY-MM-DD` business dates in the organization timezone; timestamps RFC 3339 UTC with `Z` |
| Enums | `UPPER_SNAKE_CASE`, values exactly as Document 4 and Document 2 name them (`SUBMITTED`, `TAX_INVOICE`) |
| Nulls | Absent and `null` are equivalent on read; on `PATCH`, `null` clears an optional field and absence leaves it unchanged (JSON Merge Patch semantics) |
| Versions | Every mutable resource exposes `version` (integer) and returns it as `ETag` |

### 2.8 Response Shapes

| Situation | Status | Body |
|-----------|--------|------|
| Read one | `200` | Resource object |
| Create | `201` | Resource object; `Location` and `ETag` headers |
| Action (state transition) | `200` | Updated resource, including any resources the transition created (for example the approval instance) |
| Accepted long work | `202` | Operation resource (section 2.16); `Location` header |
| Delete or discard | `204` | Empty |
| Collection | `200` | `{ "items": [ … ], "page": { "next_cursor": "…", "has_more": true, "limit": 50 } }` |
| Error | `4xx`, `5xx` | `application/problem+json` (section 2.11) |

Every resource object contains `id`, `version` (if mutable), `created_at`, `updated_at` and a `links` object with `self` and the permitted action links. Action links are advisory, like the permission projection (Document 3 §5.13).

### 2.9 Keyset Pagination

| Rule | Detail |
|------|--------|
| Parameters | `limit` (default 50, maximum 200; above returns `422 LIMIT_EXCEEDED`, never a silent clamp) and `cursor` |
| Cursor | Opaque base64url string encoding the last row's sort values, the tie-breaker `id`, a hash of the filters and sort, and a format version, signed with a server key. Clients never construct or parse it |
| Binding | A cursor used with different filters or sort returns `422 CURSOR_INVALID`; a cursor older than 24 hours returns `410 CURSOR_EXPIRED` and the client restarts |
| Stability | Sort always ends with `id`; new rows inserted ahead of the cursor do not shift pages |
| Totals | Not returned for collections that may exceed 10,000 rows (Document 3 §11.2); bounded collections may return `page.total` |
| Direction | Forward only; "previous" is a client-side history of cursors |

### 2.10 Sorting and Filtering Grammar

| Construct | Syntax | Example |
|-----------|--------|---------|
| Sort | `sort=<field>[,<field>]`, prefix `-` for descending, at most three fields from the collection's allow-list; `id` appended automatically | `sort=-expense_date,expense_number` |
| Equality | `<field>=<value>` | `status=SUBMITTED` |
| Set membership | `<field>=<v1>,<v2>` (at most 50 values) | `status=SUBMITTED,REQUIRES_INFO` |
| Range | `<field>_from`, `<field>_to` (inclusive) for dates and timestamps | `expense_date_from=2026-10-01&expense_date_to=2026-10-31` |
| Amount bounds | `<field>_min`, `<field>_max` as decimal strings in base currency | `base_amount_min=50000.0000` |
| Text | `q=<text>` (2 to 100 characters) on collections that declare it; PostgreSQL full-text and trigram search (Document 6 §7.8) | `q=generator` |
| Boolean | `true` or `false` | `has_exceptions=true` |

Unknown parameters return `422 FILTER_UNKNOWN`; unknown sort fields `422 SORT_UNKNOWN`. Filtering on a field the caller may not read returns `403`, not an empty list (Document 3 §11.14). Every allow-listed filter and sort combination is backed by an index (Document 8).

### 2.11 Errors (RFC 9457)

```json
{
  "type": "https://errors.slateiq.app/period-locked",
  "title": "Financial period is locked",
  "status": 409,
  "code": "PERIOD_LOCKED",
  "detail": "The expense date 2026-09-28 falls in the locked period September 2026. Change the date or ask a Finance Head to reopen the period.",
  "instance": "/api/v1/expenses/0193b2d4-5c1e-7a40-9f11-2b7c6e9d0a51/submit",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "errors": [
    { "field": "/expense_date", "code": "PERIOD_LOCKED", "message": "Choose a date in an open period." }
  ]
}
```

| Rule | Detail |
|------|--------|
| Stable `code` | Clients branch on `code`, never on `title` or `detail`; codes are part of the contract (Document 3 §11.15) |
| `type` | A stable URI per code; it documents the code and is not dereferenced at runtime |
| `errors[]` | Field errors with a JSON Pointer `field`, a code and a human message; row and cell references for imports use `row` and `cell` instead of `field` |
| Never exposed | SQL, stack traces, exception types, internal hostnames, credentials, other tenants' identifiers, permission internals |
| Retryability | `Retry-After` on `429`, `503` and `409 IDEMPOTENCY_IN_PROGRESS`; section 3 marks retryable codes |
| Localisation | `title`, `detail` and `message` localised by `Accept-Language`; `code` never localised |

### 2.12 Idempotency and Client-Generated Identifiers

Two mechanisms work together.

**`Idempotency-Key` header** protects against HTTP retries. The server stores, in `ops.idempotency_key`, the hash of method, path, actor and canonical body, the status `IN_PROGRESS` or `COMPLETED`, and the response. A replay with the same key and hash returns the stored response with `Idempotent-Replay: true`; a different hash returns `409 IDEMPOTENCY_KEY_REUSED`; a replay while the first request is still running returns `409 IDEMPOTENCY_IN_PROGRESS` with `Retry-After`. Keys are client-generated (UUIDv7 recommended, at most 64 characters) and retained 7 days unless Document 3 §11.14 sets 24 hours.

The header is required on every route marked `K` in section 4, which includes at least: creation of expenses, commitments, vendors, budget versions, variations, audit reports, findings, imports, exports, report runs, evidence upload tickets, legal holds, DSR requests and webhook endpoints; every approval, rejection, submission, cancellation and amendment; bulk approval; and every route listed in Document 3 §11.14.

**Client-generated `id`** protects against duplicate creation across devices and offline retries. Routes marked `C` accept an `id` (UUIDv7) in the body. The server validates the version and that the embedded timestamp is not more than 7 days in the past or 5 minutes in the future (aligned with the offline window, D-015). If a resource with that `id` exists in the tenant, was created by the same principal and the canonical payload matches, the server returns `200` with the existing resource and `Idempotent-Replay: true`; otherwise `409 RESOURCE_ID_CONFLICT`. Sync mutations use `mutation_id` in the same way (Document 6 §9.3).

### 2.13 Optimistic Concurrency (ETag and If-Match)

| Rule | Detail |
|------|--------|
| ETag | Strong ETag `"v{version}"` from the aggregate's `version` column on every mutable resource |
| If-Match | Required on `PATCH`, `PUT`, `DELETE` and every state-transition `POST` (routes marked `M`). Missing returns `428 PRECONDITION_REQUIRED`; stale returns `409 CONCURRENCY_STALE` with the current `ETag` (Document 3 §11.14 deliberately uses `409`, not `412`) |
| Child edits | Editing a child of an aggregate (a budget line, an expense line) uses the parent's ETag, because the invariant (totals) lives on the parent |
| Bulk | Bulk approval carries a per-item `if_match` value in the body and returns a per-item result |
| Reads | `If-None-Match` supported on reference data and dashboards; `304` when unchanged |

### 2.14 Rate Limits

Limits from Document 3 §11.13 apply unchanged. Business routes add the classes below. Limits are enforced at the edge and in the service (Redis counters) and return `429 RATE_LIMITED` with `Retry-After`, `RateLimit-Policy` and `RateLimit` headers.

| Endpoint class | Limit | Window | Key |
|----------------|-------|--------|-----|
| Expense writes (create, update, submit) | 120 | 1 minute | User |
| Approval decisions (single) | 600 | 1 hour | User |
| Bulk approval | 20 requests, 100 items each | 1 hour | User |
| Evidence upload tickets | 300 | 1 hour | User |
| Sync push | 12 | 1 minute | Device |
| Sync pull | 30 | 1 minute | Device |
| Sync handshake | 1 | 5 minutes | Device (Document 6 §9.3) |
| Import create | 20 | 1 hour | User |
| Import commit | 10 | 1 hour | User |
| Export | 10 | 1 hour | User (Document 3 §11.13) |
| Report run | 30 | 1 hour | User |
| Dashboard and variance reads | 120 | 1 minute | User |
| Text search (`q`) | 60 | 1 minute | User |
| Tenant aggregate, API host | 3,000 | 1 minute | Organization |
| Webhook test send (P1) | 10 | 1 hour | Endpoint |

### 2.15 Correlation and Tracing

| Element | Rule |
|---------|------|
| `traceparent` | Accepted and propagated (W3C Trace Context); generated when absent; returned on every response |
| Correlation ID | The server assigns `correlation_id` per request and returns it as `X-Correlation-Id`; it appears in audit events, outbox events and delivery attempts (FR-CMP-006, NFR-AUD-004) |
| `traceId` | Present in every error body (Document 3 §11.2) |
| `X-Request-Id` | Optional client value (≤64 characters) logged for support; never used for idempotency or authorization |
| Sync | Each mutation result carries the `correlation_id` under which it was applied |

### 2.16 Long-Running Operations

| Rule | Detail |
|------|--------|
| Trigger | Any work that may exceed 10 s or depends on a queue: exports, report runs, import parse, validate and commit, grant revocation cascade, DSR bundles, evidence packs |
| Response | `202 Accepted`, `Location` pointing at the resource's own status endpoint (`/excel/exports/{id}`, `/report-runs/{id}`) or `/jobs/{jobId}` for generic work; `Retry-After` as a polling hint |
| Status body | `status` (resource states such as `QUEUED`, `RUNNING`, `COMPLETED`, `FAILED`, `EXPIRED`, or SM-19 for jobs), `progress { processed, total }`, `result` with links, `error` as a ProblemDetails object |
| Completion signal | In-app notification and the SSE stream; polling is the fallback |
| Cancellation | `POST …/cancel` where the resource supports it; idempotent |

### 2.17 Caching and Security Headers

Authenticated responses are `Cache-Control: no-store`; reference data (currencies, countries, tax jurisdictions, permission catalogue) may be cached with a versioned `ETag`. Security headers follow Document 3 §11.2: HSTS, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, no `Server` banner, restrictive CSP on the SPA origin. CORS allows only the tenant SPA origins of the region; credentials mode is used only for `/api/v1/auth/*`.

### 2.18 Contract-First OpenAPI 3.1

The OpenAPI document is written first and code is generated from it (ADR-041).

```
contracts/
  openapi/
    slateiq-v1.yaml                 root document, info, servers, security schemes, tags
    paths/<module>/*.yaml           one file per resource family, owned by the module team
    components/
      schemas/common/               Money, Problem, Page, OperationStatus, Links, FxSnapshot
      schemas/<module>/             resource and request schemas
      parameters/  responses/  headers/
    errors/error-codes.yaml         section 3 catalogue; source of the error constants
  webhooks/slateiq-webhooks-v1.yaml OpenAPI 3.1 `webhooks` object (section 6)
  events/                           JSON Schema per event (Document 14)
  templates/                        Excel template definitions (Document 12)
```

| Stage | Gate |
|-------|------|
| Lint | Ruleset enforcing `snake_case`, `x-permission`, `x-scopes`, `x-owner`, `x-release` on every operation; `401`, `403`, `404`, `422`, `429` responses declared; error responses reference `Problem`; money fields reference `Money` |
| Permission check | Every `x-permission` value exists in the Document 3 Appendix A manifest |
| Breaking-change diff | Against the last released contract tag; a breaking change fails unless the major version changes (NFR-MNT-001) |
| Code generation | .NET request and response DTOs and route constants into each module's `Api/Generated` folder; TypeScript clients for `web/` and `mobile/` |
| Conformance | Route inventory test: the set of mapped routes equals the set of operations; integration tests validate every response against its schema |
| Publication | Rendered reference and changelog published with each release (NFR-MNT-002) |

---

## 3. Error-Code Catalogue

Codes are stable (ADR-042). Codes defined by Document 3 §11.4 keep their meaning and are listed by reference with an ERR identifier so the catalogue is complete. `R` marks codes a client may retry unchanged.

### 3.1 Pipeline, Identity and Access (Document 3 §11.4)

| ID | HTTP | Code | R |
|----|------|------|---|
| ERR-001 | 400 | `TENANT_CONTEXT_MISMATCH` | |
| ERR-002 | 401 | `TOKEN_INVALID` | |
| ERR-003 | 401 | `TOKEN_EXPIRED` | Refresh, then retry |
| ERR-004 | 401 | `SESSION_REVOKED` | |
| ERR-005 | 401 | `SESSION_EXPIRED` | |
| ERR-006 | 401 | `INVALID_CREDENTIALS` | |
| ERR-007 | 401 | `MFA_INVALID` | |
| ERR-008 | 403 | `PERMISSION_DENIED` | |
| ERR-009 | 403 | `SCOPE_DENIED` | |
| ERR-010 | 403 | `MEMBERSHIP_INACTIVE` | |
| ERR-011 | 403 | `GRANT_INACTIVE` | |
| ERR-012 | 403 | `GRANT_EXPIRED` | |
| ERR-013 | 403 | `TENANT_SUSPENDED` | |
| ERR-014 | 403 | `TENANT_ARCHIVED` | |
| ERR-015 | 403 | `STEP_UP_REQUIRED` | After step-up |
| ERR-016 | 403 | `SOD_CONFLICT` | |
| ERR-017 | 403 | `SELF_APPROVAL_PROHIBITED` | |
| ERR-018 | 403 | `APPROVAL_LIMIT_EXCEEDED` | |
| ERR-019 | 403 | `MODULE_NOT_ENTITLED` | |
| ERR-020 | 404 | `RESOURCE_NOT_FOUND` | |
| ERR-021 | 409 | `CONCURRENCY_STALE` | After re-read |
| ERR-022 | 409 | `IDEMPOTENCY_KEY_REUSED` | |
| ERR-023 | 409 | `LEGAL_HOLD_ACTIVE` | |
| ERR-024 | 422 | `VALIDATION_FAILED` | |
| ERR-025 | 423 | `ACCOUNT_LOCKED` | After lock expiry |
| ERR-026 | 429 | `RATE_LIMITED` | Yes, after `Retry-After` |
| ERR-027 | 500 | `CONTEXT_UNSET` | |
| ERR-028 | 503 | `DEPENDENCY_UNAVAILABLE` | Yes |

Grant, invitation, role, membership and workspace codes of Document 3 §11.4–§11.11 (`GRANT_OVERLAP`, `GRANT_NOT_APPROVABLE`, `ROLE_SYSTEM_IMMUTABLE`, `ROLE_IN_USE`, `MULTI_ORG_NOT_SUPPORTED`, `INVITATION_INVALID`, `RESET_TOKEN_INVALID`, `CHALLENGE_EXPIRED`, `PASSWORD_POLICY_VIOLATION`, `GRANT_TERM_INVALID`, `GRANT_SCOPE_INVALID`, `CROSS_REGION_PROHIBITED`, `ISOLATION_PROBE_FAILED`, `LAST_ADMIN`, `WORKSPACE_ALREADY_OPEN` and the others named there) are registered as ERR-029 to ERR-059 in `contracts/openapi/errors/error-codes.yaml` in the order Document 3 introduces them.

### 3.2 Protocol, Query and Concurrency

| ID | HTTP | Code | Meaning | R |
|----|------|------|---------|---|
| ERR-060 | 400 | `MALFORMED_JSON` | Body is not valid JSON | |
| ERR-061 | 413 | `PAYLOAD_TOO_LARGE` | Body above the route limit | |
| ERR-062 | 415 | `UNSUPPORTED_MEDIA_TYPE` | Not `application/json` | |
| ERR-063 | 422 | `FILTER_UNKNOWN` | Unknown query parameter | |
| ERR-064 | 422 | `SORT_UNKNOWN` | Sort field not allow-listed | |
| ERR-065 | 422 | `LIMIT_EXCEEDED` | `limit` above 200 | |
| ERR-066 | 422 | `CURSOR_INVALID` | Cursor tampered with or used with other filters | |
| ERR-067 | 410 | `CURSOR_EXPIRED` | Cursor older than 24 hours, or sync cursor older than retention | Restart |
| ERR-068 | 428 | `PRECONDITION_REQUIRED` | `If-Match` missing | |
| ERR-069 | 409 | `IDEMPOTENCY_IN_PROGRESS` | Same key still executing | Yes |
| ERR-070 | 409 | `RESOURCE_ID_CONFLICT` | Client `id` already used for different content or principal | |
| ERR-071 | 409 | `INVALID_STATE_TRANSITION` | Action not allowed from the current state (Document 4) | |
| ERR-072 | 426 | `APP_VERSION_UNSUPPORTED` | Mobile app below `min_supported_version` | |
| ERR-073 | 503 | `READ_ONLY_MODE` | Degraded mode: writes queued or refused (NFR-AVL-004) | Yes |

### 3.3 Domain Codes

| ID | HTTP | Code | Rule | Module |
|----|------|------|------|--------|
| ERR-080 | 409 | `PERIOD_LOCKED` | BR-BUD-009, FR-EXP-029 | MOD-04, MOD-07 |
| ERR-081 | 422 | `FUTURE_DATE_NOT_ALLOWED` | BR-EXP-002 | MOD-07 |
| ERR-082 | 422 | `SHOW_NOT_OPEN_FOR_CAPTURE` | BR-PRD-002 | MOD-07 |
| ERR-083 | 422 | `CATEGORY_INACTIVE` | BR-EXP-015 | MOD-07 |
| ERR-084 | 422 | `CATEGORY_NOT_MAPPED` | FR-EXP-007 | MOD-07 |
| ERR-085 | 422 | `VENDOR_BLOCKED` | BR-EXP-013 | MOD-07 |
| ERR-086 | 422 | `SPLIT_INVALID` | BR-EXP-012 | MOD-07 |
| ERR-087 | 422 | `TOTAL_MISMATCH` | BR-FIN-011 | MOD-07, MOD-04 |
| ERR-088 | 422 | `NEGATIVE_AMOUNT_NOT_ALLOWED` | BR-FIN-008 | MOD-07 |
| ERR-089 | 422 | `EVIDENCE_REQUIRED` | BR-EXP-001 | MOD-07 |
| ERR-090 | 409 | `EVIDENCE_NOT_AVAILABLE` | Evidence still `PENDING_SCAN` or quarantined | MOD-07 |
| ERR-091 | 409 | `EVIDENCE_DISPUTED` | UC-07: disputed evidence blocks final approval | MOD-07 |
| ERR-092 | 422 | `SUBMISSION_LIMIT_EXCEEDED` | BR-EXP-014 | MOD-07 |
| ERR-093 | 422 | `OUT_OF_ASSIGNMENT_SCOPE` | BR-EXP-004 | MOD-07 |
| ERR-094 | 422 | `FX_RATE_UNAVAILABLE` | No rate for the pair (Document 7 §6.6) | MOD-04 |
| ERR-095 | 422 | `APPROVAL_CHAIN_UNRESOLVED` | No active workflow for the subject | MOD-04 |
| ERR-096 | 409 | `APPROVAL_STEP_MISMATCH` | Decision names a step that is not active | All approval subjects |
| ERR-097 | 422 | `COMMENT_REQUIRED` | FR-EXP-015, FR-BUD-006 | All approval subjects |
| ERR-098 | 409 | `AMENDMENT_NOT_ALLOWED` | BR-EXP-009 state rules | MOD-07 |
| ERR-099 | 409 | `EVIDENCE_LINK_LOCKED` | BR-DOC-007 | MOD-07 |
| ERR-100 | 409 | `EXCEPTION_ALREADY_OPEN` | One open exception per rule and dedup key | MOD-07 |
| ERR-101 | 422 | `BUDGET_LINE_DUPLICATE` | BR-BUD-002 | MOD-04 |
| ERR-102 | 422 | `BUDGET_INCOMPLETE` | FR-BUD-003 | MOD-04 |
| ERR-103 | 422 | `ALLOCATION_EXCEEDS_SEASON` | BR-BUD-007 | MOD-04 |
| ERR-104 | 422 | `CONTINGENCY_DIRECT_USE` | BR-BUD-004 | MOD-04 |
| ERR-105 | 422 | `REVISION_DETAILS_REQUIRED` | BR-BUD-005 | MOD-04 |
| ERR-106 | 403 | `SOLE_AUTHOR_APPROVAL` | BR-BUD-012 | MOD-04 |
| ERR-107 | 409 | `PERIOD_CLOSE_BLOCKED` | Close checklist items open | MOD-04 |
| ERR-108 | 409 | `VENDOR_DUPLICATE` | BR-VND-001 | MOD-05 |
| ERR-109 | 409 | `COMMITMENT_NOT_OPEN` | BR-VND-003 | MOD-05 |
| ERR-110 | 409 | `CLOSURE_BLOCKED` | BR-PRD-001 | MOD-03 |
| ERR-111 | 422 | `HIERARCHY_INVALID` | BR-PRD-006 | MOD-03 |
| ERR-112 | 422 | `ASSIGNMENT_EXCEEDS_GRANT` | Assignment beyond its grant (Document 6 §5.3) | MOD-06 |
| ERR-113 | 403 | `APPOINTING_PARTY_ONLY` | BR-AUD-006 | MOD-06 |
| ERR-114 | 409 | `AUDIT_REPORT_EXISTS` | One accepted report per assignment, type and date | MOD-06 |
| ERR-115 | 409 | `ASSIGNMENT_NOT_ACTIVE` | Work outside an `ACTIVE` assignment | MOD-06, MOD-07 |
| ERR-116 | 422 | `UPLOAD_REJECTED` | Type, macro or archive rules (FR-DOC-017) | MOD-09, MOD-10 |
| ERR-117 | 422 | `HASH_MISMATCH` | SHA-256 differs from the ticket | MOD-09 |
| ERR-118 | 410 | `UPLOAD_TICKET_EXPIRED` | Ticket older than 15 minutes | MOD-09 |
| ERR-119 | 409 | `DOCUMENT_NOT_CLEAN` | Download before a clean scan (BR-DOC-005) | MOD-09 |
| ERR-120 | 422 | `QUOTA_EXCEEDED` | FR-DOC-017 storage quota | MOD-09 |
| ERR-121 | 422 | `TEMPLATE_VERSION_MISMATCH` | BR-XLS-001 | MOD-10 |
| ERR-122 | 409 | `DUPLICATE_FILE_CONFIRMATION_REQUIRED` | BR-XLS-003 | MOD-10 |
| ERR-123 | 422 | `ROW_LIMIT_EXCEEDED` | More than 50,000 rows | MOD-10 |
| ERR-124 | 409 | `IMPORT_ERROR_THRESHOLD` | BR-XLS-004 | MOD-10 |
| ERR-125 | 409 | `PARTIAL_COMMIT_DISABLED` | BR-XLS-004 | MOD-10 |
| ERR-126 | 409 | `BATCH_NOT_COMMITTABLE` | Batch not `VALIDATED` | MOD-10 |
| ERR-127 | 409 | `EXPORT_NOT_READY` | Download before `COMPLETED`, or after `EXPIRED` | MOD-10, MOD-13 |
| ERR-128 | 422 | `SYNC_SEQ_GAP` | Gap in `device_seq_range`; handshake required | MOD-14 |
| ERR-129 | 409 | `SYNC_RANGE_REPLAYED` | Range overlaps a processed batch with different content | MOD-14 |
| ERR-130 | 403 | `DEVICE_NOT_ACTIVE` | Device revoked or wiped | MOD-14 |
| ERR-131 | 409 | `SCOPE_VERSION_STALE` | Device must refresh scope before pushing | MOD-14 |
| ERR-132 | 409 | `OFFLINE_OPERATION_NOT_ALLOWED` | Operation is online-only (Document 6 §9.4) | MOD-14 |
| ERR-133 | 422 | `WEBHOOK_URL_REJECTED` | Non-HTTPS, private or link-local target (P1) | MOD-11 |

---

## 4. Endpoint Catalogue

### 4.1 Identity, Tenancy, Grants and Cross-Cutting Routes

Identity, membership, role, assignment, grant, workspace, access-review, delegation, approval-limit, SoD and platform tenant routes are specified in Document 3 §11.5 to §11.12, including permission, scope and denial per route. They are part of this contract unchanged and are not restated. Routes added here:

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| GET | `/me/navigation` | Authenticated | SELF | — | Advisory menu projection (Document 3 §5.13) |
| GET | `/approval-tasks` | Authenticated | SELF | — | Caller's pending approval steps across all subjects (FR-EXP-014); filters `subject_type`, `show_id`, `due_before` |
| GET | `/reference/currencies` | Authenticated | — | — | Global reference data; versioned `ETag` |
| GET | `/reference/countries` | Authenticated | — | — | As above |
| GET | `/reference/tax-jurisdictions` | Authenticated | — | — | As above |
| GET | `/jobs/{jobId}` | Authenticated (requester) or `ops.job.read` | SELF or PLATFORM | — | Generic operation status |
| GET | `/health/live`, `/health/ready` | Public | — | — | Outside `/api/v1`; no tenant data |

### 4.2 Production (MOD-03)

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| GET | `/shows` | `production.show.read` | ORG, SHOW | — | Filters `status`, `type`, `q`; only shows in scope (FR-PRD-015) |
| POST | `/shows` | `production.show.create` | ORG | K, C | Creates `PLANNING`; seasons for a film created implicitly |
| GET | `/shows/{showId}` | `production.show.read` | SHOW | — | Counts of seasons, episodes; hosting pattern |
| PATCH | `/shows/{showId}` | `production.show.update` | SHOW | M | Descriptive fields; change history kept (FR-PRD-002) |
| POST | `/shows/{showId}/activate` | `production.show.update` | SHOW | M | `PLANNING` to `ACTIVE` |
| POST | `/shows/{showId}/complete` | `production.show.update` | SHOW | M | `ACTIVE` to `COMPLETED` |
| POST | `/shows/{showId}/cancel` | `production.show.delete` | ORG, SHOW | M | Only `PLANNING` without financial records |
| POST | `/shows/{showId}/closures` | `production.show.submit` | SHOW | K | Creates ShowClosure `REQUESTED`, runs the checklist; returns `BLOCKED` or `READY` with items |
| GET | `/show-closures/{closureId}` | `production.show.read` | SHOW | — | Checklist, waivers, approval state |
| POST | `/show-closures/{closureId}/approve` | `production.show.approve` | SHOW | K, M | Step-up; show `CLOSED` on final step |
| POST | `/show-closures/{closureId}/reject` | `production.show.approve` | SHOW | K, M | Comment required |
| POST | `/show-closures/{closureId}/withdraw` | `production.show.submit` | SHOW | M | Requester only |
| POST | `/shows/{showId}/archive` | `production.show.approve` | ORG | M | `CLOSED` to `ARCHIVED` |
| GET | `/shows/{showId}/seasons` | `production.season.read` | SHOW | — | |
| POST | `/shows/{showId}/seasons` | `production.season.create` | SHOW | K, C | |
| PATCH | `/seasons/{seasonId}` | `production.season.update` | SHOW, SEASON | M | |
| GET | `/seasons/{seasonId}/episodes` | `production.episode.read` | SHOW, SEASON | — | Filter `status` |
| POST | `/seasons/{seasonId}/episodes` | `production.episode.create` | SHOW, SEASON | K, C | |
| PATCH | `/episodes/{episodeId}` | `production.episode.update` | SHOW, SEASON, EPISODE | M | Status per Document 2 §6.3 |
| GET | `/departments` | `production.department.read` | ORG, SHOW, DEPARTMENT | — | Tree; filter `show_id`, `active` |
| POST | `/departments` | `production.department.create` | ORG, SHOW | K, C | Depth ≤3 (BR-PRD-006) |
| PATCH | `/departments/{departmentId}` | `production.department.update` | ORG, SHOW, DEPARTMENT | M | Includes deactivation; never deleted (BR-PRD-003) |
| GET | `/categories` | `production.category.read` | ORG | — | Filter `active`, `department_id`, `cost_type` |
| POST | `/categories` | `production.category.create` | ORG | K, C | Leaf categories are postable |
| PATCH | `/categories/{categoryId}` | `production.category.update` | ORG | M | Rename or deactivate without altering history (FR-PRD-010) |
| PUT | `/categories/{categoryId}/departments` | `production.category.update` | ORG | M | Replace the department mapping (FR-PRD-009) |
| GET | `/parties` | `production.party.read` | ORG, SHOW | — | |
| POST | `/parties` | `production.party.create` | ORG | K, C | |
| PATCH | `/parties/{partyId}` | `production.party.update` | ORG | M | |
| PUT | `/shows/{showId}/parties` | `production.party.update` | SHOW | M | Show party roles (FR-PRD-004) |
| GET | `/party-links` | `production.party_link.read` | ORG | — | Visible to both linked organizations |
| POST | `/party-links` | `production.party_link.create` | ORG | K | `PROPOSED` |
| POST | `/party-links/{linkId}/accept` | `production.party_link.approve` | ORG | M | Both organizations must accept |
| POST | `/party-links/{linkId}/terminate` | `production.party_link.delete` | ORG | M | `TERMINATED` |
| GET, POST | `/shows/{showId}/comments` | `production.comment.read`, `production.comment.create` | SHOW | C (POST) | Non-financial |

### 4.3 Budget (MOD-04)

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| GET | `/shows/{showId}/budgets` | `budget.budget.read` | SHOW, SEASON | — | One budget per season |
| POST | `/shows/{showId}/budgets` | `budget.budget.create` | SHOW | K | Usually created automatically from `ShowCreated` |
| GET | `/budgets/{budgetId}` | `budget.budget.read` | SHOW, SEASON | — | SM-03 state, active version |
| GET | `/budgets/{budgetId}/versions` | `budget.budget_version.read` | SHOW, SEASON | — | History |
| POST | `/budgets/{budgetId}/versions` | `budget.budget_version.create` | SHOW, SEASON | K, C | Draft `ORIGINAL` or `REVISION` from a base version; revision requires reason and effective date (BR-BUD-005) |
| GET | `/budget-versions/{versionId}` | `budget.budget_version.read` | SHOW, SEASON | — | Totals, status, approval summary |
| PATCH | `/budget-versions/{versionId}` | `budget.budget_version.update` | SHOW, SEASON | M | Draft only |
| GET | `/budget-versions/{versionId}/lines` | `budget.budget_line.read` | SHOW, SEASON, DEPARTMENT | — | Filters `department_id`, `category_id`, `line_type` |
| POST | `/budget-versions/{versionId}/lines` | `budget.budget_version.update` | SHOW, SEASON | M, C | Parent ETag; unique key (BR-BUD-002) |
| PATCH | `/budget-versions/{versionId}/lines/{lineId}` | `budget.budget_version.update` | SHOW, SEASON | M | Draft only |
| DELETE | `/budget-versions/{versionId}/lines/{lineId}` | `budget.budget_version.update` | SHOW, SEASON | M | Draft only |
| GET | `/budget-versions/{versionId}/episode-allocations` | `budget.episode_allocation.read` | SEASON, EPISODE | — | |
| PUT | `/budget-versions/{versionId}/episode-allocations` | `budget.episode_allocation.update` | SEASON | M | Ceiling rule (BR-BUD-007) |
| POST | `/budget-versions/{versionId}/submit` | `budget.budget_version.submit` | SHOW, SEASON | K, M | Section 5.4 |
| POST | `/budget-versions/{versionId}/approve` | `budget.budget_version.approve` | SHOW, SEASON | K, M | Step-up; limit and SoD (BR-BUD-012) |
| POST | `/budget-versions/{versionId}/request-changes` | `budget.budget_version.reject` | SHOW, SEASON | K, M | `CHANGES_REQUESTED`; comment required |
| POST | `/budget-versions/{versionId}/reject` | `budget.budget_version.reject` | SHOW, SEASON | K, M | `REJECTED`; comment required (FR-BUD-006) |
| POST | `/budget-versions/{versionId}/withdraw` | `budget.budget_version.submit` | SHOW, SEASON | M | Submitter only; `WITHDRAWN` |
| GET | `/budget-versions/{versionId}/approval` | `budget.budget_version.read` | SHOW, SEASON | — | Resolved chain and decisions |
| POST | `/budget-versions/{versionId}/exports` | `budget.budget_version.export` | SHOW, SEASON | K | `202`; XLSX or PDF (FR-BUD-016) |
| GET | `/budgets/{budgetId}/variations` | `budget.variation.read` | SHOW, SEASON | — | |
| POST | `/budgets/{budgetId}/variations` | `budget.variation.create` | SHOW, SEASON | K, C | Within threshold applies directly (BR-BUD-006) |
| POST | `/variations/{variationId}/approve` | `budget.variation.approve` | SHOW, SEASON | K, M | Step-up |
| POST | `/variations/{variationId}/reject` | `budget.variation.reject` | SHOW, SEASON | K, M | |
| GET | `/financial-periods` | `budget.financial_period.read` | ORG, SHOW | — | Filters `status`, `date_from`, `date_to` |
| POST | `/financial-periods/{periodId}/close` | `budget.financial_period.update` | ORG, SHOW | K, M | Step-up; `CLOSING`, runs close checks; `LOCKED` when clear |
| GET | `/financial-periods/{periodId}/close-runs/{runId}` | `budget.financial_period.read` | ORG, SHOW | — | Checklist and snapshot reference |
| POST | `/financial-periods/{periodId}/close-runs/{runId}/waivers` | `budget.financial_period.approve` | ORG, SHOW | K | Waive a blocker with reason |
| POST | `/financial-periods/{periodId}/reopen-requests` | `budget.financial_period.update` | ORG, SHOW | K, M | Reason required |
| POST | `/financial-periods/{periodId}/reopen-requests/{requestId}/approve` | `budget.financial_period.approve` | ORG, SHOW | K | Dual approval; requester excluded |
| GET | `/approval-workflows` | `budget.approval_workflow.read` | ORG, SHOW | — | Filter `subject_type` |
| POST | `/approval-workflows` | `budget.approval_workflow.administer` | ORG, SHOW | K | New draft workflow version (FR-BUD-018) |
| PUT | `/approval-workflows/{workflowId}` | `budget.approval_workflow.administer` | ORG, SHOW | M | Draft only; activation creates a new version; never retroactive (BR-EXP-007) |
| GET | `/fx-rates` | `budget.financial_period.read` | ORG | — | Filters `base`, `quote`, `rate_date_from`, `rate_date_to`, `source` |
| POST | `/fx-rates/overrides` | `budget.financial_period.update` | ORG | K | Step-up; reason and validity window |
| GET, PUT | `/budget-versions/{versionId}/forecasts` | `budget.forecast.read`, `budget.forecast.update` | SHOW, SEASON, DEPARTMENT | M (PUT) | **P1** (FR-CIN-003) |
| GET, POST | `/budget-versions/{versionId}/comments` | `budget.comment.read`, `budget.comment.create` | SHOW, SEASON | C (POST) | FR-BUD-015 |

### 4.4 Vendor & Commitments (MOD-05)

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| GET | `/vendors` | `vendor.vendor.read` | ORG, SHOW | — | Filters `status`, `risk_rating`, `tax_id`, `q` (FR-VND-007) |
| POST | `/vendors` | `vendor.vendor.create` | ORG | K, C | `409 VENDOR_DUPLICATE` returns the existing vendor reference only if visible |
| GET | `/vendors/{vendorId}` | `vendor.vendor.read` | ORG, SHOW | — | Bank and tax identifiers masked (BR-VND-007) |
| PATCH | `/vendors/{vendorId}` | `vendor.vendor.update` | ORG | M | Master data |
| POST | `/vendors/{vendorId}/activate` | `vendor.vendor.approve` | ORG | M | `CANDIDATE` to `ACTIVE`; step-up |
| POST | `/vendors/{vendorId}/reject` | `vendor.vendor.approve` | ORG | M | `CANDIDATE` to `REJECTED` |
| POST | `/vendors/{vendorId}/block` | `vendor.vendor.approve` | ORG | M | Reason and effective date |
| POST | `/vendors/{vendorId}/unblock` | `vendor.vendor.approve` | ORG | M | |
| POST | `/vendors/{vendorId}/deactivate` | `vendor.vendor.update` | ORG | M | |
| POST | `/vendors/{vendorId}/merge` | `vendor.vendor.approve` | ORG | K, M | **P1** (FR-VND-019, BR-VND-006) |
| GET | `/vendors/{vendorId}/duplicates` | `vendor.vendor.read` | ORG | — | Candidates by tax ID, name, bank reference (FR-VND-006) |
| PUT | `/vendors/{vendorId}/tax-registrations` | `vendor.vendor.update` | ORG | M | Format validation per scheme |
| GET, POST | `/vendors/{vendorId}/contacts` | `vendor.contact.read`, `vendor.contact.create` | ORG | C (POST) | |
| PATCH | `/vendor-contacts/{contactId}` | `vendor.contact.update` | ORG | M | |
| GET | `/vendors/{vendorId}/bank-references` | `vendor.payment_term.read` | ORG | — | Masked unless the caller also holds `vendor.payment_term.update` |
| POST | `/vendors/{vendorId}/bank-references` | `vendor.payment_term.update` | ORG | K | Step-up; supersedes the active reference |
| PUT | `/vendors/{vendorId}/risk-rating` | `vendor.risk_rating.update` | ORG | M | Step-up (FR-VND-005) |
| GET, POST | `/vendors/{vendorId}/documents` | `document.document.read`, `document.document.create` | ORG | K (POST) | Links an uploaded document (FR-VND-020 is P1 for contracts) |
| GET | `/vendors/{vendorId}/spend-summary` | `vendor.vendor.read` | ORG, SHOW | — | Figures limited to shows in the caller's scope (FR-VND-008) |
| GET | `/commitments` | `vendor.commitment.read` | SHOW, DEPARTMENT | — | Filters `status`, `show_id`, `vendor_id`, `type` |
| POST | `/commitments` | `vendor.commitment.create` | SHOW, DEPARTMENT | K, C | `DRAFT` |
| GET | `/commitments/{commitmentId}` | `vendor.commitment.read` | SHOW, DEPARTMENT | — | Includes consumption from Cost Intelligence |
| PATCH | `/commitments/{commitmentId}` | `vendor.commitment.update` | SHOW, DEPARTMENT | M | Draft only |
| POST | `/commitments/{commitmentId}/submit` | `vendor.commitment.submit` | SHOW, DEPARTMENT | K, M | Locks FX snapshot; resolves chain |
| POST | `/commitments/{commitmentId}/approve` | `vendor.commitment.approve` | SHOW, DEPARTMENT | K, M | Step-up; becomes `OPEN` |
| POST | `/commitments/{commitmentId}/reject` | `vendor.commitment.reject` | SHOW, DEPARTMENT | K, M | Comment required |
| POST | `/commitments/{commitmentId}/amendments` | `vendor.commitment.update` | SHOW, DEPARTMENT | K, M | Reason required (BR-VND-004) |
| POST | `/commitment-amendments/{amendmentId}/approve` | `vendor.commitment.approve` | SHOW, DEPARTMENT | K | |
| POST | `/commitment-amendments/{amendmentId}/reject` | `vendor.commitment.reject` | SHOW, DEPARTMENT | K | |
| POST | `/commitments/{commitmentId}/close` | `vendor.commitment.update` | SHOW, DEPARTMENT | K, M | `CLOSED` |
| POST | `/commitments/{commitmentId}/cancel` | `vendor.commitment.update` | SHOW, DEPARTMENT | K, M | `CANCELLED` with reason |
| GET, POST | `/purchase-orders` | `vendor.purchase_order.read`, `vendor.purchase_order.create` | SHOW, DEPARTMENT | K, C (POST) | **P1** |
| POST | `/purchase-orders/{poId}/submit`, `/approve`, `/reject`, `/acknowledge`, `/receipts`, `/close` | `vendor.purchase_order.submit`, `.approve`, `.reject`, `.update` (acknowledge, receipts, close) | SHOW, DEPARTMENT | K, M | **P1** (FR-VND-011 to FR-VND-015) |
| GET | `/payment-references` | `vendor.payment_reference.read` | ORG, SHOW | — | Reconciliation status filter |
| POST | `/payment-references` | `vendor.payment_reference.create` | ORG, SHOW | K, C | No payment execution (FR-VND-016) |
| PATCH | `/payment-references/{referenceId}` | `vendor.payment_reference.update` | ORG, SHOW | M | |

### 4.5 Audit Operations (MOD-06)

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| GET | `/audit-teams` | `audit.audit_team.read` | ORG, SHOW | — | |
| POST | `/audit-teams` | `audit.audit_team.create` | ORG, SHOW | K, C | Exactly one lead |
| PATCH | `/audit-teams/{teamId}` | `audit.audit_team.update` | ORG, SHOW | M | Membership changes included |
| GET | `/audit-assignments` | `audit.audit_assignment.read` | ORG, SHOW, ASSIGNMENT | — | Filters `auditor_user_id`, `show_id`, `status`, `active_on` |
| POST | `/audit-assignments` | `audit.audit_assignment.create` | ORG, SHOW | K, C | `PLANNED`; start and end mandatory (BR-AUD-001); overlap returns a warning list (FR-AUD-006) |
| GET | `/audit-assignments/{assignmentId}` | `audit.audit_assignment.read` | ORG, SHOW, ASSIGNMENT | — | Scope, grant link, predecessor |
| PATCH | `/audit-assignments/{assignmentId}` | `audit.audit_assignment.update` | ORG, SHOW | M | Narrowing immediate; widening needs approval |
| POST | `/audit-assignments/{assignmentId}/activate` | `audit.audit_assignment.approve` | ORG, SHOW | M | Step-up; `ASSIGNMENT_EXCEEDS_GRANT` checked |
| POST | `/audit-assignments/{assignmentId}/suspend` | `audit.audit_assignment.update` | ORG, SHOW | M | |
| POST | `/audit-assignments/{assignmentId}/resume` | `audit.audit_assignment.approve` | ORG, SHOW | M | |
| POST | `/audit-assignments/{assignmentId}/complete` | `audit.audit_assignment.update` | ORG, SHOW | M | |
| POST | `/audit-assignments/{assignmentId}/revoke` | `audit.audit_assignment.delete` | ORG, SHOW | K, M | Step-up; appointing party only (BR-AUD-006) |
| POST | `/audit-assignments/{assignmentId}/handover` | `audit.audit_assignment.update` | ORG, SHOW | K, M | Successor assignment; predecessor `HANDED_OVER` (BR-AUD-007) |
| GET | `/auditors` | `audit.auditor_work_profile.read` | ORG | — | Profiles, workload, capacity (FR-AUD-012) |
| GET, PATCH | `/auditors/{userId}/work-profile` | `audit.auditor_work_profile.read`, `audit.auditor_work_profile.update` | ORG, SELF | M (PATCH) | FR-AUD-005 |
| GET, POST | `/coverage-requirements` | `audit.audit_plan.read`, `audit.audit_plan.create` | ORG, SHOW | K (POST) | |
| PATCH | `/coverage-requirements/{requirementId}` | `audit.audit_plan.update` | ORG, SHOW | M | |
| GET | `/audit-coverage` | `audit.audit_plan.read` | ORG, SHOW | — | Coverage and timeliness by show and period (FR-AUD-013) |
| GET | `/audit-reports` | `audit.audit_report.read` | SHOW, ASSIGNMENT | — | Filters `assignment_id`, `report_type`, `status`, `report_date_from`, `report_date_to` |
| POST | `/audit-assignments/{assignmentId}/audit-reports` | `audit.audit_report.create` | SHOW, ASSIGNMENT | K, C | `DRAFT` |
| GET | `/audit-reports/{reportId}` | `audit.audit_report.read` | SHOW, ASSIGNMENT | — | |
| PATCH | `/audit-reports/{reportId}` | `audit.audit_report.update` | SHOW, ASSIGNMENT | M | `DRAFT` or `RETURNED` only |
| POST | `/audit-reports/{reportId}/submit` | `audit.audit_report.submit` | SHOW, ASSIGNMENT | K, M | Section 5.10 |
| POST | `/audit-reports/{reportId}/accept` | `audit.audit_report.approve` | SHOW | K, M | Accepted reports are immutable |
| POST | `/audit-reports/{reportId}/return` | `audit.audit_report.reject` | SHOW | K, M | Comment required |
| POST | `/audit-reports/{reportId}/revisions` | `audit.audit_report.create` | SHOW, ASSIGNMENT | K | New version of an accepted report; the old one becomes `SUPERSEDED` on acceptance |
| POST | `/audit-reports/{reportId}/exports` | `audit.audit_report.export` | SHOW, ASSIGNMENT | K | `202`; PDF |
| GET | `/audit-findings` | `audit.audit_finding.read` | SHOW, ASSIGNMENT | — | Visibility filter enforced (BR-AUD-006) |
| POST | `/audit-findings` | `audit.audit_finding.create` | SHOW, ASSIGNMENT | K, C | `DRAFT`; linked records validated |
| PATCH | `/audit-findings/{findingId}` | `audit.audit_finding.update` | SHOW, ASSIGNMENT | M | |
| POST | `/audit-findings/{findingId}/open` | `audit.audit_finding.submit` | SHOW, ASSIGNMENT | M | `DRAFT` to `OPEN` |
| POST | `/audit-findings/{findingId}/responses` | `audit.finding_response.create` | SHOW | K, C | Production response; `RESPONDED` |
| POST | `/audit-findings/{findingId}/return-response` | `audit.audit_finding.reject` | SHOW, ASSIGNMENT | M | Back to `OPEN` |
| POST | `/audit-findings/{findingId}/close` | `audit.audit_finding.approve` | SHOW, ASSIGNMENT | M | Reason required |
| POST | `/audit-findings/{findingId}/escalate` | `audit.audit_finding.update` | SHOW, ASSIGNMENT | M | `ESCALATED` |
| POST | `/audit-findings/{findingId}/withdraw` | `audit.audit_finding.update` | SHOW, ASSIGNMENT | M | `WITHDRAWN` |
| GET, POST | `/audit-assignments/{assignmentId}/working-papers` | `audit.audit_evidence.read`, `audit.audit_evidence.create` | SHOW, ASSIGNMENT | K (POST) | Links uploaded documents |

### 4.6 Expense (MOD-07)

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| GET | `/expenses` | `expense.expense.read` | SHOW, SEASON, EPISODE, DEPARTMENT, ASSIGNMENT, SELF | — | Filters `show_id`, `department_id`, `vendor_id`, `category_id`, `status`, `expense_date_from`, `expense_date_to`, `base_amount_min`, `base_amount_max`, `has_exceptions`, `source`, `q` (FR-EXP-020) |
| POST | `/expenses` | `expense.expense.create` | SHOW, DEPARTMENT, ASSIGNMENT | K, C | `DRAFT`; section 5.1 |
| GET | `/expenses/{expenseId}` | `expense.expense.read` | As list | — | Lines, splits, evidence, exceptions, approval (FR-EXP-021) |
| PATCH | `/expenses/{expenseId}` | `expense.expense.update` | As create | M | `DRAFT` or `REQUIRES_INFO`; lines replaced as a whole array |
| DELETE | `/expenses/{expenseId}` | `expense.expense.delete` | As create | M | Discard a `DRAFT` |
| POST | `/expenses/{expenseId}/submit` | `expense.expense.submit` | As create | K, M | Section 5.2 |
| POST | `/expenses/{expenseId}/approve` | `expense.expense.approve` | SHOW, SEASON, EPISODE, DEPARTMENT | K, M | Step-up; section 5.3 |
| POST | `/expenses/{expenseId}/reject` | `expense.expense.reject` | As approve | K, M | Step-up; reason code and comment |
| POST | `/expenses/{expenseId}/request-info` | `expense.expense.reject` | As approve | K, M | `REQUIRES_INFO`; comment required (FR-EXP-015) |
| POST | `/expenses/{expenseId}/resubmit` | `expense.expense.submit` | As create | K, M | From `REQUIRES_INFO`; chain kept unless a financial field changed (BR-EXP-009) |
| POST | `/expenses/bulk-approve` | `expense.expense.approve` | As approve | K | Up to 100 items with per-item `if_match`; per-item results (FR-EXP-022) |
| POST | `/expenses/{expenseId}/amendments` | `expense.expense.update` | As create | K, M | Online only; creates a new `DRAFT` revision with the same `root_expense_id`; reason required |
| POST | `/expenses/{expenseId}/cancel` | `expense.expense.delete` | As create | K, M | Online only; reversal postings when approved (BR-EXP-010) |
| GET | `/expenses/{expenseId}/history` | `expense.expense.read` | As list | — | Status, decisions, amendments |
| GET | `/expenses/{expenseId}/revisions` | `expense.expense.read` | As list | — | Revision chain by `root_expense_id` |
| GET | `/expenses/{expenseId}/approval` | `expense.expense.read` | As list | — | Resolved chain and decisions |
| GET | `/expenses/{expenseId}/evidence` | `expense.expense.read` and `document.document.read` | As list | — | Links with scan and verification state |
| POST | `/expenses/{expenseId}/evidence` | `expense.expense.update` and `document.document.create` | As create | K, C | Links an uploaded document; allowed after approval with `REVIEW_REQUIRED` |
| DELETE | `/expenses/{expenseId}/evidence/{evidenceLinkId}` | `expense.expense.update` | As create | M | Refused on approved records (BR-DOC-007) |
| POST | `/evidence-links/{evidenceLinkId}/verify` | `document.evidence.approve` | SHOW, DEPARTMENT, ASSIGNMENT | M | `VERIFIED` |
| POST | `/evidence-links/{evidenceLinkId}/dispute` | `document.evidence.reject` | SHOW, DEPARTMENT, ASSIGNMENT | M | `DISPUTED`; reason required (FR-DOC-007) |
| GET | `/exceptions` | `expense.exception.read` | SHOW, DEPARTMENT, ASSIGNMENT | — | Filters `type`, `severity`, `status`, `owner_user_id` |
| POST | `/exceptions` | `expense.exception.create` | SHOW, DEPARTMENT, ASSIGNMENT | K, C | Reviewer-raised exception |
| POST | `/exceptions/{exceptionId}/acknowledge` | `expense.exception.update` | As list | M | |
| POST | `/exceptions/{exceptionId}/resolve` | `expense.exception.update` | As list | M | Justification required |
| POST | `/exceptions/{exceptionId}/false-positive` | `expense.exception.update` | As list | M | |
| POST | `/exceptions/{exceptionId}/reopen` | `expense.exception.update` | As list | M | Returns to `OPEN` |
| POST | `/exceptions/{exceptionId}/waiver-requests` | `expense.exception.update` | As list | K | Starts `EXCEPTION_WAIVER` approval |
| POST | `/exceptions/{exceptionId}/waive` | `expense.exception.approve` | As list | K, M | Step-up |
| GET, POST | `/expenses/{expenseId}/comments` | `expense.comment.read`, `expense.comment.create` | SHOW, DEPARTMENT, ASSIGNMENT | C (POST) | FR-EXP-027 |

### 4.7 Cost Intelligence (MOD-08)

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| GET | `/dashboards` | `report.dashboard.read` | ORG, SHOW, DEPARTMENT, ASSIGNMENT | — | Catalogue filtered by permission (DSH-01 to DSH-07) |
| GET | `/dashboards/{dashboardKey}` | `report.dashboard.read` plus the measure key (`intel.portfolio_view.read` for the portfolio) | Per dashboard | — | Section 5.9; `as_of` and freshness tier (FR-CIN-020) |
| GET | `/portfolio` | `intel.portfolio_view.read` | ORG | — | DSH-01 (FR-CIN-011) |
| GET | `/variance` | `intel.variance.read` | ORG, SHOW, SEASON, EPISODE, DEPARTMENT | — | Group by `department`, `category`, `episode`, `period`; period and cumulative (FR-CIN-004, FR-CIN-005) |
| POST | `/variance/exports` | `intel.variance.export` | As above | K | `202` |
| GET | `/cost-facts` | `intel.cost_fact.read` | ORG, SHOW, DEPARTMENT | — | Drill-down to source expense identifiers within scope (FR-CIN-015) |
| GET | `/alerts` | `intel.alert.read` | ORG, SHOW, DEPARTMENT | — | Filters `severity`, `status`, `show_id`, `rule_id` |
| GET | `/alerts/{alertId}` | `intel.alert.read` | As above | — | |
| POST | `/alerts/{alertId}/acknowledge` | `intel.alert.update` | As above | M | |
| POST | `/alerts/{alertId}/resolve` | `intel.alert.update` | As above | M | Reason required (FR-CIN-009) |
| POST | `/alerts/{alertId}/false-positive` | `intel.alert.update` | As above | M | Reason required |
| GET | `/alert-rules` | `intel.alert_rule.read` | ORG, SHOW | — | |
| POST | `/alert-rules` | `intel.alert_rule.create` | ORG, SHOW | K, C | FR-CIN-007 |
| PATCH | `/alert-rules/{ruleId}` | `intel.alert_rule.update` | ORG, SHOW | M | Creates a new rule version |
| DELETE | `/alert-rules/{ruleId}` | `intel.alert_rule.delete` | ORG, SHOW | M | Disables; history kept |
| GET, POST | `/saved-views` | `report.saved_view.read`, `report.saved_view.create` | SELF | C (POST) | FR-CIN-023 |
| PATCH, DELETE | `/saved-views/{viewId}` | `report.saved_view.update`, `report.saved_view.delete` | SELF | M | |
| GET | `/forecast` | `budget.forecast.read` | SHOW, SEASON, DEPARTMENT | — | **P1** EFC computed by MOD-08 from `budget.forecast` (RC-03) |
| GET | `/benchmarks` | `intel.benchmark.read` | ORG | — | **P1**; minimum five organizations (FR-CIN-016) |

### 4.8 Documents and Retention (MOD-09)

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| POST | `/documents/upload-tickets` | `document.document.create` | SHOW, DEPARTMENT, ASSIGNMENT, SELF | K, C | Body: `document_id` (client UUIDv7), purpose, declared type, size, SHA-256, scope keys. Returns presigned PUT ≤15 minutes |
| POST | `/documents/bulk-upload-tickets` | `document.document.create` | As above | K | Up to 50 files; per-file result (FR-DOC-014) |
| POST | `/documents/{documentId}/complete` | `document.document.create` | As above | K | Verifies hash; `PENDING_SCAN` |
| GET | `/documents` | `document.document.read` | SHOW, DEPARTMENT, ASSIGNMENT, SELF | — | Search by metadata, linked entity, status, date (FR-DOC-008) |
| GET | `/documents/{documentId}` | `document.document.read` | As above | — | Metadata, version, scan state |
| GET | `/documents/{documentId}/download` | `document.document.read` | As above | — | Returns `{ url, expires_at }`, never a redirect; ≤15 minutes; logged (FR-DOC-013); `409 DOCUMENT_NOT_CLEAN` before scan |
| GET | `/documents/{documentId}/preview` | `document.document.read` | As above | — | Thumbnail or page preview URL after clean scan (FR-DOC-009) |
| GET | `/documents/{documentId}/versions` | `document.version.read` | SHOW, ASSIGNMENT | — | |
| POST | `/documents/{documentId}/versions` | `document.version.create` | SHOW, ASSIGNMENT | K | Replacement upload; links preserved (FR-DOC-016); refused for verified evidence without a new version path (BR-DOC-001) |
| DELETE | `/documents/{documentId}` | `document.document.delete` | As above | M | Soft delete with reason (FR-DOC-010); `409 LEGAL_HOLD_ACTIVE` |
| POST | `/documents/archives` | `document.document.export` | SHOW, DEPARTMENT, ASSIGNMENT | K | Step-up; `202` bulk archive |
| GET | `/documents/{documentId}/access-log` | `compliance.audit_event.read` | ORG, SHOW | — | Views, downloads, uploads, deletes |
| GET | `/retention-policies` | `compliance.retention_policy.read` | ORG | — | |
| POST | `/retention-policies` | `compliance.retention_policy.create` | ORG | K | Draft version |
| PUT | `/retention-policies/{policyId}` | `compliance.retention_policy.update` | ORG | M | Step-up; never below the regulatory minimum |
| POST | `/retention-policies/{policyId}/approve` | `compliance.retention_policy.approve` | ORG | M | Step-up; second person |

### 4.9 Excel (MOD-10)

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| GET | `/excel/templates` | `excel.template.read` | ORG | — | Filter `target`, `status` |
| POST | `/excel/templates` | `excel.template.create` | ORG | K | FR-XLS-001 |
| GET | `/excel/templates/{templateId}/versions` | `excel.template.read` | ORG | — | |
| POST | `/excel/templates/{templateId}/versions` | `excel.template.update` | ORG | K | Draft version with column mappings |
| POST | `/excel/template-versions/{versionId}/activate` | `excel.template.update` | ORG | M | Previous version retired unless declared compatible (BR-XLS-001) |
| PUT | `/excel/template-versions/{versionId}/value-mappings` | `excel.template.update` | ORG | M | Legacy label mapping |
| GET | `/excel/template-versions/{versionId}/download` | `excel.template.read` | ORG | — | Signed URL to the blank workbook with instructions (FR-XLS-003) |
| DELETE | `/excel/templates/{templateId}` | `excel.template.delete` | ORG | M | Retire; history kept |
| POST | `/excel/imports` | `excel.import_job.create` | ORG, SHOW | K, C | Section 5.5; `UPLOADED` with upload ticket |
| POST | `/excel/imports/{importId}/upload-complete` | `excel.import_job.create` | ORG, SHOW | K | `202`; `SCANNING` |
| GET | `/excel/imports` | `excel.import_job.read` | ORG, SHOW | — | Filters `status`, `template_id`, `created_by` |
| GET | `/excel/imports/{importId}` | `excel.import_job.read` | ORG, SHOW | — | State, counts, progress |
| GET | `/excel/imports/{importId}/rows` | `excel.validation_error.read` | ORG, SHOW | — | Preview; filter `outcome` (`VALID`, `WARNING`, `ERROR`) (FR-XLS-006) |
| GET | `/excel/imports/{importId}/error-report` | `excel.validation_error.read` | ORG, SHOW | — | Signed URL to the row-level report (FR-XLS-009) |
| POST | `/excel/imports/{importId}/validate` | `excel.import_job.update` | ORG, SHOW | K, M | Re-validate after mapping change |
| POST | `/excel/imports/{importId}/confirm-duplicate` | `excel.import_job.update` | ORG, SHOW | M | BR-XLS-003 |
| POST | `/excel/imports/{importId}/commit` | `excel.import_job.update` plus the target domain's `create` key | ORG, SHOW | K, M | `202`; `AWAITING_COMMIT_APPROVAL` or `COMMITTING` |
| POST | `/excel/imports/{importId}/commit-approval/approve` | `excel.import_job.approve` | ORG, SHOW | K, M | Step-up; second person |
| POST | `/excel/imports/{importId}/commit-approval/reject` | `excel.import_job.approve` | ORG, SHOW | K, M | `REJECTED`; comment required |
| POST | `/excel/imports/{importId}/discard` | `excel.import_job.delete` | ORG, SHOW | M | Uncommitted batch to `REJECTED` (FR-XLS-008) |
| POST | `/excel/imports/{importId}/reversal` | `excel.import_job.update` | ORG, SHOW | K, M | `REVERSAL_PENDING`; approval through the `IMPORT_COMMIT` chain (BR-XLS-006) |
| POST | `/excel/exports` | `excel.export_job.create` | ORG, SHOW, DEPARTMENT | K | Section 5.6 |
| GET | `/excel/exports` | `excel.export_job.read` | ORG, SHOW, DEPARTMENT | — | Caller's own jobs unless `ORG` |
| GET | `/excel/exports/{exportId}` | `excel.export_job.read` | As above | — | Status |
| GET | `/excel/exports/{exportId}/download` | `excel.export_job.read` | As above | — | Signed URL ≤15 minutes; `409 EXPORT_NOT_READY` |

### 4.10 Notifications (MOD-11)

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| GET | `/notifications` | `notification.notification.read` | SELF | — | Inbox; filters `status`, `category` |
| POST | `/notifications/{notificationId}/read` | `notification.notification.update` | SELF | — | Idempotent |
| POST | `/notifications/read-all` | `notification.notification.update` | SELF | — | |
| GET | `/notifications/stream` | `notification.notification.read` | SELF | — | SSE with heartbeat and `Last-Event-ID`; polling fallback every 60 s (Document 6 §7.9) |
| GET | `/notification-preferences` | `notification.preference.read` | SELF | — | |
| PUT | `/notification-preferences` | `notification.preference.update` | SELF | M | Mandatory security notices cannot be disabled; quiet hours (FR-NTF-014) |
| GET | `/notification-templates` | `notification.template.read` | ORG | — | |
| PUT | `/notification-templates/{templateId}` | `notification.template.update` | ORG | M | New template version (FR-NTF-011) |
| POST | `/notification-templates/{templateId}/test-send` | `notification.template.update` | ORG | K | To the caller only |
| POST | `/announcements` | `notification.announcement.create` | ORG, SHOW | K | **P1** (FR-NTF-013) |
| — | `/webhook-endpoints/*` | Section 6 | ORG | — | **P1** |

### 4.11 Compliance and Audit Trail (MOD-12)

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| GET | `/audit-events` | `compliance.audit_event.read` | ORG, SHOW | — | Filters `actor_id`, `action`, `entity_type`, `entity_id`, `occurred_from`, `occurred_to`, `correlation_id` (FR-CMP-004) |
| GET | `/audit-events/{eventId}` | `compliance.audit_event.read` | ORG, SHOW | — | Chain position and hash |
| POST | `/audit-events/exports` | `compliance.audit_event.export` | ORG, SHOW | K | Step-up; `202` (FR-CMP-005) |
| GET | `/audit-chain/verifications` | `compliance.audit_event.read` | ORG | — | Daily verification results and anchors |
| GET | `/security-events` | `compliance.security_event.read` | ORG | — | |
| POST | `/security-events/exports` | `compliance.security_event.export` | ORG | K | Step-up; `202` |
| GET | `/legal-holds` | `compliance.legal_hold.read` | ORG | — | |
| POST | `/legal-holds` | `compliance.legal_hold.create` | ORG | K | Step-up; matter reference, targets |
| POST | `/legal-holds/{holdId}/release-requests` | `compliance.legal_hold.update` | ORG | K, M | Step-up |
| POST | `/legal-holds/{holdId}/release-requests/{requestId}/approve` | `compliance.legal_hold.approve` | ORG | K | Step-up; second approver (BR-DOC-003) |
| GET | `/dsr-requests` | `compliance.dsr_request.read` | ORG | — | |
| POST | `/dsr-requests` | `compliance.dsr_request.create` | ORG | K | Statutory clock starts (FR-CMP-008) |
| PATCH | `/dsr-requests/{requestId}` | `compliance.dsr_request.update` | ORG | M | |
| POST | `/dsr-requests/{requestId}/approve` | `compliance.dsr_request.approve` | ORG | K, M | Releases the bundle or erasure; `409 LEGAL_HOLD_ACTIVE` |
| GET | `/ropa-entries` | `compliance.ropa_entry.read` | ORG | — | FR-CMP-014 |
| PATCH | `/ropa-entries/{entryId}` | `compliance.ropa_entry.update` | ORG | M | |
| GET, POST | `/breach-incidents` | `compliance.breach_incident.read`, `compliance.breach_incident.create` | ORG | K (POST) | FR-CMP-015 |
| POST | `/breach-incidents/{incidentId}/declare` | `compliance.breach_incident.approve` | ORG | K, M | Starts the 72-hour clock |
| GET | `/controls` | `compliance.control.read` | ORG | — | FR-CMP-007 |
| PATCH | `/controls/{controlId}` | `compliance.control.update` | ORG | M | |
| POST | `/evidence-packs` | `compliance.evidence_pack.create` | ORG | K | `202` |
| GET | `/evidence-packs/{packId}/download` | `compliance.evidence_pack.export` | ORG | — | Step-up |

### 4.12 Reporting (MOD-13)

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| GET | `/reports` | `report.report_definition.read` | ORG | — | Catalogue RPT-01 to RPT-18 and custom definitions |
| POST | `/reports` | `report.report_definition.create` | ORG | K | |
| PATCH, DELETE | `/reports/{reportId}` | `report.report_definition.update`, `report.report_definition.delete` | ORG | M | System definitions immutable |
| POST | `/reports/{reportId}/runs` | `report.report_run.create` | ORG, SHOW, DEPARTMENT, ASSIGNMENT | K | `202`; parameters and scope snapshot stored |
| GET | `/report-runs` | `report.report_run.read` | As above | — | Caller's runs unless `ORG` |
| GET | `/report-runs/{runId}` | `report.report_run.read` | As above | — | Status, row count, data watermark |
| GET | `/report-runs/{runId}/download` | `report.report_run.export` | As above | — | Step-up for restricted reports; signed URL ≤15 minutes |
| GET, POST | `/report-schedules` | `report.schedule.read`, `report.schedule.create` | ORG, SHOW | K (POST) | **P1** (FR-CIN-022, FR-XLS-014) |
| PATCH, DELETE | `/report-schedules/{scheduleId}` | `report.schedule.update`, `report.schedule.delete` | ORG, SHOW | M | **P1** |

### 4.13 Sync (MOD-14)

Served by the Sync host; device-bound tokens only. Paths follow Document 6 §9.3 exactly.

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| POST | `/sync/handshake` | `sync.checkpoint.read` | SELF | — | Server time, `scope_version`, `min_supported_version`, cursor validity |
| POST | `/sync/push` | `sync.sync_batch.create` | SELF | K (`batch_id`) | ≤25 mutations, ≤512 KB; each mutation authorised against its own target permission; section 5.7 |
| GET | `/sync/pull` | `sync.sync_batch.read` | SELF | — | ≤500 records or 5 MB per page; section 5.8 |
| POST | `/sync/ack` | `sync.sync_batch.create` | SELF | — | ≤500 mutation IDs; idempotent |
| GET | `/sync/conflicts` | `sync.sync_conflict.read` | SELF | — | Open conflicts for the device user |
| POST | `/sync/conflicts/{conflictId}/resolve` | `sync.sync_conflict.update` | SELF | M | User decision |
| POST | `/sync/conflicts/{conflictId}/manager-resolve` | `sync.sync_conflict.approve` | SHOW, ASSIGNMENT | M | After 7 days (UC-05); API host |
| POST | `/sync/evidence-ticket` | `document.document.create` | SELF | K | ≤50 per batch; device `document_id` |
| POST | `/sync/evidence-complete` | `document.document.create` | SELF | K | Confirms hash |

### 4.14 Platform Operations (MOD-15)

Platform routes require `PLATFORM_INTERNAL` membership and are unreachable from tenant hostnames (Document 3 §11.12, which also lists tenant administration and support-access routes).

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| GET | `/outbox/dead-letters` | `ops.job.read` | PLATFORM | — | Deliveries and jobs in `DEAD`; metadata and payload hash |
| POST | `/outbox/dead-letters/{deliveryId}/replay` | `ops.job.update` | PLATFORM | K | New attempt counter; audited (Document 7 §8.4) |
| POST | `/jobs/{jobId}/replay` | `ops.job.update` | PLATFORM | K | Resume or restart a dead job |
| POST | `/jobs/{jobId}/cancel` | `ops.job.update` | PLATFORM | M | Second operator approval for destructive jobs |
| GET | `/feature-flags` | Authenticated | SELF | — | Effective flags for the caller's organization |
| PUT | `/feature-flags/{flagKey}` | `ops.feature_flag.update` | PLATFORM | M | Region or tenant override |

---

## 5. Representative Contracts

Identifiers in the examples are illustrative UUIDv7 values. Headers not shown (`Authorization`, `Content-Type`, `traceparent`) are always present.

### 5.1 Expense Create (Lines, Splits, FX)

`POST /api/v1/expenses` with `Idempotency-Key: 0193b2d4-77a0-7c11-8e02-5d9f4a1b2c3d`

```json
{
  "id": "0193b2d4-5c1e-7a40-9f11-2b7c6e9d0a51",
  "show_id": "0192f0aa-1b2c-7d3e-8f40-0a1b2c3d4e5f",
  "season_id": "0192f0aa-2c3d-7e4f-9a51-1b2c3d4e5f60",
  "department_id": "0192f0ab-3d4e-7f50-8b62-2c3d4e5f6071",
  "vendor_id": "0192f0ac-4e5f-7061-9c73-3d4e5f607182",
  "assignment_id": "0192f0ad-5f60-7172-8d84-4e5f60718293",
  "expense_date": "2026-11-04",
  "document_type": "TAX_INVOICE",
  "document_number": "LGR-4471",
  "payment_mode": "CARD",
  "description": "Generator hire, night shoot, unit 2",
  "currency": "USD",
  "lines": [
    {
      "id": "0193b2d4-5c1f-7b00-8a01-000000000001",
      "category_id": "0192f0ae-6071-7283-9e95-5f60718293a4",
      "episode_id": "0192f0af-7182-7394-8fa6-60718293a4b5",
      "description": "Generator 125 kVA, 2 nights",
      "quantity": "2.0000",
      "unit_price": { "amount": "450.0000", "currency": "USD" },
      "tax_rate": "18.0000",
      "tax_inclusive": false,
      "input_tax_recoverable": false,
      "commitment_id": "0192f0b0-8293-74a5-9ab7-718293a4b5c6",
      "splits": [
        { "seq": 1, "department_id": "0192f0ab-3d4e-7f50-8b62-2c3d4e5f6071", "category_id": "0192f0ae-6071-7283-9e95-5f60718293a4", "percentage": "60.0000" },
        { "seq": 2, "department_id": "0192f0b1-93a4-75b6-8bc8-8293a4b5c6d7", "category_id": "0192f0ae-6071-7283-9e95-5f60718293a4", "percentage": "40.0000" }
      ]
    },
    {
      "id": "0193b2d4-5c1f-7b00-8a01-000000000002",
      "category_id": "0192f0b2-a4b5-76c7-9cd9-93a4b5c6d7e8",
      "description": "Fuel delivery charge",
      "quantity": "1.0000",
      "unit_price": { "amount": "120.0000", "currency": "USD" },
      "tax_rate": "0.0000",
      "tax_inclusive": false,
      "input_tax_recoverable": false,
      "splits": []
    }
  ]
}
```

`201 Created`, `ETag: "v1"`, `Location: /api/v1/expenses/0193b2d4-5c1e-7a40-9f11-2b7c6e9d0a51`

```json
{
  "id": "0193b2d4-5c1e-7a40-9f11-2b7c6e9d0a51",
  "root_expense_id": "0193b2d4-5c1e-7a40-9f11-2b7c6e9d0a51",
  "revision_no": 1,
  "expense_number": null,
  "status": "DRAFT",
  "version": 1,
  "source": "WEB",
  "currency": "USD",
  "lines": [
    {
      "id": "0193b2d4-5c1f-7b00-8a01-000000000001",
      "net": { "amount": "900.0000", "currency": "USD" },
      "tax": { "amount": "162.0000", "currency": "USD" },
      "gross": { "amount": "1062.0000", "currency": "USD" },
      "recoverable_tax": { "amount": "0.0000", "currency": "USD" },
      "cost_basis": { "amount": "1062.0000", "currency": "USD" },
      "splits": [
        { "seq": 1, "percentage": "60.0000", "amount": { "amount": "637.2000", "currency": "USD" } },
        { "seq": 2, "percentage": "40.0000", "amount": { "amount": "424.8000", "currency": "USD" } }
      ]
    },
    {
      "id": "0193b2d4-5c1f-7b00-8a01-000000000002",
      "net": { "amount": "120.0000", "currency": "USD" },
      "tax": { "amount": "0.0000", "currency": "USD" },
      "gross": { "amount": "120.0000", "currency": "USD" },
      "recoverable_tax": { "amount": "0.0000", "currency": "USD" },
      "cost_basis": { "amount": "120.0000", "currency": "USD" },
      "splits": [
        { "seq": 1, "percentage": "100.0000", "amount": { "amount": "120.0000", "currency": "USD" }, "implied": true }
      ]
    }
  ],
  "totals": {
    "net": { "amount": "1020.0000", "currency": "USD" },
    "tax": { "amount": "162.0000", "currency": "USD" },
    "gross": { "amount": "1182.0000", "currency": "USD" }
  },
  "fx_preview": {
    "binding": false,
    "rate": "84.125000",
    "rate_date": "2026-11-04",
    "rate_source": "ECB",
    "is_fallback": false,
    "base_amount": { "amount": "99435.7500", "currency": "INR" }
  },
  "evidence": { "required": true, "satisfied": false, "links": [] },
  "created_at": "2026-11-06T08:41:12.004Z",
  "updated_at": "2026-11-06T08:41:12.004Z",
  "links": { "self": "/api/v1/expenses/0193b2d4-5c1e-7a40-9f11-2b7c6e9d0a51", "submit": "/api/v1/expenses/0193b2d4-5c1e-7a40-9f11-2b7c6e9d0a51/submit" }
}
```

Rules shown: amounts are computed server-side and rounded half-up at line, tax and converted amount (RC-01, BR-FIN-002); a line with no splits carries an implied 100 % split to the header department and category; splits must sum to exactly 100 % and to the line cost basis, the remainder going to the largest split (BR-EXP-012); the FX block is a non-binding preview until submission (BR-FIN-003).

### 5.2 Expense Submit

`POST /api/v1/expenses/0193b2d4-5c1e-7a40-9f11-2b7c6e9d0a51/submit` with `If-Match: "v3"` and `Idempotency-Key`

```json
{ "declaration_accepted": true, "comment": null }
```

`200 OK`, `ETag: "v4"`

```json
{
  "id": "0193b2d4-5c1e-7a40-9f11-2b7c6e9d0a51",
  "status": "SUBMITTED",
  "expense_number": "KHO-26-000412",
  "version": 4,
  "submitted_at": "2026-11-06T09:02:47.311Z",
  "fx_snapshot": {
    "original_amount": { "amount": "1182.0000", "currency": "USD" },
    "rate": "84.125000",
    "rate_date": "2026-11-04",
    "rate_source": "ECB",
    "override_id": null,
    "is_fallback": false,
    "base_amount": { "amount": "99435.7500", "currency": "INR" }
  },
  "line_base_amounts": [
    { "line_id": "0193b2d4-5c1f-7b00-8a01-000000000001", "base_cost_basis": { "amount": "89340.7500", "currency": "INR" } },
    { "line_id": "0193b2d4-5c1f-7b00-8a01-000000000002", "base_cost_basis": { "amount": "10095.0000", "currency": "INR" } }
  ],
  "approval": {
    "approval_instance_id": "0193b2e0-0a1b-7c2d-8e3f-405162738495",
    "status": "IN_PROGRESS",
    "workflow_version": 7,
    "threshold_basis": { "amount": "99435.7500", "currency": "INR" },
    "steps": [
      { "step_no": 1, "approver_rule": "DEPARTMENT_HEAD", "status": "ACTIVE", "due_at": "2026-11-08T09:02:47Z" },
      { "step_no": 2, "approver_rule": "COST_CONTROLLER", "status": "WAITING", "due_at": null }
    ]
  },
  "exceptions": [
    { "exception_id": "0193b2e0-1b2c-7d3e-9f40-5162738495a6", "type": "DUPLICATE_SUSPECTED", "severity": "MEDIUM", "status": "OPEN", "rule_code": "BR-EXP-006", "related_expense_ids": ["0193a911-0000-7000-8000-00000000a001"] }
  ],
  "warnings": [
    { "code": "COMMITMENT_NEAR_LIMIT", "message": "This expense consumes 92% of commitment DM-0031." }
  ]
}
```

The header base amount equals the sum of rounded line base amounts (BR-FIN-011). Validation failures at submission return a single `422` or `409` problem listing every field error found (FR-EXP-013), for example `PERIOD_LOCKED`, `EVIDENCE_REQUIRED`, `VENDOR_BLOCKED`, `CATEGORY_NOT_MAPPED`.

### 5.3 Approval Decision

`POST /api/v1/expenses/{expenseId}/approve` with `If-Match: "v4"` and `Idempotency-Key`

```json
{
  "approval_instance_id": "0193b2e0-0a1b-7c2d-8e3f-405162738495",
  "step_no": 1,
  "comment": "Checked against invoice and generator log.",
  "delegation_id": null
}
```

`200 OK`, `ETag: "v5"`

```json
{
  "expense_id": "0193b2d4-5c1e-7a40-9f11-2b7c6e9d0a51",
  "status": "SUBMITTED",
  "decision": {
    "decision_id": "0193b2f1-2c3d-7e4f-8051-62738495a6b7",
    "step_no": 1,
    "decision": "APPROVED",
    "actor_user_id": "0192e001-0000-7000-8000-0000000000d1",
    "on_behalf_of_user_id": null,
    "authority_source": "MEMBERSHIP",
    "limit_applied": { "amount": "250000.0000", "currency": "INR" },
    "decided_at": "2026-11-06T11:15:03.920Z"
  },
  "approval": { "status": "IN_PROGRESS", "active_step_no": 2 },
  "cost_postings_recorded": 0
}
```

On the final step the response carries `"status": "APPROVED"` and `"cost_postings_recorded": 3` (two splits of line 1 and the implied split of line 2, posted in the period of the expense date; BR-EXP-010). Rejection and request-information use the same body with a mandatory `comment` and a `reason_code` (FR-EXP-015). Typical denials: `403 SELF_APPROVAL_PROHIBITED` (BR-EXP-005), `403 APPROVAL_LIMIT_EXCEEDED`, `409 APPROVAL_STEP_MISMATCH`, `409 EVIDENCE_DISPUTED` (UC-07), `409 PERIOD_LOCKED`.

### 5.4 Budget Version Submit

`POST /api/v1/budget-versions/{versionId}/submit` with `If-Match: "v12"` and `Idempotency-Key`

```json
{ "cover_note": "Season 2 baseline including 5% contingency." }
```

`200 OK`

```json
{
  "id": "0193a0c4-11aa-7b22-8c33-d44e55f66a77",
  "budget_id": "0193a0c4-0f99-7a11-9b22-c33d44e55f66",
  "version_no": 1,
  "version_type": "ORIGINAL",
  "status": "SUBMITTED",
  "version": 13,
  "totals": {
    "operational": { "amount": "11904761.9000", "currency": "INR" },
    "contingency": { "amount": "595238.1000", "currency": "INR" },
    "total": { "amount": "12500000.0000", "currency": "INR" },
    "line_count": 148
  },
  "validation": { "complete": true, "issues": [] },
  "approval": {
    "approval_instance_id": "0193a0d0-3344-7c55-8d66-e77f88a99b00",
    "threshold_basis": "TOTAL_BUDGET_VALUE",
    "steps": [
      { "step_no": 1, "approver_rule": "FINANCE_HEAD", "status": "ACTIVE", "commissioner_final": false },
      { "step_no": 2, "approver_rule": "COMMISSIONING_ADMIN", "status": "WAITING", "commissioner_final": true }
    ]
  }
}
```

The threshold is evaluated on the total budget value (BR-BUD-011); commissioner final approval is added above the configured threshold (BR-BUD-010); the total equals the sum of lines including contingency (BR-BUD-003). Errors: `422 BUDGET_INCOMPLETE`, `422 TOTAL_MISMATCH`, `422 BUDGET_LINE_DUPLICATE`, `422 ALLOCATION_EXCEEDS_SEASON`, `422 APPROVAL_CHAIN_UNRESOLVED`.

### 5.5 Import Batch Lifecycle

| Step | Call | Resulting SM-17 state |
|------|------|----------------------|
| 1 | `POST /excel/imports` | `UPLOADED` (ticket issued) |
| 2 | `PUT` to the presigned URL (object storage) | — |
| 3 | `POST /excel/imports/{id}/upload-complete` | `SCANNING`, then `PARSING`, `VALIDATING` (worker) |
| 4 | `GET /excel/imports/{id}` | `VALIDATED` or `FAILED` |
| 5 | `GET /excel/imports/{id}/rows?outcome=ERROR` | — |
| 6 | `POST /excel/imports/{id}/commit` | `AWAITING_COMMIT_APPROVAL` or `COMMITTING` |
| 7 | `POST …/commit-approval/approve` (when required) | `COMMITTING` |
| 8 | Worker completes | `COMMITTED` or `PARTIALLY_COMMITTED` |
| 9 | `POST /excel/imports/{id}/reversal` (optional) | `REVERSAL_PENDING`, then `REVERSED` |
| — | Uncommitted for 7 days | `EXPIRED` |

Step 1 request:

```json
{
  "id": "0193c001-aaaa-7bbb-8ccc-0000000000e1",
  "template_version_id": "0193bf10-1111-7222-8333-444455556666",
  "show_id": "0192f0aa-1b2c-7d3e-8f40-0a1b2c3d4e5f",
  "commit_mode": "PARTIAL",
  "file": { "name": "unit2-expenses-oct.xlsx", "size_bytes": 2483312, "sha256": "9f2c4e…b71a" }
}
```

Step 1 response (`201`):

```json
{
  "id": "0193c001-aaaa-7bbb-8ccc-0000000000e1",
  "status": "UPLOADED",
  "version": 1,
  "upload": {
    "document_id": "0193c001-bbbb-7ccc-8ddd-0000000000e2",
    "method": "PUT",
    "url": "https://storage.example/quarantine/…",
    "headers": { "Content-Type": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "x-amz-checksum-sha256": "…" },
    "expires_at": "2026-11-06T09:20:00Z"
  }
}
```

Step 4 response (`200`):

```json
{
  "id": "0193c001-aaaa-7bbb-8ccc-0000000000e1",
  "status": "VALIDATED",
  "version": 6,
  "template_version_id": "0193bf10-1111-7222-8333-444455556666",
  "counts": { "total_rows": 1840, "valid": 1792, "warning": 31, "error": 17 },
  "error_rate": "0.9239",
  "duplicate_file": { "detected": false, "previous_batch_id": null },
  "commit": { "allowed": true, "mode": "PARTIAL", "partial_allowed": true, "requires_approval": true },
  "progress": { "processed": 1840, "total": 1840 },
  "links": { "rows": "/api/v1/excel/imports/0193c001-aaaa-7bbb-8ccc-0000000000e1/rows", "error_report": "/api/v1/excel/imports/0193c001-aaaa-7bbb-8ccc-0000000000e1/error-report" }
}
```

Step 5 item:

```json
{
  "row_number": 18,
  "outcome": "ERROR",
  "mapped": { "expense_date": "2026-10-09", "department_code": "ART", "category_code": "SET-MAT" },
  "messages": [
    { "cell": "F18", "column": "Category", "code": "CATEGORY_NOT_MAPPED", "message": "Category SET-MAT is not mapped to department ART." }
  ]
}
```

Step 6 request with `If-Match: "v6"`: `{ "acknowledge_warnings": true }`; response `202` with `{ "status": "AWAITING_COMMIT_APPROVAL", "approval_instance_id": "…" }`. Row codes are exactly the codes the UI path returns (BR-XLS-002, ADR-011). Partial commit is refused with `409 IMPORT_ERROR_THRESHOLD` when the error rate exceeds the organization threshold (BR-XLS-004).

### 5.6 Export Request

`POST /api/v1/excel/exports` with `Idempotency-Key`

```json
{
  "export_type": "EXPENSE_REGISTER",
  "format": "XLSX",
  "template_version_id": null,
  "filters": {
    "show_id": "0192f0aa-1b2c-7d3e-8f40-0a1b2c3d4e5f",
    "expense_date_from": "2026-10-01",
    "expense_date_to": "2026-10-31",
    "status": ["APPROVED"]
  },
  "columns": ["expense_number", "expense_date", "vendor_name", "department_code", "category_code", "base_amount", "status"]
}
```

`202 Accepted`, `Location: /api/v1/excel/exports/0193c0aa-0000-7000-8000-0000000000f1`

```json
{
  "id": "0193c0aa-0000-7000-8000-0000000000f1",
  "status": "QUEUED",
  "scope_snapshot": { "authority_source": "MEMBERSHIP", "scope_version": 42 },
  "created_at": "2026-11-06T10:00:00Z"
}
```

Status when finished: `{ "status": "COMPLETED", "row_count": 2214, "watermark_id": "WM-7Q3K-2214", "file_expires_at": "2026-11-13T10:00:41Z" }`. `GET …/download` returns `{ "url": "…", "expires_at": "…" }` valid for 15 minutes (NFR-SEC-008). Rows are limited to the requester's scope at generation; bank and tax identifiers are masked (BR-VND-007); cells beginning with `=`, `+`, `-` or `@` are neutralised. Exports above 1,000 rows require step-up (Document 3 §11.13).

### 5.7 Sync Push Batch

`POST /api/v1/sync/push` with `Idempotency-Key: <batch_id>`; fields follow Document 6 §9.3.

```json
{
  "batch_id": "0193c100-0000-7000-8000-00000000b001",
  "device_id": "0192d500-0000-7000-8000-00000000d001",
  "app_version": "1.4.2",
  "scope_version": 42,
  "device_seq_range": { "from": 41, "to": 43 },
  "mutations": [
    {
      "mutation_id": "0193c0ff-0001-7000-8000-000000000041",
      "seq": 41,
      "entity_type": "EXPENSE",
      "entity_id": "0193c0fe-1111-7000-8000-0000000000a1",
      "operation": "CREATE",
      "base_version": 0,
      "client_occurred_at": "2026-11-05T21:14:09+05:30",
      "payload": {
        "show_id": "0192f0aa-1b2c-7d3e-8f40-0a1b2c3d4e5f",
        "department_id": "0192f0ab-3d4e-7f50-8b62-2c3d4e5f6071",
        "expense_date": "2026-11-05",
        "currency": "INR",
        "document_type": "RECEIPT",
        "payment_mode": "CASH",
        "lines": [
          { "id": "0193c0fe-1111-7000-8000-0000000000b1", "category_id": "0192f0b2-a4b5-76c7-9cd9-93a4b5c6d7e8", "quantity": "1.0000", "unit_price": { "amount": "1850.0000", "currency": "INR" }, "tax_rate": "0.0000", "tax_inclusive": false }
        ]
      },
      "client_hash": null
    },
    {
      "mutation_id": "0193c0ff-0002-7000-8000-000000000042",
      "seq": 42,
      "entity_type": "EXPENSE",
      "entity_id": "0193c0fe-1111-7000-8000-0000000000a1",
      "operation": "UPDATE",
      "base_version": 1,
      "client_occurred_at": "2026-11-05T21:16:40+05:30",
      "payload": { "description": "Crew tea, night unit" }
    },
    {
      "mutation_id": "0193c0ff-0003-7000-8000-000000000043",
      "seq": 43,
      "entity_type": "EXPENSE",
      "entity_id": "0193c0fe-2222-7000-8000-0000000000a2",
      "operation": "SUBMIT",
      "base_version": 3,
      "client_occurred_at": "2026-11-05T21:20:02+05:30",
      "payload": {}
    }
  ]
}
```

`200 OK`

```json
{
  "batch_id": "0193c100-0000-7000-8000-00000000b001",
  "server_time": "2026-11-06T06:02:11.448Z",
  "results": [
    { "mutation_id": "0193c0ff-0001-7000-8000-000000000041", "seq": 41, "result": "APPLIED", "entity_version": 1, "correlation_id": "0193c101-0000-7000-8000-0000000000c1" },
    { "mutation_id": "0193c0ff-0002-7000-8000-000000000042", "seq": 42, "result": "APPLIED", "entity_version": 2, "policy": "C2", "correlation_id": "0193c101-0000-7000-8000-0000000000c2" },
    { "mutation_id": "0193c0ff-0003-7000-8000-000000000043", "seq": 43, "result": "REJECTED", "reason_code": "PERIOD_LOCKED", "policy": "C5", "field": "/expense_date", "correlation_id": "0193c101-0000-7000-8000-0000000000c3" }
  ],
  "checkpoint": { "last_processed_seq": 43 }
}
```

Results are `APPLIED`, `CONFLICT` (with `conflict_id` and policy `C4`), `REJECTED` (stable `reason_code`, policy `C5`) or `RETRY` (processing stops at that mutation). A repeated `mutation_id` returns its original result (Document 6 §9.3 rule 3). A gap in `device_seq_range` returns `422 SYNC_SEQ_GAP`.

### 5.8 Sync Pull Batch

`GET /api/v1/sync/pull?cursor=eyJ2IjoxLCJzIjo5ODg…&limit=500`

```json
{
  "changes": [
    { "server_seq": 988412, "entity_type": "CATEGORY", "entity_id": "0192f0ae-6071-7283-9e95-5f60718293a4", "version": 9, "op": "UPSERT", "changed_at": "2026-11-06T05:40:00Z", "data": { "code": "EQ-RENT", "name": "Equipment rental", "active": true, "department_ids": ["0192f0ab-3d4e-7f50-8b62-2c3d4e5f6071"] } },
    { "server_seq": 988430, "entity_type": "EXPENSE", "entity_id": "0193c0fe-1111-7000-8000-0000000000a1", "version": 2, "op": "UPSERT", "changed_at": "2026-11-06T06:02:11Z", "data": { "status": "DRAFT", "description": "Crew tea, night unit" } },
    { "server_seq": 988431, "entity_type": "FINANCIAL_PERIOD", "entity_id": "0193a000-0000-7000-8000-000000000910", "version": 4, "op": "UPSERT", "changed_at": "2026-11-06T06:05:00Z", "data": { "status": "LOCKED", "start": "2026-10-01", "end": "2026-10-31" } }
  ],
  "next_cursor": "eyJ2IjoxLCJzIjo5ODg0MzF9.Kq2…",
  "has_more": false,
  "scope_version": 42,
  "server_time": "2026-11-06T06:05:02.100Z"
}
```

Only data the device's current scope permits is returned (Document 6 §9.3 pull rule 3). A cursor older than the 30-day change-log retention returns `410 CURSOR_EXPIRED` and the device performs a full resync. A `scope_version` change returns `409 SCOPE_VERSION_STALE` on the next push until the device has refreshed reference data.

### 5.9 Dashboard Query

`GET /api/v1/dashboards/show-cost?show_id=0192f0aa-1b2c-7d3e-8f40-0a1b2c3d4e5f&period_id=0193a000-0000-7000-8000-000000000911&group_by=department`

```json
{
  "dashboard_key": "show-cost",
  "as_of": "2026-11-06T10:14:20Z",
  "freshness_tier": "SHOW",
  "stale": false,
  "scope": { "show_id": "0192f0aa-1b2c-7d3e-8f40-0a1b2c3d4e5f", "period_id": "0193a000-0000-7000-8000-000000000911", "cumulative": true },
  "currency": "INR",
  "measures": {
    "budget": { "amount": "12500000.0000", "currency": "INR" },
    "actual": { "amount": "8240315.5000", "currency": "INR" },
    "committed": { "amount": "1905000.0000", "currency": "INR" },
    "variance": { "amount": "2354684.5000", "currency": "INR" },
    "variance_pct": "18.8375",
    "pending_approval": { "amount": "312040.0000", "currency": "INR" }
  },
  "breakdown": [
    {
      "department_id": "0192f0ab-3d4e-7f50-8b62-2c3d4e5f6071",
      "budget": { "amount": "2000000.0000", "currency": "INR" },
      "actual": { "amount": "1780000.0000", "currency": "INR" },
      "committed": { "amount": "310000.0000", "currency": "INR" },
      "variance": { "amount": "-90000.0000", "currency": "INR" },
      "variance_pct": "-4.5000",
      "classification": "OVER_BUDGET",
      "drill": "/api/v1/cost-facts?show_id=0192f0aa-1b2c-7d3e-8f40-0a1b2c3d4e5f&department_id=0192f0ab-3d4e-7f50-8b62-2c3d4e5f6071&period_id=0193a000-0000-7000-8000-000000000911"
    }
  ],
  "alerts": { "open": 3, "critical": 1 }
}
```

Variance equals budget minus actual minus committed (Document 6 §8.10); a zero budget yields `"variance_pct": null` ("not applicable", Document 2 §7). `stale` becomes `true` when the projection lag exceeds the tier target (show ≤60 s, portfolio ≤5 min); the figures are still returned and labelled (FR-CIN-020). Departments outside the caller's scope are omitted, not zeroed.

### 5.10 Audit Report Submit

`POST /api/v1/audit-reports/{reportId}/submit` with `If-Match: "v5"` and `Idempotency-Key`

```json
{
  "summary": "Reviewed 37 expenses for unit 2; petty cash counted at close of day.",
  "petty_cash_check": {
    "counted": { "amount": "45200.0000", "currency": "INR" },
    "book_balance": { "amount": "45500.0000", "currency": "INR" },
    "difference_explanation": "Receipt for INR 300 pending from transport."
  },
  "finding_ids": ["0193c210-0000-7000-8000-0000000000f7"],
  "attestation": { "statement_version": "AR-ATT-1", "accepted": true }
}
```

`200 OK`

```json
{
  "id": "0193c200-0000-7000-8000-0000000000e9",
  "assignment_id": "0192f0ad-5f60-7172-8d84-4e5f60718293",
  "report_type": "DAILY",
  "report_date": "2026-11-05",
  "report_version": 1,
  "status": "SUBMITTED",
  "version": 6,
  "totals_snapshot": {
    "snapshot_at": "2026-11-06T13:30:00Z",
    "expenses_reviewed": 37,
    "amount_reviewed": { "amount": "612480.0000", "currency": "INR" },
    "exceptions_open": 4,
    "evidence_disputed": 1
  },
  "submitted_at": "2026-11-06T13:30:00Z"
}
```

The totals snapshot is taken in the submitting transaction (Document 7 §4.6). Errors: `409 ASSIGNMENT_NOT_ACTIVE`, `409 AUDIT_REPORT_EXISTS` (an accepted report already exists for the assignment, type and date; submit a revision instead), `422 VALIDATION_FAILED` when the attestation is not accepted.

---

## 6. Phase 1 Signed Webhooks

FR-NTF-010 and NFR-INTG-003 require signed, verifiable webhooks for organization-configured endpoints. They are released in Phase 1 (Document 6 §10.4, INTG-08) and owned by MOD-11 with deliveries in `ops.event_delivery` (Document 7 RC-7-06).

### 6.1 Endpoint Management

| Method | Path | Permission | Scope | Idem | Notes |
|--------|------|------------|-------|------|-------|
| GET | `/webhook-endpoints` | `notification.webhook.read` (requested) | ORG | — | |
| POST | `/webhook-endpoints` | `notification.webhook.create` (requested) | ORG | K | Step-up; HTTPS URL, event-type subscriptions; secret shown once |
| PATCH | `/webhook-endpoints/{endpointId}` | `notification.webhook.update` (requested) | ORG | M | URL change re-validates and re-tests |
| DELETE | `/webhook-endpoints/{endpointId}` | `notification.webhook.delete` (requested) | ORG | M | Disables; history kept |
| POST | `/webhook-endpoints/{endpointId}/rotate-secret` | `notification.webhook.update` (requested) | ORG | K | New secret; old secret valid for 24 hours |
| POST | `/webhook-endpoints/{endpointId}/test` | `notification.webhook.update` (requested) | ORG | K | Sends `WebhookTest` |
| POST | `/webhook-endpoints/{endpointId}/enable` | `notification.webhook.update` (requested) | ORG | M | After auto-disable |
| GET | `/webhook-endpoints/{endpointId}/deliveries` | `notification.webhook.read` (requested) | ORG | — | Attempts, status codes, latency |
| POST | `/webhook-deliveries/{deliveryId}/replay` | `notification.webhook.update` (requested) | ORG | K | New attempt counter |

### 6.2 Delivery Contract

```
POST https://receiver.example.com/slateiq
Content-Type: application/json
SlateIQ-Event-Id: 0193d001-0000-7000-8000-00000000aa01
SlateIQ-Event-Type: ExpenseApproved
SlateIQ-Delivery-Attempt: 1
SlateIQ-Signature: t=1762425262,v1=5f0c2a…e91b
```

```json
{
  "id": "0193d001-0000-7000-8000-00000000aa01",
  "type": "ExpenseApproved",
  "schema_version": 1,
  "occurred_at": "2026-11-06T11:15:04Z",
  "organization_id": "0192e000-0000-7000-8000-000000000001",
  "data": {
    "expense_id": "0193b2d4-5c1e-7a40-9f11-2b7c6e9d0a51",
    "expense_number": "KHO-26-000412",
    "show_id": "0192f0aa-1b2c-7d3e-8f40-0a1b2c3d4e5f",
    "status": "APPROVED"
  },
  "links": { "resource": "https://api.in.slateiq.app/api/v1/expenses/0193b2d4-5c1e-7a40-9f11-2b7c6e9d0a51" }
}
```

| Rule | Detail |
|------|--------|
| Thin events | Identifiers, status and links only; no amounts, personal data or document content. Receivers fetch details through the API with a Phase 1 service account (ADR-043) |
| Signature | `v1 = hex(HMAC-SHA256(secret, t + "." + raw_body))`; `t` is Unix seconds; receivers reject a timestamp older than 300 s; during rotation two `v1` values are sent |
| Secret | 256-bit random, stored in the secret manager, shown once (Document 6 §10.6) |
| Subscriptions | Allow-listed event types: `ExpenseApproved`, `ExpenseRejected`, `ExpenseCancelled`, `BudgetApproved`, `CommitmentApproved`, `CommitmentClosed`, `AlertTriggered`, `ImportCommitted`, `AuditReportDecided`, `PeriodLocked`, `WebhookTest` |
| Delivery | At least once; receivers deduplicate on `SlateIQ-Event-Id`; 2xx within 10 s is success; ordering per aggregate |
| Retry | RP-C (Document 7 §8.2): 1 min, 5 min, 30 min, 2 h, 6 h, then every 6 h until 24 h; then `DEAD` |
| Auto-disable | After 20 consecutive failures; the organization administrator is notified (Document 6 §10.7) |
| Egress safety | HTTPS only; public IP ranges only; DNS resolved and pinned per attempt; redirects not followed; private and link-local targets rejected with `422 WEBHOOK_URL_REJECTED` |
| Observability | Lag, failure rate and retries per endpoint (NFR-INTG-004) |

---

## 7. Traceability

| Requirement | Satisfied by |
|-------------|--------------|
| Governing brief §15 | Section 2 (all conventions), sections 3 to 5 |
| FR-TNT-018 | Section 2.4 |
| NFR-MNT-001 | Sections 2.2, 2.18 |
| NFR-MNT-002 | Section 2.18 (published reference) |
| NFR-SEC-004 | Sections 2.4, 2.5 |
| NFR-SEC-008 | Sections 4.8, 5.6 |
| NFR-SEC-013 | Section 2.14 |
| NFR-UX-008 | Sections 2.6, 2.11 |
| NFR-AUD-004, FR-CMP-006 | Section 2.15 |
| NFR-PERF-003, NFR-PERF-012 | Section 2.1 handler budget; long work moved to `202` (section 2.16) |
| NFR-INTG-001, NFR-INTG-002, NFR-INTG-003, NFR-INTG-004, FR-NTF-010 | Section 6 |
| FR-EXP-001 to FR-EXP-029 | Sections 4.6, 5.1 to 5.3 |
| FR-BUD-001 to FR-BUD-020 | Sections 4.3, 5.4 |
| FR-VND-001 to FR-VND-020 | Section 4.4 |
| FR-AUD-001 to FR-AUD-016 | Sections 4.5, 5.10 |
| FR-CIN-001 to FR-CIN-023 | Sections 4.7, 5.9 |
| FR-DOC-001 to FR-DOC-017 | Section 4.8 |
| FR-XLS-001 to FR-XLS-017 | Sections 4.9, 5.5, 5.6 |
| FR-NTF-001 to FR-NTF-015 | Sections 4.10, 6 |
| FR-CMP-001 to FR-CMP-015 | Section 4.11 |
| FR-EXP-002 to FR-EXP-004, NFR-MOB-002, NFR-MOB-006 | Sections 4.13, 5.7, 5.8 |
| BR-FIN-001, BR-FIN-002, BR-FIN-003, BR-FIN-011 | Sections 2.7, 5.1, 5.2 |
| BR-EXP-005, BR-EXP-007, BR-EXP-008, BR-EXP-010 | Section 5.3 |
| BR-BUD-003, BR-BUD-010, BR-BUD-011 | Section 5.4 |
| BR-XLS-001 to BR-XLS-006 | Sections 4.9, 5.5 |
| CAP-001 to CAP-026 | Section 4 (one subsection per owning module) |
| ADR-007, ADR-011 | Sections 2.9 to 2.13, 5.5 |
| ADR-040 to ADR-043 | Section 12 |

---

## 8. Assumptions and Open Questions

| ID | Assumption or question | Impact if wrong | Owner |
|----|------------------------|-----------------|-------|
| A9-01 | `snake_case` JSON is acceptable to the web and mobile teams given generated clients | Renaming layer in clients | Frontend Lead |
| A9-02 | Pilot clients do not need offset pagination or totals for large lists | UX change for "page N of M" designs | Product Owner |
| A9-03 | Webhook receivers can call back with a Phase 1 service account to fetch amounts | Fat events would need a separate data-classification review | Integration Lead |
| A9-04 | `payment_mode` values (`CASH`, `CARD`, `BANK_TRANSFER`, `UPI`, `CHEQUE`, `OTHER`) are confirmed by Finance | Enum change before release | Finance SME |
| OQ-9-01 | Should bulk approval be limited to expenses below a configurable amount? | Control strength versus approver productivity | Finance SME |
| OQ-9-02 | Should dashboard responses support `currency_view=ORIGINAL`, or is base currency sufficient? | Additional projection columns | Product Owner |
| OQ-9-03 | Is a public, customer-facing API (beyond webhooks and service accounts) in scope for Phase 1? | Developer portal, quotas, API keys | Product Owner |

---

## 9. Risks Raised

| ID | Risk | Likelihood | Impact | Mitigation |
|----|------|-----------|--------|------------|
| R9-01 | Contract-first OpenAPI drifts from implementation when teams edit generated code | Medium | High | Generated folders read-only; conformance and route-inventory tests in CI |
| R9-02 | Error-code proliferation makes client handling inconsistent | Medium | Medium | Catalogue review in the architecture forum; codes added only with a rule reference |
| R9-03 | Idempotency storage grows under heavy sync and import retries | Low | Medium | 7-day purge; partition `ops.idempotency_key` by day if volume requires |
| R9-04 | Clients misuse action links as authority | Low | Medium | Documented as advisory; server re-authorises every request |
| R9-05 | Webhook receivers fail to verify signatures | Medium | Medium | Reference verification snippets in the integration guide; test endpoint |

---

## 10. Catalogue Additions Requested

| Key | Actions | Scopes | Step-up | Release | Reason |
|-----|---------|--------|---------|---------|--------|
| `notification.webhook` | R C U D | ORG | C U | P1 | Webhook endpoint management (section 6.1); no existing key covers outbound integration configuration |

Table additions requested for Document 3 Appendix D and Document 8: `notify.webhook_endpoint` (family A, Phase 1), together with RC-7-03, RC-7-04 and RC-7-05 of Document 7.

---

## 11. Reconciliation Notes

| ID | Conflict | Proposed resolution |
|----|----------|---------------------|
| RC-9-01 | Document 6 Appendix A.1 derives tenant context from the token and `X-Org-Id` "matching an active membership" | Document 3 §11.2 governs: the token `org` claim is authoritative; the header is an assertion only (section 2.4) |
| RC-9-02 | Document 3 §5.11 uses `FORBIDDEN`, `OUT_OF_SCOPE`, `NOT_FOUND`, `MODULE_NOT_ENABLED`; §11.4 uses `PERMISSION_DENIED`, `SCOPE_DENIED`, `RESOURCE_NOT_FOUND`; §12.4 uses `MODULE_NOT_ENTITLED` | The §11.4 codes are canonical; `MODULE_NOT_ENTITLED` is used (ERR-019) |
| RC-9-03 | Document 3 §11.15 says the OpenAPI document is generated from code; Document 6 §6.3 generates it at build | Contract-first (ADR-041): the document in `contracts/openapi` is the source; code is generated from it; the CI diff gate is unchanged |
| RC-9-04 | Document 3 §11.14 names filters in camelCase (`granteeUserId`) | Query parameters are `snake_case` (`grantee_user_id`) per ADR-040; Document 3 routes otherwise unchanged |
| RC-9-05 | Document 6 Appendix A.5 lists `POST /budget-versions/{id}/lock`; SM-04 has no lock state | Removed. Versions are immutable once they leave `DRAFT`; periods are locked through `/financial-periods/{id}/close` |
| RC-9-06 | Document 6 Appendix A.8 issues evidence tickets per expense; A.9 places legal hold on documents | Evidence uploads use `/documents/upload-tickets` and are linked with `POST /expenses/{id}/evidence`; legal holds are `/legal-holds` resources (MOD-12) targeting documents or records |
| RC-9-07 | Document 6 Appendix A.7 `DELETE /audit-assignments/{id}` ends an assignment | Explicit SM-08 transitions (`/complete`, `/revoke`, `/handover`); no `DELETE` |
| RC-9-08 | Document 6 Appendix A.11 `POST /reports/{reportId}/run` and `POST /excel/imports` "upload a workbook" | `POST /reports/{reportId}/runs` (resource creation); workbooks travel by presigned URL, never through the API |
| RC-9-09 | Document 6 Appendix A.10 `POST /alert-rules` "create or update" | Create with `POST`, update with `PATCH /alert-rules/{id}` (new version) |

---

## 12. Architecture Decision Records

### ADR-040 — Wire Format: snake_case JSON and Decimal-String Money

**Status:** Proposed.

**Context.** Document 3 bodies and Document 6 §9 use `snake_case`; some Document 3 filters use camelCase. Money must never travel as a binary float (Authoring Brief §2).

**Decision.** Request and response bodies and query parameters use `snake_case`. Money is `{ amount, currency }` with `amount` a decimal string of scale 4; rates and percentages are decimal strings. Enums are `UPPER_SNAKE_CASE` with Document 4 state names. Unknown request fields are rejected; unknown response fields are ignored by clients. ProblemDetails extension members keep the Document 3 names (`code`, `traceId`, `errors`).

**Alternatives rejected.** camelCase (diverges from the sync protocol and the event envelope); JSON numbers for money (precision loss in JavaScript clients); minor-unit integers (currencies with three or zero minor digits and scale-4 storage make them ambiguous).

**Consequences.** One naming rule across API, sync and events. Generated TypeScript types expose `snake_case` properties.

### ADR-041 — Contract-First OpenAPI 3.1 in `contracts/`

**Status:** Proposed.

**Context.** Web, mobile and backend are separate projects (governing brief §18) that must change in step. Generating the contract from code makes the backend the de-facto owner and hides breaking changes until after implementation.

**Decision.** The OpenAPI 3.1 document in `contracts/openapi/` is written and reviewed first. Server DTOs and route constants, the web client and the mobile client are generated from it. CI lints it (naming, required extensions, error responses), checks every `x-permission` against Document 3 Appendix A, diffs it against the last release and runs a route-inventory conformance test.

**Alternatives rejected.** Code-first generation (Document 3 §11.15 text, RC-9-03): breaking changes surface late; hand-written clients: drift.

**Consequences.** API changes start with a contract pull request reviewed by all three client teams. Generated code is never edited.

### ADR-042 — Stable Error-Code Catalogue on RFC 9457

**Status:** Proposed.

**Context.** Clients branch on error codes (Document 3 §11.15). Codes were scattered across Documents 3 and 6, with conflicts (RC-9-02).

**Decision.** One catalogue (`contracts/openapi/errors/error-codes.yaml`, section 3) with an `ERR-nnn` identifier, HTTP status, code, rule reference and retryability. Every error response is RFC 9457 with `code`, `traceId` and `errors[]`. Codes are never renamed or reused; removal follows the deprecation policy.

**Alternatives rejected.** HTTP status only (insufficient for domain outcomes); per-module code lists (collisions and inconsistency).

**Consequences.** Adding a code is a contract change; clients get a closed set to handle, localised text stays out of logic.

### ADR-043 — Thin, Signed Webhooks with Pull-Back (Phase 1)

**Status:** Proposed.

**Context.** FR-NTF-010 and NFR-INTG-003 require signed webhooks. Event payloads may leave the tenant boundary to third-party systems with unknown handling.

**Decision.** Webhooks carry identifiers, status and links only, signed with HMAC-SHA256 over a timestamped body, delivered at least once with RP-C retries for 24 hours, auto-disabled after 20 consecutive failures. Receivers obtain amounts and details by calling the API with a scoped service account, so every data read is authorised and audited.

**Alternatives rejected.** Fat events with amounts (uncontrolled data egress, schema coupling); unsigned webhooks with IP allow-listing (spoofable, fails NFR-INTG-003); mutual TLS only (high receiver burden for the pilot).

**Consequences.** One extra API call per event for receivers that need details. Data access remains under the permission model and the audit trail.

---

## Document Approval

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Principal Architect | | | |
| API Lead | | | |
| Security Architect | | | |
| Frontend Lead | | | |
| Mobile Lead | | | |

*End of Document 9*
