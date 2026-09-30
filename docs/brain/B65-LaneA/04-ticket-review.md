# B65-LaneA Ticket Review

**Block**: B65-LaneA
**Phase**: 3.5 (Ticket Review)
**Date**: 2026-08-12
**Reviewer**: ptt-ticket-reviewer
**Input ticket**: docs/brain/B65-LaneA/04-tickets.md
**Architecture plan**: docs/brain/B65-LaneA/02-architecture-plan.md
**Plan review gate**: docs/brain/B65-LaneA/02-plan-review.md — REVIEW_PASS confirmed
**Source baselines read**:
  - CopyEngine.cs lines 745-760 (IsExitSignalName closing brace at line 758 confirmed)
  - CopyEngine.cs lines 1064-1085 (TryDispatchLeaderFlat 7-param current signature confirmed)
  - CopyEngineTests.cs lines 2855-3010 (B61 test region; 5 object[] invocations confirmed)
**Rules Catalog read**: docs/standards/jane-street/RULES_CATALOG.md lines 1-150

---

## T1 — Post-fill leader close propagation via IsNativeExitName

**Ticket ID**: B65-T1
**Spec Req IDs**: DW-B65-01 (= DW-B60-01)
**Related**: NT8_FULL_REFERENCE.md line 1721 (position race), lines 844-845 (Order.Name semantics)

---

### Traceability

**PASS**

All 5 changes trace directly to DW-B65-01 and the approved architecture plan:

| Change | Maps to |
|--------|---------|
| CHANGE 1: Add `IsNativeExitName` | DW-B65-01 fix; Plan Section 3 |
| CHANGE 2: Modify `TryDispatchLeaderFlat` (guard 3 bypass) | DW-B65-01 fix; Plan Section 4 |
| CHANGE 3: Update call site in `OnOrderUpdate` | Plan Section 5 (sole call site) |
| CHANGE 4: Update 5 B61 object[] invocations | Plan Section 6 (signature migration) |
| CHANGE 5: Insert T_B65_01-09 | Plan Section 7 (all 9 tests specified) |

No phantom work detected. No plan item absent from ticket.

---

### JS P0 Pre-Check

**PASS**

| Rule | Check | Evidence |
|------|-------|----------|
| JS-021: no lock() | PASS | `IsNativeExitName` and `TryDispatchLeaderFlat` are pure static helpers with no shared mutable state. No `lock()` call described anywhere in ticket Changes 1-3. |
| JS-001: no throw | PASS | Both methods return `bool` at every code path. No `throw new` in any of the 5 changes. Ticket comment `// JS-001: no throw` present on both methods. |
| JS-002: no return null | PASS | Both methods return `bool`. Null input to `IsNativeExitName` handled by `return false` (not `return null`). Ticket comment `// JS-002: no null return` present on both methods. |
| JS-003: no string sentinel for mode/state | PASS | No empty string or missing-key sentinel. `IsNativeExitName` uses explicit string comparisons (equality + prefix) returning bool. |

---

### CYC Pre-Check

**PASS**

| Method | CYC Estimate | Within JS ≤8? |
|--------|-------------|---------------|
| `IsNativeExitName` | 1 base + 5 decisions (null, "Close", "Flatten", Rev-prefix, Exit-prefix) = **6** | YES |
| `TryDispatchLeaderFlat` (modified) | 1 base + state-guard (2 conditions) + isFollower + `!IsNativeExitName` + `hasOpenPosition` (&&) + foreach loop + null-skip = **7** strict McCabe | YES |

Both methods well within CYC ≤ 8. No extraction required.

---

### NT8 Check

**PASS**

| Constraint | Result | Evidence |
|------------|--------|----------|
| NT8_FULL_REFERENCE.md line 1721 cited in code comment | PASS | CHANGE 1 comment block cites `NT8_FULL_REFERENCE.md line 1721` verbatim; CHANGE 2 comment block cites it in the rationale sentence. |
| NT8_FULL_REFERENCE.md lines 844-845 cited for Order.Name | PASS | CHANGE 3 states "NT8 sets Order.Name at submission time (NT8_FULL_REFERENCE.md lines 844-845)." |
| `IsNativeExitName` name collision confirmed absent | PASS | CHANGE 1 comment: `// NT8-VERIFY-03/04: "IsNativeExitName" confirmed NOT present in NT8 Custom codebase.` |
| No async/await in lifecycle method | PASS | No async/await in any described change. |
| No `sealed` on `TradeCopierWindow` | PASS | No WPF window touched. |
| No `DateTime.Now` | PASS | No DateTime usage in scope. |
| No hardcoded hex color | PASS | No UI code touched. |
| No `FontFamily` on WPF element | PASS | No UI code touched. |
| `CreateOrder` name prefix (N/A) | N/A | No `CreateOrder` call in scope. |

---

### Test Coverage

**PASS**

All new public/internal methods have specified [Fact] tests:

| Method | Tests | All [Fact]? |
|--------|-------|-------------|
| `IsNativeExitName` (new `internal static`) | T_B65_01 (null), T_B65_02 ("Close"), T_B65_03 ("Flatten"), T_B65_04 (Rev-prefix), T_B65_05 (Exit-prefix), T_B65_06 (PTT- prefix false), T_B65_07 (arbitrary false) | YES |
| `TryDispatchLeaderFlat` (modified `private static`) | T_B65_08 (race bypass — core regression), T_B65_09 (non-native guard still fires) + B61 regression tests T_B61_01-04 | YES |

Critical regression tests verified:
- **T_B65_08**: `orderName="Close"`, `hasOpenPosition=TRUE` (race), `result=True` (bypass), `flattenCallCount=0` (0 followers). Correctly proves guard (3) is bypassed for native exits despite stale position. ✓
- **T_B65_09**: `orderName="BuyLimit"`, `hasOpenPosition=TRUE`, `result=False` (guard blocks). Confirms bypass is exclusive to native exit names. ✓

B61 tests: 5 invocations updated from 7-element to 8-element `object[]`. Per-invocation assertion analysis confirms all outcomes unchanged. `Assert.False`/`Assert.True` results unaffected.

---

### Scan Checklist

**PASS**

All 7 scans present in ticket section "7-SCAN CHECKLIST (MANDATORY — engineer contract)":

| Scan | Present | Exact Command | Expected Result |
|------|---------|---------------|-----------------|
| SCAN-01 — lock() scan | ✓ | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | Zero results |
| SCAN-02 — throw scan | ✓ | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | Zero in new/modified code |
| SCAN-03 — return null scan | ✓ | `grep -n "return null" src/PropTraderTools/CopyEngine.cs` | Zero in `IsNativeExitName` and `TryDispatchLeaderFlat` |
| SCAN-04 — CYC scan | ✓ | `python scripts/complexity_audit.py` | Both methods CYC ≤ 8 |
| SCAN-05 — ASCII scan | ✓ | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | No new non-ASCII lines vs pre-existing baseline |
| SCAN-06 — Build scan | ✓ | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | Zero errors, zero new warnings |
| SCAN-07 — Test scan | ✓ | `dotnet test` | T_B65_01-09 and T_B61_01-04 all PASS |

Each scan also specifies failure action. Engineer contract is complete.

---

### File Routing

**PASS**

Both files route to Wave workspace only:
- `src/PropTraderTools/CopyEngine.cs` → `C:\WSGTA\universal-or-strategy\src\PropTraderTools\` ✓
- `src/PropTraderTools/CopyEngineTests.cs` → `C:\WSGTA\universal-or-strategy\src\PropTraderTools\` ✓

No Director workspace paths for `.cs` files.

---

### VERDICT: TICKET_REVIEW_PASS

---

## VIOLATIONS

None. Zero violations found across all checks.

---

## Completeness Notes (informational — not violations)

The following items were noted and confirmed satisfactory:

1. **T_B65_04 multi-assert**: Tests `"RevLong"`, `"RevShort"`, and `"Reversal"` within a single `[Fact]`. Acceptable — all three values exercise the same `StartsWith("Rev")` branch. Not a CYC concern.

2. **T_B65_05 multi-assert**: Tests `"ExitLong"` and `"Exit"` within a single `[Fact]`. Acceptable — same branch.

3. **Line number drift warning**: Ticket explicitly instructs engineer to re-read actual line numbers before editing on Changes 2, 4, and 5. Source baseline confirms Change 1 shifts downstream lines by ~19. Engineer guidance is correct and complete.

4. **T_B65_08 assertion design**: `Assert.Equal(0, flattenCallCount)` with 0 followers in rule is the correct test design (NT8 `Account` is not constructible in test context). Result `== true` proves guards passed; this is consistent with the T_B61_04 precedent.

5. **`GetTryDispatchLeaderFlat()` helper comment**: The helper comment at source line 2855 still reads "private static, 7 params". After B65 it will be 8 params. The ticket does not specify updating this comment. This is a minor doc-comment drift but NOT a blocking violation — the method resolves by name only (no parameter type array), so behavior is unaffected.

---

## Overall: TICKET_REVIEW_PASS

All individual checks PASS. Zero Jane Street rule violations. Zero traceability gaps. Zero missing test coverage. Zero NT8 constraint violations. 7-scan checklist complete and defense-in-depth chain intact. Safe to spawn ptt-engineer.
