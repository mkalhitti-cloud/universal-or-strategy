# BWAVE-NEXT Lane B -- Phase 2 Plan Review

**Plan reviewed**: `docs/brain/BWAVE-NEXT/LaneB/02-architecture-plan.md`
**Spec reviewed**: `docs/brain/BWAVE-NEXT/LaneB-mission-brief.md`
**Option D spec**: `docs/brain/BWAVE-DW/Backlog/DW-NEW-08-naked-fill-race.md`
**Rules bible**: `docs/standards/jane-street/RULES_CATALOG.md`
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-09-04
**Status**: REVIEW_PENDING -> see verdict below

---

## Check Results

### R-01 LANE-SPLIT GATE

**PASS**

Plan Section 1 states `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` on the first line, followed by
explicit Q1/Q2/Q3/Q4 reasoning:

- Q1: NO -- nearest line overlap is 230 lines (ActiveOrders at 3437 vs HandleEntryChange at 3667)
- Q2: YES (weakly) -- T2 calls `ActiveOrders()` established by T4/T5 commit; T1 finalizes that body
- Q3: YES for both -- each ticket has standalone production value
- Q4: YES for both -- each has an independent structural-test verification path
- Gate conclusion: `SEQUENTIAL TICKETS in a SINGLE PIPELINE -- not parallel lanes`

All four Q-answers are present with supporting reasoning. Gate result is correctly stated as
SINGLE-PIPELINE. Requirement satisfied.

---

### R-02 DW-NEXT-A-07 DETERMINATION

**PASS**

Architect chose `AMBIGUOUS-ADDED-TOLIST` (Section 4.1, first line). Reasoning cites:

- `NT8_FULL_REFERENCE.md` Orders Collection section (lines 2800-2844): no explicit thread-safety
  guarantee for lazy LINQ enumeration stated
- `NT8_ADDON_KNOWLEDGE.md` line 219: no thread-safety note on `acc.Orders`
- Three bot reviews (Greptile, cubic, CodeRabbit) all flagged lazy enumeration as a concern
- Director decision from mission brief directly quoted: "If NT8 docs are ambiguous or confirm it
  is NOT safe -- add .ToList()"

The `.ToList()` is placed inside the `ActiveOrders` body only (plan Section 4.1, production code
block). Return type stays `IEnumerable<Order>`. CYC stays 1 (expression body, no new branches).
Callers at lines 3468 and 3668 are unchanged. Determination is grounded in NT8 docs content, not
speculation. Requirement satisfied.

---

### R-03 DW-NEXT-A-06 PLAN

**PASS**

Plan Section 4.2 specifies the exact cast `(long)(int)Environment.TickCount`. Methods named:
`TryNakedDetect` and `NakedPositionDetector`. Line reference: "Per T4 verification artifacts,
these reads are in `NakedPositionDetector` (line 6424+) inside `_nakedDetectLastQueuedTicks.GetOrAdd`
and `.AddOrUpdate` calls (lines ~6434, ~6439)." T4 verification confirms those exact line numbers
(ticket-4-verification.md Step 1 table: `NakedPositionDetector` at line 6424, `.GetOrAdd` at 6434,
`.AddOrUpdate` at 6439). The plan instructs the engineer to `Select-String` for exact lines rather
than hard-coding, which is correct practice when applying to a changing file. No other TickCount
usages are added or changed. Requirement satisfied.

---

### R-04 T2 NEW TYPE PendingDispatchDrain

**PASS**

Plan Section 5.1 shows the sealed class with all required fields:

| Field | Type | Present |
|-------|------|---------|
| FollowerAcctKey | string | YES (property) |
| Instrument | Instrument | YES (property) |
| Qty | int | YES (property) |
| Price | double | YES (property) |
| Action (OrderAction) | OrderAction | YES (property, named `Action`) |
| PendingCancelCount | int | YES (plain field -- required for Interlocked ref) |
| TimestampTicks | long | YES (property) |

`PendingCancelCount` is declared as `internal int PendingCancelCount;` (plain field, not property)
with explicit rationale: `Interlocked.Decrement` requires `ref int`; properties cannot be passed
by ref. `TimestampTicks` is `long`. Class is `private sealed`. Constructor is explicit (NT8-001
CS0518: no `{ get; init; }` required). Section 5.3 also proposes adding `Account FollowerAccount`
field to avoid re-resolution in `SubmitDrainedEntry` -- additive, does not remove any of the 7
required fields. CYC=0 for the data class (no logic methods). Requirement satisfied.

---

### R-05 T2 DrainThenDispatch CYC TARGET 4

**PASS**

Plan Section 5.3 states `CYC=4: follower-null(1) + empty-entries-check(2) + foreach-cancel(3) +
already-in-drain(4)`. Section 6 confirms CYC target=4 in the method summary table.

Flow in plan:
1. `if (follower == null || instrument == null) return;` -- branch (1)
2. Build `entryCandidates` from `ActiveOrders`, then `if (!entryCandidates.Any()) { SubmitEntryDirect(...); return; }` -- branch (2)
3. Overwrite drain payload `_pendingDispatchDrains[acctKey] = new PendingDispatchDrain(...)`
4. `foreach (var e in entryCandidates) { follower.Cancel(...); cancelCount++; }` -- branch (3)
5. `if (cancelCount == 0) { TryRemove; SubmitEntryDirect; return; }` -- branch (4)
6. Log `[DRAIN]`

Uses `follower.Cancel(new Order[] { e })` (Account.Cancel) -- NOT Account.Change(). CYC=4 ≤8.
Requirement satisfied.

---

### R-06 T2 OnDrainCancelAck CYC TARGET 3

**PASS**

Plan Section 5.3 states `CYC=3: drain-check(1) + count-zero(2) + stale-payload(3)`.

Flow:
1. `if (!_pendingDispatchDrains.TryGetValue(acctKey, out var payload)) return;` -- branch (1)
2. `Interlocked.Decrement(ref payload.PendingCancelCount)`
3. `if (remaining < 0) { log underflow; return; }` -- branch (2)
4. `if (remaining == 0) SubmitDrainedEntry(acctKey);` -- branch (3)

Method is `private void OnDrainCancelAck(string acctKey)` -- synchronous void, not async void.
Plan note explicitly states: "NOT subscribed to any event -- called directly from OnOrderUpdate.
Synchronous void." No async void (JS-033 compliant). Plain void event handler exception is moot
here as this is not an event handler -- it is a helper called from within OnOrderUpdate. CYC=3 ≤8.
Requirement satisfied.

---

### R-07 T2 SubmitDrainedEntry CYC TARGET 3

**PASS**

Plan Section 5.3 states `CYC=3: TryRemove fail(1) + follower-resolve fail(2) + order-null(3 -- inside SubmitEntryDirect)`.

Flow:
1. `if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) return;` -- branch (1)
2. Resolve follower Account from `_rules` or from `payload.FollowerAccount` (per revised plan) -- branch (2) if not found return
3. `SubmitEntryDirect(...)` -- delegates further null-guard to SubmitEntryDirect

Uses `Account.CreateOrder()` + `Account.Submit()` (via `SubmitEntryDirect`). Plan Section 5.5
explicitly bans `Account.Change()`, `AtmStrategyCreate()`, `AtmStrategyChangeStopTarget()` and
confirms `Account.Cancel(Order[])`, `Account.CreateOrder(...)`, `Account.Submit(Order[])` as
ALLOWED. No lock(). CYC ≤3. Requirement satisfied.

---

### R-08 T2 Drain Watchdog Piggybacked in OnOrderUpdate

**PASS**

Plan Section 5.3 TryDrainWatchdog flow:
1. `if (_pendingDispatchDrains.IsEmpty) return;` -- fast path guard
2. `long now = (long)(int)Environment.TickCount;` -- same cast as DW-NEXT-A-06 fix
3. `foreach (var kv in _pendingDispatchDrains)` -- loop
4. `if (now - kv.Value.TimestampTicks > 2000L)` -- timestamp check
5. `_pendingDispatchDrains.TryRemove(kv.Key, out _); Log("[DRAIN-TIMEOUT] acctKey")`

Plan Section 5.4 OnOrderUpdate wiring shows `TryDrainWatchdog()` called unconditionally (pre-Gate-1
area, CYC delta=0). Plan explicitly states: "Do NOT submit" in the timeout path -- logs and removes
only. No `System.Threading.Timer` (Section 9 deferred list + plan narrative confirm). TickCount
uses `(long)(int)` cast. Requirement satisfied.

---

### R-09 T2 PropagateFollowerEntryReplace (actual: HandleEntryChange) CYC BUDGET

**PASS**

Plan Section 5.4 correctly identifies the actual method name as `HandleEntryChange` (not
`PropagateFollowerEntryReplace` which is the spec name from DW-NEW-08). This deviation is
documented: "Actual method: `HandleEntryChange` at line ~3667. CYC currently=7."

Plan states current CYC=7 and shows the T2 change REMOVES the `if (order != null)` guard
branch (was branch 7) because `DrainThenDispatch` handles null internally. Post-change CYC=6.
Section 6 CYC budget table confirms:

| Items | CYC |
|-------|-----|
| Original 6 branches retained | 6 |
| Removed: order null guard | -1 |
| After T2 | 6 |

CYC goes 7 → 6. Budget ≤8 satisfied. Deviation from spec name is documented. Requirement satisfied.

---

### R-10 T2 OnOrderUpdate CYC BUDGET

**PASS**

Plan Section 6 OnOrderUpdate CYC table:

| Item | CYC |
|------|-----|
| Gate 1-6 (pre-existing, from T4/T5) | 6 |
| T2 NEW: drain-ack routing | +1 |
| TryDrainWatchdog() unconditional | +0 |
| **After T2** | **7** |

T4 verification (ticket-4-verification.md Step 6, line 164): "CYC budget respected: OnOrderUpdate
CYC unchanged (unconditional call adds 0 branches)". This confirms post-T4/T5 CYC=6. Plan states
+1 for drain-ack routing = CYC=7 ≤8. Watchdog is unconditional (CYC delta=0). Requirement satisfied.

---

### R-11 NT8 API COMPLIANCE

**PASS**

Plan Section 5.5 explicit ban table:

| API | Status in plan |
|-----|---------------|
| Account.Change() | BANNED -- explicit |
| AtmStrategyCreate() | BANNED -- explicit |
| AtmStrategyChangeStopTarget() | BANNED -- explicit |
| Account.Cancel(Order[]) | ALLOWED -- used in DrainThenDispatch |
| Account.CreateOrder(...) | ALLOWED -- used in SubmitEntryDirect |
| Account.Submit(Order[]) | ALLOWED -- used in SubmitEntryDirect |
| lock() | BANNED (JS-021) -- explicit |

Section 0 Rules Catalog Gate also confirms: "Account.Change() NOT used. AtmStrategyCreate() NOT
used. AtmStrategyChangeStopTarget() NOT used." and "No lock() in any new or modified code.
ConcurrentDictionary + Interlocked only." Requirement satisfied.

---

### R-12 FIELD DECLARATION

**PASS**

Plan Section 5.2:

```csharp
private readonly ConcurrentDictionary<string, PendingDispatchDrain> _pendingDispatchDrains =
    new ConcurrentDictionary<string, PendingDispatchDrain>(StringComparer.Ordinal);
```

- Type: `ConcurrentDictionary<string, PendingDispatchDrain>` -- correct
- `readonly` modifier: present
- `StringComparer.Ordinal`: present
- Placement: "immediately after `_nakedDetectLastQueuedTicks` field (currently line 374
  post-T4/T5)" -- T4 verification confirmed `_nakedDetectLastQueuedTicks` at line 373/374

Declared in CopyEngine class body (nested class also in CopyEngine per Section 5.1: "Sealed class,
nested inside `CopyEngine` class body"). Consistent with other ConcurrentDictionary fields in the
class. Requirement satisfied.

---

### R-13 TEST PLAN

**PASS**

**T1 tests** (Section 4.3, file `BwaveNextLaneATests.cs`):
- `[Fact] ActiveOrders_ThreadSafetyVerification()` -- present, structural reflection
- `[Fact] NakedDetector_DebounceField_UsesLongArithmetic()` -- present, structural reflection

**T2 tests** (Section 5.6, file `BwaveNextLaneBTests.cs` -- NEW FILE):
- `[Fact] DrainThenDispatch_CancelsExistingEntryBeforeSubmit()` -- present
- `[Fact] OnDrainCancelAck_SubmitsDrainedEntry_WhenPendingCountReachesZero()` -- present
- `[Fact] DrainWatchdog_ClearsStuckDrain_AfterTimeout()` -- present

All are structural reflection tests requiring no live NT8 runtime. Plan Section 5.6: "No live NT8
Account required." xUnit `[Fact]` only -- plan explicitly excludes NUnit and MSTest (Section 0
rules gate confirms xUnit-only). Test names match mission brief verbatim. Requirement satisfied.

---

### R-14 FILE WRITE-SET COMPLETENESS

**PASS**

Plan Section 7 file write-set table:

| Ticket | File | Change Type |
|--------|------|-------------|
| T1 | CopyEngine.cs | Edit (2 locations) |
| T1 | BwaveNextLaneATests.cs | Add 2 [Fact] methods |
| T2 | CopyEngine.cs | New class + field + 5 methods + 2 method modifications |
| T2 | BwaveNextLaneBTests.cs | NEW FILE -- 3 [Fact] methods |
| T2 | PropTraderTools.csproj | Add compile entry for BwaveNextLaneBTests.cs |
| T3 | docs only | Director action, no pipeline |

New test file `BwaveNextLaneBTests.cs` has csproj entry planned. Sequential ordering stated:
"T4/T5 commit on main → T1 → T1 VERIFY_PASS → T2 → T2 VERIFY_PASS." T1 uses existing test file
from T4/T5 commit (not a new file -- no new csproj entry needed for T1). Requirement satisfied.

---

### R-15 7-SCAN CHECKLIST PRESENT

**PASS**

Plan Section 8 provides all 7 scan commands:

| Scan | Present |
|------|---------|
| SCAN-01 JS-021 lock() | YES -- Select-String `lock\s*\(`, 0 results |
| SCAN-02 JS-033 async void | YES -- Select-String `async void [A-Z]`, 0 results |
| SCAN-03 JS-002 return null | YES -- Select-String `return null`, 0 new |
| SCAN-04 JS-001 throw new | YES -- Select-String `throw new`, 0 results |
| SCAN-05 CYC | YES -- `complexity_audit.py` OR manual count, all ≤8 |
| SCAN-06 ASCII | YES -- Select-String `[^\x00-\x7F]`, 0 results |
| SCAN-07 xUnit | YES -- Select-String `\[Fact\]\|\[Test\]`, only [Fact] |

Section 8 applies to both tickets (header: "applies to both tickets"). Requirement satisfied.

---

### R-16 SPEC REQUIREMENT TRACEABILITY

**PASS**

**DW-NEXT-A-07** (ActiveOrders thread safety): Addressed in T1 Sub-A (Section 4.1).
AMBIGUOUS-ADDED-TOLIST determination documented with NT8 doc line references. ✓

**DW-NEXT-A-06** (TickCount wraparound): Addressed in T1 Sub-B (Section 4.2). Exact cast
`(long)(int)Environment.TickCount` specified. Methods and lines identified. ✓

**DW-NEW-08 Option D criteria**:
- `PendingDispatchDrain` sealed class: Section 5.1 ✓
- `_pendingDispatchDrains` ConcurrentDictionary: Section 5.2 ✓
- `DrainThenDispatch` CYC=4: Sections 5.3 + 6 ✓
- `OnDrainCancelAck` CYC=3: Sections 5.3 + 6 ✓
- `SubmitDrainedEntry` CYC=3: Sections 5.3 + 6 ✓
- Drain watchdog 2s timeout, piggybacked in OnOrderUpdate: Section 5.3 TryDrainWatchdog ✓
- No System.Threading.Timer: confirmed in TryDrainWatchdog narrative ✓
- `HandleEntryChange` (PropagateFollowerEntryReplace) +1 branch, CYC ≤8: Section 5.4 (result: CYC=6) ✓
- `OnOrderUpdate` +1 drain-ack branch, CYC ≤8: Section 5.4 + 6 (result: CYC=7) ✓
- No Account.Change(), AtmStrategyCreate(), AtmStrategyChangeStopTarget(): Section 5.5 ✓

Mission brief T1 acceptance criteria: all addressed. Mission brief T2 acceptance criteria: all
addressed including deferred SIM gate (non-blocking per acceptance criterion explicitly stated).
Requirement satisfied.

---

### R-17 NO SCOPE CREEP

**PASS**

Plan Section 9 Deferred/Out of Scope table explicitly excludes:
- DW-NEXT-A-03 (short positions): EXCLUDED
- DW-NEXT-A-04 (multi-instrument): EXCLUDED
- DW-NEXT-A-05 (edge case entry misclassification): EXCLUDED
- DW-RepairLC-01/02 (SIM gates): EXCLUDED
- DW-C39-09 LaneA (SaveRules, TradeCopierWindow.cs): EXCLUDED
- NEW-0x test quality gaps: EXCLUDED

T3 is "Director action, no pipeline" in the scope table (Section 3) and write-set (Section 7).
Plan does not add any new features or methods beyond the T2 spec. `SubmitEntryDirect` is a helper
extracted from the natural decomposition of DrainThenDispatch -- it is not a new feature, it is
the shared submit path required by both the direct (no drain needed) and drain-complete paths. This
is consistent with the spec: "Call existing Account.CreateOrder() + Submit() pattern." The extraction
reduces CYC by avoiding code duplication -- no scope creep. Requirement satisfied.

---

## Summary

| Check | Result |
|-------|--------|
| R-01 LANE-SPLIT GATE | PASS |
| R-02 DW-NEXT-A-07 DETERMINATION | PASS |
| R-03 DW-NEXT-A-06 PLAN | PASS |
| R-04 T2 PendingDispatchDrain type | PASS |
| R-05 DrainThenDispatch CYC=4 | PASS |
| R-06 OnDrainCancelAck CYC=3 | PASS |
| R-07 SubmitDrainedEntry CYC=3 | PASS |
| R-08 Drain watchdog piggybacked | PASS |
| R-09 HandleEntryChange CYC budget | PASS |
| R-10 OnOrderUpdate CYC budget | PASS |
| R-11 NT8 API compliance | PASS |
| R-12 _pendingDispatchDrains field | PASS |
| R-13 Test plan | PASS |
| R-14 File write-set completeness | PASS |
| R-15 7-scan checklist present | PASS |
| R-16 Spec requirement traceability | PASS |
| R-17 No scope creep | PASS |

**Total checks**: 17
**FAIL count**: 0
**PASS count**: 17

---

## Observations (Non-Blocking)

The following are observations only. They do not affect the verdict.

1. **DrainThenDispatch signature revision**: Section 5.3 contains an internal design evolution
   (signature progresses from `(Account, Instrument, double)` back to `(Account, Instrument, int,
   double, OrderAction, OrderType)`). The final stated signature is explicit: `DrainThenDispatch(acc,
   instrument, fo.Quantity, newPrice, fo.OrderAction, fo.OrderType)`. The engineer should use only
   the final stated form.

2. **PendingDispatchDrain.FollowerAccount field**: Plan Section 5.3 proposes adding `Account
   FollowerAccount` to the payload to avoid re-resolution in `SubmitDrainedEntry`. This is a sound
   additive change. The engineer should include this field in the final class body.

3. **Watchdog CYC count comment discrepancy**: The watchdog CYC comment says `CYC=3: foreach(1) +
   timestamp-check(2) + TryRemove(3)` but the preceding `if (_pendingDispatchDrains.IsEmpty) return;`
   guard is branch (0). The comment counts the wrong set of branches. Actual CYC of TryDrainWatchdog
   with all 3 branches (IsEmpty-guard, foreach, timestamp-check) = 3. This is internally consistent
   and ≤8. No violation.

4. **T4/T5 commit prerequisite**: Plan Section 2 clearly states commit `92a44332` must be on `main`
   before ticket execution. The engineer must verify this before starting T1.

---

## REVIEW_PASS
