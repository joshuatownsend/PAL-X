# Repo Structure

## Proposed tree

```text
pal/
├─ apps/
│  ├─ api/
│  │  ├─ src/
│  │  ├─ openapi/
│  │  └─ tests/
│  └─ web/
│     ├─ app/
│     ├─ components/
│     ├─ features/
│     └─ tests/
├─ services/
│  ├─ analysis-worker/
│  ├─ ingestion-worker/
│  ├─ trend-worker/
│  └─ automation-worker/
├─ packages/
│  ├─ contracts/
│  ├─ reporting/
│  ├─ web-ui/
│  ├─ pack-runtime/
│  └─ recommendation-runtime/
├─ dotnet/
│  ├─ src/
│  │  ├─ Pal.Engine/
│  │  ├─ Pal.Ingestion/
│  │  ├─ Pal.Correlation/
│  │  ├─ Pal.Policy/
│  │  ├─ Pal.Storage/
│  │  └─ Pal.Workflows/
│  ├─ tests/
│  └─ Pal.sln
├─ packs/
│  ├─ thresholds/
│  │  ├─ windows-server/
│  │  ├─ sql-server/
│  │  ├─ iis/
│  │  └─ active-directory/
│  ├─ recommendations/
│  ├─ policies/
│  ├─ schemas/
│  └─ samples/
├─ infra/
│  ├─ compose/
│  ├─ docker/
│  ├─ sql/
│  └─ environments/
├─ docs/
│  ├─ architecture/
│  ├─ product/
│  ├─ operations/
│  └─ runbooks/
├─ tools/
│  ├─ scripts/
│  └─ dev/
├─ turbo.json
├─ pnpm-workspace.yaml
├─ package.json
├─ .node-version
├─ .gitignore
└─ README.md
```

## Repository conventions

- One source of truth for contracts
- One canonical schema directory for packs
- One canonical docs directory for architecture and runbooks
- Every service must expose health checks and structured logs
- Every finding must be explainable and evidence-linked
