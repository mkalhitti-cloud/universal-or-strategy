# PTT-BUGS-LIVE-0911 Lane D — Architecture Plan
## Native NT8 ATM Bracket Protection at Follower Entry (Clone Mode)

**Block:** PTT-BUGS-LIVE-0911-LANE-D  
**Phase:** 1 — Architecture  
**Author:** ptt-architect  
**Date:** 2026-09-11  
**Status:** PLAN_COMPLETE  

---

## LANE-SPLIT GATE RESULT: SINGLE-PIPELINE

| Gate Question | Answer | Evidence |
|---|---|---|
| Q1. Same method or within 50 lines? | YES | All work targets `TryLaneDHook` + `DispatchToFollower` + one new helper — all in `CopyEngine.cs` |
| Q2. Fix B design depends on Fix A final design? | N/A | Only one workstream: implement TryLaneDHook for ATM dispatch |
| Q3. Each fix has standalone value if the other is blocked? | N/A | Single fix |
| Q4. Each fix has an independent SIM verification path? | N/A | Single fix |

Gates Q1=YES, Q2=N/A => **SINGLE-PIPELINE**. No lane split required.

---

## Section 1 — Executive Summary

### What changes
1. `TryLaneDHook` (currently an empty void stub at `CopyEngine.cs:2671`) is implemented with a per-follower ATM dispatch loop for Clone-mode followers.
2. A new private helper method `TryLaneDDispatchFollower` is added to `CopyEngine.cs` to contain per-follower dispatch logic and keep TryLaneDHook CYC low.
3. A new `private readonly ConcurrentDictionary<string, byte> _laneDDispatched` field is added to `CopyEngine` to mark followers dispatched by TryLaneDHook.
4. `DispatchToFollower` receives a one-line skip gate: if the follower was already dispatched by TryLaneDHook, return immediately (prevents double-dispatch).

### What does NOT change
- `DispatchCopy` call site — zero branches added (CYC stays at 8).
- `TryLaneDHook` signature — remains `private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)` (Lane C reflection test preserved).
- `SendCopyWithAtm` — called as-is; no modification needed.
- `GetCloneAtmMode` — called as-is from TryLaneDHook.
- Non-Clone modes (Signal, Mirror) — zero code path changes; TryLaneDHook is an unconditional no-op for those modes.
- `TradeCopierPanel.cs` — no changes.

### What the user sees
When trading in Clone mode on the -09 leader account:
- **Before Lane D**: follower accounts (-05, -07, -08) receive a raw `PTT-Copy` limit order with no stop/target. NT8 ATM brackets do not arm. Position is unprotected.
- **After Lane D**: follower accounts receive an `Entry` limit order immediately followed by `StartAtmStrategy(templateName, order)`. NT8 owns the bracket. Stop and target orders arm the moment the entry fills. The behavior matches a manually-placed ATM entry.

---

## Section 2 — LANE-SPLIT GATE (repeated inline per spec)

See top of document. **LANE-SPLIT GATE RESULT: SINGLE-PIPELINE.**

---

## Section 3 — Current Dispatch Path (Annotated)

```
OnOrderUpdate (NT8 account background thread)
  └─ DispatchCopy(order, rule)
       ├─ [Gate 0.5] IsExitSignalNameOrAnonClose  -->  blocks exit signals
       ├─ [Gate 3]   IsDispatchTriggerState        -->  blocks non-trigger states
       ├─ [Gate 4]   IsDispatchableOrderType       -->  blocks non-Market/Limit
       ├─ [Gate 5]   IsLiveEntryBlocked_Check      -->  dedup guard (no commit yet)
       ├─ CopySignal.Create(...)                   -->  baseSignal
       ├─ ResolveBaseQty(...)                      -->  baseQty
       ├─ TryLaneDHook(order, rule, baseSignal, baseQty)  <== STUB: EMPTY BODY (CYC=1)
       ├─ B119: snapshot instr + currentAction
       ├─ foreach acc in rule.FollowerAccounts
       │    ├─ ShouldSkipFollower(acc, ...) --> skip if reversal guard / null / etc.
       │    └─ DispatchToFollower(acc, order, rule, idx, baseSignal, baseQty)
       │         ├─ scaledSignal = baseSignal * GetMultiplier(rule, idx)
       │         ├─ mode = ResolveAtmMode(rule, acc.Name, instr.FullName)
       │         │     Clone mode: GetCloneAtmMode() -> Named(_cloneAtmCache) if cache non-empty
       │         │                                  -> Inherit if cache empty
       │         ├─ if mode is Named namedAtm:
       │         │     SendCopyWithAtm(acc, instr, scaledSig, namedAtm)
       │         │       CreateOrder(..., "Entry", ...)
       │         │       StartAtmStrategy(namedAtm.TemplateName, order)
       │         └─ else:
       │               SendCopy(acc, instr, scaledSig, mode)
       │                 CreateOrder(..., "PTT-Copy", ...)
       │                 follower.Submit(...)
       └─ if dispatched > 0: SetLiveEntryDispatched(instrKey, orderId)
```

**Current problem**: `TryLaneDHook` is empty. The DispatchToFollower path already routes Clone-mode Named followers through `SendCopyWithAtm` — so ATM dispatch IS architecturally present. However, the hook stub is the designated integration point for this lane, and the existing path has a timing/dedup gap:

- `SetLiveEntryDispatched` is only committed AFTER the loop when `dispatched > 0`.
- If NT8 fires a second `OnOrderUpdate` for the same fill before the loop completes, Gate 5 passes again, TryLaneDHook fires again, and a second ATM entry is dispatched.
- Lane D resolves this by: (a) having TryLaneDHook commit its own dedup after dispatching, and (b) marking dispatched followers in `_laneDDispatched` so DispatchToFollower's loop can skip them.

---

## Section 4 — Proposed Dispatch Path (Annotated)

```
OnOrderUpdate (NT8 account background thread)
  └─ DispatchCopy(order, rule)                         [CYC=8, UNCHANGED]
       ├─ [Gate 0.5-5] unchanged
       ├─ CopySignal.Create / ResolveBaseQty
       ├─ TryLaneDHook(order, rule, baseSignal, baseQty)   <== LANE D: IMPLEMENTED
       │    ├─ if GetCopyMode() != Clone: return            [branch 1]
       │    ├─ if order.OrderType != Limit: return          [branch 2]
       │    ├─ var cloneMode = GetCloneAtmMode()
       │    ├─ if !(cloneMode is Named namedMode): return   [branch 3]
       │    ├─ int laneDCount = 0; int idx = 0;
       │    ├─ foreach acc in rule.FollowerAccounts:
       │    │    if TryLaneDDispatchFollower(acc, rule, idx, order, namedMode, baseSignal, baseQty):
       │    │        laneDCount++                           [branch 4]
       │    │    idx++
       │    └─ if laneDCount > 0:                          [branch 5]
       │         var instrKey = order.Instrument.FullName + "|" + order.OrderAction
       │         SetLiveEntryDispatched(instrKey, order.OrderId.ToString())
       │
       │    TryLaneDDispatchFollower(acc, rule, idx, order, namedMode, baseSignal, baseQty) -> bool
       │    ├─ if acc == null: return false                 [branch A]
       │    ├─ if !PassesDailyCapCheck(acc): return false  [branch B]
       │    ├─ int mult = GetMultiplier(rule, idx)
       │    ├─ var scaledSig = CopySignal.Create(baseSignal.Action, baseSignal.Type,
       │    │       baseQty * mult, baseSignal.LimitPrice, baseSignal.OrderId)
       │    ├─ bool ok = SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode)
       │    ├─ if ok:                                       [branch C]
       │    │    _laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0)
       │    └─ return ok
       │
       ├─ B119: snapshot instr + currentAction
       ├─ foreach acc in rule.FollowerAccounts:
       │    ├─ ShouldSkipFollower --> unchanged
       │    └─ DispatchToFollower(acc, order, rule, idx, baseSignal, baseQty)
       │         ├─ scaledSig = baseSignal * GetMultiplier
       │         ├─ mode = ResolveAtmMode(...)  -> Named (Clone mode)
       │         ├─ [SKIP GATE] key = baseSignal.OrderId + "#" + acc.Name       [new branch]
       │         │  if _laneDDispatched.ContainsKey(key): return   <== double-dispatch guard
       │         ├─ if mode is Named: SendCopyWithAtm(...)   (non-Clone Named: unchanged)
       │         └─ else: SendCopy(...)                      (Market/Inherit: unchanged)
       └─ if dispatched > 0: SetLiveEntryDispatched   (Clone ATM followers: always 0 here; committed by hook)
```

---

## Section 5 — Double-Dispatch Prevention Design

**Mechanism**: Per-order, per-follower dispatch record in `_laneDDispatched`.

### Field
```csharp
// Lane D: dispatch record for TryLaneDHook-handled ATM followers.
// Key: orderId + "#" + accName (ASCII-only). Value: 0 (unused placeholder).
// ConcurrentDictionary: no lock (JS-021). TryAdd: atomic. ContainsKey: lock-free O(1).
// Lifecycle: entries persist for session duration. Growth: O(followers * dispatches/session).
// At 4 PA accounts x ~10 dispatches/day x ~60 bytes/key = ~2.4 KB/session. Acceptable.
private readonly ConcurrentDictionary<string, byte> _laneDDispatched =
    new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
```

### Key construction
```
dedupKey = baseSignal.OrderId + "#" + acc.Name
```
- `baseSignal.OrderId` = `order.OrderId.ToString()` — unique per NT8 order
- `acc.Name` = follower account name (e.g., "PA-APEX-422136-05")
- Separator `#` is ASCII-safe and does not appear in account names or NT8 order IDs

### Write path (TryLaneDDispatchFollower)
```csharp
bool ok = SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode);
if (ok)
    _laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);
return ok;
```
Only written if SendCopyWithAtm succeeds. If it fails (returns false), the follower is NOT marked; DispatchToFollower's existing Named branch serves as fallback.

### Read path (DispatchToFollower — added at line ~2790, before mode dispatch)
```csharp
string dedupKey = baseSignal.OrderId + "#" + acc.Name;
if (_laneDDispatched.ContainsKey(dedupKey)) return;
```
- Returns immediately without dispatching if TryLaneDHook already handled this (acc, order) pair.
- Does NOT modify dispatched counter — ensures loop's dedup commit logic is unaffected for non-ATM followers.

### Dedup commit (TryLaneDHook — at end of hook, after loop)
```csharp
if (laneDCount > 0)
{
    var instrKey = order.Instrument.FullName + "|" + order.OrderAction.ToString();
    SetLiveEntryDispatched(instrKey, order.OrderId.ToString());
}
```
Commits the Gate 5 dedup entry immediately after TryLaneDHook completes. Prevents a second `OnOrderUpdate` for the same fill from passing Gate 5 and re-entering TryLaneDHook.

---

## Section 6 — Per-Follower ATM Mode Resolution (Pseudocode)

```
// In TryLaneDHook:
CopyMode mode = GetCopyMode();
if (mode != CopyMode.Clone) return;  // non-Clone: no-op

OrderType orderType = order.OrderType;
if (orderType != OrderType.Limit) return;  // Market leader: skip ATM dispatch (SendCopyWithAtm hardcodes Limit; Limit@0 is invalid)

FollowerAtmMode cloneAtmMode = GetCloneAtmMode();
// GetCloneAtmMode(): returns Named(_cloneAtmCache) if _cloneAtmCache.Length > 0, else Inherit
// Inherit means: Clone mode active but no ATM template cached (user closed chart, etc.) -> safe no-op

if (!(cloneAtmMode is FollowerAtmMode.Named namedMode)) return;  // Inherit -> no ATM

// namedMode.TemplateName = _cloneAtmCache (set by OnCloneModeClick via GetLeaderAtmTemplateName)
// namedMode.AtmObject = null (string-only Named ctor used by GetCloneAtmMode)
// -> SendCopyWithAtm will use string overload: StartAtmStrategy(namedMode.TemplateName, order)

int laneDCount = 0;
int idx = 0;
foreach (Account acc in rule.FollowerAccounts)
{
    // TryLaneDDispatchFollower handles: null guard, daily cap, multiplier scaling, SendCopyWithAtm, dedup mark
    bool dispatched = TryLaneDDispatchFollower(acc, rule, idx, order, namedMode, baseSignal, baseQty);
    if (dispatched) laneDCount++;
    idx++;
}

if (laneDCount > 0)
{
    // Commit dedup gate: prevents second OnOrderUpdate for same fill from re-entering
    string instrKey = order.Instrument.FullName + "|" + order.OrderAction.ToString();
    SetLiveEntryDispatched(instrKey, order.OrderId.ToString());
}
```

```
// In TryLaneDDispatchFollower:
if (acc == null) return false;
if (!PassesDailyCapCheck(acc)) return false;

int mult = GetMultiplier(rule, idx);
CopySignal scaledSig = CopySignal.Create(
    baseSignal.Action,
    baseSignal.Type,             // always Limit (TryLaneDHook guards this above)
    baseQty * mult,
    baseSignal.LimitPrice,
    baseSignal.OrderId
);

bool ok = SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode);
if (ok)
    _laneDDispatched.TryAdd(baseSignal.OrderId + "#" + acc.Name, 0);
return ok;
```

**Note**: In Clone mode, ALL followers receive the SAME ATM template name (`_cloneAtmCache`). This is correct by design: all followers mirror the leader's ATM configuration.

---

## Section 7 — CYC Analysis

| Method | Branches | CYC | Budget | Status |
|---|---|---|---|---|
| `DispatchCopy` | 8 (unchanged) | 8 | ≤8 | AT LIMIT — UNCHANGED ✓ |
| `TryLaneDHook` (after impl) | 5: (1) mode!=Clone, (2) type!=Limit, (3) !Named, (4) laneDCount++, (5) laneDCount>0 | 6 | ≤8 | ✓ |
| `TryLaneDDispatchFollower` (new) | 3: (A) null acc, (B) cap check, (C) ok guard | 4 | ≤8 | ✓ |
| `DispatchToFollower` (modified) | Prior: ~2 + 1 new (ContainsKey skip gate) | 3 | ≤8 | ✓ |
| All other methods | unchanged | unchanged | n/a | ✓ |

**DW-PTT0911C-02 compliance**: `DispatchCopy` CYC=8 is unchanged. Zero branches added at call site. All Lane D logic lives inside `TryLaneDHook` and its helper. ✓

---

## Section 8 — Thread Safety Analysis

| New/Modified Shared State | Type | Read | Write | Thread-Safety Mechanism |
|---|---|---|---|---|
| `_laneDDispatched` | `ConcurrentDictionary<string, byte>` | `DispatchToFollower` (ContainsKey) | `TryLaneDDispatchFollower` (TryAdd) | ConcurrentDictionary — lock-free ✓ |
| `_cloneAtmCache` | `volatile string` | `GetCloneAtmMode` | `SetCloneAtmCache` (UI thread) | `volatile` — existing, unchanged ✓ |
| `_copyMode` | `volatile int` | `GetCopyMode` | `SetCopyMode` (UI thread) | `volatile` — existing, unchanged ✓ |

**DW-PTT0911C-03 compliance**: 
- TryLaneDHook runs on NT8 account background thread.
- No `lock()` anywhere. ✓
- No `Dispatcher.InvokeAsync` required in TryLaneDHook body: no WPF UI writes. StatusUpdate events are raised from `SendCopyWithAtm` (existing pattern, unchanged). ✓
- `SetLiveEntryDispatched`: already called from DispatchCopy on the same NT8 account thread — thread-safe by existing design. ✓

**JS-021 compliance**: Zero `lock()` in new code. ConcurrentDictionary + volatile reads only. ✓

---

## Section 9 — Non-Clone Mode Safety

**Signal mode / Mirror mode (GetCopyMode() != Clone)**:
- TryLaneDHook: branch 1 fires (`if GetCopyMode() != Clone return;`) → immediate no-op.
- `_laneDDispatched`: no entries added.
- DispatchToFollower: skip gate check fails (ContainsKey=false for all keys) → no skip.
- Existing dispatch path (Named per-rule ATM or raw SendCopy) runs unchanged. ✓

**Named mode followers in Signal/Mirror (per-rule ATM templates)**:
- TryLaneDHook: no-op (not Clone mode).
- DispatchToFollower: Named check at line 2807 routes to SendCopyWithAtm as before. ✓

**Market mode followers**:
- TryLaneDHook: no-op (not Clone mode OR leader order is Market → branch 2).
- DispatchToFollower: `else SendCopy(...)` path runs. ✓

**Inherit mode followers**:
- GetCloneAtmMode returns Inherit when _cloneAtmCache is empty.
- TryLaneDHook: branch 3 fires (`!Named`) → no-op. ✓
- DispatchToFollower: mode is Inherit → `else SendCopy(...)` path. ✓

**Proof of non-regression**: All non-Clone and non-Named paths have zero new code executed. The skip gate in DispatchToFollower is a read-only ContainsKey check that returns false for all pre-existing (non-Lane-D) dispatches. ✓

---

## Section 10 — SendCopyWithAtm Integration Analysis

### Callability from TryLaneDHook
`SendCopyWithAtm` is a `private bool` instance method of `CopyEngine`. `TryLaneDHook` is also a `private void` instance method of `CopyEngine`. They are in the same class → directly callable. ✓

### All required parameters available at TryLaneDHook call site

| SendCopyWithAtm param | Source in TryLaneDHook | Available |
|---|---|---|
| `Account follower` | `rule.FollowerAccounts[idx]` — from rule parameter | ✓ |
| `Instrument instrument` | `order.Instrument` — from order parameter | ✓ |
| `in CopySignal signal` | `CopySignal.Create(baseSignal.*, baseQty*mult)` — from baseSignal + baseQty | ✓ |
| `FollowerAtmMode.Named namedMode` | `GetCloneAtmMode()` cast to Named — from engine state | ✓ |

Nothing is missing. ✓

### DW-PTT0911C-04: StartAtmStrategy call form

`SendCopyWithAtm` (lines 5135–5179) uses the confirmed pattern:
```csharp
var order = follower.CreateOrder(..., "Entry", ...);  // MUST be "Entry" for ATM arming
if (order == null) return false;
if (namedMode.AtmObject != null)                      // object overload preferred
    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.AtmObject, order);
else                                                   // string overload fallback
    NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(namedMode.TemplateName, order);
```
For Clone mode: `namedMode.AtmObject` is always `null` (string-only Named ctor via `GetCloneAtmMode`). String overload is used. `namedMode.TemplateName` = `_cloneAtmCache` = leader's ATM template name. ✓

### DW-PTT0911C-05: ATM template null guard

`namedMode.AtmObject == null` → string overload is used (`StartAtmStrategy(namedMode.TemplateName, order)`).  
Guard is correctly implemented in `SendCopyWithAtm` at line 5160. No change needed. ✓

### Market order constraint

`SendCopyWithAtm` hardcodes `OrderType.Limit` (line 5147). For a Market leader signal, `baseSignal.LimitPrice = 0` and `baseSignal.Type = Market`. Creating `OrderType.Limit` at price 0 would produce an invalid order. Therefore TryLaneDHook guards: `if (order.OrderType != OrderType.Limit) return;` (branch 2). Market leader signals are NOT dispatched via TryLaneDHook — they fall through to the loop's existing SendCopy path which correctly executes them as market orders. ✓

---

## Section 11 — Lane E Wiring Point (Named Future Hook)

Lane E scope: re-arm ATM after QX/BE exit (follower ATM is cancelled; on re-entry, a fresh ATM bracket needs to be armed).

**Named wiring point**: `TryLaneEHook` should be placed in `OnFollowerPositionUpdate` (added in Lane B, `CopyEngine.cs`), specifically at the point where the follower is confirmed flat with no active orders (the gate-open transition). This fires on the NT8 account background thread.

```csharp
// Lane E wiring point: re-arm ATM after QX/BE if Clone mode is active and leader has an open position.
// Add stub: private void TryLaneEHook(Account follower, Instrument instrument) { }
// Call site in OnFollowerPositionUpdate after _followerSlateReady[acc.Name] is set to true.
// Lane E is OUT OF SCOPE for Lane D. This comment documents the wiring point only.
```

**Rationale**: After QX/BE, `_followerSlateReady[acc.Name]` transitions to true in `OnFollowerPositionUpdate`. At that exact moment, the follower is flat + clean. If the leader is already in a position (entered while follower was exiting), Lane E should re-fire `SendCopyWithAtm` for the follower. This is the correct and minimal wiring point.

---

## Section 12 — Spec Requirement Traceability

| Requirement ID | Description | Plan Section |
|---|---|---|
| DW-PTT0911C-01 | Implement TryLaneDHook body | Sections 3, 4, 5, 6 |
| DW-PTT0911C-02 | CYC budget: DispatchCopy stays at 8; TryLaneDHook ≤8 | Section 7 |
| DW-PTT0911C-03 | Thread context: ConcurrentDictionary + volatile only; no lock() | Section 8 |
| DW-PTT0911C-04 | StartAtmStrategy STATIC form; order name "Entry" | Section 10 |
| DW-PTT0911C-05 | ATmObject null guard; prefer object overload | Section 10 |
| JS-021 | No lock() | Sections 8, 9 |
| JS-001 | No throw in dispatch | Section 6 (SendCopyWithAtm has try/catch) |
| JS-002 | No unexpected null return | Section 6 (helper returns bool, never null) |
| JS-023 | No UI update off-thread without Dispatcher | Section 8 |
| BUG: unprotected follower entry | ATM bracket armed at entry | Sections 1, 4 |
| Non-Clone mode safety | Named/Market/Inherit unaffected | Section 9 |

---

## Section 13 — Test Strategy

All tests live in `CopyEngineTests.cs`. 6 new `[Fact]` tests.

### T-LD-01: ATM dispatch fires for Clone+Named follower
```csharp
[Fact(Skip = "NT8-runtime: SendCopyWithAtm requires Account.CreateOrder + StartAtmStrategy NT8 host")]
public void TryLaneDHook_CloneMode_NamedAtm_FollowerReceivesAtmDispatch()
```
**Setup**: CopyEngine.Instance in Clone mode, `_cloneAtmCache = "MES-ATM"`, rule with 1 follower, leader limit order signal.  
**Assert**: `_laneDDispatched` contains key `orderId + "#" + followerName` after TryLaneDHook call via reflection.  
**Verifies**: Lane D dispatch path fires for Clone+Named.

### T-LD-02: Non-Clone mode is no-op — _laneDDispatched stays empty
```csharp
[Fact]
public void TryLaneDHook_SignalMode_IsNoOp_LaneDDispatchedEmpty()
```
**Setup**: CopyEngine.Instance in Signal mode, rule with 1 follower.  
**Assert**: Invoke TryLaneDHook via reflection; `_laneDDispatched.Count == 0`.  
**Verifies**: Non-Clone mode early return.

### T-LD-03: Market order leader — TryLaneDHook is no-op
```csharp
[Fact]
public void TryLaneDHook_MarketOrderType_IsNoOp_LaneDDispatchedEmpty()
```
**Setup**: Clone mode, `_cloneAtmCache = "MES-ATM"`, but order.OrderType = Market.  
**Assert**: Invoke TryLaneDHook via reflection; `_laneDDispatched.Count == 0`.  
**Verifies**: Branch 2 (Market order guard) fires correctly.

### T-LD-04: Double-dispatch prevention — DispatchToFollower skips after LaneD mark
```csharp
[Fact]
public void DispatchToFollower_SkipsFollower_WhenLaneDDispatchedKeyPresent()
```
**Setup**: Pre-populate `_laneDDispatched` with `orderId + "#" + accName` via reflection write.  
**Assert**: Call DispatchToFollower via reflection; verify `SendCopyWithAtm` NOT called (measure via `_laneDDispatched.Count` unchanged, or verify via a spy on a extracted testable interface).  
**Verifies**: Skip gate in DispatchToFollower prevents double-dispatch.

### T-LD-05: _laneDDispatched field exists with correct type
```csharp
[Fact]
public void LaneDDispatched_FieldExists_IsPrivateReadonlyConcurrentDictionary()
```
**Setup**: Reflection — `typeof(CopyEngine).GetField("_laneDDispatched", BindingFlags.NonPublic | BindingFlags.Instance)`.  
**Assert**: Field is non-null, is private, field type is `ConcurrentDictionary<string, byte>`.  
**Verifies**: Field declaration (JS-021, JS-025 compliance).

### T-LD-06: TryLaneDDispatchFollower signature exists
```csharp
[Fact]
public void TryLaneDDispatchFollower_MethodExists_IsPrivateBool()
```
**Setup**: Reflection — `typeof(CopyEngine).GetMethod("TryLaneDDispatchFollower", BindingFlags.NonPublic | BindingFlags.Instance)`.  
**Assert**: Method is non-null; return type is `bool`.  
**Verifies**: Helper method existence and return type contract.

---

## Section 14 — Files Changed

| File | Change |
|---|---|
| `src/PropTraderTools/CopyEngine.cs` | (1) Add `_laneDDispatched` field; (2) Implement `TryLaneDHook` body; (3) Add `TryLaneDDispatchFollower` private method; (4) Add skip gate in `DispatchToFollower` |
| `src/PropTraderTools/CopyEngineTests.cs` | Add 6 new `[Fact]` tests (T-LD-01 through T-LD-06) |
| `src/PropTraderTools/TradeCopierPanel.cs` | **NO CHANGES** |
| Any other file | **NO CHANGES** |

---

## Section 15 — Method Signatures (Exact)

### Existing method (signature UNCHANGED)
```csharp
// CopyEngine.cs ~line 2671
// Signature: DO NOT CHANGE (Lane C reflection test verifies this exact form)
private void TryLaneDHook(Order order, CopyRule rule, CopySignal baseSignal, int baseQty)
```

### New private helper
```csharp
// CopyEngine.cs (new, near TryLaneDHook)
// Lane D: per-follower ATM dispatch helper. CYC=4. JS-001: catch in SendCopyWithAtm, no throw.
// JS-021: no lock. JS-002: returns bool (never null). ASCII-only.
private bool TryLaneDDispatchFollower(
    Account acc,
    CopyRule rule,
    int idx,
    Order order,
    FollowerAtmMode.Named namedMode,
    CopySignal baseSignal,
    int baseQty
)
```

### Existing method (1-line addition only)
```csharp
// CopyEngine.cs ~line 2772
// MODIFICATION: add skip gate as first statement after scaledSig/mode computation
private void DispatchToFollower(
    Account acc,
    Order order,
    CopyRule rule,
    int idx,
    CopySignal baseSignal,
    int baseQty
)
```
Add after `var mode = ResolveAtmMode(rule, acc.Name, order.Instrument.FullName);`:
```csharp
// Lane D skip gate: if TryLaneDHook already dispatched this follower via ATM, skip.
// Prevents double-dispatch (one ATM entry from hook + one raw entry from loop).
// _laneDDispatched is ConcurrentDictionary; ContainsKey is O(1) lock-free. JS-021.
string laneD_key = baseSignal.OrderId + "#" + acc.Name;
if (_laneDDispatched.ContainsKey(laneD_key)) return;
```

### New field
```csharp
// CopyEngine.cs (add near other ConcurrentDictionary fields, ~line 380 region)
// Lane D: dispatch record for TryLaneDHook-handled ATM followers (double-dispatch guard).
// Key: orderId + "#" + accName. Value: 0 (unused). ConcurrentDictionary: JS-021 compliant.
// Growth: O(followers * dispatches/session). ~60 bytes/entry. No cleanup required per session.
private readonly ConcurrentDictionary<string, byte> _laneDDispatched =
    new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
```

---

## Section K — Deferred Work (Out of Scope for Lane D)

| ID | Item | Priority | Target |
|---|---|---|---|
| DW-LD-01 | **Market-order ATM entry**: Clone-mode followers receive raw SendCopy (no ATM bracket) when the leader places a Market order. SendCopyWithAtm hardcodes Limit; Limit@0 is invalid. A Market-compatible ATM dispatch path would require a separate overload or order type routing. Out of scope for Lane D. | P2 | Future lane |
| DW-LD-02 | **_laneDDispatched cleanup**: Entries persist for session lifetime. Growth is bounded and acceptable (~2.4 KB/session for 4 followers). Formal cleanup (e.g., remove entries after loop completes) can be added if growth becomes a concern in high-volume sessions. | P3 | Future |
| DW-LD-03 | **Lane E: Re-arm ATM after QX/BE**: TryLaneEHook wiring point documented in Section 11. Implementation is out of scope for Lane D. | P1 | Lane E |
| DW-PTT0911C-01 | **TryLaneDHook implementation** — **CLOSED by this lane** | — | CLOSED |
| DW-PTT0911C-02 | **Lane D CYC constraint** — **CLOSED by this lane** (Section 7) | — | CLOSED |
| DW-PTT0911C-03 | **Lane D thread context** — **CLOSED by this lane** (Section 8) | — | CLOSED |
| DW-PTT0911C-04 | **StartAtmStrategy overload** — **CLOSED by this lane** (Section 10) | — | CLOSED |
| DW-PTT0911C-05 | **ATM null guard** — **CLOSED by this lane** (Section 10) | — | CLOSED |

All prior OPEN items from B24-B26, Lane A, Lane B, Lane C (DW-B24-01/02/03, DW-B25-01, DW-B26-01, DW-BUG-0911-A-01/02, DW-PTT0911B-01/02/03/04): **unchanged, not addressed in Lane D**.

---

## Section 16 — SCAN Checklist (Pre-flight for Engineer)

| Scan | Check | Expected Result |
|---|---|---|
| SCAN-01 | `grep -r "lock(" src/PropTraderTools/` | 0 matches in new/modified code |
| SCAN-02 | `grep -r "DateTime.Now" src/PropTraderTools/` | 0 matches in new/modified code |
| SCAN-03 | `grep -r "FontFamily" src/PropTraderTools/` | 0 matches |
| SCAN-04 | `grep -rP "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 matches in new code (ASCII-only) |
| SCAN-05 | `grep -r "throw " src/PropTraderTools/` in new methods | 0 matches (no throw in dispatch path) |
| SCAN-06 | CYC audit: TryLaneDHook ≤8, TryLaneDDispatchFollower ≤8, DispatchCopy =8 | All within budget |
| SCAN-07 | E2E SIM test: Clone mode, limit order on -09, verify -05/-07/-08 receive ATM entry + brackets | PASS |

---

*ptt-architect · PTT-BUGS-LIVE-0911 Lane D · 2026-09-11*
