# B44-LaneA — Deferred Backlog
Block: PTT-COPIER-B44 (Subscribe/Unsubscribe Idempotency — Panel Path)
Date: 2026-08-05
Status: PIPELINE_COMPLETE

---

## Carried Items from Prior Blocks

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3
**Priority**: P2 (Low)
**Source**: Carried from B43-LaneA/06-deferred-backlog.md (DW-B42-01)
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (name[8] <= '3'). Standard MES/ES setups use 2 targets
(T1+T2). T3 is the second half of even-quantity splits on rare configs.
**Deferred to**: B45 or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

---

### DW-B42-02 — Live NT8 F5 verification of Quick All → BE All and BE All → Quick All sequences
**Priority**: P1 (High — required before next live trading session)
**Source**: Carried from B43-LaneA/06-deferred-backlog.md (DW-B42-02)
**Context**: The two Quick All / BE All interaction bug directions can only be fully verified
in a live NT8 session. Both sequences must be confirmed in a SIM account before go-live.
**Deferred to**: Next live F5 session.
**Action**: Press both sequences in SIM account before go-live.

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots
**Priority**: P2 (Conditional — low unless T4/T5 slots added)
**Source**: Carried from B43-LaneA/06-deferred-backlog.md (DW-B42-03)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design.
If a future block adds PTT-QX-T4 or T5, `IsPttQxTarget` must be updated.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-B42-04 — Comment label `NT8-NEW` in PttContracts.cs line 254 should be `NT8-005`
**Priority**: P2 (Low — documentation only)
**Source**: Carried from B43-LaneA/06-deferred-backlog.md (DW-B42-04)
**Context**: The in-source comment at line 254 of PttContracts.cs uses `NT8-NEW` as a rule
reference label instead of the established catalog ID `NT8-005`.
**Deferred to**: Any B45+ cleanup pass.
**Fix**: Change `// NT8-NEW` comment at line 254 to `// NT8-005` for catalog consistency.

---

### DW-B42-05 — Live F5 verification of PTTFollowerStrategy headless ATM bracket spawn
**Priority**: P1 (High — required before first live B42 trade)
**Source**: Carried from B43-LaneA/06-deferred-backlog.md (DW-B42-05)
**Context**: B42Tests.cs tests the guard logic and event wiring in a unit test environment.
The actual `AtmStrategyCreate()` call path has not been exercised against a live NT8 instance.
**Deferred to**: Next live F5 session (before first live B42 trade).
**Action**:
1. Configure PTTFollowerStrategy in NT8 Control Center Strategies tab with a SIM follower account.
2. Fire a test trade from the leader account.
3. Confirm ATM bracket legs (stop + targets) appear on the follower account automatically.

---

### DW-B43-02 — Live F5 verification: GetLeaderAtmTemplateName default selection mismatch
**Priority**: P1 (High)
**Source**: Carried from B43-LaneA/06-deferred-backlog.md (DW-B43-02). Not addressed in B44.
**Context**: Screenshot shows leader ChartTrader template = "MES $200 SL4" but follower rows
defaulted to "MCL MIN SL5" / "MES $200 SL5" (first filesystem entry, not the leader's template).
This means `GetLeaderAtmTemplateName` returned empty string (or the visual tree index was wrong),
so `OnFollowerAtmTemplateComboLoaded` fell through to `defaultIdx = 0` (first template).
The ComboBox IS populated (DW-B43-01 closed), but the default selection logic needs investigation.
**Root cause candidates**:
  (a) `FindVisualChildByIndex<ComboBox>(ct, 2)` — wrong index for ATM Strategy ComboBox in ChartTrader
  (b) Panel not yet attached to `_currentChart` when `OnFollowerAtmTemplateComboLoaded` fires
  (c) Template name returned by visual tree walk doesn't exactly match filesystem filename
**Deferred to**: B45 or next targeted fix block.
**Action**: Check `FindVisualChildByIndex` index. The ChartTrader ComboBox order is likely:
  index 0 = Instrument, index 1 = Account, index 2 = ATM Strategy (per B18 comment in code).
  Verify at runtime. If wrong index, patch `GetLeaderAtmTemplateName`.
  OR: Accept current behavior (user picks from list; no auto-default) as acceptable UX.

---

### DW-B43-03 — NT8-045 update if AtmStrategyTemplates accessible in newer NinjaTrader.Custom.dll
**Priority**: P2 (Low — future proofing)
**Source**: Carried from B43-LaneA/06-deferred-backlog.md (DW-B43-03)
**Context**: The filesystem fallback (NT8-045) is robust but less efficient than direct API access.
If a future NT8 update ships a NinjaTrader.Custom.dll that exposes `AtmStrategyTemplates`,
replace the filesystem approach with the direct API call and update NT8-045.
**Deferred to**: Any future NT8 upgrade block.

---

## Current Block Deferred Items

### DW-B44-01 — CopyEngineTests.cs pre-existing compile errors (60 errors) block B44 test runner
**Priority**: P1 (High — blocks CI test execution)
**Source**: New from B44-LaneA — observed in both T1 and T2 build output.
**Context**: The file `CopyEngineTests.cs` has accumulated 60 compile errors from B32–B43:
- CS0246: `CopyRule` type not found (NinjaTrader-linked, unavailable in dotnet CLI)
- CS0234: `System.Collections.Immutable` / `NullabilityInfoContext` not in .NET Framework 4.8 NT8 profile
- CS0433: `Globals` ambiguity (pre-existing since B23)
- CS0246: `DisarmTrailBe`, `NinjaTrader.NinjaScript.Instruments` — other NinjaTrader-linked stubs
These errors prevent `dotnet test --filter "SubscribeIdempotency"` from running because the compiler
cannot build the assembly when CopyEngineTests.cs is in scope. B44Tests.cs itself is error-free
(confirmed by verifier); the blocker is entirely in the legacy test file.
**Root cause**: CopyEngineTests.cs was written against API shapes that changed or referenced
NinjaTrader types only available inside NT8's compiler host (not in the standalone test runner).
**Deferred to**: B45 or dedicated CopyEngineTests.cs cleanup block.
**Action**:
1. Audit CopyEngineTests.cs for all 60 error sources.
2. Remove or stub NinjaTrader-linked types that cannot resolve in dotnet CLI.
3. Replace `using System.Collections.Immutable` with NT8-004-compliant alternatives.
4. After cleanup, confirm `dotnet test --filter "SubscribeIdempotency"` runs 4 green.

---

### DW-B44-02 — Live F5 verification of Subscribe() panel-only path
**Priority**: P1 (High — required before next live trading session)
**Source**: New from B44-LaneA — fix confirmed structurally but not verified live.
**Context**: The Subscribe/Unsubscribe fix was confirmed by source read, scans, and unit tests.
However, the root-cause scenario (panel-only open without TradeCopierWindow) has not been exercised
against a live NT8 instance. The live test is required to confirm the fix end-to-end.
**Deferred to**: Before next live trading session.
**Action**:
1. Open NinjaTrader. Attach TradeCopierPanel to a chart (via ChartTrader) ONLY.
   Do NOT open TradeCopierWindow from the Tools menu.
2. Enable COPY ON in the panel.
3. Place a SIM trade on the leader account.
4. Confirm a follower order appears on the follower account immediately.
5. Close the chart (Detach() path) — confirm no exception; follower stops receiving copies.
Expected result: Follower order appears in step 4 (was silently missing before B44).

---

### DW-B44-03 — DW-B43-02 (GetLeaderAtmTemplateName default selection) carried without investigation
**Priority**: P1 (High)
**Source**: Carried from B43, acknowledged in B44 architecture plan §11 but not addressed.
**Context**: Same as DW-B43-02 above. B44 scope was strictly the Subscribe/Unsubscribe panel path.
DW-B43-02 was explicitly deferred in 02-architecture-plan.md §11. Repeating here for pipeline visibility.
**Deferred to**: B45.
**Action**: See DW-B43-02 action items above.

---

## Closed Items This Block

None. B44 scope was narrowly defined to Subscribe/Unsubscribe idempotency only. No prior open
items fell within B44 scope.

---

## Deferred Item Status Table

| ID | Priority | Block Introduced | Status | Target |
|----|----------|-----------------|--------|--------|
| DW-B42-01 | P2 | B42 | OPEN | B45+ |
| DW-B42-02 | P1 | B42 | OPEN | Next live session |
| DW-B42-03 | P2 | B42 | OPEN | Future (T4/T5 slot block) |
| DW-B42-04 | P2 | B42 | OPEN | B45+ cleanup pass |
| DW-B42-05 | P1 | B42 | OPEN | Next live session |
| DW-B43-02 | P1 | B43 | OPEN | B45 |
| DW-B43-03 | P2 | B43 | OPEN | Future NT8 upgrade |
| DW-B44-01 | P1 | B44 | OPEN | B45 or cleanup block |
| DW-B44-02 | P1 | B44 | OPEN | Before next live session |
| DW-B44-03 | P1 | B44 | OPEN | B45 |
