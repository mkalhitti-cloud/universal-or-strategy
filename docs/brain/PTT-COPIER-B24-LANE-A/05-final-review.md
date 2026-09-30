# PTT-COPIER-B24-LANE-A — Final Review
# Phase: 5 (Final Review)
# Reviewer: ptt-plan-reviewer
# Block: PTT-COPIER-B24
# Lane: A
# Defect: DW-B24-LEADER-CASTNULL-01
# Date: 2026-07-17

---

## Inputs Read

| # | File | Status |
|---|------|--------|
| 1 | `docs/brain/PTT-COPIER-B24-LANE-A/02-architecture-plan.md` | READ |
| 2 | `docs/brain/PTT-COPIER-B24-LANE-A/04-ticket-review.md` | READ |
| 3 | `docs/brain/PTT-COPIER-B24-LANE-A/ticket-1-completion.md` | READ |
| 4 | `docs/brain/PTT-COPIER-B24-LANE-A/ticket-1-verification.md` | READ |
| 5 | `docs/brain/PTT-COPIER-B23-LANE-C/06-deferred-backlog.md` | READ (prior OPEN items) |
| 6 | `docs/standards/jane-street/RULES_CATALOG.md` | READ |
| 7 | `docs/standards/NT8_COMPILER_RULES.md` | READ |

Live source file independently read:
- `C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierAddOn.cs` — lines 341–470 verified

---

## Section A — Defect Closure: DW-B24-LEADER-CASTNULL-01

**Question: Does the fix correctly solve DW-B24-LEADER-CASTNULL-01?**

**Result: YES — CLOSED ✅**

The architecture plan correctly diagnosed the root cause: at cold NT8 inject time, the WPF
`ComboBox.SelectedItem` is a framework placeholder (not yet a materialised
`NinjaTrader.Cbi.Account`), so the direct cast `as NinjaTrader.Cbi.Account` silently returns
`null`. `ComboBox.Text` already holds the display name (e.g. `"Sim101"`) and is the recovery
path.

The fix (lines 456–459 in the live source, confirmed independently) inserts the text-fallback
block in the exact correct structural position:

```
Line 455: var current = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;  ← cast
Line 456: if (current == null && accountCombo.Text != null)                    ← fallback guard
Line 457:     current = Account.All.FirstOrDefault(                            ← name lookup
Line 458:         a => string.Equals(a.Name, accountCombo.Text,
Line 459:                            StringComparison.OrdinalIgnoreCase));
Line 460: if (current != null) panel.SetLeaderAccount(current);                ← call preserved
```

The fallback:
- Is between the cast and `SetLeaderAccount` — correct order
- Uses `StringComparison.OrdinalIgnoreCase` — case-insensitive as mandated by plan invariant #1
- Calls `Account.All.FirstOrDefault` once at inject time — not in a loop/timer (plan invariant #2)
- Does NOT touch the `SelectionChanged` subscription — unchanged (plan invariant #3)
- Does NOT introduce `SetLeaderAccount(null)` as a new path — null still falls through (plan invariant #4)

`WireLeaderAccount` is called from `DoInject` at line 398, which is invoked via
`chart.Dispatcher.InvokeAsync(() => DoInject(chart))` — correct NT8-021 (Account.All safe lifecycle).

**DW-B24-LEADER-CASTNULL-01: CLOSED.**

---

## Section B — Cross-File New P0 Violations

**Question: Does TradeCopierAddOn.cs contain any new P0 violations introduced by this edit?**

**Result: NO NEW P0 VIOLATIONS ✅**

Independent scan results from live source:

| Scan | Pattern | Result | Rule |
|------|---------|--------|------|
| SCAN-01 | `lock\(` | 0 matches file-wide | JS-021 PASS |
| SCAN-02 | `async void ` | 0 matches file-wide | JS-033 PASS |
| SCAN-03 | `return null` in `WireLeaderAccount` (lines 443–469) | 0 matches in method | JS-002 PASS |
| SCAN-04 | `DateTime\.Now` | 0 matches file-wide | NT8-013 PASS |
| SCAN-05 | `volatile double` | 0 matches file-wide | NT8-003 PASS |
| SCAN-06 | CYC for `WireLeaderAccount` | CYC = 6 (manual) | Jane Street ≤ 8 PASS |
| SCAN-07 | `OrdinalIgnoreCase` | 1 match at line 459 | Mandate PASS |

Pre-existing items confirmed NOT introduced by T1 (Layer 3 verifier confirmed independently):
- `return null` at lines 474, 483, 493, 503, 522, 535, 541, 550 — all in visual tree helpers
  (`FindVisualChild<T>`, `FindVisualChildByIndex<T>`, `FindAccountComboBox`) outside
  `WireLeaderAccount` scope.
- `System.Windows.Application.Current.Dispatcher.InvokeAsync` at lines 251 and 293 — pre-existing
  in other methods (`SendCopyAsync`, `UpdateAtrOverlay`); NOT introduced by T1.
  (Advisory: NT8-042 concern — see Section K / DW-B24-LANE-A-01.)

Git diff confirms: only `TradeCopierAddOn.cs` was modified by T1, 5 insertions + 1 deletion,
all within `WireLeaderAccount`. No other files in write-set.

---

## Section C — Wiring Correctness

**Question: Is `SetLeaderAccount()` correctly called from both the text-fallback path and `SelectionChanged`?**

**Result: YES — CORRECTLY WIRED ✅**

**Text-fallback path (cold-start injection)**:
- Line 455: direct cast attempted
- Lines 456–459: text-fallback via `Account.All.FirstOrDefault`
- Line 460: `if (current != null) panel.SetLeaderAccount(current)` — called when fallback succeeds

**`SelectionChanged` subscription (future account switches)**:
- Lines 463–467: `accountCombo.SelectionChanged += (s, e) => { ... panel.SetLeaderAccount(acc); };`
- Subscription is unchanged from pre-B24 baseline (plan invariant #3, verified by Layer 3 IC-5)

Both paths are wired. The spec requirement (auto-detect account without user touch on cold start)
is satisfied by the text-fallback path. Future account switches remain handled by `SelectionChanged`.

---

## Section D — Spec Coverage

**Spec requirement**: The panel must auto-detect the account without user touch (cold-start wiring).

| Requirement | Ticket | Satisfied? | Evidence |
|-------------|--------|-----------|---------|
| Auto-detect leader account on cold start (no dropdown touch) | T1 | ✅ YES | Text-fallback via `Account.All.FirstOrDefault` wires account when `SelectedItem` cast returns null; `SetLeaderAccount` called at lines 460; status bar shows "Ready: MES SEP26" (not "No leader") per verification contract |

---

## Section E — 7-Scan Zero Confirmation

All Layer 3 (independent verifier) scans passed. All Layer 2 / Layer 3 results matched.

| Scan | Rule | L3 Result | Match L2? | Gate |
|------|------|-----------|----------|------|
| SCAN-01 | JS-021 — lock() | 0 matches | ✅ MATCH | ✅ PASS |
| SCAN-02 | JS-033 — async void | 0 matches | ✅ MATCH | ✅ PASS |
| SCAN-03 | JS-002 — return null in WireLeaderAccount | 0 in-scope | ✅ MATCH | ✅ PASS |
| SCAN-04 | NT8-013 — DateTime.Now | 0 matches | ✅ MATCH | ✅ PASS |
| SCAN-05 | NT8-003 — volatile double | 0 matches | ✅ MATCH | ✅ PASS |
| SCAN-06 | Jane Street CYC ≤ 8 | CYC = 6 | ✅ MATCH | ✅ PASS |
| SCAN-07 | OrdinalIgnoreCase mandate | 1 match (line 459) | ✅ MATCH | ✅ PASS |

**All 7 scans: ZERO violations / mandate satisfied. ✅**

---

## Section F — JS DNA Rule Check

| Rule | Applied to Introduced Code | Result |
|------|--------------------------|--------|
| JS-021 (P0) — no `lock()` | `Account.All` is read-only; no lock introduced | ✅ PASS |
| JS-002 (P0) — no `return null` | Method is `void`; no return value path | ✅ PASS |
| JS-001 (P0) — no `throw` in hot paths | No exception throwing in fix | ✅ PASS |
| JS-033 (P0) — no `async void` | Fix is synchronous void | ✅ PASS |
| ASCII-Only | All identifiers and string.Equals arguments are ASCII | ✅ PASS |

---

## Section G — NT8 Rule Check

| Rule | Applied to Introduced Code | Result |
|------|--------------------------|--------|
| NT8-042 (P0) — no new `Dispatcher.InvokeAsync` | Fix introduces none; pre-existing at 251/293 NOT touched | ✅ PASS |
| NT8-021 (P1) — `Account.All` not in constructors | Call site is `DoInject` → `WireLeaderAccount` lifecycle path | ✅ PASS |
| NT8-006 (P1) — `using System.Linq` required | Confirmed present at line 18 | ✅ PASS |
| NT8-013 (P0) — no `DateTime.Now` | 0 matches file-wide (SCAN-04) | ✅ PASS |
| NT8-003 (P0) — no `volatile double` | 0 matches file-wide (SCAN-05) | ✅ PASS |
| NT8-018 (P1) — no `lock()` | 0 matches file-wide (SCAN-01) | ✅ PASS |
| NT8-001 (P0) — no `{ get; init; }` | No properties modified | ✅ PASS |

---

## Section H — CYC Compliance

| Metric | Before Fix | After Fix | Ceiling |
|--------|-----------|-----------|---------|
| CYC (`WireLeaderAccount`) | 4 | 6 | 8 |
| Status | PASS | **PASS** | — |

CYC 6 ≤ 8 (Jane Street strict ceiling). No decomposition required.

---

## Section I — [Fact] Delta

**Delta: 0** — count unchanged at 126. Rationale accepted:
`WireLeaderAccount` requires live NT8 WPF visual tree, live `ComboBox.SelectedItem`/`Text`,
and `Account.All` populated by NT8 runtime — none available in the `CopyEngineTests` stub
harness. The verification contract is the manual cold-start gate (5 steps in ticket-1-verification.md).

---

## Section J — Pipeline Coherence

| Stage | Result |
|-------|--------|
| Phase 1 (ptt-architect) — 02-architecture-plan.md | REVIEW_PASS (cycle 2) |
| Phase 2 (ptt-plan-reviewer) — 02-plan-review.md | REVIEW_PASS |
| Phase 3.5 (ptt-ticket-reviewer) — 04-ticket-review.md | TICKET_REVIEW_PASS (cycle 2, all 10 checks PASS) |
| Phase 4a (ptt-engineer) — ticket-1-completion.md | BUILD_PASS (all 7 scans PASS) |
| Phase 4b (ptt-verifier) — ticket-1-verification.md | VERIFY_PASS (all 7 L3 scans PASS, all 9 IC checks PASS) |
| Phase 5 (this review) | FINAL_PASS (see verdict below) |

The CopyEngine + TradeCopierPanel + TradeCopierAddOn form a coherent system:
- `TradeCopierAddOn.WireLeaderAccount` (T1 fix) → `TradeCopierPanel.SetLeaderAccount` (unchanged)
- `SelectionChanged` subscription (unchanged) → `TradeCopierPanel.SetLeaderAccount` on future switches
- No cross-file pollution: write-set is strictly `TradeCopierAddOn.cs` only

---

## Section K — Deferred Work (REQUIRED)

Items that belong in [`06-deferred-backlog.md`](06-deferred-backlog.md):

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B23-LANE-C-01 | Add short-direction `[Fact]` test for `PendingBe_Armed_FiresAtPriceTarget_Short` — long direction covered; short direction (`last <= target`) untested | P2 | B24 or future | OPEN |
| DW-B23-LANE-C-02 | Pre-existing `return null` at `CopyEngine.cs` lines 653, 1059, 1065, 1118 — JS-002 compliance sweep candidates | P2 | future | OPEN |
| DW-B24-LANE-A-01 | Pre-existing `System.Windows.Application.Current.Dispatcher.InvokeAsync` calls at `TradeCopierAddOn.cs` lines 251 and 293 — NT8-042 advisory from plan-review. Not introduced by B24-LANE-A; outside T1 write-set. Pending research: `Dispatcher.BeginInvoke` alternative per NT8-042 SAFE section. | P1 | B25 or future | OPEN |
| DW-B24-LEADER-CASTNULL-01 | Cold-start leader account wiring via text-fallback `Account.All.FirstOrDefault` | P0 | B24 (this block) | **CLOSED** |

**Notes**:
- `DW-B23-LANE-C-01` and `DW-B23-LANE-C-02` are unchanged from the prior backlog — neither is in this lane's scope.
- `DW-B24-LANE-A-01` is a new P1 advisory entry. The two `Dispatcher.InvokeAsync` calls at lines 251/293 were flagged by the plan-review advisory (ADV-01). They compiled and passed VERIFY_PASS because NT8-042 does not cause a build error in all contexts — but the calls use the banned `System.Windows.Application.Current.Dispatcher.InvokeAsync` path which is listed as banned in NT8-042.
- `DW-B24-LEADER-CASTNULL-01` is **CLOSED** following VERIFY_PASS.

---

## Verdict

| Check | Result |
|-------|--------|
| 1. DW-B24-LEADER-CASTNULL-01 correctly solved | ✅ PASS |
| 2. No new P0 violations in TradeCopierAddOn.cs | ✅ PASS |
| 3. SetLeaderAccount() wired from both text-fallback and SelectionChanged | ✅ PASS |
| 4. Spec requirement (auto-detect account, no user touch) satisfied | ✅ PASS |
| 5. All 7 Layer-3 scans zero/satisfied | ✅ PASS |
| 6. Section K present with all required entries | ✅ PASS |
| 7. 06-deferred-backlog.md written | ✅ PASS |
| 8. DW-B24-LEADER-CASTNULL-01 marked CLOSED | ✅ PASS |

---

# ✅ FINAL_PASS
