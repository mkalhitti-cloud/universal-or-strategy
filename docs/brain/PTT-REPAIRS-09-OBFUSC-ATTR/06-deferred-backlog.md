# 06-deferred-backlog.md
# Epic: PTT-REPAIRS-09-OBFUSC-ATTR

---

## Block: PTT-REPAIRS-09-OBFUSC-ATTR

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-09-01 | Implement 70 missing CopyEngine helper methods referenced by obfuscation-skipped tests across B79CancelRaceGuardTests, BwaveCycT1R1BeHelperTests, BwaveCycTaR2HelperTests, BwaveCycTaR3HelperTests, BwaveCycTaR6HelperTests. Full BWAVE-CYC implementation epic required (large scope; 70 method stubs to be authored). ObfuscationAttribute decorations for LogBeSlotEviction and GetSenderAccountName are already present (block PTT-REPAIRS-09-OBFUSC-ATTR). Prerequisite for DW-09-02, DW-09-03, DW-09-04. | P1 | BWAVE-CYC epic (future) | OPEN |
| DW-09-02 | Fix binding flags for `LogBeSlotEviction` test in `BwaveCycTaR3HelperTests`: change `NonPublic\|Instance` → `NonPublic\|Static` in GetMethod call (method is `private static`). Then remove obfuscation Skip from 2 affected tests. Note: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` already added to `LogBeSlotEviction` at L1778 in this block. Prerequisite: DW-09-01 confirms LogBeSlotEviction remains private static. | P2 | After DW-09-01 | OPEN |
| DW-09-03 | Fix binding flags for `GetSenderAccountName` test in `BwaveCycTaR2HelperTests`: change `NonPublic\|Instance` → `NonPublic\|Static` in GetMethod call (method is `internal static`). Then remove obfuscation Skip from 1 affected test. Note: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` already added to `GetSenderAccountName` at L6969 in this block. Prerequisite: DW-09-01 scope for GetSenderAccountName. | P2 | After DW-09-01 | OPEN |
| DW-09-04 | Full skip removal for all 137 remaining obfuscation-skipped tests referencing non-existent methods, after DW-09-01 implements each method, binding flags are verified to match (static vs instance), and ObfuscationAttribute decorations are confirmed present. Each test must be individually validated before Skip removal. Prerequisite: DW-09-01 + DW-09-02 + DW-09-03 all complete. | P2 | After DW-09-01/02/03 | OPEN |

---

## Carried Forward from Prior Blocks

| ID | Item | Priority | Source Block | Status |
|----|------|----------|--------------|--------|
| DW-B7-01 | B79CancelRaceGuardTests: 5th TypeInit test unidentified. Architecture plan required 5 NT8-runtime skips (plan §7 Ticket 2 scope: "Target TypeInit-to-Skip conversions: 6 + 5 + 2 = 13"). RETRY-1 implementation has 4 of 5 for this class. The 5th TypeInit-triggering test must be identified by running `dotnet test --no-build --filter "FullyQualifiedName~B79CancelRaceGuardTests"` on a Wave workspace with NT8 host available, identifying the remaining TypeInit failure by exception message, and applying `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` to that test only. The Director explicitly scoped RETRY-1 to the 3 wrong-reason repairs; the 5th test was deferred. | P1 | PTT-REPAIRS-07-NT8-BULK-SKIP | OPEN |

---

## Rollup Summary

Total OPEN items: 5
Total CLOSED items: 0
