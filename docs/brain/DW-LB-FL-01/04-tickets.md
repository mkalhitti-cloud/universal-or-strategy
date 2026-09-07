# DW-LB-FL-01 — Ticket Generation

**Defect ID**: DW-LB-FL-01
**Author**: ptt-architect (Phase 3)
**Date**: 2026-09-08
**Prerequisite**: `02-plan-review.md` = REVIEW_PASS (confirmed)
**Scope lock**: DW-LB-SFB-01 is explicitly excluded from this session.

---

## PIPELINE SUMMARY

**Single pipeline. One ticket. One PR.**

Root cause confirmed: `NakedPositionDetector` Dispatcher.InvokeAsync callback runs after
new ATM brackets arm, causing a false PTT-Flatten on follower accounts after a BE ALL cycle.
Fix: two new methods (`HasArmingAtmBrackets`, `FlattenIfNotArming`) + 1-line change in
`NakedPositionDetector`. All other flatten paths (DW-B65-01, DW-LB-FL-02) are untouched.

---

## TICKET: DW-LB-FL-01-T1

**Title**: Guard NakedPositionDetector dispatch with bracket-arming check

---

### Spec Requirement IDs Satisfied

| ID | Requirement |
|----|-------------|
| REQ-DW-LB-FL-01-1 | PTT-Flatten MUST NOT fire on followers during ATM bracket arming after a BE ALL cycle |
| REQ-DW-LB-FL-01-2 | DW-B65-01 bypass (intentional leader-initiated flatten path) MUST be preserved unchanged |
| REQ-DW-LB-FL-01-3 | DW-LB-FL-02 guard (`IsNativeExitOnFlatLeader` at L4710) MUST be preserved unchanged |
| REQ-DW-LB-FL-01-4 | Existing PTT-Flatten in-flight guard (`HasInflightFlatten` at L5220) MUST remain unchanged |
| REQ-DW-LB-FL-01-5 | All new/modified methods MUST have CYC <= 8 |
| REQ-DW-LB-FL-01-6 | No `lock()` anywhere in new/modified code (JS-021) |
| REQ-DW-LB-FL-01-7 | All string literals in new/modified code MUST be ASCII-only |
| REQ-DW-LB-FL-01-8 | 10 xUnit [Fact] tests MUST be added covering HasArmingAtmBrackets (T1-T7), FlattenIfNotArming (T8-T9), regression (T10) |

---

### File to Modify

```
src/PropTraderTools/CopyEngine.cs
```

**No other file is modified.** `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`
is the test file to create or extend (new `[Fact]` methods appended to existing class).

---

### Exact Methods to Add/Modify

#### 1. NEW METHOD: `HasArmingAtmBrackets` (static, private)

**Placement**: Insert immediately after the closing brace of `HasInflightFlatten`
at line **L5232** (the blank line at L5233 becomes the separator).
Insert the block below starting at L5234 (before the `IsFlattenOrderActive` comment at L5234
— shift `IsFlattenOrderActive` and everything below it down by the number of lines added).

**Full method body**:

```csharp
        // DW-LB-FL-01: returns true if any ATM bracket order is in an active arming state.
        // Active states: Working, Submitted, Accepted, TriggerPending.
        // Reuses IsAtmBracketName (Stop1..Stop9 / Target1..Target9) for bracket identification.
        // Guard purpose: when NakedPositionDetector queues a FlattenOneAccount callback via
        // Dispatcher.InvokeAsync, bracket arming may complete before the UI thread runs the
        // callback. If ATM brackets are active at callback time, the account has valid
        // protection -- do not flatten.
        // CYC=5: base(1)+foreach(1)+instr-skip(1)+stateActive-branch(1)+IsAtmBracketName(1).
        // stateActive compound bool is assigned to a local variable -- counts as 1 branch.
        // JS-021: no lock. acc.Orders.ToList() snapshot (same pattern as HasInflightFlatten L5222).
        // JS-001: no throw. JS-002: returns bool. ASCII-only. static.
        private static bool HasArmingAtmBrackets(Account acc, Instrument instr)
        {
            foreach (var o in acc.Orders.ToList())
            {
                if (o.Instrument?.FullName != instr.FullName)
                    continue;
                bool stateActive =
                    o.OrderState == OrderState.Working
                    || o.OrderState == OrderState.Submitted
                    || o.OrderState == OrderState.Accepted
                    || o.OrderState == OrderState.TriggerPending;
                if (!stateActive)
                    continue;
                if (IsAtmBracketName(o.Name))
                    return true;
            }
            return false;
        }
```

**CYC**: 5 — PASS (base(1) + foreach(1) + instr-skip(1) + stateActive-branch(1) + IsAtmBracketName-branch(1)).
`stateActive` is a local bool variable — the compound `||` expression is NOT separate branches in McCabe.

**JS rules**:
- JS-021: no `lock()` — `.ToList()` snapshot used (same pattern as `HasInflightFlatten` L5222)
- JS-001: no throw
- JS-002: returns bool (never null)
- JS-033: synchronous static method — no async void

---

#### 2. NEW METHOD: `FlattenIfNotArming` (instance, private)

**Placement**: Insert immediately **before** `FlattenOneAccount` at line **L5167**
(i.e., insert after the closing brace of the prior method at L5165, before the
comment block that starts at L5167 `// B28 T1 -- FlattenOneAccount:`).
The new block becomes the method immediately preceding `FlattenOneAccount` in source order.

**Full method body**:

```csharp
        // DW-LB-FL-01: dispatch helper used exclusively by NakedPositionDetector
        // Dispatcher.InvokeAsync lambda.
        // Guards the FlattenOneAccount call: if ATM brackets are Working/Submitted/Accepted/
        // TriggerPending on acc for instr, skip flatten (bracket-arm race protection).
        // This prevents the false PTT-Flatten that fires after a BE ALL cancel storm when the
        // Dispatcher.InvokeAsync callback runs after the new entry's brackets have started arming.
        // Does NOT affect TryDispatchLeaderFlat -> FlattenFollower -> FlattenOneAccount path
        // (intentional leader-initiated flatten -- DW-B65-01 bypass preserved).
        // CYC=2: base(1) + HasArmingAtmBrackets branch(1).
        // JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only. Instance method.
        private void FlattenIfNotArming(Account acct, Instrument instr)
        {
            if (HasArmingAtmBrackets(acct, instr))
            {
                StatusUpdate?.Invoke(acct.Name + ": flat-guard: bracket-arm skip");
                return;
            }
            FlattenOneAccount(acct, instr);
        }
```

**CYC**: 2 — PASS (base(1) + HasArmingAtmBrackets branch(1)).

**JS rules**:
- JS-021: no `lock()` — calls into static `HasArmingAtmBrackets` and instance `FlattenOneAccount`
- JS-001: no throw
- JS-002: void (not null)
- JS-033: synchronous instance method — no async void

---

#### 3. MODIFIED METHOD: `NakedPositionDetector` — 1-line change only

**File**: `src/PropTraderTools/CopyEngine.cs`
**Lines**: L7190-7192 (confirmed against live source)

**Current code at L7190-7192**:
```csharp
            if (instr != null)
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    FlattenOneAccount(acct, instr)
                );
```

**Replace with** (change `FlattenOneAccount` to `FlattenIfNotArming` — one identifier change):
```csharp
            if (instr != null)
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    FlattenIfNotArming(acct, instr)
                );
```

**Exact diff**:
```diff
-                    FlattenOneAccount(acct, instr)
+                    FlattenIfNotArming(acct, instr)
```

**CYC impact**: Zero. The lambda body change does not add a branch to `NakedPositionDetector`.
Current CYC (<=6) is unchanged.

**No other lines in `NakedPositionDetector` are touched.**

---

### What MUST NOT Be Changed

| Location | What it is | Why protected |
|----------|-----------|---------------|
| `TryDispatchLeaderFlat` L4693 | Leader-initiated flatten dispatch | Different code path — DW-B65-01 bypass lives here |
| `TryDispatchLeaderFlat` L4710 | `IsNativeExitOnFlatLeader` guard | DW-LB-FL-02 — native exit on flat leader guard |
| `IsAccountFlattenable` L5202 | Flattenable predicate | Contains `HasInflightFlatten` guard — do not remove or weaken |
| `HasInflightFlatten` L5220 | In-flight PTT-Flatten guard | DW-LB-FL-02 upstream protection — do not modify |
| `NakedPositionDetector` L7172-7185 | null check + HasNakedPosition + debounce | Only the lambda body (L7191) changes |

---

### Test File to Create/Extend

**File**: `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`

Extend the existing test class (or create this file if it does not yet contain
`DW-LB-FL-01` tests). Use `InternalsVisibleTo("PropTraderTools.Tests")` already
declared at `CopyEngine.cs` L46 — `HasArmingAtmBrackets` is `private static` and
must be exposed via a testable seam or reflection. For T8/T9 (`FlattenIfNotArming`),
use a delegate-injection overload or test via the public effect (`StatusUpdate`
event is invoked vs `FlattenOneAccount` proceeds). See seam notes below.

**Framework**: xUnit `[Fact]` only. No NUnit. No MSTest. No `[Theory]` required.

#### Test Seam Notes

- `HasArmingAtmBrackets` is `private static` — expose via:
  - Option A (preferred): Add `internal static bool HasArmingAtmBrackets(...)` (change
    `private` → `internal`). `InternalsVisibleTo` at L46 grants test project access.
  - Option B: Use reflection (`MethodInfo.Invoke`) in test — acceptable but verbose.

- `FlattenIfNotArming` is `private` instance — expose via:
  - Same `internal` promotion if desired.
  - OR test the guard indirectly: assert `StatusUpdate` was invoked with
    `"bracket-arm skip"` suffix (T9 = guard fired), vs no `StatusUpdate` and
    `FlattenOneAccount` side effects present (T8 = guard did not fire).

**Recommendation**: Promote both methods to `internal` for clean testability.
Add `// DW-LB-FL-01 visibility` comment next to the `internal` keyword.

---

### xUnit [Fact] Test Names

All tests in `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs`.

| # | Test Method Name | Asserts |
|---|-----------------|---------|
| T1 | `HasArmingAtmBrackets_ReturnsFalse_WhenNoOrders` | `acc.Orders` is empty → returns `false` |
| T2 | `HasArmingAtmBrackets_ReturnsFalse_WhenAllBracketsAreCancelled` | Stop1+Target1 both in `OrderState.Cancelled` → returns `false` |
| T3 | `HasArmingAtmBrackets_ReturnsTrue_WhenStop1IsWorking` | Stop1 in `OrderState.Working`, correct instrument → returns `true` |
| T4 | `HasArmingAtmBrackets_ReturnsTrue_WhenTarget2IsAccepted` | Target2 in `OrderState.Accepted`, correct instrument → returns `true` |
| T5 | `HasArmingAtmBrackets_ReturnsTrue_WhenStop3IsTriggerPending` | Stop3 in `OrderState.TriggerPending`, correct instrument → returns `true` |
| T6 | `HasArmingAtmBrackets_ReturnsFalse_WhenAtmBracketsForDifferentInstrument` | Stop1 in `OrderState.Working` but `Instrument.FullName` does not match → returns `false` |
| T7 | `HasArmingAtmBrackets_ReturnsFalse_WhenOnlyNonAtmOrdersAreWorking` | Order named `"PTT-Copy"` in `OrderState.Working` → `IsAtmBracketName` returns false → returns `false` |
| T8 | `FlattenIfNotArming_CallsFlattenOne_WhenNoArmingBrackets` | No active ATM brackets → `FlattenOneAccount` is invoked (assert via side effect or delegate) |
| T9 | `FlattenIfNotArming_SkipsFlattenOne_WhenArmingBracketsPresent` | Stop1 in Working state → `FlattenOneAccount` is NOT invoked; `StatusUpdate` fires with `"bracket-arm skip"` substring |
| T10 | `IsAccountFlattenable_ExistingInflightGuardStillWorks_Regression` | PTT-Flatten order in `OrderState.Working` → `IsAccountFlattenable` returns `false` (unchanged guard regression) |

**Notes on T8/T9 implementation**:
- If `FlattenIfNotArming` is promoted to `internal`, test the method directly on a
  `CopyEngine` instance with a test-constructed account.
- `StatusUpdate` is an `event Action<string>` — subscribe in test, assert string
  contains `"bracket-arm skip"` for T9.
- For T10: test `IsAccountFlattenable` directly (promote to `internal`) with a mock
  account that has a `PTT-Flatten` order in Working state on the correct instrument.

---

### 7-SCAN CHECKLIST (Engineer Contract)

The engineer MUST complete all 7 scans before marking this ticket done.
Results MUST be recorded in the completion report.

```
[ ] SCAN-01: lock() grep
    Command: grep -n "lock(" src/PropTraderTools/CopyEngine.cs
    Scope: new and modified code only (HasArmingAtmBrackets, FlattenIfNotArming, NakedPositionDetector)
    Required: 0 matches in new/modified code
    Note: If pre-existing lock() found elsewhere, do NOT touch it (no scope creep).

[ ] SCAN-02: CYC check
    Command: python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs
    Required: HasArmingAtmBrackets <= 8 (expected 5)
              FlattenIfNotArming <= 8 (expected 2)
              NakedPositionDetector <= 8 (unchanged, expected <=6)
    PASS criterion: all three <= 8

[ ] SCAN-03: ASCII-only check
    Command: grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
    Scope: new/modified lines only
    Required: 0 non-ASCII characters in:
      "flat-guard: bracket-arm skip" (new StatusUpdate string literal)
      All new comment lines
    Note: "bracket-arm skip" contains only printable ASCII.

[ ] SCAN-04: NT8 API check
    Command: grep -n "Account.Change\|AtmStrategyCreate\|AtmStrategyChangeStopTarget" src/PropTraderTools/CopyEngine.cs
    Scope: new/modified code only
    Required: 0 matches in HasArmingAtmBrackets, FlattenIfNotArming, or NakedPositionDetector

[ ] SCAN-05: Build gate
    Command: dotnet build src/PropTraderTools/PropTraderTools.csproj
    Required: 0 errors, 0 warnings
    Note: FlattenIfNotArming must resolve: HasArmingAtmBrackets (static) + FlattenOneAccount (instance)
          NakedPositionDetector lambda must resolve: FlattenIfNotArming (instance)

[ ] SCAN-06: Test gate
    Command: dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj
    Required: ALL tests pass (pre-existing + T1-T10 new)
    Note: If any pre-existing test fails, do NOT proceed to SCAN-07.
          Report failure with test name + error — do not suppress.

[ ] SCAN-07: Sync gate
    Command: powershell -File scripts\ptt-sync-and-verify.ps1
    Required: 0 DESYNC lines, 0 MISSING lines
    Note: Run AFTER build passes (SCAN-05). Then press F5 in NinjaTrader 8 to recompile.
          F5 must complete with 0 compile errors before this ticket is DONE.
```

---

### Acceptance Criteria

The ticket is DONE when ALL of the following are true:

1. **Build**: `dotnet build` exits 0 errors, 0 warnings.
2. **Tests**: All 10 new `[Fact]` tests pass. All pre-existing tests pass.
3. **Scans 1-7**: All 7 scans pass as specified above.
4. **F5 gate**: NinjaTrader 8 F5 recompile completes with 0 errors after sync.
5. **No regressions**: `TryDispatchLeaderFlat`, `IsNativeExitOnFlatLeader`, `HasInflightFlatten` are byte-for-byte unchanged.
6. **SIM gate (manual)**: Clone mode, 3 follower accounts, BE ALL cycle fired, new entry enters → confirm no PTT-Flatten fires on followers during bracket arming window.

---

### Preserved Guard Verification (Final Checklist Before PR)

Before opening PR, the engineer MUST grep to confirm these are unchanged:

```powershell
# DW-B65-01 bypass: confirm still present in TryDispatchLeaderFlat area
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DW-B65-01" | Select-Object LineNumber, Line

# DW-LB-FL-02 guard: confirm IsNativeExitOnFlatLeader still present at L4710 area
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "IsNativeExitOnFlatLeader" | Select-Object LineNumber, Line

# HasInflightFlatten: confirm still present (regression guard)
Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "HasInflightFlatten" | Select-Object LineNumber, Line
```

All three patterns must return at least one result. If any returns empty → STOP, do not push.

---

### Risk Notes (from approved plan — carry forward)

1. **DW-LB-FL-01-DEBOUNCE-OVERFLOW** (deferred): `Environment.TickCount` int wrap at ~24.9 days.
   Low risk for 500ms window. Not in scope for this ticket. Do NOT fix as part of T1.

2. **DW-LB-FL-01-STATE-COVERAGE** (confirm in SIM gate): `HasArmingAtmBrackets` covers
   Working/Submitted/Accepted/TriggerPending. If SIM gate shows residual false PTT-Flatten,
   add `OrderState.Initialized` to the `stateActive` compound and re-run scans.

---

### Completion Report to Write

After ticket is complete, create:
```
docs/brain/DW-LB-FL-01/ticket-1-completion.md
```

Must include:
- Exact lines added/modified (with post-insertion line numbers)
- SCAN-01..07 result for each scan
- Test results (pass/fail counts)
- ptt-sync-and-verify.ps1 output excerpt
- F5 result (PASS / line count of errors if FAIL)
- SIM gate result (observed behavior)

---

## TICKET SUMMARY

| Field | Value |
|-------|-------|
| Ticket ID | DW-LB-FL-01-T1 |
| File | `src/PropTraderTools/CopyEngine.cs` |
| Methods added | `HasArmingAtmBrackets` (static, CYC=5), `FlattenIfNotArming` (instance, CYC=2) |
| Methods modified | `NakedPositionDetector` (1-line change: L7191 identifier only) |
| Methods NOT modified | `FlattenOneAccount`, `IsAccountFlattenable`, `HasInflightFlatten`, `TryDispatchLeaderFlat` |
| Tests added | 10 xUnit [Fact] in `tests/PropTraderTools.Tests/Core/CopyEngineTests.cs` |
| Preserved guards | DW-B65-01, DW-LB-FL-02, HasInflightFlatten (DW-LB-FL-02), DW-B94 |
| P0 violations | None |
| Scans | 7 (SCAN-01..07) |
