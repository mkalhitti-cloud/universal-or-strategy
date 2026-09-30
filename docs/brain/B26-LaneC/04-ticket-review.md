# Ticket Review: B26-LaneC

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Source tickets**: `docs/brain/B26-LaneC/04-tickets.md`
**Source plan**: `docs/brain/B26-LaneC/02-architecture-plan.md` (Revision 2, REVIEW_PASS)
**Spec lines**: `specs/002-trade-copier-spec.html` L10190-10252
**Rules source**: `docs/standards/jane-street/RULES_CATALOG.md`
**Date**: 2026-07-17

---

## T1 — DW-B26-03: BE Armed/Connected visual fix

### TR1 — Traceability

PASS.
- Spec ID cited: `DW-B26-03 (spec L10191-10210)`.
- Plan ref cited: `Part A (plan L27-113)`.
- Every change block traces to a specific spec line (L10202, L10203-10205, L10206, L10247).

### TR2 — Exact code for all 4 changes

PASS.

- **Change 1 (Idle)**: OLD block shows all 3 property assignments (`_beBtn2.Content`, `_beBtn2.BorderBrush = null`, `_beBtn2.BorderThickness = new Thickness(0)`). NEW block removes `BorderBrush` and `BorderThickness` lines and adds `_beBtn2.Background = BrushInactive`. Matches spec L10206.
- **Change 2 (Armed)**: OLD block shows `_beBtn2.BorderBrush = BrushCaution` + `_beBtn2.BorderThickness = new Thickness(2)`. NEW removes both and adds `_beBtn2.Background = BrushCaution`. Matches spec L10202 and Architecture Decision L10247.
- **Change 3 (Connected)**: OLD block shows `_beBtn2.BorderBrush = BrushConnected` + `_beBtn2.BorderThickness = new Thickness(2)`. NEW removes both and adds `_beBtn2.Background = BrushConnected`. Matches spec L10202 and Architecture Decision L10247.
- **Change 4 (L418 guard)**: OLD = `if (_beBtn2 != null) _beBtn2.Background = hasPosition ? BrushActive : BrushInactive;` NEW = `if (_beBtn2 != null && _beState == BeState.Idle) _beBtn2.Background = hasPosition ? BrushActive : BrushInactive;`. Matches spec L10204→L10205 exactly.

### TR3 — T2 scope (N/A for T1)

N/A — applies only to T2.

### TR4 — 7-scan checklist present

PASS.
All 7 scans present with explicit SCAN-01 through SCAN-07 labels:

| Scan | Content |
|------|---------|
| SCAN-01 | JS-021 `lock()` — grep command + expected zero |
| SCAN-02 | JS-001 `throw new` — grep command + expected zero |
| SCAN-03 | JS-002 `return null` — grep command + expected zero |
| SCAN-04 | NT8-001 `init` accessor — inspect changed lines |
| SCAN-05 | NT8-002 `record` keyword — inspect changed lines |
| SCAN-06 | NT8-003 `volatile` — inspect changed lines |
| SCAN-07 | CYC gate — `UpdateBeVisuals` = 3, `UpdateButtonColors` = 5 |

### TR5 — CYC unchanged assertion

PASS.
- Acceptance criteria #5: "`UpdateBeVisuals` CYC = **3** (unchanged — 3-case switch, same shape)".
- Acceptance criteria #6: "`UpdateButtonColors` CYC = **5** (unchanged — guard replaces unconditional write, same branch count)".
- SCAN-07 repeats both values and confirms both ≤ 8.

### TR6 — [Fact] count = 131 (N/A for T1)

N/A — T1 makes no test changes.

### TR7 — STOP mandate present

PASS.
Engineer mandate block reads: "If the actual lines differ from the OLD block shown below, STOP and report the discrepancy. Do NOT guess or apply the diff blindly." Present in the preamble of the Changes section and applies to all 4 change blocks.

### TR8 — NT8 pre-check: no P0 patterns in new code

PASS.
New lines introduced are exclusively WPF property assignments:
- `_beBtn2.Background = BrushInactive;`
- `_beBtn2.Background = BrushCaution;`
- `_beBtn2.Background = BrushConnected;`
- `if (_beBtn2 != null && _beState == BeState.Idle) _beBtn2.Background = ...`

No `{ get; init; }` (NT8-001). No `record` keyword (NT8-002). No `volatile` field (NT8-003). No `async/await`. No `lock()` (JS-021). No `throw new` (JS-001). No `return null` (JS-002). No `DateTime.Now`. No hardcoded hex color. SCAN-04/05/06 explicitly require the engineer to confirm absence.

### TR9 — Execution order: T1 before T2

PASS.
Tickets file final section states: "**T1 first** (visual fix — additive changes only, no line-number shift for T2). **T2 second** (deletions — use reverse-order deletion...)."

### VERDICT: T1

**TICKET_REVIEW_PASS**

---

## T2 — DEAD-B26: Remove 5 dead fields + 2 dead methods

### TR1 — Traceability

PASS.
- Spec ID cited: `DEAD-B26 (spec L10213-10236)`.
- Plan ref cited: `Part B (plan L116-144)`.
- Every deletion cites its spec line, line-range evidence, and grep confirmation rationale.

### TR2 — Exact code (N/A for T2)

N/A — T2 is deletion-only. TR2 applies to T1 additive changes. T2 provides exact deletion signatures (method open signatures + "ends at its closing `}`") and exact field lines to delete with explicit content-match requirement.

### TR3 — T2 scope: 7 items exactly, explicit NOT-in-scope list

PASS.

**7 items authorized:**
1. `_copyToggleBtn` (L121) — field
2. `_flattenBtn` (L122) — field
3. `_cancelBtn` (L123) — field
4. `_trimBtn` (L124) — field
5. `_beBtn` (L125) — field
6. `OnToggle` (L1270-1276) — method
7. `OnBreakEven` (L1293-1300) — method

**Explicit NOT-in-scope table** present with 6 rows:
- `_statusText` (L126) — LIVE
- `_copyEnabled` (L127) — LIVE
- `_beBufferBox` (L128) — LIVE
- `OnTrim` (L1278-1281) — "Out of scope — not authorized by DEAD-B26"
- `OnFlatten` (L1283-1286) — "Out of scope — not authorized by DEAD-B26"
- `OnCancel` (L1288-1291) — "Out of scope — not authorized by DEAD-B26"

The ticket also instructs: "If `OnTrim`, `OnFlatten`, or `OnCancel` are believed dead, raise a new deferred item (`DW-B26-backlog-01`) for Director approval. Do NOT delete them as part of this ticket."

### TR4 — 7-scan checklist present

PASS.
All 7 scans present with explicit SCAN-01 through SCAN-07 labels:

| Scan | Content |
|------|---------|
| SCAN-01 | JS-021 `lock()` — grep command + "deletion adds nothing" |
| SCAN-02 | JS-001 `throw new` — grep command + "no new throws — deletion only" |
| SCAN-03 | JS-002 `return null` — grep command + "no new return null — deletion only" |
| SCAN-04 | NT8-001/002/003 — inspect change + "no `init`/`record`/`volatile` introduced — deletion only" |
| SCAN-05 | Dead symbol refs — references post-deletion verification commands |
| SCAN-06 | `_beBufferBox` live — grep + "at least 1 result (L1417) — field retained" |
| SCAN-07 | `[Fact]` count — `grep -c "\[Fact\]" *.Tests.cs` = 131 unchanged |

Note: T2 repurposes SCAN-05 and SCAN-06 for deletion-specific verification (dead-symbol ref check and live-field retention check). This is appropriate for a deletion-only ticket — the scans are adapted to the actual risk profile. The 7-scan structural requirement is fully satisfied.

### TR5 — CYC assertion (N/A for T2)

N/A — T2 deletes code only. No methods are modified. No CYC change.

### TR6 — [Fact] count = 131 unchanged

PASS.
- Acceptance criteria #5: "`[Fact]` count = **131** (unchanged — no test references any deleted symbol)".
- SCAN-07: "`grep -c "\[Fact\]" *.Tests.cs` | 131 — unchanged".
- Plan Part C confirms no [Fact] tests reference any of the 7 deleted items.

### TR7 — STOP mandate + post-deletion grep verification

PASS.

**STOP mandate**: Engineer mandate block states: "Before deleting any block, read the exact current lines using `read_file`. If the actual content differs from the description below, STOP and report the discrepancy. Do NOT delete by line number alone — confirm content matches first."

**Post-deletion grep verification**: Three explicit PowerShell `Select-String` commands provided:
1. `Select-String -Path TradeCopierPanel.cs -Pattern "_copyToggleBtn|_flattenBtn|_cancelBtn|_trimBtn|_beBtn\b"` — expected: zero results.
2. `Select-String -Path TradeCopierPanel.cs -Pattern "OnToggle|OnBreakEven"` — expected: zero results.
3. `Select-String -Path TradeCopierPanel.cs -Pattern "_beBufferBox"` — expected: at least 1 result (L1417 DispatchShortcut — confirms live field retained).

### TR8 — NT8 pre-check: no P0 patterns in new code

PASS.
T2 is deletion-only. No new code is introduced. SCAN-04 explicitly confirms "no `init`/`record`/`volatile` introduced — deletion only." No JS-021/001/002 exposure from pure deletion. No NT8-P0 risk.

### TR9 — Execution order: T1 before T2

PASS.
Same mandate as in T1. T2 section of the Execution order note: "**T2 second** (deletions — use reverse-order deletion to preserve line number stability within T2 itself: delete `OnBreakEven` first, then `OnToggle`, then the field block at L121-125)."

### VERDICT: T2

**TICKET_REVIEW_PASS**

---

## Spec Discrepancy Note (informational — not a ticket violation)

Spec Architecture Decision L10248 states "Delete **4** dead field declarations (L121-124)" — this conflicts with spec body L10233 which states "Delete L121-125 (**5** dead field declarations)." The architecture plan (Revision 2) explicitly addresses this: `_beBtn` at L125 has "zero references anywhere" and is listed as item 5 in the authorized deletion table. The tickets correctly follow the authoritative spec requirement (L10233 = the fix description) and the plan (which explicitly includes all 5 fields). This is an internal spec inconsistency in the architecture-decisions block (L10248) vs. the fix description (L10233); the ticket correctly follows the higher-authority fix text. No ticket violation.

---

## Overall

| Check | T1 | T2 |
|-------|----|----|
| TR1 Traceability | PASS | PASS |
| TR2 Exact code | PASS | N/A (deletion-only; deletion signatures provided) |
| TR3 7-item scope + NOT-in-scope list | N/A | PASS |
| TR4 7-scan checklist | PASS | PASS |
| TR5 CYC unchanged assertion | PASS | N/A |
| TR6 [Fact] count = 131 | N/A | PASS |
| TR7 STOP mandate + post-deletion greps | PASS | PASS |
| TR8 NT8 pre-check | PASS | PASS |
| TR9 Execution order T1→T2 | PASS | PASS |
| JS-021 `lock()` | PASS | PASS |
| JS-001 `throw new` | PASS | PASS |
| JS-002 `return null` | PASS | PASS |
| NT8-001 `init` | PASS | PASS |
| NT8-002 `record` | PASS | PASS |
| NT8-003 `volatile` | PASS | PASS |
| File routing (Wave workspace) | PASS | PASS |

**No violations found in either ticket.**

## Overall: TICKET_REVIEW_PASS
