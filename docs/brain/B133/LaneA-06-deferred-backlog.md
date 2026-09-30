# B133 LaneA — Deferred Backlog

---

## B133 LaneA Block — 2026-08-31

### Resolved

- **DW-B142**: `SignalOrNameMatches` null==null false-positive (ATM drag cancel-all bug) —
  **FIXED** (B133 LaneA, one-line null-guard `signalName != null &&` at `CopyEngine.cs` L2513).
  Root cause: when both `signalName` and `order.FromEntrySignal` were `null` for ATM bracket
  orders, `null == null` evaluated to `true`, matching the first iterated bracket (`Target1`)
  and causing `SyncFollowerBracket` to call `acc.Cancel(Target1)` which OCO-cancelled the entire
  ATM group. Fix prevents this path structurally. Verified by 5 xUnit tests (B133LaneATests)
  and 28 prior regression tests — 0 failures.

### Deferred

- None.

### Pre-existing (observed, not fixed — No Scope Creep Protocol)

- `src/PropTraderTools/Tests/B131Tests.cs:156` — xUnit2004: `Assert.Equal` used for boolean
  comparison; recommended form is `Assert.True`/`Assert.False`. Pre-existing warning, pre-dates
  B133. Not in any file touched by B133 LaneA. Not fixed per No Scope Creep Protocol. Deferred
  to a future B13x test hygiene block.
