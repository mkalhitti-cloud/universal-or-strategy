# PTT-COPIER-B24 — Ticket 1 Completion Report
**Phase**: 4a (Engineer)
**Engineer**: ptt-engineer
**Date**: 2026-07-07
**Defect Closed**: DW-B23-BE-ALLACCOUNTS-01
**Prerequisite**: TICKET_REVIEW_PASS confirmed (second-pass, 04-ticket-review.md)

---

## Changes Made

### Change A — New `BreakEven(Account, Instrument, int)` Overload
**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
**Inserted at**: Line 1182 (immediately after existing `BreakEven(Instrument, int)` which ends at line 1180)
**Lines inserted**: 18 (comment block + method body)
**New method at post-insert line**: 1183

```csharp
// B24 T1 -- BreakEven(Account,Instrument,int): fires leader directly, no rule needed.
// CYC=4: null guard(1), MoveStop leader(no branch), foreach acc(2), acc==leader skip(3).
// JS-021: no lock. JS-002: null leader fires StatusUpdate + early return.
internal void BreakEven(Account leader, Instrument instrument, int bufferTicks)
{
    if (leader == null)                                      // (1) null guard
    {
        StatusUpdate?.Invoke("PTT-BE: leader null -- BE skipped");
        return;
    }
    MoveStopToBreakEven(leader, instrument, bufferTicks);   // leader direct, no rule needed
    foreach (var acc in AllAccounts(instrument))            // (2) follower fan-out
    {
        if (acc == leader) continue;                        // (3) skip duplicate
        MoveStopToBreakEven(acc, instrument, bufferTicks);
    }
}
```

### Change B — `OnPendingBeAccountUpdate` Single-Line Fix
**File**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
**Line (post-Change-A shift)**: 1415
**Original**: `BreakEven(instr, buf);`
**Changed to**: `BreakEven(acc, instr, buf);`

`acc` is the local variable captured at line 1408: `var acc = _pendingBeAccount;`
No other lines in `OnPendingBeAccountUpdate` were touched.

---

## Unchanged-Code Contract Verification

| Symbol | File | Status |
|--------|------|--------|
| `BreakEven(Instrument, int)` at line 1176 | CopyEngine.cs | UNCHANGED — confirmed by SCAN-04 |
| `MoveStopToBreakEven(Account, Instrument, int)` | CopyEngine.cs | UNCHANGED — not touched |
| `AllAccounts(Instrument)` | CopyEngine.cs | UNCHANGED — not touched |
| All lines in `OnPendingBeAccountUpdate` except the call site | CopyEngine.cs | UNCHANGED |
| `TradeCopierPanel.cs` | PropTraderTools | NOT TOUCHED (T2 scope) |
| `CopyEngineTests.cs` | PropTraderTools | NOT TOUCHED (T2 scope) |

---

## Seven-Scan Results

### SCAN-01 — JS-021: No `lock()` in write-set
```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "lock\s*\(" | Select-Object LineNumber, Line
```
**Result**: All 5 matches are comments (`-- no lock (JS-021)`, `try block(0)`) — zero actual `lock(` call expressions.
**Pass criterion met**: ✅ Zero `lock(` usages in new or modified code.

### SCAN-02 — JS-002: Null leader path emits StatusUpdate string
```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "PTT-BE: leader null -- BE skipped"
```
**Result**: Exactly 1 match at line 1189 — inside new `BreakEven(Account, Instrument, int)` overload.
**Pass criterion met**: ✅ Exactly 1 match.

### SCAN-03 — CYC ≤ 8: New overload complexity (manual count)
**Method**: `BreakEven(Account leader, Instrument instrument, int bufferTicks)`
**CYC breakdown**:
- Base: 1
- Branch 1: `if (leader == null)` early return = +1
- Branch 2: `foreach (var acc in AllAccounts(instrument))` loop = +1
- Branch 3: `if (acc == leader) continue` skip = +1
- **CYC = 4**
**Pass criterion met**: ✅ CYC = 4 ≤ 8.

### SCAN-04 — Overload coexistence: 2-param overload unchanged
```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "internal void BreakEven\(Instrument"
```
**Result**: Exactly 1 match at line 1176. Body unchanged.
**Pass criterion met**: ✅ Exactly 1 match.

### SCAN-05 — No stale 2-param call from CopyEngine internal site
```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "BreakEven\(instr, buf\)" | Measure-Object | Select-Object Count
```
**Result**: Count = 0. Line 1415 now reads `BreakEven(acc, instr, buf)`.
**Pass criterion met**: ✅ Zero matches.

### SCAN-06 — `[Fact]` count baseline
```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object | Select-Object Count
```
**Result**: Count = 126.
**Pass criterion met**: ✅ Count = 126 (T1 does not add tests; T2 will raise to 128).

### SCAN-07 — NT8-043: No null-conditional event unsubscription
```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs" -Pattern "\?\.\w+\s*-=" | Measure-Object | Select-Object Count
```
**Result**: Count = 0. Existing `acc.AccountItemUpdate -= OnPendingBeAccountUpdate` at line 1412 is guarded by `if (acc != null)` — compliant.
**Pass criterion met**: ✅ Zero matches.

---

## JS Rule Notes

| Rule | Severity | Status |
|------|----------|--------|
| JS-021 (no `lock()`) | P0 | PASS — new overload is lock-free; ConcurrentBag iteration used inside AllAccounts |
| JS-002 (no fall-through on null) | P0 | PASS — Branch 1 fires StatusUpdate then returns; no fall-through to MoveStopToBreakEven |
| JS-001 (no throw in hot path) | P0 | PASS — no throw statement in new overload |
| JS-033 (not async void) | P0 | PASS — declared `internal void`, not `async void` |
| CYC ≤ 8 | P0 | PASS — CYC = 4 |

## NT8 Compiler Notes

| Rule | Status |
|------|--------|
| NT8-043 (no `?.Event -= handler`) | PASS — zero null-conditional unsubscriptions in new/modified code |
| No `Account.All` outside Loaded handler | PASS — `AllAccounts(Instrument)` wrapper used |
| No `volatile` on reference types | PASS — `Account leader` is a local parameter, not a field |
| No `async/await` in new method | PASS |

---

## Dependency Note for T2

T1 is complete. The `BreakEven(Account, Instrument, int)` overload now exists in `CopyEngine.cs`.
T2 (ptt-engineer, Ticket 2) may now proceed to:
- Update 5 `TradeCopierPanel.cs` call sites
- Append 2 `[Fact]` tests to `CopyEngineTests.cs`

---

## Result: BUILD_PASS

All 7 scans green. Both changes applied correctly. Unchanged-code contract verified.
No JS P0 violations. No NT8 compiler rule violations. CYC = 4.

*ptt-engineer · PTT-COPIER-B24 · Ticket 1 · 2026-07-07*
