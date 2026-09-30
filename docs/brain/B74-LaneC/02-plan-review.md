# B74-LaneC Plan Review

**Phase**: 2 (Plan Review)
**Reviewer**: ptt-plan-reviewer
**Plan reviewed**: `docs/brain/B74-LaneC/02-architecture-plan.md`
**Pipeline mode**: Retrospective
**Sources read**:
1. `docs/brain/B74-LaneC/02-architecture-plan.md`
2. `docs/standards/jane-street/RULES_CATALOG.md`
3. `src/PropTraderTools/Features/PttGlobalBreakEven.cs`
4. `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
5. `src/PropTraderTools/Features/PttQuickExit.cs`
6. grep: `RaiseBeBufferChanged|GlobalBeBufferChanged|_globalQuickAllT1|GlobalQuickAllT1|IncrementQuickAll|DecrementQuickAll|GlobalQuickAllBufferChanged` in `src/PropTraderTools/CopyEngine.cs`

---

## Section A: Completeness

### A1 — All 5 hotfixes described with Problem / Fix / Compliance

| Hotfix | Problem | Fix | Compliance block |
|--------|---------|-----|-----------------|
| B74-C-01 (HOTFIX-BEALL-BUFFER-SYNC-01) | ✅ | ✅ | ✅ |
| B74-C-02 (HOTFIX-CS0070-BEBUFFER-01) | ✅ | ✅ | ✅ |
| B74-C-03 (HOTFIX-QUICKALL-SINGLETON-01) | ✅ | ✅ | ✅ |
| B74-C-04 (HOTFIX-QUICK-T3-01) | ✅ | ✅ | ✅ |
| B74-C-05 (HOTFIX-SNAPSHOT-STOP-INSTRREF) | ✅ | ✅ | ✅ |

**Result**: PASS

---

### A2 — All 4 architecture themes present

| Theme | Present |
|-------|---------|
| Theme 1: CS0070 Relay Pattern | ✅ Section 3 |
| Theme 2: N-Bracket Quick Exit Design | ✅ Section 3 |
| Theme 3: GlobalQuickAllT1 Singleton | ✅ Section 3 |
| Theme 4: Instrument FullName Equality | ✅ Section 3 |

**Result**: PASS

---

### A3 — All required test IDs present

| Group | Required IDs | Present |
|-------|-------------|---------|
| T_BE_BUF_RELAY | 01, 02, 03 | ✅ Section 5 |
| T_QA_EXEC | 01, 02, 03 | ✅ Section 5 |
| T_QX_T3 | 01, 02, 03, 04, 05, 06, 07, 08, 09 | ✅ Section 5 |
| T_SNAP_STOP | 01, 02, 03, 04 | ✅ Section 5 |

Total test IDs: 19 (minimum required: 16). **Result**: PASS

---

### A4 — Method signatures in plan match actual source

Evidence from direct source reads:

| Method | Plan claim | Source confirmation |
|--------|-----------|-------------------|
| `PttGlobalBreakEven.IncrementBuffer()` CYC=2 | Section 2 B74-C-01 | `PttGlobalBreakEven.cs` lines 90–93 ✅ |
| `PttGlobalBreakEven.DecrementBuffer()` CYC=2 | Section 2 B74-C-01 | `PttGlobalBreakEven.cs` lines 96–99 ✅ |
| `CopyEngine.GlobalBeBufferChanged` event | Section 2 B74-C-02 | `CopyEngine.cs` line 184 ✅ |
| `CopyEngine.RaiseBeBufferChanged(int)` CYC=1 | Section 2 B74-C-02 | `CopyEngine.cs` lines 186–188 ✅ |
| `CopyEngine._globalQuickAllT1 = 4` volatile int | Section 2 B74-C-03 | `CopyEngine.cs` line 191 ✅ |
| `CopyEngine.GlobalQuickAllT1` property | Section 2 B74-C-03 | `CopyEngine.cs` line 192 ✅ |
| `CopyEngine.IncrementQuickAll()` CYC=2 | Section 2 B74-C-03 | `CopyEngine.cs` lines 193–198 ✅ |
| `CopyEngine.DecrementQuickAll()` CYC=2 | Section 2 B74-C-03 | `CopyEngine.cs` lines 200–205 ✅ |
| `CopyEngine.GlobalQuickAllBufferChanged` event | Section 2 B74-C-03 | `CopyEngine.cs` line 207 ✅ |
| `PttGlobalQuickExit.Execute()` CYC=8 | Section 4 | `PttGlobalQuickExit.cs` line 23 comment ✅ |
| `PttGlobalQuickExit.ResolveQuickTicks` CYC=2 | Section 4 | `PttGlobalQuickExit.cs` line 57 comment ✅ |
| `PttGlobalQuickExit.ExecuteOne` CYC=1 | Section 4 | `PttGlobalQuickExit.cs` line 67–68 comment ✅ |
| `PttGlobalQuickExit.SnapshotTargetOrders` CYC=4 | Section 4 | `PttGlobalQuickExit.cs` line 85 comment ✅ |
| `PttQuickExit.Execute` (primary) CYC=8 | Section 4 | `PttQuickExit.cs` line 29 comment ✅ |
| `PttQuickExit.Execute` (compat) CYC=1 | Section 4 | `PttQuickExit.cs` line 168 ✅ |
| `PttQuickExit.SnapshotStopPrice` CYC=2 | Section 4 | `PttQuickExit.cs` line 177 comment ✅ |

**Result**: PASS

---

## Section B: JS-DNA Compliance

### B1 — JS-021 no lock() confirmed absent in all 5 hotfixes

| Hotfix | Concurrency primitive | JS-021 |
|--------|----------------------|--------|
| B74-C-01 | `volatile int _globalBeBuffer` | PASS |
| B74-C-02 | `Dispatcher.InvokeAsync` | PASS |
| B74-C-03 | `volatile int _globalQuickAllT1` + `Dispatcher.InvokeAsync` | PASS |
| B74-C-04 | `Interlocked` via `CopyEngine.NextQxOcoId()` | PASS |
| B74-C-05 | No concurrency primitive needed (read-only scan) | PASS |

**Result**: PASS — no lock() usage anywhere in the 5 hotfixes

---

### B2 — JS-001 no throw new in hot paths

`PttQuickExit.Execute` (primary) contains `try/catch (Exception ex)` blocks. Exceptions are caught and passed to `Output.Process` (logging); they are never re-thrown. All other modified methods are synchronous and contain no throw statements. Confirmed in source.

**Result**: PASS

---

### B3 — JS-002 no return null

| Method | Return on miss | JS-002 |
|--------|----------------|--------|
| `SnapshotTargetOrders` | `new List<...>()` (empty list) | PASS |
| `SnapshotStopPrice` | `0.0` (double) | PASS |
| `ResolveQuickTicks` | tuple from `InstrumentDefaults.GetQuickTicks` | PASS |
| `Execute` methods | `void` | PASS |
| `IncrementBuffer` / `DecrementBuffer` | `void` | PASS |
| `RaiseBeBufferChanged` | `void` (expression body returns `DispatcherOperation`) | PASS |

**Result**: PASS

---

### B4 — JS-033 no async void

No method in any of the 5 hotfixes uses `async void`. All methods are synchronous `void`, expression-bodied returning `DispatcherOperation`, or `void` returning. Confirmed in source.

**Result**: PASS

---

### B5 — CYC ≤ 8 for all modified methods

| Method | Plan CYC claim | Source comment CYC | ≤8? |
|--------|---------------|-------------------|-----|
| `PttGlobalBreakEven.IncrementBuffer` | 2 | 2 | ✅ |
| `PttGlobalBreakEven.DecrementBuffer` | 2 | 2 | ✅ |
| `CopyEngine.RaiseBeBufferChanged` | 1 | 1 (expression body) | ✅ |
| `CopyEngine.IncrementQuickAll` | 2 | 2 | ✅ |
| `CopyEngine.DecrementQuickAll` | 2 | 2 | ✅ |
| `PttGlobalQuickExit.Execute` | 8 | 8 | ✅ |
| `PttGlobalQuickExit.ResolveQuickTicks` | 2 | 2 | ✅ |
| `PttGlobalQuickExit.ExecuteOne` | 1 | 1 | ✅ |
| `PttGlobalQuickExit.SnapshotTargetOrders` | 4 | 4 | ✅ |
| `PttQuickExit.Execute` (primary) | 8 | 8 | ✅ |
| `PttQuickExit.Execute` (compat) | 1 | 1 | ✅ |
| `PttQuickExit.SnapshotStopPrice` | 2 | 2 | ✅ |

**Result**: PASS — no method exceeds CYC 8

---

## Section C: NT8 API Correctness

### C1 — CS0070 relay pattern documented correctly

Plan correctly documents that events may only be raised (invoked via `?.Invoke(...)`) from inside the declaring class (`CopyEngine`). The relay pattern — a public/internal method on `CopyEngine` that invokes the event on behalf of external callers — is the correct fix for CS0070.

Source confirms: `RaiseBeBufferChanged` at `CopyEngine.cs` line 186 raises `GlobalBeBufferChanged` (line 184) entirely within `CopyEngine`. `IncrementQuickAll` and `DecrementQuickAll` at lines 193 and 200 raise `GlobalQuickAllBufferChanged` (line 207) entirely within `CopyEngine`.

The `Dispatcher.InvokeAsync` wrapper for WPF UI thread dispatch is correctly documented and correctly applied per JS-023 (UI update from off-thread must use `Dispatcher.InvokeAsync`).

**Result**: PASS

---

### C2 — FullName equality documented as NT8 cross-account Instrument pattern

Plan Theme 4 correctly describes the NT8 behavior: a separate `Instrument` object instance is created per account context, making C# reference equality (`!=`) silently wrong. The `FullName` string equality pattern is correctly identified as the fix.

Source `PttQuickExit.cs` line 183 confirms the exact fix is in place with null guards:
```csharp
if (o.Instrument == null || o.Instrument.FullName != instr?.FullName) continue;
```

**Result**: PASS

---

### C3 — CreateOrder + Submit pattern (not AtmStrategyCreate)

Plan Section 2 B74-C-04 explicitly references `Account.CreateOrder(...)` followed by `leader.Submit(new[] { ord })`. `AtmStrategyCreate` (StrategyBase-only, not available on AddOnBase) is not referenced anywhere in the plan.

Source `PttQuickExit.cs` lines 104–118 and 130–144 confirm `leader.CreateOrder(...)` + `leader.Submit(new[] { ... })` pattern used for all bracket submissions.

**Result**: PASS

---

## Section D: Retrospective Integrity

### D1 — Plan describes existing code only; no new logic proposed

Plan header explicitly states: *"This plan describes what is in source, not what should be built."* All described code snippets are verified to exist in source with matching logic. No future-tense proposals are made.

**Result**: PASS

---

### D2 — No contradictions between plan and source

All plan code snippets cross-checked against source:

| Plan snippet | Source location | Match |
|-------------|----------------|-------|
| `IncrementBuffer` relay call | `PttGlobalBreakEven.cs` lines 92–93 | ✅ exact |
| `RaiseBeBufferChanged` with `Dispatcher.InvokeAsync` | `CopyEngine.cs` lines 186–188 | ✅ exact |
| `_globalQuickAllT1 = 4` volatile int | `CopyEngine.cs` line 191 | ✅ exact |
| `GlobalQuickAllT1 => _globalQuickAllT1` | `CopyEngine.cs` line 192 | ✅ exact |
| `IncrementQuickAll` body | `CopyEngine.cs` lines 193–198 | ✅ exact |
| `ResolveQuickTicks` singleton read | `PttGlobalQuickExit.cs` lines 60–64 | ✅ exact |
| `SnapshotTargetOrders` three name patterns | `PttGlobalQuickExit.cs` lines 100–106 | ✅ exact |
| N-bracket for-loop with `targetCount` | `PttQuickExit.cs` lines 77–152 | ✅ exact |
| Compat overload delegation | `PttQuickExit.cs` lines 168–172 | ✅ exact |
| `SnapshotStopPrice` FullName fix | `PttQuickExit.cs` line 183 | ✅ exact |

**Note**: Plan B74-C-03 states `ResolveQuickTicks` computes and returns `t2 = t1 * 2` in the tuple. Source `PttGlobalQuickExit.cs` line 40 calls `ExecuteOne(acc, pos.Instrument, ticks.t1, targets)` using only `ticks.t1`. The `t2` value is computed in `ResolveQuickTicks` but not consumed in `Execute()`. This is architecturally consistent (the tuple return was inherited from prior blocks) and the plan accurately acknowledges it. No contradiction.

**Result**: PASS

---

## Section E: Test Coverage

### E1 — Each test ID maps to method + scenario + expected result

| Test ID | Method | Scenario described | Expected result described |
|---------|--------|--------------------|--------------------------|
| T_BE_BUF_RELAY_01 | `PttGlobalBreakEven.IncrementBuffer` | ✅ | ✅ |
| T_BE_BUF_RELAY_02 | `PttGlobalBreakEven.DecrementBuffer` | ✅ | ✅ |
| T_BE_BUF_RELAY_03 | `IncrementBuffer` / `DecrementBuffer` at bounds | ✅ | ✅ (relay called unconditional; buffer value unchanged) |
| T_QA_EXEC_01 | `CopyEngine.GlobalQuickAllT1` | ✅ | ✅ (default = 4) |
| T_QA_EXEC_02 | `CopyEngine.IncrementQuickAll` | ✅ | ✅ (value=5, event fires with 5) |
| T_QA_EXEC_03 | `CopyEngine.DecrementQuickAll` | ✅ | ✅ (value=3, event fires with 3) |
| T_QX_T3_01 | `PttQuickExit.Execute` primary | ✅ (3-element targets) | ✅ (3 OCO pairs) |
| T_QX_T3_02 | `PttQuickExit.Execute` primary | ✅ (empty targets) | ✅ (2 OCO pairs fallback) |
| T_QX_T3_03 | `PttQuickExit.Execute` primary | ✅ (tick spacing) | ✅ (T1=5001, T2=5002, T3=5003) |
| T_QX_T3_04 | `PttQuickExit.Execute` primary | ✅ (per-target qty) | ✅ (targets[i].Qty used) |
| T_QX_T3_05 | `PttQuickExit.Execute` primary | ✅ (empty targets qty fallback) | ✅ (max(1, qty/count)) |
| T_QX_T3_06 | `PttQuickExit.Execute` primary | ✅ (OCO IDs per pair) | ✅ (different IDs confirmed) |
| T_QX_T3_07 | `PttQuickExit.Execute` primary | ✅ (order names) | ✅ (PTT-QX-Stop, PTT-QX-Stop2, PTT-QX-T1..T3) |
| T_QX_T3_08 | `PttQuickExit.Execute` compat overload | ✅ (delegation) | ✅ (empty list, targetCount=2) |
| T_QX_T3_09 | `PttGlobalQuickExit.SnapshotTargetOrders` | ✅ (3 target types, 1 stop) | ✅ (3 entries, Stop1 excluded) |
| T_SNAP_STOP_01 | `PttQuickExit.SnapshotStopPrice` | ✅ (FullName match, different ref) | ✅ (stop price returned, not 0) |
| T_SNAP_STOP_02 | `PttQuickExit.SnapshotStopPrice` | ✅ (Working StopMarket) | ✅ (StopPrice returned) |
| T_SNAP_STOP_03 | `PttQuickExit.SnapshotStopPrice` | ✅ (Accepted StopMarket) | ✅ (StopPrice returned) |
| T_SNAP_STOP_04 | `PttQuickExit.SnapshotStopPrice` | ✅ (o.Instrument null) | ✅ (no NRE, returns 0.0) |

**Result**: PASS

---

### E2 — No test ID invents behavior absent from source

Verification of potentially subtle test expectations:

- **T_BE_BUF_RELAY_03**: Claims relay is called *even when buffer value unchanged* (at limit). Source confirms: `RaiseBeBufferChanged` call is **unconditional** after the clamp guard in both `IncrementBuffer` and `DecrementBuffer` (`PttGlobalBreakEven.cs` lines 90–99). The `if` guard only protects the `_globalBeBuffer++/--` mutation; the relay call is outside and always executes. ✅
- **T_QX_T3_09**: Claims `Stop1` (StopMarket) is excluded. Source `PttGlobalQuickExit.cs` line 99 confirms only `OrderType.Limit` orders are included. ✅
- **T_SNAP_STOP_01**: Verifies post-fix behavior returns stop price when `FullName` matches but object references differ. Source line 183 confirms FullName comparison is the filter. ✅

**Result**: PASS

---

## Overall Verdict

| Check | Result |
|-------|--------|
| A1 — All 5 hotfixes with Problem/Fix/Compliance | PASS |
| A2 — All 4 architecture themes present | PASS |
| A3 — All required test IDs present | PASS |
| A4 — Method signatures match source | PASS |
| B1 — JS-021 no lock() | PASS |
| B2 — JS-001 no throw in hot paths | PASS |
| B3 — JS-002 no return null | PASS |
| B4 — JS-033 no async void | PASS |
| B5 — CYC ≤ 8 all methods | PASS |
| C1 — NT8 CS0070 relay pattern correct | PASS |
| C2 — NT8 FullName equality pattern correct | PASS |
| C3 — CreateOrder+Submit (not AtmStrategyCreate) | PASS |
| D1 — Retrospective only, no new logic | PASS |
| D2 — No plan/source contradictions | PASS |
| E1 — Test IDs map to method/scenario/expected | PASS |
| E2 — No test invents absent behavior | PASS |

**Violations**: None

---

## REVIEW_PASS
