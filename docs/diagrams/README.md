# Diagrams

Standalone **Mermaid** diagrams for the weather API delivery design. GitHub renders Mermaid inside `.md` files on view.

| File | Topic |
|------|--------|
| [01-system-context.md](./01-system-context.md) | Viewer → edge → Lambda → Open-Meteo / fallback |
| [02-caching-layers.md](./02-caching-layers.md) | HTTP / CDN / gateway / in-process cache stack |
| [03-open-meteo-resilience.md](./03-open-meteo-resilience.md) | Circuit breaker, retries, fallback |
| [04-ci-cd-stages.md](./04-ci-cd-stages.md) | Build → test → package → IaC → optional deploy |
| [05-terraform-environments.md](./05-terraform-environments.md) | `env/*.tfvars` and separate state keys |

**Edit workflow:** When [ARCHITECTURE.md](../ARCHITECTURE.md) changes a diagram, update the matching file here (or vice versa) so they stay aligned.

**ADRs:** When a diagram reflects a **settled** architectural choice, we will add or update an **Architecture Decision Record** under [../design/adr/](../design/adr/) and reference it from the diagram or from `ARCHITECTURE.md` as appropriate.
