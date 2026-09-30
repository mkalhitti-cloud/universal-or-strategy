# B42-LaneA — Deferred Backlog
Block: PTT-COPIER-B42 (PTTFollowerStrategy: Native ATM Brackets on Followers)
Date: 2026-08-05
Status: PIPELINE_COMPLETE

---

## Current Block Deferred Items

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3
**Priority**: P2 (Low)
**Source**: Carried from B42-QX-BE-01/06-deferred-backlog.md (DW-B42-01)
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (name[8] <= '3'). Standard MES/ES setups use 2 targets
(T1+T2). T3 is the second half of even-quantity splits on rare configs.
**Deferred to**: B43 or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

---

### DW-B42-02 — Live NT8 F5 verification of Quick All → BE All and BE All → Quick All sequences
**Priority**: P1 (High — required before next live trading session)
**Source**: Carried from B42-QX-BE-01/06-deferred-backlog.md (DW-B42-02)
**Context**: The two Quick All / BE All interaction bug directions can only be fully verified
in a live NT8 session. Both sequences must be confirmed in a SIM account before go-live.
**Deferred to**: Next live F5 session.
**Action**: Press both sequences in SIM account before go-live.

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots
**Priority**: P2 (Conditional — low unless T4/T5 slots added)
**Source**: Carried from B42-QX-BE-01/06-deferred-backlog.md (DW-B42-03)
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design.
If a future block adds PTT-QX-T4 or T5, `IsPttQxTarget` must be updated.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-B42-04 — Comment label `NT8-NEW` in PttContracts.cs line 254 should be `NT8-005`
**Priority**: P2 (Low — documentation only)
**Source**: New from B42-LaneA (noted in T1-VERIFY_PASS and confirmed by Phase 5 review)
**Context**: The in-source comment at line 254 of PttContracts.cs uses `NT8-NEW` as a rule
reference label instead of the established catalog ID `NT8-005`. This is a comment label
inconsistency only — no runtime behavior or compilation impact.
**Deferred to**: Any B43+ cleanup pass.
**Fix**: Change `// NT8-NEW` comment at line 254 to `// NT8-005` for catalog consistency.

---

### DW-B42-05 — Live F5 verification of PTTFollowerStrategy headless ATM bracket spawn
**Priority**: P1 (High — required before first live B42 trade)
**Source**: New from B42-LaneA
**Context**: B42Tests.cs tests the guard logic and event wiring in a unit test environment.
The actual `AtmStrategyCreate()` call path has not been exercised against a live NT8 instance.
Before going live, the full pipeline (CopyEngine fills → PttBus.FillSignal → PTTFollowerStrategy
→ AtmStrategyCreate → bracket legs spawned on follower account) must be confirmed in SIM.
**Deferred to**: Next live F5 session (before first live B42 trade).
**Action**:
1. Configure PTTFollowerStrategy in NT8 Control Center Strategies tab with a SIM follower account.
2. Fire a test trade from the leader account.
3. Confirm ATM bracket legs (stop + targets) appear on the follower account automatically.
4. Confirm leader ATM behaviour is unchanged.
