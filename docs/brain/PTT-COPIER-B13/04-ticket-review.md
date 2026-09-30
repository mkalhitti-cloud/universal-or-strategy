# PTT-COPIER-B13 Ticket Review -- Cycle 2

**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-07-12
**Cycle**: 2 (re-review after TRACE-T3-FILE resolution)
**Tickets**: docs/brain/PTT-COPIER-B13/04-tickets.md (unchanged from Cycle 1)
**Plan**: docs/brain/PTT-COPIER-B13/02-architecture-plan.md (REVIEW_PASS R3 -- §5.2 corrected)
**Backlog**: docs/brain/PTT-COPIER-B12/06-deferred-backlog.md
**Prior review**: docs/brain/PTT-COPIER-B13/04-ticket-review.md (Cycle 1 -- TICKET_REVIEW_FAIL: TRACE-T3-FILE)

---

## TRACE-T3-FILE Resolution (Cycle 2 primary check)

Cycle 1 blocking violation was: plan §5.2 named `AtrSizingEngine.cs` while ticket T3 Change 1
named `TradeCopierPanel.cs`. Resolution required one of: (A) correct the plan §5.2, (B) correct
the ticket, or (C) add both files to scope.

**Resolution taken: Option A -- plan §5.2 corrected (R3).**

Three-source agreement check:

| Source | File named for T3 comment change |
|--------|----------------------------------|
| Plan §5.2 (R3) | `src/PropTraderTools/TradeCopierPanel.cs` line 802 |
| Ticket T3 Change 1 | `src/PropTraderTools/TradeCopierPanel.cs` line 802 |
| B12 backlog DW-B12-DEFER-03 | "TradeCopierPanel.cs inline code comments" |

All three sources now agree. TRACE-T3-FILE is **RESOLVED**.

Live grep evidence (confirmed by orchestrator): `TradeCopierPanel.cs:802` contains
`// NT8-003: no Math.Clamp (banned in .NET 4.8). Math.Max/Min used instead.`
`AtrSizingEngine.cs` does NOT contain the misattributed comment. Plan §5.2 R3 explicitly
states: `"AtrSizingEngine.cs is READ ONLY for B13 (no changes to that file)."` This is
consistent with the ticket and backlog.

**Residual §2 component map stale entry (non-blocking):**
Plan §2 still lists `AtrSizingEngine | T3: add 1-line attribution comment only` -- this
entry was not updated in R3. However §5.2 R3 (the authoritative change description) explicitly
contradicts it, and the live grep evidence is definitive. The engineer's ground truth is §5.2
(explicit file + line) and the ticket (explicit file + line); the §2 stale entry creates no
actionable ambiguity. Noted as OBS-05 below.

---

## Per-Ticket Results

### T1 -- DW-B12-DEFER-01 (Wire GetRefPrice to MarketData.Last.Price)

**Traceability**: PASS
- DW-B12-DEFER-01 cited as spec req; confirmed in B12 backlog §K as OPEN entering B13.
- Implementation scope (replace `GetRefPrice()` stub with triple null guard + `last.Price`)
  matches plan §3 exactly.
- No phantom scope. Callers unchanged as prescribed by plan §3.4.
- File: `src/PropTraderTools/TradeCopierPanel.cs` (Wave workspace) -- matches plan §1 scope
  table and §2 component map.

**JS Pre-Check**: PASS
- JS-021 (no `lock()`): No `lock()` in the implementation. PASS.
- JS-033 (no `async void`): Method is `private double`. No async. PASS.
- JS-001 (no exception throws in hot path): Null guards return `0.0`; no `throw`. PASS.
- JS-002 (no `return null`): Returns `double` value type; `0.0` is not null. PASS.

**CYC Pre-Check**: PASS
- `GetRefPrice()` CYC = 4 (3 null-guard branches + 1 normal return). Stated in ticket and
  matches plan §3. CYC <= 8. PASS.

**NT8 Check**: PASS
- NT8-032 (`MarketData.Last` is `MarketDataEventArgs`; use `.Price`): `last.Price` used. PASS.
- NT8-027 (synchronous snapshot read): confirmed safe in plan §3.1. PASS.
- NT8-033 (no `BarsArray` from AddOn context): Not used. PASS.
- NT8-003 (no `volatile double`): `GetRefPrice()` returns a `double`; no new volatile field. PASS.
- NT8-001 (no `{ get; init; }`): No new properties. PASS.

**Test Coverage**: PASS (private method exemption documented)
- `GetRefPrice()` is `private` and depends on `NinjaTrader.Cbi.Instrument` (NT8 runtime).
- roleDefinition requires `[Fact]` for public or internal methods. `GetRefPrice()` is private.
- Exemption is explicitly documented in ticket §xUnit Test section with compensating
  Sim101 gate DW-B13-SIM-T1-01. PASS.

**Scan Checklist**: PASS (7 scans present)
- SCAN 1 through SCAN 7 present with concrete grep/build/test commands. All 7 present. PASS.
- (Non-blocking note: label format is "SCAN 1"..."SCAN 7" rather than "SCAN-01"..."SCAN-07";
  substance is complete -- see OBS-01.)

**File Routing**: PASS
- `src/PropTraderTools/TradeCopierPanel.cs` is Wave workspace (`c:\WSGTA\universal-or-strategy`). PASS.

**VERDICT**: TICKET_REVIEW_PASS

---

### T2 -- DW-B12-DEFER-02 (ATR Fraction Spinner Startup Sync)

**Traceability**: PASS
- DW-B12-DEFER-02 cited as spec req; confirmed in B12 backlog §K as OPEN entering B13.
- Scope (append `NotifyRiskChanged()` + `NotifyAtrFractionChanged()` to `OnLoaded()`)
  matches plan §4 exactly.
- Test method `UpdateAtrFraction_ForwardsToEngine_WhenEngineSet` matches plan §4.4 exactly
  (name, arrange/act/assert pattern, `Assert.Equal(5, qty)` assertion).
- No phantom scope. Supporting methods READ ONLY as prescribed by plan §2 component map.
- Files: `src/PropTraderTools/TradeCopierPanel.cs` + `src/PropTraderTools/CopyEngineTests.cs`
  (Wave workspace) -- match plan §2. PASS.

**JS Pre-Check**: PASS
- JS-021 (no `lock()`): No `lock()` in `OnLoaded` append or test. PASS.
- JS-033 (no `async void`): `OnLoaded` is `private void`; no async keyword. PASS.
- JS-001 (no exception throws): No throws in appended lines or test body. PASS.
- JS-002 (no `return null`): `NotifyRiskChanged` and `NotifyAtrFractionChanged` are void. PASS.

**CYC Pre-Check**: PASS
- `OnLoaded()` CYC unchanged (two straight-line calls, no new branches). PASS.
- New `[Fact]` test CYC = 1 (linear, no branches). PASS.
- All CYC <= 8. PASS.

**NT8 Check**: PASS
- NT8-003 (no `volatile double`): `_atrFraction` and `_maxRiskDollars` are plain `double`,
  UI-thread-only. No new volatile fields. PASS.
- NT8-018 (no `lock()`): No lock in call chain. PASS.
- NT8-019 (no `async void`): `OnLoaded` is synchronous `private void`. PASS.
- NT8-001 (no `{ get; init; }`): No new properties. PASS.

**Test Coverage**: PASS
- `OnLoaded` append verified via `[Fact]` `UpdateAtrFraction_ForwardsToEngine_WhenEngineSet`.
- `Assert.Equal(5, qty)` -- concrete, non-vacuous assertion. PASS.
- Naming convention: `Method_Condition_ExpectedResult`. PASS.
- xUnit `[Fact]`. No NUnit/MSTest. PASS.
- Teardown: `CopyEngine.Instance.SetAtrEngine(null, enabled: false)`. PASS.
- SCAN 7 explicitly names the test. PASS.

**Scan Checklist**: PASS (7 scans present)
- SCAN 1 through SCAN 7 present with valid grep/build/test commands. All 7 present. PASS.
- SCAN 7 names `UpdateAtrFraction_ForwardsToEngine_WhenEngineSet` explicitly. PASS.
- (Non-blocking format note: see OBS-01.)

**File Routing**: PASS
- `src/PropTraderTools/TradeCopierPanel.cs` and `src/PropTraderTools/CopyEngineTests.cs`
  are both Wave workspace (`c:\WSGTA\universal-or-strategy`). PASS.

**VERDICT**: TICKET_REVIEW_PASS

---

### T3 -- DW-B12-DEFER-03 (Docs and Comment Fix -- NT8-031)

**Traceability**: PASS (TRACE-T3-FILE RESOLVED)
- DW-B12-DEFER-03 cited as spec req; confirmed in B12 backlog §K as OPEN entering B13.
- Plan §5.2 R3 now names `src/PropTraderTools/TradeCopierPanel.cs` line 802 as the
  comment change target. Ticket T3 Change 1 names the same file and line. Backlog
  DW-B12-DEFER-03 names "TradeCopierPanel.cs inline code comments". All three agree.
- TRACE-T3-FILE is RESOLVED. Traceability: PASS.
- Change 2 (NT8_COMPILER_RULES.md docs update) matches plan §5.3 exactly. PASS.
- No phantom scope. No logic change. No new method. PASS.

**JS Pre-Check**: PASS
- JS-021, JS-033, JS-001, JS-002: Not applicable. Comment-only + docs change. No executable
  code introduced. PASS.

**CYC Pre-Check**: PASS (N/A)
- No logic changes. CYC unchanged. PASS.

**NT8 Check**: PASS
- NT8-003: Comment corrects attribution FROM NT8-003 TO NT8-031. No new volatile double
  introduced; existing code logic is unchanged. PASS.
- NT8-031: New rule entry is correctly defined (Math.Clamp absent = .NET 4.8 version
  constraint, not the NT8-003 volatile ban). PASS.
- NT8-001: No new properties. PASS.

**Test Coverage**: PASS (N/A)
- Docs+comment-only change. No xUnit test required. Verification is by code review.
- ptt-verifier will confirm line 802 reads `NT8-031` and `NT8_COMPILER_RULES.md` contains
  `## NT8-031` with correct BANNED/SAFE entries. PASS.

**Scan Checklist**: PASS (7 scans present)
- SCAN 1 through SCAN 7 present with valid grep/build/test commands. All 7 present. PASS.
- SCAN 1-4 note for comment-only change is explicitly documented in ticket. PASS.
- (Non-blocking format note: see OBS-01.)

**File Routing**: PASS
- `src/PropTraderTools/TradeCopierPanel.cs` -- Wave workspace. PASS for `.cs` file.
- `docs/standards/NT8_COMPILER_RULES.md` -- Director workspace. This is a `.md` docs file,
  not a `.cs` source file. The file-routing rule applies to C# source paths only. PASS.

**Acceptance Criteria note (OBS-03 carried from Cycle 1):**
Acceptance Criteria item 1 contains an inline correction note
("CORRECTION: do NOT displace the `// JS-021` comment") that was written then retracted.
The final guidance (single-line replacement of line 802 only) is unambiguous and correct.
This is a cosmetic OBS only -- does not affect correctness.

**VERDICT**: TICKET_REVIEW_PASS

---

## Violations

**No blocking violations in Cycle 2.** TRACE-T3-FILE is resolved.

---

## Non-Blocking Observations (do not cause FAIL; noted for architect awareness)

| # | Check | Observation | Ticket(s) |
|---|-------|-------------|-----------|
| OBS-01 | Scan label format | Tickets label scans "SCAN 1"..."SCAN 7" (space-separated, no zero-padding). Pipeline convention is "SCAN-01"..."SCAN-07". Cosmetic only; all 7 scans are present with valid commands. | T1, T2, T3 |
| OBS-02 | Scan content vs plan §10 | Ticket SCAN 4 = `volatile double` grep; plan §10 SCAN-04 = `DateTime.Now`. Ticket SCAN 5 = `complexity_audit.py`; plan §10 SCAN-05 = `CreateOrder PTT-` prefix. Ticket SCAN 6 = `dotnet build`; plan §10 SCAN-06 = hex color. Ticket SCAN 7 = `dotnet test`; plan §10 SCAN-07 = volatile cross-thread. Ticket scans are operationally correct and more actionable for B13's scope. Not a FAIL. Architect should add a revision note in §10 acknowledging the ticket scans supersede plan §10. | T1, T2, T3 |
| OBS-03 | T3 Acceptance Criteria inline correction | Item 1 contains "CORRECTION: do NOT displace the `// JS-021` comment" -- a mid-write retraction. Final guidance (single-line replacement of line 802 only) is correct. Clean up in next revision. | T3 |
| OBS-04 | Carried from Cycle 1 -- resolved context | Cycle 1 OBS-04 (before/after block comment discrepancy between plan and ticket suggesting different file targets) is resolved by the R3 correction. No further action needed. | T3 |
| OBS-05 | Plan §2 component map stale entry | Plan §2 still lists `AtrSizingEngine | T3: add 1-line attribution comment only` -- not updated in R3. Plan §5.2 R3 explicitly overrides this with `AtrSizingEngine.cs is READ ONLY for B13`. The stale §2 entry is contradicted by §5.2, the ticket, and the live grep evidence. Engineer's ground truth is the explicit §5.2 and the ticket. Architect should update §2 in a future R4 pass for consistency, but this does not block engineer execution. | Plan §2 |

---

## Engineer Instructions

**PROCEED.** This review returns TICKET_REVIEW_PASS.

Execute in order: **T1 → T2 → T3**. Build (`dotnet build`) after T1 before starting T2.
Build after T2 before starting T3.

**T1 (GetRefPrice):**
- File: `src/PropTraderTools/TradeCopierPanel.cs` (Wave workspace)
- Replace body of `GetRefPrice()` (lines 749-760) with the triple-null-guard implementation.
- Run SCAN 1-5 after T1. Build must be clean before starting T2.
- Sim101 gate DW-B13-SIM-T1-01 is the compensating verification for the private method.

**T2 (ATR Startup Sync):**
- Files: `TradeCopierPanel.cs` (append 2 lines to `OnLoaded`) + `CopyEngineTests.cs` (add [Fact]).
- Append `NotifyRiskChanged();` and `NotifyAtrFractionChanged();` AFTER `LoadAtmTemplates()` on
  line 338, before the closing brace.
- Add `UpdateAtrFraction_ForwardsToEngine_WhenEngineSet` [Fact] to `CopyEngineTests.cs`.
- Run `dotnet test` -- new [Fact] must pass. Run SCAN 1-7 after T2.

**T3 (Docs+Comment):**
- File 1: `src/PropTraderTools/TradeCopierPanel.cs` line 802 -- replace ONLY line 802 with
  `// NT8-031: no Math.Clamp (.NET 4.8 version constraint -- not the NT8-003 volatile ban).`
  Do NOT displace line 803 (`// JS-021: no lock ...`).
- File 2: `docs/standards/NT8_COMPILER_RULES.md` (Director workspace) -- append INDEX TABLE
  row and `## NT8-031` rule section as specified in ticket.
- No build needed (comment-only + docs). Run SCAN 1-4 to confirm 0 matches on modified file.

**Post-all-tickets cross-file scan (mandatory before ptt-verifier):**
Run the full 7-scan against the complete Wave workspace as specified in the
"Cross-File Scan" section of `04-tickets.md`.

---

## Spec Coverage Summary

| Backlog ID | Description | Ticket | Coverage |
|-----------|-------------|--------|----------|
| DW-B12-DEFER-01 | Wire GetRefPrice to MarketData.Last.Price | T1 | COVERED |
| DW-B12-DEFER-02 | ATR fraction startup sync | T2 | COVERED |
| DW-B12-DEFER-03 | Math.Clamp comment fix + NT8-031 rule | T3 | COVERED (file target resolved -- PASS) |
| DW-B9-01 | ATR canvas visualization | (none) | Shelved to B14 -- correct |
| DW-B9-03 | Click-trader offset | (none) | Shelved to B14 -- correct |
| DW-B12-DEFER-04 | Test name alignment | (none) | Shelved to B14 -- correct per plan §1 |

All three in-scope B13 deferred items are covered. No phantom work. No missing work. PASS.

---

## Summary

| Ticket | Traceability | JS Pre-Check | CYC | NT8 | Test Coverage | Scan Checklist | File Routing | Verdict |
|--------|-------------|-------------|-----|-----|---------------|----------------|-------------|---------|
| T1 | PASS | PASS | PASS | PASS | PASS (private exemption) | PASS | PASS | TICKET_REVIEW_PASS |
| T2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | TICKET_REVIEW_PASS |
| T3 | PASS | PASS | PASS | PASS | PASS (docs-only) | PASS | PASS | TICKET_REVIEW_PASS |

## Overall: TICKET_REVIEW_PASS
