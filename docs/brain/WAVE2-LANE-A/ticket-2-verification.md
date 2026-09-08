# Ticket 2 Verification: HasArmingAtmBrackets CCN 9 -> 5

**Scope lock**: TICKET 2 ONLY -- WAVE2-LANE-A
**Verifier**: ptt-verifier (Phase 4b)
**Ticket**: WAVE2-LANE-A Ticket 2
**Date**: 2026-08
**Source**: `src/PropTraderTools/CopyEngine.cs` lines 5345-5389
**Prerequisite confirmed**: Ticket 1 VERIFY_PASS (ticket-1-verification.md present)

---

## FINAL VERDICT

**VERIFY_PASS**

All 7 verification steps passed. All DNA rules satisfied. All 9 Ticket 2 tests pass.
WAVE2-LANE-A CCN goal achieved for both tickets.

---

## STEP 1: Source Verification (lines 5345-5389)

**Claim**: stateActive compound assignment replaced by IsArmingOrderState call; helper inserted immediately after.

**Independent read of lines 5345-5389 (CopyEngine.cs):**

```
5345-5357: Comment block on HasArmingAtmBrackets -- updated to:
           CCN=5: base(1)+foreach(1)+instr-skip(1)+IsArmingOrderState(1)+IsAtmBracketName(1). WAVE2-LANE-A.
           DW-LB-FL-01-V2 note preserved. JS annotations present.

5357:  internal static bool HasArmingAtmBrackets(Account acc, Instrument instr)
5358:  {
5359:      foreach (var o in acc.Orders.ToList())
5360:      {
5361:          if (o.Instrument?.FullName != instr.FullName)
5362:              continue;
5363:          if (!IsArmingOrderState(o.OrderState)) // WAVE2-LANE-A extraction
5364:              continue;
5365:          if (IsAtmBracketName(o.Name))
5366:              return true;
5367:      }
5368:      return false;
5369:  }

5371-5375: Comment block on IsArmingOrderState: CCN=6, JS annotations, DW-LB-FL-01-V2 note, InternalsVisibleTo note.
5376:  internal static bool IsArmingOrderState(OrderState s)
5377:  {
5378:      if (s == OrderState.Initialized)   return true;
5380:      if (s == OrderState.Working)        return true;
5382:      if (s == OrderState.Submitted)      return true;
5384:      if (s == OrderState.Accepted)       return true;
5386:      if (s == OrderState.TriggerPending) return true;
5388:      return false;
5389:  }
```

**Checks:**
- [x] stateActive compound assignment REMOVED; replaced by `!IsArmingOrderState(o.OrderState)` -- CONFIRMED
- [x] `// WAVE2-LANE-A extraction` comment on replacement line -- CONFIRMED
- [x] IsArmingOrderState helper is `internal static bool` immediately after HasArmingAtmBrackets -- CONFIRMED
- [x] All 5 states covered: Initialized, Working, Submitted, Accepted, TriggerPending -- CONFIRMED
- [x] DW-LB-FL-01-V2 comment preserved in HasArmingAtmBrackets comment block -- CONFIRMED
- [x] CCN=5 noted in comment block on HasArmingAtmBrackets -- CONFIRMED

**STEP 1: PASS**

---

## STEP 2: CCN Lizard Scan (independent re-run)

**Command:**
```powershell
lizard src/PropTraderTools/ -x "*/bin/*" -x "*/obj/*" -x "*Tests*" --csv |
  ConvertFrom-Csv -Header NLOC,CCN,Token,Params,Length,Location,File,Function,Sig,Start,End |
  Where-Object {[int]$_.CCN -gt 8} |
  Select-Object CCN, Function, @{L="File";E={[IO.Path]::GetFileName($_.File)}}
```

**Result: EMPTY -- no output. Zero methods exceed CCN 8.**

Cross-check vs engineer's Layer 2 report: **MATCH** (engineer also reported EMPTY).

**STEP 2: PASS**

---

## STEP 3: Test File Verification (Wave2LaneATests.cs)

**Both test classes present:**
- [x] `Wave2LaneAIsExitSignalNameTests` -- present (Ticket 1 class, intact)
- [x] `Wave2LaneAHasArmingAtmBracketsTests` -- present (Ticket 2 class)

**Ticket 2 class [Fact] methods (9 total):**

| # | Method | Status |
|---|---|---|
| 1 | `IsArmingOrderState_MethodExists_InCopyEngine` | Present |
| 2 | `IsArmingOrderState_Initialized_ReturnsTrue` | Present |
| 3 | `IsArmingOrderState_Working_ReturnsTrue` | Present |
| 4 | `IsArmingOrderState_Submitted_ReturnsTrue` | Present |
| 5 | `IsArmingOrderState_Accepted_ReturnsTrue` | Present |
| 6 | `IsArmingOrderState_TriggerPending_ReturnsTrue` | Present |
| 7 | `IsArmingOrderState_Filled_ReturnsFalse` | Present |
| 8 | `IsArmingOrderState_Cancelled_ReturnsFalse` | Present |
| 9 | `IsArmingOrderState_Rejected_ReturnsFalse` | Present |

**9 [Fact] tests >= 8 required minimum. EXCEEDS spec.**

**Framework check:**
- [x] xUnit only -- `using Xunit;` at top, `[Fact]` decorators, `Assert.True`/`Assert.False`/`Assert.NotNull`
- [x] No NUnit, no MSTest
- [x] Inline mirror pattern used (not direct reflection) -- correct per project constraint (NT8 module .cctor incompatibility on net8.0)
- [x] Method-existence test via `GetMethod` without enum boxing -- correct pattern

**Spec deviation (noted):**
The ticket spec listed 8 tests calling `TrimSignal.IsArmingOrderState(OrderState.X)` directly.
Engineer correctly identified the NT8 enum boxing constraint (same as B141/B143/PttBreakEvenB72)
and applied the inline mirror pattern. 9 tests delivered vs 8 specified -- exceeds requirement.
Inline mirror logic is bit-for-bit identical to production `IsArmingOrderState` source.
**This is ACCEPTABLE -- a documented and established project pattern.**

**STEP 3: PASS**

---

## STEP 4: Independent Test Run

**Command:**
```powershell
dotnet test tests\PropTraderTools.Tests\PropTraderTools.Tests.csproj
```

**Result:**
```
Total tests: 272
     Passed: 269
    Skipped: 3
 Total time: 0.6429 Seconds
Test Run Successful.
```

**All 9 Ticket 2 tests confirmed passing:**
- Wave2LaneAHasArmingAtmBracketsTests.IsArmingOrderState_MethodExists_InCopyEngine -- PASSED
- Wave2LaneAHasArmingAtmBracketsTests.IsArmingOrderState_Initialized_ReturnsTrue -- PASSED
- Wave2LaneAHasArmingAtmBracketsTests.IsArmingOrderState_Working_ReturnsTrue -- PASSED
- Wave2LaneAHasArmingAtmBracketsTests.IsArmingOrderState_Submitted_ReturnsTrue -- PASSED
- Wave2LaneAHasArmingAtmBracketsTests.IsArmingOrderState_Accepted_ReturnsTrue -- PASSED
- Wave2LaneAHasArmingAtmBracketsTests.IsArmingOrderState_TriggerPending_ReturnsTrue -- PASSED
- Wave2LaneAHasArmingAtmBracketsTests.IsArmingOrderState_Filled_ReturnsFalse -- PASSED
- Wave2LaneAHasArmingAtmBracketsTests.IsArmingOrderState_Cancelled_ReturnsFalse -- PASSED
- Wave2LaneAHasArmingAtmBracketsTests.IsArmingOrderState_Rejected_ReturnsFalse -- PASSED

**Cross-check vs engineer's Layer 2 report:**
Engineer reported: 272 total, 269 passing, 3 skipped, 0 failing.
Verifier measured: 272 total, 269 passing, 3 skipped, 0 failing.
**EXACT MATCH.**

269 passing >= 268 required threshold. 0 failing. **STEP 4: PASS**

---

## STEP 5: CCN Math Verification (independent count)

**HasArmingAtmBrackets (source lines 5357-5369):**

| # | Decision point | Source line | Count |
|---|---|---|---|
| base | function entry | 5357 | +1 |
| 1 | `foreach` loop | 5359 | +1 |
| 2 | `o.Instrument?.FullName != instr.FullName` skip | 5361 | +1 |
| 3 | `!IsArmingOrderState(o.OrderState)` skip | 5363 | +1 |
| 4 | `IsAtmBracketName(o.Name)` return-true | 5365 | +1 |
| **Total** | | | **5** |

Lizard independently confirms: CCN=5. **MATCH.**

**IsArmingOrderState (source lines 5376-5389):**

| # | Decision point | Source line | Count |
|---|---|---|---|
| base | function entry | 5376 | +1 |
| 1 | `s == OrderState.Initialized` | 5378 | +1 |
| 2 | `s == OrderState.Working` | 5380 | +1 |
| 3 | `s == OrderState.Submitted` | 5382 | +1 |
| 4 | `s == OrderState.Accepted` | 5384 | +1 |
| 5 | `s == OrderState.TriggerPending` | 5386 | +1 |
| **Total** | | | **6** |

Lizard independently confirms: CCN=6. **MATCH.**

Both <= 8. JS-080 satisfied.

**STEP 5: PASS**

---

## STEP 6: Scope Integrity

**Ticket 1 region check (lines 2347-2377):**

Read lines 2347-2402 independently. Confirmed:
- `IsExitSignalName` (lines 2349-2370) is intact with CCN=8 comment and `IsNativeCloseOrFlattenSignal` delegation
- `IsNativeCloseOrFlattenSignal` (lines 2376-2377) is intact as `internal static bool` expression-body
- No Ticket 2 changes visible in this region

**DNA scans (independent):**

| Scan | Pattern | Result |
|---|---|---|
| SCAN-07 (lock) | `lock\(` | 10 matches -- all in comments (no lock(), "no lock() anywhere"). PASS |
| SCAN-03 (FontFamily) | `FontFamily` | 3 matches -- all in comments ("No FontFamily"). PASS |
| SCAN-04 (hex color) | `#[0-9A-Fa-f]{6}` | Zero matches. PASS |
| SCAN-06 (DateTime.Now) | `DateTime\.Now[^U]` | 7 matches -- all in comments ("No DateTime.Now"). PASS |
| throw new | `throw new` | Zero matches. PASS |
| async/await (non-Dispatcher) | `async\|await` not in comments | Only Dispatcher.InvokeAsync usages. PASS |

**No other methods outside Ticket 2 scope modified.**

**STEP 6: PASS**

---

## STEP 7: Full WAVE2-LANE-A CCN Status (lizard confirmed)

| Method | Ticket | CCN Before | CCN After | Lizard Confirmed | JS-080 |
|---|---|---|---|---|---|
| `TrimSignal::IsExitSignalName` | T1 | 9 | 8 | 8 | PASS (<=8) |
| `TrimSignal::IsNativeCloseOrFlattenSignal` | T1 (new) | -- | 3 | 2* | PASS (<=8) |
| `TrimSignal::HasArmingAtmBrackets` | T2 | 9 | 5 | 5 | PASS (<=8) |
| `TrimSignal::IsArmingOrderState` | T2 (new) | -- | 6 | 6 | PASS (<=8) |

*Note: Lizard reports CCN=2 for `IsNativeCloseOrFlattenSignal` (expression-body `=>` form).
Manual count with base(1)+Close(1)+Flatten(1)=3. The discrepancy is methodological: Lizard counts
the expression-body form as a single expression with 1 `||` branch. Both 2 and 3 are <=8.
No violation.

**All 4 methods <= 8. WAVE2-LANE-A CCN goal fully achieved.**

**STEP 7: PASS**

---

## Discrepancy Summary

| # | Item | Engineer Report | Verifier Finding | Status |
|---|---|---|---|---|
| 1 | Test count | 269 passing, 272 total | 269 passing, 272 total | MATCH |
| 2 | CCN lizard empty | EMPTY | EMPTY | MATCH |
| 3 | IsNativeCloseOrFlattenSignal CCN | 3 (manual count) | 2 (lizard), 3 (manual) | NOTE -- not a violation |
| 4 | Architecture plan visibility | `private static` | Implemented as `internal static` per revised ticket V7 | CORRECT -- ticket V7 supersedes arch plan |
| 5 | Scope creep | None reported | None found | MATCH |

**No VERIFY_FAIL conditions found.**

---

## DNA Rule Compliance (Ticket 2 scope)

| Rule | Check | Result |
|---|---|---|
| JS-080 (CYC<=8) | HasArmingAtmBrackets=5, IsArmingOrderState=6 | PASS |
| JS-021 (no lock) | Zero lock() in new/modified lines | PASS |
| JS-001 (no throw new) | Zero throw new anywhere in file | PASS |
| JS-002 (no return null) | Both methods return bool only | PASS |
| ASCII-only | No string literals in IsArmingOrderState; comment text all ASCII | PASS |
| NT8 constraints | No async/await in new methods; no Account.All; no AtmStrategyCreate | PASS |
| .NET 4.8 compat | if/return chains only; no switch expression; no C# 9+ | PASS |
| InternalsVisibleTo | Declared at CopyEngine.cs:46; both helpers internal | PASS |

---

## VERIFY_PASS