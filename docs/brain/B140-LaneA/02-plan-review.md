# B140-LaneA Plan Review
## Phase 2 — PTT Plan Reviewer

**Plan file:** `docs/brain/B140-LaneA/02-architecture-plan.md`
**Reviewed by:** ptt-plan-reviewer
**Review date:** Block B140-LaneA, Phase 2

---

## 1. LANE-SPLIT GATE COMPLIANCE

**Result: PASS**

| Check | Finding |
|-------|---------|
| Gate result stated? | YES — `LANE-SPLIT GATE RESULT: SINGLE-PIPELINE` present (plan line 8) |
| Q1 = YES justification present? | YES — "only `SyncFollowerBracket` in `CopyEngine.cs` is touched (single insertion point)" |
| Gate reasoning complete? | YES — Q1=YES closes gate per protocol |

---

## 2. SPEC COMPLIANCE

| Requirement | Status | Notes |
|-------------|--------|-------|
| Root cause: OCO cascade on `acc.Cancel` for Stop1/Stop2 | PASS | Section 2 correctly identifies `acc.Cancel` -> OCO cascade causing Target1/Target2 cancellation |
| Surgical BEFORE/AFTER for `SyncFollowerBracket` with OCO-branch | **FAIL** | See SPEC-CONFLICT finding below — Stop3 routing conflict unresolved |
| CYC analysis: 7->8, PASS | PASS | Section 5: CYC Before=7, After=8, Limit=8, Status=PASS |
| JS-021 compliance: no `lock()` | PASS | Section 6: explicitly verified, no lock introduced |
| JS-001 compliance: try/catch, no rethrow | PASS | Section 6: catch absorbs via `StatusUpdate`, no rethrow |
| JS-002 compliance: void path | PASS | Section 6: void method, no null return |
| ASCII-only | PASS | Section 6: `": ATM STP Change error: "` confirmed ASCII-only |
| StopPrice vs StopPriceChanged note | PASS | Section 7: note present, fallback deferred to Gate 1 fail only |
| SIM Gates 1-3 defined | PASS | Section 8: all three gates with specific pass criteria |
| Gate 1 fail protocol: STOP, report DW-B154, no fallback | PASS | Section 8: "DO NOT implement a fallback. STOP immediately. Report to Director. Document as DW-B154." |
| Tests T_B140_01 through T_B140_07 | PASS | Section 9: all 7 tests present with `[Fact]` stubs and coverage map |
| Deferred work register: DW-B153 CLOSED + carry-forward | PASS | Section 10: DW-B153 CLOSED, 8 carry-forward items listed |
| Risk register: acc.Change silent no-op entry | PASS | Section 11: R1 entry covers no-op risk with Gate 1 mitigation |

---

## 3. NT8 API FACTS

**Result: PASS**

| Fact | Status | Plan Reference |
|------|--------|----------------|
| `acc.Change(Order[])` preserves OCO link (NT8_API_SURFACE.md B31) | PASS | Section 3, Fact 1 |
| `acc.Cancel()` OCO cascade confirmed | PASS | Section 3, Fact 2 |
| `fo.Oco` non-empty GUID for ATM brackets Stop1/Stop2 | PASS | Section 3, Fact 3 |
| `fo.Oco` empty string for PTT-STP-Drag | PASS | Section 3, Fact 4 |
| `acc.Change()` on ATM Stop brackets requires SIM Gate 1 | PASS | Section 3, Fact 5 |

---

## 4. SURGICAL CHANGE CORRECTNESS

**Result: FAIL**

### Passing elements

| Check | Status |
|-------|--------|
| BEFORE: routes all ATM stops to `SyncAtmFollowerBracket` | PASS |
| AFTER (3a): `!string.IsNullOrEmpty(fo.Oco)` branch uses `fo.StopPrice = newPrice` + `acc.Change(new Order[] { fo })` + try/catch + return | PASS |
| AFTER (3b): empty Oco routes to `SyncAtmFollowerBracket` (existing path preserved) | PASS |

### SPEC-CONFLICT FINDING — FAIL

**Citation: SPEC-CONFLICT: Stop3 has non-empty Oco but plan section 2 states cancel+resubmit is CORRECT for Stop3; plan section 4 AFTER code routes ALL non-empty Oco orders (including Stop3) to `acc.Change` without differentiating Stop3.**

**Conflict detail:**

- Plan **Section 2** states explicitly: *"Stop3 (Oco non-empty, paired with Target3 only) — cancel+resubmit is CORRECT, must not change."*
- Plan **Section 4 AFTER code** uses: `if (!string.IsNullOrEmpty(fo.Oco))` — this condition is TRUE for Stop3 (Stop3 has a non-empty Oco).
- Therefore Stop3 will be routed to `acc.Change` by the AFTER code, **NOT** to `SyncAtmFollowerBracket` (cancel+resubmit).
- This directly contradicts the Section 2 requirement.
- The plan provides **no explanation** for why routing Stop3 through `acc.Change` is acceptable, despite explicitly stating the cancel+resubmit path "must not change" for Stop3.

**What is required to resolve this:** The architect must explicitly address one of these two resolutions:

1. **Acceptable deviation:** Explain why using `acc.Change` for Stop3 is acceptable (e.g., "Stop3 is only paired with Target3; using `acc.Change` preserves that OCO link which is equally correct or better than cancel+resubmit"). If this is the intended design, update Section 2 to remove "must not change" or add a reconciliation note.
2. **Differentiation required:** Add an additional branch condition to exclude Stop3 from the `acc.Change` path (e.g., by checking order name or a Stop3-specific discriminator), routing Stop3 to `SyncAtmFollowerBracket` as stated in Section 2.

**No other violations found in the surgical change design.**

---

## 5. OVERALL RESULT

**REVIEW_FAIL**

### Violation Summary

| # | Rule/Citation | Location | Description |
|---|--------------|----------|-------------|
| V1 | SPEC-CONFLICT | Plan Section 2 vs Section 4 AFTER code | Stop3 has non-empty Oco; Section 2 states cancel+resubmit is CORRECT and must not change for Stop3; Section 4 AFTER code routes ALL non-empty Oco (including Stop3) to `acc.Change` without explanation or differentiation. Plan is internally contradictory and silent on the Stop3/acc.Change interaction. |

### Resolution required before REVIEW_PASS

- Return to ptt-architect.
- Architect must either (a) justify that `acc.Change` for Stop3 is correct and remove the "must not change" claim from Section 2, or (b) add a branch condition that excludes Stop3 from the `acc.Change` path.
- Re-submit for Phase 2 review after correction.

---

*Review authored by ptt-plan-reviewer, B140-LaneA, Phase 2.*
*Cycle: 1 of 2 maximum.*

---

### Cycle 2 Review (ptt-plan-reviewer)

**Reviewed:** B140-LaneA, Phase 2, Cycle 2
**Against:** `02-architecture-plan.md` (revised — Stop3 routing clarification added)

#### Prior Violation: RESOLVED

- **V1 (SPEC-CONFLICT):** Section 2 previously stated "Stop3 cancel+resubmit is CORRECT, must not change," while Section 4 AFTER code routed Stop3 to `acc.Change` without justification.
- **Resolution found in revised plan:**
  - Section 2 (line 30–31) now reads: *"Stop3 cancel+resubmit was non-destructive… This is a description of the pre-B140 state, **not a mandate to preserve cancel+resubmit**."* The "must not change" claim is removed.
  - Section 4 "Stop3 Routing Clarification" (lines 89–96) explicitly confirms Stop3 has non-empty Oco, will route to branch (3a) `acc.Change`, and justifies this as intentional and strictly better (preserves Target3 OCO link, eliminates cancel+resubmit overhead).
  - Section 11 Risk Register R2 marks the Stop3 routing concern as "Resolved" with justification.
- The internal contradiction is eliminated. The architect chose resolution path (a): justify `acc.Change` for Stop3 is correct. The justification is coherent and technically sound.
- **RESOLVED — no longer a violation.**

#### Cycle 2 Item Status

| # | Check | Result | Notes |
|---|-------|--------|-------|
| 1 | LANE-SPLIT GATE: SINGLE-PIPELINE, Q1=YES | PASS | Section 1 unchanged and correct |
| 2 | SPEC-CONFLICT V1: Stop3 routing conflict resolved | PASS | See "Prior Violation" above — fully addressed |
| 3 | CYC analysis: 7->8, at limit 8 (JS-041) | PASS | Section 5 unchanged |
| 4 | JS-021: no `lock()` introduced | PASS | Section 6 confirmed |
| 5 | JS-001: try/catch, no rethrow, hot path safe | PASS | Section 6 confirmed |
| 6 | JS-002: void method, no null return | PASS | Section 6 confirmed |
| 7 | ASCII-only string literals | PASS | `": ATM STP Change error: "` confirmed ASCII |
| 8 | No `DateTime.Now` | PASS | No date/time references |
| 9 | NT8 API facts table (5 facts) | PASS | Section 3 intact |
| 10 | `acc.Change` not a CreateOrder call — no PTT- prefix required | PASS | Correct API usage |
| 11 | SIM Gates 1–3 with Gate 1 fail → STOP/DW-B154/no fallback | PASS | Section 8 intact |
| 12 | 7 xUnit `[Fact]` tests, no NUnit/MSTest | PASS | Section 9 intact |
| 13 | Deferred work register (DW-B153 CLOSED + 8 carry-forward) | PASS | Section 10 intact |
| 14 | StopPrice vs StopPriceChanged fallback note (Gate 1 fail only) | PASS | Section 7 intact |
| 15 | No `Dictionary<K,V>` shared collection | PASS | No new collection introduced |
| 16 | No mutable fields on struct, no unfreed SolidColorBrush | PASS | Not applicable to this change |
| 17 | No `sealed TradeCopierWindow`, no `Account.All` in constructor | PASS | Not applicable to this change |
| 18 | No `async/await` in lifecycle methods | PASS | No lifecycle method modified |
| 19 | No magic string discriminated state | PASS | `fo.Oco` empty-check is a presence test, not a magic-string discriminator |
| 20 | Risk Register covers acc.Change no-op risk (R1) | PASS | Section 11 R1 intact with Gate 1 mitigation |

#### OVERALL: REVIEW_PASS

All Cycle 1 passing checks remain passing. The single Cycle 1 violation (V1 SPEC-CONFLICT) is fully resolved by the revised plan. No new violations introduced. Zero outstanding violations.

*Review authored by ptt-plan-reviewer, B140-LaneA, Phase 2.*
*Cycle: 2 of 2 maximum. Gate closed.*
