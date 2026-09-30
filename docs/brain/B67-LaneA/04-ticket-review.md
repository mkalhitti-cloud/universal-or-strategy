# Ticket Review: B67-LaneA
**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Block**: B67-LaneA
**Tickets reviewed**: 04-tickets.md (1 ticket: B67-LaneA-T1)
**Plan reviewed**: 02-architecture-plan.md
**Date**: 2026-08-13

---

## T1 — FlattenOneAccount: insert CancelQxBrackets before market order submission

### TR-01 TRACEABILITY
**PASS**

Ticket line 13 cites: `**SPEC REQ IDs**: DW-B67-01, B67-NT8-01 (cancel-before-flatten pattern)`.
Both required spec requirement IDs are explicitly present.
All ticket items (Edit 1, Edit 2, 4 tests, deploy step) trace directly to DW-B67-01 as documented
in the architecture plan Section 1 (Problem Statement) and Section 4 (Files Changed).
No phantom work (items in ticket not in plan). No missing work (all plan items covered).

---

### TR-02 FILE SCOPE
**PASS**

Ticket Section 2 specifies exactly two files:
- `src/PropTraderTools/CopyEngine.cs` — 2 edits
- `src/PropTraderTools/CopyEngineTests.cs` — 4 new [Fact] tests

Section 10 (Scope Boundary) explicitly lists all out-of-scope items (DW-B67-02, DW-B66-C-02,
DW-B66-BE-01, DW-B63-01, DW-B54-01, DW-B58-x, PRE-EXISTING-01/02/03).
No other files referenced.

---

### TR-03 EDIT-1 CORRECTNESS
**PASS**

1. **CancelQxBrackets placement**: Ticket Section 3 new code (line 89) places
   `CancelQxBrackets(acc, instrument);` after the closing `}` of the pos guard block (line 88)
   and before `var action = pos.MarketPosition ...` (line 90). Order is: guard → cancel → ternary
   → try/catch. CORRECT.

2. **ASCII arrow**: Comment on new code line 78 uses `->` (ASCII hyphen + greater-than).
   Ticket includes an explicit CRITICAL note (after new code block) mandating ASCII `->` only
   and prohibiting Unicode arrow variants. CORRECT.

3. **CYC=4 citation**: Line 79 of new code reads:
   `// CYC=4: (1) pos null/qty guard, (2) CancelQxBrackets, (3) action ternary, (4) try/catch.`
   Enumeration matches the 4 segments. CORRECT.

4. **JS rule citations**: Line 80 reads:
   `// JS-021: no lock. JS-001: no throw in hot path. JS-002: void.`
   All three required rule IDs cited. CORRECT.

---

### TR-04 EDIT-2 CORRECTNESS
**PASS** (with informational note)

- Old line cited exactly (one line):
  `// Called by PttQuickExit.Execute() before re-placing new bracket.`
- New lines (two lines) include the FlattenOneAccount caller citation:
  `// Called by PttQuickExit.Execute() before re-placing new bracket.`
  `// Also called by FlattenOneAccount (B67 DW-B67-01) before market order submission.`

The `internal void CancelQxBrackets(...)` signature line is explicitly left unchanged (ticket
Section 4, final paragraph).

**Informational note** (not a failure): The architecture plan Section 3.3 uses slightly different
wording (`"Called by FlattenOneAccount (B67 DW-B67-01) to cancel brackets before market flatten."`)
versus ticket wording (`"Also called by FlattenOneAccount (B67 DW-B67-01) before market order submission."`).
Both convey the correct intent. The ticket text is acceptable; no blocking violation.

---

### TR-05 TEST COVERAGE
**PASS**

All 4 required [Fact] methods are present with correct names and assertion contracts:

| Test | Method Name | Contract |
|------|-------------|---------- |
| T_B67_01 | `T_B67_01_CancelQxBrackets_called_before_CreateOrder` | `callLog[0]=="CancelQxBrackets"`, `callLog[1]=="CreateOrder"` |
| T_B67_02 | `T_B67_02_FlattenOneAccount_flat_position_noOp` | `cancelCallCount==0`, `createOrderCallCount==0` |
| T_B67_03 | `T_B67_03_FlattenOneAccount_long_position_produces_Sell_Market` | `OrderAction.Sell`, `OrderType.Market`, `qty=2` |
| T_B67_04 | `T_B67_04_FlattenOneAccount_short_position_produces_BuyToCover_Market` | `OrderAction.BuyToCover`, `OrderType.Market`, `qty=1` |

All assertion contracts match the architecture plan Section 7 specifications.

**Note on NotImplementedException stubs**: The stubs in the ticket bodies use
`throw new NotImplementedException(...)` as a placeholder pattern with explicit instruction:
*"Replace the `throw new NotImplementedException` stub with the real implementation"*.
This is a scaffolding marker for the engineer, not a production throw. It does not violate
JS-001 because it will be replaced before any scan can pass (S2 would fail on `throw new`).
The engineer contract (S2 scan) enforces removal before BUILD_PASS. Not a blocking violation.

---

### TR-06 TEST FRAMEWORK
**PASS**

Ticket Section 5 header: *"All tests are xUnit only. No NUnit. No MSTest."*
All 4 test stubs use the `[Fact]` attribute.
No NUnit `[Test]` attribute. No MSTest `[TestMethod]` attribute.

---

### TR-07 7-SCAN CHECKLIST
**PASS**

All 7 scans present in Ticket Section 6 with commands and pass conditions:

| Scan | ID | Present | Command | Pass Condition |
|------|----|---------|---------|----------------|
| Lock scan | S1 | ✓ | `grep -n "lock(" CopyEngine.cs \| grep -v "//"` | 0 hits |
| Throw new scan | S2 | ✓ | `grep -n "throw new" CopyEngine.cs` | 0 hits in new code |
| CYC scan | S3 | ✓ | Manual enumeration: 4 branches confirmed | CYC=4 comment matches |
| ASCII scan | S4 | ✓ | `grep -Pn "[^\x00-\x7F]" CopyEngine.cs` | 0 new non-ASCII |
| Build scan | S5 | ✓ | `dotnet build src/` | 0 errors, 0 warnings |
| Test scan | S6 | ✓ | `dotnet test src/ --filter "T_B67"` | 4/4 pass |
| SHA-256 scan | S7 | ✓ | `Get-FileHash` both paths | Hash match |

Defense-in-depth layer 1 (engineer contract) is present. Verifier (Phase 4b) anchor is established.

---

### TR-08 DEPLOY STEP
**PASS**

Mandatory SHA-256 copy step is present in Ticket Section 7 with exact paths:

- **Source**: `C:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs`
- **Destination**: `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs`
- **Deploy command**: `powershell -File C:\WSGTA\universal-or-strategy\deploy-sync.ps1`
- **Verification**: Dual `Get-FileHash` commands with explicit "both hashes MUST match" requirement.
- **Gate enforcement**: "BUILD_PASS is only valid after SHA-256 match is confirmed."

---

### TR-09 JS-DNA
**PASS**

| Rule | Scope | Assessment |
|------|-------|------------|
| JS-021 (no lock) | FlattenOneAccount new line + updated comment | No `lock()` in any new or modified code. Both methods run on NT8 dispatcher thread. PASS. |
| JS-001 (no throw in hot path) | FlattenOneAccount catch block | Catch logs via `StatusUpdate?.Invoke`. No `throw` or rethrow. (NotImplementedException stubs are scaffolding only, removed before S2 scan passes.) PASS. |
| JS-002 (no return null) | void methods | Not applicable — both methods are `void`. PASS. |
| JS-036 (no new[] in hot path) | Single-line insert | `CancelQxBrackets(acc, instrument)` introduces zero allocations. Pre-existing `new List<Order>()` in CancelQxBrackets body is unchanged. PASS. |
| ASCII-only | All new string literals and comments | `->` used (ASCII), no Unicode arrows. Explicit CRITICAL note enforces this. S4 scan enforces at build time. PASS. |
| DateTime.Now ban | `DateTime.MaxValue` (unchanged) | No `DateTime.Now` introduced. PASS. |
| CYC <= 8 | FlattenOneAccount CYC=4 | Well within limit. PASS. |

---

### TR-10 NT8 CONSTRAINTS
**PASS**

| Constraint | Assessment |
|------------|------------|
| No `AtmStrategyCreate` | Not used. `CancelQxBrackets` uses `acc.Cancel(Order[])`. PASS. |
| No `StrategyBase`-only APIs | `acc.CreateOrder` is on `Account` object — valid for `AddOnBase`. PASS. |
| `CreateOrder` name starts "PTT-" | `"PTT-Flatten"` — starts with "PTT-". PASS. |
| No `async/await` in lifecycle | No async usage in new or modified code. PASS. |
| No `sealed` on TradeCopierWindow | Not referenced in ticket. PASS. |
| No `FontFamily` on WPF element | Not referenced in ticket. PASS. |
| No hardcoded hex color | Not referenced in ticket. PASS. |
| No `DateTime.Now` | `DateTime.MaxValue` (unchanged). PASS. |
| Type compatibility (`Instrument`) | Plan Section 2 confirms `Instrument` == `NinjaTrader.Cbi.Instrument` in NT8 scope; compatible with `CancelQxBrackets` signature. PASS. |

---

### TR-11 COMPLETION ARTIFACT
**PASS**

Ticket Section 8 specifies `docs/brain/B67-LaneA/ticket-1-completion.md` schema including:
- All 7 scan results (S1–S7) in table format with command, result, and notes columns
- SHA-256 hash section with Wave workspace and NT8 AddOn directory hashes, and Match field
- Build output section (paste `dotnet build` output)
- Test results section (paste `dotnet test` output)
- DW-B67-01 closure statement: "CLOSED — CancelQxBrackets inserted before acc.CreateOrder in FlattenOneAccount."

Artifact schema is complete and sufficient for Phase 4b verifier cross-check.

---

### TR-12 SCOPE CREEP
**PASS**

Ticket touches exactly:
1. `src/PropTraderTools/CopyEngine.cs` — 2 targeted edits (FlattenOneAccount body, CancelQxBrackets comment)
2. `src/PropTraderTools/CopyEngineTests.cs` — 4 appended tests

No other files, classes, or methods are referenced for modification.
Section 10 Scope Boundary explicitly names all out-of-scope items and instructs the engineer
to REPORT (not fix) any pre-existing issue discovered during engineering.

---

## Overall: TICKET_REVIEW_PASS

All 12 TR checks pass. No violations found across:
- Traceability (TR-01)
- File scope (TR-02)
- Edit-1 correctness (TR-03)
- Edit-2 correctness (TR-04)
- Test coverage — all 4 [Fact] methods with correct assertion contracts (TR-05)
- Test framework — xUnit only (TR-06)
- 7-scan checklist — all S1–S7 present as engineer contract (TR-07)
- Deploy step — exact source + destination + SHA-256 verification (TR-08)
- JS-DNA compliance — JS-001, JS-002, JS-021, JS-036, ASCII, DateTime (TR-09)
- NT8 constraints — CreateOrder on Account, "PTT-" prefix, no forbidden APIs (TR-10)
- Completion artifact schema — 7 scans + SHA-256 + DW closure (TR-11)
- Scope creep — 2 files only, no extraneous changes (TR-12)

**The engineer may proceed with B67-LaneA Ticket-1.**

---

*Review status: TICKET_REVIEW_PASS*
