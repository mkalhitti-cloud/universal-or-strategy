# B153 Plan Review — Phase 2

**Block:** B153  
**Phase:** 2 — Plan Review  
**Reviewer:** ptt-plan-reviewer  
**Date:** 2026-09-13  
**Plan reviewed:** docs/brain/B153/02-architecture-plan.md  
**Sources:** 00-context.md, docs/standards/RULES_CATALOG.md  

---

## A. Lane-Split Gate Compliance

| Check | Result | Notes |
|-------|--------|-------|
| Gate result present in plan | PASS | "LANE-SPLIT GATE RESULT: LANES-APPROVED" (plan line 44) |
| Q1 = NO | PASS | All ticket pairs in different files or >50 lines apart |
| Q2 = NO | PASS | No cross-ticket design dependency stated or found |
| Q3 = YES | PASS | Each fix has standalone value (plan lines 30-35) |
| Q4 = YES | PASS | Each fix has independent SIM verification path (plan lines 36-43) |

**Context vs. plan discrepancy (non-blocking):** `00-context.md` labels the execution as "1 lane, sequential." The plan correctly notes this (plan line 47) — LANES-APPROVED is the architectural gate ruling; the orchestrator may still execute sequentially. No gate violation.

---

## B. Spec Coverage Matrix

| Requirement (from 00-context.md) | Addressed? | Plan Section |
|-----------------------------------|------------|--------------|
| REQ-1: Remove QX ceiling (`ResolveTargetCount`) | YES | Section 6, DW-B153-01 |
| REQ-2: Remove BE ceiling (`MoveStopToBreakEven`) | YES | Section 6, DW-B153-02 |
| REQ-3: Remove stop drag ceiling (3 coupled symbols) | YES | Section 6, DW-B153-03 |
| REQ-4: Remove `IsPttQxTarget` single-digit ceiling | YES | Section 6, DW-B153-04 |
| Closing DW-B107 (BE cap) and DW-B106 (QX cap) | YES | Section 1, Epic Summary |
| Quick2t / QAll2t intentionally unchanged | YES | Section 3, Section 11 |
| 10-scan requirement | YES | Section 10 (all 10 listed) |
| SIM Gate (5-target, 4-step) | YES | Section 12 |

**All spec requirements addressed. No missing items.**

---

## C. Architecture-Locked Items

| Item | Plan Stance | Compliant? |
|------|-------------|------------|
| DeriveLeaderBracketIndex — NO CHANGE | Explicitly locked (Section 3) | YES |
| IsNativeTargetOrder / IsPttTargetOrder — NO CHANGE | Explicitly locked (Section 3) | YES |
| Quick2t / QAll2t — NO CHANGE | Explicitly locked (Section 3, Section 11) | YES |
| NT8 AddOnBase API only | CONFIRMED — Account.Cancel/CreateOrder/Submit (Sections 3, 8) | YES |
| AtmStrategyChangeStopTarget() — BANNED | NOT USED, explicitly stated (Sections 3, 8) | YES |
| AtmStrategyCreate() — BANNED | NOT USED, explicitly stated (Sections 3, 8) | YES |
| Account.Change() — silent no-op | NOT USED, explicitly stated (Sections 3, 8) | YES |
| No lock() | CONFIRMED — FSM/Actor Enqueue model (Section 3) | YES |
| ASCII-only | ENFORCED (Section 3, JS Rule compliance table) | YES |
| DeduplicateByPrice handles residue | CONFIRMED for both T1 and T2 (Sections 3, 11) | YES |

---

## D. Fix Correctness Analysis

### DW-B153-01 (QX Ceiling)
`return Math.Min(raw, 3)` → `return raw`. Removes the hard cap. NT8 processes N QX orders. DeduplicateByPrice confirmed as residue guard. **Fix is correct.**

### DW-B153-02 (BE Ceiling)
Remove `while (targets.Count > 3) targets.RemoveAt(...)`. Removes the pre-loop trim. DeduplicateByPrice confirmed as residue guard. **Fix is correct.**

### DW-B153-03 (Stop Drag Ceiling — 3 sites + 2 propagation points)
- `TryParseStopSuffix`: `if (n < 1 || n > 3)` → `if (n < 1)` — allows leg 4+. Correct.
- `CaptureOtherLegTargetPrices`: `double[3]` + `for i <= 3` → `Dictionary<int,double>` + dynamic scan via `ParseTargetLegIndex`. Correctly captures all N legs.
- `ParseTargetLegIndex` (NEW): `PTT-TGT-Drag-` prefix check, excludeSuffix guard, TryParse → correct helper that returns -1 on any invalid/excluded input.
- `ResubmitCollateralLegs`: `for i <= 3` → `foreach kvp` over dictionary — processes all N legs.
- `HandleAtmStopSync` call site: type change `double[]` → `Dictionary<int,double>` propagated (plan Section 6E). **Fix is correct. All 5 change points identified and covered.**

### DW-B153-04 (IsPttQxTarget Ceiling)
- `name.Length != 9` → `name.Length < 9`: accepts T10+ (length 10-11). Correct.
- `name[8] >= '1' && name[8] <= '3'` → `int.TryParse(name.Substring(8), out int n) && n >= 1`: accepts T1..TN, rejects T0 and non-numeric. Correct for multi-digit target indices.
- Boundary verification: T1 (9 chars) → passes. T10 (10 chars) → passes. T0 → TryParse returns 0, `n >= 1` fails → rejected. PTT-QX-TX → TryParse fails → rejected. **Fix is correct.**

---

## E. Type Change Completeness (DW-B153-03)

Review criterion: `double[]` → `Dictionary<int,double>` must be traced to ALL call sites; `HandleAtmStopSync` must be included.

| Change point | Included in plan? |
|-------------|-------------------|
| `CaptureOtherLegTargetPrices` return type | YES (Section 6B) |
| `ResubmitCollateralLegs` parameter type | YES (Section 6D) |
| `HandleAtmStopSync` call site (local variable + parameter) | YES (Section 6E, plan line 338-343) |
| Cross-ticket dependency note requiring atomic commit | YES (Section 7, plan line 427) |

Both methods are `private`, so the compiler enforces completeness. SCAN-10 (`dotnet build → 0 errors`) is the hard gate. **Type change completeness: PASS.**

---

## F. CYC Analysis

| Method | Plan Claim | JS-013 McCabe Recount | Compliant (<=8)? |
|--------|------------|----------------------|------------------|
| `ResolveTargetCount` post-fix | "-1" impact | CYC unchanged: base=1, no branches. Math.Min is a method call, not a McCabe branch. Impact is 0, not -1. Post-fix CYC=1. | YES |
| `MoveStopToBreakEven` post-fix | "-1" impact | Removing `while` loop removes +1 branch. Impact = -1. **Correct.** | YES |
| `TryParseStopSuffix` post-fix | "-1" impact | Removing `|| n > 3` removes the `||` decision point. Impact = -1. **Correct.** | YES |
| `CaptureOtherLegTargetPrices` new | CCN=3 | base=1, foreach=+1, `if (idx >= 1)`=+1. Total=3. **Correct.** | YES |
| `ParseTargetLegIndex` new | CCN=4 | base=1, `if (orderName == null)`=+1, `if (!StartsWith)`=+1, `if (suffix==excludeSuffix)`=+1, ternary `?:`=+1. Total=**5**, not 4. | YES (5<=8) |
| `IsPttQxTarget` post-fix | "+1 branch, CCN~=4" | base=1, `||` in first if=+1, `if`=+1, second `if`=+1, `&&` in return=+1. Total=**5**, not 4. Before: same structure with different checks — also ~5. Net change ~0, not +1. | YES (5<=8) |

**Non-blocking observation (OBS-CYC-01):** `ResolveTargetCount` CYC impact is stated as "-1" but `Math.Min` is not a McCabe branch (JS-013). Actual CYC impact is 0. Post-fix CYC=1. No JS-013 violation.

**Non-blocking observation (OBS-CYC-02):** `ParseTargetLegIndex` CCN is stated as 4. By JS-013 strict McCabe (base + 4 decision points including the ternary), actual CCN=5. Still within JS-013 limit (≤8). No JS-013 violation.

**Non-blocking observation (OBS-CYC-03):** `IsPttQxTarget` pre/post CCN stated as 3→4. JS-013 strict McCabe gives ~5→5 (counting all `||`/`&&`/`if`). Net impact is ~0, not +1. Post-fix CCN ~5 is within limit. No JS-013 violation.

All methods remain well within JS-013 limit (CYC ≤ 8). None of these inaccuracies constitute a violation gate.

---

## G. Scan Completeness

| Scan | Plan Command | Context Command | Match? |
|------|-------------|-----------------|--------|
| SCAN-1 | `grep -n "n > 3" src/PropTraderTools/CopyEngine.cs` | `grep -n "n > 3" CopyEngine.cs` | YES (path scoped) |
| SCAN-2 | `grep -n "i <= 3" src/PropTraderTools/CopyEngine.cs` | `grep -n "i <= 3" CopyEngine.cs` | YES |
| SCAN-3 | `grep -n "new double\[3\]" src/PropTraderTools/CopyEngine.cs` | `grep -n "new double\[3\]" CopyEngine.cs` | YES |
| SCAN-4 | `grep -n "Math.Min.*3" src/PropTraderTools/Features/PttQuickExit.cs` | `grep -n "Math.Min.*3" PttQuickExit.cs` | YES |
| SCAN-5 | `grep -n "targets.Count > 3" src/PropTraderTools/CopyEngine.cs` | `grep -n "targets.Count > 3" CopyEngine.cs` | YES |
| SCAN-6 | `grep -n "name.Length != 9" src/PropTraderTools/Features/PttBreakEven.cs` | `grep -n "name.Length != 9" PttBreakEven.cs` | YES |
| SCAN-7 | `lizard ... --CCN 8` (all 3 files) | lizard all 3 files `--CCN 8` | YES |
| SCAN-8 | `grep -rn "lock\s*(" src/PropTraderTools/` | `grep -rn "lock\s*(" src/PropTraderTools/` | YES |
| SCAN-9 | ASCII-only byte check on all 3 edited files | ASCII-only check on all edited files | YES |
| SCAN-10 | `dotnet build` → 0 errors, 0 warnings | `dotnet build` → 0 errors 0 warnings | YES |

**All 10 scans present and correctly specified. PASS.**

---

## H. JS Rule Violations

| Rule | Violation Found? | Notes |
|------|-----------------|-------|
| JS-001 (no throw in hot paths) | NONE | No throw introduced |
| JS-002 (no unexpected null return) | NONE | New helpers return -1 as default, not null |
| JS-003 (no magic string for state) | NONE | No new string discriminators |
| JS-008 (no mutable struct / unfrozen brush) | NONE | No structs or brushes introduced |
| JS-009 (no shared Dictionary) | NONE | `Dictionary<int,double>` is local/caller-scoped per invocation; not a class field; not shared between threads |
| JS-013 (CYC ≤ 8) | NONE | All methods within limit; see Section F |
| JS-021 (no lock()) | NONE | Confirmed lock-free; SCAN-8 is the gate |
| JS-023 (no off-thread UI update) | NONE | No UI updates introduced |
| JS-025 (ConcurrentDictionary for shared state) | NONE | New Dictionary is invocation-local |
| JS-100 (no magic numbers) | NONE | The literal `3` is being *removed* by these fixes |

---

## I. NT8 API Compliance

| Rule | Status |
|------|--------|
| NT8-001 (no async/await in lifecycle/handlers) | No async introduced. PASS. |
| NT8-002 (no Account.All outside Loaded context) | Not relevant to these changes. PASS. |
| NT8-003 (PTT- prefix on CreateOrder) | Plan Section 8 confirms PTT-TGT-Drag-N naming. PASS. |
| NT8-007 (StartAtmStrategy static form) | Not used in this block. PASS. |
| NT8-008 (AtmStrategyCreate BANNED) | Explicitly confirmed NOT USED (Sections 3, 8). PASS. |
| NT8-009 (AtmStrategyChangeStopTarget BANNED) | Explicitly confirmed NOT USED (Sections 3, 8). PASS. |
| NT8-010 (Account.Change no-op) | Explicitly confirmed NOT USED (Sections 3, 8). PASS. |

---

## J. Non-Blocking Observations (Engineer Awareness)

| ID | Description | Severity |
|----|-------------|----------|
| OBS-CYC-01 | `ResolveTargetCount` CYC delta stated as -1 but `Math.Min` is not a McCabe branch; actual delta is 0. No gate impact (post-fix CYC=1). | Observation |
| OBS-CYC-02 | `ParseTargetLegIndex` CCN stated as 4; JS-013 strict count (base + 4 decisions including ternary) = 5. No gate impact (5 ≤ 8). Engineer should use `// CYC=5` comment per JS-013. | Observation |
| OBS-CYC-03 | `IsPttQxTarget` pre/post CCN stated as 3→4; JS-013 strict count is ~5→5 (both `||`/`&&` counted individually). No gate impact. Engineer should verify and use correct `// CYC=N` comment. | Observation |

None of the above observations are blocking. No JS-013 limit is breached.

---

## K. Final Verdict

All required checks pass:
- Lane-split gate: LANES-APPROVED, correctly answered
- Spec completeness: all 4 requirements addressed
- Architecture-locked items: all respected
- Fix correctness: all 4 fixes correctly remove ceilings without breaking paths
- Type change completeness: `HandleAtmStopSync` included, atomic commit mandated
- CYC compliance: all methods ≤ 8 (JS-013)
- Scan completeness: all 10 scans present and correctly specified
- JS rules: no violations
- NT8 API: no StrategyBase-only APIs used; all AddOnBase-safe

No REVIEW_FAIL triggers found.

---

STATUS: REVIEW_PASS
