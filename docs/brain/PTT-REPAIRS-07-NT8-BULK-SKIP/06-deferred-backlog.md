# 06-deferred-backlog.md
# Epic: PTT-REPAIRS-07-NT8-BULK-SKIP
# Phase: 5 -- Final Review (initial block)

---

## Block: PTT-REPAIRS-07-NT8-BULK-SKIP

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B7-01 | B79CancelRaceGuardTests: 5th TypeInit test unidentified. Architecture plan required 5 NT8-runtime skips (plan §7 Ticket 2 scope: "Target TypeInit-to-Skip conversions: 6 + 5 + 2 = 13"). RETRY-1 implementation has 4 of 5 for this class. The 5th TypeInit-triggering test must be identified by running `dotnet test --no-build --filter "FullyQualifiedName~B79CancelRaceGuardTests"` on a Wave workspace with NT8 host available, identifying the remaining TypeInit failure by exception message, and applying `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` to that test only. The Director explicitly scoped RETRY-1 to the 3 wrong-reason repairs; the 5th test was deferred. | P1 | B8 or next REPAIRS block | OPEN |

---

## Rollup Summary

Total OPEN items: 1
Total CLOSED items: 0

---
