# PTT-COPIER-B19 Final Review — Lane 1
**Reviewer**: ptt-plan-reviewer (Phase 5)
**Block**: PTT-COPIER-B19
**Lane**: Lane 1 only — Gate 2 account reference fix (DW-B19-COPIER-BUG-01)
**Date**: 2026-07-13
**Verdict**: FINAL_PASS

---

## Inputs Read

| File | Status |
|------|--------|
| `docs/brain/PTT-COPIER-B19/02-architecture-plan.md` | READ — REVIEW_PASS Cycle 2 |
| `docs/brain/PTT-COPIER-B19/04-ticket-review-lane1.md` | READ — TICKET_REVIEW_PASS |
| `docs/brain/PTT-COPIER-B19/ticket-1-completion.md` | READ — BUILD_PASS reported |
| `docs/brain/PTT-COPIER-B19/ticket-1-verification-lane1.md` | READ — VERIFY_PASS |
| `docs/brain/PTT-COPIER-B18/06-deferred-backlog.md` | READ — prior block carry-forwards |
| `docs/standards/jane-street/RULES_CATALOG.md` | READ — active standard |
| `docs/standards/NT8_COMPILER_RULES.md` | READ — B1-B19 confirmed rules |

---

## Check 1 — Coherent System: Does the Fix Fully Address DW-B19-COPIER-BUG-01?

**Finding**: YES — fully addressed.

The root cause was reference equality (`e.Order.Account == rule.MasterAccount`) at
[`CopyEngine.cs:381`](c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs:381).
After a Rithmic reconnect, NT8 recreates `Account` objects; the stored reference in
`_rules` becomes stale, causing Gate 2 to return `false` for every order and zero
follower orders to be generated.

The fix at line 381:
```csharp
// BEFORE
if (e.Order.Instrument.FullName == rule.Instrument && e.Order.Account == rule.MasterAccount)

// AFTER
if (e.Order.Instrument.FullName == rule.Instrument && e.Order.Account.Name == rule.MasterAccount?.Name)
```

`Account.Name` is a stable `string` property that survives reconnect. The null-conditional
`?.Name` correctly handles the `(Account)null` master case exercised by 5+ existing tests,
evaluating to `null` (no NRE, no false match) rather than throwing.

**Live scan verification (ptt-plan-reviewer independent)**:
- `e.Order.Account ==` pattern: **0 results** — old reference equality gone ✅
- `Account.Name ==` pattern: **1 result at line 381** — new name equality present ✅

The single-line fix is minimal, correct, and complete for the P0 bug.

---

## Check 2 — Cross-File JS Violations

**Independent scan results (ptt-plan-reviewer)**:

| Scan | Pattern | Result | Rule |
|------|---------|--------|------|
| `lock()` in CopyEngine.cs | `^\s+lock\s*\(` | **0 results** ✅ | JS-021 |
| `async void` in CopyEngine.cs | `^\s+async void ` | **0 results** ✅ | JS-033 |
| `return null` (new methods) | No new methods introduced | **N/A** ✅ | JS-002 |
| `throw` in hot path (new code) | `?.Name` is null-safe; no exception path | **0 new paths** ✅ | JS-001 |
| CYC > 8 on `OnOrderUpdate` | Fix changes comparison type only — branch count unchanged. CYC remains 7 | **PASS** ✅ | CYC ≤ 8 |

No P0 or P1 JS violations were introduced across changed files (`CopyEngine.cs`,
`CopyEngineTests.cs`). Out-of-scope files (`TradeCopierPanel.cs`, `TradeCopierWindow.cs`,
`TradeCopierAddOn.cs`, `AtrSizingEngine.cs`) are confirmed NOT modified by B19 (Layer 3
git diff check in ticket-1-verification-lane1.md, section "Out-of-Scope File Modification Check").

---

## Check 3 — Missing Wiring / NT8 Event Subscription Issues

**Finding**: None introduced.

`OnOrderUpdate` is an NT8-overridden event callback — it does not use manual subscribe/
unsubscribe wiring. The fix modifies only the Gate 2 condition predicate within the
existing `foreach` loop body. No new event subscriptions, no new delegates, no new
lifecycle hooks were introduced. No NT8 event lifecycle issues.

NT8 constraint checks from Layer 3 verification:

| Constraint | Status |
|-----------|--------|
| `Account.Name` is valid `string` | Confirmed by 10+ prior uses (lines 456, 514, 589, 820, 843, 881, 925, 967, 997, 1068) ✅ |
| `?.` null-conditional in .NET 4.8 | Valid C# 6+ / .NET 4.8 ✅ |
| NT8-001 (`init;` ban) | No new properties ✅ |
| NT8-002 (`record` ban) | No new record types ✅ |
| NT8-003 (`volatile double` ban) | No volatile fields ✅ |
| NT8-004 (`ImmutableDictionary` ban) | No immutable collections ✅ |
| NT8-032 (`dotnet test` blocker) | Documented; source scans are verification contract ✅ |
| FontFamily= ban | No WPF changes ✅ |
| Hex color (#RRGGBB) ban | No WPF changes ✅ |
| `sealed` on TradeCopierWindow ban | No Window changes ✅ |

---

## Check 4 — Spec Satisfaction Table

| Requirement | Description | Addressed? | Evidence |
|-------------|-------------|------------|---------|
| REQ-B19-01 | Gate 2 uses `Account.Name` string equality (not reference equality) | ✅ YES | `CopyEngine.cs:381` — `e.Order.Account.Name == rule.MasterAccount?.Name`; SCAN-02 Layer 2 + Layer 3 both confirm 1 result |
| REQ-B19-02 | Two new `[Fact]` tests covering the fix | ✅ YES | `Gate2_UsesAccountName_SourceContractVerified` at CopyEngineTests.cs:1901; `Gate2_NullMasterAccount_NoCopyOrder` at CopyEngineTests.cs:1931; confirmed by SCAN-06 Layer 2 + Layer 3 both independently |
| REQ-B19-03 | No regression to prior tests | ✅ YES | All prior 111 tests intact; no deletions; net delta +2 confirmed by git diff (Layer 3); SCAN-07 confirms 112/113 real [Fact] attributes (methodology note: 1 is in a comment — not a missing test) |
| REQ-B19-04 | Zero `lock()` in CopyEngine.cs | ✅ YES | SCAN-03 Layer 2 PASS + Layer 3 independently 0 results; confirmed again by ptt-plan-reviewer scan above |

**All 4 spec requirements: SATISFIED.**

---

## Check 5 — 7 Scans Zero (Layer 2 and Layer 3 Cross-Check)

| Scan ID | Description | Layer 2 | Layer 3 | ptt-plan-reviewer | Assessment |
|---------|-------------|---------|---------|-------------------|------------|
| SCAN-01 | Old reference equality gone (`e.Order.Account ==`) | 0 results | 0 results | 0 results | ✅ ZERO |
| SCAN-02 | New name equality present (`Account.Name ==`) | 1 result (line 381) | 1 result (line 381) | 1 result (line 381) | ✅ CONTRACT MET |
| SCAN-03 | No `lock()` | PASS | 0 results | 0 results | ✅ ZERO |
| SCAN-04 | No `async void` | PASS | 0 results | 0 results | ✅ ZERO |
| SCAN-05 | Build | NT8-032 documented; 0 new errors from B19 | NT8-032 confirmed; 3 pre-existing errors only | N/A (F5 gate authoritative) | ✅ ZERO NEW ERRORS |
| SCAN-06 | Gate2 tests present | Both at :1901, :1931 | Both at :1901, :1931 | Both confirmed | ✅ CONTRACT MET |
| SCAN-07 | `[Fact]` count | 113 (unanchored) | 112 (anchored) / 113 (unanchored) | Both Gate2 tests present | ✅ NO DEFICIT |

**SCAN-07 Methodology Note**: Layer 2 and Layer 3 differ by 1 on the raw `[Fact]` count.
Layer 3 identified the single discrepancy: line 1748 contains a code comment
`// B16 T2 -- 10 [Fact] tests --` which the unanchored pattern matches. This is a pre-existing
comment predating B19. The 2 new Gate2 tests are confirmed present by both layers.
This is a scan-methodology artifact, **not a missing test**. Not a failing condition.

**Result**: All 7 scans zero (or contract-met for SCAN-02, SCAN-06, SCAN-07). Three independent
layers agree. Zero violations.

---

## Check 6 — Coherence Across CopyEngine + TradeCopierPanel + TradeCopierWindow

Lane 1 scope is strictly `CopyEngine.cs` (1 line) and `CopyEngineTests.cs` (2 tests).
`TradeCopierPanel.cs` and `TradeCopierWindow.cs` are **not modified** in B19 Lane 1.
No cross-file wiring between the fix and Panel/Window is required: Gate 2 operates
entirely within `CopyEngine.OnOrderUpdate` — the Panel and Window only call
`SetEnabled()` / `AddRule()`, which are unaffected by the Gate 2 condition change.

System coherence for Lane 1 scope: ✅ COMPLETE.

---

## Section K — Deferred Work Ledger (B19 Append)

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B9-01 | ATR box visualization on chart canvas | P2 | future | OPEN |
| DW-B9-03 | Click trader Bid+1/Ask-1 auto-offset (unblocked, shelved per Director) | P3 | future | OPEN |
| DW-B12-DEFER-01 (orig) | Full-panel mode expansion: Buy Ask / Sell Bid quick-entry buttons | P2 | future | OPEN |
| DW-B17-NT8-041 | Add NT8-041 (ChartControl.Charts NOT FOUND) to NT8_COMPILER_RULES.md INDEX TABLE | P2 | B20 | OPEN |
| DW-B17-SYNC-01 | Copy ON/OFF not synced Panel <-> Window via CopyEngine event | P2 | B20 | OPEN |
| DW-B17-ACCOUNT-NAME-01 | Strip !Apex!Apex broker suffix at display layer (TradeCopierWindow.cs) | P2 | B20 | OPEN |
| DW-B19-02 | `PopulateOrderMap` dedup guard (CopyEngine.cs:659) uses reference equality — duplicate FollowerBindings after reconnect | P2 | B20+ | OPEN |

---

## Final Verdict

```
FINAL_PASS
```

| Check | Outcome |
|-------|---------|
| 1 — DW-B19-COPIER-BUG-01 fully addressed | ✅ PASS |
| 2 — Zero cross-file JS P0 violations | ✅ PASS |
| 3 — Zero NT8 wiring issues introduced | ✅ PASS |
| 4 — All 4 spec requirements satisfied | ✅ PASS |
| 5 — 7 scans zero (3-layer agreement) | ✅ PASS |
| 6 — System coherence (CopyEngine + Panel + Window) | ✅ PASS |
| Section K present | ✅ PRESENT |
| 06-deferred-backlog-lane1.md written | ✅ REQUIRED (written separately) |
