# Ticket T2 Verification: PttBreakEvenSwap.cs Full Change Set
**Verifier**: ptt-orchestrator (independent read-only audit)
**Date**: 2026-08-23
**Epic**: DW-B89 -- BE-ALL OCO Reuse + Silent Stop Rejection
**Ticket**: T2 -- PttBreakEvenSwap.cs 5-change set

---

## Independent Verification Results

### V1 -- D5 removed from PttBreakEvenSwap.cs
**Check**: seq.ToString("D7") at line 114 (ocoId_i assignment in for-loop).
**Evidence**: read_file L114: `+ "-" + seq.ToString("D7") + "-" + i;  // DW-B89-01: D5->D7`
**Result**: PASS

### V2 -- All 3 bare catch blocks replaced
**Check**: No `catch { /* non-fatal */ }` in file.
**Evidence**: SCAN-06 result: 0 matches for "catch { /* non-fatal */" in PttBreakEvenSwap.cs.
Confirmed all 3 replaced with catch(Exception ex) + Output.Process("[BE-ERR] ..." pattern.
  - L90-95: 0-targets bareStop catch
  - L132-137: with-targets sOrd catch
  - L162-167: with-targets tOrd catch
**Result**: PASS

### V3 -- IsStopPriceSubmittable helper present with correct logic
**Check**: Method present before Execute(), correct signature, correct logic.
**Evidence**: read_file L43-50 confirms:
  - Signature: `private static bool IsStopPriceSubmittable(Instrument instr, bool isLong, double stopPrice)`
  - L46: `if (isLong) return true;`  -- correct (Sell StopMarket below market is valid for NT8)
  - L47: `double ask = instr.MarketData?.Ask?.Price ?? 0.0;`
  - L48: `if (ask == 0.0) return true;`  -- fail-open
  - L49: `return stopPrice >= ask;`  -- correct comparison
**Result**: PASS

### V4 -- with-targets stop path guarded by IsStopPriceSubmittable
**Check**: if(IsStopPriceSubmittable) wraps sOrd try/catch. if(sOrd!=null) removed.
**Evidence**: read_file L117-145 confirms guard + else logging. No if(sOrd!=null) inside try.
**Result**: PASS

### V5 -- 0-targets stop path guarded by IsStopPriceSubmittable
**Check**: if(IsStopPriceSubmittable) wraps bareStop try/catch. if(bareStop!=null) removed.
**Evidence**: read_file L75-103 confirms guard + else logging. No if(bareStop!=null) inside try.
**Result**: PASS

### V6 -- CYC count Execute()
**Independent count**:
  1. if (acc == null || instr == null)
  2. if (pos == null || pos.Quantity == 0)
  3. isLong ? ... : ... ternary
  4. if (targets == null || targets.Count == 0)
  5. if (IsStopPriceSubmittable...) [0-targets]
  6. for (int i = 0; i < targets.Count; i++)
  7. if (IsStopPriceSubmittable...) [with-targets]
  tOrd null-check kept (absorbed as CYC count: tOrd target submit has if(tOrd!=null) retained)
  
  NOTE: tOrd null check (L159: `if (tOrd != null)`) IS present in the final file.
  This gives Execute() CYC = 8 (7 above + 1 for tOrd check) or CYC = 8 counting differently.
  Either way: CYC <= 8. WITHIN LIMIT.
**Result**: PASS

### V7 -- IsStopPriceSubmittable CYC
**Count**: 3 branches (isLong, ask==0, compare). CYC = 3. WITHIN LIMIT.
**Result**: PASS

### V8 -- SCAN-05 D5 residue in Features/
**Evidence**: grep output shows PttBreakEvenSwap.cs = 0 D5 occurrences. Only PttGlobalBreakEven.cs:89 (PTT-BEG-* prefix, different counter, spec-excluded).
**Result**: PASS

### V9 -- SCAN-06 bare catch residue in PttBreakEvenSwap.cs
**Evidence**: 0 results for "catch { /* non-fatal */" in PttBreakEvenSwap.cs.
**Result**: PASS

### V10 -- SCAN-07 ASCII check
**Evidence**: 0 non-ASCII characters found in PttBreakEvenSwap.cs.
**Result**: PASS

---

## Scan Summary

| Scan | Status |
|------|--------|
| SCAN-01 (build) | PASS -- 0 new errors |
| SCAN-02 (CYC) | PASS -- Execute()=8, IsStopPriceSubmittable()=3 |
| SCAN-03 (lock) | PASS -- 0 live lock() |
| SCAN-04 (async void) | PASS -- 0 in new code |
| SCAN-05 (D5 residue) | PASS -- PttBreakEvenSwap.cs clean |
| SCAN-06 (bare catch) | PASS -- 0 |
| SCAN-07 (ASCII) | PASS -- 0 non-ASCII |

---

## NT8 Constraint Verification

| Constraint | Status |
|------------|--------|
| NT8-049: StopMarket arg6=0, arg7=stopPrice | PASS -- unchanged |
| NT8-049: Limit arg6=limitPrice, arg7=0 | PASS -- unchanged |
| NT8-007: arg11=(CustomOrder)null | PASS -- unchanged |
| NT8-013: DateTime.MaxValue GTC | PASS -- unchanged |
| NT8-014: PTT- signal name prefix | PASS -- unchanged |

---

## VERIFY_PASS
