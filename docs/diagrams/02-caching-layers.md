# Caching layers

Logical cache stack for `GET /weather`. See [ARCHITECTURE.md](../ARCHITECTURE.md) §7 and §11.3.7 (correlation id vs CDN body cache).

```mermaid
flowchart TB
  subgraph client[Client]
    B[Browser / HTTP cache]
  end
  subgraph edge[Edge]
    CF[CloudFront CDN]
  end
  subgraph origin[Origin]
    GW[API Gateway optional REST stage cache]
    L[Lambda IMemoryCache]
  end
  OM[Open-Meteo]

  B --> CF --> GW --> L --> OM
```

**Notes:** HTTP API has no native stage cache like REST; “API tier” cache may be Lambda-only. `/weather` may skip shared CDN body cache when `correlationId` is in JSON (see architecture doc).
