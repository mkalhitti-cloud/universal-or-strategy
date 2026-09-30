# PTT-COPIER-B5 Ticket T3 — Completion Report

**Ticket**: T3 — Tests: BreakEven xUnit tests + StatusUpdate teardown
**File**: `src/PropTraderTools/CopyEngineTests.cs` (Wave workspace)
**Date**: 2026-07-06
**Engineer**: PTT Engineer (B5)

---

## Summary of Every Change

| Change | File | Lines (after edit) | Description |
|--------|------|--------------------|-------------|
| Header comment updated | CopyEngineTests.cs | 1 | `B3` → `B5` in top comment |
| Class declaration modified | CopyEngineTests.cs | 12 | `public class CopyEngineTests` → `public class CopyEngineTests : IDisposable` |
| Field added | CopyEngineTests.cs | 15 | `private Action<string> _statusHandler;` added after `_engine` field |
| `[Fact]` method added | CopyEngineTests.cs | 226-237 | `BreakEven_NullInstrument_NoException` |
| `[Fact]` method added | CopyEngineTests.cs | 239-253 | `BreakEven_NoMatchingRule_FiresNoStatusUpdate` |
| `Dispose()` method added | CopyEngineTests.cs | 255-262 | IDisposable implementation; unsubscribes `_statusHandler` |

---

## All 7 Scan Results

| Scan | Command | Result | Status |
|------|---------|--------|--------|
| S1 | `Select-String -Pattern "lock\s*\("` | **0** hits | PASS |
| S2 | `Select-String -Pattern "DateTime\.Now[^U]"` | **0** hits | PASS |
| S3 | `Select-String -Pattern "0x[0-9A-Fa-f]"` | **0** hits | PASS |
| S4 | Non-ASCII character scan (PowerShell regex `[^\x00-\x7F]`) | **0** non-ASCII chars | PASS |
| S5 | CYC check on all new methods | `BreakEven_NullInstrument_NoException`: CYC=1; `BreakEven_NoMatchingRule_FiresNoStatusUpdate`: CYC=1; `Dispose`: CYC=2 | PASS (all <= 8) |
| S6 | Using directive count check | All 5 original directives present: `System`, `System.Collections.Concurrent`, `System.Reflection`, `NinjaTrader.Cbi`, `Xunit` — none removed | PASS |
| S7 | Brace balance: open=31, close=31, balance=0 | **0** imbalance | PASS |

---

## Explicit Statement

**No existing B1-B3 test methods altered.**

All 14 existing `[Fact]` methods (lines 22-225) are completely untouched. The only structural
change to the class declaration was appending `: IDisposable`. The `_engine` field and both
reflection helpers (`GetField`, `GetMethod`) are unchanged. No existing `using` directive was
removed or modified.

---

## All [Fact] Methods Now in the File

| # | Method Name | Block | Lines |
|---|-------------|-------|-------|
| 1 | `SetEnabled_True_EnablesGate1` | B3 | 23-30 |
| 2 | `SetEnabled_False_BlocksGate1` | B3 | 32-40 |
| 3 | `SetDailyCapFloor_SetsFloor` | B3 | 42-50 |
| 4 | `SetDailyCapFloor_DefaultIsNegative500` | B3 | 52-60 |
| 5 | `SetRuleEnabled_False_MarksRuleDisabled` | B3 | 62-80 |
| 6 | `SetRuleEnabled_True_ReenablesRule` | B3 | 82-101 |
| 7 | `SetRuleEnabled_UnknownInstrument_NoException` | B3 | 103-113 |
| 8 | `AddRule_AddsRuleToEngine` | B3 | 115-128 |
| 9 | `AddRule_StringOverload_NoException` | B3 | 130-136 |
| 10 | `StatusUpdate_FiresOnSetEnabled` | B3 | 138-146 |
| 11 | `StatusUpdate_MessageContainsON_WhenEnabled` | B3 | 148-157 |
| 12 | `StatusUpdate_MessageContainsOFF_WhenDisabled` | B3 | 159-168 |
| 13 | `SetRuleEnabled_WithNullAccounts_NoException` | B3 | 170-177 |
| 14 | `Flatten_EngineAPI_Callable` | B3 | 179-185 |
| 15 | `CancelPendingEntries_EngineAPI_Callable` | B3 | 187-193 |
| 16 | `IsDedup_SameOrderId_ReturnsTrueOnSecondCall` | B3 | 195-208 |
| 17 | `IsDedup_DifferentOrderIds_BothAccepted` | B3 | 210-225 |
| 18 | `BreakEven_NullInstrument_NoException` | **B5** | 226-237 |
| 19 | `BreakEven_NoMatchingRule_FiresNoStatusUpdate` | **B5** | 239-253 |

**Total**: 19 `[Fact]` methods (14 existing + 2 new B5)

---

## Final Line Count

**265 lines** (was 227 lines; +38 lines added)

---

## Implementation Notes

- `_statusHandler` is typed `Action<string>` to match `CopyEngine.StatusUpdate`'s delegate signature.
- `BreakEven_NoMatchingRule_FiresNoStatusUpdate` uses `_engine.BreakEven(null, 2)` — `null` instrument triggers `FindRule(null)` null guard (CopyEngine.cs line 351), returning null, so `AllAccounts` yields zero accounts, and `StatusUpdate` never fires.
- `Dispose()` guards against double-unsubscribe by nulling `_statusHandler` after removal.
- xUnit creates a new class instance per `[Fact]` — `Dispose()` is called once after each test, cleaning up any `_statusHandler` subscription set during that test.
- Brace balance verified: 31 open = 31 close.
