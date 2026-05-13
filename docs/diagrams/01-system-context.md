# System context

High-level traffic and components. See [ARCHITECTURE.md](../ARCHITECTURE.md) §2.

**Deploy path:** the API runs as a **Lambda container image** built from the repo **Dockerfile** and stored in **Amazon ECR**; Lambda pulls that image when environments start or scale.

```mermaid
flowchart LR
  V[Viewer]
  CF[CloudFront AU + CDN cache]
  GW[API Gateway throttle]
  ECR[(Amazon ECR container image)]
  L[Lambda .NET 8 alias live PC=3]
  MEM[IMemoryCache]
  OM[Open-Meteo]
  FB[AU monthly fallback]

  V --> CF --> GW --> L
  ECR -.->|platform pulls image| L
  L --> MEM
  MEM -->|miss / expired| OM
  MEM -->|hit| R[Response]
  OM -->|success| R
  OM -->|retry + delay| OM
  L -->|failure / circuit open| FB
  FB --> R
```
