# B198 Ticket 3 Verification Report
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-10-13
**Epic**: B198 -- fo=NULL drain path fix (DW-B195-01)
**Ticket**: Ticket 3 -- fo=NULL Drain Path Fix
**Source**: docs/brain/B198/04-tickets.md §Ticket 3
**Engineer completion report**: docs/brain/B198/ticket-3-completion.md
**Architecture plan**: docs/brain/B198/02-architecture-plan.md §5
**Plan review**: docs/brain/B198/02-plan-review.md (VIO-1 RESOLVED in Cycle 2)

---

## VERDICT: VERIFY_FAIL

**Violation count: 1 (CRITICAL)**

**Primary violation**: T3 Change T3-4 is INCOMPLETE. `TrySetSlateReadyOnDrainFill` retrieves `drainFo`
from `_drainFollowerEntryOrders` (lines 1095-1096) but NEVER passes it to `SyncFollowerBracket`.
The VIO-1 fix required by plan-review cycle 2 -- "pass drainFo to SyncFollowerBracket instead of null" --
is NOT present in the implementation. This is the core spec requirement for the fo=NULL fix.

---

## Independent 7-Scan Results

### SCAN-01: JS-021 lock() ban

Command:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }
```

**Result**: 0 matches. **PASS**

Cross-check vs engineer Layer 2: Engineer reported 0. Match. ✓

---

### SCAN-02: JS-033 async void ban

Command:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+" | Where-Object { $_.Line -notmatch "handler|Handler|Event" }
```

**Result**: 0 matches. **PASS**

Cross-check vs engineer Layer 2: Engineer reported 0. Match. ✓

---

### SCAN-03: JS-001/JS-002 return null in hot path

Command:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null;" | Select-Object LineNumber, Line
```

**Result**: 15 matches total. Key T3-scope matches:
- Line 5096: `return null; // (2) T3: return null instead of false` -- IN SendCopyWithAtm
- Line 5116: `return null; // T3: return null on exception` -- IN SendCopyWithAtm catch block

Assessment: Both are in `SendCopyWithAtm`. Per plan-review (§5 VIO-1 resolution) and 02-architecture-plan.md §5:
"null is NT8 absence signal for CreateOrder failure -- established project contract, exempt."
All other `return null;` instances are pre-existing (not T3 changes).

**Result**: No new hot-path violations. **PASS** (SendCopyWithAtm exempt per NT8 absence signal contract)

Cross-check vs engineer Layer 2: Engineer claimed 0 hot-path violations with exempt note. Match. ✓

---

### SCAN-04: JS-066 CYC <= 8

Command:
```powershell
python -m lizard src/PropTraderTools/CopyEngine.cs --csv | Select-String "SendCopyWithAtm|SubmitDrainedEntry|TrySetSlateReadyOnDrainFill|AbortDrainOnFill|TryDrainWatchdog"
```

**Raw lizard results** (NLOC, CCN, ...):
| Method | Lizard CCN | Manual McCabe | Expected | JS-066 |
|--------|-----------|---------------|----------|--------|
| `SendCopyWithAtm` | **11** | ~7 | 4→4 (eng) or 4→5 (T2b) | **WARNING: Lizard CCN=11 > 8** |
| `TrySetSlateReadyOnDrainFill` | 7 | ~6 | 5→5 | PASS (≤8) |
| `SubmitDrainedEntry` | **6** | 6 | 5→6 | PASS (≤8) ✓ |
| `AbortDrainOnFill` | 2 | ~3 | 3→3 | PASS (≤8) ✓ |
| `TryDrainWatchdog` | 3 | ~4 | 4→4 | PASS (≤8) ✓ |

**SendCopyWithAtm Lizard CCN=11 Analysis**:
Lizard counts `?.` null-conditional operators as McCabe branches. `SendCopyWithAtm` has 3x `StatusUpdate?.Invoke(...)` calls (2 from T2a, 1 pre-existing). Each `?.` = +1 in lizard:
- try/catch = +1
- T2b compound if (&&, &&) = +3 (if + 2 &&)
- order null guard = +1
- AtmObject null guard = +1
- 3x `?.Invoke` = +3
- base = 1
= lizard CCN=11

Under project's **manual McCabe convention** (as used in all T1/T2a/T2b verifications; excludes `?.`):
- try/catch + T2b compound if + 2x if-guards = 6
- base = 1
= manual McCabe ~7

**Project convention** (established in T1/T2a/T2b verifications): manual McCabe, does NOT count `?.` as branch.
Under project convention, `SendCopyWithAtm` CYC=7 ≤ 8. **PASS under project convention.**
**HOWEVER**: lizard as the measurement tool reports CCN=11. This is a borderline finding that should be noted
as a WARNING and deferred to a future CYC reduction ticket.

**Engineer self-reported CYC=4** -- this is an undercount. Actual manual McCabe is ~7, not 4.

Cross-check vs engineer Layer 2: Engineer reported max CYC=6 (SubmitDrainedEntry). Discrepancy on `SendCopyWithAtm` (engineer: 4, actual: ~7 McCabe / 11 lizard). Not a blocker under project convention, but engineer's CYC comment is inaccurate.

**Overall SCAN-04 result: PASS under project manual McCabe convention (all ≤8). WARNING: SendCopyWithAtm engineer-reported CYC=4 is incorrect (actual ~7). Lizard CCN=11 exceeds 8 but `.?` not counted by project convention.**

---

### SCAN-05: ASCII-only

Command:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
```

**Result**: 0 matches. **PASS**

Cross-check vs engineer Layer 2: Engineer reported 0. Match. ✓

---

### SCAN-06: xUnit-only test framework

Command:
```powershell
Select-String -Path "src/PropTraderTools/Tests/B198Tests.cs" -Pattern "NUnit|MSTest|\[TestMethod\]|\[Test\]"
```

**Result**: 1 match -- line 9: `// Framework: xUnit only. No NUnit. No MSTest.`
This is a COMMENT only. No actual NUnit/MSTest usage.

**Result**: 0 actual violations. **PASS**

Cross-check vs engineer Layer 2: Engineer reported 0. Match. ✓

---

### SCAN-07: _drainFollowerEntryOrders field type

Command:
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_drainFollowerEntryOrders"
```

**Result**: 6 matches:
- Line 433: `private readonly ConcurrentDictionary<string, Order> _drainFollowerEntryOrders =`
- Line 1095: `_drainFollowerEntryOrders.TryGetValue(acctKey, out Order drainFo);`
- Line 1096: `_drainFollowerEntryOrders.TryRemove(acctKey, out _);`
- Line 7183: `_drainFollowerEntryOrders.TryAdd(acctKey, newOrder);`
- Line 7207: `_drainFollowerEntryOrders.TryRemove(acctKey, out _); // T3: cleanup stored entry order on abort`
- Line 7229: `_drainFollowerEntryOrders.TryRemove(kv.Key, out _); // T3: cleanup stored entry order on watchdog expiry`

Field declaration (line 433): `private readonly ConcurrentDictionary<string, Order> _drainFollowerEntryOrders` -- **CONFIRMED**
No lock() around any access. **PASS**

Cross-check vs engineer Layer 2: Engineer reported confirmed. Match. ✓

---

## Implementation Checks: T3-1 through T3-6

### T3-1: New field `_drainFollowerEntryOrders`

- Field present at line 433: `private readonly ConcurrentDictionary<string, Order> _drainFollowerEntryOrders`
- Initialized with `StringComparer.Ordinal` (line 434)
- No lock() near this field
- **STATUS: CONFIRMED** ✓

### T3-2: `SendCopyWithAtm` return type `bool` → `Order?`

- Method signature at line 5064: `private Order? SendCopyWithAtm(`
- `return order;` at line 5111 (success path)
- `return null;` at lines 5096 and 5116 (failure paths)
- Call site updates:
  - Line 2811: `bool ok = (SendCopyWithAtm(acc, order.Instrument, in scaledSig, namedMode) != null); // T3: Order?`
  - Line 2922: `bool ok = (SendCopyWithAtm(acc, instr, in sig, namedMode) != null); // T3: Order? return`
  - Line 3039: discarded result (no `bool ok =` — unchanged, correct)
  - Line 4586: discarded result (unchanged, correct)
  - Line 7043: discarded result (unchanged, correct)
  - Line 7181: `Order? newOrder = SendCopyWithAtm(...)` (captures for T3-3)
- All 6 call sites correctly updated per T3-2 spec
- **STATUS: CONFIRMED** ✓

### T3-3: `SubmitDrainedEntry` stores returned Order

- Line 7181: `Order? newOrder = SendCopyWithAtm(follower, payload.Instrument, in signal, namedAtm);`
- Line 7182: `if (newOrder != null)  // T3: CYC 5->6`
- Line 7183: `_drainFollowerEntryOrders.TryAdd(acctKey, newOrder);`
- CYC: lizard=6, manual McCabe=6 ≤ 8 ✓
- **STATUS: CONFIRMED** ✓

### T3-4: `TrySetSlateReadyOnDrainFill` retrieves and removes drainFo **(CRITICAL FAILURE)**

Read at lines 1085-1101:
```csharp
private void TrySetSlateReadyOnDrainFill(OrderEventArgs e)
{
    if (e?.Order == null) return;
    if (e.Order.OrderState != OrderState.Filled) return;
    if (!_drainOwnedOrderIds.ContainsKey(e.Order.OrderId)) return;
    if (!IsFollowerAccount(e.Order.Account)) return;
    _followerSlateReady[e.Order.Account.Name] = true;
    // T3: retrieve and remove the stored follower entry order so it is available for SyncFollowerBracket.
    string acctKey = e.Order.Account.Name;
    _drainFollowerEntryOrders.TryGetValue(acctKey, out Order drainFo);
    _drainFollowerEntryOrders.TryRemove(acctKey, out _);
    NinjaTrader.Code.Output.Process(
        "[SLATE-READY-DRAIN] acct=" + acctKey
            + (drainFo != null ? " drainFo=set" : " drainFo=null"),
        NinjaTrader.NinjaScript.PrintTo.OutputTab1);
}
```

**FINDINGS**:
- ✓ `_drainFollowerEntryOrders.TryGetValue(acctKey, out Order drainFo)` present (line 1095)
- ✓ `_drainFollowerEntryOrders.TryRemove(acctKey, out _)` present (line 1096)
- ✗ **`SyncFollowerBracket` is NOT called from this method at all**
- ✗ **`drainFo` is retrieved but NEVER passed to `SyncFollowerBracket`**

Verified by: `Select-String "SyncFollowerBracket.*drainFo|drainFo.*SyncFollowerBracket"` → 0 matches

Per ticket spec (04-tickets.md §Ticket 3 Change T3-4):
> "Then update the existing SyncFollowerBracket(...) call to pass drainFo instead of null as the fo parameter"

Per architecture plan §5 Change 3 (revised after VIO-1):
> "SyncFollowerBracket(acc, drainFo, ...); // pass drainFo instead of null"

Per plan-review Cycle 2 (02-plan-review.md VIO-1 RESOLUTION):
> "VIO-1 STATUS: RESOLVED -- Change 3 in §5 now contains the mandatory TryGetValue + TryRemove + drainFo pass-through"

The plan-review marked VIO-1 resolved in the PLAN document, but the ENGINEER did not implement the SyncFollowerBracket call. The `drainFo` is retrieved but never used as the `fo` argument to `SyncFollowerBracket`.

This means:
1. The core fo=NULL fix described in the spec is NOT implemented
2. T3-04 test only checks `TryGetValue` is present -- it does NOT verify `drainFo` is passed to `SyncFollowerBracket`
3. SIM-3 PASS condition ("All SyncFollowerBracket calls have fo=<order>") cannot be satisfied by this implementation

**STATUS: MISSING/WRONG -- SPEC VIOLATION (VERIFY_FAIL)**

### T3-5: `AbortDrainOnFill` cleanup

- Line 7207: `_drainFollowerEntryOrders.TryRemove(acctKey, out _); // T3: cleanup stored entry order on abort`
- CYC: lizard=2, manual McCabe=3 ≤ 8 ✓
- **STATUS: CONFIRMED** ✓

### T3-6: `TryDrainWatchdog` cleanup

- Line 7229: `_drainFollowerEntryOrders.TryRemove(kv.Key, out _); // T3: cleanup stored entry order on watchdog expiry`
- Inside `if (now - kv.Value.TimestampTicks > 2000L)` block ✓
- CYC: lizard=3, manual McCabe=4 ≤ 8 ✓
- **STATUS: CONFIRMED** ✓

---

## Regression Checks

### T1 Changes Still Intact

- `IsNativeExitName` present (line 2614) ✓
- `TryParseStopSuffix` present (line 3430) ✓
- SimStop predicates confirmed by prior T1 verification ✓

### T2a Changes Still Intact

- `ATM-send qty=` log line at line 5074 ✓
- `ATM-order qty=` log line at line 5098 ✓

### T2b Changes Still Intact

- `_atmTemplateGroupQty` field at line 425 ✓
- `TryGetAtmTemplateGroupCount` at line 1979 ✓
- `signal.Quantity / groupCount` division in SendCopyWithAtm at line 5080 ✓

All prior T1/T2a/T2b changes intact. **PASS**

---

## Test Verification

Command:
```powershell
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "B198Tests" --no-build
```

**Result**:
```
Passed!  - Failed: 0, Passed: 24, Skipped: 0, Total: 24, Duration: 219 ms
```

All 24 tests pass (T1-01..T1-16, T2a-01..T2a-02, T2b-01..T2b-02, T3-01..T3-04). **PASS**

**IMPORTANT NOTE on T3-04 test**: The test `T3_04_TrySetSlateReadyOnDrainFill_RetrievesAndPassesDrainFo` only asserts:
```csharp
Assert.Contains("_drainFollowerEntryOrders.TryGetValue", Src());
```
It does NOT verify that `drainFo` is passed to `SyncFollowerBracket`. The test passes (TryGetValue is present)
but the critical wiring step (pass drainFo to SyncFollowerBracket) is missing from the implementation.
The test name implies "Retrieves AND Passes drainFo" but only verifies "Retrieves".
This is a test spec gap that masks the missing implementation.

Cross-check vs engineer Layer 2: Engineer reported 24 PASS. Match on count. However, T3-04 test name is misleading -- test passes but implementation is incomplete.

---

## Deletion Audit

Command:
```powershell
git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"
```

**Result**: 0 deleted symbols (Note: B198 changes are uncommitted; diff from HEAD~1 shows no CopyEngine.cs changes in last commit).

**Result**: N/A (work uncommitted). No unauthorized deletions found in working tree changes. **PASS**

---

## Single-Hunk Ban Check

Command:
```powershell
(git show HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^@@").Count
```

**Result**: 0 (HEAD commit does not touch CopyEngine.cs -- B198 changes are uncommitted).

**Result**: N/A (work uncommitted). No single-hunk violation. **PASS**

---

## Guardian Symbol Script

Command:
```powershell
powershell -File .\scripts\verify_critical_symbols.ps1
```

**Result**: FAIL -- `TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)`

**Assessment**: This is a pre-existing condition documented in T1, T2a, and T2b verifications.
TradeCopierPanel.cs has `btn.Background = isTeal ? BrushTeal : BrushInactive` (line 1162) but not
the literal `_instr2tBtn.Background = BrushTeal` pattern the script requires.
T3 did NOT introduce or worsen this condition.

**CopyEngine guardian symbols (28 symbols)**: ALL PRESENT ✓

Manual CopyEngine check:
```
_followerSlateReady, TryMarkFollowerSlateNotReady, TrySetFollowerSlateReady, IsAccountFlat,
HasAnyActiveOrders, OnFollowerPositionUpdate, FlatGuardLoop, TryLaneDHook, _laneDDispatched,
TryLaneDDispatchFollower, TryLaneEHook, _laneEDispatched, TryLaneEDispatchFollower,
BuildLaneEKey, IsLaneEEligible, PositionUpdate, FindFollowerBracketOrder, EvictDedup,
TryCancelFollowerEntries, CancelScopedFollowerEntries, IsLiveEntryBlocked,
IsReversalToFlatFollower, ShouldSkipForReversalGuard, TryDispatchLeaderFlat,
DispatchCopy, OnOrderUpdate, Subscribe, Unsubscribe
```
All 28: PRESENT ✓

Guardian script TradeCopierPanel failure = pre-existing, not T3 scope. **Not blocking for T3 verdict.**

---

## Cross-Check vs Engineer Layer 2 Report

| Scan/Check | Engineer Layer 2 | Verifier Layer 3 | Discrepancy? |
|-----------|-----------------|-----------------|--------------|
| SCAN-01 lock() | 0 PASS | 0 PASS | None |
| SCAN-02 async void | 0 PASS | 0 PASS | None |
| SCAN-03 return null | PASS (exempt) | PASS (exempt) | None |
| SCAN-04 CYC SendCopyWithAtm | CYC=4 PASS | Manual~7, Lizard=11 WARNING | **Discrepancy: engineer reported CYC=4, actual manual~7, lizard=11. Not blocking under project convention.** |
| SCAN-04 CYC SubmitDrainedEntry | CYC=6 PASS | CYC=6 PASS | None |
| SCAN-04 CYC TrySetSlateReadyOnDrainFill | CYC=5 PASS | Lizard=7, manual~6 PASS | Minor discrepancy on TrySetSlateReadyOnDrainFill: engineer said 5, lizard=7. Not blocking. |
| SCAN-05 non-ASCII | 0 PASS | 0 PASS | None |
| SCAN-06 [Test]/[TestMethod] | 0 PASS | comment-only match PASS | None |
| SCAN-07 field type | confirmed PASS | confirmed PASS | None |
| T3-1 field present | PASS | CONFIRMED | None |
| T3-2 return type Order? | PASS | CONFIRMED | None |
| T3-3 TryAdd in SubmitDrainedEntry | PASS | CONFIRMED | None |
| T3-4 TryGetValue present | PASS | TryGetValue CONFIRMED but **SyncFollowerBracket NOT called** | **CRITICAL DISCREPANCY: Engineer claims T3-4 PASS but SyncFollowerBracket call is MISSING** |
| T3-5 AbortDrainOnFill TryRemove | PASS | CONFIRMED | None |
| T3-6 TryDrainWatchdog TryRemove | PASS | CONFIRMED | None |
| 24 tests GREEN | PASS | CONFIRMED | None |

---

## Violation Summary

### VIOLATION-1 (CRITICAL — VERIFY_FAIL): T3-4 SyncFollowerBracket wiring MISSING

**Rule**: Spec requirement (04-tickets.md §Ticket 3 Change T3-4)
**File**: `src/PropTraderTools/CopyEngine.cs`
**Method**: `TrySetSlateReadyOnDrainFill` (lines 1085-1101)
**Expected**: `drainFo` retrieved from `_drainFollowerEntryOrders` and passed to `SyncFollowerBracket` as `fo` argument
**Actual**: `drainFo` is retrieved (line 1095) and the dict entry is removed (line 1096), then `drainFo` is used only in a log message (line 1099). `SyncFollowerBracket` is NOT called from this method.
**Evidence**: `Select-String "SyncFollowerBracket.*drainFo|drainFo.*SyncFollowerBracket"` → 0 matches
**Impact**: The core fo=NULL fix (VIO-1 from plan-review) is not implemented. `SyncFollowerBracket` when called from drain path still receives null as `fo`.
**DW-B195-01**: NOT CLOSED by this implementation.

**Required fix for engineer (Cycle 2)**:
In `TrySetSlateReadyOnDrainFill`, after retrieving `drainFo` and before or instead of the current log line, find the existing `SyncFollowerBracket(...)` call path and pass `drainFo` as the `fo` argument. Per architecture plan §5 Change 3:
```csharp
SyncFollowerBracket(acc, drainFo, ...); // pass drainFo instead of null
```
Note: `TrySetSlateReadyOnDrainFill` does not currently call `SyncFollowerBracket` directly -- the engineer must trace the call chain from this method to locate where SyncFollowerBracket is invoked for the drain-fill path and wire `drainFo` through.

### WARNING (NOT blocking): SCAN-04 SendCopyWithAtm CYC discrepancy

- Engineer-reported CYC=4 is incorrect (actual manual McCabe ~7, lizard CCN=11)
- Under project convention (manual McCabe, excludes `?.`): passes JS-066 (≤8)
- Recommended: add a future DW card to reduce SendCopyWithAtm CYC (extract a helper to bring lizard CCN under 8)

---

## DNA Rule Check Results

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | SCAN-01: 0 matches | PASS |
| JS-001 (no throw) | No throw in any T3 change | PASS |
| JS-002 (return null) | SendCopyWithAtm null = NT8 absence signal, exempt | PASS |
| JS-033 (no async void) | SCAN-02: 0 matches | PASS |
| JS-066 (CYC ≤ 8) | All methods ≤ 8 under project manual McCabe convention | PASS |
| JS-008 (no mutable struct) | ConcurrentDictionary at class level, not struct field | PASS |
| JS-009 (ConcurrentDictionary) | _drainFollowerEntryOrders is ConcurrentDictionary | PASS |
| xUnit [Fact] | All T3 tests use [Fact], no NUnit/MSTest | PASS |
| ASCII-only | SCAN-05: 0 non-ASCII | PASS |
| sealed keyword on TradeCopierWindow | Not applicable to T3 | N/A |
| FontFamily on WPF element | SCAN-03 (covered by global scan) | PASS |
| DateTime.Now vs UtcNow | Not in T3 scope | N/A |
| CreateOrder PTT- prefix | T3 does not add CreateOrder calls | N/A |

---

## Architecture Compliance

| Check | Expected | Actual | Result |
|-------|----------|--------|--------|
| New field `_drainFollowerEntryOrders` at class level | ConcurrentDictionary<string, Order>, StringComparer.Ordinal | Line 433: CONFIRMED | PASS |
| SendCopyWithAtm return type | `private Order?` | Line 5064: CONFIRMED | PASS |
| SubmitDrainedEntry TryAdd | After `Order? newOrder = SendCopyWithAtm(...)` | Lines 7181-7183: CONFIRMED | PASS |
| TrySetSlateReadyOnDrainFill TryGetValue | `_drainFollowerEntryOrders.TryGetValue(acctKey, out Order drainFo)` | Line 1095: CONFIRMED | PASS |
| TrySetSlateReadyOnDrainFill TryRemove | `_drainFollowerEntryOrders.TryRemove(acctKey, out _)` | Line 1096: CONFIRMED | PASS |
| TrySetSlateReadyOnDrainFill passes drainFo to SyncFollowerBracket | `SyncFollowerBracket(acc, drainFo, ...)` | **ABSENT** | **FAIL** |
| AbortDrainOnFill TryRemove | cleanup | Line 7207: CONFIRMED | PASS |
| TryDrainWatchdog TryRemove | cleanup in expiry block | Line 7229: CONFIRMED | PASS |

---

## Final Verdict

**VERIFY_FAIL**

**Primary reason**: T3 Change T3-4 is incomplete. The engineer implemented TryGetValue + TryRemove for
`_drainFollowerEntryOrders` in `TrySetSlateReadyOnDrainFill` but did NOT call `SyncFollowerBracket` with
`drainFo`. This is the central VIO-1 fix required by plan-review cycle 2 and the spec.
The fo=NULL drain path bug (DW-B195-01) is NOT fixed by this implementation.

**What passes**: T3-1 (field), T3-2 (return type), T3-3 (TryAdd), T3-5 (AbortDrainOnFill), T3-6 (TryDrainWatchdog),
all 7 scans, all 24 tests, deletion audit, CopyEngine guardian symbols.

**What fails**: T3-4 (SyncFollowerBracket wiring absent).

**Engineer retry instruction**:
In `TrySetSlateReadyOnDrainFill` (CopyEngine.cs line 1085), after retrieving `drainFo` (line 1095),
trace the drain-fill SyncFollowerBracket call path and wire `drainFo` as the `fo` argument per
04-tickets.md §T3 Change T3-4 and architecture plan §5 Change 3.

Additionally update T3-04 test to assert `SyncFollowerBracket` wiring
(e.g., assert source contains `SyncFollowerBracket` call within `TrySetSlateReadyOnDrainFill` body or
assert source does NOT contain the original `SyncFollowerBracket(acc, null,` drain-path pattern).

---

*Written by: ptt-verifier*
*Epic: B198*
*Ticket: 3 (fo=NULL Drain Path Fix -- DW-B195-01)*
*Phase: 4b (Independent Verification)*
*Returns: VERIFY_FAIL*

---

## Retry-1 Verification
**Verifier**: ptt-verifier (Phase 4b -- Retry 1)
**Date**: 2026-10-13
**Retry reason**: Cycle 1 VERIFY_FAIL -- T3-4 SyncFollowerBracket wiring was absent.
**Engineer retry report**: docs/brain/B198/ticket-3-completion.md (Retry 1)

---

### VERDICT: VERIFY_PASS

**Violation count: 0**

The T3-4 fix is now present, correct, and complete. TrySetSlateReadyOnDrainFill (line 1085-1105)
now calls SyncFollowerBracket(e.Order.Account, drainFo ?? e.Order, false, e.Order.LimitPrice, tickSize)
at line 1100, wiring drainFo (retrieved from _drainFollowerEntryOrders.TryGetValue at line 1095)
as the o argument. The ?? e.Order fallback is acceptable per the ticket spec.
All 7 scans PASS, all T3 changes confirmed, all regressions intact, 24/24 tests GREEN.

---

### Critical T3-4 Fix Verification

Command:
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "SyncFollowerBracket" | Select-Object LineNumber, Line

**Result**: Match at line 1100 (inside TrySetSlateReadyOnDrainFill):
`
SyncFollowerBracket(e.Order.Account, drainFo ?? e.Order, false, e.Order.LimitPrice, tickSize);
`

Verification chain:
- Line 1094: string acctKey = e.Order.Account.Name;
- Line 1095: _drainFollowerEntryOrders.TryGetValue(acctKey, out Order drainFo); -- retrieves stored entry order
- Line 1096: _drainFollowerEntryOrders.TryRemove(acctKey, out _); -- removes dict entry
- Line 1099: double tickSize = e.Order.Instrument?.MasterInstrument?.TickSize ?? 0.0;
- Line 1100: SyncFollowerBracket(e.Order.Account, drainFo ?? e.Order, false, e.Order.LimitPrice, tickSize);

drainFo is the Order retrieved from _drainFollowerEntryOrders which was populated in
SubmitDrainedEntry (line 7185-7187) after SendCopyWithAtm returns.

drainFo ?? e.Order fallback: if SendCopyWithAtm failed and drainFo is null (TryGetValue
assigns default(Order)=null when key not found), falls back to e.Order (the fill event order).
This is an acceptable fallback per ticket spec §T3 Change T3-4.

**SyncFollowerBracket call with drainFo: CONFIRMED. VIO-1 fix is now IMPLEMENTED.**

T3-04 test (line 231-234 of B198Tests.cs) now asserts BOTH:
- Assert.Contains("_drainFollowerEntryOrders.TryGetValue", Src()) (retrieval)
- Assert.Contains("SyncFollowerBracket(e.Order.Account, drainFo", Src()) (wiring)

**Both assertions pass.** T3-04 now correctly verifies the full wiring.

---

### Independent 7-Scan Results (Retry-1)

#### SCAN-01: JS-021 lock() ban

Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { .Line -notmatch "//" }
**Result**: 0 matches. **PASS**
Cross-check vs engineer Layer 2: Engineer reported 0. Match. No discrepancy.

#### SCAN-02: JS-033 async void ban

Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async\s+void\s+\w+" | Where-Object { .Line -notmatch "//" }
**Result**: 0 matches. **PASS**
Cross-check vs engineer Layer 2: Engineer reported 0. Match. No discrepancy.

#### SCAN-03: JS-001/JS-002 return null in hot path

Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null;"
**Result**: 15 matches. T3-scope at lines 5100 and 5120 (SendCopyWithAtm -- NT8 absence signal, exempt per project contract).
No new hot-path violations. **PASS**
Cross-check vs engineer Layer 2: Engineer claimed no new violations. Match.

#### SCAN-04: JS-066 CYC check (manual McCabe)

TrySetSlateReadyOnDrainFill (lines 1085-1105):
- 4 if-guards (lines 1087-1090) + base = 5 McCabe. ?? and ?. excluded per project convention.
- **CYC = 5** <= 8. PASS.

SubmitDrainedEntry (lines 7166-7204):
- TryRemove guard (1) + follower null (2) + is-Named-pattern (3) + newOrder null (4) + foreach (5) + base = 6.
- **CYC = 6** <= 8. PASS.

SendCopyWithAtm (lines 5068-5122):
- catch (1) + T2b if-compound (2) + order null (3) + AtmObject null (4) + base = 5 (excludes 3x ?.).
- **CYC = 5** (manual McCabe) <= 8. PASS. Note: Lizard CCN=11 due to ?. counting -- same WARNING as prior cycles. Not blocking under project convention.

Cross-check vs engineer Layer 2: Engineer reported max CYC=6. Match on all T3-scoped methods. SendCopyWithAtm CCN discrepancy (engineer 4, actual manual 5) is a minor inaccuracy, not blocking.

#### SCAN-05: ASCII-only

Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"
**Result**: 0 matches. **PASS**

#### SCAN-06: xUnit-only test framework

Command: Select-String -Path "src/PropTraderTools/Tests/B198Tests.cs" -Pattern "NUnit|MSTest|\[TestMethod\]|\[Test\]"
**Result**: 1 match at line 9 -- comment only: // Framework: xUnit only. No NUnit. No MSTest.
0 actual violations. **PASS**

#### SCAN-07: _drainFollowerEntryOrders field type and access

Command: Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "_drainFollowerEntryOrders"
**Result**: 6 matches:
- Line 433: private readonly ConcurrentDictionary<string, Order> _drainFollowerEntryOrders = CONFIRMED
- Line 1095: TryGetValue (TrySetSlateReadyOnDrainFill) -- T3-4 wiring
- Line 1096: TryRemove (TrySetSlateReadyOnDrainFill)
- Line 7187: TryAdd (SubmitDrainedEntry)
- Line 7211: TryRemove (AbortDrainOnFill -- T3-5)
- Line 7233: TryRemove (TryDrainWatchdog -- T3-6)
Field: private readonly ConcurrentDictionary<string, Order> -- CONFIRMED. No lock() around any access. **PASS**

---

### T3 Implementation Checks (Retry-1)

| Change | Status | Evidence |
|--------|--------|----------|
| T3-1: _drainFollowerEntryOrders field | CONFIRMED | Line 433: private readonly ConcurrentDictionary<string, Order> |
| T3-2: SendCopyWithAtm return type Order? | CONFIRMED | Line 5068: private Order? SendCopyWithAtm( |
| T3-2: 6 call sites updated | CONFIRMED | Lines 2815, 2926 (!= null); 3043, 4590, 7047 (discarded); 7185 (Order? newOrder =) |
| T3-3: SubmitDrainedEntry TryAdd | CONFIRMED | Lines 7185-7187: Order? newOrder = SendCopyWithAtm(...); if (newOrder != null) TryAdd(...) |
| T3-4: TrySetSlateReadyOnDrainFill TryGetValue | CONFIRMED | Line 1095 |
| T3-4: TrySetSlateReadyOnDrainFill TryRemove | CONFIRMED | Line 1096 |
| T3-4: SyncFollowerBracket called with drainFo | **CONFIRMED (FIXED)** | Line 1100: SyncFollowerBracket(e.Order.Account, drainFo ?? e.Order, ...) |
| T3-5: AbortDrainOnFill TryRemove | CONFIRMED | Line 7211 |
| T3-6: TryDrainWatchdog TryRemove in expiry block | CONFIRMED | Line 7233 |

All T3 changes: PRESENT AND CORRECT.

---

### Regression Checks (Retry-1)

| Check | Evidence | Result |
|-------|----------|--------|
| T1 IsNativeExitName SimStop branch | Line 2630: if (name.StartsWith("SimStop", StringComparison.Ordinal)) | PASS |
| T1 TryParseStopSuffix replacement | Line 3434-3453: replaced body with SimStop prefix | PASS |
| T1 IsAtmBracketName SimStop clause | Line 885 | PASS |
| T1 IsBracketLeg SimStop | Line 3108 | PASS |
| T1 IsBracketLegStatic SimStop | Line 5950 | PASS |
| T1 IsAtmSTPOrder SimStop | Line 5931/5966 | PASS |
| T1 IsStopLeg SimStop | Line 5931 | PASS |
| T1 FindLeaderCollateralOrder simStopName | Lines 3477/3480 | PASS |
| T1 FindLeaderStopPrice simStopName | Lines 3958/3962 | PASS |
| T1 MaxBracketLegs = 9 | (confirmed by T1 verification) | PASS |
| T2a ATM-send qty= log line | Line 5078 | PASS |
| T2a ATM-order qty= log line | Line 5102 | PASS |
| T2b _atmTemplateGroupQty field | Line 425 | PASS |
| T2b TryGetAtmTemplateGroupCount | Line 1983 | PASS |
| T2b signal.Quantity / groupCount | Line 5084 | PASS |

All T1/T2a/T2b regressions intact. **PASS**

---

### Test Verification (Retry-1)

Command: dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "B198Tests" --no-build
**Result**: Passed! -- Failed: 0, Passed: 24, Skipped: 0, Total: 24, Duration: 220 ms
All 24 tests GREEN (T1-01..T1-16, T2a-01..T2a-02, T2b-01..T2b-02, T3-01..T3-04). **PASS**

T3-04 test (T3_04_TrySetSlateReadyOnDrainFill_RetrievesAndPassesDrainFo) now asserts:
1. Assert.Contains("_drainFollowerEntryOrders.TryGetValue", Src()) -- retrieval confirmed
2. Assert.Contains("SyncFollowerBracket(e.Order.Account, drainFo", Src()) -- wiring confirmed
Both assertions pass. Test name now matches implementation. **PASS**

---

### Deletion Audit (Retry-1)

Command: git diff HEAD~1 HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^-    (private|internal|public)"
**Result**: 0 deleted symbols. B198 work is uncommitted. No unauthorized deletions. **PASS**

---

### Single-Hunk Ban Check (Retry-1)

Command: (git show HEAD -- src/PropTraderTools/CopyEngine.cs | Select-String "^\@\@").Count
**Result**: 0 (HEAD does not touch CopyEngine.cs -- B198 changes uncommitted). Not 1. **PASS**

---

### Guardian Symbol Script (Retry-1)

Command: powershell -File .\scripts\verify_critical_symbols.ps1
**Result**: Exit code 1 -- single violation: TradeCopierPanel: _instr2tBtn.Background = BrushTeal (Quick2t visual fix)
This is the **same pre-existing condition** documented in T1, T2a, T2b, and Cycle 1 T3 verifications.
T3 did NOT introduce or modify TradeCopierPanel.cs. NOT blocking for T3.

CopyEngine 28 guardian symbols: independently verified ALL PRESENT via direct grep.
_followerSlateReady, TryMarkFollowerSlateNotReady, TrySetFollowerSlateReady, IsAccountFlat,
HasAnyActiveOrders, OnFollowerPositionUpdate, FlatGuardLoop, TryLaneDHook, _laneDDispatched,
TryLaneDDispatchFollower, TryLaneEHook, _laneEDispatched, TryLaneEDispatchFollower, BuildLaneEKey,
IsLaneEEligible, PositionUpdate, FindFollowerBracketOrder, EvictDedup, TryCancelFollowerEntries,
CancelScopedFollowerEntries, IsLiveEntryBlocked, IsReversalToFlatFollower, ShouldSkipForReversalGuard,
TryDispatchLeaderFlat, DispatchCopy, OnOrderUpdate, Subscribe, Unsubscribe
All 28: PRESENT. **PASS** (guardian script TradeCopierPanel failure = pre-existing, not T3 scope).

---

### Cross-Check vs Engineer Layer 2 Report (Retry-1)

| Scan/Check | Engineer Layer 2 | Verifier Layer 3 | Discrepancy? |
|-----------|-----------------|-----------------|--------------|
| SCAN-01 lock() | 0 PASS | 0 PASS | None |
| SCAN-02 async void | 0 PASS | 0 PASS | None |
| SCAN-03 return null | PASS (exempt) | PASS (exempt) | None |
| SCAN-04 CYC TrySetSlateReadyOnDrainFill | CYC=6 PASS (manual) | CYC=5 McCabe PASS | Minor: eng said 6, actual 5. Not blocking. |
| SCAN-04 CYC SubmitDrainedEntry | CYC=6 PASS | CYC=6 PASS | None |
| SCAN-04 CYC SendCopyWithAtm | ~7 (project conv) PASS | ~5 McCabe / lizard=11 PASS | Minor: both agree passes project convention. |
| SCAN-05 non-ASCII | 0 PASS | 0 PASS | None |
| SCAN-06 [Test]/[TestMethod] | comment-only PASS | comment-only PASS | None |
| SCAN-07 field type | confirmed PASS | confirmed PASS | None |
| T3-1 field present | PASS | CONFIRMED | None |
| T3-2 return type Order? | PASS | CONFIRMED | None |
| T3-3 TryAdd in SubmitDrainedEntry | PASS | CONFIRMED | None |
| T3-4 SyncFollowerBracket with drainFo | **PASS (FIXED)** | **CONFIRMED -- SyncFollowerBracket(e.Order.Account, drainFo ?? e.Order, ...) at line 1100** | None -- fix now present |
| T3-5 AbortDrainOnFill TryRemove | PASS | CONFIRMED | None |
| T3-6 TryDrainWatchdog TryRemove | PASS | CONFIRMED | None |
| 24 tests GREEN | PASS | CONFIRMED | None |
| T3-04 asserts SyncFollowerBracket wiring | PASS | CONFIRMED (both assertions present in test) | None |

---

### DNA Rule Check Results (Retry-1)

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | SCAN-01: 0 matches | PASS |
| JS-001 (no throw) | No throw in any T3 change, no throw in retry fix | PASS |
| JS-002 (return null) | SendCopyWithAtm null = NT8 absence signal, exempt | PASS |
| JS-033 (no async void) | SCAN-02: 0 matches | PASS |
| JS-066 (CYC <= 8) | All T3-scope methods <= 8 under project manual McCabe convention | PASS |
| JS-008 (no mutable struct) | _drainFollowerEntryOrders at class level, not struct | PASS |
| JS-009 (ConcurrentDictionary) | _drainFollowerEntryOrders is ConcurrentDictionary | PASS |
| xUnit [Fact] | T3-04 updated assertion still [Fact], no NUnit/MSTest | PASS |
| ASCII-only | SCAN-05: 0 non-ASCII; new fix strings all ASCII | PASS |

---

### Architecture Compliance (Retry-1)

| Check | Expected | Actual | Result |
|-------|----------|--------|--------|
| TryGetValue | _drainFollowerEntryOrders.TryGetValue(acctKey, out Order drainFo) | Line 1095: CONFIRMED | PASS |
| TryRemove | _drainFollowerEntryOrders.TryRemove(acctKey, out _) | Line 1096: CONFIRMED | PASS |
| SyncFollowerBracket called with drainFo | SyncFollowerBracket(acc, drainFo, ...) | Line 1100: SyncFollowerBracket(e.Order.Account, drainFo ?? e.Order, ...) | **PASS** |
| ?? fallback acceptable | drainFo ?? e.Order (if SendCopyWithAtm failed) | Present at line 1100 | PASS |
| CYC unchanged | 5 -> 5 (TryGetValue+TryRemove+?? = 0 new McCabe branches) | Verified: 4 if-guards + base = 5 | PASS |

VIO-1 from plan-review cycle 2 (TryGetValue + TryRemove + drainFo pass-through) is now FULLY IMPLEMENTED.
DW-B195-01 is NOW CLOSED by this implementation.

---

### Final Verdict (Retry-1)

**VERIFY_PASS**

All violations from Cycle 1 are resolved. Zero new violations found.

| Area | Verdict |
|------|---------|
| T3-4 SyncFollowerBracket wiring | **FIXED -- PASS** |
| All 7 scans (SCAN-01..SCAN-07) | PASS |
| T3-1 through T3-6 | ALL CONFIRMED |
| T1/T2a/T2b regressions | ALL INTACT |
| 24 tests GREEN | PASS |
| Deletion audit | PASS |
| Single-hunk ban | PASS |
| Guardian symbols (CopyEngine 28) | PASS |
| DNA rules (JS-021/001/002/033/066/008/009) | ALL PASS |

DW-B195-01 status: **CLOSED** by T3 Retry 1.

---

*Written by: ptt-verifier*
*Epic: B198*
*Ticket: 3 (fo=NULL Drain Path Fix -- DW-B195-01)*
*Phase: 4b (Independent Verification -- Retry 1)*
*Returns: VERIFY_PASS*
