# B195 Architecture Plan

**Epic**: B195 -- Clone Mode ATM-Aware Drain Dispatch (DW-B195-01)
**Author**: ptt-architect (Phase 1)
**Date**: 2026-10-11
**Status**: PLAN_COMPLETE
**Preceding plan**: docs/brain/B194/02-architecture-plan.md
**Deferred backlog source**: docs/brain/B194/06-deferred-backlog.md

---

## §1 Epic Summary and Scope

### LANE-SPLIT GATE RESULT: LANES-APPROVED

Gate evaluation (per orchestrator mandate):

| Question | Answer | Reasoning |
|----------|--------|-----------|
| Q1: Same method or within 50 lines? | NO | T1=CopyEngine.cs (SubmitDrainedEntry), T2=TradeCopierPanel.cs. Different files. |
| Q2: Fix B design depends on Fix A? | NO | T2 CYC extraction is fully independent of T1 ATM dispatch fix. |
| Q3: Each fix has standalone value if the other is blocked? | YES | T1 alone: fixes live naked-position risk (P1). T2 alone: reduces CYC violation. |
| Q4: Each fix has an independent SIM verification path? | YES | T1: SIM step 5 (fo=<order> not fo=NULL). T2: unit test only. |

Write-set overlap: T1=CopyEngine.cs / T2=TradeCopierPanel.cs. No overlap. LANES-APPROVED for sequential execution.

### Scope Summary

**T1 (P1 -- PRIMARY, BLOCKING)**: DW-B195-01 -- Clone Mode SubmitDrainedEntry Must Call SendCopyWithAtm
- File: `src/PropTraderTools/CopyEngine.cs`
- One method modified: `SubmitDrainedEntry` (line 6718)
- No PendingDispatchDrain field change (Option A re-fetch selected -- see §3)
- No DrainThenDispatch change

**T2 (DROPPED -- SOURCE METHOD NOT FOUND)**:
- DW-B177-CYC: `OnAtrBoxPreviewKeyDown` CYC=13 reduction.
- Investigation result: `OnAtrBoxPreviewKeyDown` does NOT exist in `TradeCopierPanel.cs` or any file in `src/PropTraderTools/`. Grep for "OnAtrBoxPreviewKeyDown", "AtrBox", "CYC=13", and "DW-B177" all returned zero matches. DW-B177-CYC also does NOT appear in `docs/brain/B194/06-deferred-backlog.md` carried-forward list.
- Action: T2 is dropped. New deferred item DW-B195-03 opened (see §8).

### Deferred Items Addressed by B195

| DW ID | Priority | Action |
|-------|----------|--------|
| DW-B195-01 | P1 | CLOSED by T1 |
| DW-B183-03 | P1 | Carried forward (not touched) |
| DW-B192-SINST-01 | P2 | Carried forward (not touched) |
| DW-B193-01 | P3 | Carried forward (not touched) |

---

## §2 Root Cause Confirmation

### PendingDispatchDrain Field Gap (Source-Confirmed)

**Class definition**: `CopyEngine.cs` line 6781.
```
private sealed class PendingDispatchDrain
```

**Current fields** (lines 6783-6795):
| Field | Type | Line |
|-------|------|------|
| FollowerAcctKey | string | 6783 |
| Instrument | Instrument | 6784 |
| Qty | int | 6785 |
| Price | double | 6786 |
| Action | OrderAction | 6787 |
| OrderType | OrderType | 6788 |
| DrainedOrderIds | IReadOnlyList<string> | 6792 |
| FollowerAccount | Account | 6793 |
| PendingCancelCount | int (mutable Interlocked) | 6794 |
| TimestampTicks | long | 6795 |

**Missing**: No ATM mode field. No FollowerAtmMode. No template name.

**Constructor**: line 6797, 10 parameters, no ATM parameter.

**Only construction site**: line 6618, inside `DrainThenDispatch`.
```
var payload = new PendingDispatchDrain(
    acctKey,           // line 6619
    instrument,        // line 6620
    qty,               // line 6621
    price,             // line 6622
    action,            // line 6623
    orderType,         // line 6624
    drainedIds,        // line 6625
    follower,          // line 6626
    pendingCancelCount: entryCandidates.Count,  // line 6627
    now                // line 6628
);
```

### SubmitDrainedEntry Path (Source-Confirmed)

**Method**: `CopyEngine.cs` lines 6718-6739.
Comment annotation: "CYC=4: (1) TryRemove fails early return, (2) FollowerAccount null early return, (3) delegated to SubmitEntryDirect, (4) F3 cleanup foreach (after submit)."

**Current body**:
```csharp
private void SubmitDrainedEntry(string acctKey)
{
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
        return;
    var follower = payload.FollowerAccount;
    if (follower == null) // (2)
        return;
    SubmitEntryDirect(     // (3) -- BUG: always bare copy, no ATM
        follower,
        payload.Instrument,
        payload.Qty,
        payload.Price,
        payload.Action,
        payload.OrderType
    );
    foreach (var id in payload.DrainedOrderIds) // (4)
        _drainOwnedOrderIds.TryRemove(id, out _);
}
```

**BUG**: Branch (3) calls `SubmitEntryDirect` unconditionally. `SubmitEntryDirect` creates a "PTT-Copy" bare order (line 6671) with no ATM strategy. In Clone+ATM mode, the replacement entry has no brackets. After leader fills, `SyncFollowerBracket` finds `fo=NULL` -- naked position.

### GetCloneAtmMode Mechanism (Source-Confirmed)

**Backing field**: `CopyEngine.cs` line 146:
```csharp
private volatile NinjaTrader.NinjaScript.AtmStrategy _cloneAtmObject = null;
```

**Method**: lines 745-753:
```csharp
internal FollowerAtmMode GetCloneAtmMode()
{
    var atmObj = _cloneAtmObject;
    if (atmObj != null)  // (1) preferred: object overload
        return new FollowerAtmMode.Named(_cloneAtmCache, atmObj);
    var cache = _cloneAtmCache;
    if (cache != null && cache.Length > 0)  // (2) fallback: string overload
        return new FollowerAtmMode.Named(cache);
    return new FollowerAtmMode.Inherit();
}
```

Thread-safety: reads `volatile` field. Safe from any thread including NT8 order event thread.
JS-002: never returns null; Inherit is the fallback sentinel.

### SendCopyWithAtm Signature (Source-Confirmed)

**Method**: `CopyEngine.cs` line 4661:
```csharp
private bool SendCopyWithAtm(
    Account follower,
    Instrument instrument,
    in CopySignal signal,
    FollowerAtmMode.Named namedMode
)
```

NT8 constraint (line 4680): order name MUST be "Entry" for StartAtmStrategy to arm brackets.
CYC=4 (unchanged by B195).

### CopySignal.Create Signature (Source-Confirmed)

**Method**: `CopyEngine.cs` lines 605-611:
```csharp
internal static CopySignal Create(
    OrderAction action,
    OrderType type,
    int qty,
    double limitPrice,
    string orderId
) => new CopySignal(action, type, qty, limitPrice, orderId);
```

Note: `type` parameter is NOT used by SendCopyWithAtm (hardcodes `OrderType.Limit` at line 4673). Pass `payload.OrderType` for correctness; it is ignored by the ATM dispatch path.

---

## §3 Fix Designs

### T1: SubmitDrainedEntry ATM-Aware Dispatch

#### ATM Freshness Decision: Option A (Re-Fetch at Dispatch Time)

**Selected**: Option A -- call `GetCloneAtmMode()` inside `SubmitDrainedEntry` at the moment of dispatch.

**Rationale**:
1. **Minimal change**: No new field in `PendingDispatchDrain`. No change to `DrainThenDispatch`. Single method modification.
2. **Correct semantics**: Uses user's current ATM selection at dispatch time (100-500ms after drain-start). If user switched templates mid-drain, the newer template is used -- correct behavior.
3. **Thread-safe by construction**: `GetCloneAtmMode()` reads a `volatile` field; safe on NT8 order event thread. No lock() required (JS-021).
4. **Consistent pattern**: `ResolveAtmMode` (line 4721) already uses `GetCloneAtmMode()` for Clone mode dispatch. This fix extends the same pattern to the drain-completion path.
5. **Fallback correctness**: If user disabled Clone ATM mode mid-drain, `GetCloneAtmMode()` returns `Inherit`, and `SubmitEntryDirect` is called -- bare copy is correct for non-ATM mode.

**Option B (snapshot in payload) was rejected** because:
- Requires adding a field to `PendingDispatchDrain` (larger change set).
- Adds a constructor parameter (all 10 existing params must be preserved + 1 new).
- Introduces stale-value risk with no practical benefit (user switching ATM mid-drag is rare; re-fetch is more correct).

#### Modified Method: SubmitDrainedEntry

**Before** (current, CYC=4):
```csharp
// CYC=4: (1) TryRemove fails early return, (2) FollowerAccount null early return,
//        (3) delegated to SubmitEntryDirect, (4) F3 cleanup foreach (after submit).
private void SubmitDrainedEntry(string acctKey)
{
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
        return;
    var follower = payload.FollowerAccount;
    if (follower == null) // (2)
        return;
    SubmitEntryDirect( // (3)
        follower,
        payload.Instrument,
        payload.Qty,
        payload.Price,
        payload.Action,
        payload.OrderType
    );
    foreach (var id in payload.DrainedOrderIds) // (4)
        _drainOwnedOrderIds.TryRemove(id, out _);
}
```

**After** (B195, CYC=5):
```csharp
// B195: SubmitDrainedEntry is now ATM-mode-aware.
// CYC=5: (1) TryRemove fails early return, (2) FollowerAccount null early return,
//        (3) ATM mode branch -- Named -> SendCopyWithAtm, else -> SubmitEntryDirect,
//        (4) F3 cleanup foreach (after submit).
// Option A: GetCloneAtmMode() re-fetched at dispatch time (volatile read, thread-safe).
// JS-021: no lock(). GetCloneAtmMode reads volatile field.
// JS-001: no throw. JS-066: CYC=5 (<=8 ceiling).
// NT8: SendCopyWithAtm uses StartAtmStrategy (AddOnBase-confirmed, NT8_FULL_REFERENCE.md line 17).
// NT8: "Entry" order name constraint satisfied inside SendCopyWithAtm (line 4680).
// ASCII-only: all identifiers and literals ASCII.
private void SubmitDrainedEntry(string acctKey)
{
    if (!_pendingDispatchDrains.TryRemove(acctKey, out var payload)) // (1)
        return;
    var follower = payload.FollowerAccount;
    if (follower == null) // (2)
        return;
    var atmMode = GetCloneAtmMode(); // (3) re-fetch at dispatch time -- volatile read, safe
    if (atmMode is FollowerAtmMode.Named namedAtm) // (3a) Clone+ATM path
    {
        var signal = CopySignal.Create(
            payload.Action,
            payload.OrderType,
            payload.Qty,
            payload.Price,
            payload.FollowerAcctKey + "-drain-atm-" + payload.TimestampTicks
        );
        SendCopyWithAtm(follower, payload.Instrument, in signal, namedAtm);
    }
    else // (3b) non-ATM path (Inherit or no Clone ATM configured)
    {
        SubmitEntryDirect(
            follower,
            payload.Instrument,
            payload.Qty,
            payload.Price,
            payload.Action,
            payload.OrderType
        );
    }
    foreach (var id in payload.DrainedOrderIds) // (4) cleanup drain IDs after dispatch
        _drainOwnedOrderIds.TryRemove(id, out _);
}
```

**orderId composition**: `payload.FollowerAcctKey + "-drain-atm-" + payload.TimestampTicks`
- Guarantees uniqueness: TimestampTicks is set at drain construction (line 6613); two drains for the same account cannot coexist (_pendingDispatchDrains.TryAdd prevents it).
- All ASCII: account names are ASCII; "-drain-atm-" is ASCII; TimestampTicks is numeric.
- Passed to `RecordFollowerCopy` inside `SendCopyWithAtm` as the orderId key.

#### Methods NOT Modified in T1

- `PendingDispatchDrain` class: unchanged (no new field, no constructor change).
- `DrainThenDispatch`: unchanged (no ATM capture needed under Option A).
- `SubmitEntryDirect`: unchanged.
- `SendCopyWithAtm`: unchanged.
- `GetCloneAtmMode`: unchanged (called, not modified).

---

## §4 Component List

### New Components
None. All changes are additive modifications to an existing method body.

### Modified Methods

| Method | File | Lines | Change |
|--------|------|-------|--------|
| `SubmitDrainedEntry` | CopyEngine.cs | 6718-6739 | Add GetCloneAtmMode() re-fetch + if/else ATM dispatch branch. CYC 4→5. |

### Called But Unchanged

| Method | File | Lines | Role |
|--------|------|-------|------|
| `GetCloneAtmMode()` | CopyEngine.cs | 745-754 | Re-fetches current ATM mode (volatile read) |
| `SendCopyWithAtm` | CopyEngine.cs | 4661-4705 | ATM entry dispatch via StartAtmStrategy |
| `CopySignal.Create` | CopyEngine.cs | 605-611 | Creates CopySignal for SendCopyWithAtm |
| `SubmitEntryDirect` | CopyEngine.cs | 6650-6688 | Non-ATM fallback path (unchanged) |

### New Test File
`src/PropTraderTools/Tests/B195Tests.cs` -- 4 test cases (see §7).

---

## §5 CYC Impact Table

| Method | Pre-B195 CYC | Post-B195 CYC | Limit | Status |
|--------|-------------|--------------|-------|--------|
| `SubmitDrainedEntry` | 4 | 5 | 8 | PASS (+1 branch for ATM dispatch) |
| `DrainThenDispatch` | 4 | 4 | 8 | PASS (unchanged) |
| `PendingDispatchDrain constructor` | 1 | 1 | 8 | PASS (unchanged) |
| `GetCloneAtmMode()` | 2 | 2 | 8 | PASS (unchanged, called) |
| `SendCopyWithAtm` | 4 | 4 | 8 | PASS (unchanged, called) |

**Note**: CYC counts follow codebase convention (each if/foreach = 1 branch point; compound boolean counted as 1). Codebase CYC comments are the reference (e.g., SubmitDrainedEntry comment at line 6714: "CYC=4").

---

## §6 JS Scan Checklist

| Rule | Check | Result |
|------|-------|--------|
| JS-021: No lock() | No lock() in modified method or any called helper. GetCloneAtmMode() uses volatile read (thread-safe, no lock). | PASS |
| JS-033: No async void | SubmitDrainedEntry is `private void` (not async). No new async methods introduced. | PASS |
| JS-066: CYC ≤ 8 | SubmitDrainedEntry: 4→5. All other methods unchanged. Maximum CYC in B195 scope = 5. | PASS |
| JS-001: No throw new Exception | No throws introduced. Early returns used. SendCopyWithAtm has its own try/catch (line 4668). | PASS |
| ASCII-only | New identifiers: `atmMode`, `namedAtm`, `signal`. String literals: `"-drain-atm-"`. All ASCII. | PASS |
| DateTime.UtcNow | No DateTime usage in B195 code. TimestampTicks uses Environment.TickCount (existing pattern, line 6613). | PASS |
| JS-002: No return null | GetCloneAtmMode() returns Inherit (not null) as fallback. No null returns in new code. | PASS |
| NT8 AddOnBase scope | StartAtmStrategy: confirmed AddOnBase-available (NT8_FULL_REFERENCE.md line 17-34). No StrategyBase-only API used. | PASS |
| "Entry" order name | SendCopyWithAtm hardcodes "Entry" at line 4680. Constraint satisfied. | PASS |

---

## §7 Test Plan

**Test file**: `src/PropTraderTools/Tests/B195Tests.cs`
**Framework**: xUnit ([Fact] attribute)
**Class**: `B195Tests`

### Test Cases

#### [Fact] T1_SubmitDrainedEntry_CloneAtmMode_CallsSendCopyWithAtm
**What it asserts**: When Clone+ATM mode is active (_cloneAtmCache has a template name and/or _cloneAtmObject is set), `SubmitDrainedEntry` routes to the `SendCopyWithAtm` path (not `SubmitEntryDirect`).
**Setup**:
1. Create CopyEngine test instance.
2. Set ATM cache via `SetCloneAtmCache("TestTemplate")`.
3. Insert a PendingDispatchDrain payload into `_pendingDispatchDrains` via test seam.
4. Call `SubmitDrainedEntry("Sim102")`.
**Assert**: A "Entry"-named order is created (via mock or interceptable CreateOrder), not a "PTT-Copy"-named order. `GetCloneAtmMode()` returns Named.

#### [Fact] T1_SubmitDrainedEntry_NoAtmMode_CallsSubmitEntryDirect
**What it asserts**: When no ATM template is set (Clone mode but no ATM configured, or non-Clone mode), `SubmitDrainedEntry` routes to `SubmitEntryDirect` (bare "PTT-Copy" order).
**Setup**:
1. CopyEngine with no ATM cache set.
2. Insert PendingDispatchDrain payload.
3. Call `SubmitDrainedEntry("Sim102")`.
**Assert**: Account.CreateOrder is called with name "PTT-Copy", not "Entry". No StartAtmStrategy call.

#### [Fact] T1_SubmitDrainedEntry_AtmObjectNull_TemplatePresent_UsesStringOverload
**What it asserts**: When `_cloneAtmObject` is null but `_cloneAtmCache` has a template name, `GetCloneAtmMode()` returns `Named` with string overload (no AtmObject), and `SendCopyWithAtm` uses `StartAtmStrategy(string, order)`.
**Setup**: `SetCloneAtmCache("MyTemplate")`. Do NOT call `SetCloneAtmObjectCache`.
**Assert**: `GetCloneAtmMode()` returns `FollowerAtmMode.Named` with `AtmObject == null` and `TemplateName == "MyTemplate"`.

#### [Fact] T1_SubmitDrainedEntry_MissingPayload_ReturnsEarly
**What it asserts**: When `_pendingDispatchDrains` does not contain the acctKey, `SubmitDrainedEntry` returns without side effects (regression guard, existing CYC branch 1).
**Setup**: Empty `_pendingDispatchDrains`.
**Assert**: No exception. No account API calls. Method returns cleanly.

---

## §8 Deferred Work Register

### Items Closed by B195

| DW ID | Description | Closed By |
|-------|-------------|-----------|
| DW-B195-01 | Clone Mode SubmitDrainedEntry Not ATM-Aware | B195-T1 |

### Items Carried Forward (Unchanged)

| DW ID | Priority | Description |
|-------|----------|-------------|
| DW-B183-03 | P1 | IsLaneEEligible CYC=8 frozen (ceiling, non-blocking) |
| DW-B192-SINST-01 | P2 | SetInstrument() CYC=8 ceiling (non-blocking) |
| DW-B193-01 | P3 | OnQuickAllBufferChanged old label strings |

### New Items Opened by B195

#### DW-B195-02: DrainThenDispatch Fast-Path Also Bare in Clone Mode

```
Priority: P1
Description: DrainThenDispatch fast-path (line 6608, !entryCandidates.Any() branch) calls
  SubmitEntryDirect directly -- same naked-position bug as DW-B195-01 for Clone+ATM mode.
  When a drag fires and no prior entry candidates exist (first drag, or candidates list empty),
  the fast-path bypasses SendCopyWithAtm. Followers get bare "PTT-Copy" with no ATM brackets.
  B195-T1 fixes the drain-completion path (SubmitDrainedEntry) but NOT this fast-path.
Root: DrainThenDispatch (line 6606-6609) calls SubmitEntryDirect without ATM mode check.
Fix direction: Add ATM mode check in DrainThenDispatch fast-path (same pattern as T1).
  var fastAtm = GetCloneAtmMode();
  if (fastAtm is FollowerAtmMode.Named namedFastAtm) {
      var sig = CopySignal.Create(action, orderType, qty, price, acctKey + "-fast-atm-" + now);
      SendCopyWithAtm(follower, instrument, in sig, namedFastAtm);
  } else {
      SubmitEntryDirect(follower, instrument, qty, price, action, orderType);
  }
  DrainThenDispatch current CYC=4. Adding one branch: CYC=5. Safe (<=8).
Blocking: Yes -- same live naked-position risk as DW-B195-01 for initial drag (no candidates).
Target Block: B196 (immediately after B195 SIM gate passes)
Status: OPEN (opened B195)
Source: B195 architecture plan §8
```

#### DW-B195-03: DW-B177-CYC Source Method Not Found

```
Priority: P2
Description: Orchestrator prompt for B195-T2 references DW-B177-CYC (OnAtrBoxPreviewKeyDown
  CYC=13 in TradeCopierPanel.cs). Source investigation confirmed this method does NOT exist
  in any .cs file in src/PropTraderTools/. No grep match for "OnAtrBoxPreviewKeyDown",
  "AtrBox", "CYC=13", or "DW-B177". DW-B177-CYC is also absent from B194/06-deferred-backlog.md.
  Before scheduling extraction, the actual target method must be located or confirmed as
  not yet implemented.
Action: Director must identify correct method name or confirm the feature is unimplemented.
Blocking: No
Target Block: B196 (investigation only)
Status: OPEN (opened B195)
Source: B195 architecture plan §1 (T2 dropped)
```

---

## §9 SIM Gate Criteria

**B195 SIM gate must be run by Director after all tickets reach VERIFY_PASS.**
Record results in `docs/brain/B195/sim-gate-completion.md`.

B195 SIM gate specifically validates the Clone+ATM drain scenario:

1. **Setup**: Leader=Sim101, Followers=Sim102/Sim103/Sim104, instrument=MGC DEC26.
   Set Clone mode ON. Set ATM template (e.g., "MyATM-1-1") for the session.

2. **Place ATM entry for Sim101**. Confirm all 3 followers receive PTT-Copy Working orders.
   (All followers should receive "Entry"-named orders with ATM brackets -- confirm in NT8 Orders panel.)

3. **First drag -- followers have existing entries**:
   Drag leader entry order (pre-fill). Confirm `[DRAIN]` fires in Output window for ALL 3 followers.
   Confirm `cancel-sent=1` (or appropriate count) per follower.

4. **Drain completes -- ATM path fires**:
   After all cancel acks arrive, confirm `[DRAIN-SUBMIT]` does NOT appear (that is SubmitEntryDirect).
   Instead confirm StartAtmStrategy fires (new "Entry" orders appear in followers' Orders panel, not "PTT-Copy").

5. **Leader fills**. All 4 accounts have open Long positions.
   Confirm `SyncFollowerBracket` log shows `fo=<order>` (not `fo=NULL`) for ALL followers.
   This is the primary B195 pass criterion.

6. **No PTT-Flatten fires on any follower** (naked position detector must NOT trigger).

7. **Drag leader bracket (Target1)**. Confirm bracket sync propagates to all followers.
   (Tests that the ATM bracket is live and syncing correctly after drain-dispatch.)

8. **Close leader position**. Followers close WITHOUT reverse position.
   Zero PTT-Flatten loops observed in the session.

**All 8 criteria must PASS before B196 execution begins.**

SIM carried forward from B194 (B194/06-deferred-backlog.md SIM Gate Note):
Steps 1, 4 (fo=<order>), 5, 6, 7, 8 are identical to B194 SIM gate steps.
Steps 3-4 are B195-specific (drain path + ATM dispatch verification).

---

## Appendix A: Source References (Line Numbers)

All line numbers reference `src/PropTraderTools/CopyEngine.cs` at the time of B195 architecture.

| Symbol | Lines | Notes |
|--------|-------|-------|
| `PendingDispatchDrain` class | 6781-6821 | sealed class, 10 fields |
| `PendingDispatchDrain` constructor | 6797-6820 | 10 params, no ATM |
| Only construction site (`new PendingDispatchDrain`) | 6618-6629 | In DrainThenDispatch |
| `DrainThenDispatch` | 6579-6644 | CYC=4 (per convention); NOT modified by B195 |
| `SubmitDrainedEntry` | 6718-6739 | CYC=4→5 by B195 |
| `SubmitEntryDirect` | 6650-6688 | CYC=2; NOT modified |
| `OnDrainCancelAck` | 6694-6711 | CYC=3; NOT modified |
| `_cloneAtmObject` field | 146 | volatile AtmStrategy |
| `GetCloneAtmMode()` | 745-754 | CYC=2; volatile read; NOT modified |
| `SendCopyWithAtm` | 4661-4705 | CYC=4; "Entry" name at 4680; NOT modified |
| `CopySignal.Create` | 605-611 | static, 5 params |
| `FollowerAtmMode.Named` | 89-109 | sealed class: TemplateName + AtmObject |
| `_pendingDispatchDrains` field | 388-389 | ConcurrentDictionary<string,PendingDispatchDrain> |

---

*Written by: ptt-architect*
*Epic: B195*
*Phase: 1 (Architecture)*
*Return: PLAN_COMPLETE*
