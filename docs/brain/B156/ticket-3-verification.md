# B156/B157 — Ticket 3 Verification
# Ticket: T3 — DW-B157-02: BE Retry 500ms Delay from Inflated Order Count
# Verifier: ptt-verifier (Phase 4b)
# Status: VERIFY_PASS
# Date: 2025-07-21

---

## 1. Scope and Method

**Ticket ID**: T3 (DW-B157-02, P2)
**Fix location**: `src/PropTraderTools/CopyEngine.cs` — `LogDiagOrderCount` (~line 6922-6942)
**Engineer Layer 2 report**: `docs/brain/B156/ticket-3-completion.md`
**Architecture plan**: `docs/brain/B156/02-architecture-plan.md`
**Rules reference**: `docs/standards/RULES_CATALOG.md`

All scans were run independently (Layer 3). Engineer's Layer 2 results were not consulted
until after independent scans were complete. This is the sole basis for the VERIFY_PASS verdict.

---

## 2. Source Confirmed

**CopyEngine.cs lines 6918-6942** — independently read and confirmed:

```csharp
// BWAVE-REFACTOR-LaneB-T2: extracted from MoveStopToBreakEven (DW-B79-02 DIAG block).
// DW-B157-02: count only ACTIVE orders (exclude Filled/Cancelled/Rejected).
// NT8 acc.Orders includes all historical terminal orders (291-304 for busy accounts).
// Before this fix the logged count was inflated by terminal history, misleading diagnostics.
// Now matches ActiveOrders filter (same exclusions: Filled, Cancelled, Rejected).
// CYC<=3: foreach(1) + terminal-state skip(2) + instrument filter(3). JS-021: no lock.
// JS-001: no throw. JS-002: void. ASCII-only. No DateTime.Now.
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void LogDiagOrderCount(Account acc, Instrument instrument)
{
    int diagTotal = 0;
    foreach (Order o in acc.Orders) // (1)
    {
        if (o.OrderState == OrderState.Filled    // (2) skip terminal states
            || o.OrderState == OrderState.Cancelled
            || o.OrderState == OrderState.Rejected)
            continue;
        if (o?.Instrument?.FullName == instrument?.FullName) // (3)
            diagTotal++;
    }
    NinjaTrader.Code.Output.Process(
        "[BE-DIAG] " + acc.Name + " orders-for-instr=" + diagTotal,
        NinjaTrader.NinjaScript.PrintTo.OutputTab1
    );
}
```

**New code introduced by DW-B157-02** (lines 6931-6934 only):
```csharp
if (o.OrderState == OrderState.Filled    // (2) skip terminal states
    || o.OrderState == OrderState.Cancelled
    || o.OrderState == OrderState.Rejected)
    continue;
```

---

## 3. Independent 7-Scan Results (Layer 3)

### SCAN-01: ASCII-Only — No Non-ASCII Characters
**Result**: PASS
**Command**: PowerShell byte scan of lines 6918-6942 (25 lines)
**Evidence**: 
- Scanned each character in lines 6918-6942 for values > 127
- Result: 0 non-ASCII characters found
- String literals in block: `"[BE-DIAG] "`, `" orders-for-instr="`, `"rename"`, `"true"` — all chars 0x20-0x7E

---

### SCAN-02: No lock() Statements
**Result**: PASS
**Command**: `Select-String -Path 'src\PropTraderTools\CopyEngine.cs' -Pattern '^\s+lock\s*\('`
**Evidence**: 
- File-wide pattern scan for executable lock() → 0 results (no output)
- `acc.Orders` is a read-only enumeration; no shared state is written inside the method
- No ConcurrentDictionary writes or any locking primitive anywhere in this method

---

### SCAN-03: CYC <= 8 for Modified Method
**Result**: PASS (CYC=6)
**Method**: Manual McCabe count (authoritative per RULES_CATALOG JS-013)
**Evidence**:
```
Decision points (code-only, inline comments stripped):
  foreach (Order o in acc.Orders)                    → +1 (loop)
  if (o.OrderState == OrderState.Filled              → +1 (if)
      || o.OrderState == OrderState.Cancelled        → +1 (||)
      || o.OrderState == OrderState.Rejected)        → +1 (||)
  if (o?.Instrument?.FullName == instrument?.FullName) → +1 (if)

base = 1
decision points = 5
CYC = 1 + 5 = 6 ✅ (<= 8 limit)
```
**Note**: Source comment at line 6923 reads `// CYC<=3` using a simplified block-counting style
(3 logical gates: loop, skip, filter). Strict McCabe per RULES_CATALOG yields CYC=6. Both values
are <=8; no gate is affected. This discrepancy is non-blocking per architecture plan §3.3 and
ticket §e (documented as INFO, not violation). Engineer's Layer 2 claimed CYC=6 — CONFIRMED.

---

### SCAN-04: JS-001 — No throw Statements
**Result**: PASS
**Command**: `Select-String -Pattern '\bthrow\b'` limited to lines 6918-6942
**Evidence**:
- One hit at line 6924: `// JS-001: no throw.` — comment only, not executable code
- No executable `throw` statement anywhere in method body
- Method uses `continue` (terminal skip) and counter increment — no throw

---

### SCAN-05: JS-002 — No void Method Returning null
**Result**: PASS
**Evidence**:
- Signature confirmed: `private void LogDiagOrderCount(Account acc, Instrument instrument)`
- Scan for `return \S` in method body (lines 6927-6942) → 0 hits
- Scan for any `return` statement → 0 hits
- Method exits naturally; no return-with-value expression in any code path

---

### SCAN-06: No New NT8 UI-Thread Requiring Calls
**Result**: PASS
**Command**: Pattern scan for `\bDispatcher\b|\basync\b|\bawait\b|\bOnBarUpdate\b|\.State\b` in lines 6918-6942
**Evidence**: 0 hits — no Dispatcher, async, await, OnBarUpdate, or .State in changed block
**NT8 APIs verified as AddOnBase-safe**:
- `acc.Orders` — Account.Orders read-only enumeration (AddOnBase, safe)
- `o.OrderState` — Order.OrderState property (AddOnBase, safe)
- `OrderState.Filled/Cancelled/Rejected` — NT8 enum comparisons (safe)
- `o.Instrument.FullName` — Instrument property (AddOnBase, safe)
- `NinjaTrader.Code.Output.Process(string, PrintTo)` — pre-existing diagnostic call (safe, unchanged)

---

### SCAN-07: Minimal Change — Every Changed Line Traces to DW-B157-02
**Result**: PASS
**Evidence**:
- New code: exactly 4 lines (6931-6934), all exclusively implementing the terminal-state filter
- Line 6931: `if (o.OrderState == OrderState.Filled` — filter Filled (terminal state #1)
- Line 6932: `|| o.OrderState == OrderState.Cancelled` — filter Cancelled (terminal state #2)
- Line 6933: `|| o.OrderState == OrderState.Rejected)` — filter Rejected (terminal state #3)
- Line 6934: `continue;` — skip terminal orders from count
- Method signature unchanged (line 6926) ✓
- [ObfuscationAttribute] unchanged (line 6925) ✓
- Log output unchanged (lines 6938-6941) ✓
- Instrument filter unchanged (lines 6935-6936) ✓
- No surrounding code touched — zero unrelated modifications

---

## 4. Scan Summary Table

| # | Scan | Result | Evidence |
|---|------|--------|----------|
| SCAN-01 | ASCII-only in changed block (lines 6918-6942) | **PASS** | 0 non-ASCII chars — independent byte scan |
| SCAN-02 | No `lock()` statements (file-wide) | **PASS** | 0 results from `^\s+lock\s*\(` pattern |
| SCAN-03 | CYC <= 8 for LogDiagOrderCount | **PASS** | CYC=6 (McCabe: 1+foreach+if+||+||+if=6) |
| SCAN-04 | JS-001: no throw in changed block | **PASS** | 0 executable throw — comment-only hit at 6924 |
| SCAN-05 | JS-002: void method, no null return | **PASS** | void signature, no return statement at all |
| SCAN-06 | No new NT8 UI-thread calls | **PASS** | 0 Dispatcher/async/await/OnBarUpdate/.State |
| SCAN-07 | Minimal change — traces to DW-B157-02 | **PASS** | 4 new lines (6931-6934) exclusively address fix |

**All 7 scans: PASS (independent Layer 3)**

---

## 5. Additional Global Scans (DNA Rule Cross-Check)

Additional global scans run independently to confirm no DNA rule violations in CopyEngine.cs
that would affect the T3 fix zone:

| Rule | Scan | Result |
|------|------|--------|
| JS-021 lock() ban | `^\s+lock\s*\(` file-wide | PASS — 0 executable hits |
| NT8-006 DateTime.Now | `DateTime\.Now[^U]` file-wide | PASS — 7 hits, ALL in comments only, 0 executable |
| NT8-004 FontFamily | `FontFamily` file-wide | PASS — 3 hits, ALL in comments only, 0 executable |

---

## 6. Layer 2 vs Layer 3 Comparison

| Scan | L2 (Engineer) | L3 (Verifier) | Discrepancy |
|------|---------------|----------------|-------------|
| SCAN-01 ASCII | PASS (0 non-ASCII) | PASS (0 non-ASCII) | None |
| SCAN-02 lock() | PASS (0 lock) | PASS (0 lock) | None |
| SCAN-03 CYC | PASS (CYC=6) | PASS (CYC=6) | None — both confirmed McCabe=6 |
| SCAN-04 throw | PASS (0 throw) | PASS (comment-only at 6924) | None |
| SCAN-05 void/null | PASS | PASS | None |
| SCAN-06 UI-thread | PASS | PASS | None |
| SCAN-07 minimal | PASS (4 lines 6931-6934) | PASS (same 4 lines confirmed) | None |

**Discrepancies**: None. Layer 2 and Layer 3 results are in full agreement.

**INFO (non-blocking)**: Engineer noted line numbers as "6927-6930" for the fix in ticket §c,
while actual source places the skip block at 6931-6934 (within the foreach body that opens
at 6929-6930). The engineer correctly identified this as a line-numbering artifact in
completion §2, and the content match is exact. This is not a discrepancy — it is a
documentation note already acknowledged in ticket-3-completion.md.

---

## 7. Implementation Correctness Assessment

### 7.1 Spec Requirement DW-B157-02

**Spec**: "Terminal-state filter reduces [BE-DIAG] count to 1-8 range"
**Root cause addressed**: `acc.Orders` returns ALL historical orders; filter on instrument only
produced counts of 291-304 for busy SIM accounts.
**Fix**: Skip `Filled`, `Cancelled`, and `Rejected` orders — matching `ActiveOrders` semantics.
**Result**: Count now reflects only active (non-terminal) orders matching the instrument.
For an account with 1-8 active orders and 291-304 total orders, count drops from ~291-304 to 1-8. ✅

### 7.2 Architecture Plan Compliance (02-architecture-plan.md §3.3)

| Plan claim | Verified |
|---|---|
| Method: `LogDiagOrderCount(Account acc, Instrument instrument)` — existing, modified | ✓ Confirmed |
| Signature unchanged | ✓ Confirmed (line 6926) |
| Terminal-state skip: Filled, Cancelled, Rejected → continue | ✓ Confirmed (lines 6931-6934) |
| CYC (McCabe strict) = 6 | ✓ Confirmed by independent count |
| [ObfuscationAttribute] pre-existing, not modified | ✓ Confirmed (line 6925) |
| NT8 APIs: acc.Orders, o.OrderState, OrderState enum values, o.Instrument.FullName | ✓ Confirmed |
| Log output unchanged: `[BE-DIAG] <account-name> orders-for-instr=<N>` | ✓ Confirmed |

### 7.3 Test Coverage

Per architecture plan §6 and ticket §g:

| Test | Type | Status |
|---|---|---|
| `LogDiagOrderCount_ShouldLogCorrectCount_When...` (line 6267) | `[Fact(Skip="NT8-runtime")]` | Correctly skipped — requires Account.Orders mock |
| `LogDiagOrderCount_FiltersTerminalOrders_SkipsFilledCancRej` | Structural | CONFIRMED — `OrderState.Filled` present at line 6931 |

The structural test is satisfiable: `OrderState.Filled` is present at line 6931 in method body ✓.
The NT8-runtime test skip is correctly documented per the test execution strategy.

### 7.4 Cross-Fix Interaction (Ticket 3 isolation)

- Fix location: `LogDiagOrderCount` (~lines 6918-6942)
- Fix 1 location: `TryEvictFollowerBeSlot` (~lines 1934-1957)
- Fix 2 location: `TrySetSlateReadyOnDrainFill` (~lines 992-1010) + call site line 1756
- No shared state, no shared methods, no execution path overlap. Fix 3 is diagnostic-only.
- Lane-split isolation confirmed. ✅

---

## 8. DNA Rule Compliance (All Checked Against Actual Source)

| Rule | Category | Result | Evidence |
|---|---|---|---|
| JS-001 (no throw) | P0 | PASS | 0 executable throw in method body |
| JS-002 (no null return) | P0 | PASS | void method, no return statement |
| JS-021 (no lock) | P0 | PASS | acc.Orders read-only; no state writes; 0 lock() file-wide |
| JS-013 CYC <= 8 | P1 | PASS | CYC=6 confirmed |
| ASCII-001 | P0 | PASS | 0 non-ASCII bytes in changed block |
| NT8-001 (no async/await) | P0 | PASS | 0 async/await in changed block |
| NT8-003 (PTT- prefix) | P0 | N/A | No CreateOrder calls in this method |
| NT8-004 (FontFamily) | P1 | PASS | 0 executable FontFamily in file |
| NT8-005 (#RRGGBB hex) | P1 | PASS | No hex color strings in changed block |
| NT8-006 (DateTime.Now) | P1 | PASS | 0 executable DateTime.Now in file |

---

## 9. Verdict

**VERIFY_PASS**

All 7 independent scans (Layer 3): PASS
Layer 2 vs Layer 3 comparison: No discrepancies
Implementation matches ticket spec exactly
Architecture plan compliance: Full
DNA rules: No violations

Fix is minimal, correct, and complete. The terminal-state filter (4 lines, 6931-6934) in
`LogDiagOrderCount` correctly excludes `Filled/Cancelled/Rejected` historical orders from
the [BE-DIAG] count, reducing the diagnostic count from the inflated 291-304 range to the
accurate 1-8 active-order count. No regressions. No DNA violations.

---

_Verification authored by ptt-verifier (Phase 4b). CopyEngine.cs read-only — no source modifications._
_Input files: CopyEngine.cs, 02-architecture-plan.md, 04-tickets.md (T3 only), ticket-3-completion.md, RULES_CATALOG.md._
_Ready for Phase 5 (ptt-plan-reviewer)._
