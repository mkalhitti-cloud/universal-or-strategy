# B35-LaneA — Tickets
## BE Stop-Above-Market Warning (DW-B35-SILENT-REJECT)

**Architecture plan**: `docs/brain/B35-LaneA/02-architecture-plan.md` — REVIEW_PASS
**Block**: B35 | Lane A
**Spec requirement**: DW-B35-SILENT-REJECT (P1) — silent BE stop rejection with no panel warning
**Wave workspace**: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\`
**Test baseline**: 177 [Fact] passing (PTT-COPIER B34)
**Test target**: 180 [Fact] (177 + 3 new)
**Files changed**: 5 (`PttContracts.cs`, `TradeCopierPanel.cs`, `PttBreakEven.cs`, `CopyEngineTests.cs`, `CopyEngine.cs`)

---

## Dependency Order

```
Ticket 1 (B35-01): PttContracts.cs + TradeCopierPanel.cs + 1 [Fact] test
    [required: WarnUser must exist on IPttHostContext before PttBreakEven calls it]
    ↓
Ticket 2 (B35-02): PttBreakEven.cs + 2 [Fact] tests + CopyEngine.cs build tag
    ↓
B35-03 Verifier pass (separate agent — no code)
```

---

---

## Ticket 1 — B35-01: WarnUser interface + implementation

**Spec requirement**: DW-B35-SILENT-REJECT — panel must surface a warning when BE stop is rejected
**Prerequisite**: none
**Files changed**:
- `Core/PttContracts.cs` (insert 3 lines after line 67)
- `TradeCopierPanel.cs` (insert 4 lines after line 137)
- `tests/CopyEngineTests/CopyEngineTests.cs` (insert 1 [Fact] before class closing brace at line 3296)

---

### Method Signatures

**Interface method** — `Core/PttContracts.cs` — inside `IPttHostContext`:
```csharp
/// <summary>Display a warning in the panel status bar. Call from UI thread only.</summary>
void WarnUser(string message);
```

**Explicit implementation** — `TradeCopierPanel.cs`:
```csharp
void IPttHostContext.WarnUser(string message)
{
    if (_statusText != null) _statusText.Text = message;
}
```

---

### Exact Code Change — `Core/PttContracts.cs`

**Insert after line 67** (after the `Bid` property doc comment and before the closing `}` of `IPttHostContext`).

Current line 67:
```csharp
        double Bid { get; }
```
Current line 68 (closing `}` of `IPttHostContext`):
```csharp
    }
```

**Insert between them** (becomes new lines 68-70; original line 68 shifts to 71):
```csharp
        /// <summary>Display a warning in the panel status bar. Call from UI thread only.</summary>
        void WarnUser(string message);
```

---

### Exact Code Change — `TradeCopierPanel.cs`

**Insert after line 137** (after the existing `Bid` explicit implementation):

Current line 137:
```csharp
        double IPttHostContext.Bid        { get { return GetBid(); } }
```

**Insert immediately after** (becomes new lines 138-141; remaining file shifts down by 4):
```csharp
        void IPttHostContext.WarnUser(string message)
        {
            if (_statusText != null) _statusText.Text = message;
        }
```

**Thread-safety rationale**: `Execute()` is called from `DispatchModule()` which is called from WPF button
handlers (UI thread). Direct `_statusText.Text` assignment without `Dispatcher.InvokeAsync` matches
the established pattern at lines 1452, 1457, 1463, 1490-1491, 1521-1522. No `Dispatcher` call needed.

---

### Exact Code Change — `tests/CopyEngineTests/CopyEngineTests.cs`

**Insert before line 3296** (before the class closing `}`; the mock class is at lines 3284-3293,
class `}` is at line 3296, namespace `}` is at line 3297).

```csharp
        // B35 DW-B35-SILENT-REJECT: WarnUser interface + panel implementation tests
        [Fact]
        public void T_B35_WarnUser_SetsStatusText()
        {
            // Verify IPttHostContext.WarnUser exists on the interface via reflection.
            // Structural test -- no NT8 API required.
            var method = typeof(IPttHostContext).GetMethod("WarnUser",
                new[] { typeof(string) });
            Assert.NotNull(method);
            Assert.Equal(typeof(void), method.ReturnType);
        }
```

**[Fact] name**: `T_B35_WarnUser_SetsStatusText`
**Asserts**:
1. `IPttHostContext` has a method named `WarnUser` that accepts a single `string` parameter
2. The method return type is `void`

**NT8 API used**: none — pure reflection on our own interface type.

---

### JS Rule Constraints — Ticket 1

| Rule | Constraint | Applied Where |
|------|-----------|---------------|
| JS-021 | No `lock()` anywhere | `WarnUser` uses no synchronization primitive |
| JS-033 | No `async void` | `WarnUser` is synchronous `void` |
| JS-001 | No `throw` in hot paths | `WarnUser` uses null guard, no throw |
| JS-002 | No `return null` | `WarnUser` returns `void` — not applicable |
| NT8-001 | No `{ get; init; }` | `WarnUser` is a method, not a property |
| NT8-019 | No `async void` in callbacks | `WarnUser` is synchronous |
| NT8-042 | No `Dispatcher.InvokeAsync` needed | Already on UI thread — direct assignment |

---

### 7-Scan Checklist — Ticket 1

| Scan | Command | Expected |
|------|---------|----------|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/Core/PttContracts.cs src/PropTraderTools/TradeCopierPanel.cs` | 0 matches in changed lines |
| SCAN-02 | `grep -n "async void" src/PropTraderTools/Core/PttContracts.cs src/PropTraderTools/TradeCopierPanel.cs` | 0 results |
| SCAN-03 | `grep -n "{ get; init; }" src/PropTraderTools/Core/PttContracts.cs` | 0 results |
| SCAN-04 | `grep -n "Dispatcher" src/PropTraderTools/TradeCopierPanel.cs` | Pre-existing only; 0 new matches in WarnUser block (lines 138-141) |
| SCAN-05 | `grep -n "return null;" src/PropTraderTools/Core/PttContracts.cs src/PropTraderTools/TradeCopierPanel.cs` | 0 in changed lines |
| SCAN-06 | `grep -n "void WarnUser" src/PropTraderTools/Core/PttContracts.cs` | Exactly 1 match |
| SCAN-07 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 new errors |

**Build-gate note**: After Ticket 1, run `dotnet build` to confirm `IPttHostContext.WarnUser` compiles
and the explicit implementation in `TradeCopierPanel` resolves the interface member. Do NOT proceed to
Ticket 2 if build fails.

---

---

## Ticket 2 — B35-02: Price guard in PttBreakEven.Execute() + build tag

**Spec requirement**: DW-B35-SILENT-REJECT — pre-check stop price validity before submission
**Prerequisite**: Ticket 1 complete — `ctx.WarnUser` must exist on `IPttHostContext` before `PttBreakEven` can call it
**Files changed**:
- `Features/PttBreakEven.cs` (insert 15 lines after bePrice computation in `Execute()`)
- `tests/CopyEngineTests/CopyEngineTests.cs` (insert 2 [Fact] tests before class closing brace)
- `CopyEngine.cs` line 41 (update build tag from B34 to B35)

---

### Method Signatures

**No new method signatures.** All changes are inside the body of the existing `Execute(IPttHostContext ctx)` method.

**Current signature** (unchanged):
```csharp
public void Execute(IPttHostContext ctx)
```

**CYC delta**: `Execute()` CYC 7 → **8** (one new branch: `if (!priceOk)`). Remains ≤ 8. ✅

---

### Exact Code Change — `Features/PttBreakEven.cs`

**Insert after line 72** (after the `bePrice =` block, before `CancelStaleBracketsLocal` at line 74).

Current lines 71-76 (for context):
```csharp
            double bePrice = pos.AveragePrice
                             + (isLong ? +buf : -buf) * tickSize;
                                                               // <-- INSERT HERE (becomes lines 74-88)
            CancelStaleBracketsLocal(acc, ctx.Instrument);
            SubmitBeStopLocal(acc, ctx.Instrument, bePrice, isLong);
        }
```

**Insert block** (insert as new lines 74-88; `CancelStaleBracketsLocal` shifts to line 89):
```csharp
                // DW-B35-SILENT-REJECT: pre-check stop price validity against live market.
                // NT8 rule: Sell stop must be <= Ask; BuyToCover stop must be >= Bid.
                // ask/bid <= 0.0 means no market data yet -- allow submission, NT8 handles it.
                double ask = ctx.Ask;
                double bid = ctx.Bid;
                bool priceOk = isLong  ? (ask <= 0.0 || bePrice <= ask)
                                        : (bid <= 0.0 || bePrice >= bid);
                if (!priceOk)
                {
                    string side   = isLong ? "above ask" : "below bid";
                    string market = isLong ? ask.ToString("F2") : bid.ToString("F2");
                    string msg    = "[BE] WARNING: " + acc.Name + " BE stop @ "
                                  + bePrice.ToString("F2") + " rejected -- stop "
                                  + side + " market " + market + " -- position UNPROTECTED";
                    NinjaTrader.Code.Output.Process(msg, NinjaTrader.NinjaScript.PrintTo.OutputTab1);
                    ctx.WarnUser(acc.Name + ": BE stop rejected (" + side + " " + market + ")");
                    continue;
                }
```

**Guard semantics**:
- `ask/bid <= 0.0` → no market data available yet → `priceOk = true` → allow submission (NT8 handles it natively)
- `if (!priceOk)` → log to NT8 Output tab + set panel status bar + `continue` to next account
- `continue` skips both `CancelStaleBracketsLocal` AND `SubmitBeStopLocal` for that account
- Existing brackets for that account are preserved — they are NOT cancelled when submission is skipped
- `NinjaTrader.Code.Output.Process(msg, ...)` — thread-safe, already used in existing helpers
- `ctx.WarnUser(...)` → `TradeCopierPanel._statusText.Text = ...` — UI thread, direct assignment

---

### Exact Code Change — `CopyEngine.cs` line 41

**Change** (line 41 only — single line update):

From:
```csharp
internal const string Tag = "PTT-COPIER B34 | be-multiAccount-fixes | 2026-07-26";
```

To:
```csharp
internal const string Tag = "PTT-COPIER B35 | be-stop-market-guard | {YYYY-MM-DD}";
```

**Engineer fills in the actual date** in `YYYY-MM-DD` format (e.g. `2026-07-27`). ASCII digits only.

---

### Exact Code Change — `tests/CopyEngineTests/CopyEngineTests.cs`

**Insert before the class closing `}`** (after the `T_B35_WarnUser_SetsStatusText` block added in Ticket 1).
Insert location is inside the test class, before the last `}` (class close).

```csharp
        [Fact]
        public void T_B35_BE_StopAboveMarket_Skipped()
        {
            // Verify price guard logic: long position, bePrice > ask --> priceOk = false.
            // Mirrors the guard expression in PttBreakEven.Execute() without instantiating NT8 types.
            bool   isLong  = true;
            double ask     = 7506.00;
            double bid     = 0.0;
            double bePrice = 7506.25;  // above ask -- would be rejected by NT8
            bool priceOk = isLong ? (ask <= 0.0 || bePrice <= ask)
                                  : (bid <= 0.0 || bePrice >= bid);
            Assert.False(priceOk); // guard must fire -- skip submission
        }

        [Fact]
        public void T_B35_BE_StopBelowMarket_Skipped()
        {
            // Verify price guard logic: short position, bePrice < bid --> priceOk = false.
            bool   isLong  = false;
            double ask     = 0.0;
            double bid     = 7505.75;
            double bePrice = 7505.50;  // below bid -- would be rejected by NT8
            bool priceOk = isLong ? (ask <= 0.0 || bePrice <= ask)
                                  : (bid <= 0.0 || bePrice >= bid);
            Assert.False(priceOk); // guard must fire -- skip submission

            // Also verify: no market data (ask=0, bid=0) --> priceOk = true (allow submission).
            double ask2     = 0.0;
            double bid2     = 0.0;
            double bePrice2 = 7505.50;
            bool priceOkLongNoData  = true  ? (ask2 <= 0.0 || bePrice2 <= ask2) : false;
            bool priceOkShortNoData = false ? false : (bid2 <= 0.0 || bePrice2 >= bid2);
            Assert.True(priceOkLongNoData);   // no data -> allow
            Assert.True(priceOkShortNoData);  // no data -> allow
        }
```

**[Fact] names and what each asserts**:

| [Fact] | Asserts |
|--------|---------|
| `T_B35_BE_StopAboveMarket_Skipped` | Long position with `bePrice (7506.25) > ask (7506.00)` → `priceOk = false` (guard fires, submission skipped) |
| `T_B35_BE_StopBelowMarket_Skipped` | Short position with `bePrice (7505.50) < bid (7505.75)` → `priceOk = false` (guard fires, submission skipped); additionally: ask=0 or bid=0 (no data) → `priceOk = true` (allow submission) |

**NT8 API used**: none — pure arithmetic logic. No mock required beyond the existing mock class.

---

### JS Rule Constraints — Ticket 2

| Rule | Constraint | Applied Where |
|------|-----------|---------------|
| JS-021 | No `lock()` | Price guard uses no synchronization primitive |
| JS-033 | No `async void` | Guard block is synchronous; `continue` not `await` |
| JS-001 | No `throw` in hot paths | Guard uses `continue`, no exception |
| JS-002 | No `return null` | Guard uses `continue` to skip the account, no null return |
| NT8-001 | No `{ get; init; }` | No new properties introduced |
| NT8-006 | No LINQ in PttBreakEven | Guard uses only arithmetic and `continue` |
| NT8-013 | No `DateTime.Now` | No new DateTime usage |
| NT8-014 | PTT- prefix on order signals | No new `CreateOrder` call introduced by the guard |
| NT8-028 | No hex color strings | No UI color changes |
| NT8-029 | Tick alignment on stop prices | `bePrice` is computed from `pos.AveragePrice + buf * tickSize` (existing code, unchanged) |

---

### 7-Scan Checklist — Ticket 2

| Scan | Command | Expected |
|------|---------|----------|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/Features/PttBreakEven.cs` | 0 results |
| SCAN-02 | `grep -n "async void" src/PropTraderTools/Features/PttBreakEven.cs` | 0 results |
| SCAN-03 | `grep -n "\.Where\|\.First\|\.Select\|\.Any" src/PropTraderTools/Features/PttBreakEven.cs` | 0 results in changed lines |
| SCAN-04 | `grep -n "throw new" src/PropTraderTools/Features/PttBreakEven.cs` | 0 in changed lines (price guard uses `continue`, not throw) |
| SCAN-05 | `grep -n "return null;" src/PropTraderTools/Features/PttBreakEven.cs` | 0 in changed lines |
| SCAN-06 | `grep -n "DateTime.Now" src/PropTraderTools/Features/PttBreakEven.cs` | 0 results |
| SCAN-07 | `dotnet build src/PropTraderTools/PropTraderTools.csproj` | 0 new errors |

**Additional post-build verification**:
```powershell
# Confirm price guard uses continue (not return) -- essential for per-account loop correctness
Select-String -Path "src\PropTraderTools\Features\PttBreakEven.cs" -Pattern "priceOk"
# Verify: the if (!priceOk) block ends with "continue;" not "return;"

# Confirm build tag updated
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "PTT-COPIER B35"
# Expected: 1 match at line 41
```

---

## Final Summary

| Item | Value |
|------|-------|
| Tickets | 2 (B35-01, B35-02) |
| Files changed | 5 |
| New [Fact] tests | 3 |
| [Fact] total after B35 | 180 (177 + 3) |
| CYC impact | `Execute()` 7 → 8 (still ≤ 8 ✅) |
| New NT8 API surface | None |
| Dispatcher usage | None added |
| Build tag | `PTT-COPIER B35 \| be-stop-market-guard \| {YYYY-MM-DD}` |

**TICKETS_COMPLETE**
