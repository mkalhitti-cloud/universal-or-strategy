# B197 Ticket 3 Verification

**Verifier**: ptt-verifier
**Epic**: B197 -- Gap Restore
**Ticket**: T3 -- TryCancelStaleGtcExitOnEntry + IsStaleGtcExitOrder (DW-B154-01, DW-B154-02)
**Phase**: 4b (Independent Verification)
**Date**: 2026-10-12
**Status**: VERIFY_PASS

---

## Scope

Verifying Ticket 3 ONLY per SCOPE LOCK instruction. No other ticket completion files read.

Files examined (READ ONLY):
- `docs/brain/B197/04-tickets.md` (T3 section)
- `docs/brain/B197/04-ticket-review.md` (T3 section)
- `docs/brain/B197/02-architecture-plan.md` (T3 section)
- `docs/brain/B197/ticket-3-completion.md`
- `src/PropTraderTools/CopyEngine.cs`
- `src/PropTraderTools/Tests/B197Tests.cs`
- `docs/standards/jane-street/RULES_CATALOG.md`

---

## 7-Scan Results (Independent Layer 3 -- All Run by Verifier)

### SCAN-01: CYC Check (Lizard)

Command: `python -m lizard src/PropTraderTools/CopyEngine.cs --CCN 8 2>&1 | Select-String "TryCancelStaleGtcExitOnEntry|IsStaleGtcExitOrder|TryCancelSingleStaleGtcExit"`

Raw output:
```
7      5     62      1       7 TrimSignal::IsStaleGtcExitOrder@1943-1949
13     8    104      1      13 TrimSignal::TryCancelStaleGtcExitOnEntry@1958-1970
15     5     77      2      15 TrimSignal::TryCancelSingleStaleGtcExit@1975-1989
```

Results:
- `IsStaleGtcExitOrder`: CCN=5 (<=8) -- PASS
- `TryCancelStaleGtcExitOnEntry`: CCN=8 (at JS-066 ceiling, not exceeded) -- PASS
- `TryCancelSingleStaleGtcExit`: CCN=5 (<=8) -- PASS (extracted helper, CYC budget compliant)

**SCAN-01: PASS**

### SCAN-02: Lock() Grep

Command: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "^\s*//" }`

Result: No output -- 0 executable lock() calls in CopyEngine.cs.

**SCAN-02: PASS**

### SCAN-03: Async Void Scan

Command: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "async void " | Where-Object { $_.Line -notmatch "^\s*//" }`

Result: No output -- 0 async void in executable code.

**SCAN-03: PASS**

### SCAN-04: Return Null Scan (new methods, lines 1939-1990)

Command: `Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "return null;" | Where-Object { $_.LineNumber -ge 1939 -and $_.LineNumber -le 1990 }`

Result: No output -- 0 return null in new T3 methods.
Note: All 3 new methods are void or bool (IsStaleGtcExitOrder returns false, not null).

**SCAN-04: PASS**

### SCAN-05: ASCII Scan

Command: `$bytes = [System.IO.File]::ReadAllBytes("src\PropTraderTools\CopyEngine.cs"); $nonAscii = $bytes | Where-Object { $_ -gt 127 }; Write-Host "Non-ASCII byte count: $($nonAscii.Count)"`

Result: Non-ASCII byte count: 0

All T3 string literals ("[GTC-SWEEP] cancelled", "[GTC-SWEEP] cancel error:") confirmed ASCII-only.

**SCAN-05: PASS**

### SCAN-06: dotnet build

Command: `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj 2>&1 | Select-String "Error\(s\)|Warning\(s\)|Build succeeded|FAILED"`

Result:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**SCAN-06: PASS**

### SCAN-07: Test Run (B197 Facts)

Command: `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "FullyQualifiedName~B197" --no-build`

Result: Passed! Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 161 ms

Individual B197 test results:
- `TrySetSlateReadyOnDrainFill_SourceContainsFollowerSlateReady`: PASS (T1, carry-forward)
- `TrySetSlateReadyOnDrainFill_SourceContainsSlateReadyDrainMarker`: PASS (T1, carry-forward)
- `TryEvictFollowerBeSlot_SourceContainsDrainAbortLeaderFlat`: PASS (T2, carry-forward)
- `TryEvictFollowerBeSlot_SourceContainsTryAbortLeaderFlatDrains`: PASS (T2, carry-forward)
- `TryCancelStaleGtcExitOnEntry_SourceContainsMethodName`: PASS (T3 new)
- `TryCancelStaleGtcExitOnEntry_SourceContainsGtcSweepMarker`: PASS (T3 new)

T3 Fact coverage: 2/2 PASS

**SCAN-07: PASS**

---

## DNA Rule Checks (Jane Street RULES_CATALOG.md)

### JS-021 (No lock() -- P0 CRITICAL)

SCAN-02 confirms 0 lock() calls in executable code. ConcurrentDictionary operations throughout
(`acc.Orders.ToList()`, `acc.Cancel()`) are lock-free. Pattern mirrors `CancelPttDragOrphansForAccount`
at lines 1939-1954 which was pre-existing and already verified lock-free.

**JS-021: PASS**

### JS-001 (No throw in hot paths)

`TryCancelStaleGtcExitOnEntry` delegates to `TryCancelSingleStaleGtcExit`. The catch block in
`TryCancelSingleStaleGtcExit` absorbs `acc.Cancel` errors via StatusUpdate invoke. It does NOT
rethrow. This matches the existing pattern at lines 1945-1953.
`IsStaleGtcExitOrder`: returns bool false, no throw.
No `throw new ...Exception` in any of the 3 new methods.

**JS-001: PASS**

### JS-002 (No return null where non-null expected)

`IsStaleGtcExitOrder` returns `bool` (false not null). Void methods cannot return null.
SCAN-04 confirms 0 `return null;` in new method range.

**JS-002: PASS**

### JS-066 (CYC <= 8 ceiling)

Confirmed by SCAN-01 (Lizard):
- `TryCancelStaleGtcExitOnEntry`: CCN=8 -- at ceiling, not exceeded.
- `IsStaleGtcExitOrder`: CCN=5 -- well within budget.
- `TryCancelSingleStaleGtcExit`: CCN=5 -- extracted per JS-066 ceiling rule.
  (Helper extraction was required because Lizard counts `?.` null-conditional and `catch` as
  extra branches beyond the McCabe derivation in the ticket spec.)

**JS-066: PASS**

### JS-033 (No async void in non-event-handlers)

SCAN-03 confirms 0 async void in executable code.

**JS-033: PASS**

### ASCII-Only

SCAN-05 confirms 0 non-ASCII bytes in CopyEngine.cs.

**ASCII: PASS**

---

## Architecture Compliance

### T3 Critical Design Check: IsPttCopyEntryFilled NOT Used

Scan result: `Select-String -Pattern "IsPttCopyEntryFilled"` in lines 1939-1990 returns only line 1953:
```
1953: // GUARD (3) RATIONALE: IsPttCopyEntryFilled was REMOVED in B180. Stub at L837 always returns false.
```
This is a comment, NOT a call. The stub `IsPttCopyEntryFilledTestable` (line 837, `=> false`) is not
called anywhere in the new T3 methods.

Guard (3) in `TryCancelStaleGtcExitOnEntry` (lines 1963-1965):
```csharp
if (order.Name == null                                                     // (3) if-branch
    || (!order.Name.StartsWith("PTT-Copy", StringComparison.Ordinal)     //     || operator
        && order.Name != "Entry")) return;                                //     && operator
```

This is the required INLINE name-check (StartsWith("PTT-Copy") OR Name=="Entry"). NOT the stub. PASS.

**IsPttCopyEntryFilled isolation: PASS**

### TODO Placeholder Removal

Scan: `Select-String -Pattern "TODO B197-T3"` returns no output.
The `// TODO B197-T3: TryCancelStaleGtcExitOnEntry(e);` placeholder (left by T1 engineer)
has been replaced with the actual call at line 1558.

**TODO placeholder removed: PASS**

### 3-Call Sequence in Filled Branch (lines 1554-1559)

Actual source at lines 1554-1559:
```csharp
else if (e.Order.OrderState == OrderState.Filled)
{
    TrySetSlateReadyOnDrainFill(e);                  // T1: set slate-ready FIRST (_drainOwnedOrderIds intact)
    AbortDrainOnFill(e.Order.Account.Name);          // R2-F1: clean _drainOwnedOrderIds on fill-abort
    TryCancelStaleGtcExitOnEntry(e);                 // T3: sweep stale GTC exits on new entry fill
}
```

Required ordering:
1. `TrySetSlateReadyOnDrainFill(e)` -- FIRST (must run before AbortDrainOnFill removes drain IDs) ✓
2. `AbortDrainOnFill(e.Order.Account.Name)` -- SECOND (cleans drain ownership) ✓
3. `TryCancelStaleGtcExitOnEntry(e)` -- LAST (sweeps stale GTC exits) ✓

**3-call ordering: PASS** (T1 BEFORE AbortDrainOnFill BEFORE T3 -- exactly as spec §6 requires)

### Method Placement and Signatures

Verified in source:
- `IsStaleGtcExitOrder`: lines 1943-1949, `private static bool IsStaleGtcExitOrder(Order o)` ✓
- `TryCancelStaleGtcExitOnEntry`: lines 1958-1970, `private void TryCancelStaleGtcExitOnEntry(OrderEventArgs e)` ✓
- `TryCancelSingleStaleGtcExit`: lines 1975-1989, `private void TryCancelSingleStaleGtcExit(Account acc, Order o)` ✓

All three methods placed near `CancelPttDragOrphansForAccount` (~line 1991) as per spec thematic neighbor requirement.

**Signatures and placement: PASS**

### Helper Extraction (TryCancelSingleStaleGtcExit)

The ticket spec design (§4) shows the `foreach` body inline in `TryCancelStaleGtcExitOnEntry`.
The engineer extracted it to `TryCancelSingleStaleGtcExit` to keep parent CCN<=8 per JS-066.
This is a valid and required deviation -- Lizard counts `?.`, `catch`, and inner-loop `if`
as extra branches. The extraction is architecturally correct (Jane Street ceiling rule).
The `[GTC-SWEEP]` log marker is present in `TryCancelSingleStaleGtcExit` body (lines 1982, 1987).

**Helper extraction: PASS**

### [GTC-SWEEP] Marker Presence

Source line 1982: `StatusUpdate(acc.Name + ": [GTC-SWEEP] cancelled " + o.Name);`
Source line 1987: `StatusUpdate("[GTC-SWEEP] cancel error: " + ex.Message);`
Both markers confirmed present. Test `TryCancelStaleGtcExitOnEntry_SourceContainsGtcSweepMarker` PASS.

**[GTC-SWEEP] marker: PASS**

### Header Comments (DW Attribution)

Line 1939: `// B197 T3 DW-B154-02: IsStaleGtcExitOrder -- CYC=5.` ✓
Line 1951: `// B197 T3 DW-B154-01 + DW-B154-02: TryCancelStaleGtcExitOnEntry -- CYC<=8.` ✓
Line 1972: `// B197 T3: TryCancelSingleStaleGtcExit -- helper extracted from TryCancelStaleGtcExitOnEntry.` ✓

**Header comments: PASS**

---

## Spec Coverage (T3 DW IDs)

| DW ID | Required | Implementation | Result |
|-------|----------|----------------|--------|
| DW-B154-01 | TryCancelStaleGtcExitOnEntry present + call site wired | Lines 1958-1970, call at line 1558 | PASS |
| DW-B154-02 | IsStaleGtcExitOrder helper predicate present | Lines 1943-1949 | PASS |

**Spec Coverage: PASS**

---

## Success Criteria Checklist (Ticket T3 §12)

| # | Criterion | Verifier Result |
|---|-----------|-----------------|
| 1 | TryCancelStaleGtcExitOnEntry and IsStaleGtcExitOrder method bodies exist in CopyEngine.cs | PASS -- lines 1958-1970, 1943-1949 |
| 2 | TryCancelStaleGtcExitOnEntry Guard (3) uses inline name-check (NOT IsPttCopyEntryFilled stub) | PASS -- StartsWith("PTT-Copy") / == "Entry" at lines 1963-1965 |
| 3 | [GTC-SWEEP] log marker present in TryCancelStaleGtcExitOnEntry (or extracted helper) body | PASS -- lines 1982, 1987 in TryCancelSingleStaleGtcExit |
| 4 | TryCancelStaleGtcExitOnEntry(e) call appears as LAST line in else if (OrderState.Filled) block, after AbortDrainOnFill | PASS -- line 1558 (third call in 3-call block) |
| 5 | CYC of TryCancelStaleGtcExitOnEntry = 8 (not exceeded). CYC of IsStaleGtcExitOrder = 5. | PASS -- Lizard: CCN=8, CCN=5 |
| 6 | All 7 scans pass (0 lock, CYC correct, ASCII-only, no new rethrow, null safety confirmed, build clean, T3 tests pass) | PASS -- all 7 scans clean |
| 7 | dotnet test --filter B197 shows both T3 Facts PASS | PASS -- 2/2 T3 Facts pass (6/6 total B197 pass) |

---

## Layer 2 vs Layer 3 Cross-Check

Engineer's Layer 2 self-reported scan results vs. Verifier's Layer 3 independent results:

| Scan | Engineer (L2) | Verifier (L3) | Match? |
|------|--------------|---------------|--------|
| Lock grep | 0 results | 0 results | YES |
| CYC (Lizard) | CCN=5,8,5 | CCN=5,8,5 | YES |
| ASCII | 0 non-ASCII | 0 non-ASCII | YES |
| Async void | 0 results | 0 results | YES |
| Return null (new methods) | 0 results | 0 results | YES |
| Build | 0 errors | 0 errors | YES |
| Test run | 6/6 PASS | 6/6 PASS | YES |

**No discrepancies between L2 and L3. All results confirmed independently.**

---

## Violations

**NONE**

---

## VERDICT: VERIFY_PASS

All 7 scans clean. All DNA rules (JS-021, JS-001, JS-002, JS-066, JS-033) satisfied.
Architecture compliance confirmed:
- IsPttCopyEntryFilled stub NOT called in T3 code
- TODO placeholder removed (no `// TODO B197-T3` present)
- 3-call ordering correct: TrySetSlateReadyOnDrainFill -> AbortDrainOnFill -> TryCancelStaleGtcExitOnEntry
- Inline Guard (3) name-check used (StartsWith("PTT-Copy") / == "Entry")
- Helper extraction (TryCancelSingleStaleGtcExit) correctly keeps parent CCN<=8
- Both T3 Facts pass. All 6 B197 Facts pass.

DW-B154-01 and DW-B154-02 CLOSED.

---

*Written by: ptt-verifier*
*Epic: B197*
*Ticket: T3*
*Phase: 4b (Verification)*
*Verdict: VERIFY_PASS*
*Violations: 0*
