# ADR 0001: Run the API on AWS Lambda (container image from ECR)

## Record metadata

| Field | Value |
|-------|--------|
| **ADR status** | **Accepted** — describes the **current** compute and packaging decision for this repository. |
| **Date** | 2026-05-12 |
| **Deciders** | Project owner / take-home author |
| **Target** | **Primary HTTP request handling** for the weather-style API: **AWS Lambda** (**.NET 8**), **packaged as a container image** in **Amazon ECR**, invoked from **Amazon API Gateway (HTTP API)**, described and deployed with **Terraform**, built and tested in **GitHub Actions** with **`terraform plan`** as the **default** CI proof. |
| **In scope** | Choice of **compute service**, **packaging format** (zip vs image), **ingress pattern** (API Gateway vs ALB vs managed PaaS), **VPC vs no-VPC** for v1, and **CI implications** (plan-first, optional push/apply). |
| **Out of scope** | Open-Meteo client design, full observability stack, CloudFront/geo, blue/green runbooks — see **Follow-up** and [ARCHITECTURE.md](../../ARCHITECTURE.md). |
| **Supersedes** | — |
| **Superseded by** | — *(when replaced, link the new ADR here and set this file’s ADR status to **Superseded**.)* |
| **Related docs** | [ARCHITECTURE.md](../../ARCHITECTURE.md), [PHASES.md](../../PHASES.md) |

### Legend — per-option **status**

| Status | Meaning |
|--------|--------|
| **Accepted** | What we implement and defend in review. |
| **Rejected** | Considered; **not** doing for this repo **at this time**. |
| **Deferred** | Reasonable for a **future** phase or a **different** product; not committed now. |
| **Reference** | Pattern we **do not** adopt but document so reviewers know it was not ignored. |

---

## Context

We are building a **.NET 8** weather-style **HTTP API** on **AWS**, with **Terraform** for infrastructure and **GitHub Actions** for **plan-first** CI. The product needs a **credible, interview-ready** compute story: minimal undifferentiated heavy lifting, clear integration with **API Gateway**, and a path to **Open-Meteo** over HTTPS from application code.

We need to choose **where the request-handling process runs** and **how we package** it for deployment.

---

## Decision (summary)

1. **Primary compute:** **AWS Lambda**.
2. **Packaging:** **Lambda container image** from **Amazon ECR** (`package_type = Image`), built from a repo **`Dockerfile`**, **`image_uri`** supplied to Terraform (prefer **immutable** digest or pinned tag for real deploys).
3. **Ingress:** **API Gateway HTTP API** → Lambda (other edge in [ARCHITECTURE.md](../../ARCHITECTURE.md)).
4. **Networking:** **No VPC** attachment for Lambda in **v1**.
5. **CI/CD:** default **`docker build`** + **`terraform plan`** on typical PRs; **ECR push** and **`terraform apply`** only in **documented optional** paths with credentials.

---

## Chosen option — detailed

### AWS Lambda + container image (ECR) + API Gateway HTTP API

| Field | Detail |
|-------|--------|
| **Status** | **Accepted** |
| **Target** | **Serverless** HTTP API with **per-request** billing, **no EC2 fleet**, **IaC-first** story (Lambda + role + logs + ECR + API GW). |
| **When this is the right pick** | Small API surface; traffic is **bursty** or **low**; team wants **Terraform + CI `plan`** without running servers; **container** parity between laptop and AWS matters. |
| **When to reconsider** | Steady **high RPS** with strict **low tail latency** and **.NET cold start** is unacceptable without **large** provisioned concurrency spend; **WebSockets** or **very long** requests as core product; **static egress IP** hard requirement without proxy. |
| **Avoid at all costs** | Treating **“CI passed with a placeholder image URI”** as equivalent to **“production is pinned to an immutable digest.”** Also: **avoid** baking secrets into the image or Terraform state. |
| **CI / Ops notes** | Pipeline adds **Docker** + (optional) **ECR auth**; **plan-only** contributors need a **documented** story (placeholder vs optional job). |
| **Cost posture** | Pay per invoke + **ECR storage** + (optional) **API Gateway** + (optional) **PC**; predictable for demos; watch **PC** baseline if enabled. |

---

## Alternatives considered — detailed

Each option below was evaluated for **this** repo (take-home, **plan-first** CI, **Terraform** demo, **.NET 8** weather API). Status is always **Rejected** or **Deferred** relative to **this** ADR’s **Accepted** choice — not a global judgment of the AWS service.

---

### 1) Lambda deployment package (zip) + managed .NET runtime (`dotnet8`)

| Field | Detail |
|-------|--------|
| **Status** | **Rejected** for this repository *(valid pattern elsewhere)*. |
| **Target** | Teams that want the **smallest** packaging story: **`dotnet publish` → zip** → `aws_lambda_function` with **`handler`** + **`runtime`**, no container registry in the loop. |
| **Best when to pick** | Simple Lambdas; **fast** CI; you trust **AWS-managed** runtime updates; **no** custom OS-level dependencies; smallest **cold-start** surface vs **full container** in some cases. |
| **Fit for this project** | Would satisfy **Lambda + API Gateway + Terraform** narrative with **less** CI machinery than **Docker + ECR**. |
| **Do not pick when** | You need **identical** bits in dev and prod (**glibc** / native deps / custom CA bundles), or you already committed to **Phase 5** “Docker → ECR” in [PHASES.md](../../PHASES.md). |
| **Avoid at all costs** | **Oversized** deployment packages near **Lambda zip limits**; **mixing** “we use zip in CI but container in prod” **without** documenting the drift. |
| **Why rejected here** | We standardize on **OCI images** for **one artifact** and **explicit** publish layout; aligns with phased delivery and interview story for **container-based Lambda**. |

---

### 2) Amazon ECS on AWS Fargate + Application Load Balancer (ALB)

| Field | Detail |
|-------|--------|
| **Status** | **Rejected** for this repository *(strong choice for other shapes)*. |
| **Target** | **Long-lived** .NET services behind **HTTP/TCP** load balancing; **always-on** capacity; **gradual** deploys with connection draining. |
| **Best when to pick** | **Sustained** traffic; **WebSockets** / **SSE** / long-lived connections; **sidecars**; **predictable** latency without serverless cold paths; team already operates **ECS**. |
| **Fit for this project** | Technically fine for a **REST** weather API, but **more** Terraform and **more** moving parts than needed for the **stated** scope. |
| **Do not pick when** | You need **fastest** time-to-demo with **minimal** infra objects, or your interview narrative is **“serverless + API Gateway”** first. |
| **Avoid at all costs** | **Accidental** “mini k8s” on ECS for **one** HTTP handler — cluster, service, task def, TG, ALB, logs, scaling, **and** CI — without trimming scope. |
| **Why rejected here** | **Higher** operational surface (cluster, service, target group, ALB, scaling policies) for a **single** small API and a **plan-first** take-home; weaker **Lambda + API Gateway** teaching story for this repo. |

---

### 3) AWS App Runner

| Field | Detail |
|-------|--------|
| **Status** | **Rejected** for this repository *(excellent for prototypes)*. |
| **Target** | **Container → HTTPS URL** with **managed** scaling and minimal user-managed networking. |
| **Best when to pick** | Rapid **MVP** from a **container image**; internal tools; **low** desire to hand-wire **API Gateway + Lambda** IAM. |
| **Fit for this project** | Gets a **URL** quickly; **less** explicit **Lambda + API Gateway** Terraform for a **DevOps engineer** demo focused on **IaC depth**. |
| **Do not pick when** | You need **fine-grained** API Gateway features (throttling models, **HTTP API** vs **REST** trade-offs) as **first-class** teaching topics, or **Lambda-centric** observability patterns. |
| **Avoid at all costs** | Choosing App Runner **and** pretending the take-home still demonstrates **Lambda** operations — be honest in the ADR/README if you pivot. |
| **Why rejected here** | **Less** granular **IAM / API Gateway / Lambda** Terraform narrative for this repository’s goals. |

---

### 4) Amazon EKS (Kubernetes on AWS)

| Field | Detail |
|-------|--------|
| **Status** | **Rejected** for this repository *(appropriate at large scale)*. |
| **Target** | Many services, **shared** platform, **advanced** rollout strategies, **portable** Kubernetes APIs. |
| **Best when to pick** | Large product/engineering org; **multi-tenant** platform; **strong** platform team; **existing** K8s investment. |
| **Fit for this project** | **Massive** overshoot for **one** HTTP API take-home; explodes **Terraform** and **CI** scope (control plane, nodes/Fargate profiles, add-ons, RBAC). |
| **Do not pick when** | Timeline is **days**, not **quarters**; interview is **AWS serverless + Terraform**, not **platform engineering**. |
| **Avoid at all costs** | **EKS for a single Deployment** of one API — unless the assignment **explicitly** requires Kubernetes. |
| **Why rejected here** | **Disproportionate** operational and review cost versus **Lambda + API Gateway** for this scope. |

---

### 5) Amazon EC2 (single instance or Auto Scaling Group) + load balancer or public IP

| Field | Detail |
|-------|--------|
| **Status** | **Reference** — **not** selected; listed so the “**why not VMs**” question is answered in one place. |
| **Target** | Full control of OS, **SSH**, **long-lived** processes, **legacy** lift-and-shift. |
| **Best when to pick** | **Strong** OS-level needs, **license** constraints, **custom** kernel modules, or **existing** VM-based operations model. |
| **Fit for this project** | Works for any HTTP API, but **shifts** work to **patching, AMIs, scaling groups, health checks**, and **secrets on disk** risks. |
| **Do not pick when** | You want **managed scaling** and **IAM-bound** request paths without babysitting **SSH** and **OS** patches. |
| **Avoid at all costs** | **Pet** EC2 with **manual** deploys and **snowflake** security groups for **prod** — especially under **Terraform** pretence without **real** discipline. |
| **Why not selected here** | **Higher** undifferentiated toil for a **small** API; **weaker** alignment with **serverless + IaC** demo goals. |

---

### 6) AWS Lambda in a VPC (with NAT for outbound internet)

| Field | Detail |
|-------|--------|
| **Status** | **Deferred** — **not** v1 for this repo; would be a **new ADR** or supersession if adopted. |
| **Target** | Lambdas that must reach **private** RDS/ElastiCache/ VPC endpoints, or **static** egress via **NAT** + controlled routing. |
| **Best when to pick** | **Private** data plane requirements; **network segmentation** mandates; **existing** VPC-centric security model. |
| **Fit for this project** | **Not** required for **Open-Meteo** over **public HTTPS** from a **VPC-less** function in v1. |
| **Do not pick when** | You only need **outbound HTTPS to the public internet** and **CloudWatch Logs** — **NAT** adds **cost** and **failure modes**. |
| **Avoid at all costs** | **NAT-less** “Lambda in private subnets” that **cannot** reach Open-Meteo; **over-wide** security groups “to make it work.” |
| **Why deferred here** | **v1** optimizes for **simplicity**: no **ENI** cold-path surprises and **no NAT** tax for a public API + public upstream. |

---

## Rationale (cross-option)

- **Lambda + API Gateway** is a **standard** AWS pattern for **HTTP APIs** and maps cleanly to **Terraform** and optional **smoke tests** after **`apply`**.
- **Container images** improve **dev/prod parity** vs ad-hoc zip layout for **non-trivial** publish outputs.
- **No VPC** in v1 keeps **IAM** and **network troubleshooting** tractable for a take-home.

---

## Consequences

### Positive

- **No EC2 fleet** to patch for compute; platform patching is largely **AWS-managed**.
- **Clear artifact line:** source → **tests** → **`docker build`** → (optional) **ECR** → **Lambda** updates via Terraform.
- **Terraform-friendly:** execution role, log group, function, (later) alias, API integration.

### Negative / accepted trade-offs

- **.NET cold starts** — mitigate with memory, **smaller images**, **provisioned concurrency** (cost); document in [ARCHITECTURE.md](../../ARCHITECTURE.md).
- **Lambda + API Gateway limits** — timeouts and payload sizes must match **retry** budgets.
- **CI complexity** — Docker + optional ECR auth; **placeholder `plan`** must never be confused with **immutable production digests**.

---

## Follow-up (out of scope for this ADR)

- **Open-Meteo** resilience (timeouts, retries, fallback) — application + architecture sections / future ADR.
- **CloudFront**, **geo**, **caching** — edge ADR or architecture.
- **Blue/green** on **Lambda aliases** — release ADR or architecture §12.
- **VPC-attached Lambda**, **Secrets Manager / SSM** — future ADR if requirements change.

---

## References

- [ARCHITECTURE.md — Compute — Lambda](../../ARCHITECTURE.md)
- [ARCHITECTURE.md — CI/CD](../../ARCHITECTURE.md)
- [PHASES.md — Phase 5](../../PHASES.md)
