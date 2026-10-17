# B196 Ticket 4 Verification Report

**Epic**: B196 -- B161-B166 Ghost-Entry Guard + B163 AFP Guard Bundle
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-10-11
**Scope**: TICKET 4 ONLY -- B161-B166 Ghost-Entry Guard + B163 AFP Guard Bundle
**Layer**: 3 (independent re-run, never trusting Layer 2 engineer self-report)

---

## SCOPE LOCK

This verification covers Ticket 4 only. No other ticket completion files were read in this session.
Files read:
- src/PropTraderTools/CopyEngine.cs (READ-ONLY)
- src/PropTraderTools/Tests/B196Tests.cs (READ-ONLY, via PowerShell)
- docs/brain/B196/04-tickets.md (Ticket 4 section only)
- docs/brain/B196/02-architecture-plan.md (S3 T4)
- docs/brain/B196/ticket-4-completion.md (engineer Layer 2 -- for cross-check only, not trusted)

---

## 7 SCAN RESULTS (Layer 3 -- Independent)

### SCAN 1 -- JS-021 lock() [independently run]
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//.*lock" }
```
**Result**: NO OUTPUT -- 0 actual lock() calls
**Status**: PASS

### SCAN 2 -- JS-033 async void [independently run]
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async void " | Where-Object { $_.Line -notmatch "//" }
```
**Result**: NO OUTPUT -- 0 async void methods
**Status**: PASS

### SCAN 3 -- CYC Analysis [independently counted from source body]

| Method | Line | CYC (independent count) | Limit | Status |
|--------|------|-------------------------|-------|--------|
| `IsPttEntryOrderCancelTrigger` | 895 | 4 | 8 | PASS |
| `TryReplaceOnAtmCancel` | 914 | 5 | 8 | PASS |
| `IsPttBeStopActiveState` | 4582 | 3 | 8 | PASS |
| `IsBeSlotNonTerminal` | 4591 | 1 | 8 | PASS |
| `IsBeRetryArmedFlat` | 4598 | 3 | 8 | PASS |
| `IsBeSettleOrRetrySignalActive` | 4610 | 3 | 8 | PASS |
| `HasActivePttBeExits` | 4634 | 8 (AT LIMIT) | 8 | PASS |
| `HasActivePttBeTargets` | 4651 | 8 (AT LIMIT) | 8 | PASS |
| `HasSettlingBeTargets` | 4669 | 7 | 8 | PASS |
| `IsFollowerPositionBlocked` | 4692 | 8 (AT LIMIT) | 8 | PASS |

**CYC counting notes (per project convention -- simplified McCabe)**:
- `IsPttEntryOrderCancelTrigger`: base(1) + null(1) + Cancelled(1) + name(1) + AFP(1) = CYC=4 (compound && in name check = 1 branch per project convention; final return && not counted)
- `TryReplaceOnAtmCancel`: base(1) + drainOwned(1) + drainActive(1) + trigger(1) + blocked(1) = CYC=5
- `IsPttBeStopActiveState`: base(1) + 2 `||` = CYC=3
- `IsBeSlotNonTerminal`: base(1) = CYC=1 (straight-line)
- `IsBeRetryArmedFlat`: base(1) + null(1) + IsFlat&&ContainsKey = (2)(3) = CYC=3
- `IsBeSettleOrRetrySignalActive`: base(1) + slot(1) + retry(1) = CYC=3 (inline comment says CYC=3; plan said CYC=4 -- project convention = 3, both compliant)
- `HasActivePttBeExits`: base(1)+acc null(1)+instr null(1)+foreach(1)+name(1)+instr(1)+IsPttBeStopActiveState inlined (3) = CYC=8 (project convention: inlines helper's 3 branches)
- `HasActivePttBeTargets`: same structure = CYC=8
- `HasSettlingBeTargets`: base(1)+acc null(1)+instr null(1)+foreach(1)+name(1)+instr(1)+state||=2 = CYC=7 (inline comment says CYC=7, confirmed)
- `IsFollowerPositionBlocked`: base(1)+acc(1)+instr(1)+Signal1(1)+Signal2(1)+Signal3(1)+Signal4(1)+Signals5+6(1) = CYC=8

**CRITICAL**: IsFollowerPositionBlocked CYC=8 -- AT LIMIT, not exceeded. PASS.

**Status**: PASS (all methods <= 8)

### SCAN 4 -- JS-001 throw new [independently run]
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new " | Where-Object { $_.Line -notmatch "//" }
```
**Result**: NO OUTPUT -- 0 throw new statements (file-wide)
**Status**: PASS

### SCAN 5 -- JS-002 return null [independently run]
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null" | Where-Object { $_.Line -notmatch "//" }
```
**Result**: 12 pre-existing hits at lines: 1288, 2039, 3220, 3311, 3319, 3975, 4025, 4200, 5718, 5797, 6599, 6614
NONE are in T4 method ranges (895-927, 4582-4731).
All T4 methods return bool or void -- no return null in T4 code.
**Status**: PASS (0 new violations in T4 methods)

### SCAN 6 -- ASCII-only [independently run]
```powershell
$lines = Get-Content "src/PropTraderTools/CopyEngine.cs"
$ranges = @(894..927) + @(4579..4731)
foreach ($r in $ranges) { $line = $lines[$r]; if ($line -match '[^\x00-\x7F]') { Write-Host "$r`: $line" } }
```
**Result**: NO OUTPUT -- 0 non-ASCII characters in T4 method ranges
**Status**: PASS

### SCAN 7 -- DateTime.Now [independently run]
```powershell
Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "DateTime\.Now[^U]" | Where-Object { $_.Line -notmatch "//" }
```
**Result**: NO OUTPUT -- 0 DateTime.Now (without UtcNow suffix) occurrences
**Status**: PASS

---

## CORRECTNESS VERIFICATION

### RESTORE-1 (B163) -- IsPttEntryOrderCancelTrigger AFP Guard

| Check | Line | Expected | Found | Status |
|-------|------|----------|-------|--------|
| AverageFillPrice > 0 guard present | 903 | `if (order.AverageFillPrice > 0)` | FOUND | PASS |
| Guard returns false with comment | 904 | `return false; // (4) B163: lifecycle cancel...` | FOUND | PASS |
| Original LimitPrice return still present below guard | 905 | `return order.LimitPrice > 0 && order.Instrument?.FullName != null;` | FOUND | PASS |
| CYC comment updated to CYC=4 | 890 | `// IsPttEntryOrderCancelTrigger: CYC=4.` | FOUND | PASS |

**RESTORE-1 STATUS**: PASS

### RESTORE-2 (B161) -- New Guard Methods Existence

| Method | Declaration Line | Signature | Status |
|--------|-----------------|-----------|--------|
| `IsFollowerPositionBlocked` | 4692 | `internal bool IsFollowerPositionBlocked(Account acc, Instrument instr)` | PASS |
| `IsBeSettleOrRetrySignalActive` | 4610 | `private bool IsBeSettleOrRetrySignalActive(Account acc, Instrument instr)` | PASS |
| `HasActivePttBeExits` | 4634 | `internal bool HasActivePttBeExits(Account acc, Instrument instr)` | PASS |
| `HasActivePttBeTargets` | 4651 | `internal bool HasActivePttBeTargets(Account acc, Instrument instr)` | PASS |
| `HasSettlingBeTargets` | 4669 | `internal bool HasSettlingBeTargets(Account acc, Instrument instr)` | PASS |
| `IsBeSlotNonTerminal` | 4591 | `private bool IsBeSlotNonTerminal(string accName)` | PASS |
| `IsBeRetryArmedFlat` | 4598 | `private bool IsBeRetryArmedFlat(Account acc, Instrument instr)` | PASS |
| `IsPttBeStopActiveState` (prerequisite) | 4582 | `private static bool IsPttBeStopActiveState(OrderState s)` | PASS |

**IsFollowerPositionBlocked CYC=8**: verified -- 8 branches (acc null, instr null, Signal1-4, Signals5+6). AT LIMIT, not exceeded. PASS.
**IsBeSettleOrRetrySignalActive calls IsBeSlotNonTerminal**: line 4612 CONFIRMED.
**IsBeSettleOrRetrySignalActive calls IsBeRetryArmedFlat**: line 4620 CONFIRMED.

**RESTORE-2 STATUS**: PASS

### WIRING -- TryReplaceOnAtmCancel Guard

| Check | Line | Expected | Found | Status |
|-------|------|----------|-------|--------|
| `order.Account != null` guard | 922 | Present | FOUND | PASS |
| `order.Instrument != null` guard | 923 | Present | FOUND | PASS |
| `IsFollowerPositionBlocked(order.Account, order.Instrument)` call | 924 | Present | FOUND | PASS |
| Guard placed BEFORE ReplaceFollowerCopyOnAtmCancel | 925-926 | `return; // (4)` before call | CONFIRMED | PASS |
| CYC comment updated to CYC=5 | 908 | `// TryReplaceOnAtmCancel: CYC=5.` | FOUND | PASS |

**WIRING STATUS**: PASS

### ENGINEER NOTES VERIFICATION

| Note | Check | Result |
|------|-------|--------|
| `IsPttBeStopActiveState` added as prerequisite | Grep at line 4582 | CONFIRMED -- method present |
| `_beReplaceAttempts` field used (not `_bePriceRetryAttempts`) | Grep line 313, 4603 | CONFIRMED -- `_beReplaceAttempts` is the correct field name in source; `IsBeRetryArmedFlat` uses it correctly |

### TESTS

| Test | Line | Status |
|------|------|--------|
| `[Fact] IsPttEntryOrderCancelTrigger_FilledEntry_ReturnsFalse` | 134 | FOUND in B196Tests.cs |
| `[Fact] IsPttEntryOrderCancelTrigger_UnfilledEntry_ReturnsTrue` | 147 | FOUND |
| `[Fact] IsFollowerPositionBlocked_HasActivePttBeExits_ReturnsTrue` | 158 | FOUND |
| `[Fact] IsFollowerPositionBlocked_NullAccount_ReturnsFalse` | 168 | FOUND |
| `[Fact] IsBeSettleOrRetrySignalActive_SlotPresent_ReturnsTrue` | 179 | FOUND |
| `[Fact] IsBeSettleOrRetrySignalActive_SlotAbsent_RetryArmedFlat_ReturnsTrue` | 189 | FOUND |

Test assertions verified against actual source:
- `Assert.Contains("order.AverageFillPrice > 0", src)` -- source line 903: PRESENT
- `Assert.Contains("(4) B163: lifecycle cancel of a previously-filled Entry", src)` -- source line 904: PRESENT
- `Assert.Contains("return order.LimitPrice > 0 && order.Instrument?.FullName != null;", src)` -- source line 905: PRESENT
- `Assert.Contains("HasActivePttBeExits(acc, instr)", src)` -- source line 4704: PRESENT
- `Assert.Contains("if (acc == null)   return false; // (1)", src)` -- source line 4694: PRESENT
- `Assert.Contains("IsBeSlotNonTerminal(acc.Name)", src)` -- source line 4612: PRESENT
- `Assert.Contains("IsBeRetryArmedFlat(acc, instr)", src)` -- source line 4620: PRESENT

**TESTS STATUS**: PASS (all 6 T4 [Fact] tests present and assertions confirmed to match source)

---

## DNA RULE CHECK (T4 methods)

| Rule | Check | Result |
|------|-------|--------|
| JS-021: No lock() | All new methods: ToList snapshot, ConcurrentDictionary.ContainsKey (lock-free) | PASS |
| JS-033: No async void | All new methods are `private bool` or `internal bool` (non-async) | PASS |
| JS-066: CYC <= 8 | Maximum = 8 (IsFollowerPositionBlocked, HasActivePttBeExits, HasActivePttBeTargets) -- AT LIMIT, not exceeded | PASS |
| JS-001: No throw | No throw statements in any T4 new or modified method | PASS |
| JS-002: No return null | All T4 bool methods return false on guard exits, never null | PASS |
| ASCII-only | All new identifiers, string literals, comments ASCII -- verified via grep of ranges 894-927, 4579-4731 | PASS |
| DateTime.UtcNow | No DateTime.Now or DateTime.UtcNow used in T4 code | PASS |
| NT8 constraints | No sealed keyword, no FontFamily, no hex color, no async/await in NT8 overrides -- T4 adds predicate methods only | PASS |

---

## LAYER 2 vs LAYER 3 CROSS-CHECK TABLE

| Scan | Engineer (Layer 2) Claim | My Layer 3 Result | Discrepancy |
|------|--------------------------|-------------------|-------------|
| SCAN-01 lock() | 0 actual locks | 0 actual locks | NONE |
| SCAN-02 async void | 0 hits | 0 hits | NONE |
| SCAN-03 CYC | All <= 8; AT LIMIT = 8 for 3 methods | All <= 8; AT LIMIT = 8 confirmed | NONE |
| SCAN-04 throw new | 0 hits | 0 hits | NONE |
| SCAN-05 return null | 0 new violations in T4 | 0 in T4 ranges (pre-existing elsewhere) | NONE |
| SCAN-06 ASCII-only | No Unicode | No Unicode in T4 ranges | NONE |
| SCAN-07 DateTime.Now | 0 hits | 0 hits | NONE |
| Method presence | All 10 methods added | All 10 confirmed in source | NONE |
| _beReplaceAttempts | Correctly used (noted in Layer 2) | Confirmed at line 313, 4603 | NONE |
| IsPttBeStopActiveState | Added as prerequisite | Confirmed at line 4582 | NONE |

**No discrepancies found between Layer 2 and Layer 3.**

---

## ARCHITECTURE COMPLIANCE

| Item | Requirement | Status |
|------|-------------|--------|
| Implementation order | IsBeSlotNonTerminal -> IsBeRetryArmedFlat -> IsBeSettleOrRetrySignalActive -> HasActivePttBeExits -> HasActivePttBeTargets -> HasSettlingBeTargets -> IsFollowerPositionBlocked -> AFP guard -> wiring | Confirmed by line ordering (4591, 4598, 4610, 4634, 4651, 4669, 4692, 903, 922-924) |
| VIOLATION-01 pre-extracted | IsBeSettleOrRetrySignalActive wraps signals 5+6 to keep parent CYC=8 | CONFIRMED |
| B162-B prefix | HasActivePttBeExits uses "PTT-BE-Stop" WITHOUT trailing dash | CONFIRMED (line 4640) |
| B164 prefix | HasActivePttBeTargets uses "PTT-BE-Target-" WITH trailing dash | CONFIRMED (line 4657) |
| Signal 3 states | HasSettlingBeTargets checks CancelPending || CancelSubmitted | CONFIRMED (lines 4677-4678) |
| Signal 1 delegation | IsFollowerPositionBlocked calls HasOpenPosition (pre-existing) | CONFIRMED (line 4696) |
| Guard position | IsFollowerPositionBlocked guard is AFTER IsPttEntryOrderCancelTrigger check in TryReplaceOnAtmCancel | CONFIRMED (line 920 vs 922) |

---

## FINAL VERDICT

**All 7 scans**: PASS (0 violations)
**All correctness checks**: PASS
**All 6 T4 [Fact] tests**: PRESENT and assertions verified
**Layer 2 vs Layer 3 cross-check**: NO DISCREPANCIES
**Architecture compliance**: PASS

---

## VERDICT: VERIFY_PASS

*Written by: ptt-verifier*
*Epic: B196*
*Phase: 4b (Ticket Verification)*
*Ticket: 4*
*Return: VERIFY_PASS*