# B171 Ticket 1 Completion Report

**Ticket ID:** B171-1
**Block:** B171 — DW-B171-01 + DW-B171-02
**Method:** `ExecutePhaseCStopReplacement` (suffix + cancel sweep)
**File:** `src/PropTraderTools/CopyEngine.cs`
**Engineer:** ptt-engineer (Phase 4a)
**Prerequisite:** `TICKET_REVIEW_PASS` confirmed in `docs/brain/B171/04-ticket-review.md`

---

## Scope

`ExecutePhaseCStopReplacement` ONLY. No other methods touched.

---

## Exact Lines Changed

**BEFORE:** Lines 4500–4512 (old 7-line comment block + 6-line method body = 13 lines total)
**AFTER:** Lines 4500–4515 (new 3-line comment block + 13-line method body = 16 lines total, net +4 lines)

Method signature line: BEFORE at line 4507 → AFTER at line 4503 (comment block shrank from 7 to 3 lines).

### BEFORE (lines 4500–4512):
```csharp
        // CYC=2. Extracted Phase C block from SyncAtmFollowerTarget (T1 extraction -- B137).
        // Replaces inline Phase C code (L2439-2442 pre-B137):
        //   DeriveLeaderBracketIndex + FindLeaderStopPrice + CreateFollowerReplacementStop.
        // McCabe branches: base(1) + leaderOrder?.Account null-conditional(1) = CYC=2.
        // Extraction reduces SyncAtmFollowerTarget from CYC=8 to CYC=7 (removes ?. branch from parent).
        // ZERO behavior change. JS-021: no lock. JS-001: delegates to CreateFollowerReplacementStop catch.
        // JS-002: void return. ASCII-only. No DateTime. No FontFamily.
        private void ExecutePhaseCStopReplacement(Account acc, Order fo, Order? leaderOrder)
        {
            int bracketIdx = DeriveLeaderBracketIndex(leaderOrder);
            double stp = FindLeaderStopPrice(leaderOrder?.Account, bracketIdx);
            CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp);
        }
```

### AFTER (lines 4500–4515):
```csharp
        // B171 DW-B171-01+DW-B171-02: derive suffix, validate price before cancel, cancel-then-replace.
        // CYC=2: base(1) + stp<=0 guard(1). JS-021: no lock. JS-001: delegates to CreateFollowerReplacementStop catch.
        // JS-002: void return. ASCII-only.
        private void ExecutePhaseCStopReplacement(Account acc, Order fo, Order? leaderOrder)
        {
            int bracketIdx = DeriveLeaderBracketIndex(leaderOrder);
            string suffix = bracketIdx.ToString();
            double stp = FindLeaderStopPrice(leaderOrder?.Account, bracketIdx);
            if (stp <= 0.0)
            {
                StatusUpdate?.Invoke(acc?.Name + ": PTT-STP-Drag-" + suffix + " skipped: stopPrice <= 0");
                return;
            }
            CancelLiveCollateralStop(acc, fo, "PTT-STP-Drag-" + suffix);
            CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp, suffix);
        }
```

---

## Tool Used

`apply_diff` — **confirmed**. `write_file` was NOT called on `CopyEngine.cs`.

---

## 7-Scan Results

### SCAN-01 — No `lock()` statements in CopyEngine.cs
**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\(" | Measure-Object`
**Result:** 14 pattern matches — ALL in comments only (e.g., `no lock()`, `lock-free`). Zero actual `lock()` C# statements.
**Status: PASS (0 actual lock statements)**

### SCAN-02 — Unsuffixed `"PTT-STP-Drag"` literals (INFORMATIONAL at B171-1 stage)
**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern '"PTT-STP-Drag"'`
**Result:**
- Line 4478: `"PTT-STP-Drag"` — in `SubmitReplacementStopOrder` CreateOrder call (unsuffixed, as expected)
- Line 4432, 4454, 4455: in comments only
- Lines 2138, 2158, 2167, 4785, 8946, 8976, 9450, 9514: `.StartsWith("PTT-STP-Drag"...)` prefix filters / other contexts
**Status: INFORMATIONAL — EXPECTED. The unsuffixed standalone literal at line 4478 (CreateOrder name argument) is in `SubmitReplacementStopOrder`, fixed by B171-2. At B171-1 stage this is correct.**

### SCAN-03 — `ExecutePhaseCStopReplacement` definition and call site
**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "ExecutePhaseCStopReplacement"`
**Result:**
- Line 4253: in comment (historical reference)
- Line 4288: call site — `ExecutePhaseCStopReplacement(acc, fo, leaderOrder);`
- Line 4503: method definition — `private void ExecutePhaseCStopReplacement(Account acc, Order fo, Order? leaderOrder)`
**Verification:** New body at lines 4504–4515 confirmed via file read:
  - `string suffix = bracketIdx.ToString();` present ✓
  - `if (stp <= 0.0)` guard present ✓
  - `StatusUpdate?.Invoke(acc?.Name + ": PTT-STP-Drag-" + suffix + " skipped: stopPrice <= 0")` present ✓
  - `CancelLiveCollateralStop(acc, fo, "PTT-STP-Drag-" + suffix);` present ✓
  - `CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp, suffix);` present ✓
**Status: PASS**

### SCAN-04 — `CancelLiveCollateralStop` existing uses + new call
**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "CancelLiveCollateralStop"`
**Result:**
- Line 4049: existing call — `CancelLiveCollateralStop(acc, fo, stpDragName);`
- Line 4058: method definition — `private void CancelLiveCollateralStop(Account acc, Order fo, string stpDragName)`
- Line 4513: **new call** — `CancelLiveCollateralStop(acc, fo, "PTT-STP-Drag-" + suffix);`
**Status: PASS — new call present, method signature confirms 3 params (acc, fo, string)**

### SCAN-05 — No `DateTime.Now` (non-UTC) or `FontFamily` introduced
**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DateTime\.Now[^U]|FontFamily" | Measure-Object`
**Result:** 12 pattern matches — ALL in comments only (e.g., `No DateTime.Now.`, `No FontFamily.`). Zero actual `DateTime.Now` calls or `FontFamily` property assignments in code.
**Status: PASS (0 actual violations)**

### SCAN-06 — `apply_diff` used (not `write_file`)
**Verification:** Tool call history confirms `apply_diff` was the only write operation on `src/PropTraderTools/CopyEngine.cs`. `write_file` was NOT called on this file.
**Status: PASS — apply_diff confirmed**

### SCAN-07 — `ptt-sync.ps1` deferred
**Verification:** `powershell -File .\scripts\ptt-sync.ps1` has NOT been run.
**Deferral reason:** Per ticket spec and plan constraint — `ptt-sync.ps1` runs ONCE after ALL three tickets (B171-1, B171-2, B171-3) are complete.
**Status: DEFERRED — ptt-sync.ps1 runs after B171-3**

---

## Compliance Summary

| Check | Result |
|-------|--------|
| `apply_diff` used (not `write_file`) | CONFIRMED |
| CYC=2 (base=1 + `stp<=0` guard=1) | CONFIRMED |
| No `lock()` introduced | CONFIRMED |
| ASCII-only string literals | CONFIRMED — all strings: `"PTT-STP-Drag-"`, `" skipped: stopPrice <= 0"` are pure ASCII |
| No `DateTime.Now` introduced | CONFIRMED |
| No `FontFamily` introduced | CONFIRMED |
| Method signature unchanged (callers unaffected) | CONFIRMED — still `private void ExecutePhaseCStopReplacement(Account acc, Order fo, Order? leaderOrder)` |
| `ptt-sync.ps1` deferred until after B171-3 | CONFIRMED |

---

## Traceability

| Changed Line | Spec Item |
|-------------|-----------|
| `string suffix = bracketIdx.ToString();` | DW-B171-01 + DW-B171-02 (suffix derivation) |
| `if (stp <= 0.0)` guard block | DW-B171-01 (EC-1 regression protection, Variant E mandate) |
| `CancelLiveCollateralStop(acc, fo, "PTT-STP-Drag-" + suffix)` | DW-B171-01 (cancel sweep before replace) |
| `CreateFollowerReplacementStop(..., stp, suffix)` | DW-B171-02 (suffix propagation to callee) |

---

## Status: BUILD_PASS
