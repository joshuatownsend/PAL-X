# PAL 2026 — Phase 2 Worker and Queue Design

## 1. Purpose

Separate API responsiveness from heavy analysis execution.

The API should validate, persist, and queue. The worker should download input, execute the engine, store outputs, and update status.

## 2. Job Lifecycle

1. API receives analysis request
2. API validates upload and requested packs
3. API writes `analysis_jobs` row with status `queued`
4. API enqueues `analysisId`
5. Worker claims job
6. Worker sets status `running`
7. Worker downloads upload and resolves pack versions
8. Worker executes Phase 1 engine
9. Worker persists results and reports
10. Worker sets status `completed` or `failed`

## 3. Queue Contract

Minimal message body:
```json
{
  "analysisId": "uuid"
}
```

Keep queue payload minimal. Fetch full execution context from the database inside the worker.

## 4. Worker Responsibilities

- fetch analysis job
- verify job is in queued state
- resolve upload and pack artifacts
- create local temp workspace
- invoke engine
- persist structured results
- persist reports
- emit audit event
- clean up temp files

## 5. Failure Handling

Failures should be explicit and recoverable.

Categories:
- upload not found
- pack resolution failed
- unsupported source type
- engine execution failed
- report generation failed
- persistence failure

Recommendation:
- store a normalized failure code
- store readable failure reason
- do not retry non-transient validation errors
- allow bounded retries for transient storage or queue failures

## 6. Idempotency

Guard against duplicate queue delivery.

Worker should:
- lock or claim job row
- no-op if job is already running or completed
- ensure repeated persistence does not create duplicate primary results

## 7. Observability

Worker must emit:
- structured logs
- stage timings
- counts of packs, findings, report formats
- error category

Suggested stages:
- resolve-input
- resolve-packs
- normalize
- analyze
- render-reports
- persist
- finalize

## 8. Runtime Recommendation

Start simple:
- .NET 8 worker service
- Redis-backed queue
- local temp file workspace
- object storage abstraction for uploads and reports

## 9. Scaling Strategy

Horizontal worker scale is safe when:
- job claiming is atomic
- storage keys are deterministic
- report persistence is idempotent

## 10. Example Pseudocode

```csharp
var job = await repo.TryClaimJobAsync(analysisId);
if (job is null) return;

try
{
    var upload = await uploads.GetAsync(job.UploadId);
    var packs = await packsRepo.ResolveAsync(job.RequestedPacks);

    var workspace = await workspaceFactory.CreateAsync(job.Id);
    var inputPath = await storage.DownloadAsync(upload.ObjectKey, workspace);

    var result = await engine.RunAsync(new AnalysisRun
    {
        InputPath = inputPath,
        Packs = packs,
        Options = job.Options
    });

    await resultsRepo.SaveAsync(job.Id, result);
    await reportsRepo.SaveAsync(job.Id, result.Reports);
    await jobs.MarkCompletedAsync(job.Id);
}
catch (Exception ex)
{
    await jobs.MarkFailedAsync(job.Id, ex);
    throw;
}
```

## 11. Discipline Rule

The worker orchestrates. The engine analyzes. Do not let business logic drift into the worker layer.
