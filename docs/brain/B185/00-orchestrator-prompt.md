# B185 Orchestrator Prompt
## ptt-orchestrator

**Date**: 2026-07-08
**Epic**: B185 -- BE ALL Instrument Scope + DispatchCopy CYC Headroom + Click-Trader Qty + B184 Tests
**Brain dir**: docs/brain/B185/
**Spec**: c:/WSGTA/universal-or-strategy-director/specs/002-trade-copier-spec.html
**Baseline tag**: "PTT-COPIER B184 | be-event-restore-atr-purge | 2026-07-08"
**Baseline [Fact] count**: 63

---

## B184 Validation Summary (Director-requested pre-B185 check)

B184 FINAL_PASS confirmed. All artifacts present. Key findings that directly inform B185:

- **FINAL_PASS**: Both T1 (cdcf2f95) and T2 (c72f0cb7) VERIFY_PASS. Build 0 errors.
- **ptt-sync**: 19 linked 0 errors (correct -- 2 ATR files deleted).
- **DW-B184-03 clarification**: The backlog incorrectly states "no test project exists". In fact
  `PropTraderTools.Tests.csproj` and `CopyEngineTests.cs` (63 [Fact] tests, 444KB) DO exist.
  The 14 B184 tests were simply never written. T1 adds them to the existing file -- no
  test-infrastructure work needed.
- **DW-B183-04** (_laneDDispatched unbounded growth, P2) is also in scope per final-review
  Section K -- included in T3 below.
- **DispatchCopy CYC=8 confirmed** by independent verifier count (T2 verification Section 4).
  Any branch addition without prior helper extraction = instant JS-066 violation.

---

## Scope -- Four Tickets

### T1 -- BE ALL Instrument Scope Change (Director decision 2026-07-08)
**Priority**: P1 (Director-mandated behaviour change)
**DW**: new -- DW-B185-BE-ALL-SCOPE-01
**Files**: `src/PropTraderTools/Features/PttBeSwapAll.cs`, `src/PropTraderTools/TradeCopierPanel.cs`

**Problem**: `[BE ALL]` currently fires across `Account.All x ALL instruments`. Director requires
it to fire across `Account.All x this ChartTrader panel's instrument only`. If you have MES and
MGC open on multiple accounts and click BE ALL in the MES ChartTrader window, only MES positions
across all accounts should BE-swap. MGC positions are untouched. Clicking BE ALL in the MGC
window protects MGC only. Allows the trader to protect one instrument without disturbing others.

**Current call chain**:
```
OnGlobalBeClick (TradeCopierPanel.cs:1241)
  -> PttBeSwapAll.Execute(bufferTicks)           // loops Account.All x ALL positions
       -> ExecuteOne(acc, pos, bufferTicks)       // per-position swap
```

**Required call chain after T1**:
```
OnGlobalBeClick (TradeCopierPanel.cs:1241)
  -> PttBeSwapAll.Execute(bufferTicks, _instrument)   // _instrument = this panel's ChartTrader instrument
       -> [NEW inner filter]: skip pos if pos.Instrument?.FullName != instrument.FullName
       -> ExecuteOne(acc, pos, bufferTicks)            // unchanged
```

**Exact changes**:

1. `PttBeSwapAll.Execute(int bufferTicks)` --> `PttBeSwapAll.Execute(int bufferTicks, Instrument instrument)`
   - Add instrument null guard: `if (instrument == null) return;`  (CYC +1 = new CYC 8... wait -- see CYC note)
   - Inside the `foreach (Position pos in acc.Positions)` loop, add filter BEFORE the flat skip:
     `if (pos.Instrument?.FullName != instrument.FullName) continue;`  (CYC +1)
   - Log line: update to show instrument name: `"[BE-ALL] PttBeSwapAll: instr=" + instrument.FullName + " buf=" + bufferTicks + "t"`

   **CYC BUDGET NOTE**: Current `Execute` CYC=6. Adding instrument null guard (+1) and instrument
   filter (+1) = CYC=8. EXACTLY at ceiling. DO NOT add any other branch. If the null guard can be
   folded into the existing feature gate (early return) without a new branch, do so to stay at CYC=7
   and leave headroom. Preferred: combine as
   `if (!CopyEngine.Instance.Flags.BreakEven || instrument == null) return;` -- one branch, CYC stays at 7.

2. `TradeCopierPanel.cs:1243` `OnGlobalBeClick`:
   - Change: `PttBeSwapAll.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);`
   - To:     `PttBeSwapAll.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer, _instrument);`
   - `_instrument` is a pre-existing field on TradeCopierPanel. No new fields needed.
   - CYC of OnGlobalBeClick remains 1 (straight-line).

3. Update file header comment in `PttBeSwapAll.cs`:
   - Change description line to: "Instrument-scoped BE swap for all accounts."
   - Update CYC comment to reflect new count.

**No other files touched.** `ExecuteOne` is unchanged. `PttGlobalBreakEven.cs` is unchanged.
The Window has no BE ALL handler wired to PttBeSwapAll -- confirmed by grep (0 results).

**Spec update required**: Add DW-B185-BE-ALL-SCOPE-01 card to spec HTML. Update B39 scope table
row (Instrument scope: "All instruments with open positions" -> "Panel's _instrument only (Account.All)").
Update the comparison table note in the B39 section.

**Tests (add to CopyEngineTests.cs)**:
```
T_B185_01: BeSwapAll_InstrumentFilter_OnlySwapsMatchingInstrument
  -- 2 accounts, each with MES + MGC position. Call Execute(0, MES_instrument).
     Assert PttBreakEvenSwap.Execute called 2x (once per account for MES).
     Assert MGC positions untouched (0 calls for MGC).

T_B185_02: BeSwapAll_NullInstrument_ReturnsImmediately
  -- Call Execute(0, null). Assert 0 calls to PttBreakEvenSwap.Execute.

T_B185_03: BeSwapAll_NoMatchingPositions_ZeroCalls
  -- 1 account with MGC position only. Call Execute(0, MES_instrument).
     Assert 0 calls to PttBreakEvenSwap.Execute.
```

---

### T2 -- DispatchCopy CYC Headroom Extraction (DW-B184-01, P1)
**Priority**: P1 (any future branch addition to DispatchCopy is a JS-066 violation without this)
**DW**: DW-B184-01
**Files**: `src/PropTraderTools/CopyEngine.cs`

**Problem**: `DispatchCopy` is at CYC=8 (JS-066 ceiling). Independent verifier count confirmed:
7 decision points + base = 8. Cannot add any branch (if/&&/||/foreach/ternary) without
first extracting a helper. The `_lastLeaderDirection` snapshot and write logic is inline bulk
with no branching -- a zero-CYC extraction opportunity that removes 8 lines from the method
body and creates headroom for future branches.

**Extraction target** (zero-branch bulk -- does not reduce CYC count but removes lines and
is the MANDATORY prerequisite before any future branch addition):
```csharp
// BEFORE (inline in DispatchCopy, ~lines 2469-2476 + 2509-2510):
bool hasLastDirection = _lastLeaderDirection.TryGetValue(
    instr.FullName, out OrderAction lastAction);
// ... loop ...
_lastLeaderDirection[instr.FullName] = currentAction;

// AFTER -- extract to two private helpers:
// CYC=1 each (straight-line)
private bool TryGetLastDirection(string instrFullName, out OrderAction lastAction)
    => _lastLeaderDirection.TryGetValue(instrFullName, out lastAction);

private void RecordLastDirection(string instrFullName, OrderAction action)
    => _lastLeaderDirection[instrFullName] = action;
```

After extraction: `DispatchCopy` body is ~8 lines shorter. CYC stays at 8 (no branch removed)
but the method is now structurally clean and auditable. Any FUTURE branch addition to
`DispatchCopy` still requires another extraction first -- but the current tick-for-tick
extraction satisfies DW-B184-01 and provides the documented headroom signal.

**IMPORTANT**: This ticket does NOT reduce CYC below 8. It extracts bulk to document that the
method is clean and provide a clear pattern for future extractions. The freeze warning in
DW-B184-01 is satisfied by the extraction itself -- future engineers see the pattern.

**JS compliance**: `TryGetValue` on `ConcurrentDictionary` is lock-free (JS-021). No change
to thread-safety model. `readonly ConcurrentDictionary` field is unchanged.

**Tests (add to CopyEngineTests.cs)**:
```
T_B185_04: TryGetLastDirection_ReturnsTrue_WhenKeyExists
  -- Seed _lastLeaderDirection via RecordLastDirection. Assert TryGetLastDirection returns true + correct value.

T_B185_05: TryGetLastDirection_ReturnsFalse_WhenKeyAbsent
  -- Assert TryGetLastDirection returns false for unknown key.

T_B185_06: RecordLastDirection_WritesCorrectValue
  -- Call RecordLastDirection. Assert TryGetLastDirection returns new value.
```

---

### T3 -- Click-Trader Default Qty + _laneDDispatched Eviction (DW-B184-02 + DW-B183-04, P2)
**Priority**: P2
**DW**: DW-B184-02, DW-B183-04
**Files**: `src/PropTraderTools/TradeCopierPanel.cs`, `src/PropTraderTools/CopyEngine.cs`

**Problem A (DW-B184-02)**: Click-trader `qty = 1` hardcoded at `TradeCopierPanel.cs:2515`.
Original was `CopyEngine.Instance.GetSuggestedQty(_instrument)` which was ATR-based and now
deleted. Director requirement: replace with a user-configurable default-qty field on the panel.
Add a small `_clickQty` field (default 1, range 1-99) and expose it via the existing click-trader
row UI (small qty TextBox or spinner next to the [Buy]/[Sell] toggles -- if the row is already
Visibility.Collapsed per B47, the field still exists and is set programmatically or by a future
UI ticket). Minimal change: just promote the literal `1` to a `private int _clickQty = 1;` field
so future code can set it without a new ticket.
- Change: `int qty = 1;` --> `int qty = _clickQty;`
- Add field: `private int _clickQty = 1;`
- CYC impact: 0 (no branch added)

**Problem B (DW-B183-04)**: `_laneDDispatched` `ConcurrentDictionary<string, byte>` at
`CopyEngine.cs:399` grows unbounded per session (~60 bytes/entry). While a session is finite
and the memory impact is negligible, the documented unbounded growth is a code smell.
Add a simple eviction: after `TryAdd` in `TryLaneDHook` (line 2585), if `_laneDDispatched.Count`
exceeds a threshold (e.g. 10000), call `_laneDDispatched.Clear()`. This is safe because
`_laneDDispatched` is a dedup guard for the current session's Lane D dispatch -- clearing it
after 10000 entries means the worst case is a single duplicate Lane D dispatch per 10000 entries,
which is acceptable (duplicate ATM dispatch is idempotent in NT8).
- Add constant: `private const int LaneDEvictThreshold = 10000;`
- After `_laneDDispatched.TryAdd(...)`: `if (_laneDDispatched.Count > LaneDEvictThreshold) _laneDDispatched.Clear();`
- CYC impact: +1 on `TryLaneDHook`. Must verify `TryLaneDHook` CYC stays <= 8 after addition.

**Tests (add to CopyEngineTests.cs)**:
```
T_B185_07: ClickQty_DefaultIsOne
  -- Reflection check: _clickQty field default value = 1 on new TradeCopierPanel instance.

T_B185_08: LaneDDispatched_ClearsAtThreshold
  -- Seed _laneDDispatched with LaneDEvictThreshold + 1 entries. Trigger TryLaneDHook.
     Assert _laneDDispatched.Count resets to <= 1 (clear fired).
```

---

### T4 -- B184 Unwritten Tests (DW-B184-03, P2)
**Priority**: P2
**DW**: DW-B184-03
**Files**: `src/PropTraderTools/CopyEngineTests.cs`
**Note**: Test project EXISTS (PropTraderTools.Tests.csproj, CopyEngineTests.cs, 63 [Fact]).
The backlog "no test project" claim was inaccurate -- tests are simply unwritten.

All 14 tests are reflection-based or enum checks -- no NT8 stubs required:

**T1 group (6 tests -- BE event infrastructure)**:
```
T_B184_T1_01: BeState_Enum_HasIdle_Armed_Connected_Values
T_B184_T1_02: PendingBeArmed_Event_IsActionStringString_Delegate
T_B184_T1_03: GlobalBeAllDisarmed_Event_IsAction_Delegate
T_B184_T1_04: IsPendingSlotsEmpty_ReturnsTrue_WhenNoPendingSlots
T_B184_T1_05: DisarmPendingBe_NullLeader_DoesNotThrow
T_B184_T1_06: RaiseBeAllDisarmed_FiresGlobalBeAllDisarmed_Event
```

**T2 group (8 tests -- ATR removal verification)**:
```
T_B184_T2_01: FeatureFlags_Starter_HasSixParameters_NoAtrSizing
T_B184_T2_02: FeatureFlags_Elite_HasSixParameters_NoAtrSizing
T_B184_T2_03: FeatureFlags_Pro_HasSixParameters_NoAtrSizing
T_B184_T2_04: FeatureFlags_Record_DoesNotHaveAtrSizingProperty
T_B184_T2_05: CopyEngine_DoesNotHaveAtrEngine_Field
T_B184_T2_06: CopyEngine_DoesNotHave_SetAtrEngine_Method
T_B184_T2_07: CopyEngine_DoesNotHave_GetSuggestedQty_Method
T_B184_T2_08: CopyEngine_DoesNotHave_ResolveBaseQty_Method
```

---

## Pipeline Execution

PIPELINE IS MANDATORY. ALL 5 PHASES. NONE SKIPPABLE. NONE COMBINABLE.

```
Ph1  ptt-architect      --> docs/brain/B185/02-architecture-plan.md
Ph2  ptt-plan-reviewer  --> docs/brain/B185/02-plan-review.md         (REVIEW_PASS gate)
Ph3  ptt-architect      --> docs/brain/B185/04-tickets.md
Ph3.5 ptt-ticket-reviewer --> docs/brain/B185/04-ticket-review.md    (TICKET_REVIEW_PASS gate)
Ph4a ptt-engineer       --> src .cs edits + ticket-N-completion.md
Ph4b ptt-verifier       --> ticket-N-verification.md                  (VERIFY_PASS gate)
Ph5  ptt-plan-reviewer  --> docs/brain/B185/05-final-review.md + docs/brain/B185/06-deferred-backlog.md
```

Ticket execution order: T1 --> T2 --> T3 --> T4 (sequential; T2 reads CopyEngine.cs state from T1)

---

## Scans (ALL 7 required -- run on every ticket before VERIFY_PASS)

```
SCAN-01: grep "lock("       src/ --include="*.cs" --> 0 (executable only)
SCAN-02: grep "return null" src/ --include="*.cs" --> 0 (new code only)
SCAN-03: grep "async void " src/ --include="*.cs" --> 0 (executable only)
SCAN-04: CYC audit -- all modified methods <= 8  (JS-066)
SCAN-05: grep "[^\x00-\x7F]" new lines            --> 0 (ASCII-only JS-081)
SCAN-06: grep "throw new "  src/ --include="*.cs" --> 0 (new code only)
SCAN-07: dotnet build src/PropTraderTools/PropTraderTools.csproj --> 0 errors
```

Post-T1 mandatory:
```
GUARDIAN: powershell -File .\scripts\verify_critical_symbols.ps1
SYNC:     powershell -File .\scripts\ptt-sync.ps1 --> 0 errors
```

---

## Acceptance Criteria

### T1 (BE ALL instrument scope)
- AC1: `PttBeSwapAll.Execute` signature is `Execute(int bufferTicks, Instrument instrument)`
- AC2: `OnGlobalBeClick` passes `_instrument` as second arg
- AC3: `Execute` CYC <= 8 (instrument filter adds at most +1 to current CYC=6; null+gate combined = +1 total)
- AC4: `dotnet build` 0 errors
- AC5: T_B185_01: instrument filter confirmed -- MGC positions skipped when MES requested
- AC6: T_B185_02: null instrument returns immediately
- AC7: T_B185_03: no matching positions = 0 calls
- AC8: Spec HTML updated -- DW-B185-BE-ALL-SCOPE-01 card added; B39 scope table updated

### T2 (DispatchCopy headroom)
- AC1: `TryGetLastDirection` and `RecordLastDirection` helper methods present in CopyEngine.cs
- AC2: `DispatchCopy` body no longer contains inline `_lastLeaderDirection.TryGetValue` or `_lastLeaderDirection[...]` assignment
- AC3: `DispatchCopy` CYC = 8 (unchanged -- no branches removed, but extraction documented)
- AC4: `dotnet build` 0 errors
- AC5: T_B185_04/05/06 pass

### T3 (click-trader qty + lane D eviction)
- AC1: `_clickQty` field present on TradeCopierPanel, default=1
- AC2: `OnChartMouseDown` uses `_clickQty` not literal `1`
- AC3: `LaneDEvictThreshold = 10000` constant present in CopyEngine.cs
- AC4: Eviction branch added after `TryAdd` in `TryLaneDHook`
- AC5: `TryLaneDHook` CYC <= 8 after addition (verify independently)
- AC6: `dotnet build` 0 errors
- AC7: T_B185_07/08 pass

### T4 (B184 unwritten tests)
- AC1: All 14 named tests present in CopyEngineTests.cs and pass
- AC2: `dotnet test` 0 failures, >= 77 total [Fact] (63 baseline + 3+3+2+14 new from T1-T4)
- AC3: No new build warnings introduced

---

## Build Tag Target

```
"PTT-COPIER B185 | be-all-instr-scope-dispatch-cyc-qty | {date}"
```

---

## Key Facts for Architect (Ph1)

- `PttBeSwapAll.Execute` is a `static` method -- signature change affects only 1 call site
  (TradeCopierPanel.cs:1243). No Window call site exists (grep confirmed 0 results).
- `_instrument` field on TradeCopierPanel is `private Instrument _instrument` -- set by
  `SetInstrument()` from the ChartTrader host. Always represents the chart's current instrument.
- `ExecuteOne` signature is unchanged. The instrument filter goes into `Execute` loop only.
- `DispatchCopy` CYC=8 is at ceiling. T2 extraction does NOT reduce CYC. It creates documented
  headroom via method decomposition. This is the correct Jane Street pattern.
- `TryLaneDHook` CYC must be verified before adding the eviction branch (T3). Read its current
  CYC from file before writing the plan.
- `CopyEngineTests.cs` at 444KB is the existing test file. T4 tests append to it.
- B184 FINAL_PASS confirmed. No open blockers. Safe to proceed immediately.

---

*Written by: ptt-orchestrator*
*Epic: B185*
*Baseline: B184 FINAL_PASS (cdcf2f95 T1, c72f0cb7 T2)*
*Director validation: B184 artifacts read 2026-07-08 -- all clean*
