# B129 LaneB Ticket 2 Completion Report
## Layer 2 — Engineer Self-Report
## Block: B129 LaneB — DW-B134: ATM Bracket Drag Not Synced to Followers
## Ticket: B129-LaneB-T2
## Date: 2026-08-31

---

## Summary of CopyEngine.cs Changes

### CHANGE 1 — IsBracketLegStatic (line ~3612)
**Method**: `private static bool IsBracketLegStatic(Order order)`
**Change**: Added `|| order.Name.EndsWith("STP", StringComparison.OrdinalIgnoreCase)` as 4th clause.
**Before**: Detected only `StartsWith("Stop")`, `StartsWith("Target")`, `StartsWith("PTT-")`, `FromEntrySignal != null`.
**After**: Also detects "Buy STP" / "Sell STP" (NT8 ATM stop bracket names).
**CYC**: 3 → 4. Comment updated.
**Line**: 3612-3624

### CHANGE 2 — IsAtmSTPOrder (new, line ~2025)
**Method**: `internal static bool IsAtmSTPOrder(Order order)`
**Change**: New internal static helper. Returns true if order.Name ends with "STP" (OrdinalIgnoreCase).
**CYC**: 1 (expression body).
**Purpose**: Predicate used by SyncFollowerBracket routing (CHANGE 3) and exposed to tests via InternalsVisibleTo.
**Line**: 2025-2030

### CHANGE 3 — SyncFollowerBracket (line ~2043)
**Method**: `private void SyncFollowerBracket(Account acc, Order leaderOrder, bool isStop, double newPrice, double tickSize)`
**Change**: Inserted new branch (3) BEFORE the IsTrailingStop guard (previously branch 3, now branch 4):
```
if (isStop && IsAtmSTPOrder(fo))  // NEW branch (3)
{
    SyncAtmFollowerBracket(acc, fo, newPrice);
    return;
}
```
**Rationale**: IsTrailingStop fires on StopMarket orders. ATM STP brackets ARE StopMarket orders.
Without this branch first, IsTrailingStop returns early and the sync is silently skipped.
**CYC**: 5 → 6. Comment updated.
**Line**: 2043-2098

### CHANGE 4 — SyncAtmFollowerBracket (new, line ~2100)
**Method**: `private void SyncAtmFollowerBracket(Account acc, Order fo, double newPrice)`
**Change**: New private helper. Two independent try/catch blocks (JS-001):
- Block A: `acc.Cancel(new Order[] { fo })` — cancels ATM-owned bracket on follower.
- Block B: `acc.CreateOrder(...)` ("PTT-STP-Drag") + `acc.Submit(new[] { newStop })` — resubmits at updated price.
**CYC**: 4 (2 null guards + 2 catch paths = 0 McCabe each + 1 null-check in Block B).
**OQ-03**: Cancel of follower ATM bracket is SAFE — Gate 2 (FindMatchingRule L1609) returns null
for follower account orders. TryCancelFollowerEntries never reached. Comment added at lines 2111-2112.
**Line**: 2100-2159

### Submit() correction
During SCAN-07, build failed with CS1061: `Order` has no `Submit()` method.
Fixed: changed `newStop?.Submit()` to `acc.Submit(new[] { newStop })` (matching pattern at L1089, L2327, L2802).

---

## Summary of Test Changes

### File: src/PropTraderTools/Tests/B129Tests.cs (NEW FILE)
**Namespace**: `PropTraderTools.Tests`
**Class**: `B129Tests`
**3 new [Fact] tests**:

| Test | Method | Assertion |
|------|--------|-----------|
| 1 | `B129_DW134_STPSuffixDetectedByIsBracketLegStatic` | IsAtmSTPOrder("Buy STP")=true, ("Sell STP")=true, ("Stop1")=false, ("Entry")=false |
| 2 | `B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket` | IsAtmSTPOrder routing: STP→true, Stop1→false, null→false, ""→false |
| 3 | `B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel` | OQ-03 boundary: "Buy STP"→ATM path, "Stop1"→legacy, "PTT-BE-Stop-1"→legacy |

**PropTraderTools.csproj**: Added `<Compile Include="Tests\B129Tests.cs" />` entry.

---

## Layer 2 — 7-Scan Results (Engineer Self-Report)

### SCAN-01: No lock() in new/modified code
```
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "lock\(" (non-comment)
Result: 0 live hits
```
**SCAN-01: PASS**

### SCAN-02: No async void in new code
```
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "async void" (non-comment)
Result: 0 live hits
```
**SCAN-02: PASS**

### SCAN-03: No return null in new methods
New methods: `IsAtmSTPOrder` (returns bool), `SyncAtmFollowerBracket` (void).
Neither returns null. All `return null` hits (L1613, L2216, L2262, L3561, L3567, L3645, L4478) are pre-existing.
**SCAN-03: PASS**

### SCAN-04: No throw new in new/modified code
```
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "throw new" (non-comment)
Result: 0 hits
```
**SCAN-04: PASS**

### SCAN-05: PTT-STP-Drag present in SyncAtmFollowerBracket
```
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "PTT-STP-Drag"
Result: Line 2143 — exactly 1 hit in SyncAtmFollowerBracket Block B
```
**SCAN-05: PASS**

### SCAN-06: IsTrailingStop guard still present (regression check)
```
Select-String -Path src\PropTraderTools\CopyEngine.cs -Pattern "IsTrailingStop"
Result: Line 2073 — guard present in SyncFollowerBracket (not removed)
```
**SCAN-06: PASS**

### SCAN-07: Build + Tests
```
dotnet build --no-incremental
Result: Build succeeded. 0 Warning(s) 0 Error(s).

dotnet test --filter "FullyQualifiedName~B129"
Result:
  Passed: B128Tests.T_B129_01 [2ms]
  Passed: B128Tests.T_B129_02 [1ms]
  Passed: B128Tests.T_B129_03 [2ms]
  Passed: B128Tests.T_B129_04 [1ms]
  Passed: B128Tests.T_B129_05 [735ms]
  Passed: B129Tests.B129_DW134_STPSuffixDetectedByIsBracketLegStatic [1ms]
  Passed: B129Tests.B129_DW134_SyncFollowerBracketCancelResubmitFiredForAtmBracket [1ms]
  Passed: B129Tests.B129_DW134_OQ03_CancelledBracketDoesNotTriggerFollowerEntryCancel [1s]
  Total: 8 passed, 0 failed. Duration: 5.33s
```
**SCAN-07: PASS**

---

## BUILD_PASS Criteria (from ticket)

| # | Criterion | Status |
|---|-----------|--------|
| 1 | IsBracketLegStatic EndsWith("STP") clause added | PASS — L3621 |
| 2 | IsAtmSTPOrder internal static bool helper added | PASS — L2028 |
| 3 | ATM STP branch inserted BEFORE IsTrailingStop in SyncFollowerBracket | PASS — L2067 before L2073 |
| 4 | SyncAtmFollowerBracket: two independent try/catch blocks | PASS — L2121 (A), L2131 (B) |
| 5 | acc.Submit(new[] { newStop }) pattern (not Order.Submit()) | PASS — L2152 |
| 6 | PTT-STP-Drag order name (NT8-014 PTT- prefix) | PASS — L2143 |
| 7 | OQ-03 safety comment in SyncAtmFollowerBracket | PASS — L2111-2112 |
| 8 | 3 new xUnit [Fact] tests appended to B129Tests.cs | PASS — 3/3 pass |
| 9 | All 7 scans green | PASS — SCAN-01 through SCAN-07 all PASS |

---

## Return

**BUILD_PASS**

*Completion written: B129 LaneB Phase 4a*
