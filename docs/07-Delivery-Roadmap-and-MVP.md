# Delivery Roadmap and MVP Cut Line

## Recommended build order

### Foundation sprint
- create repo
- bootstrap pnpm + turbo
- create .NET solution
- define contracts package
- define initial pack schemas
- stand up Compose stack
- establish CI for build, lint, test

### Milestone 1 — Phase 1 engine MVP
Build:
- BLG ingest path
- normalized evidence model
- rule engine
- pack validation
- JSON result output
- HTML/Markdown report output
- CLI execution

### Milestone 2 — Phase 2 platform MVP
Build:
- API
- job queue
- analysis worker
- result persistence
- artifact persistence
- thin web UI for upload and result review

### Milestone 3 — Phase 3 focused intelligence MVP
Build:
- baseline creation from prior runs
- run-vs-baseline comparison
- simple trend rollups
- first cross-signal correlation path
- recommendation blocks

### Milestone 4 — Phase 4 operational MVP
Build:
- scheduled ingestion for at least one signal family
- policies
- alert creation
- notification integration
- safe follow-up automation trigger

## True v1 cut line
If you want a realistic first product, stop here for v1:
- Phase 1 complete
- Phase 2 complete
- selective Phase 3 only:
  - baselines
  - comparisons
  - simple recommendations

Do not require full Phase 4 for v1.

## Suggested version framing
- v0.1 = engine + CLI
- v0.5 = platform + web
- v0.9 = baseline/comparison intelligence
- v1.0 = explainable diagnostics platform
- v2.0 = continuous operations and SaaS expansion
