# Ticket Review: PTT-COPIER-B25

**Reviewer**: ptt-ticket-reviewer
**Block**: PTT-COPIER-B25, Lane B
**Defect**: DW-B25-02 — Per-Account BE State Isolation
**Plan ref**: docs/brain/PTT-COPIER-B25/02-architecture-plan.md (REVIEW_PASS, Cycle 2)
**Tickets ref**: docs/brain/PTT-COPIER-B25/04-tickets.md
**Date**: 2026-07-07

---

## Defense-in-Depth Checklist (10-Point)

This review applies the mandatory 10-point checklist from the task brief against T1.
All citations reference exact ticket section and rule ID.

---

## T1 — DW-B25-02: Per-Account BE State Isolation

### 1. Traceability: PASS

**Spec requirement**: DW-B25-02 (singleton volatile int state corruption under multi-panel
topology) is explicitly cited in the ticket header and Spec Requirements Satisfied section.

**Plan coverage**: Ticket Parts A1–A10 map to plan §2 Component List, §3 Field Changes,
§4–§5 Method Signatures + Bodies, §6 Caller Changes, §7 Test Changes, §8 Threading Model,
§11 CYC Budget, §12 Access Site Map — all plan sections are covered.

| Ticket section | Plan section | Status |
|----------------|-------------|--------|
| A1 (remove old fields) | §3.1 Remove | ✅ |
| A2 (add new ConcurrentDictionary fields) | §3.2 Add | ✅ |
| A3 (ArmPendingBe body) | §5.1 | ✅ |
| A4 (DisarmPendingBe sig+body) | §5.2 | ✅ |
| A5 (ArmTrailBe body) | §5.3 | ✅ |
| A6 (DisarmTrailBe sig+body) | §5.4 | ✅ |
| A7 (IsPendingBeArmed helper) | §5.5 | ✅ |
| A8 (IsTrailBeArmed helper) | §5.5 | ✅ |
| A9 (OnTrailBeAccountUpdate guard) | §5.6 | ✅ |
| A10 (OnPendingBeAccountUpdate 2 sites) | §5.7 | ✅ |
| B (5 TradeCopierPanel call sites) | §6 Caller Changes | ✅ |
| C1 (test: ArmTrailBe_NullInstrument_NoException) | §7.1 | ✅ |
| C2 (test: DisarmTrailBe_WhenNotArmed_NoException) | §7.2 | ✅ |
| C3 (test: DisarmTrailBe_Idempotent) | §7.2 | ✅ |

**Phantom work**: None detected. All ticket items trace to plan or spec.
**Missing work**: None detected. All plan components appear in the ticket.

---

### 2. JS Pre-Check: PASS

**JS-021 (lock BANNED — P0):**
No `lock()` described in any method body. The ticket Threading Invariants section
(point 4) explicitly states "No lock anywhere." ConcurrentDictionary TryAdd/TryRemove/
TryGetValue are lock-free at the API level. ✅

**JS-033 (async void BANNED — P0):**
No async methods described anywhere in the ticket. ✅

**JS-001 (throw in hot path — P0):**
No exception throws in any described method body. All failure paths use early-return. ✅

**JS-002 (return null — P0):**
No `return null;` in any new method. The helpers return bool. Arm/Disarm methods return void. ✅

**JS-023 (atomic primitives — concurrent state):**
State transition (`_pendingBeStates`, `_trailBeStates`) uses ConcurrentDictionary indexer
setter (arm), TryRemove (disarm), TryGetValue (read). All are thread-safe without lock. ✅

**JS-008 (readonly structs) / JS-009 (immutable collections):**
No struct mutations described. `ConcurrentDictionary` is the NT8-safe replacement for
ImmutableDictionary (NT8-004 requires this substitution). ✅

**Concurrency violation check (shared mutable Dictionary):**
No `Dictionary<K,V>` for shared state — `ConcurrentDictionary` is used throughout. ✅

**UI update from non-UI thread:**
Threading Invariants section point 2 explicitly forbids UI calls inside callbacks. ✅

---

### 3. CYC Pre-Check: PASS

| Method | Target | Actual | Analysis |
|--------|--------|--------|----------|
| `IsPendingBeArmed` | ≤ 1 | 1 | Expression-body `=>` with `&&` chain. No if/while/for/?:. Lizard counts 0 decision points in expression bodies. ✅ |
| `IsTrailBeArmed` | ≤ 1 | 1 | Same pattern as IsPendingBeArmed. ✅ |
| `ArmPendingBe` | ≤ 4 | 4 | 3 explicit if-branches (null instr, null masterAcc, IsFlat) + base 1 = 4. ✅ |
| `DisarmPendingBe` | ≤ 4 | 4 | 3 explicit if-branches (leader null, TryRemove fail, acc null for unsub) + base 1 = 4. Director-sanctioned (F2). ✅ |
| `ArmTrailBe` | ≤ 4 | 4 | 3 explicit if-branches (null instr, null masterAcc, IsFlat) + base 1 = 4. ✅ |
| `DisarmTrailBe` | ≤ 4 | 4 | 3 explicit if-branches (leader null, TryRemove fail, acc null for unsub) + base 1 = 4. Director-sanctioned (F3). ✅ |
| `OnTrailBeAccountUpdate` | ≤ 8 | 5 | Net delta 0 at guard site (IsTrailBeArmed replaces 1-branch volatile read 1-for-1). ✅ |
| `OnPendingBeAccountUpdate` | ≤ 8 | 8 | 7 explicit if + base 1 = 8. F1 fix: IsPendingBeArmed helper absorbs compound guard; net delta = 0 at both sites. Full method body shown in A10 for engineer verification. ✅ |

All CYC budgets achievable and correctly stated.

---

### 4. NT8 Constraints: PASS

| Rule | Check | Verdict |
|------|-------|---------|
| NT8-001 | `{ get; init; }` | No `init` properties in any new or modified code. PASS ✅ |
| NT8-003 | `volatile double` | Old volatile int fields removed; no new volatile declarations introduced. PASS ✅ |
| NT8-004 | `ImmutableDictionary` | Using `ConcurrentDictionary<string, int>`. Ticket A2 explicitly notes "NT8-004: ImmutableDictionary BANNED; ConcurrentDictionary is the NT8-safe replacement." PASS ✅ |
| NT8-017 | Cross-thread volatile bool/int | Replaced by ConcurrentDictionary. No new volatile int required. PASS ✅ |
| NT8-018 | `lock()` | No lock anywhere in described code. PASS ✅ |
| NT8-043 | Null-conditional unsubscribe (`?.Event -=`) | All event unsubscriptions use explicit `if (acc != null) acc.AccountItemUpdate -= handler;` pattern. `StatusUpdate?.Invoke(...)` in DisarmPendingBe/DisarmTrailBe is a null-conditional event *fire* (invoke), not a subscribe/unsubscribe — NT8-043 does not apply to Invoke calls. PASS ✅ |

**NT8-043 detail (StatusUpdate?.Invoke):**
The pattern `StatusUpdate?.Invoke("DisarmPendingBe: leader null -- no-op")` uses `?.` as a
null-conditional method invocation, not as a null-conditional compound assignment. NT8-043
bans `?.` on the LEFT side of `-=` or `+=`. This is a right-side `.Invoke()` call with a
null guard. NT8 compiles this correctly under C# 7.3. PASS ✅

---

### 5. Completeness: PASS

**CopyEngine.cs changes (10 required):**
A1 (remove _pendingBeState), A2 (remove _trailBeState), A2 (add _pendingBeStates + _trailBeStates
fields), A3 (ArmPendingBe body), A4 (DisarmPendingBe sig+body), A5 (ArmTrailBe body),
A6 (DisarmTrailBe sig+body), A7 (IsPendingBeArmed helper), A8 (IsTrailBeArmed helper),
A9 (OnTrailBeAccountUpdate guard), A10 (OnPendingBeAccountUpdate 2 sites) — all 10 present. ✅

**TradeCopierPanel.cs call sites (5 required):**
Lines 402, 403, 807, 812, 813 — all 5 present with correct `_leaderAccount` argument. ✅

**CopyEngineTests.cs (3 test updates required):**
C1 (ArmTrailBe_NullInstrument_NoException — field name + assertion change),
C2 (DisarmTrailBe_WhenNotArmed_NoException — null arg),
C3 (DisarmTrailBe_Idempotent_NoExceptionOnDoubleCall — null args) — all 3 present. ✅

**Null-safety of TradeCopierPanel call sites:**
- Lines 402/403 (Detach path): `_leaderAccount` not yet null at time of call (nulled on line 406 AFTER disarm). Engine null guard handles defensive double-Detach. ✅
- Lines 807/812/813 (OnBeClick path): Guard at line 798 (`if (_leaderAccount == null) return;`) ensures non-null. ✅

---

### 6. Test Coverage: PASS

**Method count (128 baseline / 128 final):**
No new tests added, no tests deleted. Three existing tests updated to match new API
(field name change + signature change). Test count invariant preserved. ✅

**Private helper methods (IsPendingBeArmed, IsTrailBeArmed):**
Both are `private` expression-body methods. Direct unit testing of private methods via
reflection is not required per the PTT standard (helper semantics are exercised transitively
through the existing callback tests). No [Fact] required for private helpers. ✅

**Updated tests cover:**
- Null instrument guard fires before dict write (C1 — Assert.Empty on _trailBeStates). ✅
- Null argument to DisarmTrailBe does not throw (C2 — null guard path). ✅
- Idempotent double-call with null arg does not throw (C3 — TryRemove idempotency). ✅

**Missing [Fact] analysis:**
All public and internal methods changed (ArmPendingBe, DisarmPendingBe, ArmTrailBe,
DisarmTrailBe) have pre-existing test coverage in the 128-test baseline. The three updated
tests are the correct minimal change to keep the test suite compiling and semantically correct
against the new API. No gaps detected. ✅

---

### 7. Scan Checklist Presence: PASS

All 7 scans (SCAN-01 through SCAN-07) are present in the ticket's "7-Scan Checklist (Engineer Contract)" section with executable grep commands and explicit required results.

| Scan | Command | Required Result | Correctness Assessment |
|------|---------|----------------|----------------------|
| SCAN-01 | `grep -n "_pendingBeState\b"` | 0 matches | ✅ Trailing `\b` correctly excludes `_pendingBeStates` (word boundary fires; no match when followed by `s`) |
| SCAN-02 | `grep -n "_trailBeState\b"` | 0 matches | ✅ Same pattern; correctly excludes `_trailBeStates` |
| SCAN-03 | `grep -n "_pendingBeStates"` | ≥5 matches | ✅ No false-positive risk (the new plural form is the target; all arm/disarm/helper/callback sites counted) |
| SCAN-04 | `grep -n "_trailBeStates"` | ≥5 matches | ✅ Same rationale |
| SCAN-05 | `grep -rn "lock\s*("` | 0 matches | ✅ JS-021 / NT8-018 compliance check |
| SCAN-06 | `grep -rn "ImmutableDictionary"` | 0 matches | ✅ NT8-004 compliance check |
| SCAN-07 | `grep -rn "\?\.\w\+\s*[-+]="` | 0 matches | ✅ NT8-043 compliance check; GNU grep syntax valid on Linux; note for engineer: on Windows, use Git Bash or WSL for this scan |

**SCAN-01 word-boundary analysis (specifically required by task brief):**
Pattern `_pendingBeState\b` anchors a word boundary AFTER the pattern. In `_pendingBeStates`,
the character after `_pendingBeState` is `s` (a word character), so `\b` does NOT fire —
no match. The pattern is safe. PASS ✅

**3-Layer scan contract (SCAN-01..07 in ticket = Layer 1):**
The engineer reads these scans as their build contract (Layer 1). They self-report results
in ticket-completion.md (Layer 2). The verifier independently re-runs all 7 scans (Layer 3).
All 7 layers are correctly anchored by this ticket. ✅

---

### 8. Helper Method Definition: PASS

**IsPendingBeArmed (A7):**
```csharp
private bool IsPendingBeArmed(Account acc)
    => acc != null
    && _pendingBeStates.TryGetValue(acc.Name, out int st)
    && st == 1;
```
- Visibility: `private` ✅
- Form: expression-body (`=>`) ✅
- Null guard: `acc != null &&` (first clause) ✅
- TryGetValue + `st == 1` check: both present ✅ (not just TryGetValue result — value equality checked)

**IsTrailBeArmed (A8):**
```csharp
private bool IsTrailBeArmed(Account acc)
    => acc != null
    && _trailBeStates.TryGetValue(acc.Name, out int st)
    && st == 1;
```
- Visibility: `private` ✅
- Form: expression-body (`=>`) ✅
- Null guard: `acc != null &&` ✅
- TryGetValue + `st == 1` check: both present ✅

Both helpers conform exactly to the required specification.

---

### 9. Commit Message: N/A (not required by plan or spec for B25)

Neither the architecture plan nor the spec establishes a commit message requirement for this
block. The ticket does not specify a commit message. This is not a gap introduced by the
architect — no commit message was scoped in the plan. Not a violation.

---

### 10. Companion Fields Unchanged: PASS

Plan §3.3 explicitly lists the 7 companion singleton fields as NOT changed:

| Field | Type | Status in B25 |
|-------|------|--------------|
| `_pendingBeAccount` | Account (plain ref) | Unchanged ✅ |
| `_pendingBeInstrument` | Instrument (plain ref) | Unchanged ✅ |
| `_pendingBeBufferTicks` | volatile int | Unchanged ✅ |
| `_trailBeAccount` | Account (plain ref) | Unchanged ✅ |
| `_trailBeInstrument` | Instrument (plain ref) | Unchanged ✅ |
| `_trailBeBufferTicks` | volatile int | Unchanged ✅ |
| `_trailBeLastPnl` | plain long (Interlocked-guarded) | Unchanged ✅ |

The ticket methods write to `_pendingBeAccount = null` and `_trailBeAccount = null` in
Disarm methods — this is the existing singleton-clear pattern (unchanged from B24), not a
migration to per-account. The deferred items DW-B25-01 and DW-B25-02 in the ticket correctly
acknowledge these fields remain as singletons. ✅

---

## File Routing: PASS

All .cs source paths point to the Wave workspace:
```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs         ✅
c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierPanel.cs   ✅
c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs    ✅
```
No .cs paths point to the Director workspace (`c:\WSGTA\universal-or-strategy-director\`). ✅

---

## Overall: TICKET_REVIEW_PASS

### T1 Verdict Summary

| Check | Result | Notes |
|-------|--------|-------|
| 1. Traceability | **PASS** | All 14 plan components accounted for. No phantom work. |
| 2. JS Pre-Check | **PASS** | JS-021, JS-033, JS-001, JS-002, JS-023, JS-008 all satisfied. |
| 3. CYC Pre-Check | **PASS** | All 8 methods within stated and achievable targets. |
| 4. NT8 Constraints | **PASS** | NT8-001, -003, -004, -017, -018, -043 all satisfied. |
| 5. Completeness | **PASS** | 10 CopyEngine changes, 5 Panel call sites, 3 test updates — all present. |
| 6. Test Coverage | **PASS** | 128→128 count. Three updates correct. Private helpers exempt. |
| 7. Scan Checklist | **PASS** | All 7 scans present, executable, SCAN-01/02 word-boundary safe. |
| 8. Helper Methods | **PASS** | Both helpers: private, expression-body, null-guard, TryGetValue+st==1. |
| 9. Commit Message | **N/A** | Not scoped in plan or spec. |
| 10. Companion Fields | **PASS** | All 7 companion fields correctly left unchanged. |
| File Routing | **PASS** | All .cs paths → Wave workspace. |

**TICKET_REVIEW_PASS**

The engineer may proceed with T1 implementation. No violations found. The 7-scan checklist
(SCAN-01 through SCAN-07) in the ticket is the engineer's binding contract. Verifier (Phase 4b)
will independently re-run all 7 scans against the built source and compare against the
engineer's self-reported results in ticket-T1-completion.md.

---

*ptt-ticket-reviewer · PTT-COPIER-B25 · 04-ticket-review.md · 2026-07-07*
