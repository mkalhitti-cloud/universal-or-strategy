# B75-LaneA Architecture Plan
**Status**: REVIEW_PASS
**Epic**: B75-LaneA
**Phase**: Retrospective documentation + CYC refactor spec
**Author**: ptt-architect
**Produced**: Phase 1 (Architecture Planning)

---

## Section A — Epic Context

### Block Identity
- **Block**: B75-LaneA
- **Lane**: A (CopyEngine.cs — core dispatch engine)
- **File in Scope**: `src/PropTraderTools/CopyEngine.cs`
- **Prior Blocks**: B72-LaneA (FINAL_PASS), B73-LaneB (FINAL_PASS), B74-LaneC (FINAL_PASS)

### Purpose of This Block
B75-LaneA is a **retrospective architecture plan** documenting all 12 hotfixes applied directly to
`CopyEngine.cs` between B74-LaneC and the current state (B75 baseline), plus the mandatory CYC
refactors required to bring `OnOrderUpdate` and `TryDispatchLeaderFlat` back to CYC <= 8 before
ticket writing.

No new features are introduced. All hotfixes documented here are already in the live source.
The CYC refactor extractions described in Section G are the only net-new code changes this block
authorizes the engineer to write.

### Rules Catalog P0 Gate Result: PASS
- `lock()`: zero occurrences in `CopyEngine.cs` — PASS
- `async void`: zero occurrences — PASS
- `throw new XxxException` in hot paths: zero occurrences — PASS
- `volatile double` / `volatile float`: zero occurrences (only `volatile string`, `volatile bool`,
  `volatile int`, `volatile NinjaTrader.NinjaScript.AtmStrategy` reference — all compliant) — PASS

---

## Section B — Twelve Hotfix Themes

### HOTFIX-B63-FLATTEN-01 — TryDispatchLeaderFlat gate 2.5
**File location**: `TryDispatchLeaderFlat` method, line 1465.

**Change**: Added the following guard as gate (2.5), immediately after the isFollower guard (2):
```csharp
if (orderName != null && orderName.StartsWith("PTT-", StringComparison.Ordinal)) return false;
```

**Why**: PTT-QX-T* partial fills on the leader account, and PTT-BE-Stop fills, were triggering
`TryDispatchLeaderFlat` during the multi-wave incident at 06:35 AM. Any order whose name begins
with "PTT-" is a PTT-owned order (not a user close signal), so flattening followers on a partial
fill is incorrect. Gate position: between gate (2) follower-guard and gate (3) hasOpenPosition.

**CYC impact**: +1 branch on `TryDispatchLeaderFlat`. Running total after this hotfix: CYC=7.

---

### HOTFIX-B63-COPY-CANCEL-01 — OnOrderUpdate B56 block ATM bracket guard
**File location**: `OnOrderUpdate`, line 878.

**Change**: Added `if (IsAtmBracketName(e.Order.Name)) return;` as the first line inside the
`if (e.Order.OrderState == OrderState.Cancelled)` block, before the `CancelOneAccount` loop.

**Why**: When the user presses Chart Trader Close, NT8 cancels all ATM bracket orders
(Stop1/Stop2/Target1 etc.) before cancelling the entry. Each bracket `Cancelled` event was
propagating through `CancelOneAccount`, which wiped the follower's live PTT-Copy entry order
mid-trade. The `IsAtmBracketName` predicate (CYC=3) intercepts these names and exits early.

**CYC impact**: +1 branch on `OnOrderUpdate`.

---

### HOTFIX-B64-ENTRY-FLATTEN-01 — TryDispatchLeaderFlat gate 2.6
**File location**: `TryDispatchLeaderFlat`, line 1466.

**Change**: Added the following guard as gate (2.6), immediately after gate (2.5):
```csharp
if (orderName == "Entry") return false;
```

**Why**: When NT8-ATM fires the "Entry" order fill, `HasOpenPosition` returns false because
NT8 does not update the position model until the next `OnBarUpdate`. Gate (3) therefore passes
incorrectly and flattens followers on every Entry fill. Blocking "Entry" at gate (2.6) eliminates
this race without touching the position model. Gate position: immediately after HOTFIX-B63-FLATTEN-01.

**CYC impact**: +1 branch on `TryDispatchLeaderFlat`. Running total: CYC=8. This exhausts the
CYC budget — any further guard additions require extraction (see Section G).

---

### HOTFIX-B65-GATE-C-FILL-GUARD-01 — OnOrderUpdate Gate C outer condition
**File location**: `OnOrderUpdate`, line 913.

**Change**: Added `&& e.Order.Filled == 0` as a third condition in the Gate C `if`-expression.

**Why**: NT8 can fire concurrent `Accepted` and `Working` state transitions for a partially-filled
order at the same timestamp (observed on Sim102 and live Apex). Both events carried `Filled > 0`,
causing `HandleEntryChange` to cancel the live PTT-Copy follower order that was correctly placed
on the prior `Working` event, effectively flattening the follower mid-fill. The `Filled == 0`
guard prevents Gate C from firing on any mid-fill event.

**CYC impact**: +1 branch in Gate C block on `OnOrderUpdate`.

---

### HOTFIX-B66-COPY-REPLACE — OnOrderUpdate pre-Gate-1 block + new ReplaceFollowerCopyOnAtmCancel
**File location**: `OnOrderUpdate` pre-Gate-1 (lines 827-834); new method `ReplaceFollowerCopyOnAtmCancel`
(line 1398).

**Change (pre-Gate-1 block)**:
```csharp
if (e.Order != null
    && e.Order.OrderState == OrderState.Cancelled
    && (e.Order.Name == "PTT-Copy" || e.Order.Name == "Entry")
    && e.Order.Instrument?.FullName != null
    && e.Order.LimitPrice > 0)
{
    ReplaceFollowerCopyOnAtmCancel(e.Order);
}
```

**Change (new method)**: `ReplaceFollowerCopyOnAtmCancel(Order cancelledOrder)` — CYC=7. Walks
`_rules` to find the follower, verifies leader has open position, verifies no replacement in flight,
then re-fires `SendCopy` (non-Named) or `SendCopyWithAtm` (Named). Uses orderId suffix `"-R"` to
bypass the dedup cache for the replacement.

**Why**: NT8's ATM bracket-arming sweep (`StartAtmStrategy`) cancels all existing follower Limit
orders on the account before placing ATM bracket legs. Nothing in the normal dispatch path detects
or re-places the cancelled follower entry. This pre-Gate-1 path intercepts the cancel before Gate 1
(enabled check) because the cancel originates on the follower account, not the master — Gate 2 would
never match it.

**CYC impact**: `OnOrderUpdate` +1 (single pre-Gate-1 `if`). `ReplaceFollowerCopyOnAtmCancel` is a
new method with CYC=7, well within budget.

---

### HOTFIX-B66-COPY-REPLACE-FIX — ReplaceFollowerCopyOnAtmCancel drag-cancel guard + new HasWorkingPttCopy
**File location**: `ReplaceFollowerCopyOnAtmCancel` line 1421; new method `HasWorkingPttCopy` line 1498.

**Change**: Added gate (6) inside `ReplaceFollowerCopyOnAtmCancel`:
```csharp
if (HasWorkingPttCopy(cancelledOrder.Account, cancelledOrder.Instrument)) return;
```

**New method** `HasWorkingPttCopy(Account acc, Instrument instrument)` — CYC=3. Iterates
`acc.Orders.ToList()` (snapshot prevents `InvalidOperationException`) and returns true if any order
with state `Working|Accepted|Submitted` and name `"PTT-Copy"` or `"Entry"` exists for the instrument.

**Why**: Entry drag (`HandleEntryChange`) fires `Cancel+CreateOrder+Submit` in one synchronous step.
The Cancelled event arrives in `OnOrderUpdate` after the replacement order is already `Working/Accepted`.
The ATM-sweep cancel arrives when no replacement is in flight — the sweep wipes everything.
`HasWorkingPttCopy` discriminates these two scenarios: drag = replacement already in flight → skip;
sweep = nothing in flight → re-place.

**CYC impact**: `ReplaceFollowerCopyOnAtmCancel` +1 for gate (6). `HasWorkingPttCopy` is a new
method with CYC=3.

---

### HOTFIX-B66-NATIVE-ATM — new SendCopyWithAtm + DispatchCopy Named routing
**File location**: New method `SendCopyWithAtm` (line 1576); `DispatchCopy` Named branch updated
(line 1091); `ReplaceFollowerCopyOnAtmCancel` Named branch (line 1432); pre-Gate-1 widened to
catch `"Entry"` cancels.

**Changes**:
- New method `SendCopyWithAtm(Account follower, Instrument instrument, in CopySignal signal, FollowerAtmMode.Named namedMode)` — CYC=4. Creates order with name `"Entry"` (required by NT8 `StartAtmStrategy`), then calls `AtmStrategy.StartAtmStrategy(atmObj, order)` (object overload, preferred) or `StartAtmStrategy(templateName, order)` (string fallback).
- `DispatchCopy` inner loop: Named ATM mode now routes to `SendCopyWithAtm` instead of `SendCopy`.
- `ReplaceFollowerCopyOnAtmCancel`: Named mode also routes to `SendCopyWithAtm`.
- Pre-Gate-1 block: widened from `Name == "PTT-Copy"` to `(Name == "PTT-Copy" || Name == "Entry")` to detect Clone-mode entry cancels.

**Why**: Clone mode must use `name = "Entry"` when calling `StartAtmStrategy` — the NT8 runtime only
arms brackets when the entry order name is exactly "Entry". A bare `"PTT-Copy"` Limit order submitted
via `SendCopy` receives no ATM brackets. The `StartAtmStrategy` static method is confirmed callable
from `AddOnBase` per `NT8_FULL_REFERENCE.md`.

**CYC impact**: `SendCopyWithAtm` is new with CYC=4. `DispatchCopy` and `ReplaceFollowerCopyOnAtmCancel`
routing branches: net zero (replaced existing Named branch, not added to it).

---

### HOTFIX-B67-ENTRY-UNBLOCK — IsExitSignalName: removed "Entry"
**File location**: `IsExitSignalName` (or equivalent name-guard), around line 1001-1023.

**Change**: Removed the guard `if (name == "Entry") return true;` that was added by HOTFIX-B66-NATIVE-ATM.

**Why**: `IsExitSignalName` is called inside `TryDispatchLeaderFlat` gate (3). Adding "Entry" there
caused the leader's own `"Entry"` order fills to bypass the `hasOpenPosition` guard, making
`TryDispatchLeaderFlat` return `true` on every Entry fill — silently blocking all copy dispatch for
the rest of that bar. Gate 2 already ensures only the master account reaches `TryDispatchLeaderFlat`;
follower "Entry" orders (from `SendCopyWithAtm`) never reach Gate 2. The guard was unnecessary and
actively harmful.

**CYC impact**: -1 branch in `IsExitSignalName` or `TryDispatchLeaderFlat`. Net: restored from
over-budget back to budget.

---

### HOTFIX-CLONE-DRAG — FindFollowerEntryOrder: name guard widened
**File location**: `FindFollowerEntryOrder`, line 1267.

**Change**:
```csharp
// Before:
&& order.Name == "PTT-Copy"
// After:
&& (order.Name == "PTT-Copy" || order.Name == "Entry")
```

**Why**: Clone mode places follower entries as `"Entry"` (required by `StartAtmStrategy`).
`HandleEntryChange` calls `FindFollowerEntryOrder` to locate the follower's working entry order for
the drag-propagation path. Because the follower entry is now named `"Entry"`, the old guard never
matched it, making drag propagation a silent no-op for all Clone-mode entries.

**CYC impact**: Zero (`||` inside existing expression — not a separate branch in McCabe terms).

---

### HOTFIX-B66-ATM-OBJ — _cloneAtmObject volatile field + SetCloneAtmObjectCache + SendCopyWithAtm object overload
**File location**: `_cloneAtmObject` field (line 120); `SetCloneAtmObjectCache` (line 443);
`GetCloneAtmMode` (line 453); `SendCopyWithAtm` object-overload path (line 1594).

**Changes**:
- New field: `private volatile NinjaTrader.NinjaScript.AtmStrategy _cloneAtmObject = null;`
- New method: `SetCloneAtmObjectCache(NinjaTrader.NinjaScript.AtmStrategy atmObj)` — CYC=1. Stores live object in `_cloneAtmObject`.
- `GetCloneAtmMode`: priority order — (1) `_cloneAtmObject != null` → return `Named(cache, atmObj)`; (2) `_cloneAtmCache.Length > 0` → return `Named(cache)`; (3) → return `Inherit`. CYC=2.
- `SendCopyWithAtm`: uses `StartAtmStrategy(namedMode.AtmObject, order)` when `AtmObject != null`, else `StartAtmStrategy(namedMode.TemplateName, order)`.

**Why**: `ChartTrader.AtmStrategy.Name` returns the C# class name `"AtmStrategy"`, not the user
template name. Therefore `StartAtmStrategy(string templateName, order)` receives `"AtmStrategy"` as
the template name and silently produces no brackets. The object overload `StartAtmStrategy(AtmStrategy, order)`
bypasses string resolution entirely and uses the live template object. `volatile` on a reference type
is valid and atomic on CLR 4.0+ (JS-023 compliant; NT8-003 bans `volatile double/float`, not
reference types).

**CYC impact**: `SetCloneAtmObjectCache` is new CYC=1. `GetCloneAtmMode` CYC=2 (unchanged).
`SendCopyWithAtm` +1 for the `AtmObject != null` branch.

---

### HOTFIX-B67-CHECKBOX-RESTORE (CopyEngine side only)
**File location**: New method `GetSavedFollowerNames` (line 479).

**Change**: New method:
```csharp
internal HashSet<string> GetSavedFollowerNames(string instrument, string masterName)
```
CYC=2 (foreach rules + foreach followers). Returns a `HashSet<string>` of follower account names
matching the given instrument and master account from `_rules`. Returns empty set, never null
(JS-002 compliant).

**Why**: After NT8 restart, the `TradeCopierPanel` must restore `IsSelected` checkbox states for
follower accounts. It calls `GetSavedFollowerNames` from `OnLoaded` to retrieve which accounts
were configured as followers under a given master, then marks their checkboxes as checked.

**Scope note**: The Panel side (tickets T_B67_01 through T_B67_03) is LaneB scope. Only the
CopyEngine side (T_B67_04 and T_B67_05, which cover this method) is in scope for B75-LaneA.

**CYC impact**: New method CYC=2. Zero impact on existing methods.

---

### DIAG-CLEANUP — DIAG-CancelAll + DIAG-CancelOne removed
**File location**: Throughout `CopyEngine.cs`, all `Output.Process` lines tagged `DIAG-CancelAll`
and `DIAG-CancelOne`.

**Change**: All `Output.Process` diagnostic lines with `DIAG-CancelAll` and `DIAG-CancelOne`
prefixes removed. `[PTT-CLONE]` diagnostic lines retained temporarily per the repair log note
associated with HOTFIX-CLONE-DRAG.

**Why**: Production diagnostics for cancel paths were generating excessive log noise in the NT8
Output tab on every cancel event. They were added for the original B56 cancel investigation and
are no longer needed. `[PTT-CLONE]` diagnostics remain until Clone mode is confirmed stable.

**CYC impact**: Zero.

---

## Section C — Two-Cache Design (_cloneAtmObject)

### Design Rationale
Clone mode requires two independent caches for the ATM strategy reference, serving different purposes.
Conflating them into a single field would force callers to inspect the type before use.

### Cache 1 — _cloneAtmCache (volatile string)
```csharp
private volatile string _cloneAtmCache = string.Empty;
```
- **Purpose**: Display/logging only (shown in status bar, written to diagnostic output).
- **Set by**: `SetCloneAtmCache(string templateName)`, called from `TradeCopierPanel.OnCloneModeClick`.
- **Read by**: `GetCloneAtmMode` fallback branch; `StatusUpdate` formatting.
- **Thread safety**: `volatile string` — reference writes are atomic on CLR 4.0+ (JS-023 compliant).

### Cache 2 — _cloneAtmObject (volatile reference)
```csharp
private volatile NinjaTrader.NinjaScript.AtmStrategy _cloneAtmObject = null;
```
- **Purpose**: Drives dispatch — passed as first argument to `StartAtmStrategy(AtmStrategy, Order)`.
- **Set by**: `SetCloneAtmObjectCache(AtmStrategy atmObj)`, called from `TradeCopierPanel.OnCloneModeClick` alongside `SetCloneAtmCache`.
- **Read by**: `GetCloneAtmMode` primary branch; `SendCopyWithAtm` object-overload path.
- **Thread safety**: `volatile` on a reference type is valid C# and atomic on CLR 4.0+. NT8-003 bans `volatile double/float` — reference types are compliant (see `NT8_FULL_REFERENCE.md`).

### GetCloneAtmMode Priority
```
Priority 1 (preferred): _cloneAtmObject != null
  → return FollowerAtmMode.Named(_cloneAtmCache, _cloneAtmObject)  // object overload

Priority 2 (fallback): _cloneAtmCache.Length > 0
  → return FollowerAtmMode.Named(_cloneAtmCache)                   // string overload

Priority 3 (default): both empty/null
  → return FollowerAtmMode.Inherit()
```

### Why Two Caches
`ChartTrader.AtmStrategy.Name` returns the C# class name `"AtmStrategy"`, not the user-configured
template name. The string overload of `StartAtmStrategy` therefore silently fails when given the
cached string if it was copied from the `.Name` property. The object overload bypasses string
resolution entirely. Both caches are set atomically (same call site in `OnCloneModeClick`), so they
are always consistent.

### Both Set Together
`TradeCopierPanel.OnCloneModeClick`:
```csharp
CopyEngine.Instance.SetCloneAtmCache(templateName);
CopyEngine.Instance.SetCloneAtmObjectCache(atmStrategy);
```

---

## Section D — ReplaceFollowerCopyOnAtmCancel + HasWorkingPttCopy Gate Chain

### Trigger Condition (pre-Gate-1 block)
```csharp
e.Order != null
&& e.Order.OrderState == OrderState.Cancelled
&& (e.Order.Name == "PTT-Copy" || e.Order.Name == "Entry")
&& e.Order.Instrument?.FullName != null
&& e.Order.LimitPrice > 0
```
Fires on **follower** accounts only (master accounts never have "PTT-Copy" or "Entry" orders placed
by this engine). Runs before Gate 1 because the follower account does not appear in Gate 2's
master-account match.

### Gate Chain Inside ReplaceFollowerCopyOnAtmCancel
| Gate | Condition | Action |
|------|-----------|--------|
| (1) | `!_isCopyEnabled` | `return` — engine disabled |
| (2) | Walk `_rules` foreach instrument match + follower-account match | Find rule + followerIndex |
| (3) | `!matchedRule.HasValue \|\| followerIndex < 0` | `return` — not a managed follower order |
| (4) | `leader == null` | `return` — rule has no master |
| (5) | `!HasOpenPosition(leader, instrument)` | `return` — leader flat; normal close cancel |
| (6) | `HasWorkingPttCopy(account, instrument)` | `return` — drag-cancel; replacement in flight |
| (7) | `mode is FollowerAtmMode.Named namedAtm` | Route to `SendCopyWithAtm` |
| (8) | else | Route to `SendCopy` |

### HasWorkingPttCopy Discriminator
```
ATM-sweep cancel scenario:
  Leader has position. Follower "PTT-Copy"/"Entry" cancelled.
  No Working/Accepted/Submitted PTT-Copy or Entry on this account+instrument.
  → HasWorkingPttCopy = false → re-place entry.

Entry-drag cancel scenario:
  HandleEntryChange fires Cancel+CreateOrder+Submit synchronously.
  New replacement order reaches Working/Accepted before Cancelled event arrives.
  → HasWorkingPttCopy = true → skip (replacement already in flight).
```

### OrderId Replacement Key
Re-placed order uses `cancelledOrder.OrderId.ToString() + "-R"` as the dedup key. This bypasses the
existing dedup cache entry for the original order (which is in terminal `Cancelled` state and would
have been evicted by `EvictDedup` at method entry). The `"-R"` suffix ensures no false dedup
collision if the same orderId is reused by NT8 in a subsequent session.

---

## Section E — Full Gate Ordering in OnOrderUpdate

The following is the authoritative gate sequence as of the B75 baseline:

```
OnOrderUpdate entry
│
├── EvictDedup(orderId, state)
│     Always runs. Removes terminal-state entries from _dedupCache.
│
├── HOTFIX-FLAT-DISARM-FOLLOWER
│     if PTT-BE-Stop-* + Filled + not-leader-account:
│       fire PositionStateChanged for follower account
│     (Panel's _beState stays Armed otherwise after follower BE fires)
│
├── PRE-GATE-1: HOTFIX-B66-COPY-REPLACE
│     if Cancelled + (Name=="PTT-Copy" || Name=="Entry") + LimitPrice>0:
│       ReplaceFollowerCopyOnAtmCancel(e.Order)
│     (runs before Gate 1 because follower orders don't pass Gate 2)
│
├── GATE 1: !_isCopyEnabled → return
│
├── GATE 2: find matchedRule by instrument + master account
│     if matchedRule == null → return
│
├── GATE 2.5: !matchedRule.Value.Enabled → return
│
├── TryFirePositionState(e)
│     fires PositionStateChanged for leader account+instrument only
│
├── Mirror mode relay
│     if CopyMode.Mirror: MirrorOrderUpdate(e.Order, matchedRule.Value)
│
├── B56 CANCELLED BLOCK
│     if OrderState.Cancelled:
│       HOTFIX-B63-COPY-CANCEL-01: if IsAtmBracketName(name) → return
│       foreach follower: CancelOneAccount(acc, instrument)
│       return
│
├── TryDispatchLeaderFlat(account, instrument, state, orderName, rule, ...)
│     Internal gates:
│       (1) state != Filled && state != Cancelled → return false
│       (2) isFollower(account) → return false
│       (2.5) HOTFIX-B63: name.StartsWith("PTT-") → return false
│       (2.6) HOTFIX-B64: name == "Entry" → return false
│       (3) !IsNativeExitName(name) && hasOpenPosition(account, instrument) → return false
│       (4) foreach follower: flattenOne(acc, instrument)
│       return true
│     if returned true → return (gate exit)
│
├── GATE B: IsWorkingBracket(e.Order)
│     if true: PopulateOrderMap + HandleBracketChange → return
│
├── GATE C: Limit|StopLimit + Accepted|Working + HOTFIX-B65 Filled==0
│     if TryGetValue(orderId, storedPrice) && |currentPrice-storedPrice| >= tickSize:
│       upsert dedupCache[orderId] = currentPrice
│       HandleEntryChange(order, rule) → return
│
└── DispatchCopy(e.Order, matchedRule.Value)
      fall-through: normal copy dispatch
```

---

## Section F — Open Items Carry-Forward

The following items are READ-ONLY in this block (carry-forward from repair log). No action is
authorized in B75-LaneA tickets. Each requires separate Director resolution.

| ID | Priority | Status | Description |
|----|----------|--------|-------------|
| DW-B66-BE-01 | P1 | OPEN | `CancelQxBrackets` cancels PTT-BE-Stop. Director confirmation needed before adding `IsAtmBracketName` guard to QX cancel path. |
| DW-B66-C-02 | P1 | OPEN | `DispatchCopy` Gate 5 dedup key = `0.0` for all StopLimit entries (because `LimitPrice == 0` for StopLimit orders). Duplicate follower entries possible on repeated StopLimit dispatch. |
| DW-B63-01 | P1 | OPEN | Spurious PTT-Copy bracket orders on Sim102 after ATM fill. Root cause not yet isolated — may be related to HOTFIX-B66-COPY-REPLACE firing on Sim102 when it should not. |
| DW-B54-01 | P1 | OPEN (blocked) | ATM auto-inject blocked: `AtmStrategyCreate()` is `StrategyBase`-only. Not available from `AddOnBase`. Confirmed by `NT8_FULL_REFERENCE.md`. No workaround without StrategyBase host. |

---

## Section G — CYC Refactor Plan (Ph2 Spec)

### Motivation
Two methods in `CopyEngine.cs` exceed the CYC <= 8 Jane Street strict standard as a result of
incremental hotfixes. The engineer must extract predicates to bring both methods back to budget
before any new logic is added in subsequent blocks.

---

### G.1 — OnOrderUpdate: extract IsPttManagedOrderName

**Current CYC**: 10 (hotfix exception, per repair log).

**Root cause**: Each hotfix added one branch inline: `PTT-BE-Stop` guard (+1), pre-Gate-1 compound
condition (+1), Gate C `Filled==0` guard (+1), plus baseline branches from B56 and B62.

**Extraction target**: The "is this a PTT-owned order or follower entry name" test appears in two
independent places within `OnOrderUpdate`:
1. The B56 cancel block calls `IsAtmBracketName(e.Order.Name)`.
2. The pre-Gate-1 block checks `(e.Order.Name == "PTT-Copy" || e.Order.Name == "Entry")`.

These two checks are complementary (bracket detection vs. entry-name detection) and are not the
same predicate — they should remain separate. The CYC reduction must come from restructuring the
outer `if`-conditions, not from merging unrelated guards.

**Method to create**:
```csharp
// src/PropTraderTools/CopyEngine.cs
// Returns true if the order is a PTT-managed entry (copy or ATM clone entry).
// Used in pre-Gate-1 block to detect follower entry cancels eligible for re-placement.
// CYC=1. JS-002: returns bool, never throws.
private static bool IsPttManagedEntryName(string name)
    => name == "PTT-Copy" || name == "Entry";
```

**Refactored pre-Gate-1 block** (replaces the inline compound):
```csharp
if (e.Order != null
    && e.Order.OrderState == OrderState.Cancelled
    && IsPttManagedEntryName(e.Order.Name)
    && e.Order.Instrument?.FullName != null
    && e.Order.LimitPrice > 0)
{
    ReplaceFollowerCopyOnAtmCancel(e.Order);
}
```
The `||` inside `IsPttManagedEntryName` is a single branch in McCabe counting when extracted to a
helper — the call site costs only 1 branch (the `if` condition), not 2.

**CYC saved**: -1 branch on `OnOrderUpdate` (the `||` moves inside the helper).
**Target CYC**: 9. If further reduction to 8 is needed, the `HOTFIX-FLAT-DISARM-FOLLOWER` block
(lines 800-820) should be extracted to `TryFireFollowerBeDisarm(OrderEventArgs e)` (CYC=3 in helper,
saves 2 inline branches). Net result: CYC = 7.

**Preferred approach**: Extract both `IsPttManagedEntryName` AND `TryFireFollowerBeDisarm` in
the same ticket to guarantee CYC <= 8 in one pass.

---

### G.2 — TryDispatchLeaderFlat: extract IsDispatchBlockedOrderName

**Current CYC**: 9 (one over budget after HOTFIX-B64 added gate 2.6).

**Root cause**: Gates (2.5) and (2.6) were added as inline `return false` guards. Combined, they
represent "is this order name blocked from triggering follower flatten?"

**Method to create**:
```csharp
// src/PropTraderTools/CopyEngine.cs
// Returns true if the order name belongs to a PTT-owned or NT8-internal order that must NEVER
// trigger follower flatten, regardless of fill state or position.
// Called from TryDispatchLeaderFlat gates (2.5) and (2.6) combined.
// CYC=2: PTT-prefix check + "Entry" literal check.
// JS-001: no throw. JS-002: returns bool.
private static bool IsDispatchBlockedOrderName(string orderName)
{
    if (orderName == null) return false;
    if (orderName.StartsWith("PTT-", StringComparison.Ordinal)) return true;
    return orderName == "Entry";
}
```

**Refactored TryDispatchLeaderFlat** (gates 2.5 + 2.6 replaced):
```csharp
if (isFollower(account)) return false;                                           // (2)
if (IsDispatchBlockedOrderName(orderName)) return false;                         // (2.5+2.6 combined)
if (!IsNativeExitName(orderName) && hasOpenPosition(account, instrument)) return false; // (3)
```

**CYC saved**: -1 on `TryDispatchLeaderFlat` (two inline branches → one call site branch).
**Target CYC**: 8. Exactly at budget.

---

### G.3 — Summary Table

| Method | Current CYC | Extraction | Post-Extraction CYC |
|--------|-------------|------------|---------------------|
| `OnOrderUpdate` | 10 | Extract `IsPttManagedEntryName` + `TryFireFollowerBeDisarm` | 7 |
| `TryDispatchLeaderFlat` | 9 | Extract `IsDispatchBlockedOrderName` | 8 |
| `IsPttManagedEntryName` | — | New method | 1 |
| `IsDispatchBlockedOrderName` | — | New method | 2 |
| `TryFireFollowerBeDisarm` | — | New method (extract HOTFIX-FLAT-DISARM-FOLLOWER block) | 3 |

All other methods in scope: already CYC <= 8 (verified by inline comments in source).

---

### G.4 — xUnit Tests for Extracted Methods (engineer contract)

The following `[Fact]` tests are mandatory for the extracted helpers:

**IsPttManagedEntryName**:
- `IsPttManagedEntryName_PttCopy_ReturnsTrue`
- `IsPttManagedEntryName_Entry_ReturnsTrue`
- `IsPttManagedEntryName_Close_ReturnsFalse`
- `IsPttManagedEntryName_Null_ReturnsFalse`

**IsDispatchBlockedOrderName**:
- `IsDispatchBlockedOrderName_PttPrefix_ReturnsTrue`
- `IsDispatchBlockedOrderName_PttBeStop_ReturnsTrue`
- `IsDispatchBlockedOrderName_Entry_ReturnsTrue`
- `IsDispatchBlockedOrderName_Close_ReturnsFalse`
- `IsDispatchBlockedOrderName_Null_ReturnsFalse`
- `IsDispatchBlockedOrderName_EmptyString_ReturnsFalse`

**TryFireFollowerBeDisarm** (if extracted):
- Pure logic tests are blocked by NT8 runtime dependency (`Account`, `Instrument`).
  Engineer must note this and supply integration-style tests or skip with `[Fact(Skip="NT8-runtime")]`.

---

## Section H — JS-DNA Compliance Summary

### Jane Street Rules Applied in This Block

| Rule | Category | Application |
|------|----------|-------------|
| JS-021 | Concurrency | No `lock()` anywhere in `CopyEngine.cs`. All shared state uses `volatile`, `ConcurrentDictionary`, `ConcurrentBag`, or single-writer patterns. |
| JS-023 | Concurrency | `_isCopyEnabled`, `_copyModeValue`, `_cloneAtmCache`, `_cloneAtmObject` — all `volatile`. Atomic reference and int writes on CLR 4.0+. |
| JS-025 | Concurrency | `_dedupCache` is `ConcurrentDictionary<string,double>` — lock-free. `_rules` is `ConcurrentBag<CopyRule>` — lock-free. |
| JS-001 | Type Safety | No `throw` in any hot path. `SendCopyWithAtm` uses `try/catch` with status log on failure — does not propagate. `ReplaceFollowerCopyOnAtmCancel` returns void with early exits. |
| JS-002 | Type Safety | No `return null`. All methods return value types, `bool`, `void`, or non-null collections. `GetCloneAtmMode` returns `Inherit` as fallback. `GetSavedFollowerNames` returns empty `HashSet`, not null. |
| JS-010 | Type Safety | `FollowerAtmMode` base constructor is private — no external subclassing. |
| NT8-003 | NT8 Constraints | `volatile double/float` banned. `_cloneAtmObject` is a reference type — compliant. |

### Mutable Struct Compliance
`CopySignal` is passed as `in CopySignal signal` in `SendCopyWithAtm` and `SendCopy` — prevents
accidental mutation of the value type across the call boundary (JS-038 / readonly-in pattern).

### ASCII-Only Compliance
All identifiers, string literals, and diagnostic messages in the hotfixed paths use ASCII-only
characters. `[PTT-CLONE]` log prefixes are ASCII. No FontFamily, no hex color literals, no Unicode.

### DateTime.UtcNow
No `DateTime.Now` usage in the hotfixed code paths. `DateTime.MaxValue` used as the GTC expiry in
`SendCopyWithAtm` (correct NT8 pattern per `NT8_FULL_REFERENCE.md`).

### "PTT-" Order Name Prefix
All CreateOrder calls in this block use names `"PTT-Copy"` or `"Entry"` (the latter required by NT8
`StartAtmStrategy`). The `"Entry"` name does not carry the PTT prefix by NT8 design constraint — this
is documented and acknowledged in HOTFIX-B66-NATIVE-ATM.

### Dispatcher.InvokeAsync
No NT8 UI mutations in `CopyEngine.cs`. All UI-bound events (`StatusUpdate`, `PositionStateChanged`)
are fired as delegates — callers (Panel/Window) are responsible for dispatching to the UI thread.
`CopyEngine` contains no direct UI element references. Compliant.

### Threading Model
- `OnOrderUpdate` fires on NT8's order-event thread (not UI thread, not Dispatcher thread).
- All reads of `_isCopyEnabled`, `_copyModeValue`, `_cloneAtmCache`, `_cloneAtmObject` are
  `volatile` reads — no lock needed, no torn reads possible on CLR 4.0+.
- `acc.Orders.ToList()` snapshot in `HasWorkingPttCopy` and `FindFollowerEntryOrder` prevents
  `InvalidOperationException` from concurrent collection modification.
- `_dedupCache.TryGetValue` and indexer `[]` on `ConcurrentDictionary` are thread-safe without locking.

---

*End of B75-LaneA Architecture Plan.*
