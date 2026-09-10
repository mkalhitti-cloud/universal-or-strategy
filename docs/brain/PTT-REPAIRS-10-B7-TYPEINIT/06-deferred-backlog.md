# 06-deferred-backlog.md
# Epic: PTT-REPAIRS-10-B7-TYPEINIT

---

## Block: PTT-REPAIRS-10-B7-TYPEINIT

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B7-01 | B79CancelRaceGuardTests: 5th TypeInit test -- CLOSED. Applied `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` to `LogDiagOrderCount_ShouldLogCorrectCount_WhenOrdersExistForInstrument` at L6091. All 5 NT8-runtime skips in B79CancelRaceGuardTests are now present. No bare [Fact] without Skip remains in class range L5829-L6474. Final test counts: passed=23, failed=0, skipped=491, total=514. | P1 | PTT-REPAIRS-10-B7-TYPEINIT | CLOSED |

**No new deferred items created by this block.**

---

## Carried Forward from Prior Blocks

| ID | Item | Priority | Source Block | Status |
|----|------|----------|--------------|--------|
| DW-09-01 | Implement 70 missing CopyEngine helper methods referenced by obfuscation-skipped tests across B79CancelRaceGuardTests, BwaveCycT1R1BeHelperTests, BwaveCycTaR2HelperTests, BwaveCycTaR3HelperTests, BwaveCycTaR6HelperTests. Full BWAVE-CYC implementation epic required (large scope; 70 method stubs to be authored). ObfuscationAttribute decorations for LogBeSlotEviction and GetSenderAccountName are already present (block PTT-REPAIRS-09-OBFUSC-ATTR). Prerequisite for DW-09-02, DW-09-03, DW-09-04. | P1 | PTT-REPAIRS-09-OBFUSC-ATTR | OPEN |
| DW-09-02 | Fix binding flags for `LogBeSlotEviction` test in `BwaveCycTaR3HelperTests`: change `NonPublic\|Instance` -> `NonPublic\|Static` in GetMethod call (method is `private static`). Then remove obfuscation Skip from 2 affected tests. Note: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` already added to `LogBeSlotEviction` at L1778 in PTT-REPAIRS-09-OBFUSC-ATTR. Prerequisite: DW-09-01 confirms LogBeSlotEviction remains private static. | P2 | After DW-09-01 | OPEN |
| DW-09-03 | Fix binding flags for `GetSenderAccountName` test in `BwaveCycTaR2HelperTests`: change `NonPublic\|Instance` -> `NonPublic\|Static` in GetMethod call (method is `internal static`). Then remove obfuscation Skip from 1 affected test. Note: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` already added to `GetSenderAccountName` at L6969 in PTT-REPAIRS-09-OBFUSC-ATTR. Prerequisite: DW-09-01 scope for GetSenderAccountName. | P2 | After DW-09-01 | OPEN |
| DW-09-04 | Full skip removal for all 137 remaining obfuscation-skipped tests referencing non-existent methods, after DW-09-01 implements each method, binding flags are verified to match (static vs instance), and ObfuscationAttribute decorations are confirmed present. Each test must be individually validated before Skip removal. Prerequisite: DW-09-01 + DW-09-02 + DW-09-03 all complete. | P2 | After DW-09-01/02/03 | OPEN |

---

## Rollup Summary

Total OPEN items: 4 (DW-09-01, DW-09-02, DW-09-03, DW-09-04)
Total CLOSED items: 1 (DW-B7-01 -- closed by PTT-REPAIRS-10-B7-TYPEINIT)
