---
# TICKET REAPER-EXPANSION-03: Entries Safety Module
# Epic: REAPER-EXPANSION
# Sequence: 3 of 3
# Depends on: NONE (independent of Tickets 01-02)
---

## Objective
Implement entry signal validation and duplicate suppression layer with mode validation, duplicate signal detection, staleness checking, and quantity clamping.

## Scope
IN scope:
- Create new file: `src/V12_002.REAPER.Entries.cs`
- Modify: `src/V12_002.Entries.OR.cs` (ExecuteLong, ExecuteShort)
- Modify: `src/V12_002.Entries.RMA.cs` (ExecuteTrendSplitEntry)
- Modify: `src/V12_002.Entries.MOMO.cs` (ExecuteMOMOEntry)
- Modify: `src/V12_002.Entries.FFMA.cs` (ExecuteFFMAEntry)
- Modify: `src/V12_002.Entries.Trend.cs` (ExecuteTRENDEntry)
- Modify: `src/V12_002.Entries.Retest.cs` (ExecuteRetestEntry)
- Add state: `EntryMode` enum, `_currentEntryMode`, `_lastEntrySignalTime`, `_maxEntryQuantity`

OUT of scope:
- SIMA safety module (Ticket 01)
- IPC safety module (Ticket 02)
- Automated tests (deferred to Phase 4)

## Context References
- Analysis: docs/brain/REAPER-EXPANSION/01-analysis.md -- Section "Entry Safety Gaps"
- Approach: docs/brain/REAPER-EXPANSION/02-approach-entries.md -- Complete module specification

## Implementation Instructions

### Step 1: Create V12_002.REAPER.Entries.cs

Extract 5 methods from the approach blueprint:

| New Method | Responsibility | Min LOC | CYC Target |
|------------|---------------|---------|------------|
| ValidateEntryPreconditions | Primary entry point - orchestrates 4 checks | 25 | ≤ 5 |
| ValidateEntryMode | Check CurrentEntryMode vs calling method | 15 | ≤ 2 |
| CheckDuplicateSignal | 500ms grace period per mode (atomic CAS) | 25 | ≤ 3 |
| CheckSignalStaleness | 3-bar max lookback | 20 | ≤ 3 |
| ValidateEntryQuantity | Clamp to MaxEntryQuantity | 15 | ≤ 2 |

**State Fields** (add to V12_002.REAPER.Entries.cs):
```csharp
public enum EntryMode
{
    OR,
    RMA,
    TREND,
    MOMO,
    FFMA,
    RETEST
}

private volatile EntryMode _currentEntryMode = EntryMode.OR;
private readonly ConcurrentDictionary<EntryMode, long> _lastEntrySignalTime
    = new ConcurrentDictionary<EntryMode, long>();
private int _maxEntryQuantity = 1;  // Default = PositionSize
```

**Accessor Methods** (add to V12_002.REAPER.cs):
```csharp
internal void SetCurrentEntryMode(EntryMode mode)
{
    _currentEntryMode = mode;
    Print(string.Format("[REAPER][ENTRIES] Mode switched to: {0}", mode));
}
```

### Step 2: Integration - All Entry Methods (6 files)

Apply the same pattern to ALL entry methods across 6 files:

**Files to Modify**:
1. `src/V12_002.Entries.OR.cs` - ExecuteLong, ExecuteShort
2. `src/V12_002.Entries.RMA.cs` - ExecuteTrendSplitEntry
3. `src/V12_002.Entries.MOMO.cs` - ExecuteMOMOEntry
4. `src/V12_002.Entries.FFMA.cs` - ExecuteFFMAEntry
5. `src/V12_002.Entries.Trend.cs` - ExecuteTRENDEntry
6. `src/V12_002.Entries.Retest.cs` - ExecuteRetestEntry

**Pattern** (example from Entries.OR.cs:37):

**BEFORE**:
```csharp
private void ExecuteLong(int contracts)
{
    if (!IsOrderAllowed()) return;
    if (contracts <= 0)
    {
        Print(string.Format("[OR] ExecuteLong received invalid contracts={0}. Aborting entry.", contracts));
        return;
    }
    // ... submit bracket ...
}
```

**AFTER**:
```csharp
private void ExecuteLong(int contracts)
{
    if (!IsOrderAllowed()) return;
    
    // NEW: Entry preconditions validation
    if (!ValidateEntryPreconditions(EntryMode.OR, contracts, CurrentBar))
    {
        Print("[OR] ExecuteLong failed preconditions check. Aborting entry.");
        return;
    }
    
    // NEW: Quantity clamping
    contracts = ValidateEntryQuantity(contracts);
    
    // ... submit bracket ...
}
```

**EntryMode Mapping**:
- OR.cs → `EntryMode.OR`
- RMA.cs → `EntryMode.RMA`
- MOMO.cs → `EntryMode.MOMO`
- FFMA.cs → `EntryMode.FFMA`
- Trend.cs → `EntryMode.TREND`
- Retest.cs → `EntryMode.RETEST`

### Step 3: Duplicate Signal Logic

The `CheckDuplicateSignal` method uses `ConcurrentDictionary.AddOrUpdate` for atomic timestamp updates:
- First call for a mode: stores timestamp, returns true (unique)
- Subsequent calls: compares delta against 500ms threshold
- Delta < 500ms: returns false (duplicate, suppressed)
- Delta >= 500ms: updates timestamp, returns true (unique)

This prevents signal flooding while allowing legitimate rapid mode switches.

### Step 4: Signal Staleness Logic

The `CheckSignalStaleness` method compares bar numbers:
- `delta = CurrentBar - signalBarNumber`
- If delta > 3: signal is stale, reject
- Otherwise: signal is fresh, proceed

This prevents execution of signals generated >3 bars ago.

## V12 DNA Guardrails
- [ ] Zero new lock() statements
- [ ] Zero non-ASCII characters in string literals
- [ ] All sub-methods >= 15 LOC (extraction floor)
- [ ] ValidateEntryPreconditions CYC ≤ 5
- [ ] All helpers CYC ≤ 3
- [ ] No logic drift -- pure structural movement only
- [ ] Jane Street Compliance: 100% (Atomic, Wait-Free, Bounded, Deterministic)

## Post-Edit Verification (Mandatory)
```powershell
# 1. Re-establish hard links (MANDATORY after every src/ edit)
powershell -File .\deploy-sync.ps1

# 2. Complexity verification
python scripts/complexity_audit.py

# 3. Lock regression (must return ZERO)
grep -r "lock(" src/

# 4. ASCII gate (must return ZERO)
grep -Prn "[^\x00-\x7F]" src/
```

## Acceptance Criteria
- [ ] V12_002.REAPER.Entries.cs created with 5 methods (~120 LOC)
- [ ] Entry mode validation (reject mismatched modes)
- [ ] Duplicate signal suppression (500ms grace period, configurable)
- [ ] Signal staleness detection (3-bar threshold, configurable)
- [ ] Entry quantity validation (clamp to MaxEntryQuantity)
- [ ] All 6 entry files modified with validation integration
- [ ] EntryMode enum defined with 6 values
- [ ] deploy-sync.ps1 ASCII gate: PASS
- [ ] complexity_audit.py shows all methods ≤ target CYC
- [ ] lock() audit: ZERO matches
- [ ] Director presses F5 in NinjaTrader -- BUILD_TAG banner visible
- [ ] BUILD_TAG: `1111.008-reaper-expansion-t3`