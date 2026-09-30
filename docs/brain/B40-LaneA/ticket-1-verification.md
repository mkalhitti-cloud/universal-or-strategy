# B40 Ticket T1 Verification

**Date**: 2026-07-30
**Verifier**: ptt-verifier
**Engineer Report**: `docs/brain/B40-LaneA/ticket-1-completion.md`
**Wave workspace**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\`

---

## Source Cross-Check Results

### PttGlobalBreakEven.cs

| Claim | Result |
|-------|--------|
| `_ocoSeq` field exists as `private volatile int _ocoSeq = 0;` (line 23) | **CONFIRMED** |
| `BuildGlobalBeOcoId` exists as `internal static` returning `"PTT-BEG-" + seq.ToString("D5") + "-" + accIdx + "-" + pairIndex` (line 82) | **CONFIRMED** |
| `Execute(int bufferTicks)` body is exactly `Interlocked.Increment(ref _ocoSeq)` + `CopyEngine.Instance.ArmAllPendingBe(bufferTicks)` (lines 44-46) | **CONFIRMED** |
| `Execute(IEnumerable<Account>, int)` test-seam overload is UNCHANGED | **CONFIRMED** (lines 50-60) |
| `ExecuteOne`, `IncrementBuffer`, `DecrementBuffer`, `GlobalBeBuffer` UNCHANGED | **CONFIRMED** (lines 64-95) |

### CopyEngine.cs

| Claim | Result |
|-------|--------|
| Build tag updated to `"PTT-COPIER B40 \| be-all-armed-oco-fix \| 2026-07-30"` (line 41) | **CONFIRMED** |
| `_beAllOcoSeq` field exists as `private volatile int _beAllOcoSeq = 0;` (line 141, after `_pendingBeSlots`) | **CONFIRMED** |
| `IsPendingSlotsEmpty()` is `internal bool` expression body returning `_pendingBeSlots.IsEmpty` (line 1991) | **CONFIRMED** |
| `ComputeBePrice(Position, int)` is `internal static` with null-coalesce tick size fallback 0.25 (lines 1999-2009) | **CONFIRMED** — note: architecture plan said `private static` but implementation is `internal static` per T3 test-seam requirement |
| `ComputeBePrice(MarketPosition, double, int, double)` test-seam overload exists (lines 2014-2020) | **CONFIRMED** — bonus overload not in original ticket spec but fully compliant |
| `IsPriceAlreadyAtBeForAccount` is `private bool` using `acc.Get(AccountItem.BidPrice, ...)` for Long and `acc.Get(AccountItem.AskPrice, ...)` for Short (lines 2029-2045) | **CONFIRMED** — variable names `ask`/`bid` are swapped vs. logical direction (cosmetic only; API calls and comparisons are correct per spec) |
| `ArmAllPendingBe` is `internal int` with `Interlocked.Increment(ref _beAllOcoSeq)` + foreach Account.All + `armedCount` return (lines 2054-2079) | **CONFIRMED** |
| `SubmitBeStop` signature updated to `internal void SubmitBeStop(Account leaderAcc, Instrument instr, double bePrice, string ocoOverride = null)` (line 1578) | **CONFIRMED** |
| OCO ID conditional inside per-pair loop uses `ocoOverride != null ? (ocoOverride + "-" + i) : (...)` (lines 1637-1642) | **CONFIRMED** |

### Discrepancies vs Engineer Layer 2 Report

| Item | Layer 2 Claim | Layer 3 Finding |
|------|---------------|-----------------|
| `ComputeBePrice` visibility | Engineer says `internal static` | Source confirms `internal static` ✅ (plan said `private static`; engineer correctly used `internal` for T3 test access) |
| Variable names in `IsPriceAlreadyAtBeForAccount` | Not explicitly noted | Variable `ask` holds `BidPrice` result; variable `bid` holds `AskPrice` result. Names are inverted vs. what they hold, but comparisons (`ask >= bePrice` for long, `bid <= bePrice` for short) are logically correct. **Cosmetic issue only — no functional impact.** |
| `verify_links.ps1` reported `OK=12` | Engineer reported `OK=11, FIXED=1` | My independent run: `OK=12, DESYNC=0, MISSING=0, FIXED=0, SKIPPED=1`. The previous FIXED=1 (TradeCopierWindow hash repair) is now fully settled. ✅ |

---

## Independent 7-Scan Results

### SCAN-01: `lock(` usage — JS-021

**Command run**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs","src/PropTraderTools/Features/PttGlobalBreakEven.cs" -Pattern "lock\s*\("`

**Independent result**: All 10 matches are in **comments only** (e.g., `// JS-021: no lock()`). Zero actual `lock(` keyword usage.
→ **0 ACTUAL VIOLATIONS** ✅

**Vs. Engineer Layer 2**: Engineer reported 0 violations. **Layer 2 = Layer 3. MATCH.**

---

### SCAN-02: `async void` — JS-033

**Command run**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs","src/PropTraderTools/Features/PttGlobalBreakEven.cs" -Pattern "async void "`

**Independent result**: No output — zero matches.
→ **0 VIOLATIONS** ✅

**Vs. Engineer Layer 2**: Engineer reported 0 violations. **Layer 2 = Layer 3. MATCH.**

---

### SCAN-03: `return null;` in new methods — JS-002

**Command run**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs","src/PropTraderTools/Features/PttGlobalBreakEven.cs" -Pattern "return null;"`

**Independent result**: 4 hits found:
- `CopyEngine.cs:707` — `FindFollowerBracketOrder` (pre-existing, returns `Order?` nullable)
- `CopyEngine.cs:1340` — `FindRule` null guard (pre-existing)
- `CopyEngine.cs:1346` — `FindRule` not-found return (pre-existing)
- `CopyEngine.cs:1408` — `FindPosition` not-found return (pre-existing)

Zero hits in any B40 new methods (lines 1988-2079 and PttGlobalBreakEven.cs lines 20-83).
→ **0 NEW VIOLATIONS** ✅

**Vs. Engineer Layer 2**: Engineer reported 4 pre-existing hits at same line numbers (707, 1340, 1346, 1408). **Layer 2 = Layer 3. MATCH.**

---

### SCAN-04: `throw new` — JS-001

**Command run**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs","src/PropTraderTools/Features/PttGlobalBreakEven.cs" -Pattern "throw new "`

**Independent result**: No output — zero matches anywhere in either file.
→ **0 VIOLATIONS** ✅

**Vs. Engineer Layer 2**: Engineer reported 0 violations. **Layer 2 = Layer 3. MATCH.**

---

### SCAN-05: Complexity audit — CYC ≤ 8

**Command run**: `python scripts/complexity_audit.py`

**Independent result**: `complexity_audit.py` not present in Wave workspace (confirmed: `scripts/complexity_audit.py` does not exist at `c:\WSGTA\universal-or-strategy\scripts\`). This is consistent with the engineer's report.

**Manual CYC verification from source** (Layer 3 independent):

| Method | Location | CYC | Calculation | Verdict |
|--------|----------|-----|-------------|---------|
| `Execute(int)` (PttGlobalBreakEven) | PttGBE.cs:42 | **1** | 2 straight-line statements, no branches | ✅ ≤ 8 |
| `BuildGlobalBeOcoId` | PttGBE.cs:82 | **1** | Pure expression body | ✅ ≤ 8 |
| `IsPendingSlotsEmpty` | CE.cs:1991 | **1** | Expression body | ✅ ≤ 8 |
| `ComputeBePrice(Position, int)` | CE.cs:1999 | **2** | 1 base + 1 ternary (isLong direction) | ✅ ≤ 8 |
| `ComputeBePrice(MarketPosition, double, int, double)` | CE.cs:2014 | **2** | 1 base + 1 ternary (direction) | ✅ ≤ 8 |
| `IsPriceAlreadyAtBeForAccount` | CE.cs:2029 | **4** | null-guard(1), qty guard(2), isLong if(3), price comparison(4) | ✅ ≤ 8 |
| `ArmAllPendingBe` | CE.cs:2054 | **5** | foreach-Account(1), foreach-Position(2), IsFlat guard(3), IsPriceAlready branch(4), arm vs fire(5) | ✅ ≤ 8 |

All new B40 methods CYC ≤ 8. → **0 VIOLATIONS** ✅

**Vs. Engineer Layer 2**: Engineer performed same manual verification with identical CYC values. **Layer 2 = Layer 3. MATCH.**

---

### SCAN-06: `dotnet build` / `dotnet test`

**Command run**: `dotnet test "src/PropTraderTools/PropTraderTools.csproj"`

**Independent result**:
```
AtrSizingEngine.cs(20,31): error CS0234: 'Indicators' does not exist in 'NinjaTrader.NinjaScript'
AtrSizingEngine.cs(24,36): error CS0246: 'Indicator' type not found
CopyEngine.cs(688,22): warning CS8632: nullable annotation context warning
```
**Zero new errors from B40 changes.** Only pre-existing AtrSizingEngine errors (exempt per DW-B39-INFO-01) and pre-existing CS8632 warning at line 688.
→ **0 NEW VIOLATIONS** ✅

**Vs. Engineer Layer 2**: Engineer reported identical errors (same files, same error codes). **Layer 2 = Layer 3. MATCH.**

---

### SCAN-07: `verify_links.ps1` hard-link integrity

**Command run**: `powershell -File scripts/verify_links.ps1`

**Independent result**:
```
OK      : 12
DESYNC  : 0
MISSING : 0
FIXED   : 0
SKIPPED : 1
PASS -- All deployable source files match NinjaTrader.
```
→ **OK=12 DESYNC=0** ✅

**Vs. Engineer Layer 2**: Engineer reported `OK=11, DESYNC=0, FIXED=1`. My run shows OK=12, FIXED=0 — the previously repaired TradeCopierWindow.cs hash mismatch is now fully settled (no longer needs repair). **Net result: same clean state. No discrepancy in integrity.**

---

## DNA Rule Compliance Check

| Rule | Check | Result |
|------|-------|--------|
| **JS-021** — no `lock()` | SCAN-01: 0 actual lock statements | ✅ PASS |
| **JS-023** — volatile int ok, volatile double banned | `_ocoSeq` and `_beAllOcoSeq` are `volatile int` | ✅ PASS |
| **JS-001** — no `throw new` in hot paths | SCAN-04: 0 results | ✅ PASS |
| **JS-002** — no `return null` in new methods | SCAN-03: 4 pre-existing only | ✅ PASS |
| **JS-010** — private CopyEngine constructor | `CopyEngine()` remains `private` | ✅ PASS |
| **JS-033** — no `async void` | SCAN-02: 0 results | ✅ PASS |
| **NT8-003** — no `volatile double` | No `volatile double` anywhere in new fields | ✅ PASS |
| **NT8-013** — `DateTime.MaxValue` not `DateTime.Now` | No `DateTime.Now` in new code | ✅ PASS |
| **NT8-014** — signal name starts with PTT- | OCO ID prefix is `"PTT-BEG-"` (distinct from per-account `"PTT-BE-"`) | ✅ PASS |
| **NT8-021** — `Account.All` post-init only | `ArmAllPendingBe` called from UI button handler (post-Loaded) | ✅ PASS |
| **CYC ≤ 8** — Jane Street strict | Max CYC = 5 (`ArmAllPendingBe`), all ≤ 8 | ✅ PASS |
| **ASCII-only** | All string literals in new code are ASCII: `"PTT-BEG-"`, `"D5"`, no Unicode/emoji | ✅ PASS |
| **No FontFamily / hex colors** | No WPF elements or color literals in CopyEngine.cs or PttGlobalBreakEven.cs | ✅ PASS |

---

## Spec Compliance Check

| Spec Requirement | Verification | Result |
|-----------------|--------------|--------|
| **DW-B39-OCO-01 (P0)** — OCO ID collision when accounts share 4-char prefix | `BuildGlobalBeOcoId` uses `seq.ToString("D5") + "-" + accIdx + "-" + pairIndex`, guaranteeing uniqueness per call × account × pair. `SubmitBeStop(ocoOverride)` uses `ocoOverride + "-" + i` when set. | ✅ PASS |
| **OCO prefix** — must be distinct from per-account `"PTT-BE-"` | New global prefix is `"PTT-BEG-"` (Global BE). Per-account path remains `"PTT-BE-"` unchanged. | ✅ PASS |
| **DW-B39-BEHAVIOR-01 (P1)** — engine-side armed/wait | `ArmAllPendingBe` handles immediate-fire vs. `ArmPendingBe` path correctly. Returns `armedCount` for UI FSM check. `IsPendingSlotsEmpty()` allows auto-reset detection. | ✅ PASS |
| **`IsPriceAlreadyAtBeForAccount`** — must use `acc.Get(AccountItem.BidPrice/AskPrice)` per-account API (NOT MarketData feed) | Source lines 2037, 2042 confirm `acc.Get(AccountItem.BidPrice, pos.Instrument)` and `acc.Get(AccountItem.AskPrice, pos.Instrument)`. Per-account API confirmed. | ✅ PASS |
| **`ComputeBePrice`** — null-coalesce tick size fallback 0.25 | `pos.Instrument.MasterInstrument.TickSize > 0 ? ... : 0.25` (line 2002-2004). | ✅ PASS |
| **`IsPendingSlotsEmpty`** — returns `_pendingBeSlots.IsEmpty` | `internal bool IsPendingSlotsEmpty() => _pendingBeSlots.IsEmpty;` (line 1991). ConcurrentDictionary.IsEmpty is lock-free. | ✅ PASS |
| **`Execute(IEnumerable<Account>, int)` test-seam overload** — UNCHANGED | Lines 50-60 of PttGlobalBreakEven.cs confirmed identical to pre-B40. | ✅ PASS |
| **Build tag** — updated to B40 | Line 41: `"PTT-COPIER B40 \| be-all-armed-oco-fix \| 2026-07-30"` | ✅ PASS |

---

## Layer 2 vs Layer 3 Comparison Summary

| Scan | Engineer Layer 2 | Verifier Layer 3 | Match? |
|------|-----------------|------------------|--------|
| SCAN-01 `lock(` | 0 actual violations (comments only) | 0 actual violations (comments only) | ✅ MATCH |
| SCAN-02 `async void` | 0 matches | 0 matches | ✅ MATCH |
| SCAN-03 `return null;` | 4 pre-existing (lines 707, 1340, 1346, 1408) | 4 pre-existing (same lines) | ✅ MATCH |
| SCAN-04 `throw new` | 0 matches | 0 matches | ✅ MATCH |
| SCAN-05 CYC | Manual verification, all ≤ 8 | Manual verification, all ≤ 8 | ✅ MATCH |
| SCAN-06 build | 0 new errors; 2 pre-existing AtrSizingEngine | 0 new errors; same 2 pre-existing | ✅ MATCH |
| SCAN-07 verify_links | OK=11, DESYNC=0, FIXED=1 | OK=12, DESYNC=0, FIXED=0 | ✅ NET MATCH (FIXED=1 settled to OK=12) |

**No discrepancies between Layer 2 and Layer 3.** All engineer self-reports are confirmed accurate.

---

## Additional Observations (Non-Blocking)

1. **Variable naming in `IsPriceAlreadyAtBeForAccount`** (CE.cs:2037, 2042): Variable named `ask` holds the result of `acc.Get(AccountItem.BidPrice, ...)` for the long branch, and variable named `bid` holds the result of `acc.Get(AccountItem.AskPrice, ...)` for the short branch. The variable names are logically inverted, but the comparisons (`ask >= bePrice` for long, `bid <= bePrice` for short) are correct. **This is cosmetic only — no functional or spec impact.** Engineer may wish to rename in T3 for readability.

2. **`ComputeBePrice` visibility**: Architecture plan specified `private static`, but engineer correctly used `internal static` to enable direct xUnit testing via `[InternalsVisibleTo]`. This is an approved deviation (ticket T3 requires direct test access).

3. **Test-seam overload**: Engineer proactively added `ComputeBePrice(MarketPosition, double, int, double)` — not in original ticket spec but required by T3 test definitions. Correct forward-compatibility decision.

4. **`[Fact]` count after T1**: 202 (unchanged, as expected — tests are in T3). ✅

---

## Verdict

**VERIFY_PASS**

All 7 independent scans passed. All engineer Layer 2 claims confirmed by Layer 3 independent inspection. All DNA rules compliant. All spec requirements for DW-B39-OCO-01 (P0) and DW-B39-BEHAVIOR-01 engine-side (P1) are satisfied. Build produces 0 new errors. Hard-link integrity OK=12 DESYNC=0.

**T2 (UI wiring) and T3 (tests) are unblocked.**

---

*ptt-verifier | Phase 4b | B40-LaneA | T1 | 2026-07-30*
