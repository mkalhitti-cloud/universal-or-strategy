# B129 LaneB — Ticket Review (RETRY)

**Block**: B129 LaneB
**Defect**: DW-B134 — ATM Bracket Drag Not Synced to Followers
**Phase**: 3.5 (Ticket Review — RETRY after TR-06 repair)
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-08-21
**Ticket file**: `docs/brain/B129/LaneB-04-tickets.md`
**Plan file**: `docs/brain/B129/LaneB-02-architecture-plan.md` — REVIEW_PASS
**Rules reference**: `docs/standards/jane-street/RULES_CATALOG.md`
**Prior review**: `docs/brain/B129/LaneB-04-ticket-review.md` — TICKET_REVIEW_FAIL (1 violation: TR-06)
**Repair**: Architect split combined try/catch into two independent try/catch blocks (Block A + Block B).

---

## Ticket Review: B129 LaneB — Ticket B129-LaneB-T2

---

### TR-01 — Ticket ID is B129-LaneB-T2 (not T1, not conflicting with LaneA)

**Result**: PASS

Ticket header: `## Ticket B129-LaneB-T2` (line 16). Correct lane designation. No conflict with LaneA
(LaneA uses T1; LaneB uses T2).

---

### TR-02 — All spec requirement IDs listed (DW-B134-L1..L6, OQ-03, PTT)

**Result**: PASS

Section 2 requirement table contains: DW-B134-L1, DW-B134-L2, DW-B134-L3, DW-B134-L4,
DW-B134-L5, DW-B134-L6, DW-B134-OQ03, DW-B134-PTT. All defect brief mandatory IDs
(L1, L2, L3, OQ-03) are present. Additional rows (L4, L5, L6, PTT) exceed the minimum
without conflicting.

---

### TR-03 — Method signatures for ALL 4 methods specified

**Result**: PASS

All four method signatures explicitly stated in Section 4:

- **`IsBracketLegStatic`** (Section 4.1): BEFORE/AFTER shown.
  `private static bool IsBracketLegStatic(Order order)` — no signature change, internal logic only.
- **`IsAtmSTPOrder`** (Section 4.2): `internal static bool IsAtmSTPOrder(Order order) =>` with
  full expression body.
- **`SyncFollowerBracket`** (Section 4.3): `private void SyncFollowerBracket(Account acc, Order leaderOrder, bool isStop, double newPrice, double tickSize)` — all 5 parameters named.
- **`SyncAtmFollowerBracket`** (Section 4.4): `private void SyncAtmFollowerBracket(Account acc, Order fo, double newPrice)` — all 3 parameters named.

---

### TR-04 — ATM STP branch inserted BEFORE IsTrailingStop guard (not after)

**Result**: PASS

Step 3 Sub-change 3b AFTER block: `if (isStop && IsAtmSTPOrder(fo))` at position `(3)` appears before
`if (isStop && IsTrailingStop(fo))` at position `(4)`. The explanatory comment states the
architectural reason: "IsTrailingStop fires on StopMarket orders; ATM STP brackets ARE StopMarket.
Without this branch, IsTrailingStop would return early and skip the sync." SCAN-06 in the 7-scan
checklist enforces correct line ordering at build time.

---

### TR-05 — SyncAtmFollowerBracket uses "PTT-" prefixed order name (NT8-014)

**Result**: PASS

Step 4 code: `"PTT-STP-Drag"` passed as the `name` argument to `acc.CreateOrder(...)`.
Section 4.4 comment annotates `// NT8-014: order name starts with "PTT-"`. SCAN-05 verifies
`grep -n "PTT-STP-Drag"` returns exactly 1 hit in `SyncAtmFollowerBracket`.

---

### TR-06 — SyncAtmFollowerBracket: TWO separate independent try/catch blocks (JS-001)

**Result**: PASS — **TR-06 violation RESOLVED**

**Previous finding (FAIL)**: A single combined try/catch wrapped `acc.Cancel`, `acc.CreateOrder`,
and `acc.Submit` together, masking distinct failure modes and creating naked-position risk.

**Repair verified**: The repaired ticket Step 4 (`SyncAtmFollowerBracket` body, lines 253–315 of
`LaneB-04-tickets.md`) now contains **two syntactically independent try/catch blocks**:

**Block A — Cancel only** (lines 277–284 of ticket):
```csharp
try
{
    acc.Cancel(new Order[] { fo });
}
catch (Exception ex)
{
    StatusUpdate?.Invoke(acc.Name + ": STP cancel error: " + ex.Message);
}
```
- Contains **only** `acc.Cancel`. No `return` in catch — execution always falls through to Block B.
- If Cancel throws: catch logs the error message, then falls through. Block B executes regardless.

**Block B — CreateOrder+Submit only** (lines 287–314 of ticket):
```csharp
try
{
    var newStop = acc.CreateOrder(...);
    if (newStop == null) { StatusUpdate?.Invoke(...); return; }
    newStop?.Submit();
    StatusUpdate?.Invoke(acc.Name + ": ATM STP resubmit -> " + newPrice);
}
catch (Exception ex)
{
    StatusUpdate?.Invoke(acc.Name + ": STP create error: " + ex.Message);
}
```
- Contains **only** `acc.CreateOrder` + `acc.Submit`. Independent from Block A.
- If CreateOrder/Submit throws: catch logs distinctly (`"STP create error"`), no naked-position risk.

**Isolation confirmed**:
1. Block A and Block B are sequential, back-to-back, NOT nested. Neither is inside the other.
2. Block A catch has no `return` — Block B **always runs** regardless of Block A outcome.
3. Distinct error messages distinguish Cancel failure (`"STP cancel error"`) from
   CreateOrder/Submit failure (`"STP create error"`) — failure modes are no longer masked.
4. Section 4.4 comment explicitly documents: "Block A (Cancel): if Cancel throws, Block B still
   executes (independent isolation)."

**JS-001 compliance**: No exception is thrown in the hot path. Each catch invokes
`StatusUpdate?.Invoke(...)` only. No rethrow. PASS.

---

### TR-07 — CYC stated for each method; all ≤ 8

**Result**: PASS (with minor documentation note)

| Method | Code-Derived CYC | Ticket Comment States | ≤ 8? |
|--------|------------------|-----------------------|------|
| `IsBracketLegStatic` | 4 | 3→4 | PASS |
| `IsAtmSTPOrder` | 1 | 1 | PASS |
| `SyncFollowerBracket` | 6 | 5→6 | PASS |
| `SyncAtmFollowerBracket` | 4 | "CYC=2" (comment undercounts) | PASS |

**Documentation note — `SyncAtmFollowerBracket` CYC comment**: The Section 4.4 comment and Step 4
code comment state `CYC=2` counting only the acc/fo null guards. However, the `if (newStop == null)`
branch inside Block B adds a third McCabe decision point: actual CYC = **4** (base 1 + three guards).
The comment labels the `newStop` branch as `// (3)` in the code, confirming it exists. The CYC
undercount in the prose comment is a minor documentation inaccuracy — the code itself is correct and
correct CYC = 4, which is ≤ 8. This is a WARNING, not a blocking violation.

**All methods ≤ 8. CYC Pre-Check: PASS.**

---

### TR-08 — 7-scan checklist present with ALL 7 scans and exact commands

**Result**: PASS

Section 7 contains SCAN-01 through SCAN-07, each with:
- An exact PowerShell shell command
- An expected output description
- A pass/fail criterion

| Scan | Command | Pass Criterion Stated |
|------|---------|----------------------|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | Yes |
| SCAN-02 | `grep -n "async void" src/PropTraderTools/CopyEngine.cs` | Yes |
| SCAN-03 | `grep -n "return null" src/PropTraderTools/CopyEngine.cs` | Yes |
| SCAN-04 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | Yes |
| SCAN-05 | `grep -n "PTT-STP-Drag" src/PropTraderTools/CopyEngine.cs` | Yes |
| SCAN-06 | `grep -n "IsTrailingStop\|IsAtmSTPOrder" src/PropTraderTools/CopyEngine.cs` | Yes |
| SCAN-07 | `dotnet build --no-incremental` + `dotnet test --filter "FullyQualifiedName~B129"` | Yes |

All 7 scan commands present verbatim. Defense-in-depth contract is complete (LAYER 1 — engineer
contract for self-report; anchors verifier cross-check at LAYER 3). PASS.

---

### TR-09 — SCAN-06 (IsTrailingStop regression guard) present — confirms guard NOT removed

**Result**: PASS

SCAN-06 explicitly:
1. Checks `IsTrailingStop` is still present in the file.
2. Verifies `IsAtmSTPOrder` call line number < `IsTrailingStop` call line number within `SyncFollowerBracket`.
3. States pass criteria: "IsTrailingStop is still present (guard not accidentally removed)."

Both a regression guard (guard not removed) and an ordering contract (ATM STP fires before
TrailingStop guard) are enforced by this scan.

---

### TR-10 — OQ-03 gate explicitly stated in BOTH Section 6 AND Section 10

**Result**: PASS

**Section 6** (lines 487–510 of ticket): Explicit OQ-03 gate block with:
- Exact command: `dotnet test --filter "FullyQualifiedName~B129_DW134_OQ03"`
- Expected result: 1 test, PASSED
- FAIL protocol: STOP, do not proceed, report to ptt-architect with failing assertion detail

**Section 9** criterion 9: `dotnet test --filter "FullyQualifiedName~B129_DW134_OQ03"` listed as a
required BUILD_PASS criterion in the 9-row table.

**Section 10** (lines 658–670 of ticket): Ph4a gate statement — "Ph4a (NT8 simulator testing) MUST
NOT BEGIN until: 1. All 9 BUILD_PASS criteria above are confirmed GREEN. 2. OQ-03 gate test
(`B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel`) reports PASS."

OQ-03 gate is present in all three required locations: Section 6, Section 9 (BUILD_PASS table),
and Section 10 (Ph4a gate statement). RC-12 omission fully corrected.

---

### TR-11 — All 3 xUnit [Fact] method names present with assertion specs

**Result**: PASS

Section 8 table lists all 3 method names with purpose descriptions. Step 5 provides full test
body code for all three. Names match defect brief:

| # | Method Name | Present |
|---|-------------|---------|
| 1 | `B129_DW134_STPSuffixDetectedByIsBracketLegStatic` | Yes |
| 2 | `B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket` | Yes |
| 3 | `B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel` | Yes |

Each test includes Assert.* statements, purpose comments, and inline rationale. PASS.

---

### TR-12 — Test 3 (OQ-03 confirmation) verifiable without NT8 runtime

**Result**: PASS

Test 3 body uses only:
- `typeof(CopyEngine).GetMethod("FindMatchingRule", BindingFlags.NonPublic | BindingFlags.Instance)` — reflection, no NT8 runtime
- `typeof(CopyEngine).GetField("_rules", BindingFlags.NonPublic | BindingFlags.Instance)` — reflection, no NT8 runtime
- `!"Sim102".Equals("Sim101", StringComparison.Ordinal)` — pure string comparison
- `Assert.Equal`, `Assert.NotNull`, `Assert.True` — xUnit intrinsics only

No `Account`, `Order`, or NT8-sealed type instantiation required. Executable in dotnet test
runner without NinjaTrader loaded.

---

### TR-13 — BUILD_PASS definition present with all criteria

**Result**: PASS

Section 9 defines BUILD_PASS as a 9-criterion table with exact commands and expected results:

| # | Criterion | Command | Expected Result |
|---|-----------|---------|-----------------|
| 1 | SCAN-01: no `lock()` | `grep -n "lock("` | 0 hits in new methods |
| 2 | SCAN-02: no `async void` | `grep -n "async void"` | 0 hits |
| 3 | SCAN-03: no `return null` in new methods | `grep -n "return null"` | 0 hits in new methods |
| 4 | SCAN-04: no `throw new` in hot path | `grep -n "throw new"` | 0 hits in `SyncAtmFollowerBracket` |
| 5 | SCAN-05: `PTT-STP-Drag` present | `grep -n "PTT-STP-Drag"` | Exactly 1 hit |
| 6 | SCAN-06: call ordering | `grep -n "IsAtmSTPOrder\|IsTrailingStop"` | ATM STP line < TrailingStop line |
| 7 | SCAN-07: clean build | `dotnet build --no-incremental` | 0 errors, 0 new warnings |
| 8 | SCAN-07: tests pass | `dotnet test --filter "~B129"` | All B129 tests PASS |
| 9 | OQ-03 gate | `dotnet test --filter "~B129_DW134_OQ03"` | PASS |

All 9 criteria present with commands and expected results. BUILD_PASS definition is complete
and unambiguous. PASS.

---

### TR-14 — No scope creep (IsBracketLeg instance untouched, no Target STP handling added)

**Result**: PASS

- `IsBracketLeg` (instance method, used by `CancelOneAccount`) not mentioned in any step.
  Only `IsBracketLegStatic` is modified.
- No Target STP (`EndsWith("TGT")` or similar) logic introduced anywhere.
- No methods outside the 4 named methods are described as modified.
- Section 3 FORBIDDEN block explicitly prohibits editing files outside the listed set.
- Plan Section 9 (open risk J, OCO orphan issue) deliberately deferred — not included in this ticket.

PASS.

---

### TR-15 — File routing: all .cs paths in Wave workspace (src/PropTraderTools/)

**Result**: PASS

Section 3 lists 3 files:
1. `src/PropTraderTools/CopyEngine.cs` — Wave workspace
2. `src/PropTraderTools/Tests/B129Tests.cs` — Wave workspace
3. `src/PropTraderTools/PropTraderTools.csproj` — Wave workspace

No .cs or .csproj file paths point to the Director workspace
(`c:\WSGTA\universal-or-strategy-director`). All file paths are within the Wave workspace
`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`. PASS.

---

## JS Pre-Check (All New/Modified Method Descriptions)

| Check | Rule | Result |
|-------|------|--------|
| `lock()` described in any new method | JS-021 (P0) | PASS — No `lock()` in any described method body |
| `return null` described in `IsAtmSTPOrder` or `SyncAtmFollowerBracket` | JS-002 (P0) | PASS — Both are `bool`/`void`; no `return null` described |
| `throw new` described in hot path methods | JS-001 (P0) | PASS — Both catches invoke `StatusUpdate?.Invoke(...)` only; no rethrow |
| Single combined try/catch masking Cancel+CreateOrder | JS-001 (P0) | PASS — Two independent blocks confirmed (TR-06 resolved) |
| `async void` in lifecycle methods | NT8 constraint | PASS — All new methods synchronous |
| `Dictionary<K,V>` on CopyEngine fields | JS-009 | PASS — No new Dictionary fields described |
| `DateTime.Now` usage | NT8 constraint | PASS — Not described anywhere |
| Hardcoded hex color | NT8 constraint | PASS — N/A (no UI changes) |
| `FontFamily` set | NT8 constraint | PASS — N/A (no UI changes) |
| `CreateOrder` name not starting "PTT-" | NT8-014 | PASS — `"PTT-STP-Drag"` starts with "PTT-" |
| `sealed` on TradeCopierWindow | NT8 constraint | PASS — N/A (no Window changes) |

**JS Pre-Check: PASS — 0 violations.**

---

## Complexity Pre-Check

| Method | Code-Derived CYC | Ticket Stated | ≤ 8? | Note |
|--------|-----------------|---------------|------|------|
| `IsBracketLegStatic` | 4 | 3→4 | PASS | |
| `IsAtmSTPOrder` | 1 | 1 | PASS | |
| `SyncFollowerBracket` | 6 | 5→6 | PASS | |
| `SyncAtmFollowerBracket` | 4 | "CYC=2" | PASS | Comment undercounts by 2; actual=4; still ≤ 8 (WARNING, not blocking) |

**CYC Pre-Check: PASS — No method exceeds CYC 8.**

---

## TR-06 Resolution Summary

| Item | Previous Review | This Review |
|------|----------------|-------------|
| Try/catch structure | Single combined block (FAIL) | Two independent blocks: Block A (Cancel only) + Block B (CreateOrder+Submit only) |
| Block A catch falls through? | N/A | YES — no `return` in catch; Block B always executes |
| Failure modes distinguished? | NO — masked | YES — "STP cancel error" vs "STP create error" |
| Naked-position risk? | Present | Eliminated — Cancel failure does not prevent resubmit |
| JS-001 compliance | FAIL | PASS |
| CYC after fix | Expected 4 | Actual 4 (code correct; prose comment says "CYC=2" — minor undercount, not blocking) |

---

## Overall Check Summary

| Check | Result |
|-------|--------|
| TR-01 Ticket ID | PASS |
| TR-02 Spec Req IDs | PASS |
| TR-03 Method Signatures | PASS |
| TR-04 Branch Ordering | PASS |
| TR-05 PTT- Prefix | PASS |
| TR-06 Separate try/catch (JS-001) | **PASS — RESOLVED** |
| TR-07 CYC ≤ 8 | PASS (minor comment undercount noted, not blocking) |
| TR-08 7-Scan Checklist | PASS |
| TR-09 SCAN-06 Regression Guard | PASS |
| TR-10 OQ-03 Gate (Section 6 + Section 10) | PASS |
| TR-11 [Fact] Names + Assertions | PASS |
| TR-12 Test 3 NT8-Runtime-Free | PASS |
| TR-13 BUILD_PASS Definition | PASS |
| TR-14 No Scope Creep | PASS |
| TR-15 File Routing | PASS |

---

## Blocking Violations Summary

**0 blocking violations.**

The single blocking violation found in the prior review (TR-06 / JS-001) has been resolved. The
repaired ticket contains two syntactically independent try/catch blocks in `SyncAtmFollowerBracket`.
No new violations were introduced by the repair.

---

## Overall: TICKET_REVIEW_PASS

**TICKET_REVIEW_PASS.** All 15 criteria evaluated. Zero blocking violations. TR-06 (JS-001)
resolved by splitting the combined try/catch into two independent blocks. The engineer is cleared
to execute Phase 4a. OQ-03 gate (`B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel`)
must pass before NT8 simulator testing begins (Sections 6, 9, and 10 all enforce this gate).

*Review written by ptt-ticket-reviewer (Phase 3.5 RETRY). All 15 criteria evaluated. 0 blocking violations.*
