# Ticket 1 Verification -- RegisterBeRetrySlotIfNeeded

**Scope**: TICKET 1 ONLY (WAVE1-LANE-A-01)
**Verifier**: ptt-verifier
**Date**: 2026-09-07
**Source file**: `src/PropTraderTools/CopyEngine.cs` (READ-ONLY)
**Brain dir**: `docs/brain/WAVE1-LANE-A/`

---

## Scope: TICKET 1 ONLY

Independent verification of extraction of `RegisterBeRetrySlotIfNeeded` (A-01).
No other tickets read or touched in this session.

---

## Independent CCN Scan (Lizard)

Command run:
```
lizard src/PropTraderTools/CopyEngine.cs --csv -x "*/bin/*" -x "*/obj/*" | Select-String "RegisterBeRetrySlotIfNeeded|IsBeRetrySlotNeeded|RegisterPendingBeSlot"
```

| Method | Engineer Reported CCN | Verifier Measured CCN | Lines | Match? |
|--------|-----------------------|----------------------|-------|--------|
| `IsBeRetrySlotNeeded` | 4 | 4 | 6262-6267 | YES |
| `RegisterPendingBeSlot` (4-param, new) | 1 | 1 | 6273-6284 | YES |
| `RegisterBeRetrySlotIfNeeded` | 6 | 6 | 6286-6310 | YES |
| `RegisterPendingBeSlot` (3-param, pre-existing) | 4 (not in scope) | 4 (untouched) | 6528-6543 | YES |

All CCN <= 8. No violations.

---

## Independent 7-Scan Results

| Scan | Description | Engineer Result | Verifier Result | Match? |
|------|-------------|-----------------|-----------------|--------|
| SCAN-01 | `lock(` in new/modified methods | 0 hits | 0 actual hits (comments only) | YES |
| SCAN-02 | `async void` in new code | 0 hits | 0 hits (1 comment only) | YES |
| SCAN-03 | `return null;` in scope methods | 0 hits | 0 hits | YES |
| SCAN-04 | CCN <= 8 for all 3 methods | PASS | PASS | YES |
| SCAN-05 | `CreateOrder` in new helpers | N/A | N/A (0 hits) | YES |
| SCAN-06 | Non-ASCII characters in scope methods | 0 hits | 0 hits | YES |
| SCAN-07 | `public` visibility on new helpers | 0 hits | 0 hits | YES |

---

## Implementation Correctness Check

| Check | Expected | Result |
|-------|----------|--------|
| `IsBeRetrySlotNeeded` is `private static bool` | YES | PASS -- line 6262 |
| `IsBeRetrySlotNeeded` has 4 params (bool, int, int, bool) | YES | PASS -- lines 6263-6266 |
| `RegisterPendingBeSlot` is `private void` | YES | PASS -- line 6273 |
| `RegisterPendingBeSlot` has 4 params (3 required + `delayMs = 500`) | YES | PASS -- lines 6274-6278 |
| `RegisterBeRetrySlotIfNeeded` signature unchanged (6 params) | YES | PASS -- lines 6286-6292 |
| CRITICAL ORDERING: slot write FIRST | YES | PASS -- line 6279 (dict indexer write) |
| CRITICAL ORDERING: log SECOND | YES | PASS -- lines 6280-6282 |
| CRITICAL ORDERING: QueueBeRetryFallback THIRD | YES | PASS -- line 6283 |
| No new `lock()` in any of 3 methods | YES | PASS |
| Both helpers are `private` (not public/internal) | YES | PASS -- SCAN-07: 0 hits |

---

## Test Verification

Command run:
```
dotnet test tests/PropTraderTools.Tests/PropTraderTools.Tests.csproj
```

| Metric | Engineer Reported | Verifier Measured | Match? |
|--------|-------------------|-------------------|--------|
| Passing | 127 | 127 | YES |
| Failing | 0 | 0 | YES |
| Skipped | 3 | 3 | YES |
| Total | 130 | 130 | YES |

All 8 new `[Fact]` test methods confirmed present in `CopyEngineTests.cs`:
- `IsBeRetrySlotNeeded_ReturnsFalse_WhenNotFollowerAccount` (T31)
- `IsBeRetrySlotNeeded_ReturnsFalse_WhenLeaderCountZero` (T32)
- `IsBeRetrySlotNeeded_ReturnsFalse_WhenTargetsCountEqualsLeaderCount` (T33)
- `IsBeRetrySlotNeeded_ReturnsFalse_WhenTargetsCountExceedsLeaderCount` (T34)
- `IsBeRetrySlotNeeded_ReturnsFalse_WhenPositionIsFlat` (T35)
- `IsBeRetrySlotNeeded_ReturnsTrue_WhenPartialFollowerWithOpenPosition` (T36)
- `RegisterPendingBeSlot_SlotWrittenWithCorrectKeys_WhenBothDelayVariants` (T37)
- `RegisterPendingBeSlot_DefaultDelayMs_Is500` (T38)

---

## Cross-Check Discrepancies

**None.** Every engineer-reported metric matched verifier-measured results exactly.

- CCN values match lizard output column-for-column.
- 7-scan results all match independently re-run scans.
- Test count 127/0/3/130 confirmed by independent `dotnet test` run.
- Implementation ordering (critical slot-write-first contract) verified against actual source.

---

## DNA Rule Audit

| Rule | Check | Result |
|------|-------|--------|
| JS-021 (no lock) | No `lock(` in any new or modified method | PASS |
| JS-001 (no throw in gate methods) | No `throw` in helpers or `RegisterBeRetrySlotIfNeeded` | PASS |
| JS-002 (no return null) | `IsBeRetrySlotNeeded` returns `bool`; `RegisterPendingBeSlot` returns `void` | PASS |
| JS-080 (CYC <= 8) | All three methods: 4, 1, 6 | PASS |
| JS-033 (no async void) | Both helpers synchronous | PASS |
| ASCII-only | No non-ASCII in scope lines | PASS |

---

## VERIFY_PASS