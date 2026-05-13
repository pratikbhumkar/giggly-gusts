# Phased build plan

Build in **thin vertical slices**. **Application and Terraform evolve together every phase** so you can demo **iterative DevOps**: same PR tightens app **and** infra, CI always runs **.NET + Terraform** (no `apply` in the default story unless you opt in). Cross-check [ARCHITECTURE.md](./ARCHITECTURE.md) for the target shape (Lambda, API Gateway, CloudFront, resilience, etc.).

**Principle:** each phase ends with **(1)** something new in the **app**, **(2)** something new or stricter in **`infra/`**, and **(3)** **CI** still green — **plan-only** on AWS until you explicitly add credentials for `apply`.

---

## Phase 1 — Heartbeat + Terraform skeleton

**Goal:** Runnable API **and** a real **`infra/`** tree that validates — **no AWS resources required** (no keys, no `apply`).

### Application

- Minimal host; **`GET /health`** or **`GET /`** → **200**, tiny body.
- Root **README**: `dotnet run`, `curl`.

### IaC (Terraform)

- Create **`infra/`** (or `terraform/`) with: **`versions.tf`** / **`terraform` block**, **`variables.tf`** (e.g. `project_name`, `environment`), **`outputs.tf`** (surface a value from vars).
- **Optional:** `providers.tf` with **`aws`** provider **only if** you can run **`terraform init -backend=false` + `validate` + `plan`** without credentials (empty config is fine; **avoid** resources that force credential checks during `plan` until you are ready — use **no resources** in Phase 1 if `plan` errors without keys).
- README: **`terraform init -backend=false`**, **`terraform validate`**, **`terraform plan`** from `infra/`.

### CI

- **Not required in Phase 1** (local only is OK) — CI lands in **Phase 2**.

### Done when

- [ ] App heartbeat works locally.
- [ ] From `infra/`: **`init -backend=false`**, **`validate`**, **`plan`** succeed (no backend, no secrets).
- [ ] README documents app + Terraform commands.

### Tips

- If **`plan`** insists on AWS creds because of a provider default, **defer** the AWS provider block to **Phase 3** and keep Phase 1 to **pure config** (`terraform` + vars + outputs only).

---

## Phase 2 — CI: .NET + Terraform (still no `apply`)

**Goal:** **Every PR** runs **application** quality gates **and** the same **Terraform** gates you use locally — **iterative DevOps visible in GitHub**.

**Depends on:** Phase 1.

### Application (CI)

- Workflow: checkout → setup-dotnet (pinned) → restore → **`dotnet format --verify-no-changes`** → build → test.
- **`.editorconfig`**, ≥1 real test.
- Root README: **CI** section (pinned SDK, how to run format locally).

### IaC (CI)

- Same or sibling workflow job: setup Terraform (pin version) → **`terraform fmt -check`** in `infra/` → **`init -backend=false`** → **`validate`** → **`plan`** (working directory `infra/`).
- **Optional:** cache Terraform plugin dir.

### Done when

- [ ] PR + push to **`main`** run **both** jobs; all green without secrets.
- [ ] Bad C# formatting fails CI; bad HCL `fmt` fails CI; failing test fails CI (prove on throwaway branches).

### Tips

- Use **`paths` filters** only if you split workflows later; for a small repo, **one workflow** with two jobs (`dotnet`, `terraform`) is easy to narrate in an interview.

---

## Phase 3 — Environment awareness + first AWS-shaped infra (optional app; small IaC)

**Goal:** Show **config split** (app) and **first real or “almost real”** infra increment — still **`plan` only** in CI.

**Depends on:** Phase 2.

### Application (optional)

- `appsettings.*.json`; heartbeat echoes **environment** name or similar (no secrets).

### IaC increment (pick one track)

- **Track A — no creds yet:** add **modules** layout (`infra/modules/...`) with **no** new `apply`-time dependencies; more `outputs`, `locals`, naming conventions.
- **Track B — first AWS resource:** e.g. **`aws_cloudwatch_log_group`** or **`random_id`** / **`tls_private_key`** only if your `plan` works without keys — otherwise wait until you have a **mock** or **disabled** provider pattern.

### CI

- Unchanged gates; both jobs must stay green.

### Done when

- [ ] README explains what changed this phase for **app** and **infra**.
- [ ] `terraform plan` in CI still passes with **no** `apply`.

---

## Phase 4 — Mock `GET /weather` + infra toward compute

**Goal:** Lock **weather contract** (mock) and move Terraform **toward** Lambda/API (stubs, variables, zip path variable — **full wiring can be incremental**).

**Depends on:** Phase 2; Phase 3 optional.

### Application

- **`GET /weather?city=...`** mock JSON; AU rules; tests (service / handler).

### IaC increment

- Add **IAM role** skeleton, **`aws_lambda_function`** placeholder (or zip path **`variable`** + `source_code_hash` lifecycle), **`aws_cloudwatch_log_group`** if not already — **each PR can add one logical slice** as long as **`plan`** stays valid without `apply`.
- Document in README or comment: **“not deployed yet — plan only.”**

### CI

- Still **no** `apply`. If Lambda zip is required for **valid** `plan`, add **`dotnet publish`** zip as **artifact** and pass path into **`TF_VAR_...`** for the plan job (**Phase 5** can deepen this if you split).

### Done when

- [ ] Mock weather + tests green.
- [ ] `terraform plan` shows **expected next resources** (even if “will be created” is deferred across PRs).

---

## Phase 5 — Publish package + Terraform plan uses artifact (still no `apply` by default)

**Goal:** CI **builds the deployable** (Lambda zip or container) and **Terraform plan** references it — **demo “build once, plan many”**.

**Depends on:** Phase 4 (or parallelize only if you can keep both green).

### Application

- Stabilize publish profile (RID, trimming flags) per README.

### IaC increment

- Wire **`aws_lambda_function`** `filename` / `source_code_hash` (or ECR image) to **CI-produced artifact** via **`TF_VAR`** or artifact download path.
- Add **API Gateway HTTP API** + integration **when ready** — can be **this phase or early Phase 6**; keep **one PR = one narrative slice** for demos.

### CI

- Job order example: **build app** → **upload zip artifact** → **terraform job** downloads artifact → **`plan`** with `TF_VAR_lambda_zip_path=...` (exact var names yours).

### Done when

- [ ] Plan output **changes** when zip changes (prove with two commits).
- [ ] Still **no** required AWS secrets for default CI.

### Optional

- **`terraform apply`** + smoke **`curl`** on `/health` — **explicit opt-in** when credentials exist.

---

## Phase 6 — Open-Meteo (live path) + keep infra growing

**Goal:** Live weather path + **continue** small infra PRs (alias, provisioned concurrency, stage variables — whatever your architecture doc lists next).

**Depends on:** Phase 4 mock stable.

### Application

- Open-Meteo client; timeouts; retries + jitter; fallback; faked tests.

### IaC increment (examples)

- **`aws_lambda_alias`**, **`aws_lambda_provisioned_concurrency_config`**, API stage **`throttle`** — **one or two per PR** is fine for a “iterative improvement” story.

### CI

- Same .NET + Terraform gates; **no** `apply` unless opted in.

---

## Phase 7 — Plan remains default; optional `apply` + smoke

**Goal:** Same as architecture **Phase 7** narrative: default remains **plan**; **apply** + smoke only with GitHub Environment + credentials.

**Depends on:** Phase 5–6 maturity.

### Done when

- [ ] Documented path for **`apply`** + smoke; **or** explicit “we stay plan-only for take-home.”

---

## Phase 8+ — Edge, observability, blue/green, multi-env, ADRs

**Goal:** CloudFront, AU geo, correlation id polish, metric filters / OTel, **`env/*.tfvars`**, blue/green, ADRs — **each** as small PRs; **Terraform and app keep moving together**.

**Rule**

- Each PR: **.NET** (`dotnet format`, build, test) + **Terraform** (`fmt`, `validate`, `plan`) green.
- **`terraform apply`**: only protected workflow + credentials **you** control.

---

## Summary

| Phase | Application | IaC (evolving) | CI |
|-------|-------------|----------------|-----|
| **1** | Heartbeat | `infra/` skeleton, `validate` / `plan` local | — |
| **2** | (same) | (same) | **.NET** + **Terraform** gates, no `apply` |
| **3** | Optional env echo | First modules / first AWS-ish resource if `plan` allows | Both green |
| **4** | Mock `/weather` | Toward Lambda / IAM / logs | Both green |
| **5** | Publish profile | Plan consumes **artifact**; API GW when ready | Both green |
| **6** | Open-Meteo + resilience | Alias, PC, throttles, … incremental | Both green |
| **7** | (stabilize) | Optional **`apply`** + smoke | Documented |
| **8+** | Hardening | CloudFront, multi-env, blue/green, alarms | Both green |

**Sequencing**

- **Mock weather before Open-Meteo** (Phase 4 before Phase 6) unless you accept joint debugging.
- **Terraform never “waits until the end”** — it **grows with** the app so interviewers see **continuous** DevOps iteration.

Update this file when you change phase boundaries or add demo-specific steps.
