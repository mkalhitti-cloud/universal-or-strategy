# PTT-REPAIRS-03-POST Plan Review

**Epic:** PTT-REPAIRS-03-POST
**Phase:** 2 (Plan Review)
**Reviewer:** ptt-plan-reviewer
**Date:** 2026-09-06
**Plan reviewed:** `docs/brain/PTT-REPAIRS-03-POST/02-architecture-plan.md`
**Source of truth:** `docs/brain/PTT-REPAIRS-03/direct-edits.md`
**Source read:** `src/PropTraderTools/CopyEngine.cs`, `src/PropTraderTools/CopyEngineTests.cs`

---

## VERDICT: REVIEW_PASS

No blocking violations found. Ph3 (ticket generation) is unlocked.

---

## Section A: LANE-SPLIT GATE Compliance

| Question | Answer | Source Evidence |
|----------|--------|----------------|
| Q1: Same method or within 50 lines? | NO | BUG-C at lines ~5783-5888; BUG-D at lines ~2357-2464 (~3428 lines apart) |
| Q2: Fix B design depends on Fix A final design? | NO | Gate-5 (`_liveEntryInstruments`) fully orthogonal to gate-0.5 (`IsExitSignalName`) |
| Q3: Each fix has standalone value if the other is blocked? | YES | Both production-critical, independent failure modes |
| Q4: Each fix has an independent SIM verification path? | YES | BUG-C: orderId equality test; BUG-D: OrderType enum test |

**Gate result stated in plan:** LANES-APPROVED
**Gate result verified:** CORRECT (Q1=NO, Q2=NO, Q3=YES, Q4=YES)
**Gate compliance: PASS**

---

## Section B: Spec Coverage Matrix

| Requirement (from direct-edits.md) | Addressed? | Plan Section |
|------------------------------------|-----------|--------------|
| BUG-C: `ContainsKey`-only false-block when NT8 late Cancelled | YES | Section 2a |
| BUG-C: Map type change `byte`->`string`, value=orderId | YES | Section 2b |
| BUG-C: Gate predicate change `ContainsKey`->`TryGetValue+equality` | YES | Section 2c |
| BUG-C: `TryAdd`->`indexer` in `SetLiveEntryDispatched` | YES | Section 2d |
| BUG-C: Value-guarded `TryRemove` in `EvictDedup` Cancelled branch | YES | Section 2e |
| BUG-C: Value-guarded `TryRemove` in `EvictDedup` Filled branch | YES | Section 2e |
| BUG-C: TOCTOU window acknowledged | YES | Section 2e + DW-REPAIRS-03-POST-01 |
| BUG-C: Missing T3 test identified + spec provided | YES | Section 2g + DW-REPAIRS-03-POST-02 |
| BUG-D: Empty-name branch removed from `IsExitSignalName` | YES | Section 3b |
| BUG-D: `IsExitSignalNameOrAnonClose` new type-aware wrapper | YES | Section 3c |
| BUG-D: `DispatchCopy` gate0.5 call site updated | YES | Section 3d |
| BUG-D: `T_B59_07` contract restoration documented | YES | Section 3e |
| BUG-D: 6 AnonClose tests added | YES | Section 3f |
| JS compliance table for all changed methods | YES | Section 4 |
| Deferred items carried forward | YES | Section 5 |

**Coverage: COMPLETE. All spec requirements addressed.**

---

## Section C: Per-Checklist Findings

### C1 — LANE-SPLIT GATE RESULT present and correctly reasoned
**PASS.** Section 1 of plan contains full gate table with per-bug-per-question answers. Result
LANES-APPROVED is consistent with source evidence. Line distances confirmed from source.

### C2 — Plan accurately describes what is in source
**PASS** (with one non-blocking narrative note).

Confirmed changes in source vs plan description:

| Change | Plan | Source | Match |
|--------|------|--------|-------|
| `_liveEntryInstruments` field declaration | `ConcurrentDictionary<string,string>` | Line 203: `ConcurrentDictionary<string, string>` | EXACT |
| `IsLiveEntryBlocked_Check` predicate | `TryGetValue+equality` | Line 5804: `TryGetValue(instrKey, out var liveOrderId) && liveOrderId == orderId` | EXACT |
| `SetLiveEntryDispatched` store | indexer `[instrKey] = orderId` | Line 5823: `_liveEntryInstruments[instrKey] = orderId` | EXACT |
| `EvictDedup` Cancelled value-guard | `TryGetValue+equality+TryRemove` | Lines 5866-5872: exact pattern | EXACT |
| `EvictDedup` Filled value-guard | identical to Cancelled | Lines 5881-5887: exact pattern | EXACT |
| `IsExitSignalName` empty-name branch | absent | Lines 2360-2379: no `name.Length==0` branch | EXACT |
| `IsExitSignalNameOrAnonClose` | new method, CYC=3 | Lines 2439-2444: present, matches decision table | EXACT |
| `DispatchCopy` gate0.5 call site | `IsExitSignalNameOrAnonClose(order.Name, order.OrderType)` | Line 2454: exact match | EXACT |

**Non-blocking narrative note (not a violation):** Plan Section 3b states "Empty string now falls
through to `null` check → returns `false`". In source, the `null` check at line 2362 tests for
`name == null`, which `""` does not satisfy. `""` falls through all branches and reaches the
`return false` at line 2378. The final behaviour (returns `false`) is correct, but the plan's
description of the control flow is imprecise. No rule is violated; the fix is sound.

### C3 — BUG-C root cause and fix strategy
**PASS.** Root cause (late NT8 Cancelled delivery causing `ContainsKey`-based false-block for new
orderId on same instrKey) confirmed by direct-edits.md. Fix strategy (type change, predicate
change, indexer overwrite, value-guarded eviction) confirmed in source.

### C4 — BUG-D root cause and fix strategy
**PASS.** Root cause (DW-LB-FL-01 V6 `name.Length==0` branch over-blocked empty-name Limit entry
orders) confirmed by direct-edits.md. Fix (branch removed, type-aware wrapper added, call site
updated) confirmed in source.

### C5 — CYC numbers match actual method bodies
**PASS.** All CYC values verified against source using project's counting convention (compound
short-circuit `&&` counts as +1 decision node, consistent with existing source comments).

| Method | Plan CYC | Verified CYC | Within JS-013 limit (≤8)? |
|--------|----------|-------------|--------------------------|
| `IsLiveEntryBlocked_Check` | 4 | 4 (project convention) | PASS |
| `SetLiveEntryDispatched` | 1 | 1 | PASS |
| `EvictDedup` | 6 | 6 (confirmed by source comment at line 5843) | PASS |
| `IsExitSignalName` | 7 | 7 (6 decisions + base) | PASS |
| `IsExitSignalNameOrAnonClose` | 3 | 3 (project convention) | PASS |
| `DispatchCopy` | 8 | 8 (source comment at line 2448, no change) | PASS |

**Presentation note (not a violation):** Plan Section 4 lists `EvictDedup (Cancelled)` and
`EvictDedup (Filled)` as separate rows both showing CYC=6. These are branches within one method.
The combined method CYC=6 is correct. No rule is violated.

### C6 — Value-guarded TryRemove TOCTOU acknowledged in deferred section
**PASS.** Plan Section 2e explicitly states the TOCTOU window exists and is documented as
DW-REPAIRS-03-POST-01 in Section 5. The window is correctly assessed as non-exploitable in NT8
single-threaded `OnOrderUpdate` callback context.

### C7 — Missing T3 test for BUG-C identified with sufficient spec
**PASS.** Plan Section 2g and DW-REPAIRS-03-POST-02 both identify
`IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked` as missing. The test is confirmed
absent from CopyEngineTests.cs lines 3120-3200. The spec in Section 2g is complete: method name,
required shim `SetLiveEntryDispatched_ForTest`, shim implementation, arrange/act/assert
pseudocode, and what pre-fix vs post-fix behaviour to assert.

### C8 — 6 AnonClose tests listed in plan match CopyEngineTests.cs
**PASS.** All 6 tests confirmed present and correct in source lines 3141-3182:

| Plan entry | Source method | Assertion verified |
|-----------|---------------|--------------------|
| T_B59_AnonClose_01: `""`,`Limit`→`false` | `T_B59_AnonClose_01_EmptyName_LimitType_ReturnsFalse` (line 3142) | `Assert.False(...Limit)` ✓ |
| T_B59_AnonClose_02: `""`,`Market`→`true` | `T_B59_AnonClose_02_EmptyName_MarketType_ReturnsTrue` (line 3149) | `Assert.True(...Market)` ✓ |
| T_B59_AnonClose_03: `""`,`StopMarket`→`true` | `T_B59_AnonClose_03_EmptyName_StopMarketType_ReturnsTrue` (line 3155) | `Assert.True(...StopMarket)` ✓ |
| T_B59_AnonClose_04: `"PTT-Copy"`,any→`true` | `T_B59_AnonClose_04_NamedPttPrefix_AnyType_ReturnsTrue` (line 3162) | `Assert.True` for Limit and Market ✓ |
| T_B59_AnonClose_05: `"Entry"`,`Limit`→`false` | `T_B59_AnonClose_05_NamedEntry_LimitType_ReturnsFalse` (line 3171) | `Assert.False` ✓ |
| T_B59_AnonClose_06: `null`,`Market`→`false` | `T_B59_AnonClose_06_NullName_ReturnsFalse` (line 3177) | `Assert.False(...null, Market)` ✓ |

### C9 — No lock() introduced in any changed method
**PASS.** Source confirmed: no `lock(` statement in `IsLiveEntryBlocked_Check`,
`SetLiveEntryDispatched`, `EvictDedup`, `IsExitSignalName`, `IsExitSignalNameOrAnonClose`, or
`DispatchCopy`. JS-021: NO VIOLATION.

### C10 — No Unicode in any changed string literal
**PASS.** All string literals in changed methods are ASCII-only. `DispatchCopy` diagnostic log
string uses only `[`, `]`, `-`, `=`, ` ` characters. JS-ASCII: NO VIOLATION.

### C11 — JS-001/JS-002/JS-021/JS-013/ASCII compliance per changed method
**PASS.**

| Method | JS-001 no throw | JS-002 bool safety | JS-021 no lock | JS-013 CYC≤8 | ASCII |
|--------|-----------------|-------------------|----------------|--------------|-------|
| `IsLiveEntryBlocked_Check` | PASS | PASS (bool-only) | PASS | PASS (4) | PASS |
| `SetLiveEntryDispatched` | PASS | N/A (void) | PASS | PASS (1) | PASS |
| `EvictDedup` | PASS | N/A (void) | PASS | PASS (6) | PASS |
| `IsExitSignalName` | PASS | PASS (bool-only) | PASS | PASS (7) | PASS |
| `IsExitSignalNameOrAnonClose` | PASS | PASS (bool-only) | PASS | PASS (3) | PASS |
| `DispatchCopy` (gate0.5 change) | PASS | N/A (void) | PASS | PASS (8) | PASS |

No NT8 StrategyBase-only APIs used. No `async/await` in lifecycle methods. No `Account.All` in
constructor. No `DateTime.Now`. No `CreateOrder` without PTT- prefix. No hardcoded hex colours.

---

## Section D: Violations Table

| # | Severity | Rule ID | Description | Location |
|---|----------|---------|-------------|----------|
| — | — | — | No violations found | — |

---

## Section E: Non-Blocking Notes (informational only — do not block Ph3)

| # | Note | Location |
|---|------|----------|
| N1 | Plan Section 3b narrative states empty string "falls through to `null` check" — control flow is imprecise (null check tests `== null`; `""` passes the null check and falls through all branches to `return false`). Behaviour is correct. | Plan Section 3b |
| N2 | Plan Section 4 lists `EvictDedup (Cancelled branch)` and `EvictDedup (Filled branch)` as separate rows with CYC=6 each. These are two branches of one method; the method CYC is 6 total. Table is misleading but the CYC value is correct. | Plan Section 4, 6 |
| N3 | Plan CYC counting convention uses compound `&&` as +1 decision node. Strict McCabe would give `IsLiveEntryBlocked_Check`=5 and `IsExitSignalNameOrAnonClose`=4. Both are within JS-013 limit of 8 under either convention. | Plan Sections 2f, 3c |

---

## Section F: Ph3 Gate

**Ph3 (ticket generation) is UNLOCKED.**

- All spec requirements addressed in plan.
- All changed methods confirmed in source.
- All JS-rule compliance confirmed.
- LANE-SPLIT GATE: LANES-APPROVED (valid).
- One mandatory next-pass deliverable tracked: `DW-REPAIRS-03-POST-02` (P1, OPEN) — add
  `IsLiveEntryBlocked_DifferentOrderId_SameInstrKey_NotBlocked` test and
  `SetLiveEntryDispatched_ForTest` shim. Full spec provided in plan Section 2g.

---

*ptt-plan-reviewer · PTT-REPAIRS-03-POST · 02-plan-review.md · Phase 2*
