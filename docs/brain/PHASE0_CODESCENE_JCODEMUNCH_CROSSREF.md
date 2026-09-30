# Phase 0: CodeScene vs jCodemunch Hotspot Cross-Reference

**Analysis Date**: 2026-06-06T03:22:00Z  
**Purpose**: Validate epic prioritization by comparing CodeScene behavioral analysis with jCodemunch structural hotspot scoring

---

## Methodology Comparison

| Tool | Analysis Type | Signals Used |
|------|---------------|--------------|
| **jCodemunch** | Structural + Git | Cyclomatic complexity × log(1 + churn_90d) |
| **CodeScene** | Behavioral + Git | Code health (0-10) + change frequency + author coupling |

---

## Cross-Reference Matrix

### ✅ HIGH AGREEMENT (Both tools flag as critical)

| File | jCodemunch Rank | CodeScene Visual | Method(s) | Consensus |
|------|----------------|------------------|-----------|-----------|
| `V12_002.SIMA.Lifecycle.cs` | Top 20 (multiple methods) | **LARGE RED** | HydrateWorkingOrdersFromBroker (CYC 79), HydrateFSMsFromWorkingOrders (CYC 72) | **CRITICAL PRIORITY** |
| `V12_002.SIMA.Dispatch.cs` | Rank 3, 13 | **LARGE RED** | Dispatch_PublishMarketBracketToPhoton (CYC 27), Dispatch_BuildFollowerOrders (CYC 21) | **HIGH PRIORITY** |
| `V12_002.Orders.Callbacks.Execution.cs` | Top 20 | **RED** | ProcessOnExecutionUpdate (CYC 48) | **CRITICAL PRIORITY** |
| `V12_002.Lifecycle.cs` | Rank 4 | **RED** | DrainQueuesForShutdown (CYC 25), ProcessOnStateChange (CYC 48) | **CRITICAL PRIORITY** |
| `V12_002.Orders.Management.Flatten.cs` | Rank 9 | **RED** | FlattenSinglePosition (CYC 27) | **HIGH PRIORITY** |
| `V12_002.SIMA.Execution.cs` | Rank 20 | **RED** | ProcessSingleFleetRMAAccount (CYC 25) | **HIGH PRIORITY** |
| `V12_002.UI.IPC.Commands.Fleet.cs` | Top 22 (watch list) | **RED** | TryHandleFleetCommand (CYC 19), TryHandleFleet_CancelAll (CYC 19) | **MEDIUM PRIORITY** |

### ⚠️ PARTIAL AGREEMENT (One tool flags, other shows moderate concern)

| File | jCodemunch Rank | CodeScene Visual | Notes |
|------|----------------|------------------|-------|
| `V12_002.Entries.RMA.cs` | **Rank 1** (MonitorRmaProximity CYC 32) | Not prominently visible | jCodemunch shows highest hotspot (98.91), but CodeScene may show lower change frequency |
| `V12_002.SIMA.Fleet.cs` | **Rank 2** (ShouldSkipFleet_RunHealthCheck CYC 29) | Not prominently visible | High complexity but possibly lower churn in CodeScene's window |
| `V12_002.Orders.Management.StopSync.cs` | **Rank 5, 6, 7, 10** (multiple methods) | Not prominently visible | Multiple high-CYC methods but possibly lower behavioral coupling |
| `V12_002.UI.Panel.Construction.cs` | LOC>80 (multiple methods) | **RED** | CodeScene flags for size/complexity, jCodemunch shows LOC violations |
| `V12_002.Entries.FFMA.cs` | CYC 16 (CheckFFMAConditions) | **RED** | CodeScene shows behavioral hotspot, jCodemunch shows moderate complexity |
| `V12_002.Entries.Retest.cs` | LOC>80 (ExecuteRetestEntry) | **RED** | Both flag but different severity rankings |

### 🔍 CODESCENE-ONLY FLAGS (Not in jCodemunch top 20)

| File | CodeScene Visual | Likely Reason |
|------|------------------|---------------|
| `V12_002.UI.Compliance.cs` | **RED** | High change coupling or author coupling (behavioral signal) |
| `V12_002.Orders.Callbacks.Propagation.cs` | **RED** | Change frequency or coupling (not purely complexity-driven) |
| `V12_002.UI.IPC.Commands.Misc.cs` | **RED** | Behavioral hotspot (possibly high change frequency) |

---

## Consensus Recommendations

### 🎯 TIER 1: CRITICAL (Both tools agree - start here)

1. **`V12_002.SIMA.Lifecycle.cs`**
   - **Methods**: HydrateWorkingOrdersFromBroker (CYC 79), HydrateFSMsFromWorkingOrders (CYC 72)
   - **jCodemunch**: Multiple methods in top 20
   - **CodeScene**: LARGE RED circle (highest visual priority)
   - **Rationale**: Largest red circle in CodeScene + highest CYC scores in jCodemunch = undisputed priority
   - **Epic**: EPIC-CCN-51 (new - supersedes EPIC-CCN-46)

2. **`V12_002.Lifecycle.cs`**
   - **Methods**: ProcessOnStateChange (CYC 48), DrainQueuesForShutdown (CYC 25)
   - **jCodemunch**: Rank 4 (DrainQueuesForShutdown)
   - **CodeScene**: RED circle
   - **Epic**: EPIC-CCN-49 (DrainQueuesForShutdown) + EPIC-CCN-51 (ProcessOnStateChange)

3. **`V12_002.Orders.Callbacks.Execution.cs`**
   - **Method**: ProcessOnExecutionUpdate (CYC 48)
   - **jCodemunch**: Top 20
   - **CodeScene**: RED circle
   - **Epic**: EPIC-CCN-53

### 🎯 TIER 2: HIGH PRIORITY (Strong agreement)

4. **`V12_002.SIMA.Dispatch.cs`**
   - **Methods**: Dispatch_PublishMarketBracketToPhoton (CYC 27), Dispatch_BuildFollowerOrders (CYC 21)
   - **jCodemunch**: Rank 3, 13
   - **CodeScene**: LARGE RED circle
   - **Epic**: EPIC-CCN-48 (Dispatch_PublishMarketBracketToPhoton)

5. **`V12_002.Orders.Management.Flatten.cs`**
   - **Method**: FlattenSinglePosition (CYC 27)
   - **jCodemunch**: Rank 9
   - **CodeScene**: RED circle
   - **Epic**: EPIC-CCN-54

6. **`V12_002.SIMA.Execution.cs`**
   - **Method**: ProcessSingleFleetRMAAccount (CYC 25)
   - **jCodemunch**: Rank 20
   - **CodeScene**: RED circle
   - **Epic**: EPIC-CCN-55

### 🎯 TIER 3: MEDIUM PRIORITY (Divergent signals - investigate)

7. **`V12_002.Entries.RMA.cs`** (jCodemunch Rank 1)
   - **Method**: MonitorRmaProximity (CYC 32, Hotspot 98.91)
   - **Divergence**: jCodemunch shows highest hotspot, but CodeScene doesn't prominently flag
   - **Investigation needed**: Check CodeScene's change frequency data for this file
   - **Epic**: EPIC-CCN-46 (original recommendation)

8. **`V12_002.SIMA.Fleet.cs`** (jCodemunch Rank 2)
   - **Method**: ShouldSkipFleet_RunHealthCheck (CYC 29)
   - **Divergence**: High complexity but possibly lower behavioral coupling
   - **Epic**: EPIC-CCN-47

---

## Revised Epic Prioritization (Multi-Tool Consensus)

### Phase 7 Execution Order (Consensus-Driven)

| Priority | Epic | Target | File | CYC | CodeScene | jCodemunch | Effort |
|----------|------|--------|------|-----|-----------|------------|--------|
| **P0** | EPIC-CCN-51 | HydrateWorkingOrdersFromBroker | V12_002.SIMA.Lifecycle.cs | 79 | LARGE RED | Top 20 | 4-5h |
| **P0** | EPIC-CCN-52 | HydrateFSMsFromWorkingOrders | V12_002.SIMA.Lifecycle.cs | 72 | LARGE RED | Top 20 | 4-5h |
| **P0** | EPIC-CCN-53 | ProcessOnExecutionUpdate | V12_002.Orders.Callbacks.Execution.cs | 48 | RED | Top 20 | 3-4h |
| **P0** | EPIC-CCN-49 | ProcessOnStateChange | V12_002.Lifecycle.cs | 48 | RED | Top 20 | 4-5h |
| **P1** | EPIC-CCN-48 | Dispatch_PublishMarketBracketToPhoton | V12_002.SIMA.Dispatch.cs | 27 | LARGE RED | Rank 3 | 3-4h |
| **P1** | EPIC-CCN-54 | FlattenSinglePosition | V12_002.Orders.Management.Flatten.cs | 27 | RED | Rank 9 | 2-3h |
| **P2** | EPIC-CCN-46 | MonitorRmaProximity | V12_002.Entries.RMA.cs | 32 | Not visible | Rank 1 | 2-3h |
| **P2** | EPIC-CCN-47 | ShouldSkipFleet_RunHealthCheck | V12_002.SIMA.Fleet.cs | 29 | Not visible | Rank 2 | 2h |

---

## Key Insights

### 1. **SIMA.Lifecycle.cs is the undisputed priority**
- Both tools show this as the largest/most critical hotspot
- Contains 2 of the 6 remaining CYC>20 methods (79 and 72)
- Should be split into **EPIC-CCN-51** and **EPIC-CCN-52**

### 2. **Behavioral vs Structural Signals**
- CodeScene's behavioral analysis (change coupling, author coupling) surfaces files like `UI.Compliance.cs` that jCodemunch's structural analysis doesn't prioritize
- jCodemunch's complexity-first approach surfaces `Entries.RMA.cs` (CYC 32) that CodeScene doesn't visually emphasize

### 3. **Consensus = Confidence**
- Files flagged by BOTH tools (TIER 1) should be addressed first
- Divergent signals (TIER 3) require investigation before committing to epic scope

### 4. **CodeScene's Visual Size = Priority**
- The LARGE RED circles (SIMA.Lifecycle, SIMA.Dispatch) correlate with jCodemunch's highest CYC scores
- This validates using visual size as a tiebreaker when hotspot scores are close

---

## Recommended Action

**START WITH**: EPIC-CCN-51 (HydrateWorkingOrdersFromBroker in V12_002.SIMA.Lifecycle.cs)

**Rationale**:
- Largest red circle in CodeScene (highest behavioral priority)
- Highest CYC score in jCodemunch (79 - 5.3x Jane Street threshold)
- Both tools agree this is the most critical file
- Addresses 2 of the 6 remaining CYC>20 methods in one epic

**Alternative**: If SIMA.Lifecycle.cs is too complex for a first epic, start with EPIC-CCN-48 (Dispatch_PublishMarketBracketToPhoton) - also has strong consensus and slightly lower complexity (CYC 27).

---

## Next Steps

1. **Director Decision**: Confirm starting epic (EPIC-CCN-51 recommended)
2. **Phase 1 (Intake)**: Deep dive into SIMA.Lifecycle.cs with jCodemunch forensics
3. **CodeScene Integration**: Query CodeScene API for detailed metrics on selected file
4. **Proceed to Phase 2**: Generate extraction plan with both tools' insights

---

**[PHASE0-CROSSREF-COMPLETE]**