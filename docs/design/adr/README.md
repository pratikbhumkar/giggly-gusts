# Architecture Decision Records (ADRs)

We will add **ADRs** in this folder as **Markdown** files (`0001-title.md`, `0002-title.md`, …) to record important decisions: **context**, **options considered**, **decision**, **consequences**, and optional **status** (proposed / accepted / superseded).

- **Trigger:** Closing an item in [ARCHITECTURE.md §18](../../ARCHITECTURE.md) or any material trade-off (stack, deploy model, observability, security boundary).
- **Process:** Open a PR with the ADR; merge when accepted; update [ARCHITECTURE.md](../../ARCHITECTURE.md) to match and link to the ADR where useful.

## Index

| ADR | Title |
|-----|--------|
| [0001-lambda-container-compute.md](./0001-lambda-container-compute.md) | Run the API on **AWS Lambda** (**container image** from **ECR**), API Gateway, Terraform **plan-first** CI |
