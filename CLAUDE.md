# SlateIQ — Project Notes

## Standing constraint C-01 (business owner, 2026-11-12)

The complete application must be buildable and deployable free of charge: no paid licences, no paid build service, and a hosting option with no recurring charge.

- Keep every dependency open source and reachable through a standard protocol (PostgreSQL wire, Redis protocol, S3 API, SMTP, OTLP) so the same images run on the zero-cost profile (Docker Compose: PostgreSQL 17, Valkey, MinIO, ClamAV, Caddy) and the managed AWS profile.
- Do not add a paid SaaS, proprietary SDK or managed-only service without a free, self-hostable equivalent.
- Known limits at zero cost: iOS distribution (Apple fee), NFR-AVL targets, managed key custody, Object Lock compliance mode.
- Architecture record: `architecture/00-foundation-index.md` §8, ADR-068 (provisional), open questions OQ-0-01 to OQ-0-03.

## Architecture set

`architecture/` holds the 18 foundation documents plus `00-foundation-index.md` (start here). `_archive/` is the rollback path; archive a file there before replacing it. Document 17's registers are generated from Documents 1–16; do not hand-edit them.
