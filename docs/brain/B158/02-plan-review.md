# B158 Plan Review
# ptt-plan-reviewer Phase 2 output
# Block: B158
# Reviewed: 2026-09-15
# Reviewer role: ptt-plan-reviewer
# Input: docs/brain/B158/02-architecture-plan.md
# Rules reference: docs/standards/RULES_CATALOG.md
# Status: REVIEW_PASS

---

## VERDICT: REVIEW_PASS

Zero violations found. All checklist items pass. Plan is approved for ticket generation (Phase 3).

---

## 1. LANE-SPLIT GATE Verification

**Gate result stated in plan**: LANES-APPROVED (Section 2)

| Q | Question | Plan Answer | Verification | Result |
|---|----------|-------------|--------------|--------|
| Q1 | Same method or within 50 lines? | NO -- T1 ~L1929-1974, T2 ~L6517, delta ~4543 lines | Confirmed: source grep shows TryEvictFollowerBeSlot at L1929 and IsReversalToFlatFollower at L6520. Delta = ~4591 lines. | PASS |
| Q2 | Fix B design depends on Fix A final design? | NO -- T2 is annotation-only | Confirmed: T2 changes only comment at L6517. Zero dependency on T1's SIM gate outcome. | PASS |
| Q3 | Each fix has standalone value if the other is blocked? | YES | Confirmed: DW-B156-01 closure is independent of DW-B157-COMMENT-01 correction. | PASS |
| Q4 | Each fix has an independent SIM/verification path? | YES | Confirmed: T1 SIM gate is a live NT8 5-step process; T2 has dotnet test + build-only path. | PASS |

Gate conditions (LANES-APPROVED): Q1=NO, Q2=NO, Q3=YES, Q4=YES. All four satisfied. Gate format correct.

Independence check (mandatory per spec):
- T1 region: L1929-1974 (TryEvictFollowerBeSlot + IsLeaderFlatDrainAbort)
- T2 region: L6517 (IsReversalToFlatFollower comment)
- Separation: >50 lines. Different methods. No shared design dependency. PASS.

---

## 2. T1 Architecture: DW-B156-01 Leader-Flat Drain Abort

### 2.1 Fix3 Already-Implemented Claim

**Claim in plan (Section 3.1)**: `IsLeaderFlatDrainAbort` is already implemented at CopyEngine.cs:1950 and exactly matches the DW-B156-01 Fix3 spec.

**Source verification result**: CONFIRMED.

| Item | Plan Claim | Source Line | Match |
|------|-----------|-------------|-------|
| Method presence | CopyEngine.cs:1950 | L1950: `private void IsLeaderFlatDrainAbort(Order o, string instrFull)` | YES |
| Call site | TryEvictFollowerBeSlot at L1938 | L1938: `IsLeaderFlatDrainAbort(o, instrFull);` | YES |
| Guard: skip follower | `if (IsFollowerAccount(o.Account)) return;` | L1952-1953 | YES |
| Guard: Filled only | `if (o.OrderState != OrderState.Filled) return;` | L1954-1955 | YES |
| Guard: leader flat | `if (!IsFlat(FindPosition(...))) return;` | L1956-1957 | YES |
| Loop: _pendingDispatchDrains | `foreach (var kv in _pendingDispatchDrains)` | L1959 | YES |
| Instrument match | `kv.Value.Instrument?.FullName == instrFull` | L1961 | YES |
| Abort call | `AbortDrainOnFill(kv.Key)` | L1963 | YES |
| Log marker | `[DRAIN-ABORT-LEADER-FLAT]` | L1969 | YES |

Plan source evidence is accurate and grounded. No speculation or memory reliance detected.

### 2.2 T1 Verification-Only Status

**Plan explicitly states**: "T1 is VERIFICATION-ONLY. Zero code changes." (Section 3.5)

**FAIL escalation path confirmed**: Section 3.5 step 4 states: "If SIM FAIL, do NOT implement a new fix within B158. Create a defect card for escalation (see Section 7 Risk/Dependency notes)." Section 7 Risk 1 provides the specific defect card ID (DW-B158-DRAIN-RACE-01) and escalation steps. PASS.

### 2.3 Hook Point Rationale

**Claim**: `TryEvictFollowerBeSlot` (pre-Gate-1, L1712) is the correct hook, not `TryClearLeaderDirectionOnFlat` (post-Gate-1 via `TryFirePositionState` at L1767).

**Verified**: Call order in `OnOrderUpdate` is documented: L1712 calls `TryEvictFollowerBeSlot`, L1763 is Gate 1 (`TryResolveEnabledRule`), L1767 calls `TryFirePositionState` (which leads to `TryClearLeaderDirectionOnFlat`). Pre-Gate-1 hook is strictly better for drain abort purposes. Rationale is correct and complete. PASS.

### 2.4 _pendingDispatchDrains Key Format

**Claim (Section 3.3)**: Key = follower `acc.Name`, `ConcurrentDictionary<string, PendingDispatchDrain>`, `StringComparer.Ordinal`.

**Source verified at L387-388**:
```
private readonly ConcurrentDictionary<string, PendingDispatchDrain> _pendingDispatchDrains =
    new ConcurrentDictionary<string, PendingDispatchDrain>(StringComparer.Ordinal);
```
Source comment at L385: "Key = follower account name." CONFIRMED. PASS.

### 2.5 AbortDrainOnFill Signature

**Claim (Section 3.4)**: `private void AbortDrainOnFill(string acctKey)`. CYC=3. No lock.

**Source verified at L8343-8348**: Signature matches exactly. Source comment at L8342: "CYC=3". No `lock()` present. PASS.

### 2.6 SIM Steps

**5-step SIM gate (Section 5.2)**: Reviewed in full.

| Check | Result |
|-------|--------|
| Setup defined (1 leader, 2 followers) | PRESENT |
| Step 1 PASS/FAIL criteria | PRESENT |
| Step 2 PASS/FAIL criteria | PRESENT |
| Step 3 PASS/FAIL criteria | PRESENT and correct (checks [DRAIN-ABORT-LEADER-FLAT]) |
| Step 4 PASS/FAIL criteria | PRESENT (ghost re-entry check) |
| Step 5 PASS/FAIL criteria | PRESENT (informational [DRAIN-ABORT-FLAT] check) |
| Overall PASS/FAIL definition | PRESENT |
| FAIL/TIMING RACE scenario documented | PRESENT |

All SIM steps complete and correct. PASS.

### 2.7 SIM Marker Discrepancy Note

**Requirement**: Plan must document [DRAIN-ABORT-FLAT] vs [DRAIN-ABORT-LEADER-FLAT] discrepancy.

**Result**: DOCUMENTED in multiple locations:
- Section 5.2: "CORRECT LOG MARKER TO CHECK: [DRAIN-ABORT-LEADER-FLAT] (NOT [DRAIN-ABORT-FLAT] -- that is the B155/B156 SubmitDrainedEntry marker. Different fix.)"
- Section 7 Risk 1: "The 2026-09-13 SIM may have been checking the wrong marker. The T1 SIM gate must check for [DRAIN-ABORT-LEADER-FLAT] specifically."
- Summary item 4: "Correct SIM marker: [DRAIN-ABORT-LEADER-FLAT]"

PASS.

---

## 3. T2 Architecture: DW-B157-COMMENT-01

### 3.1 Comment Location

**Claim (Section 4.1)**: CopyEngine.cs L6517 (stale comment line).

**Source verified**: `grep` finds `IsReversalToFlatFollower` at L6520; `read_file` L6516-6527 confirms the stale comment is at L6517:
```
6517 | // Returns true iff the current dispatch reverses the last direction AND the follower is flat.
```
CONFIRMED. PASS.

### 3.2 Zero Logic Impact

**Claim (Section 4.3)**: "No other lines change. No logic change. No CYC change."

**Verified**: The method body at L6526 (`return currentAction != lastAction && !followerIsFlat;`) is unchanged. Only the comment text at L6517 changes. PASS.

### 3.3 Old/New Comment Text Both Stated

| Item | Present | Verified |
|------|---------|----------|
| Old comment text in plan | YES: "Returns true iff the current dispatch reverses the last direction AND the follower is flat." | Matches source L6517 exactly |
| New comment text in plan | YES: "Returns true iff the current dispatch reverses the last direction AND the follower is NOT flat (open position + reversal = protection path)." | Accurate per method logic |

PASS.

### 3.4 ASCII-Only Check (T2 new text)

Plan Section 4.4 states: "ASCII-only: new comment text uses only ASCII characters (0x20-0x7E). PASS."

Independent verification: the new comment text contains only printable ASCII characters. No Unicode, curly quotes, em-dashes, or non-ASCII characters detected. ASCII-001 compliant. PASS.

---

## 4. Constraints Compliance (DNA Block)

### 4.1 lock() — JS-021 (P0)

Plan Section 3.7 explicitly confirms: all operations in `IsLeaderFlatDrainAbort` are lock-free via `ConcurrentDictionary`. No new code introduced by T1 or T2. Source lines 1950-1974 and 8343-8348 contain no `lock()` calls. PASS.

### 4.2 throw in hot paths — JS-001 (P0)

No `throw` statements in `IsLeaderFlatDrainAbort`, `TryEvictFollowerBeSlot`, or `AbortDrainOnFill`. Source confirms early-return guards at L1952, L1954, L1956. PASS.

### 4.3 UI update from off-thread — JS-023 (P0)

Plan Section 3.7 confirms: "No Dispatcher.InvokeAsync needed (no UI updates)." `NinjaTrader.Code.Output.Process()` is NT8-safe from any thread. T2 is annotation-only. PASS.

### 4.4 async/await in lifecycle methods — NT8-001 (P0)

No async/await introduced. T1 = verification-only. T2 = comment-only. PASS.

### 4.5 Account.All in constructor — NT8-002 (P0)

Not applicable. No constructor changes. PASS.

### 4.6 DateTime.Now — NT8-006 (P1)

No new executable code. N/A. PASS.

### 4.7 CreateOrder without PTT- prefix — NT8-003 (P0)

No new `CreateOrder` calls. N/A. PASS.

### 4.8 CYC <= 8 — JS-013 (P1)

Plan Section 3.8 CYC table:

| Method | Plan CYC | Source Comment CYC | Independent Count | Status |
|--------|----------|-------------------|-------------------|--------|
| IsLeaderFlatDrainAbort | 8 | 8 (L1947) | 8 (base+IsFollowerAccount+OrderState+IsFlat+foreach+if+?.+abortCount) | AT LIMIT -- no changes |
| TryEvictFollowerBeSlot | 7 | 7 (L1926) | 7 (base+?.+null+IsEvictTriggerState+?.+??+IsFollowerAccount) | PASS |
| AbortDrainOnFill | 3 | 3 (L8342) | 3 (base+TryRemove+foreach) | PASS |

All methods CYC <= 8. No new methods introduced. PASS.

**Note (informational, not a plan violation)**: Source file has a stale top-level comment for `TryEvictFollowerBeSlot` at L1904 stating `CYC=6`. The updated CYC comment at L1926 (added by DW-B156-DEFER-1 decomposition) correctly states `CYC=7`. The plan correctly uses CYC=7. The L1904 stale comment is a pre-existing source inconsistency -- not a plan defect. Recommend the engineer note this in the completion report for future cleanup.

### 4.9 Magic strings for discriminated state — JS-003 (P0)

No new state discriminators introduced. Log markers are diagnostic strings, not state discriminators. PASS.

### 4.10 ConcurrentDictionary for shared state — JS-009/JS-025 (P1)

Plan confirms `_pendingDispatchDrains` is `ConcurrentDictionary<string, PendingDispatchDrain>` with `StringComparer.Ordinal`. Source confirmed at L387-388. PASS.

---

## 5. Spec Coverage Matrix

| Requirement | Source | Addressed? | Plan Section |
|-------------|--------|------------|--------------|
| DW-B156-01: Leader-flat drain abort (ghost re-entry fix) | 06-deferred-backlog.md B156 | YES -- T1 SIM gate closes this if PASS | §3, §5 |
| DW-B157-COMMENT-01: Stale comment at IsReversalToFlatFollower | 06-deferred-backlog.md B157 | YES -- T2 comment correction at L6517 | §4 |
| Fix3 = IsLeaderFlatDrainAbort (already implemented) | B156/B157 pipeline history | YES -- verified in source | §3.1, §8 |
| Correct log marker [DRAIN-ABORT-LEADER-FLAT] | DW-B156-01 spec | YES -- documented and SIM step verifies | §5.2, §7 |
| SIM FAIL escalation path | Standard protocol | YES -- DW-B158-DRAIN-RACE-01 defect card path | §3.5, §7 |
| Deferred items carried forward | Standard protocol | YES -- 4 deferred items listed in §7 Risk 4 | §7 |
| NT8 API compliance | RULES_CATALOG.md | YES -- no new API calls; banned APIs explicitly listed | §7 Risk 5 |

All spec requirements addressed. PASS.

---

## 6. 7-Scan Checklist Verification

Both T1 and T2 have complete 7-scan tables in Section 6 of the plan.

### T1 Scan Checklist

| Scan | Present | Command Given | Required Result | Assessment |
|------|---------|--------------|-----------------|------------|
| SCAN-01 | YES | PowerShell byte scan | Count=0 | PASS |
| SCAN-02 | YES | `Select-String lock\s*\(` | 0 hits | PASS |
| SCAN-03 | YES | `Select-String DateTime\.Now` | 0 hits | PASS |
| SCAN-04 | YES | N/A (no methods modified) | IsLeaderFlatDrainAbort CYC=8 pre-existing | PASS |
| SCAN-05 | YES | `Select-String async void` | 0 hits | PASS |
| SCAN-06 | YES | `dotnet test` with specific test name | LogMarkerIsExactString must PASS | PASS |
| SCAN-07 | YES | Empty diff = zero code changes | Build PASS | PASS |

### T2 Scan Checklist

| Scan | Present | Command Given | Required Result | Assessment |
|------|---------|--------------|-----------------|------------|
| SCAN-01 | YES | PowerShell byte scan | Count=0 (ASCII-only new text) | PASS |
| SCAN-02 | YES | `Select-String lock\s*\(` | 0 hits | PASS |
| SCAN-03 | YES | `Select-String DateTime\.Now` | 0 hits | PASS |
| SCAN-04 | YES | Verify CYC comment unchanged | CYC=2 for IsReversalToFlatFollower | PASS |
| SCAN-05 | YES | N/A for comment change | N/A | PASS |
| SCAN-06 | YES | `dotnet test` | All IsReversalToFlatFollower tests PASS | PASS |
| SCAN-07 | YES | `git diff CopyEngine.cs` | ONLY L6517 changed | PASS |

All 7 scans present for both tickets with specific commands and required results. PASS.

---

## 7. Risk and Dependency Section

Section 7 of the plan documents 5 risks:

| Risk | Present | Adequate | Notes |
|------|---------|----------|-------|
| Risk 1: SIM FAIL / timing race | YES | YES | Escalation path with defect card ID documented |
| Risk 2: IsLeaderFlatDrainAbort CYC=8 at limit | YES | YES | No-change note correct |
| Risk 3: T2 line drift | YES | YES | Acknowledged T1 is verification-only so drift risk is minimal |
| Risk 4: Deferred items not addressed | YES | YES | 4 OPEN items correctly carried forward |
| Risk 5: NT8 API compliance | YES | YES | Banned APIs listed and confirmed absent |

PASS.

---

## 8. Output File Path Compliance

Plan output path stated: `docs/brain/B158/02-architecture-plan.md`. Correct for B158.
This review output: `docs/brain/B158/02-plan-review.md`. Correct.

PASS.

---

## 9. Violation Summary

| Rule ID | Severity | Description | Location in Plan | Result |
|---------|----------|-------------|------------------|--------|
| (none) | -- | No violations found | -- | -- |

---

## 10. Final Decision

**REVIEW_PASS**

The B158 architecture plan satisfies all requirements:
- LANES-APPROVED gate correctly structured and conditions verified against source
- T1 Fix3 implementation claim is source-evidenced and confirmed
- T1 is explicitly verification-only with a documented SIM FAIL escalation path
- Hook point rationale (TryEvictFollowerBeSlot pre-Gate-1) is correct and explained
- _pendingDispatchDrains key format confirmed (follower acc.Name, StringComparer.Ordinal)
- SIM steps complete (5 steps, PASS/FAIL criteria, timing-race scenario)
- SIM marker discrepancy ([DRAIN-ABORT-FLAT] vs [DRAIN-ABORT-LEADER-FLAT]) documented
- T2 comment location confirmed (L6517), old/new text stated, zero logic impact
- All CYC values <= 8 (confirmed independently against source)
- ASCII-001 compliant (no non-ASCII in new content)
- JS-021 compliant (lock-free throughout)
- 7-scan checklist present for both tickets with specific commands
- Risk/dependency section complete
- Deferred items carried forward

Plan is approved for Phase 3 (ticket generation).

---

_Review authored by ptt-plan-reviewer (Phase 2). Block B158._
_Source lines verified: CopyEngine.cs L387-388, L1712, L1763, L1767, L1904, L1926-1974, L6516-6527, L8342-8348._
_Test verified: CopyEngineTests.cs L9481 (TryEvictFollowerBeSlot_LeaderFlatDrainAbort_LogMarkerIsExactString present)._
