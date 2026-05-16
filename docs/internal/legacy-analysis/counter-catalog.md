# PAL v2 — Performance Counter Catalog

Source: `QuickSystemOverview.xml`, `SystemOverview.xml`, `SQLServer.xml`, `IIS.xml`.
Counter path format: `\ObjectName(Instance)\CounterName`. `*` means all instances; `_Total` is the rollup instance.

---

## Windows — Core OS Counters

### Memory

| Counter | Threshold (Warning) | Threshold (Critical) | Notes |
|---------|--------------------|--------------------|-------|
| `\Memory\Available MBytes` | < 10% of physical RAM | < 5% of RAM or < 64 MB | Dynamic; requires `$PhysicalMemory` question answer |
| `\Memory\Committed Bytes` | — | — | Stats only; used with Available MBytes |
| `\Memory\% Committed Bytes In Use` | > 80% | > 90% | Static |
| `\Memory\Pages/sec` | > 1000 | — | Paging activity indicator |
| `\Memory\Page Faults/sec` | — | — | Stats only |
| `\Memory\Pool Nonpaged Bytes` | Trend > 1 MB/hr | — | Trend-only for leak detection |
| `\Memory\Pool Paged Bytes` | Trend > 1 MB/hr | — | Trend-only |
| `\Paging File(*)\% Usage` | > 80% | > 90% | Excludes `_Total` instance |

### Processor

| Counter | Threshold (Warning) | Threshold (Critical) | Notes |
|---------|--------------------|--------------------|-------|
| `\Processor(*)\% Processor Time` | > 50% per core | > 80% per core | |
| `\Processor(*)\% Privileged Time` | > 20% | > 30% | High = I/O or driver pressure |
| `\Processor(*)\% User Time` | — | — | Stats only |
| `\Processor(*)\% Interrupt Time` | > 15% | — | |
| `\Processor(*)\Interrupts/sec` | > 35,000 | — | |
| `\System\Context Switches/sec` | > 15,000/core | — | Dynamic; multiplied by CPU count |
| `\System\Processor Queue Length` | > 2/core sustained | — | Dynamic threshold |

### Physical Disk

| Counter | Threshold (Warning) | Threshold (Critical) | Notes |
|---------|--------------------|--------------------|-------|
| `\PhysicalDisk(*)\Avg. Disk sec/Read` | > 0.025 sec (25 ms) | > 0.050 sec (50 ms) | |
| `\PhysicalDisk(*)\Avg. Disk sec/Write` | > 0.025 sec (25 ms) | > 0.050 sec (50 ms) | |
| `\PhysicalDisk(*)\Avg. Disk sec/Transfer` | > 0.025 sec | > 0.050 sec | |
| `\PhysicalDisk(*)\Current Disk Queue Length` | > 2 | — | Per physical disk |
| `\PhysicalDisk(*)\% Idle Time` | < 30% | < 10% | Inverted — low idle is bad |
| `\PhysicalDisk(*)\Disk Reads/sec` | — | — | Stats only |
| `\PhysicalDisk(*)\Disk Writes/sec` | — | — | Stats only |

### Network Interface

| Counter | Threshold (Warning) | Threshold (Critical) | Notes |
|---------|--------------------|--------------------|-------|
| `\Network Interface(*)\Bytes Total/sec` | — | — | Used to derive % Utilization |
| `\Network Interface(*)\Current Bandwidth` | — | — | Used as divisor for % Utilization |
| *Generated* `% Network Utilization` | > 30% | > 50% | `BytesTotal * 8 / CurrentBandwidth * 100` |
| *Generated* `% Network Utilization Sent` | > 30% | > 50% | Same formula using `Bytes Sent/sec` |
| *Generated* `% Network Utilization Received` | > 30% | > 50% | Same formula using `Bytes Received/sec` |
| `\Network Interface(*)\Output Queue Length` | > 1 packet | > 2 packets | Queue backlog indicator |
| `\Network Interface(*)\Packets Received Errors` | > 0 | — | Any error is notable |

### Process (per-process)

| Counter | Threshold (Warning) | Threshold (Critical) | Notes |
|---------|--------------------|--------------------|-------|
| `\Process(*)\Private Bytes` | Trend > 100 MB/hr OR > 1 GB | — | `_Total` excluded; 1 GB absolute + trend |
| `\Process(*)\Working Set` | Trend > 100 MB/hr | — | Trend-only |
| `\Process(*)\Virtual Bytes` | > 60% of max VA space | > 75% of max VA space | Dynamic (2 GB 32-bit / 8 TB 64-bit) |
| `\Process(*)\Handle Count` | Trend > 100/hr OR > 16,384 | — | Handle leak + port exhaustion signal |
| `\Process(*)\Thread Count` | Trend > 100/hr OR > 1000 | — | Thread leak |
| `\Process(*)\% Processor Time` | > 50% × CPU count | > 75% × CPU count | Dynamic per-CPU scaled |
| `\Process(*)\% Privileged Time` | > 10% × CPU count | > 20% × CPU count | Dynamic |
| `\Process(*)\IO Data Operations/sec` | > 100 (info), > 1000 (warning) | — | Two separate thresholds at different priorities |
| `\Process(*)\IO Read Operations/sec` | > 1000 | — | |
| `\Process(*)\IO Write Operations/sec` | > 1000 | — | |

---

## SQL Server Counters

Counter object names support both default instance (`SQLServer:`) and named instances (`MSSQL$InstanceName:`). PAL uses regex `\(MSSQL|SQLServer).*:<object>` to match both.

### Process (SQL Server process)

| Counter | Threshold (Warning) | Threshold (Critical) |
|---------|--------------------|--------------------|
| `\Process(sqlservr)\% Privileged Time` | > 20% | > 30% |
| `\Process(sqlservr)\% User Time` | — | — |

### SQLServer:Access Methods

| Counter | Threshold | Type |
|---------|-----------|------|
| `\SQLServer:Access Methods\Forwarded Records/sec` | *See ratio* | Base counter |
| *Generated* `Forwarded Records / Batch Requests %` | > 10% (Warning) | Ratio to batch requests |
| `\SQLServer:Access Methods\FreeSpace Scans/sec` | *See ratio* | Base counter |
| *Generated* `FreeSpace Scans / Batch Requests %` | > 10% (Warning) | Ratio |
| `\SQLServer:Access Methods\Full Scans/sec` | — | Stats only |
| `\SQLServer:Access Methods\Index Searches/sec` | — | Used for Full Scan ratio |
| *Generated* `Index Searches / Full Scans ratio` | < 1000 when both > 1000 (Warning) | Ratio |
| `\SQLServer:Access Methods\Page Splits/sec` | *See ratio* | Base counter |
| *Generated* `Page Splits / Batch Requests %` | > 20% (Warning) | Ratio |
| `\SQLServer:Access Methods\Scan Point Revalidations/sec` | > 10/sec (Warning) | Direct |
| `\SQLServer:Access Methods\Workfiles Created/sec` | *See ratio* | Base counter |
| *Generated* `Workfiles Created / Batch Requests %` | > 20% (Warning) | Ratio |
| `\SQLServer:Access Methods\Worktables Created/sec` | > 200/sec (Warning) | Direct |

### SQLServer:Buffer Manager

| Counter | Threshold (Warning) | Threshold (Critical) |
|---------|--------------------|--------------------|
| `\SQLServer:Buffer Manager\Buffer cache hit ratio` | < 97% (Warning) | — |
| `\SQLServer:Buffer Manager\Page life expectancy` | — | < 300 seconds (5 min) |
| `\SQLServer:Buffer Manager\Free pages` | < 640 pages (Warning) | — |
| `\SQLServer:Buffer Manager\Lazy writes/sec` | — | > 20/sec (Critical) |
| `\SQLServer:Buffer Manager\Checkpoint pages/sec` | — | — |
| `\SQLServer:Buffer Manager\Page lookups/sec` | *See ratio* | — |
| *Generated* `Page lookups / Batch Requests %` | High ratio (Warning) | — |
| `\SQLServer:Buffer Manager\Page reads/sec` | — | — |
| `\SQLServer:Buffer Manager\Page writes/sec` | — | — |
| `\SQLServer:Buffer Manager\Database pages` | — | — |

### SQLServer:SQL Statistics

| Counter | Threshold (Warning) | Notes |
|---------|--------------------|--------------------|
| `\SQLServer:SQL Statistics\Batch Requests/sec` | — | Primary throughput baseline |
| `\SQLServer:SQL Statistics\SQL Compilations/sec` | *See ratio* | |
| *Generated* `Compilations / Batch Requests %` | > 10% (Warning) | Re-compilation pressure |
| `\SQLServer:SQL Statistics\SQL Re-Compilations/sec` | *See ratio* | |
| *Generated* `Re-compilations / Batch Requests %` | > 10% (Warning) | |
| `\SQLServer:SQL Statistics\Attention rate` | > 0 (Warning) | Client-initiated query cancellation |

### SQLServer:Locks

| Counter | Threshold (Warning) | Threshold (Critical) |
|---------|--------------------|--------------------|
| `\SQLServer:Locks(*)\Lock Waits/sec` | > 0 (Warning) | — |
| `\SQLServer:Locks(*)\Lock Wait Time (ms)` | > 0 (Warning) | — |
| `\SQLServer:Locks(*)\Number of Deadlocks/sec` | — | > 0 (Critical) |
| `\SQLServer:Locks(*)\Average Wait Time (ms)` | — | — |

### SQLServer:General Statistics

| Counter | Threshold (Warning) | Notes |
|---------|--------------------|--------------------|
| `\SQLServer:General Statistics\User Connections` | — | Stats only |
| `\SQLServer:General Statistics\Logins/sec` | — | Stats only |
| `\SQLServer:General Statistics\Logouts/sec` | — | Stats only |
| `\SQLServer:General Statistics\Blocked processes` | > 0 (Warning) | Any blocking is notable |

### SQLServer:Memory Manager

| Counter | Threshold (Warning) | Notes |
|---------|--------------------|--------------------|
| `\SQLServer:Memory Manager\Memory Grants Pending` | > 0 (Warning) | Queries waiting for memory |
| `\SQLServer:Memory Manager\Total Server Memory (KB)` | — | Stats only |
| `\SQLServer:Memory Manager\Target Server Memory (KB)` | — | Used to compute ratio |

---

## IIS / Web Server Counters

### APP_POOL_WAS (IIS Worker Process Activation)

| Counter | Threshold | Notes |
|---------|-----------|-------|
| `\APP_POOL_WAS(*)\Current Application Pool State` | — | States: 1=Uninitialized…7=Delete Pending |
| `\APP_POOL_WAS(*)\Current Application Pool Uptime` | — | Stats only |
| `\APP_POOL_WAS(*)\Current Worker Processes` | — | Stats only |
| `\APP_POOL_WAS(*)\Recent Worker Process Failures` | ≥ 1 (Warning) | Rapid-fail protection interval |
| `\APP_POOL_WAS(*)\Total Worker Process Failures` | — | Stats only |
| `\APP_POOL_WAS(*)\Total Worker Process Ping Failures` | — | Stats only |
| `\APP_POOL_WAS(*)\Total Worker Process Shutdown Failures` | — | Stats only |
| `\APP_POOL_WAS(*)\Total Worker Process Startup Failures` | — | Stats only |

### HTTP Service Request Queues

| Counter | Threshold | Notes |
|---------|-----------|-------|
| `\HTTP Service Request Queues(*)\ArrivalRate` | — | Stats only |
| `\HTTP Service Request Queues(*)\CacheHitRate` | — | Stats only |
| `\HTTP Service Request Queues(*)\CurrentQueueSize` | — | Stats only |
| `\HTTP Service Request Queues(*)\MaxQueueItemAge` | — | Stats only |
| `\HTTP Service Request Queues(*)\RejectedRequests` | — | Stats only |
| `\HTTP Service Request Queues(*)\RejectionRate` | — | Stats only |

### HTTP Service Url Groups

| Counter | Notes |
|---------|-------|
| `\HTTP Service Url Groups(*)\AllRequests` | Total requests per site |
| `\HTTP Service Url Groups(*)\BytesSentRate` | Throughput sent |
| `\HTTP Service Url Groups(*)\BytesReceivedRate` | Throughput received |
| `\HTTP Service Url Groups(*)\BytesTransferredRate` | Total throughput |
| `\HTTP Service Url Groups(*)\ConnectionAttempts` | Connection attempt rate |
| `\HTTP Service Url Groups(*)\CurrentConnections` | Active connections |
| `\HTTP Service Url Groups(*)\GetRequests` | GET method rate |
| `\HTTP Service Url Groups(*)\HeadRequests` | HEAD method rate |
| `\HTTP Service Url Groups(*)\MaxConnections` | Peak concurrent connections |

### W3SVC_W3WP (IIS Worker Process)

| Counter | Notes |
|---------|-------|
| `\W3SVC_W3WP(*)\Active Requests` | Requests in flight |
| `\W3SVC_W3WP(*)\Active Threads Count` | Threads processing requests |
| `\W3SVC_W3WP(*)\Maximum Threads Count` | Thread pool ceiling |
| `\W3SVC_W3WP(*)\Total Threads` | All available threads |
| `\W3SVC_W3WP(*)\Requests / Sec` | HTTP request throughput |
| `\W3SVC_W3WP(*)\Current File Cache Memory Usage` | User-mode file cache memory |
| `\W3SVC_W3WP(*)\Current Files Cached` | Files in user-mode cache |
| `\W3SVC_W3WP(*)\Current Metadata Cached` | Metadata blocks in cache |
| `\W3SVC_W3WP(*)\Current URIs Cached` | URI blocks in cache |
| `\W3SVC_W3WP(*)\File Cache Misses / sec` | Cache miss rate (file) |
| `\W3SVC_W3WP(*)\Metadata Cache Misses / sec` | Cache miss rate (metadata) |
| `\W3SVC_W3WP(*)\Output Cache Current Memory Usage` | Output cache memory |
| `\W3SVC_W3WP(*)\Output Cache Misses / sec` | Output cache miss rate |

### Web Service (IIS 5/6 native)

| Counter | Notes |
|---------|-------|
| `\Web Service(_Total)\Current Connections` | Active connections to Web service |
| `\Web Service(_Total)\ISAPI Extension Requests/sec` | ISAPI request rate |
| `\Web Service(_Total)\Connection Attempts/sec` | Connection attempt rate |
| `\Web Service(_Total)\Other Request Methods/sec` | Non-standard HTTP verb rate |
| `\Web Service(*)\Bytes Total/sec` | Total bytes sent + received |
| `\Web Service(*)\Current Anonymous Users` | Anonymous concurrent users |
| `\Web Service(*)\Current NonAnonymous Users` | Authenticated concurrent users |
| `\Web Service Cache\File Cache Hits %` | User-mode file cache hit ratio |
| `\Web Service Cache\Kernel: URI Cache Hits %` | Kernel URI cache hit ratio |
| `\Web Service Cache\Output Cache Current Hits %` | Output cache hit ratio |

### WAS_W3WP

| Counter | Notes |
|---------|-------|
| `\WAS_W3WP(*)\Health Ping Reply Latency` | Time (100 ns units) for worker to reply to health ping |
| `\WAS_W3WP(*)\Active Listener Channels` | Listener channels in worker process |
| `\WAS_W3WP(*)\Active Protocol Handlers` | Protocol handlers in worker process |

---

## ASP.NET Counters (via AspDotNet.xml inheritance)

| Counter | Threshold | Notes |
|---------|-----------|-------|
| `\ASP.NET\Application Restarts` | > 0 (Warning) | Any restart is significant |
| `\ASP.NET\Worker Process Restarts` | > 0 (Warning) | |
| `\ASP.NET\Requests Rejected` | > 0 (Critical) | Queue overflow; requests dropped |
| `\ASP.NET\Request Wait Time` | > 1000 ms (Warning) | Queue wait time |
| `\ASP.NET Applications(*)\Requests In Application Queue` | > 0 (Warning) | Backpressure indicator |
| `\ASP.NET Applications(*)\Request Execution Time` | > 1000 ms (Warning) | Execution latency |
| `\ASP.NET Applications(*)\Errors Total/sec` | > 0 (Warning) | Any unhandled error rate |

---

## Generated / Derived Counters

PAL computes these counters in `<DATASOURCE TYPE="Generated">` elements; they have no native Windows counterpart.

| Generated Counter | Formula | Used In |
|------------------|---------|---------|
| `\Network Interface(*)\% Network Utilization` | `(BytesTotal × 8) / CurrentBandwidth × 100` | QuickSystemOverview |
| `\Network Interface(*)\% Network Utilization Sent` | `(BytesSent × 8) / CurrentBandwidth × 100` | QuickSystemOverview |
| `\Network Interface(*)\% Network Utilization Received` | `(BytesReceived × 8) / CurrentBandwidth × 100` | QuickSystemOverview |
| `\PAL Generated(*)\Forwarded Records to Batch Requests Ratio Percentage` | `(ForwardedRecords / BatchRequests) × 100` | SQLServer |
| `\PAL Generated(*)\FreeSpace Scans to Batch Requests Ratio Percentage` | `(FreeSpaceScans / BatchRequests) × 100` | SQLServer |
| `\PAL Generated(*)\Page Splits to Batch Requests Ratio Percentage` | `(PageSplits / BatchRequests) × 100` | SQLServer |
| `\PAL Generated(*)\Workfiles Created to Batch Requests Ratio Percentage` | `(WorkfilesCreated / BatchRequests) × 100` | SQLServer |
| `\PAL Generated(*)\Page lookups to Batch Requests Ratio Percentage` | `(PageLookups / BatchRequests) × 100` | SQLServer |
| `\PAL Generated(*)\Full Scans to Index Searches Ratio` | `IndexSearches / FullScans` | SQLServer |
| `\PAL Generated(*)\SQL Compilations to Batch Requests Ratio Percentage` | `(Compilations / BatchRequests) × 100` | SQLServer |
| `\PAL Generated(*)\SQL Re-Compilations to Batch Requests Ratio Percentage` | `(ReCompilations / BatchRequests) × 100` | SQLServer |

All SQL-related generated counters use `ExtractSqlNamedInstanceFromCounterObjectPath` to match counters from the same SQL Server instance when multiple instances are present.
