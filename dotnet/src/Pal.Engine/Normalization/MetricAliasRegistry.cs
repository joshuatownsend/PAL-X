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
        // ASP.NET versioned application counters (\ASP.NET Apps vX.Y.ZZZZZ(*)\...) map to the same canonical IDs
        reg.Add(@"\\[^\\]+\\ASP\.NET Apps v[0-9.]+\([^)]*\)\\Request Execution Time", "aspnet.request_execution_time");

        // .NET CLR
        reg.Add(@"\\[^\\]+\\\.NET CLR Exceptions\([^)]*\)\\# of Exceps Thrown / sec", "dotnetclr.exceptions_thrown_per_sec");
        reg.Add(@"\\[^\\]+\\\.NET CLR Memory\([^)]*\)\\% Time in GC", "dotnetclr.percent_time_in_gc");

        // NTDS — Active Directory domain controller (singleton object, no instance)
        reg.Add(@"\\[^\\]+\\NTDS\\LDAP Bind Time", "ntds.ldap_bind_time");
        reg.Add(@"\\[^\\]+\\NTDS\\DRA Pending Replication Operations", "ntds.dra_pending_replication_operations");

        // ── Workload packs (ported from legacy PAL v2 threshold files; see ADR 0005) ──

        // print-server
        reg.Add(@"\\[^\\]+\\Print Queue\([^)]*\)\\Out of Paper Errors", "printqueue.out_of_paper_errors");
        reg.Add(@"\\[^\\]+\\Print Queue\([^)]*\)\\Bytes Printed/sec", "printqueue.bytes_printed_per_sec");
        reg.Add(@"\\[^\\]+\\Print Queue\([^)]*\)\\Job Errors", "printqueue.job_errors");
        reg.Add(@"\\[^\\]+\\Print Queue\([^)]*\)\\Not Ready Errors", "printqueue.not_ready_errors");

        // hyper-v
        reg.Add(@"\\[^\\]+\\Hyper-V Hypervisor Virtual Processor\([^)]*\)\\% Guest Run Time", "hyperv.hypervisor_virtual_processor_percent_guest_run_time");
        reg.Add(@"\\[^\\]+\\Hyper-V Hypervisor Logical Processor\([^)]*\)\\% Total Run Time", "hyperv.hypervisor_logical_processor_percent_total_run_time");
        reg.Add(@"\\[^\\]+\\Hyper-V Hypervisor Logical Processor\([^)]*\)\\Context Switches/sec", "hyperv.hypervisor_logical_processor_context_switches_per_sec");
        reg.Add(@"\\[^\\]+\\Hyper-V Hypervisor Root Partition\([^)]*\)\\Address Spaces", "hyperv.hypervisor_root_partition_address_spaces");
        reg.Add(@"\\[^\\]+\\Hyper-V Virtual Machine Health Summary\\Health Critical", "hyperv.virtual_machine_health_summary_health_critical");
        reg.Add(@"\\[^\\]+\\Hyper-V Virtual Storage Device\([^)]*\)\\Error Count", "hyperv.virtual_storage_device_error_count");
        reg.Add(@"\\[^\\]+\\Hyper-V VM Vid Partition\([^)]*\)\\Remote Physical Pages", "hyperv.vm_vid_partition_remote_physical_pages");
        reg.Add(@"\\[^\\]+\\Hyper-V Dynamic Memory VM\([^)]*\)\\Average Pressure", "hyperv.dynamic_memory_vm_average_pressure");
        reg.Add(@"\\[^\\]+\\Hyper-V Dynamic Memory Balancer\([^)]*\)\\Average Pressure", "hyperv.dynamic_memory_balancer_average_pressure");
        reg.Add(@"\\[^\\]+\\Hyper-V Dynamic Memory VM\([^)]*\)\\Smart Paging Working Set Size", "hyperv.dynamic_memory_vm_smart_paging_working_set_size");
        reg.Add(@"\\[^\\]+\\NUMA Node Memory\([^)]*\)\\Available MBytes", "numanodememory.available_mbytes");
        reg.Add(@"\\[^\\]+\\Hyper-V Virtual Switch Processor\([^)]*\)\\Number of VMQs", "hyperv.virtual_switch_processor_number_of_vmqs");
        reg.Add(@"\\[^\\]+\\Hyper-V Virtual Machine Bus\\Throttle Events", "hyperv.virtual_machine_bus_throttle_events");
        reg.Add(@"\\[^\\]+\\Hyper-V Legacy Network Adapter\([^)]*\)\\Frames Dropped", "hyperv.legacy_network_adapter_frames_dropped");
        reg.Add(@"\\[^\\]+\\Hyper-V Legacy Network Adapter\([^)]*\)\\Bytes Dropped", "hyperv.legacy_network_adapter_bytes_dropped");

        // sharepoint-2013
        reg.Add(@"\\[^\\]+\\SharePoint Publishing Cache\([^)]*\)\\Publishing cache flushes / second", "sharepoint.publishing_cache_flushes_per_sec");
        reg.Add(@"\\[^\\]+\\SharePoint Publishing Cache\([^)]*\)\\Publishing cache hit ratio", "sharepoint.publishing_cache_hit_ratio");
        reg.Add(@"\\[^\\]+\\SharePoint Publishing Cache\([^)]*\)\\Publishing cache misses / sec", "sharepoint.publishing_cache_misses_per_sec");
        reg.Add(@"\\[^\\]+\\Office Server Search Archival Plugin\([^)]*\)\\Total docs in first queue", "sharepoint.search_archival_total_docs_first_queue");
        reg.Add(@"\\[^\\]+\\Office Server Search Archival Plugin\([^)]*\)\\Total docs in Second queue", "sharepoint.search_archival_total_docs_second_queue");
        reg.Add(@"\\[^\\]+\\Office Server Search Gatherer\\Idle Threads", "sharepoint.search_gatherer_idle_threads");

        // exchange-2016
        reg.Add(@"\\[^\\]+\\MSExchange ADAccess Domain Controllers\\LDAP Read Time", "msexchange.adaccess_dc_ldap_read_time");
        reg.Add(@"\\[^\\]+\\MSExchange ADAccess Domain Controllers\\LDAP Search Time", "msexchange.adaccess_dc_ldap_search_time");
        reg.Add(@"\\[^\\]+\\MSExchange ADAccess Processes\([^)]*\)\\LDAP Read Time", "msexchange.adaccess_processes_ldap_read_time");
        reg.Add(@"\\[^\\]+\\MSExchange ADAccess Processes\([^)]*\)\\LDAP Search Time", "msexchange.adaccess_processes_ldap_search_time");
        reg.Add(@"\\[^\\]+\\MSExchange Database ==> Instances\([^)]*\)\\I/O Database Reads \(Attached\) Average Latency", "msexchange.db_io_reads_attached_avg_latency");
        reg.Add(@"\\[^\\]+\\MSExchange Database ==> Instances\([^)]*\)\\I/O Database Writes \(Attached\) Average Latency", "msexchange.db_io_writes_attached_avg_latency");
        reg.Add(@"\\[^\\]+\\MSExchange Database ==> Instances\([^)]*\)\\I/O Log Writes Average Latency", "msexchange.db_io_log_writes_avg_latency");
        reg.Add(@"\\[^\\]+\\MSExchange Database ==> Instances\([^)]*\)\\I/O Database Reads \(Recovery\) Average Latency", "msexchange.db_io_reads_recovery_avg_latency");
        reg.Add(@"\\[^\\]+\\MSExchange RpcClientAccess\\RPC Averaged Latency", "msexchange.rpc_client_access_averaged_latency");
        reg.Add(@"\\[^\\]+\\MSExchange RpcClientAccess\\RPC Requests", "msexchange.rpc_client_access_rpc_requests");
        reg.Add(@"\\[^\\]+\\MSExchangeIS Client Type\([^)]*\)\\RPC Average Latency", "msexchange.is_client_type_rpc_avg_latency");
        reg.Add(@"\\[^\\]+\\MSExchangeIS Store\([^)]*\)\\RPC Average Latency", "msexchange.is_store_rpc_avg_latency");

        // sql-engine-2014
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Access Methods\\Workfiles Created/sec", "sql.workfiles_created_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Access Methods\\Worktables Created/sec", "sql.worktables_created_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Access Methods\\Worktables From Cache Ratio", "sql.worktables_from_cache_ratio");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Buffer Manager\\Free list stalls/sec", "sql.free_list_stalls_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Buffer Manager\\Extension outstanding IO counter", "sql.extension_outstanding_io");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Buffer Manager\\Extension free pages", "sql.extension_free_pages");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Buffer Node\([^)]*\)\\Foreign pages", "sql.buffer_node_foreign_pages");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):SQL Statistics\\SQL Attention rate", "sql.sql_attention_rate");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):SQL Errors\([^)]*\)\\Errors/sec", "sql.sql_errors_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):General Statistics\\Logins/sec", "sql.logins_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):General Statistics\\Logouts/sec", "sql.logouts_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Latches\\Latch Waits/sec", "sql.latch_waits_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Latches\\Total Latch Wait Time \(ms\)", "sql.total_latch_wait_time_ms");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Locks\([^)]*\)\\Lock Requests/sec", "sql.lock_requests_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Locks\([^)]*\)\\Lock Wait Time \(ms\)", "sql.lock_wait_time_ms");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Locks\([^)]*\)\\Lock Timeouts/sec", "sql.lock_timeouts_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Databases\([^)]*\)\\Log Flush Wait Time", "sql.log_flush_wait_time");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Databases\([^)]*\)\\Log Flush Waits/sec", "sql.log_flush_waits_per_sec");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Databases\([^)]*\)\\Log Growths", "sql.log_growths");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Databases\([^)]*\)\\Log Shrinks", "sql.log_shrinks");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Databases\([^)]*\)\\Percent Log Used", "sql.percent_log_used");
        reg.Add(@"\\[^\\]+\\(SQLServer|MSSQL\$[^:]+):Deprecated Features\([^)]*\)\\Usage", "sql.deprecated_features_usage");

        // citrix-xenapp
        reg.Add(@"\\[^\\]+\\Citrix MetaFrame Presentation Server\\Resolution WorkItem Queue Ready Count", "citrix.resolution_workitem_queue_ready_count");
        reg.Add(@"\\[^\\]+\\Citrix MetaFrame Presentation Server\\WorkItem Queue Ready Count", "citrix.workitem_queue_ready_count");
        reg.Add(@"\\[^\\]+\\Citrix MetaFrame Presentation Server\\Data Store Connection Failure", "citrix.data_store_connection_failure");
        reg.Add(@"\\[^\\]+\\Citrix MetaFrame Presentation Server\\Number of busy XML threads", "citrix.number_of_busy_xml_threads");
        reg.Add(@"\\[^\\]+\\Citrix Licensing\\License Server Connection Failure", "citrix.license_server_connection_failure");

        // dynamics-ax
        reg.Add(@"\\[^\\]+\\ServiceModelService 4\.0\.0\.0\([^)]*\)\\Calls Duration", "dynamicsax.wcf_calls_duration");
        reg.Add(@"\\[^\\]+\\ServiceModelService 4\.0\.0\.0\([^)]*\)\\Calls Failed", "dynamicsax.wcf_calls_failed");
        reg.Add(@"\\[^\\]+\\ServiceModelService 4\.0\.0\.0\([^)]*\)\\Calls Faulted", "dynamicsax.wcf_calls_faulted");
        reg.Add(@"\\[^\\]+\\ServiceModelService 4\.0\.0\.0\([^)]*\)\\Percent Of Max Concurrent Calls", "dynamicsax.wcf_percent_max_concurrent_calls");
        reg.Add(@"\\[^\\]+\\ServiceModelService 4\.0\.0\.0\([^)]*\)\\Percent Of Max Concurrent Instances", "dynamicsax.wcf_percent_max_concurrent_instances");
        reg.Add(@"\\[^\\]+\\ServiceModelService 4\.0\.0\.0\([^)]*\)\\Percent Of Max Concurrent Sessions", "dynamicsax.wcf_percent_max_concurrent_sessions");

        // dynamics-crm
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Workflow Operations Failed", "dynamicscrm.async_service.workflow_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Activity Propagation Operations Failed", "dynamicscrm.async_service.activity_propagation_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\ActivityQueue: Total Operations Failed", "dynamicscrm.async_service.activityqueue_total_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Bulk Email Operations Failed", "dynamicscrm.async_service.bulk_email_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\BulkDelete Operations Failed", "dynamicscrm.async_service.bulkdelete_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Event Operations Failed", "dynamicscrm.async_service.event_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Exchange Sync: Tasks Failed", "dynamicscrm.async_service.exchange_sync_tasks_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Exchange Sync: ACTs Failed", "dynamicscrm.async_service.exchange_sync_acts_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Exchange Sync: Appointments Failed", "dynamicscrm.async_service.exchange_sync_appointments_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Exchange Sync: Contacts Failed", "dynamicscrm.async_service.exchange_sync_contacts_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Exchange Sync: Transient Errors for ACTs", "dynamicscrm.async_service.exchange_sync_transient_errors_for_acts");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Exchange Sync: Transient Errors for Appointments", "dynamicscrm.async_service.exchange_sync_transient_errors_for_appointments");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Exchange Sync: Transient Errors for Contacts", "dynamicscrm.async_service.exchange_sync_transient_errors_for_contacts");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Exchange Sync: Transient Errors for Tasks", "dynamicscrm.async_service.exchange_sync_transient_errors_for_tasks");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\ExecuteSdkMessage Operations Failed", "dynamicscrm.async_service.executesdkmessage_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Import Operations Failed", "dynamicscrm.async_service.import_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\IncomingEmailProcessing Operations Failed", "dynamicscrm.async_service.incomingemailprocessing_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\MailboxQueue: Total Operations Failed", "dynamicscrm.async_service.mailboxqueue_total_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\OutgoingActivity Operations Failed", "dynamicscrm.async_service.outgoingactivity_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Parse Operations Failed", "dynamicscrm.async_service.parse_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\PostToYammer Operations Failed", "dynamicscrm.async_service.posttoyammer_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Publish Duplicate Rule Operations Failed", "dynamicscrm.async_service.publish_duplicate_rule_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Quick Campaign Operations Failed", "dynamicscrm.async_service.quick_campaign_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Server-Side Synchronization \(Incoming\): Items Failed", "dynamicscrm.async_service.server_side_synchronization_incoming_items_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Server-Side Synchronization \(Incoming\): Transient Errors", "dynamicscrm.async_service.server_side_synchronization_incoming_transient_errors");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Server-Side Synchronization \(Outgoing\): Items Failed", "dynamicscrm.async_service.server_side_synchronization_outgoing_items_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Server-Side Synchronization \(Outgoing\): Transient Errors", "dynamicscrm.async_service.server_side_synchronization_outgoing_transient_errors");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\AuditPartitionCreation Operations Failed", "dynamicscrm.async_service.auditpartitioncreation_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Bulk Detect Duplicates Operations Failed", "dynamicscrm.async_service.bulk_detect_duplicates_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\CalculateOrgMaxStorageSize Operations Failed", "dynamicscrm.async_service.calculateorgmaxstoragesize_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\CheckForLanguagePackUpdates Operations Failed", "dynamicscrm.async_service.checkforlanguagepackupdates_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\BulkDeleteChild Operations Failed", "dynamicscrm.async_service.bulkdeletechild_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\CleanupInactiveWorkflowAssemblies Operations Failed", "dynamicscrm.async_service.cleanupinactiveworkflowassemblies_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Collect Sqm Data Operations Failed", "dynamicscrm.async_service.collect_sqm_data_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\CollectOrgDBStats Operations Failed", "dynamicscrm.async_service.collectorgdbstats_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\CollectOrgSizeStats Operations Failed", "dynamicscrm.async_service.collectorgsizestats_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\DatabaseLogBackup Operations Failed", "dynamicscrm.async_service.databaselogbackup_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\DatabaseTuning Operations Failed", "dynamicscrm.async_service.databasetuning_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\DeletionService Operations Failed", "dynamicscrm.async_service.deletionservice_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\EncryptionHealthCheck Operations Failed", "dynamicscrm.async_service.encryptionhealthcheck_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\FullTextCatalogIndex Operations Failed", "dynamicscrm.async_service.fulltextcatalogindex_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\GoalRollup Operations Failed", "dynamicscrm.async_service.goalrollup_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\IndexManagement Operations Failed", "dynamicscrm.async_service.indexmanagement_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\OrgDBUpdate Operations Failed", "dynamicscrm.async_service.orgdbupdate_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Persist Match Code Operations Failed", "dynamicscrm.async_service.persist_match_code_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\RecurringSeriesExpansion Operations Failed", "dynamicscrm.async_service.recurringseriesexpansion_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\RefreshReadSharingSnapshots Operations Failed", "dynamicscrm.async_service.refreshreadsharingsnapshots_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\RefreshRowCountSnapshots Operations Failed", "dynamicscrm.async_service.refreshrowcountsnapshots_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\ReindexAll Operations Failed", "dynamicscrm.async_service.reindexall_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\ShrinkDatabase Operations Failed", "dynamicscrm.async_service.shrinkdatabase_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\ShrinkLogFile Operations Failed", "dynamicscrm.async_service.shrinklogfile_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\SnapshotIsolationUpdate Operations Failed", "dynamicscrm.async_service.snapshotisolationupdate_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\SolutionUpdate Operations Failed", "dynamicscrm.async_service.solutionupdate_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\StorageLimitNotification Operations Failed", "dynamicscrm.async_service.storagelimitnotification_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\Total Operations Failed", "dynamicscrm.async_service.total_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Async Service\([^)]*\)\\UpdateStatisticIntervals Operations Failed", "dynamicscrm.async_service.updatestatisticintervals_operations_failed");
        reg.Add(@"\\[^\\]+\\CRM Sandbox Host\\% CPU Usage", "dynamicscrm.sandbox_host.percent_cpu_usage");
        reg.Add(@"\\[^\\]+\\CRM Sandbox Host\\% Worker Processes Crashed", "dynamicscrm.sandbox_host.percent_worker_processes_crashed");
        reg.Add(@"\\[^\\]+\\CRM Sandbox Host\\% Worker Processes Terminated", "dynamicscrm.sandbox_host.percent_worker_processes_terminated");
        reg.Add(@"\\[^\\]+\\CRM Server\([^)]*\)\\Failed Internal Organization Service Requests", "dynamicscrm.server.failed_internal_organization_service_requests");
        reg.Add(@"\\[^\\]+\\CRM Server\([^)]*\)\\Failed Organization Service Metadata Requests", "dynamicscrm.server.failed_organization_service_metadata_requests");
        reg.Add(@"\\[^\\]+\\CRM Server\([^)]*\)\\Failed Organization Service Requests", "dynamicscrm.server.failed_organization_service_requests");
        reg.Add(@"\\[^\\]+\\CRM Server\([^)]*\)\\Failed Report Render Requests", "dynamicscrm.server.failed_report_render_requests");
        reg.Add(@"\\[^\\]+\\CRM Authentication\\ClaimsAuthenticationFailuresInTheLastMinute", "dynamicscrm.authentication.claimsauthenticationfailuresinthelastminute");
        reg.Add(@"\\[^\\]+\\CRM Authentication\\ConfigDBWindowsAuthenticationFailuresInTheLastMinute", "dynamicscrm.authentication.configdbwindowsauthenticationfailuresinthelastminute");
        reg.Add(@"\\[^\\]+\\CRM Authentication\\CrmPostAuthenticationFailuresInTheLastMinute", "dynamicscrm.authentication.crmpostauthenticationfailuresinthelastminute");
        reg.Add(@"\\[^\\]+\\CRM Authentication\\WindowsAuthenticationFailuresInTheLastMinute", "dynamicscrm.authentication.windowsauthenticationfailuresinthelastminute");
        reg.Add(@"\\[^\\]+\\CRM Discovery\\Failed Discovery Service Requests", "dynamicscrm.discovery.failed_discovery_service_requests");
        reg.Add(@"\\[^\\]+\\CRM LocatorService\\LocatorServiceFailedCacheFlushRequests", "dynamicscrm.locatorservice.locatorservicefailedcacheflushrequests");
        reg.Add(@"\\[^\\]+\\CRM Sandbox Client\([^)]*\)\\% Execute Failures", "dynamicscrm.sandbox_client.percent_execute_failures");
        reg.Add(@"\\[^\\]+\\CRM Sandbox Client\([^)]*\)\\% SDK Failures", "dynamicscrm.sandbox_client.percent_sdk_failures");

        // skype-for-business
        reg.Add(@"\\[^\\]+\\LS:USrv - DBStore\\USrv - Queue Latency \(msec\)", "sfb.usrv_dbstore.queue_latency_msec");
        reg.Add(@"\\[^\\]+\\LS:USrv - DBStore\\USrv - Sproc Latency \(msec\)", "sfb.usrv_dbstore.sproc_latency_msec");
        reg.Add(@"\\[^\\]+\\LS:USrv - REGDBStore\\USrv - Sproc Latency \(msec\)", "sfb.usrv_regdbstore.sproc_latency_msec");
        reg.Add(@"\\[^\\]+\\LS:USrv - DBStore\\USrv - Throttled requests\/sec", "sfb.usrv_dbstore.throttled_requests_per_sec");
        reg.Add(@"\\[^\\]+\\LS:SIP - Peers\([^)]*\)\\SIP - Sends Timed-Out\/sec", "sfb.sip_peers.sends_timed_out_per_sec");
        reg.Add(@"\\[^\\]+\\LS:SIP - Responses\\SIP - Local 503 Responses\/sec", "sfb.sip_responses.local_503_responses_per_sec");
        reg.Add(@"\\[^\\]+\\LS:SIP - Peers\([^)]*\)\\SIP - Average Outgoing Queue Delay", "sfb.sip_peers.average_outgoing_queue_delay");
        reg.Add(@"\\[^\\]+\\LS:SIP - Peers\([^)]*\)\\SIP - Flow-controlled Connections", "sfb.sip_peers.flow_controlled_connections");
        reg.Add(@"\\[^\\]+\\LS:SIP - Protocol\\SIP - Incoming Requests Dropped\/sec", "sfb.sip_protocol.incoming_requests_dropped_per_sec");
        reg.Add(@"\\[^\\]+\\LS:SIP - Protocol\\SIP - Incoming Responses Dropped\/sec", "sfb.sip_protocol.incoming_responses_dropped_per_sec");
        reg.Add(@"\\[^\\]+\\LS:SIP - Protocol\\SIP - Average Incoming Message Processing Time", "sfb.sip_protocol.average_incoming_message_processing_time");
        reg.Add(@"\\[^\\]+\\LS:SIP - Authentication\\SIP - Authentication System Errors\/sec", "sfb.sip_authentication.authentication_system_errors_per_sec");
        reg.Add(@"\\[^\\]+\\LS:SIP - Load Management\\SIP - Average Holding Time For Incoming Messages", "sfb.sip_load_management.average_holding_time_for_incoming_messages");
        reg.Add(@"\\[^\\]+\\LS:SIP - Load Management\\SIP - Incoming Messages Timed out", "sfb.sip_load_management.incoming_messages_timed_out");
        reg.Add(@"\\[^\\]+\\LS:RoutingApps - Inter Cluster Routing\\RoutingApps - Number of primary registrar timeouts", "sfb.routingapps_inter_cluster_routing.number_of_primary_registrar_timeouts");
        reg.Add(@"\\[^\\]+\\LS:RoutingApps - Inter Cluster Routing\\RoutingApps - Number of backup registrar timeouts", "sfb.routingapps_inter_cluster_routing.number_of_backup_registrar_timeouts");
        reg.Add(@"\\[^\\]+\\LS:LYSS - Storage Service API\\LYSS - Current number of Storage Service stale queue items", "sfb.lyss_storage_service_api.current_number_of_storage_service_stale_queue_items");
        reg.Add(@"\\[^\\]+\\LS:USrv - Cluster Manager\\USrv - Number of data loss events with state change", "sfb.usrv_cluster_manager.number_of_data_loss_events_with_state_change");
        reg.Add(@"\\[^\\]+\\LS:USrv - Cluster Manager\\USrv - Number of data loss events without state change", "sfb.usrv_cluster_manager.number_of_data_loss_events_without_state_change");
        reg.Add(@"\\[^\\]+\\LS:USrv - Cluster Manager\\USrv - Number of failures of replication operations sent to other Replicas per second", "sfb.usrv_cluster_manager.number_of_failures_of_replication_operations_sent_to_other_replicas_per_second");
        reg.Add(@"\\[^\\]+\\LS:USrv - Cluster Manager\\USrv - Whether server is connected to fabric pool manager", "sfb.usrv_cluster_manager.whether_server_is_connected_to_fabric_pool_manager");
        reg.Add(@"\\[^\\]+\\LS:XmppFederation - SIP Instant Messaging\\XmppFederation - Failure IMDNs sent\/sec", "sfb.xmppfederation_sip_instant_messaging.failure_imdns_sent_per_sec");
        reg.Add(@"\\[^\\]+\\LS:RoutingApps - Emergency Call Routing\\RoutingApps - Number of incoming failure responses", "sfb.routingapps_emergency_call_routing.number_of_incoming_failure_responses");
        reg.Add(@"\\[^\\]+\\LS:LYSS - Storage Service API\\LYSS - Current percentage of space used by Storage Service DB\.", "sfb.lyss_storage_service_api.current_percentage_of_space_used_by_storage_service_db");
        reg.Add(@"\\[^\\]+\\LS:DATAMCU - MCU Health And Performance\\DATAMCU - MCU Health State", "sfb.datamcu_mcu_health_and_performance.mcu_health_state");
        reg.Add(@"\\[^\\]+\\LS:AVMCU - MCU Health And Performance\\AVMCU - MCU Health State", "sfb.avmcu_mcu_health_and_performance.mcu_health_state");
        reg.Add(@"\\[^\\]+\\LS:AsMcu - MCU Health And Performance\\ASMCU - MCU Health State", "sfb.asmcu_mcu_health_and_performance.mcu_health_state");
        reg.Add(@"\\[^\\]+\\LS:ImMcu - MCU Health And Performance\\IMMCU - MCU Health State", "sfb.immcu_mcu_health_and_performance.mcu_health_state");
        reg.Add(@"\\[^\\]+\\LS:ImMcu - IMMcu Conferences\\IMMCU - Throttled Sip Connections", "sfb.immcu_immcu_conferences.throttled_sip_connections");
        reg.Add(@"\\[^\\]+\\LS:WEB - Distribution List Expansion\\WEB - Timed out Active Directory Requests\/sec", "sfb.web_distribution_list_expansion.timed_out_active_directory_requests_per_sec");
        reg.Add(@"\\[^\\]+\\LS:WEB - Address Book File Download\\WEB - Failed File Requests\/Second", "sfb.web_address_book_file_download.failed_file_requests_per_second");
        reg.Add(@"\\[^\\]+\\LS:JoinLauncher - Join Launcher Service Failures\\JOINLAUNCHER - Join failures", "sfb.joinlauncher_join_launcher_service_failures.join_failures");
        reg.Add(@"\\[^\\]+\\LS:WEB - UCWA\([^)]*\)\\UCWA - HTTP 5xx Responses\/Second", "sfb.web_ucwa.http_5xx_responses_per_second");
        reg.Add(@"\\[^\\]+\\LS:WEB - Auth Provider related calls\\WEB - Failed validate cert calls to the cert auth provider", "sfb.web_auth_provider_related_calls.failed_validate_cert_calls_to_the_cert_auth_provider");
        reg.Add(@"\\[^\\]+\\LS:WEB - Address Book Web Query\\WEB - Failed search requests\/sec", "sfb.web_address_book_web_query.failed_search_requests_per_sec");
        reg.Add(@"\\[^\\]+\\LS:WEB - Location Information Service\\WEB - Failed Get Locations Requests\/Second", "sfb.web_location_information_service.failed_get_locations_requests_per_second");
        reg.Add(@"\\[^\\]+\\LS:CAA - Operations\\CAA - Incomplete calls per sec", "sfb.caa_operations.incomplete_calls_per_sec");
        reg.Add(@"\\[^\\]+\\LS:USrv - Conference Control Notification\\USrv - Notifications in processing", "sfb.usrv_conference_control_notification.notifications_in_processing");
        reg.Add(@"\\[^\\]+\\LS:USrv - Conference Mcu Allocator\\USrv - Create Conference Latency \(msec\)", "sfb.usrv_conference_mcu_allocator.create_conference_latency_msec");
        reg.Add(@"\\[^\\]+\\LS:USrv - Conference Mcu Allocator\\USrv - Factory Call Latency \(msec\)", "sfb.usrv_conference_mcu_allocator.factory_call_latency_msec");
        reg.Add(@"\\[^\\]+\\LS:USrv - Conference Mcu Allocator\\USrv - Allocation Latency \(msec\)", "sfb.usrv_conference_mcu_allocator.allocation_latency_msec");
        reg.Add(@"\\[^\\]+\\LS:WEB - Mobile Communication Service\\WEB - Push Notification Requests Failed\/Second", "sfb.web_mobile_communication_service.push_notification_requests_failed_per_second");
        reg.Add(@"\\[^\\]+\\LS:WEB - Mobile Communication Service\\WEB - Push Notification Requests Throttled\/Second", "sfb.web_mobile_communication_service.push_notification_requests_throttled_per_second");
        reg.Add(@"\\[^\\]+\\LS:WEB - Mobile Communication Service\\WEB - Requests Failed\/Second", "sfb.web_mobile_communication_service.requests_failed_per_second");
        reg.Add(@"\\[^\\]+\\LS:WEB - Mobile Communication Service\\WEB - Requests Rejected\/Second", "sfb.web_mobile_communication_service.requests_rejected_per_second");
        reg.Add(@"\\[^\\]+\\LS:A\/V Auth - Requests\\- Bad Requests Received\/sec", "sfb.a_v_auth_requests.bad_requests_received_per_sec");
        reg.Add(@"\\[^\\]+\\LS:A\/V Edge - UDP Counters\([^)]*\)\\A\/V Edge - Authentication Failures\/sec", "sfb.a_v_edge_udp_counters.a_v_edge_authentication_failures_per_sec");
        reg.Add(@"\\[^\\]+\\LS:A\/V Edge - UDP Counters\([^)]*\)\\A\/V Edge - Allocate Requests Exceeding Port Limit\/sec", "sfb.a_v_edge_udp_counters.a_v_edge_allocate_requests_exceeding_port_limit_per_sec");
        reg.Add(@"\\[^\\]+\\LS:A\/V Edge - UDP Counters\([^)]*\)\\A\/V Edge - Packets Dropped\/sec", "sfb.a_v_edge_udp_counters.a_v_edge_packets_dropped_per_sec");
        reg.Add(@"\\[^\\]+\\LS:A\/V Edge - TCP Counters\([^)]*\)\\A\/V Edge - Authentication Failures\/sec", "sfb.a_v_edge_tcp_counters.a_v_edge_authentication_failures_per_sec");
        reg.Add(@"\\[^\\]+\\LS:A\/V Edge - TCP Counters\([^)]*\)\\A\/V Edge - Allocate Requests Exceeding Port Limit\/sec", "sfb.a_v_edge_tcp_counters.a_v_edge_allocate_requests_exceeding_port_limit_per_sec");
        reg.Add(@"\\[^\\]+\\LS:A\/V Edge - TCP Counters\([^)]*\)\\A\/V Edge - Packets Dropped\/sec", "sfb.a_v_edge_tcp_counters.a_v_edge_packets_dropped_per_sec");
        reg.Add(@"\\[^\\]+\\LS:SIP - Peers\([^)]*\)\\SIP - Above Limit Connections Dropped \(Access Proxies only\)", "sfb.sip_peers.above_limit_connections_dropped_access_proxies_only");
        reg.Add(@"\\[^\\]+\\LS:DATAPROXY - Server Connections\([^)]*\)\\DATAPROXY - Current count of server connections that are throttled", "sfb.dataproxy_server_connections.current_count_of_server_connections_that_are_throttled");
        reg.Add(@"\\[^\\]+\\LS:DATAPROXY - Server Connections\([^)]*\)\\DATAPROXY - System is throttling", "sfb.dataproxy_server_connections.system_is_throttling");
        reg.Add(@"\\[^\\]+\\LS:XmppFederationProxy - Streams\\XmppFederationProxy - Failed inbound stream establishes\/sec", "sfb.xmppfederationproxy_streams.failed_inbound_stream_establishes_per_sec");
        reg.Add(@"\\[^\\]+\\LS:XmppFederationProxy - Streams\\XmppFederationProxy - Failed outbound stream establishes\/sec", "sfb.xmppfederationproxy_streams.failed_outbound_stream_establishes_per_sec");
        reg.Add(@"\\[^\\]+\\LS:MediationServer - Media Relay\\- Candidates Missing", "sfb.mediationserver_media_relay.candidates_missing");
        reg.Add(@"\\[^\\]+\\LS:MediationServer - Media Relay\\- Media Connectivity Check Failure", "sfb.mediationserver_media_relay.media_connectivity_check_failure");
        reg.Add(@"\\[^\\]+\\LS:MediationServer - Health Indices\\- Load Call Failure Index", "sfb.mediationserver_health_indices.load_call_failure_index");
        reg.Add(@"\\[^\\]+\\LS:MediationServer - Global Counters\\- Total failed calls caused by unexpected interaction from the Proxy", "sfb.mediationserver_global_counters.total_failed_calls_caused_by_unexpected_interaction_from_the_proxy");
        reg.Add(@"\\[^\\]+\\LS:MediationServer - Global Per Gateway Counters\([^)]*\)\\- Total failed calls caused by unexpected interaction from a gateway", "sfb.mediationserver_global_per_gateway_counters.total_failed_calls_caused_by_unexpected_interaction_from_a_gateway");

        // classic-asp
        reg.Add(@"\\[^\\]+\\Active Server Pages\\Request Execution Time", "asp.request_execution_time");
        reg.Add(@"\\[^\\]+\\Active Server Pages\\Requests Queued", "asp.requests_queued");

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
