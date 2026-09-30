# B66-LaneA Tickets

**Block**: B66-LaneA
**Plan**: 02-architecture-plan.md (REVIEW_PASS confirmed -- 02-plan-review.md, 2026-08-13)
**Total tickets**: 1

---

## Ticket-1 -- Fix CancelQxBrackets: add IsAtmBracketName + IsQxCancelCandidate helpers

### Spec Requirement IDs
- DW-B66-01: CancelQxBrackets misses ATM bracket order names (Stop1/Stop2/Target1/Target2)
- Live incident: 2026-08-13 ~07:50 UTC, double-brackets remained live on 4 follower accounts after Quick Exit with active ATM strategy

### Files Modified
- `src/PropTraderTools/CopyEngine.cs` -- add `IsAtmBracketName` + `IsQxCancelCandidate` before line 422; replace line 436 predicate; update CYC comment on line 424
- `src/PropTraderTools/CopyEngineTests.cs` -- insert 7 [Fact] tests T_B66_01 through T_B66_07 before line 3287 (closing `}` of test class)

---

### Method Signatures

```csharp
// NEW -- insert immediately before the CancelQxBrackets comment block (before line 422).
// IsAtmBracketName: true if name is a standard NT8 ATM bracket order name.
// NT8-REF: NT8_FULL_REFERENCE.md line 1631: "The order name such as 'Stop1' or 'Target2'"
// CYC=1 (expression body -- no if-branches in method body). JS-021: no lock. JS-001: no throw.
// ASCII-only string literals.
internal static bool IsAtmBracketName(string name) =>
    name == "Stop1" || name == "Stop2" || name == "Target1" || name == "Target2";

// NEW -- insert immediately after IsAtmBracketName (still before CancelQxBrackets).
// IsQxCancelCandidate: returns true if order should be cancelled by CancelQxBrackets.
// Covers ATM bracket names (via IsAtmBracketName), PTT-QX-* prefix, PTT-BE-* prefix.
// CYC=5: 1 (base) + 4 if-branches (null guard, IsAtmBracketName, PTT-QX-, PTT-BE-).
// Roslyn/Lizard convention: || inside single if counts as 1 decision point (the if).
// JS-021: no lock. JS-001: no throw. JS-002: returns bool (never null). ASCII-only.
internal static bool IsQxCancelCandidate(Order o)
{
    if (o == null || o.Name == null) return false;                               // (1) null guard
    if (IsAtmBracketName(o.Name)) return true;                                   // (2) ATM bracket names
    if (o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)) return true;    // (3) QX prefix
    if (o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)) return true;    // (4) BE prefix
    return false;
}

// MODIFIED -- CancelQxBrackets: replace line 436 predicate only.
// CYC=6 (unchanged): null guard(1) + foreach(2) + stateOk(3) + instrument check(4)
//                    + IsQxCancelCandidate(5) + staleCount(6). All <= 8. JS-021: no lock.
```

---

### Exact Code Changes

#### CopyEngine.cs Change 1 -- Insert two new methods before line 422

Insert immediately before the line:
```
        // CancelQxBrackets: cancel all Working/Initialized PTT-QX-* orders on acc for instr.
```
(currently line 422)

Insert this block:
```csharp
        // IsAtmBracketName: true if name is a standard NT8 ATM bracket order name.
        // NT8-REF: NT8_FULL_REFERENCE.md line 1631: "The order name such as 'Stop1' or 'Target2'"
        // CYC=1: expression body -- no if-branches in method body (Roslyn convention).
        // JS-021: no lock. JS-001: no throw. ASCII-only string literals.
        internal static bool IsAtmBracketName(string name) =>
            name == "Stop1" || name == "Stop2" || name == "Target1" || name == "Target2";

        // IsQxCancelCandidate: returns true if order should be cancelled by CancelQxBrackets.
        // Covers: ATM bracket names (via IsAtmBracketName), PTT-QX-* prefix, PTT-BE-* prefix.
        // CYC=5: 1 (base) + 4 if-branches. Roslyn: || inside single if = 1 decision point.
        // JS-021: no lock. JS-001: no throw. JS-002: returns bool (never null). ASCII-only.
        internal static bool IsQxCancelCandidate(Order o)
        {
            if (o == null || o.Name == null) return false;                               // (1)
            if (IsAtmBracketName(o.Name)) return true;                                   // (2)
            if (o.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)) return true;    // (3)
            if (o.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)) return true;    // (4)
            return false;
        }

```

#### CopyEngine.cs Change 2 -- Update CancelQxBrackets comment (line 422-424 block)

Replace (current lines 422-424):
```
        // CancelQxBrackets: cancel all Working/Initialized PTT-QX-* orders on acc for instr.
        // Called by PttQuickExit.Execute() before re-placing new bracket.
        // CYC=4: null guard(1) + foreach(2) + stateOk(3) + prefix check(4). JS-021: no lock.
```
With:
```
        // CancelQxBrackets: cancel all Working/Initialized/Accepted ATM-bracket + PTT-* orders on acc for instr.
        // Called by PttQuickExit.Execute() before re-placing new bracket.
        // CYC=6: null guard(1) + foreach(2) + stateOk(3) + instrument check(4) + IsQxCancelCandidate(5) + staleCount(6).
        // JS-021: no lock. Predicate logic in IsQxCancelCandidate (CYC=5) + IsAtmBracketName (CYC=1).
```

#### CopyEngine.cs Change 3 -- Replace line 436 predicate

Replace (current line 436):
```csharp
                if (o.Name != null && o.Name.StartsWith("PTT-QX-"))  // (4)
```
With:
```csharp
                if (IsQxCancelCandidate(o))                           // (5) widened via helper
```

**No other lines in CancelQxBrackets are changed.**

---

#### CopyEngineTests.cs Change 1 -- Insert 7 [Fact] tests before line 3287

Insert the following block immediately before line 3287 (the closing `    }` of the test class,
directly after the blank line following the `T_B63_04` test body ending at line 3284):

```csharp
        // =====================================================================
        // B66 Ticket-1: IsQxCancelCandidate -- widen CancelQxBrackets to ATM+BE brackets
        // DW-B66-01: live incident 2026-08-13 double-brackets bug.
        // TESTABILITY: internal static -- callable directly (same assembly).
        // =====================================================================

        [Fact]
        public void T_B66_01_IsQxCancelCandidate_PttQxPrefix_ReturnsTrue()
        {
            var order = MakeOrder(OrderState.Working, "PTT-QX-Stop01");
            bool result = CopyEngine.IsQxCancelCandidate(order);
            Assert.True(result, "IsQxCancelCandidate: 'PTT-QX-Stop01' must return true (PTT-QX- prefix)");
        }

        [Fact]
        public void T_B66_02_IsQxCancelCandidate_Stop1_ReturnsTrue()
        {
            var order = MakeOrder(OrderState.Working, "Stop1");
            bool result = CopyEngine.IsQxCancelCandidate(order);
            Assert.True(result, "IsQxCancelCandidate: 'Stop1' must return true (ATM bracket name)");
        }

        [Fact]
        public void T_B66_03_IsQxCancelCandidate_Stop2_ReturnsTrue()
        {
            var order = MakeOrder(OrderState.Working, "Stop2");
            bool result = CopyEngine.IsQxCancelCandidate(order);
            Assert.True(result, "IsQxCancelCandidate: 'Stop2' must return true (ATM bracket name)");
        }

        [Fact]
        public void T_B66_04_IsQxCancelCandidate_Target1_ReturnsTrue()
        {
            var order = MakeOrder(OrderState.Working, "Target1");
            bool result = CopyEngine.IsQxCancelCandidate(order);
            Assert.True(result, "IsQxCancelCandidate: 'Target1' must return true (ATM bracket name)");
        }

        [Fact]
        public void T_B66_05_IsQxCancelCandidate_Target2_ReturnsTrue()
        {
            var order = MakeOrder(OrderState.Working, "Target2");
            bool result = CopyEngine.IsQxCancelCandidate(order);
            Assert.True(result, "IsQxCancelCandidate: 'Target2' must return true (ATM bracket name)");
        }

        [Fact]
        public void T_B66_06_IsQxCancelCandidate_PttBeStop_ReturnsTrue()
        {
            var order = MakeOrder(OrderState.Working, "PTT-BE-Stop");
            bool result = CopyEngine.IsQxCancelCandidate(order);
            Assert.True(result, "IsQxCancelCandidate: 'PTT-BE-Stop' must return true (PTT-BE- prefix)");
        }

        [Fact]
        public void T_B66_07_IsQxCancelCandidate_SomeOtherOrder_ReturnsFalse()
        {
            var order = MakeOrder(OrderState.Working, "SomeOtherOrder");
            bool result = CopyEngine.IsQxCancelCandidate(order);
            Assert.False(result, "IsQxCancelCandidate: 'SomeOtherOrder' must return false (no matching prefix or name)");
        }
```

---

### xUnit [Fact] Names and Assertions (T_B66_01 through T_B66_07)

| Test | Name | Asserts |
|------|------|---------|
| T_B66_01 | `T_B66_01_IsQxCancelCandidate_PttQxPrefix_ReturnsTrue` | `Assert.True` -- "PTT-QX-Stop01" matches PTT-QX- prefix (branch 3). Regression: existing behavior preserved. |
| T_B66_02 | `T_B66_02_IsQxCancelCandidate_Stop1_ReturnsTrue` | `Assert.True` -- "Stop1" matched by IsAtmBracketName (branch 2). Core fix for live incident. |
| T_B66_03 | `T_B66_03_IsQxCancelCandidate_Stop2_ReturnsTrue` | `Assert.True` -- "Stop2" matched by IsAtmBracketName (branch 2). Core fix for live incident. |
| T_B66_04 | `T_B66_04_IsQxCancelCandidate_Target1_ReturnsTrue` | `Assert.True` -- "Target1" matched by IsAtmBracketName (branch 2). Core fix for live incident. |
| T_B66_05 | `T_B66_05_IsQxCancelCandidate_Target2_ReturnsTrue` | `Assert.True` -- "Target2" matched by IsAtmBracketName (branch 2). Core fix for live incident. |
| T_B66_06 | `T_B66_06_IsQxCancelCandidate_PttBeStop_ReturnsTrue` | `Assert.True` -- "PTT-BE-Stop" matches PTT-BE- prefix (branch 4). Validates widened cancellation path. |
| T_B66_07 | `T_B66_07_IsQxCancelCandidate_SomeOtherOrder_ReturnsFalse` | `Assert.False` -- "SomeOtherOrder" returns false (default path). Guards against over-broad matching. |

---

### NT8 API / Rule Constraints

- **NT8-014**: Signal names must start with "PTT-" -- NOT applicable here. `IsAtmBracketName` and `IsQxCancelCandidate` only READ `o.Name`; they do not create or name orders.
- **NT8-REF line 1631**: ATM bracket order names are "Stop1", "Stop2", "Target1", "Target2" -- confirmed citation for `IsAtmBracketName` exact matches.
- `CancelQxBrackets` still calls `acc.Cancel()` -- existing NT8 cancel behavior unchanged; only the candidate set is widened.
- `acc.Orders` / `Order.Name` are both valid NT8 AddOn-accessible properties (not restricted to `StrategyBase`).
- No new NT8 API surface added beyond what already existed in `CancelQxBrackets`.

---

### 7-SCAN CHECKLIST (engineer contract -- must report 0 on ALL 7 before BUILD_PASS)

| # | Scan | Command | Required Result |
|---|------|---------|-----------------|
| S1 | JS-021 lock() ban | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | 0 new hits in IsAtmBracketName, IsQxCancelCandidate, modified CancelQxBrackets |
| S2 | JS-001 throw ban | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` | 0 hits in new methods |
| S3 | JS-002 return null | `grep -n "return null" src/PropTraderTools/CopyEngine.cs` | 0 hits in new methods (both return bool) |
| S4 | ASCII-only | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | 0 new non-ASCII characters in new/modified methods |
| S5 | CYC <= 8 | `python scripts/complexity_audit.py` | IsAtmBracketName=1, IsQxCancelCandidate=5, CancelQxBrackets<=8 |
| S6 | Test count | `grep -c "T_B66_0" src/PropTraderTools/CopyEngineTests.cs` | >= 7 |
| S7 | xUnit only | `grep -n "using NUnit\|using MSTest\|using Microsoft.VisualStudio.TestTools" src/PropTraderTools/CopyEngineTests.cs` | 0 hits |

---

### Acceptance Criteria

- [ ] `IsAtmBracketName` (expression body, CYC=1) inserted before `CancelQxBrackets` comment block in `CopyEngine.cs`
- [ ] `IsQxCancelCandidate` (internal static bool, CYC=5) inserted after `IsAtmBracketName` and before `CancelQxBrackets` comment block in `CopyEngine.cs`
- [ ] `CancelQxBrackets` line 436 predicate replaced: `if (o.Name != null && o.Name.StartsWith("PTT-QX-"))` -> `if (IsQxCancelCandidate(o))`
- [ ] `CancelQxBrackets` CYC comment updated (lines 422-424): old "CYC=4" replaced with correct "CYC=6" with updated branch descriptions
- [ ] All 7 tests T_B66_01..T_B66_07 inserted in `CopyEngineTests.cs` before closing `}` of test class (before line 3287)
- [ ] All 7 scans (S1-S7) report 0 violations on new/modified code
- [ ] `dotnet build` passes with 0 errors
- [ ] `dotnet test` passes with all 7 new tests green

---

### Commit Format

```
git add src/PropTraderTools/
git commit -m "fix(ptt): B66-LaneA -- widen CancelQxBrackets to ATM+BE brackets [7 tests]"
```
