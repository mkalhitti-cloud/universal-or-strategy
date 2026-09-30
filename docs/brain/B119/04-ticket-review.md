# B119 Ticket Review

**Reviewer**: ptt-ticket-reviewer
**Block**: B119
**Defect**: DW-B128
**Ticket file**: `docs/brain/B119/04-tickets.md`
**Plan file**: `docs/brain/B119/02-architecture-plan.md`
**Plan review**: `docs/brain/B119/02-plan-review.md` (REVIEW_PASS 2026-08-27)
**Review date**: 2026-08-27
**Phase**: 3.5 -- Ticket Review

---

## Review Result: TICKET_REVIEW_PASS

---

## Checklist Results

### A. Traceability

| Item | Result | Note |
|------|--------|------|
| DW-B128 defect ID present in ticket | **PASS** | Section 1 row 1: `DW-B128` cited explicitly. |
| Spec section `specs/002-trade-copier-spec.html#section-dw-b128` referenced | **PASS** | Section 1 row 2: exact URL fragment cited. Also cites `#section-dw-b122` and `#section-b8`. |
| Plan sections 3.1-3.4 referenced | **PASS** | Section 1 row 5: `docs/brain/B119/02-architecture-plan.md Sections 3.1-3.4`. |
| Acceptance criteria cover all 4 guard scenarios | **PASS** | AC1 = first entry (no prior direction); AC2 = same direction repeat; AC3 = reversal + follower flat (guard fires); AC4 = reversal + follower has open position (guard does NOT fire). All 4 required scenarios present. |
| Per-follower independence stated | **PASS** | AC5 explicitly states each follower is evaluated independently within a single dispatch call. |
| Dictionary-update-after-loop invariant stated as acceptance criterion | **PASS** | AC6 explicitly states `_lastLeaderDirection[instr.FullName] = currentAction` executes once AFTER the foreach loop, not inside it. |

**Section A verdict**: PASS

---

### B. Method Signatures

| Item | Result | Note |
|------|--------|------|
| `_lastLeaderDirection` field: `ConcurrentDictionary<string, OrderAction>` | **PASS** | Section 3a declares `private readonly ConcurrentDictionary<string, OrderAction> _lastLeaderDirection = new ConcurrentDictionary<string, OrderAction>();`. Not Dictionary, not SortedDictionary, not a plain field. |
| `IsReversalToFlatFollower` signature: `internal static bool` with 3-param signature | **PASS** | Section 3b declares `internal static bool IsReversalToFlatFollower(OrderAction currentAction, OrderAction lastAction, bool followerIsFlat)`. Exact match to plan Section 3.2. |
| `IsFlat` cited at correct line number | **PASS** | Ticket Section 3d cites L3302. Live source confirmed: `private static bool IsFlat(NinjaTrader.Cbi.Position pos)` at `CopyEngine.cs:3302`. Exact match. |
| `FindPosition` cited at correct line number | **PASS** | Ticket Section 3d cites L3348. Live source confirmed: `private Position FindPosition(Account acc, Instrument instrument)` at `CopyEngine.cs:3348`. Exact match. |
| `DispatchCopy` line range verified | **PASS** | Ticket cites L1784 for signature, L1824-L1868 for loop. Live source: `private void DispatchCopy(Order order, CopyRule rule)` at L1784; `int idx = 0;` at L1824; `foreach` at L1825; `}` closing loop at L1868. All match. |
| Guards L1827-L1836 verified | **PASS** | Ticket cites `if (acc == null)` at L1827-L1831 and `if (!PassesDailyCapCheck(acc))` at L1832-L1836. Live source confirms both guards at exactly those line ranges. |
| `IsReversalToFlatFollower` placement after `IsFlat` L3305 | **PASS** | `IsFlat` body closes `}` at L3305 in live source. Ticket Step 2 instructs insertion after L3305. Correct. |

**Section B verdict**: PASS

---

### C. Implementation Instructions

| Item | Result | Note |
|------|--------|------|
| Step 1: field placement specified | **PASS** | Step 1 instructs placement "Near the other ConcurrentDictionary fields in the CopyEngine class body". Verification command `grep -c "ConcurrentDictionary"` specified. |
| Step 2: helper body specified as single return expression | **PASS** | Step 2 body is `return currentAction != lastAction && followerIsFlat;`. Single return, no branches beyond the && expression. CYC=2 comment present. |
| Step 3: TryGetValue before loop | **PASS** | Step 3a inserts `TryGetValue` with `out OrderAction lastAction` before the `foreach`, after `int baseQty = ...` line (L1821). |
| Step 3: check inside loop, update after loop | **PASS** | Step 3b places reversal guard inside the foreach. Step 3c places `_lastLeaderDirection[instr.FullName] = currentAction` after the foreach closing brace. |
| Log string `[PTT-COPY-GUARD] skip reversal entry: {acc.Name} {instr.FullName} follower flat` | **PASS** | Step 3b contains exact log string using `NinjaTrader.Code.Output.Process` with `PrintTo.OutputTab1`. String is 7-bit ASCII. |
| `continue` statement used to skip follower | **PASS** | Step 3b ends the reversal guard block with `idx++; continue;`. Not `return`. |
| Branch-merge technique documented with CYC budget math | **PASS** | Step 3b documents removal of L1827-L1831 and L1832-L1836 and their replacement with `if (acc == null || !PassesDailyCapCheck(acc))`. Comment states "Compound || = 1 McCabe branch (per project convention L1802). CYC budget: replaces 2 separate branches with 1 compound, freeing one slot for the guard below." |

**Section C verdict**: PASS

---

### D. 7-Scan Checklist (DEFENSE IN DEPTH -- non-negotiable)

| Scan | Result | Command verified |
|------|--------|-----------------|
| SCAN-01: lock() audit | **PASS** | `grep -r "lock(" src/PropTraderTools/CopyEngine.cs` -- expected ZERO new lock() matches. Present in ticket Section 5 verbatim. |
| SCAN-02: async void audit | **PASS** | `grep -rn "async void " src/PropTraderTools/CopyEngine.cs` -- expected ZERO. Present in ticket Section 5 verbatim. |
| SCAN-03: return null audit | **PASS** | `grep -rn "return null;" src/PropTraderTools/CopyEngine.cs` -- expected ZERO new sites. Pre-existing FindPosition L3353 explicitly exempted by ticket. Present verbatim. |
| SCAN-04: throw audit | **PASS** | `grep -rn "throw " src/PropTraderTools/CopyEngine.cs` -- expected ZERO new throw statements. Present verbatim. |
| SCAN-05: ASCII audit | **PASS** | `powershell -Command "$f='src/PropTraderTools/CopyEngine.cs'; [regex]::Matches([System.IO.File]::ReadAllText($f), '[^\x00-\x7F]').Count"` -- expected ZERO. Full PowerShell command present. |
| SCAN-06: CYC audit | **PASS** | `python scripts/complexity_audit.py src/PropTraderTools/CopyEngine.cs` with thresholds: DispatchCopy <= 8 (budget math stated); IsReversalToFlatFollower <= 4 (CYC=2 by McCabe strict, upper bound 3 by tool). Both thresholds explicitly stated. |
| SCAN-07: build audit | **PASS** | `dotnet build src/PropTraderTools/PropTraderTools.csproj` -- expected ZERO errors, ZERO new warnings, exit code 0. Present verbatim. |

All 7 scans present with exact commands and expected outcomes. Defense-in-depth contract is complete.

**Section D verdict**: PASS

---

### E. Test Coverage

| Item | Result | Note |
|------|--------|------|
| At least 6 [Fact] tests for `IsReversalToFlatFollower` | **PASS** | 11 [Fact] tests total: A1-A6 (6 pure unit), B1-B3 (3 dictionary invariant), C1-C2 (2 BuyToCover/SellShort variants). Requirement exceeded. |
| All 4 direction combos covered | **PASS** | A1 (Buy,Buy,flat=F); A2 (Sell,Sell,flat=F); A3 (Sell,Buy,flat=T returns true); A4 (Buy,Sell,flat=T returns true). All 4 covered. |
| Flat and not-flat variants | **PASS** | A3/A4 = reversal + flat = fires. A5 = reversal + not-flat = does not fire. Both covered. |
| Absent-key / first-entry test | **PASS** | A6 tests the "first entry" invariant at unit level (same-action workaround documented). B1 tests `TryGetValue` returns false on absent key directly. Both present. |
| xUnit framework specified | **PASS** | Step 4 intro: "Framework: xUnit only. NEVER NUnit or MSTest." |
| Test file path specified | **PASS** | Step 4 intro and Section 8: `src/PropTraderTools/Tests/B119Tests.cs`. Correct workspace path. |
| Zero NT8 API calls in tests | **PASS** | Step 4 test isolation notes: "Zero NT8 API calls in any test. No Account, Order, or Instrument objects used." |
| InternalsVisibleTo or same-assembly access for internal static method | **PASS** | Step 4 notes: "call `CopyEngine.IsReversalToFlatFollower(...)` directly (`internal static`, accessible from the test assembly via `InternalsVisibleTo` or same assembly)." |

**WARN (cosmetic, not a failure)**: Test [Fact] method names in the ticket (e.g., `T_IsReversalToFlatFollower_SameDirection_Buy_NotFired`) differ from the names in the plan (e.g., `T_IsReversalToFlat_BuyBuy_Flat_ReturnsFalse`). The ticket's names are the engineering contract; the plan's names are reference-only. The scenarios covered are identical. No action required; architect should align plan names in a future revision for consistency.

**Section E verdict**: PASS

---

### F. JS Rules Pre-Check

| Rule | Result | Evidence |
|------|--------|----------|
| JS-021: No lock() -- ConcurrentDictionary confirmed | **PASS** | `_lastLeaderDirection` declared as `ConcurrentDictionary<string, OrderAction>`. AC7 explicitly states "Zero `lock()` statements introduced anywhere." Section 7 compliance table row JS-021: "COMPLIANT". |
| JS-001: No throw in helper or DispatchCopy modification | **PASS** | `IsReversalToFlatFollower` body is `return currentAction != lastAction && followerIsFlat;` -- single return expression, no throw path. Section 7 compliance table row JS-001: "COMPLIANT". |
| JS-002: No return null for missing values | **PASS** | TryGetValue with `out` param is used (correct pattern). No new `return null` sites introduced. Section 7: "COMPLIANT". |
| JS-033: No async void | **PASS** | No new async methods introduced. Section 7: "COMPLIANT -- no new async methods introduced." |
| CYC <= 8 for DispatchCopy | **PASS** | Branch merge documented: L1827-1831 + L1832-1836 merged into compound `||` (1 McCabe branch per project convention). Revised 8-branch table in Step 3b matches plan Section 3.3 table. |
| CYC <= 4 for IsReversalToFlatFollower | **PASS** | Section 3b comment: "CYC=2 (one && expression in a single return)". Section 7: CYC <= 8 row confirms IsReversalToFlatFollower=2. |
| ASCII-only: log strings and identifiers | **PASS** | SCAN-05 command verifies. Log string `[PTT-COPY-GUARD] skip reversal entry: ... follower flat` -- all 7-bit ASCII. New identifiers `_lastLeaderDirection`, `IsReversalToFlatFollower`, `hasLastDirection`, `followerIsFlat`, `currentAction`, `lastAction`, `instr` -- all ASCII-only. Section 5 SCAN-05 states this explicitly. |

**Section F verdict**: PASS

---

### G. NT8 API Claims

| Item | Result | Note |
|------|--------|------|
| No new NT8 API calls introduced | **PASS** | Section 6 NT8 API Claims table confirms. `IsFlat` and `FindPosition` are existing in-file helpers already called throughout CopyEngine. `ConcurrentDictionary` is .NET BCL, not NT8. |
| `OrderAction` enum source verified | **PASS** | Section 1 row: "NT8 OrderAction enum -- `docs/standards/NT8_FULL_REFERENCE.md` L854-859". Section 6 row: "CONFIRMED". |
| No NT8 mock required for unit tests | **PASS** | Step 4: "Zero NT8 API calls in any test." Part B tests use `new ConcurrentDictionary<string, OrderAction>()` directly -- no CopyEngine instance. Part A/C tests use the static helper with `OrderAction` enum values only. |
| `OrderAction` enum is value type -- usable in tests without NT8 runtime | **PASS** | Step 4 notes: "if NT8 types are not available in the test assembly, Part A, B, C tests still compile because `OrderAction` is an enum that can be referenced as `NinjaTrader.Cbi.OrderAction.Buy` etc. (it is a value type with no runtime NT8 dependency for pure value comparison)." |

**Section G verdict**: PASS

---

### File Routing

| File | Path | Result |
|------|------|--------|
| `CopyEngine.cs` | `src/PropTraderTools/CopyEngine.cs` | **PASS** -- Wave workspace (c:\WSGTA\universal-or-strategy\src\PropTraderTools\) |
| `B119Tests.cs` | `src/PropTraderTools/Tests/B119Tests.cs` | **PASS** -- Wave workspace, correct tests subdirectory |

**File Routing verdict**: PASS

---

## Violations

None.

---

## Warnings (cosmetic -- do not block)

| # | Section | Description | Severity |
|---|---------|-------------|----------|
| W1 | Section E | [Fact] test method names in ticket differ from names in plan (e.g., ticket uses `T_IsReversalToFlatFollower_SameDirection_Buy_NotFired`; plan used `T_IsReversalToFlat_BuyBuy_Flat_ReturnsFalse`). All scenarios are identical. Ticket names are the engineering contract and supersede plan names. | WARN (cosmetic) |

---

## Decision

**TICKET_REVIEW_PASS** -- ticket B119-T1 is approved for Phase 4 engineering.

All 7 checks (A through G) PASS. Zero rule violations. All 7 scans present with exact commands and thresholds. Line numbers verified against live source (`CopyEngine.cs` L1784, L1824-L1868, L3302, L3348). Method signatures exact. CYC budget math sound (branch-merge documented). 11 [Fact] tests specified covering all required scenarios. xUnit only. Zero NT8 mocks. File routing correct. One cosmetic warning recorded (test name mismatch between plan and ticket -- no action required before engineering proceeds).
