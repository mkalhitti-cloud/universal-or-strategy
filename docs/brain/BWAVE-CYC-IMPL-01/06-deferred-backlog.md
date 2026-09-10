# 06-deferred-backlog.md
# Epic: BWAVE-CYC-IMPL-01

---

## Block: BWAVE-CYC-IMPL-01

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-09-01 | Implement 70 missing CopyEngine helper methods referenced by obfuscation-skipped tests across B79CancelRaceGuardTests, BwaveCycT1R1BeHelperTests, BwaveCycTaR2HelperTests, BwaveCycTaR3HelperTests, BwaveCycTaR6HelperTests. | P1 | BWAVE-CYC-IMPL-01 | **CLOSED** |
| DW-BWAVE-01 | BWAVE-CYC-LOGIC: Implement actual production logic for all 70 stubs (Groups A–E, methods #1–#70, lines 7885–8199 in CopyEngine.cs). All methods are currently reflection-target stubs returning false/null/void/0.0/string.Empty. Full production logic required before skip removal is viable. | P1 | BWAVE-CYC-LOGIC epic | OPEN |

### DW-09-01 Closure Notes

DW-09-01 is CLOSED by this epic. Evidence:

- 70/70 methods inserted in `src/PropTraderTools/CopyEngine.cs` at lines 7877–8199 (Groups A–E)
- 70/70 methods carry `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]`
- Build: 0 errors. Tests: Failed=0, Passed=24, Skipped=490, Total=514 (baseline preserved)
- All 7 DNA scans pass (0 lock/0 throw/0 DateTime.Now/0 non-ASCII/0 FontFamily/0 hex/0 build errors)
- All 5 ticket engineers and 5 independent verifiers confirmed VERIFY_PASS

---

## Carried Forward from PTT-REPAIRS-09-OBFUSC-ATTR/06-deferred-backlog.md

Items DW-09-02, DW-09-03, DW-09-04 carried forward. DW-09-01 is now CLOSED.

| ID | Item | Priority | Source Block | Status |
|----|------|----------|--------------|--------|
| DW-09-02 | Fix binding flags for `LogBeSlotEviction` test in BwaveCycTaR3HelperTests: change `NonPublic\|Instance` → `NonPublic\|Static` in GetMethod call (method is `private static`). Then remove obfuscation Skip from 2 affected tests. Note: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` already added to `LogBeSlotEviction` at L1778 in block PTT-REPAIRS-09-OBFUSC-ATTR. Prerequisite: DW-09-01 confirmed LogBeSlotEviction remains private static. | P2 | After BWAVE-CYC-IMPL-01 | OPEN |
| DW-09-03 | Fix binding flags for `GetSenderAccountName` test in BwaveCycTaR2HelperTests: change `NonPublic\|Instance` → `NonPublic\|Static` in GetMethod call (method is `internal static`). Then remove obfuscation Skip from 1 affected test. Note: `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` already added to `GetSenderAccountName` at L6969 in block PTT-REPAIRS-09-OBFUSC-ATTR. | P2 | After BWAVE-CYC-IMPL-01 | OPEN |
| DW-09-04 | Full skip removal for all 137 remaining obfuscation-skipped tests referencing non-existent methods, after DW-BWAVE-01 implements each method, binding flags are verified to match (static vs instance), and ObfuscationAttribute decorations are confirmed present. Each test must be individually validated before Skip removal. Prerequisite: DW-BWAVE-01 + DW-09-02 + DW-09-03 all complete. | P2 | After DW-BWAVE-01/09-02/09-03 | OPEN |
| DW-B7-01 | B79CancelRaceGuardTests: 5th TypeInit test unidentified. Requires running `dotnet test --no-build --filter "FullyQualifiedName~B79CancelRaceGuardTests"` on a Wave workspace with NT8 host available, identifying the remaining TypeInit failure by exception message, and applying `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` to that test only. Carried from PTT-REPAIRS-07-NT8-BULK-SKIP. | P1 | Next repair block | OPEN |

---

## Rollup Summary

| Status | Count |
|--------|-------|
| CLOSED this block | 1 (DW-09-01) |
| OPEN this block | 1 (DW-BWAVE-01) |
| OPEN carried forward | 4 (DW-09-02, DW-09-03, DW-09-04, DW-B7-01) |
| **Total OPEN** | **5** |
| **Total CLOSED** | **1** |
