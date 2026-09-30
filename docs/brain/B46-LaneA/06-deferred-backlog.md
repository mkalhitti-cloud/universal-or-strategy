# B46-LaneA — Deferred Backlog
Block: PTT-COPIER-B46 (ATM Template Wiring Fix)
Date: 2026-08-06
Status: PIPELINE_COMPLETE

---

## Closed Items This Block

### DW-B43-02 PARTIALLY CLOSED (Component b)
**Prior ID**: DW-B43-02 (carried through B44 as DW-B44-03)
**Closed component**: **(b) `item.AtmModeName` not written at auto-select load time.**
B46 T2 inserts `item.AtmModeName = "Named:" + selName` after `cb.SelectedIndex = defaultIdx`
in `OnFollowerAtmTemplateComboLoaded` (TradeCopierPanel.cs lines 1639-1650). Confirmed by
T2 verifier. The serialisation format `"Named:templateName"` matches `OnFollowerAtmTemplateComboChanged`
exactly — T_B46_03 validates the round-trip.

**Still open component**: **(a) `GetLeaderAtmTemplateName` visual-tree index accuracy.**
`FindVisualChildByIndex<ComboBox>(ct, 2)` may return the wrong ComboBox for some chart configurations,
causing `defaultIdx` to remain 0 (no template auto-selected). Not in B46 scope. User can override
manually via ComboBox. Carried forward — see DW-B43-02 entry in Carried Items section below.

---

### DW-B42-05 UNBLOCKED (not yet closed)
**Prior ID**: DW-B42-05
**Change**: Root cause barriers removed by B46:
  - DW-B46-ATM-EMPTY-GUARD-01 fix (T1) eliminates `"Strategy template name parameter missing"` throw.
  - DW-B46-COMBO-AUTOSELECT-02 fix (T2) ensures `item.AtmModeName` is populated on load.
Full closure requires a live F5 session to verify D1–D6 acceptance criteria.
Moved to DW-B46-01 for tracking.

---

## Current Block Deferred Items

### DW-B46-01 — Live F5 verification: DW-B42-05 re-run after B46
**Priority**: P1 (High — required before next live trading session)
**Source**: New from B46-LaneA — B46 removes root causes; live verification now feasible.
**Context**: DW-B42-05 acceptance criteria D1–D6 can now be tested end-to-end:
  - D2/D3 (stop + target legs): T1 guard keeps strategy alive; T2 ensures non-empty template flows
    through `ParseAtmModeName` → `CopyEngine.SendCopy` → `PttBus.RaiseFillSignal` → `CallAtmStrategyCreate`.
  - D5 (no ATM errors): T1 guard skips `AtmStrategyCreate("")` → no NT8 exception → no "ATM error" message.
  - D6 (strategy stays alive): No MaxRestarts accumulation → strategy not auto-disabled.
**Action**:
1. Configure `PTTFollowerStrategy` in NT8 Control Center Strategies tab with Sim101 as follower.
2. Select a real ATM template in the follower row ComboBox (e.g., "MES $200 SL4"). Click Apply.
3. Fire a test trade from the leader account.
4. Verify D1–D6: orders tab shows follower entry + stop + targets; NT8 Output has no "ATM error";
   strategy remains enabled after the trade.
**Deferred to**: Next live F5 session.

---

### DW-B46-02 — dotnet test runner blocked by DW-B44-01
**Priority**: P1 (High — blocks CI verification of B46Tests.cs)
**Source**: New from B46-LaneA — `CopyEngineTests.cs` pre-existing 60 errors prevent assembly compilation.
**Context**: `B46Tests.cs` introduces zero new compile errors (T4 verifier confirmed: all 3 [Fact] tests
use only production types available in the same assembly). The test runner is blocked solely because
`CopyEngineTests.cs` (DW-B44-01) fails to compile, which prevents the test binary from being produced.
Once DW-B44-01 is resolved, `dotnet test --filter "FullyQualifiedName~B46Tests"` is expected to run all 3 green:
  - `T_B46_01_EmptyAtmTemplateName_GuardFires` — `IsNullOrWhiteSpace(string.Empty)` returns `true`
  - `T_B46_02_NonEmptyAtmTemplateName_GuardDoesNotFire` — `IsNullOrWhiteSpace("MES $200 SL5")` returns `false`
  - `T_B46_03_ComboAutoSelectFormat_ParsesAsNamedMode` — `ParseAtmModeName("Named:MES $200 SL5")` returns `Named("MES $200 SL5")`
**Deferred to**: B47+ or dedicated `CopyEngineTests.cs` cleanup block (DW-B44-01 must close first).

---

## Carried Items from Prior Blocks (Status Updated)

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3
**Priority**: P2 (Low)
**Source**: Carried from B43, B44
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (name[8] <= '3'). Standard MES/ES setups use 2 targets (T1+T2).
**Status After B46**: STILL OPEN — not in B46 scope.
**Deferred to**: B47+ or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

---

### DW-B42-02 — Live NT8 F5 verification of Quick All → BE All sequences
**Priority**: P1 (High)
**Source**: Carried from B43, B44
**Context**: Quick All / BE All interaction sequences can only be verified in a live NT8 session.
Must be confirmed in SIM account before go-live.
**Status After B46**: STILL OPEN — not in B46 scope.
**Deferred to**: Next live F5 session.

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots
**Priority**: P2 (Conditional — low unless T4/T5 slots added)
**Source**: Carried from B43, B44
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design.
If PTT-QX-T4 or T5 slots added, `IsPttQxTarget` must be updated.
**Status After B46**: STILL OPEN — not in B46 scope.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-B42-04 — Comment label `NT8-NEW` in PttContracts.cs:254 should be `NT8-005`
**Priority**: P2 (Low — documentation only)
**Source**: Carried from B43, B44
**Context**: In-source comment at PttContracts.cs line 254 uses `NT8-NEW` instead of catalog ID `NT8-005`.
**Status After B46**: STILL OPEN — not in B46 scope.
**Deferred to**: Any B47+ cleanup pass.
**Fix**: Change `// NT8-NEW` at line 254 to `// NT8-005`.

---

### DW-B42-05 — Live F5 verification of PTTFollowerStrategy ATM bracket spawn
**Priority**: P1 (High)
**Source**: Carried from B43, B44
**Status After B46**: **UNBLOCKED** — B46 T1 (guard) and T2 (write-back) remove the root-cause
barriers. Full closure requires live F5 session.
**Reference**: See DW-B46-01 — this item is superseded by DW-B46-01 for tracking purposes.
**Deferred to**: Next live F5 session (DW-B46-01).

---

### DW-B43-02 — GetLeaderAtmTemplateName visual-tree index accuracy
**Priority**: P1 (High)
**Source**: Carried from B43, B44 (as DW-B44-03)
**Context**: `FindVisualChildByIndex<ComboBox>(ct, 2)` at GetLeaderAtmTemplateName may return the
wrong ComboBox for some chart configurations (wrong visual tree index), causing the follower ComboBox
to default to index 0 ("(none)") instead of the leader's ATM template. B46 T2 closes the write-back
sub-issue (component b: if the correct index IS found, AtmModeName is now written properly).
The index accuracy investigation (component a) remains open.
**Status After B46**: **PARTIALLY CLOSED** — Component (b) CLOSED by T2. Component (a) STILL OPEN.
**Scope**: Component (a) = investigate `FindVisualChildByIndex` index correctness for ChartTrader ComboBox hierarchy.
**Deferred to**: B47 or next targeted investigation block.
**Action**: Check whether index 2 in ChartTrader visual tree actually maps to ATM Strategy ComboBox.
  Verify at runtime. Options: (a) fix index, (b) use name-based lookup, (c) accept manual user override.

---

### DW-B43-03 — NT8-045 update if AtmStrategyTemplates API becomes accessible
**Priority**: P2 (Low — future proofing)
**Source**: Carried from B43, B44
**Context**: The filesystem fallback (NT8-045) is robust. If a future NT8 update exposes
`AtmStrategyTemplates` API, replace the filesystem approach with direct API call.
**Status After B46**: STILL OPEN — not in B46 scope.
**Deferred to**: Future NT8 upgrade block.

---

### DW-B44-01 — CopyEngineTests.cs 60 pre-existing compile errors block test runner
**Priority**: P1 (High — blocks CI test execution for all B42–B46 test files)
**Source**: First identified in B44
**Context**: `CopyEngineTests.cs` has 60 accumulated compile errors from B32–B43:
  CS0246 (`CopyRule` not found), CS0234 (`System.Collections.Immutable` / `NullabilityInfoContext`),
  CS0433 (`Globals` ambiguity), CS0246 (`DisarmTrailBe`, `NinjaTrader.NinjaScript.Instruments`).
Prevents `dotnet test` from executing any test in the assembly (including B44Tests.cs, B45Tests.cs,
B46Tests.cs — all of which are individually error-free).
**Status After B46**: STILL OPEN — explicitly excluded from B46 scope per V12.23.
**Deferred to**: Dedicated `CopyEngineTests.cs` cleanup block.
**Action**:
1. Audit CopyEngineTests.cs for all 60 error sources.
2. Remove/stub NinjaTrader-linked types unavailable in dotnet CLI.
3. Replace `using System.Collections.Immutable` with NT8-004-compliant alternatives.
4. After cleanup, confirm `dotnet test` runs all BXX test filters green.

---

### DW-B44-02 — Live F5 verification of Subscribe() panel-only path
**Priority**: P1 (High)
**Source**: First identified in B44
**Context**: Subscribe/Unsubscribe fix confirmed structurally (B44) but not verified in a live
NT8 session where TradeCopierPanel is attached to a chart without TradeCopierWindow open.
**Status After B46**: STILL OPEN — not in B46 scope.
**Deferred to**: Before next live trading session.
**Action**:
1. Open NT8. Attach TradeCopierPanel to chart via ChartTrader (panel only — no TradeCopierWindow).
2. Enable COPY ON in panel.
3. Place SIM trade on leader account.
4. Confirm follower order appears; close chart — confirm no exception.

---

### DW-B44-03 — DW-B43-02 GetLeaderAtmTemplateName default selection
**Priority**: P1 (High)
**Source**: Carried from B44 (mirrors DW-B43-02)
**Status After B46**: **PARTIALLY CLOSED** — same as DW-B43-02. Component (b) CLOSED by B46 T2;
component (a) still open. See DW-B43-02 above for full context and action items.
**Deferred to**: B47.

---

## Deferred Item Status Table

| ID | Priority | Block Introduced | Status After B46 | Target |
|----|----------|-----------------|-----------------|--------|
| DW-B42-01 | P2 | B42 | OPEN | B47+ |
| DW-B42-02 | P1 | B42 | OPEN | Next live session |
| DW-B42-03 | P2 | B42 | OPEN | Future (T4/T5 slot block) |
| DW-B42-04 | P2 | B42 | OPEN | B47+ cleanup pass |
| DW-B42-05 | P1 | B42 | UNBLOCKED — superseded by DW-B46-01 | Next live session (DW-B46-01) |
| DW-B43-02 | P1 | B43 | PARTIALLY CLOSED (b: CLOSED by T2; a: still open) | B47 |
| DW-B43-03 | P2 | B43 | OPEN | Future NT8 upgrade |
| DW-B44-01 | P1 | B44 | OPEN | Dedicated cleanup block |
| DW-B44-02 | P1 | B44 | OPEN | Before next live session |
| DW-B44-03 | P1 | B44 | PARTIALLY CLOSED (mirrors DW-B43-02) | B47 |
| DW-B46-01 | P1 | B46 | OPEN | Next live session |
| DW-B46-02 | P1 | B46 | OPEN (blocked by DW-B44-01) | B47+ or DW-B44-01 closure |
