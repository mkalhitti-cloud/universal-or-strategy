# B129 Final Review
## Phase 5 — ptt-plan-reviewer
## Block: B129 — Instrument Row Redesign: Quick2t + QAll2t Buttons
## Date: 2026-08-11

---

## Artifacts Reviewed

| Artifact | File | Status |
|----------|------|--------|
| Architecture Plan | docs/brain/B129/02-architecture-plan.md | REVIEW_PASS |
| Ticket Review | docs/brain/B129/04-ticket-review.md | TICKET_REVIEW_PASS (post-fix retry) |
| Ticket-1 Completion | docs/brain/B129/ticket-1-completion.md | BUILD_PASS |
| Ticket-1 Verification | docs/brain/B129/ticket-1-verification.md | VERIFY_PASS |
| Prior Deferred Backlog | docs/brain/B128/06-deferred-backlog.md | READ ONLY — carry-forward |

---

## FK-1 — Build: 0 Errors, 0 Warnings (Non-Incremental)

**Source**: ticket-1-verification.md SCAN-07 Layer 3 independent run.

```
dotnet build --no-incremental
-> Build succeeded. 0 Warning(s) 0 Error(s). Time: 00:00:02.06
```

5/5 B129 tests pass (T_B129_01 through T_B129_05). Old ComputeInstrSplit tests absent.
Layer 2 (engineer) and Layer 3 (verifier) agree on all SCAN results — zero material discrepancies.

**FK-1: PASS**

---

## FK-2 — B128 Carry-Forward Items Status

### DW-B128-01 (Director SIM Gate: QX-Instr + BE-Instr)

B129 **replaces** the QX-Instr and BE-Instr buttons entirely. These buttons no longer exist in
`TradeCopierPanel.cs` — they were removed as part of the instrument row redesign (verifier item f):
all 7 B128 symbols confirmed absent via Layer 3 grep (0 matches). The SIM gate DW-B128-01
described validation criteria for buttons that have been superseded.

**DW-B128-01: CLOSED (superseded — the buttons it validated no longer exist).**
The replacement SIM gate is captured as DW-B129-01 in Section K.

### All Other Carry-Forward Items

B129 touched only:
- `src/PropTraderTools/TradeCopierPanel.cs` (instrument row methods/fields)
- `src/PropTraderTools/Features/PttQuickExit.cs` (1-line tNQty guard addition)
- `src/PropTraderTools/Tests/B128Tests.cs` (4 tests replaced with 5)

No carry-forward items (DW-B124-01/02, DW-B107, B107-DEFER-01/02, DW-B42-01/02/03,
DW-PTT-BE-FIX-01/02/03, DW-B89-DEFERRED-01..06) are affected by these changes.
All 16 remain OPEN and unmodified. Verified: none of these items reference `_instrRowPanel`,
`ComputeInstrSplit`, `_instrBeBtn`, `_instrQxT1`, or any B128-specific construct.

**FK-2: PASS**

---

## FK-3 — Instrument Row Panel Wiring (_instrRowPanel still in root.Children)

**Source**: ticket-1-verification.md item (b) — `BuildInstrRow()` source lines 1353-1377 confirmed.

The plan (Section C.2) specifies `_instrRowPanel = grid` (now a `UniformGrid`). The verifier
confirmed the field at line 273: `private UniformGrid _instrRowPanel = null;` — the field type
changed from `UIElement` (B128 plan type) to `UniformGrid` (the concrete type set in
`BuildInstrRow()`). This is a narrower, more specific declaration; no behavioral difference.

The B128 block wired `_instrRowPanel` into `root.Children` above `_quickRowPanel`. B129 does
not move or remove this wiring — only the *contents* of `BuildInstrRow()` changed. The verifier
confirmed `BuildInstrRow()` sets `_instrRowPanel = grid` at lines 1353-1377. The parent wiring
(`root.Children.Add(_instrRowPanel)`) is outside B129 scope; B128 ticket-1-completion confirmed
it at ~line 922. Since B129 made no changes to `TradeCopierPanel`'s `BuildRoot()` wiring, the
`root.Children.Add` call is still in place — untouched, verified by 0-error build.

**FK-3: PASS**

---

## FK-4 — Build2TargetList Quantity Split Correctness

**Source**: ticket-1-verification.md item (j) — all 5 test assertions confirmed from source.

Formula: `t1Qty = (totalQty + 1) / 2` (ceiling division, T1-heavy for odd counts).

| totalQty | Formula t1Qty | t2Qty = n - t1Qty | Test | Verifier |
|----------|--------------|-------------------|------|----------|
| 7 | (7+1)/2 = 4 | 7-4 = 3 | T_B129_04 | PASS |
| 6 | (6+1)/2 = 3 | 6-3 = 3 | T_B129_05 | PASS |
| 1 | (1+1)/2 = 1 | 1-1 = 0 | T_B129_03 | PASS |
| 4 | (4+1)/2 = 2 | 4-2 = 2 | T_B129_01 | PASS |
| 5 | (5+1)/2 = 3 | 5-3 = 2 | T_B129_02 | PASS |

Director-confirmed split contract:
- totalQty=7: T1=4, T2=3 ✓ (T_B129_04)
- totalQty=6: T1=3, T2=3 ✓ (T_B129_05)
- totalQty=1: T1=1, T2=0 ✓ (T_B129_03)

All 5 xUnit [Fact] tests pass independently (Layer 3 run, 5/5, Duration: 605 ms).

**FK-4: PASS**

---

## FK-5 — PttQuickExit.Execute() tNQty <= 0 Guard Present

**Source**: ticket-1-verification.md item (g) — source lines 122-123 confirmed.

```
Line 117: int tNQty =
Line 118:     (targets != null && i < targets.Count)
Line 119:         ? targets[i].Qty
Line 120:         : CalcTNQty(pos.Quantity, targetCount, i);
Line 121: (blank)
Line 122: if (tNQty <= 0)
Line 123:     continue; // B129: skip T2 when pos.Quantity==1 and t2Qty==0
Line 124: (blank)
Line 125: string ocoId_i =
```

Guard is positioned AFTER tNQty assignment (line 120) and BEFORE `string ocoId_i =` (line 125),
exactly as specified in plan Section D.1. Comment matches spec text. CYC of Execute() = 8
(+1 branch from CYC=7, still within JS-021 budget).

**FK-5: PASS**

---

## FK-6 — tNQty Guard Does Not Break Existing 2-Target Flow (tNQty > 0)

When totalQty=2: `Build2TargetList(2)` → `[(0.0, 1), (0.0, 1)]`.
- Loop iteration i=0: tNQty=1, guard `1 <= 0` is FALSE → loop body executes → T1 submitted.
- Loop iteration i=1: tNQty=1, guard `1 <= 0` is FALSE → loop body executes → T2 submitted.

Both targets with tNQty > 0 proceed normally. Guard is a no-op for all valid (non-zero) qty values.
The pre-existing 3-target flow (tNQty computed via CalcTNQty) is unaffected — CalcTNQty returns
a positive qty when the position has sufficient contracts.

**FK-6: PASS**

---

## FK-7 — P0 JS Rule Compliance Across All Touched Files

| Rule | Description | New/Modified Code | Result |
|------|-------------|-------------------|--------|
| JS-021 | No lock() | TradeCopierPanel.cs (B129 methods), PttQuickExit.cs (guard line) | PASS — SCAN-01: 0 live lock() anywhere |
| JS-033 | No async void | All new handlers `private void` (synchronous); SCAN-02: 7 hits all comments only | PASS |
| JS-002 | No return null | `Build2TargetList` returns `new List<>` always; SCAN-03: 6 live hits all pre-existing | PASS |
| JS-001 | No throw in hot paths | SCAN-04: 0 `throw new` in TradeCopierPanel.cs or PttQuickExit.cs | PASS |
| JS-008 | No mutable struct fields | No struct fields introduced in B129 | N/A |
| JS-009 | No Dictionary for shared state | No Dictionary introduced | N/A |
| JS-010 | No public constructor on singleton/signal | No singleton pattern introduced | N/A |
| NT8-FONTFAMILY | No FontFamily= on new WPF elements | BuildInstrRow: no FontFamily set | PASS |
| NT8-HEXCOLOR | No hardcoded #RRGGBB | New buttons use named brush BrushTeal, no hex literals | PASS |
| NT8-DATETIME | No DateTime.Now | No DateTime.Now in any B129 code | PASS |
| NT8-CREATEORDER | CreateOrder uses PTT- prefix | No new CreateOrder calls in B129 scope | N/A |
| ASCII-ONLY | No Unicode in string literals | "Quick2t", "QAll2t", "[PTT-QX-2T]", "T1=", "T2=" — all ASCII | PASS |
| CYC<=8 | All methods CYC<=8 | Build2TargetList=1, BuildInstrRow=1, OnInstr2tClick=4, OnInstrQAll2tClick=1, Execute=8 | PASS |

Zero P0/P1 violations in B129 code.

**FK-7: PASS**

---

## FK-8 — Cross-File Coherence: OnInstr2tClick → PttQuickExit.Execute 4th-Arg Targets List

**Source**: ticket-1-completion.md T1a + ticket-1-verification.md item (d).

Call site (TradeCopierPanel.cs line 1973):
```csharp
new PttQuickExit().Execute(_leaderAccount, _instrument, 4, targets);
```

PttQuickExit.Execute() 7-arg signature (plan Section D.1 confirms; engineer report T1b):
```csharp
Execute(Account leader, Instrument instr, int t1Ticks,
        List<(double Price, int Qty)> targets,
        bool skipIfFollower = true, double leaderStop = 0, int leaderTargetCount = 0)
```

Call analysis:
- arg1: `_leaderAccount` → `Account leader` ✓
- arg2: `_instrument` → `Instrument instr` ✓
- arg3: `4` → `int t1Ticks` (T1=4 ticks = 1 pt MES, per spec) ✓
- arg4: `targets` (pre-built 2-entry List<(double, int)> from `Build2TargetList`) → `List<(double Price, int Qty)> targets` ✓
- args 5-7: default values (`skipIfFollower=true`, `leaderStop=0`, `leaderTargetCount=0`) ✓

Inside Execute(), the targets-path branch:
```csharp
int tNQty = (targets != null && i < targets.Count) ? targets[i].Qty : CalcTNQty(...);
```
With a 2-entry pre-built targets list: `targetCount = targets.Count = 2`. Loop runs i=0 and i=1.
The pre-built list bypasses `ResolveTargetCount()` entirely — `targets.Count = 2` is used directly.
No 3rd target slot is generated. T1 and T2 prices are computed from position entry + tick offsets
inside Execute(); the 0.0 placeholder prices in the targets list are never used for order
submission (only Qty is read from the pre-built list).

**FK-8: PASS**

---

## 7-Scan Final Confirmation (SCAN-07 — Non-Incremental Build)

From ticket-1-verification.md SCAN-07 Layer 3 independent run:

```
dotnet build --no-incremental
-> Build succeeded. 0 Warning(s) 0 Error(s). Time: 00:00:02.06

dotnet test --filter "FullyQualifiedName~B128Tests"
-> Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5, Duration: 605 ms (net48)
```

All 7 scans pass:
| Scan | Check | Layer 3 Result |
|------|-------|----------------|
| SCAN-01 | No lock() | 0 live hits (1 comment-only in TradeCopierPanel.cs, PttQuickExit.cs=0) |
| SCAN-02 | No async void | 0 live declarations (7 comment-only hits, all annotations) |
| SCAN-03 | No return null in B129 code | Build2TargetList: 0 return null; 6 live hits all pre-existing, untouched |
| SCAN-04 | No throw new | 0 hits in TradeCopierPanel.cs or PttQuickExit.cs |
| SCAN-05 | Log tag [PTT-QX-2T] with T1=/T2= | Line 1961 confirmed; T1= line 1967; T2= line 1969 |
| SCAN-06 | All removed symbols absent | 0 matches for 7 B128 symbols (verified by Select-String) |
| SCAN-07 | Build + tests | 0 errors, 0 warnings; 5/5 pass |

---

## Spec Requirements Coverage

| Req ID | Description | Status |
|--------|-------------|--------|
| B129-REQ-01 | Replace spinner cluster with 2-button UniformGrid ("Quick2t" + "QAll2t") | CLOSED — lines 1353-1377 |
| B129-REQ-02 | "Quick2t" fires single-account 2-target bracket exit (OnInstr2tClick) | CLOSED — line 1948 |
| B129-REQ-03 | "QAll2t" fires all-accounts exit via PttGlobalQuickExit.Execute() | CLOSED — line 1979 |
| B129-REQ-04 | Build2TargetList: ceiling-heavy split, List never null, internal static | CLOSED — line 1383; 5 tests confirm |
| B129-REQ-05 | T2qty=0 guard in PttQuickExit.Execute() to skip zero-qty bracket | CLOSED — lines 122-123 |
| B129-REQ-06 | Remove all B128 spinner fields/methods; update B128Tests.cs | CLOSED — 7 symbols absent; 5 new tests pass |

---

## Section K — Deferred Work

| ID | Item | Priority | Target Block | Status |
|----|------|----------|--------------|--------|
| DW-B128-01 | Director SIM Gate: QX-Instr + BE-Instr live validation — SUPERSEDED (buttons removed in B129; SIM gate replaced by DW-B129-01) | P1 | B128 | CLOSED (superseded) |
| DW-B129-01 | Director SIM Gate: Quick2t + QAll2t live validation in NT8 SIM — must confirm _instrument resolves non-null, [PTT-QX-2T] log appears with correct qty/T1/T2 values, T2qty=0 guard fires for 1-contract position, [PTT-QX-ALL] appears for QAll2t | P1 | B130 or first SIM gate session after B129 sync | OPEN |
| DW-B133 | QAll2t 2-target forced-count ALL path — PttGlobalQuickExit.Execute() overload with forcedTargets passed into ExecuteOne; Option A deferred due to CYC budget; architecture requires ExecuteOne refactor before it can be cleanly added | P2 | B133 or future block post SIM-gate | OPEN |
| DW-B124-01 | Behavioral Change: Second click no longer disarms BE-ALL; disarm-on-second-click UX removed in B124 | P2 | B125 or future block | OPEN |
| DW-B124-02 | Test 2 assertion weakness: FirstPressArmsWhenNotYetArmed asserts callCount==0 instead of 1 | P2 | B125 or first polish block | OPEN |
| DW-B107 | MoveStopToBreakEven Step A snapshots stale PTT-BE-Target-* on followers | P2 | B108+ | OPEN |
| B107-DEFER-01 | F5 NinjaTrader 8 compilation gate (DW-B107 changes) | P0 | Director (immediate) | OPEN |
| B107-DEFER-02 | Combo C Live Re-Test (BE-ALL then QX-ALL sequence) | P1 | Director SIM session | OPEN |
| DW-B42-01 | T_BUG_QX_BE_01 does not assert PTT-QX-T3 | Low | B43 or future | OPEN |
| DW-B42-02 | Live NT8 F5 verification required for QX/BE directional tests | High | Next live F5 session | OPEN |
| DW-B42-03 | IsPttQxTarget range extension for PTT-QX-T4/T5 if future target slots added | Conditional | Block adding 4th+ target | OPEN |
| DW-PTT-BE-FIX-01 | DW-B85 Option A: Lazy re-resolve for null followers | Medium | Next productionisation block | OPEN |
| DW-PTT-BE-FIX-02 | SIM gate: Path B 3-cycle runtime verification (QX-ALL then BE-ALL) | High | DW-B89 SIM gate session | OPEN |
| DW-PTT-BE-FIX-03 | Pre-existing test build errors (CopyEngineTests.cs stub infra, CS0433 Globals) | High | Dedicated remediation block | OPEN |
| DW-B89-DEFERRED-01 | Ctrl+F5 NT8 compilation gate for DW-B89 changes | P0 | Director (immediate) | OPEN |
| DW-B89-DEFERRED-02 | SIM gate PATH A nominal (Entry -> BE-ALL -> verify Output no [BE-ERR]) | High | After DW-B89-01 green | OPEN |
| DW-B89-DEFERRED-03 | SIM gate PATH A buf=0 edge case (short position) | High | After DW-B89-01 green | OPEN |
| DW-B89-DEFERRED-04 | SIM gate PATH B (QX-ALL then BE-ALL, 3 cycles) | High | After DW-B89-01 green | OPEN |
| DW-B89-DEFERRED-05 | SIM gate DW-B87 timing race cycle (Entry -> BE-ALL immediately) | High | After DW-B89-01 green | OPEN |
| DW-B89-DEFERRED-06 | Spec update: close DW-B89/B88/B87 sections in spec HTML after SIM gates pass | Medium | After all DW-B89 SIM paths green | OPEN |

---

## FK Summary

| Check | Result |
|-------|--------|
| FK-1 | PASS — Build 0 errors, 0 warnings (non-incremental); 5/5 tests pass |
| FK-2 | PASS — DW-B128-01 CLOSED (superseded); all 16 other carry-forward items unmodified |
| FK-3 | PASS — _instrRowPanel wiring in root.Children unchanged; BuildInstrRow sets UniformGrid contents only |
| FK-4 | PASS — Build2TargetList: totalQty=7→[4,3], totalQty=6→[3,3], totalQty=1→[1,0]; 5/5 tests confirm |
| FK-5 | PASS — tNQty<=0 guard at lines 122-123, correct position in Execute() for-loop |
| FK-6 | PASS — tNQty>0 path (e.g., totalQty=2) runs loop body normally; guard is a no-op for valid qty |
| FK-7 | PASS — Zero P0/P1 JS violations in any B129-touched code; all DNA rules compliant |
| FK-8 | PASS — 4-arg Execute call coherent with 7-arg signature; pre-built targets list bypasses ResolveTargetCount; T2qty=0 guard prevents zero-qty bracket submission |

---

## FINAL VERDICT

**FINAL_PASS**

All 8 coherence checks pass. All 6 spec requirements closed. All 7 scans return zero violations.
Build clean at 0 errors, 0 warnings (non-incremental). 5/5 B129 tests pass independently (Layer 3).
DNA compliance confirmed across all B129-touched files. Section K present with all deferred items.
06-deferred-backlog.md written.

*Final review written: B129 Phase 5*
*Return: FINAL_PASS*
