# Developer Workflow and CI

## Local workflow
- run Postgres + Redis + object storage locally
- run API and web in watch mode
- run worker services selectively
- use sample packs and sample evidence bundles for deterministic testing

## Core scripts
- pnpm dev
- pnpm build
- pnpm lint
- pnpm test
- pnpm typecheck
- pnpm generate:contracts
- pnpm dev:stack
- pnpm seed:samples

## CI pipeline
At minimum:
1. install dependencies
2. build TypeScript packages/apps
3. build .NET solution
4. run unit tests
5. validate pack schemas
6. validate OpenAPI generation
7. run sample golden-output tests

## Golden-output testing
For sample evidence bundles, assert:
- expected findings exist
- expected severity/confidence values are stable enough
- report generation succeeds
- baseline/comparison output is consistent
