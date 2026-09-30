# B171 Plan Review
**Block:** B171 — DW-B171-01 + DW-B171-02: PTT-STP-Drag Duplicate Stop + fo=NULL on Stop Drag
**Written by:** ptt-plan-reviewer (Phase 2)
**Plan reviewed:** `docs/brain/B171/02-architecture-plan.md`
**Spec (ground truth):** `docs/brain/B171-orchestrator-prompt.md`
**Prior backlog:** `docs/brain/B170/06-deferred-backlog.md`

---

## Check 1 — LANE-SPLIT GATE Compliance

**Result: PASS**

- Gate result is present: §1 concludes with `### LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` in an explicit callout box.
- Q2=YES is cited as the stop condition verbatim: *"Q2 = YES → STOP. Gate result: SINGLE-PIPELINE."*
- The rationale (compile dependency between B171-1 call site and B171-2 callee signatures) is correctly stated and matches the spec's dependency description.
- Q3 and Q4 are answered for completeness, both confirming SINGLE-PIPELINE.
- No lane-split is used. Constraint not violated.

---

## Check 2 — Bug Root-Cause Fidelity

**Result: PASS**

**DW-B171-01:**
- Line 4478 cited as unsuffixed `"PTT-STP-Drag"`: ✅ — plan §2: *"Line 4478: `"PTT-STP-Drag"` ← unsuffixed (THE BUG)"*
- Missing `CancelLiveCollateralStop` on target-drag path cited: ✅ — plan §2: *"there is no `CancelLiveCollateralStop` call before creation on the Phase C target-drag path. Compare to `ResubmitOneCollateralLeg` (lines 4038-4053, stop-drag path) which correctly calls `CancelLiveCollateralStop` before creating"*

**DW-B171-02:**
- `IsDragNameMatch` suffixed pattern (`"PTT-STP-Drag-" + legSuffix`) vs unsuffixed creation (`"PTT-STP-Drag"`) cited: ✅ — plan §2: *"`IsDragNameMatch` (lines 4740-4748) checks: `orderName == "PTT-STP-Drag-" + legSuffix` (SUFFIXED pattern)* / *`SubmitReplacementStopOrder` (line 4478) creates stop as: `"PTT-STP-Drag"` (UNSUFFIXED)"*
- `fo=NULL` consequence cited: ✅ — plan §2: *"Exact match fails → `fo = NULL` → stop drag not synced for any follower"*

Both root causes match the spec exactly.

---

## Check 3 — Variant E Selection Rationale

**Result: PASS**

- EC-1 regression explained: ✅ — plan §3: *"If `FindLeaderStopPrice` returns <= 0: The existing live stop is cancelled (follower is now unprotected) / No replacement stop is submitted (price guard fires and returns) / The follower bracket has zero live stops until the next drag event"*
- Variant E validates price BEFORE cancel confirmed: ✅ — plan §3 shows the explicit 5-step order with *"3. if (stp <= 0) → StatusUpdate + return // EARLY EXIT — cancel is never reached / 4. CancelLiveCollateralStop(...) // Only reached if stp > 0"*
- CYC neutrality of the guard explained: ✅ — *"The `if (stp <= 0)` guard does NOT increase the net CYC of `ExecutePhaseCStopReplacement` because the branch replaces what was previously a delegated guard in `CreateFollowerReplacementStop`"*

---

## Check 4 — BEFORE/AFTER Accuracy

**Result: PASS**

### `ExecutePhaseCStopReplacement` AFTER

| Required element | Present in plan |
|---|---|
| Suffix derivation (`string suffix = bracketIdx.ToString()`) | ✅ plan §4 Method A AFTER line 136 |
| `stp <= 0` guard with `StatusUpdate?.Invoke(...)` + `return` | ✅ plan §4 Method A AFTER lines 138-142 |
| `CancelLiveCollateralStop(acc, fo, "PTT-STP-Drag-" + suffix)` call | ✅ plan §4 Method A AFTER line 143 |
| `CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp, suffix)` call with suffix param | ✅ plan §4 Method A AFTER line 144 |

All elements present and match the spec's Method A AFTER block verbatim.

### `CreateFollowerReplacementStop` AFTER

| Required element | Present in plan |
|---|---|
| `string suffix` param added to signature | ✅ plan §4 Method B AFTER line 185 |
| `StatusUpdate` message uses suffix: `"PTT-STP-Drag-" + suffix + " skipped: stopPrice <= 0"` | ✅ plan §4 Method B AFTER line 190 |
| `SubmitReplacementStopOrder(followerAcc, instr, qty, stopAction, stopPrice, suffix)` gains suffix | ✅ plan §4 Method B AFTER line 193 |

All elements present and match the spec's Method B AFTER block verbatim.

### `SubmitReplacementStopOrder` AFTER

| Required element | Present in plan |
|---|---|
| `string suffix` param added to signature | ✅ plan §4 Method C AFTER line 256 |
| Order name literal changed: `"PTT-STP-Drag-" + suffix` (was `"PTT-STP-Drag"`) | ✅ plan §4 Method C AFTER line 271 |
| Null-check StatusUpdate gains suffix: `"PTT-STP-Drag-" + suffix + ": CreateOrder returned null"` | ✅ plan §4 Method C AFTER line 277 |
| Placed StatusUpdate gains suffix: `"PTT-STP-Drag-" + suffix + " placed @ "` | ✅ plan §4 Method C AFTER line 281 |

All three mandatory "PTT-STP-Drag" literals changed. Plan matches spec Method C AFTER verbatim.

---

## Check 5 — CYC Table

**Result: PASS**

Plan §5 table:

| Method | Before | After | Delta | Compliant (<=8) |
|--------|--------|-------|-------|-----------------|
| `ExecutePhaseCStopReplacement` | 2 | 2 | 0 | YES |
| `CreateFollowerReplacementStop` | 2 | 2 | 0 | YES |
| `SubmitReplacementStopOrder` | 4 | 4 | 0 | YES |

All values match the spec's CYC Summary. All methods at delta=0. Max CYC=4, well within the JS <=8 limit.

---

## Check 6 — Caller Safety

**Result: PASS**

Plan §6:
- `CreateFollowerReplacementStop`: exactly one caller = `ExecutePhaseCStopReplacement` (line 4511) ✅
- `SubmitReplacementStopOrder`: exactly one caller = `CreateFollowerReplacementStop` (line 4450) ✅
- No test directly calls either method ✅

Matches spec "Caller safety (confirmed, one caller each)" exactly.

---

## Check 7 — Methods Unchanged List

**Result: PASS**

Plan §7 table lists:

| Method | Listed in plan |
|--------|----------------|
| `CancelLiveCollateralStop` | ✅ |
| `IsDragNameMatch` | ✅ |
| `DeriveLeaderBracketIndex` | ✅ |
| `ResubmitOneCollateralLeg` | ✅ |

All four required methods are present with correct line ranges and roles matching the spec.

---

## Check 8 — Jane Street Compliance Checklist

**Result: PASS**

Plan §8 contains a full compliance table with the following items verified:

| Rule | Plan Status |
|------|-------------|
| JS-021 — No `lock()` | PASS |
| JS-001 — No unhandled throw in dispatch path | PASS |
| JS-002 — Void return | PASS |
| ASCII-only — No Unicode/curly quotes | PASS |
| No `DateTime.Now` | PASS |
| No `FontFamily` | PASS |
| CYC <= 8 | PASS |
| `apply_diff` only (no `write_file`) | ENFORCED |
| `ptt-sync.ps1` run after all edits | ENFORCED |
| Line traceability | PASS |

All items are explicitly checked. No item is missing.

---

## Check 9 — Ticket Outline

**Result: PASS**

Plan §9 defines exactly 3 tickets:

**B171-1** — `ExecutePhaseCStopReplacement`: Suffix + Cancel Sweep
- File and line range stated ✅
- Dependency: None ✅
- Bugs closed: DW-B171-01 ✅

**B171-2** — `CreateFollowerReplacementStop` + `SubmitReplacementStopOrder`: Suffix Threading
- File and line ranges stated ✅
- **Dependency on B171-1 explicitly stated**: *"DEPENDS ON B171-1. B171-1 must be committed first."* ✅
- Bugs closed: DW-B171-02 ✅

**B171-3** — xUnit Tests
- Dependency on B171-1 + B171-2 stated ✅
- All 5 test scenarios from spec are present verbatim:
  1. Single target drag → exactly one `PTT-STP-Drag-{N}` ✅
  2. Two consecutive target drags on same bracket → still exactly one (duplicate cancelled) ✅
  3. Two target drags on different brackets → two stops with different suffixes ✅
  4. Stop drag after target drag → `fo != NULL` ✅
  5. `stp <= 0` path → exits without calling `CancelLiveCollateralStop` (EC-1 guard) ✅

All 5 scenarios present. No gap in test coverage.

---

## Check 10 — apply_diff-Only Constraint

**Result: PASS**

Stated in plan §8 compliance table: *"`apply_diff` used for all CopyEngine.cs edits (NO `write_file`) | ENFORCED — engineer constraint in Ph4a"*

Also stated in B171-2 ticket: *"Engineer applies both apply_diff blocks before building."*

Constraint is doubly stated and applies to `src/PropTraderTools/CopyEngine.cs`.

---

## Spec Coverage Matrix

| Spec requirement | Addressed? | Plan section |
|---|---|---|
| LANE-SPLIT GATE result present | YES | §1 |
| Q2=YES cited as SINGLE-PIPELINE stop condition | YES | §1 |
| DW-B171-01 root cause: line 4478 unsuffixed | YES | §2 |
| DW-B171-01 root cause: missing `CancelLiveCollateralStop` on target-drag path | YES | §2 |
| DW-B171-02 root cause: `IsDragNameMatch` suffixed vs unsuffixed creation | YES | §2 |
| DW-B171-02 root cause: `fo=NULL` consequence | YES | §2 |
| Variant E: EC-1 regression explained (cancel-before-price-validation) | YES | §3 |
| Variant E: validates price BEFORE cancel | YES | §3 |
| `ExecutePhaseCStopReplacement` AFTER: suffix, guard, cancel, create+suffix | YES | §4 Method A |
| `CreateFollowerReplacementStop` AFTER: suffix param, StatusUpdate with suffix, SubmitReplacementStopOrder+suffix | YES | §4 Method B |
| `SubmitReplacementStopOrder` AFTER: suffix param, all 3 "PTT-STP-Drag" literals suffixed | YES | §4 Method C |
| CYC: Before=2/2/4, After=2/2/4, Delta=0/0/0, all <=8 | YES | §5 |
| Caller safety: one caller each, no tests call directly | YES | §6 |
| Methods unchanged: CancelLiveCollateralStop, IsDragNameMatch, DeriveLeaderBracketIndex, ResubmitOneCollateralLeg | YES | §7 |
| Jane Street compliance checklist present and all items checked | YES | §8 |
| 3 tickets: B171-1, B171-2, B171-3 | YES | §9 |
| B171-2 dependency on B171-1 stated | YES | §9 |
| B171-3 covers all 5 test scenarios from spec | YES | §9 |
| apply_diff-only constraint for CopyEngine.cs stated | YES | §8, §9 |

All 20 spec requirements addressed. Zero gaps.

---

## Violations Found

**None.**

No Jane Street rule violations (JS-001 through JS-023 applicable set).
No NT8 API violations.
No spec omissions.
No BEFORE/AFTER discrepancies.
No CYC errors.
No caller safety violations.
No missing unchanged-methods entries.
No missing ticket or test scenario.

---

## Final Verdict

**REVIEW_PASS**

The plan is complete, internally consistent, and correctly implements the Variant E winning design from the orchestrator spec. All 10 mandated checks pass. The plan is cleared to proceed to Phase 3 (ticket generation by ptt-architect).
