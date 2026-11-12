# SlateIQ — Project Notes

## Standing constraint C-01 (business owner, 2026-11-12; ADR-068 accepted)

- Building and evaluating the complete application must cost nothing: no paid licences, no paid build service, and a free single-host evaluation profile (Docker Compose: PostgreSQL 17, Valkey, MinIO, ClamAV, Caddy).
- Production is paid and managed: AWS managed profile in the tenant's home region (India: `ap-south-1`). iOS and Android ship through the App Store and Play Store; those fees are accepted.
- Keep every dependency open source and reachable through a standard protocol (PostgreSQL wire, Redis protocol, S3 API, SMTP, OTLP) so the same images run on both profiles. Never add a paid SaaS or proprietary SDK as a build-time dependency.
- Records: `architecture/00-foundation-index.md` §8, `architecture/15-infrastructure-deployment-architecture.md` §14A.

## Architecture set

`architecture/` holds the 18 foundation documents plus `00-foundation-index.md` (start here). `_archive/` is the rollback path; archive a file there before replacing it. Document 17's registers are generated from Documents 1–16; do not hand-edit them except the ADR-068 and C-01 rows.
