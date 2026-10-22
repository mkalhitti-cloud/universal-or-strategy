# B212 Ticket T2 Verification Report
# Phase 4b -- PTT Verifier

**Ticket**: T2 -- B212 OCO Stop Pair per Move-Target Slot (MA-STOP-02)
**Verifier**: PTT Verifier (ptt-verifier mode)
**Source**: src/PropTraderTools/Features/PttMoveTargetOne.cs (READ-ONLY)
**Tests**: src/PropTraderTools/Tests/B212Tests.cs
**Spec refs**: docs/brain/B212/04-tickets.md (T2 section), docs/brain/NO-PIPELINE-REPAIRS.md (HOTFIX-B212-MA-STOP-02)
**Engineer report**: docs/brain/B212/ticket-2-completion.md

---

## LAYER 3 VERIFICATION PROTOCOL

All scans run independently by this verifier. Engineer's Layer 2 results are NOT trusted until
Layer 3 independently confirms them. Any discrepancy between Layer 2 and Layer 3 = VERIFY_FAIL.

---

## Section A -- OCO Pair Implementation Correctness

### A1: pairIdx loop present

**Spec**: `for (int pairIdx = 0; pairIdx < targets.Length; pairIdx++)` calling `SubmitTargetPair(..., pairIdx)`

**Source (lines 73-76)**:
```csharp
for (int pairIdx = 0; pairIdx < targets.Length; pairIdx++)
{
    SubmitTargetPair(acc, instr, targets[pairIdx], isLong, useAsk, exitPrice, snapshotStop, pairIdx);
}
```

**Verifier verdict**: PASS -- pairIdx loop present, passes pairIdx to SubmitTargetPair.

---

### A2: snapshotStop taken BEFORE acc.Cancel(targets)

**Spec**: SnapshotStopPrice call must precede the acc.Cancel(targets) call.

**Source**:
- Line 59: `double snapshotStop = PttQuickExit.SnapshotStopPrice(acc, instr);`
- Line 72: `acc.Cancel(targets);` (inside try block)

**Order confirmed**: snapshot at line 59 precedes cancel at line 72. PASS.

**Verifier verdict**: PASS

---

### A3: ocoId format

**Spec**: `"PTT-MA-" + acc.Name[..8] + "-" + NextBeOcoSeq().ToString("D7") + "-" + pairIdx`

**Source (lines 108-111)**:
```csharp
string ocoId = "PTT-MA-"
    + acc.Name.Substring(0, Math.Min(8, acc.Name.Length))
    + "-" + CopyEngine.Instance.NextBeOcoSeq().ToString("D7")
    + "-" + pairIdx;
```

Breakdown:
- Prefix: "PTT-MA-" -- CORRECT
- Account name: `Substring(0, Math.Min(8, acc.Name.Length))` -- safe truncation to 8 chars
- Sequence: `NextBeOcoSeq().ToString("D7")` -- D7 format (7 digits with leading zeros)
- Slot: `"-" + pairIdx` -- appended per-slot index

**Verifier verdict**: PASS -- exact spec match.

---

### A4: Stop order uses same ocoId as target order for same slot

**Source**: ocoId computed once at lines 108-111.
- Line 126: `ocoId,` -- arg8 of StopMarket CreateOrder
- Line 149: `ocoId,` -- arg8 of Limit CreateOrder
Same variable reference -- NOT recomputed between calls. PASS.

**Verifier verdict**: PASS

---

### A5: StopMarket parameters

**Spec**: StopMarket, Gtc, limitPrice=0.0, stopPrice=snapshotStop, ocoId=ocoId

**Source (lines 117-129)**:
- `OrderType.StopMarket` -- CORRECT
- `TimeInForce.Gtc` -- CORRECT
- `0.0,` (arg6: limitPrice) -- CORRECT
- `snapshotStop,` (arg7: stopPrice) -- CORRECT
- `ocoId,` (arg8) -- CORRECT

**Verifier verdict**: PASS

---

### A6: Limit target parameters

**Spec**: Limit, Day, limitPrice=exitPrice, stopPrice=0.0, ocoId=ocoId

**Source (lines 140-152)**:
- `OrderType.Limit` -- CORRECT
- `TimeInForce.Day` -- CORRECT
- `exitPrice,` (arg6: limitPrice) -- CORRECT
- `0.0,` (arg7: stopPrice) -- CORRECT
- `ocoId,` (arg8) -- CORRECT

**Verifier verdict**: PASS

---

### A7: No-stop path ocoId=string.Empty

**Spec**: When snapshotStop==0 -- submit target only with ocoId=string.Empty

**Source (line 180)**:
```csharp
string.Empty,
```
This is in the `else` branch (lines 167-190), entered only when `snapshotStop <= 0`.

**Verifier verdict**: PASS

---

### A8: IsWorkingTarget excludes StopMarket orders

**Spec**: OrderType.Limit filter in IsWorkingTarget prevents StopMarket orders from entering CollectWorkingTargets.

**Source (line 255)**:
```csharp
if (o.OrderType != OrderType.Limit) return false;   // (3)
```

PTT-MA-Stop-* orders are OrderType.StopMarket. They fail this guard at line 255 BEFORE name
checking. No double-cancel risk exists.

**Verifier verdict**: PASS

---

## Section A Summary

All 8 OCO pair invariants: **PASS**

---

## Section B -- NT8-049 Compliance (Argument Positions)

NT8-049: CreateOrder arg positions matter -- wrong order silently submits wrong price.

| Order type | Call location | arg6 (limitPrice) | arg7 (stopPrice) | arg8 (ocoId) | Verifier result |
|------------|--------------|-------------------|-----------------|--------------|-----------------|
| StopMarket | L117-129 | `0.0` | `snapshotStop` | `ocoId` | PASS |
| Limit (OCO path) | L140-152 | `exitPrice` | `0.0` | `ocoId` | PASS |
| Limit (no-stop path) | L171-183 | `exitPrice` | `0.0` | `string.Empty` | PASS |

All 3 CreateOrder calls: **NT8-049 COMPLIANT**

**Cross-check vs engineer Layer 2**: Engineer's SCAN-05 table (L124-125, L147-148, L178-179) --
verifier-counted lines match exactly. No discrepancy.

---

## Section C -- OCO Structural Invariant

**Critical invariant for HOTFIX-B212-MA-STOP-02:**
The same non-empty ocoId string MUST be passed to BOTH CreateOrder calls for the same target slot.
This is what enables Working-state transition (NT8 links stop to target via ocoId at order
submission time). An empty ocoId produces an Accepted-forever stop -- the STOP-01 regression.

**ocoId generation (lines 108-111)**:
```csharp
string ocoId = "PTT-MA-"
    + acc.Name.Substring(0, Math.Min(8, acc.Name.Length))
    + "-" + CopyEngine.Instance.NextBeOcoSeq().ToString("D7")
    + "-" + pairIdx;
```

**Stop order: ocoId used at line 126**:
```csharp
ocoId,                    // arg8: OCO pair
```
(inside StopMarket CreateOrder call, lines 117-129)

**Target order: ocoId used at line 149**:
```csharp
ocoId,                    // arg8: OCO pair
```
(inside Limit CreateOrder call, lines 140-152)

**Structural invariant status**: Both calls reference the same local variable `ocoId`. The variable
is declared and assigned once (lines 108-111) inside the `if (snapshotStop > 0)` block, shared
by both CreateOrder calls within that block. This is structurally identical to the proven BE
SubmitSwapPair pattern.

**Invariant**: CONFIRMED INTACT

---

## Section D -- B212Tests.cs

### D1: File existence
File at `src/PropTraderTools/Tests/B212Tests.cs` -- CONFIRMED PRESENT (Select-String returned results)

### D2: [Fact] test count
Independent scan via `Select-String -Pattern "\[Fact\]"` returned 15 [Fact] occurrences total.
10 T2-specific tests identified by test name pattern scan:

T2 tests confirmed by name scan:
1. `OcoId_NotEmpty_WhenSnapshotStopPresent` (line 122) -- T2-01
2. `OcoId_Empty_WhenSnapshotStopZero` (line 144) -- T2-02
3. `StopOrder_NotSubmitted_WhenSnapshotStopZero` (line 164) -- T2-03
4. `StopPriceArg_IsSnapshotStop_NotLimitPrice` (line 171) -- T2-04
5. `TargetPriceArg_IsExitPrice_NotStopPrice` (line 178) -- T2-05
6. `B212_STOP02_OcoId_Format_ContainsPttMaPrefix` (line 185) -- T2-06
7. `B212_STOP02_OcoId_Format_PairIdx_Appended` (line 201) -- T2-07
8. `B212_STOP02_OcoId_Format_AccountNameTruncated` (line 219) -- T2-08
9. `B212_STOP02_Execute_MethodExists_InPttMoveTargetOne` (line 233) -- T2-09
10. `OcoId_StopAndTarget_ShareSameNonEmptyString` (line 249) -- T2-10 (critical invariant)

**Count**: >= 6 T2 [Fact] tests: CONFIRMED (10 T2 tests present)

### D3: Critical invariant test present
`OcoId_StopAndTarget_ShareSameNonEmptyString` at line 249: CONFIRMED

### D4: xUnit only
All decorators are `[Fact]` -- xUnit. No `[Test]` (NUnit), no `[TestMethod]` (MSTest). PASS.

### D5: csproj registration
`Select-String -Path PropTraderTools.Tests.csproj -Pattern "B212Tests"` returned:
```
PropTraderTools.Tests.csproj:49:    <Compile Include="Tests\B212Tests.cs" />
```
REGISTERED at line 49. PASS.

**Section D summary**: PASS

---

## Section E -- CYC Check (Independent Count)

### E1: Execute() CYC

Method: lines 32-82. Decision points counted independently:

| Decision point | Code | Line | Count |
|----------------|------|------|-------|
| null guard | `if (instr == null)` | 35 | +1 |
| TryGet guard | `if (!TryGetExitPrice(...))` | 38 | +1 |
| compound if | `if (pos == null \|\| pos.Quantity == 0)` | 42 | +2 (if + \|\|) |
| length guard | `if (targets.Length == 0)` | 50 | +1 |
| ternary | `(useAsk ? "Ask" : "Bid")` | 62 | +1 |
| for loop | `for (int pairIdx = 0; ...)` | 73 | +1 |

**Sum**: 7 decision points. **CYC = 1 + 7 = 8** -- within <= 8 limit. PASS.

Note: T2 spec T2.4 target was "<= 7" but T2.7 pass criteria explicitly states "<= 7 (not > 8)",
meaning CYC=8 is accepted. Jane Street hard limit is CYC<=8. CYC=8 is compliant.

### E2: SubmitTargetPair() CYC

Method: lines 88-191. Decision points counted independently:

| Decision point | Code | Line | Count |
|----------------|------|------|-------|
| qty guard | `if (qty <= 0) return` | 96 | +1 |
| ternary | `isLong ? ... : ...` | 98 | +1 |
| ternary | `useAsk ? ... : ...` | 99 | +1 |
| stop branch | `if (snapshotStop > 0)` | 102 | +1 |
| null guard | `if (stopOrd != null)` | 130 | +1 |
| null guard | `if (newOrder != null)` OCO | 153 | +1 |
| null guard | `if (newOrder != null)` no-stop | 184 | +1 |

**Sum**: 7 decision points. **CYC = 1 + 7 = 8** -- within <= 8 limit. PASS.

### E3: No regression from T1

Engineer T1 Cycle 2 verification reported Execute=8, SubmitTargetPair=8.
My independent T2 count: Execute=8, SubmitTargetPair=8. No regression. PASS.

---

## Section F -- 7-Scan Independent Re-run (Layer 3)

### SCAN-01: lock() prohibition

**Command**: `Select-String -Path PttMoveTargetOne.cs -Pattern "lock\("`
**Layer 3 output**: (no output -- 0 results)
**Engineer Layer 2**: "0 results" (PASS)
**Layer 2 / Layer 3 match**: YES
**Result**: PASS

---

### SCAN-02: throw new prohibition

**Command**: `Select-String -Path PttMoveTargetOne.cs -Pattern "throw new"`
**Layer 3 output**:
```
PttMoveTargetOne.cs:31:  // JS-021: no lock. JS-001: no throw new. JS-033:...
PttMoveTargetOne.cs:87:  // JS-021: no lock. JS-001: no throw new. JS-033:...
```
2 hits -- both comment-only occurrences. Zero code-level `throw new` statements.
**Engineer Layer 2**: "2 comment hits, 0 code" (PASS)
**Layer 2 / Layer 3 match**: YES -- identical line numbers 31 and 87.
**Result**: PASS

---

### SCAN-03: async void prohibition

**Command**: `Select-String -Path PttMoveTargetOne.cs -Pattern "async void"`
**Layer 3 output**: (no output -- 0 results)
**Engineer Layer 2**: "0 results" (PASS)
**Layer 2 / Layer 3 match**: YES
**Result**: PASS

---

### SCAN-04: CYC audit (manual)

**Layer 3 independent count**: Execute = 8, SubmitTargetPair = 8. Both <= 8. PASS.
**Engineer Layer 2**: Execute=8, SubmitTargetPair=8 (PASS)
**Layer 2 / Layer 3 match**: YES -- identical counts.
**Result**: PASS

---

### SCAN-05: NT8-049 argument positions (manual)

**Layer 3 review**: All 3 CreateOrder calls verified from source.
- StopMarket L117-129: arg6=0.0 (limitPrice), arg7=snapshotStop (stopPrice) -- CORRECT
- Limit OCO L140-152: arg6=exitPrice (limitPrice), arg7=0.0 (stopPrice) -- CORRECT
- Limit no-stop L171-183: arg6=exitPrice (limitPrice), arg7=0.0 (stopPrice) -- CORRECT
**Engineer Layer 2**: All 3 calls correct (PASS)
**Layer 2 / Layer 3 match**: YES
**Result**: PASS

---

### SCAN-06: ptt-sync hard-link verification

**Command**: `powershell -File .\scripts\ptt-sync.ps1`
**Layer 3 output**:
```
ptt-sync complete: 21 linked, 0 errors
```
Full list: 21 files linked (CopyEngine.cs, FeatureFlags.cs, LicenseClient.cs, PttTickMath.cs,
TradeCopierAddOn.cs, TradeCopierPanel.cs, TradeCopierWindow.cs, Core\PttContracts.cs,
Features\PttBeSwapAll.cs, Features\PttBeSwapOne.cs, Features\PttBreakEvenSwap.cs,
Features\PttCancel.cs, Features\PttCopier.cs, Features\PttFlatten.cs,
Features\PttFollowerStrategy.cs, Features\PttGlobalBreakEven.cs, Features\PttGlobalQuickExit.cs,
Features\PttMoveTargetAll.cs, Features\PttMoveTargetOne.cs, Features\PttQuickExit.cs,
Features\PttTrim.cs)
**Engineer Layer 2**: "21 linked, 0 errors" (PASS)
**Layer 2 / Layer 3 match**: YES
**Result**: PASS

---

### SCAN-07: Test suite baseline

**Command**: `dotnet test PropTraderTools.Tests.csproj --no-build`
**Layer 3 output**: Failed=24, Passed=189, Skipped=540, Total=753, Duration=1s
**Engineer Layer 2**: Failed=24, Passed=189, Skipped=540, Total=753 (PASS)
**Layer 2 / Layer 3 match**: YES -- all four counters identical.
Analysis:
- Failed=24 -- baseline maintained, no new regressions
- Passed=189 -- +7 vs T1 baseline of 182 (7 new pure-logic T2 tests passing)
- Skipped=540 -- +3 vs T1 baseline of 537 (3 new NT8-skip T2 tests: T2-03, T2-04, T2-05)
- Total=753 -- +10 vs T1 baseline of 743
**Result**: PASS

---

## Section F Summary -- 7-Scan Results Table

| Scan | Command/Method | Layer 3 Result | Engineer Layer 2 | Match | Status |
|------|---------------|----------------|-----------------|-------|--------|
| SCAN-01 | `lock(` grep on PttMoveTargetOne.cs | 0 results | 0 results | YES | PASS |
| SCAN-02 | `throw new` grep on PttMoveTargetOne.cs | 2 comment hits, 0 code | 2 comment hits, 0 code | YES | PASS |
| SCAN-03 | `async void` grep on PttMoveTargetOne.cs | 0 results | 0 results | YES | PASS |
| SCAN-04 | Manual CYC count | Execute=8, SubmitTargetPair=8 | Execute=8, SubmitTargetPair=8 | YES | PASS |
| SCAN-05 | Manual NT8-049 arg review | All 3 calls correct | All 3 calls correct | YES | PASS |
| SCAN-06 | `ptt-sync.ps1` | 21 linked, 0 errors | 21 linked, 0 errors | YES | PASS |
| SCAN-07 | `dotnet test --no-build` | F=24, P=189, S=540, T=753 | F=24, P=189, S=540, T=753 | YES | PASS |

**Layer 2 / Layer 3 discrepancies**: NONE

---

## Additional DNA Rule Checks

### JS-021: No lock()
SCAN-01 confirmed. Interlocked.Increment(ref _maSeq) at line 100 and 114 -- lock-free. PASS.

### JS-001: No throw new
SCAN-02 confirmed. All NT8 calls wrapped in try/catch log-and-swallow at lines 115-135, 139-158, 169-189. PASS.

### JS-002: No null return
No null return in any method.
- Execute(): void
- SubmitTargetPair(): void
- TryGetExitPrice(): out double + bool return
- CollectWorkingTargets(): `new Order[0]` on empty, never null
- IsWorkingTarget(): bool return
PASS.

### JS-033: No async void
SCAN-03 confirmed. All methods are synchronous void. PASS.

### NT8 Hard Constraints
- No async/await: PASS
- No sealed on class: PttMoveTargetOne is `internal static` (not TradeCopierWindow), N/A. PASS.
- No FontFamily: 0 results. PASS.
- No hex color #RRGGBB: 0 results. PASS.
- CreateOrder names start with "PTT-": targetName uses "PTT-MA-Ask/Bid-" prefix (line 100 + PttOrderNames.MaAskPrefix/MaBidPrefix), stopName uses "PTT-MA-Stop-" prefix (line 114). PASS.
- DateTime.Now: 0 results. PASS.

### ASCII-only
No Unicode characters visible in source. All string literals and comments use ASCII characters only. PASS.

---

## Architecture Compliance

### T2 spec requirements per 04-tickets.md

| Requirement | Status |
|------------|--------|
| pairIdx loop in Execute | PASS (lines 73-76) |
| snapshotStop BEFORE cancel | PASS (line 59 before line 72) |
| ocoId format: "PTT-MA-" + acc.Name[..8] + "-" + seq(D7) + "-" + pairIdx | PASS (lines 108-111) |
| Stop: StopMarket, Gtc, limitPrice=0.0, stopPrice=snapshotStop, ocoId | PASS (lines 117-129) |
| Target: Limit, Day, limitPrice=exitPrice, stopPrice=0.0, ocoId | PASS (lines 140-152) |
| No-stop path: ocoId=string.Empty | PASS (line 180) |
| IsWorkingTarget excludes StopMarket | PASS (line 255) |
| Same ocoId variable for both orders in pair | PASS (lines 126 and 149) |

### HOTFIX-B212-MA-STOP-02 spec (NO-PIPELINE-REPAIRS.md lines 3653-3719)

All requirements from HOTFIX document confirmed in source:
- OCO pair pattern: APPLIED
- NT8-049 arg compliance: CONFIRMED
- PTT-MA- prefix on all orders: CONFIRMED
- JS-DNA: JS-021/JS-001/JS-002/JS-033: all PASS
- ptt-sync 21/0: CONFIRMED

---

## Spec Coverage

### T2.5 Test coverage

All 10 T2 tests required by spec present in B212Tests.cs:
- T2-01 through T2-10: CONFIRMED (names match spec exactly)
- Critical invariant T2-10 (`OcoId_StopAndTarget_ShareSameNonEmptyString`): PRESENT at line 249

---

## Violation Summary

**P0 violations found**: NONE
**P1 violations found**: NONE
**DNA rule violations**: NONE
**NT8 constraint violations**: NONE
**Layer 2 / Layer 3 discrepancies**: NONE

---

## FINAL VERDICT

**VERIFY_PASS**

All verification criteria met:
- Section A: 8/8 OCO invariants PASS
- Section B: NT8-049 PASS (all 3 CreateOrder calls correct)
- Section C: OCO structural invariant CONFIRMED (same ocoId at lines 108-111, 126, 149)
- Section D: B212Tests.cs PASS (15 [Fact] tests, 10 T2-specific, critical T2-10 present, csproj registered)
- Section E: CYC PASS (Execute=8, SubmitTargetPair=8, no regression)
- Section F: All 7 scans PASS, zero Layer 2/Layer 3 discrepancies

*Verification produced by ptt-verifier (Phase 4b). VERIFY_PASS issued.*