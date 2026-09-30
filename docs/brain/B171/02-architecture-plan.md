# B171 Architecture Plan
**Block:** B171 — DW-B171-01 + DW-B171-02: PTT-STP-Drag Duplicate Stop + fo=NULL on Stop Drag
**Written by:** ptt-architect (Phase 1)
**Design variant:** Variant E (Best-of-5 Winner — Adversarial Improved Baseline)
**Status:** PLAN_COMPLETE

---

## 1. LANE-SPLIT GATE

### Q1. Are the changes within the same method or within 50 lines of each other?

The three changed methods span lines 4437-4512 in `CopyEngine.cs`:
- `CreateFollowerReplacementStop`: lines 4437-4451
- `SubmitReplacementStopOrder`: lines 4458-4498
- `ExecutePhaseCStopReplacement`: lines 4507-4512

Total span: 4437 to 4512 = **75 lines**. NOT within 50 lines of each other. NOT within the same method. Q1 = NO.

### Q2. Does Fix B's design depend on Fix A's final design?

**YES.** B171-2 adds a `string suffix` parameter to `CreateFollowerReplacementStop` and `SubmitReplacementStopOrder`. B171-1 calls `CreateFollowerReplacementStop` with the new `suffix` argument. The compile dependency is bidirectional:
- If B171-2 is committed first (callee signature changes, call site unchanged): `ExecutePhaseCStopReplacement` still calls the OLD no-suffix signature → **COMPILE ERROR**.
- If B171-1 is committed first (call site changes, callee signature unchanged): `ExecutePhaseCStopReplacement` calls new suffix signature that does not yet exist → **COMPILE ERROR**.
- Both changes must be applied before the file compiles.

**Q2 = YES → STOP. Gate result: SINGLE-PIPELINE.**

### Q3. Does each fix have standalone value if the other is blocked?

For completeness (Q2 already terminates the gate):
- B171-1 alone (cancel sweep, suffix on call site): cancel call would search for "PTT-STP-Drag-{N}" but `SubmitReplacementStopOrder` still creates "PTT-STP-Drag" (unsuffixed) → cancel finds nothing → no deduplication → DW-B171-01 not fixed.
- B171-2 alone (suffix the name, add param): resolves DW-B171-02 (fo=NULL) but DW-B171-01 (duplicates) persists — no cancel sweep on the target-drag path.

Q3 = NO for both. Confirms SINGLE-PIPELINE.

### Q4. Does each fix have an independent SIM verification path?

No. Both fixes address the same root cause, produce a compile-atomic unit, and share a single SIM gate sequence (orchestrator prompt §SIM GATE). Q4 = NO. Confirms SINGLE-PIPELINE.

### LANE-SPLIT GATE RESULT: **SINGLE-PIPELINE**

> Rationale: Q2 = YES (compile dependency between B171-1 call site and B171-2 callee signatures). Tickets B171-1, B171-2, B171-3 are ordered sequentially in a single pipeline. No lane split permitted.

---

## 2. Bug Root-Cause Analysis

### DW-B171-01 — DUPLICATE-STP-DRAG-ON-REPEATED-TGT-DRAG (P0)

**Trigger:** Director drags any target order 2+ times, or drags multiple different targets in sequence.

**Root cause (line 4478):** `SubmitReplacementStopOrder` creates the follower's drag-replacement stop with the hardcoded unsuffixed name `"PTT-STP-Drag"`:
```
Line 4478:  "PTT-STP-Drag"          <- unsuffixed (THE BUG)
```

**Missing mechanism (lines 4507-4512):** `ExecutePhaseCStopReplacement` calls `CreateFollowerReplacementStop` → `SubmitReplacementStopOrder` directly — there is **no** `CancelLiveCollateralStop` call before creation on the Phase C target-drag path. Compare to `ResubmitOneCollateralLeg` (lines 4038-4053, stop-drag path) which correctly calls `CancelLiveCollateralStop` before creating a replacement.

**Effect:** Each target drag stacks a new `PTT-STP-Drag` on top of prior ones. After drag #1: 1 stop. After drag #2: 2 stops. After drag #3: 3 stops.

**Evidence:** `docs/output.md` lines 196-242 — three consecutive PTT-STP-Drag entries in follower order list.

### DW-B171-02 — fo=NULL-ON-STOP-DRAG-WHEN-STP-IS-PTT-NAMED (P0)

**Trigger:** Director drags a STOP order after at least one target drag has occurred (so the follower's live stop is named `"PTT-STP-Drag"` instead of the original `"Stop2"` / `"Stop3"`).

**Root cause — name mismatch between creation and lookup:**
- `IsDragNameMatch` (lines 4740-4748) checks: `orderName == "PTT-STP-Drag-" + legSuffix` (SUFFIXED pattern)
- `SubmitReplacementStopOrder` (line 4478) creates stop as: `"PTT-STP-Drag"` (UNSUFFIXED)
- Exact match fails → `fo = NULL` → stop drag not synced for any follower.

**Effect:** All followers (Sim102, Sim103, Sim104) show `fo=NULL` on every stop drag event. Stop drag is never synced.

**Evidence:** `docs/output.md` lines 287-339 — all three followers report `fo=NULL`.

**Relationship:** Both bugs are caused by the identical missing suffix at line 4478. Fixing the name to `"PTT-STP-Drag-" + suffix` at line 4478 AND adding the pre-cancel sweep in `ExecutePhaseCStopReplacement` fixes BOTH bugs simultaneously.

---

## 3. Variant E Selection Rationale

### Variants A-D: EC-1 Regression (cancel-before-price-validation)

All four losing variants (A–D) share a structural flaw: the cancel call (`CancelLiveCollateralStop`) is placed **before** the price validation (`FindLeaderStopPrice` result check). If `FindLeaderStopPrice` returns <= 0:
- The existing live stop is **cancelled** (follower is now unprotected)
- No replacement stop is submitted (price guard fires and returns)
- The follower bracket has **zero live stops** until the next drag event

This is the EC-1 regression: the "fix" can leave followers more exposed than the original bug.

### Variant E: EC-1 Closed by Pre-Cancel Price Guard

Variant E inverts the order: **price validation BEFORE cancel**. The structure is:

```
1. Derive suffix
2. Get stop price
3. if (stp <= 0) → StatusUpdate + return  // EARLY EXIT — cancel is never reached
4. CancelLiveCollateralStop(...)           // Only reached if stp > 0
5. CreateFollowerReplacementStop(...)
```

If `FindLeaderStopPrice` returns <= 0, the method exits at step 3 without ever calling `CancelLiveCollateralStop`. The follower's existing live stop remains untouched. The cancel-then-create sequence is unconditionally safe — it only executes when a valid replacement price exists.

### CYC Impact

The `if (stp <= 0)` guard does NOT increase the net CYC of `ExecutePhaseCStopReplacement` because the branch replaces what was previously a delegated guard in `CreateFollowerReplacementStop`. The orchestrator confirms: CYC=2 → CYC=2, delta=0.

**Variant E is the only correct design. Selection is mandatory. No deviations.**

---

## 4. BEFORE / AFTER for Each Changed Method

### Method A — `ExecutePhaseCStopReplacement` (lines 4507-4512)

**BEFORE:**
```csharp
private void ExecutePhaseCStopReplacement(Account acc, Order fo, Order? leaderOrder)
{
    int bracketIdx = DeriveLeaderBracketIndex(leaderOrder);
    double stp = FindLeaderStopPrice(leaderOrder?.Account, bracketIdx);
    CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp);
}
```

**AFTER (Variant E):**
```csharp
// B171 DW-B171-01+DW-B171-02: derive suffix, validate price before cancel, cancel-then-replace.
// CYC=2: base(1) + stp<=0 guard(1). JS-021: no lock. JS-001: delegates to CreateFollowerReplacementStop catch.
// JS-002: void return. ASCII-only.
private void ExecutePhaseCStopReplacement(Account acc, Order fo, Order? leaderOrder)
{
    int bracketIdx = DeriveLeaderBracketIndex(leaderOrder);
    string suffix = bracketIdx.ToString();
    double stp = FindLeaderStopPrice(leaderOrder?.Account, bracketIdx);
    if (stp <= 0.0)
    {
        StatusUpdate?.Invoke(acc?.Name + ": PTT-STP-Drag-" + suffix + " skipped: stopPrice <= 0");
        return;
    }
    CancelLiveCollateralStop(acc, fo, "PTT-STP-Drag-" + suffix);
    CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp, suffix);
}
```

**Lines changed:** +4 (suffix declaration, stp guard with StatusUpdate + return, CancelLiveCollateralStop call); 1 modified (CreateFollowerReplacementStop call gains `, suffix`).
**Ticket:** B171-1.

---

### Method B — `CreateFollowerReplacementStop` (lines 4437-4451)

**BEFORE:**
```csharp
private void CreateFollowerReplacementStop(
    Account followerAcc,
    Instrument instr,
    int qty,
    OrderAction stopAction,
    double stopPrice
)
{
    if (stopPrice <= 0.0)
    {
        StatusUpdate?.Invoke(followerAcc?.Name + ": PTT-STP-Drag skipped: stopPrice <= 0");
        return;
    }
    SubmitReplacementStopOrder(followerAcc, instr, qty, stopAction, stopPrice);
}
```

**AFTER (Variant E):**
```csharp
// B171: suffix param added (pass-through). CYC=2 unchanged.
// stopPrice guard remains as defensive check for any future caller.
// JS-021: no lock. JS-001: try/catch absorbed in SubmitReplacementStopOrder. JS-002: void return.
private void CreateFollowerReplacementStop(
    Account followerAcc,
    Instrument instr,
    int qty,
    OrderAction stopAction,
    double stopPrice,
    string suffix
)
{
    if (stopPrice <= 0.0)
    {
        StatusUpdate?.Invoke(followerAcc?.Name + ": PTT-STP-Drag-" + suffix + " skipped: stopPrice <= 0");
        return;
    }
    SubmitReplacementStopOrder(followerAcc, instr, qty, stopAction, stopPrice, suffix);
}
```

**Lines changed:** +1 (suffix param); 2 modified (StatusUpdate message gains `-" + suffix`, SubmitReplacementStopOrder call gains `, suffix`).
**Ticket:** B171-2.

---

### Method C — `SubmitReplacementStopOrder` (lines 4458-4498)

**BEFORE:**
```csharp
private void SubmitReplacementStopOrder(
    Account followerAcc,
    Instrument instr,
    int qty,
    OrderAction stopAction,
    double stopPrice
)
{
    try
    {
        var newStop = followerAcc.CreateOrder(
            instr,
            stopAction,
            OrderType.StopMarket,
            OrderEntry.Automated,
            TimeInForce.Day,
            qty,
            0,
            stopPrice,
            "",
            "PTT-STP-Drag",
            NinjaTrader.Core.Globals.MaxDate,
            (NinjaTrader.Cbi.CustomOrder)null
        );
        if (newStop == null)
        {
            StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag: CreateOrder returned null");
            return;
        }
        followerAcc.Submit(new[] { newStop });
        StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag placed @ " + stopPrice.ToString());
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag error: " + ex.Message);
    }
}
```

**AFTER (Variant E):**
```csharp
// B171 DW-B171-02: suffix param added; order name changed from "PTT-STP-Drag" to "PTT-STP-Drag-"+suffix.
// CYC=4 unchanged: base(1)+newStop null(1)+try block(1)+catch(1).
// JS-021: no lock (private, no shared state). JS-001: try/catch absorbs. JS-002: void return. ASCII-only.
private void SubmitReplacementStopOrder(
    Account followerAcc,
    Instrument instr,
    int qty,
    OrderAction stopAction,
    double stopPrice,
    string suffix
)
{
    try
    {
        var newStop = followerAcc.CreateOrder(
            instr,
            stopAction,
            OrderType.StopMarket,
            OrderEntry.Automated,
            TimeInForce.Day,
            qty,
            0,
            stopPrice,
            "",
            "PTT-STP-Drag-" + suffix,
            NinjaTrader.Core.Globals.MaxDate,
            (NinjaTrader.Cbi.CustomOrder)null
        );
        if (newStop == null)
        {
            StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag-" + suffix + ": CreateOrder returned null");
            return;
        }
        followerAcc.Submit(new[] { newStop });
        StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag-" + suffix + " placed @ " + stopPrice.ToString());
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag error: " + ex.Message);
    }
}
```

**Lines changed:** +1 (suffix param); 3 modified (order name at line ~4478, null-check StatusUpdate gains suffix, placed StatusUpdate gains suffix).
**Ticket:** B171-2.

---

## 5. CYC Table

| Method | Before | After | Delta | Compliant (<=8) |
|--------|--------|-------|-------|-----------------|
| `ExecutePhaseCStopReplacement` | 2 | 2 | 0 | YES |
| `CreateFollowerReplacementStop` | 2 | 2 | 0 | YES |
| `SubmitReplacementStopOrder` | 4 | 4 | 0 | YES |

**CYC counting model used:** McCabe simplified — base(1) + explicit `if`/`catch` branches. All methods at zero delta. All well within the CYC <= 8 limit.

---

## 6. Caller Safety Analysis

**`CreateFollowerReplacementStop`:**
- Exactly **one caller**: `ExecutePhaseCStopReplacement` at line 4511.
- No test directly calls this method.
- Signature change (add `string suffix`) is safe — single call site is updated atomically in B171-1.

**`SubmitReplacementStopOrder`:**
- Exactly **one caller**: `CreateFollowerReplacementStop` at line 4450.
- No test directly calls this method.
- Signature change (add `string suffix`) is safe — single call site is updated atomically in B171-2.

**`ExecutePhaseCStopReplacement`:**
- Signature is **unchanged** (same parameters: `Account acc, Order fo, Order? leaderOrder`).
- Body changes only. All existing callers remain unaffected.

**Blast radius: zero.** No other methods require modification. No callers outside the three-method chain.

---

## 7. Methods Unchanged

The following methods are **referenced** by the repair design but receive **zero edits**:

| Method | Lines | Role in Repair | Change |
|--------|-------|----------------|--------|
| `CancelLiveCollateralStop` | 4058-4073 | Called by `ExecutePhaseCStopReplacement` AFTER to cancel existing stop before replacement | NONE — reused as-is |
| `IsDragNameMatch` | 4740-4748 | Checks `orderName == "PTT-STP-Drag-" + legSuffix`; automatically resolves DW-B171-02 when stop name is corrected | NONE — already correct |
| `DeriveLeaderBracketIndex` | 4391-4406 | Returns `int bracketIdx` used to derive `suffix` | NONE — reused as-is |
| `ResubmitOneCollateralLeg` | 4038-4053 | Reference pattern: already calls `CancelLiveCollateralStop` before creating; confirms the repair pattern is correct | NONE |

---

## 8. Jane Street Compliance Checklist

| Rule | Requirement | Status |
|------|-------------|--------|
| JS-021 | No `lock()` statement introduced | PASS — none in any changed method body |
| JS-001 | No unhandled throw in dispatch path; exceptions caught at boundary | PASS — `SubmitReplacementStopOrder` wraps in `try/catch`; no propagation |
| JS-002 | Void return for dispatch methods | PASS — all three methods are `void` |
| ASCII-only | No Unicode, curly quotes, em-dashes, or non-ASCII characters in string literals | PASS — all literals are pure ASCII |
| No `DateTime.Now` | No `DateTime.Now` introduced | PASS — none introduced |
| No `FontFamily` | No `FontFamily` reference introduced | PASS — none introduced |
| CYC <= 8 | All changed methods have CYC <= 8 | PASS — max CYC is 4 (`SubmitReplacementStopOrder`) |
| `apply_diff` only | `CopyEngine.cs` edits use `apply_diff` (no `write_file`) | ENFORCED — engineer constraint in Ph4a |
| `ptt-sync.ps1` | Run after all edits | ENFORCED — engineer constraint in Ph4a, after ALL tickets complete |
| Line traceability | Every changed line traces to DW-B171-01 or DW-B171-02 | PASS — all changes are suffix propagation or cancel sweep |

---

## 9. Ticket Outline

### B171-1 — `ExecutePhaseCStopReplacement`: Suffix + Cancel Sweep

**File:** `src/PropTraderTools/CopyEngine.cs`
**Lines:** 4507-4512
**Bugs closed:** DW-B171-01 (duplicate stop creation)
**Change:** Implement Method A AFTER block exactly.
**Summary:** Add `suffix = bracketIdx.ToString()`, price guard (`if stp <= 0 → return`), `CancelLiveCollateralStop` call, pass `suffix` to `CreateFollowerReplacementStop`.
**CYC:** 2 → 2 (delta 0)
**Dependency:** None (no prior ticket required)

### B171-2 — `CreateFollowerReplacementStop` + `SubmitReplacementStopOrder`: Suffix Threading

**File:** `src/PropTraderTools/CopyEngine.cs`
**Lines:** 4437-4451 (`CreateFollowerReplacementStop`), 4458-4498 (`SubmitReplacementStopOrder`)
**Bugs closed:** DW-B171-02 (fo=NULL on stop drag)
**Change:** Implement Method B AFTER and Method C AFTER blocks exactly.
**Summary:** Add `string suffix` param to both methods; change order name from `"PTT-STP-Drag"` to `"PTT-STP-Drag-" + suffix`; update all StatusUpdate messages to include suffix.
**CYC:** 2 → 2 (delta 0), 4 → 4 (delta 0)
**Dependency:** **DEPENDS ON B171-1.** B171-1 must be committed first. The file only compiles once BOTH B171-1 and B171-2 edits are applied. Engineer applies both apply_diff blocks before building.

### B171-3 — xUnit Tests

**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Bugs verified:** DW-B171-01 + DW-B171-02
**Change:** Add 5 xUnit `[Fact]` tests covering:
1. Single target drag → exactly one `PTT-STP-Drag-{N}` in follower orders; zero `PTT-STP-Drag` (unsuffixed)
2. Two consecutive target drags on same bracket → still exactly one `PTT-STP-Drag-{N}` (duplicate cancelled, not stacked)
3. Two target drags on different brackets → two stops with different suffixes; neither cancels the other
4. Stop drag after target drag → `fo != NULL` for stop matching `"PTT-STP-Drag-{N}"`
5. `stp <= 0` path → `ExecutePhaseCStopReplacement` exits without calling `CancelLiveCollateralStop` (EC-1 guard)

**Access pattern:** Tests exercise via public `HandleBracketChange` surface or `InternalsVisibleTo` test-seam helpers (pattern already established in `CopyEngineTests.cs`). No direct calls to private methods.
**Dependency:** DEPENDS ON B171-1 + B171-2 both complete and compiling.

### Ticket Dependency Graph

```
B171-1  →  B171-2  →  B171-3
```

Sequential. No parallel execution. B171-2 and B171-3 must not begin until the prior ticket is applied to `CopyEngine.cs`.

---

## 10. Data Flow Summary

```
Target-drag event
  → ExecutePhaseCStopReplacement(acc, fo, leaderOrder)
      bracketIdx = DeriveLeaderBracketIndex(leaderOrder)       [int, e.g. 3]
      suffix = bracketIdx.ToString()                           ["3"]
      stp = FindLeaderStopPrice(leaderOrder?.Account, bracketIdx)
      if stp <= 0: StatusUpdate + return                       [EC-1 guard]
      CancelLiveCollateralStop(acc, fo, "PTT-STP-Drag-3")      [dedup sweep]
      CreateFollowerReplacementStop(acc, instr, qty, action, stp, "3")
          if stopPrice <= 0: StatusUpdate + return             [defensive]
          SubmitReplacementStopOrder(acc, instr, qty, action, stp, "3")
              CreateOrder(..., "PTT-STP-Drag-3", ...)          [FIX: suffixed]
              Submit(newStop)
              StatusUpdate: "PTT-STP-Drag-3 placed @ {price}"

Stop-drag event (later)
  → IsDragNameMatch(orderName="PTT-STP-Drag-3", legSuffix="3")
      "PTT-STP-Drag-3" == "PTT-STP-Drag-" + "3"  → TRUE      [FIX: match succeeds]
      fo != NULL  → stop drag synced correctly
```

---

## 11. NT8 API Surface

All NT8 API calls used in the changed methods are present in the BEFORE blocks (pre-validated) and are AddOnBase-compatible:

| NT8 API | Method used in | AddOnBase available | Change in B171 |
|---------|----------------|---------------------|----------------|
| `Account.CreateOrder(...)` | `SubmitReplacementStopOrder` | YES | Order name string only (`"PTT-STP-Drag-" + suffix`); signature unchanged |
| `Account.Submit(Order[])` | `SubmitReplacementStopOrder` | YES | No change |
| `CancelLiveCollateralStop(acc, fo, name)` | `ExecutePhaseCStopReplacement` (NEW CALL) | N/A (internal CopyEngine method) | Reused as-is; new call site added |

Zero new NT8 API methods introduced. All NT8 calls are proven by the BEFORE blocks and by the `ResubmitOneCollateralLeg` reference pattern (which already calls `CancelLiveCollateralStop` in the same context).

**KEY NT8 FACTS applied:**
- `AtmStrategyChangeStopTarget()` — NOT used (StrategyBase-only, not available in AddOnBase). N/A.
- `AtmStrategyCreate()` — NOT used (StrategyBase-only). N/A.
- `Account.Change()` — NOT used (silent no-op on ATM-owned brackets). N/A.
- `Account.Cancel() + Account.CreateOrder() + Submit()` — AddOnBase confirmed. Used correctly.

---

## 12. Threading Model

No threading changes introduced. All three changed methods are called within the NT8 order-change event callback chain, which is the same thread context used by the existing `ResubmitOneCollateralLeg` reference pattern. `CancelLiveCollateralStop` is already proven thread-safe in that context.

- No `Dispatcher.InvokeAsync` required (no new UI operations).
- No `ConcurrentQueue` changes (no new state enqueued).
- `StatusUpdate?.Invoke(...)` follows the existing null-conditional event pattern throughout the file.
- `suffix` is a local `string` (immutable reference type). No shared mutable state introduced.

---

## 13. Deferred Backlog Carry-Forward

The following items from `B170/06-deferred-backlog.md` are carried into B171:

| ID | Block | Description | Priority | Status in B171 |
|----|-------|-------------|----------|----------------|
| DW-B171-01 | B171 | DUPLICATE-STP-DRAG-ON-REPEATED-TGT-DRAG | P0 | TARGET — expected CLOSED on VERIFY_PASS of B171-1+B171-2 |
| DW-B171-02 | B171 | fo=NULL-ON-STOP-DRAG-WHEN-STP-IS-PTT-NAMED | P0 | TARGET — expected CLOSED on VERIFY_PASS of B171-1+B171-2 |
| DW-B170-SIM | B170 | SIM GATE: Director drags target on leader — confirm follower stop preserved. DW-B170-01 live regression verify. | P0 | OPEN — may close in B171 if SIM gate run confirms stop-drag sync |
| DW-B169-K1 | B169 | SIM GATE: 3-stop simultaneous drag — 0 missing follower stops | P0 | OPEN (Sim102 blocked by B167-DEFER-02) |
| DW-B169-K2 | B169 | SIM GATE: single-stop drag regression | P0 | OPEN |
| DW-B168-K1 | B168 | Superseded by DW-B169-K1/K2 | P0 | OPEN (superseded) |
| B167-DEFER-02 | B167 | fo=NULL: follower ATM brackets never placed after leader fill | P1 | OPEN |
| B167-DEFER-01 | B167 | ECA-1 double-position race. Monitor at SIM. | P2 | OPEN |
| B167-DEFER-03 | B167 | Combined B161-B166 SIM gate pending | P1 | OPEN |
| B167-DEFER-04 | B167 | B167 SIM gate pending | P1 | OPEN |
| DW-B159-K2 | B159 | 14 xUnit tests | P2 | OPEN |
| DW-B159-K3 | B159 | Attempt cap hardcoded at 3 | P2 | OPEN |
| DW-B159-K4 | B159 | intendedBePrice sourcing | P2 | OPEN |
| DW-B159-K5 | B159 | Stale line comment | P3 | OPEN |

---

## GATE RESULT

**PLAN_COMPLETE**
