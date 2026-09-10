# Ticket Review: PTT-REPAIRS-08-JS002
# JS-002 return-null repairs in CopyEngine.cs
# Reviewer: ptt-ticket-reviewer (Phase 3.5)
# Review Date: 2026-09-10 (Correction Cycle 2)
# Input: 04-tickets.md (REVISED), 02-architecture-plan.md (REVIEW_PASS Cycle 2), CopyEngine.cs (source)
# Output: docs/brain/PTT-REPAIRS-08-JS002/04-ticket-review.md

---

## Correction Cycle 2 -- V1, V2, V3 Fix Verification

| ID | Fix Required | Ticket | Status |
|----|-------------|--------|--------|
| V1 | Add [Fact] `FindRule_NoMatch_ReturnsDefault` to T1 test table | T1 | FIXED -- present at ticket line 329 |
| V2 | Change "6 substitutions" to "5" in scope lock and success criteria | T1 | FIXED -- scope lock (line 61) says "5"; success criteria (line 335) says "5" |
| V3 | Add [Fact] `FindPositionPublic_NoMatch_ReturnsNull` to T2 test table | T2 | FIXED -- present at ticket line 720 |

All 3 prior violations resolved. Full re-review below.

---

---

## T1 -- Struct-Nullable Cosmetic: `return null` -> `return default`

### Traceability

PASS.

All 5 described edit sites map to spec requirements and the architecture plan:

| Method | Plan Section | Spec Req | Status |
|--------|-------------|----------|--------|
| FindMatchingRule (L1997) | Plan Site 2 | REQ-JS002 | PASS |
| CaptureLinkedTargetPrice (L3030) | Plan Site 3 | REQ-JS002 | PASS |
| FindFollowerRuleForOrder (L4439) | Plan Site 8 | REQ-JS002 | PASS |
| FindRule (L5990) | Plan Sites 11-12 | REQ-JS002 | PASS |
| FindRule (L5996) | Plan Sites 11-12 | REQ-JS002 | PASS |

No phantom work. No plan-required site missing from this ticket.

### JS Pre-Check

PASS.

| Rule | Check | Finding |
|------|-------|---------|
| JS-002 | No `return null` for Nullable<struct> return types | PASS -- `return default` substitutions only; no `return null` text introduced |
| JS-021 | No `lock()` introduced | PASS -- cosmetic body edits only; no lock() added or touched |
| JS-001 | No new `throw` | PASS -- no throw keyword in any change |
| JS-013 | CYC <= 8 per method | PASS -- cosmetic changes only; no branches added; all methods CYC 3-5 |

### CYC Pre-Check

PASS.

| Method | Pre-fix CYC | Post-fix CYC | Source Verified | <= 8? |
|--------|------------|-------------|-----------------|-------|
| FindMatchingRule | 3 | 3 | L1987-1998: foreach + if = CYC 3 | YES |
| CaptureLinkedTargetPrice | 5 | 5 | L3027-3043: if + foreach + if + elseif = CYC 5 | YES |
| FindFollowerRuleForOrder | 5 | 5 | L4425-4440: foreach + continue + call + if = CYC 5 | YES |
| FindRule | 3 | 3 | L5987-5997: if + foreach + if = CYC 3 | YES |

### NT8 Check

PASS. All changed methods are pure query methods on already-loaded objects.
No NT8 lifecycle methods, no Account.All, no CreateOrder, no DateTime.Now.

### Test Coverage

PASS. (V1 and V2 fixes applied.)

`FindRule` has two distinct changed exit paths, both now covered:

| Test Name | Assertion | Path Covered | Status |
|-----------|-----------|--------------|--------|
| `FindMatchingRule_NoMatch_ReturnsDefaultNotNull` | `Assert.False(result.HasValue)` | After-foreach path | PASS |
| `CaptureLinkedTargetPrice_InvalidSuffix_ReturnsDefault` | `Assert.False(result.HasValue)` | Early-return guard | PASS |
| `FindFollowerRuleForOrder_NoMatch_ReturnsDefault` | `Assert.False(result.HasValue)` | After-foreach path | PASS |
| `FindRule_NullInstrument_ReturnsDefault` | `Assert.False(result.HasValue)` | Null-guard path (L5990) | PASS |
| `FindRule_NoMatch_ReturnsDefault` | `Assert.False(result.HasValue)` | Not-found path (L5996) -- V1 fix | PASS |

All 5 distinct exit paths covered. All methods are `private` or `internal`. Contract satisfied.

### Scan Checklist

PASS.

SCAN-1 through SCAN-7 are all present in the T1 body (lines 273-313).
All 7 scans include verify command, PASS criterion, and pre-assessment.
Labels use single-digit form `SCAN-1` through `SCAN-7` (vs canonical `SCAN-01`) -- cosmetic;
engineer contract is substantively intact.

### File Routing

PASS. All paths target `src/PropTraderTools/CopyEngine.cs`. No Director workspace paths.

### Scope Lock

PASS. Scope lock states: "ONLY T1 changes (5 `return null` -> `return default` substitutions
across 4 methods)." Count matches source and change inventory. (V2 fix applied.)

### Caller Coverage

PASS.

| Method | Caller | Null-Guard | Action |
|--------|--------|------------|--------|
| FindMatchingRule | L1522: `if (matchedRule == null)` | HasValue pattern -- safe | None |
| CaptureLinkedTargetPrice | L2838/L2842: `if (capturedTargetPrice.HasValue)` | HasValue pattern -- safe | None |
| FindFollowerRuleForOrder | L4390/L4391: `if (!matchedRule.HasValue || followerIndex < 0)` | HasValue pattern -- safe | None |
| FindRule | Multiple callers: `== null` or `.HasValue` | Already safe for both null and default | None |

### T1 VERDICT: TICKET_REVIEW_PASS

---

---

## T2 -- Reference-Type Annotation: T -> T? plus propagation

### Traceability

PASS.

All annotation sites map to spec requirements and the architecture plan:

| Method / Site | Plan Section | Spec Req | Status |
|---------------|-------------|----------|--------|
| FindBePosition (L1260) | Plan Site 1 | REQ-JS002 | PASS |
| FindLeaderCollateralOrder return (L3132) | Plan Sites 4-5 | REQ-JS002 | PASS |
| FindLeaderCollateralOrder caller L3301 | Plan Sites 4-5 | REQ-CALLER-PROPAGATION | PASS |
| ResolveNullFollowerSlot (L5950) | Plan Sites 9-10 | REQ-JS002, REQ-NT8-CONTRACT | PASS |
| FindPosition (L6075) | Plan Site 13 | REQ-JS002 | PASS |
| IsFlat parameter (L6008) | Plan Site 13 propagation | REQ-CALLER-PROPAGATION | PASS |
| SubmitMarketFlattenOrder parameter (L5501) | Plan Site 13 propagation | REQ-CALLER-PROPAGATION | PASS |
| FindPositionPublic (L6086) | Plan Site 13 propagation | REQ-JS002, REQ-CALLER-PROPAGATION | PASS |

No phantom work. No plan-required site missing from this ticket.

### JS Pre-Check

PASS.

| Rule | Check | Finding |
|------|-------|---------|
| JS-002 | All reference-type returns that returned null now declare T? | PASS -- annotation-only; all target types are reference types (Position, Order, Account) |
| JS-021 | No `lock()` introduced | PASS -- annotation-only changes; Section 8 of plan confirms no threading changes |
| JS-001 | No new `throw` | PASS -- no throw keyword in any change |
| JS-013 | CYC <= 8 per method | PASS -- all annotation-only; no branches added; highest existing CYC is 3 |

### CYC Pre-Check

PASS.

| Method | Pre-fix CYC | Post-fix CYC | Source Verified | <= 8? |
|--------|------------|-------------|-----------------|-------|
| FindBePosition | 3 | 3 | L1260-1272: foreach + if + && = CYC 3 | YES |
| FindLeaderCollateralOrder | 3 | 3 | L3132-3144: if + foreach + if = CYC 3 | YES |
| ResolveNullFollowerSlot | 3 | 3 | L5950-5978: if + TryGetValue + if = CYC 3 | YES |
| FindPosition | 2 | 2 | L6075-6081: foreach + if = CYC 2 | YES |
| IsFlat | 1 | 1 | L6008-6011: return expr = CYC 1 | YES |
| SubmitMarketFlattenOrder | 3 | 3 | L5501-5513: if + ternary = CYC 3 | YES |
| FindPositionPublic | 1 | 1 | L6086-6087: expression body delegate = CYC 1 | YES |

### NT8 Check

PASS.

ResolveNullFollowerSlot `return null` preservation: VERIFIED against source.
- L5955: `return null; // NT8 pattern: null = slot could not be resolved` -- must be preserved verbatim.
- L5977: `return null; // NT8 pattern: null = slot could not be resolved` -- must be preserved verbatim.

Ticket instruction (line 628): "Both `return null; // NT8 pattern: null = slot could not be resolved`
lines must remain exactly as they are." Correct. ✅

NT8 additional verification block present in T2 body (lines 701-705). ✅

No NT8 lifecycle method changes. No new Account.All calls. No new CreateOrder. ✅

### Test Coverage

PASS. (V3 fix applied.)

All `internal` (and additional `private`) changed methods have [Fact] tests specified:

| Test Name | Assert | Method Covered | Visibility |
|-----------|--------|---------------|------------|
| `FindBePosition_NoMatch_ReturnsNull` | `Assert.Null(result)` | FindBePosition | internal ✅ |
| `FindLeaderCollateralOrder_NullAccount_ReturnsNull` | `Assert.Null(result)` | FindLeaderCollateralOrder | private static |
| `FindPosition_NoMatch_ReturnsNull` | `Assert.Null(result)` | FindPosition | private |
| `IsFlat_NullPosition_ReturnsTrue` | `Assert.True(result)` | IsFlat | private static |
| `FindPositionPublic_NoMatch_ReturnsNull` | `Assert.Null(result)` | FindPositionPublic | internal ✅ -- V3 fix |

Both internal methods (FindBePosition, FindPositionPublic) have [Fact] tests. Contract satisfied.

### Scan Checklist

PASS.

SCAN-1 through SCAN-7 are all present in the T2 body (lines 655-700).
All 7 scans include verify command, PASS criterion, and pre-assessment.
Same cosmetic note as T1: single-digit labels `SCAN-1` through `SCAN-7`. Substantively complete.

### File Routing

PASS. All paths target `src/PropTraderTools/CopyEngine.cs`.
External files (TradeCopierPanel.cs, PttBreakEvenSwap.cs) correctly flagged as read-only.

### Scope Lock

PASS. "Engineer must implement ONLY T2 changes (7 return-type annotations + 1 variable annotation +
2 parameter annotations). Do NOT edit TradeCopierPanel.cs or PttBreakEvenSwap.cs." ✅

### Caller Coverage

PASS.

- FindBePosition: 1 caller (L1248), explicit null check, no action. ✅
- FindLeaderCollateralOrder: 1 caller (L3301), requires `Order?` annotation -- action itemized. ✅
- ResolveNullFollowerSlot: 1 caller (L5938), explicit null check, no action. ✅
- FindPosition: 20+ callers enumerated by pattern (IsFlat, explicit checks, SubmitMarketFlattenOrder). ✅
- IsFlat: all callers assessed as nullable-compatible. ✅
- SubmitMarketFlattenOrder: all callers assessed as nullable-compatible. ✅
- FindPositionPublic: all 3 external callers listed with file, line, and null-guard. ✅

### T2 VERDICT: TICKET_REVIEW_PASS

---

---

## T3 -- Array Return-Type Annotation

### Traceability

PASS.

| Method / Site | Plan Section | Spec Req | Status |
|---------------|-------------|----------|--------|
| ResolveMultipliers (L7352) | Plan Site 14 | REQ-JS002 | PASS |
| DtoToRule caller (L7295) | Plan Site 14 | REQ-CALLER-PROPAGATION | PASS |

No phantom work. No plan-required site missing.

### JS Pre-Check

PASS.

| Rule | Check | Finding |
|------|-------|---------|
| JS-002 | `int[]` return type annotated `int[]?` | PASS -- annotation-only; `return null` in body kept for behavioral contract |
| JS-021 | No `lock()` | PASS |
| JS-001 | No new `throw` | PASS |
| JS-013 | CYC <= 8 | PASS -- ResolveMultipliers CYC=2; no branches added |

### CYC Pre-Check

PASS.

| Method | Pre-fix CYC | Post-fix CYC | Source Verified | <= 8? |
|--------|------------|-------------|-----------------|-------|
| ResolveMultipliers | 2 | 2 | L7352-7357: if (||) = CYC 2 | YES |

### NT8 Check

PASS. No NT8 API changes. `return null` in ResolveMultipliers body preserved for
CopyRule.Create null-vs-empty semantic contract. Correctly documented in scope lock:
"Do NOT change the `return null;` in the method body to anything else."

### Test Coverage

PASS.

`ResolveMultipliers` is `internal static`. Three [Fact] tests provided:

| Test Name | Assert | Path Covered |
|-----------|--------|-------------|
| `ResolveMultipliers_NullDto_ReturnsNull` | `Assert.Null(result)` | null FollowerMultipliers guard |
| `ResolveMultipliers_EmptyMultipliers_ReturnsNull` | `Assert.Null(result)` | empty array guard |
| `ResolveMultipliers_ValidMultipliers_ReturnsArray` | `Assert.NotNull(result); Assert.Equal(N, result.Length)` | valid non-empty path |

All 3 paths (null, empty, valid) covered. ✅

### Scan Checklist

PASS.

SCAN-1 through SCAN-7 are all present in the T3 body (lines 866-912).
All 7 scans include verify command, PASS criterion, and pre-assessment. ✅

### File Routing

PASS. All paths target `src/PropTraderTools/CopyEngine.cs`.

### Scope Lock

PASS. "Engineer must implement ONLY T3 changes: 1 return-type annotation and 1 variable annotation." ✅

### Caller Coverage

PASS. Single caller (L7295 in DtoToRule) listed with before/after, null-guard reasoning,
and downstream CopyRule.Create parameter compatibility assessed. ✅

### T3 VERDICT: TICKET_REVIEW_PASS

---

---

## 15-Site Completeness Check

All 15 sites accounted for across the 3 tickets:

| # | Site | Method | Ticket | Status |
|---|------|--------|--------|--------|
| 1 | L1271 | FindBePosition | T2 (return type annotation) | COVERED |
| 2 | L1997 | FindMatchingRule | T1 (return default) | COVERED |
| 3 | L3030 | CaptureLinkedTargetPrice | T1 (return default) | COVERED |
| 4 | L3135 | FindLeaderCollateralOrder (path 1) | T2 (return type annotation) | COVERED |
| 5 | L3143 | FindLeaderCollateralOrder (path 2) | T2 (return type annotation) | COVERED |
| 6 | L3951 | FindFollowerBracketOrder | N/A | ALREADY COMPLIANT (Order?) -- source verified ✅ |
| 7 | L4150 | FindFollowerEntryOrder | N/A | ALREADY COMPLIANT (Order?) -- source verified ✅ |
| 8 | L4439 | FindFollowerRuleForOrder | T1 (return default) | COVERED |
| 9 | L5955 | ResolveNullFollowerSlot (path 1) | T2 (return type annotation; return null preserved) | COVERED |
| 10 | L5977 | ResolveNullFollowerSlot (path 2) | T2 (return type annotation; return null preserved) | COVERED |
| 11 | L5990 | FindRule (path 1 -- null guard) | T1 (return default) | COVERED |
| 12 | L5996 | FindRule (path 2 -- not found) | T1 (return default) | COVERED |
| 13 | L6080 | FindPosition | T2 (return type annotation) | COVERED |
| 14 | L7355 | ResolveMultipliers | T3 (return type annotation) | COVERED |
| 15 | L7370 | FindFollowerAccount | N/A | ALREADY COMPLIANT (Account?) -- source verified ✅ |

**All 15 sites: 12 in tickets + 3 already-compliant = 15. COMPLETE. PASS.**

No site appears in more than one ticket. No site is unaccounted for.

---

## Violation Register

No active violations. All 3 prior violations (V1, V2, V3) are resolved.

---

## Overall: TICKET_REVIEW_PASS

All 3 tickets pass all checks:

| Ticket | Traceability | JS Pre-Check | CYC Pre-Check | NT8 Check | Test Coverage | Scan Checklist | File Routing | Scope Lock | Caller Coverage | VERDICT |
|--------|-------------|-------------|--------------|-----------|---------------|----------------|-------------|------------|-----------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |
| T3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **PASS** |

---

## Return

**TICKET_REVIEW_PASS**

Orchestrator is cleared to spawn the engineer (Phase 4).
Engineer reads `04-ticket-review.md` then `04-tickets.md`.
Execution order: T1 -> T2 -> T3, each ticket built and verified (SCAN-1 through SCAN-7) before the next begins.

---

*ptt-ticket-reviewer -- PTT-REPAIRS-08-JS002 -- Phase 3.5 -- Correction Cycle 2 -- 2026-09-10*
