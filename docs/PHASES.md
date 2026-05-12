# Phased build plan

Build in **thin vertical slices**. Finish each phase before starting the next unless you explicitly split work across people. Cross-check details with [ARCHITECTURE.md](./ARCHITECTURE.md) (resilience, flags, correlation id, CDN, multi-env).

---

## Phase 1 — Heartbeat only

**Goal:** Prove the ASP.NET Core host starts and responds over HTTP — **nothing else**.

**Depends on:** repo + SDK installed locally.

**Deliverables**

- Minimal web host (Minimal APIs or controllers — pick one and keep it).
- **`GET /health`** or **`GET /`** returning **HTTP 200** and a tiny payload (JSON like `{ "status": "ok" }` or plain `Healthy`).
- **`README.md`** (repo root): how to **`dotnet run`** and **`curl`** the heartbeat.

**Out of scope**

- Weather, Open-Meteo, Terraform, AWS, auth, CloudFront, correlation id, feature flags.

**Done when**

- [ ] `dotnet run` from repo root (or documented project path) serves the heartbeat.
- [ ] `curl -i` shows **200** and expected body.
- [ ] You can explain the project entrypoint in one sentence (interview-ready).

**Tips**

- Keep **Program.cs** (or equivalent) small; avoid pulling in every NuGet “for later.”
- If you already know you will deploy to Lambda, still **defer** Lambda packaging until Phase 7 — Phase 1 is host-only.

---

## Phase 2 — Basic CI (GitHub Actions)

**Goal:** **Every PR** runs the same checks your machine runs — no AWS, no Terraform.

**Depends on:** Phase 1 complete (something to build and test).

**Deliverables**

- `.github/workflows/ci.yml` (or split files later) triggered on **`pull_request`** and **`push`** to **`main`** (adjust branches to your branching model).
- Job steps: checkout → **setup-dotnet** (pin SDK version to match `global.json` / repo) → **restore** → **build** → **test**.
- At least **one** non-empty test (e.g. asserts heartbeat route exists or a trivial unit test) so **`dotnet test`** is meaningful.
- **Optional:** `actions/cache` for NuGet (`~/.nuget/packages`).

**Done when**

- [ ] CI passes on a PR touching only app code.
- [ ] Failing test fails CI (verify once with a throwaway branch).
- [ ] No secrets required for the workflow.

**Tips**

- Pin **.NET version** explicitly in the workflow to avoid “works on my machine” drift.
- Add **`dotnet format`** or analyzers later (Phase 8+); not required here.

---

## Phase 3 — Environment awareness (optional but small)

**Goal:** Same binary, **different config** per environment — still heartbeat-sized.

**Depends on:** Phase 2 (so CI exercises env in a repeatable way).

**Deliverables**

- `appsettings.json` + **`appsettings.Development.json`** / **`appsettings.Production.json`** (or only Development if that is enough).
- Heartbeat or logs expose **`IHostEnvironment.EnvironmentName`** (or safe subset) — **no secrets** in response.
- Document in **README** how CI sets **`DOTNET_ENVIRONMENT`** / **`ASPNETCORE_ENVIRONMENT`** if you set it in the workflow.

**Done when**

- [ ] Running locally with `Development` vs `Production` shows a **visible, intentional** difference (response field or log line).
- [ ] CI runs with **`Development`** (typical) and still passes.

**Tips**

- Skip this phase entirely if it slows you down; merge into Phase 1–2 only if you need it for demos.

---

## Phase 4 — `GET /weather` (mock)

**Goal:** Lock the **HTTP contract** and **validation** without network flakiness.

**Depends on:** Phase 1–2; Phase 3 optional.

**Deliverables**

- **`GET /weather?city={city}`** returning JSON aligned with architecture: at minimum **`city`**, **`tempC`**, **`condition`**, and any fields you already committed to (e.g. **`source`**, **`correlationId`** — if not yet, add them in Phase 8+ or here if stable).
- **AU-only** rules as per architecture (allowlist or validation — document which).
- **ProblemDetails** or consistent **400** body for bad input; **`Cache-Control: no-store`** on errors if you set headers this early.
- **Unit tests** on the service or mapper that builds the response (happy path + invalid city + at least one edge case you care about).

**Out of scope**

- Open-Meteo, retries, circuit breaker, Terraform changes beyond “app still builds.”

**Done when**

- [ ] Mock response matches the documented contract.
- [ ] **`dotnet test`** green in CI.
- [ ] You can demo **`curl`** `/weather?city=Melbourne` without any external API.

**Tips**

- Put mock logic behind **`IWeatherProvider`** (or similar) so Phase 6 swaps implementation without rewriting the endpoint.
- Keep **normalization** (`Trim`, case rules) consistent — you will reuse in Phase 6.

---

## Phase 5 — Terraform in CI (no apply required)

**Goal:** Infrastructure is **versioned**, **formatted**, **valid**, and **planned** like application code — **no AWS credentials** in the default pipeline.

**Depends on:** Phase 2 (CI exists); app code from Phase 4 can evolve in parallel but merge conflicts are easier if `infra/` lands when the repo is already green.

**Deliverables**

- `infra/` or `terraform/` with at least: **variables**, **outputs**, and skeleton resources (Lambda + API Gateway stub is enough to start; align with [ARCHITECTURE.md](./ARCHITECTURE.md)).
- CI job (same or new workflow): install Terraform → **`terraform fmt -check`** → **`init -backend=false`** → **`validate`** → **`plan`** (no `apply`).
- Document in **README**: how to run the same commands locally without keys.

**Done when**

- [ ] PR comments or logs show a **non-empty plan** or a deliberate “no changes” plan after first merge.
- [ ] `fmt -check` fails CI when someone misformats HCL (verify once on a throwaway commit).
- [ ] Repo remains **clone-and-plan** without AWS access.

**Tips**

- Start with **fewer resources** and expand; a huge first PR is harder to review.
- Match **resource names** to `name_prefix` / `environment` variables early — multi-env in Phase 8+ is easier.

---

## Phase 6 — Open-Meteo (live weather data)

**Goal:** Same route as Phase 4, but **live path** calls **Open-Meteo** and maps into the **same** success shape; failures degrade predictably.

**Depends on:** Phase 4 (contract + tests); Phase 5 can proceed in parallel but merge **Open-Meteo after** mock is stable to avoid debugging two unknowns.

**Deliverables**

- **`HttpClient`** (or `IHttpClientFactory` named client) with **base URL** from configuration; **HTTPS** only.
- **Geocoding + forecast** (or single Open-Meteo API flow you chose in architecture): parse JSON, map to **`tempC`** / **`condition`**; handle **malformed** payload.
- **Resilience (minimum):** per-request **timeout**; **retries with exponential backoff + jitter** on transient errors only; **circuit breaker** optional in this phase if time is tight — but **fallback** when live path gives up is strongly recommended.
- **Fallback:** AU monthly typical data (or documented mock) with **`source: fallback`** (or equivalent) when Open-Meteo fails or circuit is open.
- **Tests:** **`HttpMessageHandler`** fakes for **200 success**, **timeout**, **502/503**, **garbage JSON**; assert **`source`**, status codes, and no unhandled exceptions.

**Out of scope (Phase 8+)**

- CloudFront, AU geo at edge, full OTel, blue/green, `terraform apply`, multi-account OIDC.

**Done when**

- [ ] Local run: **`source: live`** against a real city when Open-Meteo is reachable.
- [ ] Local run (or test): **`source: fallback`** when handler simulates outage.
- [ ] CI green **without** depending on Open-Meteo uptime (all network in tests faked or strictly optional behind a flag not used in CI).

**Tips**

- Log **`correlationId`** / attempt count **before** you add CloudWatch metric filters (Phase 8+).
- If CI must stay hermetic, use **`USE_OPEN_METEO=false`** in test configuration only — document the split.

---

## Phase 7 — Infra “deploy” path (plan-only by default)

**Goal:** CI produces an **artifact** and Terraform **plan** shows how that artifact would land in AWS — **real `apply` optional**.

**Depends on:** Phase 5–6 (Lambda code shape stable enough to package); strictly you can package a heartbeat-only app in Phase 7 and add weather in Phase 6 in parallel **if** your team coordinates — linear order above is safer.

**Deliverables**

- **`dotnet publish`** for **Lambda** RID / layout your Terraform `aws_lambda_function` expects (zip path wired via variable or CI artifact upload).
- CI: upload **artifact** → Terraform job consumes it → **`terraform plan`** with **`-backend=false`** (or read-only backend) so plan runs in PRs **without** secrets.
- **README:** exact command sequence for maintainers; what is **not** run in CI (`apply`).

**Optional stretch (explicit opt-in)**

- GitHub **Environment** `dev`, AWS credentials, **`terraform apply`**, smoke **`curl`** on **`/health`** then **`/weather`**.

**Done when (default)**

- [ ] CI publishes **artifact** and completes **`terraform plan`** successfully on `main` / PRs.
- [ ] Plan output is readable (resource names, counts, no secrets in stdout).
- [ ] Repo still **does not require** AWS keys for contributors running build/test/plan locally.

**Done when (optional apply path)**

- [ ] One environment returns **200** from deployed **`/health`** and **`/weather`** (mock or live per config).

**Tips**

- First successful **`apply`** should target **smallest** resource set (Lambda + HTTP API + role + log group) before CloudFront.
- After optional **`apply`**, add **smoke** step only if URL is stable and non-secret.

---

## Phase 8+ — Iterative product and hardening

**Goal:** Close gaps to **architecture doc** and production habits — **small PRs**, each keeping CI green.

**Depends on:** Phases 1–7 baseline; order below is a **menu**, not a strict sequence.

**Typical work items**

| Area | Examples |
|------|-----------|
| **Open-Meteo path** | Circuit breaker tuning, structured logs per attempt, metric filters on log lines |
| **API surface** | **`correlationId`** + **`X-Correlation-Id`**, ProblemDetails consistency, `Cache-Control` |
| **Flags** | **`USE_OPEN_METEO`**, **`MAINTENANCE_MODE`** wired from env / Terraform |
| **Edge** | CloudFront, AU geo restriction, CDN vs `correlationId` body cache decision |
| **Multi-env** | `env/dev.tfvars`, `env/prod.tfvars`, separate state keys |
| **Deploy** | Blue/green alias weights or CodeDeploy; optional `apply` to staging |
| **Observability** | Dashboards, alarms, SNS; OTel per architecture §11 |
| **Governance** | **ADRs** in `docs/design/adr/` for major choices |

**Rule**

- Each PR: **`dotnet` build + test** green; **`terraform fmt -check` / `validate` / `plan`** green where infra touched.
- **`terraform apply`**: only from protected workflow + credentials you control.

---

## Summary

| Phase | Focus |
|-------|--------|
| **1** | Heartbeat only |
| **2** | GitHub Actions build + test |
| **3** | Environment config (small, optional) |
| **4** | Mock `GET /weather` + tests |
| **5** | Terraform fmt / validate / plan in CI |
| **6** | Open-Meteo live path + timeouts / retries / fallback + faked tests |
| **7** | Package artifact + **`terraform plan`** (optional **`apply`** + smoke) |
| **8+** | Edge, flags, correlation id polish, multi-env, blue/green, OTel/metrics, ADRs |

**Sequencing reminders**

- Keep **Phase 4 mock before Phase 6 live** unless you want to debug contract + HTTP client together.
- **Phase 7** can start when packaging is clear; **live Open-Meteo** does not require **Phase 7** to be finished first.
- If you add **real `apply`**, prove **`/health`** in the cloud **before** layering CloudFront + geo on the same URL.

Adjust numbering if you merge optional phases; update this file when you change exit criteria.
