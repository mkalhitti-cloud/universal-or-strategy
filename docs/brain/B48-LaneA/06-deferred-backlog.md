# Deferred Backlog — PTT-COPIER

---

## B48-LaneA — Block Summary

**Block**: PTT-COPIER-B48 Lane A
**Topic**: DW-B44-01 Test File Isolation — Move B*Tests.cs out of NT8 compile path
**Date**: 2026-08-07
**Status**: FINAL_PASS

### Items Opened This Block

| ID | Description | Priority | Target |
|----|-------------|----------|--------|
| DW-B48-01 | CopyEngineTests.cs 60-error fix — full `dotnet test` runner requires CS0246 `CopyRule`, CS0234 `System.Collections.Immutable`, CS0433 `Globals`, CS0246 `DisarmTrailBe` cleanup. NT8 F5 isolation path is now closed; this item covers the `dotnet test` runner path only. Originally DW-B44-01 sub-item 2. | P1 | Dedicated cleanup block |
| DW-B48-02 | Inter-lane coordination lesson — Lane C must place new `BXXTests.cs` files directly in `Tests\` subfolder per `NT8_ADDON_KNOWLEDGE.md ## B48`. B48 verifier caught `B47Tests.cs` at flat root (hard-linked to NT8). Remediation was clean. Protocol documented. No code change required. | P2 | Process — N/A |

### Items Closed This Block

| ID | Description | Closure Evidence |
|----|-------------|-----------------|
| DW-B47-01 | `B47Tests.cs` creation for block-specific test coverage (xUnit tests T_B47_01 through T_B47_04 for `IsFollowerAccount` + guards). Lane C owns test file. | B47Tests.cs confirmed at `src/PropTraderTools/Tests/B47Tests.cs`; `<Compile Include="Tests\B47Tests.cs" />` at csproj line 107; `"B47Tests.cs"` in `$DeployExcludes`; verifier SCAN-04a confirmed 6 files in Tests\ |

### Items Partially Closed This Block

| ID | Description | What Closed | What Remains Open |
|----|-------------|------------|-------------------|
| DW-B44-01 | CopyEngineTests.cs 60 pre-existing compile errors | NT8 F5 path: all test files (`B42–B47Tests.cs`) now excluded from NT8 deployment via `$DeployExcludes` + Layer 1 directory skip. F5 will not encounter any xUnit files. | `dotnet test` runner: `CopyEngineTests.cs` 60 errors still prevent test binary from being produced. Tracked as DW-B48-01. |
| DW-B46-02 | `dotnet test` runner blocked by DW-B44-01 | Test files `B42–B47Tests.cs` are now isolated in `Tests\` and correctly referenced in csproj. The isolation gap is closed. | `CopyEngineTests.cs` compile errors still block the test runner. Tracked as DW-B48-01. |

---

## Carried Items from B47-LaneA (status updated for B48)

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: P2 (Low)
**Introduced**: B42
**Context**: `T_BUG_QX_BE_01` asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (`name[8] <= '3'`). Standard MES/ES setups use 2
targets.
**Status After B48**: STILL OPEN — not in B48 scope.
**Target**: B49+ or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to `T_BUG_QX_BE_01`.

---

### DW-B42-02 — Live NT8 F5 verification of Quick All → BE All sequences

**Priority**: P1 (High)
**Introduced**: B42
**Context**: Quick All / BE All interaction sequences can only be verified in a live NT8 session.
Must be confirmed in SIM account before go-live.
**Status After B48**: STILL OPEN — not in B48 scope. Can be combined with DW-B47-02 and DW-B46-01 in next live session.
**Target**: Next live session.

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots

**Priority**: P2 (Conditional — low unless T4/T5 slots added)
**Introduced**: B42
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design.
If PTT-QX-T4 or T5 slots added, `IsPttQxTarget` must be updated.
**Status After B48**: STILL OPEN — not in B48 scope.
**Target**: Block that adds 4th+ target slot.

---

### DW-B42-04 — Comment label `NT8-NEW` in PttContracts.cs:254 should be `NT8-005`

**Priority**: P2 (Low — documentation only)
**Introduced**: B42
**Context**: In-source comment at `PttContracts.cs` line 254 uses `NT8-NEW` instead of catalog
ID `NT8-005`.
**Status After B48**: STILL OPEN — not in B48 scope.
**Target**: B49+ cleanup pass.
**Fix**: Change `// NT8-NEW` at line 254 to `// NT8-005`.

---

### DW-B42-05 — Live F5 verification of PTTFollowerStrategy ATM bracket spawn

**Priority**: P1 (High)
**Introduced**: B42
**Context**: Superseded by DW-B46-01 for tracking. Root-cause barriers removed by B46. Full
closure requires live F5 session (DW-B46-01).
**Status After B48**: STILL OPEN — superseded by DW-B46-01.
**Target**: Next live session (DW-B46-01 combined).

---

### DW-B43-02 — GetLeaderAtmTemplateName visual-tree index accuracy (component a)

**Priority**: P1 (High)
**Introduced**: B43
**Context**: `FindVisualChildByIndex<ComboBox>(ct, 2)` may return the wrong ComboBox for some
chart configurations, causing `defaultIdx` to remain 0. B46 T2 closed component (b): write-back
of `AtmModeName` on load. Component (a) — index correctness investigation — is still open.
**Status After B48**: STILL OPEN (component a) — not in B48 scope.
**Target**: B49+ or next targeted investigation block.
**Action**: Check whether index 2 in ChartTrader visual tree actually maps to ATM Strategy
ComboBox. Options: (a) fix index, (b) use name-based lookup, (c) accept manual override.

---

### DW-B43-03 — NT8-045 update if AtmStrategyTemplates API becomes accessible

**Priority**: P2 (Low — future-proofing)
**Introduced**: B43
**Context**: Filesystem fallback (NT8-045) is robust. If a future NT8 update exposes
`AtmStrategyTemplates` API, replace the filesystem approach with direct API call.
**Status After B48**: STILL OPEN — not in B48 scope.
**Target**: Future NT8 upgrade block.

---

### DW-B44-01 — CopyEngineTests.cs: NT8 F5 path closed; dotnet test runner open (DW-B48-01)

**Priority**: P1 (High — blocks CI test execution for all B42–B47 test files)
**Introduced**: B44
**Context**: `CopyEngineTests.cs` has 60 accumulated errors from B32–B43 (CS0246 `CopyRule`,
CS0234 `System.Collections.Immutable`, CS0433 `Globals`, CS0246 `DisarmTrailBe`). These prevent
`dotnet test` from executing any test in the assembly.
**Status After B48**: PARTIALLY CLOSED.
- **Sub-item 1 (NT8 F5 path)**: CLOSED — B42–B47Tests.cs all excluded from NT8 deployment
  via `$DeployExcludes` (Layer 2) and `Tests\` subfolder Layer 1 skip. F5 will not compile
  any xUnit file.
- **Sub-item 2 (dotnet test runner)**: OPEN — 60 errors in `CopyEngineTests.cs` prevent
  `dotnet test` from running. Tracked as DW-B48-01.
**Target**: Dedicated `CopyEngineTests.cs` cleanup block.
**Action**:
1. Audit `CopyEngineTests.cs` for all 60 error sources.
2. Remove/stub NinjaTrader-linked types unavailable in dotnet CLI.
3. Replace `using System.Collections.Immutable` with NT8-004-compliant alternatives.
4. After cleanup, confirm `dotnet test` runs all BXX test filters green.

---

### DW-B44-02 — Live F5 verification of Subscribe() panel-only path

**Priority**: P1 (High)
**Introduced**: B44
**Context**: Subscribe/Unsubscribe fix confirmed structurally (B44) but not verified in a live
NT8 session where TradeCopierPanel is attached to a chart without TradeCopierWindow open.
**Status After B48**: STILL OPEN — not in B48 scope.
**Target**: Before next live trading session.
**Action**:
1. Open NT8. Attach TradeCopierPanel to chart via ChartTrader (panel only, no TradeCopierWindow).
2. Enable COPY ON in panel. Place SIM trade on leader account.
3. Confirm follower order appears; close chart — confirm no exception.

---

### DW-B44-03 — GetLeaderAtmTemplateName default selection (component a)

**Priority**: P1 (High)
**Introduced**: B44
**Context**: Same as DW-B43-02. Component (b) closed by B46 T2. Component (a) still open.
**Status After B48**: STILL OPEN (component a) — same as DW-B43-02.
**Target**: B49+.

---

### DW-B46-01 — Live F5 verification: DW-B42-05 re-run after B46

**Priority**: P1 (High — required before next live trading session)
**Introduced**: B46
**Context**: B46 removed the root-cause barriers (ATM guard + write-back). Full closure of
DW-B42-05 requires live F5 session to verify D1–D6 acceptance criteria. B47 adds additional
verification requirement: Sim102 brackets no longer wiped (DW-B47-02 combined).
**Status After B48**: STILL OPEN — not in B48 scope.
**Target**: Next live F5 session.
**Action**:
1. Configure `PTTFollowerStrategy` with Sim101 leader / Sim102 follower.
2. Select real ATM template in follower row. Click Apply.
3. Fire test trade from leader. Verify D1–D6 and confirm Quick ALL / BE ALL do not touch
   Sim102 brackets (DW-B47-02 combined).

---

### DW-B46-02 — dotnet test runner blocked by CopyEngineTests.cs errors

**Priority**: P1 (High)
**Introduced**: B46
**Context**: `B46Tests.cs` (3 tests) and `B47Tests.cs` (Lane C) introduce zero new compile
errors, but `CopyEngineTests.cs` prevents the test binary from being produced.
**Status After B48**: PARTIALLY CLOSED.
- **Test file isolation**: CLOSED — all BXXTests.cs files are now in `Tests\` with correct
  csproj entries. The isolation gap that caused NT8 F5 failures is resolved.
- **dotnet test runner**: OPEN — CopyEngineTests.cs 60 errors still block `dotnet test`.
  Tracked as DW-B48-01.
**Target**: B49+ or DW-B44-01 / DW-B48-01 closure.

---

### DW-B47-01 — B47Tests.cs creation for block-specific test coverage

**Priority**: P1 (High)
**Introduced**: B47
**Context**: `B47Tests.cs` was planned as xUnit tests T_B47_01 through T_B47_04 for the
`IsFollowerAccount` guard added by B47. Lane C owned the test file.
**Status After B48**: **CLOSED** — `B47Tests.cs` confirmed at
`src/PropTraderTools/Tests/B47Tests.cs` (6 files in `Tests\`); `<Compile Include="Tests\B47Tests.cs" />`
at csproj line 107; `"B47Tests.cs"` in `$DeployExcludes`; re-verification SCAN-04a confirmed.
The file was initially placed at the flat root (causing B48 initial VERIFY_FAIL), then moved to
`Tests\` in remediation per B48 convention.

---

### DW-B47-02 — Live F5 verification: BE ALL / Quick ALL no longer fires on Sim102 after B47

**Priority**: P1 (High)
**Introduced**: B47
**Context**: 17 `CancelStaleBrackets` calls eliminated by B47 `IsFollowerAccount` guard. Live
verification needed: Sim102 brackets should not be wiped by BE ALL or Quick ALL operations.
**Status After B48**: STILL OPEN — not in B48 scope. Can be combined with DW-B46-01.
**Target**: Next live session.

---

### DW-B47-03 — PttBuild.Tag update to reflect B47 topic

**Priority**: P1 (High)
**Introduced**: B47
**Context**: `PttBuild.Tag` in `CopyEngine.cs` line 41 currently reads:
`"PTT-COPIER B47 | panel-ux-redesign | 2026-08-07"`.
The B47 version number is present but the description references "panel-ux-redesign" — a
different lane's topic. The B47 Lane A topic was "be-follower-scope". Lane C owns the tag.
**Status After B48**: STILL OPEN — Lane C must correct the description suffix if needed.
Current tag shows B47 version which is correct. Description suffix is a Lane C concern.
**Target**: Lane C next block.

---

### DW-B47-04 — Add T_B47_05: IsFollowerAccount_ReturnsFalse_WhenNoRules edge case

**Priority**: P2 (Low)
**Introduced**: B47
**Context**: Plan §10 listed T_B47_05 (`IsFollowerAccount_ReturnsFalse_WhenNoRules` — empty
`_rules` edge case) but the ticket dropped it. Lane C should add to `B47Tests.cs`.
**Status After B48**: STILL OPEN — `B47Tests.cs` now exists; Lane C should add T_B47_05.
**Target**: Lane C with B47Tests.cs.

---

### DW-B47-05 — FindRule return null: pre-existing JS-002 debt

**Priority**: P2 (Low)
**Introduced**: B47
**Context**: `FindRule` in `CopyEngine.cs` lines 1381/1387 contains `return null` —
pre-existing JS-002 debt not introduced by B47 or B48. Both occurrences are in the
`return null` pre-existing count (DW-B47-05 exemption documented in verifier SCAN-03).
**Status After B48**: STILL OPEN — not in B48 scope.
**Target**: Future cleanup block.

---

### DW-B48-01 — CopyEngineTests.cs 60-error fix (dotnet test runner)

**Priority**: P1 (High)
**Introduced**: B48
**Context**: Full `dotnet test` runner requires `CopyEngineTests.cs` to be clean. Current
errors: CS0246 `CopyRule` (private nested type in `CopyEngine`), CS0234 `System.Collections.Immutable`,
CS0433 `Globals` (ambiguous), CS0246 `DisarmTrailBe`. These 60 errors prevent the test binary
from being produced, blocking all BXX tests even though they themselves compile correctly.
This is originally DW-B44-01 sub-item 2 — the NT8 F5 path was closed in B48.
**Status**: OPEN
**Target**: Dedicated `CopyEngineTests.cs` cleanup block.
**Action**:
1. Audit all 60 error sources in `CopyEngineTests.cs`.
2. Remove/stub `CopyRule` references (private nested type) or restructure tests to avoid
   private type access.
3. Fix `System.Collections.Immutable` references (NT8-004 compliant alternative).
4. Resolve `Globals` ambiguity (CS0433).
5. Fix `DisarmTrailBe` reference.
6. After cleanup, confirm `dotnet test` runs all BXX test filters green.

---

### DW-B48-02 — Inter-lane coordination: new BXXTests.cs must go in Tests\ subfolder

**Priority**: P2 (Process improvement — no code change required)
**Introduced**: B48
**Context**: Lane C placed `B47Tests.cs` at the flat root of `src/PropTraderTools/` after B48
T1–T4 were complete. `verify_links.ps1` hard-linked it to NT8, causing an NT8 F5 failure risk.
The verifier caught this and declared VERIFY_FAIL. Remediation was clean (file moved to `Tests\`,
csproj entry added, `$DeployExcludes` updated).
**Protocol**: Any lane creating a new `BXXTests.cs` file MUST:
1. Read `docs/standards/NT8_ADDON_KNOWLEDGE.md ## B48` for the placement convention.
2. Create the file at `src/PropTraderTools/Tests/B*Tests.cs` (NOT flat root).
3. Add `<Compile Include="Tests\B*Tests.cs" />` to `PropTraderTools.csproj`.
4. Do NOT update `$DeployExcludes` — Layer 1 directory skip covers `Tests\` automatically.
**Status**: OPEN (process improvement; no code change required)
**Target**: All future lanes delivering BXXTests.cs files.

---

## Deferred Item Status Table

| ID | Priority | Block Introduced | Status After B48 | Target |
|----|----------|-----------------|------------------|--------|
| DW-B42-01 | P2 | B42 | OPEN | B49+ |
| DW-B42-02 | P1 | B42 | OPEN | Next live session |
| DW-B42-03 | P2 | B42 | OPEN | Future (T4/T5 block) |
| DW-B42-04 | P2 | B42 | OPEN | B49+ cleanup pass |
| DW-B42-05 | P1 | B42 | OPEN — superseded by DW-B46-01 | Next live session |
| DW-B43-02 | P1 | B43 | OPEN (component a; b closed by B46) | B49+ |
| DW-B43-03 | P2 | B43 | OPEN | Future NT8 upgrade |
| DW-B44-01 | P1 | B44 | PARTIALLY CLOSED — NT8 F5 path closed B48; dotnet test runner open as DW-B48-01 | Dedicated cleanup block |
| DW-B44-02 | P1 | B44 | OPEN | Before next live session |
| DW-B44-03 | P1 | B44 | OPEN (component a; b closed by B46) | B49+ |
| DW-B46-01 | P1 | B46 | OPEN | Next live session |
| DW-B46-02 | P1 | B46 | PARTIALLY CLOSED — isolation done; runner blocked as DW-B48-01 | B49+ or DW-B48-01 closure |
| DW-B47-01 | P1 | B47 | **CLOSED** — B47Tests.cs in Tests\, csproj line 107, $DeployExcludes; re-verify SCAN-04a | — |
| DW-B47-02 | P1 | B47 | OPEN | Next live session |
| DW-B47-03 | P1 | B47 | OPEN — tag shows B47 version; description suffix is Lane C concern | Lane C next block |
| DW-B47-04 | P2 | B47 | OPEN — B47Tests.cs exists; Lane C adds T_B47_05 | Lane C with B47Tests.cs |
| DW-B47-05 | P2 | B47 | OPEN — pre-existing FindRule JS-002 debt | Future cleanup block |
| DW-B48-01 | P1 | B48 | OPEN — CopyEngineTests.cs 60-error dotnet test runner fix | Dedicated cleanup block |
| DW-B48-02 | P2 | B48 | OPEN — process improvement; no code change required | All future lanes |
