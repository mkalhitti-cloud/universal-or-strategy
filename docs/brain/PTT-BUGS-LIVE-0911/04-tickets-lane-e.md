# PTT-BUGS-LIVE-0911 Lane E — Implementation Tickets
## Re-arm NT8 ATM Bracket Protection After Partial QX/BE Exit

**Status**: TICKETS_COMPLETE  
**Phase**: 3 (Ticket Generation)  
**Author**: ptt-architect  
**Date**: 2026-09-11  
**Source plan**: `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-e.md` (REVIEW_PASS — Cycle 3)

---

## LANE-E-T1 — Re-arm ATM Bracket After QX/BE Partial Exit (Clone Mode)

---

### Scope Lock

This ticket covers **exactly** the following changes to **exactly** these two files:

| File | Change type |
|---|---|
| `src/PropTraderTools/CopyEngine.cs` | 1 new field, 4 new methods, 3 modified methods |
| `src/PropTraderTools/CopyEngineTests.cs` | 10 new [Fact] tests (additive only — zero existing tests modified) |

**Any change not listed above is OUT OF SCOPE. Do not touch any other file.**

---

### Background / Problem Statement

After a partial QX or BE exit on a Clone-mode follower account, the follower's ATM bracket orders are cancelled by `CancelQxBrackets` (existing Lane B behavior). The follower retains a remaining position but has no ATM bracket protection. `TrySetFollowerSlateReady` returns early at its `!IsAccountFlat(acc)` guard, so the follower gate remains permanently closed and no new bracket is placed. Lane E closes this gap by hooking into `OnFollowerPositionUpdate` immediately after `TrySetFollowerSlateReady`, detecting the post-QX/BE remaining-position state via a five-guard chain, and re-arming an ATM bracket via the existing `SendCopyWithAtm` path. A `ConcurrentDictionary` dedup guard (`_laneEDispatched`) prevents double-arm on subsequent position events; the dedup is reset in both `CancelQxBrackets` overloads when the next QX/BE cycle begins.

---

### Spec Requirement IDs

| Req ID | Requirement | Architecture Element | Plan Section |
|---|---|---|---|
| LANE-E-REQ-01 | Re-arm ATM bracket on follower after partial QX/BE exit | `TryLaneEHook` + `TryLaneEDispatchFollower` | §11.5, §11.6 |
| LANE-E-REQ-02 | Trigger only on post-QX/BE remaining-position transition, not first entry or full exit | Five-guard chain in `IsLaneEEligible` (guards A–E) | §4, §11.5a |
| LANE-E-REQ-03 | Reuse existing `SendCopyWithAtm` / `StartAtmStrategy` for re-arm | `TryLaneEDispatchFollower` calls `SendCopyWithAtm` | §3.4, §11.6 |
| LANE-E-REQ-04 | No double-arm on multiple position events for same slot | `_laneEDispatched` ConcurrentDictionary dedup | §7, §11.1 |
| LANE-E-REQ-05 | Dedup reset at start of each new QX/BE cycle | `TryRemove` in both `CancelQxBrackets` overloads | §7, §11.3, §11.4 |
| LANE-E-REQ-06 | Gate opens after successful re-arm so Lane B bracket sync proceeds | `_followerSlateReady[acc.Name] = true` after dispatch in `TryLaneEHook` | §8, §11.5 |
| LANE-E-REQ-07 | Gate integrity: `TrySetFollowerSlateReady` and `TryMarkFollowerSlateNotReady` unchanged | Both methods: zero modification | §8 |
| LANE-E-REQ-08 | Non-Clone modes: complete no-op — zero behavior change | First guard in `IsLaneEEligible`: `GetCopyMode() != CopyMode.Clone` | §10 |
| LANE-E-REQ-09 | No `lock()` anywhere | `ConcurrentDictionary` throughout (JS-021) | §7, §11.1 |
| LANE-E-REQ-10 | No `throw` in dispatch path | All early-return pattern; `SendCopyWithAtm` has its own try/catch (JS-001) | §11.5–11.7 |
| LANE-E-REQ-11 | No WPF update off-thread | `StatusUpdate?.Invoke` stays inside `SendCopyWithAtm` — not in new methods (JS-023) | §3.4 |
| LANE-E-REQ-12 | CYC ≤ 8 per method | All new methods within budget (JS-013) | §9 |
| LANE-E-REQ-13 | ASCII-only identifiers and strings | All string literals ASCII; no Unicode (ASCII-001) | §11 |
| LANE-E-REQ-14 | `StartAtmStrategy` only (not `AtmStrategyCreate`, not `AtmStrategyChangeStopTarget`) | `SendCopyWithAtm` uses static `StartAtmStrategy` only (NT8-007, NT8-008, NT8-009) | §3.4, App.A |

---

### Pre-Implementation Checklist

Before writing any code, the engineer MUST read the following:

1. `docs/brain/PTT-BUGS-LIVE-0911/02-architecture-plan-lane-e.md` — Full REVIEW_PASS plan (Cycle 3). Read sections 3–11 in full.
2. `docs/standards/RULES_CATALOG.md` — JS-001, JS-013, JS-021, JS-023, JS-025, NT8-001, NT8-007, NT8-008, ASCII-001.
3. `src/PropTraderTools/CopyEngine.cs` — Read the following before touching anything:
   - `OnFollowerPositionUpdate` (line ~1020–1027)
   - `TrySetFollowerSlateReady` (line ~966–977) — understand `!IsAccountFlat` early-return
   - `TryMarkFollowerSlateNotReady` (line ~951–957)
   - `CancelQxBrackets` 2-param (line ~1038–1052)
   - `CancelQxBrackets` 3-param (line ~1153–1190)
   - `_laneDDispatched` field declaration (line ~406–411) — Lane E field follows same pattern
   - `TryLaneDHook` (line ~2679–2713) — Lane E hook follows same structural pattern
   - `TryLaneDDispatchFollower` (line ~2713–2753) — Lane E dispatch follows same pattern
   - `SendCopyWithAtm` (line ~5221–5265) — Lane E reuses unchanged
4. **Do not modify** `TrySetFollowerSlateReady`, `TryMarkFollowerSlateNotReady`, or `SendCopyWithAtm`. They are read-only reference material.

---

### JS Rule Constraints (per method)

| Method | Rules | Key constraint |
|---|---|---|
| `_laneEDispatched` field | JS-021, JS-025 | `ConcurrentDictionary<string, byte>` with `StringComparer.Ordinal` — no lock, no plain `Dictionary` |
| `IsLaneEEligible` | JS-001, JS-013, JS-021, ASCII-001 | CYC=8 (exactly at limit). No `throw`. No lock. All `\|\|` operators must live here (not in `TryLaneEHook`). No non-ASCII. |
| `TryLaneEHook` | JS-001, JS-013, JS-021, JS-023, ASCII-001 | CYC=5. No `throw`. No lock. No WPF update. Zero `\|\|`/`&&` operators — all compound guards delegated to `IsLaneEEligible`. |
| `TryLaneEDispatchFollower` | JS-001, JS-013, JS-021, ASCII-001, NT8-007 | CYC=5. No `throw`. No lock. Calls `SendCopyWithAtm` (existing try/catch covers NT8 errors). No direct `StartAtmStrategy` call (delegated). `?:` ternary action assignment counts +1 per JS-013. |
| `BuildLaneEKey` | JS-013, JS-021, ASCII-001 | CYC=2. `private static`. No lock. Null-coalescing guard for `instr` lives here — not in callers. |
| `OnFollowerPositionUpdate` (modified) | JS-001, JS-013, JS-021, JS-023, NT8-001 | CYC unchanged = 3. Adding a void call adds zero branches. No async/await. |
| `CancelQxBrackets` 2-param (modified) | JS-013 | CYC unchanged = 4. `TryRemove(BuildLaneEKey(...))` is a helper call = +0 CYC. |
| `CancelQxBrackets` 3-param (modified) | JS-013 | CYC unchanged = 8 (already at limit). `TryRemove(BuildLaneEKey(...))` is a helper call = +0 CYC. The `??` null-guard lives inside `BuildLaneEKey` — not here. |

---

### Implementation Steps

#### Step 1 — Add `_laneEDispatched` Field

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Insertion point**: Immediately after the `_laneDDispatched` field declaration (~line 411). The new field goes on line 412 (before the blank line that follows `_laneDDispatched`).  
**Anchor**: Find the block:
```csharp
private readonly ConcurrentDictionary<string, byte> _laneDDispatched =
    new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
```
Insert immediately after this closing semicolon:

```csharp
// Lane E: re-arm dedup guard. Prevents double-arm on multiple PositionUpdate events for the
// same post-QX/BE slot. Key: accName + "#LANE-E#" + instrFullName (ASCII-only). Value: 0 (unused).
// ConcurrentDictionary: no lock (JS-021). TryAdd: atomic. TryRemove: called from CancelQxBrackets.
// Growth: O(followers * instruments). ~60 bytes/entry. Cleaned up per QX/BE cycle.
private readonly ConcurrentDictionary<string, byte> _laneEDispatched =
    new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
```

**Verification**: Field must be `private readonly ConcurrentDictionary<string, byte>` with `StringComparer.Ordinal`.

---

#### Step 2 — Modify `OnFollowerPositionUpdate`

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Location**: Line ~1020–1027.  
**Anchor**: Find the exact block:
```csharp
private void OnFollowerPositionUpdate(object sender, PositionEventArgs e)
{
    if (e?.Position?.Account == null)           // (1)
        return;
    if (!IsFollowerAccount(e.Position.Account)) // (2)
        return;
    TrySetFollowerSlateReady(e.Position.Account);
}
```
Add **one line** after the `TrySetFollowerSlateReady(e.Position.Account);` call, before the closing brace:

```csharp
private void OnFollowerPositionUpdate(object sender, PositionEventArgs e)
{
    if (e?.Position?.Account == null)           // (1)
        return;
    if (!IsFollowerAccount(e.Position.Account)) // (2)
        return;
    TrySetFollowerSlateReady(e.Position.Account);
    TryLaneEHook(e.Position.Account, e.Position); // Lane E: re-arm ATM after partial QX/BE
}
```

**CYC impact**: +0 (void call, no branch). CYC remains 3.  
**JS constraint**: No `async`, no WPF update, no `throw`, no `lock`. Passes JS-001, JS-013, JS-021, JS-023, NT8-001.

---

#### Step 3 — Modify `CancelQxBrackets` 2-param Overload

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Location**: Line ~1038–1052.  
**Anchor**: Find the line `TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent` inside the 2-param overload. Add **one line** immediately after it:

```csharp
TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent
_laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _); // Lane E: reset re-arm dedup
```

**CYC impact**: +0 (helper call, not a branch). CYC remains 4.  
**Note**: The null-coalescing guard for `instr` lives inside `BuildLaneEKey` — this call site sees +0 CYC from it.

---

#### Step 4 — Modify `CancelQxBrackets` 3-param Overload

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Location**: Line ~1153–1190.  
**Anchor**: Find the line `TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent (3-param path)` inside the 3-param overload. Add **one line** immediately after it:

```csharp
TryMarkFollowerSlateNotReady(acc); // BUG2: bracket wipe imminent (3-param path)
_laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _); // Lane E: reset re-arm dedup
```

**CYC impact**: +0 (helper call, not a branch). CYC remains 8.  
**Critical**: This overload is already at CYC=8 (the hard limit). The `??` null-guard lives inside `BuildLaneEKey` (CYC=2, separate method). Adding this call charges +0 CYC to this overload. Do NOT inline `instr?.FullName ?? string.Empty` here — that would push CYC to 9 (P0 violation).

---

#### Step 5 — Add `IsLaneEEligible` Method

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Location**: After `TryLaneDDispatchFollower` (~line 2753). Add both `IsLaneEEligible` and `TryLaneEHook` in the same region. Place `IsLaneEEligible` first (called from `TryLaneEHook`).  
**Anchor**: Find the closing brace of `TryLaneDDispatchFollower`. Insert immediately after it.

**Full method** (copy verbatim — no paraphrasing):

```csharp
// Lane E: eligibility predicate for TryLaneEHook. Absorbs all compound boolean guards so
// TryLaneEHook contains zero || or && operators (JS-013 compliance).
// CYC=8: entry(1) + if(1) + if+||(2) + if(1) + if+||(2) + if(1). Exactly at budget.
// JS-001: no throw. JS-021: no lock. JS-002: returns bool, never null.
// ASCII-only. Thread: NT8 account background thread (same as TryLaneEHook).
private bool IsLaneEEligible(Account acc, NinjaTrader.Cbi.Position pos)
{
    // (1): non-Clone mode -> not eligible (Signal/Mirror/Market no-op)
    if (GetCopyMode() != CopyMode.Clone)
        return false;

    // (2): null guard -- || is +1 CYC per JS-013, isolated here intentionally
    if (pos == null || pos.Instrument == null)
        return false;

    // (3): no remaining position -> not a re-arm event (flat or zero-qty update)
    if (pos.Quantity <= 0)
        return false;

    // (4): gate must be closed (false) -- confirms post-QX/BE state.
    // Absent key (first session entry) treated as ready -> returns false -> no-op. Correct.
    // || is +1 CYC per JS-013, isolated here intentionally.
    bool ready;
    if (!_followerSlateReady.TryGetValue(acc.Name, out ready) || ready)
        return false;

    // (5): ATM template must be Named -- Inherit/unconfigured = no-op
    if (!(GetCloneAtmMode(pos.Instrument.FullName) is FollowerAtmMode.Named))
        return false;

    return true;
}
```

**CYC verification**: entry=1, (1)if=1, (2)if=1+(||)=1, (3)if=1, (4)if=1+(||)=1, (5)if=1 → Total = **8** ≤ 8 ✓  
**JS constraints**: JS-001 (no throw), JS-013 (CYC=8), JS-021 (no lock), ASCII-001. All `||` operators are here, not in `TryLaneEHook`.

---

#### Step 6 — Add `TryLaneEHook` Method

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Location**: Immediately after `IsLaneEEligible` (Step 5).

**Full method** (copy verbatim):

```csharp
// Lane E: re-arm ATM hook. Called from OnFollowerPositionUpdate after TrySetFollowerSlateReady.
// Fires when follower has a remaining position (post-partial QX/BE) with gate closed and no ATM brackets.
// CYC=5: (1)IsLaneEEligible gate,(2)dedup check,(3)ok gate-open. All compound guards in IsLaneEEligible.
// JS-001: no throw (early returns + SendCopyWithAtm has its own try/catch). JS-021: no lock.
// JS-023: no UI update -- StatusUpdate raised inside SendCopyWithAtm (existing pattern).
// ASCII-only. Thread: NT8 account background thread (same as OnFollowerPositionUpdate).
private void TryLaneEHook(Account acc, NinjaTrader.Cbi.Position pos)
{
    // Branch (1): eligibility gate -- all compound guards delegated to IsLaneEEligible
    if (!IsLaneEEligible(acc, pos))
        return;

    // Resolve ATM template and dedup key (assignments only, +0 CYC)
    string instrFullName = pos.Instrument.FullName;
    FollowerAtmMode.Named namedMode =
        GetCloneAtmMode(instrFullName) as FollowerAtmMode.Named;
    string key = acc.Name + "#LANE-E#" + instrFullName;

    // Branch (2): dedup check -- only re-arm once per account+instrument slot
    if (_laneEDispatched.ContainsKey(key))
        return;

    // Dispatch: re-arm ATM bracket for the remaining position
    bool ok = TryLaneEDispatchFollower(acc, pos.Instrument, pos, namedMode, key);

    // Branch (3): gate open -- ATM is now armed; bracket sync is safe to proceed
    if (ok)
        _followerSlateReady[acc.Name] = true;
}
```

**CYC verification**: entry=1, (1)if=1, (2)if=1, (3)if=1 → local assignments +0 → Total = **5** ≤ 8 ✓  
**Critical constraint**: `TryLaneEHook` MUST contain zero `||` and zero `&&` operators. All compound guards are in `IsLaneEEligible`. If any `||`/`&&` appears here, CYC will exceed budget and SCAN-02 fails.

---

#### Step 7 — Add `TryLaneEDispatchFollower` Method

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Location**: Immediately after `TryLaneEHook` (Step 6).

**Full method** (copy verbatim):

```csharp
// Lane E: per-follower ATM re-arm helper. Keeps TryLaneEHook CYC low.
// CYC=4: (A) acc null guard, (B) daily cap check, (C) SendCopyWithAtm result.
// JS-001: no throw -- returns false on all failure paths; SendCopyWithAtm has its own try/catch.
// JS-021: no lock -- ConcurrentDictionary.TryAdd is atomic. JS-002: returns bool (never null).
// ASCII-only. Thread: NT8 account background thread (same as TryLaneEHook caller).
private bool TryLaneEDispatchFollower(
    Account acc,
    NinjaTrader.Cbi.Instrument instr,
    NinjaTrader.Cbi.Position pos,
    FollowerAtmMode.Named namedMode,
    string dedupKey
)
{
    // Branch (A): null guard
    if (acc == null)
        return false;

    // Branch (B): daily position cap -- respect existing cap contract
    if (!PassesDailyCapCheck(acc))
        return false;

    // Build signal from follower's remaining position
    OrderAction action = pos.MarketPosition == NinjaTrader.Cbi.MarketPosition.Long
        ? OrderAction.Buy
        : OrderAction.SellShort;
    string syntheticOrderId = "LANE-E#" + acc.Name + "#" + instr.FullName;
    CopySignal sig = CopySignal.Create(
        action,
        OrderType.Limit,
        pos.Quantity,       // remaining quantity (already multiplier-scaled from original entry)
        pos.AveragePrice,   // ATM bracket anchored at original entry price
        syntheticOrderId
    );

    // Branch (C): dispatch ATM entry; mark dedup only on success
    bool ok = SendCopyWithAtm(acc, instr, in sig, namedMode);
    if (ok)
        _laneEDispatched.TryAdd(dedupKey, 0);
    return ok;
}
```

**CYC verification**: entry(1) + A(1) + B(1) + ?:action(1) + C/if(ok)(1) = **5** ≤ 8 ✓.
The `?:` ternary for `action` is +1 per JS-013 as an independent decision point. It cannot be subsumed into another branch's count (JS-013 §1: each `?:` is +1 independently).

**Note on `syntheticOrderId`**: This string is used as the `CopySignal` order ID, not as an NT8 `CreateOrder` name. The "PTT-" order name rule (NT8-003) applies to `CreateOrder` call sites only — this is internal signal ID construction. The `CreateOrder` call inside `SendCopyWithAtm` uses `"Entry"` (NT8-007).

---

#### Step 8 — Add `BuildLaneEKey` Method

**File**: `src/PropTraderTools/CopyEngine.cs`  
**Location**: Immediately after `TryLaneEDispatchFollower` (Step 7). May also be grouped near `_laneEDispatched` field usage — but immediately after `TryLaneEDispatchFollower` is preferred for region coherence.

**Full method** (copy verbatim):

```csharp
// Lane E: dedup key builder. Isolates the instr null-guard so CancelQxBrackets callers
// incur +0 CYC from the null check. Called from both CancelQxBrackets overloads.
// CYC=2: entry(1) + ternary null guard(1). ASCII-only. No lock (JS-021). static: no state.
private static string BuildLaneEKey(Account acc, NinjaTrader.Cbi.Instrument instr)
{
    return acc.Name + "#LANE-E#" + (instr != null ? instr.FullName : string.Empty);
}
```

**CYC verification**: entry=1, ternary `?:`=1 → Total = **2** ≤ 8 ✓  
**Critical**: `acc.Name` is not null-guarded here. Both `CancelQxBrackets` overloads guard `acc` before reaching this call site (via the `TryMarkFollowerSlateNotReady(acc)` call preceding it). Do not add an `acc` null guard — that would push CYC to 3 and contradict the plan.

---

### Field Declarations (Exact)

| Field | Declaration | Location |
|---|---|---|
| `_laneEDispatched` | `private readonly ConcurrentDictionary<string, byte> _laneEDispatched = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);` | After `_laneDDispatched` (~line 412) |

---

### Method Signatures (Exact)

| Method | Signature | Location |
|---|---|---|
| `IsLaneEEligible` | `private bool IsLaneEEligible(Account acc, NinjaTrader.Cbi.Position pos)` | After `TryLaneDDispatchFollower` (~line 2754) |
| `TryLaneEHook` | `private void TryLaneEHook(Account acc, NinjaTrader.Cbi.Position pos)` | After `IsLaneEEligible` |
| `TryLaneEDispatchFollower` | `private bool TryLaneEDispatchFollower(Account acc, NinjaTrader.Cbi.Instrument instr, NinjaTrader.Cbi.Position pos, FollowerAtmMode.Named namedMode, string dedupKey)` | After `TryLaneEHook` |
| `BuildLaneEKey` | `private static string BuildLaneEKey(Account acc, NinjaTrader.Cbi.Instrument instr)` | After `TryLaneEDispatchFollower` |

---

### Test Implementation

**File**: `src/PropTraderTools/CopyEngineTests.cs`  
**Rule**: All 10 tests are **additive only**. Zero existing tests are modified. Add them as a new region `#region Lane E Tests` at the end of the test class.

---

#### T_LANE_E_01

```csharp
[Fact(Skip = "NT8-runtime: requires Account.CreateOrder + StartAtmStrategy NT8 host")]
public void TryLaneEHook_CloneMode_PositionExists_ArmsAtmBracket()
{
    // Asserts: With CopyMode.Clone, gate=false, pos.Quantity=1, Named ATM template configured,
    // TryLaneEHook calls SendCopyWithAtm exactly once and opens _followerSlateReady gate.
    // Verification: _followerSlateReady[acc.Name] == true after call.
    //               _laneEDispatched.ContainsKey(BuildLaneEKey(acc, instr)) == true.
    // Skip reason: SendCopyWithAtm calls account.CreateOrder + StartAtmStrategy — both require
    //              a live NT8 runtime host. Cannot be stubbed without NT8 AddOn infrastructure.
}
```

---

#### T_LANE_E_02

```csharp
[Fact]
public void TryLaneEHook_NonCloneMode_IsNoOp()
{
    // Asserts: With CopyMode.Signal (or Mirror/Market), calling TryLaneEHook with a valid
    // pos (Quantity=1, gate=false) results in NO dispatch and NO gate change.
    // Verification: Use reflection to read _followerSlateReady — must remain false.
    //               _laneEDispatched must be empty.
    // Test is runnable without NT8 runtime: uses reflection to set CopyMode and gate state
    // on a CopyEngine instance. IsLaneEEligible check (1) fires and returns false immediately.
    // No Skip required — pure reflection/state check, no NT8 Account/Position objects needed.
}
```

---

#### T_LANE_E_03

```csharp
[Fact]
public void TryLaneEHook_NullPosition_IsNoOp()
{
    // Asserts: TryLaneEHook(acc, null) returns without throwing, without dispatching,
    // and without opening the gate.
    // Verification: No exception. _laneEDispatched empty. Gate unchanged.
    // No Skip — IsLaneEEligible null guard (2) fires. No NT8 objects required.
}
```

---

#### T_LANE_E_04

```csharp
[Fact]
public void TryLaneEHook_ZeroQuantity_IsNoOp()
{
    // Asserts: With CopyMode.Clone, gate=false, but pos.Quantity=0, TryLaneEHook returns
    // without dispatching and without opening the gate.
    // Verification: _laneEDispatched empty. Gate remains false.
    // No Skip — IsLaneEEligible guard (3) fires. No NT8 Account/Order objects required.
}
```

---

#### T_LANE_E_05

```csharp
[Fact(Skip = "NT8-runtime: TrySetFollowerSlateReady reads Account positions")]
public void TryLaneEHook_GateAlreadyOpen_IsNoOp()
{
    // Asserts: With CopyMode.Clone and _followerSlateReady[acc.Name]=true, TryLaneEHook
    // does NOT dispatch (gate already open = not a re-arm event).
    // Verification: _laneEDispatched contains no new key after call.
    // Skip reason: Setting up _followerSlateReady to true in a verifiable way requires
    //              TrySetFollowerSlateReady to have run, which reads Account.Positions — NT8 runtime.
}
```

---

#### T_LANE_E_06

```csharp
[Fact(Skip = "NT8-runtime: second call after dispatch should be blocked by dedup key")]
public void TryLaneEHook_DoubleArm_DeduplicatedByLaneEDispatched()
{
    // Asserts: Calling TryLaneEHook twice for the same (acc, instr) slot in Clone mode
    // dispatches SendCopyWithAtm exactly once. Second call is blocked by _laneEDispatched dedup.
    // Verification: _laneEDispatched ContainsKey == true after first call.
    //               Second call returns immediately at dedup check (branch 2 in TryLaneEHook).
    // Skip reason: First call requires SendCopyWithAtm (NT8 Account.CreateOrder + StartAtmStrategy).
}
```

---

#### T_LANE_E_07

```csharp
[Fact]
public void BuildLaneEKey_ReturnsExpectedKeyFormat()
{
    // Asserts: BuildLaneEKey(acc, instr) returns "accName#LANE-E#instrFullName" exactly.
    // Example: acc.Name="SIM101", instr.FullName="MNQ 12-26" -> "SIM101#LANE-E#MNQ 12-26"
    // Verification: string equality check.
    // No Skip — pure static string construction. No NT8 runtime required.
    // Access via reflection (method is private static) or promote to internal for test assembly.
}
```

---

#### T_LANE_E_08

```csharp
[Fact]
public void BuildLaneEKey_NullInstrument_ReturnsKeyWithEmptyInstrSuffix()
{
    // Asserts: BuildLaneEKey(acc, null) returns "accName#LANE-E#" (empty instrument suffix).
    // Example: acc.Name="SIM101", instr=null -> "SIM101#LANE-E#"
    // Verification: string equality check. No exception thrown.
    // No Skip — pure static string construction. Null instrument is valid call path
    //           (called from CancelQxBrackets which may have null instr in edge cases).
}
```

---

#### T_LANE_E_09

```csharp
[Fact(Skip = "NT8-runtime: CancelQxBrackets requires live Account/Order objects")]
public void CancelQxBrackets_ResetsLaneEDispatchedKey()
{
    // Asserts: After _laneEDispatched is seeded with a key for (acc, instr), calling
    // CancelQxBrackets(acc, instr) removes the key, allowing re-arm on the next cycle.
    // Verification: _laneEDispatched.ContainsKey(key) == false after CancelQxBrackets call.
    //               A subsequent TryLaneEHook call is NOT blocked by the (now-removed) dedup key.
    // Skip reason: CancelQxBrackets requires live NT8 Account and Order collections to run.
}
```

---

#### T_LANE_E_10

```csharp
[Fact(Skip = "NT8-runtime: IsLaneEEligible reads _followerSlateReady gate state")]
public void IsLaneEEligible_AllGuardsPass_ReturnsTrue()
{
    // Asserts: When all five guards pass (Clone mode, non-null pos, qty>0, gate=false,
    // Named template exists), IsLaneEEligible returns true.
    // Verification: return value == true.
    // Skip reason: Guard (4) requires _followerSlateReady ConcurrentDictionary to have been
    //              explicitly set to false for acc.Name — this state is set by CancelQxBrackets,
    //              which requires live NT8 Account/Order objects to execute correctly.
}
```

---

### 7-Scan Checklist (Layer 1 — Engineer Contract)

The engineer MUST run all 7 scans and record results before opening any PR. The verifier (ptt-verifier, Ph4b) re-runs every scan independently. Any discrepancy is a VERIFY_FAIL.

```
SCAN-01: grep -r "lock(" src/PropTraderTools/ -> must return 0 matches
         (PowerShell equivalent: Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '^\s+lock\s*\(')
         Required: 0 executable lock() statements. Hits inside // comments are acceptable.

SCAN-02: Manual McCabe CYC recount for ALL new/modified methods:
         OnFollowerPositionUpdate  : count if-branches only -> must equal 3 (unchanged)
         TryLaneEHook              : entry(1) + 3 if-branches = must equal 5
         IsLaneEEligible           : entry(1) + 5 if/|| nodes = must equal 8 (exactly at limit)
         TryLaneEDispatchFollower  : entry(1) + 2 if-branches + 1 ternary ?: + 1 if-branch = must equal 5
         BuildLaneEKey             : entry(1) + 1 ternary = must equal 2
         CancelQxBrackets 2-param  : must equal 4 (unchanged)
         CancelQxBrackets 3-param  : must equal 8 (unchanged, at limit)
         Failure condition: ANY method exceeds CYC 8 = P0 violation (JS-013).

SCAN-03: grep -r "\basync\b\|\bawait\b" src/PropTraderTools/ -> 0 in new/modified executable code
         (PowerShell: Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '\basync\b|\bawait\b')
         Required: 0 hits in the 4 new methods and 3 modified methods.

SCAN-04: Non-ASCII byte scan on all modified .cs files -> 0
         PowerShell:
           [System.IO.File]::ReadAllBytes('src\PropTraderTools\CopyEngine.cs') |
               Where-Object { $_ -gt 127 } | Measure-Object | Select-Object -ExpandProperty Count
         Required: 0. Any value > 0 = P0 violation (ASCII-001).

SCAN-05: grep -r "AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/ -> 0 actual calls
         (PowerShell: Select-String -Path 'src\PropTraderTools\CopyEngine.cs'
                       -Pattern 'AtmStrategyCreate|AtmStrategyChangeStopTarget')
         Required: 0 hits in executable code. Hits inside // comments are acceptable.
         These APIs are StrategyBase-only and BANNED in AddOnBase (NT8-008, NT8-009).

SCAN-06: dotnet build -> 0 errors; dotnet test -> 0 failures, 0 regressions vs baseline
         Commands:
           powershell -File .\scripts\build_readiness.ps1
         Required:
           - 0 build errors
           - 0 new test failures
           - All pre-existing passing tests still pass
           - T_LANE_E_07 and T_LANE_E_08 pass (no Skip, pure string tests)
           - T_LANE_E_02, T_LANE_E_03, T_LANE_E_04 pass (no Skip, pure reflection/state tests)
           - T_LANE_E_01,05,06,09,10 are skipped (NT8-runtime) -- 0 failures, 0 errors on these

SCAN-07: Structural verification (all items required -- verify each in diff before PR):
         (a) TryLaneEHook wired in OnFollowerPositionUpdate: the call
             TryLaneEHook(e.Position.Account, e.Position) appears AFTER TrySetFollowerSlateReady call
             and BEFORE the closing brace of OnFollowerPositionUpdate.
         (b) _laneEDispatched field declared as ConcurrentDictionary<string, byte> with
             StringComparer.Ordinal. Must be private readonly. Must appear after _laneDDispatched.
         (c) BuildLaneEKey called in BOTH CancelQxBrackets overloads:
             _laneEDispatched.TryRemove(BuildLaneEKey(acc, instr), out _) must appear in BOTH.
             Appears immediately after TryMarkFollowerSlateNotReady(acc) in each overload.
         (d) IsLaneEEligible called as FIRST statement in TryLaneEHook:
             if (!IsLaneEEligible(acc, pos)) return; must be first executable statement.
         (e) TryLaneEHook opens gate AFTER dispatch (not in TrySetFollowerSlateReady):
             _followerSlateReady[acc.Name] = true appears inside if (ok) block in TryLaneEHook.
             TrySetFollowerSlateReady is byte-for-byte unchanged (diff confirms).
         (f) Non-Clone no-op: IsLaneEEligible first guard is GetCopyMode() != CopyMode.Clone.
             Verified by T_LANE_E_02 passing without Skip.
         (g) All 10 new [Fact] tests present in CopyEngineTests.cs:
             T_LANE_E_01 through T_LANE_E_10 -- all method names must match exactly.
             T_LANE_E_07 and T_LANE_E_08 have NO Skip attribute.
             T_LANE_E_01,05,06,09,10 have [Fact(Skip="NT8-runtime: ...")] attribute.
             T_LANE_E_02,03,04 have plain [Fact] attribute (no Skip).
```

---

### Success Criteria

The engineer session is complete when ALL of the following are true:

1. `dotnet build` exits with 0 errors.
2. `dotnet test` exits with 0 failures (Skipped tests do not count as failures).
3. T_LANE_E_07 and T_LANE_E_08 PASS (green — no Skip, pure string tests).
4. T_LANE_E_02, T_LANE_E_03, T_LANE_E_04 PASS (green — no Skip, pure reflection/state tests).
5. All 7 scans return required results (SCAN-01 through SCAN-07).
6. `powershell -File .\deploy-sync.ps1` outputs all three gate lines:
   - `ASCII GATE PASS - all source files are clean`
   - `DIFF GUARD PASS: Diff size (N chars) is within limits.`
   - `SOVEREIGN AUDIT PASS: Architectural integrity verified.`
7. No existing test in `CopyEngineTests.cs` has been modified or deleted.
8. No file outside `src/PropTraderTools/CopyEngine.cs` and `src/PropTraderTools/CopyEngineTests.cs` has been modified.

---

### Out of Scope

The following are explicitly OUT OF SCOPE for this ticket. Do not touch:

- `TrySetFollowerSlateReady` — logic, signature, and body are READ-ONLY reference. Zero modification.
- `TryMarkFollowerSlateNotReady` — logic, signature, and body are READ-ONLY reference. Zero modification.
- `SendCopyWithAtm` — reused as-is. Zero modification.
- `TryLaneDHook` and `TryLaneDDispatchFollower` — used as structural pattern reference only. Zero modification.
- `_laneDDispatched` field — reference only. Zero modification.
- `_followerSlateReady` field declaration — Lane E writes to it using the indexer (`[acc.Name] = true`). No change to field declaration.
- Any file outside `src/PropTraderTools/CopyEngine.cs` and `src/PropTraderTools/CopyEngineTests.cs`.
- SIM end-to-end verification (DW-PTT0911B-01) — deferred to ptt-verifier post-compile step per §15 of the architecture plan.
- UI changes, configuration changes, or any non-`src/` file changes.

---

*Ticket written by ptt-architect. Source plan: 02-architecture-plan-lane-e.md (REVIEW_PASS, Cycle 3). Ready for ptt-ticket-reviewer (Ph3.5).*
