# infra — Terraform skeleton (Phase 3 structure)

No cloud providers are configured yet; configuration uses **variables**, **locals**, and **outputs** only so `fmt`, `validate`, and `plan` run in CI **without AWS credentials**.

Layout:

- **`main.tf`** — wires the **`modules/naming`** child module (variables, locals, outputs only; no `resource` blocks).
- **`modules/naming/`** — shared **name prefix**, **standard tag map**, and **planned resource name strings** for Phase 4+ (Lambda, logs, ECR, API Gateway).

**Default story:** no remote backend and **no `terraform apply`** — local and CI use **`-backend=false`** for init and stop at **plan**.

From this directory (`infra/`):

```bash
terraform init -backend=false
terraform validate
terraform plan
```

After adding providers or changing version constraints, re-run **`terraform init -backend=false`** and commit **`.terraform.lock.hcl`** when introduced.
