# infra — Terraform (Phase 4+)

Root module wires **`modules/naming`**, the **`aws`** provider, and a **thin compute slice** toward the Lambda path described in **[`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md)** (container image on Lambda, logs, execution role).

**Terraform CLI version:** GitHub Actions pins **Terraform 1.10.5** (see [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) `hashicorp/setup-terraform`). Match that locally for consistent **`fmt`** / lockfile behaviour.

## Layout

- **`main.tf`** — naming module and shared wiring.
- **`providers.tf`** — `provider "aws"`; when **`var.use_localstack`** is **true**, dummy credentials and service **endpoints** point at LocalStack (CI), so **`plan`** does not need real **AWS** keys.
- **`compute.tf`** — **`aws_iam_role`** (Lambda assume role), **`aws_iam_role_policy_attachment`** (AWSLambdaBasicExecutionRole), **`aws_cloudwatch_log_group`**, **`aws_lambda_function`** with **`package_type = "Image"`** and **`image_uri = var.container_image`** (set **`TF_VAR_container_image`** in automation or use the default placeholder image URI). Phase 6 additions: **`publish = true`** + **`aws_lambda_alias "live"`** (so API Gateway / clients target a stable qualifier) and **`aws_lambda_provisioned_concurrency_config`** that is created only when **`var.provisioned_concurrency_count > 0`** (default `0` to keep `plan` honest in the take-home). The Lambda environment is populated from the Phase 6 variables (**`var.use_open_meteo`**, **`var.maintenance_mode`**, **`var.weather_http.*`**, **`var.open_meteo_base_url`**) using the **`Weather__Foo__Bar`** key convention so the .NET `IConfiguration` binder picks them up.
- **`ecr.tf`** — **`aws_ecr_repository`** for the API image (scan-on-push, AES256) plus an **`aws_ecr_lifecycle_policy`** that expires untagged images after 14 days and caps retained images at 20. Tag mutability is **MUTABLE** for iteration; switch to **IMMUTABLE** before any real deploy and pin Lambda **`image_uri`** to a `@sha256:<digest>` so the function references a single, immutable artifact.
- **`variables.tf`**, **`outputs.tf`**, **`versions.tf`** — configuration surface and provider pins.
- **`.terraform.lock.hcl`** — commit this after **`terraform init`** so CI resolves the same provider builds.

## Default story

No remote backend in-repo and **no `terraform apply`** in the **default** GitHub Actions workflow: CI runs **`init -backend=false`**, **`validate`**, and **`plan`** only.

## Local commands (from `infra/`)

```bash
terraform fmt -recursive
terraform init -backend=false -input=false
terraform validate
```

**Plan against real AWS:** configure the normal provider (do **not** set **`use_localstack`**) and ensure AWS credentials are available; **`terraform plan`**.

**Plan like CI (LocalStack):** run LocalStack with **`SERVICES=iam,lambda,logs,sts,ecr`**, then for example:

```bash
export TF_VAR_use_localstack=true
export TF_VAR_localstack_endpoint=http://127.0.0.1:4566
export TF_VAR_container_image=public.ecr.aws/lambda/dotnet:8
terraform init -backend=false -input=false
terraform plan -input=false
```

After changing provider constraints, re-run **`terraform init -backend=false`** and commit **`.terraform.lock.hcl`** when it changes.
