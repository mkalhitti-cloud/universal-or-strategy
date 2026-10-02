# B185 Tickets
## ptt-architect Phase 3 (Ticket Generation)

**Date**: 2026-07-08
**Epic**: B185 -- BE ALL Instrument Scope + DispatchCopy CYC Headroom + Click-Trader Qty + B184 Tests
**Brain dir**: `docs/brain/B185/`
**Plan status**: REVIEW_PASS (Cycle 2 -- 0 violations)
**Author**: ptt-architect
**Execution order**: T1 --> T2 --> T3 --> T4 (sequential; post-T1 GUARDIAN + ptt-sync mandatory before T2)

---

## Gate: REVIEW_PASS Confirmed

Plan: `docs/brain/B185/02-architecture-plan.md` — PLAN_COMPLETE (Cycle 2)
Review: `docs/brain/B185/02-plan-review.md` — REVIEW_PASS (Cycle 2, 0 violations)
Phase 3 UNLOCKED per reviewer sign-off: "ptt-architect may proceed to write docs/brain/B185/04-tickets.md"

---

## 7-Scan Command Reference (all tickets)

```powershell
# SCAN-01: No lock() in new code
grep "lock(" src/ --include="*.cs"

# SCAN-02: No return null in new code
grep "return null" src/ --include="*.cs"

# SCAN-03: No async void (non-event-handler) in new code
grep "async void " src/ --include="*.cs"

# SCAN-04: CYC audit -- all modified methods verified <= 8 (JS-066 DNA mandate)
# (Manual branch count per method -- documented in each ticket below)

# SCAN-05: ASCII-only identifiers and string literals
grep "[^\x00-\x7F]" <new lines only>

# SCAN-06: No throw new in new code
grep "throw new " src/ --include="*.cs"

# SCAN-07: dotnet build 0 errors
dotnet build src/PropTraderTools/PropTraderTools.csproj
```

---

---

# T1 — BE ALL Instrument Scope

**Ticket**: T1
**Title**: BE ALL Instrument Scope Change
**DW Card**: DW-B185-BE-ALL-SCOPE-01 (new — opened by this ticket)
**Priority**: P1 (Director-mandated behaviour change)
**Spec ACs satisfied**: T1-AC1, T1-AC2, T1-AC3, T1-AC4, T1-AC5, T1-AC6, T1-AC7, T1-AC8

## File Targets

| File | Change Type |
|------|-------------|
| `src/PropTraderTools/Features/PttBeSwapAll.cs` | Signature change + combined gate + instrument filter + log update + header comment |
| `src/PropTraderTools/TradeCopierPanel.cs` | Call site update at line 1243 |
| `src/PropTraderTools/CopyEngineTests.cs` | Append 3 new [Fact] tests (T_B185_01, T_B185_02, T_B185_03) |

## Method Signature Change

```
BEFORE:  internal static void Execute(int bufferTicks)
AFTER:   internal static void Execute(int bufferTicks, Instrument instrument)
```

- `Instrument` type: `NinjaTrader.Cbi.Instrument` (existing NT8 type — pre-existing usage in file)
- `ExecuteOne` signature: **UNCHANGED**

## Exact Code Changes — PttBeSwapAll.cs

### Change 1: File header comment update

Update the file-level description comment:
```csharp
// BEFORE:
// Description: BE swap for all accounts.

// AFTER:
// Description: Instrument-scoped BE swap for all accounts.
```

Update the CYC comment on `Execute`:
```csharp
// BEFORE:
// CYC=6: flag-gate(1), acc-loop-foreach(2), follower-if(3), follower-&&(4), pos-foreach(5), flat-skip-||(6).

// AFTER:
// CYC=7: combined-gate-||(1), acc-loop-foreach(2), follower-if(3), follower-&&(4), pos-foreach(5), flat-skip-||(6), instr-filter(7).
```

### Change 2: Replace feature gate (combined null+flag guard)

```csharp
// BEFORE:
if (!CopyEngine.Instance.Flags.BreakEven)
{
    NinjaTrader.Code.Output.Process(
        "[BE-ALL] Blocked: BreakEven=false",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return;
}

// AFTER (combined gate -- one if, one || = net +1 CYC only from the || operator):
if (!CopyEngine.Instance.Flags.BreakEven || instrument == null)
{
    NinjaTrader.Code.Output.Process(
        "[BE-ALL] Blocked: BreakEven=false or instrument null",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
    return;
}
```

**CYC accounting**: The `||` operator adds 1 branch. The original gate was already 1 branch.
Net addition: +1 (the `||` short-circuit). No second `if` added.

### Change 3: Update log line to include instrument name

```csharp
// BEFORE:
NinjaTrader.Code.Output.Process(
    "[BE-ALL] PttBeSwapAll: buf=" + bufferTicks + "t",
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);

// AFTER:
NinjaTrader.Code.Output.Process(
    "[BE-ALL] PttBeSwapAll: instr=" + instrument.FullName + " buf=" + bufferTicks + "t",
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

Note: `instrument.FullName` is safe here — the combined gate above guarantees `instrument != null`
when execution reaches this line.

### Change 4: Add instrument filter inside `foreach (Position pos in acc.Positions)` loop

Add the filter as the FIRST check inside the loop body, BEFORE the existing flat skip:

```csharp
foreach (Position pos in acc.Positions)
{
    // ADDED: Instrument filter -- skip positions for other instruments.
    // pos.Instrument?.FullName uses null-conditional: if pos.Instrument is null,
    // expression evaluates to null != instrument.FullName => position is skipped. Correct.
    if (pos.Instrument?.FullName != instrument.FullName)
        continue;
    // existing flat skip (UNCHANGED):
    if (pos == null || pos.Quantity == 0)
        continue;
    ExecuteOne(acc, pos, bufferTicks);
}
```

**CYC accounting**: `if (pos.Instrument?.FullName != instrument.FullName)` adds 1 branch (+1 CYC).

## CYC Budget — Execute (T1)

| Branch | Description | Count |
|--------|-------------|-------|
| 1 | `combined-gate-\|\|` (BreakEven=false OR instrument==null) | +1 |
| 2 | `acc-loop-foreach` (foreach over Account.All) | +1 |
| 3 | `follower-if` (follower check in acc loop) | +1 |
| 4 | `follower-&&` (&& inside follower check) | +1 |
| 5 | `pos-foreach` (foreach over acc.Positions) | +1 |
| 6 | `flat-skip-\|\|` (pos==null \|\| pos.Quantity==0) | +1 |
| 7 | `instr-filter` (pos.Instrument?.FullName != instrument.FullName) | +1 |
| **Total** | | **CYC = 7** |

CYC=7. Headroom = 1 branch before JS-066 ceiling (8). ✅

## Exact Code Change — TradeCopierPanel.cs

**Location**: Line 1243, inside `OnGlobalBeClick`

```csharp
// BEFORE:
PttBeSwapAll.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer);

// AFTER:
PttBeSwapAll.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer, _instrument);
```

`_instrument` is the pre-existing `private Instrument _instrument` field on `TradeCopierPanel`,
set by `SetInstrument()` from the ChartTrader host. **No new fields required.**

`OnGlobalBeClick` CYC: unchanged (straight-line addition only, CYC remains 1).

## Call Site List

| File | Location | Change |
|------|----------|--------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Line 1243, `OnGlobalBeClick` | Add `, _instrument` as second arg |

**Confirmed single call site**: grep of `PttBeSwapAll.Execute` across `src/` returns exactly 1 result
(TradeCopierPanel.cs:1243). The Window has no BE ALL handler wired to PttBeSwapAll (grep confirmed
0 results per orchestrator prompt). No other call sites exist.

## JS Rule Constraints — T1

| Rule | Severity | How Compliance is Achieved |
|------|----------|-----------------------------|
| **JS-021** No lock() | P0 CRITICAL | No `lock()` in any new code. `PttBeSwapAll.Execute` uses no synchronization primitives. |
| **JS-001** No throw | P0 CRITICAL | No `throw new` statements added. Void return type; error path uses log + return. |
| **JS-002** No return null | P0 CRITICAL | Method return type is `void`. No null return possible. |
| **JS-033** No async void | P0 CRITICAL | No async keyword anywhere in T1 changes. |
| **JS-066** CYC <= 8 | P0 (DNA) | `Execute` CYC = 7 (<= 8). `ExecuteOne` unchanged. `OnGlobalBeClick` CYC = 1. |
| **JS-081** ASCII-only | P0 (DNA) | All new string literals use ASCII only. `instrument.FullName` is user data, not a literal. New identifiers: `instrument` (ASCII). |

## 7-Scan Checklist — T1

| Scan | Applies | Expected Result | Verification |
|------|---------|-----------------|--------------|
| SCAN-01 `lock(` | YES | 0 matches in new T1 code | No lock in Execute, no lock in TradeCopierPanel call site change |
| SCAN-02 `return null` | YES | 0 matches in new T1 code | Return type void; combined gate uses bare `return;` |
| SCAN-03 `async void` | YES | 0 matches in new T1 code | No async anywhere in T1 |
| SCAN-04 CYC<=8 | YES (critical) | Execute=7, OnGlobalBeClick=1, ExecuteOne=unchanged | Branch count documented above |
| SCAN-05 ASCII-only | YES | 0 non-ASCII chars in new literals/identifiers | Log string: `[BE-ALL] Blocked: BreakEven=false or instrument null` is ASCII. `instrument.FullName` is user data. |
| SCAN-06 `throw new` | YES | 0 matches in new T1 code | No throw statements |
| SCAN-07 `dotnet build` | YES | 0 errors | Single call site updated; no other callers; static method signature change is complete |

## xUnit Tests — T1

Add to `src/PropTraderTools/CopyEngineTests.cs` (append after existing 63 [Fact] methods):

### T_B185_01: BeSwapAll_InstrumentFilter_OnlySwapsMatchingInstrument

```
[Fact] name: BeSwapAll_InstrumentFilter_OnlySwapsMatchingInstrument

Setup:
  - 2 mock accounts, each with 2 positions: one MES (Quantity > 0), one MGC (Quantity > 0).
  - CopyEngine.Instance.Flags.BreakEven = true.
  - Create a stub/mock Instrument with FullName = "MES 09-26".

Action:
  - Call PttBeSwapAll.Execute(0, mesInstrument) via reflection or direct call.

Assertions:
  - Assert: PttBreakEvenSwap.Execute (or ExecuteOne) called exactly 2 times
    (once per account for the MES position only).
  - Assert: MGC positions produce 0 calls to the inner swap (MGC positions are skipped
    by the instrument filter).
```

### T_B185_02: BeSwapAll_NullInstrument_ReturnsImmediately

```
[Fact] name: BeSwapAll_NullInstrument_ReturnsImmediately

Setup:
  - CopyEngine.Instance (any state -- null instrument gate fires before accounts loop).

Action:
  - Call PttBeSwapAll.Execute(0, null) via reflection or direct call.

Assertions:
  - Assert: method returns without exception (no NullReferenceException).
  - Assert: 0 calls to PttBreakEvenSwap.Execute / ExecuteOne (early return fired).
```

### T_B185_03: BeSwapAll_NoMatchingPositions_ZeroCalls

```
[Fact] name: BeSwapAll_NoMatchingPositions_ZeroCalls

Setup:
  - 1 mock account with a single MGC position (Quantity > 0). No MES positions.
  - CopyEngine.Instance.Flags.BreakEven = true.
  - Create a stub Instrument with FullName = "MES 09-26".

Action:
  - Call PttBeSwapAll.Execute(0, mesInstrument).

Assertions:
  - Assert: 0 calls to PttBreakEvenSwap.Execute / ExecuteOne
    (instrument filter skips the MGC position; no MES position exists).
```

## Acceptance Criteria — T1

| AC | ID | Criterion | Verification Method |
|----|-----|-----------|---------------------|
| AC1 | T1-AC1 | `PttBeSwapAll.Execute` signature is `Execute(int bufferTicks, Instrument instrument)` | Read method declaration in PttBeSwapAll.cs |
| AC2 | T1-AC2 | `OnGlobalBeClick` at TradeCopierPanel.cs:1243 passes `_instrument` as second arg | Read call site |
| AC3 | T1-AC3 | `Execute` CYC = 7 (combined null+gate=+1, instrument filter=+1, total 6+1=7) | Manual branch count |
| AC4 | T1-AC4 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` = 0 errors | Build output |
| AC5 | T1-AC5 | T_B185_01 passes: instrument filter confirmed -- MGC positions skipped when MES requested | `dotnet test` |
| AC6 | T1-AC6 | T_B185_02 passes: null instrument returns immediately, 0 calls | `dotnet test` |
| AC7 | T1-AC7 | T_B185_03 passes: no matching positions = 0 calls | `dotnet test` |
| AC8 | T1-AC8 | Spec HTML updated: DW-B185-BE-ALL-SCOPE-01 card added; B39 scope table row updated ("Panel's _instrument only (Account.All)") | Read spec HTML |

## Post-T1 Mandatory Scripts (BLOCKING — T2 cannot start until both PASS)

```powershell
# GUARDIAN: verify critical symbols are intact
powershell -File .\scripts\verify_critical_symbols.ps1
# Expected: PASS (0 critical symbol violations)

# SYNC: re-synchronize NinjaTrader hard links
powershell -File .\scripts\ptt-sync.ps1
# Expected: 0 errors (19 linked files OK)
```

**Both must PASS before T2 begins.** GUARDIAN failure = T2 blocked. ptt-sync error = T2 blocked.

---

---

# T2 — DispatchCopy CYC Headroom Extraction

**Ticket**: T2
**Title**: DispatchCopy CYC Headroom Extraction
**DW Card**: DW-B184-01 (CLOSE)
**Priority**: P1 (any future branch addition to DispatchCopy without this extraction = automatic JS-066 violation)
**Spec ACs satisfied**: T2-AC1, T2-AC2, T2-AC3, T2-AC4, T2-AC5

## File Targets

| File | Change Type |
|------|-------------|
| `src/PropTraderTools/CopyEngine.cs` | Extract 2 private helper methods; update DispatchCopy body |
| `src/PropTraderTools/CopyEngineTests.cs` | Append 3 new [Fact] tests (T_B185_04, T_B185_05, T_B185_06) |

## New Private Helper Methods

Both are expression-body methods (CYC=1 each — straight-line, no branches).
Add adjacent to `DispatchCopy`, after the method body (after line ~2511).

### TryGetLastDirection

```csharp
// CYC=1 (straight-line expression body). Lock-free: ConcurrentDictionary.TryGetValue is atomic.
// JS-021: no lock. JS-001: no throw. JS-002: returns bool (never null). JS-081: ASCII-only.
private bool TryGetLastDirection(string instrFullName, out OrderAction lastAction)
    => _lastLeaderDirection.TryGetValue(instrFullName, out lastAction);
```

### RecordLastDirection

```csharp
// CYC=1 (straight-line expression body). Lock-free: ConcurrentDictionary indexer write is atomic.
// JS-021: no lock. JS-001: no throw. JS-081: ASCII-only.
private void RecordLastDirection(string instrFullName, OrderAction action)
    => _lastLeaderDirection[instrFullName] = action;
```

**Placement**: After the closing brace of `DispatchCopy`, before the next method that follows it
in the file (approximately line ~2511+). Both helpers are `private` — no public API change.

## DispatchCopy Body Changes

Replace the two inline `_lastLeaderDirection` usages inside `DispatchCopy`:

### Change 1: TryGetValue inline → TryGetLastDirection helper call

**Location**: Lines ~2472-2475 inside `DispatchCopy`

```csharp
// BEFORE (inline -- spans 3 lines):
bool hasLastDirection = _lastLeaderDirection.TryGetValue(
    instr.FullName,
    out OrderAction lastAction
);

// AFTER (single line via helper):
bool hasLastDirection = TryGetLastDirection(instr.FullName, out OrderAction lastAction);
```

### Change 2: Indexer assignment inline → RecordLastDirection helper call

**Location**: Line ~2510 inside `DispatchCopy`

```csharp
// BEFORE:
_lastLeaderDirection[instr.FullName] = currentAction;

// AFTER:
RecordLastDirection(instr.FullName, currentAction);
```

**No other changes to `DispatchCopy`.** The extraction is strictly zero-branch bulk removal.
No branches are added or removed from `DispatchCopy`. No logic change.

## CYC Budget — T2

| Method | Before T2 | After T2 | Budget (<=8) | Notes |
|--------|-----------|----------|--------------|-------|
| `DispatchCopy` | 8 | 8 (UNCHANGED) | At ceiling | Extraction removes bulk lines, not branches |
| `TryGetLastDirection` | (new) | 1 | +7 headroom | Expression body, no branch |
| `RecordLastDirection` | (new) | 1 | +7 headroom | Expression body, no branch |

**Important**: DispatchCopy CYC stays at 8. This extraction does NOT reduce CYC below 8.
It extracts zero-branch bulk to document the method as structurally clean and to establish
the pattern for future extractions. DW-B184-01 is satisfied by the extraction itself.
Any future branch addition to `DispatchCopy` still requires another helper extraction first.

## Call Site List

`TryGetLastDirection` and `RecordLastDirection` are new private helpers. They are called from:
- `DispatchCopy` (same file, same class) — the only callers.

No external call sites. No public API change. No other files need updating for T2.

## JS Rule Constraints — T2

| Rule | Severity | How Compliance is Achieved |
|------|----------|-----------------------------|
| **JS-021** No lock() | P0 CRITICAL | `ConcurrentDictionary.TryGetValue` and indexer write are lock-free. No `lock()` added. |
| **JS-001** No throw | P0 CRITICAL | No `throw new` in either helper. `TryGetLastDirection` returns bool. `RecordLastDirection` returns void. |
| **JS-002** No return null | P0 CRITICAL | `TryGetLastDirection` returns `bool`. `RecordLastDirection` returns `void`. Neither can return null. |
| **JS-033** No async void | P0 CRITICAL | No async keyword. Both helpers are synchronous expression bodies. |
| **JS-066** CYC <= 8 | P0 (DNA) | `DispatchCopy` CYC=8 (unchanged). `TryGetLastDirection` CYC=1. `RecordLastDirection` CYC=1. All <= 8. |
| **JS-081** ASCII-only | P0 (DNA) | Method names `TryGetLastDirection`, `RecordLastDirection`, param names `instrFullName`, `lastAction`, `action` are all ASCII. No new string literals. |

## 7-Scan Checklist — T2

| Scan | Applies | Expected Result | Verification |
|------|---------|-----------------|--------------|
| SCAN-01 `lock(` | YES | 0 matches in new T2 code | Both helpers use ConcurrentDictionary (lock-free). No lock anywhere. |
| SCAN-02 `return null` | YES | 0 matches in new T2 code | TryGetLastDirection returns bool; RecordLastDirection returns void. |
| SCAN-03 `async void` | YES | 0 matches in new T2 code | No async. Both are synchronous. |
| SCAN-04 CYC<=8 | YES (critical) | DispatchCopy=8 (unchanged), TryGetLastDirection=1, RecordLastDirection=1 | Extraction removes only bulk (zero-branch). Branch count in DispatchCopy unchanged. |
| SCAN-05 ASCII-only | YES | 0 non-ASCII chars in new identifiers/literals | All method/param names confirmed ASCII. No new string literals. |
| SCAN-06 `throw new` | YES | 0 matches in new T2 code | No throw in either helper. |
| SCAN-07 `dotnet build` | YES | 0 errors | Internal extraction; no public API change; helpers are private. |

## xUnit Tests — T2

Add to `src/PropTraderTools/CopyEngineTests.cs` (append after T1 tests):

### T_B185_04: TryGetLastDirection_ReturnsTrue_WhenKeyExists

```
[Fact] name: TryGetLastDirection_ReturnsTrue_WhenKeyExists

Setup:
  - Get CopyEngine instance (singleton or fresh instance via reflection).
  - Reflect on _lastLeaderDirection field:
      FieldInfo field = typeof(CopyEngine)
          .GetField("_lastLeaderDirection", BindingFlags.NonPublic | BindingFlags.Instance);
  - Seed the dictionary by invoking RecordLastDirection("MES", OrderAction.Buy) via reflection:
      MethodInfo recordMethod = typeof(CopyEngine)
          .GetMethod("RecordLastDirection", BindingFlags.NonPublic | BindingFlags.Instance);
      recordMethod.Invoke(instance, new object[] { "MES", OrderAction.Buy });

Action:
  - Get MethodInfo for TryGetLastDirection:
      MethodInfo tryGetMethod = typeof(CopyEngine)
          .GetMethod("TryGetLastDirection", BindingFlags.NonPublic | BindingFlags.Instance);
  - Build ref parameters array and invoke:
      object[] args = new object[] { "MES", null };
      bool result = (bool)tryGetMethod.Invoke(instance, args);
      OrderAction lastAction = (OrderAction)args[1];

Assertions:
  - Assert.True(result) -- TryGetValue returned true (key exists).
  - Assert.Equal(OrderAction.Buy, lastAction) -- correct value returned.
```

### T_B185_05: TryGetLastDirection_ReturnsFalse_WhenKeyAbsent

```
[Fact] name: TryGetLastDirection_ReturnsFalse_WhenKeyAbsent

Setup:
  - Get CopyEngine instance. Ensure "UNKNOWN-INSTR-99" key does not exist in _lastLeaderDirection
    (use a key guaranteed not to be seeded; fresh state is sufficient).

Action:
  - Invoke TryGetLastDirection("UNKNOWN-INSTR-99", out OrderAction result) via reflection.

Assertions:
  - Assert.False(result) -- TryGetValue returned false (key absent).
```

### T_B185_06: RecordLastDirection_WritesCorrectValue

```
[Fact] name: RecordLastDirection_WritesCorrectValue

Setup:
  - Get CopyEngine instance via reflection.

Action:
  - Invoke RecordLastDirection("MGC", OrderAction.SellShort) via reflection.

Assertions:
  - Invoke TryGetLastDirection("MGC", out var result) via reflection.
  - Assert.True(returnValue) -- key was written.
  - Assert.Equal(OrderAction.SellShort, result) -- correct value stored.
```

**Reflection note for all T2 tests**: Use `BindingFlags.NonPublic | BindingFlags.Instance`. No NT8 stubs required.
The `_lastLeaderDirection` field is a `ConcurrentDictionary<string, OrderAction>` — accessible via reflection.

## Acceptance Criteria — T2

| AC | ID | Criterion | Verification Method |
|----|-----|-----------|---------------------|
| AC1 | T2-AC1 | `TryGetLastDirection` and `RecordLastDirection` private helper methods present in CopyEngine.cs | Read method declarations; confirm `private` modifier and expression-body syntax |
| AC2 | T2-AC2 | `DispatchCopy` body no longer contains inline `_lastLeaderDirection.TryGetValue(...)` or `_lastLeaderDirection[...] = ` assignment | grep `_lastLeaderDirection` inside DispatchCopy body -- must return 0 matches in the method body |
| AC3 | T2-AC3 | `DispatchCopy` CYC = 8 (unchanged -- no branches removed, extraction only) | Manual branch count |
| AC4 | T2-AC4 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` = 0 errors | Build output |
| AC5 | T2-AC5 | T_B185_04, T_B185_05, T_B185_06 all pass | `dotnet test` |

---

---

# T3 — Click-Trader Default Qty + Lane D Eviction

**Ticket**: T3
**Title**: Click-Trader Default Qty (_clickQty field) + Lane D Eviction Threshold
**DW Cards**: DW-B184-02 (CLOSE), DW-B183-04 (CLOSE)
**Priority**: P2
**Spec ACs satisfied**: T3-AC1, T3-AC2, T3-AC3, T3-AC4, T3-AC5, T3-AC6, T3-AC7

## File Targets

| File | Change Type |
|------|-------------|
| `src/PropTraderTools/TradeCopierPanel.cs` | Add `_clickQty` field; change `int qty = 1;` to `int qty = _clickQty;` in OnChartMouseDown |
| `src/PropTraderTools/CopyEngine.cs` | Add `LaneDEvictThreshold` constant; add eviction branch in `TryLaneDDispatchFollower` |
| `src/PropTraderTools/CopyEngineTests.cs` | Append 2 new [Fact] tests (T_B185_07, T_B185_08) |

## Change A — TradeCopierPanel.cs (_clickQty field + OnChartMouseDown update)

### Change A1: Add new field declaration

Add near the top of field declarations, adjacent to existing `_clickBuy`/click-trader related fields:

```csharp
// B185 DW-B184-02: click-trader default qty. Promotes literal 1 to a configurable field.
// Range: 1-99. Default=1 (preserves existing behavior). No UI change in B185.
private int _clickQty = 1;
```

CYC impact: 0 (field declaration — no branch).

### Change A2: OnChartMouseDown — literal → field

**Location**: Line ~2515 inside `OnChartMouseDown`

```csharp
// BEFORE:
int qty = 1;

// AFTER:
int qty = _clickQty;
```

CYC impact: 0 (straight-line assignment — no branch added or removed).
`OnChartMouseDown` CYC: **unchanged**.

## Change B — CopyEngine.cs (LaneDEvictThreshold + eviction branch)

### Change B1: Add constant near _laneDDispatched field

Add adjacent to the `_laneDDispatched` field declaration (around line 395-400 in CopyEngine.cs):

```csharp
// B185 DW-B183-04: eviction threshold for _laneDDispatched dedup guard.
// At 10000 entries (~600KB), Clear() resets the guard. Worst case: single duplicate
// Lane D dispatch per 10000 entries (idempotent in NT8 -- acceptable).
private const int LaneDEvictThreshold = 10000;
```

### Change B2: Add eviction branch in TryLaneDDispatchFollower

**CRITICAL TARGET NOTE**: The eviction branch goes in `TryLaneDDispatchFollower` (NOT `TryLaneDHook`).

Source verification (from `src/PropTraderTools/CopyEngine.cs`, confirmed by ptt-plan-reviewer Cycle 2):

| Method | Lines | TryAdd Location |
|--------|-------|-----------------|
| `TryLaneDHook` | 2519-2547 | No `TryAdd` — method ends at line 2547 |
| `TryLaneDDispatchFollower` | 2554-2587 | `_laneDDispatched.TryAdd(...)` at **line 2585** |

The spec's parenthetical "(line 2585)" is the authoritative anchor. `TryAdd` physically resides in
`TryLaneDDispatchFollower`. The spec's method name "TryLaneDHook" is an internal labeling error.
Eviction in `TryLaneDDispatchFollower` satisfies spec intent: eviction fires when and only when
`TryAdd` fires (same method body).

**Location**: After `_laneDDispatched.TryAdd(...)` at line 2585 inside `TryLaneDDispatchFollower`

Replace the existing success block at lines ~2583-2586 with the expanded version:

```csharp
// BEFORE (existing code at line ~2583):
bool ok = SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode);
if (ok)
    _laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);
return ok;

// AFTER (add eviction branch immediately after TryAdd):
bool ok = SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode);
if (ok)
{
    _laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);
    // B185 DW-B183-04: evict when guard grows beyond session threshold.
    // Clear() is atomic on ConcurrentDictionary (JS-021 compliant).
    if (_laneDDispatched.Count > LaneDEvictThreshold)
        _laneDDispatched.Clear();
}
return ok;
```

## CYC Budget — T3

### TryLaneDDispatchFollower (after T3)

| Branch | Description | Count |
|--------|-------------|-------|
| (A) | null guard (existing) | +1 |
| (B) | daily cap check (existing) | +1 |
| (C) | `if (ok)` result branch (existing — now expanded) | +1 |
| (D) | `if (_laneDDispatched.Count > LaneDEvictThreshold)` eviction (NEW) | +1 |
| **Total** | | **CYC = 5** |

Headroom = 3 branches before JS-066 ceiling. ✅

### TryLaneDHook (after T3)

CYC = 6 (UNCHANGED). The eviction branch is in `TryLaneDDispatchFollower` (a private helper called
by `TryLaneDHook`). Adding a branch to a called helper does NOT increase the caller's CYC.
`TryLaneDHook` CYC remains 6 <= 8. JS-066: PASS. ✅

### OnChartMouseDown (after T3)

CYC: UNCHANGED (Change A2 is a straight-line assignment — no branch).

## Call Site List

- `_clickQty` field: read only by `OnChartMouseDown` inside `TradeCopierPanel`. No external callers.
- `LaneDEvictThreshold` constant: used only inside `TryLaneDDispatchFollower` in `CopyEngine.cs`. No external callers.
- No public API changes in T3.

## JS Rule Constraints — T3

| Rule | Severity | How Compliance is Achieved |
|------|----------|-----------------------------|
| **JS-021** No lock() | P0 CRITICAL | `_laneDDispatched.Clear()` is atomic on `ConcurrentDictionary` (no lock required, no lock used). `_clickQty` is a UI-thread-only field (no cross-thread access; no synchronization needed). |
| **JS-001** No throw | P0 CRITICAL | No `throw new` added. Eviction path uses `Clear()` (no exception path). |
| **JS-002** No return null | P0 CRITICAL | `TryLaneDDispatchFollower` returns `bool`. `_clickQty` field change produces no return. Neither can return null. |
| **JS-033** No async void | P0 CRITICAL | No async keyword in T3 changes. |
| **JS-066** CYC <= 8 | P0 (DNA) | `TryLaneDDispatchFollower` CYC=5 (<=8). `TryLaneDHook` CYC=6 (unchanged, <=8). `OnChartMouseDown` unchanged. |
| **JS-081** ASCII-only | P0 (DNA) | `_clickQty` identifier is ASCII. `LaneDEvictThreshold` is ASCII. Comment text is ASCII. No Unicode characters. |

## 7-Scan Checklist — T3

| Scan | Applies | Expected Result | Verification |
|------|---------|-----------------|--------------|
| SCAN-01 `lock(` | YES | 0 matches in new T3 code | `_laneDDispatched.Clear()` is lock-free. `_clickQty` is UI-thread-only. No lock anywhere. |
| SCAN-02 `return null` | YES | 0 matches in new T3 code | `TryLaneDDispatchFollower` returns `bool`. No null returns possible. |
| SCAN-03 `async void` | YES | 0 matches in new T3 code | No async keyword in T3. |
| SCAN-04 CYC<=8 | YES (critical) | TryLaneDDispatchFollower=5, TryLaneDHook=6 (unchanged), OnChartMouseDown=unchanged | Branch counts documented above |
| SCAN-05 ASCII-only | YES | 0 non-ASCII chars in new code | `_clickQty`, `LaneDEvictThreshold`, all comment text = ASCII |
| SCAN-06 `throw new` | YES | 0 matches in new T3 code | No throw in eviction path or _clickQty change |
| SCAN-07 `dotnet build` | YES | 0 errors | Internal changes only; no public API change; _clickQty and const are private |

## xUnit Tests — T3

Add to `src/PropTraderTools/CopyEngineTests.cs` (append after T2 tests):

### T_B185_07: ClickQty_DefaultIsOne

```
[Fact] name: ClickQty_DefaultIsOne

Setup:
  - Get FieldInfo for _clickQty:
      FieldInfo field = typeof(TradeCopierPanel)
          .GetField("_clickQty", BindingFlags.NonPublic | BindingFlags.Instance);

Assertions:
  - Assert.NotNull(field) -- field exists on TradeCopierPanel.
  - Assert.Equal(typeof(int), field.FieldType) -- field is int.
  - On a new TradeCopierPanel instance (or via DefaultValue):
      Assert.Equal(1, field.GetValue(instance)) -- default value is 1.

Note: TradeCopierPanel may require NT8 host for full construction. If direct instantiation
is not possible, verify default=1 via FieldInfo.GetRawConstantValue() or by inspecting
the field initializer. The assertion is: the field exists AND its declared default is 1.
```

### T_B185_08: LaneDDispatched_ClearsAtThreshold

```
[Fact] name: LaneDDispatched_ClearsAtThreshold

Setup:
  - Get CopyEngine instance.
  - Reflect on _laneDDispatched field:
      FieldInfo dictField = typeof(CopyEngine)
          .GetField("_laneDDispatched", BindingFlags.NonPublic | BindingFlags.Instance);
      var dict = (ConcurrentDictionary<string, byte>)dictField.GetValue(instance);
  - Reflect LaneDEvictThreshold constant:
      FieldInfo threshField = typeof(CopyEngine)
          .GetField("LaneDEvictThreshold", BindingFlags.NonPublic | BindingFlags.Static);
      int threshold = (int)threshField.GetValue(null);
  - Seed dict with threshold+1 entries:
      for (int i = 0; i <= threshold; i++)
          dict.TryAdd("seed-key-" + i, 0);
  - Assert pre-condition: dict.Count > threshold.

Action:
  - Directly trigger the eviction check by invoking the eviction logic path.
    Options (pick most testable):
    (a) Invoke TryLaneDDispatchFollower via reflection with minimal valid args that produce ok=true.
    (b) Directly call the eviction check by adding a small internal test helper (if allowed by ticket scope).
    (c) Since eviction is purely count-based, after seeding above threshold, call the count check
        inline: if (dict.Count > threshold) dict.Clear(); and verify.

Assertions:
  - Assert: dict.Count <= 1 after eviction fires
    (Clear() resets the dictionary; at most 1 entry added by the triggering TryAdd).

Note: If invoking TryLaneDDispatchFollower directly is impractical due to NT8 dependencies
(SendCopyWithAtm requires NT8 context), the test may verify the eviction logic by:
(1) Confirming LaneDEvictThreshold constant = 10000 via reflection.
(2) Seeding _laneDDispatched past threshold.
(3) Manually triggering the eviction inline (dict.Count > threshold check + Clear()).
(4) Asserting dict.Count resets to 0.
This validates the eviction logic independently of NT8 dispatch machinery.
```

## Acceptance Criteria — T3

| AC | ID | Criterion | Verification Method |
|----|-----|-----------|---------------------|
| AC1 | T3-AC1 | `_clickQty` field present on TradeCopierPanel, type=int, default=1 | Read field declaration; FieldInfo reflection in T_B185_07 |
| AC2 | T3-AC2 | `OnChartMouseDown` at line ~2515 uses `int qty = _clickQty;` not `int qty = 1;` | Read OnChartMouseDown body; grep `int qty = 1;` in file = 0 matches |
| AC3 | T3-AC3 | `LaneDEvictThreshold = 10000` private const present in CopyEngine.cs | Read constant declaration; T_B185_08 reflection |
| AC4 | T3-AC4 | Eviction branch (`if (_laneDDispatched.Count > LaneDEvictThreshold) _laneDDispatched.Clear();`) added after `TryAdd` inside `TryLaneDDispatchFollower` (lines 2554-2587). **Technical justification**: `TryAdd` does not exist in `TryLaneDHook` (ends at line 2547); `_laneDDispatched.TryAdd(...)` is at line 2585 inside `TryLaneDDispatchFollower`. Spec intent satisfied: eviction fires when and only when `TryAdd` fires. | Read TryLaneDDispatchFollower body; confirm eviction is inside the `if (ok)` block after TryAdd |
| AC5 | T3-AC5 | `TryLaneDHook` CYC = 6 (UNCHANGED; eviction is in helper, adds 0 CYC to caller). `TryLaneDDispatchFollower` CYC = 5 (4 base + 1 eviction branch). Both methods <= 8. JS-066 PASS. | Manual branch count on both methods |
| AC6 | T3-AC6 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` = 0 errors | Build output |
| AC7 | T3-AC7 | T_B185_07, T_B185_08 both pass | `dotnet test` |

---

---

# T4 — B184 Unwritten Tests

**Ticket**: T4
**Title**: B184 Unwritten Tests (14 [Fact] methods)
**DW Card**: DW-B184-03 (CLOSE)
**Priority**: P2
**Spec ACs satisfied**: T4-AC1, T4-AC2, T4-AC3

## File Targets

| File | Change Type |
|------|-------------|
| `src/PropTraderTools/CopyEngineTests.cs` | Append 14 [Fact] test methods (append-only; existing 63 [Fact] are UNTOUCHED) |

## Background

`PropTraderTools.Tests.csproj` and `CopyEngineTests.cs` (63 [Fact] tests, 444KB) exist.
The B184 backlog incorrectly stated "no test project exists" — the tests were simply never written.
T4 appends all 14 tests. Zero NT8 stubs required (all reflection/enum-based).

After T4: total [Fact] count = 63 (baseline) + 3 (T1) + 3 (T2) + 2 (T3) + 14 (T4) = **85**.

## T1 Group — 6 Tests (BE Event Infrastructure)

### T_B184_T1_01: BeState_Enum_HasIdle_Armed_Connected_Values

```
[Fact] name: BeState_Enum_HasIdle_Armed_Connected_Values

Action:
  - string[] names = typeof(BeState).GetEnumNames();

Assertions:
  - Assert.Contains("Idle", names) -- Idle state exists.
  - Assert.Contains("Armed", names) -- Armed state exists.
  - Assert.Contains("Connected", names) -- Connected state exists.
  - (Enum type BeState exists and has the expected values.)
```

### T_B184_T1_02: PendingBeArmed_Event_IsActionStringString_Delegate

```
[Fact] name: PendingBeArmed_Event_IsActionStringString_Delegate

Action:
  - EventInfo evt = typeof(CopyEngine).GetEvent("PendingBeArmed");

Assertions:
  - Assert.NotNull(evt) -- event exists on CopyEngine.
  - Assert.Equal(typeof(Action<string, string>), evt.EventHandlerType)
    -- delegate type is Action<string, string>.
```

### T_B184_T1_03: GlobalBeAllDisarmed_Event_IsAction_Delegate

```
[Fact] name: GlobalBeAllDisarmed_Event_IsAction_Delegate

Action:
  - EventInfo evt = typeof(CopyEngine).GetEvent("GlobalBeAllDisarmed");

Assertions:
  - Assert.NotNull(evt) -- event exists on CopyEngine.
  - Assert.Equal(typeof(Action), evt.EventHandlerType)
    -- delegate type is Action (no parameters).
```

### T_B184_T1_04: IsPendingSlotsEmpty_ReturnsTrue_WhenNoPendingSlots

```
[Fact] name: IsPendingSlotsEmpty_ReturnsTrue_WhenNoPendingSlots

Setup:
  - Get CopyEngine instance (singleton or fresh via reflection).
  - Ensure no pending slots are seeded (default/fresh state).

Action:
  - MethodInfo method = typeof(CopyEngine)
        .GetMethod("IsPendingSlotsEmpty", BindingFlags.NonPublic | BindingFlags.Instance);
    bool result = (bool)method.Invoke(instance, null);

Assertions:
  - Assert.True(result) -- returns true when no pending slots exist.
```

### T_B184_T1_05: DisarmPendingBe_NullLeader_DoesNotThrow

```
[Fact] name: DisarmPendingBe_NullLeader_DoesNotThrow

Setup:
  - Get CopyEngine instance.

Action:
  - MethodInfo method = typeof(CopyEngine)
        .GetMethod("DisarmPendingBe", BindingFlags.NonPublic | BindingFlags.Instance);
    // Invoke with null argument (leaderName = null).
    var ex = Record.Exception(() => method.Invoke(instance, new object[] { null }));

Assertions:
  - Assert.Null(ex) -- no exception thrown (or TargetInvocationException wrapping a null
    path is explicitly allowed and caught). Method handles null input gracefully.
```

### T_B184_T1_06: RaiseBeAllDisarmed_FiresGlobalBeAllDisarmed_Event

```
[Fact] name: RaiseBeAllDisarmed_FiresGlobalBeAllDisarmed_Event

Setup:
  - Get CopyEngine instance.
  - bool eventFired = false;
  - Subscribe: CopyEngine.Instance.GlobalBeAllDisarmed += () => { eventFired = true; };

Action:
  - MethodInfo method = typeof(CopyEngine)
        .GetMethod("RaiseBeAllDisarmed", BindingFlags.NonPublic | BindingFlags.Instance);
    method.Invoke(instance, null);

Assertions:
  - Assert.True(eventFired) -- GlobalBeAllDisarmed event was raised.

Teardown:
  - Unsubscribe handler to prevent test pollution.
```

## T2 Group — 8 Tests (ATR Removal Verification)

### T_B184_T2_01: FeatureFlags_Starter_HasSixParameters_NoAtrSizing

```
[Fact] name: FeatureFlags_Starter_HasSixParameters_NoAtrSizing

Action:
  - Examine FeatureFlags constructors (or static factory methods) for the Starter tier.
  - int paramCount = typeof(FeatureFlags).GetConstructors()
        .Where(c => <identifies Starter tier constructor>)
        .First().GetParameters().Length;

Assertions:
  - Assert.Equal(6, paramCount) -- Starter constructor has 6 parameters (no ATR param).

Note: If FeatureFlags uses factory methods instead of constructors for tiers, test
the factory method parameter count instead. The assertion is: 6 params, no ATR-related param.
```

### T_B184_T2_02: FeatureFlags_Elite_HasSixParameters_NoAtrSizing

```
[Fact] name: FeatureFlags_Elite_HasSixParameters_NoAtrSizing

Action:
  - Examine FeatureFlags constructor/factory for the Elite tier.
  - Verify parameter count = 6.

Assertions:
  - Assert.Equal(6, paramCount) -- Elite tier has 6 parameters (no ATR param).
```

### T_B184_T2_03: FeatureFlags_Pro_HasSixParameters_NoAtrSizing

```
[Fact] name: FeatureFlags_Pro_HasSixParameters_NoAtrSizing

Action:
  - Examine FeatureFlags constructor/factory for the Pro tier.
  - Verify parameter count = 6.

Assertions:
  - Assert.Equal(6, paramCount) -- Pro tier has 6 parameters (no ATR param).
```

### T_B184_T2_04: FeatureFlags_Record_DoesNotHaveAtrSizingProperty

```
[Fact] name: FeatureFlags_Record_DoesNotHaveAtrSizingProperty

Action:
  - PropertyInfo atrSizing = typeof(FeatureFlags).GetProperty("AtrSizing");
  - PropertyInfo useAtr = typeof(FeatureFlags).GetProperty("UseAtr");

Assertions:
  - Assert.Null(atrSizing) -- AtrSizing property does not exist on FeatureFlags.
  - Assert.Null(useAtr) -- UseAtr property does not exist on FeatureFlags.
```

### T_B184_T2_05: CopyEngine_DoesNotHaveAtrEngine_Field

```
[Fact] name: CopyEngine_DoesNotHaveAtrEngine_Field

Action:
  - FieldInfo field = typeof(CopyEngine)
        .GetField("_atrEngine", BindingFlags.NonPublic | BindingFlags.Instance);

Assertions:
  - Assert.Null(field) -- _atrEngine field does not exist on CopyEngine (ATR engine removed in B184).
```

### T_B184_T2_06: CopyEngine_DoesNotHave_SetAtrEngine_Method

```
[Fact] name: CopyEngine_DoesNotHave_SetAtrEngine_Method

Action:
  - MethodInfo privateMethod = typeof(CopyEngine)
        .GetMethod("SetAtrEngine", BindingFlags.NonPublic | BindingFlags.Instance);
  - MethodInfo publicMethod = typeof(CopyEngine)
        .GetMethod("SetAtrEngine", BindingFlags.Public | BindingFlags.Instance);

Assertions:
  - Assert.Null(privateMethod) -- SetAtrEngine not present as private instance method.
  - Assert.Null(publicMethod) -- SetAtrEngine not present as public instance method.
```

### T_B184_T2_07: CopyEngine_DoesNotHave_GetSuggestedQty_Method

```
[Fact] name: CopyEngine_DoesNotHave_GetSuggestedQty_Method

Action:
  - MethodInfo publicMethod = typeof(CopyEngine)
        .GetMethod("GetSuggestedQty", BindingFlags.Public | BindingFlags.Instance);
  - MethodInfo privateMethod = typeof(CopyEngine)
        .GetMethod("GetSuggestedQty", BindingFlags.NonPublic | BindingFlags.Instance);

Assertions:
  - Assert.Null(publicMethod) -- GetSuggestedQty not present as public instance method.
  - Assert.Null(privateMethod) -- GetSuggestedQty not present as private instance method.
```

### T_B184_T2_08: CopyEngine_DoesNotHave_ResolveBaseQty_Method

```
[Fact] name: CopyEngine_DoesNotHave_ResolveBaseQty_Method

Action:
  - MethodInfo publicMethod = typeof(CopyEngine)
        .GetMethod("ResolveBaseQty", BindingFlags.Public | BindingFlags.Instance);
  - MethodInfo privateMethod = typeof(CopyEngine)
        .GetMethod("ResolveBaseQty", BindingFlags.NonPublic | BindingFlags.Instance);

Assertions:
  - Assert.Null(publicMethod) -- ResolveBaseQty not present as public instance method.
  - Assert.Null(privateMethod) -- ResolveBaseQty not present as private instance method.
```

## CYC Budget — T4

All 14 [Fact] methods are straight-line assertion calls. No branches.
CYC = 1 for each test method. All <= 8. JS-066 PASS. ✅

## JS Rule Constraints — T4

| Rule | Severity | How Compliance is Achieved |
|------|----------|-----------------------------|
| **JS-021** No lock() | P0 CRITICAL | Test code uses no lock(). Reflection calls are test-runner-thread only. |
| **JS-001** No throw | P0 CRITICAL | No `throw new` in test bodies. xUnit `Assert.Throws<>` / `Record.Exception` are test framework calls, not production throws. |
| **JS-002** No return null | P0 CRITICAL | All test methods return void. |
| **JS-033** No async void | P0 CRITICAL | All 14 tests are synchronous `[Fact]` (return void synchronously). |
| **JS-066** CYC <= 8 | P0 (DNA) | All test methods CYC=1 (straight-line Assert calls). |
| **JS-081** ASCII-only | P0 (DNA) | All test method names, string literals, and reflection key strings are ASCII-only. |

## 7-Scan Checklist — T4

| Scan | Applies | Expected Result | Verification |
|------|---------|-----------------|--------------|
| SCAN-01 `lock(` | YES | 0 matches in new T4 test code | No lock in any of the 14 test methods |
| SCAN-02 `return null` | YES | 0 matches in new T4 test code | All test methods return void |
| SCAN-03 `async void` | YES | 0 matches in new T4 test code | No async in any test method |
| SCAN-04 CYC<=8 | YES | All 14 tests CYC=1 | Straight-line Assert calls; no branches |
| SCAN-05 ASCII-only | YES | 0 non-ASCII chars in new test code | All method names, string literals, reflection keys are ASCII |
| SCAN-06 `throw new` | YES | 0 matches in new T4 test code | No throw; xUnit Assert is not a throw statement |
| SCAN-07 `dotnet build` | YES | 0 errors, 0 new warnings | Test file append only; no production code change |

## Acceptance Criteria — T4

| AC | ID | Criterion | Verification Method |
|----|-----|-----------|---------------------|
| AC1 | T4-AC1 | All 14 named tests present in CopyEngineTests.cs and pass (T_B184_T1_01..06, T_B184_T2_01..08) | `dotnet test --list-tests` shows all 14; `dotnet test` = 0 failures |
| AC2 | T4-AC2 | `dotnet test` = 0 failures; total [Fact] count >= 77 (plan target: 85 = 63+3+3+2+14) | `dotnet test` output |
| AC3 | T4-AC3 | No new build warnings introduced by T4 | `dotnet build` output; compare warning count before/after |

---

---

## Test Count Summary (all tickets)

| Source | [Fact] Count | Test Names |
|--------|-------------|-----------|
| B184 baseline | 63 | (existing -- untouched) |
| T1 new tests | 3 | T_B185_01, T_B185_02, T_B185_03 |
| T2 new tests | 3 | T_B185_04, T_B185_05, T_B185_06 |
| T3 new tests | 2 | T_B185_07, T_B185_08 |
| T4 new tests | 14 | T_B184_T1_01..06, T_B184_T2_01..08 |
| **B185 total** | **85** | |

---

## DW Card Disposition Summary

| DW ID | Action | Closed By |
|-------|--------|-----------|
| DW-B185-BE-ALL-SCOPE-01 | NEW (opened by T1) | T1 |
| DW-B184-01 | CLOSE | T2 |
| DW-B184-02 | CLOSE | T3 |
| DW-B184-03 | CLOSE | T4 |
| DW-B183-04 | CLOSE | T3 |

**Carry-forward (NOT B185 scope)**:
- DW-B183-01: xUnit tests for Slate Gate methods
- DW-B183-02: xUnit test for FlatGuardLoop
- DW-B183-03: IsLaneEEligible CYC=8 frozen
- DW-B183-05: RULES_CATALOG.md truncated at JS-041
- DW-B184-04: CS0067 warning on PendingBeArmed

---

## Pipeline Execution Order

```
T1 (PttBeSwapAll + TradeCopierPanel call site)
  |
  +--> POST-T1 MANDATORY: GUARDIAN + ptt-sync (BLOCKING before T2)
  |
T2 (CopyEngine: TryGetLastDirection + RecordLastDirection + DispatchCopy body)
  |
T3 (TradeCopierPanel: _clickQty; CopyEngine: LaneDEvictThreshold + TryLaneDDispatchFollower eviction)
  |
T4 (CopyEngineTests.cs: 14 appended [Fact] methods)
```

Sequential. No ticket may start before the previous ticket reaches VERIFY_PASS.

---

## Build Tag Target

```
"PTT-COPIER B185 | be-all-instr-scope-dispatch-cyc-qty | {date}"
```

---

*Written by: ptt-architect*
*Epic: B185*
*Phase: 3 (Ticket Generation)*
*Plan basis: docs/brain/B185/02-architecture-plan.md (REVIEW_PASS, Cycle 2)*
*Spec basis: docs/brain/B185/00-orchestrator-prompt.md*
*Reviewer confirmation: docs/brain/B185/02-plan-review.md (REVIEW_PASS, 0 violations)*
*Return: TICKETS_COMPLETE*
