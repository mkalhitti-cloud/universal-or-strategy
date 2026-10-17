# B209 — Ticket File
# PTT-COPIER-B209: BE-ALL Permanent Lock Fix (FIX-G-1 + FIX-G-2)

**Block**: B209
**Phase**: 3 — Ticket Generation
**Status**: TICKETS_COMPLETE
**Author**: ptt-architect
**Date**: 2026-07-07
**Plan source**: docs/brain/B209/02-architecture-plan.md (REVIEW_PASS)

---

## EXECUTION ORDER MANDATE

Ticket-1 MUST be completed and VERIFY_PASS confirmed before Ticket-2 begins.
Both tickets share the `CopyEngine.cs` write-set — concurrent editing is BANNED.

---

---

# TICKET-1 — FIX-G-1: TryClearBeSwapOnStopWorking + IsBeClearStateTrigger Seam

## SCOPE LOCK — TICKET 1 ONLY

Only `TryClearBeSwapOnStopWorking` is modified. Only `IsBeClearStateTrigger` is added.
Do NOT touch `TryCancelSingleStaleGtcExit`. Do NOT touch `IsBeSwapGtcSweepGuard`.
Do NOT create B209Tests.cs in this ticket. Do NOT modify any other method.

## Spec Requirement IDs

- B209-FIX-G-1
- B208 SIM Round 6 root-cause: permanent `_beSwapInProgress` flag lock on OCO cancel path

## Files to Modify

| Action | File | Location |
|--------|------|----------|
| MODIFY | `src/PropTraderTools/CopyEngine.cs` | `TryClearBeSwapOnStopWorking` at ~line 3159 |
| ADD | `src/PropTraderTools/CopyEngine.cs` | `IsBeClearStateTrigger` immediately after `TryClearBeSwapOnStopWorking` (~line 3170) |

**No other files are touched in Ticket-1.**

## Method Signatures

### TryClearBeSwapOnStopWorking — unchanged signature, body modified

**BEFORE (current live code — verified at line 3159–3169):**
```csharp
// B208-FIX-F-B: clear _beSwapInProgress when PTT-BE-Stop-N reaches Working.
// Called from OnOrderUpdate before TryNakedDetect. NT8 Working = stop is live at broker.
// Once Working, the position is protected -- NakedPositionDetector must not suppress flatten.
// CYC=3: (1) name null/prefix check, (2) Working state check, (3) IsBeSwapInProgress check.
// JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
private void TryClearBeSwapOnStopWorking(OrderEventArgs e)
{
    if (e.Order.Name == null
        || !e.Order.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) // (1)
        return;
    if (e.Order.OrderState != OrderState.Working) // (2)
        return;
    if (!IsBeSwapInProgress(e.Order.Account?.Name)) // (3)
        return;
    SetBeSwapInProgress(e.Order.Account.Name, false);
}
```

**AFTER (exact replacement body):**
```csharp
// B208-FIX-G-1: clear on Working (normal: stop live), Cancelled (OCO: target filled),
// Rejected (bad price: retry handles), Accepted (SIM belt+suspenders: mirrors FIX-F-A).
// All four are terminal or confirmed-live outcomes for PTT-BE-Stop-N.
// Permanent lock eliminated: flag cannot leak across trade cycles.
// CYC=5: (1) name check, (2a-d) four-state isClearState, (3) IsBeSwapInProgress.
// JS-021: no lock. JS-001: no throw. JS-002: void. ASCII-only.
private void TryClearBeSwapOnStopWorking(OrderEventArgs e)
{
    if (e.Order.Name == null
        || !e.Order.Name.StartsWith("PTT-BE-Stop", StringComparison.Ordinal)) // (1)
        return;
    bool isClearState = e.Order.OrderState == OrderState.Working    // (2a) normal: stop live
        || e.Order.OrderState == OrderState.Cancelled               // (2b) OCO: target filled
        || e.Order.OrderState == OrderState.Rejected                // (2c) bad price: evict+retry
        || e.Order.OrderState == OrderState.Accepted;               // (2d) SIM: mirrors FIX-F-A
    if (!isClearState)
        return;
    if (!IsBeSwapInProgress(e.Order.Account?.Name)) // (3)
        return;
    SetBeSwapInProgress(e.Order.Account.Name, false);
}
```

### IsBeClearStateTrigger — new method, placed immediately after TryClearBeSwapOnStopWorking

**Signature**: `internal static bool IsBeClearStateTrigger(string orderName, OrderState state)`

**Exact body:**
```csharp
// B209: testable seam for TryClearBeSwapOnStopWorking state-check logic.
// Extracts isClearState predicate so xUnit can verify all four states without NT8 OrderEventArgs.
// CYC=1 (expression body). JS-021: no lock. JS-001: no throw. ASCII-only.
internal static bool IsBeClearStateTrigger(string orderName, OrderState state)
{
    if (orderName == null || !orderName.StartsWith("PTT-BE-Stop", StringComparison.Ordinal))
        return false;
    return state == OrderState.Working
        || state == OrderState.Cancelled
        || state == OrderState.Rejected
        || state == OrderState.Accepted;
}
```

**Placement rule**: Insert the entire method block directly after the closing `}` of
`TryClearBeSwapOnStopWorking`, before the `// B200-CLOSE-LANED-01` comment block.

## Edit Technique

Use `apply_diff` or `search_and_replace` for surgical edits ONLY.
Do NOT replace the entire file. Do NOT use write_file on CopyEngine.cs.
Single-hunk ban: the resulting commit MUST have more than one `@@` hunk.

**Step 1 — Replace TryClearBeSwapOnStopWorking body** using search_and_replace:
- Search: the exact BEFORE block shown above (lines 3154–3169 inclusive, including the comment header)
- Replace: the exact AFTER block shown above

**Step 2 — Insert IsBeClearStateTrigger** using insert_content:
- Target: immediately after the new closing `}` of `TryClearBeSwapOnStopWorking`
- Content: the IsBeClearStateTrigger method block shown above (with blank line before and after)

## CYC Targets

| Method | Spec CYC | Strict Lizard max | Required |
|--------|----------|-------------------|----------|
| `TryClearBeSwapOnStopWorking` | 5 | 7 | <= 8 — PASS |
| `IsBeClearStateTrigger` | 1 (seam) | ~5 | <= 8 — PASS |

**Diff summary**: Only two lines removed (`// CYC=3` comment line and `if (e.Order.OrderState != OrderState.Working) // (2) return;`). Only the `bool isClearState` block and `if (!isClearState) return;` added. Total net new lines: ~6.

## JS Rule Constraints

| Rule | Constraint | This Ticket |
|------|-----------|-------------|
| JS-021 (P0) | No `lock()` anywhere in modified/added code | PASS — `_beSwapInProgress` is `ConcurrentDictionary`, no lock added |
| JS-001 (P0) | No `throw new XxxException` in hot paths | PASS — no throw in either method |
| JS-002 (P0) | No `return null` in new methods | PASS — both methods return `void` or `bool` |
| JS-033 (P0) | No `async void` (non-event-handler) | PASS — both methods are synchronous |
| JS-015 (P1) | `StringComparison.Ordinal` on all `StartsWith` | PASS — `StringComparison.Ordinal` present in both calls |
| ASCII-only | No Unicode/emoji in string literals | PASS — "PTT-BE-Stop" is ASCII |

## 7-SCAN CHECKLIST (TICKET-1)

Engineer MUST run all 7 scans after edits and before commit. All must PASS.

```
SCAN-01: grep -rn "lock(" src/PropTraderTools/ --include="*.cs"
         REQUIRED: zero matches in lines added/modified by this ticket.
         (Existing lock() matches in unrelated methods are pre-existing — do not count.)

SCAN-02: grep -rn "async void " src/PropTraderTools/ --include="*.cs"
         REQUIRED: zero matches in lines added/modified by this ticket.

SCAN-03: grep -rn "return null;" src/PropTraderTools/ --include="*.cs"
         REQUIRED: zero matches in lines added/modified by this ticket.
         (IsBeClearStateTrigger returns bool. TryClearBeSwapOnStopWorking is void.)

SCAN-04: CYC check — verify cyclomatic complexity:
         TryClearBeSwapOnStopWorking: CYC <= 8 (target spec=5, strict Lizard max=7)
         IsBeClearStateTrigger: CYC <= 8 (target=1 expression-body logic, max strict ~5)
         Run: lizard src/PropTraderTools/CopyEngine.cs --CCN 8
         OR manually count branches: if/||/&&/? each = +1.

SCAN-05: grep -rn "throw new" src/PropTraderTools/ --include="*.cs"
         REQUIRED: zero matches in lines added/modified by this ticket.

SCAN-06: ASCII-only check — visually inspect all string literals in modified/added code:
         "PTT-BE-Stop" — ASCII PASS
         "[GTC-SWEEP]" — not in this ticket
         All comments — ASCII-only, no Unicode arrows or symbols.
         REQUIRED: zero non-ASCII characters in new/modified lines.

SCAN-07: ptt-sync: powershell -File .\scripts\ptt-sync.ps1
         REQUIRED: output N/0 (zero sync errors).
         Run AFTER the CopyEngine.cs edit is saved.
```

## Guardian Symbol Check

```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```

REQUIRED: PASS before VERIFY_PASS may be issued.

## Ph4b Deletion Audit

```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"
```

**Expected result for Ticket-1**: The only deleted lines are the old `// CYC=3` comment block
and the single `if (e.Order.OrderState != OrderState.Working) // (2) return;` line.
These are NOT method-signature deletions — they are body lines being replaced.
No `private`/`internal`/`public` method signatures should appear in the deletion output.
If any method signature is listed as deleted: STOP — report to Director before proceeding.

## Single-Hunk Ban Check

```powershell
(git show HEAD -- CopyEngine.cs | Select-String "^@@").Count -eq 1
```

**Required result**: `False`.
If `True` (only one hunk): this indicates a full-file replace was committed — automatic VERIFY_FAIL.
Engineer must revert and redo with surgical `apply_diff`/`search_and_replace` only.

## Build Pass Requirement

After all edits and before committing:
```powershell
dotnet build src/PropTraderTools/ --no-incremental
```
REQUIRED: zero errors, zero new warnings.

## No Tests in Ticket-1

Ticket-1 does not include test creation. Tests are in Ticket-2 only.
The `IsBeClearStateTrigger` seam is added here so that Ticket-2 can reference it in B209Tests.cs.

---

---

# TICKET-2 — FIX-G-2: TryCancelSingleStaleGtcExit + IsBeSwapGtcSweepGuard Seam + B209Tests.cs

## PREREQUISITE GATE

**Ticket-2 MUST NOT begin until Ticket-1 VERIFY_PASS is confirmed.**
The engineer must have the `ticket-1-verification.md` VERIFY_PASS document before starting.

## SCOPE LOCK — TICKET 2 ONLY

Only `TryCancelSingleStaleGtcExit` is modified in CopyEngine.cs.
Only `IsBeSwapGtcSweepGuard` is added to CopyEngine.cs.
`B209Tests.cs` is created as a new file.
Do NOT modify `TryClearBeSwapOnStopWorking`. Do NOT modify `IsBeClearStateTrigger`.
Do NOT modify any other existing method in CopyEngine.cs.

## Spec Requirement IDs

- B209-FIX-G-2
- B208 SIM Round 6 root-cause: premature GTC sweep of live PTT-BE-Stop-N during rapid re-entry

## Files to Modify / Create

| Action | File | Location |
|--------|------|----------|
| MODIFY | `src/PropTraderTools/CopyEngine.cs` | `TryCancelSingleStaleGtcExit` at ~line 2177 |
| ADD | `src/PropTraderTools/CopyEngine.cs` | `IsBeSwapGtcSweepGuard` after `IsBeClearStateTrigger` (placed by Ticket-1) |
| CREATE | `src/PropTraderTools/Tests/B209Tests.cs` | New file — all 10 [Fact] tests |

**No other files are touched in Ticket-2.**

## InternalsVisibleTo Prerequisite

Before creating `B209Tests.cs`, verify that the test project has access to `internal` members of
`PropTraderTools`. Check `AssemblyInfo.cs` or the assembly's `Properties` folder for:
```csharp
[assembly: InternalsVisibleTo("PropTraderTools.Tests")]
```
(Exact assembly name may differ — match to the test project name.)
If missing: add the attribute to `AssemblyInfo.cs` before creating the test file.
This is a prerequisite for `CopyEngine.IsBeClearStateTrigger` and `CopyEngine.IsBeSwapGtcSweepGuard`
to be accessible from the test project. Reference: B197 T3 used this same seam pattern.

## Method Signatures

### TryCancelSingleStaleGtcExit — unchanged signature, body modified

**BEFORE (current live code — verified at line 2174–2191):**
```csharp
// B197 T3: TryCancelSingleStaleGtcExit -- helper extracted from TryCancelStaleGtcExitOnEntry.
// Handles per-order stale GTC cancel with try/catch. Keeps parent CYC<=8.
// JS-021: no lock. JS-001: catch does NOT rethrow. ASCII-only.
private void TryCancelSingleStaleGtcExit(Account acc, Order o)
{
    if (!IsStaleGtcExitOrder(o)) return;
    try
    {
        acc.Cancel(new Order[] { o });
        if (StatusUpdate != null)
            StatusUpdate(acc.Name + ": [GTC-SWEEP] cancelled " + o.Name);
    }
    catch (Exception ex)
    {
        if (StatusUpdate != null)
            StatusUpdate("[GTC-SWEEP] cancel error: " + ex.Message);
    }
}
```

**AFTER (exact replacement body):**
```csharp
// B208-FIX-G-2: do not cancel PTT-BE-* orders while BE-swap is in progress.
// IsBeSwapInProgress stays true until PTT-BE-Stop-N reaches a terminal state (FIX-G-1).
// Without this guard, a rapid PTT-Copy/Entry fill can sweep a Working PTT-BE-Stop-N
// via TryCancelStaleGtcExitOnEntry, leaving the follower unprotected.
// CYC: +1 branch. ASCII-only. JS-021: IsBeSwapInProgress is lock-free. JS-001: no throw.
private void TryCancelSingleStaleGtcExit(Account acc, Order o)
{
    if (!IsStaleGtcExitOrder(o)) return;
    // B208-FIX-G-2: protect live PTT-BE-Stop-N during active swap window.
    if (o.Name != null
        && o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
        && IsBeSwapInProgress(acc.Name))
        return;
    try
    {
        acc.Cancel(new Order[] { o });
        if (StatusUpdate != null)
            StatusUpdate(acc.Name + ": [GTC-SWEEP] cancelled " + o.Name);
    }
    catch (Exception ex)
    {
        if (StatusUpdate != null)
            StatusUpdate("[GTC-SWEEP] cancel error: " + ex.Message);
    }
}
```

### IsBeSwapGtcSweepGuard — new method, placed after IsBeClearStateTrigger

**Signature**: `internal static bool IsBeSwapGtcSweepGuard(string orderName, bool beSwapInProgress)`

**Exact body:**
```csharp
// B209: testable seam for FIX-G-2 guard in TryCancelSingleStaleGtcExit.
// Extracts the BE-swap skip predicate for xUnit testing.
// CYC=1 (expression body). JS-021: no lock. JS-001: no throw. ASCII-only.
internal static bool IsBeSwapGtcSweepGuard(string orderName, bool beSwapInProgress)
    => orderName != null
        && orderName.StartsWith("PTT-BE-", StringComparison.Ordinal)
        && beSwapInProgress;
```

**Placement rule**: Insert after the closing `}` of `IsBeClearStateTrigger` (added by Ticket-1),
before the `// B200-CLOSE-LANED-01` comment block. Blank line before and after.

## Edit Technique

Use `apply_diff` or `search_and_replace` for surgical edits ONLY.
Do NOT replace the entire file. Do NOT use write_file on CopyEngine.cs.
Single-hunk ban: the resulting commit MUST have more than one `@@` hunk.

**Step 1 — Replace TryCancelSingleStaleGtcExit body** using search_and_replace:
- Search: the exact BEFORE block shown above (lines 2174–2191, including the comment header)
- Replace: the exact AFTER block shown above

**Step 2 — Insert IsBeSwapGtcSweepGuard** using insert_content:
- Target: after the closing `}` of `IsBeClearStateTrigger`
- Content: the `IsBeSwapGtcSweepGuard` method block shown above (with blank line before and after)

**Step 3 — Create B209Tests.cs** using write_file (new file, safe to use write_file here):
- File: `src/PropTraderTools/Tests/B209Tests.cs`
- Content: exact test file shown below

## CYC Targets

| Method | Spec CYC | Strict Lizard max | Required |
|--------|----------|-------------------|----------|
| `TryCancelSingleStaleGtcExit` | 2 (delta) | 7 (total) | <= 8 — PASS |
| `IsBeSwapGtcSweepGuard` | 1 (seam) | ~3 | <= 8 — PASS |

**Diff summary**: Three lines inserted into `TryCancelSingleStaleGtcExit` (comment + guard if block).
No lines deleted from the method signature or any other method. The `try/catch` block is unchanged.

## JS Rule Constraints

| Rule | Constraint | This Ticket |
|------|-----------|-------------|
| JS-021 (P0) | No `lock()` anywhere in modified/added code | PASS — `IsBeSwapInProgress` reads `ConcurrentDictionary`, no lock added |
| JS-001 (P0) | No `throw new XxxException` in hot paths | PASS — no new throw; existing `catch` block inherited unchanged |
| JS-002 (P0) | No `return null` in new methods | PASS — both new/modified methods return `void` or `bool` |
| JS-033 (P0) | No `async void` (non-event-handler) | PASS — all methods are synchronous |
| JS-015 (P1) | `StringComparison.Ordinal` on all `StartsWith` | PASS — `StringComparison.Ordinal` present in new guard |
| ASCII-only | No Unicode/emoji in string literals | PASS — "PTT-BE-", "[GTC-SWEEP]" are ASCII |
| [Fact] only | xUnit only — no NUnit/MSTest imports | PASS — B209Tests.cs uses only `using Xunit;` |

## xUnit Tests (B209Tests.cs) — All 10 [Fact] Tests

**Test file path**: `src/PropTraderTools/Tests/B209Tests.cs`
**Framework**: xUnit only. All tests use `[Fact]`. No NUnit. No MSTest.
**Class**: `B209Tests` (no namespace — matches existing test convention; adjust if project convention differs)

### Test List

| # | Test Name | Method Under Test | Input | Expected |
|---|-----------|-------------------|-------|----------|
| 1 | `IsBeClearStateTrigger_Working_ReturnsTrue` | `IsBeClearStateTrigger` | "PTT-BE-Stop-1", Working | `true` |
| 2 | `IsBeClearStateTrigger_Cancelled_ReturnsTrue` | `IsBeClearStateTrigger` | "PTT-BE-Stop-1", Cancelled | `true` |
| 3 | `IsBeClearStateTrigger_Rejected_ReturnsTrue` | `IsBeClearStateTrigger` | "PTT-BE-Stop-1", Rejected | `true` |
| 4 | `IsBeClearStateTrigger_Accepted_ReturnsTrue` | `IsBeClearStateTrigger` | "PTT-BE-Stop-1", Accepted | `true` |
| 5 | `IsBeClearStateTrigger_Submitted_ReturnsFalse` | `IsBeClearStateTrigger` | "PTT-BE-Stop-1", Submitted | `false` |
| 6 | `IsBeClearStateTrigger_WrongName_ReturnsFalse` | `IsBeClearStateTrigger` | "PTT-BE-Target-1", Working | `false` |
| 7 | `IsBeSwapGtcSweepGuard_PttBeName_SwapActive_ReturnsTrue` | `IsBeSwapGtcSweepGuard` | "PTT-BE-Stop-1", true | `true` |
| 8 | `IsBeSwapGtcSweepGuard_PttBeName_SwapInactive_ReturnsFalse` | `IsBeSwapGtcSweepGuard` | "PTT-BE-Stop-1", false | `false` |
| 9 | `IsBeSwapGtcSweepGuard_NonPttBeName_SwapActive_ReturnsFalse` | `IsBeSwapGtcSweepGuard` | "Stop1", true | `false` |
| 10 | `IsBeSwapGtcSweepGuard_NullName_ReturnsFalse` | `IsBeSwapGtcSweepGuard` | null, true | `false` |

### Exact Test File Content

```csharp
// B209: xUnit tests for FIX-G-1 (IsBeClearStateTrigger) and FIX-G-2 (IsBeSwapGtcSweepGuard) seams.
// [assembly: InternalsVisibleTo] required for internal static access to CopyEngine.
// JS-021: no lock. [Fact] only. ASCII-only. xUnit only -- no NUnit/MSTest.
using Xunit;
using NinjaTrader.Cbi;
using PropTraderTools;

public class B209Tests
{
    // ---- FIX-G-1 seam tests: IsBeClearStateTrigger ----

    [Fact]
    public void IsBeClearStateTrigger_Working_ReturnsTrue()
        => Assert.True(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Working));

    [Fact]
    public void IsBeClearStateTrigger_Cancelled_ReturnsTrue()
        => Assert.True(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Cancelled));

    [Fact]
    public void IsBeClearStateTrigger_Rejected_ReturnsTrue()
        => Assert.True(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Rejected));

    [Fact]
    public void IsBeClearStateTrigger_Accepted_ReturnsTrue()
        => Assert.True(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Accepted));

    [Fact]
    public void IsBeClearStateTrigger_Submitted_ReturnsFalse()
        => Assert.False(CopyEngine.IsBeClearStateTrigger("PTT-BE-Stop-1", OrderState.Submitted));

    [Fact]
    public void IsBeClearStateTrigger_WrongName_ReturnsFalse()
        => Assert.False(CopyEngine.IsBeClearStateTrigger("PTT-BE-Target-1", OrderState.Working));

    // ---- FIX-G-2 seam tests: IsBeSwapGtcSweepGuard ----

    [Fact]
    public void IsBeSwapGtcSweepGuard_PttBeName_SwapActive_ReturnsTrue()
        => Assert.True(CopyEngine.IsBeSwapGtcSweepGuard("PTT-BE-Stop-1", true));

    [Fact]
    public void IsBeSwapGtcSweepGuard_PttBeName_SwapInactive_ReturnsFalse()
        => Assert.False(CopyEngine.IsBeSwapGtcSweepGuard("PTT-BE-Stop-1", false));

    [Fact]
    public void IsBeSwapGtcSweepGuard_NonPttBeName_SwapActive_ReturnsFalse()
        => Assert.False(CopyEngine.IsBeSwapGtcSweepGuard("Stop1", true));

    [Fact]
    public void IsBeSwapGtcSweepGuard_NullName_ReturnsFalse()
        => Assert.False(CopyEngine.IsBeSwapGtcSweepGuard(null, true));
}
```

**Test count verification**: 6 tests for `IsBeClearStateTrigger` + 4 tests for `IsBeSwapGtcSweepGuard` = 10 total. PASS.

## 7-SCAN CHECKLIST (TICKET-2)

Engineer MUST run all 7 scans after edits and before commit. All must PASS.

```
SCAN-01: grep -rn "lock(" src/PropTraderTools/ --include="*.cs"
         REQUIRED: zero matches in lines added/modified by this ticket.

SCAN-02: grep -rn "async void " src/PropTraderTools/ --include="*.cs"
         REQUIRED: zero matches in lines added/modified by this ticket.

SCAN-03: grep -rn "return null;" src/PropTraderTools/ --include="*.cs"
         REQUIRED: zero matches in lines added/modified by this ticket.
         (IsBeSwapGtcSweepGuard returns bool. TryCancelSingleStaleGtcExit is void.)

SCAN-04: CYC check — verify cyclomatic complexity:
         TryCancelSingleStaleGtcExit: CYC <= 8 (spec delta=+1, strict Lizard total max=7)
         IsBeSwapGtcSweepGuard: CYC <= 8 (expression-body seam, max strict ~3)
         Run: lizard src/PropTraderTools/CopyEngine.cs --CCN 8
         OR manually count branches: if/||/&&/? each = +1.

SCAN-05: grep -rn "throw new" src/PropTraderTools/ --include="*.cs"
         REQUIRED: zero matches in lines added/modified by this ticket.
         (Existing catch block uses ex.Message — no throw. Unchanged.)

SCAN-06: ASCII-only check — visually inspect all string literals in modified/added code:
         "PTT-BE-" — ASCII PASS
         "PTT-BE-Stop-1" (test file) — ASCII PASS
         "[GTC-SWEEP]" — ASCII PASS (existing, unchanged)
         "Stop1" (test file) — ASCII PASS
         All comments — ASCII-only, no Unicode arrows or symbols.
         REQUIRED: zero non-ASCII characters in new/modified lines.

SCAN-07: ptt-sync: powershell -File .\scripts\ptt-sync.ps1
         REQUIRED: output N/0 (zero sync errors).
         Run AFTER all CopyEngine.cs edits and B209Tests.cs creation are saved.
```

## Guardian Symbol Check

```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```

REQUIRED: PASS before VERIFY_PASS may be issued.

## Ph4b Deletion Audit

```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"
```

**Expected result for Ticket-2**: Zero method-signature deletions.
The `TryCancelSingleStaleGtcExit` signature is unchanged — only the body gains a guard (3 new lines).
`IsBeSwapGtcSweepGuard` is added, not deleted.
If any method signature is listed as deleted: STOP — report to Director before proceeding.

## Single-Hunk Ban Check

```powershell
(git show HEAD -- CopyEngine.cs | Select-String "^@@").Count -eq 1
```

**Required result**: `False`.
If `True` (only one hunk): automatic VERIFY_FAIL. Revert and redo with surgical edits.

## Build Pass Requirement

After all edits and before committing:
```powershell
dotnet build src/PropTraderTools/ --no-incremental
```
REQUIRED: zero errors, zero new warnings.

---

---

## CROSS-TICKET COMPLIANCE MATRIX

| Rule | Ticket-1 | Ticket-2 | Test File |
|------|----------|----------|-----------|
| JS-021 (no lock) | PASS | PASS | N/A |
| JS-001 (no throw in hot path) | PASS | PASS (catch inherited) | N/A |
| JS-002 (no return null) | PASS | PASS | N/A |
| JS-033 (no async void) | PASS | PASS | N/A |
| JS-015 (StringComparison.Ordinal) | PASS | PASS | N/A |
| ASCII-only | PASS | PASS | PASS |
| CYC <= 8 | PASS (max 7) | PASS (max 7) | N/A |
| [Fact] only | N/A | N/A | PASS |
| ptt-sync N/0 | REQUIRED | REQUIRED | N/A |
| guardian symbols PASS | REQUIRED | REQUIRED | N/A |
| no full-file replace | REQUIRED | REQUIRED | N/A |

---

## Ph5 SPEC UPDATE REQUIREMENTS (for ptt-plan-reviewer, not ptt-engineer)

After Ticket-1 and Ticket-2 both reach VERIFY_PASS, the following spec updates are required in Ph5:

1. `specs/002-trade-copier-spec.html` — B208 block: mark FIX-G-1 and FIX-G-2 as APPLIED with ptt-sync output.
2. `specs/002-trade-copier-spec.html` — B208 card title: change from "FIX-G DESIGNED — AWAITING DIRECTOR APPROVAL" to "FIX-G APPLIED — AWAITING SIM ROUND 7".
3. `specs/002-trade-copier-spec.html` — B208 bug table row: update status column to reflect both fixes applied.
4. `docs/brain/B209/06-deferred-backlog.md` — record: "CLOSED: permanent _beSwapInProgress lock on OCO cancel path (FIX-G-1). CLOSED: premature GTC sweep of live PTT-BE-Stop-N (FIX-G-2)."
5. `NakedPositionDetector` cross-check: verify other `IsBeSwapInProgress` callers are unaffected by new clear-on-Cancelled behaviour.

**These Ph5 tasks are NOT in scope for ptt-engineer. They belong exclusively to ptt-plan-reviewer in Ph5.**
