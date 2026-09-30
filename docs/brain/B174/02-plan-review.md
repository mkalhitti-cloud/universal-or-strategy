# B174 Plan Review
# Epic: DW-B174-ATR-INSTRUMENT-MISMATCH
# Phase 2 output: ptt-plan-reviewer
# Plan reviewed: docs/brain/B174/02-architecture-plan.md
# Spec reviewed: docs/brain/B174-orchestrator-prompt.md
# Rules catalog: docs/standards/RULES_CATALOG.md
# Source verified: src/PropTraderTools/AtrCalc.cs, src/PropTraderTools/TradeCopierPanel.cs

---

## VERDICT: REVIEW_PASS (with two non-blocking findings)

No P0 or P1 rule violations were found. Two documentation-quality findings are
recorded below. They must be corrected in Ph3 (ticket generation) but do not block
Ph3 from starting.

---

## CHECKLIST RESULTS

---

### Item 1 -- Lane-Split Gate Result Present and Valid

**Result: PASS**

Gate table present at plan top. All four questions answered with rationale:
- Q1 NO: methods span multiple files / more than 50 lines apart.
- Q2 YES: Method C depends on Method A (3-param overload) to compile.
- Q3 NO: all 4 methods form one atomic instrument-propagation chain.
- Q4 NO: correct end-to-end behavior requires all 4 methods deployed.

Gate result stated explicitly: **SINGLE-PIPELINE**. Correct per Q2=YES and Q3=NO.

No lane-split compliance failure. Single-pipeline declaration is present.

---

### Item 2 -- Bug Root Cause Correctly Identified

**Result: PASS**

**Primary root cause:** [`AtrCalc.Compute`](src/PropTraderTools/AtrCalc.cs:75) line 75:
`var bars = cc.BarsArray[0].Bars;` -- confirmed in source. Hardcoded `[0]`
returns the first-loaded instrument regardless of active tab.

**Secondary root cause:** [`TradeCopierPanel.SetInstrument`](src/PropTraderTools/TradeCopierPanel.cs:519)
confirmed in source (lines 519-524): sets `_instrument` on outer panel but
does NOT call `_widget?.SetInstrument(...)`. `AtrWidget` has no `_instrument`
field and no `SetInstrument` method -- gap confirmed.

Both root causes match source. Call chain (TradeCopierAddOn -> SetAtrChartControl ->
AtrWidget.SetChartControl; TradeCopierPanel.SetInstrument missing widget call;
OnTimerTick -> AtrCalc.Compute -> BarsArray[0]) is accurate.

---

### Item 3 -- All 4 Repair Methods Present with Exact Before/After Code

**Result: PASS**

Each method verified against source:

| Method | Location | BEFORE verified | AFTER present |
|--------|----------|-----------------|---------------|
| A: AtrCalc.Compute (ChartControl) | AtrCalc.cs:67-94 | CONFIRMED exact | YES |
| B: AtrWidget._instrument + SetInstrument (NEW) | TradeCopierPanel.cs after 3168 | N/A (new) | YES |
| C: AtrWidget.OnTimerTick line 3249 | TradeCopierPanel.cs:3249 | CONFIRMED exact | YES |
| D: TradeCopierPanel.SetInstrument | TradeCopierPanel.cs:519-524 | CONFIRMED exact | YES |

Source cross-check:
- [`AtrCalc.cs:75`](src/PropTraderTools/AtrCalc.cs:75): `var bars = cc.BarsArray[0].Bars;` -- matches plan BEFORE.
- [`TradeCopierPanel.cs:519-524`](src/PropTraderTools/TradeCopierPanel.cs:519): SetInstrument body -- matches plan BEFORE.
- [`TradeCopierPanel.cs:3249`](src/PropTraderTools/TradeCopierPanel.cs:3249): `double atr = AtrCalc.Compute(_chartControl, 14);` -- matches plan BEFORE.
- `AtrWidget._chartControl` confirmed at line 3168 (last field before insertion point). No `_instrument` field currently. Insertion point is correct.

---

### Item 4 -- CYC Table Present and All Values <= 8

**Result: PASS (compliant) -- with FINDING F-01 (CYC annotation error)**

The CYC table is present. All claimed values are <= 8 and the **actual** CYC of every
method is also <= 8. However, the CYC for Method A AFTER is documented incorrectly
in the plan. See FINDING F-01 below.

**Manual McCabe recount (project convention: no base=1, no &&/|| counts, catch counts):**

| Method | Plan claims | Reviewer recount | Status |
|--------|-------------|-----------------|--------|
| AtrCalc.Compute BEFORE | 5 | 5 (if-cc + if-BarsArray + if-bars + for-k + catch) | Match |
| AtrCalc.Compute AFTER | **6** | **8** (if-cc + if-BarsArray + for-idx + if-candidate + if-hasName + if-bars + for-k + catch) | **MISMATCH** |
| AtrWidget.SetInstrument (NEW) | 1 | 1 (straight-line) | Match |
| AtrWidget.OnTimerTick | 3 | 3 (if-cc + if-atr + if-hasData) | Match |
| TradeCopierPanel.SetInstrument | 2 | 2 (if-statusText-and-instrument) | Match |

**Actual AFTER CYC = 8.** Two branches were omitted from the plan's count:
1. `if (!hasName || candidate.Instrument.FullName == instrumentFullName)` (plan line ~129) -- not listed.
2. `catch (Exception)` -- was counted in BEFORE (correctly, as #5), but omitted from AFTER annotation.

**CYC=8 <= 8. JS-013 is NOT violated.** The method is compliant. However the plan's
documentation states CYC=6 and the engineer will write `// CYC=6` into source when
the correct annotation is `// CYC=8`. This will cause the verifier's SCAN-02 to
report a discrepancy.

See FINDING F-01 below.

---

### Item 5 -- No lock() Introduced in Any Method

**Result: PASS**

All AFTER code blocks reviewed. No `lock()`, `Monitor`, `Mutex`, or `SemaphoreSlim`
appears in any of Methods A, B, C, or D or in the test class B174AtrCalcTests.
JS-021 compliant.

---

### Item 6 -- No Unicode/Curly Quotes in Any Changed Code

**Result: PASS**

All AFTER code blocks reviewed. All string literals use straight ASCII double-quotes.
All comments use double-hyphen `--` (ASCII 0x2D 0x2D), not em-dash or en-dash.
Identifiers: `instrumentFullName`, `hasName`, `candidate`, `_instrument`, `instr` --
all ASCII. ASCII-001 compliant.

---

### Item 7 -- Rationale for Instrument-Scan Approach Documented

**Result: PASS**

Section 4 documents two alternatives with rejection rationale:
- **Alternative 1 (Tab-change reactive):** Rejected -- no public tab-change event in
  AddOnBase context; tab changes do not alter BarsArray[0]; adds event/Detach complexity.
- **Alternative 2 (Separate BarsArray index param):** Rejected -- NT8 BarsArray index
  not guaranteed stable; wider parameter surface through 3 call sites; more fragile.
- **Chosen approach (Instrument name scan):** Justified -- self-contained, stable identifier
  (`FullName`), correct fallback, minimal surface, no new event subscriptions.

Rationale is adequate for a P1 fix.

---

### Item 8 -- 3-Ticket Structure with Correct Dependencies

**Result: PASS**

| Ticket | Content | Dependency |
|--------|---------|------------|
| B174-1 | AtrCalc.Compute instrument scan | None |
| B174-2 | AtrWidget SetInstrument + OnTimerTick + outer SetInstrument wiring | B174-1 MUST complete first (explicit) |
| B174-3 | CopyEngineTests.cs B174AtrCalcTests | B174-1 MUST complete first (explicit) |

Dependency chain is explicitly stated at B174-2: "B174-2 depends on B174-1
(AtrCalc.Compute new overload must exist before TradeCopierPanel.cs is updated
to call it with the third argument)." Correct and sufficient.

---

### Item 9 -- JS Compliance Checklist Present

**Result: PASS**

Section 6 contains a complete JS compliance matrix covering:
JS-001, JS-002, JS-021, JS-023, NT8-003, ASCII-only, DateTime.Now, FontFamily,
hex colors, CYC<=8, BarsArray[0] absence, apply_diff constraint, ptt-sync.ps1.
All cells show PASS with rationale. Checklist is adequate for Ph3 ticket generation.

---

### Item 10 -- Deferred Backlog Carry-Forward Matches B173/06-deferred-backlog.md

**Result: PASS (against spec)**

Note: `docs/brain/B173/06-deferred-backlog.md` does not exist on disk. The
authoritative source for the expected carry-forward is the orchestrator prompt
(`docs/brain/B174-orchestrator-prompt.md`, Section "DEFERRED BACKLOG CARRY-FORWARD").

Plan Section 9 matches the orchestrator prompt carry-forward exactly:
- All 15 items present (DW-B174-ATR-INSTRUMENT-MISMATCH through DW-B159-K5).
- DW-B173-01 correctly marked CLOSED (Director SIM confirmed).
- DW-B174-ATR-INSTRUMENT-MISMATCH correctly marked as TARGET OF B174.
- DW-B174-SIM correctly added as NEW OPEN item.
- All other items carry status unchanged.

---

### Item 11 -- BarsArray[0] Literal Absent from AtrCalc.Compute(ChartControl,...) After Fix

**Result: PASS**

Method A AFTER does not contain `BarsArray[0]` in the method body. The scan loop
uses `cc.BarsArray[idx]` where `idx` is a loop variable, never the literal `[0]`.

The plan explicitly states: "**`BarsArray[0]` literal:** ABSENT from method body after fix."
The AFTER code confirms this. The verify step in B174-1 includes:
`grep -c "BarsArray\[0\]" src/PropTraderTools/AtrCalc.cs` must return 0. Correct.

---

### Item 12 -- Signature Change is Backward-Compatible (Optional Param with Default null)

**Result: PASS**

`string instrumentFullName = null` is an optional parameter with default value null.
The only caller of `Compute(ChartControl, int)` is `AtrWidget.OnTimerTick` (confirmed
single caller per B173 final review + source read). The default null causes the method
to fall back to `BarsArray[0]` (preserving existing behavior). After B174-2 applies,
the call will explicitly pass `_instrument?.FullName`. The optional-param default
is a compile-safety measure for the B174-1->B174-2 ordering dependency. Correct.

---

## FINDINGS

---

### FINDING F-01 -- CYC Annotation Error in Plan (Non-blocking)

**Severity:** Documentation quality (non-blocking -- no JS-013 violation)
**Location in plan:** Section 2 Method A AFTER comment; Section 3 CYC table row 1
**Rule:** JS-013 (CYC <= 8 per method). Method is COMPLIANT at actual CYC=8.

**Description:**
The plan's CYC annotation comment states:
> "CYC=6: cc null(1)+BarsArray null/empty(2)+instrument scan loop(3)+
>         candidate null guard(4)+bars null/count guard(5)+extraction loop(6)."

The correct count by project convention (count: `if`, `for`, `catch`; not `&&/||`;
no base=1) is **CYC=8**. Two decision points are omitted from the annotation:

1. `if (!hasName || candidate.Instrument.FullName == instrumentFullName)` on the line
   inside the scan loop (after the `if (candidate == null) continue;` guard).
   This is an `if` with a compound condition. By project convention, the `if` counts
   as +1. It was not listed.

2. `catch (Exception)` -- the catch block was correctly counted in the BEFORE
   annotation as item #5, but was NOT listed in the AFTER annotation.

**Correct AFTER count:**
| # | Branch | Type |
|---|--------|------|
| 1 | `if (cc == null)` | if |
| 2 | `if (cc.BarsArray == null \|\| cc.BarsArray.Count == 0)` | if |
| 3 | `for (int idx = 0; ...)` | for |
| 4 | `if (candidate == null) continue;` | if |
| 5 | `if (!hasName \|\| candidate.Instrument.FullName == instrumentFullName)` | if |
| 6 | `if (bars == null \|\| bars.Count < period + 1)` | if |
| 7 | `for (int k = 0; k <= period; k++)` | for |
| 8 | `catch (Exception)` | catch |

**Correct delta:** 5 -> 8 (+3), not 5 -> 6 (+1).
**CYC=8 is still <= 8. JS-013 is NOT violated.**

**Action required (Ph3):** The architect must correct the CYC annotation in the
plan's AFTER code block from `// CYC=6:...` to `// CYC=8:...` with the full
8-item breakdown. The CYC table in Section 3 must be updated from 6 to 8.
The delta column should show "+3", not "+1". The "Compliant" column remains YES.

The engineer will write the `// CYC=N` comment into source. If the plan says 6,
the source comment will be wrong, and the Ph4b verifier's SCAN-02 will raise a flag.
Correcting now prevents downstream VERIFY_FAIL churn.

---

### FINDING F-02 -- JS-PUSH-01 Three-Step Analysis Not Explicitly Cited (Non-blocking)

**Severity:** Documentation quality (non-blocking)
**Location in plan:** Section 7 (Threading Model); Section 2 Method B
**Rule:** JS-PUSH-01 (prefer push over pull for state propagation)
**Reference:** docs/standards/RULES_CATALOG.md Section 1 JS-PUSH-01:
> "ptt-plan-reviewer must flag any design that introduces a new shared mutable field
>  read by a downstream component without documenting why push was not possible."

**Description:**
The plan introduces `AtrWidget._instrument` as a new mutable field on AtrWidget.
This field is read by `AtrWidget.OnTimerTick` (downstream). The plan does not
explicitly perform the JS-PUSH-01 three-step analysis or cite the rule by ID.

**Assessment:**
The plan's Section 7 (Threading Model) does address the substance:
- Instrument is pushed to AtrWidget when it changes (via `SetInstrument`).
- The stored field is read at timer tick time to pass as a parameter to `AtrCalc.Compute`.
- A pure-push alternative (recomputing ATR in `SetInstrument` directly) is not feasible
  because ATR computation requires the ChartControl to be live and the timer to fire;
  there is no single call-time push that can replace periodic polling.

The design IS architecturally push-first: the instrument reference is pushed on change
(`TradeCopierPanel.SetInstrument` -> `AtrWidget.SetInstrument`). The stored field is
the pushed value, not a polling sink. The `_instrument` field is UI-thread-only with
no cross-thread read, so the JS-PUSH-01 multi-thread hazard class does not apply.

**Action required (Ph3):** Add an explicit JS-PUSH-01 three-step documentation
block to the plan (may be inserted in Section 7 or as a sub-note to Method B).
Example format:
```
// JS-PUSH-01 three-step:
// 1. Data source: TradeCopierPanel.SetInstrument -- instrument reference changes here.
// 2. Consumer: AtrWidget.OnTimerTick -- reads instrument to pass to AtrCalc.Compute.
// 3. Direct call path: YES (SetInstrument -> AtrWidget.SetInstrument pushes at change
//    time). Pure push to computation is not possible: ATR requires periodic ChartControl
//    access; instrument cannot be passed at SetInstrument time without a live cc.
//    Stored field is the pushed value; field is UI-thread-only.
```

---

## SPEC COVERAGE MATRIX

| Requirement (from orchestrator prompt) | Addressed? | Plan section |
|---------------------------------------|------------|--------------|
| Root cause: BarsArray[0] hardcoded | YES | Sec 1 |
| Root cause: SetInstrument not propagated to AtrWidget | YES | Sec 1 |
| Fix: AtrCalc.Compute instrument-scan loop | YES | Sec 2 Method A |
| Fix: AtrWidget._instrument field + SetInstrument | YES | Sec 2 Method B |
| Fix: OnTimerTick passes _instrument?.FullName | YES | Sec 2 Method C |
| Fix: TradeCopierPanel.SetInstrument wires _widget | YES | Sec 2 Method D |
| CYC delta documented | YES (see F-01 correction needed) | Sec 3 |
| Rationale vs. alternatives documented | YES | Sec 4 |
| 3-ticket structure (B174-1/2/3) | YES | Sec 5 |
| B174-2 depends on B174-1 explicit | YES | Sec 5 B174-2 |
| JS compliance checklist | YES | Sec 6 |
| Threading model documented | YES | Sec 7 |
| NT8 API surface verified vs. existing source | YES | Sec 8 |
| Deferred backlog carry-forward | YES | Sec 9 |
| Exact line references for ptt-engineer | YES | Sec 10 |
| BarsArray[0] absent after fix | YES | Sec 2 Method A |
| Signature backward-compatible (optional param) | YES | Sec 2 Method A |
| Fallback to BarsArray[0] for null/empty name | YES | Sec 2 Method A |
| Separate-windows UX boundary documented | YES | Sec 1 (UX boundary) |
| Test class B174AtrCalcTests (3 facts) | YES | Sec 5 B174-3 |
| ptt-sync.ps1 post-edit requirement | YES | Sec 6 |

All 20 spec requirements addressed.

---

## SUMMARY

**Gate: REVIEW_PASS**

No P0 violations (no lock, no throw, no null return, no magic string, no Unicode,
no DateTime.Now, no FontFamily, no hex colors, no async/await, no Account.All).

No P1 violations (CYC=8 <= 8 per actual count; no mutable struct fields; no
unfrozen brushes; no plain Dictionary for shared state; no public constructor
on singleton; no speculative NT8 API).

Two non-blocking documentation findings documented (F-01, F-02). Both must be
corrected in Ph3 ticket generation before tickets are issued.

**Action before Ph3:**
1. F-01 (REQUIRED): Correct CYC annotation in plan AFTER code block and CYC table:
   change "CYC=6" -> "CYC=8", delta "+1" -> "+3", with the full 8-item branch breakdown.
2. F-02 (REQUIRED): Add explicit JS-PUSH-01 three-step documentation to Section 7 or
   Method B notes. Cite JS-PUSH-01 by rule ID.

Proceed to Ph3 (ticket generation) after correcting F-01 and F-02 in the plan.

---

*Review authored by: ptt-plan-reviewer*
*Block: B174 -- DW-B174-ATR-INSTRUMENT-MISMATCH*
*Phase: 2 (Plan Review)*
*Verdict: REVIEW_PASS*
