# B47-LaneB — Deferred Backlog
Block: PTT-COPIER-B47 (Panel UX Redesign)
Date: 2026-08-07
Status: PIPELINE_COMPLETE

---

## Closed Items This Block

### UI Redesign — New Items Completed (B47-LaneB Scope)

B47-LaneB was a UI redesign block. It introduced 6 new DW items (DW-B47-INLINE-FOLLOWERS-02,
DW-B47-AUTO-RULE-01, DW-B47-COPIER-COLLAPSE-05, DW-B47-FOLLOWERS-SORT-06, DW-B47-BUTTON-LAYOUT-03,
DW-B47-PANEL-ORDER-04) and **closed all 6** via tickets T1-B through T6-B. These are not carry-forward
items — they were B47 scope items opened and closed within B47-LaneB.

No items from B46-LaneA's deferred backlog were in scope for B47-LaneB. B46 items are carried forward
unchanged (see Carried Items section below).

---

## Current Block Deferred Items

### DW-B47-01 — GetLeaderAtmTemplateName visual-tree index accuracy (component a of DW-B43-02)

**Priority**: P2 (downgraded from P1 — see note below)
**Source**: Carried from DW-B43-02 component (a) via B44, B46. Targeted to B47 in B46 backlog.
**Context**: `FindVisualChildByIndex<ComboBox>(ct, 2)` in `GetLeaderAtmTemplateName` may return the
wrong ComboBox for some chart configurations (wrong visual tree index), causing `defaultIdx` to
remain 0 (no template auto-selected). The index accuracy investigation remains open.

**Priority downgrade rationale**: With B47-LaneB delivering the inline ScrollViewer replacement,
the critical failure path for this issue is now mitigated:
- The inline follower rows use imperative `DataContext` binding (`atmCombo.DataContext = item`) — not
  `FindVisualChildByIndex`. `GetLeaderAtmTemplateName` is still used for default selection, but if
  the index is wrong, the user now sees an explicit ATM ComboBox per-row and can select the correct
  template manually. `TryAutoApply()` fires immediately on selection change, eliminating the silent
  failure mode. Priority downgraded from P1 to P2.

**Status After B47**: CARRIED FORWARD — not addressed in B47-LaneB (B47-LaneB is Panel UX redesign
only; visual-tree index investigation is separate scope).
**Deferred to**: B48+
**Action**: Investigate whether index 2 in ChartTrader visual tree correctly maps to ATM Strategy
ComboBox. Options: (a) fix index, (b) use name-based lookup, (c) accept manual user override
(now less critical given TryAutoApply wiring).

---

## Carried Items from Prior Blocks (Status Updated)

### DW-B42-01 — T_BUG_QX_BE_01 does not assert PTT-QX-T3

**Priority**: P2 (Low)
**Source**: Carried from B42, B43, B44, B46
**Context**: T_BUG_QX_BE_01 asserts true for PTT-QX-T1 and PTT-QX-T2 only. The production
predicate `IsPttQxTarget` also accepts T3 (name[8] <= '3'). Standard MES/ES setups use 2 targets (T1+T2).
**Status After B47**: STILL OPEN — not in B47 scope.
**Deferred to**: B48+ or first block where T3 is confirmed in production use.
**Fix**: Add `Assert.True(IsPttQxTargetInline("PTT-QX-T3"))` to T_BUG_QX_BE_01.

---

### DW-B42-02 — Live NT8 F5 verification of Quick All → BE All sequences

**Priority**: P1 (High)
**Source**: Carried from B42, B43, B44, B46
**Context**: Quick All / BE All interaction sequences can only be verified in a live NT8 session.
Must be confirmed in SIM account before go-live. B47-LaneB T5-B restructured the BE/Quick panels
into new 2-col UniformGrids (_beRowPanel / _quickRowPanel) — live verification should cover the
new layout as well.
**Status After B47**: STILL OPEN — not in B47 scope.
**Deferred to**: Next live F5 session.

---

### DW-B42-03 — IsPttQxTarget range extension for future target slots

**Priority**: P2 (Conditional — low unless T4/T5 slots added)
**Source**: Carried from B42, B43, B44, B46
**Context**: Current range `name[8] >= '1' && name[8] <= '3'` matches B41 two-OCO-group design.
If PTT-QX-T4 or T5 slots added, `IsPttQxTarget` must be updated.
**Status After B47**: STILL OPEN — not in B47 scope.
**Deferred to**: Block that adds 4th+ target slot.

---

### DW-B42-04 — Comment label `NT8-NEW` in PttContracts.cs:254 should be `NT8-005`

**Priority**: P2 (Low — documentation only)
**Source**: Carried from B42, B43, B44, B46
**Context**: In-source comment at PttContracts.cs line 254 uses `NT8-NEW` instead of catalog ID `NT8-005`.
**Status After B47**: STILL OPEN — not in B47 scope.
**Deferred to**: B48+ cleanup pass.
**Fix**: Change `// NT8-NEW` at line 254 to `// NT8-005`.

---

### DW-B42-05 — Live F5 verification of PTTFollowerStrategy ATM bracket spawn

**Priority**: P1 (High)
**Source**: Carried from B42, B43, B44, B46
**Status After B47**: STILL OPEN — not in B47 scope. B46 removed root-cause barriers. B47-LaneB
adds TryAutoApply wiring which further improves the ATM template application path, but live
F5 verification has not been run.
**Reference**: Superseded by DW-B46-01 for tracking purposes.
**Deferred to**: Next live F5 session (see DW-B46-01).

---

### DW-B43-02 — GetLeaderAtmTemplateName visual-tree index accuracy

**Priority**: P2 (downgraded from P1 by B47 — see DW-B47-01)
**Source**: Carried from B43, B44 (as DW-B44-03), B46
**Context**: Component (b) CLOSED by B46 T2 (write-back fix). Component (a) — index accuracy
investigation — remains open. See DW-B47-01 for full status and priority-change rationale.
**Status After B47**: PARTIALLY CLOSED — Component (b) CLOSED by B46. Component (a) carried as
DW-B47-01 with priority downgraded to P2 (mitigation by TryAutoApply wiring in B47-LaneB).
**Deferred to**: B48+

---

### DW-B43-03 — NT8-045 update if AtmStrategyTemplates API becomes accessible

**Priority**: P2 (Low — future proofing)
**Source**: Carried from B43, B44, B46
**Context**: The filesystem fallback (NT8-045) is robust. If a future NT8 update exposes
`AtmStrategyTemplates` API, replace the filesystem approach with direct API call.
**Status After B47**: STILL OPEN — not in B47 scope.
**Deferred to**: Future NT8 upgrade block.

---

### DW-B44-01 — CopyEngineTests.cs 60 pre-existing compile errors block test runner

**Priority**: P1 (High — blocks CI test execution for all B42–B47 test files)
**Source**: First identified in B44; carried B44, B46
**Context**: `CopyEngineTests.cs` has 60 accumulated compile errors from B32–B43.
Prevents `dotnet test` from executing any test in the assembly (including B44Tests.cs,
B45Tests.cs, B46Tests.cs — all individually error-free). B47-LaneB Lane C test file (when
written) will be similarly blocked until this is resolved.
**Status After B47**: STILL OPEN — explicitly excluded from B47 scope per V12.23.
**Deferred to**: Dedicated `CopyEngineTests.cs` cleanup block.

---

### DW-B44-02 — Live F5 verification of Subscribe() panel-only path

**Priority**: P1 (High)
**Source**: First identified in B44; carried B44, B46
**Context**: Subscribe/Unsubscribe fix confirmed structurally (B44) but not verified in a live
NT8 session where TradeCopierPanel is attached to a chart without TradeCopierWindow open.
**Status After B47**: STILL OPEN — not in B47 scope.
**Deferred to**: Before next live trading session.

---

### DW-B46-01 — Live F5 verification: DW-B42-05 re-run after B46 + B47

**Priority**: P1 (High — required before next live trading session)
**Source**: New from B46-LaneA; carried to B47
**Context**: B46 removes root-cause barriers (ATM empty guard T1, write-back fix T2). B47-LaneB
adds `TryAutoApply()` wiring — ATM template changes and follower checkbox toggles now automatically
re-apply the copy rule. Full closure requires a live F5 session to verify D1–D6 acceptance criteria.
**Status After B47**: STILL OPEN — live verification not run in B47-LaneB.
**Deferred to**: Next live F5 session.

---

### DW-B46-02 — dotnet test runner blocked by DW-B44-01

**Priority**: P1 (High — blocks CI verification of B46Tests.cs, and future B47 Lane C tests)
**Source**: New from B46-LaneA; carried to B47
**Context**: `B46Tests.cs` is individually error-free. Test runner is blocked solely because
`CopyEngineTests.cs` (DW-B44-01) fails to compile. B47 Lane C test file will be in the same
situation once written.
**Status After B47**: STILL OPEN — blocked by DW-B44-01. Not in B47 scope.
**Deferred to**: B48+ or DW-B44-01 closure block.

---

## Deferred Item Status Table

| ID | Priority | Block Introduced | Status After B47 | Target |
|----|----------|-----------------|-----------------|--------|
| DW-B42-01 | P2 | B42 | OPEN | B48+ |
| DW-B42-02 | P1 | B42 | OPEN | Next live session |
| DW-B42-03 | P2 | B42 | OPEN | Future (T4/T5 slot block) |
| DW-B42-04 | P2 | B42 | OPEN | B48+ cleanup pass |
| DW-B42-05 | P1 | B42 | OPEN — superseded by DW-B46-01 | Next live session |
| DW-B43-02 | P2 | B43 | PARTIALLY CLOSED (b: CLOSED by B46 T2; a: carried as DW-B47-01, P2) | B48+ |
| DW-B43-03 | P2 | B43 | OPEN | Future NT8 upgrade |
| DW-B44-01 | P1 | B44 | OPEN | Dedicated cleanup block |
| DW-B44-02 | P1 | B44 | OPEN | Before next live session |
| DW-B44-03 | P1 | B44 | PARTIALLY CLOSED — mirrors DW-B43-02; component (a) now DW-B47-01 | B48+ |
| DW-B46-01 | P1 | B46 | OPEN | Next live session |
| DW-B46-02 | P1 | B46 | OPEN (blocked by DW-B44-01) | B48+ or DW-B44-01 closure |
| DW-B47-01 | P2 | B47 | OPEN — visual-tree index accuracy (DW-B43-02 component a; priority downgraded from P1 to P2 by B47 TryAutoApply mitigation) | B48+ |
