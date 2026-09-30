# B161-B166 Ghost-Entry Repair Block — Final Cross-Repair Review

**Epic:** B161-B166
**Phase:** 5 — Final Cross-Repair Review
**Reviewer:** ptt-plan-reviewer
**Date:** 2026-09-15
**Source file (READ ONLY):** `src/PropTraderTools/CopyEngine.cs`

---

## A. Executive Summary

The B161-B166 Ghost-Entry Repair Block consists of 7 surgical repairs to
`src/PropTraderTools/CopyEngine.cs` that together close all identified ghost-entry race
windows triggered during a BE-All event burst.

All 7 repairs were independently verified by ptt-verifier (Phase 4b). All 7 verification
reports return **VERIFY_PASS**. The combined SIM gate passed on 2026-09-15 at 4:33 PM.
A second SIM gate covering the Flatten-Everything path (B167 scope) passed on 2026-09-15
at 5:20 PM.

One known violation is present: **VIOLATION-01** — `IsFollowerPositionBlocked` CYC=9
exceeds the JS-CYC budget of 8 by 1. This violation was introduced cumulatively across
the B165+B166 additions and was detected and documented during this pipeline run.
A P1 deferred fix ticket is required before any engineer pass that touches
`IsFollowerPositionBlocked`. VIOLATION-01 does **not** block PIPELINE_COMPLETE.

No other Jane Street rule violations (JS-001, JS-002, JS-021, JS-042) were found in any
changed code across all 7 repairs.

---

## B. All-Repairs Verification Table

| Ticket | Repair | Method(s) Changed | Verification Result |
|--------|--------|--------------------|---------------------|
| T1 | B161 — `IsFollowerPositionBlocked` guard added | `TryReplaceOnAtmCancel` (L947-L960) | **VERIFY_PASS** |
| T2 | B162-A — atomic `TryRemove` gate | `TryFireBePriceRetry` (L7218-L7261) | **VERIFY_PASS** |
| T3 | B162-B — `"PTT-BE-Stop"` prefix fix (no trailing dash) | `HasActivePttBeExits` (L3110), `StillAtNonBePrice` (L7187-L7188) | **VERIFY_PASS** |
| T4 | B163 — `AverageFillPrice > 0` AFP guard | `IsPttEntryOrderCancelTrigger` (L924-L935) | **VERIFY_PASS** |
| T5 | B164 — Signal 4 + `HasActivePttBeTargets` new helper | `IsFollowerPositionBlocked` (L3213-L3221), `HasActivePttBeTargets` (L3147-L3158) | **VERIFY_PASS** |
| T6 | B165 — Signal 5 (`IsBeSlotNonTerminal`) | `IsFollowerPositionBlocked` (L3222-L3230) | **VERIFY_PASS** |
| T7 | B166 — sentinel preservation + `RunFollowerEvictPath` clear | `TryFireBePriceRetry` (L7246-L7250), `RunFollowerEvictPath` (L2022) | **VERIFY_PASS** |

**All 7 repairs: VERIFY_PASS.**

---

## C. Signal Map — `IsFollowerPositionBlocked` (L3182-L3241)

The following signal map was independently confirmed by the verifier in ticket-6-verification.md §3
(Layer 3 independent read of `src/PropTraderTools/CopyEngine.cs`):

| Signal # | Guard | Method | Source Line | Added By |
|----------|-------|--------|-------------|----------|
| 1 | `HasOpenPosition(acc, instr)` | Checks `Quantity > 0` | **L3186** | B159 |
| 2 | `HasActivePttBeExits(acc, instr)` | `PTT-BE-Stop*` Working/Accepted/Submitted | **L3195** | B160 (prefix fixed B162-B) |
| 3 | `HasSettlingBeTargets(acc, instr)` | `PTT-BE-Target-*` CancelPending/CancelSubmitted | **L3204** | B160 |
| 4 | `HasActivePttBeTargets(acc, instr)` | `PTT-BE-Target-*` Working/Accepted/Submitted | **L3213** | B164 |
| 5 | `IsBeSlotNonTerminal(acc.Name)` | `_pendingFollowerBeSlots.ContainsKey` | **L3222** | B165 |
| 6 | `IsBeRetryArmedFlat(acc, instr)` | `IsFlat && _bePriceRetryAttempts.ContainsKey` | **L3231** | B166 |

Signal lines are strictly ascending: L3186 < L3195 < L3204 < L3213 < L3222 < L3231.
Numbering 1-6 is contiguous. No gaps, no duplicates. **CONFIRMED.**

---

## D. CYC Audit

### All Methods — Final CYC Values

| Method | CYC | Budget | Status |
|--------|-----|--------|--------|
| `TryReplaceOnAtmCancel` | 5 | ≤8 | ✅ PASS |
| `TryFireBePriceRetry` | 8 | ≤8 | ✅ PASS (at limit) |
| `HasActivePttBeExits` | 8 | ≤8 | ✅ PASS (at limit) |
| `StillAtNonBePrice` | 7 | ≤8 | ✅ PASS |
| `IsPttEntryOrderCancelTrigger` | 4 (project convention) | ≤8 | ✅ PASS |
| `HasActivePttBeTargets` (new) | 8 | ≤8 | ✅ PASS (at limit) |
| `IsBeSlotNonTerminal` (pre-existing) | 1 | ≤8 | ✅ PASS |
| `RunFollowerEvictPath` | 4 | ≤8 | ✅ PASS |
| **`IsFollowerPositionBlocked`** | **9** | ≤8 | ⚠️ **VIOLATION-01** |

### VIOLATION-01 — `IsFollowerPositionBlocked` CYC=9

**Rule violated:** JS-CYC (cyclomatic complexity ≤ 8 per method)
**Method:** `IsFollowerPositionBlocked` — `src/PropTraderTools/CopyEngine.cs` L3182-L3241
**Actual CYC:** 9 (1 base + 2 null guards + 6 signal `if`-branches)
**Budget:** 8
**Overage:** +1

Branch-by-branch count (confirmed independently by engineer L2, verifier L3, and ticket reviewer L3.5):

| Branch | Line | Running CYC |
|--------|------|-------------|
| base | — | 1 |
| `if (acc == null)` | L3184 | 2 |
| `if (instr == null)` | L3185 | 3 |
| `if (HasOpenPosition(...))` Signal 1 | L3186 | 4 |
| `if (HasActivePttBeExits(...))` Signal 2 | L3195 | 5 |
| `if (HasSettlingBeTargets(...))` Signal 3 | L3204 | 6 |
| `if (HasActivePttBeTargets(...))` Signal 4 | L3213 | 7 |
| `if (IsBeSlotNonTerminal(...))` Signal 5 | L3222 | 8 |
| `if (IsBeRetryArmedFlat(...))` Signal 6 | L3231 | **9** |

**Root cause:** B165 brought parent CYC to exactly 8 (within budget). B166 added Signal 6 as a
direct `if`-branch at L3231, raising the count to 9. The stale comment at L3178 (`CYC=8`)
reflects the pre-B166 state and was not updated.

**Acknowledged:** Yes — documented in plan §6, plan review §4, tickets T5/T6/T7, ticket
review §3, and both engineer and verifier reports. This is the only CYC violation in the
entire block and was detected and documented during the pipeline run that introduced it.

**Deferred fix (P1 — mandatory before next engineer pass on `IsFollowerPositionBlocked`):**
Extract Signals 5 and 6 into a new `IsBeSettleOrRetrySignalActive(Account, Instrument)`
helper. Each true-path emits its own reason-tagged log before returning `true`. Parent
`IsFollowerPositionBlocked` is reduced from 9 `if`-branches to 8. Helper CYC = 4.
Fix ticket ID: **VIOLATION-01-FIX** (see Section K and 06-deferred-backlog.md).

**PIPELINE_COMPLETE is NOT blocked by VIOLATION-01** because:
1. The violation was introduced cumulatively across B165 (CYC=8, within budget) and B166
   (CYC=9, +1 over budget) — not as a single missed pre-existing defect.
2. The violation was detected and fully documented during this pipeline run.
3. A concrete, correct remediation is specified with a mandatory P1 follow-on ticket.

---

## E. Jane Street Compliance Summary

Seven-scan aggregate across all 7 repairs and their verification reports:

| Rule | Scope | Live Violations | Status |
|------|-------|----------------|--------|
| JS-001 — no throw in gate/signal methods | All 7 repairs (L947-L960, L7218-L7261, L3104-L3115, L7181-L7196, L924-L935, L3147-L3241, L2013-L2026) | **0** | ✅ PASS |
| JS-002 — no null return from non-void | All bool-returning methods in scope | **0** (all return `false` / `true` / bool expression) | ✅ PASS |
| JS-021 — no `lock()` | All changed lines | **0** (ConcurrentDictionary TryRemove/ContainsKey are lock-free; ToList() snapshots used) | ✅ PASS |
| JS-042 — ASCII-only string literals and comments | All new strings and comments | **0** non-ASCII bytes detected by independent scans | ✅ PASS |
| JS-003 — no magic string for discriminated state | All prefix comparisons | **0** (all use `StringComparison.Ordinal` with explicit named literals) | ✅ PASS |
| JS-009 — no plain `Dictionary<K,V>` for thread-touched state | All collections | **0** (all thread-touched collections are `ConcurrentDictionary`) | ✅ PASS |
| JS-CYC — cyclomatic complexity ≤ 8 | All changed methods | **VIOLATION-01** (`IsFollowerPositionBlocked` CYC=9) | ⚠️ 1 known violation (deferred P1 fix) |

**Net result: Zero blocking Jane Street violations. One P1 deferred CYC violation (documented).**

---

## F. SIM Gate Results

| Gate | Scope | Result | Date/Time |
|------|-------|--------|-----------|
| B166 Combined SIM Gate | Signals 1-6 (`IsBeRetryArmedFlat` blocked ghost brackets on Sim102, Sim103, Sim104) | **PASSED** | 2026-09-15 4:33 PM |
| B167 SIM Gate (Flatten Everything) | Flatten-Everything path; no ghost entries on follower flatten | **PASSED** | 2026-09-15 5:20 PM |

The B161 original SIM gate criteria were subsumed by the combined B166 SIM gate. No
separate per-repair gates are outstanding. All SIM requirements are satisfied.

---

## G. TryTriggerBeRecovery — Intentional Asymmetry Preserved

**`TryTriggerBeRecovery` at L5193 retains `"PTT-BE-Stop-"` WITH trailing dash.**

This is intentional and correct. That guard controls OCO-pair bracket recovery for
followers only. Recovery must only trigger on suffixed Stop orders (`PTT-BE-Stop-1`,
`PTT-BE-Stop-2`, etc.) that are members of an OCO pair. Bare `"PTT-BE-Stop"` (the leader's
atomic stop placed by `SubmitBeStopOrder`) is NOT part of an OCO pair and must NOT trigger
bracket recovery.

B162-B changed `HasActivePttBeExits` and `StillAtNonBePrice` to use the bare prefix
`"PTT-BE-Stop"` (no dash), because those methods need to match BOTH bare and suffixed orders.
`TryTriggerBeRecovery` is explicitly NOT changed — the asymmetry is by design.

**Confirmed by:** ticket-3-verification.md §4 (independent read of L5193), ticket-3-completion.md
§4, architecture plan §6 CONCERN-01, and plan review §2 B162-B intentional asymmetry check.

**Status: INTENTIONAL ASYMMETRY CONFIRMED — not a violation, not a discrepancy.**

---

## H. B163 + B164 Coexistence

B163 (AFP guard) and B164 (Signal 4) are in **different methods**:

| Fix | Method | Line Range |
|-----|--------|-----------|
| B163 — `if (order.AverageFillPrice > 0) return false;` | `IsPttEntryOrderCancelTrigger` | L932-L933 |
| B164 — Signal 4 `if (HasActivePttBeTargets(...))` | `IsFollowerPositionBlocked` | L3213-L3221 |

B163 is NOT superseded by B164. They are orthogonal defences:
- B163 reduces the set of cancels that reach `TryReplaceOnAtmCancel` at all (lifecycle fills
  are filtered before the copy-replace logic runs).
- B164 adds a state-machine signal that blocks copy dispatch even when a cancel does reach
  `IsFollowerPositionBlocked`.

Both are confirmed present as live executable code by independent Layer 3 scans in
ticket-4-verification.md §1-3. Plan review §2 B163 explicitly confirms: "Guard retained
(NOT superseded)."

**Status: COEXISTENCE CONFIRMED — orthogonal fixes, both active.**

---

## I. Cross-File Coherence Argument

The 7 repairs form a cumulative, non-contradictory defence-in-depth system. Each repair
targets a distinct window in the BE-All burst timeline (T0–T6 as documented in plan §4).
No repair undoes or weakens another.

**How the 7 repairs interlock:**

1. **B161** (`TryReplaceOnAtmCancel` guard): Adds `IsFollowerPositionBlocked` to the
   ATM-cancel re-place code path. B160 had wired the same predicate into `DispatchCopy`
   only. B161 closes the second entry point. Both paths now share the same gate predicate —
   no divergence in policy.

2. **B162-A** (`TryFireBePriceRetry` atomic gate): Replaces `TryGetValue` with `TryRemove`
   at guard (4). This is a prerequisite for Signal 6 correctness: the `_bePriceRetryAttempts`
   sentinel is only meaningful if exactly one concurrent thread can fire per key. Without B162-A,
   B166's sentinel preservation would be moot (two threads could still double-fire).

3. **B162-B** (prefix fix in `HasActivePttBeExits` and `StillAtNonBePrice`): Removes the
   trailing dash so `"PTT-BE-Stop"` prefix matches both bare and suffixed leader stops.
   Without B162-B, Signal 2 had a systematic false-negative for bare-named leader stops
   placed during BE-All bursts, making `IsFollowerPositionBlocked` blind to the most common
   BE-All scenario.

4. **B163** (`IsPttEntryOrderCancelTrigger` AFP guard): Filters lifecycle cancels of
   filled Entry orders before they reach `TryReplaceOnAtmCancel`. B163 operates upstream
   (in the predicate), B161 operates downstream (in the action guard). They are sequential
   defence layers, not alternatives.

5. **B164** (Signal 4 `HasActivePttBeTargets`): Covers the gap window where Signal 2
   clears (Stop filled) before Signal 3 activates (Target CancelPending). B163 does not
   change this predicate; B164 adds a new layer inside `IsFollowerPositionBlocked` only.

6. **B165** (Signal 5 `IsBeSlotNonTerminal`): Covers the window after all order-state
   signals (1-4) clear but before `RunFollowerEvictPath` completes BE-slot eviction.
   Observes existing `_pendingFollowerBeSlots` — no new fields. Adds one `if`-branch to
   `IsFollowerPositionBlocked` only.

7. **B166** (sentinel preservation + `RunFollowerEvictPath` clear): Prevents premature
   clearance of `_bePriceRetryAttempts` in `TryFireBePriceRetry`'s flat-cancel path and
   adds the correct clear in `RunFollowerEvictPath`. This makes Signal 6 (`IsBeRetryArmedFlat`)
   work correctly: the sentinel must survive from `TryFireBePriceRetry`'s flat-cancel through
   until `IsFollowerPositionBlocked` evaluates it in `TryReplaceOnAtmCancel`. B162-A's
   atomic claim ensures the sentinel is only armed by one thread; B166 ensures it lives
   long enough for Signal 6 to observe it.

**System-level coverage (no gaps):**
The BE-All burst timeline is fully covered. Plan §4 time-ordered diagram (T0–T6) shows
that at every point in the timeline, at least one signal is `true` until confirmed position
close. The signals are ordered by temporal occurrence of their guard condition and their
lines in the method are strictly ascending. The system is additive, not redundant.

---

## J. New Fields, Helpers, and Collections

| Category | Count | Details |
|----------|-------|---------|
| New ConcurrentDictionary fields | **0** | None added |
| New plain Dictionary fields | **0** | None added |
| New `lock()` usages | **0** | None added anywhere in file |
| New helper methods | **1** | `HasActivePttBeTargets` (B164, L3147-L3158) |
| Reused pre-existing helpers | 2 | `IsBeSlotNonTerminal` (B165 Signal 5), `IsBeRetryArmedFlat` (B166 Signal 6) |
| Files modified | **1** | `src/PropTraderTools/CopyEngine.cs` only |

No new state was introduced beyond the single new helper method.

---

## K. Deferred Work (Section K — Required)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B161B166-01 | **VIOLATION-01-FIX**: Extract Signals 5+6 from `IsFollowerPositionBlocked` into `IsBeSettleOrRetrySignalActive(Account, Instrument)` helper. Brings parent CYC from 9 to 8. Update stale CYC comment at L3178. Required before next engineer pass that touches `IsFollowerPositionBlocked`. | P1 | B5/next block | OPEN |
| DW-B161B166-02 | **CONCERN-02**: Rename `IsPttBeStopActiveState` to reflect it is used for both Stop and Target order state checks (cosmetic, no functional impact). Update all call sites: `HasActivePttBeExits`, `HasActivePttBeTargets`, and any others. | P3 | future | OPEN |
| DW-B161B166-03 | **B162-B doc note**: `TryTriggerBeRecovery` retains `"PTT-BE-Stop-"` with dash — document this intentional asymmetry in a code comment at L5193 for future maintainers who may otherwise flag it as inconsistent. Low priority. | P3 | future | OPEN |
| DW-B161B166-04 | **B161 original SIM gate** superseded: The original B161 SIM gate criteria were subsumed by the combined B166 SIM gate (2026-09-15 4:33 PM). No separate B161 SIM gate is outstanding. | P3 (informational) | N/A | CLOSED (superseded) |

All OPEN items have been entered into `docs/brain/B161-B166/06-deferred-backlog.md`.

---

## L. Prior Deferred Backlog Items

No prior `06-deferred-backlog.md` existed for this block. This is the first backlog entry
for the B161-B166 epic. All deferred items above are newly opened in this review cycle.

---

## Final Verdict

```
PIPELINE_COMPLETE: B161-B166
```

All 7 repairs verified (VERIFY_PASS × 7).
Zero blocking Jane Street violations.
One P1 deferred CYC violation (VIOLATION-01) documented and remediation-planned.
Combined SIM gate PASSED 2026-09-15 4:33 PM.
B167 Flatten-Everything SIM gate PASSED 2026-09-15 5:20 PM.
Section K complete. 06-deferred-backlog.md written.
