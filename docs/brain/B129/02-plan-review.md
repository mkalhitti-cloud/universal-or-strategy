# B129 Plan Review
## Phase 2 — Architecture Plan Review
## Reviewer: ptt-plan-reviewer
## Block: B129 — Instrument Row Redesign: Quick2t + QAll2t
## Plan Under Review: `docs/brain/B129/02-architecture-plan.md`
## Result: REVIEW_FAIL

---

## Violations Summary

| # | Rule ID | Severity | Dimension | Location in Plan | Description |
|---|---------|----------|-----------|-----------------|-------------|
| 1 | SPEC COMPLETENESS (P0) | P0 | R1 | Section C.4 + C.5 + G.SCAN-05 | Log tag mismatch vs Director-confirmed code. Plan uses `[PTT-2T-INSTR]`; Director-confirmed spec requires `[PTT-QX-2T]`. |

---

## Per-Dimension Review

### R1 — Spec Completeness: **FAIL**

| Requirement | Addressed? | Plan Section | Notes |
|-------------|------------|--------------|-------|
| BuildInstrRow: 2 plain buttons, no spinner | YES | C.2 | Grid with 2 ColumnDefinitions, no spinner fields |
| Exact labels: "Quick2t" / "QAll2t" | YES | C.2 pseudocode | `Content = "Quick2t"` and `Content = "QAll2t"` |
| B128Tests.cs: 4 old ComputeInstrSplit tests removed, 4 new Build2TargetList tests added | YES | C.7 | Exact test names and assert values specified |
| T2qty=0 guard in Execute loop | YES | D.1 | `if (tNQty <= 0) continue;` inside for-loop |
| Field removals: `_instrQxT1`, `_instrBeBtn` deleted; `_instrQxBtn` repurposed | YES | C.1 + C.6 | All 5 removed methods listed by name |
| DW-B133 logged for deferred QAll2t 2-target path | YES | Deferred Items section + Section B DW-B133 |  |
| **Log tag `[PTT-QX-2T]` for Quick2t handler** | **NO** | C.4, SCAN-05 | **Plan uses `[PTT-2T-INSTR]`. Director-confirmed code specifies `[PTT-QX-2T] button: " + _leaderAccount.Name + " " + _instrument.FullName + " qty=" + qty`. This is a spec deviation.** |

**VIOLATION — SPEC COMPLETENESS (P0):**
- The Director-confirmed code in the mission brief explicitly states the `OnInstr2tClick` output line as:
  ```
  "[PTT-QX-2T] button: " + _leaderAccount.Name + " " + _instrument.FullName + " qty=" + qty + " T1=" + targets[0].Qty + " T2=" + targets[1].Qty
  ```
- The plan at Section C.4 specifies:
  ```
  "[PTT-2T-INSTR] button: " + leader.Name + " " + _instrument.FullName + " qty=" + pos.Quantity + " t1q=" + targets[0].Qty + " t2q=" + targets[1].Qty
  ```
- Discrepancies: (a) tag `[PTT-2T-INSTR]` vs `[PTT-QX-2T]`; (b) field keys `t1q=` / `t2q=` vs `T1=` / `T2=`.
- DW-B129-01 in the plan also references the wrong tag `[PTT-2T-INSTR]`, confirming the deviation is carried through.
- This constitutes a spec requirement not correctly addressed in the plan (SPEC COMPLETENESS, P0).

---

### R2 — CYC Compliance: **PASS**

| Method | File | Planned CYC | Budget | Result |
|--------|------|------------|--------|--------|
| `Build2TargetList(int)` | TradeCopierPanel.cs | 1 | ≤8 | PASS |
| `OnInstr2tClick()` | TradeCopierPanel.cs | 4 | ≤8 | PASS |
| `OnInstrQAll2tClick()` | TradeCopierPanel.cs | 1 | ≤8 | PASS |
| `BuildInstrRow()` (modified) | TradeCopierPanel.cs | ~2 | ≤8 | PASS |
| `PttQuickExit.Execute()` (modified) | PttQuickExit.cs | 8 | ≤8 | PASS — was 7, +1 guard = 8, on budget |

All methods within Jane Street strict standard (CYC ≤ 8). No violation.

---

### R3 — P0 Rules Gate: **PASS**

| Rule ID | Description | Check | Result |
|---------|-------------|-------|--------|
| JS-021 | No `lock()` | SCAN-01 explicit; no lock() in any new/modified code | PASS |
| JS-001 | No `throw` in hot paths | Handlers log via `Output.Process`, never throw | PASS |
| JS-002 | No `return null` | `Build2TargetList` returns `new List<>` (never null); all handlers are `void` | PASS |
| JS-033 | No `async void` | All new handlers are synchronous `private void` | PASS |
| NT8: No `DateTime.Now` | Use `DateTime.UtcNow` | No `DateTime.Now` in any B129 code; unchanged call sites already use UtcNow | PASS |
| NT8: ASCII-only | No Unicode in string literals | All labels and log tags are pure ASCII | PASS |
| NT8: No `FontFamily` | No FontFamily on new buttons | Confirmed in C.2 and SCAN-06 | PASS |
| NT8: No hardcoded hex | No `#RRGGBB` in new code | Confirmed in C.2 and SCAN-06 | PASS |

No P0 concurrency or type safety violations found in the plan.

---

### R4 — NT8 API Constraints: **PASS**

| API Usage | Validity | Notes |
|-----------|----------|-------|
| `new PttQuickExit().Execute(leader, _instrument, 4, targets, true, 0, 0)` — 7-arg overload | VALID | Existing overload; no new NT8 API introduced |
| `new PttGlobalQuickExit().Execute()` — zero-arg | VALID | Existing SIM-validated path; Option B decision |
| `Account.All` | NOT used in new code | PttGlobalQuickExit.Execute() handles this internally |
| No `AtmStrategyCreate()` | N/A | Not present; AddOn-level pattern is correct |

No NT8 API violations. No `async`/`await` in constructors or `OnInitialize`/`OnDestroyed`. No `Account.All` in constructor.

---

### R5 — Test Coverage: **PASS**

| Test | Input | Expected Asserts | Covered in Plan? |
|------|-------|-----------------|-----------------|
| `T_B129_Build2TargetList_EvenQty` | totalQty=4 | Count==2; [0].Qty==2; [1].Qty==2 | YES — Section C.7 |
| `T_B129_Build2TargetList_OddQty` | totalQty=5 | Count==2; [0].Qty==3; [1].Qty==2 | YES — Section C.7 |
| `T_B129_Build2TargetList_SingleQty` | totalQty=1 | Count==2; [0].Qty==1; [1].Qty==0 | YES — Section C.7 |
| `T_B129_Build2TargetList_LargeQty` | totalQty=7 | Count==2; [0].Qty==4; [1].Qty==3 | YES — Section C.7 |
| Price placeholder assertions | all | [0].Price==0.0; [1].Price==0.0 | YES — Section C.7 (additional assertions) |

All 4 required [Fact] tests specified with exact names, inputs, and assertions. `internal static` access via `InternalsVisibleTo` noted. PASS.

---

### R6 — 7-Scan Checklist: **PASS**

Section G contains all 7 scans explicitly:

| Scan | Item | Present? |
|------|------|---------|
| SCAN-01 | No `lock(` anywhere | YES — with grep command |
| SCAN-02 | No `async void` (non-event-handler) | YES |
| SCAN-03 | No `DateTime.Now` | YES |
| SCAN-04 | No `return null` | YES |
| SCAN-05 | ASCII-only identifiers and string literals | YES |
| SCAN-06 | No FontFamily, no hardcoded hex colors | YES |
| SCAN-07 | All `CreateOrder` calls use `PTT-` prefix | YES |

All 7 scans present and complete with explicit confirmation for each new/modified method. PASS.

---

### R7 — No Scope Creep: **PASS**

| File | Change Type | In Scope? |
|------|-------------|----------|
| `src/PropTraderTools/TradeCopierPanel.cs` | ADD/REMOVE/REPLACE methods + fields | YES |
| `src/PropTraderTools/Features/PttQuickExit.cs` | 1-line guard addition | YES |
| `src/PropTraderTools/Tests/B128Tests.cs` | 4 tests removed, 4 tests added | YES |
| `src/PropTraderTools/Features/PttGlobalQuickExit.cs` | NO CHANGES (Option B) | N/A — explicitly declared out of scope |

Component summary (bottom of plan) confirms only the 3 in-scope files are modified. No extraneous files touched. PASS.

---

## Spec Coverage Matrix

| Director-Confirmed Requirement | Addressed? | Plan Section | Status |
|-------------------------------|------------|--------------|--------|
| 2-button instrument row: "Quick2t" left, "QAll2t" right | YES | C.2 | PASS |
| T1=entry+4t, T2=entry+8t (hardcoded) | YES | C.4 `t1Ticks=4`, C.3 notes | PASS |
| T1qty = ceil(totalQty/2) = (totalQty+1)/2 | YES | C.3 `int t1Qty = (totalQty + 1) / 2` | PASS |
| T2qty = totalQty - T1qty | YES | C.3 `int t2Qty = totalQty - t1Qty` | PASS |
| T2qty=0 guard: skip T2 when pos.Quantity==1 | YES | D.1 `if (tNQty <= 0) continue;` | PASS |
| Quick2t calls PttQuickExit.Execute 7-arg with targets list + t1Ticks=4 | YES | C.4 | PASS |
| QAll2t calls PttGlobalQuickExit.Execute() (Option B) | YES | C.5, Section B | PASS |
| Build2TargetList: CYC=1, returns 2-entry list, never null | YES | C.3 | PASS |
| OnInstr2tClick: CYC ≤ 4 | YES | C.4 CYC=4 | PASS |
| Log tag `[PTT-QX-2T]` with format `qty=N T1=N T2=N` | **NO** | C.4 uses `[PTT-2T-INSTR]` with `t1q=`/`t2q=` | **FAIL** |
| DW-B133 logged for future 2-target QAll2t path | YES | Deferred items + Section B | PASS |
| Old spinner methods/fields removed (OnInstrQxUp/Down, _instrQxT1, etc.) | YES | C.1, C.6 | PASS |
| B128Tests.cs: 4 new Build2TargetList tests | YES | C.7 | PASS |

---

## Required Fix (Blocking — must be corrected before REVIEW_PASS)

**Fix 1 (SPEC LOG TAG):**
In `docs/brain/B129/02-architecture-plan.md`:

- **Section C.4** — Replace the `Output.Process` call with the Director-confirmed format:
  ```csharp
  NinjaTrader.Code.Output.Process(
      "[PTT-QX-2T] button: " + leader.Name
      + " " + _instrument.FullName
      + " qty=" + pos.Quantity
      + " T1=" + targets[0].Qty + " T2=" + targets[1].Qty,
      NinjaTrader.NinjaScript.PrintTo.OutputTab1);
  ```
  (Change tag from `[PTT-2T-INSTR]` → `[PTT-QX-2T]`; change `t1q=`/`t2q=` → `T1=`/`T2=`.)

- **Section G SCAN-05** — Update the log tag reference from `[PTT-2T-INSTR]` to `[PTT-QX-2T]`.

- **Section H (DW-B129-01)** — Update the log tag reference in the SIM gate criteria from `[PTT-2T-INSTR]` to `[PTT-QX-2T]`.

No other changes needed. All other dimensions PASS.

---

## Overall Result

**REVIEW_FAIL**

Reason: 1 violation (SPEC COMPLETENESS P0) — log tag and field-key format in `OnInstr2tClick` output line deviates from the Director-confirmed specification. All Jane Street DNA rules (JS-XXX) are satisfied. All CYC budgets are within limit. All 7 scans are specified. Scope is correct. One targeted fix to the log format is required before REVIEW_PASS can be issued.

Return to ptt-architect for correction (Cycle 1 of 2).

---

## REVIEW RETRY (Post-Fix)
Date: 2025-01-27
Violation Fixed: YES

### R1 Re-check: PASS

All three required locations now carry the correct tag and key format:

| Location | Required | Found | Result |
|----------|----------|-------|--------|
| Section C.4 `Output.Process` string | `[PTT-QX-2T]` + `T1=` / `T2=` | `[PTT-QX-2T]` + `T1=` / `T2=` | PASS |
| Section G SCAN-05 log tag reference | `[PTT-QX-2T]` | `[PTT-QX-2T]` | PASS |
| DW-B129-01 SIM gate criteria | `[PTT-QX-2T]` + `T1=` / `T2=` | `[PTT-QX-2T]` + `T1=` / `T2=` | PASS |

The prior violation (`[PTT-2T-INSTR]` tag and `t1q=`/`t2q=` keys) is fully corrected in all three locations.

### R2–R7: Unchanged PASS

The fix was surgical (string literal only). No method signatures, CYC counts, NT8 API calls, field lists, or test specifications were altered. All dimensions confirmed unchanged from initial PASS rulings.

| Dimension | Result |
|-----------|--------|
| R2 — CYC Compliance | PASS (unchanged) |
| R3 — P0 Rules Gate | PASS (unchanged; ASCII-only SCAN-05 now correct) |
| R4 — NT8 API Constraints | PASS (unchanged) |
| R5 — Test Coverage | PASS (unchanged) |
| R6 — 7-Scan Checklist | PASS (unchanged) |
| R7 — No Scope Creep | PASS (unchanged) |

### Overall: REVIEW_PASS
