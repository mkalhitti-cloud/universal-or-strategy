# Deferred Backlog
**Repository**: universal-or-strategy (PTT / PropTraderTools)
**Maintained by**: ptt-plan-reviewer (Phase 5)
**Last updated**: Epic PTT-REPAIRS-DW-F-R06

---

## Active Entries

### PTT-REPAIRS-DW-F-R06

| Field | Value |
|-------|-------|
| **ID** | DW-F-R06-01 |
| **Epic** | PTT-REPAIRS-DW-F-R06 |
| **Item** | EvictDedup_CancelledEntry_ClearsLastLeaderDirection NT8 Skip removal |
| **File** | `src/PropTraderTools/CopyEngineTests.cs` line 8059 |
| **Class** | `BwaveCycTaR7HelperTests` |
| **Current state** | `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` |
| **Desired state** | `[Fact]` — Skip attribute removed; test runs and passes in NT8 host |
| **Condition** | When NT8 host isolation harness is available (allows CopyEngine singleton initialization outside NinjaTrader runtime) |
| **Priority** | LOW |
| **Target Block** | future |
| **Status** | OPEN |
| **Classification** | Known limitation. Not a defect. Test logic is mathematically correct per PTT-REPAIRS-DW-F-R06 arch plan §2 data flow proof. The Skip is a test-runner constraint only. No production code is affected. |
| **Introduced by** | PTT-REPAIRS-DW-F-R06 T2 (ticket-2-completion.md, NT8 Skip Fallback Applied: YES) |

---

## Closed Entries

*(none — this is the initial backlog entry for this repository)*

---

## Backlog History

| Epic | Entry ID | Item Summary | Status | Closed by |
|------|----------|--------------|--------|-----------|
| PTT-REPAIRS-DW-F-R06 | DW-F-R06-01 | EvictDedup NT8 Skip removal | OPEN | — |
