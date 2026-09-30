# B67-LaneA — Engineer Tickets
**Block**: B67-LaneA
**Source Plan**: docs/brain/B67-LaneA/02-architecture-plan.md (REVIEW_PASS)
**Author**: ptt-architect
**Date**: 2026-08-13

---

## TICKET B67-LaneA-T1

**TITLE**: FlattenOneAccount — insert CancelQxBrackets before market order submission
**DW ITEM**: DW-B67-01 (P0)
**SPEC REQ IDs**: DW-B67-01, B67-NT8-01 (cancel-before-flatten pattern)

---

### 1. Problem

`CopyEngine.FlattenOneAccount` calls `acc.CreateOrder(Market...)` while follower ATM / QX bracket
orders are still live. At Rithmic/Apex the broker rejects the flatten because the OCO bracket is in
Working state at the same time. NT8 logs: `Close operation failed. Operation timed out.`
The follower position is NOT closed. Confirmed in live trading 2026-08-12.

---

### 2. Files to Modify

| # | File | Change Type |
|---|------|-------------|
| 1 | `src/PropTraderTools/CopyEngine.cs` | 2 edits (comment replace + 1-line insert + caller comment update) |
| 2 | `src/PropTraderTools/CopyEngineTests.cs` | Add 4 new [Fact] tests after last test T_B66_07 |

No other files are touched.

---

### 3. Edit 1 — CopyEngine.cs: Replace FlattenOneAccount (lines 1423–1446)

Replace the ENTIRE method including its header comment block.

**EXACT OLD CODE** (lines 1423–1446 — match character-for-character):

```csharp
        // B28 T1 -- FlattenOneAccount: per-account market flatten helper. CYC=3.
        // (1) pos null/qty guard, (2) action ternary, (3) try/catch CreateOrder.
        private void FlattenOneAccount(Account acc, Instrument instrument)
        {
            var pos = FindPosition(acc, instrument);
            if (pos == null || pos.Quantity == 0)
            {
                StatusUpdate?.Invoke(acc.Name + ": flat skip");
                return;
            }
            var action = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
            try
            {
                acc.CreateOrder(
                    instrument, action, OrderType.Market, OrderEntry.Manual,
                    TimeInForce.Gtc, pos.Quantity, 0, 0, null, "PTT-Flatten",
                    DateTime.MaxValue, null);
                StatusUpdate?.Invoke(acc.Name + ": flatten " + pos.Quantity);
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke("PTT-Flatten error: " + ex.Message);
            }
        }
```

**EXACT NEW CODE** (replacing the above):

```csharp
        // B28 T1 -- FlattenOneAccount: per-account market flatten helper.
        // B67 DW-B67-01: cancel follower ATM+QX brackets BEFORE submitting market order.
        // NT8 precedent: @2Custom-0909edcc FlattenPositionByName V8.31 comment:
        //   "Cancel ALL bracket orders first to prevent race conditions."
        // Rithmic/Apex: incoming market order conflicts with live OCO bracket at broker layer
        //   -> "Close operation failed. Operation timed out." without this cancel step.
        // CYC=4: (1) pos null/qty guard, (2) CancelQxBrackets, (3) action ternary, (4) try/catch.
        // JS-021: no lock. JS-001: no throw in hot path. JS-002: void.
        private void FlattenOneAccount(Account acc, Instrument instrument)
        {
            var pos = FindPosition(acc, instrument);
            if (pos == null || pos.Quantity == 0)
            {
                StatusUpdate?.Invoke(acc.Name + ": flat skip");
                return;
            }
            CancelQxBrackets(acc, instrument);   // B67 DW-B67-01: cancel before market order
            var action = pos.MarketPosition == MarketPosition.Long ? OrderAction.Sell : OrderAction.BuyToCover;
            try
            {
                acc.CreateOrder(
                    instrument, action, OrderType.Market, OrderEntry.Manual,
                    TimeInForce.Gtc, pos.Quantity, 0, 0, null, "PTT-Flatten",
                    DateTime.MaxValue, null);
                StatusUpdate?.Invoke(acc.Name + ": flatten " + pos.Quantity);
            }
            catch (Exception ex)
            {
                StatusUpdate?.Invoke("PTT-Flatten error: " + ex.Message);
            }
        }
```

> **CRITICAL — ASCII only**: The arrow character in the comment above is `->` (ASCII hyphen + gt).
> Do NOT use Unicode arrow characters (-->, ->, or any non-ASCII arrow variant).
> PRE-EXISTING-02 in the backlog notes pre-existing non-ASCII arrows in the file; do NOT introduce
> new non-ASCII characters. Use `->` throughout all new and modified comments.

---

### 4. Edit 2 — CopyEngine.cs: Update CancelQxBrackets caller comment (~line 443)

The region around line 443 currently reads:

```csharp
        // Called by PttQuickExit.Execute() before re-placing new bracket.
        internal void CancelQxBrackets(Account acc, NinjaTrader.Cbi.Instrument instr)
```

Replace **only the single comment line** immediately above the `internal void CancelQxBrackets` signature:

**OLD** (one line):
```csharp
        // Called by PttQuickExit.Execute() before re-placing new bracket.
```

**NEW** (two lines — insert the second line; do NOT change the `internal void` line):
```csharp
        // Called by PttQuickExit.Execute() before re-placing new bracket.
        // Also called by FlattenOneAccount (B67 DW-B67-01) before market order submission.
```

The `internal void CancelQxBrackets(Account acc, NinjaTrader.Cbi.Instrument instr)` line is NOT changed.

> NOTE: The exact line number of the `CancelQxBrackets` comment may have shifted by a few lines
> if earlier edits changed the file. Locate it by searching for the string
> `"Called by PttQuickExit.Execute() before re-placing new bracket."` — there is exactly one occurrence.

---

### 5. Tests — src/PropTraderTools/CopyEngineTests.cs

Add the following 4 [Fact] methods to the existing test class, after the last test `T_B66_07`.
All tests are xUnit only. No NUnit. No MSTest.

Confirm `[assembly: InternalsVisibleTo("CopyEngineTests")]` is already present in the project
(added in B28 T1). If missing, add it to `src/PropTraderTools/AssemblyInfo.cs` before running
the tests.

Use the reflection / stub pattern already established in the file for accessing private methods
and recording call sequences. The pattern for `FlattenOneAccount` is the same as used for other
`private void` helpers in prior blocks (B28, B62, B65, B66-LaneB).

---

#### T_B67_01 — CancelQxBrackets_called_before_CreateOrder

**What it asserts**: `CancelQxBrackets` is invoked BEFORE `acc.CreateOrder` when the position
is non-zero long.

**Setup**:
- Subclass (or use existing test subclass of) `CopyEngine` that overrides / wraps
  `CancelQxBrackets` to append `"CancelQxBrackets"` to a `List<string> callLog`.
- Stub `FindPosition` to return a non-null position with `Quantity=1`, `MarketPosition=Long`.
- Stub `acc.CreateOrder` (or use null-safe wrapper) to append `"CreateOrder"` to `callLog`.

**Assert**:
```csharp
Assert.Equal("CancelQxBrackets", callLog[0]);
Assert.Equal("CreateOrder", callLog[1]);
```

```csharp
[Fact]
public void T_B67_01_CancelQxBrackets_called_before_CreateOrder()
{
    // arrange: subclass records call order
    // act: invoke FlattenOneAccount via reflection with long position qty=1
    // assert: callLog[0]=="CancelQxBrackets", callLog[1]=="CreateOrder"
    // Use the reflection/harness pattern already established in this file.
    throw new NotImplementedException("Engineer: implement using established reflection harness");
}
```

> Replace the `throw new NotImplementedException` stub with the real implementation following the
> existing reflection/harness pattern in this file. The stub is provided here only to define the
> assertion contract.

---

#### T_B67_02 — FlattenOneAccount_flat_position_noOp

**What it asserts**: When `FindPosition` returns null, neither `CancelQxBrackets` nor
`acc.CreateOrder` is called (early return guard is respected).

**Setup**:
- Stub `FindPosition` to return `null`.
- Record `cancelCallCount` and `createOrderCallCount` (both should remain 0).

**Assert**:
```csharp
Assert.Equal(0, cancelCallCount);
Assert.Equal(0, createOrderCallCount);
```

```csharp
[Fact]
public void T_B67_02_FlattenOneAccount_flat_position_noOp()
{
    // arrange: FindPosition returns null
    // act: invoke FlattenOneAccount via reflection
    // assert: cancelCallCount==0, createOrderCallCount==0
    throw new NotImplementedException("Engineer: implement using established reflection harness");
}
```

---

#### T_B67_03 — FlattenOneAccount_long_position_produces_Sell_Market

**What it asserts**: When the position is `MarketPosition.Long`, `acc.CreateOrder` is called
with `OrderAction.Sell` and `OrderType.Market`.

**Setup**:
- Stub `FindPosition` to return `Quantity=2`, `MarketPosition=Long`. No bracket orders.
- Capture the `action` and `orderType` arguments passed to `acc.CreateOrder`.

**Assert**:
```csharp
Assert.Equal(OrderAction.Sell, capturedAction);
Assert.Equal(OrderType.Market, capturedOrderType);
Assert.Equal(2, capturedQty);
```

```csharp
[Fact]
public void T_B67_03_FlattenOneAccount_long_position_produces_Sell_Market()
{
    // arrange: FindPosition returns Long qty=2
    // act: invoke FlattenOneAccount via reflection
    // assert: OrderAction.Sell, OrderType.Market, qty=2
    throw new NotImplementedException("Engineer: implement using established reflection harness");
}
```

---

#### T_B67_04 — FlattenOneAccount_short_position_produces_BuyToCover_Market

**What it asserts**: When the position is `MarketPosition.Short`, `acc.CreateOrder` is called
with `OrderAction.BuyToCover` and `OrderType.Market`.

**Setup**:
- Stub `FindPosition` to return `Quantity=1`, `MarketPosition=Short`. No bracket orders.
- Capture the `action` and `orderType` arguments passed to `acc.CreateOrder`.

**Assert**:
```csharp
Assert.Equal(OrderAction.BuyToCover, capturedAction);
Assert.Equal(OrderType.Market, capturedOrderType);
Assert.Equal(1, capturedQty);
```

```csharp
[Fact]
public void T_B67_04_FlattenOneAccount_short_position_produces_BuyToCover_Market()
{
    // arrange: FindPosition returns Short qty=1
    // act: invoke FlattenOneAccount via reflection
    // assert: OrderAction.BuyToCover, OrderType.Market, qty=1
    throw new NotImplementedException("Engineer: implement using established reflection harness");
}
```

---

### 6. 7-SCAN ENGINEER CONTRACT (Layer 1 scan chain — ALL must pass before ticket is DONE)

Execute every scan in order. A single failing scan = ticket is NOT complete. Do not skip any scan.

```
S1: grep src/PropTraderTools/CopyEngine.cs -n "lock(" | grep -v "//"     -> 0 hits
S2: grep src/PropTraderTools/CopyEngine.cs -n "throw new"                -> 0 hits in new code
S3: Verify FlattenOneAccount CYC = 4 (enumerate: guard, CancelQxBrackets, ternary, try/catch)
S4: grep src/PropTraderTools/CopyEngine.cs -n "[^\x00-\x7F]"             -> 0 new non-ASCII
    (pre-existing non-ASCII at lines 398, 499, ~1449-1450 are NOT regressions)
S5: dotnet build src/ -> 0 errors, 0 warnings
S6: dotnet test src/ --filter "T_B67" -> 4/4 pass
S7: SHA-256 match: CopyEngine.cs Wave <-> NT8 AddOn directory
```

**S1 — Lock scan**:
```powershell
grep -n "lock(" src/PropTraderTools/CopyEngine.cs | grep -v "//"
```
PASS = zero results.

**S2 — Throw new scan**:
```powershell
grep -n "throw new" src/PropTraderTools/CopyEngine.cs
```
PASS = zero hits in any new or modified lines introduced by this ticket.

**S3 — CYC scan** (manual):
Open the updated `FlattenOneAccount`. Enumerate branches:
1. `if (pos == null || pos.Quantity == 0)` — guard/early return
2. `CancelQxBrackets(acc, instrument)` — method call (segment per project CYC convention)
3. `pos.MarketPosition == MarketPosition.Long ? ... : ...` — ternary
4. `try/catch (Exception ex)` — exception handler
Confirm comment text reads: `CYC=4: (1) pos null/qty guard, (2) CancelQxBrackets, (3) action ternary, (4) try/catch.`
PASS = CYC comment matches enumeration.

**S4 — ASCII scan**:
```powershell
grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs
```
PASS = zero NEW non-ASCII characters introduced by this ticket.
NOTE: If lines 398, 499, or the area around 1449–1450 appear in grep output, those are
pre-existing non-ASCII (tracked as PRE-EXISTING-02 in the deferred backlog). They are NOT a
regression caused by this ticket. Do not change them. Report them in ticket-1-completion.md
as PRE-EXISTING, not as new violations.

**S5 — Build scan**:
```powershell
dotnet build src/
```
PASS = zero errors, zero warnings.

**S6 — Test scan**:
```powershell
dotnet test src/ --filter "T_B67"
```
PASS = 4 tests found, 4 pass, 0 fail, 0 skip.

**S7 — SHA-256 scan**:
```powershell
(Get-FileHash "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs").Hash
```
Record this hash in `ticket-1-completion.md`. After the deploy step below, confirm both hashes match.

---

### 7. MANDATORY DEPLOY STEP

After all 7 scans pass, deploy the updated file to the NinjaTrader 8 AddOn directory.

**Source**:
```
C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
```

**Destination**:
```
C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs
```

**Deploy command** (hard-link sync via project script):
```powershell
powershell -File C:\WSGTA\universal-or-strategy\deploy-sync.ps1
```

**Verify SHA-256 match** (both hashes must be identical — BUILD_PASS is NOT valid until this passes):
```powershell
(Get-FileHash "C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs").Hash
(Get-FileHash "C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs").Hash
```

Both hashes MUST match. Report the hash value in `ticket-1-completion.md`.
BUILD_PASS is only valid after SHA-256 match is confirmed.

---

### 8. Completion Artifact

Create `docs/brain/B67-LaneA/ticket-1-completion.md` documenting:

```markdown
## B67-LaneA Ticket-1 Completion Report

**Ticket**: B67-LaneA-T1
**Engineer**: [name]
**Date**: [date]

### Scan Results
| Scan | Command | Result | Notes |
|------|---------|--------|-------|
| S1 — lock() | grep -n "lock(" CopyEngine.cs \| grep -v "//" | PASS / FAIL | [output] |
| S2 — throw new | grep -n "throw new" CopyEngine.cs | PASS / FAIL | [output] |
| S3 — CYC=4 | Manual enumeration | PASS / FAIL | [list 4 branches] |
| S4 — ASCII | grep -Pn "[^\x00-\x7F]" CopyEngine.cs | PASS / FAIL | [pre-existing lines listed if any] |
| S5 — Build | dotnet build src/ | PASS / FAIL | [error count] |
| S6 — Tests | dotnet test --filter T_B67 | PASS / FAIL | [4/4 green] |
| S7 — SHA-256 | Get-FileHash | PASS / FAIL | [hash value] |

### SHA-256 Hash
Wave workspace: [hash]
NT8 AddOn directory: [hash]
Match: YES / NO

### Build Output
[paste dotnet build output]

### Test Results
[paste dotnet test output]

### DW-B67-01 Status
CLOSED — CancelQxBrackets inserted before acc.CreateOrder in FlattenOneAccount.
```

---

### 9. JS-DNA Compliance Summary

| Rule | Method | Status |
|------|--------|--------|
| JS-021 (no lock) | FlattenOneAccount, CancelQxBrackets | PASS — no lock() in new or modified code |
| JS-001 (no throw in hot path) | FlattenOneAccount catch block | PASS — catch logs, does not rethrow |
| JS-002 (no return null) | Both void methods | PASS — not applicable (void) |
| JS-036 (no new[] in hot path) | Single-line insert | PASS — zero new allocations introduced |
| ASCII-only | All new string literals | PASS — ASCII only, no Unicode, -> not Unicode arrow |
| DateTime.Now ban | DateTime.MaxValue (unchanged) | PASS |
| CYC <= 8 | FlattenOneAccount CYC=4 | PASS |

---

### 10. Scope Boundary

This ticket closes **DW-B67-01 only**.

The following items are explicitly OUT OF SCOPE for this ticket:
- DW-B67-02 (open in B67-LaneB — parallel lane)
- DW-B66-C-02, DW-B66-BE-01, DW-B63-01, DW-B54-01, DW-B58-x, PRE-EXISTING-01/02/03
- Any changes to `CancelQxBrackets` logic (method body is unchanged)
- Any other methods in CopyEngine.cs

If any pre-existing issue is discovered during engineering, REPORT it to the orchestrator.
Do NOT fix it in this ticket.

---

*Tickets status: TICKETS_COMPLETE*
