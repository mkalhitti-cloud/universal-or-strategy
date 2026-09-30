# V12 Telemetry → Paperclip Wiki Integration Plan

**Status**: DESIGN PHASE (REVISED)  
**Build**: V12.18 + Paperclip Wiki Plugin  
**Date**: 2026-05-25  
**Objective**: Integrate V12 distributed tracing with existing Paperclip LLM Wiki for compounding intelligence

---

## Executive Summary

**CRITICAL DISCOVERY**: The "compounding intelligence plugin" already exists as **Paperclip's LLM Wiki** (`infrastructure/paperclip/packages/plugins/plugin-llm-wiki/`).

The Paperclip Wiki implements:
- ✅ Persistent knowledge storage (`wiki/` directories)
- ✅ Compounding artifacts (synthesis across runs)
- ✅ Agent skills: `wiki-ingest`, `wiki-query`, `wiki-lint`
- ✅ Structured knowledge graph (sources, projects, entities, concepts, synthesis)

**New Goal**: Connect V12 telemetry traces to Paperclip Wiki so trading session insights compound into persistent intelligence.

---

## Architecture Overview

```
V12 Strategy (C#)
  ↓ [Telemetry Events]
Phoenix/Arize (Observability)
  ↓ [Trace Export]
Paperclip Wiki Ingest
  ↓ [wiki-ingest skill]
Wiki Knowledge Base
  ├── wiki/sources/v12-traces/
  ├── wiki/projects/v12-trading/
  ├── wiki/entities/fsm-actors/
  ├── wiki/concepts/latency-patterns/
  └── wiki/synthesis/session-insights/
```

---

## Phase 1: V12 Telemetry Activation (UNCHANGED)

**See**: `docs/brain/tracing-integration-plan.md` Phase 1

Wire telemetry calls to FSM operations:
- `TrackFsmTransition()` in `Enqueue()`
- `TrackSimaDispatch()` in SIMA fleet
- `TrackReaperAudit()` in REAPER
- `EmitMetricsSummary()` in `OnStateChangeTerminated()`

---

## Phase 2: Phoenix/Arize Export (UNCHANGED)

**See**: `docs/brain/tracing-integration-plan.md` Phase 2

Export traces to Phoenix/Arize for real-time observability.

---

## Phase 3: Paperclip Wiki Integration (NEW)

### 3.1 Wiki Structure for V12 Traces

**Wiki Root**: `C:\Users\<User>\Documents\Paperclip\Wikis\v12-trading\`

**Directory Structure**:
```
wiki/
├── sources/
│   └── v12-traces/
│       ├── session-2026-05-25-143045.md
│       ├── session-2026-05-24-091230.md
│       └── ...
├── projects/
│   └── v12-trading/
│       ├── index.md
│       ├── standup.md
│       └── architecture.md
├── entities/
│   ├── fsm-actors.md
│   ├── sima-fleet.md
│   ├── reaper-audit.md
│   └── symmetry-fsm.md
├── concepts/
│   ├── latency-patterns.md
│   ├── order-submission-timing.md
│   ├── ipc-command-volume.md
│   └── fsm-transition-rate.md
└── synthesis/
    ├── session-insights-2026-05.md
    ├── performance-trends.md
    └── anomaly-patterns.md
```

---

### 3.2 Trace Ingestion via `wiki-ingest` Skill

**Mechanism**: After each V12 session, export trace data as markdown and ingest into Paperclip Wiki.

**Implementation**:

1. **V12 Session Export** (C# in `V12_002.ObsidianExporter.cs`):
   - Generate markdown report (same as Phase 3 in original plan)
   - Save to `wiki/sources/v12-traces/session-{timestamp}.md`

2. **Paperclip Wiki Ingest** (TypeScript):
   - Use `wiki-ingest` skill to process new trace files
   - Extract entities (FSM actors, modules)
   - Extract concepts (latency patterns, error types)
   - Link to existing wiki pages

**Example Trace Source File** (`wiki/sources/v12-traces/session-2026-05-25-143045.md`):

```markdown
---
source_type: v12_trace
session_id: 2026-05-25-143045
build_tag: V12.18
duration_minutes: 383
total_traces: 12345
---

# V12 Trading Session - 2026-05-25 14:30:45

## Session Metrics

| Metric | Count |
|--------|-------|
| FSM Transitions | 12,345 |
| SIMA Dispatches | 567 |
| Reaper Audits | 89 |
| Order Submissions | 345 |
| IPC Commands | 123 |

## Top 10 Slowest Operations

| Module | Operation | Duration (ms) | Trace ID |
|--------|-----------|---------------|----------|
| SIMA.Fleet | DispatchFleet | 245 | 00042 |
| REAPER.Audit | AuditApexPositions | 189 | 00123 |

## Error Events

| Trace ID | Module | Error | Timestamp |
|----------|--------|-------|-----------|
| 00567 | Symmetry.FSM | Order rejection: Insufficient margin | 2026-05-25T14:23:45Z |

## Entities Referenced

- [[FSM Actors]] (12,345 transitions)
- [[SIMA Fleet]] (567 dispatches)
- [[REAPER Audit]] (89 cycles)

## Concepts Observed

- [[Latency Patterns]]: SIMA dispatch latency spiked during high IPC volume
- [[Order Submission Timing]]: 345 submissions, 2 rejections
```

---

### 3.3 Wiki Query for Historical Analysis

**Use Case**: Agent queries wiki to answer questions like:

- "What are the historical latency patterns for SIMA dispatch?"
- "How many order rejections occurred in the last 30 days?"
- "What correlations exist between IPC command volume and dispatch latency?"

**Implementation**: Use `wiki-query` skill with natural language queries.

**Example Query**:
```
Query: "What are the top 3 latency patterns observed in V12 trading sessions?"

Response (from wiki/synthesis/performance-trends.md):
1. SIMA dispatch latency correlates with IPC command volume (r=0.78)
2. Reaper audit cycles cause temporary order submission delays
3. Symmetry FSM Replace operations spike during high volatility
```

---

### 3.4 Wiki Synthesis for Compounding Intelligence

**Mechanism**: Periodically run `wiki-lint` and manual synthesis to aggregate insights across sessions.

**Synthesis Files**:

1. **`wiki/synthesis/session-insights-2026-05.md`**:
   - Aggregates all May 2026 sessions
   - Identifies trends (e.g., "Order submission rate increased 23% vs. April")
   - Flags anomalies (e.g., "3 sessions had >2σ SIMA latency")

2. **`wiki/synthesis/performance-trends.md`**:
   - Long-term performance trends
   - Baseline metrics (mean, stddev for each counter)
   - Recommendations (e.g., "Implement IPC rate limiting")

3. **`wiki/synthesis/anomaly-patterns.md`**:
   - Catalog of known anomalies
   - Root cause analysis
   - Mitigation strategies

**Example Synthesis** (`wiki/synthesis/session-insights-2026-05.md`):

```markdown
# V12 Session Insights - May 2026

**Sessions Analyzed**: 23  
**Total Traces**: 284,135  
**Date Range**: 2026-05-01 to 2026-05-25

## Key Findings

### 1. SIMA Dispatch Latency Correlation

**Pattern**: SIMA dispatch latency increases linearly with IPC command volume.

**Evidence**:
- [[session-2026-05-15-091230]]: 567 dispatches, avg 245ms, 123 IPC commands
- [[session-2026-05-20-143045]]: 789 dispatches, avg 312ms, 189 IPC commands
- Correlation coefficient: r=0.78 (p<0.01)

**Recommendation**: Implement IPC rate limiting to prevent dispatch queue saturation.

### 2. Order Submission Rate Increase

**Trend**: Order submission rate increased 23% vs. April 2026.

**Evidence**:
- April avg: 281 submissions/session
- May avg: 345 submissions/session
- Growth rate: +23%

**Hypothesis**: Increased market volatility driving more entry signals.

### 3. Reaper Audit Frequency

**Observation**: Reaper audit cycles occur every 89 FSM transitions on average.

**Evidence**:
- May sessions: 89 ± 12 transitions between audits
- April sessions: 92 ± 15 transitions between audits
- Stable pattern across 2 months

**Conclusion**: Reaper audit frequency is consistent and predictable.

## Anomalies Detected

### Session 2026-05-18-102345

**Anomaly**: SIMA dispatch latency exceeded 2σ (avg 512ms vs. baseline 245ms).

**Root Cause**: Network congestion during market open.

**Mitigation**: None required (transient event).

## Recommendations

1. **Implement IPC Rate Limiting**: Cap IPC commands at 100/minute to prevent dispatch saturation.
2. **Monitor Order Submission Rate**: Track for sustained growth (may indicate strategy drift).
3. **Baseline Reaper Audit Timing**: Use 89 transitions as expected interval for anomaly detection.

## Links

- [[V12 Trading Project]]
- [[FSM Actors]]
- [[SIMA Fleet]]
- [[Performance Trends]]
```

---

## Phase 4: Automated Wiki Maintenance

### 4.1 Post-Session Ingest Workflow

**Trigger**: After `OnStateChangeTerminated()` completes

**Steps**:
1. V12 exports trace markdown to `wiki/sources/v12-traces/`
2. Paperclip agent runs `wiki-ingest` skill
3. Agent extracts entities and concepts
4. Agent updates `wiki/log.md` with session summary
5. Agent flags synthesis tasks (e.g., "Update May insights")

---

### 4.2 Weekly Synthesis Workflow

**Trigger**: Every Sunday at 00:00 UTC

**Steps**:
1. Paperclip agent runs `wiki-query` to aggregate last 7 days
2. Agent generates synthesis markdown
3. Agent updates `wiki/synthesis/session-insights-{YYYY-MM}.md`
4. Agent flags anomalies for human review

---

### 4.3 Monthly Trend Analysis

**Trigger**: First day of each month

**Steps**:
1. Paperclip agent runs `wiki-query` to aggregate previous month
2. Agent compares to historical baseline
3. Agent updates `wiki/synthesis/performance-trends.md`
4. Agent generates recommendations

---

## Phase 5: Integration with Phoenix/Arize

### 5.1 Bidirectional Sync

**Phoenix → Wiki**:
- Export Phoenix traces to markdown
- Ingest into `wiki/sources/v12-traces/`

**Wiki → Phoenix**:
- Tag Phoenix traces with wiki page links
- Embed wiki insights in Phoenix dashboard

---

### 5.2 Phoenix Dashboard Widgets

**Widget 1: Wiki Insights Panel**
- Display latest synthesis from `wiki/synthesis/session-insights-{YYYY-MM}.md`
- Link to full wiki page

**Widget 2: Anomaly Alerts**
- Query `wiki/synthesis/anomaly-patterns.md`
- Highlight known patterns in current session

---

## Phase 6: Configuration & Deployment

### 6.1 Paperclip Wiki Setup

**Install Paperclip**:
```bash
npm install -g @paperclipai/cli
paperclip init
```

**Create V12 Trading Wiki**:
```bash
paperclip wiki create v12-trading
cd ~/Documents/Paperclip/Wikis/v12-trading
```

**Install LLM Wiki Plugin**:
```bash
paperclip plugin install @paperclipai/plugin-llm-wiki
```

---

### 6.2 V12 Configuration

**Add to `src/V12_002.Properties.cs`**:
```csharp
[Display(Name = "Paperclip Wiki Path", GroupName = "Telemetry", Order = 10)]
public string PaperclipWikiPath
{
    get { return _paperclipWikiPath; }
    set { _paperclipWikiPath = value; }
}
```

**Default Path**: `C:\Users\<User>\Documents\Paperclip\Wikis\v12-trading\wiki\sources\v12-traces\`

---

### 6.3 Automated Ingest Script

**File**: `scripts/ingest_v12_traces.ps1`

```powershell
# Ingest V12 traces into Paperclip Wiki
param(
    [string]$WikiPath = "$env:USERPROFILE\Documents\Paperclip\Wikis\v12-trading",
    [string]$TracesDir = "$WikiPath\wiki\sources\v12-traces"
)

# Find new trace files (not yet ingested)
$newTraces = Get-ChildItem -Path $TracesDir -Filter "session-*.md" | 
    Where-Object { -not (Select-String -Path "$WikiPath\wiki\log.md" -Pattern $_.Name -Quiet) }

if ($newTraces.Count -eq 0) {
    Write-Host "No new traces to ingest."
    exit 0
}

Write-Host "Ingesting $($newTraces.Count) new trace files..."

# Run Paperclip wiki-ingest skill
foreach ($trace in $newTraces) {
    Write-Host "Ingesting: $($trace.Name)"
    paperclip run wiki-ingest --file "$($trace.FullName)" --wiki-id v12-trading
}

Write-Host "Ingest complete."
```

**Schedule**: Run after each V12 session via Task Scheduler or cron.

---

## Success Metrics

1. **Trace Coverage**: 100% of V12 sessions exported to wiki
2. **Ingest Reliability**: >99% of trace files successfully ingested
3. **Synthesis Quality**: >80% of generated insights actionable by human review
4. **Query Accuracy**: Wiki queries return relevant results in <2s
5. **Compounding Evidence**: Each new session adds to historical knowledge base

---

## Risk Assessment

### High Risk
- **Wiki Corruption**: Malformed markdown could break wiki index
  - **Mitigation**: Validate markdown before ingest; use `wiki-lint` to detect issues

### Medium Risk
- **Storage Growth**: Wiki could grow unbounded with trace files
  - **Mitigation**: Archive old traces (>90 days) to separate directory

### Low Risk
- **Ingest Latency**: Large trace files could slow ingest
  - **Mitigation**: Batch ingest; run async after session

---

## Next Steps

1. **Immediate**: Wire V12 telemetry calls (Phase 1) → Switch to `code` mode
2. **Short-term**: Build V12 markdown exporter → New file `V12_002.PaperclipExporter.cs`
3. **Medium-term**: Set up Paperclip Wiki → Install plugin, create wiki structure
4. **Long-term**: Automate ingest workflow → PowerShell script + Task Scheduler

---

## References

- **Paperclip Wiki Plugin**: `infrastructure/paperclip/packages/plugins/plugin-llm-wiki/`
- **Wiki Skills**: `wiki-ingest`, `wiki-query`, `wiki-lint`
- **V12 Telemetry**: `src/V12_002.Telemetry.cs`
- **Original Plan**: `docs/brain/tracing-integration-plan.md`

---

**Plan Status**: READY FOR IMPLEMENTATION  
**Estimated Effort**: 2-3 days (P4/P5 combined)  
**Blocking Issues**: None  
**Dependencies**: Paperclip CLI, LLM Wiki plugin
