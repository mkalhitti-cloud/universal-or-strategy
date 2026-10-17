# B194 Ticket 2 -- Verification Report (Cycle 2)

**Epic**: B194 -- Fix 4 confirmed NakedPositionDetector + DrainThenDispatch bugs
**Ticket**: T2 -- F2 (HasNakedPosition TriggerPending Fix + IsBracketStopOrder Helper)
**Verifier**: ptt-verifier (Phase 4b)
**Cycle**: 2 of 3 (Cycle 1 VERIFY_FAIL resolved)
**Date**: 2026-10-07
**Verdict**: VERIFY_PASS

---

## Cycle 1 VERIFY_FAIL Resolution

**Cycle 1 failure reason**: Test `IsBracketStopOrder_ReturnsFalse_ForWorkingLimitOrder` (spec
section 7, test #4 anti-regression guard) was absent from B194Tests.cs. The engineer had
substituted `IsBracketStopOrder_TriggerPendingMarket_NoStopPrice_ReturnsFalse` which, while
valid, was not the spec-required regression guard.

**Cycle 2 change**: Tests-only. `IsBracketStopOrder_ReturnsFalse_ForWorkingLimitOrder` added
at line 178 of B194Tests.cs. CopyEngine.cs was NOT modified.

**Cycle 1 failure resolved**: CONFIRMED -- test present at line 178 (scan 07 below).

---

## PRIMARY CHECK: Cycle 1 Failure

| Check | Result |
|-------|--------|
| `IsBracketStopOrder_ReturnsFalse_ForWorkingLimitOrder` present in B194Tests.cs | PRESENT (line 178) |
| Total test count = 7 (2 T1 + 5 T2) | CONFIRMED: 7 [Fact] at lines 39, 68, 98, 117, 134, 154, 177 |
| CopyEngine.cs NOT modified in Cycle 2 | CONFIRMED: git stash round-trip shows same diff as T1+T2 only |

---

## 1. SOURCE CODE VERIFICATION

### IsBracketStopOrder (src/PropTraderTools/CopyEngine.cs lines 6495-6505)

```
internal static bool IsBracketStopOrder(Order o)
{
    if (o.OrderState == OrderState.TriggerPending
        && o.OrderType == OrderType.Market
        && o.StopPrice > 0)
        return true;
    if ((o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted)
        && (o.OrderType == OrderType.StopMarket || o.OrderType == OrderType.StopLimit))
        return true;
    return false;
}
```

- Visibility: `internal static` -- CORRECT (enables xUnit access via InternalsVisibleTo)
- TriggerPending+Market+StopPrice>0 path: PRESENT
- Working|Submitted+StopMarket|StopLimit path: PRESENT
- StopPrice>0 guard: PRESENT (line 6499)
- ASCII-only: PASS (all identifiers and literals are ASCII)
- CYC=3: base(1) + first-if(+1) + second-if(+1) = 3. PASS (<=8)
- No throw: PASS
- No lock: PASS

### HasNakedPosition (src/PropTraderTools/CopyEngine.cs lines 6516-6542)

```
internal static bool HasNakedPosition(Account acct)
{
    bool hasPosition = false;
    foreach (Position p in acct.Positions)
    {
        if (p.Quantity > 0) { hasPosition = true; break; }
    }
    if (!hasPosition)
        return false;

    bool hasStop = false;
    bool hasTarget = false;
    foreach (Order o in acct.Orders)
    {
        if (IsBracketStopOrder(o)) { hasStop = true; continue; }
        if ((o.OrderState == OrderState.Working || o.OrderState == OrderState.Submitted)
            && o.OrderType == OrderType.Limit)
            hasTarget = true;
    }
    return !hasStop && !hasTarget;
}
```

- Visibility: `internal static` -- CORRECT (promoted from private static per DW-B194-02 authorization)
- Inner loop uses IsBracketStopOrder(o): CONFIRMED (line 6536)
- CYC=7: base(1)+foreach-pos(+1)+qty-check(+1)+no-pos(+1)+foreach-ord(+1)+IsBracketStop-if(+1)+Limit-if(+1) = 7. PASS (<=8, improved from pre-F2 CYC=8)
- Semantic equivalence preserved: Working|Submitted+StopMarket|StopLimit path still covered by IsBracketStopOrder second branch. CONFIRMED.
- No throw: PASS
- No lock: PASS

---

## 2. SCOPE VERIFICATION

T2 scope lock: HasNakedPosition (refactor) + new IsBracketStopOrder only. No other methods.

| Change | Location | In Scope? |
|--------|----------|-----------|
| IsBracketStopOrder new method | CopyEngine.cs ~6495 | YES -- T2 required |
| HasNakedPosition inner loop refactor | CopyEngine.cs ~6516 | YES -- T2 required |
| TryNakedDetect F1 guard | CopyEngine.cs ~6427 | T1 change (pre-existing, not T2) |
| DrainThenDispatch F3 LINQ extension | CopyEngine.cs ~6570 | T1 change (pre-existing, not T2) |
| _flattenFilledTicks / FlattenOneAccount | NOT present | Correct -- T3 scope |

Working tree diff shows 4 hunks all from T1+T2 only. No T3 changes in CopyEngine.cs. PASS.

---

## 3. SEMANTIC EQUIVALENCE

Old HasNakedPosition inner loop checked `Working|Submitted` + `StopMarket|StopLimit`.
New IsBracketStopOrder second branch: `(Working||Submitted) && (StopMarket||StopLimit)`.
These are logically identical -- semantic equivalence preserved. CONFIRMED.

New capability added: TriggerPending+Market+StopPrice>0 (NT8 SIM ATM stops) now recognized as
bracket stops. This was the BUG-2 blind spot. CONFIRMED fixed.

---

## 4. InternalsVisibleTo

`[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]`
present at CopyEngine.cs line 42. CONFIRMED.

Comments in both methods reference "L46" but the attribute is at L42 -- minor discrepancy
in comments only. The attribute itself is present and functional. PASS.

---

## 5. INDEPENDENT 7-SCAN

### SCAN-01 [JS-021]: lock()

Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\(" | Where-Object { $_.LineNumber -ge 6480 -and $_.LineNumber -le 6550 }`
Result: 0 executable lock() in T2 area

Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\("` (full file)
Result: 9 matches, ALL in comments (pattern "no lock()"). Zero executable lock statements anywhere.
**SCAN-01: PASS**

### SCAN-02 [JS-033]: async void

Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async void"`
Result: 2 matches -- both in comments only ("NOT async void (JS-033)", "Tick is not async void").
Zero executable async void.
**SCAN-02: PASS**

### SCAN-03 [JS-066]: CYC count

IsBracketStopOrder: branches = base(1) + if(TriggerPending&&Market&&StopPrice>0)(+1) + if(Working||Submitted&&StopMarket||StopLimit)(+1) = CYC=3. PASS (<=8)
HasNakedPosition: branches = base(1)+foreach-pos(+1)+if-qty(+1)+if-!hasPos(+1)+foreach-ord(+1)+if-IsBracket(+1)+if-Working-Limit(+1) = CYC=7. PASS (<=8)
**SCAN-03: PASS**

### SCAN-04 [JS-001]: throw new

Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new" | Where-Object { $_.LineNumber -ge 6480 -and $_.LineNumber -le 6550 }`
Result: 0 matches.
**SCAN-04: PASS**

### SCAN-05 [JS-002]: return null

Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null" | Where-Object { $_.LineNumber -ge 6480 -and $_.LineNumber -le 6550 }`
Result: 1 match at line 6545 -- in COMMENT only ("return type Instrument (nullable reference, no raw return null)"). Outside T2 method bodies. Zero return null in IsBracketStopOrder or HasNakedPosition.
**SCAN-05: PASS**

### SCAN-06 [ASCII]: Non-ASCII + DateTime.Now

Command: `Get-Content "src/PropTraderTools/Tests/B194Tests.cs" | Where-Object { $_ -match '[^\x00-\x7F]' }`
Result: 0 matches.
Command: `Select-String -Path "src/PropTraderTools/Tests/B194Tests.cs" -Pattern "DateTime\.Now[^U]"`
Result: 0 matches.
**SCAN-06: PASS**

### SCAN-07 [Test coverage]: All 7 [Fact] tests

Command: `Select-String -Path "src/PropTraderTools/Tests/B194Tests.cs" -Pattern "\[Fact"`
Result: 7 matches at lines 39, 68, 98, 117, 134, 154, 177.

Test names confirmed present (by line):
- Line 39: `TryNakedDetect_SkipsPTTCopyFill_NoNakedDetectorCalled` (T1)
- Line 68: `DrainThenDispatch_IncludesSubmittedInEntryCandidates` (T1)
- Line 98: `IsBracketStopOrder_TriggerPendingMarket_WithStopPrice_ReturnsTrue` (T2)
- Line 117: `IsBracketStopOrder_TriggerPendingMarket_NoStopPrice_ReturnsFalse` (T2)
- Line 134: `IsBracketStopOrder_WorkingStopMarket_ReturnsTrue` (T2)
- Line 154: `HasNakedPosition_TriggerPendingStop_ReturnsFalse` (T2)
- Line 177: `IsBracketStopOrder_ReturnsFalse_ForWorkingLimitOrder` (T2) -- ADDED CYCLE 2

Count: 2 T1 + 5 T2 = 7. PASS.
**SCAN-07: PASS**

---

## 6. BUILD

Command: `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj 2>&1 | Select-String "^Build|error CS"`
Result: `Build succeeded.`
Errors: 0
Warnings: pre-existing CS0436 and CS8632 (not introduced by T2).
**BUILD: PASS**

---

## 7. CopyEngine.cs MODIFICATION STATUS IN CYCLE 2

git stash test confirmed: git stash / verify_critical_symbols shows same pre-existing failure
with and without T2 changes applied. CopyEngine.cs working-tree diff shows exactly 4 hunks
(T1 F1 at hunk 1, T2 IsBracketStopOrder insert at hunk 2, T2 HasNakedPosition refactor at
hunk 3, T1 F3 at hunk 4). No hunk corresponds to a Cycle-2-only CopyEngine.cs change.
**CopyEngine.cs NOT modified in Cycle 2: CONFIRMED**

---

## 8. DELETION AUDIT

Command: `git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-\s+(private|internal|public)"`
Result: 1 deleted symbol: `private static bool HasNakedPosition(Account acct)`

Assessment: This is the `private` -> `internal` visibility promotion authorized by DW-B194-02
(per 04-tickets.md Section 4: "Visibility: internal static -- promoted from private static --
required for xUnit test project access via InternalsVisibleTo"). The method is replaced with
`internal static bool HasNakedPosition(Account acct)` at line 6516. Not a deletion -- a
visibility promotion with authorization.

No other deleted symbols.
**DELETION AUDIT: PASS (1 authorized promotion, DW-B194-02)**

---

## 9. SINGLE-HUNK BAN

Command: `git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@"`
Result: 4 hunks (@@ -6427, @@ -6475, @@ -6498, @@ -6537)
Single-hunk ban applies only if count == 1 (full-file replace). Count = 4. Ban does NOT apply.
**SINGLE-HUNK BAN: PASS (4 hunks)**

---

## 10. GUARDIAN SCRIPT

Command: `powershell -File .\scripts\verify_critical_symbols.ps1`
Result: EXIT 1 -- GUARDIAN FAIL

Failure message: `TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)`

Pre-existing check: stashed T2 changes, ran guardian on HEAD. Same failure.
**The guardian failure is PRE-EXISTING and unrelated to B194 T2.**

T2 introduces NO new guardian failures. The TradeCopierPanel failure predates B194.
**GUARDIAN: Pre-existing failure only -- no new failures from T2. PASS (no regressions introduced)**

---

## 11. PLAN ALIGNMENT

| Requirement | Status |
|-------------|--------|
| F2 closes BUG-2 (DW-B194-02) | CONFIRMED -- IsBracketStopOrder added, HasNakedPosition inner loop refactored |
| IsBracketStopOrder: internal static | CONFIRMED (line 6495) |
| HasNakedPosition: internal static | CONFIRMED (line 6516) |
| CYC(IsBracketStopOrder) = 3 | CONFIRMED |
| CYC(HasNakedPosition) = 7 | CONFIRMED |
| StopPrice>0 guard present | CONFIRMED (line 6499) |
| T1 changes (TryNakedDetect, DrainThenDispatch) still present | CONFIRMED |
| No T3 changes | CONFIRMED |

---

## 12. SPEC COMPLIANCE TABLE

| Spec Test Name (04-tickets.md section 7) | Present as | Status |
|------------------------------------------|-----------|--------|
| HasNakedPosition_ReturnsFalse_WhenOnlyTriggerPendingStopExists | `HasNakedPosition_TriggerPendingStop_ReturnsFalse` (L154) | PRESENT (functional equivalent, accepted Cycle 1) |
| IsBracketStopOrder_ReturnsTrue_ForTriggerPendingMarketWithStopPrice | `IsBracketStopOrder_TriggerPendingMarket_WithStopPrice_ReturnsTrue` (L98) | PRESENT |
| IsBracketStopOrder_ReturnsTrue_ForWorkingStopMarket | `IsBracketStopOrder_WorkingStopMarket_ReturnsTrue` (L134) | PRESENT |
| IsBracketStopOrder_ReturnsFalse_ForWorkingLimitOrder | `IsBracketStopOrder_ReturnsFalse_ForWorkingLimitOrder` (L177) | PRESENT -- CYCLE 2 ADDITION |

---

## 13. DNA RULE SUMMARY

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (lock ban) | Zero executable lock() in T2 methods | PASS |
| JS-033 (async void ban) | Zero async void in T2 methods | PASS |
| JS-066 (CYC<=8) | IsBracketStopOrder=3, HasNakedPosition=7 | PASS |
| JS-001 (no throw in hot paths) | Zero throw in T2 methods | PASS |
| JS-002 (no return null) | Zero return null in T2 methods | PASS |
| ASCII-only | No non-ASCII in T2 changes | PASS |
| DateTime.UtcNow | No DateTime.Now in T2 changes | PASS |
| InternalsVisibleTo | Present at line 42 | PASS |
| No sealed on TradeCopierWindow | Not applicable (T2 is CopyEngine.cs only) | N/A |
| No FontFamily | No UI changes in T2 | PASS |
| No hex colors | No UI changes in T2 | PASS |

---

## VERDICT

**VERIFY_PASS**

Cycle 1 VERIFY_FAIL resolved: `IsBracketStopOrder_ReturnsFalse_ForWorkingLimitOrder` present at line 177.
All 7 tests present (2 T1 + 5 T2).
CopyEngine.cs not modified in Cycle 2.
All 7 scans PASS.
Build succeeds.
Deletion audit: 1 authorized visibility promotion (DW-B194-02).
Single-hunk ban: not triggered (4 hunks).
Guardian failure: pre-existing, no new regressions from T2.

*Written by: ptt-verifier*
*Epic: B194*
*Ticket: T2*
*Phase: 4b (Verifier) -- Cycle 2*