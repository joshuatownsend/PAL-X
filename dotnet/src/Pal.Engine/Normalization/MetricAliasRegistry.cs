using System.Text.RegularExpressions;
using Pal.Engine.Model;

namespace Pal.Engine.Normalization;

public sealed class MetricAliasRegistry
{
    private readonly List<(Regex pattern, string canonicalId)> _entries = [];

    public static MetricAliasRegistry BuildDefault()
    {
        var reg = new MetricAliasRegistry();

        // Processor
        reg.Add(@"\\[^\\]+\\Processor\([^)]*\)\\% Processor Time", "processor.percent_processor_time");
        reg.Add(@"\\[^\\]+\\Processor\([^)]*\)\\% Privileged Time", "processor.percent_privileged_time");
        reg.Add(@"\\[^\\]+\\Processor\([^)]*\)\\% User Time", "processor.percent_user_time");
        reg.Add(@"\\[^\\]+\\Processor\([^)]*\)\\% Interrupt Time", "processor.percent_interrupt_time");
        reg.Add(@"\\[^\\]+\\Processor\([^)]*\)\\Interrupts/sec", "processor.interrupts_per_sec");
        reg.Add(@"\\[^\\]+\\System\\Context Switches/sec", "system.context_switches_per_sec");
        reg.Add(@"\\[^\\]+\\System\\Processor Queue Length", "system.processor_queue_length");

        // Memory
        reg.Add(@"\\[^\\]+\\Memory\\Available MBytes", "memory.available_mbytes");
        reg.Add(@"\\[^\\]+\\Memory\\Committed Bytes", "memory.committed_bytes");
        reg.Add(@"\\[^\\]+\\Memory\\% Committed Bytes In Use", "memory.percent_committed_bytes_in_use");
        reg.Add(@"\\[^\\]+\\Memory\\Pages/sec", "memory.pages_per_sec");
        reg.Add(@"\\[^\\]+\\Memory\\Page Faults/sec", "memory.page_faults_per_sec");
        reg.Add(@"\\[^\\]+\\Memory\\Pool Nonpaged Bytes", "memory.pool_nonpaged_bytes");
        reg.Add(@"\\[^\\]+\\Memory\\Pool Paged Bytes", "memory.pool_paged_bytes");
        reg.Add(@"\\[^\\]+\\Paging File\([^)]*\)\\% Usage", "pagingfile.percent_usage");

        // Physical Disk
        reg.Add(@"\\[^\\]+\\PhysicalDisk\([^)]*\)\\Avg\. Disk sec/Read", "physicaldisk.avg_disk_sec_per_read");
        reg.Add(@"\\[^\\]+\\PhysicalDisk\([^)]*\)\\Avg\. Disk sec/Write", "physicaldisk.avg_disk_sec_per_write");
        reg.Add(@"\\[^\\]+\\PhysicalDisk\([^)]*\)\\Avg\. Disk sec/Transfer", "physicaldisk.avg_disk_sec_per_transfer");
        reg.Add(@"\\[^\\]+\\PhysicalDisk\([^)]*\)\\Current Disk Queue Length", "physicaldisk.current_disk_queue_length");
        reg.Add(@"\\[^\\]+\\PhysicalDisk\([^)]*\)\\% Idle Time", "physicaldisk.percent_idle_time");
        reg.Add(@"\\[^\\]+\\PhysicalDisk\([^)]*\)\\Disk Reads/sec", "physicaldisk.disk_reads_per_sec");
        reg.Add(@"\\[^\\]+\\PhysicalDisk\([^)]*\)\\Disk Writes/sec", "physicaldisk.disk_writes_per_sec");

        // Network Interface
        reg.Add(@"\\[^\\]+\\Network Interface\([^)]*\)\\Bytes Total/sec", "network.bytes_total_per_sec");
        reg.Add(@"\\[^\\]+\\Network Interface\([^)]*\)\\Bytes Sent/sec", "network.bytes_sent_per_sec");
        reg.Add(@"\\[^\\]+\\Network Interface\([^)]*\)\\Bytes Received/sec", "network.bytes_received_per_sec");
        reg.Add(@"\\[^\\]+\\Network Interface\([^)]*\)\\Current Bandwidth", "network.current_bandwidth");
        reg.Add(@"\\[^\\]+\\Network Interface\([^)]*\)\\Output Queue Length", "network.output_queue_length");
        reg.Add(@"\\[^\\]+\\Network Interface\([^)]*\)\\Packets Received Errors", "network.packets_received_errors");

        // Process
        reg.Add(@"\\[^\\]+\\Process\([^)]*\)\\Private Bytes", "process.private_bytes");
        reg.Add(@"\\[^\\]+\\Process\([^)]*\)\\Working Set", "process.working_set");
        reg.Add(@"\\[^\\]+\\Process\([^)]*\)\\Virtual Bytes", "process.virtual_bytes");
        reg.Add(@"\\[^\\]+\\Process\([^)]*\)\\Handle Count", "process.handle_count");
        reg.Add(@"\\[^\\]+\\Process\([^)]*\)\\Thread Count", "process.thread_count");
        reg.Add(@"\\[^\\]+\\Process\([^)]*\)\\% Processor Time", "process.percent_processor_time");
        reg.Add(@"\\[^\\]+\\Process\([^)]*\)\\% Privileged Time", "process.percent_privileged_time");
        reg.Add(@"\\[^\\]+\\Process\([^)]*\)\\IO Data Operations/sec", "process.io_data_operations_per_sec");
        reg.Add(@"\\[^\\]+\\Process\([^)]*\)\\IO Read Operations/sec", "process.io_read_operations_per_sec");
        reg.Add(@"\\[^\\]+\\Process\([^)]*\)\\IO Write Operations/sec", "process.io_write_operations_per_sec");

        // SQL Server Buffer Manager
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Buffer Manager\\Buffer cache hit ratio", "sql.buffer_cache_hit_ratio");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Buffer Manager\\Page life expectancy", "sql.page_life_expectancy");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Buffer Manager\\Free pages", "sql.buffer_free_pages");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Buffer Manager\\Lazy writes/sec", "sql.lazy_writes_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Buffer Manager\\Page lookups/sec", "sql.page_lookups_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Buffer Manager\\Checkpoint pages/sec", "sql.checkpoint_pages_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Buffer Manager\\Page reads/sec", "sql.page_reads_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Buffer Manager\\Page writes/sec", "sql.page_writes_per_sec");

        // SQL Server SQL Statistics
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):SQL Statistics\\Batch Requests/sec", "sql.batch_requests_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):SQL Statistics\\SQL Compilations/sec", "sql.compilations_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):SQL Statistics\\SQL Re-Compilations/sec", "sql.recompilations_per_sec");

        // SQL Server Locks
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Locks\([^)]*\)\\Number of Deadlocks/sec", "sql.deadlocks_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Locks\([^)]*\)\\Lock Waits/sec", "sql.lock_waits_per_sec");

        // SQL Server Memory Manager
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Memory Manager\\Memory Grants Pending", "sql.memory_grants_pending");

        // SQL Server General Statistics
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):General Statistics\\User Connections", "sql.user_connections");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):General Statistics\\Blocked processes", "sql.blocked_processes");

        // IIS APP_POOL_WAS
        reg.Add(@"\\[^\\]+\\APP_POOL_WAS\([^)]*\)\\Recent Worker Process Failures", "iis.recent_worker_process_failures");
        reg.Add(@"\\[^\\]+\\APP_POOL_WAS\([^)]*\)\\Current Worker Processes", "iis.current_worker_processes");

        // ASP.NET
        reg.Add(@"\\[^\\]+\\ASP\.NET\\Application Restarts", "aspnet.application_restarts");
        reg.Add(@"\\[^\\]+\\ASP\.NET\\Worker Process Restarts", "aspnet.worker_process_restarts");
        reg.Add(@"\\[^\\]+\\ASP\.NET\\Requests Rejected", "aspnet.requests_rejected");
        reg.Add(@"\\[^\\]+\\ASP\.NET\\Request Wait Time", "aspnet.request_wait_time");
        reg.Add(@"\\[^\\]+\\ASP\.NET Applications\([^)]*\)\\Requests In Application Queue", "aspnet.requests_in_application_queue");
        reg.Add(@"\\[^\\]+\\ASP\.NET Applications\([^)]*\)\\Request Execution Time", "aspnet.request_execution_time");
        reg.Add(@"\\[^\\]+\\ASP\.NET Applications\([^)]*\)\\Errors Total/sec", "aspnet.errors_total_per_sec");

        return reg;
    }

    public void Add(string pattern, string canonicalId)
    {
        _entries.Add((new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled), canonicalId));
    }

    public void AddFromPack(IReadOnlyDictionary<string, IReadOnlyList<string>> metricAliases)
    {
        foreach (var (canonicalId, patterns) in metricAliases)
        {
            foreach (var pat in patterns)
                Add(Regex.Escape(pat).Replace(@"\*", ".*").Replace(@"\?", "."), canonicalId);
        }
    }

    public string? Resolve(string counterPath)
    {
        foreach (var (pattern, canonical) in _entries)
        {
            if (pattern.IsMatch(counterPath))
                return canonical;
        }
        return null;
    }
}
