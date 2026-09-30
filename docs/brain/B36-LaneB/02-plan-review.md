# B36-LaneB Plan Review
# Epic: DW-B35-TARGETS-01 | be-targets-oco
# Reviewer: ptt-plan-reviewer (Phase 2)
# Date: 2026-07-27
# Result: REVIEW_PASS

---

## Files Reviewed

| File | Purpose |
|------|---------|
| `docs/brain/B36-LaneB/02-architecture-plan.md` | Plan under review |
| `src/PropTraderTools/Features/PttBreakEven.cs` | Source baseline (pre-change state) |
| `docs/standards/jane-street/RULES_CATALOG.md` | Rules authority |

---

## Check 1 — Spec Change Coverage (C1–C5)

| Change ID | Spec Requirement | Plan Section | Addressed? |
|-----------|-----------------|--------------|-----------|
| C1 | NEW `SnapshotTargetsLocal(Account, Instrument)` → `List<(double, int, OrderAction)>`, Working/Accepted only, `IsAtmTargetName` filter, NT8-006, CYC≤3 | §C1 | ✅ YES |
| C2 | NEW `IsAtmTargetName(string)` → bool, true for Target1..Target9 only, CYC≤3 | §C2 | ✅ YES |
| C3 | NEW `SubmitBeTargetsLocal(Account, Instrument, string ocoId, List<...>)`, Limit, arg6=limitPrice, arg7=0, arg8=ocoId, arg9="PTT-BE-Target-{i+1}", non-fatal try/catch, CYC≤4 | §C3 | ✅ YES |
| C4 | MODIFY `Execute()` foreach — snapshot BEFORE cancel, ocoId built, `SubmitBeTargetsLocal` AFTER stop. Execute() CYC stays ≤8 | §C4, §5, §6 | ✅ YES |
| C5 | MODIFY `SubmitBeStopLocal` — add ocoId param, arg8=ocoId (replace string.Empty) | §C5 | ✅ YES |

**Result: 5/5 spec changes addressed. PASS.**

---

## Check 2 — Test Coverage (T1–T4)

| Test ID | Spec Requirement | Plan Section | Addressed? |
|---------|-----------------|--------------|-----------|
| T1 | Reflection test: `SnapshotTargetsLocal` exists, correct signature `(Account, Instrument)`, returns `List<ValueTuple<double,int,OrderAction>>` | §T1 | ✅ YES |
| T2 | Reflection invoke: `IsAtmTargetName` correct for Target1=true, Target9=true, Stop1=false, Target0=false, PTT-BE-Target-1=false | §T2 | ✅ YES |
| T3 | Reflection test: `SubmitBeTargetsLocal` exists, returns void, 4 params `(Account, Instrument, string, List<...>)` | §T3 | ✅ YES |
| T4 | Pure-arithmetic ocoId formula: starts "PTT-BE-", 4-char prefix, "-", integer ticks | §T4 | ✅ YES |

**Note on T2/C2 reconciliation**: The spec says "Target1..Target9 only" (implying Target0=false), but the literal CopyEngine pattern `char.IsDigit(name[6])` returns true for Target0. The plan correctly identifies this conflict (§C2 note), resolves it with the `name[6] != '0'` guard, and provides the final implementation. T2 will pass. ✅

**Note on T4**: T4 inlines the formula rather than calling `BuildBeOcoId` directly (since `BuildBeOcoId` is a new private static with no reflection test). This correctly validates the arithmetic without requiring NT8 runtime. The arithmetic `((int)(4400.50 / 0.25))` = `((int)(17602.0))` = `17602` → `"PTT-BE-ACCT-17602"`. Verified correct. ✅

**Result: 4/4 tests addressed. PASS.**

---

## Check 3 — Execute() CYC Analysis

**Source baseline** (verified from `PttBreakEven.cs` lines 54–106):

| # | Branch | Source |
|---|--------|--------|
| 1 | `if (!IsEnabled) return` | line 56 |
| 2 | `if (leaderPos == null \|\| leaderPos.Quantity == 0) return` | line 59 |
| 3 | `foreach (Account acc in ctx.AllAccounts)` | line 66 |
| 4 | `if (pos == null \|\| pos.Quantity == 0) continue` | line 69 |
| 5 | ternary `(isLong ? +buf : -buf)` in bePrice | line 73 |
| 6 | ternary `isLong ? (...) : (...)` in priceOk | lines 80–81 |
| 7 | `if (!priceOk)` | line 82 |
| 8 | ternary `(leaderIsLong ? +buf : -buf)` in leaderBePrice | line 102 |

**Baseline CYC = 8.** (McCabe: 8 branch points, no unreachable paths.)

**B36-LaneB additions to Execute()** (per plan §C4):
- `var targets = SnapshotTargetsLocal(...)` — method call, **+0** branches
- `string ocoId = BuildBeOcoId(...)` — method call via extracted helper, **+0** branches
- `SubmitBeTargetsLocal(...)` — method call, **+0** branches
- The ternary that builds the OCO ID prefix is **extracted to `BuildBeOcoId`** (plan §C4 mandatory path, confirmed in §6 CYC table)

**Net Execute() CYC = 8. PASS.**

**Critical engineer instruction** (confirmed in plan): The engineer MUST use `BuildBeOcoId` as the default implementation. If the ternary is inlined into Execute(), Execute() CYC rises to 9, violating the ≤8 limit. The plan explicitly flags this risk and resolves it. The review confirms this instruction is correct.

---

## Check 4 — NT8-006 (No LINQ)

**SnapshotTargetsLocal** (new method, C1):
- Plan specifies `foreach (Order o in acc.Orders)` — no `.ToList()`, no `.Where()`, no `.Select()`, no `.Any()`, no `.First()`.
- Justification confirmed by existing `CancelStaleBracketsLocal` precedent at line 124 of source: `foreach (Order o in acc.Orders)` — same pattern, proven compilable in NT8.
- SCAN-03 in §10 (7-scan checklist) specifically targets LINQ patterns with 0-result expectation. ✅

**No other new method uses LINQ.** PASS.

---

## Check 5 — NT8-049 (Limit order arg positions)

**SubmitBeTargetsLocal** (new method, C3):
```
arg6 = t.Price      ← limitPrice   (NT8-049: Limit uses arg6)
arg7 = 0            ← stopPrice=0  (NT8-049: Limit does not use stop price)
```
Plan §C3 pseudocode and §7 compliance table both confirm this explicitly. Matches the NT8 `CreateOrder` overload where StopMarket uses `arg6=0, arg7=stopPrice` and Limit uses `arg6=limitPrice, arg7=0`. PASS.

---

## Check 6 — NT8-007 (arg11 cast)

**SubmitBeTargetsLocal** (new method, C3):
```csharp
(NinjaTrader.Cbi.CustomOrder)null    // arg11: NOT a string (NT8-007)
```
Correctly cast, not a string literal. §7 NT8 compliance table confirms. PASS.

---

## Check 7 — NT8-013 (DateTime.MaxValue)

**SubmitBeTargetsLocal** (new method, C3):
```csharp
DateTime.MaxValue    // arg10: GTC (NT8-013)
```
SCAN-04 targets `DateTime.Now` with 0-result expectation. The new method uses `DateTime.MaxValue` exclusively. Existing `SubmitBeStopLocal` (source line 178) also uses `DateTime.MaxValue` — same pattern. PASS.

---

## Check 8 — Snapshot-before-Cancel Ordering

Plan §5 (Ordering Rationale) explicitly defines the mandatory sequence:

```
Step A: SnapshotTargetsLocal(acc, instr)        ← BEFORE CancelStaleBrackets
Step B: BuildBeOcoId(acc, bePrice, tickSize)    ← pure computation
Step C: CancelStaleBracketsLocal(acc, instr)    ← clears old ATM bracket
Step D: SubmitBeStopLocal(..., ocoId)
Step E: SubmitBeTargetsLocal(..., ocoId, tgts)
```

Rationale for A-before-C: "Targets must still be Working when read. After cancel (Step C), they are Cancelled — SnapshotTargetsLocal would return empty list." This is correct. The pre-existing source line 94 (`CancelStaleBracketsLocal`) will move to Step C; the snapshot insertion at Step A is placed BEFORE it. PASS.

---

## Check 9 — JS-021 (lock) Risk

**Scan of all new code in plan:**
- `SnapshotTargetsLocal`: no lock, no Monitor, no Mutex, no SemaphoreSlim. Iterates `acc.Orders` with plain foreach. ✅
- `IsAtmTargetName`: no lock, pure string computation. ✅
- `BuildBeOcoId`: no lock, pure arithmetic. ✅
- `SubmitBeTargetsLocal`: no lock, sequential for loop. ✅
- `SubmitBeStopLocal` modification: adds one parameter only, no structural changes to concurrency model. ✅
- `Execute()` modification: method calls only, no lock. ✅

SCAN-01 in §10 explicitly: `grep -n "lock(" PttBreakEven.cs` → 0 results. Plan file header (`// JS-021: no lock anywhere`) confirms design intent. **JS-021: PASS.**

---

## Check 10 — JS-033 (async void) Risk

All new methods are synchronous:
- `SnapshotTargetsLocal` — `private static List<...>` (sync return)
- `IsAtmTargetName` — `private static bool` (sync return)
- `BuildBeOcoId` — `private static string` (sync return)
- `SubmitBeTargetsLocal` — `private static void` (sync, non-async)

No `async` keyword appears in any new method. Plan §C3 explicitly states "synchronous void, no async". SCAN-02 in §10 explicitly: `grep -n "async void " PttBreakEven.cs` → 0 results. **JS-033: PASS.**

---

## Check 11 — JS-001 (throw in hot path)

No `throw new XxxException(...)` in any new method. The only exception handling is a bare `catch { }` (non-fatal swallow) in `SubmitBeTargetsLocal`, matching the existing pattern in `CancelStaleBracketsLocal` (source line 143). This is correct NT8 pattern: submission failures must not crash the module. **JS-001: PASS.**

---

## Check 12 — JS-002 (return null in new code)

| New Method | Return Behavior | Compliant? |
|-----------|----------------|-----------|
| `SnapshotTargetsLocal` | Returns empty `List<...>`, **never null** | ✅ YES |
| `IsAtmTargetName` | Returns `bool` | ✅ YES |
| `BuildBeOcoId` | Returns `string` (non-null via concatenation) | ✅ YES |
| `SubmitBeTargetsLocal` | Returns `void` | ✅ YES |

**Pre-existing `FindPositionLocal` `return null` (source lines 205, 209)**: NOT introduced by B36-LaneB. Correctly identified in plan SCAN-05 note as exempt pre-existing pattern. **JS-002: PASS (new code only).**

---

## CYC Summary (Complete)

| Method | CYC | Limit | Status |
|--------|-----|-------|--------|
| `Execute()` (modified) | 8 | ≤8 | ✅ |
| `SnapshotTargetsLocal` (new) | 3 | ≤3 | ✅ |
| `IsAtmTargetName` (new) | 2 | ≤3 | ✅ |
| `BuildBeOcoId` (new helper) | 2 | ≤3 | ✅ |
| `SubmitBeTargetsLocal` (new) | 4 | ≤4 | ✅ |
| `SubmitBeStopLocal` (modified) | 3 | ≤3 | ✅ |

---

## Violations Found

**None.**

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | lock() anywhere in new code | ✅ 0 instances |
| JS-033 | async void in new code | ✅ 0 instances |
| JS-001 | throw new XxxException in hot path | ✅ 0 instances |
| JS-002 | return null in NEW methods | ✅ 0 instances |
| NT8-006 | LINQ usage in SnapshotTargetsLocal | ✅ foreach only |
| NT8-049 | Limit arg6=limitPrice, arg7=0 | ✅ confirmed |
| NT8-007 | arg11 cast (not string) | ✅ confirmed |
| NT8-013 | DateTime.MaxValue (not .Now) | ✅ confirmed |
| CYC | Any method > its stated limit | ✅ all within limits |
| C1–C5 | Any spec change not addressed | ✅ all 5 addressed |
| T1–T4 | Any test not addressed | ✅ all 4 addressed |
| Ordering | Snapshot after cancel | ✅ A before C confirmed |

---

## Spec Coverage Matrix

| Requirement | Addressed in Plan | Section |
|------------|-------------------|---------|
| C1: SnapshotTargetsLocal signature + NT8-006 + CYC≤3 | ✅ | §C1 |
| C2: IsAtmTargetName Target1..9, Target0=false, CYC≤3 | ✅ | §C2 |
| C3: SubmitBeTargetsLocal Limit/ocoId/non-fatal/NT8-049/007/013/014, CYC≤4 | ✅ | §C3 |
| C4: Execute() snapshot+ocoId+targets, ordering correct, CYC stays ≤8 | ✅ | §C4, §5, §6 |
| C5: SubmitBeStopLocal ocoId param, string.Empty replaced | ✅ | §C5 |
| T1: SnapshotTargetsLocal reflection test | ✅ | §T1 |
| T2: IsAtmTargetName functional test (5 cases) | ✅ | §T2 |
| T3: SubmitBeTargetsLocal reflection test | ✅ | §T3 |
| T4: ocoId arithmetic test | ✅ | §T4 |
| Single-file scope (PttBreakEven.cs only) | ✅ | §9 |
| Hard-link gate (verify_links.ps1 -Fix) | ✅ | §8 |
| 7-scan checklist | ✅ | §10 |

---

## Engineer Notes (Mandatory — Not Optional)

These are binding constraints, not suggestions. The engineer **must**:

1. **Use `BuildBeOcoId` helper** (not inline ternary). Inlining makes Execute() CYC=9 — immediate re-review required.

2. **Maintain snapshot-before-cancel ordering**. The current source line 94 is `CancelStaleBracketsLocal(acc, ctx.Instrument)`. The snapshot insertion at `var targets = SnapshotTargetsLocal(...)` must appear BEFORE this line, not after.

3. **Use `name[6] != '0'` guard** in `IsAtmTargetName`. Without it, T2 (Target0=false) fails.

4. **`SubmitBeTargetsLocal` try/catch is per-order** (inside the for loop), not around the whole method. The plan pseudocode at §C3 confirms this. A single try/catch wrapping the whole loop would swallow all errors silently.

5. **SCAN-05 exemption scope**: `FindPositionLocal`'s `return null` is pre-existing and exempt. Do NOT introduce any new `return null` in B36-LaneB code.

---

## Conclusion

**REVIEW_PASS**

All 5 spec changes (C1–C5) are addressed. All 4 tests (T1–T4) are addressed. No JS-rule violations (JS-001, JS-002, JS-021, JS-033) exist in any new code. All NT8 rules (NT8-006, NT8-007, NT8-013, NT8-049) are correctly specified. CYC limits respected for all methods. Snapshot-before-cancel ordering is correctly mandated in §5. The plan is ready for Phase 3 (ticket generation).

**Phase gate**: REVIEW_PASS → Phase 3 (ptt-architect writes 04-tickets.md) is unlocked.
