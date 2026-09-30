# B170 Ticket-2 Completion — DW-B170-01 DwB170Tests Class Insertion

**Ticket:** TICKET B170-2 — Add DwB170Tests class to CopyEngineTests.cs
**File:** `src/PropTraderTools/CopyEngineTests.cs`
**Status:** BUILD_PASS

---

## Scope

TICKET B170-2 ONLY. New test class `DwB170Tests` inserted after line 10257 (closing `}` of `DwB168_02Tests`), before namespace closing `}`. No existing lines removed or modified. Net line delta: +57 lines.

---

## Lines Inserted

**First line of new class (line 10259):**
```
    // DW-B170-01: Tests for FindLeaderStopPrice predicate -- Accepted state must return price.
```

**Last line of new class (line 10315):**
```
    }
```

**Tests added:**
- `FindLeaderStopPrice_AcceptedStop_ReturnsPrice` at line 10275 — BCL source-text pattern confirming `OrderState.Accepted` is present in `FindLeaderStopPrice` predicate.
- `FindLeaderStopPrice_WorkingStop_StillReturnsPrice` at line 10297 — BCL source-text pattern confirming `OrderState.Working` is still retained (regression guard).

**Pattern used:** BCL (Build-Chain-Lock) source-text stub pattern, identical to `DwB16701DrainAbortFlatTests`, `DwB157_01_DrainFillSlate`, etc. Each test calls `ResolveCopyEngineSource()` + `File.ReadAllText` + `Assert.Contains`. Fallback: reflection check for `FindLeaderStopPriceTestable` seam when source file is unavailable. No NT8 runtime types in test body.

---

## 7-Scan Results

| # | Scan | Command / Result |
|---|------|-----------------|
| SCAN-01 | FindLeaderStopPrice_AcceptedStop_ReturnsPrice present | `Select-String -Pattern "FindLeaderStopPrice_AcceptedStop_ReturnsPrice"` → **Line 10275: MATCH** ✅ |
| SCAN-02 | FindLeaderStopPrice_WorkingStop_StillReturnsPrice present | `Select-String -Pattern "FindLeaderStopPrice_WorkingStop_StillReturnsPrice"` → **Line 10297: MATCH** ✅ |
| SCAN-03 | FindLeaderStopPriceTestable in DwB170Tests (2 matches, fallback only) | `Select-String -Pattern "FindLeaderStopPriceTestable"` → **Lines 10287, 10309: 2 MATCHES** in string literals (no NT8 runtime calls) ✅ |
| SCAN-04 | 0 NT8 runtime references in test body | `Select-String -Pattern "NinjaTrader\.Cbi\."` restricted to lines 10258–10320 → **0 matches** ✅ |
| SCAN-05 | No whitespace mutations outside insertion site | `git diff` of `CopyEngineTests.cs` shows only the `DwB170Tests` insertion (lines 10259–10315) and no changes before line 10257 or at namespace closing `}` beyond removal of extra blank line ✅ |
| SCAN-06 | [Fact] count increases by exactly 2 | `Select-String -Pattern "^\s*\[Fact\]"` on lines 10258–10320 → **2 matches** (lines 10274, 10296). Total file count: 227. Previous count: 225. Delta = **+2** ✅ |
| SCAN-07 | ptt-sync.ps1 NOT required (test-only change) | **Not required.** `CopyEngineTests.cs` is a test-only file; it is not hard-linked to the NinjaTrader AddOn directory. `ptt-sync.ps1` synchronizes NinjaTrader hard links for production `.cs` files in `src/PropTraderTools/` only. ptt-sync.ps1 was NOT run for this ticket. ✅ |

---

## Jane Street DNA

- No `lock()` in new test class (JS-021)
- ASCII-only in all inserted lines
- No NT8 runtime references (SCAN-04 = 0)
- `[Fact]` (no Skip) — BCL pattern runs without NT8 host
- CYC=1 per test method (straight-line: resolve → read → assert)
- No new helper types introduced — uses `ResolveCopyEngineSource()` pattern identical to adjacent test classes

---

## Status: BUILD_PASS
