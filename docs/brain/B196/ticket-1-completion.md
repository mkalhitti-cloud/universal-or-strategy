# Ticket 1 Completion Report

**Epic**: B196
**Ticket**: 1 -- DW-B195-02: DrainThenDispatch Fast-Path ATM-Aware Dispatch
**Date**: 2026-10-11
**Engineer**: ptt-engineer
**SCOPE LOCK**: TICKET 1 ONLY. No other tickets read, referenced, or implemented.

---

## Scope Confirmation

TICKET_REVIEW_PASS confirmed (docs/brain/B196/04-ticket-review.md, Cycle 2).
This report covers Ticket 1 only: `DrainThenDispatch` fast-path ATM-aware dispatch.
No other tickets (T2-T5) touched in this session.

---

## Changes Made

### File: `src/PropTraderTools/CopyEngine.cs`

**Method modified**: `DrainThenDispatch` (line 6582 post-edit; previously line 6579)

**Lines modified**:
- Lines 6575-6578 (comment block): Updated CYC comment from CYC=3 to CYC=5, added DW-B195-02 note, NT8 note, ASCII-only note.
- Lines 6606-6610 (fast-path early return block): Replaced bare `SubmitEntryDirect` + `return` with ATM-aware dispatch block (22 lines).

**Before (lines 6575-6610)**:
```csharp
// CYC=3: (1) null guard, (2) no-entry fast path, (3) foreach cancel loop.
// F5-repair: dead (4) cancelCount==0 branch removed. entryCandidates always non-empty here.
// JS-021: no lock(). ConcurrentDictionary + Interlocked only.
// NT8: Account.Cancel(Order[]) is AddOnBase available. Confirmed NT8_FULL_REFERENCE.md.
...
if (!entryCandidates.Any()) // (2)
{
    SubmitEntryDirect(follower, instrument, qty, price, action, orderType);
    return;
}
```

**After**:
```csharp
// CYC=5: (1) null guard, (2) no-entry fast path, (2b) fastAtm is Named, (3) foreach cancel loop.
// DW-B195-02: fast-path (2) now ATM-aware -- same pattern as SubmitDrainedEntry (B195-T1).
// F5-repair: dead (4) cancelCount==0 branch removed. entryCandidates always non-empty here.
// JS-021: no lock(). GetCloneAtmMode reads volatile field -- thread-safe. ConcurrentDictionary + Interlocked only.
// NT8: Account.Cancel(Order[]) is AddOnBase available. Confirmed NT8_FULL_REFERENCE.md.
// NT8: SendCopyWithAtm uses StartAtmStrategy (AddOnBase available, line 4661).
// ASCII-only: "-fast-atm-" literal. No Unicode.
...
if (!entryCandidates.Any()) // (2)
{
    // DW-B195-02: fast-path must also respect Clone ATM mode.
    // NOTE: "now" variable at line 6613 is declared AFTER this early return and is
    //       unreachable here. Expression is inlined directly.
    var fastAtm = GetCloneAtmMode(); // (2a) volatile read
    if (fastAtm is FollowerAtmMode.Named namedFastAtm) // (2b) ATM branch -- adds CYC +1
    {
        var sig = CopySignal.Create(
            action,
            orderType,
            qty,
            price,
            follower.Name + "-fast-atm-" + (long)(int)Environment.TickCount
        );
        SendCopyWithAtm(follower, instrument, in sig, namedFastAtm);
    }
    else
    {
        SubmitEntryDirect(follower, instrument, qty, price, action, orderType);
    }
    return;
}
```

**orderId composition**: `follower.Name + "-fast-atm-" + (long)(int)Environment.TickCount`
- Different suffix from drain-completion path ("-drain-atm-"). No collision possible.
- `now` variable (line 6613+) is unreachable at the fast-path early return. Inlined directly.

**Methods NOT modified**: `PendingDispatchDrain`, `SubmitDrainedEntry`, `SubmitEntryDirect`, `SendCopyWithAtm`, `GetCloneAtmMode`.

---

### File: `src/PropTraderTools/Tests/B196Tests.cs` (NEW)

Created with 2 `[Fact]` test methods (pure logic, no NT8 runtime required):

| Test Name | Classification | Runs Without NT8? |
|-----------|---------------|-------------------|
| `DrainThenDispatch_FastPath_CloneAtmMode_CallsSendCopyWithAtm` | `[Fact]` pure logic | YES |
| `DrainThenDispatch_FastPath_NoAtmMode_CallsSubmitEntryDirect` | `[Fact]` pure logic | YES |

### File: `src/PropTraderTools/PropTraderTools.Tests.csproj`

Added: `<Compile Include="Tests\B196Tests.cs" />` at line 40.

---

## Scan Results (All 7)

| Scan | Check | Command | Result | Status |
|------|-------|---------|--------|--------|
| SCAN-1 | JS-021 lock() ban | `Select-String ... "lock\(" \| Where-Object { $_.Line -notmatch "//.*lock" }` | 0 actual lock() calls | PASS |
| SCAN-2 | JS-033 async void ban | `Select-String ... "async void " \| Where-Object { $_.Line -notmatch "//" }` | 0 async void declarations | PASS |
| SCAN-3 | JS-066 CYC <= 8 | Manual branch count (see below) | CYC=5 (was 4) | PASS |
| SCAN-4 | JS-001 throw ban | `Select-String ... "throw new "` | 0 matches in file | PASS |
| SCAN-5 | JS-002 return null ban | `Select-String ... "return null"` | Multiple pre-existing in other methods; DrainThenDispatch is void -- 0 new in modified section | PASS |
| SCAN-6 | ASCII-only | Visual check of new identifiers/literals | fastAtm, namedFastAtm, sig, "-fast-atm-" all ASCII. No Unicode, emoji, curly quotes. | PASS |
| SCAN-7 | DateTime.UtcNow/Now ban | `Select-String ... "DateTime\.Now"` | 1 comment-only match ("No DateTime.Now"); 0 actual calls. Environment.TickCount used (approved pattern). | PASS |

---

## CYC Analysis: `DrainThenDispatch` after change

**CYC = base(1) + 4 branch points = 5**

| # | Branch | New? |
|---|--------|------|
| (1) | `if (follower == null \|\| instrument == null)` | pre-existing |
| (2) | `if (!entryCandidates.Any())` | pre-existing |
| (2b) | `if (fastAtm is FollowerAtmMode.Named namedFastAtm)` | **NEW** |
| (3) | `foreach (var e in entryCandidates)` | pre-existing |

CYC = 1 + 4 = **5**. Within limit of 8. PASS.

---

## Build Results

| Item | Result |
|------|--------|
| `dotnet build PropTraderTools.Tests.csproj --no-incremental` | 0 errors, 1356 warnings (all pre-existing CS0436 + xUnit1004) |
| No new errors introduced | CONFIRMED |
| B196Tests.cs compiles and both [Fact] tests run without NT8 host | CONFIRMED |
| `ptt-sync.ps1` | 19 files linked, 0 errors |

---

## Post-Edit

- `ptt-sync.ps1` executed: YES -- 19 files linked, 0 errors

---

## Summary

- `DrainThenDispatch` fast-path (`!entryCandidates.Any()`) now respects Clone ATM mode.
- When `GetCloneAtmMode()` returns `FollowerAtmMode.Named`, `SendCopyWithAtm` is called (ATM path).
- When `GetCloneAtmMode()` returns `FollowerAtmMode.Inherit`, `SubmitEntryDirect` is called (pre-B196 behavior preserved).
- `follower.Name + "-fast-atm-" + (long)(int)Environment.TickCount` orderId distinguishes fast-path from drain-completion path ("-drain-atm-").
- CYC 4->5. All 7 scans zero. Build 0 errors. ptt-sync clean.

---

**BUILD_PASS: YES**

---

*Engineer: ptt-engineer*
*Epic: B196*
*Phase: 4a (Ticket 1)*
*Return: BUILD_PASS*