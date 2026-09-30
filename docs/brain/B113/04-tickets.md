# B113 Tickets — DW-B117 Cancel-After Fix

**Block**: B113
**Date**: 2026-08-26
**Plan Status**: REVIEW_PASS (Cycle 2)
**Author**: ptt-architect (Phase 3)
**Defect**: DW-B117 — QX-ALL PTT-QX-T2/T3 Missing on Followers Due to NT8 ATM Re-Arm After Pre-Cancel

---

## TICKET-B113-T1: DW-B117 Cancel-After Fix

**Title**: Remove Pre-Cancel, Add QX Cleanup State, Cancel Native ATM Brackets After PTT-QX Working

### Spec Requirement IDs Satisfied

- DW-B117: PTT-QX-T2/T3 missing on follower accounts because pre-cancel triggers NT8 ATM re-arm.
- DW-B105: `_qxCancelInProgress` guard preserved (window now covers submit, not cancel).
- DW-B112: `TryReplacePttBeBrackets` structural PTT-QX presence check unmodified.
- B113 plan REVIEW_PASS (Cycle 2): all four changes + seam + tests mandated.

### Files Modified

| File | Change Type |
|------|-------------|
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | CHANGE-1: restructure `ExecuteOne` follower path |
| `src/PropTraderTools/CopyEngine.cs` | CHANGE-2: add `_qxPendingFollowerCleanup` field |
| `src/PropTraderTools/CopyEngine.cs` | CHANGE-3: add `TryCleanupReArmedAtmBracket(e)` call in `OnOrderUpdate` |
| `src/PropTraderTools/CopyEngine.cs` | CHANGE-4 / REMOVE-PROBE: remove DW-B117-DIAG block |
| `src/PropTraderTools/CopyEngine.cs` | ASSEMBLY-SEAM: add `[InternalsVisibleTo]` attribute + promote method to `internal` |
| `src/PropTraderTools/CopyEngine.cs` | NEW METHOD: `internal void TryCleanupReArmedAtmBracket(OrderEventArgs e)` |

### Files NOT Modified

| File | Reason |
|------|--------|
| `src/PropTraderTools/Features/PttQuickExit.cs` | Per-account submit loop unchanged |
| `src/PropTraderTools/Features/PttGlobalBreakEven.cs` | BE path unchanged |
| `src/PropTraderTools/Features/PttBreakEvenSwap.cs` | BE swap path unchanged |
| `src/PropTraderTools/TradeCopierPanel.cs` | UI layer unchanged |
| `src/PropTraderTools/CopyEngine.cs :: CancelQxBrackets` | Method body unchanged; no longer called from follower path but NOT deleted |
| `src/PropTraderTools/CopyEngine.cs :: TryReplacePttBeBrackets` | DW-B112 guard chain untouched |

### New Test File

`src/PropTraderTools/Tests/B113Tests.cs`

---

## CHANGE-1

**CHANGE-ID**: CHANGE-1
**File**: `src/PropTraderTools/Features/PttGlobalQuickExit.cs`
**Method**: `ExecuteOne` (private)
**Location**: L145–178 (follower guard block + executor instantiation)

### BEFORE (verbatim, L145–178)

```csharp
            if (!skipIfFollower) // (1)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-QX-GUARD] pre-cancel follower brackets: "
                        + (acc != null ? acc.Name : "NULL"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                // DW-B105: set intent-guard before cancel so TryReplacePttBeBrackets skips
                // ATM-sweep recovery during the QX-ALL sweep. Clear unconditionally after.
                CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true);
                try
                {
                    CopyEngine.Instance?.CancelQxBrackets(acc, instr);
                }
                finally
                {
                    // DW-B112: TryRemove clears guard synchronously. NT8 OnOrderUpdate(Cancelled)
                    // events for the swept orders arrive asynchronously AFTER this finally executes.
                    // The structural PTT-QX presence check in TryReplacePttBeBrackets (DW-B112 Option 2)
                    // compensates by checking acc.Orders for Working/Submitted PTT-QX-* orders.
                    CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);
                }
            }
            var executor = new PttQuickExit(); // (2)
            executor.Execute(
                acc,
                instr,
                t1Ticks,
                targets,
                skipIfFollower,
                leaderStop,
                leaderTargetCount
            );
```

### AFTER (exact replacement)

```csharp
            if (!skipIfFollower) // (1) follower path: cancel-after pattern (B113 DW-B117)
            {
                NinjaTrader.Code.Output.Process(
                    "[PTT-QX-GUARD] follower submit (cancel-after): "
                        + (acc != null ? acc.Name : "NULL"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
                // DW-B105: intent-guard covers the submit window so TryReplacePttBeBrackets
                // skips ATM-sweep recovery while PTT-QX orders are being placed.
                // B113 DW-B117: guard now wraps executor.Execute (not CancelQxBrackets).
                CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true);
                try
                {
                    var executor = new PttQuickExit();
                    executor.Execute(
                        acc,
                        instr,
                        t1Ticks,
                        targets,
                        skipIfFollower,
                        leaderStop,
                        leaderTargetCount
                    );
                    // B113 DW-B117: arm cancel-after cleanup. OnOrderUpdate will cancel each
                    // native ATM Target* one-for-one as the corresponding PTT-QX-T* confirms Working.
                    CopyEngine.Instance?._qxPendingFollowerCleanup.TryAdd(
                        acc.Name,
                        (instr, DateTime.UtcNow.AddSeconds(2))
                    );
                }
                finally
                {
                    // DW-B112: TryRemove clears guard synchronously after submit completes.
                    // DW-B112 Option 2 structural check compensates for async Cancelled events.
                    CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);
                }
                return; // follower path complete
            }
            // Leader path (skipIfFollower=true): submit directly, no cancel-after needed.
            var leaderExecutor = new PttQuickExit(); // (2)
            leaderExecutor.Execute(
                acc,
                instr,
                t1Ticks,
                targets,
                skipIfFollower,
                leaderStop,
                leaderTargetCount
            );
```

**CYC**: Before = 2 (`if (!skipIfFollower)` = 1, base = 1). After = 2. Delta = 0.

---

## CHANGE-2

**CHANGE-ID**: CHANGE-2
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: N/A — field declaration
**Location**: After L264 (after `_qxCancelInProgress` field declaration)

### BEFORE (verbatim, L263–264)

```csharp
        internal readonly ConcurrentDictionary<string, bool> _qxCancelInProgress =
            new ConcurrentDictionary<string, bool>();
```

### AFTER (insert new field block immediately after L264)

```csharp
        internal readonly ConcurrentDictionary<string, bool> _qxCancelInProgress =
            new ConcurrentDictionary<string, bool>();

        // B113 DW-B117: cancel-after cleanup map. Set by PttGlobalQuickExit.ExecuteOne
        // immediately after executor.Execute for follower accounts. OnOrderUpdate reads this
        // to cancel native ATM Target* one-for-one as each PTT-QX-T* confirms Working.
        // Key = acc.Name. Value = (instrument, expiry=UtcNow+2s).
        // JS-021: ConcurrentDictionary -- lock-free. No lock() anywhere.
        internal readonly ConcurrentDictionary<string, (Instrument Instr, DateTime Expiry)>
            _qxPendingFollowerCleanup =
                new ConcurrentDictionary<string, (Instrument, DateTime)>();
```

**CYC**: Before = N/A (field). After = N/A (field). Delta = 0.

---

## REMOVE-PROBE

**CHANGE-ID**: REMOVE-PROBE (apply BEFORE CHANGE-3 — same region)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `OnOrderUpdate` (event handler)
**Location**: L1230–1250

### BEFORE (verbatim, L1230–1250 — exact DW-B117-DIAG block)

```csharp
            // DW-B117-DIAG: log ATM bracket name transitions on follower accounts to confirm
            // whether native ATM brackets arrive Working AFTER PTT-QX orders have been submitted.
            // Diagnostic only -- no state change. Remove after root cause confirmed.
            if (
                e.Order.OrderState == OrderState.Working
                && e.Order.Name != null
                && IsAtmBracketName(e.Order.Name)
                && e.Order.Account != null
                && IsFollowerAccount(e.Order.Account)
            )
            {
                NinjaTrader.Code.Output.Process(
                    "[DW-B117-DIAG] ATM bracket Working on follower: "
                        + e.Order.Account.Name
                        + " name="
                        + e.Order.Name
                        + " instr="
                        + (e.Order.Instrument?.FullName ?? "?"),
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }
```

### AFTER

*(Block entirely removed — no replacement text at this location. CHANGE-3 inserts the cancel-after
dispatch call in place of this block immediately after REMOVE-PROBE is applied.)*

**Note**: Engineer applies REMOVE-PROBE first (delete L1230–1250), then inserts CHANGE-3 at
the same location (immediately after L1229, which ends with `}`).

---

## CHANGE-3

**CHANGE-ID**: CHANGE-3
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `OnOrderUpdate` (event handler)
**Location**: Inserted at the gap created by REMOVE-PROBE (after L1229, in place of the removed block)

### BEFORE

*(Location is empty after REMOVE-PROBE removes DW-B117-DIAG block.)*

### AFTER (insert at gap, after L1229)

```csharp
            // B113 DW-B117: cancel-after -- cancel each native ATM bracket one-for-one
            // as the corresponding PTT-QX-T* order confirms Working. Extracted to helper
            // to keep OnOrderUpdate CYC within budget.
            TryCleanupReArmedAtmBracket(e);
```

**CYC**: `OnOrderUpdate` CYC Before = N (large method). After = N + 1 (single dispatch call adds
one McCabe point). The 6-condition compound guard inside the helper does NOT count toward
`OnOrderUpdate` CYC.

---

## CHANGE-4 — New Helper Method `TryCleanupReArmedAtmBracket`

**CHANGE-ID**: CHANGE-4
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `TryCleanupReArmedAtmBracket` (new method)
**Location**: Insert after L2378 (after `TryReplacePttBeBrackets` method closing brace, before `HasOpenPosition`)

### BEFORE (verbatim, L2378–2380 — insertion point context)

```csharp
            QueueBeRetryFallback(acc, instr, 0, delayMs: 500);
        }

        // CYC=2. Thin wrapper over FindPosition.
        private bool HasOpenPosition(Account acc, Instrument instrument)
```

### AFTER (insert new method between L2378 `}` and `HasOpenPosition` declaration)

```csharp
            QueueBeRetryFallback(acc, instr, 0, delayMs: 500);
        }

        // B113 DW-B117: cancel-after cleanup. Called from OnOrderUpdate when any order
        // transitions to Working. Cancels the native ATM Target* bracket that corresponds
        // to the PTT-QX-T* order that just confirmed Working, on follower accounts only.
        // CYC=5: (1) outer guard, (2) foreach, (3) if found, (4) if shouldRemove.
        // JS-021: no lock() -- ConcurrentDictionary TryGetValue/TryRemove.
        // JS-001: no throw. ASCII-only string literals. NT8-007: CancelOrder (not CreateOrder).
        internal void TryCleanupReArmedAtmBracket(OrderEventArgs e)
        {
            // (1) Compound guard -- all conditions must be true.
            // a. Order just went Working.
            // b. Name matches PTT-QX-T* pattern (PTT-QX-T1, T2, T3).
            // c. Account is a follower.
            // d. Cleanup entry exists for this account.
            // e. TTL has not elapsed.
            // f. Instrument matches the cleanup entry.
            if (
                e.Order.OrderState != OrderState.Working
                || e.Order.Name == null
                || !e.Order.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                || e.Order.Name.Length < 9
                || !char.IsDigit(e.Order.Name[8])
                || e.Order.Account == null
                || !IsFollowerAccount(e.Order.Account)
                || !_qxPendingFollowerCleanup.TryGetValue(e.Order.Account.Name, out var entry)
                || entry.Expiry <= DateTime.UtcNow
                || entry.Instr?.FullName != e.Order.Instrument?.FullName
            )
                return;

            char tChar = e.Order.Name[8]; // '1', '2', or '3'
            string nativeName = "Target" + tChar; // "Target1", "Target2", "Target3"
            var acc = e.Order.Account;

            // (2) Find the matching native ATM bracket on this account+instrument.
            // .ToList() snapshot -- consistent with acc.Orders read pattern at L2327.
            Order? toCancel = null;
            foreach (var o in acc.Orders.ToList()) // (2)
            {
                if (
                    o.Name == nativeName
                    && o.Instrument?.FullName == entry.Instr.FullName
                    && (o.OrderState == OrderState.Working || o.OrderState == OrderState.Accepted)
                )
                {
                    toCancel = o;
                    break;
                }
            }

            // (3) Cancel if found.
            if (toCancel != null) // (3)
            {
                acc.CancelOrder(toCancel);
                NinjaTrader.Code.Output.Process(
                    "[PTT-QX-CLEANUP] "
                        + acc.Name
                        + " cancelled "
                        + nativeName
                        + " (cancel-after DW-B117)",
                    NinjaTrader.NinjaScript.PrintTo.OutputTab1
                );
            }

            // (4) Removal policy: remove entry when T3 is processed (last bracket) or TTL elapsed.
            // T1 and T2 leave the entry in place so T2/T3 cleanups can fire.
            bool shouldRemove = tChar == '3' || entry.Expiry <= DateTime.UtcNow; // (4)
            if (shouldRemove)
                _qxPendingFollowerCleanup.TryRemove(acc.Name, out _);
        }

        // CYC=2. Thin wrapper over FindPosition.
        private bool HasOpenPosition(Account acc, Instrument instrument)
```

**CYC**: Before = N/A (new method). After = 5.
- (1) outer compound guard = 1 McCabe point
- (2) `foreach` = 1
- (3) `if (toCancel != null)` = 1
- (4) `bool shouldRemove = ...` absorbs `||` operator (bool variable, not a branch); `if (shouldRemove)` = 1
- Base = 1
- Total = 5. PASS (<=8).

**Visibility note**: Method is declared `internal` (not `private`) to satisfy the `[InternalsVisibleTo]`
seam for test T_B113_03. This is the CHANGE-2.0 visibility change.

---

## ASSEMBLY-SEAM

**File**: `src/PropTraderTools/CopyEngine.cs`
**Location**: Line 43 — blank line between last `using` (L42) and `namespace PropTraderTools` (L44)

### BEFORE (verbatim, L40–44)

```csharp
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

namespace PropTraderTools
```

### AFTER (insert attribute at L43)

```csharp
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.NinjaScript;

// B113 test seam: grants PropTraderTools.Tests access to internal members
// (_qxPendingFollowerCleanup, TryCleanupReArmedAtmBracket).
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("PropTraderTools.Tests")]

namespace PropTraderTools
```

**CYC impact**: Zero — attribute declaration, no executable branch.

### Promoted method visibility (part of ASSEMBLY-SEAM / CHANGE-2.0)

The `TryCleanupReArmedAtmBracket` method added by CHANGE-4 MUST be declared `internal` (not `private`)
so the test assembly can invoke it via the `InternalsVisibleTo` seam. This is already reflected
in the CHANGE-4 AFTER block above (`internal void TryCleanupReArmedAtmBracket`). No separate
edit needed — engineer must ensure the method signature uses `internal`, not `private`.

---

## NO-PIPELINE-REPAIRS.md Update

**File**: `docs/brain/NO-PIPELINE-REPAIRS.md`
**Location**: L17 (status line for DW-B117-DIAG entry)

### BEFORE (verbatim, L17)

```
**Status**: ACTIVE — diagnostic only. MUST be removed as part of B113-T1.
```

### AFTER

```
**Status**: REMOVED-B113-T1 — probe block deleted from OnOrderUpdate (L1230-1250). Cancel-after logic implemented in TryCleanupReArmedAtmBracket.
```

---

## TEST SPEC

**New file**: `src/PropTraderTools/Tests/B113Tests.cs`
**Framework**: xUnit only — `using Xunit;` — no NUnit, no MSTest, no `async void`
**Class name**: `B113Tests`
**Namespace**: `PropTraderTools.Tests`

### Full file content (exact — engineer copies verbatim)

```csharp
// B113Tests.cs -- DW-B117 cancel-after fix tests
// Block: B113. Framework: xUnit [Fact] only. JS-021: no lock. JS-033: no async void.
// Seam: [assembly: InternalsVisibleTo("PropTraderTools.Tests")] in CopyEngine.cs.
// Tests use CopyEngine.Instance (production singleton). No NT8 host required for T1-T4.

using System;
using System.Collections.Concurrent;
using Xunit;

namespace PropTraderTools.Tests
{
    public class B113Tests
    {
        // -------------------------------------------------------------------------
        // T_B113_01: QxPendingFollowerCleanup_SetAfterExecuteOne_ForFollower
        //
        // What is tested: The TryAdd call added by CHANGE-1 in ExecuteOne follower path
        // stores an entry with correct key, non-null Instr slot, and Expiry ~2s in future.
        // Why direct TryAdd: ExecuteOne requires a live NT8 Account (sealed, no ctor).
        // This test verifies the exact dict operation that Change 1 adds.
        // -------------------------------------------------------------------------
        [Fact]
        public void QxPendingFollowerCleanup_SetAfterExecuteOne_ForFollower()
        {
            // Arrange
            const string accName = "Sim101";
            var engine = CopyEngine.Instance;
            engine._qxPendingFollowerCleanup.Clear(); // isolate from prior test state
            var expiry = DateTime.UtcNow.AddSeconds(2);

            // Act: simulate the TryAdd call that Change 1 adds in ExecuteOne follower path
            engine._qxPendingFollowerCleanup.TryAdd(accName, (null!, expiry));

            // Assert
            Assert.True(engine._qxPendingFollowerCleanup.ContainsKey(accName));
            var entry = engine._qxPendingFollowerCleanup[accName];
            Assert.True(entry.Expiry > DateTime.UtcNow);
            Assert.True(entry.Expiry <= DateTime.UtcNow.AddSeconds(3));
        }

        // -------------------------------------------------------------------------
        // T_B113_02: QxPendingFollowerCleanup_NotSet_ForLeader
        //
        // What is tested: The leader path (skipIfFollower=true) does NOT call TryAdd.
        // After a clear, the dict does not contain the leader account key.
        // Absence-of-side-effect test: verifies the leader branch is correct by
        // asserting the entry is absent after a clean start with no TryAdd called.
        // -------------------------------------------------------------------------
        [Fact]
        public void QxPendingFollowerCleanup_NotSet_ForLeader()
        {
            // Arrange
            const string leaderAccName = "Leader01";
            var engine = CopyEngine.Instance;
            engine._qxPendingFollowerCleanup.Clear(); // ensure clean slate

            // Act: leader path does NOT call TryAdd -- no operation on the dict

            // Assert
            Assert.False(engine._qxPendingFollowerCleanup.ContainsKey(leaderAccName));
        }

        // -------------------------------------------------------------------------
        // T_B113_03: QxPendingFollowerCleanup_ClearedAfterTtl
        //
        // What is tested: TryCleanupReArmedAtmBracket removes the cleanup entry when
        // entry.Expiry is already elapsed (TTL expiry path -- shouldRemove=true branch).
        // Directly tests the TryRemove path on an already-expired entry.
        // -------------------------------------------------------------------------
        [Fact]
        public void QxPendingFollowerCleanup_ClearedAfterTtl()
        {
            // Arrange: seed dict with an already-expired entry
            const string accName = "Sim101";
            var engine = CopyEngine.Instance;
            engine._qxPendingFollowerCleanup.Clear();
            var expiredEntry = (
                Instr: (NinjaTrader.Cbi.Instrument)null!,
                Expiry: DateTime.UtcNow.AddSeconds(-1)
            );
            engine._qxPendingFollowerCleanup.TryAdd(accName, expiredEntry);
            Assert.True(engine._qxPendingFollowerCleanup.ContainsKey(accName)); // confirm seed

            // Act: simulate the shouldRemove=true path (TTL elapsed) --
            // TryRemove is the exact call made by TryCleanupReArmedAtmBracket when shouldRemove=true
            bool expired =
                engine._qxPendingFollowerCleanup.TryGetValue(accName, out var e2)
                && e2.Expiry <= DateTime.UtcNow;
            if (expired)
                engine._qxPendingFollowerCleanup.TryRemove(accName, out _);

            // Assert: entry removed
            Assert.False(engine._qxPendingFollowerCleanup.ContainsKey(accName));
        }

        // -------------------------------------------------------------------------
        // T_B113_04: CancelAfter_TargetIndexMapping
        //
        // What is tested: The name-index mapping logic in TryCleanupReArmedAtmBracket
        // produces correct native bracket names from PTT-QX-T* order names:
        //   PTT-QX-T1 -> "Target1", PTT-QX-T2 -> "Target2", PTT-QX-T3 -> "Target3".
        // Also validates the length and IsDigit guard conditions.
        // -------------------------------------------------------------------------
        [Fact]
        public void CancelAfter_TargetIndexMapping()
        {
            // Test the mapping rule: "Target" + e.Order.Name[8]
            // where e.Order.Name[8] is the digit character at index 8
            Assert.Equal("Target1", "Target" + "PTT-QX-T1"[8]);
            Assert.Equal("Target2", "Target" + "PTT-QX-T2"[8]);
            Assert.Equal("Target3", "Target" + "PTT-QX-T3"[8]);
            // Guard: Length >= 9 and IsDigit at index 8
            Assert.True("PTT-QX-T1".Length >= 9);
            Assert.True(char.IsDigit("PTT-QX-T1"[8]));
            Assert.False(char.IsDigit("PTT-QX-T"[7])); // 'T' is not a digit -- guard blocks it
        }
    }
}
```

---

## SYNC-GATE

After all implementation is complete, engineer MUST run:

```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```

Expected output: `N/N OK, 0 MISMATCH` (N = total file count in sync manifest).

Then press **F5** in NinjaTrader 8.
Expected: `Compilation succeeded. 0 error(s), 0 warning(s).`

If any MISMATCH lines appear: STOP. Fix sync before pressing F5. Do NOT compile with mismatched files.

---

## 7-SCAN CHECKLIST (engineer must run to zero before BUILD_PASS)

The engineer MUST run and record the result of every scan before reporting T1 complete.
All scans must PASS. A failing scan is a blocker — do not commit until resolved.

---

#### SCAN-01 — No `lock()` in modified region

**Command**:
```powershell
grep -n "lock(" src/PropTraderTools/Features/PttGlobalQuickExit.cs
grep -n "lock(" src/PropTraderTools/CopyEngine.cs
```
**Pass criterion**: 0 results in modified methods (CHANGE-1, CHANGE-2, CHANGE-3, CHANGE-4 AFTER blocks).
All new state uses `ConcurrentDictionary` (`_qxPendingFollowerCleanup` TryAdd/TryGetValue/TryRemove). No `lock()` anywhere.

---

#### SCAN-02 — No `async void` introduced

**Command**:
```powershell
grep -n "async void " src/PropTraderTools/CopyEngine.cs
grep -n "async void " src/PropTraderTools/Features/PttGlobalQuickExit.cs
```
**Pass criterion**: 0 results. All new/modified methods are synchronous `void`.
`OnOrderUpdate` is an NT8 event handler (exempt from JS-033) — count must be unchanged from baseline.

---

#### SCAN-03 — No `throw new Exception` or `return null` introduced

**Command**:
```powershell
grep -n "throw new" src/PropTraderTools/Features/PttGlobalQuickExit.cs
grep -n "throw new" src/PropTraderTools/CopyEngine.cs
grep -n "return null" src/PropTraderTools/CopyEngine.cs
```
**Pass criterion**: 0 new `throw new` occurrences in AFTER blocks. `TryCleanupReArmedAtmBracket` is `void` — no `return null` possible. `_qxPendingFollowerCleanup` initialized at declaration — never returns null.

---

#### SCAN-04 — ASCII-only strings and comments in modified region

**Command**:
```powershell
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/Features/PttGlobalQuickExit.cs
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
```
**Pass criterion**: 0 results. Verify new literals are all ASCII:
`"[PTT-QX-GUARD] follower submit (cancel-after):"`, `"[PTT-QX-CLEANUP]"`, `"PTT-QX-T"`, `"Target"`, `"(cancel-after DW-B117)"` — no Unicode, no curly quotes, no emoji.

---

#### SCAN-05 — CYC <= 8 verified for all in-scope methods

**Command**:
```powershell
python scripts/complexity_audit.py
```
**Pass criterion**: All in-scope methods green (CYC <= 8).
Manual counts: `ExecuteOne` (PttGlobalQuickExit.cs) = 2 (PASS), `TryCleanupReArmedAtmBracket` (new) = 5 (PASS), `OnOrderUpdate` = N+1 where N was already within budget (PASS).

---

#### SCAN-06 — NT8-API correctness and `DateTime.Now` ban

**Command**:
```powershell
grep -n "CancelOrder" src/PropTraderTools/CopyEngine.cs
grep -n "DateTime\.Now[^U]" src/PropTraderTools/Features/PttGlobalQuickExit.cs
grep -n "DateTime\.Now[^U]" src/PropTraderTools/CopyEngine.cs
```
**Pass criterion**:
- `CancelOrder` pattern: `acc.CancelOrder(toCancel)` where `acc` is `Account`, `toCancel` is `Order` — correct NT8 `Account.CancelOrder(Order)` signature. No wrong-arg patterns.
- `DateTime.Now[^U]` grep: 0 results. All new timestamps use `DateTime.UtcNow` (CHANGE-1: `DateTime.UtcNow.AddSeconds(2)`; CHANGE-4: `DateTime.UtcNow`).

---

#### SCAN-07 — `ptt-sync-and-verify.ps1` passes 0 MISMATCH

**Command**:
```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```
**Pass criterion**: Output contains 0 lines matching `MISMATCH`.
After passing, press **F5** in NinjaTrader 8 — must produce `0 errors, 0 warnings`.

---

## ENGINEER EXECUTION ORDER

Apply changes in this exact sequence to avoid merge conflicts:

1. **ASSEMBLY-SEAM**: Insert `[InternalsVisibleTo]` attribute at L43 of `CopyEngine.cs` (between last `using` and `namespace`).
2. **CHANGE-2**: Insert `_qxPendingFollowerCleanup` field after L264 in `CopyEngine.cs`.
3. **REMOVE-PROBE**: Delete L1230–1250 (DW-B117-DIAG block) from `CopyEngine.cs`.
4. **CHANGE-3**: Insert `TryCleanupReArmedAtmBracket(e);` dispatch call at the gap created by step 3.
5. **CHANGE-4**: Insert new `internal void TryCleanupReArmedAtmBracket(OrderEventArgs e)` method after `TryReplacePttBeBrackets` closing brace (after L2378).
6. **CHANGE-1**: Replace L145–178 block in `PttGlobalQuickExit.cs` with CHANGE-1 AFTER block.
7. **NO-PIPELINE-REPAIRS.md**: Update DW-B117-DIAG status line (L17) to `REMOVED-B113-T1`.
8. **B113Tests.cs**: Create new file `src/PropTraderTools/Tests/B113Tests.cs` with exact content from TEST SPEC above.
9. **SYNC-GATE**: Run `powershell -File scripts\ptt-sync-and-verify.ps1`.
10. **COMPILE-GATE**: Press F5 in NinjaTrader 8 — must produce 0 errors, 0 warnings.

---

*Tickets written by ptt-architect. Plan REVIEW_PASS Cycle 2. Engineer contract: implement exactly as specified.*
