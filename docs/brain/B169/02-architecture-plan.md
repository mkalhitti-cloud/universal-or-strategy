# B169 Architecture Plan
## Block: DW-B168-02 — _dragsInFlight HashSet<string> Race Fix
## Status: PLAN_COMPLETE

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

Q1. Same method or within 50 lines?
NO — changes span field declaration (~line 438), TryHandleBracketDrag (~line 2362),
TryCancelFollowerEntries (~line 2276), HandleAtmStopSync (~line 3504),
ResubmitCollateralLegs (~line 3981), dead-code removal (~line 3815),
seam (~line 4171), and tests (~line 10175). Thousands of lines apart.

Q2. Fix B design depends on Fix A final design?
N/A — there is one atomic fix (Design A: HashSet<string>). All six change sites
serve the same invariant and cannot be deployed independently.

Q3. Each fix has standalone value if the other is blocked?
N/A — single fix. Partial deployment leaves system broken:
Add-without-Remove = permanent orphan. Guard-without-Add = guard never fires.

Q4. Each fix has an independent SIM verification path?
N/A — one SIM test covers the full round-trip (three-stop drag, no CancelPending storm).

---

## 1. Root Cause

`IsLeaderLegBeingChanged` reads `leaderLeg.OrderState` at `Accepted` callback time,
but the NT8 SIM broker acknowledges all three dragged stops in the same event batch
before any handler body runs, so all three are already `Accepted` when Stop1's callback
fires and the guard never returns `true`.

---

## 2. Source Read — Exact Line Numbers

All line numbers confirmed by reading `src/PropTraderTools/CopyEngine.cs` directly.

| Label | Symbol | Line(s) |
|-------|--------|---------|
| A | ChangePending Add site — new call in TryHandleBracketDrag BEFORE IsWorkingBracket | 2362 (insert before) |
| B | Accepted Clear site — end of HandleAtmStopSync body, after ResubmitCollateralLegs | 3504 (insert after) |
| C | ChangeRejected Clear site — inside TryTrackDragInFlight (new method), called from A | new method |
| D | Cancelled Clear site — inside TryCancelFollowerEntries, before `return true` at line 2277 | 2277 (inline) |
| E | ResubmitCollateralLegs loop body — current guard at line 3983, FindLeaderCollateralOrder at 3981 | 3981-3984 |
| F | IsLeaderLegBeingChanged definition | 3815-3818 |
| G | IsLeaderLegBeingChangedTestable seam definition | 3820-3824 (including comment) |
| H | Field insertion point — after _laneEDispatched field | after line 438 |

### A — ChangePending dispatch (TryHandleBracketDrag, line 2362)

`TryHandleBracketDrag` runs only for LEADER account orders (after TryResolveEnabledRule gate).
The new `TryTrackDragInFlight(order)` call is inserted BEFORE the `IsWorkingBracket` guard
at line 2362, so it fires for all states including ChangePending and ChangeRejected before
the early return for non-Working/Accepted states.

### B — Accepted clear (HandleAtmStopSync, after line 3504)

Method body (lines 3494-3505) ends after `ResubmitCollateralLegs` call at line 3504.
The clear `_dragsInFlight.Remove(legSuffix)` is inserted between line 3504 and the
closing brace, so it executes AFTER both `SyncAtmFollowerBracket` and
`ResubmitCollateralLegs` complete.

### C — ChangeRejected clear (TryTrackDragInFlight — new method)

The new helper handles both ChangePending (Add) and ChangeRejected (Remove). Called from
TryHandleBracketDrag at site A. The Accepted Remove is separate (site B, end of HandleAtmStopSync).

### D — Cancelled clear (TryCancelFollowerEntries, line 2276-2277)

Current code at line 2276:
```csharp
if (IsAtmBracketName(order.Name))
    return true; // HOTFIX-B63-COPY-CANCEL-01
```
The ATM bracket Cancelled event is consumed here. `TryRemoveDragSuffix(order)` is called
inside this block BEFORE `return true` so the suffix is removed before the early return
swallows the event. This correctly handles "Stop1/2/3" Cancelled.

### E — ResubmitCollateralLegs (lines 3973-3986)

Current order (lines 3981-3984):
```
3981: Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
3982: // DW-B168-01: skip -- concurrent drag for this leg is in flight.
3983: if (IsLeaderLegBeingChanged(leaderLeg))
3984:     continue;
```
New order: `_dragsInFlight.Contains(s)` guard BEFORE `FindLeaderCollateralOrder` call.
`FindLeaderCollateralOrder` (O(n) ToList scan) is only called when the leg is NOT in-flight.

### F — IsLeaderLegBeingChanged

Lines 3815-3818:
```csharp
private static bool IsLeaderLegBeingChanged(Order? leaderLeg) =>
    leaderLeg != null
    && (leaderLeg.OrderState == OrderState.ChangePending
        || leaderLeg.OrderState == OrderState.ChangeSubmitted);
```
REMOVED completely (not commented out).

### G — IsLeaderLegBeingChangedTestable

Lines 3820-3824 (including comment block):
```csharp
// Test seam: exposes private IsLeaderLegBeingChanged for unit tests.
// CYC=1. Same pattern as IsPttStpDragCancellableTestable (line 4141).
// DW-B168-01: nullable param mirrors production signature -- required for Null test case.
internal static bool IsLeaderLegBeingChangedTestable(Order? o) =>
    IsLeaderLegBeingChanged(o);
```
REMOVED completely (not commented out).

### H — _dragsInFlight field

Insertion after line 438 (_laneEDispatched field). New field is placed in the
per-CopyEngine-instance field block.

---

## 3. Suffix Extraction Expression

Confirmed by reading source:

`ParseLegSuffix(order.Name)` (line 3477-3481) is the expression used at all new call sites.

```csharp
private static string ParseLegSuffix(string orderName)
{
    TryParseStopSuffix(orderName, out string stopSuffix);
    return stopSuffix ?? string.Empty;
}
```

`TryParseStopSuffix` (lines 3772-3782):
- Takes the 5th character onward: `raw = stopName.Substring(4)`
- For "Stop1" → raw = "1" → suffix = "1"
- For "Stop2" → raw = "2" → suffix = "2"
- For "Stop3" → raw = "3" → suffix = "3"
- For "PTT-STP-Drag-1" → raw = "STP-Drag-1" → int.TryParse fails → null → empty string

At the Accepted site (inside HandleAtmStopSync), the suffix is already available as the
`legSuffix` parameter (line 3491) — no re-extraction needed.

At ChangePending, ChangeRejected (inside TryTrackDragInFlight), and Cancelled (inside
TryCancelFollowerEntries), the suffix is extracted via `ParseLegSuffix(order.Name)`.

---

## 4. Files Touched and Line-Count Delta

| File | Change | Lines +/- |
|------|--------|-----------|
| src/PropTraderTools/CopyEngine.cs | Add _dragsInFlight field (+2 lines with comment block, +8 for full comment) | +10 |
| | Add TryTrackDragInFlight method (new, ~12 lines) | +12 |
| | Add TryRemoveDragSuffix method (new, ~10 lines) | +10 |
| | Modify TryHandleBracketDrag: add TryTrackDragInFlight call (+1 line) | +1 |
| | Modify TryCancelFollowerEntries: add TryRemoveDragSuffix + block braces (+3 lines) | +3 |
| | Modify HandleAtmStopSync: add _dragsInFlight.Remove at end (+2 lines) | +2 |
| | Modify ResubmitCollateralLegs: reorder guard + replace IsLeaderLegBeingChanged (+0 net, reorder) | 0 |
| | REMOVE IsLeaderLegBeingChanged (lines 3815-3818) | -4 |
| | REMOVE IsLeaderLegBeingChangedTestable (lines 3820-3824) | -5 |
| | Add DragsInFlightForTest seam (+3 lines) | +3 |
| **CopyEngine.cs total** | | **+32 net** |
| src/PropTraderTools/CopyEngineTests.cs | REMOVE DwB168Tests class (lines 10175-10267) | -93 |
| | ADD DwB168_02Tests class (~65 lines) | +65 |
| **CopyEngineTests.cs total** | | **-28 net** |

Total estimated diff: ~60-80 net lines changed. Well within 10,000 char DIFF GUARD.

---

## 5. Full C# Code Samples

### 5a. _dragsInFlight field declaration (insert after line 438)

```csharp
// DW-B168-02: tracks which stop suffixes ("1","2","3") are mid-drag.
// Populated at ChangePending (before broker round-trip) so the set is
// correct at Accepted callback time regardless of broker batch ordering.
// JS-021: no lock -- all NT8 order callbacks for the leader account execute
//   on the same NT8 background thread; concurrent access is impossible.
// JS-036: HashSet<string> -- bounded to max 3 entries (one per stop leg).
// ASCII-only. No Unicode.
private readonly HashSet<string> _dragsInFlight =
    new HashSet<string>(StringComparer.Ordinal);
```

### 5b. TryHandleBracketDrag: call TryTrackDragInFlight (insert at line 2362, before IsWorkingBracket)

Inside `TryHandleBracketDrag`, insert before the existing `if (!IsWorkingBracket(order))` check:

```csharp
// DW-B168-02: track in-flight stop drags at ChangePending / clear at ChangeRejected.
TryTrackDragInFlight(order);
if (!IsWorkingBracket(order))
    return false;
```

### 5c. HandleAtmStopSync Accepted: _dragsInFlight.Remove (insert after line 3504)

At the end of `HandleAtmStopSync` body, after `ResubmitCollateralLegs` call:

```csharp
ResubmitCollateralLegs(acc, fo, newPrice, otherLegPrices, legSuffix, leaderOrder); // DW-B142-QTY-DESYNC-01
// DW-B168-02: remove suffix from in-flight set once Accepted processing is complete.
// This allows the last handler (Stop3) to see a clear set for legs 1 and 2
// and execute the normal ResubmitCollateralLegs path.
if (!string.IsNullOrEmpty(legSuffix))
    _dragsInFlight.Remove(legSuffix);
```

### 5d. TryCancelFollowerEntries Cancelled: call TryRemoveDragSuffix (at line 2276-2277)

Current:
```csharp
if (IsAtmBracketName(order.Name))
    return true; // HOTFIX-B63-COPY-CANCEL-01
```

New:
```csharp
if (IsAtmBracketName(order.Name))
{
    TryRemoveDragSuffix(order); // DW-B168-02: clear in-flight record before event is consumed
    return true; // HOTFIX-B63-COPY-CANCEL-01
}
```

### 5e. New helper TryTrackDragInFlight (new method, insert near line 2368)

```csharp
// DW-B168-02: record or clear drag-in-flight suffix for leader stop orders.
// ChangePending fires before any broker round-trip -- guarantees the set is
// populated before any Accepted callback reads it (immune to NT8 batch-ack race).
// ChangeRejected: drag was rejected -- remove to prevent permanent suppression.
// Only tracks stop legs (not target legs) since _dragsInFlight guards stop resubmit.
// CYC=3: base(1) + if ChangePending(1) + else if ChangeRejected(1).
// JS-021: no lock -- single NT8 callback thread per account. JS-001: no throw.
// JS-002: void. ASCII-only.
private void TryTrackDragInFlight(Order order)
{
    if (!IsStopLeg(order))
        return;
    string suffix = ParseLegSuffix(order.Name);
    if (string.IsNullOrEmpty(suffix))
        return;
    if (order.OrderState == OrderState.ChangePending)    // (1)
        _dragsInFlight.Add(suffix);
    else if (order.OrderState == OrderState.ChangeRejected) // (2)
        _dragsInFlight.Remove(suffix);
}
```

### 5f. New helper TryRemoveDragSuffix (new method, insert near TryTrackDragInFlight)

```csharp
// DW-B168-02: clear drag-in-flight suffix for a Cancelled stop order.
// Called from TryCancelFollowerEntries when an ATM bracket stop (Stop1/2/3) is Cancelled.
// Without this clear, a rejected/cancelled drag orphans the suffix in the set and
// permanently suppresses all future collateral resubmits for that leg.
// CYC=2: base(1) + if suffix empty(1). JS-021: no lock. JS-001: no throw. JS-002: void.
// ASCII-only.
private void TryRemoveDragSuffix(Order order)
{
    string suffix = ParseLegSuffix(order.Name);
    if (!string.IsNullOrEmpty(suffix))    // (1)
        _dragsInFlight.Remove(suffix);
}
```

### 5g. Modified ResubmitCollateralLegs loop body

Full changed region (replaces lines 3979-3985):

```csharp
foreach (var kvp in otherLegPrices) // (1) foreach
{
    string s = kvp.Key.ToString();
    double price = kvp.Value;
    if (price <= 0) // (2) if
        continue;
    // DW-B142-QTY-DESYNC-01 + DW-B168-02 guard.
    // HashSet.Contains is O(1) alloc-free. Skip if this suffix's own
    // HandleAtmStopSync is in flight -- resubmitting here would storm.
    // Guard precedes FindLeaderCollateralOrder (O(n) ToList) to short-circuit.
    if (_dragsInFlight.Contains(s)) // (3) if -- DW-B168-02
        continue;
    Order? leaderLeg = FindLeaderCollateralOrder(leaderOrder, s);
    ResubmitOneCollateralLeg(acc, fo, newPrice, price, s, leaderLeg);
}
```

### 5h. Internal seam for _dragsInFlight access (insert after line 4169)

Following `IsPttStpDragCancellableTestable` pattern at line 4169:

```csharp
// DW-B168-02: test seam for _dragsInFlight. Returns the live instance HashSet for
// test priming (_dragsInFlight.Add/Remove) and assertion (Contains).
// CYC=1: pure property delegation. No NT8 runtime required.
// Different from static seams: _dragsInFlight is instance state, not static.
internal HashSet<string> DragsInFlightForTest() => _dragsInFlight;
```

---

## 6. Methods to REMOVE

### IsLeaderLegBeingChanged — lines 3815-3818

```csharp
private static bool IsLeaderLegBeingChanged(Order? leaderLeg) =>
    leaderLeg != null
    && (leaderLeg.OrderState == OrderState.ChangePending
        || leaderLeg.OrderState == OrderState.ChangeSubmitted);
```
**REMOVED — not commented out.**

### IsLeaderLegBeingChangedTestable — lines 3820-3824 (including comment block)

```csharp
// Test seam: exposes private IsLeaderLegBeingChanged for unit tests.
// CYC=1. Same pattern as IsPttStpDragCancellableTestable (line 4141).
// DW-B168-01: nullable param mirrors production signature -- required for Null test case.
internal static bool IsLeaderLegBeingChangedTestable(Order? o) =>
    IsLeaderLegBeingChanged(o);
```
**REMOVED — not commented out.**

All callers: only `ResubmitCollateralLegs` called `IsLeaderLegBeingChanged` (line 3983).
Only `DwB168Tests` (CopyEngineTests.cs) called `IsLeaderLegBeingChangedTestable`.
Both callers are also removed/replaced in this ticket.

---

## 7. Test Class Signatures

Replace `DwB168Tests` (lines 10175-10267) with `DwB168_02Tests`:

```csharp
// DW-B168-02: Tests for _dragsInFlight HashSet guard via DragsInFlightForTest seam.
// No NT8 runtime required. ASCII-only. No lock(). JS-036: bounded set (max 3 entries).
public class DwB168_02Tests
{
    [Fact]
    public void DragsInFlight_SingleStop_CollateralResubmitted() { ... }

    [Fact]
    public void DragsInFlight_ThreeStop_CollateralSuppressedForInFlightLeg() { ... }

    [Fact]
    public void DragsInFlight_ThreeStop_LastHandlerResubmitsAllLegs() { ... }

    [Fact]
    public void DragsInFlight_ClearOnCancelled_PreventsPermamentSkip() { ... }
}
```

#### DragsInFlight_SingleStop_CollateralResubmitted
Arrange: CopyEngine instance. Call `engine.DragsInFlightForTest()` to confirm empty set.
Act: Do NOT add suffix "2" (single-stop path — only Stop2 in flight, but we test the
     ResubmitCollateralLegs guard for leg "1" which is NOT in-flight).
Assert: `engine.DragsInFlightForTest().Contains("1")` == false → guard does not skip leg 1.
        (Verifies: absent suffix = no skip = collateral resubmit proceeds.)

#### DragsInFlight_ThreeStop_CollateralSuppressedForInFlightLeg
Arrange: CopyEngine instance. Prime set: `engine.DragsInFlightForTest().Add("2"); engine.DragsInFlightForTest().Add("3")`.
Act: Check `engine.DragsInFlightForTest().Contains("2")`.
Assert: returns true → guard fires → collateral resubmit for leg "2" is skipped.

#### DragsInFlight_ThreeStop_LastHandlerResubmitsAllLegs
Arrange: CopyEngine instance. Set is empty (all previous suffixes cleared).
Act: Check `engine.DragsInFlightForTest().Contains("1")` and `.Contains("2")`.
Assert: both return false → guard does not skip legs 1 or 2 → both resubmit proceeds.

#### DragsInFlight_ClearOnCancelled_PreventsPermamentSkip
Arrange: CopyEngine instance. Prime set: `engine.DragsInFlightForTest().Add("1")`.
Act: Simulate ChangeRejected/Cancelled path → `engine.DragsInFlightForTest().Remove("1")`.
Assert: `engine.DragsInFlightForTest().Contains("1")` == false → suffix cleared → no
        permanent suppression.

---

## 8. CYC Audit

| Method | CYC Before | CYC After | Delta | Within <=8? |
|--------|-----------|-----------|-------|-------------|
| HandleAtmStopSync | 2 | 3 | +1 | YES |
| TryHandleBracketDrag | 3 | 3 | 0 (new call adds no branch in parent) | YES |
| TryCancelFollowerEntries | 4 | 4 | 0 (call inside existing if block, no new branch) | YES |
| ResubmitCollateralLegs | 3 | 3 | 0 (guard replaces guard, same branch count) | YES |
| TryTrackDragInFlight (new) | -- | 3 | new | YES |
| TryRemoveDragSuffix (new) | -- | 2 | new | YES |
| IsLeaderLegBeingChanged | 2 | REMOVED | N/A | N/A |
| IsLeaderLegBeingChangedTestable | 1 | REMOVED | N/A | N/A |

Net CYC budget across all touched methods: no method exceeds 8. All within budget.

HandleAtmStopSync CYC detail:
- (1) fo.StopPrice < tickSize guard (existing)
- (2) capturedTargetPrice.HasValue branch (existing)
- (3) if (!string.IsNullOrEmpty(legSuffix)) for Remove (new)
= CYC 3.

---

## 9. Constraint Checklist

- [x] No new lock() anywhere — HashSet<string> used on single NT8 callback thread; no lock needed
- [x] No Unicode in string literals (ASCII-only) — all string literals and identifiers are ASCII
- [x] FindLeaderCollateralOrder AFTER _dragsInFlight.Contains check — see Section 5g
- [x] IsLeaderLegBeingChanged REMOVED (not commented out) — see Section 6
- [x] IsLeaderLegBeingChangedTestable REMOVED (not commented out) — see Section 6
- [x] ptt-sync.ps1 listed as post-change step — run `powershell -File .\scripts\ptt-sync.ps1` after any src/ edit
- [x] No other methods touched — changes confined to: _dragsInFlight field, TryHandleBracketDrag, TryCancelFollowerEntries, HandleAtmStopSync, ResubmitCollateralLegs, two new helpers, one new seam, test class replacement

---

## 10. Component List

### New Components

| Component | Type | File | Line |
|-----------|------|------|------|
| `_dragsInFlight` | `private readonly HashSet<string>` field | CopyEngine.cs | after L438 |
| `TryTrackDragInFlight(Order)` | private void method | CopyEngine.cs | near L2368 |
| `TryRemoveDragSuffix(Order)` | private void method | CopyEngine.cs | near TryTrackDragInFlight |
| `DragsInFlightForTest()` | internal HashSet<string> seam | CopyEngine.cs | after L4169 |
| `DwB168_02Tests` | xUnit test class | CopyEngineTests.cs | replaces L10175-10267 |

### Modified Components

| Component | File | Change |
|-----------|------|--------|
| `TryHandleBracketDrag` | CopyEngine.cs | +1 call to TryTrackDragInFlight before IsWorkingBracket |
| `TryCancelFollowerEntries` | CopyEngine.cs | +1 call to TryRemoveDragSuffix inside IsAtmBracketName block |
| `HandleAtmStopSync` | CopyEngine.cs | +2 lines: Remove(legSuffix) after ResubmitCollateralLegs |
| `ResubmitCollateralLegs` | CopyEngine.cs | Replace IsLeaderLegBeingChanged guard; move FindLeaderCollateralOrder after guard |

### Removed Components

| Component | File | Lines |
|-----------|------|-------|
| `IsLeaderLegBeingChanged` | CopyEngine.cs | 3815-3818 |
| `IsLeaderLegBeingChangedTestable` | CopyEngine.cs | 3820-3824 |
| `DwB168Tests` | CopyEngineTests.cs | 10175-10267 |

---

## 11. NT8 API / Threading Notes

- All `_dragsInFlight` operations (Add, Remove, Contains) execute on the NT8 account
  background thread via `acc.OrderUpdate` callbacks. Per NT8 AddOn threading model,
  callbacks for a single account are serialized on one thread. All Stop1/Stop2/Stop3
  callbacks are for the SAME leader account → single thread → no lock required.
- No `Dispatcher.InvokeAsync` needed: `_dragsInFlight` is never accessed from the UI thread.
- `HashSet<string>` with `StringComparer.Ordinal` — O(1) Contains, bounded to max 3 entries.
- Zero heap allocation on hot path: Contains and Remove do not allocate.

---

## 12. Post-Change Steps (Engineer Contract)

1. Apply all `apply_diff` changes to `src/PropTraderTools/CopyEngine.cs`
2. Apply all `apply_diff` changes to `src/PropTraderTools/CopyEngineTests.cs`
3. Run `powershell -File .\scripts\ptt-sync.ps1` to re-synchronize NT8 hard links
4. Run `powershell -File .\scripts\test_stress.ps1` to confirm tests pass
5. Run `grep -r "lock(" src/` — must return zero matches
6. Run `grep -r "IsLeaderLegBeingChanged" src/` — must return zero matches

---

## Status: PLAN_COMPLETE
