# B215 Lane B -- Architecture Plan

**Block**: B215-LaneB  
**Epic**: DW-B212-01 Close (comment) + DW-B214-01 CYC annotation fix  
**Produced by**: ptt-architect (Phase 1)  
**Status**: PLAN_COMPLETE  
**Date**: 2026  

---

## Section A: Lane-Split Gate Result

**LANE-SPLIT GATE RESULT: SINGLE-PIPELINE**  
(Q1=NO, Q2=NO, Q3=YES, Q4=YES -- lanes approved by gate logic, but single ticket chosen because
combined work is 2 comment lines across 2 files -- complexity does not justify lane split.)

| Q | Question | Answer | Rationale |
|---|----------|--------|-----------|
| Q1 | Same method or within 50 lines? | NO | CopyEngine.cs:5818 vs TradeCopierPanel.cs:566 -- different files, different methods |
| Q2 | Fix B design depends on Fix A final design? | NO | Independent comment edits with no cross-dependency |
| Q3 | Each fix has standalone value if the other is blocked? | YES | Each comment fix is independently meaningful and verifiable |
| Q4 | Each fix has an independent SIM verification path? | YES | Each verified by reading the one-line comment in isolation |

**Chosen pipeline**: SINGLE (1 ticket: LB1 covering both comment fixes).

---

## Section B: Component List

| Component | File | Change Type | Lines Affected |
|-----------|------|-------------|----------------|
| Trim(Instrument) investigation comment | src/PropTraderTools/CopyEngine.cs | Comment add | Before line 5818 |
| TryResolveLeaderAccount CYC annotation | src/PropTraderTools/TradeCopierPanel.cs | Comment replace | Line 566 only |

**Total files touched**: 2  
**Total lines changed**: 2 comment lines  
**Logic changes**: NONE  
**New tests required**: NONE  

---

## Section C: Investigation Findings -- DW-B212-01

### Finding: CLOSED-NO-ACTION

**Question posed by DW-B212-01**: Does `TrimOneAccount` call `CancelAllAccountOrders`? If so, the same
NT8 ATM re-arm risk as pre-fix `Flatten(Instrument)` applies and a leader-skip guard is needed.

**Investigation**: Read `TrimOneAccount` body at `src/PropTraderTools/CopyEngine.cs` lines 5977-6015.

**Proof line (verbatim from source)**:
```csharp
acc.CreateOrder(
    instrument,
    action,
    OrderType.Market,
    OrderEntry.Manual,
    TimeInForce.Gtc,
    trimQty,
    0,
    0,
    null,
    "PTT-Trim",
    DateTime.MaxValue,
    null
);
```

**Conclusion**: `TrimOneAccount` calls `acc.CreateOrder(...)` only -- a targeted partial market sell
(half of the open position). It does **NOT** call `CancelAllAccountOrders`. No ATM bracket is
cancelled. No NT8 ATM re-arm trigger. No spurious copy re-entry risk. The leader-skip guard
applied to `Flatten(Instrument)` in HF-08 (B212) is **NOT needed** for `Trim`.

**Additional NT8 API compliance check**:
- Order name `"PTT-Trim"` has required `"PTT-"` prefix. PASS.
- Uses `DateTime.MaxValue` (not `DateTime.Now`). PASS (JS-compliant).
- `acc.CreateOrder()` is valid AddOnBase API. PASS (not StrategyBase-only).

**DW-B212-01 STATUS: CLOSED-NO-ACTION**

---

## Section D: Comment Specifications

### D1 -- Comment for Trim(Instrument) (CopyEngine.cs)

**Location**: Insert immediately BEFORE `internal void Trim(Instrument instrument)` at line 5818,
replacing the blank line at 5817 with the comment block then preserving the blank line structure.

**Style reference**: Matches the B212-FLATTEN-REENTRY block at lines 5829-5833 (same project style).

**Exact comment text to insert**:
```csharp
        // B215-DW-B212-01: leader-skip NOT needed for Trim path.
        // TrimOneAccount calls acc.CreateOrder (PTT-Trim partial market sell) only.
        // No CancelAllAccountOrders -> no ATM bracket cancel -> no NT8 ATM re-arm risk.
        // DW-B212-01 CLOSED-NO-ACTION. Compare: Flatten uses HF-08 leader-skip (CancelAllAccountOrders present).
```

**Placement**: The 4-line block above sits between the closing brace of the previous method (line 5816)
and the `internal void Trim(Instrument instrument)` declaration. A blank line separates the comment
from the prior method's closing brace; the comment sits directly above the method declaration.

**Resulting file structure at that location**:
```
        }
        }
        // blank line

        // B215-DW-B212-01: leader-skip NOT needed for Trim path.
        // TrimOneAccount calls acc.CreateOrder (PTT-Trim partial market sell) only.
        // No CancelAllAccountOrders -> no ATM bracket cancel -> no NT8 ATM re-arm risk.
        // DW-B212-01 CLOSED-NO-ACTION. Compare: Flatten uses HF-08 leader-skip (CancelAllAccountOrders present).
        internal void Trim(Instrument instrument)
        {
```

### D2 -- Comment replacement for TryResolveLeaderAccount (TradeCopierPanel.cs)

**Location**: Line 566 only. Lines 567-568 (JS-002 and JS-021 annotations) are UNTOUCHED.

**Old line (exact)**:
```csharp
        // CYC=6: chart null(1), combo found(2), Account cast(3), string name empty(4), Account.All loop(5), combo found wire(6).
```

**New line (exact)**:
```csharp
        // CYC=8 (strict McCabe: 8 decision points; Ph4a path-counting=6, both pass JS-066 ceiling).
```

**Lines 567-568 preserved verbatim (DO NOT TOUCH)**:
```csharp
        // JS-002: returns null (not throw) -- callers use null as a no-op sentinel.
        // JS-021: no lock. UI-thread-only (called only from WPF button Click handlers).
```

**Verification**: After edit, lines 566-568 must read exactly:
```csharp
        // CYC=8 (strict McCabe: 8 decision points; Ph4a path-counting=6, both pass JS-066 ceiling).
        // JS-002: returns null (not throw) -- callers use null as a no-op sentinel.
        // JS-021: no lock. UI-thread-only (called only from WPF button Click handlers).
```

---

## Section E: Method Signatures

No new methods. No signature changes. Existing methods for reference:

```csharp
// CopyEngine.cs -- UNCHANGED
internal void Trim(Instrument instrument)               // CYC=2, unchanged
private void TrimOneAccount(Account acc, Instrument instrument)  // CYC=3, unchanged

// TradeCopierPanel.cs -- UNCHANGED
private NinjaTrader.Cbi.Account TryResolveLeaderAccount()       // CYC=8, annotation corrected
```

---

## Section F: NinjaTrader 8 API Usage

| API | Location | Scope | Status |
|-----|----------|-------|--------|
| `Account.CreateOrder(...)` | TrimOneAccount (line 5995) | AddOnBase -- available | Pre-existing, confirmed correct |
| `Account.All` | TryResolveLeaderAccount (line 590) | AddOnBase -- available | Pre-existing, confirmed correct |

**Key NT8 facts (embedded per protocol)**:
- `AtmStrategyChangeStopTarget()` -- StrategyBase-only. NOT AddOnBase. NOT used. N/A.
- `AtmStrategyCreate()` -- StrategyBase-only. NOT AddOnBase. NOT used. N/A.
- `Account.CreateOrder()` -- AddOnBase available. Confirmed used in TrimOneAccount. CORRECT.
- `Account.Change()` -- AddOnBase available but silent no-op on ATM-owned brackets. NOT used in TrimOneAccount. N/A.

---

## Section G: Threading Model

No new threading constructs. Both edits are comment-only.

`TryResolveLeaderAccount` annotation explicitly states `UI-thread-only (called only from WPF button Click handlers)` -- preserved on line 568 (unchanged). No Dispatcher.InvokeAsync placement needed.

`TrimOneAccount` is called from the relay path within CopyEngine -- no threading invariant affected by comment addition.

---

## Section H: Data Flow

Not applicable for this block. No logic changes. For audit trail:

```
Trim(Instrument) [CopyEngine.cs:5818]
  -> TrimOneAccount(acc, instrument) [CopyEngine.cs:5980]
     -> acc.CreateOrder(..., "PTT-Trim", ...) [partial market sell ONLY]
     [NO CancelAllAccountOrders -- no ATM bracket touch -- no NT8 re-arm]
```

This flow is unchanged by B215 Lane B. The comment documents it for future maintainers.

---

## Section I: JS Rule Compliance Pre-Scan

| Rule | Check | Status |
|------|-------|--------|
| JS-021 (no lock()) | No new code | PASS |
| JS-001 (no throw in hot paths) | No new code | PASS |
| JS-002 (no return null) | No new code; TryResolveLeaderAccount returns null per documented JS-002 sentinel pattern | PASS |
| JS-033 (no async void) | No new code | PASS |
| JS-036/037 (no heap alloc in hot paths) | No new code | PASS |
| JS-066 (CYC <= 8) | Trim=CYC2, TrimOneAccount=CYC3, TryResolveLeaderAccount=CYC8 -- all pass | PASS |
| ASCII-only | All comment text ASCII only | PASS |
| DateTime.UtcNow (not .Now) | No new code; existing uses DateTime.MaxValue | PASS |
| "PTT-" prefix on order names | Pre-existing "PTT-Trim" confirmed | PASS |
| FontFamily / hex colors | No UI code | PASS |

**RULES CATALOG GATE: PASS**

---

## Section J: Ticket LB1 -- 7-Scan Checklist

**Ticket**: LB1  
**Title**: B215 Lane B -- DW-B212-01 + DW-B214-01 comment fixes  
**Files**: src/PropTraderTools/CopyEngine.cs, src/PropTraderTools/TradeCopierPanel.cs  
**Spec requirements satisfied**: DW-B212-01 (closed no-action), DW-B214-01 (annotation aligned)  

### Method signatures to implement

None -- comment-only edits. No new methods.

### xUnit tests to write

None -- comment-only edits. Zero logic changes. No test required.

### 7-Scan Checklist (SCAN-01 through SCAN-07)

| Scan | Check | Required Result |
|------|-------|-----------------|
| SCAN-01 | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/TradeCopierPanel.cs` | 0 matches in new/changed lines |
| SCAN-02 | `grep -n "throw new" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/TradeCopierPanel.cs` | 0 new throw statements |
| SCAN-03 | `grep -n "DateTime.Now" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/TradeCopierPanel.cs` | 0 matches |
| SCAN-04 | Verify comment text is ASCII-only (no Unicode, no emoji, no curly quotes) | PASS |
| SCAN-05 | Verify CYC unchanged: Trim=CYC2, TrimOneAccount=CYC3, TryResolveLeaderAccount=CYC8 | CYC unchanged for all 3 methods |
| SCAN-06 | `powershell -File .\scripts\ptt-sync.ps1` after each .cs edit | EXIT 0 |
| SCAN-07 | `powershell -File .\scripts\verify_critical_symbols.ps1` (Guardian) + `powershell -File .\deploy-sync.ps1` (DIFF GUARD) | Guardian: known false positive only; DIFF GUARD: < 10,000 chars (expected ~400-600 chars) |

### Verification Steps (SIM)

1. Open `src/PropTraderTools/CopyEngine.cs` at line 5818. Confirm 4-line comment block is present above `internal void Trim(Instrument instrument)`.
2. Confirm comment contains: "No CancelAllAccountOrders" and "DW-B212-01 CLOSED-NO-ACTION".
3. Open `src/PropTraderTools/TradeCopierPanel.cs` at line 566. Confirm CYC annotation reads `CYC=8 (strict McCabe: 8 decision points; Ph4a path-counting=6, both pass JS-066 ceiling)`.
4. Confirm lines 567-568 are UNCHANGED: JS-002 and JS-021 annotations present verbatim.
5. Confirm no method body has changed (diff shows only comment lines).

---

## Section K: Deferred Work

### Items CLOSED by B215 Lane B

| ID | Item | Closure Evidence |
|----|------|-----------------|
| DW-B212-01 | Trim(Instrument) leader-skip investigation | TrimOneAccount body read (lines 5977-6015): no CancelAllAccountOrders. acc.CreateOrder only. No ATM re-arm risk. Comment added at CopyEngine.cs:5818. CLOSED-NO-ACTION. |
| DW-B214-01 | CYC annotation mismatch on TryResolveLeaderAccount | CYC comment on line 566 updated from CYC=6 (path-counting) to CYC=8 (strict McCabe). Both counts pass JS-066. CLOSED. |

### Items OPEN -- Carry Forward to B216+

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B214-02 | 24 pre-existing test failures in pre-B214 test classes | P1 | Dedicated test-debt block | OPEN |
| DW-B214-LB-01 | B74LaneCTests.cs cap assertions require manual update (gitignored file) | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-LB-02 | B214Tests.cs B214T2Tests class (3 [Fact] tests) must be created manually | P1 | Manual (NT8 project folder) | OPEN |
| DW-B214-LB-03 | B73Tests.cs FormatGlobalBeBuffer assertions require manual update (gitignored) | P1 | Manual (NT8 project folder) | OPEN |
| DW-B209-01 | Guardian script single-hunk false positive fix | P2 | B216+ | OPEN |
| DW-B209-02 | TradeCopierPanel.cs BrushTeal gap (_instr2tBtn.Background) | P2 | B216+ | OPEN |
| DW-B209-04 | specs/002-trade-copier-spec.html B209 block updates | P1 | Before SIM R8 | OPEN |
| DW-B211-01 | Direction-aware validation for Ask/Bid | P2 | Future block | OPEN |
| DW-B211-02 | Target nudge buttons (depends DW-B211-01) | P2 | Future block (after DW-B211-01) | OPEN |
| DW-B212-02 | PttMoveTargetAll.cs future role (retain vs deprecate) | P2 | B216+ | OPEN |

### Priority Order for Next Block

1. **DW-B214-02** (P1): 24 pre-existing test failures -- dedicated remediation block before production push.
2. **DW-B214-LB-01/02/03** (P1 x3): Manual NT8 project folder test updates (gitignored files -- agent cannot write).
3. **DW-B209-04** (P1): specs HTML update before SIM R8.
4. **DW-B209-01/02** (P2): Guardian false positive + BrushTeal cosmetic.
5. **DW-B212-02** (P2): PttMoveTargetAll disposition decision.
6. **DW-B211-01/02** (P2): Direction-aware validation and nudge buttons (future feature block).

---

## Section L: Pre-Flight Summary

| Check | Result |
|-------|--------|
| Mandatory investigation reads completed | PASS (6/6 files read) |
| DW-B212-01 root question answered | PASS -- TrimOneAccount does NOT call CancelAllAccountOrders (confirmed lines 5977-6015) |
| DW-B214-01 CYC count verified | PASS -- CYC=8 strict McCabe, passes JS-066 |
| Sequential thinking: 8 thoughts completed | PASS |
| JS rule pre-scan | PASS (all P0/P1 rules clean) |
| ASCII-only comment text | PASS |
| Lane-split gate | SINGLE-PIPELINE (documented above) |
| No .cs source code written by architect | PASS (comments specified for engineer to apply) |
| Deferred backlog carry-forward complete | PASS (10 open items catalogued in Section K) |

---

**PLAN_COMPLETE**

*Produced by ptt-architect, Phase 1. Ready for ptt-plan-reviewer (Phase 2).*  
*Output for Phase 3 (ticket generation): docs/brain/B215/04-tickets-lane-b.md*
