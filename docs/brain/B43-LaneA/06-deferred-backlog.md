# B43-LaneA — Deferred Backlog
Block: PTT-COPIER-B43 (Per-Follower ATM Template ComboBox)
Date: 2026-08-05
Status: PIPELINE_COMPLETE

---

## Carried Items from Prior Blocks

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3
**Priority**: P2 (Low)
**Source**: Carried from B42-LaneA/06-deferred-backlog.md (DW-B42-01)
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (name[8] <= '3'). Standard MES/ES setups use 2 targets
(T1+T2). T3 is the second half of even-quantity splits on rare configs.
**Deferred to**: B44 or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

---

### DW-B42-02 — Live NT8 F5 verification of Quick All → BE All and BE All → Quick All sequences
**Priority**: P1 (High — required before next live trading session)
**Source**: Carried from B42-LaneA/06-deferred-backlog.md (DW-B42-02)
**Context**: The two Quick All / BE All interaction bug directions can only be fully verified
in a live NT8 session. Both sequences must be confirmed in a SIM account before go-live.
**Deferred to**: Next live F5 session.
**Action**: Press both sequences in SIM account before go-live.

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots
**Priority**: P2 (Conditional — low unless T4/T5 slots added)
**Source**: Carried from B42-LaneA/06-deferred-backlog.md (DW-B42-03)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design.
If a future block adds PTT-QX-T4 or T5, `IsPttQxTarget` must be updated.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-B42-04 — Comment label `NT8-NEW` in PttContracts.cs line 254 should be `NT8-005`
**Priority**: P2 (Low — documentation only)
**Source**: Carried from B42-LaneA/06-deferred-backlog.md (DW-B42-04)
**Context**: The in-source comment at line 254 of PttContracts.cs uses `NT8-NEW` as a rule
reference label instead of the established catalog ID `NT8-005`.
**Deferred to**: Any B44+ cleanup pass.
**Fix**: Change `// NT8-NEW` comment at line 254 to `// NT8-005` for catalog consistency.

---

### DW-B42-05 — Live F5 verification of PTTFollowerStrategy headless ATM bracket spawn
**Priority**: P1 (High — required before first live B42 trade)
**Source**: Carried from B42-LaneA/06-deferred-backlog.md (DW-B42-05)
**Context**: B42Tests.cs tests the guard logic and event wiring in a unit test environment.
The actual `AtmStrategyCreate()` call path has not been exercised against a live NT8 instance.
**Deferred to**: Next live F5 session (before first live B42 trade).
**Action**:
1. Configure PTTFollowerStrategy in NT8 Control Center Strategies tab with a SIM follower account.
2. Fire a test trade from the leader account.
3. Confirm ATM bracket legs (stop + targets) appear on the follower account automatically.

---

## Current Block Deferred Items

### DW-B43-01 — CLOSED 2026-08-05
**Status**: CLOSED — F5 screenshot confirmed template names populate correctly.
Filesystem path `Documents\NinjaTrader 8\templates\AtmStrategy\*.xml` resolves on this machine.
Panel follower rows show real template names: MCL MIN SL5, MES $200 SL5, etc. (NT8-045 PASS).

---

### DW-B43-02 — Live F5 verification: GetLeaderAtmTemplateName default selection mismatch
**Priority**: P1 (High)
**Source**: New from B43-LaneA — partially verified at F5 2026-08-05
**Context**: Screenshot shows leader ChartTrader template = "MES $200 SL4" but follower rows
defaulted to "MCL MIN SL5" / "MES $200 SL5" (first filesystem entry, not the leader's template).
This means `GetLeaderAtmTemplateName` returned empty string (or the visual tree index was wrong),
so `OnFollowerAtmTemplateComboLoaded` fell through to `defaultIdx = 0` (first template).
The ComboBox IS populated (DW-B43-01 closed), but the default selection logic needs investigation.
**Root cause candidates**:
  (a) `FindVisualChildByIndex<ComboBox>(ct, 2)` — wrong index for ATM Strategy ComboBox in ChartTrader
  (b) Panel not yet attached to `_currentChart` when `OnFollowerAtmTemplateComboLoaded` fires
  (c) Template name returned by visual tree walk doesn't exactly match filesystem filename
**Deferred to**: B44 or next targeted fix block.
**Action**: Check `FindVisualChildByIndex` index. The ChartTrader ComboBox order is likely:
  index 0 = Instrument, index 1 = Account, index 2 = ATM Strategy (per B18 comment in code).
  Verify at runtime. If wrong index, patch `GetLeaderAtmTemplateName`.
  OR: Accept current behavior (user picks from list; no auto-default) as acceptable UX.

---

### DW-B43-03 — NT8-045 update if AtmStrategyTemplates accessible in newer NinjaTrader.Custom.dll
**Priority**: P2 (Low — future proofing)
**Source**: New from B43-LaneA
**Context**: The filesystem fallback (NT8-045) is robust but less efficient than direct API access.
If a future NT8 update ships a NinjaTrader.Custom.dll that exposes `AtmStrategyTemplates`,
replace the filesystem approach with the direct API call and update NT8-045.
**Deferred to**: Any future NT8 upgrade block.
