# B33-LaneA: 03-validation-report.md
# DW-B33-01 -- New-Stop BE Approach: SubmitBeStop + OrphanCancelGuard
# Validator: ptt-verifier | Phase 4b | 2026-07-20 (RETRY)
# Source: c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs (READ-ONLY)

---

## STEP 0 -- RULES CATALOG GATE

| Document | Result |
|---|---|
| docs/standards/jane-street/RULES_CATALOG.md | Read in full -- UTF-8 clean |
| docs/standards/NT8_COMPILER_RULES.md | Read in full -- NT8-049 post-mortem confirmed |
| JS-021 lock( anywhere in src/ | 0 executable -- PASS |
| JS-033 async void | 0 results -- PASS |
| JS-001 throw new Exception in hot path | None in new code -- PASS |
| NT8-007 CreateOrder last arg CustomOrder | (NinjaTrader.Cbi.CustomOrder)null -- PASS |
| NT8-013 DateTime.MaxValue | Confirmed at line 1577 -- PASS |
| NT8-014 Signal name "PTT-BE-Stop" starts with PTT- | Confirmed -- PASS |
| NT8-049 Three bugs from live test | All 3 fixed -- confirmed below |

GATE RESULT: PASS

---

## STEP 2 -- FULL CHECKLIST (Layer 3 independent verification)

### SCAN-01 BUILD TAG

| Item | Result |
|---|---|
| Line 41: Tag = "PTT-COPIER B33 | new-stop BE | 2026-07-20" | PASS (line 41) |

### SCAN-02 FIELD

| Item | Result |
|---|---|
| _pendingBeStop field exists | PASS (line 163) |
| private volatile Order _pendingBeStop = null; | PASS (line 165) |
| Type is Order | PASS -- private volatile Order confirmed |

### SCAN-03 LEADER BE PATH

| Item | Result |
|---|---|
| BreakEven(Account leader,...) does NOT call MoveStopToBreakEven(leader,...) | PASS -- leader path calls SubmitBeStop only (line 1642) |
| BreakEven calls FindPosition(leader, instrument) | PASS -- var leaderPos = FindPosition(leader, instrument) (line 1635) |
| BreakEven calls SubmitBeStop(leader, instrument, newStop) -- 3 args, no qty | PASS -- line 1642 |
| Follower foreach still calls MoveStopToBreakEven(acc, instrument, bufferTicks) unchanged | PASS -- line 1647 |
| MoveStopToBreakEven method body is UNCHANGED | PASS -- lines 1464-1544 intact |

### SCAN-04 ORPHAN GUARD HOOK IN TryFirePositionState

| Item | Result |
|---|---|
| if (!hasPos) OrphanCancelGuard(e.Order.Account, e.Order.Instrument); present | PASS (lines 740-741) |
| Hook is AFTER PositionStateChanged?.Invoke(...) | PASS -- Invoke at line 738, hook at 739-741 |
| Hook is BEFORE closing brace of TryFirePositionState | PASS -- closing brace at line 742 |

### SCAN-05 SUBMITBESTOP METHOD (NT8-049 all 3 bugs fixed)

| Item | Result |
|---|---|
| Signature: private void SubmitBeStop(Account leaderAcc, Instrument instr, double bePrice) -- 3 params, NO qty | PASS (line 1554) |
| Flat guard uses leaderAcc.Positions[instr] (not FindPosition + IsFlat) | PASS (line 1556) |
| pos.Quantity used inside method (not passed-in qty) | PASS (line 1574) |
| CreateOrder arg6 = 0 (limitPrice) -- comment "MUST be 0 for StopMarket (NT8-049)" present | PASS (line 1575) |
| CreateOrder arg7 = bePrice (stopPrice) -- comment "bePrice goes HERE (NT8-049)" present | PASS (line 1576) |
| CreateOrder last arg = (NinjaTrader.Cbi.CustomOrder)null | PASS (line 1578) |
| Signal name is "PTT-BE-Stop" (starts with PTT-) | PASS (line 1577) |
| DateTime.MaxValue present | PASS (line 1577) |
| leaderAcc.Submit(new[] { _pendingBeStop }) present AFTER CreateOrder | PASS (line 1579) |
| NO other account submitted | PASS -- only leaderAcc.Submit in this method |
| _pendingBeStop = leaderAcc.CreateOrder(...) assigned | PASS (line 1572) |
| Print "[BE] SubmitBeStop {direction} {qty} @ {bePrice:F2}" | PASS (lines 1580-1582) |

### SCAN-06 ORPHANCANCELGUARD

| Item | Result |
|---|---|
| Signature: private void OrphanCancelGuard(Account acc, Instrument instr) | PASS (line 1596) |
| Null guard: if (_pendingBeStop == null) return; | PASS (line 1598) |
| State guard: if (_pendingBeStop.OrderState != OrderState.Working) { _pendingBeStop = null; return; } | PASS (lines 1600-1603) |
| acc.Cancel(new Order[] { _pendingBeStop }); | PASS (line 1607) |
| _pendingBeStop = null after cancel (all paths) | PASS -- line 1602 (early exit), line 1615 (post-cancel) |
| Print "[BE] OrphanCancelGuard fired -- pending BE stop cancelled" | PASS (line 1608) |

### SCAN-07 P0 COMPLIANCE (Independent shell scans -- Layer 3)

| Scan | Command | Result |
|---|---|---|
| lock( executable | Select-String -Pattern "lock\(" | PASS -- 3 hits ALL in comments only (lines 618, 1553, 1595) |
| async void | Select-String -Pattern "async void" | PASS -- 0 results |
| Non-ASCII chars | Get-Content | Where-Object non-ASCII | PASS -- 0 results |
| FontFamily | Select-String -Pattern "FontFamily" | PASS -- 0 results |
| Hex color #RRGGBB | Select-String -Pattern "#[0-9A-Fa-f]{6}" | PASS -- 0 results |
| leaderAcc.Submit exactly 1 executable result | Select-String | PASS -- 1 result line 1579 + 1 comment line 1549 |
| SubmitBeStop.*Quantity as parameter | Select-String -Pattern "SubmitBeStop.*Quantity" | PASS -- 1 hit in Print body (line 1581) only, not a parameter |

---

## NT8-049 CONFIRMATION -- All 3 Bugs Fixed

| Bug | Confirmed Fixed |
|---|---|
| Bug 1 -- arg order: bePrice was in limitPrice slot (arg6) instead of stopPrice (arg7) | FIXED -- line 1575: arg6=0, line 1576: arg7=bePrice |
| Bug 2 -- account scope: called inside foreach-all-accounts (submitted to Sim102) | FIXED -- SubmitBeStop called once for leader only (line 1642), never inside AllAccounts loop |
| Bug 3 -- qty passed as parameter summed from outer loop | FIXED -- no qty param; reads pos.Quantity from leaderAcc.Positions[instr] (line 1574) |

---

## UNCHANGED GUARDS VERIFICATION

| Guard | Result |
|---|---|
| ArmPendingBe not modified | PASS -- lines 1770-1793, body intact |
| OnPendingBeAccountUpdate not modified | PASS -- lines 1886-1941, body intact |
| TradeCopierPanel.cs has 0 references to SubmitBeStop | PASS -- Select-String returned 0 |

---

## STEP 3 -- TEST FILE VERIFICATION

File: src/PropTraderTools/CopyEngineTests.cs (lines 2721-2766)

| Item | Result |
|---|---|
| SubmitBeStop_MethodExists_And_HasThreeParameters [Fact] exists (NOT HasFourParameters) | PASS (line 2723) |
| Assert.Equal(3, parms.Length) present | PASS (line 2730) |
| No typeof(int) param check (qty removed) | PASS -- only Account, Instrument, double checked (lines 2732-2734) |
| OrphanCancelGuard_MethodExists_And_HasTwoParameters [Fact] exists | PASS (line 2739) |
| PendingBeStop_FieldExists_And_InitialValueIsNull [Fact] exists | PASS (line 2754) |
| All 3 use [Fact] (not [Test] / [TestMethod]) | PASS -- [Fact] on lines 2722, 2738, 2753 |
| All 3 use Assert.NotNull / Assert.Equal / Assert.Null (xUnit) | PASS -- xUnit assertions throughout |

---

## SUMMARY TABLE

| Section | Items | Passed | Failed |
|---|---|---|---|
| SCAN-01 Build Tag | 1 | 1 | 0 |
| SCAN-02 Field | 3 | 3 | 0 |
| SCAN-03 Leader BE Path | 5 | 5 | 0 |
| SCAN-04 Orphan Guard Hook | 3 | 3 | 0 |
| SCAN-05 SubmitBeStop | 12 | 12 | 0 |
| SCAN-06 OrphanCancelGuard | 6 | 6 | 0 |
| SCAN-07 P0 Compliance | 7 | 7 | 0 |
| NT8-049 Bugs | 3 | 3 | 0 |
| Unchanged Guards | 3 | 3 | 0 |
| Test File | 7 | 7 | 0 |
| TOTAL | 50 | 50 | 0 |

---

## OVERALL VERDICT: VERIFY_PASS

All 50 checklist items passed.
Zero P0 violations.
Zero DNA violations.
Zero NT8 compiler violations.
Zero unchanged-guard modifications.
3 new [Fact] xUnit tests present and correct (HasThreeParameters -- qty removed per NT8-049).
TradeCopierPanel.cs unmodified.
NT8-049: all 3 live-test bugs confirmed fixed.

---

END OF B33-LaneA VALIDATION REPORT
Verifier: ptt-verifier (Lane C) | 2026-07-20 RETRY