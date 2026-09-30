# B140-LaneA Architecture Plan
## Phase 1 — PTT Architect

---

## 1. LANE-SPLIT GATE RESULT

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**

Q1. Same method or within 50 lines?
Answer: YES — only `SyncFollowerBracket` in `CopyEngine.cs` is touched (single insertion point).
-> SINGLE PIPELINE. Gate closed here.

---

## 2. Root Cause Summary

*(Confirmed — no re-investigation required.)*

`SyncFollowerBracket` routes ALL ATM stop brackets (`Stop1`/`Stop2`/`Stop3`) to `SyncAtmFollowerBracket`.
`SyncAtmFollowerBracket` calls `acc.Cancel(fo)` to cancel the existing bracket before resubmitting.

For `Stop1` (`Oco='f2ec29be...'`) and `Stop2` (`Oco='3089bce1...'`), `acc.Cancel` triggers NT8 OCO cascade:

- `Stop1` Cancelled -> `Target1` Cancelled (same Oco group, atomic)
- `Stop2` Cancelled -> `Target2` Cancelled (same Oco group, atomic)

**Result:** follower loses `Target1` and `Target2` on every stop drag. Naked position risk.

`Stop3` (`Oco` non-empty, paired with `Target3` only) — cancel+resubmit was non-destructive (only pairs with `Target3`, no naked position risk). This is a description of the pre-B140 state, **not a mandate to preserve cancel+resubmit**. See Section 4 Stop3 Routing Clarification.
`PTT-STP-Drag` (`Oco=''`) — cancel+resubmit is **CORRECT**, must not change (routed via 3b, `fo.Oco` empty).

---

## 3. NT8 API Facts

*(Embedded directly — confirmed, do not re-validate.)*

| # | Fact | Source |
|---|------|--------|
| 1 | `acc.Change(Order[])` preserves OCO link, does NOT cancel the order | NT8_API_SURFACE.md B31 |
| 2 | `acc.Cancel()` on an OCO-linked order cascade-cancels the OCO partner | Confirmed SIM log |
| 3 | `fo.Oco` is a non-empty GUID for ATM brackets `Stop1`/`Stop2` | Confirmed SIM log |
| 4 | `fo.Oco` is empty string `""` for `PTT-STP-Drag`/`PTT-TGT-Drag` | Confirmed SIM log |
| 5 | `acc.Change()` on ATM Stop brackets from AddOn: MUST VERIFY IN SIM (Gate 1) | Gate requirement |

---

## 4. Surgical Change Design

**File:** `src/PropTraderTools/CopyEngine.cs`
**Method:** `SyncFollowerBracket` (approx. line 2280)

### BEFORE

```csharp
if (isStop && IsAtmSTPOrder(fo)) // (3)
{
    SyncAtmFollowerBracket(acc, fo, newPrice);
    return;
}
```

### AFTER

```csharp
if (isStop && IsAtmSTPOrder(fo)) // (3)
{
    if (!string.IsNullOrEmpty(fo.Oco)) // (3a) B140: OCO-linked -- Change preserves OCO partner
    {
        fo.StopPrice = newPrice;
        try { acc.Change(new Order[] { fo }); }
        catch (Exception ex)
        { StatusUpdate?.Invoke(acc.Name + ": ATM STP Change error: " + ex.Message); }
        return;
    }
    SyncAtmFollowerBracket(acc, fo, newPrice); // (3b) no OCO -- cancel+resubmit (existing path)
    return;
}
```

### Change Summary

| Branch | Condition | Action |
|--------|-----------|--------|
| (3a) OCO-linked | `fo.Oco` non-empty | `fo.StopPrice = newPrice; acc.Change(new Order[] { fo })` — preserves OCO partner |
| (3b) No OCO | `fo.Oco` empty | Existing `SyncAtmFollowerBracket` path — cancel+resubmit unchanged |

### Stop3 Routing Clarification

`Stop3` has a non-empty `Oco` GUID (paired with `Target3`). The `!string.IsNullOrEmpty(fo.Oco)` branch (3a) **WILL** route `Stop3` to `acc.Change`. This is intentional and correct:

- The spec root cause section notes Stop3 cancel+resubmit is "not harmful" (only pairs with `Target3`, no `Stop1`/`Stop2` naked position risk). This is a description of the current state, not a mandate to preserve cancel+resubmit.
- Using `acc.Change` for `Stop3` is strictly **better**: it preserves the `Target3` OCO link and eliminates the cancel+resubmit overhead for `Stop3` as well.
- The spec AFTER code does NOT differentiate `Stop3` — all non-empty `Oco` orders go to `acc.Change`.
- No separate `Stop3` branch is needed or desired.

---

## 5. CYC Analysis

| Method | CYC Before | CYC After | Limit | Status |
|--------|-----------|-----------|-------|--------|
| `SyncFollowerBracket` | 7 | 8 | 8 (JS-041) | **PASS — at limit, no extraction required** |

The single added `if (!string.IsNullOrEmpty(fo.Oco))` branch raises CYC by 1.
CYC 8 is the Jane Street strict limit. No further branching may be added to this method.

---

## 6. JS Rules Compliance

| Rule | Requirement | Status |
|------|-------------|--------|
| JS-021 | No `lock()` anywhere | PASS — no lock introduced |
| JS-001 | No `throw` on hot path | PASS — `catch` absorbs exception via `StatusUpdate` invoke, no rethrow |
| JS-002 | No `return null` for missing values | PASS — void method, no null return path |
| ASCII-only | All string literals ASCII | PASS — `": ATM STP Change error: "` is ASCII-only |
| No DateTime | No `DateTime.Now` usage | PASS — no date/time references introduced |

---

## 7. StopPrice Assignment Note

Existing `acc.Change()` usage at approximately line 2300 in `CopyEngine.cs` uses `fo.StopPrice = newPrice`
and this pattern works in SIM per NT8_API_SURFACE.md B31.

The B140 change uses `fo.StopPrice = newPrice` consistent with that established pattern.

**If SIM Gate 1 fails** (acc.Change is a no-op on Stop brackets):
Switch assignment to `fo.StopPriceChanged = newPrice` and re-test.
Do NOT implement this fallback speculatively — only after Gate 1 failure is confirmed.

---

## 8. SIM Gate Requirements

*(Verifier must record results in `ticket-1-verification.md`.)*

### Gate 1 — acc.Change() on Stop1/Stop2 is NOT a silent no-op (CRITICAL)

- Drag leader stop.
- Confirm follower `Stop1` AND `Stop2` price update in NT8 Order Grid.
- Confirm: `Target1` and `Target2` are **NOT** cancelled after the drag.

**Gate 1 FAIL Protocol:**
If `acc.Change` is confirmed as a no-op on Stop brackets:
- DO NOT implement a fallback.
- STOP immediately.
- Report to Director.
- Document as **DW-B154**.

### Gate 2 — Stop3 cancel+resubmit still works (regression)

- Drag leader stop.
- Confirm follower `Stop3` Cancelled, `PTT-STP-Drag` Working at new price.
- The `fo.Oco` empty path (3b) is NOT affected by B140.

### Gate 3 — Second drag works, no cascade

- Drag stop twice consecutively.
- Confirm `Stop1`/`Stop2` prices updated on both drags.
- Confirm no target cancellation on either drag.

---

## 9. Test Requirements

All tests use **xUnit only** (`[Fact]` attribute). No NUnit or MSTest.

### Method Stubs

```csharp
// T_B140_01
[Fact]
public void SyncFollowerBracket_OcoLinkedFo_CallsAccChange()
// Assert: compiled IL / mock path for OCO-linked fo contains acc.Change call, not acc.Cancel

// T_B140_02
[Fact]
public void SyncFollowerBracket_EmptyOcoFo_RoutesToSyncAtmFollowerBracket()
// Assert: fo with Oco="" routes to SyncAtmFollowerBracket (regression guard)

// T_B140_03
[Fact]
public void IsAtmSTPOrder_Stop1_ReturnsTrue()
// Assert: IsAtmSTPOrder returns true for order named "Stop1" (regression guard)

// T_B140_04
[Fact]
public void IsAtmSTPOrder_Stop2_ReturnsTrue()
// Assert: IsAtmSTPOrder returns true for order named "Stop2" (regression guard)

// T_B140_05
[Fact]
public void IsAtmSTPOrder_Stop3_ReturnsTrue()
// Assert: IsAtmSTPOrder returns true for order named "Stop3" (regression guard)

// T_B140_06
[Fact]
public void SyncFollowerBracket_OcoLinkedFo_NoAccCancelCall()
// Assert: OCO-linked fo branch does NOT call acc.Cancel (cascade eliminated)
// Verify via mock: acc.Cancel is never invoked when fo.Oco is non-empty

// T_B140_07
[Fact]
public void SyncFollowerBracket_AtmTargetBranch_RoutesToSyncAtmFollowerTarget()
// Assert: isStop=false, IsAtmSTPOrder=true still routes to SyncAtmFollowerTarget (unchanged path)
```

### Test Coverage Map

| Test ID | Validates |
|---------|-----------|
| T_B140_01 | New OCO path calls `acc.Change` |
| T_B140_02 | Empty Oco regression (3b path intact) |
| T_B140_03 | `IsAtmSTPOrder` Stop1 detection |
| T_B140_04 | `IsAtmSTPOrder` Stop2 detection |
| T_B140_05 | `IsAtmSTPOrder` Stop3 detection |
| T_B140_06 | No `acc.Cancel` on OCO-linked order |
| T_B140_07 | ATM target branch not disturbed |

---

## 10. Deferred Work Register

| ID | Severity | Status | Description |
|----|----------|--------|-------------|
| DW-B153 | P0 | **CLOSED by B140** | OCO cascade on Stop1/Stop2 drag — root cause fixed |
| DW-B64-01 | P0 | OPEN | HandleEntryChange not firing — drag sync broken (next P0 after B140) |
| DW-B71-01..04 | P1 | OPEN | Quick ALL follower bracket dispatch + QX guard |
| DW-B63-01 | P1 | OPEN | Double PTT-Flatten 11ms apart |
| DW-B141 | P1 | OPEN | Phase C SIM Test A (SIM confirmation, no code change) |
| DW-B138 | P1 | OPEN | Stop drag SIM Test B (SIM confirmation, no code change) |
| B135-DEFER-01 | P1 | OPEN | Gap B two simultaneous entries |
| B135-DEFER-02 | P2 | OPEN | Stale orders multi-session |
| DW-B134-OCO-OBS | P1 | OPEN | OBS-A/B/C/D partial-fill race conditions |

---

## 11. Risk Register

| # | Risk | Likelihood | Impact | Mitigation |
|---|------|-----------|--------|------------|
| R1 | `acc.Change()` is a silent no-op on ATM Stop brackets from AddOn context | Medium (unconfirmed in AddOn) | Critical — stop drag has zero effect on follower | **Gate 1** mandatory SIM verification before merge. If no-op confirmed: STOP, report as DW-B154, no fallback code. |

| R2 | `Stop3` routed via `acc.Change` instead of cancel+resubmit | Resolved | Acceptable — `acc.Change` preserves the `Target3` OCO link, which is strictly better than cancel+resubmit for `Stop3`. See Section 4 Stop3 Routing Clarification. |

No other risks identified. The surgical change is constrained to a single `if` branch inside one method.
The existing `SyncAtmFollowerBracket` (3b) path is unmodified — regression surface is minimal.

---

## Component Summary

| Component | File | Change Type |
|-----------|------|-------------|
| `SyncFollowerBracket` | `src/PropTraderTools/CopyEngine.cs` (~line 2280) | Surgical insert — 9 lines |
| `SyncAtmFollowerBracket` | `src/PropTraderTools/CopyEngine.cs` | No change |
| `IsAtmSTPOrder` | `src/PropTraderTools/CopyEngine.cs` | No change |
| Tests (7 `[Fact]`) | `tests/PropTraderTools.Tests/` | New xUnit test methods |

---

*Plan authored by ptt-architect, B140-LaneA, Phase 1.*
*Revised by ptt-architect, B140-LaneA, Phase 1 REVISION (cycle 1 of 2) — Stop3 routing clarification added per REVIEW_FAIL V1.*
*Status: awaiting ptt-plan-reviewer (Phase 2), cycle 2.*
