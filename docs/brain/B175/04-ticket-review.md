# Ticket Review — Block B175
# Defects: DW-B175-ATR-FORMULA + DW-B175-ATR-TEXTBOX-FOCUS
# Phase: 3.5 — Ticket Review
# Reviewer: ptt-ticket-reviewer
# Source tickets: docs/brain/B175/04-tickets.md
# Source plan: docs/brain/B175/02-architecture-plan.md (REVIEW_PASS — 0 violations)
# Date: 2025-01-27

---

## Ticket B175-1 — Wilder's ATR Formula (AtrCalc.cs)

### Traceability
Maps to **DW-B175-ATR-FORMULA** (architecture plan §7 B175-1, plan §4a–4c).
- Change A (`Compute(double[])` Wilder's replacement) → plan §4a ✓
- Change B (bar-read block ternary + `ReadBarsToArrays` call) → plan §4b ✓
- Change C (`ReadBarsToArrays` new helper) → plan §4c ✓
No phantom work. All plan items present in ticket.

**Traceability: PASS**

---

### Line Number Accuracy (apply_diff SEARCH anchors)

**Change A — `:start_line:17`**
Actual `src/PropTraderTools/AtrCalc.cs` line 17:
```
        // Primary testable overload. No NT8 runtime dependency.
```
Ticket SEARCH block line 1: `        // Primary testable overload. No NT8 runtime dependency.`
Match: ✓

The SEARCH block runs through line 52 (`        }`). Actual file lines 17–52 exactly match the
ticket SEARCH block content (confirmed by reading AtrCalc.cs lines 17–52).

**Change B — `:start_line:94`**
Actual line 94: `                if (bars == null || bars.Count < period + 1)`
Ticket SEARCH block line 1: `                if (bars == null || bars.Count < period + 1)`
Match: ✓
Block continues through line 106 (`                return Compute(highs, lows, closes, period);`).
Confirmed exact match.

**Change C — `:start_line:112`**
Actual lines 112–114:
```
        }
    }
}
```
Ticket SEARCH block: identical.
Match: ✓

**Line Number Accuracy: PASS**

---

### CYC Pre-Check

| Method | CYC After | Ticket Claim | Assessment |
|---|---|---|---|
| `Compute(double[],double[],double[],int)` | 8 | 8 | Branches: base(1)+null(1)+period<1(1)+length(1)+seed loop(1)+Wilder's if(1)+smooth loop(1)+catch(1)=8. ≤8 ✓ |
| `Compute(ChartControl,int,string)` | 8 | 8 | k-loop extracted, ternary added, net delta=0. Stays 8. ≤8 ✓ |
| `ReadBarsToArrays` (NEW) | 2 | 2 | base(1)+for(1)=2. ≤8 ✓ |

**CYC Pre-Check: PASS**

---

### Wilder's Formula Correctness

- Seed loop: `for (int i = 1; i <= period; i++)` accumulates TRs → `atr /= period` ✓
- Wilder's check: `if (highs.Length >= 2 * period + 1)` ✓
- Smooth loop: `for (int i = period + 1; i <= 2 * period; i++)` with `atr = (atr * (period - 1) + tr) / period` ✓
- No `sum / period` in the smoothing path (sum variable eliminated entirely) ✓
- Fallback: returns `atr` (seed simple avg) when array < 2*period+1 — NOT 0.0 ✓

**Wilder's Formula Correctness: PASS**

---

### NT8 Overload Correctness

- Ternary: `int readLen = bars.Count >= 2 * period + 1 ? 2 * period + 1 : period + 1;` ✓
- Call: `ReadBarsToArrays(bars, readLen, out double[] highs, out double[] lows, out double[] closes);` ✓
- `ReadBarsToArrays` present as `private static void` ✓
- `barsAgo` in helper: `int barsAgo = readLen - 1 - k;` (oldest-first, index 0 = oldest) ✓

**NT8 Overload Correctness: PASS**

---

### JS Rules

- **JS-001**: `Compute(double[])` has try/catch. `Compute(ChartControl)` has try/catch. `ReadBarsToArrays` is a void helper called inside the outer try/catch — no own catch required. PASS.
- **JS-021**: Zero `lock()` statements introduced. PASS.
- **JS-023**: No new volatile fields. No fields of any kind introduced. PASS.
- **ASCII-only**: "Wilder's" uses straight apostrophe (0x27). All other text 7-bit ASCII. PASS.
- **No DateTime.Now**: Not used. PASS.
- **No hex colors**: Not used. PASS.
- **No FontFamily**: Not used. PASS.

**JS Rules: PASS**

---

### Test Coverage

New public/internal methods changed or added:
- `AtrCalc.Compute(double[],double[],double[],int)` → covered by B175-3 (T1-01 through T1-04) ✓
- `AtrCalc.ReadBarsToArrays` → private static, no direct [Fact] required. Called indirectly via NT8 overload which is NT8-runtime-dependent (no NT8 mock required per pipeline rules). ✓

Note: `AtrCalc.Compute(ChartControl,int,string)` (NT8 overload) is not tested with NT8 mocks per pipeline rules (NT8 runtime dependency). Consistent with B173/B174 precedent. Acceptable.

**Test Coverage: PASS**

---

### 7-Scan Checklist Presence

B175-1 ticket section "7-Scan Checklist — B175-1" contains 7 items:
1. JS-001 (try/catch) ✓
2. JS-021 (no lock) ✓
3. JS-023 (no volatile double) ✓
4. ASCII-only ✓
5. CYC ≤8 per method ✓
6. apply_diff used ✓
7. ptt-sync.ps1 ✓

All 7 scan dimensions present.

**7-Scan Checklist: PASS**

---

### File Routing

All `.cs` source paths reference `src/PropTraderTools/AtrCalc.cs` (Wave workspace). No Director workspace path referenced.

**File Routing: PASS**

---

### VERDICT B175-1: TICKET_REVIEW_PASS

---

---

## Ticket B175-2 — ATR TextBox Focus Fix (TradeCopierPanel.cs)

### Traceability
Maps to **DW-B175-ATR-TEXTBOX-FOCUS** (architecture plan §4d–4e, §7 B175-2).
- Change A (subscription line in `BuildAtrWidgetRows`) → plan §4e ✓
- Change B (`OnAtrBoxPreviewKeyDown` new method) → plan §4d ✓
No phantom work. All plan items present.

**Traceability: PASS**

---

### Line Number Accuracy (apply_diff SEARCH anchors)

**Change A — `:start_line:3212`**
Actual `src/PropTraderTools/TradeCopierPanel.cs` line 3212:
```
                _atrFractionBox.LostFocus += OnAtrFractionLostFocus;
```
Ticket SEARCH block: `                _atrFractionBox.LostFocus += OnAtrFractionLostFocus;`
Match: ✓

**Change B — `:start_line:3304`**
Actual lines 3304–3309:
```
                if (_hasData) // guard (2)
                    RecalcDisplay();
            }
        }
    }
}
```
Ticket SEARCH block at `:start_line:3304`: identical content.
Match: ✓

**Line Number Accuracy: PASS**

---

### CYC Pre-Check

| Method | CYC After | Ticket Claim | Assessment |
|---|---|---|---|
| `AtrWidget.BuildAtrWidgetRows` | 1 | 1 | Subscription line is straight-line, no branch. ≤8 ✓ |
| `AtrWidget.OnAtrBoxPreviewKeyDown` (NEW) | 2 | 2 | base(1)+if(1)=2. `&&` compound treated as single branch. ≤8 ✓ |

**CYC Pre-Check: PASS**

---

### TextBox Fix Correctness

- Subscription: `_atrFractionBox.PreviewKeyDown += OnAtrBoxPreviewKeyDown;` present in Change A ✓
- Handler checks `_atrFractionBox.IsKeyboardFocused` before setting `e.Handled = true` ✓
- No unconditional `e.Handled = true` (guarded by null check + `IsKeyboardFocused` check) ✓
- The `&&` compound guard (`_atrFractionBox != null && _atrFractionBox.IsKeyboardFocused`) is defensive-correct ✓

**TextBox Fix Correctness: PASS**

---

### JS Rules

- **JS-001**: `OnAtrBoxPreviewKeyDown` has no fallible return value (void handler, null check + DependencyProperty read + bool assignment — none throwable in normal execution). No try/catch needed. PASS.
- **JS-021**: Zero `lock()` statements introduced. PASS.
- **JS-023**: No new volatile fields. PASS.
- **ASCII-only**: All comment text 7-bit ASCII. PASS.
- **No DateTime.Now**: Not used. PASS.
- **No FontFamily**: Not used. PASS.

**JS Rules: PASS**

---

### Test Coverage

- `OnAtrBoxPreviewKeyDown`: WPF event handler requiring live `IsKeyboardFocused` state (WPF focus subsystem). No [Fact] test specified; verified by SIM gate DW-B175-SIM-FOCUS (plan §8 deferred backlog). Consistent with pipeline rules for UI-thread-only WPF handlers. Acceptable.
- `BuildAtrWidgetRows`: one straight-line subscription added; no new branch path. No [Fact] required for straight-line assignment. ✓

**Test Coverage: PASS**

---

### 7-Scan Checklist Presence

B175-2 ticket section "7-Scan Checklist — B175-2" contains 7 items:
1. JS-001 (try/catch) ✓
2. JS-021 (no lock) ✓
3. JS-023 (no volatile double) ✓
4. ASCII-only ✓
5. CYC ≤8 per method ✓
6. apply_diff used ✓
7. ptt-sync.ps1 ✓

All 7 scan dimensions present.

**7-Scan Checklist: PASS**

---

### File Routing

All `.cs` source paths reference `src/PropTraderTools/TradeCopierPanel.cs` (Wave workspace). No Director workspace path referenced.

**File Routing: PASS**

---

### VERDICT B175-2: TICKET_REVIEW_PASS

---

---

## Ticket B175-3 — New xUnit Tests (CopyEngineTests.cs)

### Traceability
Maps to **DW-B175-ATR-FORMULA** test coverage (architecture plan §7 B175-1 test bullets).
- T1-01 (constant TR Wilder's identity) → plan §7 B175-1 test bullet 1 ✓
- T1-02 (volatility spike vs simple avg) → plan §7 B175-1 test bullet 2 ✓
- T1-03 (fallback short history) → plan §7 B175-1 test bullet 3 ✓
  **NOTE (WARN only, not FAIL)**: Plan names this test `T_B175_T1_03_FallbackPathReturnsSimpleAvg`;
  ticket uses `T_B175_T1_03_ShortHistory_FallsBackToSimpleAvg`. Both names map to identical
  test semantics and the same spec requirement. Name divergence is within architect authority.
- T1-04 (B174 regression guard) → plan §7 B175-1 test bullet 4 ✓

Insert location: after line 10313 (end of file). Actual file last lines verified:
```
10310 |         }
10311 |     }
10312 |
10313 | }
```
The SEARCH block in B175-3 (`        }\n    }\n\n}`) matches lines 10310–10313 exactly. ✓

**Traceability: PASS**

---

### Line Number Accuracy (apply_diff SEARCH anchor)

**`:start_line:10310`**
Actual lines 10310–10313:
```
        }
    }

}
```
Ticket SEARCH block: identical.
Match: ✓

**Line Number Accuracy: PASS**

---

### CYC Pre-Check

| Method | CYC | Ticket Claim | Assessment |
|---|---|---|---|
| `T_B175_T1_01_WildersAtr_ConstantTR_EqualsConstantTR` | 2 | 2 (base+for) | ≤8 ✓ |
| `T_B175_T1_02_WildersAtr_VolatilitySpike_ExceedsSimpleAvg` | 3 | 3 (base+for+for) | ≤8 ✓ |
| `T_B175_T1_03_ShortHistory_FallsBackToSimpleAvg` | 2 | 2 (base+for) | ≤8 ✓ |
| `T_B175_T1_04_B174RegressionGuard_ShortHistory_SimpleAvg` | 1 | 1 (straight-line) | ≤8 ✓ |

**CYC Pre-Check: PASS**

---

### Test Completeness

- All 4 [Fact] methods present: T1-01, T1-02, T1-03, T1-04 ✓
- T1-04 labeled "B174 regression guard" with exact H/L/C/period inputs matching plan §7 ✓
- No calls to private/internal methods via reflection ✓ (all calls to `AtrCalc.Compute(double[],double[],double[],int)` which is `internal static`)
- No NT8 runtime mock required — pure-array tests only ✓
- T1-02 math verified: seed=1.0 (14 TRs of 1.0), then 14 Wilder's steps with TR=3.0 → result > 1.0 ✓
- T1-04 math verified: TR[1]=3, TR[2]=1, TR[3]=3, sum=7, ATR=7/3 ✓

**Test Completeness: PASS**

---

### JS Rules

- **JS-001**: Test methods have no fallible return value. xUnit captures exceptions as test failures — no try/catch required. PASS.
- **JS-021**: Zero `lock()` statements. PASS.
- **JS-023**: No new fields declared in test class. PASS.
- **ASCII-only**: `"Wilder's"` uses straight apostrophe (0x27). All identifiers and string literals ASCII. PASS.
- **No reflection**: `AtrCalc.Compute` is `internal` — accessible from test project via `[assembly: InternalsVisibleTo]`. No reflection. PASS.

**JS Rules: PASS**

---

### 7-Scan Checklist Presence

B175-3 ticket section "7-Scan Checklist — B175-3" contains 7 items:
1. JS-001 (try/catch) ✓
2. JS-021 (no lock) ✓
3. JS-023 (no volatile double) ✓
4. ASCII-only ✓
5. CYC ≤8 per method ✓
6. apply_diff used ✓
7. ptt-sync.ps1 (N/A for test file, correctly documented) ✓

All 7 scan dimensions present.

**7-Scan Checklist: PASS**

---

### File Routing

All `.cs` source paths reference `src/PropTraderTools/CopyEngineTests.cs` (Wave workspace). No Director workspace path referenced.

**File Routing: PASS**

---

### VERDICT B175-3: TICKET_REVIEW_PASS

---

---

## Overall Summary

| Ticket | Traceability | Line Numbers | CYC | Formula/Fix | JS Rules | Test Coverage | 7-Scan | File Routing | VERDICT |
|---|---|---|---|---|---|---|---|---|---|
| B175-1 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| B175-2 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |
| B175-3 | PASS | PASS | PASS | PASS | PASS | PASS | PASS | PASS | **TICKET_REVIEW_PASS** |

### Warnings (non-blocking)

- **B175-3, T1-03 name divergence**: Architecture plan §7 names the test `T_B175_T1_03_FallbackPathReturnsSimpleAvg`; ticket uses `T_B175_T1_03_ShortHistory_FallsBackToSimpleAvg`. Both name the same test. Non-blocking — architect may retain ticket name.

### Violations

None.

---

## Overall: TICKET_REVIEW_PASS
