# B122 Ticket Review

**Block**: B122
**Phase**: 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-08-25
**Input**: docs/brain/B122/04-tickets.md
**Plan Input**: docs/brain/B122/02-architecture-plan.md (REVIEW_PASS)
**Plan Review Input**: docs/brain/B122/02-plan-review.md (REVIEW_PASS)
**Rules Catalog**: docs/standards/jane-street/RULES_CATALOG.md
**csproj verified**: src/PropTraderTools/PropTraderTools.csproj (read directly)

---

## Ticket Review: B122

### T1 -- B122-T1: Fix pre-existing build errors blocking test suite

---

#### TR-1: TRACEABILITY

**Spec requirement cited in ticket**: `DW-PTT-BE-FIX-03` (docs/brain/B107/06-deferred-backlog.md)

**Verification**: `docs/brain/B107/06-deferred-backlog.md` confirmed. Item `DW-PTT-BE-FIX-03`
appears at line 139 of that file:
> "Pre-existing errors in CopyEngineTests.cs stub infrastructure (83 errors) plus CS0433 Globals
> ambiguity at CopyEngine.cs:L3350. Confirmed pre-existing, unrelated to B107."

Every edit prescribed in the ticket (remove SKGL.Extension PackageReference, conditional CS0433
NoWarn append) maps directly to the two defects described in DW-PTT-BE-FIX-03. No phantom work
(items in ticket but not in plan/spec). No missing work (all plan Section F items have corresponding
ticket sections).

**TR-1: PASS**

---

#### TR-2: SCOPE

**Architecture plan scope** (Section F): single ticket, `PropTraderTools.csproj` only.
- Mandatory: remove `<PackageReference Include="SKGL.Extension" Version="2.0.23" />` at line 85
- Conditional: append `;CS0433` to `<NoWarn>` at line 26 if CS0433 fires post-Edit-1

**Ticket scope**: Exactly matches. One mandatory edit (Edit 1) and one conditional edit (Edit 2)
targeting the same file. No additional files added beyond the plan. No scope expansion.

**TR-2: PASS**

---

#### TR-3: FILE SCOPE

**Files permitted by ticket**: `src/PropTraderTools/PropTraderTools.csproj` only.

**Hard bans listed explicitly** (ticket section "Hard bans"):
- `CopyEngine.cs`, `LicenseClient.cs`, `TradeCopierAddOn.cs`, `TradeCopierWindow.cs`,
  `TradeCopierPanel.cs`, `CopyEngineTests.cs`, any file under `Tests/`
- No test method additions, deletions, or restructuring

No `.cs` file is in scope. SCAN-03 (`git diff --name-only`) is the runtime enforcement gate.

**TR-3: PASS**

---

#### TR-4: EDIT PRECISION

**Ticket Edit 1 claim**: Delete line 85, exact text
`    <PackageReference Include="SKGL.Extension" Version="2.0.23" />`

**Verified against actual csproj**:
- Line 85 (read directly): `    <PackageReference Include="SKGL.Extension" Version="2.0.23" />`
  — EXACT MATCH.
- Surrounding context (lines 83-90) provided in ticket matches actual csproj content.
- Post-edit expected ItemGroup state is shown and is structurally correct (xunit and
  xunit.runner.visualstudio entries remain, SKGL PackageReference removed).

**TR-4: PASS**

---

#### TR-5: CONDITIONAL GUARD

**Ticket Edit 2 trigger**: "Apply ONLY if `CS0433` appears in post-Edit-1 build output."

**Guard language**: Explicit. "If CS0433 does NOT appear: Skip Edit 2 entirely. Do not add the
suppression speculatively."

**Before text** (line 26):
`    <NoWarn>MSB3245;MSB3246;CS0012;CS8632;CS0234;CS0246;CS0436</NoWarn>`

**Verified against actual csproj line 26**: EXACT MATCH.

**After text** (conditional): appends `;CS0433` — correctly preserves all existing suppressions.

The conditional gate is unambiguous. The engineer cannot apply Edit 2 without first observing
`CS0433` in post-Edit-1 build output.

**TR-5: PASS**

---

#### TR-6: BUILD VERIFICATION

Two mandatory build checkpoints defined:

1. After Edit 1 only: `dotnet build src/PropTraderTools/PropTraderTools.csproj 2>&1 | Select-Object -Last 30`
   Expected: "NuGet restore succeeds. Compilation begins."

2. Final gate (after all edits): same command.
   Required output: `Build succeeded.` + `    0 Error(s)`

Failure protocol: if `0 Error(s)` is not achieved, engineer reports verbatim errors in
`ticket-1-completion.md` and escalates. No silent workarounds beyond Edits 1 and 2. This is
correctly scoped — the ticket does not authorize the engineer to improvise further fixes.

**TR-6: PASS**

---

#### TR-7: 7-SCAN CHECKLIST

All 7 scans present with explicit commands and gates:

| Scan | Command | Gate | Present |
|------|---------|------|---------|
| SCAN-01 | `Select-String -Path "src/PropTraderTools/PropTraderTools.csproj" -Pattern "lock\("` | 0 results | YES |
| SCAN-02 | `Select-String -Path "src/PropTraderTools/PropTraderTools.csproj" -Pattern "async void"` | 0 results | YES |
| SCAN-03 | `git diff --name-only` | only PropTraderTools.csproj in output | YES |
| SCAN-04 | `dotnet build ... \| Select-String -Pattern "Error\(s\)"` | `0 Error(s)` | YES |
| SCAN-05 | `Select-String` for unqualified `Globals.` references in `*.cs` | 0 results | YES |
| SCAN-06 | `Select-String -Path "*.csproj" -Pattern "xunit"` | >= 2 lines | YES |
| SCAN-07 | `Select-String -Path "*.csproj" -Pattern "SKGL\.Extension"` | exactly 1 line | YES |

**Observation**: SCAN-01 and SCAN-02 in the ticket run against `PropTraderTools.csproj` only,
whereas the architecture plan Section F specified running them against all `*.cs` files. This is
a scope narrowing — the ticket redirects these scans to the single modified file (XML, where
C# `lock()` and `async void` cannot exist). The `.cs` file integrity is enforced by SCAN-03
(`git diff`). This approach is acceptable: SCAN-03 is the stronger guard for `.cs` safety; SCAN-01/02
confirm the csproj edit did not accidentally introduce unexpected content. No violation.

All 7 scans are present with exact commands and clear pass/fail gates.

**TR-7: PASS**

---

#### TR-8: COMPLETION ARTIFACT

`ticket-1-completion.md` contract defined with 6 mandatory elements:

1. Exact edits made (file:line, old text, new text) — REQUIRED
2. Build output after Edit 1 (verbatim last 30 lines) — REQUIRED
3. Build output after all edits (must show `0 Error(s)`) — REQUIRED
4. 7-scan results (one section per scan, SCAN-01 through SCAN-07) — REQUIRED
5. Test run output (verbatim last 30 lines of `dotnet test --no-build`) — REQUIRED
6. Status line (`Status: BUILD_PASS` or `Status: BUILD_FAIL: <reason>`) — REQUIRED

Required passing test files enumerated: `B120Tests.cs`, `B119Tests.cs`, `B118Tests.cs`.
Test count invariant stated: total passing count >= pre-ticket count.

**TR-8: PASS**

---

#### TR-9: RULES CATALOG — P0/P1 VIOLATIONS

This ticket prescribes only XML edits to a `.csproj` file. No C# code is introduced.

| Rule | Verdict |
|------|---------|
| JS-021 (P0) — No `lock()` | PASS by construction — no `.cs` changes |
| JS-001 (P0) — No throw in hot path | PASS by construction — no `.cs` changes |
| JS-033 (P0) — No `async void` | PASS by construction — no `.cs` changes |
| JS-002 (P0) — No `return null` | PASS by construction — no `.cs` changes |
| JS-010 (P0) — Private constructors | N/A — no new types introduced |
| JS-036 (P0) — No heap alloc in hot path | N/A — no `.cs` changes |
| JS-037 (P0) — No bare `new byte[]` | N/A — no `.cs` changes |
| JS-051 (P1) — xUnit only | PASS — xUnit PackageReference retained; SCAN-06 enforces |
| JS-066 (P1) — Diff < 10k chars | PASS — single XML line deletion (~55 chars) |

Adding `CS0433` to `<NoWarn>` is a project-level LSP suppression consistent with existing
`CS0436` suppression. It does not introduce any P0 or P1 violation.

**P0 violations introduced: ZERO**

**TR-9: PASS**

---

#### TR-10: NT8 CONSTRAINTS — HintPath Reference Integrity

**Critical check**: The SKGL.Extension HintPath `<Reference>` at lines 56-59 must NOT be
the deleted line. Only the `<PackageReference>` at line 85 is deleted.

**Verification against actual csproj**:

Lines 56-59 (actual):
```xml
<Reference Include="SKGL.Extension">
  <HintPath>$(USERPROFILE)\Documents\NinjaTrader 8\bin\Custom\SKGL.Extension.dll</HintPath>
  <Private>false</Private>
</Reference>
```
This is in the NT8 DLL references `<ItemGroup>` (lines 39-60). It is the HintPath DLL reference
that NT8 requires. The ticket's Edit 1 does NOT touch this block.

Line 85 (actual):
```xml
    <PackageReference Include="SKGL.Extension" Version="2.0.23" />
```
This is in the xUnit `<ItemGroup>` (lines 83-90). It is the broken NuGet PackageReference.
This is the ONLY line deleted by Edit 1.

SCAN-07 gate enforces the invariant post-edit: exactly 1 line containing `SKGL.Extension`
must remain (the HintPath reference). 0 lines = HintPath deleted (FAIL). 2 lines = PackageReference
not removed (FAIL). The gate correctly catches both failure modes.

No NT8 HintPath references are at risk. No other NT8 DLL references (`NinjaTrader.Core`,
`NinjaTrader.Gui`, `NinjaTrader.Client`, `NinjaTrader.Custom`) are in scope.

**TR-10: PASS**

---

#### Method Signatures / [Fact] Test Coverage

N/A — This ticket introduces zero new methods. CYC delta = 0. No [Fact] test specifications
are required. The ticket explicitly states: "This ticket contains no new methods. All changes
are XML edits to PropTraderTools.csproj only."

**Test Coverage: PASS (N/A)**

---

#### File Routing

All `.csproj` references point to `src/PropTraderTools/PropTraderTools.csproj` within
`c:\WSGTA\universal-or-strategy` (Wave workspace). No Director workspace paths. No `.cs` files
routed to any prohibited location.

**File Routing: PASS**

---

### T1 VERDICT: TICKET_REVIEW_PASS

| Check | Result |
|-------|--------|
| TR-1: Traceability | PASS |
| TR-2: Scope | PASS |
| TR-3: File Scope | PASS |
| TR-4: Edit Precision | PASS |
| TR-5: Conditional Guard | PASS |
| TR-6: Build Verification | PASS |
| TR-7: 7-Scan Checklist | PASS |
| TR-8: Completion Artifact | PASS |
| TR-9: Rules Catalog (P0/P1) | PASS |
| TR-10: NT8 HintPath Integrity | PASS |

---

## Overall: TICKET_REVIEW_PASS

All 10 checklist items pass across the single ticket in this block. Zero P0 or P1 violations
introduced. The 7-scan checklist is complete with exact commands and unambiguous gates. The
completion artifact contract is fully defined. The engineer has a clear, unambiguous contract
to execute against.

**Result**: TICKET_REVIEW_PASS
