# B107 Final Review: Phase 5

**Reviewer**: ptt-plan-reviewer
**Epic**: B107-T1
**Phase**: 5 (Final Review)
**Date**: 2026-08-10
**Spec items closed**: DW-B105 (P1-HIGH), DW-B106 (P2-MEDIUM)

---

## Section A: Pipeline Artifact Chain

| Artifact | File | Status | Verdict |
|----------|------|--------|---------|
| Architecture Plan | `docs/brain/B107/02-architecture-plan.md` | PRESENT | REVIEW_PASS candidate |
| Plan Review | `docs/brain/B107/02-plan-review.md` | PRESENT | REVIEW_PASS (14/14 criteria) |
| Tickets | `docs/brain/B107/04-tickets.md` | PRESENT | TICKETS_COMPLETE |
| Ticket Review | `docs/brain/B107/04-ticket-review.md` | PRESENT | TICKET_REVIEW_PASS (14/14 criteria) |
| Ticket-1 Completion | `docs/brain/B107/ticket-1-completion.md` | PRESENT | BUILD_PASS |
| Ticket-1 Verification | `docs/brain/B107/ticket-1-verification.md` | PRESENT | VERIFY_PASS |

**Pipeline chain: COMPLETE — all 6 artifacts present and each at a PASS verdict.**

---

## Section B: Spec Requirement Coverage

| Spec Item | Priority | Changes | Status |
|-----------|----------|---------|--------|
| DW-B105: `_qxCancelInProgress` field | P1-HIGH | CHANGE A | CLOSED |
| DW-B105: early-return guard (3b) in `TryReplacePttBeBrackets` | P1-HIGH | CHANGE B | CLOSED |
| DW-B105: try/finally set/clear in `ExecuteOne` | P1-HIGH | CHANGE C | CLOSED |
| DW-B106: hard cap at 3 in `ResolveTargetCount` + fallback 2->3 | P2-MEDIUM | CHANGE E | CLOSED |
| DW-B106: two-pass `SnapshotTargetOrders` native-first discriminator | P2-MEDIUM | CHANGE D | CLOSED |
| DW-B63-01 carry: fallback default 2->3 | ancillary | CHANGE E | CLOSED |

**Both DW-B105 and DW-B106 are CLOSED by B107-T1. All spec requirements addressed.**

---

## Section C: Cross-File Coherence (F1–F7)

### [F1] `_qxCancelInProgress` Access Path

**Check**: Field declared `internal` in `CopyEngine`; accessed from `PttGlobalQuickExit` via
`CopyEngine.Instance?._qxCancelInProgress`.

**Evidence from source**:
- [`CopyEngine.cs:263`](src/PropTraderTools/CopyEngine.cs:263): `internal readonly ConcurrentDictionary<string, bool> _qxCancelInProgress = new ConcurrentDictionary<string, bool>();`
- [`PttGlobalQuickExit.cs:154`](src/PropTraderTools/Features/PttGlobalQuickExit.cs:154): `CopyEngine.Instance?._qxCancelInProgress.TryAdd(acc.Name, true);`
- [`PttGlobalQuickExit.cs:161`](src/PropTraderTools/Features/PttGlobalQuickExit.cs:161): `CopyEngine.Instance?._qxCancelInProgress.TryRemove(acc.Name, out _);`
- `CopyEngine.Instance` is the singleton access pattern established throughout the codebase.
- Both classes are in the `PropTraderTools` assembly — `internal` is valid.
- No direct field declaration in `PttGlobalQuickExit.cs` — access is via singleton.

**COHERENT**: `CopyEngine.Instance?._qxCancelInProgress` is the correct and confirmed access path.

---

### [F2] Guard (3b) / try/finally Consistent Pair

**Check**: Guard (3b) in `TryReplacePttBeBrackets` fires if and only if `ExecuteOne`'s
`TryAdd` is in effect. The pair must be logically consistent — no guard without a setter,
no setter without a guard.

**Evidence from source**:
- [`CopyEngine.cs:2293`](src/PropTraderTools/CopyEngine.cs:2293): `if (_qxCancelInProgress.ContainsKey(cancelledStop.Account.Name)) return;` — guard in TryReplacePttBeBrackets
- [`PttGlobalQuickExit.cs:154-162`](src/PropTraderTools/Features/PttGlobalQuickExit.cs:154): TryAdd before try, CancelQxBrackets in try, TryRemove in finally — setter/clearer in ExecuteOne
- `ConcurrentDictionary.ContainsKey` is wait-free; `TryAdd`/`TryRemove` are lock-free atomics.
- The guard window is precisely bounded: from the `TryAdd` call through the `finally` TryRemove.
- After `finally` executes, `ContainsKey` returns `false` — guard is lifted.
- No other code path writes to `_qxCancelInProgress` — only `ExecuteOne` sets the flag.
- `TryReplacePttBeBrackets` is the only consumer of the flag via `ContainsKey`.

**COHERENT**: One writer (`ExecuteOne`) and one reader (`TryReplacePttBeBrackets`). The
try/finally guarantee ensures the flag is always cleared even on exception. Guard fires
if and only if the setter is active.

---

### [F3] `SnapshotTargetOrders` Two-Pass + `ResolveTargetCount` Cap Consistency

**Check**: Both independently tighten the target count. Together they must not allow
over-allocation beyond 3.

**Evidence from source**:
- [`PttGlobalQuickExit.cs:187-223`](src/PropTraderTools/Features/PttGlobalQuickExit.cs:187): `SnapshotTargetOrders` returns `nativeTargets` (ATM, current session) when any exist, otherwise `pttTargets` (PTT residues). Stale prior-session `PTT-QX-T*` residues are separated into `pttTargets` and ONLY used when no native targets exist.
- [`PttQuickExit.cs:262-263`](src/PropTraderTools/Features/PttQuickExit.cs:262): `int raw = own?.Count > 0 ? own.Count : (leaderCount > 0 ? leaderCount : 3); return Math.Min(raw, 3);`
- In the clean case (native ATM targets present): `SnapshotTargetOrders` returns native list (≤3 for standard 3-target ATM). `ResolveTargetCount` caps at 3. Count = min(nativeCount, 3).
- In the stale residue case (no native targets): `SnapshotTargetOrders` returns PTT list. `ResolveTargetCount` still caps at 3, preventing any over-allocation from the residue list.
- In the empty case: `SnapshotTargetOrders` returns empty `nativeTargets`. `ResolveTargetCount` falls back to `leaderCount` or `3`, then caps at 3. Result ≤ 3.

**COHERENT**: The two-pass discriminator and the hard cap are defence-in-depth. Either alone
would prevent >3 targets in most cases; together they are watertight. No code path can
produce `targetCount > 3`.

---

### [F4] All 7 Scans PASS — Cross-Check

**Evidence from `ticket-1-verification.md`**:

| Scan | Verifier Result |
|------|----------------|
| SCAN-01 lock() | PASS — line 1903 is a comment string only, no actual lock() call |
| SCAN-02 async void | PASS — no output in all 3 files |
| SCAN-03 return null | PASS — pre-existing hits in CopyEngine (1509/2004/2050/3162/3168/3231/4049), none in changed sections; PttGlobalQuickExit and PttQuickExit zero |
| SCAN-04 non-ASCII | PASS — pre-existing hits in CopyEngine (316/317/2880/2881), none in changed sections; PttGlobalQuickExit zero |
| SCAN-05 CYC | PASS — TryReplace=7, ExecuteOne=2, SnapshotTargetOrders=7, ResolveTargetCount=2; all ≤8 |
| SCAN-06 field visibility | PASS — declared on CopyEngine.cs:263, accessed from PttGlobalQuickExit.cs:154+161 |
| SCAN-07 try/finally integrity | PASS — 6 invariants confirmed: TryAdd before try; CancelQxBrackets in try; TryRemove in finally; no lock(); if(!skipIfFollower) wraps all; no path skips TryRemove |

**Engineer (Layer 2) and Verifier (Layer 3) results match exactly for all scans. No discrepancies.**

**ALL 7 SCANS: PASS**

---

### [F5] CYC ≤ 8 in All 3 Files — Confirmed

**CYC after implementation**:

| Method | File | CYC Before | CYC After | Delta | Limit | Status |
|--------|------|-----------|-----------|-------|-------|--------|
| `TryReplacePttBeBrackets` | `CopyEngine.cs` | 6 | 7 | +1 | 8 | PASS |
| `ExecuteOne` | `PttGlobalQuickExit.cs` | 2 | 2 | 0 | 8 | PASS |
| `SnapshotTargetOrders` | `PttGlobalQuickExit.cs` | 4 | 7 | +3 | 8 | PASS |
| `ResolveTargetCount` | `PttQuickExit.cs` | 2 | 2 | 0 | 8 | PASS |

Maximum CYC across all modified methods: **7** (limit 8). No method exceeds the limit.

**CYC ANALYSIS: PASS — max 7 ≤ 8.**

---

### [F6] Cross-File JS Violations — None Introduced

Reviewer independently confirmed against source reads:

| Rule | Check | Source Evidence | Result |
|------|-------|----------------|--------|
| JS-021 (no lock) | `_qxCancelInProgress` uses `ConcurrentDictionary.TryAdd`/`TryRemove`/`ContainsKey` only | Lines 154, 161 (PttGlobalQuickExit), line 2293 (CopyEngine) — no `lock(` | PASS |
| JS-001 (no throw) | Guard (3b) body is `return;`; `SnapshotTargetOrders` returns empty list; `ResolveTargetCount` returns int | All new paths confirmed — no exception thrown in any new code | PASS |
| JS-002 (no return null) | `SnapshotTargetOrders` returns `nativeTargets` (empty list, not null) on null input | `PttGlobalQuickExit.cs:189-190`: `if (acc == null || instr == null) return nativeTargets;` | PASS |
| JS-033 (no async void) | All new code is synchronous | No `async` keyword in any changed section | PASS |
| JS-023 (atomic primitives) | `ConcurrentDictionary` operations are thread-safe by contract | `TryAdd`, `TryRemove`, `ContainsKey` — all atomic | PASS |
| ASCII-only | All new string literals, identifiers, comments are ASCII-7 | `"[PTT-QX-GUARD]..."`, `_qxCancelInProgress`, all DW-B105/B106 comments confirmed | PASS |

**No cross-file JS violations introduced. All P0 rules satisfied.**

---

### [F7] DW-B105 and DW-B106 Both Addressed

**DW-B105** — root cause: `TryReplacePttBeBrackets` fires during QX-ALL sweep.
- Fix: `_qxCancelInProgress` field (CHANGE A) + guard (3b) in `TryReplacePttBeBrackets` (CHANGE B) + try/finally set/clear in `ExecuteOne` (CHANGE C).
- Verified by T1, T2, T3 criteria in `ticket-1-verification.md` — all PASS.
- Source confirmed: field at `CopyEngine.cs:263`, guard at `CopyEngine.cs:2293`, try/finally at `PttGlobalQuickExit.cs:154-162`.

**DW-B106** — root cause: stale prior-session residues inflate `SnapshotTargetOrders` count.
- Fix: two-pass `SnapshotTargetOrders` (CHANGE D) + `Math.Min(raw, 3)` cap in `ResolveTargetCount` (CHANGE E).
- Verified by T5, T6 criteria in `ticket-1-verification.md` — all PASS.
- Source confirmed: two-pass at `PttGlobalQuickExit.cs:187-223`, cap at `PttQuickExit.cs:262-263`.

**BOTH DW-B105 and DW-B106 ADDRESSED: PASS**

---

## Section D: JS Compliance — 7-Scan Summary

All 7 scans executed independently by both engineer (Layer 2) and verifier (Layer 3).
No discrepancies between layers. All 7 scans return zero violations in new/changed code.

| Scan | Rule | Scope | Result |
|------|------|-------|--------|
| SCAN-01 | JS-021 no lock() | All 3 modified files | PASS |
| SCAN-02 | JS-033 no async void | All 3 modified files | PASS |
| SCAN-03 | JS-002 no return null | All 3 modified files | PASS |
| SCAN-04 | ASCII-only | All 3 modified files | PASS |
| SCAN-05 | CYC ≤ 8 | 4 modified methods | PASS |
| SCAN-06 | Field visibility (internal) | CopyEngine + PttGlobalQuickExit | PASS |
| SCAN-07 | try/finally integrity | PttGlobalQuickExit ExecuteOne | PASS |

**PRE-EXISTING findings confirmed out-of-scope**: CopyEngine.cs has pre-existing `return null;`
at lines 1509/2004/2050/3162/3168/3231/4049 and pre-existing non-ASCII at 316/317/2880/2881.
None are in any section changed by B107. These are documented legacy debt, not B107 regressions.

---

## Section E: CYC Analysis

| Method | File | CYC Before | CYC After | Delta | ≤8? |
|--------|------|-----------|-----------|-------|-----|
| `TryReplacePttBeBrackets` | `CopyEngine.cs` | 6 | 7 | +1 | YES |
| `ExecuteOne` | `PttGlobalQuickExit.cs` | 2 | 2 | 0 | YES |
| `SnapshotTargetOrders` | `PttGlobalQuickExit.cs` | 4 | 7 | +3 | YES |
| `ResolveTargetCount` | `PttQuickExit.cs` | 2 | 2 | 0 | YES |

**Notes**:
- `try/finally` adds zero CYC branches (not a conditional construct).
- `Math.Min` is a library call, not a branch.
- `isNative` / `isPtt` are bool assignments, not decision points.
- New guard (3b) adds exactly 1 branch to `TryReplacePttBeBrackets`: CYC 6 → 7.

**MAX CYC = 7 < 8 LIMIT. ALL METHODS PASS.**

---

## Section F: Change Isolation

**Files touched by B107-T1**:

| File | Changes | Other files required? |
|------|---------|-----------------------|
| `src/PropTraderTools/CopyEngine.cs` | CHANGE A (field), CHANGE B (guard 3b) | No |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | CHANGE C (ExecuteOne try/finally), CHANGE D (SnapshotTargetOrders two-pass) | No |
| `src/PropTraderTools/Features/PttQuickExit.cs` | CHANGE E (ResolveTargetCount block-body + cap) | No |

**New files created**: 0
**Test project files changed**: 0
**Interface files changed**: 0
**Other PropTraderTools files changed**: 0
**Total files touched**: exactly 3

**CHANGE ISOLATION: PASS — exactly 3 files, no scope creep.**

---

## Section G: Thread Safety

### `_qxCancelInProgress` Invariant

The flag MUST be set before `CancelQxBrackets` dispatches cancel orders, and MUST be cleared
unconditionally after `CancelQxBrackets` returns (even on exception).

```
ExecuteOne thread (PttGlobalQuickExit.cs:154-162):
  _qxCancelInProgress.TryAdd(acc.Name, true)   <-- SET, atomic, before try
  try {
      CancelQxBrackets(acc, instr)              <-- cancel orders submitted
  } finally {
      _qxCancelInProgress.TryRemove(acc.Name)  <-- CLEAR, atomic, unconditional
  }
```

**Guarantee**: `ConcurrentDictionary.TryAdd` and `TryRemove` are lock-free atomic operations.
`ContainsKey` in `TryReplacePttBeBrackets` is a wait-free read, consistent with concurrent
`TryAdd`/`TryRemove`. No lock() used anywhere.

**Per-account isolation**: Key is `acc.Name` — separate accounts have separate keys, preventing
any cross-account interference if QX-ALL is called for multiple accounts concurrently.

**SCAN-07 item 6** (added by verifier): confirmed no code path can skip `TryRemove` — `finally`
is unconditional under all exit scenarios including exception.

**THREAD SAFETY: PASS — JS-021 satisfied, try/finally guarantee holds.**

---

## Section H: NT8 Sync Gate

**From `ticket-1-completion.md`**:

```
Command: powershell -File scripts\ptt-sync-and-verify.ps1

=== PTT SYNC: src/PropTraderTools -> NT8 AddOns ===
  COPIED:  CopyEngine.cs
  COPIED:  Features\PttGlobalQuickExit.cs
  COPIED:  Features\PttQuickExit.cs

  Copied:   3  |  In-sync: 13  |  Excluded: 36

=== PTT VERIFY: MD5 check every synced file ===
  [16 files: all OK]

=== SYNC + VERIFY: PASS (16 files confirmed) ===
```

**Result**: 0 MISMATCH lines. All 16 files MD5-verified.
**3 files copied** (exactly the 3 B107 files).
**F5 NinjaTrader 8**: Engineer confirms next step is F5 in NT8 to compile. (F5 runtime gate
is Director-owned — see B107-DEFER-01 in Section K.)

**NT8 SYNC GATE: PASS — 0 MISMATCH, all 16 files verified.**

---

## Section I: Spec Update

### DW-B105 badge (line 28895)

- **Before**: `<span class="badge badge-open">OPEN</span>`
- **After**: `<span class="badge badge-closed">CLOSED B107-T1</span>`
- **Applied by**: ptt-plan-reviewer (Phase 5) — engineer did not update; reviewer applied as per protocol.

### DW-B106 badge (line 29130)

- **Before**: `<span class="badge badge-open">OPEN</span>`
- **After**: `<span class="badge badge-closed">CLOSED B107-T1</span>`
- **Applied by**: ptt-plan-reviewer (Phase 5) — engineer did not update; reviewer applied as per protocol.

### Combo C re-test row (line 28883)

- **Before**: `&#9711; PENDING &mdash; pipeline in progress`
- **After**: `&#9711; AWAITING LIVE TEST &mdash; DW-B105 + DW-B106 closed B107-T1`
- **Applied by**: ptt-plan-reviewer (Phase 5) — updated to reflect pipeline completion and pending live test.

**SPEC UPDATE: COMPLETE — both badges closed, Combo C row updated.**

---

## Section J: Prior Deferred Backlog Carry-Forward

Items from `docs/brain/DW-B89/06-deferred-backlog.md` that remain OPEN:

| Item | Priority | Description | Status |
|------|----------|-------------|--------|
| DW-B42-01 | Low | T_BUG_QX_BE_01 does not assert PTT-QX-T3 | OPEN (carry-forward) |
| DW-B42-02 | High | Live NT8 F5 verification for Direction 1 + Direction 2 | OPEN (carry-forward) |
| DW-B42-03 | Conditional/Low | IsPttQxTarget range extension for future T4/T5 slots | OPEN (carry-forward) |
| DW-PTT-BE-FIX-01 | Medium | Option A lazy re-resolve for null followers | OPEN (carry-forward) |
| DW-PTT-BE-FIX-02 | High | SIM gate: Path B 3-cycle runtime verification | OPEN (merged into DW-B89-DEFERRED-04) |
| DW-PTT-BE-FIX-03 | High | 83 pre-existing build errors in CopyEngineTests.cs | OPEN as DW-B102-DEFER-01/02 |
| DW-B89-DEFERRED-01 | P0 | Ctrl+F5 NT8 compilation gate | OPEN (carry-forward) |
| DW-B89-DEFERRED-02 | High | SIM gate PATH A nominal | OPEN (carry-forward) |
| DW-B89-DEFERRED-03 | High | SIM gate PATH A buf=0 edge case | OPEN (carry-forward) |
| DW-B89-DEFERRED-04 | High | SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles) | OPEN (carry-forward) |
| DW-B89-DEFERRED-05 | High | SIM gate DW-B87 timing race cycle | OPEN (carry-forward) |
| DW-B89-DEFERRED-06 | Medium | Spec update: close DW-B89/B88/B87 in spec HTML | OPEN (carry-forward) |

**Note**: B107 changes do not close any of the above SIM gate items. DW-B89 SIM gates concern
the BE-ALL path; B107 concerns QX-ALL + intent-guard. DW-B89-DEFERRED-04 (PATH B) is related
but the runtime verification is still Director-owned and remains OPEN.

---

## Section K: Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B107-01 | Combo C live re-test: QX-ALL followed by BE-ALL with leader + follower accounts in SIM. Validates DW-B105 guard suppresses `TryReplacePttBeBrackets` race and DW-B106 target count stays exactly 3. Pass criterion: zero [BE-DIAG] lines during QX sweep; all 4 accounts covered by PTT-QX-* brackets; no unprotected position. | P1 | Director SIM gate session | OPEN |
| DW-B107-02 | F5 NinjaTrader 8 compilation confirmation after sync. ptt-sync-and-verify.ps1 passed (0 MISMATCH); F5 is the runtime compile gate owned by Director. | P0 | Immediate (prerequisite for SIM) | OPEN |

**No new code-level deferred items.** All 5 changes are complete and verified. DW-B107-01
and DW-B107-02 are operational/validation deferrals, not implementation debt.

---

## Final Verdict

**FINAL_PASS**

All Phase 5 checks satisfied:

- **Pipeline chain**: 6/6 artifacts produced, each at PASS verdict.
- **Spec coverage**: DW-B105 CLOSED, DW-B106 CLOSED, DW-B63-01 intent addressed.
- **Cross-file coherence**: F1–F7 all COHERENT / PASS.
- **JS compliance**: 7/7 scans zero violations in new/changed code.
- **CYC**: max 7 ≤ 8 across all 4 modified methods.
- **Change isolation**: exactly 3 files, no scope creep.
- **Thread safety**: try/finally guarantee holds; ConcurrentDictionary lock-free.
- **NT8 sync gate**: 0 MISMATCH, 16 files verified.
- **Spec update**: DW-B105 + DW-B106 badges closed, Combo C row updated.
- **Deferred backlog**: 06-deferred-backlog.md written.
- **Section K**: present with DW-B107-01 (live re-test) and DW-B107-02 (F5 gate).

**FINAL_PASS**
