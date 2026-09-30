# Deferred Backlog — PTT-COPIER

---

## B47-LaneC — Block Summary

**Block**: PTT-COPIER-B47 Lane C
**Items closed this block**: DW-B47-01, DW-B47-03, DW-B47-04
**Date**: 2026-08-08
**Status**: FINAL_PASS

### Items Closed This Block

| ID | Description | Closed By |
|----|-------------|-----------|
| DW-B47-01 | `B47Tests.cs` — xUnit tests T_B47_01 through T_B47_09 for `IsFollowerAccount` + panel UX spec coverage | T1-C — B47Tests.cs created with all 9 tests |
| DW-B47-03 | `PttBuild.Tag` = `"PTT-COPIER B47 | panel-ux-redesign | 2026-08-07"` | T2-C — VERIFIED_NO_CHANGE; tag already correct at CopyEngine.cs line 41 |
| DW-B47-04 | Add T_B47_05: `TryAutoApply_NullLeader_AddRuleNotCalled` (null-leader guard proxy) | T1-C — T_B47_05 present in B47Tests.cs |

### Items Opened This Block

None. Lane C closes only. No new deferred items introduced.

---

## B47-LaneA — Block Summary (carried from docs/brain/B47-LaneA/06-deferred-backlog.md)

**Block**: PTT-COPIER-B47 Lane A
**Defect closed**: DW-B47-BE-FOLLOWER-SCOPE (P0 — `IsFollowerAccount` guard added to all 3 fan-out paths)
**Date**: 2026-08-08
**Status**: FINAL_PASS

### Items Opened in B47-LaneA (all resolved by LaneC or carried forward)

| ID | Description | Priority | Status After B47-LaneC |
|----|-------------|----------|------------------------|
| DW-B47-01 | `B47Tests.cs` tests | P1 | **CLOSED** by B47-LaneC T1-C |
| DW-B47-02 | Live F5 session: verify BE ALL / Quick ALL no longer fires on Sim102 | P1 | OPEN — Next live session |
| DW-B47-03 | `PttBuild.Tag` update to B47 value | P1 | **CLOSED** by B47-LaneC T2-C |
| DW-B47-04 | T_B47_05 null-leader guard proxy | P2 | **CLOSED** by B47-LaneC T1-C |
| DW-B47-05 | `FindRule` `return null` — JS-002 pre-existing debt | P2 | OPEN — Future cleanup block |

---

## Carried Items — Status After B47-LaneC

### DW-B47-02 — Live F5: Sim102 not fired by BE ALL / Quick ALL

**Priority**: P1 (High)
**Introduced**: B47-LaneA
**Context**: B47-LaneA added `IsFollowerAccount` guard to all 3 fan-out paths (CopyEngine,
PttBreakEven, PttGlobalQuickExit). Structural verification complete. Live F5 needed to confirm
17 `CancelStaleBrackets` calls are eliminated.
**Status After B47-LaneC**: OPEN — not in Lane C scope.
**Target**: Next live session (can combine with DW-B42-02 and DW-B46-01 for a single live session).

---

### DW-B47-05 — FindRule return null (JS-002 pre-existing debt)

**Priority**: P2 (Medium)
**Introduced**: B47-LaneA
**Context**: `FindRule` at CopyEngine.cs lines 1381 and 1387 contains `return null` — JS-002
violation. This is pre-existing debt not introduced by B47. Confirmed by T2-C SCAN-03 scan
(pre-existing hits noted, not on touched line).
**Status After B47-LaneC**: OPEN — not in Lane C scope.
**Target**: Future cleanup block.
**Fix**: Replace `return null` with `Option<CopyRule>` or nullable annotation.

---

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: P2 (Low)
**Introduced**: B42
**Context**: `T_BUG_QX_BE_01` asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (`name[8] <= '3'`). Standard MES/ES setups use 2 targets.
**Status After B47-LaneC**: OPEN — not in B47 scope.
**Target**: B48+ or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

---

### DW-B42-02 — Live NT8 F5: Quick All / BE All interaction sequences

**Priority**: P1 (High)
**Introduced**: B42
**Context**: Quick All / BE All interaction sequences can only be verified in a live NT8 session.
Must be confirmed in SIM account before go-live.
**Status After B47-LaneC**: OPEN — can combine with DW-B47-02 and DW-B46-01 in next live session.
**Target**: Next live session.

---

### DW-B42-03 — IsPttQxTarget range extension for future T4/T5 slots

**Priority**: P2 (Conditional — low unless T4/T5 slots added)
**Introduced**: B42
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design.
If PTT-QX-T4 or T5 slots added, `IsPttQxTarget` must be updated.
**Status After B47-LaneC**: OPEN — not in scope.
**Target**: Block that adds 4th+ target slot.

---

### DW-B42-04 — Comment label NT8-NEW at PttContracts.cs:254

**Priority**: P2 (Low — documentation only)
**Introduced**: B42
**Context**: In-source comment at PttContracts.cs line 254 uses `NT8-NEW` instead of catalog ID `NT8-005`.
**Status After B47-LaneC**: OPEN — not in scope.
**Target**: B48+ cleanup pass.
**Fix**: Change `// NT8-NEW` at line 254 to `// NT8-005`.

---

### DW-B42-05 — Live F5: PTTFollowerStrategy ATM bracket spawn

**Priority**: P1 (High)
**Introduced**: B42
**Context**: Superseded by DW-B46-01 for tracking. Root-cause barriers removed by B46.
Full closure requires live F5 session (DW-B46-01).
**Status After B47-LaneC**: OPEN — superseded by DW-B46-01.
**Target**: Next live session (DW-B46-01).

---

### DW-B43-02 — GetLeaderAtmTemplateName visual-tree index accuracy (component a)

**Priority**: P1 (High)
**Introduced**: B43
**Context**: `FindVisualChildByIndex<ComboBox>(ct, 2)` may return wrong ComboBox for some chart
configurations. B46 T2 closed component (b) — write-back of `AtmModeName` on load. Component (a)
index correctness investigation is still open.
**Status After B47-LaneC**: OPEN (component a).
**Target**: B48+ or next targeted investigation block.
**Action**: Check whether index 2 in ChartTrader visual tree maps to ATM Strategy ComboBox.

---

### DW-B43-03 — NT8-045 update if AtmStrategyTemplates API becomes accessible

**Priority**: P2 (Low — future-proofing)
**Introduced**: B43
**Context**: Filesystem fallback (NT8-045) is robust. If NT8 update exposes `AtmStrategyTemplates`
API, replace filesystem approach with direct API call.
**Status After B47-LaneC**: OPEN.
**Target**: Future NT8 upgrade block.

---

### DW-B44-01 — CopyEngineTests.cs 60 pre-existing compile errors block test runner

**Priority**: P1 (High — blocks CI test execution for all B42–B47 test files)
**Introduced**: B44
**Context**: `CopyEngineTests.cs` has 60 accumulated errors from B32–B43 (CS0246 `CopyRule`,
CS0234 `System.Collections.Immutable`, CS0433 `Globals`, CS0246 `DisarmTrailBe`). Prevents
`dotnet test` from executing any test in the assembly. Individual test files B42–B47 are
error-free; they are blocked solely by `CopyEngineTests.cs`. B47Tests.cs is individually clean
but cannot be run until this is resolved.
**Status After B47-LaneC**: OPEN — explicitly excluded from B47 scope.
**Target**: Dedicated `CopyEngineTests.cs` cleanup block.
**Action**:
1. Audit `CopyEngineTests.cs` for all 60 error sources.
2. Remove/stub NinjaTrader-linked types unavailable in dotnet CLI.
3. Replace `using System.Collections.Immutable` with NT8-004-compliant alternatives.
4. After cleanup, confirm `dotnet test` runs all BXX test filters green.

---

### DW-B44-02 — Live F5: Subscribe() panel-only path verification

**Priority**: P1 (High)
**Introduced**: B44
**Context**: Subscribe/Unsubscribe fix confirmed structurally (B44) but not verified in a live
NT8 session where TradeCopierPanel is attached to a chart without TradeCopierWindow open.
**Status After B47-LaneC**: OPEN.
**Target**: Before next live trading session.

---

### DW-B44-03 — GetLeaderAtmTemplateName default selection (mirrors DW-B43-02)

**Priority**: P1 (High)
**Introduced**: B44
**Context**: Same as DW-B43-02. Component (b) closed by B46 T2. Component (a) still open.
**Status After B47-LaneC**: OPEN (component a).
**Target**: B48+.

---

### DW-B46-01 — Live F5: DW-B42-05 re-run after B46; combine DW-B47-02

**Priority**: P1 (High — required before next live trading session)
**Introduced**: B46
**Context**: B46 removed root-cause barriers (ATM guard + write-back). Full closure of DW-B42-05
requires live F5 session. B47-LaneA adds: confirm Sim102 brackets no longer wiped (DW-B47-02
combined verification).
**Status After B47-LaneC**: OPEN.
**Target**: Next live F5 session.
**Action**:
1. Configure `PTTFollowerStrategy` with Sim101 leader / Sim102 follower.
2. Select real ATM template in follower row. Click Apply (or auto-apply per B47-AUTO-RULE-01).
3. Fire test trade from leader. Verify D1–D6 and confirm Quick ALL / BE ALL do not touch Sim102
   brackets (DW-B47-02 combined verification).

---

### DW-B46-02 — dotnet test runner blocked by DW-B44-01

**Priority**: P1 (High — blocks CI verification of B46Tests.cs and B47Tests.cs)
**Introduced**: B46
**Context**: `B46Tests.cs` (3 tests) and `B47Tests.cs` (9 tests, created by B47-LaneC) introduce
zero new compile errors, but `CopyEngineTests.cs` prevents the test binary from being produced.
**Status After B47-LaneC**: OPEN — blocked by DW-B44-01.
**Target**: B48+ or DW-B44-01 closure.

---

## Deferred Item Status Table — After B47-LaneC

| ID | Priority | Block Introduced | Status After B47-LaneC | Target |
|----|----------|-----------------|------------------------|--------|
| DW-B42-01 | P2 | B42 | OPEN | B48+ |
| DW-B42-02 | P1 | B42 | OPEN | Next live session |
| DW-B42-03 | P2 | B42 | OPEN | Future (T4/T5 block) |
| DW-B42-04 | P2 | B42 | OPEN | B48+ cleanup pass |
| DW-B42-05 | P1 | B42 | OPEN — superseded by DW-B46-01 | Next live session |
| DW-B43-02 | P1 | B43 | OPEN (component a; b closed by B46) | B48+ |
| DW-B43-03 | P2 | B43 | OPEN | Future NT8 upgrade |
| DW-B44-01 | P1 | B44 | OPEN | Dedicated cleanup block |
| DW-B44-02 | P1 | B44 | OPEN | Before next live session |
| DW-B44-03 | P1 | B44 | OPEN (component a; b closed by B46) | B48+ |
| DW-B46-01 | P1 | B46 | OPEN | Next live session |
| DW-B46-02 | P1 | B46 | OPEN (blocked by DW-B44-01) | B48+ or DW-B44-01 closure |
| DW-B47-01 | P1 | B47 | **CLOSED** by B47-LaneC T1-C | — |
| DW-B47-02 | P1 | B47 | OPEN | Next live session |
| DW-B47-03 | P1 | B47 | **CLOSED** by B47-LaneC T2-C | — |
| DW-B47-04 | P2 | B47 | **CLOSED** by B47-LaneC T1-C | — |
| DW-B47-05 | P2 | B47 | OPEN | Future cleanup block |
