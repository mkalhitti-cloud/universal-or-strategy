# Ticket Review: PTT-REPAIRS-02
**Reviewer**: ptt-ticket-reviewer
**Phase**: 3.5 (Re-Review — Correction Cycle 1)
**Date**: 2026-09-07
**Source**: `docs/brain/PTT-REPAIRS-02/04-tickets.md`
**Prior Review**: `04-ticket-review.md` (Cycle 0) — TICKET_REVIEW_FAIL on V1 (Section H [Fact] count 474)
**This Review**: Full re-check after architect correction

---

## Correction Cycle 1 Verification

| Check | Expected | Found | Result |
|-------|----------|-------|--------|
| Text "474" absent from Section H | Not present | No matches | **PASS** |
| Text "300" present as [Fact] count before | Present at line 233 | `**[Fact] count before this ticket**: 300` | **PASS** |
| Text "301" present as [Fact] count after | Present at line 234 | `**[Fact] count after this ticket**: 301` | **PASS** |
| Language "grown since PTT-REPAIRS-01" absent | Not present | No matches | **PASS** |
| No other sections accidentally altered | All sections A-K and Deferred Work intact | Verified by full read | **PASS** |

---

## Ticket PTT-REPAIRS-02-T1

### 1. Traceability

- Section A maps PTT-REPAIRS-02-T1 to Plan Sections 1, 3, 5, 9.
- Section A maps PTT-REPAIRS-02-T1-TEST to Plan Sections 7, 9.
- Section B enforces scope lock: CopyEngine.cs only.
- Every substantive section (D, E, F, G, H, I) cites plan sections or spec defect root cause.
- No phantom work detected (nothing in ticket absent from plan).
- No missing work detected (plan single-defect fix is fully covered by T1).

**Traceability**: **PASS**

---

### 2. Code Change Accuracy

- Section E Change 1: header comment update at line 5738 — exact before/after provided.
- Section E Change 2: `EvictDedup` Filled block (lines 5762-5768) — exact before/after provided.
- Lines deleted and lines added are enumerated explicitly.
- Pattern proof shows new Filled block mirrors existing Cancelled branch (lines 5758-5759).
- Both collections operated on (`_entryInstrKeyByOrderId`, `_liveEntryInstruments`) are `ConcurrentDictionary` — correct lock-free primitives (JS-025).
- No signature change to `EvictDedup` (line 5740): `internal void EvictDedup(string orderId, OrderState state)` — confirmed unchanged.
- File paths: `src/PropTraderTools/CopyEngine.cs` and `src/PropTraderTools/CopyEngineTests.cs` — both in Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`).

**Code Change Accuracy**: **PASS**

---

### 3. MGC Guard Integrity

- Section G explicitly asserts `_entryDispatchedOrders` guard (DW-B142-MGC-02 / DW-B91-A) at line 5716 is **untouched**.
- Cancelled cleanup path (lines 5758-5759) confirmed unmodified.
- Full MGC cancel+resubmit scenario table provided in Section G — all rows verified intact after fix.
- The comment added in the Filled block (`// MGC cancel+resubmit guard provided by _entryDispatchedOrders (DW-B91-A)`) explicitly documents the guard relationship.
- No removal of any existing guard described anywhere in the ticket.

**MGC Guard Integrity**: **PASS**

---

### 4. CYC Pre-Check

| Method | CYC Before | CYC After | Budget | Verdict |
|--------|-----------|-----------|--------|---------|
| `EvictDedup` (CopyEngine.cs:5740) | 5 | **6** | ≤7 | **PASS** |
| `IsLiveEntryBlocked` (CopyEngine.cs:5710) | 4 | 4 (unchanged) | ≤5 | **PASS** |

Branch count for `EvictDedup` CYC=6 shown explicitly in Section F: base(1) + 5 decision points.
No method exceeds its budget. No extraction required.

**CYC Pre-Check**: **PASS**

---

### 5. NT8 Constraints

| Constraint | Status | Evidence |
|------------|--------|----------|
| No `async/await` in lifecycle methods | PASS | Fix is two `TryRemove` calls; no async |
| No `Account.All` outside Loaded handler | N/A | No `Account.All` usage |
| No `sealed` on TradeCopierWindow | N/A | No window class touched |
| No `FontFamily` on WPF element | N/A | No UI code |
| No hardcoded hex color | N/A | No UI code |
| No `DateTime.Now` | PASS | SCAN-02 command present; no `DateTime.Now` described |
| No `CreateOrder` without "PTT-" prefix | N/A | No order creation |
| No Unicode / non-ASCII in string literals | PASS | SCAN-05 command present; ASCII-only strings confirmed in Section H JS compliance note |
| No `lock()` | PASS | JS-021 table in Section J confirms; SCAN-01 command present |

**NT8 Constraints**: **PASS**

---

### 6. Test Coverage

- Method `EvictDedup` (internal) — covered by `[Fact]` `IsLiveEntryBlocked_ClearsOnFill_AllowsReentry` via seam `EvictDedup_ForTest` (Step 3).
- Method `IsLiveEntryBlocked` (private) — covered by same `[Fact]` via seam `IsLiveEntryBlocked_ForTest` (Steps 1, 5).
- Full test implementation provided in Section H with 5-step assertion table.
- `[Fact]` count before: **300**. `[Fact]` count after: **301**. Delta = +1. Correct.
- Text "474" is **absent** from the ticket (V1 correction confirmed).
- No language claiming the file "has grown since PTT-REPAIRS-01" present.
- Seams table (Section H) confirms all 4 seams exist at `CopyEngine.cs` lines 4264-4282, granted by `InternalsVisibleTo` at line 46.
- JS compliance note in Section H: no `lock()`, no `DateTime.Now`, ASCII-only, `[Fact]` only, CYC=1.

**Test Coverage**: **PASS**

---

### 7. 7-Scan Checklist

Section I contains all 7 scans with exact PowerShell commands and expected results:

| Scan | Present | Command Provided | Expected Result Stated |
|------|---------|-----------------|----------------------|
| SCAN-01 | YES | `Select-String ... lock\s*\(` | 0 matches |
| SCAN-02 | YES | `Select-String ... DateTime\.Now` | 0 matches |
| SCAN-03 | YES | `Select-String ... return null` | 0 matches |
| SCAN-04 | YES | `Select-String ... async void` | 0 matches |
| SCAN-05 | YES | `Select-String ... [^\x00-\x7F]` | 0 matches |
| SCAN-06 | YES | `Select-String ... \?\.\w+\s*-=` | 0 matches |
| SCAN-07 | YES | Manual CYC count per Section F | EvictDedup=6 (≤7), IsLiveEntryBlocked=4 (≤5) |

All 7 scans present. Engineer contract statement present at end of Section I.
Section K (Definition of Done) mirrors all 7 scans as verifiable conditions.

**Scan Checklist**: **PASS**

---

### 8. Completeness

- Single ticket covers the single defect in scope (PTT-REPAIRS-02).
- Definition of Done (Section K) enumerates 11 verifiable conditions covering: all 7 scans, test pass, 5-step assertion, `_entryDispatchedOrders` guard confirmation, Cancelled path confirmation, `[Fact]` count verification.
- Deferred Work section lists 8 out-of-scope items with IDs, priorities, targets, and statuses — no scope leakage into this ticket.
- Scope Lock stated in both the global header (Section SCOPE LOCK) and per-ticket (Section B).

**Completeness**: **PASS**

---

### T1 Verdict

| Check | Result |
|-------|--------|
| Traceability | PASS |
| Code Change Accuracy | PASS |
| MGC Guard Integrity | PASS |
| CYC Pre-Check | PASS |
| NT8 Constraints | PASS |
| Test Coverage | PASS |
| 7-Scan Checklist (SCAN-01 through SCAN-07) | PASS |
| Completeness | PASS |

**VERDICT: TICKET_REVIEW_PASS**

---

## Overall: TICKET_REVIEW_PASS

All 8 checklist items PASS for PTT-REPAIRS-02-T1.
Correction Cycle 1 violation (V1: Section H [Fact] count 474) is fully resolved.
No new violations introduced.

**→ Safe to spawn Phase 4a engineer.**

---

*ptt-ticket-reviewer · PTT-REPAIRS-02 · Re-Review Correction Cycle 1 · 2026-09-07*
