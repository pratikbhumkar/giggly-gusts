# Documentation

This folder holds **design and delivery documentation** for the project (architecture, infra, CI/CD, runbooks). Keep it **next to the code** so PRs can update behaviour and docs together.

## Contents

| Document | Purpose |
|----------|---------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | System architecture, AWS/Terraform shape, resilience, observability, environments, CI/CD, and open decisions. |
| [PHASES.md](./PHASES.md) | **Phased build plan** — app **and** Terraform grow **every phase** (skeleton `infra/` → CI **.NET + Terraform** → mock API + compute-shaped IaC → artifact-backed **plan** → **Open-Meteo** + more IaC → optional **apply** → hardening). |
| [diagrams/README.md](./diagrams/README.md) | Standalone **Mermaid** diagrams (system context, caches, resilience, CI/CD, environments). |
| [design/README.md](./design/README.md) | Short design capsules (deployment strategies, links into `ARCHITECTURE.md`). |
| [design/adr/README.md](./design/adr/README.md) | **Architecture Decision Records (ADRs)** — we will add ADRs here for major settled decisions. |

## When to update

- **Change behaviour or infra** (new endpoint, new env, new alarm, toggle semantics) → update **ARCHITECTURE.md** and any matching file under **`diagrams/`** or **`design/`** (and **root `README.md`** run/build instructions if user-facing steps change).
- **Change phased delivery order or exit criteria** → update **[PHASES.md](./PHASES.md)**.
- **Close an open decision** in §18 of `ARCHITECTURE.md` → add or update an **ADR** under **`design/adr/`**, record the choice in the relevant architecture section, and trim or resolve the open item.

## Conventions

- Prefer **short sections** and **tables** over long prose.
- Cross-reference sections (e.g. **§12.4**) instead of duplicating paragraphs.
- **ADRs:** use **`design/adr/`** for durable decision history (why we chose X over Y); link from `ARCHITECTURE.md` where helpful.
