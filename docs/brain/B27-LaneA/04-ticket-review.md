# PTT-COPIER-B27 Lane A — Ticket Review
# Phase: 3.5 (Ticket Review)
# Reviewer: ptt-ticket-reviewer
# Date: 2026-07-16
# Input: docs/brain/B27-LaneA/04-tickets.md (1 ticket)
# Plan:  docs/brain/B27-LaneA/02-architecture-plan.md (REVIEW_PASS)

---

## STEP 0 — RULES CATALOG GATE

File: `c:\WSGTA\universal-or-strategy-director\docs\standards\jane-street\RULES_CATALOG.md`
Encoding: UTF-8 (no BOM, no wide-character markers). Raw bytes confirmed clean.

**Gate result: PASS**

---

## Ticket Review: B27-LaneA

### B27-T1 — DW-B27-01: Replace singleton BE fields with per-account slot dicts

---

### A. TRACEABILITY

**A1. Ticket references spec req DW-B27-01 (P0)?**
Section 1 explicitly lists `DW-B27-01 | P0` as the sole spec requirement satisfied. The
description matches the root cause documented in the architecture plan (§0) verbatim.
**PASS**

**A2. All changed methods traced to defect root cause (singleton fields)?**
Section 2 (GOAL) explicitly names each of the 6 body rewrites and 2 method deletions,
tracing each to the singleton field removal. Section 7 provides exact BEFORE/AFTER for
every changed block. Section 5 names all 9 fields for deletion. No phantom work found.
All plan items (architecture plan §4.1–§4.8, §8.1–§8.3) appear in the ticket. No plan
section omitted.
**PASS**

**Traceability verdict: PASS**

---

### B. JS PRE-CHECK

**B1. JS-021: No lock() anywhere in ticket's code specs?**
Review of all code blocks in Sections 4–8 and Section 7:
- `_pendingBeSlots[masterAcc.Name] = ...` — ConcurrentDictionary indexer (lock-free). PASS.
- `_pendingBeSlots.TryRemove(...)` — atomic. PASS.
- `_trailBeSlots[masterAcc.Name] = ...` — atomic. PASS.
- `_trailBeLastPnlBits.AddOrUpdate(...)` — internal CAS loop, no lock. PASS.
- `_pendingBeSlots.TryGetValue(...)` / `TryRemove(...)` — lock-free reads. PASS.
Section 9 rule table row for JS-021 confirms: "SCAN-01 must return 0 results."
No `lock()` construct appears anywhere in the ticket's prescribed code.
**PASS**

**B2. JS-033: No async void non-event-handler in specs?**
All 6 rewritten methods are `private void` or `internal void`. Section 9 confirms
"All methods are synchronous void event handlers. No async anywhere." SCAN-07 mandated.
No `async` keyword appears in any code block in the ticket.
**PASS**

**B3. JS-001: No throw new XxxException in hot paths?**
Section 9 rule table: "All callbacks use guard returns only. No try/catch introduced."
No `throw` statement appears in any prescribed code block.
**PASS**

**B4. JS-002: No return null for missing values in specs?**
All 6 methods are `void`. The ticket's Section 9 explicitly notes "All methods are void.
N/A for method returns." No nullable return type appears anywhere.
**PASS**

**JS Pre-Check verdict: PASS**

---

### C. CYC PRE-CHECK

**C1. Every method in ticket has explicit CYC annotation?**
- Section 7.1 (ArmPendingBe): `CYC=4` annotated. PASS.
- Section 7.2 (DisarmPendingBe): `CYC=3` annotated. PASS.
- Section 7.4 (ArmTrailBe): `CYC=4` annotated. PASS.
- Section 7.5 (DisarmTrailBe): `CYC=3` annotated. PASS.
- Section 7.7 (OnTrailBeAccountUpdate): `CYC<=6` annotated. PASS.
- Section 7.8 (OnPendingBeAccountUpdate): `CYC=8` annotated. PASS.
- Section 9 rule table lists all values explicitly. PASS.
**PASS**

**C2. All CYC values are <= 8?**
ArmPendingBe=4, DisarmPendingBe=3, ArmTrailBe=4, DisarmTrailBe=3,
OnTrailBeAccountUpdate<=6, OnPendingBeAccountUpdate=8.
Maximum is 8. All within ceiling.
**PASS**

**C3. ArmPendingBe=4, DisarmPendingBe=3, ArmTrailBe=4, DisarmTrailBe=3?**
Values in Section 7 match exactly. Plan §4.1–§4.5 match as well.
**PASS**

**C4. OnPendingBeAccountUpdate=8, OnTrailBeAccountUpdate<=8?**
OnPendingBeAccountUpdate=8 (branch-by-branch breakdown in §4.7 of plan and §7.8 of ticket).
OnTrailBeAccountUpdate annotated as CYC<=6 (§7.7 / plan §4.8). Both within 8-ceiling.
**PASS**

**CYC Pre-Check verdict: PASS**

---

### D. NT8 CONSTRAINTS

**D1. NT8-001: struct fields `internal readonly`, not init setters — confirmed in ticket spec?**
Section 4 (STRUCTS TO ADD) and Section 9 rule table:
- Fields: `internal readonly Account Account;`, `internal readonly Instrument Instrument;`,
  `internal readonly int BufferTicks;` — plain readonly fields, no `{ get; init; }`.
- NT8 COMPILER NOTES in Section 4 explicitly cite NT8-001 and state
  "Fields MUST be `internal readonly T Field;` (NOT `{ get; init; }`)."
**PASS**

**D2. NT8-003: no volatile on long — all PnL CAS via AddOrUpdate/Interlocked?**
Section 6 field spec for `_trailBeLastPnlBits`:
  "NT8-003: long (NOT volatile double). ConcurrentDictionary.AddOrUpdate provides CAS semantics."
Section 7.7 (OnTrailBeAccountUpdate) body uses `_trailBeLastPnlBits.AddOrUpdate(...)` with
CAS lambda. No `volatile` keyword on any new field. Section 9 rule table row NT8-003 confirms.
SCAN-06 (`grep volatile ... | grep trail|pending` → 0) mandated.
**PASS**

**D3. NT8-004: ConcurrentDictionary<string,TStruct> confirmed safe in NT8?**
Section 6 comment block on every new field: "NT8-004: ConcurrentDictionary is safe
(ImmutableDictionary BANNED)." Section 9 rule table row NT8-004 confirms.
**PASS**

**D4. NT8-005 (CS8341): struct is NOT declared `readonly struct`?**
Section 4 NT8 COMPILER NOTES: "NT8-005: NOT declared `readonly struct`."
Both struct declarations: `private struct PendingBeSlot` and `private struct TrailBeSlot`
(not `readonly struct`). Fields inside are `readonly` — only the struct declaration itself
omits the `readonly` modifier. Matches NT8-005 Option A.
**PASS**

**D5. Ticket does NOT touch TradeCopierPanel.cs?**
Section 2 (GOAL): "TradeCopierPanel.cs: ZERO changes."
CHANGE SUMMARY TABLE confirms TradeCopierPanel.cs entry: "ZERO changes."
Plan §2.2 (Files Out of Scope) lists TradeCopierPanel.cs explicitly.
**PASS**

**NT8 Constraints verdict: PASS**

---

### E. COMPLETENESS

**E1. All 9 singleton fields listed for deletion?**
Section 5 lists all 9 fields:
  1. `_pendingBeStates` (ConcurrentDictionary<string,int>)
  2. `_pendingBeBufferTicks` (volatile int)
  3. `_pendingBeAccount` (Account)
  4. `_pendingBeInstrument` (Instrument)
  5. `_trailBeStates` (ConcurrentDictionary<string,int>)
  6. `_trailBeBufferTicks` (volatile int)
  7. `_trailBeLastPnl` (long)
  8. `_trailBeAccount` (Account)
  9. `_trailBeInstrument` (Instrument)
Cross-checked against CopyEngine.cs lines 100-114: all 9 present in source. All 9 listed.
**PASS**

**E2. Both structs (PendingBeSlot, TrailBeSlot) specified?**
Section 4 provides complete struct declarations for both. Plan §3.2 matches exactly.
**PASS**

**E3. All 3 replacement fields (_pendingBeSlots, _trailBeSlots, _trailBeLastPnlBits) specified?**
Section 6 provides all 3 fields with full declarations, comments, and rationale.
Plan §3.3 matches exactly.
**PASS**

**E4. IsPendingBeArmed + IsTrailBeArmed both scheduled for DELETE?**
Section 7.3: "DELETE: IsPendingBeArmed (~L1336-L1339). Remove the entire method body and
declaration."
Section 7.6: "DELETE: IsTrailBeArmed (~L1390-L1393). Remove the entire method body and
declaration."
CHANGE SUMMARY TABLE: both listed as "DELETE method."
**PASS**

**E5. _trailBeLastPnlBits rationale documented (CAS on boxed struct field is impossible)?**
Section 6 comment on `_trailBeLastPnlBits`:
  "NOTE: _trailBeLastPnlBits is SEPARATE from TrailBeSlot -- struct values in ConcurrentDictionary
  are boxed; cannot take ref to a boxed struct field for Interlocked CAS."
Plan §3.3 contains the same rationale. Fully documented.
**PASS**

**Completeness verdict: PASS**

---

### F. CALLBACK REWRITE CORRECTNESS

**F1. OnPendingBeAccountUpdate: accName derived from (sender as Account)?.Name — NOT from _pendingBeAccount?**
Section 7.8 prescribed body, line 2:
  `string accName = (sender as NinjaTrader.Cbi.Account)?.Name ?? string.Empty;`
The old pattern (`var acc = _pendingBeAccount;`) is entirely absent from the new body.
Plan §4.7 KEY CHANGE note: "sender cast replaces _pendingBeAccount capture."
**PASS**

**F2. OnPendingBeAccountUpdate: TryGetValue for gates 2-6, TryRemove at gate 7 (atomic claim)?**
Section 7.8 prescribed body:
  Gate (2): `if (!_pendingBeSlots.TryGetValue(accName, out var slot))` — TryGetValue, read-only.
  Gates (3)–(6): slot fields used, no dict mutation.
  Gate (7): `if (!_pendingBeSlots.TryRemove(accName, out var removed))` — atomic disarm win.
  Only one thread can win TryRemove; all concurrent callbacks for the same account lose and
  return. Correct CAS-win pattern.
**PASS**

**F3. OnTrailBeAccountUpdate: accName derived from (sender as Account)?.Name — NOT from _trailBeAccount?**
Section 7.7 prescribed body, line 2:
  `string accName = (sender as NinjaTrader.Cbi.Account)?.Name ?? string.Empty;`
The old `var acc = _trailBeAccount;` capture is absent from the rewrite.
**PASS**

**F4. OnTrailBeAccountUpdate: AddOrUpdate CAS for _trailBeLastPnlBits, AddOrUpdate for BufferTicks increment?**
Section 7.7 prescribed body:
  `long actual = _trailBeLastPnlBits.AddOrUpdate(accName, newBits, (k, cur) => cur < newBits ? newBits : cur);`
  `_trailBeSlots.AddOrUpdate(accName, new TrailBeSlot(..., slot.BufferTicks + 1), (k, old) => new TrailBeSlot(..., old.BufferTicks + 1));`
Both AddOrUpdate calls present. CAS-style high-water mark on PnL bits; then slot increment.
**PASS**

**Callback Rewrite Correctness verdict: PASS**

---

### G. TEST COVERAGE

**G1. Existing test ArmTrailBe_NullInstrument_NoException update specified (field name + type cast)?**
Section 8.1 specifies exact 3-line update:
  - Line 1668: `"_trailBeStates"` → `"_trailBeSlots"`
  - Line 1671: `ConcurrentDictionary<string, int>` → `ConcurrentDictionary<string, TrailBeSlot>`
  - Line 1666 comment: updated text provided.
BEFORE/AFTER blocks match current source (CopyEngineTests.cs lines 1666–1672 confirmed).
**PASS**

**G2. T_B27_01 [Fact] test specified with correct assertion targets?**
Section 8.2 provides full test body for `T_B27_01_ArmTwoPanels_SecondArmDoesNotNullFirstInstrument`:
  - Reflects `_pendingBeSlots` field.
  - Reflects `PendingBeSlot` nested type.
  - Asserts `Account`, `Instrument`, `BufferTicks` fields exist via reflection.
  - Structural contract: proves data model migration from singleton fields.
Assertions are concrete and will fail if the engineer does not implement the structs/dicts.
**PASS**

**G3. T_B27_02 [Fact] test specified with correct assertion targets?**
Section 8.3 provides full test body for `T_B27_02_DisarmOneAccount_DoesNotAffectOther`:
  - Reflects all three replacement fields: `_pendingBeSlots`, `_trailBeSlots`, `_trailBeLastPnlBits`.
  - Triple structural proof of complete data model migration.
  - Note on test design (no live NT8 session available) is documented.
**PASS**

**G4. [Fact] count target = 135?**
Section 2: "133 --> 135" (+2 new [Fact] tests, 1 existing test updated = net +2).
Section 10 SCAN-05: `Select-String ... \[Fact\] | Measure-Object` expected Count = 135.
**PASS**

**Test Coverage verdict: PASS**

---

### H. 7-SCAN CHECKLIST PRESENCE

Section 10 of the ticket contains all 7 scans. Verified line-by-line:

**H1. All 7 scans present in ticket?**
Section 10 lists SCAN-01 through SCAN-07 in sequence, each with grep command, expected result,
and the rule/constraint it checks. All 7 are present.
**PASS**

**H2. SCAN-01: grep lock() → 0?**
`grep -n "lock(" CopyEngine.cs` / Expected: 0 results / Checks: JS-021.
Present. Correctly targets JS-021.
**PASS**

**H3. SCAN-02: grep deleted pending fields → 0?**
`grep -n "_pendingBeAccount\|_pendingBeInstrument\|_pendingBeStates\|_pendingBeBufferTicks" CopyEngine.cs`
Expected: 0 results. Checks all 4 pending BE singleton fields are deleted.
Present.
**PASS**

**H4. SCAN-03: grep deleted trail fields → 0?**
`grep -n "_trailBeAccount\|_trailBeInstrument\|_trailBeStates\|_trailBeBufferTicks\|_trailBeLastPnl[^B]" CopyEngine.cs`
Expected: 0 results. NOTE annotation explains `[^B]` excludes the new `_trailBeLastPnlBits` field.
This is correct regex construction — essential for avoiding false match on the new field.
Present.
**PASS**

**H5. SCAN-04: grep IsPendingBeArmed|IsTrailBeArmed → 0?**
`grep -n "IsPendingBeArmed\|IsTrailBeArmed" CopyEngine.cs`
Expected: 0 results. Checks both deleted helper method declarations and all call sites.
Present.
**PASS**

**H6. SCAN-05: [Fact] count = 135?**
`Select-String -Path CopyEngineTests.cs -Pattern "\[Fact\]" | Measure-Object`
Expected: Count = 135.
Present.
**PASS**

**H7. SCAN-06: grep volatile trail|pending → 0?**
`grep -n "volatile" CopyEngine.cs | grep -i "trail\|pending"`
Expected: 0 results. Checks NT8-003 — no volatile keyword on any trail/pending BE field.
Present.
**PASS**

**H8. SCAN-07: grep async void → 0?**
`grep -n "async void " CopyEngine.cs`
Expected: 0 results. Checks JS-033 async void ban.
Present.
**PASS**

**7-Scan Checklist Presence verdict: PASS (all 7 scans present)**

---

### I. ASCII COMPLIANCE

**I1. No Unicode/emoji/curly-quotes planned in string literals?**
All string literals in the ticket's prescribed code use ASCII only:
  - `string.Empty` (not "" with smart quotes)
  - Comment text: all ASCII printable characters confirmed throughout Sections 4–10
  - No Unicode escape sequences (`\uXXXX`) anywhere
  - No emoji characters
  - Section 9 rule table row "ASCII": "All field names, string literals, comments are
    ASCII-only. No emoji, curly quotes, or non-ASCII."
**PASS**

**ASCII Compliance verdict: PASS**

---

## ADDITIONAL OBSERVATIONS (non-blocking, for architect awareness)

**OBS-1 — OnTrailBeAccountUpdate CYC counting**
The ticket annotates `CYC<=6` for `OnTrailBeAccountUpdate`. The plan counts 4 explicit
branches (1 filter, 2 TryGetValue-miss, 3b improvement-check, 4 lost-race). The ternary
inside the AddOrUpdate lambda is not counted as a method-level branch per the team
convention (documented in §7.7 note). This is internally consistent and within the 8-ceiling.
No violation.

**OBS-2 — `(3a)` and `(3b)` dual-gate in OnTrailBeAccountUpdate**
The ticket labels gate `(3a)` as the `_trailBeLastPnlBits.TryGetValue` check and `(3b)` as the
`newPnl <= oldPnl` comparison. This adds a gate not present in the current source (the old code
reads from a single field). This new gate (3a) is architecturally correct: if the PnL slot is
absent (arm not yet complete), the callback should silently return. No violation — this is a
defensive correctness improvement.

**OBS-3 — CYC of DisarmPendingBe and DisarmTrailBe: plan vs. ticket discrepancy**
The plan header comment for `DisarmPendingBe` (§4.2) reads `CYC=3`; the ticket Section 7.2
header also reads `CYC=3`. However, the plan's existing source at line 1311 shows
`CYC=4`. The ticket's annotation of CYC=3 reflects the REWRITTEN body (leader null guard +
TryRemove-miss + slot.Account null guard = 3 decision points). This is consistent with the
rewritten body spec. The discrepancy is between EXISTING source (CYC=4 per comment at L1311)
and PLANNED rewrite (CYC=3). The ticket correctly annotates the rewrite. No violation.

---

## File Routing

C# source files are routed to:
  `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
  `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`

Both are Wave workspace paths (correct). Director workspace is used only for this review
document. No .cs path points to the Director workspace.

**File Routing verdict: PASS**

---

## Summary Verdict Table

| Check | Result |
|-------|--------|
| A. Traceability | PASS |
| B. JS Pre-Check (JS-021, JS-033, JS-001, JS-002) | PASS |
| C. CYC Pre-Check (all methods ≤ 8, values correct) | PASS |
| D. NT8 Constraints (NT8-001/003/004/005 + no TradeCopierPanel) | PASS |
| E. Completeness (9 fields, 2 structs, 3 new fields, 2 deletes) | PASS |
| F. Callback Rewrite Correctness | PASS |
| G. Test Coverage (update + 2 new [Fact], count 135) | PASS |
| H. 7-Scan Checklist (SCAN-01 through SCAN-07 all present) | PASS |
| I. ASCII Compliance | PASS |
| File Routing | PASS |

---

## Overall: TICKET_REVIEW_PASS

**TICKET_REVIEW_PASS**

All 9 check categories pass. No JS violations, no NT8 violations, no missing scans,
no phantom work, no orphaned plan sections. Ticket B27-T1 is cleared for engineering.
