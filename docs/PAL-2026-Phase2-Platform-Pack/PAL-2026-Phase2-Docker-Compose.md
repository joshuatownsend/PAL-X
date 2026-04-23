# PAL 2026 — Phase 2 Docker Compose Stack

Below is a recommended local stack for Phase 2 development.

```yaml
version: "3.9"

services:
  api:
    build:
      context: .
      dockerfile: ./infra/docker/api.Dockerfile
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__Postgres: Host=postgres;Port=5432;Database=pal;Username=pal;Password=paldev
      Redis__Configuration: redis:6379
      Storage__Provider: local
      Storage__LocalRoot: /data/storage
    volumes:
      - pal_storage:/data/storage
    depends_on:
      - postgres
      - redis

  worker:
    build:
      context: .
      dockerfile: ./infra/docker/worker.Dockerfile
    environment:
      ConnectionStrings__Postgres: Host=postgres;Port=5432;Database=pal;Username=pal;Password=paldev
      Redis__Configuration: redis:6379
      Storage__Provider: local
      Storage__LocalRoot: /data/storage
      Worker__TempRoot: /tmp/pal
    volumes:
      - pal_storage:/data/storage
    depends_on:
      - postgres
      - redis

  web:
    build:
      context: .
      dockerfile: ./infra/docker/web.Dockerfile
    ports:
      - "3000:3000"
    environment:
      NEXT_PUBLIC_API_BASE_URL: http://localhost:8080
    depends_on:
      - api

  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: pal
      POSTGRES_USER: pal
      POSTGRES_PASSWORD: paldev
    ports:
      - "5432:5432"
    volumes:
      - pal_pg:/var/lib/postgresql/data

  redis:
    image: redis:7
    ports:
      - "6379:6379"

volumes:
  pal_pg:
  pal_storage:
```

## Notes

- Use local object storage first through a filesystem-backed abstraction.
- Keep API, worker, and web isolated from one another.
- This stack is for development and internal testing only.
- Production should move to managed Postgres, managed Redis, and durable object storage.
