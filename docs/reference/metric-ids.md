---
title: Canonical metric IDs
description: The snake_case metric identifiers PAL-X recognises out of the box and how they map to Windows counter paths.
---

# Canonical metric IDs

Pack rules reference metrics by canonical, snake_case identifiers like `processor.percent_processor_time`. PAL-X normalises every incoming Windows counter path to one of these IDs before evaluation, so packs are portable across machines, languages, and capture tools.

This page lists every canonical ID the built-in registry recognises and the counter-path pattern that maps to it. If you have an unusual counter path — non-English Windows, vendor counters, a custom CSV — see [Pack-level metric_aliases](#pack-level-metric_aliases) below.

The authoritative source is `dotnet/src/Pal.Engine/Normalization/MetricAliasRegistry.cs`. This page is the human-readable rendering; if the two disagree, the source file wins.

## How normalisation works

1. The collector reads counter paths in their raw form: `\\MACHINE\Object(Instance)\Counter`.
2. The `MetricAliasRegistry` runs each path against the registered regex patterns in registration order.
3. The first match wins — the path is rewritten to the canonical ID.
4. If no built-in pattern matches, the registry falls back to pack-level `metric_aliases` (see below), then finally to `unknown.<sanitized_path>`.

Series tagged `unknown.*` won't be matched by any rule — they're still indexed in the report's `series_index` and visible via `pal inspect-dataset`.

## Naming convention

- All canonical IDs are **lowercase snake_case**.
- The first segment is the **object family**: `processor`, `memory`, `physicaldisk`, `network`, `process`, `pagingfile`, `system`, `aspnet`, `iis`, `sql`.
- The remaining segments are the **counter name**, with `% `, `/`, and spaces replaced by `_`, `per_`, and `_` respectively.
- Instances (`_Total`, `C:`, process names) are captured **separately** from the ID — use the `instance:` field on a condition to filter them.

This is one of the ratified deviations from the seed docs — see [ADR 0001](../architecture/adr/0001-deviations-from-seed-docs.md).

## Processor

| Canonical ID | Maps from |
|---|---|
| `processor.percent_processor_time` | `\Processor(*)\% Processor Time` |
| `processor.percent_privileged_time` | `\Processor(*)\% Privileged Time` |
| `processor.percent_user_time` | `\Processor(*)\% User Time` |
| `processor.percent_interrupt_time` | `\Processor(*)\% Interrupt Time` |
| `processor.interrupts_per_sec` | `\Processor(*)\Interrupts/sec` |

## System

| Canonical ID | Maps from |
|---|---|
| `system.context_switches_per_sec` | `\System\Context Switches/sec` |
| `system.processor_queue_length` | `\System\Processor Queue Length` |

## Memory

| Canonical ID | Maps from |
|---|---|
| `memory.available_mbytes` | `\Memory\Available MBytes` |
| `memory.committed_bytes` | `\Memory\Committed Bytes` |
| `memory.percent_committed_bytes_in_use` | `\Memory\% Committed Bytes In Use` |
| `memory.pages_per_sec` | `\Memory\Pages/sec` |
| `memory.page_faults_per_sec` | `\Memory\Page Faults/sec` |
| `memory.pool_nonpaged_bytes` | `\Memory\Pool Nonpaged Bytes` |
| `memory.pool_paged_bytes` | `\Memory\Pool Paged Bytes` |
| `pagingfile.percent_usage` | `\Paging File(*)\% Usage` |

## Physical disk

| Canonical ID | Maps from |
|---|---|
| `physicaldisk.avg_disk_sec_per_read` | `\PhysicalDisk(*)\Avg. Disk sec/Read` |
| `physicaldisk.avg_disk_sec_per_write` | `\PhysicalDisk(*)\Avg. Disk sec/Write` |
| `physicaldisk.avg_disk_sec_per_transfer` | `\PhysicalDisk(*)\Avg. Disk sec/Transfer` |
| `physicaldisk.current_disk_queue_length` | `\PhysicalDisk(*)\Current Disk Queue Length` |
| `physicaldisk.percent_idle_time` | `\PhysicalDisk(*)\% Idle Time` |
| `physicaldisk.disk_reads_per_sec` | `\PhysicalDisk(*)\Disk Reads/sec` |
| `physicaldisk.disk_writes_per_sec` | `\PhysicalDisk(*)\Disk Writes/sec` |

## Network interface

| Canonical ID | Maps from |
|---|---|
| `network.bytes_total_per_sec` | `\Network Interface(*)\Bytes Total/sec` |
| `network.bytes_sent_per_sec` | `\Network Interface(*)\Bytes Sent/sec` |
| `network.bytes_received_per_sec` | `\Network Interface(*)\Bytes Received/sec` |
| `network.current_bandwidth` | `\Network Interface(*)\Current Bandwidth` |
| `network.output_queue_length` | `\Network Interface(*)\Output Queue Length` |
| `network.packets_received_errors` | `\Network Interface(*)\Packets Received Errors` |

## Process

| Canonical ID | Maps from |
|---|---|
| `process.private_bytes` | `\Process(*)\Private Bytes` |
| `process.working_set` | `\Process(*)\Working Set` |
| `process.virtual_bytes` | `\Process(*)\Virtual Bytes` |
| `process.handle_count` | `\Process(*)\Handle Count` |
| `process.thread_count` | `\Process(*)\Thread Count` |
| `process.percent_processor_time` | `\Process(*)\% Processor Time` |
| `process.percent_privileged_time` | `\Process(*)\% Privileged Time` |
| `process.io_data_operations_per_sec` | `\Process(*)\IO Data Operations/sec` |
| `process.io_read_operations_per_sec` | `\Process(*)\IO Read Operations/sec` |
| `process.io_write_operations_per_sec` | `\Process(*)\IO Write Operations/sec` |

## SQL Server

The registry matches both **default-instance** (`SQLServer:Object`) and **named-instance** (`MSSQL$<name>:Object`) counter prefixes.

### Buffer Manager

| Canonical ID | Maps from |
|---|---|
| `sql.buffer_cache_hit_ratio` | `\SQLServer:Buffer Manager\Buffer cache hit ratio` |
| `sql.page_life_expectancy` | `\SQLServer:Buffer Manager\Page life expectancy` |
| `sql.buffer_free_pages` | `\SQLServer:Buffer Manager\Free pages` |
| `sql.lazy_writes_per_sec` | `\SQLServer:Buffer Manager\Lazy writes/sec` |
| `sql.page_lookups_per_sec` | `\SQLServer:Buffer Manager\Page lookups/sec` |
| `sql.checkpoint_pages_per_sec` | `\SQLServer:Buffer Manager\Checkpoint pages/sec` |
| `sql.page_reads_per_sec` | `\SQLServer:Buffer Manager\Page reads/sec` |
| `sql.page_writes_per_sec` | `\SQLServer:Buffer Manager\Page writes/sec` |

### SQL Statistics

| Canonical ID | Maps from |
|---|---|
| `sql.batch_requests_per_sec` | `\SQLServer:SQL Statistics\Batch Requests/sec` |
| `sql.compilations_per_sec` | `\SQLServer:SQL Statistics\SQL Compilations/sec` |
| `sql.recompilations_per_sec` | `\SQLServer:SQL Statistics\SQL Re-Compilations/sec` |

### Locks

| Canonical ID | Maps from |
|---|---|
| `sql.deadlocks_per_sec` | `\SQLServer:Locks(*)\Number of Deadlocks/sec` |
| `sql.lock_waits_per_sec` | `\SQLServer:Locks(*)\Lock Waits/sec` |

### Memory Manager

| Canonical ID | Maps from |
|---|---|
| `sql.memory_grants_pending` | `\SQLServer:Memory Manager\Memory Grants Pending` |

### General Statistics

| Canonical ID | Maps from |
|---|---|
| `sql.user_connections` | `\SQLServer:General Statistics\User Connections` |
| `sql.blocked_processes` | `\SQLServer:General Statistics\Blocked processes` |

## IIS / ASP.NET

### IIS APP_POOL_WAS

| Canonical ID | Maps from |
|---|---|
| `iis.recent_worker_process_failures` | `\APP_POOL_WAS(*)\Recent Worker Process Failures` |
| `iis.current_worker_processes` | `\APP_POOL_WAS(*)\Current Worker Processes` |

### ASP.NET

| Canonical ID | Maps from |
|---|---|
| `aspnet.application_restarts` | `\ASP.NET\Application Restarts` |
| `aspnet.worker_process_restarts` | `\ASP.NET\Worker Process Restarts` |
| `aspnet.requests_rejected` | `\ASP.NET\Requests Rejected` |
| `aspnet.request_wait_time` | `\ASP.NET\Request Wait Time` |
| `aspnet.requests_in_application_queue` | `\ASP.NET Applications(*)\Requests In Application Queue` |
| `aspnet.request_execution_time` | `\ASP.NET Applications(*)\Request Execution Time` |
| `aspnet.errors_total_per_sec` | `\ASP.NET Applications(*)\Errors Total/sec` |

## `unknown.*` series

Any counter path that doesn't match a registered pattern is exposed under a synthetic ID of the form `unknown.<sanitised_path>` — letters and digits preserved, everything else replaced with `_`.

Two places to find these:

- **`pal inspect-dataset --input <file>`** lists them under "Series → canonical metric".
- **The `series_index` array in the JSON report** carries `canonical_metric: "unknown.…"` so you can grep for them.

Series tagged `unknown.*` are still ingested and statistically summarised — they just don't get matched by any rule. If you want to write rules against them, register them via `metric_aliases` in your pack.

## Pack-level `metric_aliases`

If you regularly need to map non-standard counter paths to canonical IDs (non-English Windows, third-party agents, custom CSV exports), declare them at the pack level rather than patching the engine:

```yaml
metric_aliases:
  processor.percent_processor_time:
    - '\\*\Processeur(_Total)\% Temps processeur'   # French
    - '\\*\Prozessor(_Total)\% Prozessorzeit'       # German
  sql.batch_requests_per_sec:
    - '\\*\MSSQL$PROD:SQL Statistics\Batch Requests/sec'
```

Patterns support `*` and `?` glob wildcards, which the loader compiles to regex with `Regex.Escape`. Pack-level aliases run **after** the built-in registry — they extend the registry rather than override it. To override, ship a custom build of `Pal.Engine`.

See [Pack schema v1 — Metric aliases](pack-schema-v1.md#metric-aliases) for the full schema treatment.

## Related

- **[Pack schema v1](pack-schema-v1.md)** — the `metric` field on a condition takes one of the IDs above.
- **[Report schema](report-schema.md)** — the `canonical_metric` field on series and evidence.
- **[`pal inspect-dataset`](cli/pal-inspect-dataset.md)** — see how your input normalises before running rules.
- **[ADR 0001 — Deviations from Seed Docs](../architecture/adr/0001-deviations-from-seed-docs.md)** — why snake_case canonical IDs.
