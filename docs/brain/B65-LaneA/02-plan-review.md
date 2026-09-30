# B65-LaneA Plan Review

**Block**: B65-LaneA
**Phase**: 2 (Plan Review)
**Date**: 2026-08-12
**Reviewer**: ptt-plan-reviewer
**Input**: docs/brain/B65-LaneA/02-architecture-plan.md
**Source baselines read**: CopyEngine.cs lines 745-765 + 1064-1085; CopyEngineTests.cs lines 2855-3001
**Rules Catalog read**: docs/standards/jane-street/RULES_CATALOG.md lines 1-120

---

## VIOLATIONS

None. Zero violations found.

---

## Criterion-by-Criterion Results

### CRITERION 1 — NT8 Evidence

| Sub-check | Result | Evidence |
|---|---|---|
| NT8-VERIFY-01: line 1721 cited as root cause | PASS | Plan Section 2 explicitly quotes NT8_FULL_REFERENCE.md line 1721: "Changes to positions will not be reflected till at least the next OnBarUpdate() event after an order fill." |
| NT8-VERIFY-02: line 845 cited for Order.Name semantics | PASS | Plan Section 2 (NT8-VERIFY-02) cites NT8_FULL_REFERENCE.md lines 844-845 for Order.Name string assignment. |
| NT8-VERIFY-03/04: IsNativeExitName not in @2Custom (no collision) | PASS | Plan Section 2 (NT8-VERIFY-03) reports jcodemunch search_text result count = 0. Net-new symbol. No overload ambiguity confirmed. |

**CRITERION 1: PASS**

---

### CRITERION 2 — IsNativeExitName Helper Spec

| Sub-check | Result | Evidence |
|---|---|---|
| Returns false for null | PASS | Plan Section 3: `if (name == null) return false;` |
| Returns true for "Close", "Flatten", StartsWith("Rev", Ordinal), StartsWith("Exit", Ordinal) | PASS | Plan Section 3: all four branches present in method spec. |
| Returns false for "PTT-*" names | PASS | No PTT- branch in method; falls through to `return false`. Confirmed by plan comparison table (Section 3): IsNativeExitName("PTT-Flatten") = false. |
| CYC = 6 (≤ 8) | PASS | Plan Section 3: 1 base + 5 decision points = CYC 6. Explicitly stated. |
| Distinct from IsExitSignalName (no PTT- branch) | PASS | Plan Section 3 comparison table confirms the two methods differ precisely on the PTT- prefix branch. Not merged. |
| Insert position documented | PASS | Plan Section 3: "after line 758, the closing brace of IsExitSignalName, before the blank line at line 760." Source confirms IsExitSignalName closes at line 758 (verified). |

**CRITERION 2: PASS**

---

### CRITERION 3 — TryDispatchLeaderFlat Signature Change

| Sub-check | Result | Evidence |
|---|---|---|
| New signature has 8 params, orderName is 4th (after state) | PASS | Plan Section 4: `Account account, Instrument instrument, OrderState state, string orderName, CopyRule rule, ...` — orderName is 4th. |
| Guard (3): `!IsNativeExitName(orderName) && hasOpenPosition(...)` → return false | PASS | Plan Section 4: `if (!IsNativeExitName(orderName) && hasOpenPosition(account, instrument)) return false;` Semantics analysis correct. |
| Comment cites NT8_FULL_REFERENCE.md line 1721 | PASS | Plan Section 4 method spec includes: `// NT8_FULL_REFERENCE.md line 1721` inline comment on guard (3). |
| CYC ≤ 8 | PASS | Plan Section 4: CYC 5 (spec-comment) / 7 (strict McCabe). Both ≤ 8. |
| No lock(), no throw, no return null | PASS | Plan Section 4 method header comment: "JS-021: no lock. JS-001: no throw. JS-002: no null return." Method only returns bool. |

**CRITERION 3: PASS**

---

### CRITERION 4 — Call-Site Update

| Sub-check | Result | Evidence |
|---|---|---|
| Only one call site (private static) confirmed | PASS | Plan Section 5: "TryDispatchLeaderFlat is private static. Only one call site exists in the codebase: OnOrderUpdate at line 651. No other updates required." Private static scope confirmed in source (line 1070). |
| e.Order.Name added as 4th arg | PASS | Plan Section 5 shows new call: `e.Order.Account, e.Order.Instrument, e.Order.OrderState, e.Order.Name, matchedRule.Value, ...` |
| Current call (7 args) matches current source | PASS | Source CopyEngine.cs line 1070-1073 has 7 params. Plan's current-call snippet shows 7 args. Consistent. |

**CRITERION 4: PASS**

---

### CRITERION 5 — B61 Test Migration

| Sub-check | Result | Evidence |
|---|---|---|
| All B61 invocations identified | PASS | Plan Section 6 identifies 5 invocations: T_B61_01, T_B61_02, T_B61_03, T_B61_04 primary, T_B61_04 Cancelled sub-assertion. Source lines 2875-2884, 2905-2914, 2935-2944, 2976-2985, 2993-2999 confirm all 5 object[] blocks. More complete than the minimum 4 required. |
| "BuyLimit" (non-native) added as 4th arg for all invocations | PASS | Plan Section 6 table: all 5 invocations insert `"BuyLimit"` as new element at position 3 (0-indexed). |
| All B61 assertion outcomes unchanged | PASS | Plan Section 6 per-invocation analysis: T_B61_01 still returns false (hasOpenPosition=true, non-native), T_B61_02 still returns false (state guard fires first), T_B61_03 still returns false (follower guard fires first), T_B61_04 still returns true (all guards pass, non-native, flat), T_B61_04-Cancelled still returns true. All outcomes confirmed unchanged. |
| Reflection helper GetTryDispatchLeaderFlat compatible | PASS | Plan Section 6: helper uses GetMethod by name only (no type array). Single overload — still resolves correctly after param count change. Source line 2857-2859 confirmed. |

**CRITERION 5: PASS**

---

### CRITERION 6 — New Tests (T_B65_01–09)

| Sub-check | Result | Evidence |
|---|---|---|
| All 9 tests specified | PASS | Plan Section 7: T_B65_01 through T_B65_09 fully specified with Setup/Assert/Rationale. |
| T_B65_08 race bypass correctly tested | PASS | Plan Section 7, T_B65_08: orderName="Close", hasOpenPosition=true (race), result==true. The `result == true` assertion despite `hasOpenPosition=true` is the meaningful regression proof for DW-B65-01. 0-followers design is consistent with T_B61_04 pattern (NT8 Account not constructible in test context). |
| T_B65_09 guard-still-works correctly tested | PASS | Plan Section 7, T_B65_09: orderName="BuyLimit", hasOpenPosition=true → result==false. Guard (3) blocks as required for non-native exits. |
| All tests are xUnit [Fact] | PASS | Plan Section 7 header: "All tests are xUnit [Fact] only. No NUnit, no MSTest." |

**CRITERION 6: PASS**

---

### CRITERION 7 — Jane Street P0 Compliance

| Rule | Result | Evidence |
|---|---|---|
| JS-021: no lock() | PASS | IsNativeExitName and TryDispatchLeaderFlat are pure static helpers with no shared mutable state. Plan Section 8 confirms. |
| JS-001: no throw | PASS | Both methods return bool at all code paths. No exceptions thrown. Plan Section 8 confirms. |
| JS-002: no return null | PASS | Both methods return bool. Null is an impossible return value. Plan Section 8 confirms. |
| CYC ≤ 8 all methods | PASS | IsNativeExitName CYC=6; TryDispatchLeaderFlat CYC=7 (strict McCabe). Both within limit. |
| ASCII-only string literals | PASS | All literals: "Close", "Flatten", "Rev", "Exit", "PTT-Flatten", "BuyLimit" — pure ASCII. Plan Section 8 confirms. |
| xUnit [Fact] only | PASS | Plan Section 8 and Section 7 both confirm xUnit-only test framework. |

**CRITERION 7: PASS**

---

### CRITERION 8 — Scan Checklist

| Scan | Present | Expected Result |
|---|---|---|
| SCAN-01: lock() scan | PASS | Zero results in CopyEngine.cs |
| SCAN-02: throw scan | PASS | Zero results in new/modified code |
| SCAN-03: return null scan | PASS | Zero results in IsNativeExitName and TryDispatchLeaderFlat |
| SCAN-04: CYC scan | PASS | Both methods CYC ≤ 8 |
| SCAN-05: ASCII scan | PASS | No new non-ASCII lines vs pre-existing baseline |
| SCAN-06: Build scan | PASS | Zero errors, zero new warnings |
| SCAN-07: Test scan | PASS | All T_B65_01-09 and T_B61_01-04 PASS |

All 7 scans present in Section 9. ✅

**CRITERION 8: PASS**

---

### CRITERION 9 — Files Changed

Plan Section 10 lists exactly:
- `src/PropTraderTools/CopyEngine.cs` — Insert IsNativeExitName + modify TryDispatchLeaderFlat
- `src/PropTraderTools/CopyEngineTests.cs` — Add T_B65_01-09 + update 5 B61 invocations

"No other files are touched." ✅

**CRITERION 9: PASS**

---

### CRITERION 10 — Deferred Backlog

| Sub-check | Result | Evidence |
|---|---|---|
| DW-B60-01 closure (this IS the fix) | PASS | Plan Section 11: DW-B65-01 (= DW-B60-01) listed as CLOSED by B65-LaneA Ticket-1. Resolution described as IsNativeExitName + guard bypass. |
| DW-B59-02 closure noted | PASS | Plan Section 3 and Section 11 both confirm DW-B59-02 CLOSED. Evidence: CopyEngine.cs line 755 already uses StartsWith("Rev", Ordinal). Source confirmed (line 755 verified). |
| Carry-forward items from B62 backlog listed | PASS | Plan Section 11 lists: DW-B58-01, DW-B58-02, DW-B58-03, DW-B54-01, PRE-EXISTING-01, PRE-EXISTING-02, PRE-EXISTING-03. All open items carried forward with status and priority. |

**CRITERION 10: PASS**

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|---|---|---|
| Root cause of post-fill race condition identified | YES | Section 1 + Section 2 (NT8-VERIFY-01) |
| NT8 API evidence cited for both line 1721 and Order.Name | YES | Section 2 |
| New IsNativeExitName helper fully specified | YES | Section 3 |
| IsNativeExitName distinct from IsExitSignalName | YES | Section 3 comparison table |
| TryDispatchLeaderFlat 8-param signature | YES | Section 4 |
| Guard (3) bypass logic correct | YES | Section 4 guard analysis |
| Call-site update in OnOrderUpdate | YES | Section 5 |
| B61 test migration (5 invocations) | YES | Section 6 |
| 9 new B65 tests specified | YES | Section 7 |
| Jane Street P0 compliance verified | YES | Section 8 |
| 7 scans listed for engineer/verifier | YES | Section 9 |
| Files in scope (2 files only) | YES | Section 10 |
| DW-B60-01 closed | YES | Section 11 |
| DW-B59-02 closed | YES | Section 3 + Section 11 |
| Carry-forward backlog items | YES | Section 11 |

---

## Final Verdict

**REVIEW_PASS**

All 10 criteria pass. Zero Jane Street rule violations (JS-001, JS-002, JS-021). Zero spec gaps. Zero NT8 API concerns. The plan is coherent, complete, and safe to proceed to Phase 3 (ticket generation).
