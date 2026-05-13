# Phased build plan

Build in **thin vertical slices**. **Phase 1 is already done** (heartbeat only, no Terraform). **From Phase 2 onward**, **application and Terraform evolve together** so you can demo **iterative DevOps**: same PR tightens app **and** infra; CI runs **.NET + Terraform** (no `apply` in the default story unless you opt in). Cross-check [ARCHITECTURE.md](./ARCHITECTURE.md) for the target shape (Lambda, API Gateway, CloudFront, resilience, etc.).

**Principle:** **Phase 2+** each phase ends with **(1)** something new in the **app** (or unchanged if that phase is infra-heavy), **(2)** something new or stricter in **`infra/`**, and **(3)** **CI** still green — **plan-only** on AWS until you explicitly add credentials for `apply`.

---

## Phase 1 — Heartbeat only (**already committed**)

**Goal:** Prove the ASP.NET Core host starts and responds over HTTP — **nothing else**. This phase is **done** in your repo baseline; do not retroactively add Terraform here.

### Application

- Minimal host; **`GET /health`** or **`GET /`** → **200**, tiny body.
- Root **README**: `dotnet run`, `curl`.

### IaC / CI

- **Out of scope** — no **`infra/`**, no GitHub Actions for this slice.

### Done when

- [x] `dotnet run` serves the heartbeat; `curl -i` shows **200** and expected body.

### Tips

- Treat Phase 1 as **frozen history**; all **DevOps iteration** (Terraform + CI) starts in **Phase 2**.

---

## Phase 2 — Terraform skeleton + CI: .NET + Terraform (still no `apply`)

**Goal:** Land **`infra/`** and **GitHub Actions** so **every PR** runs **application** quality gates **and** **Terraform** gates — first step of **iterative infra** on top of the committed heartbeat.

**Depends on:** Phase 1 (heartbeat already in `main` or your trunk).

### Application (CI)

- Workflow: checkout → setup-dotnet (pinned) → restore → **`dotnet format --verify-no-changes`** → build → test.
- **`.editorconfig`**, ≥1 real test.
- Root README: **CI** section (pinned SDK, how to run format locally).

### IaC (new this phase)

- Create **`infra/`** (or `terraform/`) with: **`versions.tf`** / **`terraform` block**, **`variables.tf`** (e.g. `project_name`, `environment`), **`outputs.tf`** (surface a value from vars).
- **Optional:** `providers.tf` with **`aws`** provider **only if** **`terraform init -backend=false` + `validate` + `plan`** work **without** credentials (empty config is fine; **avoid** resources that force credential checks during `plan` until you are ready — use **no resources** first if `plan` errors without keys).
- README: **`terraform init -backend=false`**, **`terraform validate`**, **`terraform plan`** from `infra/` (same doc as CI section).

### IaC (CI)

- Same or sibling workflow job: setup Terraform (pin version) → **`terraform fmt -check`** in `infra/` → **`init -backend=false`** → **`validate`** → **`plan`** (working directory `infra/`).
- **Optional:** cache Terraform plugin dir.

### Done when

- [ ] PR + push to **`main`** run **both** jobs; all green without secrets.
- [ ] From `infra/` locally: **`init -backend=false`**, **`validate`**, **`plan`** succeed (no backend, no secrets).
- [ ] Bad C# formatting fails CI; bad HCL `fmt` fails CI; failing test fails CI (prove on throwaway branches).

### Tips

- If **`plan`** insists on AWS creds because of a provider default, **defer** the AWS provider block to **Phase 3** and keep Phase 2 to **pure config** (`terraform` + vars + outputs only).
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

**Goal:** Lock **weather contract** (mock) and move Terraform **toward** Lambda/API (stubs, **container image** **`variable`** — **full wiring can be incremental**).

**Depends on:** Phase 2; Phase 3 optional.

### Application

- **`GET /weather?city=...`** mock JSON; AU rules; tests (service / handler).

### IaC increment

- Add **IAM role** skeleton, **`aws_lambda_function`** placeholder (container **`image_uri`** **`variable`** or stub), **`aws_cloudwatch_log_group`** if not already — **each PR can add one logical slice** as long as **`plan`** stays valid without `apply`.
- Document in README or comment: **“not deployed yet — plan only.”**

### CI

- Still **no** `apply`. If **`plan`** needs a concrete image reference, pass **`TF_VAR_...`** from CI (see **Phase 5** for **Docker / ECR**).

### Done when

- [ ] Mock weather + tests green.
- [ ] `terraform plan` shows **expected next resources** (even if “will be created” is deferred across PRs).

---

## Phase 5 — Docker image, ECR, Lambda (container) + Terraform `plan` (still no `apply` by default)

**Goal:** CI **builds a runnable container image** of the API, **pushes it to Amazon ECR** when you allow AWS credentials in the pipeline, and **Terraform plans** a **Lambda deployed as a container image** (plus wiring toward **API Gateway**) — **demo “build once, plan many”** on the **Docker → ECR → Lambda** path.

**Depends on:** Phase 4 (or parallelize only if you can keep both green).

### Application

- **`Dockerfile`** (multi-stage is fine): **build** and **run** the ASP.NET Core app inside the image; document **`docker build`** / **`docker run`** locally (ports, `/health`).
- Keep **`dotnet build` / `dotnet test`** in CI on the solution — the image build **proves** the same code ships in the container Lambda will run.

### IaC increment

- **`aws_ecr_repository`** (and sensible **lifecycle** / **image tag** hygiene if you add them).
- **`aws_lambda_function`** with **`package_type = "Image"`** and **`image_uri`** (or equivalent) coming from a **Terraform variable** (e.g. digest or `repo_url:tag` from CI via **`TF_VAR_...`**).
- **IAM** for Lambda execution role (logs, minimal outbound HTTPS for later Open-Meteo).
- Add **API Gateway HTTP API** + **Lambda integration** **when ready** — can be **this phase or early Phase 6**; keep **one PR = one narrative slice** for demos.

### CI

- Typical order: **restore / format / build / test (.NET)** → **`docker build`** (tag with **`GITHUB_SHA`** or similar) → **Terraform** **`fmt` / `validate` / `plan`** with **`TF_VAR_container_image`** (or your variable name) set to the image reference Terraform should plan against.
- **Push to ECR:** requires **AWS auth** in that job (OIDC or narrow IAM). For **plan-only** PRs, you may **skip push** and still pass a **stable placeholder** image URI into **`plan`** so CI stays green **without** secrets — **document** which mode you use; real **`apply`** always uses an image that **exists** in ECR.

### Done when

- [ ] **`docker build`** succeeds in CI on **`main`** / PRs.
- [ ] **`terraform plan`** shows the **Lambda (Image)** + ECR-related changes you expect, and **changes** when the **passed-in image reference** changes (prove with two commits).
- [ ] Default story: **no** required AWS secrets **or** a clearly documented **optional** job that pushes + plans with real URIs.

### Optional

- **`terraform apply`** + smoke **`curl`** on **`/health`** (and **`/weather`** when it exists) — **explicit opt-in** when credentials and ECR push are enabled.

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
| **1** | Heartbeat only (**committed**) | — | — |
| **2** | (same + format/test in CI) | **`infra/`** skeleton; local + CI **`fmt` / `validate` / `plan`** | **.NET** + **Terraform**, no `apply` |
| **3** | Optional env echo | First modules / first AWS-ish resource if `plan` allows | Both green |
| **4** | Mock `/weather` | Toward Lambda / IAM / logs | Both green |
| **5** | Dockerfile + **Docker build** in CI | **ECR** + **Lambda (`package_type = Image`)** in Terraform; **`plan`** uses image ref; API GW when ready | Both green |
| **6** | Open-Meteo + resilience | Alias, PC, throttles, … incremental | Both green |
| **7** | (stabilize) | Optional **`apply`** + smoke | Documented |
| **8+** | Hardening | CloudFront, multi-env, blue/green, alarms | Both green |

**Sequencing**

- **Mock weather before Open-Meteo** (Phase 4 before Phase 6) unless you accept joint debugging.
- **Terraform starts in Phase 2** (after the committed heartbeat) and **grows with** the app so interviewers see **continuous** DevOps iteration.

Update this file when you change phase boundaries or add demo-specific steps.
