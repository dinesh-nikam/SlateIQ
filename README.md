# SlateIQ — Production Cost Intelligence & Audit Monitoring Platform

> **"Intelligence Behind Every Production"**

SlateIQ is a centralized, intelligent production cost monitoring and audit management platform enabling OTT platforms, television broadcasters, film production houses, and independent audit firms to monitor production expenses, standardize audit operations, detect cost overruns early, and guarantee cryptographic audit integrity.

---

## Architecture Foundation

The platform is designed to enterprise production standards across 18 governing architecture deliverables located in [`architecture/`](./architecture/):

- **Master Index & Blueprint:** [`00-foundation-index.md`](./architecture/00-foundation-index.md)
- **Requirements & Scope:** [`01-product-vision-business-requirements.md`](./architecture/01-product-vision-business-requirements.md)
- **Domain Model & Capabilities:** [`02-domain-model-business-capabilities.md`](./architecture/02-domain-model-business-capabilities.md)
- **Tenancy, Identity & RBAC:** [`03-actor-permission-architecture.md`](./architecture/03-actor-permission-architecture.md)
- **Workflows & State Machines:** [`04-workflows-state-machines.md`](./architecture/04-workflows-state-machines.md)
- **Business Rules & Invariants:** [`05-business-rules-invariants.md`](./architecture/05-business-rules-invariants.md)
- **High-Level Solution Architecture:** [`06-high-level-solution-architecture.md`](./architecture/06-high-level-solution-architecture.md)
- **Module & Dependency Boundaries:** [`07-module-dependency-architecture.md`](./architecture/07-module-dependency-architecture.md)
- **Database Conceptual Model & ERD:** [`08-database-conceptual-model-erd.md`](./architecture/08-database-conceptual-model-erd.md)
- **API Architecture & Catalogue:** [`09-api-architecture-endpoint-catalogue.md`](./architecture/09-api-architecture-endpoint-catalogue.md)
- **Security Architecture:** [`10-security-architecture.md`](./architecture/10-security-architecture.md)
- **Mobile & Offline Architecture:** [`11-mobile-offline-architecture.md`](./architecture/11-mobile-offline-architecture.md)
- **Excel Integration Architecture:** [`12-excel-integration-architecture.md`](./architecture/12-excel-integration-architecture.md)
- **Analytics & Cost Calculation:** [`13-analytics-cost-calculation.md`](./architecture/13-analytics-cost-calculation.md)
- **Audit & Observability:** [`14-audit-observability-architecture.md`](./architecture/14-audit-observability-architecture.md)
- **Infrastructure & Deployment:** [`15-infrastructure-deployment-architecture.md`](./architecture/15-infrastructure-deployment-architecture.md)
- **Testing Strategy (TS-01..42):** [`16-testing-strategy.md`](./architecture/16-testing-strategy.md)
- **Risk Register & ADRs (ADR-001..067):** [`17-risk-register-adrs.md`](./architecture/17-risk-register-adrs.md)
- **Phased Implementation Roadmap:** [`18-phased-implementation-roadmap.md`](./architecture/18-phased-implementation-roadmap.md)

---

## Monorepo Layout

```
.
├── architecture/      # 18 Governing Architecture Documents (SLQ-ARCH-000..018)
├── contracts/         # Contract-First Specifications
│   ├── errors/        # Canonical API error code registry (registry.yaml)
│   ├── roles/         # 18 system roles, 7 scopes, permission dictionary
│   ├── events/        # Transactional outbox JSON schemas (Draft-07)
│   └── openapi/       # OpenAPI 3.1 RESTful contracts (/api/v1)
├── backend/           # Modular Monolith (.NET 10 LTS Minimal APIs)
│   ├── src/           # Domain, Application, Infrastructure, API host profiles
│   └── tests/         # Unit, Integration, and Isolation test suites
├── web/               # Responsive Web Application (React 19 + Vite + Tailwind)
├── mobile/            # Auditor Field Mobile App (React Native + Expo + SQLCipher)
├── infra/             # Infrastructure as Code & Container Setup
│   ├── docker/        # PostgreSQL 17 init scripts, local service configs
│   └── terraform/     # Multi-account AWS Terraform modules
├── docker-compose.yml # Local development infrastructure stack
└── package.json       # Monorepo workspaces and developer tasks
```

---

## Local Development Prerequisites

1. **Docker & Docker Compose** (for local PostgreSQL 17, Redis 7.4, and MinIO S3)
2. **.NET 10 LTS SDK** (for backend services and migrator)
3. **Node.js (>= 20.0.0) & npm** (for web, mobile, and contract tooling)

---

## Quickstart

### 1. Initialize Environment
Copy the development environment template:
```bash
cp .env.example .env
```

### 2. Start Local Infrastructure Stack
Launch PostgreSQL 17, Redis 7.4, and MinIO S3:
```bash
npm run infra:up
```
- **PostgreSQL 17:** `localhost:5432` (database: `slateiq`, user: `postgres`)
- **Redis 7.4:** `localhost:6379` (password protected)
- **MinIO S3 Console:** `http://localhost:9001` (user: `slateiq_admin`, password: `slateiq_local_s3_secret`)

### 3. Stop Local Services
```bash
npm run infra:down
```

---

## Core Invariants

1. **Fixed-Point Financial Storage:** Stored money is strictly `numeric(19,4)` and exchange rates `numeric(19,6)`. Zero floating-point data types.
2. **Half-Up Rounding:** All rounding uses `MidpointRounding.AwayFromZero` at line amount, tax, converted amount, and total boundaries (RC-01).
3. **Forced Row-Level Security:** Every tenant-owned table carries `org_id uuid not null` with PostgreSQL `FORCE ROW LEVEL SECURITY`.
4. **Append-Only Cost Postings:** Actual production expenses post append-only to `expense.cost_posting`. Reversals generate compensating postings.
5. **Zero Production Data in Non-Prod:** Staging, QA, and local environments exclusively use mathematically generated synthetic datasets (ADR-067).
