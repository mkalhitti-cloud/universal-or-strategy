# Deferred Backlog — PTT-COPIER

---

## B47-LaneA — Block Summary

**Block**: PTT-COPIER-B47 Lane A
**Defect closed**: DW-B47-BE-FOLLOWER-SCOPE (P0 — `IsFollowerAccount` guard added to all 3 fan-out paths)
**Date**: 2026-08-08
**Status**: FINAL_PASS

### Items Opened This Block

| ID | Description | Priority | Target |
|----|-------------|----------|--------|
| DW-B47-01 | `B47Tests.cs` — xUnit tests T_B47_01 through T_B47_04 for `IsFollowerAccount` + guards. Lane C owns test file. | P1 | Lane C this block |
| DW-B47-02 | Live F5 session: verify BE ALL / Quick ALL no longer fires on Sim102 after B47. 17 `CancelStaleBrackets` calls eliminated. | P1 | Next live session |
| DW-B47-03 | `PttBuild.Tag` update from `"PTT-COPIER B46 | atm-template-guard | 2026-08-06"` to `"PTT-COPIER B47 | be-follower-scope | 2026-08-08"`. Lane A must NOT touch this — Lane C owns the tag. | P1 | Lane C this block |
| DW-B47-04 | Add T_B47_05: `IsFollowerAccount_ReturnsFalse_WhenNoRules` (empty `_rules` edge case; plan §10 listed; ticket dropped it). Lane C should add to B47Tests.cs. | P2 | Lane C with B47Tests.cs |
| DW-B47-05 | `FindRule` (CopyEngine.cs lines 1381/1387) contains `return null` — JS-002 pre-existing debt not introduced by B47. | P2 | Future cleanup block |

### Items Closed This Block

None from prior blocks were in scope for B47-LaneA. B47-LaneA was surgical: three source files only.

---

## Carried Items from B46-LaneA (status updated for B47)

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: P2 (Low)
**Introduced**: B42
**Context**: `T_BUG_QX_BE_01` asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (`name[8] <= '3'`). Standard MES/ES setups use 2 targets.
**Status After B47**: STILL OPEN — not in B47 scope.
**Target**: B48+ or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

---

### DW-B42-02 — Live NT8 F5 verification of Quick All → BE All sequences

**Priority**: P1 (High)
**Introduced**: B42
**Context**: Quick All / BE All interaction sequences can only be verified in a live NT8 session.
Must be confirmed in SIM account before go-live.
**Status After B47**: STILL OPEN — not in B47 scope. Note: B47 now guards followers in both Quick
ALL and BE ALL paths; the live session should verify the corrected behaviour.
**Target**: Next live session (can be combined with DW-B47-02).

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots

**Priority**: P2 (Conditional — low unless T4/T5 slots added)
**Introduced**: B42
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design.
If PTT-QX-T4 or T5 slots added, `IsPttQxTarget` must be updated.
**Status After B47**: STILL OPEN — not in B47 scope.
**Target**: Block that adds 4th+ target slot.

---

### DW-B42-04 — Comment label `NT8-NEW` in PttContracts.cs:254 should be `NT8-005`

**Priority**: P2 (Low — documentation only)
**Introduced**: B42
**Context**: In-source comment at PttContracts.cs line 254 uses `NT8-NEW` instead of catalog ID `NT8-005`.
**Status After B47**: STILL OPEN — not in B47 scope.
**Target**: Any B48+ cleanup pass.
**Fix**: Change `// NT8-NEW` at line 254 to `// NT8-005`.

---

### DW-B42-05 — Live F5 verification of PTTFollowerStrategy ATM bracket spawn

**Priority**: P1 (High)
**Introduced**: B42
**Context**: Superseded by DW-B46-01 for tracking. Root-cause barriers removed by B46.
Full closure requires live F5 session (DW-B46-01).
**Status After B47**: STILL OPEN — superseded by DW-B46-01.
**Target**: Next live session (DW-B46-01).

---

### DW-B43-02 — GetLeaderAtmTemplateName visual-tree index accuracy (component a)

**Priority**: P1 (High)
**Introduced**: B43
**Context**: `FindVisualChildByIndex<ComboBox>(ct, 2)` may return the wrong ComboBox for some
chart configurations, causing `defaultIdx` to remain 0. B46 T2 closed component (b): write-back
of `AtmModeName` on load. Component (a) — index correctness investigation — is still open.
**Status After B47**: STILL OPEN (component a) — not in B47 scope.
**Target**: B48+ or next targeted investigation block.
**Action**: Check whether index 2 in ChartTrader visual tree actually maps to ATM Strategy ComboBox.
  Options: (a) fix index, (b) use name-based lookup, (c) accept manual override.

---

### DW-B43-03 — NT8-045 update if AtmStrategyTemplates API becomes accessible

**Priority**: P2 (Low — future-proofing)
**Introduced**: B43
**Context**: Filesystem fallback (NT8-045) is robust. If a future NT8 update exposes
`AtmStrategyTemplates` API, replace the filesystem approach with direct API call.
**Status After B47**: STILL OPEN — not in B47 scope.
**Target**: Future NT8 upgrade block.

---

### DW-B44-01 — CopyEngineTests.cs 60 pre-existing compile errors block test runner

**Priority**: P1 (High — blocks CI test execution for all B42–B47 test files)
**Introduced**: B44
**Context**: `CopyEngineTests.cs` has 60 accumulated errors from B32–B43 (CS0246 `CopyRule`,
CS0234 `System.Collections.Immutable`, CS0433 `Globals`, CS0246 `DisarmTrailBe`). Prevents
`dotnet test` from executing any test in the assembly. Individual test files B42–B47 are
error-free; they are blocked solely by `CopyEngineTests.cs`.
**Status After B47**: STILL OPEN — explicitly excluded from B47 scope.
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
**Status After B47**: STILL OPEN — not in B47 scope.
**Target**: Before next live trading session.
**Action**:
1. Open NT8. Attach TradeCopierPanel to chart via ChartTrader (panel only — no TradeCopierWindow).
2. Enable COPY ON in panel. Place SIM trade on leader account.
3. Confirm follower order appears; close chart — confirm no exception.

---

### DW-B44-03 — DW-B43-02 GetLeaderAtmTemplateName default selection (mirrors DW-B43-02)

**Priority**: P1 (High)
**Introduced**: B44
**Context**: Same as DW-B43-02. Component (b) closed by B46 T2. Component (a) still open.
**Status After B47**: STILL OPEN (component a) — same as DW-B43-02.
**Target**: B48+.

---

### DW-B46-01 — Live F5 verification: DW-B42-05 re-run after B46

**Priority**: P1 (High — required before next live trading session)
**Introduced**: B46
**Context**: B46 removed the root-cause barriers (ATM guard + write-back). Full closure of
DW-B42-05 requires live F5 session to verify D1–D6 acceptance criteria. B47 adds additional
verification requirement: Sim102 brackets no longer wiped (DW-B47-02 can be combined).
**Status After B47**: STILL OPEN — not closed by B47.
**Target**: Next live F5 session.
**Action**:
1. Configure `PTTFollowerStrategy` with Sim101 leader / Sim102 follower.
2. Select real ATM template in follower row. Click Apply (or auto-apply after B47-AUTO-RULE-01).
3. Fire test trade from leader. Verify D1–D6 and additionally confirm Quick ALL / BE ALL do not
   touch Sim102 brackets (DW-B47-02 combined verification).

---

### DW-B46-02 — dotnet test runner blocked by DW-B44-01

**Priority**: P1 (High — blocks CI verification of B46Tests.cs and B47Tests.cs)
**Introduced**: B46
**Context**: `B46Tests.cs` (3 tests) and `B47Tests.cs` (Lane C) introduce zero new compile errors,
but `CopyEngineTests.cs` prevents the test binary from being produced.
**Status After B47**: STILL OPEN — blocked by DW-B44-01.
**Target**: B48+ or DW-B44-01 closure.

---

## Deferred Item Status Table

| ID | Priority | Block Introduced | Status After B47 | Target |
|----|----------|-----------------|------------------|--------|
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
| DW-B47-01 | P1 | B47 | OPEN — Lane C owns B47Tests.cs | Lane C this block |
| DW-B47-02 | P1 | B47 | OPEN | Next live session |
| DW-B47-03 | P1 | B47 | OPEN — Lane C must execute tag update | Lane C this block |
| DW-B47-04 | P2 | B47 | OPEN — Lane C should add T_B47_05 | Lane C with B47Tests.cs |
| DW-B47-05 | P2 | B47 | OPEN — pre-existing FindRule JS-002 debt | Future cleanup block |
