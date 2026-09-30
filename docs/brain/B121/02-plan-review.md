# B121 Plan Review

**Status**: REVIEW_PASS
**Reviewer**: ptt-plan-reviewer
**Date**: 2026-08-11
**Plan reviewed**: docs/brain/B121/02-architecture-plan.md

---

## Verdict

**REVIEW_PASS** — 0 violations. All 13 checklist items PASS.

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|------------|--------------|
| DW-B130: IsFollowerAccount null-slot name fallback | YES | §3 (full root cause + fix) |
| DW-B130b: dev_mode.txt sentinel bypass | YES | §4 (full root cause + fix) |
| Fix 1 matches mission brief verbatim | YES | §3.3 |
| Fix 2 matches mission brief verbatim | YES | §4.2 |
| 2 tickets, 1 file each | YES | §6, §11 |
| Tests per ticket | YES | §10 |
| 7-scan checklist | YES | §7 |
| Out-of-scope items listed | YES | §8 |
| Dependencies confirmed | YES | §9 |
| NT8 sync requirement | YES | §12 |

---

## Checklist Results

### Item 1 — Plan correctly identifies both bugs and their root causes

**PASS.** §3.1 correctly identifies that `DtoToRule`/`FindFollowerAccount` produces null slots in
`FollowerAccounts[]` when SIM accounts are absent from `Account.All` at `State.Configure` time, and
that the existing `IsFollowerAccount` inner-foreach silently skips those null slots. §4.1 correctly
identifies that on a clean install `LicenseClient.Validate(string.Empty)` returns `Starter()` and
blocks Elite features, with no sentinel path to bypass the call for dev/test workflows.

### Item 2 — Plan proposes the exact fixes from the mission brief

**PASS.** §3.3 and §4.2 reproduce the proposed code verbatim, with matching method signatures,
logic structure, and comment annotations. No deviations detected.

### Item 3 — CYC proof for IsFollowerAccount ≤ 8

**PASS.** §3.4 enumerates 7 decision points from base=1 to CYC=8. The table and footnote correctly
explain that the final `&&` operand in the compound `if` (row #8) does not add a separate decision
node under Lizard's counting, holding CYC at 8. CYC = 8 is exactly at the limit (≤ 8). The plan
accurately notes that SCAN-01 (`complexity_audit.py`) will empirically confirm at implementation
time, which is the correct posture for a boundary-value CYC claim.

### Item 4 — CYC proof for LoadAndValidateLicense ≤ 8

**PASS.** §4.3 enumerates 3 decision points: try/catch (1), `if (File.Exists(devMode))` (2),
ternary `File.Exists(licenseTxt) ? ... : ...` (3). CYC = 4. Well within the ≤ 8 limit.

### Item 5 — JS-021: no new lock() usage

**PASS.** §3.5 and §4.4 both explicitly state no `lock()` is added or needed. §2 (Rules Catalog
Gate) confirms JS-021 status. The proposed code contains no `lock(` patterns. Source-confirmed:
existing `IsFollowerAccount` at [`CopyEngine.cs`](src/PropTraderTools/CopyEngine.cs:723) and
`LoadAndValidateLicense` at [`TradeCopierAddOn.cs`](src/PropTraderTools/TradeCopierAddOn.cs:629)
contain no locks; neither fix introduces any.

### Item 6 — JS-001: no throw in hot paths

**PASS.** `IsFollowerAccount` has no try/catch and no `throw` — it is a pure predicate returning
bool. `LoadAndValidateLicense` uses a `catch (Exception)` that returns `FeatureFlags.Starter()`
(a value-type factory call), not re-throws. The plan correctly calls this out at §2 and in the
inline comment in §4.2.

### Item 7 — JS-002: no new return null in value paths

**PASS.** `IsFollowerAccount` returns `bool` — null return is structurally impossible.
`LoadAndValidateLicense` returns `FeatureFlags` — all three exit paths return non-null value
instances (`Elite()`, `LicenseClient.Validate(key)`, `Starter()`). §2 confirms JS-002 PASS.

### Item 8 — JS-033: no async void

**PASS.** Both methods are synchronous (`bool` and `FeatureFlags` return types). §2 and §3.5/§4.4
confirm no async surface is introduced.

### Item 9 — Ticket scope: exactly 2 tickets

**PASS.** §6 defines T1 (CopyEngine.cs / `IsFollowerAccount`) and T2 (TradeCopierAddOn.cs /
`LoadAndValidateLicense`). §11 (File Split Validation) confirms each ticket touches exactly one
file and one method, with no cross-file contamination.

### Item 10 — 7-scan checklist present in plan

**PASS.** §7 contains all 7 scans with exact command lines:
- SCAN-01 CYC (`complexity_audit.py`)
- SCAN-02 lock() grep
- SCAN-03 async void grep
- SCAN-04 return null grep
- SCAN-05 ASCII grep
- SCAN-06 build (`dotnet build`)
- SCAN-07 test (`dotnet test`)

### Item 11 — Out-of-scope items correctly listed

**PASS.** §8 lists 6 items with rationale: PttGlobalQuickExit.cs, PttQuickExit.cs,
TradeCopierPanel.cs, LicenseClient.TryRemoteValidate, AllAccounts() B127 lazy-resolve,
and DtoToRule/LoadRules. Each exclusion is justified. The list is consistent with the
mission brief's two-file scope.

### Item 12 — FollowerAccountNames dependency verified

**PASS.** §3.2 and §9 both confirm the field. Source-verified:
[`CopyEngine.cs:423`](src/PropTraderTools/CopyEngine.cs:423) —
`internal readonly string[] FollowerAccountNames;` (added B127).
[`CopyEngine.cs:4375`](src/PropTraderTools/CopyEngine.cs:4375) — `dto.FollowerAccountNames`
passed as 8th argument to `CopyRule.Create` in `DtoToRule`, confirming names are always
populated for null-slot scenarios.

### Item 13 — FeatureFlags.Elite() dependency verified

**PASS.** §4.5 and §9 both confirm the factory. Source-verified:
[`FeatureFlags.cs:24`](src/PropTraderTools/FeatureFlags.cs:24) —
`public static FeatureFlags Elite() => new(true, true, true, true, true, true, true);`
Sealed record, static factory, returns all-true `FeatureFlags`. No null path.

---

## Violation Log

None.

---

## Notes for Engineer

1. CYC=8 for `IsFollowerAccount` is a boundary value. SCAN-01 (`complexity_audit.py`) **must**
   be run post-implementation and its output recorded in `ticket-1-completion.md`. If the tool
   reports CYC=9, the ticket is incomplete — extract the inner compound condition into a helper.

2. The `catch (Exception)` in `LoadAndValidateLicense` intentionally swallows all exceptions
   (I/O errors, path errors, null refs from `Globals.UserDataDir`). This is the existing
   behaviour, not a new pattern. Do not change it.

3. `T2_LoadAndValidateLicense_NoDevMode_NoLicenseTxt_DelegatesToLicenseClient` requires a test
   seam for `LicenseClient.Validate` or isolation of the static call. If `LicenseClient` is not
   mockable, the assertion should verify the result matches `FeatureFlags.Starter()` (which is
   what `Validate(string.Empty)` returns in the absence of a valid key), not verify the call
   itself. The plan's intent is correct; implementation detail left to engineer.

---

**REVIEW_PASS — Proceed to ticket generation (Phase 3).**
