# B186 Architecture Plan
## PTT-COPIER B186 | Quick ALL + QAll2t Instrument Scope

**Author**: ptt-architect (Phase 1)
**Date**: 2026-07-08
**Baseline**: 0c27cd88 (B185-POST)
**Status**: PLAN_COMPLETE

---

## 0. LANE-SPLIT GATE

| Question | Answer |
|----------|--------|
| Q1: Same method or within 50 lines? | No — T1 and T2 are in different methods in the same file. T3 is a different file entirely. |
| Q2: Fix B design depends on Fix A final design? | T3 depends on T1 and T2 final signatures. T3 cannot merge independently. T2 follows the same pattern as T1 independently within the same file. |
| Q3: Each fix has standalone value if the other is blocked? | T1 has standalone value (Quick ALL scoped, QAll2t unscoped). T2 has no standalone value without T3 (OnInstrQAll2tClick). |
| Q4: Each fix has an independent SIM verification path? | Yes — Quick ALL button (T1+T3) and QAll2t button (T2+T3) are independently verifiable in NT8 SIM. |

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

Rationale: T1 and T2 are in the same file and follow the same instrument-scope pattern. T3 is a hard dependency on both. Parallel lane execution would create a race on PttGlobalQuickExit.cs. Sequential order: T1 → T2 → T3 (T1+T2 may be implemented in a single engineer pass on PttGlobalQuickExit.cs, then T3).

---

## 1. Component Inventory

| Component | File | Tickets |
|-----------|------|---------|
| `PttGlobalQuickExit` | `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | T1, T2 |
| `TradeCopierPanel` | `src/PropTraderTools/TradeCopierPanel.cs` | T3 |
| `CopyEngineTests` | `src/PropTraderTools/CopyEngineTests.cs` | T1+T2+T3 (6 tests) |

No new classes. No new files. All changes are surgical parameter additions to existing methods.

---

## 2. Pattern Reference — B185 PttBeSwapAll.Execute

B186 applies the exact same instrument-scope pattern that B185 applied to `PttBeSwapAll.Execute`.
The engineer MUST follow this pattern verbatim.

```csharp
// B185 PttBeSwapAll.Execute pattern (confirmed template):
internal void Execute(GlobalBeBuffer buffer, Instrument instrument)
{
    if (!flags.BreakEven || instrument == null) return;        // combined gate
    // ...
    foreach (Account acc in Account.All)
    {
        // ...
        foreach (Position pos in acc.Positions)
        {
            if (pos == null || pos.Quantity == 0) continue;
            if (pos.Instrument?.FullName != instrument.FullName) continue;  // instrument filter
            // ... rest of logic
        }
    }
}

// B185 T1 call site pattern (TradeCopierPanel):
beSwap.Execute(CopyEngine.Instance.GlobalBe.GlobalBeBuffer, _instrument);
```

---

## 3. T1 — DW-B186-QX-ALL-SCOPE-01: PttGlobalQuickExit.Execute() Instrument Scope

### File
`src/PropTraderTools/Features/PttGlobalQuickExit.cs`

### Spec requirement
DW-B186-QX-ALL-SCOPE-01

### Current signature
```csharp
internal void Execute()
```

### New signature
```csharp
internal void Execute(Instrument instrument)
```

### CYC Analysis
- Current Execute() CYC = 7
  - (1) acc foreach loop
  - (2) follower guard `if (engine != null && engine.IsFollowerAccount(acc)) continue;`
  - (3) pos foreach loop
  - (4) null/flat guard `if (pos == null || pos.Quantity == 0) continue;`
  - (5) DW-B115-DIAG (in LogLeaderDiag helper — NOT counted in Execute itself; but original doc comment attributes it here — see note)
  - (6) NeedsLeaderFallbackFlatten guard `if (...) { ... continue; }`
  - (7) ExecuteFollowers delegate call (counted as decision point by B120 doc comment)
- B186 adds: `if (pos.Instrument?.FullName != instrument.FullName) continue;` — +1 branch
- **New Execute(Instrument) CYC = 8** — AT CEILING. Acceptable per spec.

### Changes to make

**1. Combined gate (replaces existing split gate + blocked log):**
```csharp
// BEFORE:
if (!CopyEngine.Instance.Flags.QxGlobalExit)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-ALL] Blocked: Global Quick Exit requires Elite tier",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    return;
}

// AFTER:
if (!CopyEngine.Instance.Flags.QxGlobalExit || instrument == null) return;
```
NOTE: The "Blocked" log line is REMOVED. Combined gate is silent early-return on flag off or null instrument (matches B185 pattern exactly). The existing "[PTT-QX-ALL] GlobalQuickExit fired" log is replaced by the new instr-scoped log.

**2. New log line (replaces existing fired log):**
```csharp
// REMOVE old:
NinjaTrader.Code.Output.Process(
    "[PTT-QX-ALL] GlobalQuickExit fired",
    NinjaTrader.NinjaScript.PrintTo.OutputTab1
);

// ADD new (immediately after combined gate):
NinjaTrader.Code.Output.Process(
    "[PTT-QX-ALL] GlobalQuickExit fired instr=" + instrument.FullName,
    NinjaTrader.NinjaScript.PrintTo.OutputTab1);
```

**3. Instrument filter inside pos loop (new line after null/flat guard):**
```csharp
// After:
if (pos == null || pos.Quantity == 0)
    continue; // (4)

// ADD immediately after:
if (pos.Instrument?.FullName != instrument.FullName) continue; // (new) +1 CYC -> 8
```

**4. Updated doc comment:**
Replace CYC annotation:
```
/// CYC=7: acc loop(1), follower guard(2), pos loop(3), null/flat continue(4),
///        DW-B115-DIAG for-loop(5), NeedsLeaderFallbackFlatten guard(6), delegate via ExecuteFollowers(7).
```
with:
```
/// CYC=8: combined gate(1), acc loop(2), follower guard(3), pos loop(4),
///        null/flat continue(5), instrument filter(6), NeedsLeaderFallbackFlatten guard(7),
///        delegate via ExecuteFollowers(8).
/// B186 DW-B186-QX-ALL-SCOPE-01: instrument scope applied. Filter skips positions not on _instrument.
/// Combined gate: !QxGlobalExit || instrument == null -> early return (silent).
```

### What is NOT changed in T1
- ExecuteFollowers signature — UNCHANGED
- ExecuteOne signature — UNCHANGED  
- All helper methods — UNCHANGED
- The blocked log line for the QxGlobalExit flag is folded into the combined gate (silent return) per B185 pattern

### Tests (T_B186_01, T_B186_02)
See Section 6.

---

## 4. T2 — DW-B186-QX-ALL-SCOPE-02: PttGlobalQuickExit.Execute(forcedTargets) Instrument Scope

### File
`src/PropTraderTools/Features/PttGlobalQuickExit.cs`

### Spec requirement
DW-B186-QX-ALL-SCOPE-02 (closes DW-B185-QX-ALL-SCOPE-01 deferred item)

### Current signature
```csharp
internal void Execute(
    System.Collections.Generic.List<(double Price, int Qty)> forcedTargets
)
```

### New signature
```csharp
internal void Execute(
    System.Collections.Generic.List<(double Price, int Qty)> forcedTargets,
    Instrument instrument
)
```

### CYC Hold Strategy — CRITICAL
Current Execute(forcedTargets) CYC = 8:
- (1) flag guard `if (!CopyEngine.Instance.Flags.QxGlobalExit) { ... return; }`
- (2) null/empty guard `if (forcedTargets == null || forcedTargets.Count < 2) { ... return; }`
- (3) acc foreach loop
- (4) follower skip
- (5) pos foreach loop
- (6) null/flat guard `if (pos == null || pos.Quantity == 0) continue;`
- (7) NeedsLeaderFallbackFlatten guard
- (8) ExecuteFollowers delegate

**Strategy**: Fold instrument filter into existing branch (6) — extend compound condition rather than add new branch:
```csharp
// WAS (branch 6):
if (pos == null || pos.Quantity == 0)
    continue; // (6)

// BECOMES (branch 6 extended — CYC unchanged at 8):
if (pos == null || pos.Quantity == 0 || pos.Instrument?.FullName != instrument.FullName)
    continue; // (6) extended — instrument filter folded in
```
**CYC stays at 8** — extends existing branch condition, does not add a new branch.

### Changes to make

**1. Combined gate (replaces existing split gate + blocked log):**
```csharp
// BEFORE:
if (!CopyEngine.Instance.Flags.QxGlobalExit)
{
    NinjaTrader.Code.Output.Process(
        "[PTT-QX-2T-ALL] Blocked: Global Quick Exit requires Elite tier",
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
    return;
}

// AFTER:
if (!CopyEngine.Instance.Flags.QxGlobalExit || instrument == null) return;
```
NOTE: "Blocked" log folded into silent combined gate (same as T1).

**2. Extended null/flat/instrument guard (branch 6):**
```csharp
// BEFORE:
if (pos == null || pos.Quantity == 0)
    continue; // (6)

// AFTER:
if (pos == null || pos.Quantity == 0 || pos.Instrument?.FullName != instrument.FullName)
    continue; // (6) extended
```

**3. Updated doc comment:**
Replace:
```
/// CYC=8: flag-guard(1), null/empty-guard(2), acc-loop(3), follower-skip(4),
///        pos-loop(5), null/flat-continue(6), flatten-guard(7), ExecuteFollowers-call(8).
```
with:
```
/// CYC=8: combined-gate(1), null/empty-guard(2), acc-loop(3), follower-skip(4),
///        pos-loop(5), null/flat/instr-continue(6), flatten-guard(7), ExecuteFollowers-call(8).
/// B186 DW-B186-QX-ALL-SCOPE-02: instrument scope applied. Filter folded into branch (6).
/// Combined gate: !QxGlobalExit || instrument == null -> early return (silent).
```

### What is NOT changed in T2
- forcedTargets null/empty guard — UNCHANGED (branch 2 is separate and unaffected)
- ExecuteFollowers call — UNCHANGED
- ExecuteOne call — UNCHANGED
- Logging inside the account/position loop — UNCHANGED

### Tests (T_B186_04, T_B186_05)
See Section 6.

---

## 5. T3 — DW-B186-PANEL-CALLSITES-01: TradeCopierPanel.cs Call Sites

### File
`src/PropTraderTools/TradeCopierPanel.cs`

### Spec requirement
DW-B186-PANEL-CALLSITES-01

### Change 1 — OnQuickAllClick (line ~1617)

**Current:**
```csharp
// B41: OnQuickAllClick -- fires all-accounts Quick Exit bracket swap. CYC=1.
// JS-033: synchronous void event handler. JS-021: no lock.
private void OnQuickAllClick(object sender, RoutedEventArgs e)
{
    var gqx = new PttGlobalQuickExit();
    gqx.Execute();
}
```

**After:**
```csharp
// B41: OnQuickAllClick -- fires all-accounts Quick Exit bracket swap. CYC=1.
// B186 DW-B186-PANEL-CALLSITES-01: pass _instrument for instrument scope.
// JS-033: synchronous void event handler. JS-021: no lock.
private void OnQuickAllClick(object sender, RoutedEventArgs e)
{
    var gqx = new PttGlobalQuickExit();
    gqx.Execute(_instrument);
}
```

### Change 2 — OnInstrQAll2tClick (line ~1706)

**Current:**
```csharp
// B123/R9: OnInstrQAll2tClick -- fires global 2-target quick exit on all followers.
// CYC=2: (1)TryResolve2TargetContext false-path, (2)Execute call.
// JS-021: no lock. ASCII-only.
private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)
{
    if (!TryResolve2TargetContext(out int qty, out var targets))
        return; // (1)
    LogQxTwoTarget("[PTT-QX-2T-ALL]", qty, targets);
    new PttGlobalQuickExit().Execute(targets); // (2)
}
```

**After:**
```csharp
// B123/R9: OnInstrQAll2tClick -- fires global 2-target quick exit on all followers.
// B186 DW-B186-PANEL-CALLSITES-01: pass _instrument for instrument scope.
// CYC=2: (1)TryResolve2TargetContext false-path, (2)Execute call.
// JS-021: no lock. ASCII-only.
private void OnInstrQAll2tClick(object sender, RoutedEventArgs e)
{
    if (!TryResolve2TargetContext(out int qty, out var targets))
        return; // (1)
    LogQxTwoTarget("[PTT-QX-2T-ALL]", qty, targets);
    new PttGlobalQuickExit().Execute(targets, _instrument); // (2)
}
```

### No null guard at call site
Both Execute overloads gate on `instrument == null` in their combined gate. No null guard needed at the call site. This matches the B185 T1 pattern exactly (`PttBeSwapAll.Execute(buffer, _instrument)` — no null guard at call site).

### CYC impact on T3
- `OnQuickAllClick`: CYC=1, unchanged (no new branches)
- `OnInstrQAll2tClick`: CYC=2, unchanged (no new branches — just passing additional argument)

### Tests (T_B186_03, T_B186_06)
See Section 6.

---

## 6. Test Plan — 6 xUnit Tests (NT8-Runtime Skip Pattern)

All 6 tests use the NT8-runtime skip pattern established in T_B185_01/T_B185_02:
- Tests use reflection to inspect method signatures and method bodies
- Tests skip if NT8 runtime types are not present (avoids build failures outside NT8)
- No actual NT8 broker calls; structural/reflection tests only

### File
`src/PropTraderTools/CopyEngineTests.cs` — new methods added to existing test class

### T_B186_01 — Execute(Instrument) signature check

```
[Fact]
public void T_B186_01_ExecuteHasInstrumentParameter()
{
    // SKIP_IF_NT8_RUNTIME_ABSENT
    // Arrange: get PttGlobalQuickExit type via reflection
    // Act: get Execute methods; find overload with exactly 1 parameter of type Instrument
    // Assert: exactly one such overload exists
    // Satisfies: DW-B186-QX-ALL-SCOPE-01 AC1
}
```

### T_B186_02 — Execute(null) early return (structural)

```
[Fact]
public void T_B186_02_ExecuteNullInstrumentReturnsImmediately()
{
    // SKIP_IF_NT8_RUNTIME_ABSENT
    // Arrange: instantiate PttGlobalQuickExit via reflection
    // Act: invoke Execute(null) via reflection — should not throw
    // Assert: completes without exception (combined gate returns early on null instrument)
    // Satisfies: DW-B186-QX-ALL-SCOPE-01 AC2 — combined gate verified structurally
    // Note: QxGlobalExit flag check fires first; test validates no NRE on null instrument path
}
```

### T_B186_03 — OnQuickAllClick passes _instrument (reflection)

```
[Fact]
public void T_B186_03_OnQuickAllClickPassesInstrument()
{
    // SKIP_IF_NT8_RUNTIME_ABSENT
    // Arrange: get TradeCopierPanel type and OnQuickAllClick method body via reflection
    // Act: inspect IL or source reference for Execute call with _instrument argument
    // Assert: method body contains reference to _instrument (FieldInfo or IL ldarg check)
    // Satisfies: DW-B186-PANEL-CALLSITES-01 AC3
}
```

### T_B186_04 — Execute(List, Instrument) signature check

```
[Fact]
public void T_B186_04_ExecuteForcedTargetsHasInstrumentParameter()
{
    // SKIP_IF_NT8_RUNTIME_ABSENT
    // Arrange: get PttGlobalQuickExit type via reflection
    // Act: get Execute methods; find overload with exactly 2 parameters (List + Instrument)
    // Assert: exactly one such overload exists
    // Satisfies: DW-B186-QX-ALL-SCOPE-02 AC2
}
```

### T_B186_05 — Execute(forcedTargets, null) early return (structural)

```
[Fact]
public void T_B186_05_ExecuteForcedTargetsNullInstrumentReturnsImmediately()
{
    // SKIP_IF_NT8_RUNTIME_ABSENT
    // Arrange: instantiate PttGlobalQuickExit via reflection; build dummy target list
    // Act: invoke Execute(targets, null) via reflection — should not throw
    // Assert: completes without exception (combined gate returns early on null instrument)
    // Satisfies: DW-B186-QX-ALL-SCOPE-02 — combined gate verified structurally
}
```

### T_B186_06 — OnInstrQAll2tClick passes _instrument (reflection)

```
[Fact]
public void T_B186_06_OnInstrQAll2tClickPassesInstrument()
{
    // SKIP_IF_NT8_RUNTIME_ABSENT
    // Arrange: get TradeCopierPanel type and OnInstrQAll2tClick method body via reflection
    // Act: inspect for Execute call with _instrument as second argument
    // Assert: method body contains reference to _instrument
    // Satisfies: DW-B186-PANEL-CALLSITES-01 AC4
}
```

---

## 7. DNA Constraints (ALL Tickets)

| Rule | Constraint | B186 Status |
|------|-----------|-------------|
| JS-021 | No `lock()` anywhere | PASS — no lock in any changed method |
| JS-001 | No `throw new` in hot paths | PASS — no throw statements added |
| JS-002 | No `return null` | PASS — methods are void; early returns are `return;` |
| JS-033 | No `async void` | PASS — all methods synchronous void |
| ASCII | ASCII-only string literals | PASS — `"[PTT-QX-ALL] GlobalQuickExit fired instr="` is ASCII |
| CYC | CYC ≤ 8 per method | PASS — Execute(Instrument) = 8, Execute(List, Instrument) = 8 |
| NT8 | `DateTime.UtcNow` (no `DateTime.Now`) | PASS — no new DateTime usage added |

---

## 8. NT8 API Surface (Grounded in Source Code)

All APIs used in B186 changes are confirmed in existing `PttGlobalQuickExit.cs` source:

| API | Confirmed Usage | Line Reference |
|-----|----------------|----------------|
| `Instrument` (NinjaTrader.Cbi) | `ResolveQuickTicks(Instrument instr)` | Line 306 |
| `instrument.FullName` | `o.Instrument.FullName == instr.FullName` | Line 468 |
| `pos.Instrument?.FullName` | `_p.Instrument.FullName == pos.Instrument.FullName` | Line 226-227 |
| `Account.All` | `foreach (Account acc in Account.All)` | Lines 51, 140 |
| `NinjaTrader.Code.Output.Process` | Used throughout for logging | Lines 40-49, 67-79, etc. |
| `NinjaTrader.NinjaScript.PrintTo.OutputTab1` | All log calls | Throughout |

**Key NT8 Architectural Facts (mandatory per role)**:
- `AtmStrategyChangeStopTarget()` — StrategyBase-only. NOT used here (correct — B186 does not modify bracket strategies).
- `AtmStrategyCreate()` — StrategyBase-only. NOT used here (correct).
- `Account.Change()` — AddOnBase available but silent no-op on ATM-owned brackets. NOT used here.
- `Account.Cancel()` + `Account.CreateOrder()` — used in `PttQuickExit` (delegated to by ExecuteOne), NOT changed in B186.

---

## 9. Threading Model

| Aspect | Analysis |
|--------|----------|
| Call origin | Both Execute() overloads called from WPF click handlers (UI thread) |
| `_instrument` access | Read-only field access on UI thread — no race condition |
| `Account.All` access | NT8-021: safe after Loaded event, called from UI thread ✅ |
| Dispatcher.InvokeAsync | NOT required — already on UI thread |
| lock() | BANNED (JS-021) — not used, not needed |
| ConcurrentQueue | NOT modified in T1/T2/T3 — `_qxCancelInProgress` etc. unchanged |

---

## 10. Data Flow Diagrams

### T1+T3: Quick ALL Button Flow
```
[User: Quick ALL click]
  -> OnQuickAllClick(sender, e)           [UI thread, TradeCopierPanel.cs ~L1614]
  -> gqx.Execute(_instrument)             [T3 change: pass panel's _instrument]
  -> combined gate: !QxGlobalExit || instrument==null -> return   [T1 change]
  -> log: "[PTT-QX-ALL] GlobalQuickExit fired instr=" + FullName [T1 change]
  -> foreach acc in Account.All
     -> skip follower accounts
     -> foreach pos in acc.Positions
        -> skip: pos==null || qty==0
        -> skip: pos.Instrument?.FullName != instrument.FullName  [T1 new filter, CYC->8]
        -> CancelPttBeOrders + WaitForPttBeCancelled               [UNCHANGED]
        -> SnapshotTargetOrders                                    [UNCHANGED]
        -> NeedsLeaderFallbackFlatten? -> acc.Flatten             [UNCHANGED]
        -> ExecuteOne                                              [UNCHANGED]
        -> ExecuteFollowers                                        [UNCHANGED]
```

### T2+T3: QAll2t Button Flow
```
[User: QAll2t click]
  -> OnInstrQAll2tClick(sender, e)         [UI thread, TradeCopierPanel.cs ~L1701]
  -> TryResolve2TargetContext -> targets
  -> gqx.Execute(targets, _instrument)     [T3 change: pass _instrument as 2nd param]
  -> combined gate: !QxGlobalExit || instrument==null -> return   [T2 change]
  -> forcedTargets null/empty guard                               [UNCHANGED]
  -> foreach acc in Account.All
     -> skip follower accounts
     -> foreach pos in acc.Positions
        -> skip: pos==null || qty==0 || pos.Instrument?.FullName!=FullName [T2 folded into branch 6]
        -> CancelPttBeOrders + WaitForPttBeCancelled               [UNCHANGED]
        -> NeedsLeaderFallbackFlatten? -> acc.Flatten             [UNCHANGED]
        -> ExecuteOne (with forcedTargets)                        [UNCHANGED]
        -> ExecuteFollowers                                        [UNCHANGED]
```

---

## 11. 7-Scan Checklist (SCAN-01..07) — All Tickets

```powershell
# SCAN-01: no new lock()
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "lock\("
# Expected: 0 matches

# SCAN-02: no new return null
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "return null"
# Expected: 0 matches

# SCAN-03: no new async void
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "async void "
# Expected: 0 matches

# SCAN-04: CYC manual count
# Execute(Instrument instrument): verify 8 branches (see Section 3 CYC analysis)
# Execute(List<...>, Instrument): verify 8 branches (see Section 4 CYC analysis)
# Expected: both == 8

# SCAN-05: no non-ASCII in new lines
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "[^\x00-\x7F]"
# Expected: 0 matches

# SCAN-06: no throw new
git diff HEAD~1 HEAD -- src/ | Select-String "^\+" | Select-String "throw new"
# Expected: 0 matches

# SCAN-07: build
dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 5
# Expected: 0 errors

# Additional AC checks:
Select-String -Path src/PropTraderTools/CopyEngineTests.cs -Pattern "T_B186_"
# Expected: 6 matches (T_B186_01..T_B186_06)

Select-String -Path src/PropTraderTools/Features/PttGlobalQuickExit.cs -Pattern "Execute.*Instrument"
# Expected: 2 matches (both Execute overloads with Instrument param)

Select-String -Path src/PropTraderTools/TradeCopierPanel.cs -Pattern "gqx\.Execute.*_instrument|Execute\(targets.*_instrument\)"
# Expected: 2 matches (OnQuickAllClick + OnInstrQAll2tClick)

# Guardian + ptt-sync
powershell -File .\scripts\verify_critical_symbols.ps1
powershell -File .\scripts\ptt-sync.ps1
# Expected: verify_critical_symbols exit 0; ptt-sync 19/0
```

---

## 12. Acceptance Criteria Traceability

| AC | Check | Plan Coverage |
|----|-------|--------------|
| AC1 | `PttGlobalQuickExit.Execute(Instrument)` present, CYC=8 | Section 3 — T1 |
| AC2 | `PttGlobalQuickExit.Execute(List, Instrument)` present, CYC=8 | Section 4 — T2 |
| AC3 | `OnQuickAllClick` passes `_instrument` | Section 5 — T3, Change 1 |
| AC4 | `OnInstrQAll2tClick` passes `_instrument` | Section 5 — T3, Change 2 |
| AC5 | DW-B185-QX-ALL-SCOPE-01 eliminated (wrong-instrument qty split gone) | T2 instrument filter closes this item |
| AC6 | dotnet build 0 errors; NT8 compile PASS; ptt-sync 19/0; 6 new tests | SCAN-07 + 6 test methods |
| DNA SCAN-01..06 | 0 new violations | Section 11 |
| Guardian | Exit 0 (or DELETION-AUDIT-only with authorised DW-cards) | Section 11 |

**Expected build tag**: `PTT-COPIER B186 | qx-all-instr-scope | {date}`

---

## 13. Deferred Items

### Deferred TO B186 (from B185) — CLOSED by this epic
| Item | Status |
|------|--------|
| DW-B185-QX-ALL-SCOPE-01 (QAll2t wrong-instrument qty split) | CLOSED by T2 |

### Deferred FROM B186 to B187+
| DW ID | Description | Priority | Target |
|-------|-------------|----------|--------|
| DW-B186-DIAG-CLEANUP-01 | Remove [DW-B115-DIAG] log blocks from PttGlobalQuickExit.Execute and ExecuteFollowers | P3 | B187+ |
| DW-B186-WARN-01 | CS0067 PendingBeArmed never raised warning (same as DW-B184-04 carried forward) | P3 | B187+ |

### Carried Forward from B185 (OPEN — NOT addressed by B186)
| DW ID | Description | Priority |
|-------|-------------|----------|
| DW-B185-BE-ALL-SCOPE-01 | Spec HTML update for B185 BE ALL scope change | P2 |
| DW-B183-05 | RULES_CATALOG.md truncated at JS-041 | P1 |
| DW-B183-03 | IsLaneEEligible CYC=8 frozen | P1 |
| DW-B183-01 | xUnit tests for Slate Gate methods | P2 |
| DW-B183-02 | xUnit test for FlatGuardLoop | P2 |
| DW-B184-04 | CS0067 PendingBeArmed warning | P3 |

---

## 14. Implementation Order

1. **T1 + T2** (single engineer pass on `PttGlobalQuickExit.cs`):
   - Modify `Execute()` → `Execute(Instrument instrument)` per Section 3
   - Modify `Execute(List)` → `Execute(List, Instrument)` per Section 4
   - Both changes in same file — one commit on PttGlobalQuickExit.cs
   
2. **T3** (`TradeCopierPanel.cs`):
   - Update `OnQuickAllClick` call site per Section 5, Change 1
   - Update `OnInstrQAll2tClick` call site per Section 5, Change 2
   - One commit on TradeCopierPanel.cs
   - Run `ptt-sync.ps1` after

3. **Tests** (`CopyEngineTests.cs`):
   - Add T_B186_01..T_B186_06 per Section 6
   - Can be committed together with T3 or as separate commit

4. **Post-implementation**:
   - Run SCAN-01..07 checklist (Section 11)
   - Run `verify_critical_symbols.ps1`
   - Run `ptt-sync.ps1`
   - NT8 SIM smoke test: Quick ALL button → verify only _instrument positions exit
   - NT8 SIM smoke test: QAll2t button → verify only _instrument positions exit

---

*Written by: ptt-architect (Phase 1)*
*Epic: B186*
*Baseline: 0c27cd88 (B185-POST)*
*Next phase: ptt-plan-reviewer -> 02-plan-review.md (REVIEW_PASS gate)*
