# infra — Terraform skeleton

No cloud providers are configured yet; this module only declares **variables** and **outputs** so `fmt`, `validate`, and `plan` run in CI without credentials.

**Default story:** no remote backend and **no `terraform apply`** here — local and CI use **`-backend=false`** for init and stop at **plan**.

From this directory (`infra/`):

```bash
terraform init -backend=false
terraform validate
terraform plan
```

After changing providers or Terraform version constraints, re-run **`terraform init -backend=false`** and commit the updated **`.terraform.lock.hcl`** if you add providers later.
