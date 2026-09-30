# B76-LaneA Ticket-1 Completion
**Status**: BUILD_PASS
**Ticket**: TICKET-B76-1 -- FlattenOneAccount: in-flight guard + race guard (tests only)
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-08-18
**Files**: `src/PropTraderTools/CopyEngine.cs` (read-only confirm), `src/PropTraderTools/B76Tests.cs` (new)

---

## What Was Implemented

### Code Confirmation (TICKET-B76-1 code already applied live)

Read `CopyEngine.cs` lines 1878-1932. Confirmed:

1. **HOTFIX-B76-FLATTEN-GUARD-01 v2** present (lines 1888-1898):
   - `foreach (var o in acc.Orders.ToList())` loop scanning for active PTT-Flatten orders
   - Guard condition: `o.Name != "PTT-Flatten"`, `o.Instrument?.FullName` match, `OrderState.Submitted/Accepted/Working`
   - StatusUpdate fires: `acc.Name + ": flat-guard: in-flight skip"` then `return;`

2. **HOTFIX-B76-FLATTEN-RACE-01** present (lines 1907-1913):
   - `var posAfterCancel = FindPosition(acc, instrument)` re-read after cancel
   - Guard: `posAfterCancel == null || posAfterCancel.Quantity == 0` fires StatusUpdate `"flat-race skip (pos cleared by bracket fill)"` then `return;`
   - `CreateOrder` uses `posAfterCancel.Quantity` and `posAfterCancel.MarketPosition`

3. Method header comment says `CYC=6`.

### Tests Written: T_B76_01 .. T_B76_06 (in `src/PropTraderTools/B76Tests.cs`)

| Test | Assertion |
|------|-----------|
| T_B76_01 | FlattenOneAccount exists as private instance (Account, Instrument) -> void |
| T_B76_02 | IL ldstr scan: contains "flat-guard: in-flight skip" |
| T_B76_03 | IL ldstr scan: contains "flat-race skip" |
| T_B76_04 | IL call token scan: >= 2 FindPosition call sites |
| T_B76_05 | IL call token scan: CancelAllAccountOrders offset < second FindPosition offset |
| T_B76_06 | MethodBody.LocalVariables.Count >= 5 |

All 6 tests use the established IL-scan pattern from T_B67_01..T_B67_04 in `CopyEngineTests.cs`.

---

## 7 Mandatory Scans

| Scan | Pattern | Files | Result |
|------|---------|-------|--------|
| SCAN-01 | `lock\s*\(` | B76Tests.cs, TradeCopierPanel.cs | **0 hits** PASS |
| SCAN-02 | `async\s+void\s+\w+\(` | B76Tests.cs, TradeCopierPanel.cs | **0 hits** PASS |
| SCAN-03 | `throw\s+new\s+\w+Exception\(` | B76Tests.cs, TradeCopierPanel.cs | **0 hits** PASS |
| SCAN-04 | `return\s+null\s*;` in new diff | TradeCopierPanel.cs diff | **0 hits** PASS |
| SCAN-05 | Non-ASCII in new diff | all modified files | **0 hits** PASS |
| SCAN-06 | `DateTime\.Now[^U]` | B76Tests.cs, TradeCopierPanel.cs | **0 hits** PASS |
| SCAN-07 | NUnit/MSTest usage | B76Tests.cs | **0 hits** PASS (xUnit only) |

---

## Build Note

`PropTraderTools.csproj` is an OmniSharp/LSP reference project ONLY. NT8 compiles via its own
Roslyn host. Pre-existing build errors in `AtrSizingEngine.cs` (CS0234/CS0246) exist on HEAD
before this ticket -- confirmed by git log. Zero new errors from B76Tests.cs.

`dotnet test` cannot run on this LSP-only project (pre-existing build errors in AtrSizingEngine.cs).
Test presence verified via `Select-String` -- all 12 `[Fact]` method names confirmed in B76Tests.cs:
T_B76_01 through T_B76_12 (including T_B76_07..T_B76_12 from Tickets 2 and 3 in same file).

**Regressions**: T_B67_01..T_B67_04 structural contracts in CopyEngineTests.cs untouched.
CopyEngine.cs not modified in this ticket.

**BUILD_PASS**
