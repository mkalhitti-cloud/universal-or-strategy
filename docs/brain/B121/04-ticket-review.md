# Ticket Review: B121

**Reviewer**: ptt-ticket-reviewer  
**Phase**: 3.5 — Pre-Engineer Gate  
**Date**: 2026-08-11  
**Tickets reviewed**: `docs/brain/B121/04-tickets.md`  
**Plan reviewed**: `docs/brain/B121/02-architecture-plan.md` (REVIEW_PASS)  
**Plan review read**: `docs/brain/B121/02-plan-review.md` (REVIEW_PASS, 0 violations)  

---

## T1 — IsFollowerAccount null-slot name fallback

### Traceability
**PASS.**  
Ticket header cites spec requirement DW-B130. Plan §3 (full root cause + fix) and §6 (ticket
scope table) both map this ticket to DW-B130. No phantom work. No missing plan items for this
ticket.

### JS Pre-Check
**PASS.**  
- JS-021 (no `lock()`): proposed code contains no `lock(` pattern. Pure index-loop predicate.  
- JS-002 (no `return null`): method returns `bool`; null return is structurally impossible.  
- JS-001 (no throw in hot path): method has no try/catch and no `throw` statement.  
- JS-033 (no async void): method is synchronous — `internal bool` return type.  

### CYC Pre-Check
**PASS.**  
Ticket claims CYC = 8 and provides inline comment enumerating 8 decision points:
`null guard(1) + foreach(2) + for-i(3) + f-not-null(4) + f-null(5) + names-not-null(6) +
i-in-range(7) + name-match(8)`. Plan §3.4 provides the full CYC proof table (base=1, 7
additional decision points). The final `&&` operand in the compound `if` (row #8 in the plan
table) does not add a separate decision node under Lizard's counting, holding CYC at 8.
CYC = 8 is exactly at the ≤8 limit. SCAN-01 (`complexity_audit.py`) will empirically confirm
at implementation time — the correct posture for a boundary-value CYC claim.

### NT8 Check
**PASS.**  
No NT8 API is introduced or referenced in this ticket. `IsFollowerAccount` is a pure C#
predicate; `Account.Name` is a standard property used identically in the existing method.
No lifecycle constraint, no `Account.All` call, no `DateTime.Now`, no UI thread concern.

### Test Coverage
**PASS.**  
Four `[Fact]` tests specified with full method names and assertions:

| Test name | Asserts | Branch covered |
|-----------|---------|----------------|
| `T_B121_01_IsFollowerAccount_NullSlotNameMatch_ReturnsTrue` | `== true` | NEW: null-slot name fallback path |
| `T_B121_02_IsFollowerAccount_NullSlotNameMismatch_ReturnsFalse` | `== false` | null slot, wrong name — no false positive |
| `T_B121_03_IsFollowerAccount_ResolvedFollower_ReturnsTrue` | `== true` | original f != null path preserved |
| `T_B121_04_IsFollowerAccount_NullArg_ReturnsFalse` | `== false` | top-level null guard preserved |

All new branches (the bug fix) and all preserved existing branches are exercised.

### Scan Checklist
**PASS.**  
All 7 scans present in the T1 section with exact commands and required results:

| Scan | Command | Required result |
|------|---------|-----------------|
| SCAN-01 CYC | `python scripts/complexity_audit.py` | `IsFollowerAccount` CYC ≤ 8 |
| SCAN-02 lock() | `grep -rn "lock(" src/PropTraderTools/CopyEngine.cs` | 0 results |
| SCAN-03 async void | `grep -rn "async void " src/PropTraderTools/CopyEngine.cs` | 0 new results |
| SCAN-04 return null | `grep -rn "return null;" src/PropTraderTools/CopyEngine.cs` | 0 new value-path nulls |
| SCAN-05 ASCII | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 results |
| SCAN-06 Build | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors |
| SCAN-07 Tests | `dotnet test src/PropTraderTools/PropTraderTools.csproj` | all passing |

### File Routing
**PASS.**  
Ticket file: `src/PropTraderTools/CopyEngine.cs`. Correct Wave workspace
(`c:\WSGTA\universal-or-strategy`). No Director workspace path for any `.cs` artifact.

### VERDICT: TICKET_REVIEW_PASS

---

## T2 — dev_mode.txt sentinel bypass in LoadAndValidateLicense

### Traceability
**PASS.**  
Ticket header cites spec requirement DW-B130b. Plan §4 (full root cause + fix) and §6 (ticket
scope table) both map this ticket to DW-B130b. No phantom work. No missing plan items for this
ticket.

### JS Pre-Check
**PASS.**  
- JS-021 (no `lock()`): proposed code contains no `lock(` pattern. Static file-I/O method with
  no shared mutable state.  
- JS-001 (no throw in hot path): `catch (Exception)` block returns `FeatureFlags.Starter()` — a
  value-type static factory call. No rethrow. No new `throw` statement introduced.  
- JS-002 (no `return null`): all three exit paths return non-null `FeatureFlags` instances
  (`Elite()`, `LicenseClient.Validate(key)`, `Starter()`).  
- JS-033 (no async void): method is `private static FeatureFlags` — fully synchronous.  

### CYC Pre-Check
**PASS.**  
Ticket claims CYC = 4 with inline comment: `try-enter(1) + devMode.Exists(2) +
licenseTxt.Exists(3) + catch(4)`. Plan §4.3 provides the proof table (base=1, 3 decision
points: try/catch, if devMode.Exists, ternary licenseTxt.Exists). CYC = 4 — well within
the ≤8 limit.

### NT8 Check
**PASS.**  
- `NinjaTrader.Core.Globals.UserDataDir` — already referenced in the existing method body at
  L633. No new NT8 API surface introduced.  
- `System.IO.File.Exists` and `System.IO.File.ReadAllText` — already present in the existing
  method. Same surface, same constraints.  
- Called from `State.Configure` (startup path, not a hot path) — confirmed by plan §4.4.
  File I/O is safe in this lifecycle position.  
- No `Account.All` call, no `sealed` on `TradeCopierWindow`, no `FontFamily`, no hardcoded
  hex color, no `CreateOrder`, no `DateTime.Now`.  

### Test Coverage
**PASS.**  
Two `[Fact]` tests specified with full method names and assertions:

| Test name | Asserts | Branch covered |
|-----------|---------|----------------|
| `T_B121_05_LoadAndValidateLicense_DevModePresent_ReturnsElite` | Elite tier returned | NEW: sentinel path — dev_mode.txt present |
| `T_B121_06_LoadAndValidateLicense_NoDevMode_NoLicense_DelegatesToLicenseClient` | Delegates to LicenseClient / returns Starter result | Original paths preserved when sentinel absent |

**Note on T_B121_05 (Option C):** The ticket explicitly acknowledges that
`LoadAndValidateLicense` uses `static System.IO.File`, which requires a test seam
(`IFileSystem` injection, reflection, or real temp directory). Three options are provided in
order of preference. Option C (last resort) permits marking T_B121_05 as "manual SIM gate
only" with rationale, paired with a pure-logic unit test for `FeatureFlags.Elite().IsElite ==
true`. This is an explicitly permitted fallback — not a test coverage gap — and matches the
plan reviewer's Note 3 verbatim. No violation.

### Scan Checklist
**PASS.**  
All 7 scans present in the T2 section with exact commands and required results:

| Scan | Command | Required result |
|------|---------|-----------------|
| SCAN-01 CYC | `python scripts/complexity_audit.py` | `LoadAndValidateLicense` CYC ≤ 8 |
| SCAN-02 lock() | `grep -rn "lock(" src/PropTraderTools/TradeCopierAddOn.cs` | 0 results |
| SCAN-03 async void | `grep -rn "async void " src/PropTraderTools/TradeCopierAddOn.cs` | 0 new results |
| SCAN-04 return null | `grep -rn "return null;" src/PropTraderTools/TradeCopierAddOn.cs` | 0 new value-path nulls |
| SCAN-05 ASCII | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/TradeCopierAddOn.cs` | 0 results |
| SCAN-06 Build | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 errors |
| SCAN-07 Tests | `dotnet test src/PropTraderTools/PropTraderTools.csproj` | all passing |

### File Routing
**PASS.**  
Ticket file: `src/PropTraderTools/TradeCopierAddOn.cs`. Correct Wave workspace
(`c:\WSGTA\universal-or-strategy`). No Director workspace path for any `.cs` artifact.

### VERDICT: TICKET_REVIEW_PASS

---

## Cross-Ticket Checks

| Check | Result |
|-------|--------|
| T1 touches CopyEngine.cs only — no cross-file contamination | PASS |
| T2 touches TradeCopierAddOn.cs only — no cross-file contamination | PASS |
| T1 and T2 have independent acceptance criteria | PASS |
| Both tickets share `B121Tests.cs` as output artifact (T2 appends to T1's file) | PASS |
| NT8 sync gate present after both tickets (scripts\ptt-sync-and-verify.ps1) | PASS |
| Execution order: T1 and T2 are independent — parallel execution permitted | PASS |
| Current code in tickets verified against actual source files | PASS — CopyEngine.cs L722-732 and TradeCopierAddOn.cs L626-646 match ticket descriptions exactly |
| Required code in tickets matches plan §3.3 / §4.2 verbatim | PASS |
| All spec requirements in scope covered: DW-B130 (T1), DW-B130b (T2) | PASS |
| No duplicate spec coverage across tickets | PASS |
| All dependencies confirmed in plan §9 (FollowerAccountNames field, FeatureFlags.Elite()) | PASS |

---

## Violation Log

**None.**

---

## Summary

Both tickets are complete, traceable, JS-compliant, NT8-safe, CYC-bounded, and carry all
required test specifications and 7-scan checklists. Current code in both tickets matches the
actual source files exactly. Required code matches the architecture plan verbatim. No phantom
work, no missing coverage, no rule violations.

---

## Overall: TICKET_REVIEW_PASS

**Engineer may proceed.** Execute T1 and T2 in any order (or in parallel).  
T1 output artifact: `src/PropTraderTools/CopyEngine.cs` + `src/PropTraderTools/Tests/B121Tests.cs`  
T2 output artifact: `src/PropTraderTools/TradeCopierAddOn.cs` + `src/PropTraderTools/Tests/B121Tests.cs` (appended)  
Both tickets must complete before running `scripts\ptt-sync-and-verify.ps1` and pressing F5 in NinjaTrader 8.
