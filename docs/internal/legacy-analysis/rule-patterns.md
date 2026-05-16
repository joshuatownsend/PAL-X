# PAL v2 — Rule Patterns and Finding Explanations

Source: `legacy/pal-v2/PAL2/PALWizard/bin/Debug/*.xml` threshold files.

---

## Rule Anatomy

Every analysis rule in PAL v2 consists of four cooperating pieces:

```xml
<ANALYSIS NAME="..." CATEGORY="..." ENABLED="True" ID="{GUID}">
  <DATASOURCE ... />        <!-- what to collect -->
  <THRESHOLD ...>           <!-- when to fire -->
    <CODE>...</CODE>
  </THRESHOLD>
  <CHART ...>               <!-- how to visualize -->
    <SERIES NAME="Warning">...</SERIES>
  </CHART>
  <DESCRIPTION>...</DESCRIPTION>  <!-- how to explain it -->
</ANALYSIS>
```

The `<DESCRIPTION>` block is rendered verbatim into the HTML report per analysis. It is the primary vehicle for communicating *why* a finding matters and *what to do*.

---

## Pattern 1: Simple Absolute Threshold

**Shape:** Single counter, single fixed numeric limit, one or two severity levels.

**Example — CPU utilization:**
```xml
<THRESHOLD NAME="More than 50% processor utilization" CONDITION="Warning" COLOR="Yellow" PRIORITY="50">
  <CODE><![CDATA[
    StaticThreshold -CollectionOfCounterInstances $CollectionOfPercentProcessorTime -Operator 'gt' -Threshold 50
  ]]></CODE>
</THRESHOLD>
<THRESHOLD NAME="More than 80% processor utilization" CONDITION="Critical" COLOR="Red" PRIORITY="100">
  <CODE><![CDATA[
    StaticThreshold -CollectionOfCounterInstances $CollectionOfPercentProcessorTime -Operator 'gt' -Threshold 80
  ]]></CODE>
</THRESHOLD>
```

**What it fires on:** Any quantized time slice where Min, Avg, or Max exceeds the threshold.

**Explanation pattern in `<DESCRIPTION>`:**
1. Technical definition of the counter
2. Why the value matters (performance impact)
3. Threshold values stated plainly: "Yellow: > 50%, Red: > 80%"
4. Next steps: what to investigate
5. References: MSDN links, blog posts

**Used for:** CPU %, disk latency, network queue length, SQL lock waits, IIS worker process failures.

---

## Pattern 2: Inverted Threshold (Should Stay Above)

**Shape:** `Operator 'lt'` — fires when the counter *falls below* the limit. Always paired with `BACKGRADIENTSTYLE="BottomTop"` on the chart so the "bad zone" renders at the bottom.

**Example — SQL Page Life Expectancy:**
```xml
<THRESHOLD NAME="Page life expectancy is less then 5 minutes" CONDITION="Critical" COLOR="Red" PRIORITY="100">
  <CODE><![CDATA[
    StaticThreshold -CollectionOfCounterInstances $CollectionOfSQLServerBufferManagerPagelifeexpectancy -Operator 'lt' -Threshold 300
  ]]></CODE>
</THRESHOLD>
```

**Example — SQL Buffer Cache Hit Ratio:**
```xml
<THRESHOLD NAME="Less than 97 percent buffer cache hit ratio" CONDITION="Warning" COLOR="Yellow" PRIORITY="50">
  <CODE><![CDATA[
    StaticThreshold -CollectionOfCounterInstances $CollectionOfSQLServerBufferManagerBuffercachehitratio -Operator 'lt' -Threshold 97
  ]]></CODE>
</THRESHOLD>
```

**Explanation pattern:**
- Explains what the counter represents as a "health" metric (higher = healthier)
- Threshold restated as the minimum acceptable value
- Correlation with related counters (e.g., PLE low → Lazy Writes high → physical reads increase → disk latency increases)
- A causal chain linking the finding to downstream symptoms

**Used for:** `Page Life Expectancy`, `Buffer cache hit ratio`, `Available MBytes`, `% Idle Time` on disks.

---

## Pattern 3: Trend Threshold (Leak Detection)

**Shape:** `IsTrendOnly=$True` — evaluates the per-hour slope of the counter value, not the absolute value. Fires when the counter is growing at a rate exceeding the threshold.

**Example — memory leak:**
```xml
<THRESHOLD NAME="Increasing trend of more than 100 MB per hour" CONDITION="Warning" COLOR="Yellow" PRIORITY="51">
  <CODE><![CDATA[
    StaticThreshold -CollectionOfCounterInstances $ProcessPrivateBytes -Operator 'gt' -Threshold 100MB -IsTrendOnly $True
  ]]></CODE>
</THRESHOLD>
```

**Example — handle leak:**
```xml
<THRESHOLD NAME="Increasing trend of more than 100 handles per hour" CONDITION="Warning" COLOR="Yellow" PRIORITY="50">
  <CODE><![CDATA[
    StaticThreshold -CollectionOfCounterInstances $ProcessHandleCount -Operator 'gt' -Threshold 100 -IsTrendOnly $True
  ]]></CODE>
</THRESHOLD>
```

**Note from source:** Descriptions frequently include the caveat "may not be accurate on counter logs of less than 1 hour" — the slope calculation needs sufficient data points to be meaningful.

**Explanation pattern:**
- Distinguishes between "large but stable" (acceptable) and "growing" (leak symptom)
- Points to Debug Diag or similar profiler tools for confirmation
- Pairs with the absolute-value threshold at a different priority level (e.g., "trend > 100 MB/hr" at priority 51 AND "value > 1 GB" at priority 50 — different thresholds, same analysis)

**Used for:** `Process Private Bytes`, `Process Handle Count`, `Process Thread Count`, `Process Working Set`, `Memory Pool Nonpaged Bytes`.

---

## Pattern 4: Dynamic Threshold — RAM-Relative

**Shape:** Threshold value computed from `$PhysicalMemory` (a user-answered question), so the rule adapts to the server's memory configuration.

**Example — Available MBytes:**
```xml
<!-- Warning: less than 10% of installed RAM -->
$TenPercentOfPhysicalMemory = $([Int] $PhysicalMemory * 1024) * 0.10
StaticThreshold -CollectionOfCounterInstances $CollectionOfAvailableMBytes -Operator 'lt' -Threshold $TenPercentOfPhysicalMemory

<!-- Critical: less than 5% or 64 MB, whichever is larger -->
$FivePercentOfPhysicalMemory = $([Int] $PhysicalMemory * 1024) * 0.05
If ($FivePercentOfPhysicalMemory -lt 64) { $FivePercentOfPhysicalMemory = 64 }
StaticThreshold -CollectionOfCounterInstances $CollectionOfAvailableMBytes -Operator 'lt' -Threshold $FivePercentOfPhysicalMemory
```

**Explanation pattern:**
- States the threshold as a percentage (not just an absolute): "Less than 10% of installed RAM"
- Notes the floor (64 MB minimum) as a safety net for systems with very small RAM
- Directs user to check correlated memory counters (paging file, committed bytes)

**Used for:** Available MBytes, Committed Bytes in Use, Paging File % Usage.

---

## Pattern 5: Dynamic Threshold — CPU-Count-Relative

**Shape:** Threshold multiplied by the number of logical processors detected from the `\Processor(*)\% Processor Time` counter collection size.

**Example — per-process CPU:**
```xml
[Int] $NumberOfLogicalProcessors = $CollectionOfProcessorPercentProcessorTime.Count - 1
$OverallProcessThreshold = $NumberOfLogicalProcessors * 50
StaticThreshold -CollectionOfCounterInstances $ProcessPercentProcessorTimeALL -Operator 'gt' -Threshold $OverallProcessThreshold
```

**Why:** A single process consuming 50% of CPU on a 4-core machine is 12.5% of total capacity — very different from 50% on a 1-core machine. This pattern scales the warning appropriately.

**Explanation pattern:**
- Notes that the threshold is "50% of overall processor time" — expressed relative to total capacity
- Distinguishes user-mode vs. kernel-mode CPU as separate concerns
- References profiler tools (KernRate, Debug Diag, DebugDiag)

**Used for:** `\Process(*)\% Processor Time`, `\Process(*)\% Privileged Time`, `\System\Context Switches/sec`.

---

## Pattern 6: Normalized Ratio Threshold (Workload-Relative)

**Shape:** A secondary counter is divided by `Batch Requests/sec` (the SQL Server throughput baseline) and expressed as a percentage. The threshold fires on the *ratio*, not the raw value.

**Example — Forwarded Records:**
```xml
<!-- Generated datasource computes the ratio per instance -->
[int]$iRatio = ([double]$ForwardedRecords / [double]$BatchRequests) * 100

<!-- Threshold on the derived metric -->
<THRESHOLD NAME="A ratio of more than 1 forwarded record for every 10 batch requests" CONDITION="Warning" COLOR="Yellow" PRIORITY="50">
  <CODE><![CDATA[
    StaticThreshold -CollectionOfCounterInstances $CollectionOfPalGeneratedForwardedRecordsToBatchRequestsRatioPercentage -Operator 'gt' -Threshold 10
  ]]></CODE>
</THRESHOLD>
```

**Why this pattern exists:** A raw counter value like "500 page splits/sec" is meaningless without knowing whether the server is doing 100 or 10,000 batch requests/sec. Normalizing eliminates false positives on busy servers and false negatives on idle ones.

**Instance matching:** The generated datasource iterates both collections and matches counters by SQL Server instance name (`ExtractSqlNamedInstanceFromCounterObjectPath`), ensuring ratios are computed per named instance rather than cross-instance.

**Explanation pattern:**
- States the threshold as a ratio: "more than 1 per every 10 batch requests"
- Explains what causes the symptom (e.g., heap tables, missing clustered indexes)
- Lists DBA-actionable remediation steps (add clustered index, increase fillfactor, use CHAR instead of VARCHAR)
- References DMVs and profiler events for diagnosis (e.g., `sys.dm_db_index_physical_stats`)

**Used for:** All `\SQLServer:Access Methods\*` counters, `Buffer Manager\Page lookups/sec`, `SQL Statistics\Compilations/sec`.

---

## Pattern 7: Existence / Any-Nonzero Threshold

**Shape:** `Operator 'ge' -Threshold 1` — fires as soon as any non-zero value appears. Used for counters where any occurrence represents a problem.

**Examples:**
```xml
<!-- IIS: worker process failure -->
StaticThreshold -CollectionOfCounterInstances $CollectionOfAPPPOOLWASRecentWorkerProcessFailures -Operator 'ge' -Threshold 1

<!-- SQL: deadlock -->
StaticThreshold -CollectionOfCounterInstances $CollectionOfSQLServerLocksNumberofDeadlockssec -Operator 'gt' -Threshold 0

<!-- ASP.NET: requests rejected (HTTP 503) -->
StaticThreshold -CollectionOfCounterInstances $CollectionOfASPNETRequestsRejected -Operator 'gt' -Threshold 0
```

**Explanation pattern:**
- Binary framing: "any occurrence warrants investigation"
- Describes the operational impact (service unavailability, data contention)
- Immediate next-steps focused (check event log, check application errors, check deadlock trace)

**Used for:** Worker process failures, deadlocks, requests rejected, application restarts.

---

## Description Template (from XML source)

Every `<DESCRIPTION>` block in PAL's XML follows a consistent internal structure:

```
<B>Counter Name</B>
<BR>
<B>Description:</B> [What the counter measures, technically]
[Explanation of performance impact and significance]

<B>Threshold:</B>
<B>Yellow:</B> [Warning condition in plain language]
<B>Red:</B> [Critical condition in plain language]

<B>Next Steps:</B>
[Ordered list of diagnostic and remediation actions]

<B>Reference:</B>
[Links to MSDN, TechNet, blog posts, KB articles]
```

Not all analyses use all sections — stats-only analyses (no thresholds) may have only the Description.
Finding descriptions consistently:
1. Separate what you observe from what it means
2. State the threshold values in plain language (not just the operator and number)
3. Provide actionable next steps, not just "investigate further"
4. Cite authoritative sources

---

## Finding Types Summary

| Pattern | Key Indicator | Fires When |
|---------|---------------|------------|
| Absolute | Fixed limit | Value exceeds (or falls below) a constant |
| Inverted | Fixed limit, `lt` | Value falls below minimum acceptable |
| Trend | Hourly slope | Counter growing faster than X per hour |
| RAM-relative | % of installed RAM | Proportional to system memory |
| CPU-relative | × logical processors | Proportional to core count |
| Ratio/normalized | ÷ batch requests | Workload-relative secondary metric |
| Existence | Any nonzero | Single occurrence is significant |
