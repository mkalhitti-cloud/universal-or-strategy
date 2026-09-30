# Ticket Review: PTT-COPIER-B52 Lane B — knowledge-doc-weak-refs

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Date**: 2026-08-08
**Tickets file**: docs/brain/B52-LaneB/04-tickets.md
**Plan file**: docs/brain/B52-LaneB/02-architecture-plan.md

---

## T1 — Add NT8_ADDON_KNOWLEDGE.md entry for NinjaTrader.Client.dll removal

### CHECK 1 — Traceability
PASS
- T1 maps to `DW-B50C-02` (stated explicitly in ticket header).
- Description (append documentation block about `NinjaTrader.Client.dll` CS0433 removal)
  matches plan Section 2 verbatim append instruction.

### CHECK 2 — Docs-Only Constraint
PASS
- T1 lists a single modified file: `docs/standards/NT8_ADDON_KNOWLEDGE.md`.
- Ticket header states "Type: Docs-only — zero .cs files touched".
- No `.cs` path appears anywhere in T1.

### CHECK 3 — NT8 Language Compliance
N/A (docs-only ticket — no C# introduced)

### CHECK 4 — Scan Coverage
PASS
- SCAN-08 (`grep -n "NinjaTrader.Client" docs/standards/NT8_ADDON_KNOWLEDGE.md`) is
  present with expected result (>= 1 hit) and pass criteria defined.

### CHECK 5 — CYC Pre-Check
N/A (docs-only ticket — no methods introduced)

### CHECK 6 — 7-Scan Checklist Presence
PASS
- "7-Scan Checklist (T1)" section is present.
- Section explicitly states SCAN-01 through SCAN-07 are not applicable (docs-only),
  and provides SCAN-08 as the applicable verification scan.
- Section includes pass criteria.

### CHECK 7 — Build Tag Update Scope
N/A (docs-only ticket — no build tag in T1)

### WARN — Minor text divergence between plan and ticket (non-blocking)
The architecture plan (Section 2) and the ticket T1 implementation block use slightly
different label lines (`**Assembly removed**` in the plan vs `**Block removed**` in the
ticket). The substance is identical, DW-B50C-02 is correctly closed, and the entry
remains informative. This is flagged as an architect note — not a blocking violation.

### VERDICT: TICKET_REVIEW_PASS

---

## T2 — Replace _atmComboRefs hard-refs with WeakReference<ComboBox> in TradeCopierPanel.cs

### CHECK 1 — Traceability
PASS
- T2 maps to `DW-B50-02` (stated explicitly in ticket header).
- Three surgical edits (field declaration, registration block, iteration method) plus
  a build-tag update match plan Sections 3a, 3b, 3c, and the implied tag update exactly.
- File path (`src/PropTraderTools/TradeCopierPanel.cs`, Wave workspace) matches plan.

### CHECK 2 — Docs-Only Constraint
N/A (T2 is a src ticket — docs-only constraint applies to T1 only)

### CHECK 3 — NT8 Language Compliance
PASS
- **WeakReference<T> availability**: Ticket states "WeakReference<T> is available in
  .NET 4.8 (NT8 host)". Plan Section 3d also confirms ".NET 4.5+ — safe for NT8 use
  without compiler rule violation". PASS.
- **JS-021 (lock())**: No `lock()` keyword appears in any of the four edits. SCAN-01
  explicitly checks for zero lock hits. PASS.
- **JS-033 (async void)**: No `async` keyword in any edit. SCAN-02 explicitly checks
  for zero non-event-handler `async void`. PASS.
- **NT8-001 ({ get; init; })**: No property initializers introduced. PASS.
- **NT8-002 (abstract record / sealed record)**: No record types introduced. PASS.

### CHECK 4 — Scan Coverage
PASS
- SCAN-01 (`grep -r "lock(" ... --include="*.cs"`) present, expected 0 hits. PASS.
- SCAN-02 (`grep -rn "async void " ... --include="*.cs"`) present, expected 0 non-event-handler hits. PASS.
- SCAN-05 (`dotnet build ... PropTraderTools.csproj`) present, expected 0 errors. PASS.
- SCAN-06 (branch count audit on `UpdateAtmComboVisibility`, CYC = 4) present and
  explicitly names the method. PASS.
- SCAN-07 (`powershell -File ... verify_links.ps1`, DESYNC=0 MISSING=0) present. PASS.
- All 5 applicable scans enumerated. PASS.

### CHECK 5 — CYC Pre-Check
PASS
- CYC before = 2: `foreach` loop body (branch 1) + `if (cb != null)` guard (branch 2)
  = 1 (base) + 1 + 1 = confirmed 2. Matches plan Section 3c and ticket CYC table.
- CYC after = 4: 1 (base) + 1 (for-loop condition) + 1 (TryGetTarget true path) +
  1 (TryGetTarget false/prune path) = 4. Confirmed by McCabe derivation in both plan
  and ticket. 4 <= 8 PASS.

### CHECK 6 — 7-Scan Checklist Presence
PASS
- "7-Scan Checklist (T2)" section is present.
- All 5 applicable scans (SCAN-01, SCAN-02, SCAN-05, SCAN-06, SCAN-07) are enumerated
  with commands, expected results, and pass checkbox.
- Pass criteria statement provided at section end.

### CHECK 7 — Build Tag Update Scope
PASS
- EDIT 4 instructs updating `PttBuild.Tag` to exactly:
  `"PTT-COPIER B52 | knowledge-doc-weak-refs | 2026-08-08"`.
- Tag string matches the required value exactly.
- No scope creep: all four edits are confined to `TradeCopierPanel.cs` and are
  directly traceable to `DW-B50-02`.

### VERDICT: TICKET_REVIEW_PASS

---

## Overall: TICKET_REVIEW_PASS

All checks across both tickets pass. No blocking violations found.

| Check | T1 | T2 |
|-------|----|----|
| CHECK 1 — Traceability | PASS | PASS |
| CHECK 2 — Docs-Only Constraint | PASS | N/A |
| CHECK 3 — NT8 Language Compliance | N/A | PASS |
| CHECK 4 — Scan Coverage | PASS | PASS |
| CHECK 5 — CYC Pre-Check | N/A | PASS |
| CHECK 6 — 7-Scan Checklist Presence | PASS | PASS |
| CHECK 7 — Build Tag Update Scope | N/A | PASS |

**WARN (non-blocking)**: Minor label-text divergence between plan Section 2 and T1
implementation block (`**Assembly removed**` vs `**Block removed**`). No DW-ID
misalignment. Architect may align at will; not required before engineer execution.

**The engineer may proceed. This is the green light for Phase 4a.**
