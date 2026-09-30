# PTT-COPIER-B25 Final Review

**Reviewer**: ptt-plan-reviewer (Lane B)
**Block**: PTT-COPIER-B25
**Defect**: DW-B25-02 — Per-Account BE State Isolation
**Phase**: 5 (Final Review)
**Verdict**: **FINAL_PASS**
**Date**: 2026-07-07

---

## 1. Artifacts Read

| # | Artifact | Status |
|---|----------|--------|
| 1 | `docs/brain/PTT-COPIER-B25/02-architecture-plan.md` | REVIEW_PASS (Cycle 2) |
| 2 | `docs/brain/PTT-COPIER-B25/04-ticket-review.md` | TICKET_REVIEW_PASS |
| 3 | `docs/brain/PTT-COPIER-B25/ticket-1-completion.md` | BUILD_PASS |
| 4 | `docs/brain/PTT-COPIER-B25/ticket-1-verification.md` | VERIFY_PASS |
| 5 | `docs/brain/PTT-COPIER-B25/04-tickets.md` | Read |
| 6 | `docs/brain/PTT-COPIER-B25/02-plan-review.md` | Cycle 1 FAIL → Cycle 2 PASS |
| 7 | `docs/brain/PTT-COPIER-B24/06-deferred-backlog.md` | Read (carry-forward) |
| 8 | `docs/standards/NT8_COMPILER_RULES.md` | Read (v1.6) |
| 9 | `docs/standards/jane-street/RULES_CATALOG.md` | Read (v1.0) |
| 10 | `src/PropTraderTools/CopyEngine.cs` | Live source read |
| 11 | `src/PropTraderTools/TradeCopierPanel.cs` | Live source read |
| 12 | `src/PropTraderTools/CopyEngineTests.cs` (via verification) | VERIFY_PASS confirmed |

---

## 2. Check A — Cross-File Coherence

### A1. DisarmPendingBe / DisarmTrailBe Signature Consistency

**CopyEngine.cs definition (from live source)**:
- Line 1315: `internal void DisarmPendingBe(Account leader)` ✅
- Line 1372: `internal void DisarmTrailBe(Account leader)` ✅

**TradeCopierPanel.cs call sites (from live source + verification)**:

| Line | Call | Argument | Status |
|------|------|----------|--------|
| 402 | `_engine.DisarmPendingBe(_leaderAccount)` | `_leaderAccount` | ✅ PASS |
| 403 | `_engine.DisarmTrailBe(_leaderAccount)` | `_leaderAccount` | ✅ PASS |
| 807 | `_engine.DisarmPendingBe(_leaderAccount)` | `_leaderAccount` | ✅ PASS |
| 812 | `_engine.DisarmPendingBe(_leaderAccount)` | `_leaderAccount` | ✅ PASS |
| 813 | `_engine.DisarmTrailBe(_leaderAccount)` | `_leaderAccount` | ✅ PASS |

All 5 call sites pass `_leaderAccount`. Signatures match definitions exactly. **PASS.**

### A2. ArmPendingBe / ArmTrailBe (unchanged signature — already had Account params)

Both `ArmPendingBe(Instrument, Account, int)` and `ArmTrailBe(Instrument, Account, int)` were
already in the correct form from B14. No signature change in B25. Call sites in TradeCopierPanel.cs
(lines ~798 and ~OnBeConnected) pass `_leaderAccount` correctly. **PASS.**

**Check A: COHERENT SYSTEM ✅**

---

## 3. Check B — Cross-File JS/NT8 Scan Results (Live Independent Verification)

### SCAN-05 (JS-021): `lock\s*\(` in `*.cs`

```powershell
Select-String -Path "src\PropTraderTools\*.cs" -Pattern "lock\s*\("
```

**Result**: 5 matches — ALL are in **comments** only:
- `// ConcurrentBag rebuild pattern -- no lock (JS-021).` (×3)
- `// try block(0).` (×1 — false-positive word fragment)
- `// CYC=3: null guard(1), alreadyTighter(2), try block(0).` (×1)

Zero actual `lock()` calls in source code. **SCAN-05: 0 violations ✅** (JS-021 PASS)

### SCAN-07 (NT8-043): `\?\.\w+\s*[-+]=` in `*.cs`

```powershell
Select-String -Path "src\PropTraderTools\*.cs" -Pattern "\?\.\w+\s*[-+]="
```

**Result**: 2 matches — BOTH are in **comments** only:
- `// NT8-043: no ?.Event -= -- explicit if (acc != null) guard.` (×2 — in CopyEngine.cs)

Zero actual null-conditional compound assignments. **SCAN-07: 0 violations ✅** (NT8-043 PASS)

### SCAN-06 (NT8-004): `ImmutableDictionary` in `*.cs`

```powershell
Select-String -Path "src\PropTraderTools\*.cs" -Pattern "ImmutableDictionary"
```

**Result**: 5 matches examined:
- `CopyEngine.cs:98` — comment: `// NT8-004: ConcurrentDictionary is safe (ImmutableDictionary BANNED in NT8).`
- `CopyEngine.cs:107` — comment: same pattern
- `CopyEngine.cs:819` — comment: `// ImmutableDictionary.SetItem returns a NEW dictionary...`
- `CopyEngineTests.cs:482` — `System.Collections.Immutable.ImmutableDictionary<string, FollowerAtmMode>.Empty`
- `CopyEngineTests.cs:511` — same pattern

**Assessment**: CopyEngine.cs matches are **comments only** — zero production code violations.
CopyEngineTests.cs lines 482 and 511 use `ImmutableDictionary` as a test argument passed to
`AddRule()`. This is a pre-existing pattern from B8; it exists in the xUnit test runner which
compiles against the full .NET SDK (where `System.Collections.Immutable` is available), NOT the
NT8 NinjaScript Roslyn host. NT8-004 prohibits this type in **NT8-deployed AddOn files** only.
CopyEngineTests.cs is not deployed to NT8 — it is a test file compiled by the dotnet SDK.
This pattern is NOT a new violation introduced by B25. B25 touched zero lines in CopyEngineTests.cs
that involve ImmutableDictionary.

**SCAN-06: 0 NT8 production code violations ✅** (NT8-004 PASS)

**Check B: ZERO CROSS-FILE JS/NT8 VIOLATIONS ✅**

---

## 4. Check C — No Parameterless Disarm Calls Remain

```powershell
Select-String -Path "src\PropTraderTools\*.cs" -Pattern "DisarmPendingBe\(\)|DisarmTrailBe\(\)"
```

**Result**: 1 match — CopyEngine.cs line 1402:

```
// STAYS SUBSCRIBED until DisarmTrailBe() is called -- unlike OnPendingBeAccountUpdate (one-shot).
```

This is a **comment** only. The parentheses in the comment text matched the regex pattern but
represent no actual source call. Zero parameterless `DisarmPendingBe()` or `DisarmTrailBe()`
call sites exist in production source.

**Check C: ZERO PARAMETERLESS DISARM CALLS ✅**

---

## 5. Check D — Spec Requirements Satisfied

| Requirement | Evidence | Status |
|-------------|----------|--------|
| DW-B25-02: singleton `volatile int` state fields removed | SCAN-01/02: 0 hits for `_pendingBeState\b` / `_trailBeState\b` | ✅ CLOSED |
| Per-account `ConcurrentDictionary<string,int>` fields added | SCAN-03/04: 5 hits each for `_pendingBeStates` / `_trailBeStates` | ✅ PASS |
| NT8-004: No `ImmutableDictionary` in production code | SCAN-06: 0 production hits | ✅ PASS |
| JS-021: No `lock()` | SCAN-05: 0 actual calls | ✅ PASS |
| NT8-043: No null-conditional unsubscription | SCAN-07: 0 actual compound assignments | ✅ PASS |
| NT8-043: Explicit `if (acc != null)` guards in disarm bodies | CopyEngine.cs:1325, 1382 (verified by verifier Layer 3) | ✅ PASS |
| `DisarmPendingBe` takes `Account leader` parameter | CopyEngine.cs:1315 (live source confirmed) | ✅ PASS |
| `DisarmTrailBe` takes `Account leader` parameter | CopyEngine.cs:1372 (live source confirmed) | ✅ PASS |
| All 5 TradeCopierPanel call sites pass `_leaderAccount` | Lines 402, 403, 807, 812, 813 (live source confirmed) | ✅ PASS |
| `IsPendingBeArmed` helper: private, expression-body, CYC=1 | ticket-1-verification.md §4.D | ✅ PASS |
| `IsTrailBeArmed` helper: private, expression-body, CYC=1 | ticket-1-verification.md §4.E | ✅ PASS |
| All CYC targets met (all methods ≤ 8) | ticket-1-verification.md §8 | ✅ PASS |
| Test count preserved: 128 → 128 | ticket-1-verification.md §5 | ✅ PASS |
| 7 companion singleton fields unchanged | ticket-1-verification.md §6 | ✅ PASS |
| TOCTOU: `acc` local captured at callback entry | ticket-1-verification.md §9 | ✅ PASS |

**Check D: ALL SPEC REQUIREMENTS SATISFIED ✅**

---

## 6. Check E — All 7 Scans Confirmed Zero (Aggregate)

Verifier (Layer 3) independently ran all 7 scans against `src/PropTraderTools/`. Results confirmed
zero violations. Engineer Layer 2 self-report matched Layer 3 exactly (no discrepancies).

| Scan | Pattern | Layer 2 | Layer 3 | Final Reviewer | Status |
|------|---------|---------|---------|----------------|--------|
| SCAN-01 | `_pendingBeState\b` (old singleton) | 0 | 0 | 0 (comments excluded) | ✅ ZERO |
| SCAN-02 | `_trailBeState\b` (old singleton) | 0 | 0 | 0 (comments excluded) | ✅ ZERO |
| SCAN-03 | `_pendingBeStates` (new dict) | ≥5 | 5 | 5 | ✅ PRESENT |
| SCAN-04 | `_trailBeStates` (new dict) | ≥5 | 5 | 5 | ✅ PRESENT |
| SCAN-05 | `lock\s*\(` | 0 | 0 | 0 (comment-only hits) | ✅ ZERO |
| SCAN-06 | `ImmutableDictionary` | 0 | 0 | 0 (production code) | ✅ ZERO |
| SCAN-07 | `\?\.\w+\s*[-+]=` | 0 | 0 | 0 (comment-only hits) | ✅ ZERO |

**Check E: ALL 7 SCANS ZERO (AGGREGATE) ✅**

---

## 7. DNA Rule Compliance (Cross-File)

| Rule | Check | Status |
|------|-------|--------|
| JS-021 (lock BANNED) | SCAN-05: 0 actual calls | ✅ PASS |
| JS-001 (throw in hot path BANNED) | All Arm/Disarm/helper bodies use early-return, no throw | ✅ PASS |
| JS-002 (return null BANNED) | All new methods are void; helpers return bool | ✅ PASS |
| JS-033 (async void BANNED) | No async methods in any modified file | ✅ PASS |
| NT8-003 (volatile double BANNED) | No new volatile declarations | ✅ PASS |
| NT8-004 (ImmutableDictionary BANNED in NT8) | ConcurrentDictionary used; 0 production hits | ✅ PASS |
| NT8-018 (lock() BANNED) | SCAN-05: 0 actual calls | ✅ PASS |
| NT8-043 (null-conditional unsubscription BANNED) | SCAN-07: 0; explicit `if (acc != null)` guards present | ✅ PASS |

---

## 8. Architecture Coherence Assessment

**CopyEngine.cs ↔ TradeCopierPanel.cs coherence**:
- `DisarmPendingBe(Account)` and `DisarmTrailBe(Account)` — defined in CopyEngine.cs, called
  from TradeCopierPanel.cs. Signatures, arities, and argument types match exactly across files.
- `ArmPendingBe(Instrument, Account, int)` and `ArmTrailBe(Instrument, Account, int)` — unchanged
  from B14; remain coherent.
- Multi-panel isolation: Panel A keys on `_leaderAccount.Name = "SIM101"`, Panel B on `"SIM102"`.
  `TryRemove("SIM101")` does not affect `"SIM102"` key. ConcurrentDictionary provides full
  per-key isolation.

**CopyEngine.cs ↔ CopyEngineTests.cs coherence**:
- 3 existing tests updated to reflect new API (field name change + null parameter for disarm).
- Test count 128 → 128 maintained.

**No missing wiring detected. No orphaned call sites. No signature drift.**

---

## 9. Plan Review History Summary

| Cycle | Verdict | Violations |
|-------|---------|------------|
| Cycle 1 | REVIEW_FAIL | V1 (DisarmPendingBe CYC claimed=3, actual=4), V2 (DisarmTrailBe same), V3 (HARD FAIL: OnPendingBeAccountUpdate CYC projected=10), V4 (doc heading) |
| Cycle 2 | REVIEW_PASS | All 4 violations resolved: F1 (IsPendingBeArmed helper extracted), F2/F3 (CYC targets revised to ≤4, Director-sanctioned), F4 (heading corrected) |

Plan entered Phase 3 after Cycle 2 PASS. Zero new violations discovered in implementation.

---

## 10. Violations Found

**None.**

---

## Section K — Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B24-01 | **NT8-043 formal rule entry**: Confirm null-conditional event unsubscription (`?.Event -=`) causes silent runtime crash under NT8 Roslyn. Add to `docs/standards/NT8_COMPILER_RULES.md` as NT8-043 (P1). B24/B25 code has zero null-conditional unsubscriptions (SCAN-07 = 0 both blocks); rule is WATCH-only. Needs explicit confirmation before promoting to P1 CONFIRMED. **Note**: NT8-043 was added in B23/B24 as a P0 compiler error rule. DW-B24-01 tracks runtime crash confirmation specifically. | P2 | B26 or future | OPEN |
| DW-B24-02 | **Manual E2E runtime verification**: Press B on a solo account (no copy rule registered) in a live NinjaTrader session. Confirm stop moves. Unit tests cover null-leader path and no-throw paths but cannot substitute for in-process NT8 runtime validation. Must be done before releasing B24/B25 changes to production users. | P1 | B26 pre-release | OPEN |
| DW-B24-03 | **Skip-duplicate guard test**: The `if (acc == leader) continue` guard (CopyEngine.cs:~1195) prevents double-firing when the leader account appears in the `AllAccounts` fan-out. A formal [Fact] test for this scenario is absent. Add a test that wires a rule where master == leader account and verifies `MoveStopToBreakEven` is called exactly once for that account. | P2 | B26 | OPEN |
| DW-B25-01 | **Companion field race**: `_pendingBeAccount`, `_pendingBeInstrument`, `_trailBeAccount`, `_trailBeInstrument` remain plain refs (single-writer UI thread). In a multi-panel topology, two panels could race on the same singleton companion ref. These fields are intentionally kept as singletons in B25 (per-account isolation was scoped to state slots only). Full companion-field isolation deferred. | P3 | B26 or future | OPEN |
| DW-B25-02 | **Per-account BE state isolation**: Replace singleton `volatile int _pendingBeState` and `volatile int _trailBeState` with `ConcurrentDictionary<string, int>`. Update DisarmPendingBe/DisarmTrailBe signatures to accept `Account leader`. Update all 5 TradeCopierPanel call sites. | — | B25 (this block) | **CLOSED** |

---

## FINAL_PASS

All checks passed. No violations found across all reviewed artifacts and live source files.

- **Check A** (cross-file coherence): PASS
- **Check B** (JS/NT8 scans): PASS — zero violations in production code
- **Check C** (parameterless disarm calls): PASS — zero parameterless calls
- **Check D** (spec requirements): PASS — all requirements satisfied, DW-B25-02 CLOSED
- **Check E** (7 scans zero): PASS — all 7 scans confirmed zero aggregate

Section K is present. `06-deferred-backlog.md` written with current-block entry.

---

*ptt-plan-reviewer · PTT-COPIER-B25 · 05-final-review.md · 2026-07-07*
