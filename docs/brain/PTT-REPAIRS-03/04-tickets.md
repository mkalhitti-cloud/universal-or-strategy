# PTT-REPAIRS-03 Tickets

**Block**: PTT-REPAIRS-03
**Source plan**: `docs/brain/PTT-REPAIRS-03/02-architecture-plan.md` (REVIEW_PASS — Cycle 2, Revision 2)
**Plan review**: `docs/brain/PTT-REPAIRS-03/02-plan-review.md` (REVIEW_PASS — Cycle 2 FINAL)
**Engineer contract**: Two tickets. Implement exactly. No phantom work. No unrelated changes.
**[Fact] baseline**: 475 (measured via Select-String, confirmed STEP 0 of architecture plan)
**[Fact] target after T1**: 476
**[Fact] target after T2 (final)**: 477
**Date**: 2026-09-08
**Revision**: Ticket Revision Cycle 1 — T2 corrected (T2-TRACE-01, T2-TRACE-02, T2-TEST-01 resolved)

---

## PTT-REPAIRS-03-T1

### A. Ticket ID and Title

**ID**: PTT-REPAIRS-03-T1
**Title**: Fix reversal guard false-positive when follower has working entry orders (BUG-A)

---

### B. Spec Requirement IDs

- **BUG-A**: Reversal guard `ShouldSkipForReversalGuard` incorrectly blocks dispatch to a follower
  that has no filled position but HAS a working (unfilled) entry order. The flat check uses
  position-only semantics; a follower with a working entry order is NOT flat.
- **DW-REPAIRS-03-01** (promoted from deferred to T1 in-scope, V-02 remediation):
  `HasWorkingEntries` enumerates `acc.Orders` without `.ToList()` snapshot, creating a
  `InvalidOperationException` risk when called from the `OnOrderUpdate` hot path (the new T1
  call site). Fix: `acc.Orders.ToList()` — mirrors `HasWorkingPttCopy` (line 4863).

---

### C. Scope Lock

**SCOPE LOCK — TICKET T1 ONLY**

T1 touches exactly four locations in `CopyEngine.cs`:
1. One line changed in `ShouldSkipForReversalGuard` (line 2594)
2. Two comment lines updated above `ShouldSkipForReversalGuard` (lines 2579–2580)
3. One line changed in `HasWorkingEntries` (line 4842)
4. Four comment lines replaced at `HasWorkingEntries` header (line 4839)

T1 adds exactly ONE `[Fact]` test in `CopyEngineTests.cs`.

**Do NOT touch** `IsReversalToFlatFollower` (line 5916) — unchanged.
**Do NOT touch** any method targeted by T2 (`DispatchCopy`, `IsLiveEntryBlocked`, `IsEntryDispatched`).
**Do NOT touch** any other method not listed in this ticket.

---

### D. Files to Modify

| File | Action |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Modify (4 targeted locations) |
| `src/PropTraderTools/CopyEngineTests.cs` | Add 1 `[Fact]` test method |

---

### E. Method Signatures — Exact Before/After

#### E.1 `ShouldSkipForReversalGuard` — change at line 2594

**Signature** (unchanged — internal, no signature delta):
```csharp
internal bool ShouldSkipForReversalGuard(
    Account acc,
    NinjaTrader.Cbi.Instrument instr,
    OrderAction currentAction,
    OrderAction lastAction,
    bool hasLastDirection
)
```

**Before** (line 2594):
```csharp
bool followerIsFlat = IsFlat(FindPosition(acc, instr));
```

**After**:
```csharp
bool followerIsFlat = IsFlat(FindPosition(acc, instr))
                      && !HasWorkingEntries(acc, instr);
```

**Comment update** — replace lines 2579–2580:

Before (lines 2579–2580):
```csharp
        // Returns true when hasLastDirection is true and follower is flat and direction reversed.
        // CCN<=3: hasLastDirection + IsReversalToFlatFollower + IsFlat = 3 Lizard branches.
```

After:
```csharp
        // Returns true when hasLastDirection is true and follower is truly flat (no position AND
        // no working entries) and direction reversed.
        // CCN<=4: hasLastDirection + IsReversalToFlatFollower + IsFlat + HasWorkingEntries = 4 Lizard branches.
```

Full method body after change (structural, for engineer reference):
```csharp
        internal bool ShouldSkipForReversalGuard(
            Account acc,
            NinjaTrader.Cbi.Instrument instr,
            OrderAction currentAction,
            OrderAction lastAction,
            bool hasLastDirection
        )
        {
            if (!hasLastDirection)
                return false;
            bool followerIsFlat = IsFlat(FindPosition(acc, instr))
                                  && !HasWorkingEntries(acc, instr);
            if (!IsReversalToFlatFollower(currentAction, lastAction, followerIsFlat))
                return false;
            NinjaTrader.Code.Output.Process(
                "[PTT-COPY-GUARD] skip reversal entry: "
                    + acc.Name
                    + " "
                    + instr.FullName
                    + " cur=" + currentAction
                    + " last=" + lastAction
                    + " flat=" + followerIsFlat,
                NinjaTrader.NinjaScript.PrintTo.OutputTab1
            );
            return true;
        }
```

#### E.2 `HasWorkingEntries` — change at line 4839 (comment) and line 4842 (body)

**Signature** (unchanged):
```csharp
private bool HasWorkingEntries(Account acc, Instrument instrument)
```

**Before** (line 4839 comment, line 4842 body):
```csharp
        // CYC=3. Returns true if any working non-bracket order exists for the instrument.
        private bool HasWorkingEntries(Account acc, Instrument instrument)
        {
            foreach (var order in acc.Orders) // (1) branch
```

**After**:
```csharp
        // CYC=5: foreach(1) + instrument check(2) + state check(3) + bracket check(4) + early-return path(5).
        // Returns true if any working non-bracket order exists for the instrument.
        // JS-001: acc.Orders.ToList() snapshot prevents InvalidOperationException on concurrent NT8 modification
        //         (pattern mirrors HasWorkingPttCopy line 4861). Promoted in-scope by PTT-REPAIRS-03 V-02 fix.
        private bool HasWorkingEntries(Account acc, Instrument instrument)
        {
            foreach (var order in acc.Orders.ToList()) // JS-001: snapshot live collection; pattern matches HasWorkingPttCopy
```

The remainder of `HasWorkingEntries` body (lines 4843–4851) is **unchanged**.

---

### F. Jane Street Rule Constraints

| Rule | Constraint | Applied where |
|------|-----------|---------------|
| **JS-001** | No throw in gate chain. `acc.Orders.ToList()` snapshots the live collection before enumeration, preventing `InvalidOperationException` from concurrent NT8 modification on `OnOrderUpdate` hot path. | `HasWorkingEntries` line 4842 |
| **JS-002** | Predicates return `bool`. No null return. | `ShouldSkipForReversalGuard` (unchanged), `HasWorkingEntries` (unchanged) |
| **JS-003** | No magic-string state discrimination. | Not applicable to T1 changes |
| **JS-021** | No `lock()`. `FindPosition`, `IsFlat`, `HasWorkingEntries` are all lock-free reads. `acc.Orders.ToList()` is lock-free. | Both changed methods |
| **JS-023** | Immutable where possible. `ShouldSkipForReversalGuard` is a pure predicate (no side effects). `HasWorkingEntries` is a pure predicate (no side effects). | Both changed methods |
| **JS-066** | CYC ≤ 8 per method. `ShouldSkipForReversalGuard` post-T1 = 4. `HasWorkingEntries` = 5 (stale comment corrected; no CYC delta from `.ToList()`). | Both changed methods |
| **JS-042** | ASCII-only identifiers and string literals. All changed lines are ASCII-only. | All changed lines |

---

### G. xUnit `[Fact]` Test Specification

**Test method name**: `ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders`

**File**: `src/PropTraderTools/CopyEngineTests.cs`

**Block position**: Append after the last existing `[Fact]` in the file (line ~6237+), or group
with existing `ShouldSkipForReversalGuard` tests if a grouping section exists.

**Scenario**: Follower has no filled position (`IsFlat` returns `true`) but HAS a working entry
order (`HasWorkingEntries` returns `true`). Leader dispatches a Buy (reversal from last direction
Sell, `hasLastDirection = true`).

With the fix applied: `followerIsFlat = IsFlat(pos)==true && !HasWorkingEntries()==false = false`.
`IsReversalToFlatFollower(Buy, Sell, false)` = `false`.
Therefore `ShouldSkipForReversalGuard` must return `false` (dispatch allowed, no skip).

**Arrange/Act/Assert outline**:

```csharp
[Fact]
public void ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders()
{
    // Arrange
    // Option A (preferred if Account/Position can be stubbed in test harness):
    //   Create a test-double Account with:
    //     - No filled position for the instrument (FindPosition returns null or qty=0 -> IsFlat=true)
    //     - One Working, non-bracket order for the instrument (HasWorkingEntries returns true)
    //   Use the same Account/Position mock pattern established in existing CopyEngineTests.cs.
    //
    // Option B (fallback if NT8 Account is not mockable in this harness):
    //   Test the two sub-predicates independently:
    //   (B1) Assert IsReversalToFlatFollower(OrderAction.Buy, OrderAction.Sell, followerIsFlat: false)
    //        returns false.  This is a pure static call requiring no NT8 types.
    //   (B2) Assert via reflection that ShouldSkipForReversalGuard returns false when invoked
    //        with a follower account that has a Working order (use GetMethod pattern).
    //
    // Engineer must implement the pattern consistent with the existing test infrastructure.
    // Do NOT introduce a new mocking framework. Use what already exists in this test file.

    // Act
    // bool result = _engine.ShouldSkipForReversalGuard(acc, instr,
    //     OrderAction.Buy, OrderAction.Sell, hasLastDirection: true);
    // OR via reflection:
    // var m = GetMethod("ShouldSkipForReversalGuard") or direct call (internal, InternalsVisibleTo L46)

    // Assert
    // Assert.False(result);
    // The reversal guard must NOT skip (must allow dispatch) when the follower has working orders.
}
```

**What this test asserts**:
1. When `HasWorkingEntries` returns `true` for the follower account+instrument, the computed
   `followerIsFlat` is `false`, and `ShouldSkipForReversalGuard` returns `false`.
2. Dispatch to this follower is allowed despite the `IsFlat(position)` check returning `true`.

---

### H. 7-SCAN CHECKLIST (Engineer Contract)

**SCAN-01: `lock()` grep — zero matches in changed methods**
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'lock\s*\(' |
    Where-Object { $_.LineNumber -ge 2578 -and $_.LineNumber -le 2610 }
# Expected: 0 matches

Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'lock\s*\(' |
    Where-Object { $_.LineNumber -ge 4839 -and $_.LineNumber -le 4852 }
# Expected: 0 matches
```
Pass condition: Zero matches in the changed line ranges.

**SCAN-02: Unicode/emoji/curly-quote grep — zero matches in changed lines**
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '[^\x00-\x7F]' |
    Where-Object { $_.LineNumber -ge 2578 -and $_.LineNumber -le 2610 }
# Expected: 0 matches

Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '[^\x00-\x7F]' |
    Where-Object { $_.LineNumber -ge 4839 -and $_.LineNumber -le 4852 }
# Expected: 0 matches
```
Pass condition: Zero matches in the changed line ranges.

**SCAN-03: CYC check — all modified methods ≤ 8**

| Method | Expected CYC post-T1 |
|--------|---------------------|
| `ShouldSkipForReversalGuard` | 4 (base=1 + !hasLastDirection=2 + !IsReversal=3 + &&HasWorkingEntries=4) |
| `HasWorkingEntries` | 5 (base=1 + foreach=2 + instrument check=3 + state check=4 + bracket check=5) |

Engineer must manually verify branch count matches these values before submitting.
Both values ≤ 8. Pass condition: no method exceeds 8.

**SCAN-04: `[Fact]` count — Select-String baseline + delta verification**
```powershell
# Before T1 implementation:
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern '^\s*\[Fact\]' | Measure-Object
# Expected before: 475

# After T1 implementation:
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern '^\s*\[Fact\]' | Measure-Object
# Expected after: 476 (baseline 475 + 1 new test)
```
Pass condition: count increases from 475 to 476 (delta = +1). No more, no less.

**SCAN-05: Build — msbuild or dotnet build succeeds zero errors**
```powershell
# Run from repo root:
powershell -File .\scripts\build_readiness.ps1
# OR:
dotnet build src\PropTraderTools\PropTraderTools.csproj
```
Pass condition: Build exits code 0 with zero errors. Warnings are acceptable; errors are not.

**SCAN-06: Null-conditional event handler check (NT8-043)**
T1 does NOT add or modify any event handler wire-up or unwire code.
**NT8-043 is NOT APPLICABLE to T1. State explicitly in PR: "SCAN-06: N/A — no event handlers changed in T1."**

**SCAN-07: `HasWorkingEntries` `.ToList()` check**
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'acc\.Orders\.ToList\(\)' |
    Where-Object { $_.LineNumber -ge 4839 -and $_.LineNumber -le 4852 }
# Expected: 1 match at line 4842 (or the new line number after edit)
```
Pass condition: Exactly 1 match of `acc.Orders.ToList()` in the `HasWorkingEntries` body range.
(T2 SCAN-07: state N/A for T2.)

---

### I. NT8 Constraints

- **No `lock()` keyword** anywhere in T1 changes. `IsFlat`, `FindPosition`, `HasWorkingEntries`
  are all lock-free reads. `acc.Orders.ToList()` is an enumeration snapshot — no lock required.
- **Actor pattern**: `DispatchCopy` gate chain is the actor entry point. T1 narrows one predicate
  inside the gate chain with no new shared-state writes.
- **ASCII-only**: All changed identifiers and string literals (including comment text) are
  printable ASCII. No Unicode, no curly quotes, no em-dashes, no emoji.
- **`DateTime.UtcNow`**: Not applicable — T1 does not involve time-based logic.
- **`FontFamily`**: Not applicable.
- **`PTT-` order name prefix**: Not applicable — T1 does not create orders.
- **Dispatcher.InvokeAsync**: Not applicable — T1 does not touch UI or event handler code.

---

### J. CYC Budget Summary

| Method | Before | After | Budget | Status |
|--------|--------|-------|--------|--------|
| `ShouldSkipForReversalGuard` | 3 | **4** | ≤ 8 | ✓ |
| `HasWorkingEntries` | 5 (stale comment said 3; actual = 5) | **5** (unchanged) | ≤ 8 | ✓ |

Delta: `ShouldSkipForReversalGuard` +1 (the `&&` operand on `followerIsFlat` assignment is one
Lizard decision branch). `HasWorkingEntries` 0 delta (`.ToList()` is a method call, not a branch).

---

### K. PTT-DIAG Log Preservation

All 5 PTT-DIAG log lines are **PERMANENT** and must survive T1 unchanged.

| Log prefix | Source location | T1 impact |
|-----------|----------------|-----------|
| `[PTT-COPY-DIAG] gate0.5 exit:` | `DispatchCopy` lines 2433–2440 | Not touched by T1 ✓ |
| `[PTT-COPY-DIAG] gate3 exit:` | `DispatchCopy` lines 2446–2453 | Not touched by T1 ✓ |
| `[PTT-COPY-DIAG] gate4 exit:` | `DispatchCopy` lines 2459–2466 | Not touched by T1 ✓ |
| `[PTT-COPY-DIAG] gate5 exit:` | `DispatchCopy` lines 2476–2482 | Not touched by T1 ✓ |
| `[PTT-COPY-GUARD] skip reversal entry:` | `ShouldSkipForReversalGuard` lines 2597–2607 | **Preserved exactly** — log is inside the method body AFTER the `followerIsFlat` change; T1 does not remove or alter the log statement ✓ |

The `[PTT-COPY-GUARD]` log fires on the same condition as before (when `IsReversalToFlatFollower`
returns true). The T1 change narrows when `followerIsFlat` is `true` — the log still fires
correctly when a genuine reversal-to-flat skip is triggered.

---

### L. Hard-Link Sync Instructions

After ALL `.cs` edits for this ticket are complete, run the following in order:

```powershell
# Step 1: deploy-sync (re-synchronizes all NinjaTrader hard links)
powershell -File .\deploy-sync.ps1

# Step 2: Manual PropTraderTools re-link (run if deploy-sync does not cover these files)
$nt8Dir  = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools"
$repoDir = "C:\WSGTA\universal-or-strategy\src\PropTraderTools"
foreach ($f in @("CopyEngine.cs","TradeCopierAddOn.cs","TradeCopierPanel.cs",
                 "TradeCopierWindow.cs","AtrSizingEngine.cs","FeatureFlags.cs",
                 "LicenseClient.cs")) {
    Remove-Item "$nt8Dir\$f" -Force
    New-Item -ItemType HardLink -Path "$nt8Dir\$f" -Value "$repoDir\$f" | Out-Null
}
```

Note: If T1 and T2 are applied in the same session, run hard-link sync **once** after BOTH
tickets are fully implemented and the build passes — not after each ticket individually.

---

---

## PTT-REPAIRS-03-T2

### A. Ticket ID and Title

**ID**: PTT-REPAIRS-03-T2
**Title**: Fix phantom instrKey set when no follower dispatch occurs (BUG-B)
**Revision**: Ticket Revision Cycle 1 — T2-TRACE-01, T2-TRACE-02, T2-TEST-01 resolved

---

### B. Spec Requirement IDs

- **BUG-B**: `IsLiveEntryBlocked` writes `_liveEntryInstruments[instrKey]` and
  `_entryInstrKeyByOrderId[orderId]` BEFORE any follower dispatch occurs. If all followers are
  skipped (dispatched == 0), the instrKey is phantom-locked — the next order for the same
  instrKey is incorrectly blocked at gate5(a) even though no copy was ever sent.
- **V-01 (DispatchCopy CYC)**: Current `DispatchCopy` CYC = 8 (confirmed from source).
  Adding the `if (dispatched > 0)` guard (+1 CYC) would push to 9, violating JS-066.
  Resolution: extract the two per-follower skip-guard if-blocks into `ShouldSkipFollower`,
  collapsing two branches to one (8 → 7), then add the guard (7 → 8). Final CYC = 8.

---

### C. Scope Lock

**SCOPE LOCK — TICKET T2 ONLY**

T2 touches exactly these locations in `CopyEngine.cs`:
1. `DispatchCopy` (lines 2424–2537): four targeted changes (comment update, loop extraction,
   gate5 call-site rename, dispatched counter + post-loop setter)
2. `IsLiveEntryBlocked` (lines 5739–5759): **deleted in full**
3. `IsEntryDispatched` (lines 5727–5737): simplified body + comment update
4. New method `ShouldSkipFollower`: added in `CopyEngine.cs` (placement: near
   `ShouldSkipFollowerDispatch` and `ShouldSkipForReversalGuard` — approximately after line 2577)
5. New method `IsLiveEntryBlocked_Check`: added in `CopyEngine.cs` (placement: where
   `IsLiveEntryBlocked` was, approximately line 5748)
6. New method `SetLiveEntryDispatched`: added in `CopyEngine.cs` (placement: immediately after
   `IsLiveEntryBlocked_Check`)
7. **`IsLiveEntryBlocked_ForTest` shim (lines 4302–4306, `#region B143 test seam`)**:
   redirect from `IsLiveEntryBlocked` to combined check+commit semantics. See E.3 for full
   disposition. This is required to prevent a compile error after `IsLiveEntryBlocked` is deleted.

T2 adds exactly ONE `[Fact]` test in `CopyEngineTests.cs`.

**Existing `[Fact]` `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` (line 7784)**:
Updated via `IsLiveEntryBlocked_ForTest` shim redirection. The test is **NOT deleted**.
Its behavioral contract (fill clears instrKey → reentry passes gate5) remains valid after T2.
See Section E.3 for shim body and Section G for full test disposition analysis.

**Do NOT touch** `EvictDedup` — unchanged.
**Do NOT touch** `ClearLiveEntryForInstrument` — unchanged.
**Do NOT touch** `IsDedup` — unchanged (its internal `_dedupCache.TryAdd` side effect is preserved).
**Do NOT touch** `ShouldSkipForReversalGuard` — that is T1 scope.
**Do NOT touch** `HasWorkingEntries` — that is T1 scope.
**Do NOT touch** any other B143 test seam shim (lines 4308–4319) — unchanged.

---

### D. Files to Modify

| File | Action |
|------|--------|
| `src/PropTraderTools/CopyEngine.cs` | Modify (7 targeted locations: 1 method changed, 1 method deleted, 1 method simplified, 3 new methods added, 1 test seam shim redirected) |
| `src/PropTraderTools/CopyEngineTests.cs` | Add 1 `[Fact]` test method (existing test preserved — see Section G) |

---

### E. Method Signatures — Exact Before/After

#### E.1 `ShouldSkipFollower` — NEW METHOD (add near line 2577, after `ShouldSkipForReversalGuard`)

**Before**: Does not exist.

**After** (new method — add to `CopyEngine.cs`, placement after `ShouldSkipForReversalGuard` method, before `DispatchToFollower`):

```csharp
        // Extracted from DispatchCopy loop to reduce DispatchCopy CYC budget (V-01 fix: PTT-REPAIRS-03).
        // Short-circuits identically to the original two sequential ifs: ShouldSkipFollowerDispatch first,
        // then ShouldSkipForReversalGuard. Zero new logic.
        // CYC=3: base + dispatch-skip check + reversal-guard check.
        // JS-001: no throw. JS-021: no lock. JS-042: ASCII-only.
        private bool ShouldSkipFollower(
            Account acc,
            NinjaTrader.Cbi.Instrument instr,
            OrderAction currentAction,
            OrderAction lastAction,
            bool hasLastDirection
        )
        {
            if (ShouldSkipFollowerDispatch(acc))
                return true;
            if (ShouldSkipForReversalGuard(acc, instr, currentAction, lastAction, hasLastDirection))
                return true;
            return false;
        }
```

**Signature**:
```
private bool ShouldSkipFollower(Account acc, NinjaTrader.Cbi.Instrument instr, OrderAction currentAction, OrderAction lastAction, bool hasLastDirection)
CYC: 3
Side effects: NONE (pure predicate — delegates to no-side-effect predicates)
```

#### E.2 `DispatchCopy` — four changes (lines 2424–2537)

**Signature** (unchanged):
```csharp
private void DispatchCopy(Order order, CopyRule rule)
```

**Change 0 — Stale CYC comment update (line 2427)**:

Before:
```csharp
        // CYC<=6 after extraction. JS-001: no throw in hot path. JS-021: no lock.
```

After:
```csharp
        // CYC=8 after ShouldSkipFollower extraction + T2 dispatched-guard (PTT-REPAIRS-03).
        // JS-001: no throw in hot path. JS-021: no lock.
```

**Change 1 — Gate5 call-site rename (line 2474)**:

Before:
```csharp
            if (IsLiveEntryBlocked(instrKey, orderId, order.LimitPrice)) // DW-B142-MGC-02
```

After:
```csharp
            if (IsLiveEntryBlocked_Check(instrKey, orderId, order.LimitPrice)) // DW-B142-MGC-02
```

All surrounding gate5 log lines (2476–2482) are **unchanged**.

**Change 2 — `dispatched` counter initialization (add before line 2507)**:

Before (line 2507):
```csharp
            // B8 T1: index-tracking loop applies per-follower multiplier
            int idx = 0;
```

After:
```csharp
            // B8 T1: index-tracking loop applies per-follower multiplier
            int dispatched = 0;
            int idx = 0;
```

**Change 3 — ShouldSkipFollower extraction + dispatched increment in loop body (lines 2510–2531)**:

Before (lines 2510–2531):
```csharp
                if (ShouldSkipFollowerDispatch(acc))
                {
                    idx++;
                    continue;
                }

                if (
                    ShouldSkipForReversalGuard(
                        acc,
                        instr,
                        currentAction,
                        lastAction,
                        hasLastDirection
                    )
                )
                {
                    idx++;
                    continue;
                }

                DispatchToFollower(acc, order, rule, idx, baseSignal, baseQty);
                idx++;
```

After:
```csharp
                if (ShouldSkipFollower(acc, instr, currentAction, lastAction, hasLastDirection))
                {
                    idx++;
                    continue;
                }

                DispatchToFollower(acc, order, rule, idx, baseSignal, baseQty);
                idx++;
                dispatched++;
```

**Change 4 — Post-loop `SetLiveEntryDispatched` guard (add after line 2532, before line 2534)**:

Before (line 2533 — close brace of foreach, then line 2534 comment):
```csharp
            }

            // B119: DW-B128 -- record direction dispatched for this instrument.
```

After:
```csharp
            }
            if (dispatched > 0)
                SetLiveEntryDispatched(instrKey, orderId);

            // B119: DW-B128 -- record direction dispatched for this instrument.
```

**Full DispatchCopy structure after all four changes** (structural outline for verification):
```
DispatchCopy (CYC=8)
  [branch 2] if (IsExitSignalName)         -> gate0.5 log + return
  [branch 3] if (!IsDispatchTriggerState)  -> gate3 log + return
  [branch 4] if (!IsDispatchableOrderType) -> gate4 log + return
  [branch 5] if (IsLiveEntryBlocked_Check) -> gate5 log + return
  int dispatched = 0;
  int idx = 0;
  [branch 6] foreach (var acc in rule.FollowerAccounts)
  {
    [branch 7] if (ShouldSkipFollower(acc, instr, cur, last, hasLastDir)) -> idx++; continue;
    DispatchToFollower(acc, order, rule, idx, baseSignal, baseQty);
    idx++;
    dispatched++;
  }
  [branch 8] if (dispatched > 0)
    SetLiveEntryDispatched(instrKey, orderId);
  _lastLeaderDirection[instr.FullName] = currentAction;
```

#### E.3 `IsLiveEntryBlocked` — DELETED (lines 5748–5759) + `IsLiveEntryBlocked_ForTest` REDIRECTED (line 4302–4306)

**IsLiveEntryBlocked deletion:**

**Before** (lines 5739–5759, full method with comment):
```csharp
        // DW-B142-MGC-02: Gate 5 compound predicate for DispatchCopy.
        // CYC=4: liveInstr guard(1) + IsDedup(2) + IsEntryDispatched(3).
        // Returns true (block dispatch) on any of:
        //   (a) instrument already has a live entry dispatched this slot -- blocks resubmit dup.
        //   (b) same orderId seen before -- orderId-level dup guard.
        //   (c) orderId was previously dispatched and survived EvictDedup -- eviction-bypass guard.
        // On false (first real dispatch): records instrKey in _liveEntryInstruments
        //   and orderId in _entryInstrKeyByOrderId.
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool. ASCII-only.
        private bool IsLiveEntryBlocked(string instrKey, string orderId, double limitPrice)
        {
            if (_liveEntryInstruments.ContainsKey(instrKey))
                return true;
            if (IsDedup(orderId, limitPrice))
                return true;
            if (IsEntryDispatched(orderId))
                return true;
            _liveEntryInstruments.TryAdd(instrKey, 0);
            _entryInstrKeyByOrderId.TryAdd(orderId, instrKey);
            return false;
        }
```

**After**: Method is **entirely deleted**. No wrapper or stub is retained.
The single production call site in `DispatchCopy` is updated to `IsLiveEntryBlocked_Check` (see E.2 Change 1).

**Caller count correction (T2-TRACE-01 fix)**:
After deleting `IsLiveEntryBlocked`, two callers exist that must be addressed:
1. **`IsLiveEntryBlocked_ForTest` at line 4306** — delegates to `IsLiveEntryBlocked`. After deletion
   this is a compile error. Disposition: **redirect the shim body** (see below).
2. **Comment at line 201** (`// Written in IsLiveEntryBlocked at Gate 5 pass time.`) — stale.
   Engineer must update or remove this comment during the deletion pass to avoid misleading
   documentation. Not a compile issue but must be addressed.

Verification command after deletion:
```powershell
# Must return zero matches (no remaining calls to the deleted method):
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'IsLiveEntryBlocked[^_]'
# Expected: 0 matches
```

---

**IsLiveEntryBlocked_ForTest shim — REDIRECT (line 4302–4306, `#region B143 test seam`)**:

**Before** (lines 4302–4306):
```csharp
        internal bool IsLiveEntryBlocked_ForTest(
            string instrKey,
            string orderId,
            double limitPrice
        ) => IsLiveEntryBlocked(instrKey, orderId, limitPrice);
```

**After** (redirect to replicate DispatchCopy check+commit semantics):
```csharp
        // B143 test seam: after PTT-REPAIRS-03 B1 split, replicates DispatchCopy check+commit path.
        // Calls IsLiveEntryBlocked_Check (pure predicate) -- if not blocked, calls SetLiveEntryDispatched
        // to record the maps exactly as DispatchCopy does after dispatched > 0.
        // This preserves the behavioral contract of IsLiveEntryBlocked_ClearsOnFill_AllowsReentry:
        //   first call passes gate + sets instrKey; fill clears it; second call passes again.
        internal bool IsLiveEntryBlocked_ForTest(
            string instrKey,
            string orderId,
            double limitPrice
        )
        {
            if (IsLiveEntryBlocked_Check(instrKey, orderId, limitPrice))
                return true;
            SetLiveEntryDispatched(instrKey, orderId);
            return false;
        }
```

**Signature** (unchanged — same external contract: `internal bool`, same parameters):
```
internal bool IsLiveEntryBlocked_ForTest(string instrKey, string orderId, double limitPrice)
CYC: 2 (base=1 + if-IsLiveEntryBlocked_Check=2)
Side effects: calls SetLiveEntryDispatched on pass (writes _liveEntryInstruments, _entryInstrKeyByOrderId, _entryDispatchedOrders)
```

**Rationale**: The test seam must replicate the production behavior post-split so that the existing
`[Fact]` test `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` continues to verify the correct
behavioral guarantee: (1) first call passes gate5 AND sets instrKey; (2) fill EvictDedup clears it;
(3) second call passes gate5 again. Splitting the shim into check+commit exactly mirrors what
`DispatchCopy` now does, keeping the test valid without modification.

#### E.4 `IsLiveEntryBlocked_Check` — NEW METHOD (placement: where `IsLiveEntryBlocked` was, ~line 5748)

**Before**: Does not exist.

**After** (new method — add at the location where `IsLiveEntryBlocked` was deleted):

```csharp
        // DW-B142-MGC-02 (PTT-REPAIRS-03 B1 split): pure check predicate for DispatchCopy gate5.
        // CYC=4: base(1) + instrKey ContainsKey(2) + IsDedup(3) + orderId ContainsKey(4).
        // Returns true (block dispatch) on any of:
        //   (a) instrument already has a live entry dispatched this slot -- blocks resubmit dup.
        //   (b) same orderId+price seen before -- IsDedup orderId-level dedup guard.
        //       Note: IsDedup retains its _dedupCache.TryAdd side effect (records event for idempotency
        //       across NT8 Accepted+Working double-fire). This is safe and intentional.
        //   (c) orderId was previously committed via SetLiveEntryDispatched -- eviction-bypass guard.
        // Side effects: NONE on _liveEntryInstruments or _entryInstrKeyByOrderId.
        //   (IsDedup side effect on _dedupCache is intentional -- see note above.)
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool. JS-023: pure check path. ASCII-only.
        private bool IsLiveEntryBlocked_Check(string instrKey, string orderId, double limitPrice)
        {
            if (_liveEntryInstruments.ContainsKey(instrKey))
                return true;
            if (IsDedup(orderId, limitPrice))
                return true;
            if (_entryDispatchedOrders.ContainsKey(orderId))
                return true;
            return false;
        }
```

**Signature**:
```
private bool IsLiveEntryBlocked_Check(string instrKey, string orderId, double limitPrice)
CYC: 4
Side effects: NONE on _liveEntryInstruments or _entryInstrKeyByOrderId
```

#### E.5 `SetLiveEntryDispatched` — NEW METHOD (placement: immediately after `IsLiveEntryBlocked_Check`)

**Before**: Does not exist.

**After** (new method — add immediately after `IsLiveEntryBlocked_Check`):

```csharp
        // DW-B142-MGC-02 (PTT-REPAIRS-03 B1 split): commit method -- called only when dispatched > 0.
        // Writes the three maps that IsLiveEntryBlocked_Check reads.
        // CYC=1: no decision branches. Three sequential ConcurrentDictionary TryAdd calls.
        // JS-021: no lock. ConcurrentDictionary TryAdd is lock-free. JS-001: no throw. ASCII-only.
        private void SetLiveEntryDispatched(string instrKey, string orderId)
        {
            _liveEntryInstruments.TryAdd(instrKey, 0);
            _entryInstrKeyByOrderId.TryAdd(orderId, instrKey);
            _entryDispatchedOrders.TryAdd(orderId, 0);
        }
```

**Signature**:
```
private void SetLiveEntryDispatched(string instrKey, string orderId)
CYC: 1
Side effects: writes _liveEntryInstruments, _entryInstrKeyByOrderId, _entryDispatchedOrders
```

#### E.6 `IsEntryDispatched` — simplified (lines 5731–5737)

**Signature** (unchanged):
```csharp
private bool IsEntryDispatched(string orderId)
```

**Before** (lines 5727–5737):
```csharp
        // DW-B91-A: guard -- returns true if this orderId was already dispatched (blocks re-dispatch).
        // Side-effect on first call: TryAdd records the orderId as dispatched.
        // CYC=2: 1 base + 1 if (ContainsKey).
        // JS-021: ContainsKey + TryAdd are lock-free. JS-001: no throw. JS-002: returns bool.
        private bool IsEntryDispatched(string orderId)
        {
            if (_entryDispatchedOrders.ContainsKey(orderId))
                return true;
            _entryDispatchedOrders.TryAdd(orderId, 0);
            return false;
        }
```

**After**:
```csharp
        // DW-B91-A (PTT-REPAIRS-03 B1 split): pure check -- returns true if orderId was committed
        // via SetLiveEntryDispatched. TryAdd side effect removed; commit is now in SetLiveEntryDispatched.
        // CYC=1: single return, no decision branches.
        // JS-021: ContainsKey is lock-free. JS-001: no throw. JS-002: returns bool. ASCII-only.
        private bool IsEntryDispatched(string orderId)
        {
            return _entryDispatchedOrders.ContainsKey(orderId);
        }
```

Note: `IsEntryDispatched` is no longer called by `IsLiveEntryBlocked_Check` directly —
`IsLiveEntryBlocked_Check` inlines the `ContainsKey` check. `IsEntryDispatched` may be retained
for any other call sites. If no other call sites exist, it can be removed — but the architect's
choice is to retain it as a named helper for legibility. Engineer must check for other callers
before deciding to remove it. If no other callers: either keep or remove; document the choice.

---

### F. Jane Street Rule Constraints

| Rule | Constraint | Applied where |
|------|-----------|---------------|
| **JS-001** | No throw in gate chain. `ContainsKey`, `TryAdd` on `ConcurrentDictionary` do not throw. `ShouldSkipFollower` delegates to no-throw predicates. | All new and changed methods |
| **JS-002** | Predicates return `bool`. No null return. `ShouldSkipFollower` and `IsLiveEntryBlocked_Check` are pure bool predicates. `IsLiveEntryBlocked_ForTest` shim returns `bool`. | `ShouldSkipFollower`, `IsLiveEntryBlocked_Check`, `IsLiveEntryBlocked_ForTest` |
| **JS-003** | No magic-string state discrimination. | Not applicable to T2 changes |
| **JS-021** | No `lock()`. All map operations use `ConcurrentDictionary` (`TryAdd`, `ContainsKey`). No new `lock()` statement anywhere. | All new methods, `DispatchCopy` changes, `IsLiveEntryBlocked_ForTest` shim |
| **JS-023** | Immutable where possible. `ShouldSkipFollower` is a pure predicate. `IsLiveEntryBlocked_Check` is a pure predicate (no writes to `_liveEntryInstruments` or `_entryInstrKeyByOrderId`). Side effects isolated to `SetLiveEntryDispatched`. `IsLiveEntryBlocked_ForTest` shim calls both — side effects are the same as the original `IsLiveEntryBlocked` (test seam contract preserved). | `ShouldSkipFollower`, `IsLiveEntryBlocked_Check` |
| **JS-025** | `ConcurrentDictionary TryRemove` lock-free. No new `TryRemove` introduced. `EvictDedup` `TryRemove` calls are unchanged. | `SetLiveEntryDispatched`, `EvictDedup` |
| **JS-066** | CYC ≤ 8 per method. See Section J for full table. All methods ≤ 8. | All changed and new methods |
| **JS-042** | ASCII-only identifiers and string literals. All new method names and comment text are ASCII. | All new and changed code |

---

### G. xUnit `[Fact]` Test Specification

#### G.1 Existing `[Fact]` disposition — `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` (line 7784)

**Approach chosen: APPROACH X — preserve test, update shim only.**

**Justification**:
The test `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` verifies a behavioral guarantee that
survives T2's B1 split unchanged:

> *After a fill, EvictDedup clears `_liveEntryInstruments[instrKey]`, allowing a second dispatch
> for the same instrKey to pass gate5.*

This guarantee does NOT depend on WHERE instrKey is set (inside a combined predicate or via a
separate commit method). It only requires that:
1. `IsLiveEntryBlocked_ForTest` (first call) returns `false` AND sets instrKey in `_liveEntryInstruments`
2. `EvictDedup_ForTest(orderId1, Filled)` clears instrKey from `_liveEntryInstruments`
3. `IsLiveEntryBlocked_ForTest` (second call, new orderId) returns `false` (not blocked)

After the shim is redirected to call `IsLiveEntryBlocked_Check` + `SetLiveEntryDispatched` (see E.3),
requirement (1) is satisfied by `SetLiveEntryDispatched` writing `_liveEntryInstruments`. `EvictDedup`
is unchanged, so requirement (2) is satisfied. Requirement (3) is satisfied by `IsLiveEntryBlocked_Check`
finding the instrKey absent after the fill. All three assertions in the test pass.

**Test modifications required**: NONE. The test body (`IsLiveEntryBlocked_ClearsOnFill_AllowsReentry`)
is **unchanged**. Only `IsLiveEntryBlocked_ForTest` in `CopyEngine.cs` is redirected (see E.3).

**Assertion analysis by line**:

| Test line | Assertion | Status after T2 |
|-----------|-----------|----------------|
| 7796 | `blocked1 = IsLiveEntryBlocked_ForTest(instrKey, orderId1, 0.0)` → `Assert.False` | PASS: `IsLiveEntryBlocked_Check` returns `false` (instrKey not set); then `SetLiveEntryDispatched` writes instrKey |
| 7800 | `Assert.True(LiveEntryInstrumentsContains_ForTest(instrKey))` | PASS: `SetLiveEntryDispatched` wrote `_liveEntryInstruments[instrKey]` in the shim's commit path |
| 7803 | `EvictDedup_ForTest(orderId1, Filled)` | PASS: `EvictDedup` unchanged; clears `_liveEntryInstruments[instrKey]` |
| 7806 | `Assert.False(LiveEntryInstrumentsContains_ForTest(instrKey))` | PASS: `EvictDedup` cleared it |
| 7809 | `blocked2 = IsLiveEntryBlocked_ForTest(instrKey, orderId2, 0.0)` → `Assert.False` | PASS: instrKey absent (cleared by EvictDedup); orderId2 is new → `IsLiveEntryBlocked_Check` returns `false` |

**[Fact] count impact**: The existing test is preserved (not deleted, not replaced). Delta = 0 for
this test. Combined with the new test in G.2 below: T2 net delta = **+1** (476 → 477). ✓

#### G.2 New `[Fact]` — `DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped`

**Test method name**: `DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped`

**File**: `src/PropTraderTools/CopyEngineTests.cs`

**Block position**: Append after `ShouldSkipForReversalGuard_AllowsEntryWhenFollowerHasWorkingOrders`
(added by T1), or after the last existing `[Fact]` in the file if T1 test is not yet present.

**Scenario**: `DispatchCopy` runs, all followers in the rule are skipped (`dispatched == 0`).
After the call, `_liveEntryInstruments` must NOT contain the `instrKey`. A subsequent call to
`IsLiveEntryBlocked_Check` for the same `instrKey` must return `false` (not blocked).

**Arrange/Act/Assert outline**:

```csharp
[Fact]
public void DispatchCopy_DoesNotSetPhantomInstrKey_WhenAllFollowersSkipped()
{
    // Arrange
    // Instantiate or obtain CopyEngine.Instance (same as existing test pattern).
    // Configure a CopyRule with at least one follower account that will be skipped.
    //
    // Preferred skip mechanism (no NT8 Account mock required):
    //   Use ShouldSkipFollowerDispatch to cause skip -- e.g., configure follower account as null
    //   or in a state where ShouldSkipFollowerDispatch returns true.
    //   This avoids needing to mock NT8 Account/Position for the reversal guard path.
    //
    // Alternatively (if Account can be mocked):
    //   Configure all followers to be in reversal-guard state (hasLastDirection=true, flat, reversed).
    //
    // instrKey computation (mirrors DispatchCopy line 2472-2473):
    //   string instrKey = order.Instrument.FullName + "|" + order.OrderAction;
    //
    // Access _liveEntryInstruments via reflection (same GetField pattern):
    //   var field = GetField("_liveEntryInstruments");
    //   var liveEntryInstruments = field.GetValue(_engine) as ConcurrentDictionary<string, byte>;
    //
    // Invoke DispatchCopy via OnOrderUpdate (public entry point) OR promote DispatchCopy to
    // internal (via InternalsVisibleTo at L46) for direct test invocation.
    //
    // Ensure _liveEntryInstruments does NOT contain instrKey before the call (pre-condition).

    // Act
    // Trigger DispatchCopy -- call OnOrderUpdate with an appropriate order event,
    // OR call DispatchCopy directly if promoted to internal.

    // Assert 1: instrKey not phantom-locked
    // Assert.False(liveEntryInstruments.ContainsKey(instrKey));

    // Assert 2: subsequent IsLiveEntryBlocked_Check returns false for same instrKey
    // var checkMethod = GetMethod("IsLiveEntryBlocked_Check");
    // bool blocked = (bool)checkMethod.Invoke(_engine,
    //     new object[] { instrKey, "newOrderId-999", 0.0 });
    // Assert.False(blocked);
}
```

**What this test asserts**:
1. When all followers are skipped (`dispatched == 0`), `SetLiveEntryDispatched` is NOT called,
   and `_liveEntryInstruments` does NOT contain the instrKey after `DispatchCopy` returns.
2. A subsequent `IsLiveEntryBlocked_Check` for the same instrKey returns `false` — the instrKey
   is not phantom-locked, and the next legitimate order for this instrument can pass gate5.

---

### H. 7-SCAN CHECKLIST (Engineer Contract)

**SCAN-01: `lock()` grep — zero matches in changed methods**
```powershell
# Check all new and changed method locations in CopyEngine.cs
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'lock\s*\('
# Expected: 0 matches anywhere in the file (pre-existing and new code)
# Forensic baseline: grep -r "lock(" src/ returns 0 (project invariant)
```
Pass condition: Zero matches in the entire `src/PropTraderTools/` directory.

**SCAN-02: Unicode/emoji/curly-quote grep — zero matches in changed lines**
```powershell
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '[^\x00-\x7F]'
# Expected: 0 matches in any line touched by T2
```
Pass condition: Zero non-ASCII characters in any line added or modified by T2.

**SCAN-03: CYC check — all modified methods ≤ 8**

| Method | Expected CYC post-T2 |
|--------|---------------------|
| `ShouldSkipFollower` (new) | 3 (base=1 + ShouldSkipFollowerDispatch-check=2 + ShouldSkipForReversalGuard-check=3) |
| `IsLiveEntryBlocked_Check` (new) | 4 (base=1 + instrKey-ContainsKey=2 + IsDedup=3 + orderId-ContainsKey=4) |
| `SetLiveEntryDispatched` (new) | 1 (no branches) |
| `IsEntryDispatched` (simplified) | 1 (single return, no branches) |
| `DispatchCopy` (changed) | 8 (branches 2–8 as enumerated in Section E.2) |
| `IsLiveEntryBlocked_ForTest` (redirected) | 2 (base=1 + if-IsLiveEntryBlocked_Check=2) |

All values ≤ 8. Pass condition: no method exceeds 8.

**SCAN-04: `[Fact]` count — Select-String baseline + delta verification**
```powershell
# T2 must be applied after T1. Baseline going into T2 = 476 (post-T1).
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern '^\s*\[Fact\]' | Measure-Object
# Expected before T2: 476
# Expected after T2:  477 (delta = +1: 1 new test added, existing test preserved unchanged)
```
Pass condition: count increases from 476 to 477 (delta = +1). No more, no less.

**SCAN-05: Build — msbuild or dotnet build succeeds zero errors**
```powershell
powershell -File .\scripts\build_readiness.ps1
# OR:
dotnet build src\PropTraderTools\PropTraderTools.csproj
```
Pass condition: Build exits code 0 with zero errors.
Critical: `IsLiveEntryBlocked_ForTest` shim must be redirected (E.3) before `IsLiveEntryBlocked` is
deleted. Deleting `IsLiveEntryBlocked` without redirecting the shim = compile error = SCAN-05 FAIL.

**SCAN-06: Null-conditional event handler check (NT8-043)**
T2 does NOT add or modify any event handler wire-up or unwire code.
**NT8-043 is NOT APPLICABLE to T2. State explicitly in PR: "SCAN-06: N/A — no event handlers changed in T2."**

**SCAN-07: `HasWorkingEntries` `.ToList()` check — T2 scope**
**SCAN-07 is NOT APPLICABLE to T2.** T2 does not touch `HasWorkingEntries`.
State explicitly in PR: "SCAN-07: N/A (T2) — HasWorkingEntries is T1 scope."

Additional T2-specific verification (not a formal scan item but required):
```powershell
# Verify IsLiveEntryBlocked is fully deleted (zero remaining references except the new _Check variant):
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'IsLiveEntryBlocked[^_]'
# Expected: 0 matches (all references must use IsLiveEntryBlocked_Check or IsLiveEntryBlocked_ForTest)

# Verify SetLiveEntryDispatched is called in two places (DispatchCopy + IsLiveEntryBlocked_ForTest shim):
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'SetLiveEntryDispatched'
# Expected: 3 matches (1 definition + 1 call in DispatchCopy + 1 call in IsLiveEntryBlocked_ForTest shim)

# Verify shim body was updated (no longer delegates to IsLiveEntryBlocked):
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'IsLiveEntryBlocked_ForTest' -Context 0,6
# Expected: shows redirected body calling IsLiveEntryBlocked_Check + SetLiveEntryDispatched

# Verify stale comment at line ~201 was updated:
Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern 'Written in IsLiveEntryBlocked at Gate'
# Expected: 0 matches (comment updated or removed)
```

---

### I. NT8 Constraints

- **No `lock()` keyword** anywhere in T2 changes. All shared-state writes use `ConcurrentDictionary`
  `TryAdd` / `ContainsKey` — lock-free operations.
- **Actor pattern**: `DispatchCopy` is the actor. T2 restructures its side-effect commit to occur
  only after `dispatched > 0`. The actor's gate chain structure is preserved.
- **ASCII-only**: All new method names (`ShouldSkipFollower`, `IsLiveEntryBlocked_Check`,
  `SetLiveEntryDispatched`), variable names (`dispatched`, `instrKey`, `orderId`), and comment
  text are printable ASCII. No Unicode, no curly quotes, no em-dashes, no emoji.
- **`DateTime.UtcNow`**: Not applicable — T2 does not involve time-based logic.
- **`FontFamily`**: Not applicable.
- **`PTT-` order name prefix**: Not applicable — T2 does not create orders.
- **Dispatcher.InvokeAsync**: Not applicable — T2 does not touch UI or event handler code.
- **MGC guard preserved**: `SetLiveEntryDispatched` writes all three maps atomically (TryAdd is
  lock-free). `EvictDedup` cleans all three maps on cancel/fill. Gate5(a/b/c) semantics are
  preserved — see architecture plan Section D MGC guard verification for full proof.

---

### J. CYC Budget Summary

| Method | Before | After | Budget | Status |
|--------|--------|-------|--------|--------|
| `IsLiveEntryBlocked` (original) | 4 | **deleted** | — | — |
| `ShouldSkipFollower` (new) | — | **3** | ≤ 8 | ✓ |
| `IsLiveEntryBlocked_Check` (new) | — | **4** | ≤ 8 | ✓ |
| `SetLiveEntryDispatched` (new) | — | **1** | ≤ 8 | ✓ |
| `IsEntryDispatched` (simplified) | 2 | **1** | ≤ 8 | ✓ |
| `IsLiveEntryBlocked_ForTest` (redirected) | 1 (expression-bodied) | **2** (+1 for if-branch) | ≤ 8 | ✓ |
| `DispatchCopy` | **8** (measured) | **7** (after ShouldSkipFollower extraction) → **8** (after `if (dispatched > 0)`) | ≤ 8 | ✓ |

DispatchCopy CYC arithmetic:
- Pre-T2 (measured from source, confirmed by plan reviewer): 8
- After Change 3 (ShouldSkipFollower extraction collapses branches 7+8 → one branch): 7
- After Change 4 (`if (dispatched > 0)` post-loop guard): +1 → **8**
- Final: 8 ≤ 8 ✓

---

### K. PTT-DIAG Log Preservation

All 5 PTT-DIAG log lines are **PERMANENT** and must survive T2 unchanged.

| Log prefix | Source location | T2 impact |
|-----------|----------------|-----------|
| `[PTT-COPY-DIAG] gate0.5 exit:` | `DispatchCopy` lines 2433–2440 | **Preserved** — not near any T2 change ✓ |
| `[PTT-COPY-DIAG] gate3 exit:` | `DispatchCopy` lines 2446–2453 | **Preserved** — not near any T2 change ✓ |
| `[PTT-COPY-DIAG] gate4 exit:` | `DispatchCopy` lines 2459–2466 | **Preserved** — not near any T2 change ✓ |
| `[PTT-COPY-DIAG] gate5 exit:` | `DispatchCopy` lines 2476–2482 | **Preserved exactly** — this log fires on `IsLiveEntryBlocked_Check` returning true. The log block (lines 2476–2482) is NOT modified by T2; only line 2474 (the call expression) changes from `IsLiveEntryBlocked` to `IsLiveEntryBlocked_Check`. Log content and placement unchanged ✓ |
| `[PTT-COPY-GUARD] skip reversal entry:` | `ShouldSkipForReversalGuard` lines 2597–2607 | **Preserved** — T2's `ShouldSkipFollower` extraction calls `ShouldSkipForReversalGuard` as a whole; the log is inside that method's body and is not moved or modified ✓ |

The `ShouldSkipFollower` extraction does NOT move any log-emitting code. All five logs remain
inside the original methods (gate logs in `DispatchCopy`; guard log in `ShouldSkipForReversalGuard`).

---

### L. Hard-Link Sync Instructions

After ALL `.cs` edits for this ticket are complete (both T1 and T2 if applied together), run:

```powershell
# Step 1: deploy-sync (re-synchronizes all NinjaTrader hard links)
powershell -File .\deploy-sync.ps1

# Step 2: Manual PropTraderTools re-link (run if deploy-sync does not cover these files)
$nt8Dir  = "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools"
$repoDir = "C:\WSGTA\universal-or-strategy\src\PropTraderTools"
foreach ($f in @("CopyEngine.cs","TradeCopierAddOn.cs","TradeCopierPanel.cs",
                 "TradeCopierWindow.cs","AtrSizingEngine.cs","FeatureFlags.cs",
                 "LicenseClient.cs")) {
    Remove-Item "$nt8Dir\$f" -Force
    New-Item -ItemType HardLink -Path "$nt8Dir\$f" -Value "$repoDir\$f" | Out-Null
}
```

Run hard-link sync exactly **once** after both tickets are fully implemented and the build passes.
Do not run between T1 and T2 if applying both in the same session.

---

---

## IMPLEMENTATION ORDER

Apply T1 first, then T2. Rationale:
- T1 is independent of T2 (confirmed by LANE-SPLIT gate Q2=NO).
- T2 calls `ShouldSkipFollower` which calls `ShouldSkipForReversalGuard` — T1 modifies
  `ShouldSkipForReversalGuard`. If T2 is applied first, `ShouldSkipForReversalGuard` still
  works (T1 is additive, not destructive), but the test for T1 validates T1's semantics.
- Logical order: fix BUG-A (T1) → fix BUG-B (T2) → run all scans → build → hard-link sync.

**T2 implementation order within T2** (to avoid a transient compile error):
1. Add `IsLiveEntryBlocked_Check` (new method, ~line 5748)
2. Add `SetLiveEntryDispatched` (new method, immediately after `IsLiveEntryBlocked_Check`)
3. Redirect `IsLiveEntryBlocked_ForTest` shim body (line 4302–4306)
4. Delete `IsLiveEntryBlocked` (line 5739–5759) — safe only after steps 1–3 are applied
5. Apply all four `DispatchCopy` changes (E.2 Changes 0–4)
6. Add `ShouldSkipFollower` (new method, ~after line 2577)
7. Simplify `IsEntryDispatched` (E.6)
8. Update stale comment at line ~201

Do not delete `IsLiveEntryBlocked` (step 4) before the shim redirect (step 3) — doing so creates
a compile error between those two steps.

**After both tickets are implemented and SCAN-01 through SCAN-07 pass:**
```powershell
# Verify final [Fact] count:
Select-String -Path 'src\PropTraderTools\CopyEngineTests.cs' -Pattern '^\s*\[Fact\]' | Measure-Object
# Expected: 477

# Verify zero lock() in src/:
grep -r "lock(" src/
# Expected: 0 matches

# Build:
powershell -File .\scripts\build_readiness.ps1
# Expected: 0 errors

# Hard-link sync:
powershell -File .\deploy-sync.ps1
```

---

*ptt-architect · PTT-REPAIRS-03 · 04-tickets.md · 2026-09-08*
*Plan basis: 02-architecture-plan.md (Revision 2 — V-02 corrected) — REVIEW_PASS (Cycle 2 FINAL)*
*Ticket Revision Cycle 1: T1 PASS (carried forward unchanged). T2 corrected — T2-TRACE-01, T2-TRACE-02, T2-TEST-01 resolved.*

TICKETS_COMPLETE
