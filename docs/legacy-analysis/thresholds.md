# PAL v2 — Threshold Logic Patterns

Source: `legacy/pal-v2/PAL2/PALWizard/bin/Debug/PAL.ps1` and `*.xml` threshold files.

---

## Overview

Every threshold in PAL v2 follows the same execution pipeline:

1. An XML `<ANALYSIS>` element declares what counters to collect (`<DATASOURCE>`).
2. Each `<THRESHOLD>` element embeds a PowerShell `<CODE>` block that calls `StaticThreshold`.
3. PAL's engine (`ProcessThresholds` → `ProcessThreshold` → `ExecuteCodeForThreshold`) invokes `Invoke-Expression` on that code block per analysis.
4. `StaticThreshold` evaluates the counter collection and calls `CreateAlert` for any broken time slices.

---

## Core Evaluation Functions

### `StaticThreshold` (PAL.ps1:4259)

The primary threshold function. Iterates every counter instance in a collection and every quantized time slice.

**Signature:**
```powershell
StaticThreshold -CollectionOfCounterInstances $collection -Operator 'gt' -Threshold 80 [-IsTrendOnly $False]
```

**Operators supported:** `gt`, `ge`, `lt`, `le`, `eq` (default falls through to `gt`)

**Per time slice, evaluates three statistics independently:**
- `QuantizedMin[$t]` — minimum value within the time slice
- `QuantizedAvg[$t]` — average value within the time slice
- `QuantizedMax[$t]` — maximum value within the time slice

Each of the three comparisons sets its own `IsMinThresholdBroken`, `IsAvgThresholdBroken`, `IsMaxThresholdBroken` flag. An alert fires if **any one of them** is true.

**Trend mode** (`-IsTrendOnly $True`): Evaluates `QuantizedTrend[$t]` (the calculated hourly slope across the time series) instead of Min/Avg/Max. Used for leak detection.

### `OverallStaticThreshold` (PAL.ps1:3989)

Called automatically by `StaticThreshold` when the counter instance name contains `INTERNAL_OVERALL_COUNTER_STATS_`. Operates on the aggregate `Min`, `Avg`, `Max` across the entire log (not per time slice), producing a single alert at time slice 0.

### `StaticChartThreshold` (PAL.ps1:3117)

Used exclusively inside `<CHART><SERIES>` blocks to define the yellow/red band range on charts. Does not create alerts. Returns a constant-value series across the data range.

**Signature:**
```powershell
StaticChartThreshold -CollectionOfCounterInstances $collection -MinThreshold 300 -MaxThreshold 640 [-UseMaxValue $True] [-IsOperatorGreaterThan $True]
```

- `IsOperatorGreaterThan $True` (default): the bad zone is **above** `MaxThreshold`; the band extends up to the data's own max.
- `IsOperatorGreaterThan $False`: the bad zone is **below** `MinThreshold`; the band extends down to the data's own min (used for "should stay above" counters like Page Life Expectancy).

---

## Severity Levels

Two severity levels, encoded on the `<THRESHOLD>` element:

| Level    | COLOR    | PRIORITY | Rendered as              |
|----------|----------|----------|--------------------------|
| Warning  | `Yellow` | `50`     | Yellow background in HTML |
| Critical | `Red`    | `100`    | Red background in HTML    |

The `CONDITIONCOLOR` attribute on an alert carries the hex or named color into the HTML renderer. White (`#FFFFFF`) means "evaluated but not broken."

---

## Threshold Value Patterns

### 1. Absolute Static Threshold

Direct numeric comparison against a fixed value.

```xml
<THRESHOLD CONDITION="Warning" COLOR="Yellow" PRIORITY="50">
  <CODE><![CDATA[
    StaticThreshold -CollectionOfCounterInstances $CollectionOfProcessorPercent -Operator 'gt' -Threshold 50
  ]]></CODE>
</THRESHOLD>
```

Used for: CPU %, network queue length, worker process failure counts, SQL lazy writes.

### 2. Dynamic Threshold — Scaled by Physical RAM

The threshold value is computed from a `$PhysicalMemory` question variable (GB entered by user).

```powershell
# Warning: <10% of RAM available
$TenPercentOfPhysicalMemory = $([Int] $PhysicalMemory * 1024) * 0.10
StaticThreshold -CollectionOfCounterInstances $CollectionOfAvailableMBytes -Operator 'lt' -Threshold $TenPercentOfPhysicalMemory

# Critical: <5% or <64 MB
$FivePercentOfPhysicalMemory = $([Int] $PhysicalMemory * 1024) * 0.05
If ($FivePercentOfPhysicalMemory -lt 64) { $FivePercentOfPhysicalMemory = 64 }
StaticThreshold -CollectionOfCounterInstances $CollectionOfAvailableMBytes -Operator 'lt' -Threshold $FivePercentOfPhysicalMemory
```

Used for: `\Memory\Available MBytes`, `\Memory\Committed Bytes`, paging file usage.

### 3. Dynamic Threshold — Scaled by CPU Count

The threshold is multiplied by the number of logical processors (derived from the size of the `\Processor(*)\% Processor Time` collection minus the `_Total` instance).

```powershell
[Int] $NumberOfLogicalProcessors = $CollectionOfProcessorPercentProcessorTime.Count - 1
$OverallProcessThreshold = $NumberOfLogicalProcessors * 50   # 50% per-core scaled to total
StaticThreshold -CollectionOfCounterInstances $ProcessPercentProcessorTimeALL -Operator 'gt' -Threshold $OverallProcessThreshold
```

Used for: per-process CPU (scales warning/critical to the number of cores on the machine).

### 4. Dynamic Threshold — OS Address Space (32-bit vs 64-bit)

Thresholds for virtual address space differ by architecture. PAL uses the `$OS` question variable (first two chars are `"32"` for 32-bit).

```powershell
$SixtyFourBit = $OS.substring(0,2)
If ($SixtyFourBit -eq '32') {
    $MaxProcessAddressSpace = 2GB
} Else {
    $MaxProcessAddressSpace = 8TB
}
$PercentageOfMaxProcessAddressSpace = 0.60 * $MaxProcessAddressSpace
StaticThreshold -CollectionOfCounterInstances $ProcessVirtualBytesALL -Operator 'gt' -Threshold $PercentageOfMaxProcessAddressSpace
```

Used for: `\Process(*)\Virtual Bytes`.

### 5. Ratio Threshold — Counter Normalized Against Batch Requests

PAL generates a derived metric by dividing a secondary counter by `Batch Requests/sec`, then thresholds the ratio as a percentage.

```powershell
# Generated datasource computes:
[int]$iRatio = ([double]$ForwardedRecords / [double]$BatchRequests) * 100

# Threshold fires if ratio > 10%
StaticThreshold -CollectionOfCounterInstances $CollectionOfPalGeneratedForwardedRecordsToBatchRequestsRatioPercentage -Operator 'gt' -Threshold 10
```

Pattern used for: Forwarded Records, FreeSpace Scans, Page Splits, Workfiles Created, Page Lookups — all normalized per batch request to remain valid across low/high load.

### 6. Trend-Only Threshold (Leak Detection)

Uses `IsTrendOnly=$True` to evaluate only the hourly slope of the counter, not its absolute value. Fires when the trend (rate of increase per hour) exceeds the threshold.

```powershell
# Process Private Bytes growing >100 MB/hour
StaticThreshold -CollectionOfCounterInstances $ProcessPrivateBytes -Operator 'gt' -Threshold 100MB -IsTrendOnly $True

# Process Handle Count growing >100 handles/hour
StaticThreshold -CollectionOfCounterInstances $ProcessHandleCount -Operator 'gt' -Threshold 100 -IsTrendOnly $True
```

Used for: memory leak detection, handle leak detection, thread count trends.

---

## Threshold Inheritance and Composition

Threshold files can inherit counters and analyses from parent files:

```xml
<INHERITANCE FILEPATH="AspDotNet.xml" />
<INHERITANCE FILEPATH="SystemOverview.xml" />
```

The `InheritFromThresholdFiles()` function merges parent XML into the active document. Duplicate detection uses both `ID` (GUID) and `NAME` attributes to prevent double-counting. Circular includes are blocked by a visited-file dictionary.

Inheritance chain example for `IIS.xml`:
- `IIS.xml` → inherits `AspDotNet.xml` → inherits `SystemOverview.xml` → inherits `QuickSystemOverview.xml`

---

## Alert Metadata

Each fired alert (`<ALERT>`) carries:

| Attribute        | Description                                    |
|------------------|------------------------------------------------|
| `TIMESLICEINDEX` | Which quantized time slice triggered the alert |
| `CONDITION`      | `Warning` or `Critical`                        |
| `CONDITIONCOLOR` | Hex or named color                             |
| `COUNTER`        | Full counter path including computer/instance  |
| `MIN`/`AVG`/`MAX`| Quantized statistics for that time slice       |
| `TREND`          | Hourly slope for that time slice               |
| `MINCOLOR`/`AVGCOLOR`/`MAXCOLOR`/`TRENDCOLOR` | Per-column color (white if not violated) |
| `MINPRIORITY`/etc. | Priority of the threshold that fired each column |
| `PARENTANALYSIS` | Name of the owning analysis (for HTML links)   |
| `ISINTERNALONLY` | `True` for `INTERNAL_OVERALL_COUNTER_STATS_` alerts (hidden from UI) |
