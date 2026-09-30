## DW-NEW-09 -- Stale Terminal Orders Accumulate in Account.Orders Scan

**Source**: Pre-compaction investigation session, 2026-09-04
**Discovered**: Analysis of 14+ cancelled Entry orders visible in PA-04 follower history
  during MGC DEC26 09-03 live trade; compaction-recovered 2026-09-04
**Status**: OPEN -- analysis complete, fix design complete, awaiting wave assignment

---

### Observed Symptom

After an ATM drag-repositioning session with 14+ entry cancel/resubmit cycles, every
`acc.Orders.ToList()` scan in CopyEngine iterates the **full terminal history** of the
account -- Cancelled, Filled, Rejected orders included. On a busy follower account this
list can grow to dozens of stale entries before NT8 garbage-collects them.

The diagnostic log at `TryLogSFBTrace` (line 1958) exposed this clearly:

```
followerOrders=[Entry:Filled, Entry:CancelSubmitted, Entry:Cancelled, Entry:Cancelled,
  Entry:Cancelled, Entry:Cancelled, Entry:Cancelled, Entry:Cancelled, Entry:Cancelled,
  Entry:Cancelled, Entry:Cancelled, Entry:Cancelled, Entry:Cancelled, Entry:Cancelled]
```

14 Cancelled Entry orders + 1 Filled Entry order, 0 Stop or Target orders.

---

### Root Cause

`Account.Orders` in NT8 is a **platform-managed collection that includes all orders
in terminal states** (Cancelled, Filled, Rejected) from the current session. NT8 does
not prune this list on a predictable schedule. PTT has no control over its size.

Every `acc.Orders.ToList()` call in CopyEngine creates a snapshot of this ever-growing
list. There are **25 call sites** across CopyEngine.cs that do this (grep confirmed).

**This is NOT a performance bug** on its own -- `ToList()` is O(n) and the list is
typically small (single-digit orders). The problem is **correctness under high
cancel-cycle load**:

1. `FindFollowerBracketOrder` (line 3452) scans all orders and **returns the first
   Working/Accepted/Submitted/ChangeSubmitted stop or target it finds by name**. With
   14 cancelled entries in the list, the scan is noisy but still correct for brackets
   because the state filter (`OrderState != Working && ... != ChangeSubmitted -> continue`)
   skips terminal orders. The bracket scanner **is already state-safe**.

2. `FindFollowerEntryOrder` (line 3635) scans all orders and returns the first
   **Working or Accepted** Limit/StopLimit named "PTT-Copy" or "Entry". It also has a
   state filter. Also already safe.

3. **The actual risk**: With many stale entries in the list, scan time grows linearly,
   and more importantly: on a future code change that accidentally loosens the state
   filter, stale terminal orders become false positives immediately. The code is
   **structurally fragile** -- its correctness depends entirely on the state filter
   being comprehensive and never loosened.

---

### Is This Garbage Collection Territory?

**No.** This is not GC territory -- that would mean managing memory lifetimes.
`Account.Orders` is an NT8 platform object; PTT cannot and should not free NT8 order
objects.

This is a **bounded scan problem** under the Jane Street model:

| Approach | Jane Street Classification | Cost |
|----------|---------------------------|------|
| Filter at scan time (current) | Cheap, no state, correct today | O(n) linear, brittle on filter relaxation |
| Maintain a bounded active-order set in PTT | Explicit control flow, "make illegal states unrepresentable" | Small ConcurrentDictionary, slightly more state |
| Wrapper that filters before handing to finders | Single fix point, zero caller changes | New 1-line wrapper method |

---

### Recommended Fix: Filter-at-Entry Wrapper (Jane Street: minimal, explicit)

The Jane Street pattern is: **make the illegal input disappear before it reaches logic**.
Rather than relying on every scanner to have a correct state filter, add one authoritative
filter point that produces a clean active-order view.

#### Fix Design

Add one private helper:

```csharp
// ActiveOrders: CYC=1. Returns only orders in non-terminal states.
// Single fix point: all 25 acc.Orders.ToList() callers that care about active orders
// should use this instead. Terminal state orders (Filled/Cancelled/Rejected) are never
// actionable -- filtering them at entry shrinks the scan set and removes fragility.
// JS-021: no lock (LINQ Where is not mutating). JS-002: returns IEnumerable<Order>.
// JS-036: no heap allocation beyond the Where enumerator (lazy, not materialised here).
private static IEnumerable<Order> ActiveOrders(Account acc) =>
    acc.Orders.Where(static o =>
        o.OrderState != OrderState.Filled
        && o.OrderState != OrderState.Cancelled
        && o.OrderState != OrderState.Rejected);
```

Then in the two primary scanners replace the `ToList()` snapshot with `ActiveOrders()`:

**`FindFollowerBracketOrder` (line 3437)**:
```csharp
// Before:
FindFollowerBracketOrder(follower.Orders.ToList(), ...)
// After:
FindFollowerBracketOrder(ActiveOrders(follower), ...)
```

**`FindFollowerEntryOrder` (line 3637)**:
```csharp
// Before:
foreach (var order in follower.Orders.ToList())
// After:
foreach (var order in ActiveOrders(follower))
```

The `IEnumerable<Order>` overload of `FindFollowerBracketOrder` already exists
(line 3452) and accepts `IEnumerable<Order>` -- so no signature change is needed.
`FindFollowerEntryOrder` already iterates with `foreach` -- the change is one token.

---

### Scope: Which of the 25 Call Sites?

NOT all 25 call sites need this change. The fix is targeted:

| Site | Needs ActiveOrders? | Reason |
|------|--------------------|----|
| `FindFollowerBracketOrder` (line 3437) | **YES** | Looking for active brackets |
| `FindFollowerEntryOrder` (line 3637) | **YES** | Looking for active entry order |
| `CancelPttDragOrphansForAccount` (line 1708) | NO | Must see Working/Accepted -- already has `IsPttDragOrphanCancellable` gate |
| `TryLogSFBTrace` (line 1947) | NO | Diagnostic dump -- intentionally shows full history |
| `CancelStaleExitOrders` (line 4845) | NO | Already filters by state + name inside |
| All others | NO | Each has its own state gate, or scans for different purposes |

**Two call-site changes only.** All other sites retain their existing patterns.

---

### CYC Impact

| Method | Before | After | Delta |
|--------|--------|-------|-------|
| `ActiveOrders` | N/A | 1 | +1 (new method) |
| `FindFollowerBracketOrder` (Account overload) | 1 | 1 | 0 (one-liner unchanged) |
| `FindFollowerEntryOrder` | 3 | 3 | 0 (same loop, different input source) |

All targets remain <= 8. Jane Street CYC gate: PASS.

---

### Why Not PartFilled?

`PartFilled` is intentionally excluded from `ActiveOrders` filter above -- it must remain
visible to `FindFollowerBracketOrder` because a partially-filled entry's brackets may
still be in motion. The `OrderState` enum values filtered out are Filled, Cancelled, and
Rejected only -- these are unambiguously terminal.

The MGC PA-04 09-03 log showed `Entry:Filled` and `Entry:CancelSubmitted` but no brackets.
`CancelSubmitted` is NOT terminal and would pass the `ActiveOrders` filter correctly.
The bracket absence was a timing issue (ATM arm incomplete), not a scan issue.

---

### Relationship to DW-NEW-08

DW-NEW-08 (naked fill race) and DW-NEW-09 (stale scan) are **separate problems**:

| Problem | Cause | Fix |
|---------|-------|-----|
| DW-NEW-08 | ATM brackets never placed (race) | Accelerated detection (Option E) + drain (Option D) |
| DW-NEW-09 | Scan iterates terminal orders unnecessarily | `ActiveOrders` filter wrapper |

DW-NEW-09 does NOT fix DW-NEW-08 and vice versa. However, DW-NEW-09 makes the scan
code structurally safer: after the fix, if a similar race happens, the scan set is
smaller and cleaner.

---

### Acceptance Criteria

- [ ] `ActiveOrders(Account)` helper added -- CYC=1, no lock, no heap alloc beyond lazy Where
- [ ] `FindFollowerBracketOrder` Account overload uses `ActiveOrders(follower)` instead of `follower.Orders.ToList()`
- [ ] `FindFollowerEntryOrder` uses `ActiveOrders(follower)` instead of `follower.Orders.ToList()`
- [ ] All other `acc.Orders.ToList()` call sites unchanged
- [ ] `dotnet build` 0 errors, 0 warnings
- [ ] 1 xUnit `[Fact]`: `FindFollowerBracketOrder_SkipsFilledAndCancelledOrders()`
      -- inject list of 14 Cancelled + 1 Working stop order; assert the Working stop is returned
- [ ] 1 xUnit `[Fact]`: `FindFollowerEntryOrder_SkipsFilledAndCancelledEntries()`
      -- inject list with Cancelled "PTT-Copy" + Working "PTT-Copy"; assert only Working returned
- [ ] Jane Street: no lock(), CYC <= 8 all modified methods, ASCII-only, xUnit-only
- [ ] `FindFollowerBracketOrderTestable(IEnumerable<Order>, ...)` test seam (line 3593)
      remains unchanged -- test seam injecting a clean list still works

---

### Wave Assignment

| Priority | Target Wave | Lane | Notes |
|----------|-------------|------|-------|
| P2 | BWAVE-NEXT | Lane A or B cleanup ticket | Small (1 new method + 2 one-line changes + 2 tests). Can be T5 in Lane A or grouped into the cleanup lane. |

Small enough to be a single ticket in any lane. Does not require NT8 restart (no NT8 API
surface change -- purely internal logic). Sync + F5 still required for production file change.

---

*DW-NEW-09 backlog file created: 2026-09-04 | copier-spec mode*
*Source inspected: CopyEngine.cs lines 3430-3484 (FindFollowerBracketOrder), 3635-3654 (FindFollowerEntryOrder), 5033-5063 (EvictDedup), 1700-1722 (CancelPttDragOrphansForAccount), 1940-1960 (TryLogSFBTrace). All 25 .Orders.ToList() call sites grepped.*
