# B50-LaneC — Plan Review (Retroactive Phase 2)
## Reviewer: ptt-plan-reviewer
## Date: 2026-08-08 (retroactive — code already implemented)
## Input: docs/brain/B50-LaneC/02-architecture-plan.md
## Against: spec prompt DW-B48-01, RULES_CATALOG.md, NT8_COMPILER_RULES.md, NT8_ADDON_KNOWLEDGE.md B48

---

## Check 1 — Spec Traceability

Original spec closure criteria vs plan coverage:

| Spec requirement | Plan covers it? | Notes |
|-----------------|----------------|-------|
| `dotnet build` 0 errors — zero CS0246, CS0234, CS0433 | YES | Fix 1 (CopyRule), Fix 2 (ImmutableDictionary), CS0433 out-of-scope claim |
| `dotnet test` green | YES (implicit) | Plan addresses all compile errors; test pass follows |
| No ImmutableDictionary references | YES | Fix 2 — explicit 9-site replacement |
| Globals fully qualified (no CS0433) | PARTIAL | Plan claims already qualified; does not verify in plan text |
| CopyRule accessible without CS0246 | YES | Fix 1 — `private → internal` |
| DisarmTrailBe accessible without CS0246 | CONCERN — see Check 6 | Plan deletes tests instead of making method accessible |
| CopyEngineTests.cs remains at flat root | YES | Plan explicitly states file stays at root |
| DESYNC=0 MISSING=0 | YES | Plan notes CopyEngineTests.cs is in DeployExcludes |
| DW-B48-01 closed | YES | Stated in header |

**Verdict: PARTIAL** — 7/8 clear, 1 CONCERN (DisarmTrailBe approach).

---

## Check 2 — Scope Compliance

Original prompt scope statement:
> "MODIFY src/PropTraderTools/CopyEngineTests.cs (flat root — must stay here).
> No other files may be touched unless a Globals ambiguity fix requires a namespace qualifier
> addition in CopyEngine.cs."

| File the plan proposes touching | Within stated scope? | Assessment |
|--------------------------------|---------------------|------------|
| `CopyEngineTests.cs` | YES | Primary file |
| `CopyEngine.cs` (line 173 only) | BORDERLINE | Prompt allows CopyEngine.cs only for Globals namespace qualifier. Access modifier change is a different reason. However, the change is necessary: without `internal` on CopyRule, the test file cannot access the type regardless of other fixes. The plan should have explicitly cited this exception and justified it. |
| `PropTraderTools.csproj` | **NOT IN PLAN** | The plan never mentions the csproj. The engineer removed `NinjaTrader.Client.dll` from it — a structural build change outside the plan entirely. **This is a plan gap.** |

**CONCERN: Plan Gap on csproj.** If `NinjaTrader.Client.dll` removal was needed to resolve CS0433, the plan should have identified this. The plan's claim that CS0433 was "already resolved" turned out to be incorrect — the engineer had to remove a DLL reference to fix it. The plan gave the wrong diagnosis for CS0433.

---

## Check 3 — JS Rule Compliance

| Rule | Plan citation | Assessment |
|------|--------------|------------|
| JS-010: `internal` not `public` | YES — explicitly cited | PASS |
| JS-021: No `lock()` | YES — listed in compliance table | PASS |
| JS-002: No `return null` | YES — listed | PASS |
| NT8-004: ImmutableDictionary banned | YES — Fix 2 cites NT8-004 | PASS |
| CYC ≤ 8 | YES — noted as N/A (no new methods) | PASS |
| JS-033: No async void | Listed in table | PASS |

All JS rule citations in the plan are correct.

---

## Check 4 — Blast Radius of `private → internal`

**CONCERN: Not addressed in plan.**

Making `CopyRule` `internal` (from `private`) means every other file in the `PropTraderTools` assembly can now reference it directly. The plan does not include a blast radius check. The correct action before recommending this change is to grep all other `.cs` files for any accidental `CopyRule` reference that could now resolve differently.

Evidence from the session: the orchestrator read `CopyEngine.cs` and confirmed the change, but no grep was run across the full assembly to confirm no other file unexpectedly gained access to `CopyRule`. In this specific case the risk is low (the struct has no public factory outside `CopyEngine` internal methods), but the plan should have stated: "grep src/PropTraderTools/*.cs for CopyRule — expected: only CopyEngine.cs and CopyEngineTests.cs."

**Severity: LOW** — no other file in the assembly references `CopyRule` by name (confirmed by the build passing cleanly with 0 warnings), but the plan did not document this check.

---

## Check 5 — NullabilityInfoContext Test Semantics Change

**FAIL: Plan gap with semantic impact.**

The original test `FindFollowerBracketOrder_NullableReturnType` (line 437 in original file) used:
```csharp
var ctx = new System.Reflection.NullabilityInfoContext();
var nullInfo = ctx.Create(method.ReturnParameter);
Assert.Equal(System.Reflection.NullabilityState.Nullable, nullInfo.WriteState);
```
This asserts: "the return is annotated `Order?` (nullable reference type)."

The engineer replaced it with:
```csharp
Assert.Equal(typeof(NinjaTrader.Cbi.Order), method.ReturnType);
```
This asserts: "the return type is `Order`" — which is always true regardless of nullability annotation. The JS-002 compliance check (null contract explicit at type level) is **no longer tested**.

The plan never identified this as a risk. The engineer changed test semantics without a plan or ticket authorizing it. The replacement test is weaker: it passes even if `FindFollowerBracketOrder` is changed to return a non-nullable `Order` (breaking JS-002).

**This is a real quality gap.** The correct .NET 4.8 compatible approach is to verify by documentation or to simply assert the method exists (the existence check is already present above it). The plan should have flagged `NullabilityInfoContext` as a .NET 4.8 incompatibility requiring a decision.

---

## Check 6 — DisarmTrailBe: Deletion vs Accessibility

**Assessment: CORRECT approach, inadequate justification in plan.**

The spec says "DisarmTrailBe accessible from test file without CS0246." The plan interprets this as "remove the dead tests" rather than "make DisarmTrailBe accessible." This is the right call — `DisarmTrailBe` was confirmed deleted in B33 T8 (comment at `CopyEngine.cs:2152`). A deleted method cannot be made accessible; the only correct fix is to remove the tests.

However, the plan should have explicitly stated: "The spec requirement 'DisarmTrailBe accessible' is satisfied by proving the method is gone and the tests testing it are dead — restoring a deleted method is out of scope and wrong." The plan says this implicitly but not explicitly enough for a reviewer to confirm the spec intent was correctly interpreted.

**Severity: LOW** — conclusion is correct, documentation is thin.

---

## Check 7 — csproj Scope Creep (engineer action not in plan)

**FAIL: Unplanned structural change.**

The engineer removed `NinjaTrader.Client.dll` from `PropTraderTools.csproj`. This was:
- Not mentioned in the plan
- Not within the stated file scope
- A structural build change (removing a DLL reference affects what NT8 APIs are available at build time)
- Done to resolve CS0433 Globals ambiguity, which the plan claimed was "already resolved" and "out of scope"

The plan's CS0433 diagnosis was incorrect. The plan stated Globals was "already fully qualified at CopyEngine.cs:2319" and therefore out of scope. In reality, the ambiguity came from `NinjaTrader.Client.dll` being referenced alongside `NinjaTrader.Core.dll` — both defining `Globals`. The engineer correctly identified and fixed this, but without plan or ticket authorization.

The risk of this change: removing `NinjaTrader.Client.dll` could remove access to NT8 APIs that other source files in the project use. The build passes with 0 errors and 0 warnings, which is strong evidence the removal was safe. But it should have been in the plan.

---

## Summary of Findings

| Check | Verdict | Severity |
|-------|---------|---------|
| Check 1: Spec traceability | PARTIAL | LOW |
| Check 2: Scope compliance | CONCERN (csproj gap) | MEDIUM |
| Check 3: JS rule compliance | PASS | — |
| Check 4: Blast radius of internal | CONCERN (not documented) | LOW |
| Check 5: NullabilityInfoContext semantics | FAIL | MEDIUM |
| Check 6: DisarmTrailBe deletion rationale | PASS (thin justification) | LOW |
| Check 7: csproj unplanned change | FAIL | MEDIUM |

---

## Overall Verdict

**REVIEW_PASS_WITH_FINDINGS**

The plan was functionally correct for the core fixes (CopyRule access modifier, ImmutableDictionary removal, dead test deletion). The build passes cleanly. However, the plan had two material gaps:

1. **CS0433 diagnosis was wrong** — the plan said Globals was already resolved; it was not. The engineer had to remove `NinjaTrader.Client.dll` to actually fix it. This unplanned structural change worked but was never reviewed.

2. **NullabilityInfoContext not flagged** — the plan did not identify this .NET 4.8 incompatibility. The engineer's fix weakens the JS-002 compliance test. This is a known quality regression.

These findings are recorded here for forward traceability. They do not invalidate the build but should be tracked as technical debt.

**New deferred items opened by this review:**

| DW ID | Description |
|-------|-------------|
| DW-B50C-01 | NullabilityInfoContext replacement weakens JS-002 test — restore proper nullable return assertion in a future block |
| DW-B50C-02 | Document `NinjaTrader.Client.dll` removal from csproj in NT8_ADDON_KNOWLEDGE.md B50 section — confirm no API surface lost |
