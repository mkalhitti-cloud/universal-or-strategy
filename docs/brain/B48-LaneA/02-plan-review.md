# B48-LaneA — Plan Review
**Block**: PTT-COPIER-B48 Lane A  
**Reviewer**: ptt-plan-reviewer  
**Plan File**: `docs/brain/B48-LaneA/02-architecture-plan.md`  
**Date**: 2026-08-07  
**Phase**: 2 (Plan Review)

---

## SECTION A — PLAN REVIEW CHECKLIST

Gate each spec requirement (R1–R7) against the plan.

| Req | Description | Addressed in Plan? | Plan Section | Status |
|-----|-------------|-------------------|--------------|--------|
| R1 | Move B42–B47Tests.cs out of NT8 compile path | ✅ B42–B46 explicitly moved via `git mv`; B47Tests.cs does not exist yet — plan defers it as DW-B47-01 per Section 7 and Section 8 | §4.2, §7, §8 | PASS |
| R2 | F5 produces zero errors after change | ✅ NT8 flat-scan behavior confirmed; subfolder files invisible to NT8 Roslyn host; AC-01 acceptance criterion | §2, §5 | PASS |
| R3 | Option A: relocate to subfolder NT8 does not scan | ✅ `src/PropTraderTools/Tests/` subfolder chosen; NT8-054 flat-scan behavior documented as the mechanism | §3, §2 | PASS |
| R4 | Test files remain compilable by dotnet build | ✅ csproj `<Compile Include="Tests\B*.cs" />` entries updated; SCAN-07 requires exit code 0; AC-09 criterion | §4.3, §5, §6 | PASS |
| R5 | verify_links.ps1 must not hard-link test files to NT8 bin | ✅ Two-layer defence: directory-based `\\Tests\\` skip (Layer 1) + B46Tests.cs added to `$DeployExcludes` (Layer 2); both changes specified exactly | §2, §4.4, §6 | PASS |
| R6 | PropTraderTools.csproj references all moved files at new paths | ✅ All 5 entries updated to `Tests\B*Tests.cs`; B46Tests.cs ADD explicitly called out as bug fix | §4.3 | PASS |
| R7 | No new P0 JS rule violations | ✅ Plan introduces zero C# source code; Section 10 explicitly states no lock(), no async void, no return null; all changes are file moves + XML edits + PS edits | §10 | PASS |

---

## SECTION B — SPEC COMPLETENESS DEEP-DIVE

### B1. All 6 test files addressed (B42–B47)?

- **B42–B46**: Explicitly moved to `Tests\` via `git mv` (§4.2). ✅
- **B47Tests.cs**: Does not exist. Plan correctly defers creation to Lane C (DW-B47-01) and defers the `<Compile Include="Tests\B47Tests.cs" />` csproj entry to the block that delivers B47Tests.cs (§7, §8). ✅ Acceptable — you cannot move a file that does not exist.

### B2. NT8 compile path issue (NT8-054) identified?

Plan cites NT8-054 by rule ID in §2 and explicitly explains the flat-glob `AddOns\PropTraderTools\*.cs` vs recursive-but-subdirectory-safe reality. Confirmed as correct by comparing to Wave workspace:
- All 5 B*Tests.cs files are currently at `src/PropTraderTools/` root ✅ (verified live)
- `$DeployExcludes` in verify_links.ps1 currently lists B42–B45 but NOT B46 ✅ (verified live: line 9)
- B46Tests.cs is NOT in csproj ✅ (verified live: lines 97–103 show only B42–B45)

### B3. Plan updates BOTH verify_links.ps1 AND PropTraderTools.csproj?

- `verify_links.ps1`: Two explicit changes specified — array entry addition + if-block insertion with exact PowerShell text (§4.4). ✅
- `PropTraderTools.csproj`: Four path updates + one new ADD entry with exact XML (§4.3). ✅

### B4. B46Tests.cs missing from csproj identified as a bug?

Yes. Plan calls it out explicitly: "Also ADD the missing B46Tests.cs entry (was never in csproj — bug fix)" with distinct XML comment (§4.3). Three independent bugs are listed in §1. ✅

### B5. 7-scan checklist present?

Section 6 contains a full 7-scan table (SCAN-01 through SCAN-07) with command, purpose, and expected result for each. ✅ Format matches ptt-ticket-reviewer contract.

### B6. CopyEngineTests.cs scope correctly excluded?

Yes. Section 3 and Section 7 explicitly state `CopyEngineTests.cs` stays at root permanently with documented rationale (private nested type access; `CopyRule` struct only reachable within same assembly). Section 9 confirms NO CHANGE. SCAN-01 excludes it by name from the zero-result assertion. ✅

### B7. Deferred items from B47 preserved?

Section 8 lists DW-B44-01 (sub-item 2) and DW-B47-01 both as still-open deferred items. Convention for future blocks (B49+) is documented in §8 and §4.5 (NT8_ADDON_KNOWLEDGE.md append). ✅

### B8. Subfolder path rationale sound?

`Tests\` inside `PropTraderTools` (not a sibling project `PropTraderTools.Tests`) is the correct choice for this scope because:
- MSBuild `<Compile Include="Tests\*.cs" />` stays within the single project file — no need for a second `.csproj`, no NuGet reference duplication, no solution file changes.
- NT8 flat-glob does not recurse, so files in `Tests\` are never compiled by NT8. ✅
- CI `dotnet build src/PropTraderTools/PropTraderTools.csproj` covers them. ✅
- The plan documents this rationale in §3. ✅

A sibling `PropTraderTools.Tests` project would require: a new `.csproj`, a solution file change, and would violate the rule that CopyEngineTests.cs cannot be in a separate assembly. The `Tests\` subfolder approach avoids all of this. **The choice is architecturally sound.**

---

## SECTION C — RULE COMPLIANCE (JS P0 + NT8)

| Rule | Description | Scan Required? | Finding |
|------|-------------|---------------|---------|
| JS-021 | `lock()` anywhere in src/ | Plan introduces 0 C# files | ✅ CLEAN — no C# written |
| JS-033 | `async void` | Plan introduces 0 C# files | ✅ CLEAN — no C# written |
| JS-001 | `throw new XxxException` in hot paths | Plan introduces 0 C# files | ✅ CLEAN — no C# written |
| JS-002 | `return null` for missing values | Plan introduces 0 C# files | ✅ CLEAN — no C# written |
| NT8-054 | Test files with xUnit refs must not be deployed to NT8 bin | Core requirement of block | ✅ ADDRESSED — two-layer exclusion |

SCAN-06 in the 7-scan checklist specifically targets `lock\s*\(` in the modified PowerShell file as a zero-match gate. That is correct and sufficient for the PowerShell changes. No C# source is introduced.

---

## SECTION D — GAPS / VIOLATIONS

**No violations found.**

The following are observations, not violations:

1. **OBSERVATION (non-blocking)**: The plan's Section 4.3 comment edit for the B42 section is cosmetic ("comment only") and adds no functional value. It is harmless but the engineer may omit it without failing any acceptance criterion.

2. **OBSERVATION (non-blocking)**: SCAN-03 expects exactly `5 matches` for `Tests\\B4[2-9]Tests`. After the move this is correct (B42–B46 = 5 files). However if a future block adds B49Tests.cs before B48 is reviewed, the count changes. This is a stable assertion for the B48 delivery context and is not a plan defect.

3. **CONFIRMATION**: The live Wave workspace state was verified against the plan's problem statement:
   - B42–B46 at root ✅ (matches §1 table)
   - B46Tests.cs absent from csproj ✅ (matches §1 table bug row)
   - B46Tests.cs absent from `$DeployExcludes` ✅ (matches §1 table bug row)
   - B47Tests.cs does not exist ✅ (matches §7 out-of-scope row)
   - CopyEngineTests.cs at root, in csproj, in DeployExcludes ✅

---

## SECTION E — VERDICT

```
REVIEW_PASS
```

**All 7 spec requirements (R1–R7) are addressed.**  
**All 8 architectural questions are answered correctly.**  
**Zero P0 JS rule violations.**  
**Zero NT8 rule violations.**  
**7-scan checklist present and correct.**  
**Live workspace state matches the plan's problem statement exactly.**

The plan is approved for Phase 3 (ticket generation). The engineer receives a single-ticket scope:
- `git mv` 5 files → `Tests\`
- csproj: 4 path updates + 1 add
- verify_links.ps1: 1 array entry + 1 if-block
- NT8_ADDON_KNOWLEDGE.md: B48 section append
