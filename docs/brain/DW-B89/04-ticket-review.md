# Ticket Review: DW-B89 -- BE-ALL OCO Reuse + Silent Stop Rejection
**Reviewer**: ptt-ticket-reviewer
**Date**: 2026-08-23
**Source tickets**: docs/brain/DW-B89/04-tickets.md
**Plan**: docs/brain/DW-B89/02-architecture-plan.md (REVIEW_PASS)
**Rules**: docs/standards/jane-street/RULES_CATALOG.md

---

## T1 -- CopyEngine.cs Seed Fix

**Traceability**: PASS
- T1 maps to spec requirement DW-B89-01 (OCO ID reuse root cause fix).
- Plan Section 2 File 1 (CopyEngine.cs L205 seed fix) fully represented.
- No phantom work. No missing plan items.

**Spec Compliance -- Verbatim before/after L205**: PASS
- BEFORE: `private volatile int _mstbeOcoSeq = Environment.TickCount;`
- AFTER: `private volatile int _mstbeOcoSeq = Math.Abs(Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF));`
- Exact match to spec formula. No deviation.

**Spec Compliance -- Comment block update (lines 199-204)**: PASS
- BEFORE and AFTER comment blocks specified verbatim.
- Minor note: spec says "lines 200-204", ticket says "lines 199-204". Acceptable: lines shift during edit; content is fully specified.

**JS Pre-Check**: PASS
- JS-021 (P0): No `lock()` added. `volatile int` + `Interlocked.Increment` pattern preserved. PASS.
- JS-023 (P1): `volatile int` field preserved. `Interlocked.Increment` in `NextBeOcoSeq()` unchanged. XOR seed is a field initializer expression (single-threaded construction). PASS.
- JS-033 (P0): No async code touched. PASS.
- JS-001 (P0): No new `throw` statements. PASS.
- JS-002 (P0): No `return null` in any new code. PASS.
- ASCII-only: `Math.Abs(...)` is pure ASCII. PASS.
- `DateTime.Now` ban: XOR formula uses `DateTime.UtcNow.Ticks`. `DateTime.Now` does not appear. PASS.

**CYC Pre-Check**: PASS
- `NextBeOcoSeq()` CYC = 1 (unchanged). Field initializer expression: no branches.

**NT8 Check**: PASS
- No NT8 API calls in scope. No NT8 constraints violated.

**Test Coverage**: PASS
- No new methods introduced in T1. Seed entropy indirectly validated by T3's `T_OCO_SEED_03_NextBeOcoSeq_D7Format_SevenDigitPadding`. Rationale accepted per plan Section 7 (Ticket T1).

**Scan Checklist**: PASS
- All 7 scans present (SCAN-01 through SCAN-07) with exact command and exact expected result.
- SCAN-05 scopes to `src/PropTraderTools/Features/` only. PASS.
- SCAN-06 targets `PttBreakEvenSwap.cs` specifically. PASS.

**File Routing**: PASS
- File: `src/PropTraderTools/CopyEngine.cs` -- Wave workspace (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`). Correct.

**VERDICT: TICKET_REVIEW_PASS**

---

## T2 -- PttBreakEvenSwap.cs Full Change Set

**Traceability**: PASS
- T2 maps to DW-B89-01 (D7 format string, Change 1) and DW-B89-02 (catch logging Change 2, submittability guard Changes 3-5).
- Plan Section 2 File 2 (all 5 changes + header comment update) fully represented.
- No phantom work. No missing plan items.

**Spec Compliance -- Change 1 (D5→D7 L84)**: PASS
- BEFORE: `+ "-" + seq.ToString("D5") + "-" + i;`
- AFTER: `+ "-" + seq.ToString("D7") + "-" + i;`
- Verbatim. Exact match.

**Spec Compliance -- All 3 bare catch replacements**: PASS
- Ticket explicitly states "There are exactly **3** bare `catch { /* non-fatal */ }` blocks (lines 73, 101, 118)".
- Identical logging catch replacement specified for all 3.
- `[BE-ERR]` prefix present in all log messages.
- `ex` variable referenced via `ex.Message` (no unused variable warning).

**Spec Compliance -- IsStopPriceSubmittable signature**: PASS
- `private static bool IsStopPriceSubmittable(Instrument instr, bool isLong, double stopPrice)` -- exact match to spec.

**Spec Compliance -- isLong branch returns true**: PASS
- `if (isLong) return true;` -- correct. Sell StopMarket (short entry bracket stop, below market) is valid in NT8. Guard is needed only for BuyToCover StopMarket.

**Spec Compliance -- ask==0 branch returns true (fail-open)**: PASS
- `if (ask == 0.0) return true;` -- fail-open when no market data subscription. Correct per spec.

**Spec Compliance -- with-targets path guarded**: PASS
- Change 4 provides full verbatim BEFORE/AFTER wrapping the stop-submit block in `if (IsStopPriceSubmittable(instr, isLong, newStop))`.
- `if (sOrd != null)` guard removed; catch absorbs NullReferenceException. CYC exchange documented.
- `else` branch logs `[BE-ERR] ... stop below market @ ... -- skipping tranche`.

**Spec Compliance -- 0-targets path guarded**: PASS
- Change 5 provides full verbatim BEFORE/AFTER wrapping the bare-stop block in `if (IsStopPriceSubmittable(instr, isLong, newStop))`.
- `if (bareStop != null)` guard removed; catch absorbs NullReferenceException. CYC exchange documented.
- `else` branch logs `[BE-ERR] ... PTT-BE-Stop stop below market @ ... -- skipping tranche`.

**Spec Compliance -- Execute() CYC <= 8**: PASS
- CYC Analysis table enumerates all 8 branches:
  (1) null guard, (2) flat guard, (3) direction ternary, (4) 0-targets branch,
  (5) IsStopPriceSubmittable 0-targets guard (NEW), (6) for-loop,
  (7) IsStopPriceSubmittable per-tranche guard (NEW), (8) if(tOrd!=null).
- Removed: `if(bareStop!=null)` and `if(sOrd!=null)`.
- CYC = 8. Limit <= 8. Stated and verified in ticket.

**Spec Compliance -- IsStopPriceSubmittable CYC <= 3**: PASS
- CYC = 2 stated with both branches identified. Limit <= 3. Advisory A1 (null-conditional refactor if Lizard flags it) present.

**JS Pre-Check**: PASS
- JS-001 (P0): No new `throw` statements. `catch(Exception ex)` logs only. PASS.
- JS-002 (P0): `IsStopPriceSubmittable` returns `bool`. No `return null` anywhere in new code. PASS.
- JS-021 (P0): No `lock()` added. No locking construct of any kind. PASS.
- JS-023 (P1): `Execute()` is synchronous, per-account. No shared state introduced. PASS.
- JS-033 (P0): `Execute()` is `static void` (synchronous). `IsStopPriceSubmittable` is `static bool`. No async methods added. PASS.
- ASCII-only: `[BE-ERR]`, `"submit failed: "`, `"stop below market @ "`, `"skipping tranche"` -- all ASCII. PASS.
- `DateTime.Now` ban: `DateTime.MaxValue` used for GTC (not `DateTime.Now`). PASS.

**CYC Pre-Check**: PASS
- `Execute()` CYC = 8 (at limit, not over). PASS.
- `IsStopPriceSubmittable` CYC = 2 (well within <= 3). PASS.

**NT8 Check**: PASS
- NT8-049: arg6=limitPrice, arg7=stopPrice preserved in all `CreateOrder` calls. PASS.
- NT8-007: arg11 = `(NinjaTrader.Cbi.CustomOrder)null` preserved. PASS.
- NT8-013: arg10 = `DateTime.MaxValue` for GTC preserved. PASS.
- NT8-014: Signal names start with `PTT-` (`"PTT-BE-Stop"`, `"PTT-BE-Stop-" + (i + 1)`, `"PTT-BE-Target-" + (i + 1)`). PASS.

**Test Coverage**: PASS
- No new public/internal methods. `IsStopPriceSubmittable` is `private static` and verified via SIM gate + SCAN-02. Accepted per plan Section 7 (Ticket T2).

**Scan Checklist**: PASS
- All 7 scans present (SCAN-01 through SCAN-07) with exact command and exact expected result.
- SCAN-02 specifies both `Execute()` CYC = 8 exactly AND `IsStopPriceSubmittable()` CYC <= 3. PASS.
- SCAN-05 scopes to `src/PropTraderTools/Features/` only. PASS.
- SCAN-06 targets `src/PropTraderTools/Features/PttBreakEvenSwap.cs` specifically with required result 0 matches. PASS.

**File Routing**: PASS
- File: `src/PropTraderTools/Features/PttBreakEvenSwap.cs` -- Wave workspace. Correct.

**VERDICT: TICKET_REVIEW_PASS**

---

## T3 -- PttBreakEven.cs D7 Alignment + T_OCO_SEED_03 Test Update

**Traceability**: PASS
- T3 maps to DW-B89-01 (D7 alignment across all BE OCO paths).
- Plan Section 2 File 3 (PttBreakEven.cs L357 + L10) and File 4 (CopyEngineB72Tests.cs rename + assertion) fully represented.
- No phantom work. No missing plan items.
- `PttGlobalBreakEven.cs` correctly declared out of scope in T3 body with explicit prohibition.

**Spec Compliance -- PttBreakEven.cs L357 D5→D7**: PASS
- BEFORE: `return "PTT-BE-" + prefix + "-" + seq.ToString("D5") + "-" + pairIndex.ToString();`
- AFTER: `return "PTT-BE-" + prefix + "-" + seq.ToString("D7") + "-" + pairIndex.ToString();`
- Verbatim. Exact match to plan Section 2 File 3 Change 1.

**Spec Compliance -- PttBreakEven.cs L10 header comment update**: PASS
- BEFORE: `//   New formula: "PTT-BE-"+accPrefix+"-"+seq.ToString("D5")+"-"+pairIndex  (always unique)`
- AFTER: `//   New formula: "PTT-BE-"+accPrefix+"-"+seq.ToString("D7")+"-"+pairIndex  (always unique, DW-B89-01)`
- Verbatim. Present.

**Spec Compliance -- T_OCO_SEED_03 D7 assertion update**: PASS
- Change B2 specifies BEFORE (D5 / 5-char padding assertions) and AFTER:
  `Assert.True(formatted.Length >= 7)` and `Assert.Matches(@"^\d{7,}$", formatted)`.
- Covers all assertion forms (`Assert.Equal(5,...)` and `Assert.Matches` with 5-digit pattern).
- Preserves all other assertions verbatim.

**Spec Compliance -- Method rename specified**: PASS
- BEFORE: `public void T_OCO_SEED_03_NextBeOcoSeq_D5Format_FiveDigitPadding()`
- AFTER: `public void T_OCO_SEED_03_NextBeOcoSeq_D7Format_SevenDigitPadding()`
- Exact match to spec constraint.

**Spec Compliance -- .bobignore / execute_command guidance**: PASS
- Explicit "WARNING -- FILE ACCESS" block present: engineer MUST use `execute_command` (Get-Content / Set-Content). `read_file` and `write_file` tools prohibited for this file.

**JS Pre-Check**: PASS
- JS-021 (P0): No `lock()` added. PASS.
- JS-033 (P0): No async code touched. PASS.
- ASCII-only: `"D7"` and `@"^\d{7,}$"` are ASCII. PASS.
- `BuildBeOcoId` CYC = 2 (unchanged). String literal `"D5"` → `"D7"` is not a branch. PASS.

**CYC Pre-Check**: PASS
- `BuildBeOcoId` CYC = 2 (unchanged). PASS.

**NT8 Check**: PASS
- No NT8 API calls modified in T3. `BuildBeOcoId` is pure string computation. PASS.

**Test Coverage**: PASS
- `[Fact]` test `T_OCO_SEED_03_NextBeOcoSeq_D7Format_SevenDigitPadding` is a renamed and updated existing test -- not a new untested method. All assertions specified in ticket. PASS.
- Pre-existing 83 errors in `CopyEngineTests.cs` correctly declared out of scope (DW-PTT-BE-FIX-03). Do-not-fix directive present. PASS.

**Scan Checklist**: PASS
- All 7 scans present (SCAN-01 through SCAN-07) with exact command and exact expected result.
- SCAN-06 correctly marked "Not applicable to T3 (PttBreakEvenSwap.cs not touched)" with "N/A". PASS.
- SCAN-05 scopes to `src/PropTraderTools/Features/` only. Expected result: 0 matches in `PttBreakEven.cs`; only permitted match `PttGlobalBreakEven.cs` (out of scope). PASS.

**File Routing**: PASS
- Files: `src/PropTraderTools/Features/PttBreakEven.cs` and `src/PropTraderTools/Tests/CopyEngineB72Tests.cs` -- both in Wave workspace. Correct.

**VERDICT: TICKET_REVIEW_PASS**

---

## Spec Coverage Aggregate Check

| Spec Req | Covered By | Status |
|----------|-----------|--------|
| DW-B89-01 seed fix (CopyEngine.cs L205 XOR formula) | T1 | PASS |
| DW-B89-01 D7 format (PttBreakEvenSwap.cs L84) | T2 | PASS |
| DW-B89-01 D7 format (PttBreakEven.cs L357) | T3 | PASS |
| DW-B89-02 all 3 bare catch replacements | T2 | PASS |
| DW-B89-02 IsStopPriceSubmittable helper | T2 | PASS |
| DW-B89-02 with-targets guard | T2 | PASS |
| DW-B89-02 0-targets guard | T2 | PASS |
| DW-B89-01 T_OCO_SEED_03 test rename + D7 assertion | T3 | PASS |
| PttGlobalBreakEven.cs NOT in scope | T3 | PASS |

No spec requirement uncovered. No duplicate coverage.

---

## Overall: TICKET_REVIEW_PASS

All 3 tickets pass all checks. No JS violations. No NT8 violations. No missing scans. No phantom work. No missing plan items. All 23 checklist items PASS.

**Engineer may proceed.**
