# Recommended Root Files

## package.json
Should include scripts roughly like:

```json
{
  "name": "pal",
  "private": true,
  "packageManager": "pnpm@10.30.3",
  "scripts": {
    "dev": "turbo run dev --parallel",
    "build": "turbo run build",
    "lint": "turbo run lint",
    "test": "turbo run test",
    "typecheck": "turbo run typecheck",
    "generate:contracts": "pnpm --filter @pal/contracts build",
    "dev:stack": "docker compose -f infra/compose/docker-compose.yml up -d",
    "dev:stack:down": "docker compose -f infra/compose/docker-compose.yml down"
  }
}
```

## pnpm-workspace.yaml

```yaml
packages:
  - apps/*
  - services/*
  - packages/*
```

## turbo.json
Use pipeline/tasks for:
- build
- dev
- lint
- test
- typecheck

## .gitignore
Should cover:
- node_modules
- .next
- dist
- coverage
- bin
- obj
- .turbo
- local artifact directories
- .env*
