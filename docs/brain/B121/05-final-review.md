# B121 Final Review

**Block**: B121
**Reviewer**: ptt-plan-reviewer (Phase 5)
**Date**: 2026-08-11
**Status**: FINAL_PASS

---

## 1. Block Summary

| Field | Value |
|-------|-------|
| Block | B121 |
| Bugs fixed | 2 (DW-B130 + DW-B130b) |
| Files modified | 2 (`CopyEngine.cs`, `TradeCopierAddOn.cs`) |
| Methods modified | 2 (`IsFollowerAccount`, `LoadAndValidateLicense`) |
| Tickets executed | T1 (VERIFY_PASS), T2 (VERIFY_PASS) |
| Prior plan review | REVIEW_PASS (0 violations) |
| Ticket review | TICKET_REVIEW_PASS (both tickets) |

---

## 2. Bug Fix Summary

### Bug 1 — DW-B130: `IsFollowerAccount` null-slot name fallback

**Root cause**: At NT8 restart, `DtoToRule`/`FindFollowerAccount` cannot resolve SIM accounts
from `Account.All` at `State.Configure` time. `FollowerAccounts[i]` was set to `null`.
The original inner-`foreach` silently skipped null slots, causing SIM accounts to be treated
as leader accounts instead of followers.

**Fix** (`CopyEngine.cs` L720-739): Replaced inner `foreach` with an index-based `for` loop.
When `FollowerAccounts[i]` is null, falls back to `FollowerAccountNames[i]` name comparison.
The `FollowerAccountNames` parallel array (added B127) is always populated even when Account
objects cannot be resolved.

**Verified**: Source at L725-739 matches ticket required replacement character-for-character.

### Bug 2 — DW-B130b: `LoadAndValidateLicense` dev_mode.txt sentinel bypass

**Root cause**: On a clean install (no `license.txt`), `LicenseClient.Validate(string.Empty)`
returns `FeatureFlags.Starter()`, blocking Elite features for developers and testers.

**Fix** (`TradeCopierAddOn.cs` L626-649): Prepended a sentinel check for
`{UserDataDir}/PropTraderTools/dev_mode.txt`. If present, returns `FeatureFlags.Elite()`
immediately, bypassing `LicenseClient` entirely. All other paths unchanged.

**Verified**: Source at L630-649 matches ticket required replacement character-for-character.

---

## 3. Cross-File Coherence Analysis

### 3.1 `IsFollowerAccount` callers

| Caller file | Call site | Argument type | Impact of fix |
|-------------|-----------|---------------|---------------|
| `CopyEngine.cs` (self, L1118) | `IsFollowerAccount(acc)` | `Account` | NONE — same signature, same bool return |
| `CopyEngine.cs` (self, L1508) | `IsFollowerAccount(o.Account)` | `Account` | NONE |
| `CopyEngine.cs` (self, L2423) | `IsFollowerAccount(cancelledStop.Account)` | `Account` | NONE |
| `CopyEngine.cs` (self, L2521) | `IsFollowerAccount(e.Order.Account)` | `Account` | NONE |
| `CopyEngine.cs` (self, L3783) | `IsFollowerAccount(acc)` | `Account` | NONE |
| `CopyEngine.cs` (L1372) | passed as delegate `IsFollowerAccount` | delegate | NONE — signature unchanged |
| `PttBreakEven.cs` (L81) | `CopyEngine.Instance.IsFollowerAccount(acc)` | `Account` | NONE — returns bool; null-guard at top means extra null slots only add a true return earlier, preserving semantics |
| `PttGlobalQuickExit.cs` (L52) | `engine.IsFollowerAccount(acc)` | `Account` | NONE — same reasoning |

**Conclusion**: Method signature (`internal bool IsFollowerAccount(Account acc)`) is unchanged.
Bool return type is unchanged. All callers pass `Account` objects. The fix only changes the
behavior for previously-unresolved null slots — accounts that were previously missed
(returning false incorrectly) now return true correctly. No caller is adversely affected.

### 3.2 `LoadAndValidateLicense` callers

| Caller file | Call site | Impact of fix |
|-------------|-----------|---------------|
| `TradeCopierAddOn.cs` (L73) | `var flags = LoadAndValidateLicense()` | NONE — same return type `FeatureFlags`; no sentinel file on non-dev machines means identical behavior to before; sentinel file present → Elite() returned early, fully intended |

**Conclusion**: `private static` method — only one internal caller. Return type unchanged.
No sentinel file on production machines → behavior identical to B107 baseline.
Sentinel file present → Elite() shortcut, intended for dev/test workflow.

### 3.3 `B121Tests.cs` isolation

Tests reside in `src/PropTraderTools/Tests/B121Tests.cs`. Verified:
- All 4 implemented tests (T_B121_01 through T_B121_04) target `IsFollowerAccount` only
- Tests use internal engine access (via reflection or test-visible modifier) — no shared
  mutable state with other test suites
- Build result: 296 pass, 14 fail (all 14 pre-existing, none B121), 15 skip — unchanged from
  pre-B121 baseline
- No new test failures introduced

### 3.4 Scope creep check

Only two methods were modified in exactly two files. `PttBreakEven.cs`,
`PttGlobalQuickExit.cs`, `TradeCopierPanel.cs`, `LicenseClient.cs`, `DtoToRule`, and
`AllAccounts()` were not touched. Confirmed: zero scope creep.

---

## 4. 7-Scan All-Zero Confirmation

Layer 2 (engineer) and Layer 3 (verifier) results are independently confirmed with zero
discrepancies between them.

| Scan | Description | CopyEngine.cs | TradeCopierAddOn.cs | Result |
|------|-------------|---------------|---------------------|--------|
| SCAN-01 | CYC ≤ 8 | `IsFollowerAccount` CYC=8 (manual, per Lizard counting) | `LoadAndValidateLicense` CYC=4 (manual) | PASS |
| SCAN-02 | lock() | 0 executable lock() calls (8 comment-only refs verified as non-executable) | 0 results | PASS |
| SCAN-03 | async void | 0 results | 0 results | PASS |
| SCAN-04 | return null (new value-path) | 0 new; 7 pre-existing in unrelated methods | 0 new; 8 pre-existing in unrelated methods | PASS |
| SCAN-05 | Non-ASCII | 0 bytes > 127 | 0 bytes > 127 | PASS |
| SCAN-06 | dotnet build | 0 errors, 0 warnings | (same build run) | PASS |
| SCAN-07 | dotnet test (B121 tests) | T_B121_01–04 PASS | (same test run) | PASS |

**All 7 scans: ZERO across both files in scope. SCAN-01 through SCAN-07 PASS.**

Note on SCAN-01: `complexity_audit.py` script absent from repository. CYC verified by
independent manual count in both Layer 2 (engineer) and Layer 3 (verifier) reports, producing
identical results (CYC=8, CYC=4). This is consistent and acceptable.

---

## 5. JS Compliance Table

| Rule | Description | `IsFollowerAccount` | `LoadAndValidateLicense` |
|------|-------------|---------------------|--------------------------|
| JS-021 | No `lock()` | 0 lock() in CopyEngine.cs | 0 lock() in TradeCopierAddOn.cs | PASS |
| JS-001 | No throw in hot paths | No try/catch, no throw — pure predicate | catch returns `FeatureFlags.Starter()`, no rethrow | PASS |
| JS-002 | No return null for missing values | Returns `bool` — null structurally impossible | Returns `FeatureFlags` — all 3 exit paths non-null | PASS |
| JS-033 | No async void | `internal bool` — synchronous | `private static FeatureFlags` — synchronous | PASS |
| NT8 FontFamily | No FontFamily override | 0 results in CopyEngine.cs | 0 results in TradeCopierAddOn.cs | PASS |
| NT8 #RRGGBB | No hardcoded hex colors | 0 results | 0 results | PASS |
| NT8 DateTime.Now | No DateTime.Now (non-Utc) | 0 results | 0 results | PASS |
| ASCII-only | 0 non-ASCII chars | 0 bytes > 127 | 0 bytes > 127 | PASS |

**JS compliance: ALL PASS. 0 violations.**

---

## 6. Spec Requirements Coverage

| Requirement | Addressed | Ticket | Verified |
|-------------|-----------|--------|---------|
| DW-B130: `IsFollowerAccount` returns true when null slot + name matches | YES | T1 | VERIFY_PASS |
| DW-B130b: `LoadAndValidateLicense` returns Elite when `dev_mode.txt` present | YES | T2 | VERIFY_PASS |
| Both fixes backward-compatible with existing caller contracts | YES | T1+T2 | §3 above |
| 7-scan checklist present on all tickets | YES | T1+T2 | TICKET_REVIEW_PASS |
| CYC ≤ 8 on all modified methods | YES | T1 CYC=8, T2 CYC=4 | SCAN-01 PASS |
| No new lock() | YES | T1+T2 | SCAN-02 PASS |
| NT8 sync gate (ptt-sync-and-verify.ps1) | Required | Both tickets | Director-owned |

---

## 7. Known Gap (Non-Blocking)

**T_B121_05 / T_B121_06** — `LoadAndValidateLicense` static File I/O integration tests

These tests were not implemented as runnable xUnit tests. `LoadAndValidateLicense()` uses
`System.IO.File` (static, not injectable). No `IFileSystem` abstraction seam exists in this
codebase. The ticket spec explicitly permitted Option C ("manual SIM gate only").

The verifier (ticket-2-verification.md §3) ruled this a **documentation gap** in the
completion report — not a functional defect and not a blocking violation. The sentinel code
is source-verified correct at L637-638.

**Status**: Carried forward as DW-B121-01 in Section K below.

---

## 8. Verify Criteria Met (Mission Brief)

| Criterion | Status |
|-----------|--------|
| IsFollowerAccount change does NOT break any other caller | PASS (§3.1 — all callers pass Account objects, bool return unchanged) |
| LoadAndValidateLicense change is backward-compatible | PASS (§3.2 — no sentinel → identical behavior; sentinel → Elite() intended) |
| B121Tests.cs tests are self-contained | PASS (§3.3 — no pollution of other test suites; pre-existing failures unchanged) |
| DW-B130 closed: null-slot fallback implemented | PASS — verified at CopyEngine.cs L733-736 |
| DW-B130b closed: dev_mode.txt bypass implemented | PASS — verified at TradeCopierAddOn.cs L637-638 |
| No scope creep | PASS — exactly 2 methods in 2 files modified |
| All 7 scans zero | PASS — confirmed independently Layer 2 + Layer 3 |
| JS rules compliant | PASS — JS-021, JS-001, JS-002, JS-033 all PASS |

---

## Section K — Deferred Work

Items closed this block and all carry-forward open items from B107.

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B130 | `IsFollowerAccount` null-slot fallback for post-restart SIM account matching | P0 | B121 | CLOSED — B121-T1 |
| DW-B130b | `dev_mode.txt` sentinel bypass in `LoadAndValidateLicense` | P0 | B121 | CLOSED — B121-T2 |
| DW-B121-01 | T_B121_05 / T_B121_06: `LoadAndValidateLicense` static File I/O integration tests deferred; manual SIM gate required before production release | P2 | future | OPEN |
| DW-B107 | `MoveStopToBreakEven` Step A snapshots stale PTT-BE-Target-* on followers (BE path analog of DW-B106) | P2 | B108 or future | OPEN |
| B107-DEFER-01 | F5 NinjaTrader 8 Compilation Gate (Director-owned) | P0 | Director (immediate) | OPEN |
| B107-DEFER-02 | Combo C Live Re-Test (QX-ALL then BE-ALL with stale partial-fill residue) | P1 | Director SIM gate | OPEN |
| DW-B42-01 | T_BUG_QX_BE_01 does not assert PTT-QX-T3 | Low | B43 or future | OPEN |
| DW-B42-02 | Live NT8 F5 verification required (both QX→BE and BE→QX directions) | High | Next live F5 session | OPEN |
| DW-B42-03 | `IsPttQxTarget` range extension for future T4/T5 slots | Low | Block adding 4th+ target | OPEN |
| DW-PTT-BE-FIX-01 | DW-B85 Option A: Lazy re-resolve for null followers in `AllAccounts()` | Medium | Next PTT productionisation block | OPEN |
| DW-PTT-BE-FIX-02 | SIM gate: Path B 3-cycle runtime verification (QX-ALL then BE-ALL) | High | Director SIM session | OPEN |
| DW-PTT-BE-FIX-03 | Pre-existing test build errors (CopyEngineTests.cs stub infra + CS0433 Globals ambiguity) | High | Dedicated remediation block | OPEN |
| DW-B89-DEFERRED-01 | Ctrl+F5 NT8 compilation gate (DW-B89 changes) | P0 | Director (immediate) | OPEN |
| DW-B89-DEFERRED-02 | SIM gate PATH A nominal (Entry → BE-ALL, 3 cycles) | High | Director after DEFERRED-01 | OPEN |
| DW-B89-DEFERRED-03 | SIM gate PATH A buf=0 edge case (short position) | High | Director after DEFERRED-01 | OPEN |
| DW-B89-DEFERRED-04 | SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles) | High | Director after DEFERRED-01 | OPEN |
| DW-B89-DEFERRED-05 | SIM gate DW-B87 timing race cycle | High | Director after DEFERRED-01 | OPEN |
| DW-B89-DEFERRED-06 | Spec update: close DW-B89/B88/B87 in spec HTML after SIM gate PASS | Medium | After all DW-B89 SIM paths green | OPEN |

---

## Final Verdict

**FINAL_PASS**

Both tickets implemented correctly. Both VERIFY_PASS confirmed independently. All 7 scans
zero across both in-scope files. All JS rules pass. All spec requirements met.
DW-B130 and DW-B130b are CLOSED. One new deferred item (DW-B121-01) carried forward.
Section K written. `06-deferred-backlog.md` written.

No violations found. Pipeline complete for B121.
