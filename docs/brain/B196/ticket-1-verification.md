# Ticket 1 Verification Report

**Epic**: B196 -- Gap Restore: DW-B195-02 + DW-B170-01 + DW-B169 COLLATERAL-STORM + B161-B166 Ghost-Entry Guard + B163 AFP Guard
**Ticket**: 1 -- DW-B195-02: DrainThenDispatch Fast-Path ATM-Aware Dispatch
**Verifier**: ptt-verifier (Phase 4b)
**Date**: 2026-10-11
**SCOPE LOCK**: TICKET 1 ONLY. No other ticket completion files read in this session.

---

## SCOPE LOCK Confirmation

SCOPE LOCK honored. Only the following files were read:
- `src/PropTraderTools/CopyEngine.cs` (READ-ONLY)
- `docs/brain/B196/04-tickets.md` (Ticket 1 section only)
- `docs/brain/B196/02-architecture-plan.md` (section 3 T1 design)
- `docs/brain/B196/ticket-1-completion.md` (engineer Layer 2 report)
- `src/PropTraderTools/Tests/B196Tests.cs` (test verification)

No completion files for Tickets 2-5 were read, opened, or referenced.

---

## All 7 Independent Scan Results (Layer 3)

All scans run independently using PowerShell `Select-String` / grep. Engineer's Layer 2 results NOT trusted until cross-checked.

### SCAN 1 -- JS-021 lock() Check

**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\(" | Where-Object { $_.Line -notmatch "//.*lock" }`

**Result**: 0 results

**Verdict**: PASS -- No lock() calls in source. DrainThenDispatch uses GetCloneAtmMode() (volatile read) -- lock-free per JS-021.

---

### SCAN 2 -- JS-033 async void Check

**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async void " | Where-Object { $_.Line -notmatch "//" }`

**Result**: 0 results

**Verdict**: PASS -- No async void declarations. DrainThenDispatch is `private void` (synchronous).

---

### SCAN 3 -- JS-066 CYC Independent Count

**Method read**: `DrainThenDispatch` (CopyEngine.cs lines 6582-6669)

**Independent branch count**:

| # | Branch | Line | New? |
|---|--------|------|------|
| base | implicit | -- | +1 |
| (1) | `if (follower == null \|\| instrument == null)` | 6591 | pre-existing |
| (2) | `if (!entryCandidates.Any())` | 6609 | pre-existing |
| (2b) | `if (fastAtm is FollowerAtmMode.Named namedFastAtm)` | 6619 | **NEW** |
| (3) | `foreach (var e in entryCandidates)` | 6658 | pre-existing |

**Independent CYC = 1 + 4 = 5**

**Verdict**: PASS -- CYC=5. Within limit of 8. Matches engineer's reported CYC=5 exactly.

---

### SCAN 4 -- JS-001 throw new Check

**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "throw new " | Select-Object LineNumber, Line`

**Result**: 0 results

**Verdict**: PASS -- No throw statements anywhere in CopyEngine.cs. DrainThenDispatch uses early return pattern exclusively.

---

### SCAN 5 -- JS-002 return null Check

**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null" | Select-Object LineNumber, Line`

**Result**: Multiple pre-existing matches at lines 1271, 2022, 3158, 3239, 3247, 3891, 3941, 4116, 5471, 5477, 5556, 6358, 6373. All in methods OTHER than DrainThenDispatch.

**DrainThenDispatch region check (lines 6582-6669)**: Zero `return null` in this range.

**Additional note**: DrainThenDispatch is declared `private void` -- a null return is not syntactically possible.

**Verdict**: PASS -- 0 new return null in the modified section. Pre-existing instances are not in scope for T1.

---

### SCAN 6 -- ASCII-Only Check

**Method**: Visual inspection of lines 6609-6635 (new fast-path block).

New identifiers:
- `fastAtm` -- ASCII only
- `namedFastAtm` -- ASCII only
- `sig` -- ASCII only

New string literals:
- `"-fast-atm-"` (line 6626) -- 11 ASCII characters: hyphen, letters, hyphen. No Unicode.

Comments in new block (lines 6611-6617):
- All DW tags, JS rule references, NT8 notes -- 7-bit ASCII only.
- No curly quotes, no emoji, no non-ASCII characters found.

**Verdict**: PASS -- All new code is strictly ASCII-only.

---

### SCAN 7 -- DateTime.Now / DateTime.UtcNow Check

**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "DateTime\.Now[^U]" | Select-Object LineNumber, Line`

**Result**: 1 match at line 4391 -- comment text `"No DateTime.Now. No lock()"`. This is documentation, not a DateTime.Now call.

**DrainThenDispatch check**: New code uses `Environment.TickCount` (line 6626) -- approved pattern per existing `SubmitDrainedEntry` baseline (B195-T1). No DateTime usage anywhere in the modified section.

**Verdict**: PASS -- 0 actual DateTime.Now calls. The single match is a comment explaining the absence of DateTime.Now.

---

## Correctness Checklist (8 Items)

All items verified against actual source (CopyEngine.cs lines 6609-6635):

| # | Check | Line | Verdict |
|---|-------|------|---------|
| 1 | Fast-path `(!entryCandidates.Any())` calls `GetCloneAtmMode()` before `SubmitEntryDirect` | 6618 | PASS |
| 2 | `GetCloneAtmMode()` pattern match: `FollowerAtmMode.Named namedFastAtm` | 6619 | PASS |
| 3 | If Named: builds `CopySignal` with `orderId = follower.Name + "-fast-atm-" + (long)(int)Environment.TickCount` | 6621-6627 | PASS |
| 4 | If Named: calls `SendCopyWithAtm(follower, instrument, in sig, namedFastAtm)` | 6628 | PASS |
| 5 | If NOT Named: calls `SubmitEntryDirect` (backwards-compatible path preserved) | 6630-6633 | PASS |
| 6 | `return;` follows both branches | 6634 | PASS |
| 7 | `"now"` variable NOT used in fast-path (declared at line 6638, after early return -- unreachable) | 6638 | PASS |
| 8 | orderId suffix is `"-fast-atm-"` NOT `"-drain-atm-"` | 6626 | PASS |

---

## Ticket Satisfaction

| Check | Verdict |
|-------|---------|
| Fix matches exact design from `04-tickets.md` Ticket 1 (Replacement Code block) | PASS |
| Fix matches DW-B195-02 design from `02-architecture-plan.md` section 3 T1 | PASS |
| `[Fact]` test `DrainThenDispatch_FastPath_CloneAtmMode_CallsSendCopyWithAtm` exists in B196Tests.cs | PASS |
| `[Fact]` test `DrainThenDispatch_FastPath_NoAtmMode_CallsSubmitEntryDirect` exists in B196Tests.cs | PASS |
| Methods NOT modified: `PendingDispatchDrain`, `SubmitDrainedEntry`, `SubmitEntryDirect`, `SendCopyWithAtm`, `GetCloneAtmMode` | PASS (not touched) |
| CYC comment in method header updated from CYC=3 to CYC=5 (line 6575) | PASS |
| DW-B195-02 annotation added to method header (line 6576) | PASS |
| NT8 note for `SendCopyWithAtm` / `StartAtmStrategy` added to method header (line 6580) | PASS |

---

## Layer 2 Cross-Check (Engineer Self-Report vs Independent Re-Runs)

| Scan | Engineer Layer 2 | Verifier Layer 3 | Match? |
|------|-----------------|-----------------|--------|
| SCAN-1 lock() | 0 actual lock() calls | 0 results | MATCH |
| SCAN-2 async void | 0 async void declarations | 0 results | MATCH |
| SCAN-3 CYC | CYC=5 (base + 4 branches) | CYC=5 (identical branch count) | MATCH |
| SCAN-4 throw new | 0 matches in file | 0 results | MATCH |
| SCAN-5 return null | Multiple pre-existing; 0 new in DrainThenDispatch | Multiple pre-existing; 0 in DrainThenDispatch | MATCH |
| SCAN-6 ASCII-only | All ASCII confirmed by visual check | All ASCII confirmed independently | MATCH |
| SCAN-7 DateTime | 1 comment-only match; 0 actual calls | 1 comment at L4391; 0 actual calls | MATCH |

**No discrepancies found between Layer 2 (engineer self-report) and Layer 3 (independent verification).**

---

## DNA Rule Check Summary

| Rule | Check | Status |
|------|-------|--------|
| JS-021 (no lock) | GetCloneAtmMode() is volatile read; no lock() | PASS |
| JS-033 (no async void) | DrainThenDispatch is `private void` (synchronous) | PASS |
| JS-066 (CYC <= 8) | DrainThenDispatch CYC=5 (was 4); within limit | PASS |
| JS-001 (no throw in hot path) | No throw statements; early return pattern used | PASS |
| JS-002 (no return null) | Method is void; no null returns possible | PASS |
| ASCII-only | All new identifiers and string literals are 7-bit ASCII | PASS |
| DateTime.UtcNow (no DateTime.Now) | Environment.TickCount used; no DateTime usage | PASS |
| JS-010 (no public constructor on signal struct) | CopySignal.Create() factory method used (not `new CopySignal()`) | PASS |
| NT8: CreateOrder name starts with "PTT-" | SendCopyWithAtm handles "Entry" name internally (line 4680) | PASS |

---

## Architecture Compliance

| Check | Verdict |
|-------|---------|
| Method `DrainThenDispatch` modified (not replaced) | PASS |
| CYC comment in header updated to CYC=5 | PASS |
| `GetCloneAtmMode()` is the approved volatile-read seam (line 745) | PASS |
| `SendCopyWithAtm()` is the approved ATM dispatch path (line 4661) | PASS |
| `follower.Name` used as acctKey in orderId (not `acctKey` which is declared after fast-path return) | PASS |
| Pattern mirrors `SubmitDrainedEntry` (B195-T1) as required by DW-B195-02 | PASS |
| No new methods introduced (T1 scope: one code block replacement only) | PASS |

---

## Spec Coverage

**DW-B195-02 requirement**: Clone mode entry dispatch path must respect ATM mode on ALL dispatch branches, including the fast-path (no drain candidates).

**Verification**: The fast-path block at lines 6609-6635 now:
1. Reads ATM mode via `GetCloneAtmMode()` (volatile, lock-free)
2. Routes to `SendCopyWithAtm` when ATM template is configured (Named branch)
3. Falls through to `SubmitEntryDirect` when no ATM template (Inherit branch -- backwards compatible)

The spec requirement is fully satisfied. No naked `SubmitEntryDirect` call remains in the fast-path.

---

## Violations

**None.**

All 7 scans: 0 violations.
All 8 correctness checks: PASS.
All DNA rules: PASS.
All ticket contract items: satisfied.
Layer 2 vs Layer 3: no discrepancies.

---

## VERDICT

**VERIFY_PASS**

---

*Verifier: ptt-verifier*
*Epic: B196*
*Phase: 4b (Ticket 1 Verification)*
*Return: VERIFY_PASS*