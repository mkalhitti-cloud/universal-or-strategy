# Plan Review — PTT-REPAIRS-11-BINDING-FLAGS-03

**Phase:** 2 — Plan Review  
**Reviewer:** PTT Plan Reviewer  
**Plan artifact:** `docs/brain/PTT-REPAIRS-11-BINDING-FLAGS-03/02-architecture-plan.md`  
**RULES_CATALOG:** Hardcoded DNA block (file not present on disk; role definition is authoritative)  

---

## Verdict

**REVIEW_PASS**

Zero violations found. All 11 mandate checks pass. Plan is cleared for Phase 3 (ticket generation).

---

## Check Results

### Check 1 — LANE-SPLIT GATE Compliance

**PASS.**

Plan Section 1 contains the required gate result statement:

> `GATE RESULT: SINGLE PIPELINE — Fix A (new helper) and Fix B (test attribute + call site) are one indivisible atomic unit.`

Format is compliant (`SINGLE PIPELINE` / `LANES-APPROVED` keyword present). Q1–Q4 are explicitly evaluated.

Q1–Q4 defensibility review:

| Question | Answer in Plan | Defensible? |
|----------|---------------|-------------|
| Q1. Same method or within 50 lines? | Both in `BwaveCycTaR2HelperTests`, L6694–L6810 (~116 lines apart) | YES — same class, one logical unit |
| Q2. Fix B depends on Fix A design? | YES — call site requires new overload | YES — compile-time dependency confirmed |
| Q3. Each fix has standalone value if other is blocked? | NO | YES — dead code alone / compile error alone; SINGLE correct |
| Q4. Each fix has independent SIM verification path? | NO — single `dotnet test` run | YES — single verification path is correct |

SINGLE-PIPELINE determination is correct and defensible.

---

### Check 2 — Surgical Scope (ONLY CopyEngineTests.cs modified)

**PASS.**

Plan Section 3 explicitly states:

> `Total files changed: 1. No production .cs files touched. No deploy-sync.ps1 required.`

Three changes (A, B, C) are all scoped to `src/PropTraderTools/CopyEngineTests.cs`. No production `.cs` modification is planned or implied anywhere in the document.

Source-verified: `CopyEngine.cs` L6970 confirms `internal static string GetSenderAccountName(object sender)` — no changes to this file are in scope.

---

### Check 3 — Correctness of the Fix

**PASS.**

Root cause analysis (Section 2) is accurate:

- `GetSenderAccountName` in `CopyEngine.cs` is declared `internal static` — confirmed at L6970.
- Existing `GetMethod` uses `BindingFlags.NonPublic | BindingFlags.Instance` — confirmed at test file L6694–6695.
- `BindingFlags.Instance` excludes static members; `Type.GetMethod` returns `null` for a static member when only `Instance` is specified. This is correct .NET reflection behaviour.
- Proposed `GetStaticMethod` uses `BindingFlags.NonPublic | BindingFlags.Static` — correct flag combination for `internal static`.
- `[ObfuscationAttribute(Feature = "rename", Exclude = true)]` is present on the production method (confirmed at CopyEngine.cs L6969), protecting the name string `"GetSenderAccountName"` from rename obfuscation.
- `InternalsVisibleTo("PropTraderTools.Tests")` is declared at `CopyEngine.cs` L46 — confirmed. Test assembly has access to `internal` members.
- `Assert.NotNull(m)` only requires `MethodInfo` resolution; no NT8 runtime host required. Test PASS probability is near-certain.

---

### Check 4 — Existing GetMethod Helper NOT Modified

**PASS.**

Plan Section 4.1 explicitly states:

> `Do NOT modify the existing helper.`

The existing helper is reproduced in the plan as a comment-annotated read-only reference:

```csharp
// L6694 — existing, untouched
private static MethodInfo GetMethod(string name) =>
    typeof(CopyEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
```

Source-verified: L6694–6695 matches exactly. Plan does not touch this line.

---

### Check 5 — BwaveCycT1R1BeHelperTests (~L6570/L6580) NOT Touched

**PASS.**

Plan Section 9 "DO NOT TOUCH list" explicitly calls out:

| Location | Reason |
|----------|--------|
| `BwaveCycT1R1BeHelperTests` L6570 — `GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNull` | NT8-runtime-skipped; instance invocation path; out of scope |
| `BwaveCycT1R1BeHelperTests` L6580 — `GetSenderAccountName_ShouldReturnEmpty_WhenSenderIsNotAccount` | NT8-runtime-skipped; instance invocation path; out of scope |

Source-verified: L6570 and L6580 are `[Fact(Skip = "NT8-runtime: CopyEngine.cctor requires NT8 host")]` — correct Skip reason, correct class, out of scope. No plan action touches them.

---

### Check 6 — No Other Skip Removals

**PASS.**

The plan specifies exactly ONE Skip removal: the `[Fact(Skip = "obfuscation: ...")]` at L6803. The DO NOT TOUCH list (Section 9) explicitly prohibits removing Skip from any other test. The component list (Section 3) enumerates only three changes (A, B, C), none of which involve any other test's Skip attribute.

---

### Check 7 — Jane Street DNA Rule Compliance

**PASS on all applicable rules.**

| Rule ID | Rule | Plan Check | Result |
|---------|------|-----------|--------|
| JS-021 | No `lock()` / Monitor/Mutex/Semaphore for state | No locking of any kind in new code | PASS |
| JS-021 | No concurrency primitives | Pure test helper; no threading | PASS |
| JS-023 | UI update from off-thread via Dispatcher.InvokeAsync | No UI code; xUnit test class only | PASS (N/A) |
| JS-001 | No `throw` in dispatch chain | No throw statements in new code | PASS |
| JS-002 | No null return where value expected | `GetStaticMethod` may return null but caller asserts `NotNull`; null is a valid MethodInfo result for a missing method — test is the guard | PASS |
| JS-003 | No magic string for discriminated state | `"GetSenderAccountName"` is a reflection lookup string, not a discriminated state key | PASS (N/A) |
| JS-009 | No `Dictionary<K,V>` for shared/thread-touched collection | No collections introduced | PASS (N/A) |
| JS-008 | No mutable fields on struct; SolidColorBrush Freeze()d | No structs or brushes touched | PASS (N/A) |
| JS-010 | No public constructor on singleton or signal struct | No new types introduced | PASS (N/A) |
| ASCII-only | No Unicode/emoji in identifiers or literals | `GetStaticMethod` — all ASCII; string literal `"GetSenderAccountName"` — all ASCII | PASS |
| SCAN-06 | No `DateTime.Now` | No datetime usage | PASS (N/A) |
| SCAN-04 | No hardcoded `#RRGGBB` hex | No UI code | PASS (N/A) |
| SCAN-03 | No `FontFamily` override | No UI code | PASS (N/A) |
| SCAN-05 | No `CreateOrder` without PTT- prefix | No order creation | PASS (N/A) |
| CYC > 8 | No method CYC > 8 | `GetStaticMethod` CYC=1 (expression body, no branches). Test method CYC=1. | PASS |
| No async/await in OnInitialize/OnDestroyed/OnWindowCreated | No NT8 lifecycle methods | PASS (N/A) |
| No `Account.All` in constructor | No constructor code | PASS (N/A) |

---

### Check 8 — 7-Scan Checklist Planned for Ticket

**PASS.**

The plan is a Phase 1 architecture document; the 7-scan checklist mandate belongs to the ticket (Phase 3.5 territory). However, the plan's JS Rules Compliance table (Section 8) enumerates the equivalent scan coverage for the new code:

- No `lock(` — covered
- No `DateTime.Now` — covered
- No hardcoded hex — covered
- No `FontFamily` — covered
- No `CreateOrder` without prefix — covered
- No `async/await` in NT8 lifecycle hooks — covered
- ASCII-only identifiers — covered

The ticket template will carry the formal 7-scan checklist. Nothing in the plan blocks that.

---

### Check 9 — `dotnet build` 0 Error(s) Verifiable

**PASS.**

Plan Section 9 specifies the exact build gate:

```
dotnet build src/PropTraderTools/PropTraderTools.csproj
```
Expected: `0 Error(s)`

The new method signature is valid C# — expression-body static helper returning `MethodInfo`, using `BindingFlags.NonPublic | BindingFlags.Static`. No new `using` directives are required (same `BindingFlags` namespace already in scope at L6694). No compilation error is possible from this change.

---

### Check 10 — Test Must PASS; Fallback if Fails

**PASS.**

Plan Section 9 specifies the test gate with exact filter command:

```
dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "FullyQualifiedName~BwaveCycTaR2HelperTests"
```

Expected: `GetSenderAccountName_ShouldBeReusedByOnTrailBeAccountUpdate — PASSED`

Plan Section 10 provides the contingency:

> `If failure occurs anyway, document as: DW-REPAIRS-03-FALLBACK: GetSenderAccountName reflection fails with NonPublic|Static despite ObfuscationAttribute(Exclude=true). Investigate assembly load order.`

The fallback instruction correctly directs the engineer NOT to remove the Skip but to document a new deferred item — consistent with the mission brief constraint quoted in Section 10.

---

### Check 11 — No Other Violations (Spec Completeness)

**PASS.**

Spec requirements cross-checked against the plan:

| Spec Requirement | Addressed? | Plan Section |
|-----------------|-----------|--------------|
| Add `GetStaticMethod` overload immediately after existing `GetMethod` helper | YES | Section 3 (A), Section 4.1 |
| Switch affected test from `GetMethod` to `GetStaticMethod` | YES | Section 3 (C), Section 4.2 |
| Remove `[Fact(Skip="obfuscation:...")]` from that one test | YES | Section 3 (B), Section 4.2 |
| No other changes | YES | Section 3, Section 9 DO NOT TOUCH list |
| Single file modified: `CopyEngineTests.cs` | YES | Section 3 |
| No production `.cs` changes | YES | Section 3 |
| Build gate: 0 Error(s) | YES | Section 9 |
| Test gate: PASSED | YES | Section 9, Section 10 |

All spec requirements are addressed. No requirement is missing from the plan.

---

## Violations Log

| # | Rule ID | Description | Location in Plan | Severity |
|---|---------|-------------|-----------------|----------|
| — | — | No violations found | — | — |

---

## Summary

The plan is minimal, precise, and correct. Root cause diagnosis is accurate (BindingFlags mismatch, not obfuscation). The fix is a single-file, three-line surgical change with CYC=1. All Jane Street DNA rules are satisfied. Spec coverage is complete. Fallback path is documented. The plan is cleared for ticket generation.
