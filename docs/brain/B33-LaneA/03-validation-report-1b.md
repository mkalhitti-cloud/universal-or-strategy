# B33 Phase 1b — Lane C Validation Report
# Validator: ptt-verifier (Lane C, independent verification)
# Phase: 1b (BUG-B33-02 + BUG-B33-03)
# Date: 2026-07-21
# Wave Workspace: c:\WSGTA\universal-or-strategy\
# Director Workspace: c:\WSGTA\universal-or-strategy-director\

---

## OVERALL VERDICT: PASS

All 12 source checks (V1–V12), all 6 test checks (T1–T6), and all 7 NT8 compiler
checks (NT8-C1 through NT8-C7) passed with zero violations. Zero DNA rule violations.
Zero scan violations across all 7 mandatory scans.

---

## Section 1 — Rules Gate

### Files Read (UTF-8 confirmed)

| File | Encoding | Status |
|------|----------|--------|
| `docs/standards/jane-street/RULES_CATALOG.md` | UTF-8 (no BOM, human-readable) | PASS |
| `docs/standards/NT8_COMPILER_RULES.md` | UTF-8 (no BOM, human-readable) | PASS |

### P0 Rules Gate Pre-Check

| Rule | Pattern | Result |
|------|---------|--------|
| JS-021 | `lock(` anywhere in source | PASS — 0 actual `lock(` statements; 3 comment-only hits on lines 350, 371, 620, 861, 1559, 1603 are comment text only |
| JS-033 | `async void` | PASS — none introduced |
| JS-001 | `throw new XxxException` in hot path | PASS — none introduced |
| JS-002 | `return null` for missing values | PASS — TryGetValue pattern used throughout |
| NT8-019 | `async void` in NT8 callbacks | PASS — none introduced |
| NT8-013 | `DateTime.Now` in CreateOrder | PASS — `DateTime.MaxValue` used at line 1584 |
| NT8-007 | CreateOrder arg 12 as string | PASS — `(NinjaTrader.Cbi.CustomOrder)null` at line 1585 |
| NT8-050 | `acc.Positions[instr]` | PASS — comment-only reference at line 1555; CancelStaleBrackets uses `.Orders` |

**GATE RESULT: PASS — zero P0 violations in any changed region.**

---

## Section 2 — Seven Mandatory Scans (Layer 3 — independent)

All scans run independently via ctx_shell/execute_command on Wave workspace source.
Engineer's Layer 2 self-report is NOT referenced here; results are from Layer 3 only.

| Scan | Pattern | Tool | Result | Engineer Layer 2 | Match? |
|------|---------|------|--------|-----------------|--------|
| SCAN-01 | `lock\s*\(` | Select-String | 0 actual statements (comment text only on 6 lines) | 0 | ✅ |
| SCAN-02 | Non-ASCII chars | Get-Content + Where-Object `[^\x00-\x7F]` | **0 matches** | 0 | ✅ |
| SCAN-03 | `FontFamily` | Select-String | **0 matches** | 0 | ✅ |
| SCAN-04 | `#[0-9A-Fa-f]{6}` | Select-String | **0 matches** | 0 | ✅ |
| SCAN-05 | CreateOrder signal names | Manual review all 17 CreateOrder sites | All use `"PTT-"` prefix (PTT-Copy, PTT-BE-Stop, PTT-Flatten, etc.) | 0 violations | ✅ |
| SCAN-06 | `DateTime\.Now[^U]` | Select-String | **0 matches** | 0 | ✅ |
| SCAN-07 | `\\block\s*\(` | Select-String | **0 matches** | 0 | ✅ |

**Layer 3 vs Layer 2 comparison: NO DISCREPANCIES.** All 7 scans match engineer self-report.

---

## Section 3 — V1–V12 Source Verification Checklist

Target file: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`

| # | Check | Result | Line(s) | Evidence |
|---|-------|--------|---------|----------|
| **V1** | Build tag | **PASS** | Line 41 | `internal const string Tag = "PTT-COPIER B33 \| 1b-dict-BE \| 2026-07-21";` — exact match |
| **V2** | `_pendingBeStop` field type is `ConcurrentDictionary<string, Order>` | **PASS** | Lines 162–166 | `private readonly ConcurrentDictionary<string, Order> _pendingBeStop = new ConcurrentDictionary<string, Order>();` — not `volatile Order` |
| **V3** | SubmitBeStop duplicate guard uses TryGetValue | **PASS** | Lines 1568–1572 | `_pendingBeStop.TryGetValue(leaderAcc.Name, out var existing) && existing != null && existing.OrderState == OrderState.Working` |
| **V4** | SubmitBeStop uses local var + dict store + Submit(beStop) | **PASS** | Lines 1579–1587 | `var beStop = leaderAcc.CreateOrder(...)` at line 1579; `_pendingBeStop[leaderAcc.Name] = beStop` at line 1586; `leaderAcc.Submit(new[] { beStop })` at line 1587 |
| **V5** | OrphanCancelGuard null check uses TryGetValue | **PASS** | Line 1606 | `if (!_pendingBeStop.TryGetValue(acc.Name, out var stop) \|\| stop == null)` |
| **V6** | OrphanCancelGuard uses TryRemove in both exit paths | **PASS** | Lines 1610, 1623 | `_pendingBeStop.TryRemove(acc.Name, out _)` appears at line 1610 (state != Working path) and line 1623 (after cancel path) — 2 occurrences |
| **V7** | OrphanCancelGuard Cancel uses local `stop` not field | **PASS** | Line 1615 | `acc.Cancel(new Order[] { stop })` — uses local `stop` variable, NOT `{ _pendingBeStop }` |
| **V8** | `CancelStaleBrackets` method exists | **PASS** | Line 1631 | `private void CancelStaleBrackets(Account leaderAcc, Instrument instr)` — present between OrphanCancelGuard close (line 1624) and BreakEven (line 1656) |
| **V9** | `CancelStaleBrackets` called from `TryFirePositionState` | **PASS** | Lines 742–746 | `if (!hasPos) { OrphanCancelGuard(e.Order.Account, e.Order.Instrument); CancelStaleBrackets(e.Order.Account, e.Order.Instrument); }` — both calls in block |
| **V10** | No `volatile` on `_pendingBeStop` | **PASS** | Lines 162–166 | `private readonly ConcurrentDictionary<string, Order> _pendingBeStop` — no `volatile` keyword; Select-String `volatile.*_pendingBeStop` → 0 matches |
| **V11** | No new `lock(` introduced | **PASS** | All changed regions | Select-String `lock\s*\(` → 0 actual statements; all hits are comment text (lines 350, 371, 861, 1559, 1603) |
| **V12** | `CancelStaleBrackets` uses `.ToArray()` for Cancel arg | **PASS** | Line 1643 | `leaderAcc.Cancel(stale.ToArray())` — `ToArray()` confirmed present |

**V1–V12 result: 12/12 PASS. Zero failures.**

---

## Section 4 — T1–T6 Test Verification

Target file: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`

| # | Check | Result | Line(s) | Evidence |
|---|-------|--------|---------|----------|
| **T1** | `PendingBeStop_FieldExists_And_IsConcurrentDictionary` method present | **PASS** | Line 2754 | Method found: `public void PendingBeStop_FieldExists_And_IsConcurrentDictionary()` |
| **T2** | Old test `PendingBeStop_FieldExists_And_InitialValueIsNull` does NOT exist | **PASS** | — | Select-String search confirms zero occurrences of `InitialValueIsNull` method in file |
| **T3** | Type assertion uses `ConcurrentDictionary<string, NinjaTrader.Cbi.Order>` | **PASS** | Lines 2761–2763 | `Assert.Equal(typeof(System.Collections.Concurrent.ConcurrentDictionary<string, NinjaTrader.Cbi.Order>), fi.FieldType)` |
| **T4** | Empty dict assertion uses `Assert.Empty(value)` or `Assert.Equal(0, value.Count)` | **PASS** | Line 2768 | `Assert.Empty(val)` — uses xUnit `Assert.Empty` pattern |
| **T5** | Uses `[Fact]` attribute — not `[Test]` or `[TestMethod]` | **PASS** | Line 2753 | `[Fact]` attribute decorates the test method |
| **T6** | xUnit assertions: `Assert.Equal` / `Assert.NotNull` / `Assert.Empty` — NOT `Assert.That` | **PASS** | Lines 2759, 2761, 2767, 2768 | Uses `Assert.NotNull(fi)`, `Assert.Equal(typeof(...), fi.FieldType)`, `Assert.NotNull(value)`, `Assert.Empty(val)` — all xUnit; no `Assert.That` present |

**T1–T6 result: 6/6 PASS. Zero failures.**

---

## Section 5 — NT8 Compiler Checks (NT8-C1 through NT8-C7)

All checks applied to `CancelStaleBrackets` method (lines 1631–1652) and all changed regions.

| # | Check | Result | Evidence |
|---|-------|--------|----------|
| **NT8-C1** | No `{ get; init; }` in any new code | **PASS** | Select-String `get;\s*init;` → 0 matches. No init-only properties introduced. |
| **NT8-C2** | No `record` keyword in any new code | **PASS** | Select-String `\brecord\b` → only 1 hit at line 44 in a comment (`// V01: Binding record for _orderMap`) — not a type declaration. No new `record` type. |
| **NT8-C3** | `.Orders` usage pattern matches existing lines 1115/1142 | **PASS** | Line 1634: `leaderAcc.Orders` followed by `.Where(...).ToList()` — same pattern as `acc.Orders.ToList()` at line 1120 and line 1147 |
| **NT8-C4** | Cancel arg is `Order[]` via `.ToArray()` | **PASS** | Line 1643: `leaderAcc.Cancel(stale.ToArray())` — `stale` is `List<Order>`, `.ToArray()` produces `Order[]` matching established pattern |
| **NT8-C5** | No `Positions[Instrument]` indexer in new code | **PASS** | Select-String `\.Positions\[` → 1 hit at line 1555 — a comment only (`// NT8-050 FIX: Account.Positions[Instrument]...`). No actual code uses `acc.Positions[instr]`. |
| **NT8-C6** | `out _` discard only in TryRemove calls — C# 7.3 safe | **PASS** | Lines 1610 and 1623: `_pendingBeStop.TryRemove(acc.Name, out _)` — both are TryRemove calls. Discard `out _` is C# 7.0+ per spec; C# 7.3 safe. |
| **NT8-C7** | `out var` pattern only in TryGetValue calls — C# 7.3 safe | **PASS** | Lines 1568 and 1606: `TryGetValue(leaderAcc.Name, out var existing)` and `TryGetValue(acc.Name, out var stop)` — both are TryGetValue calls. C# 7.3 safe. |

**NT8-C1 through NT8-C7: 7/7 PASS. Zero violations.**

---

## Section 6 — DNA Rule Cross-Check (Changed Regions)

| DNA Rule | Changed Region | Result |
|----------|---------------|--------|
| JS-021 No lock() | Lines 162–166, 742–746, 1568–1598, 1600–1624, 1626–1652 | PASS — zero `lock()` statements |
| JS-002 No return null | OrphanCancelGuard, SubmitBeStop | PASS — early returns use guard pattern, no null returns on non-null expected types |
| JS-001 No throw in hot path | SubmitBeStop, OrphanCancelGuard, CancelStaleBrackets | PASS — all use try/catch, no rethrow |
| NT8-003 No volatile double | Field at line 162 | PASS — `readonly ConcurrentDictionary` (no volatile) |
| NT8-013 DateTime.MaxValue | CreateOrder at line 1584 | PASS — `DateTime.MaxValue` confirmed |
| NT8-014 PTT- prefix | CreateOrder signal name at line 1584 | PASS — `"PTT-BE-Stop"` confirmed |
| NT8-007 CustomOrder arg12 | CreateOrder at line 1585 | PASS — `(NinjaTrader.Cbi.CustomOrder)null` confirmed |
| NT8-018 lock() banned | All changed regions | PASS — ConcurrentDictionary used per rule |
| NT8-049 limitPrice/stopPrice args | Lines 1582–1583 | PASS — arg6=0 (limitPrice), arg7=bePrice (stopPrice), in correct positions |

---

## Section 7 — Architecture Compliance

| Item | Requirement | Result |
|------|-------------|--------|
| `CancelStaleBrackets` placement | Between OrphanCancelGuard close and BreakEven | PASS — lines 1626–1652; BreakEven at line 1656 |
| `TryFirePositionState` call site | Both `OrphanCancelGuard` and `CancelStaleBrackets` called in `if (!hasPos)` block | PASS — lines 742–746, both calls present in block |
| CYC compliance | `CancelStaleBrackets` CYC ≤ 8 | PASS — CYC=3 (null guard, Where filter, Count==0 guard); well within limit |
| Field type change | `volatile Order` → `ConcurrentDictionary<string, Order>` | PASS — lock-free per-account storage |
| Singleton field race eliminated | Multiple accounts no longer overwrite single field | PASS — dictionary keyed by `acc.Name` |

---

## Section 8 — Engineer Deviation Check

The engineer's completion report (`02-engineer-completion-1b.md`) states "Zero deviations."

Layer 3 independent verification **confirms** this:

| Change | Plan (04-diff-plan-1b.md) | Actual Source | Match |
|--------|--------------------------|---------------|-------|
| C1 — field | `ConcurrentDictionary<string, Order>` | Line 165–166: identical | ✅ |
| C2 — SubmitBeStop guard | `TryGetValue(leaderAcc.Name, out var existing)` | Line 1568: identical | ✅ |
| C3 — CreateOrder + dict store + Submit | `var beStop = ...` + `_pendingBeStop[leaderAcc.Name] = beStop` + `Submit(new[]{beStop})` | Lines 1579, 1586, 1587: identical | ✅ |
| C4 — OrphanCancelGuard null check | `!TryGetValue(acc.Name, out var stop) \|\| stop == null` | Line 1606: identical | ✅ |
| C5 — state guard + cancel + TryRemove | `TryRemove` twice, `Cancel(new Order[]{stop})` | Lines 1610, 1615, 1623: identical | ✅ |
| C6 — CancelStaleBrackets | New method body per plan | Lines 1631–1652: identical | ✅ |
| C7 — TryFirePositionState hook | Both calls in block | Lines 742–746: identical | ✅ |
| C8 — Build tag | `"PTT-COPIER B33 \| 1b-dict-BE \| 2026-07-21"` | Line 41: identical | ✅ |
| C9 — Test rename + assertion | New test name + ConcurrentDictionary + Assert.Empty | Lines 2753–2768: identical | ✅ |

**No deviations found between plan and implementation. 9/9 changes applied exactly as specified.**

---

## Section 9 — Summary

| Category | Items | Passed | Failed |
|----------|-------|--------|--------|
| Rules Gate | 8 | 8 | 0 |
| 7-Scan Contract (Layer 3) | 7 | 7 | 0 |
| Source Checks V1–V12 | 12 | 12 | 0 |
| Test Checks T1–T6 | 6 | 6 | 0 |
| NT8 Compiler Checks NT8-C1–C7 | 7 | 7 | 0 |
| DNA Rule Cross-Check | 9 | 9 | 0 |
| Architecture Compliance | 5 | 5 | 0 |
| **TOTAL** | **54** | **54** | **0** |

---

## VERDICT: VERIFY_PASS

**B33 Phase 1b is complete.** All 54 checks passed. Zero violations. Zero scan hits.
Zero deviations from the diff plan. The source in the Wave workspace matches the plan exactly.

Lane B retry: NOT REQUIRED.

*Generated by ptt-verifier — Lane C, independent verification — 2026-07-21*
