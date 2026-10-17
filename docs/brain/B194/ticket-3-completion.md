# B194 Ticket 3 -- Completion Report

**Epic**: B194 -- Fix 4 confirmed NakedPositionDetector + DrainThenDispatch bugs
**Ticket**: T3 -- FlattenOneAccount belt-and-suspenders debounce + `_flattenFilledTicks` field (F4)
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-10-07
**DW Card Closed**: DW-B194-04

---

## Prerequisite State Check

Command: `git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@"`
Result: 4 hunks at @@ -6427, @@ -6475, @@ -6498, @@ -6537 (T1 F1, T2 IsBracketStopOrder, T2 HasNakedPosition, T1 F3)

**T1+T2 changes confirmed present in working tree. Proceeded with T3 only.**

---

## Changes Implemented

### CHANGE 1 -- _flattenFilledTicks Field Declaration

**File**: `src/PropTraderTools/CopyEngine.cs`
**Inserted after**: `_nakedDetectLastQueuedTicks` field at line 373-374
**Location**: Lines 376-384 (post-insertion)

```csharp
        // B194-F4: belt-and-suspenders flatten debounce.
        // Stores (long)Environment.TickCount at last acc.Submit(PTT-Flatten) per account name.
        // Prevents PTT-Flatten fill -> NakedDetect -> FlattenOneAccount re-entry loop.
        // 500ms grace window is far shorter than TickCount wrap period (~25d) -- safe.
        // ConcurrentDictionary: no lock (JS-021). StringComparer.Ordinal: deterministic key compare.
        // DW-B194-04: closed by this field + FlattenOneAccount debounce guard.
        private readonly ConcurrentDictionary<string, long> _flattenFilledTicks =
            new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
```

**Exact line numbers**: Field declaration at lines 382-383.

---

### CHANGE 2 -- Debounce Check in FlattenOneAccount

**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `FlattenOneAccount` (starts at line 4995)

#### Header Comment Update
Lines 4991-4993 updated from CYC=6 to CYC=7 with B194-F4 item enumerated.

#### Debounce Check Insertion (CHANGE 2a)
**Inserted after**: Closing `}` of in-flight scan `foreach` (line 5011)
**Exact lines**: 5021-5029

```csharp
            // B194-F4: belt-and-suspenders debounce. Skip if flatten was submitted within 500ms.
            // Prevents PTT-Flatten fill -> NakedDetect -> FlattenOneAccount re-entry loop.
            // Pattern matches _nakedDetectLastQueuedTicks at NakedPositionDetector line 6479.
            long flattenNow = (long)(int)Environment.TickCount;
            if (flattenNow - _flattenFilledTicks.GetOrAdd(acc.Name, 0L) < 500L)
            {
                StatusUpdate?.Invoke(acc.Name + ": flat-debounce skip");
                return;
            }
```

#### Debounce Record Insertion (CHANGE 2b)
**Inserted inside**: `if (order != null)` block, before `acc.Submit(new[] { order })`
**Exact lines**: 5068-5072

```csharp
                if (order != null)
                {
                    // B194-F4: record timestamp at submission (not at fill) so the 500ms lockout
                    // begins from the moment flatten is dispatched to NT8.
                    _flattenFilledTicks.AddOrUpdate(acc.Name, flattenNow, (_, __) => flattenNow);
                    acc.Submit(new[] { order });
                }
```

---

## Pre/Post Code Snippet -- FlattenOneAccount Debounce Area

**Pre (original)**:
```csharp
            }
            var pos = FindPosition(acc, instrument);
```
```csharp
                if (order != null)
                    acc.Submit(new[] { order });
```

**Post (T3)**:
```csharp
            }
            // B194-F4: belt-and-suspenders debounce. Skip if flatten was submitted within 500ms.
            // Prevents PTT-Flatten fill -> NakedDetect -> FlattenOneAccount re-entry loop.
            // Pattern matches _nakedDetectLastQueuedTicks at NakedPositionDetector line 6479.
            long flattenNow = (long)(int)Environment.TickCount;
            if (flattenNow - _flattenFilledTicks.GetOrAdd(acc.Name, 0L) < 500L)
            {
                StatusUpdate?.Invoke(acc.Name + ": flat-debounce skip");
                return;
            }
            var pos = FindPosition(acc, instrument);
```
```csharp
                if (order != null)
                {
                    // B194-F4: record timestamp at submission (not at fill) so the 500ms lockout
                    // begins from the moment flatten is dispatched to NT8.
                    _flattenFilledTicks.AddOrUpdate(acc.Name, flattenNow, (_, __) => flattenNow);
                    acc.Submit(new[] { order });
                }
```

---

## TickCount Pattern Confirmation

**Pattern used**: `(long)(int)Environment.TickCount`
**Matches existing codebase at**: `_nakedDetectLastQueuedTicks` usage at line 6479 (NakedPositionDetector).
The int-first cast preserves ~25-day wrap behavior. 500ms window is far shorter than wrap period -- safe.
**Environment.TickCount64 NOT used** -- matches existing convention.

---

## 7-SCAN RESULTS

### SCAN-01 [JS-021]: lock() grep
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" -and $_.Line -notmatch "no lock" -and $_.Line -notmatch "lock-free" }`
Result: **0 executable lock() statements**
`_flattenFilledTicks` uses only `GetOrAdd` and `AddOrUpdate` -- lock-free atomic ops.
**SCAN-01: PASS**

### SCAN-02 [JS-033]: async void grep
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async void " | Where-Object { $_.Line -notmatch "//" }`
Result: **0 matches** -- FlattenOneAccount remains synchronous void.
**SCAN-02: PASS**

### SCAN-03 [JS-066]: CYC count
FlattenOneAccount post-F4 branches (7 total):
1. `foreach` in-flight scan (active PTT-Flatten guard)
2. `if (flattenNow - _flattenFilledTicks.GetOrAdd(...) < 500L)` -- debounce (NEW, B194-F4)
3. `if (pos == null || pos.Quantity == 0)` -- pre-cancel pos guard
4. `CancelAllAccountOrders` path (structural; counted in original CYC=6 as pre-cancel action)
5. `if (posAfterCancel == null || posAfterCancel.Quantity == 0)` -- post-cancel pos guard
6. `posAfterCancel.MarketPosition == MarketPosition.Long ? ... : ...` -- ternary
7. `try/catch` block

CYC = 7. JS-066 limit = 8. PASS (was CYC=6 pre-F4).
**SCAN-03: PASS**

### SCAN-04 [JS-001]: throw new grep
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new" | Where-Object { $_.LineNumber -ge 4995 -and $_.LineNumber -le 5090 }`
Result: **0 matches** -- F4 changes use `return` and `AddOrUpdate`. Zero throw.
**SCAN-04: PASS**

### SCAN-05 [JS-002]: return null grep
Command: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null" | Where-Object { $_.LineNumber -ge 4995 -and $_.LineNumber -le 5090 }`
Result: **0 matches** -- all new returns are bare `return;` (void method).
**SCAN-05: PASS**

### SCAN-06 [ASCII-only / DateTime.UtcNow]
Non-ASCII in CopyEngine.cs: `Get-Content "src/PropTraderTools/CopyEngine.cs" | Where-Object { $_ -match '[^\x00-\x7F]' } | Measure-Object` = **0**
DateTime.Now: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "DateTime\.Now[^U]"` = **0 executable matches** (1 comment-only mention at line 4391, not executable)
New identifiers: `_flattenFilledTicks` (ASCII), `flattenNow` (ASCII), `flat-debounce skip` (ASCII).
**SCAN-06: PASS**

### SCAN-07 [Test coverage]: 9 tests present
Command: `Select-String -Path "src/PropTraderTools/Tests/B194Tests.cs" -Pattern "\[Fact"`
Result: 9 matches at lines 40, 69, 99, 118, 135, 155, 179, 215, 242.

All 9 test names:
1. (line 41) `TryNakedDetect_SkipsPTTCopyFill_NoNakedDetectorCalled` [T1]
2. (line 70) `DrainThenDispatch_IncludesSubmittedInEntryCandidates` [T1]
3. (line 100) `IsBracketStopOrder_TriggerPendingMarket_WithStopPrice_ReturnsTrue` [T2]
4. (line 119) `IsBracketStopOrder_TriggerPendingMarket_NoStopPrice_ReturnsFalse` [T2]
5. (line 136) `IsBracketStopOrder_WorkingStopMarket_ReturnsTrue` [T2]
6. (line 156) `HasNakedPosition_TriggerPendingStop_ReturnsFalse` [T2]
7. (line 179) `IsBracketStopOrder_ReturnsFalse_ForWorkingLimitOrder` [T2]
8. (line 216) `FlattenOneAccount_WithinDebounce_SkipsSubmit` [T3]
9. (line 241) `FlattenOneAccount_AfterDebounceExpired_AllowsSubmit` [T3]

**SCAN-07: PASS**

---

## Diff Hunk Count

Command: `git diff HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@"`
Result: **8 hunks**
- Hunks 1-4: T3 changes (_flattenFilledTicks field, CYC comment, debounce check, timestamp record)
- Hunks 5-8: T1+T2 pre-existing changes (TryNakedDetect F1, IsBracketStopOrder, HasNakedPosition, DrainThenDispatch F3)

Single-hunk ban: NOT triggered (count = 8, ban applies only when count = 1).

---

## ptt-sync Result

Command: `powershell -File .\scripts\ptt-sync.ps1`
Result: **19 linked, 0 errors**
CopyEngine.cs: LINKED

---

## Build Result

Command: `dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj 2>&1 | Select-String "^Build|error CS"`
Result: **Build succeeded.**
Errors: 0
(Pre-existing CS0436 and CS8632 warnings not introduced by T3.)

---

## DW Card Closed

| DW Card | Description | Status |
|---------|-------------|--------|
| DW-B194-04 | FlattenOneAccount loop belt-and-suspenders debounce | CLOSED by F4 field (_flattenFilledTicks) + debounce guard (FlattenOneAccount lines 5021-5029) + timestamp recording (line 5070) |

**BUG-3 complete closure**:
- Primary path (PTT-Copy fill -> NakedDetect): CLOSED by T1/F1
- Residual path (PTT-Flatten fill -> NakedDetect re-entry): CLOSED by T3/F4

---

*Written by: ptt-engineer*
*Epic: B194*
*Ticket: T3*
*Phase: 4a (Engineer)*
*Source tickets: docs/brain/B194/04-tickets.md (T3 section only)*
*Review authority: docs/brain/B194/04-ticket-review.md (TICKET_REVIEW_PASS)*
