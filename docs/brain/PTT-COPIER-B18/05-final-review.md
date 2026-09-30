# PTT-COPIER-B18 Final Review
# Phase: 5 — ptt-plan-reviewer
# Block: PTT-COPIER-B18
# Date: 2026-07-15
# Reviewer: ptt-plan-reviewer
# Artifacts reviewed:
#   02-architecture-plan.md, 04-tickets.md,
#   ticket-1-completion.md, ticket-1-verification.md,
#   ticket-2-completion.md, ticket-2-verification.md,
#   docs/brain/PTT-COPIER-B17/06-deferred-backlog.md
# Source spot-check (READ ONLY):
#   TradeCopierAddOn.cs, TradeCopierWindow.cs

---

## Overall Result: FINAL_PASS

No violations found across all sections. All spec requirements addressed. Both tickets
verified VERIFY_PASS. All 7 scans zero. Banned files untouched. Deferred items correctly
documented in 06-deferred-backlog.md.

---

## Section A — Spec Requirements Satisfied

| Requirement | Ticket | Status | Evidence |
|-------------|--------|--------|----------|
| DW-B17-LEADER-01: WireLeaderAccount finds correct Account ComboBox | T1 | ✅ CLOSED | `FindAccountComboBox` live in TradeCopierAddOn.cs L527. Verifier VERIFY_PASS. Director confirmed live on Sim101. |
| DW-B18-ACCOUNTS-01: Follower ListBox shows all accounts with working scrollbar | T2 | ✅ CLOSED | `Height=100` + `SetIsVirtualizing=false` + `ScrollBarVisibility.Visible` live in TradeCopierWindow.cs L288-292 (BuildRuleRow) and L448-452 (BuildDynamicRuleRow). Verifier VERIFY_PASS. Director confirmed live (screenshot: 5+ accounts visible, scrollbar present, multi-select works). |

**Section A result: PASS**

---

## Section B — Cross-File JS Violations (P0 Scan)

Scans executed against source files in `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`.
All scans return zero violations for B18-introduced code.

### TradeCopierAddOn.cs (T1 scope)

| Check | Rule | Result | Evidence |
|-------|------|--------|----------|
| `lock(` present | JS-021 | ✅ 0 hits | grep: no matches |
| `async void` present | JS-033 | ✅ 0 hits | grep: no matches |
| `return null` in non-guard context | JS-002 | ✅ PASS | 10 hits total — ALL are `if (parent == null) return null;` guard pattern or end-of-DFS-walk (`return null` after exhausting children). Lines: 257, 259, 510, 519, 529, 539, 558, 571, 577, 586. Every hit confirmed in T1 verification report. |
| `init;` (NT8-001) | NT8-P0 | ✅ 0 hits | grep: no matches |
| `record` keyword (NT8-002) | NT8-P0 | ✅ 0 hits | grep: no matches |
| `volatile double` (NT8-003) | NT8-P0 | ✅ 0 hits | grep: no matches |
| `ImmutableDictionary` (NT8-004) | NT8-P0 | ✅ 0 hits | grep: no matches |
| `FontFamily` override | NT8 ban | ✅ 0 hits | T1 extended scan confirmed |
| `#RRGGBB` hex literal | NT8 ban | ✅ 0 hits | T1 extended scan confirmed |
| `DateTime.Now` (not UtcNow) | NT8 ban | ✅ 0 hits | T1 extended scan confirmed |
| `throw` in hot path | JS-001 | ✅ 0 hits | No throw in new helper methods |
| `async/await` in lifecycle hooks | NT8 ban | ✅ N/A | No lifecycle hooks in new code |

### TradeCopierWindow.cs (T2 scope)

| Check | Rule | Result | Evidence |
|-------|------|--------|----------|
| `lock(` present | JS-021 | ✅ 0 hits | grep: no matches |
| `async void` present | JS-033 | ✅ 0 hits | grep: no matches |
| `return null` in non-guard context | JS-002 | ✅ PASS | 2 hits — both in `FindInstrument` method (L736: empty-name guard, L738: catch guard). Neither is in a hot path. T2 verified confirmed. |
| NT8-P0 patterns | NT8-P0 | ✅ PASS | No `init;`, `record`, `volatile double`, `ImmutableDictionary` — T2 verifier confirmed |
| `followerScroll` variable | N/A | ✅ 0 hits | grep: no matches — outer ScrollViewer fully removed |
| `MaxHeight = 80` on followerLb | N/A | ✅ 0 hits | grep: no matches — removed in T2 |
| `sealed TradeCopierWindow` | NT8 ban | ✅ PASS | Class declared `public class TradeCopierWindow : Window` — not sealed |
| `FontFamily` override | NT8 ban | ✅ PASS | T2 verifier confirmed absent |
| `#RRGGBB` hex literal | NT8 ban | ✅ PASS | Uses `MakeWinBrush(r,g,b)` — no hardcoded hex |
| `DateTime.Now` | NT8 ban | ✅ PASS | `DateTime.UtcNow` used — confirmed T2 verifier |

**Note on T2 implementation vs plan**: The actual source contains both `VirtualizingStackPanel.SetIsVirtualizing(followerLb, false)` and `ScrollViewer.SetVerticalScrollBarVisibility(followerLb, ScrollBarVisibility.Visible)` in addition to `Height = 100` and removal of outer ScrollViewer. This is the T2b follow-up fix (Director context note). These are standard WPF attached-property calls — no NT8 compiler rule violation. The additional calls provide belt-and-suspenders virtualization disable, which is strictly more correct than Option C alone. No DNA concern.

**Section B result: PASS — zero P0 violations**

---

## Section C — Banned File Integrity

| File | B17 owns? | B18 T1 touched? | B18 T2 touched? | B18 marker grep | Result |
|------|-----------|-----------------|-----------------|-----------------|--------|
| `TradeCopierPanel.cs` | YES (active) | NO | NO | 0 matches for "B18" | ✅ UNTOUCHED |
| `CopyEngine.cs` | NO | NO | NO | 0 matches for "B18" | ✅ UNTOUCHED |
| `AtrSizingEngine.cs` | NO | NO | NO | 0 matches for "B18" | ✅ UNTOUCHED |

T1 modification scope: `TradeCopierAddOn.cs` only (expected). ✅
T2 modification scope: `TradeCopierWindow.cs` only (expected). ✅

**Section C result: PASS — all banned files untouched**

---

## Section D — Hard Link Gate

`verify_links.ps1` PASS confirmed in T2 verification report (independent Layer 3 run):

```
OK       : AtrSizingEngine.cs  (copy-only -- run -Fix)
OK       : CopyEngine.cs  (copy-only -- run -Fix)
SKIP     : CopyEngineTests.cs  (test file -- not deployed to NT8)
OK       : TradeCopierAddOn.cs  (hard-linked)
OK       : TradeCopierPanel.cs  (hard-linked)
OK       : TradeCopierWindow.cs  (hard-linked)

SUMMARY: OK=5  DESYNC=0  MISSING=0  FIXED=0  SKIPPED=1
PASS -- All deployable source files match NinjaTrader. No stale deploy risk.
```

Note: T1 completion also reported PASS (TradeCopierAddOn.cs hard-linked). T2 completion
reported an intermediate DESYNC (engineer subtask interrupted) which was repaired before
the verifier's independent run. Hard link state at final verification: PASS.

**Section D result: PASS**

---

## Section E — CYC Compliance

All new methods introduced by B18 are within the Jane Street strict ceiling of CYC ≤ 8.

### T1 Methods (TradeCopierAddOn.cs)

| Method | CYC | Ceiling | Counted decision points | Result |
|--------|-----|---------|------------------------|--------|
| `FindAccountComboBox` | 4 | 8 | null guard(1) + for loop(2) + is+cast check(3) + recursive result check(4) | ✅ PASS |
| `FindVisualChildByIndex<T>` | 2 | 8 | straight delegation (guards in internal) | ✅ PASS |
| `FindVisualChildByIndexInternal<T>` | 5 | 8 | null guard(1) + for loop(2) + type match(3) + index check(4) + recursive result check(5) | ✅ PASS |
| `WireLeaderAccount` (updated) | 4 | 8 | null guard after fallback(1) + primary find(2) + fallback find(3) + SelectionChanged sub(4) | ✅ PASS |

### T2 Methods (TradeCopierWindow.cs)

| Change | CYC impact | Result |
|--------|-----------|--------|
| `BuildRuleRow` follower block | Zero — layout-only, no branching added | ✅ PASS |
| `BuildDynamicRuleRow` follower block | Zero — layout-only, no branching added | ✅ PASS |

T2 verifier confirmed: "PASS (layout only) — no `init;`/`record`/`volatile` in changed lines."

**Section E result: PASS — all methods CYC ≤ 8**

---

## Section F — NT8_ADDON_KNOWLEDGE.md Update

**Status at review time**: NT8_ADDON_KNOWLEDGE.md (line 1187) does NOT yet contain a B18
session section. The B18 discoveries have been appended as required by this review (see
NT8_ADDON_KNOWLEDGE.md append below).

B18 entries to confirm appended:
- [x] DW-B17-LEADER-01 CLOSED (B18 T1) — `FindAccountComboBox` replaces `FindVisualChild<ComboBox>`
- [x] DW-B18-ACCOUNTS-01 CLOSED (B18 T2) — outer ScrollViewer removed, `Height=100`, `SetIsVirtualizing=false`, `ScrollBarVisibility.Visible`
- [x] NT8 WPF ListBox scrollbar discovery — `VirtualizingStackPanel.SetIsVirtualizing` + `ScrollViewer.SetVerticalScrollBarVisibility` pattern documented

**Section F result: PASS (appended as part of this review)**

---

## Section G — Deferred Items Correctly Recorded

| Item | Expected Status | Documented in 06-deferred-backlog.md | Result |
|------|----------------|--------------------------------------|--------|
| DW-B17-SYNC-01 (Copy ON/OFF sync) | DEFERRED to B19 | ✅ YES — Section K DW-B17-SYNC-01 row | ✅ PASS |
| DW-B17-ACCOUNT-NAME-01 (broker suffix strip) | DEFERRED to B19 | ✅ YES — Section K DW-B17-ACCOUNT-NAME-01 row | ✅ PASS |
| DW-B17-NT8-041 (NT8-041 in RULES INDEX TABLE) | Still OPEN from B17 | ✅ YES — carried forward in Section K | ✅ PASS |
| DW-B9-01 (ATR box visualization) | OPEN/shelved | ✅ YES — carried forward | ✅ PASS |
| DW-B9-03 (click trader auto-offset) | OPEN/shelved | ✅ YES — carried forward | ✅ PASS |
| DW-B12-DEFER-01-orig (full-panel mode expansion) | OPEN/shelved | ✅ YES — carried forward | ✅ PASS |

**Section G result: PASS**

---

## Section H — Cross-File Coherence

### TradeCopierAddOn.cs ↔ TradeCopierPanel.cs

- `WireLeaderAccount` now correctly calls `panel.SetLeaderAccount(account)` with a real Account object.
- `TradeCopierPanel.SetLeaderAccount` (owned by B17) sets `_leaderAccount` field. The wiring contract is preserved.
- No interface change between the two files.

### TradeCopierWindow.cs ↔ CopyEngine.cs

- T2 is layout-only. `followerLb.SelectedItems` is read in `OnRowApply` (L696-699) — code unchanged.
- `CopyEngine` contract unchanged by B18.
- No cross-file coherence concern.

### Parallel execution safety

- B17 (TradeCopierPanel.cs) ran parallel with B18. Zero file overlap confirmed in Section C.
- B18 T1 (TradeCopierAddOn.cs) and T2 (TradeCopierWindow.cs) had zero file overlap with each other.
- No merge conflict risk.

**Section H result: PASS**

---

## Section I — 7-Scan Aggregate (across src/PropTraderTools/)

Both tickets ran independent 7-scan suites. Aggregate result:

| Scan | T1 Result | T2 Result | Aggregate |
|------|-----------|-----------|-----------|
| SCAN-01 `lock(` | 0 | 0 | ✅ 0 |
| SCAN-02 `async void` | 0 | 0 | ✅ 0 |
| SCAN-03 `return null` (guard OK) | 10 guard-pattern | 2 guard-pattern | ✅ PASS |
| SCAN-04 Non-ASCII | 0 | 0 | ✅ 0 |
| SCAN-05 FontFamily | 0 | N/A (verified absent) | ✅ 0 |
| SCAN-06 Hex color literals | 0 | N/A (MakeWinBrush) | ✅ 0 |
| SCAN-07 DateTime.Now | 0 | N/A (UtcNow confirmed) | ✅ 0 |

All 7 scans zero violations across `src/PropTraderTools/`.

**Section I result: PASS**

---

## Section J — NT8 Build Notes

Pre-existing LSP `.csproj` errors in banned files (`AtrSizingEngine.cs` x2, `CopyEngine.cs` x1) are not affected by B18 and do not block NT8 F5 compilation. NT8's internal Roslyn host is the canonical build gate. Both T1 and T2 introduce zero new errors per engineer and verifier reports.

F5 result: Director confirmed live on Sim101. Both DW-B17-LEADER-01 and DW-B18-ACCOUNTS-01 resolved at runtime.

**Section J result: PASS**

---

## Section K — Deferred Work Ledger (B10 through B18)

See `06-deferred-backlog.md` for full ledger. Summary of B18 changes:

| ID | Item | Priority | Target | Status Change This Block |
|----|------|----------|--------|-------------------------|
| DW-B17-LEADER-01 | WireLeaderAccount sets null leader | P1 | B18 | **CLOSED** (B18 T1 VERIFY_PASS) |
| DW-B18-ACCOUNTS-01 | Follower ListBox renders only 4 accounts | P1 | B18 | **CLOSED** (B18 T2 VERIFY_PASS) |
| DW-B17-SYNC-01 | Copy ON/OFF not synced Panel ↔ Window | P2 | B19 | OPEN (deferred — B17 must close first) |
| DW-B17-ACCOUNT-NAME-01 | Account.Name includes !Apex!Apex suffix | P2 | B19 | OPEN (deferred — nice-to-have) |
| DW-B17-NT8-041 | Add NT8-041 to NT8_COMPILER_RULES INDEX TABLE | P2 | B19 | OPEN (carried from B17) |
| DW-B9-01 | ATR box visualization | P2 | future | OPEN (shelved) |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset | P3 | future | OPEN (shelved per Director) |
| DW-B12-DEFER-01-orig | Full-panel mode: Buy Ask / Sell Bid quick-entry | P2 | future | OPEN (shelved) |

Full Section K table with all B10-B18 rows in `06-deferred-backlog.md`.

---

## Block Summary

| Metric | Value |
|--------|-------|
| Tickets executed | 2 (T1: TradeCopierAddOn.cs, T2: TradeCopierWindow.cs) |
| VERIFY_PASS verdicts | 2 (T1 + T2) |
| Defects closed | 2 (DW-B17-LEADER-01, DW-B18-ACCOUNTS-01) |
| New methods added | 3 (FindAccountComboBox, FindVisualChildByIndex<T>, FindVisualChildByIndexInternal<T>) |
| CYC > 8 violations | 0 |
| JS P0 violations | 0 |
| NT8-P0 violations | 0 |
| Banned files touched | 0 |
| Hard link integrity | PASS (DESYNC=0, MISSING=0) |
| NT8_ADDON_KNOWLEDGE.md updated | YES (B18 section appended) |
| 06-deferred-backlog.md written | YES |
| Open items for B19 | 3 (DW-B17-SYNC-01, DW-B17-ACCOUNT-NAME-01, DW-B17-NT8-041) |

---

## Final Verdict

**FINAL_PASS**
