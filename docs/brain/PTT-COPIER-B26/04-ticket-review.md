# Ticket Review: PTT-COPIER-B26

**Epic**: PTT-COPIER-B26  
**Phase**: 3.5 — Ticket Review  
**Reviewer**: ptt-ticket-reviewer  
**Date**: 2026-07-07  
**Tickets reviewed**: 1  
**Source tickets**: `docs/brain/PTT-COPIER-B26/04-tickets.md`  
**Source plan**: `docs/brain/PTT-COPIER-B26/02-architecture-plan.md` (REVIEW_PASS, Cycle 1)  

---

## T1 — Fix `_beBufferBox` NullRef in `DispatchShortcut`

### Traceability

| Item | Status | Evidence |
|------|--------|----------|
| Spec requirement `DW-PR120-GREPTILE-B` cited | ✅ PASS | T1 §"Spec Requirement IDs": `DW-PR120-GREPTILE-B` named with exact file+line cross-reference |
| Edit 1 traces to plan decision | ✅ PASS | Plan §5.1: delete `private TextBox _beBufferBox;` at line 202 — exact match |
| Edit 2 traces to plan decision | ✅ PASS | Plan §5.2: replace Key.B case body, use `_beBuffer` — exact match |
| Edit 3 traces to plan decision | ✅ PASS | Plan §5.3: update stale comment at line 3041 — exact match |
| No phantom work (work in ticket not in plan) | ✅ PASS | Ticket write set = plan write set: 3 edits to `TradeCopierPanel.cs` + 1 [Fact]. No additional files or methods touched. |
| No missing work (plan work absent from ticket) | ✅ PASS | Plan §13 write set fully covered. No plan section left without a ticket edit. |

**Traceability: PASS**

---

### Edit Correctness

| Edit | Status | Evidence |
|------|--------|----------|
| Edit 1: `_beBufferBox` field deletion is exact (line 202, no other deletions) | ✅ PASS | Ticket Edit 1 deletes only `private TextBox _beBufferBox;` at line 202. No other field deletions described. |
| Edit 2: Key.B case uses `_beBuffer` (int) directly — not `_beBufferBox.Text`, not hardcoded `2` | ✅ PASS | Ticket Edit 2 NEW block: `_engine.BreakEven(_leaderAccount, _instrument, _beBuffer);` — `_beBuffer` is confirmed as `private int _beBuffer = 1;` at line 245. The hardcoded `2` scaffolding and `_beBufferBox.Text` parse are both explicitly deleted. |
| Edit 3: stale comment updated, `_beBufferBox.Text` reference removed | ✅ PASS | Ticket Edit 3 removes `_beBufferBox.Text` from comment and replaces it with `_beBuffer (int field, maintained by OnBeUp/OnBeDown)`. Comment is factually correct. |
| No other methods touched beyond 3 specified edits | ✅ PASS | Ticket write set is `TradeCopierPanel.cs` (3 edits) + test file (1 [Fact]). No other methods or files mentioned. |

**Note (non-blocking)**: Edit 3 comment wording in ticket ("maintained by OnBeUp/OnBeDown") differs slightly from plan §5.3 wording ("int, UI-thread-only"). Both are factually correct; neither contains `_beBufferBox`. Not a rule violation.

**Edit Correctness: PASS**

---

### JS Rule Pre-Check

| Rule ID | Severity | Description | Ticket States | Status |
|---------|----------|-------------|---------------|--------|
| JS-021 | P0 | No `lock()` in change set | PASS — `DispatchShortcut` is UI-thread-only; no lock anywhere in change set | ✅ PASS |
| JS-001 | P0 | No `throw new XxxException` in hot paths | PASS — fix removes crash path, introduces no `throw` statements | ✅ PASS |
| JS-002 | P0 | No `return null` | PASS — no return statements of any kind in the 3-edit change set | ✅ PASS |
| JS-033 | P0 | No `async void` (non-handler) | PASS — `DispatchShortcut` is `private void`, not async | ✅ PASS |
| JS-036 | P0 | No `new byte[]` heap alloc in hot path | N/A — no allocations in change set | ✅ PASS |
| JS-037 | P0 | No `new T[]` without ArrayPool in hot path | N/A — no array allocations in change set | ✅ PASS |
| CYC ≤ 8 | Arch | `DispatchShortcut` cyclomatic complexity | CYC = 5 before and after: 4 switch cases + base 1. Edit 2 removes 2 lines inside an existing arm; arm count unchanged. No branches added or removed. | ✅ PASS |

**JS Rule Pre-Check: PASS**

---

### NT8 Check

| Constraint | Status | Evidence |
|------------|--------|----------|
| No new NT8 API calls added | ✅ PASS | Change set is pure C# field/int substitution. `BreakEven(Account, Instrument, int)` was established in B24; no new NT8 surface touched. |
| No AddOn lifecycle methods affected | ✅ PASS | `DispatchShortcut` is a private WPF key-dispatch helper, not an AddOn lifecycle method. |
| No `sealed` on `TradeCopierWindow` | ✅ PASS | Not in scope. |
| No `FontFamily` set on WPF element | ✅ PASS | Not in scope. |
| No hardcoded hex color | ✅ PASS | Not in scope. |
| No `CreateOrder` with non-PTT- name | ✅ PASS | Not in scope. |
| No `DateTime.Now` usage | ✅ PASS | Not in scope. |
| No `async/await` in lifecycle method | ✅ PASS | `DispatchShortcut` is `private void`; not async, not a lifecycle method. |

**NT8 Check: PASS**

---

### Test Coverage

| Item | Status | Evidence |
|------|--------|----------|
| `[Fact] DispatchShortcut_KeyB_CallsBreakEvenWithBeBuffer` present | ✅ PASS | Ticket §"Test Requirement": exact method name stated at lines 111, 126 |
| Test verifies Key.B uses `_beBuffer` value (not hardcoded `2`) | ✅ PASS | Option A asserts `mockEngine.Verify(e => e.BreakEven(..., 3), Times.Once)` after setting `_beBuffer = 3` via reflection. Option B asserts `_beBufferBox` field does not exist (Assert.Null). Both exclude the hardcoded `2` fallback. |
| Test count delta stated: 128 → 129 | ✅ PASS | Ticket §"Test Requirement" line 109: "New test count: 128 → 129" |
| xUnit framework (no NUnit / MSTest) | ✅ PASS | Ticket explicitly states `[Fact] only — NOT NUnit, NOT MSTest` |
| Fallback strategy documented for WPF non-instantiability | ✅ PASS | Option B (compile-time assertion via reflection) documented with clear minimum acceptance bar |

**Test Coverage: PASS**

---

### Scan Checklist (7-Scan Defense-in-Depth Contract)

All 7 scans present in ticket §"7-Scan Checklist (Engineer Contract)". Each scan includes:
- the exact shell command to run
- the expected result (0 / zero)
- a rationale tying the scan to the specific change

| Scan | Command | Expected | Present in Ticket | Status |
|------|---------|----------|-------------------|--------|
| SCAN-01 | `grep -r "lock(" src/ --include="*.cs"` | 0 results | ✅ Line 185 | ✅ PASS |
| SCAN-02 | `grep -rn "async void " src/ --include="*.cs"` | 0 non-handler results | ✅ Line 188 | ✅ PASS |
| SCAN-03 | `grep -rn "throw new " src/ --include="*.cs"` | 0 hot-path results | ✅ Line 191 | ✅ PASS |
| SCAN-04 | `grep -rn "return null;" src/ --include="*.cs"` | 0 new results | ✅ Line 194 | ✅ PASS |
| SCAN-05 | `grep -rn "_beBufferBox" src/ --include="*.cs"` | 0 results | ✅ Lines 197–199 | ✅ PASS |
| SCAN-06 | `python scripts/complexity_audit.py` | DispatchShortcut CYC = 5 | ✅ Lines 201–203 | ✅ PASS |
| SCAN-07 | `grep -rn "\?\." src/PropTraderTools/TradeCopierPanel.cs` | 0 null-conditional event unsubscriptions | ✅ Lines 205–207 | ✅ PASS |

**SCAN-05 specifically checks `_beBufferBox` → 0**: ✅ Confirmed at lines 197–199. Ticket explicitly labels it "the primary correctness gate for B26: any remaining reference = incomplete fix."

All 7 scans present with expected result = 0 (or a specific non-zero baseline for SCAN-06 CYC = 5). SCAN-06 reports CYC = 5, not 0 — this is correct; the scan verifies no CYC regression, not a zero count. This is a valid, well-specified scan target.

**Scan Checklist: PASS**

---

### File Routing

| Item | Status | Evidence |
|------|--------|----------|
| C# source path points to Wave workspace | ✅ PASS | `src/PropTraderTools/TradeCopierPanel.cs` under `C:\WSGTA\universal-or-strategy` |
| Test file path in Wave workspace | ✅ PASS | `tests/PropTraderTools.Tests/CopyEngineTests.cs` — under Wave workspace |
| No paths pointing to Director workspace | ✅ PASS | No `universal-or-strategy-director` paths in ticket |

**File Routing: PASS**

---

### Build & Sync Completeness

| Item | Status | Evidence |
|------|--------|----------|
| Build command present | ✅ PASS | `dotnet build src/PropTraderTools/PropTraderTools.csproj` → 0 errors, 0 warnings |
| `CS0103` guard note for field-deletion order | ✅ PASS | Ticket warns: apply Edit 2 before Edit 1 to avoid dangling-reference compilation errors |
| `ptt-sync-and-verify.ps1` present | ✅ PASS | Ticket §"Build Verification": `powershell -File scripts\ptt-sync-and-verify.ps1` → 0 MISMATCH |
| F5 in NinjaTrader 8 mentioned | ✅ PASS | "Then press F5 in NinjaTrader 8 to recompile. GREEN = ticket complete." |

**Completeness: PASS**

---

### VERDICT: TICKET_REVIEW_PASS

All 8 gate categories pass. No violations found.

---

## Overall

| Category | T1 |
|----------|----|
| Traceability | ✅ PASS |
| Edit Correctness | ✅ PASS |
| JS Rule Pre-Check | ✅ PASS |
| NT8 Check | ✅ PASS |
| Test Coverage | ✅ PASS |
| Scan Checklist (7 scans) | ✅ PASS |
| File Routing | ✅ PASS |
| Build & Sync Completeness | ✅ PASS |

## Overall: TICKET_REVIEW_PASS

Zero violations. Zero warnings requiring architect action.  
Safe to spawn ptt-engineer for T1 execution.

---

*ptt-ticket-reviewer · PTT-COPIER-B26 · 2026-07-07 · TICKET_REVIEW_PASS*
