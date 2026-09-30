# B171-2 Verification Report

**Ticket ID:** B171-2
**Block:** B171 — DW-B171-01 + DW-B171-02: PTT-STP-Drag Duplicate Stop + fo=NULL on Stop Drag
**Verifier:** ptt-verifier (Phase 4b)
**Source Read:** `src/PropTraderTools/CopyEngine.cs` (READ-ONLY)
**Status:** VERIFY_PASS

---

## 1. Actual Method Bodies Found in Source

### CreateFollowerReplacementStop — Lines 4430–4448

```csharp
// B171: suffix param added (pass-through). CYC=2 unchanged.
// stopPrice guard remains as defensive check for any future caller.
// JS-021: no lock. JS-001: try/catch absorbed in SubmitReplacementStopOrder. JS-002: void return.
private void CreateFollowerReplacementStop(
    Account followerAcc,
    Instrument instr,
    int qty,
    OrderAction stopAction,
    double stopPrice,
    string suffix
)
{
    if (stopPrice <= 0.0)
    {
        StatusUpdate?.Invoke(followerAcc?.Name + ": PTT-STP-Drag-" + suffix + " skipped: stopPrice <= 0");
        return;
    }
    SubmitReplacementStopOrder(followerAcc, instr, qty, stopAction, stopPrice, suffix);
}
```

### SubmitReplacementStopOrder — Lines 4450–4490

```csharp
// B171 DW-B171-02: suffix param added; order name changed from "PTT-STP-Drag" to "PTT-STP-Drag-"+suffix.
// CYC=4 unchanged: base(1)+newStop null(1)+try block(1)+catch(1).
// JS-021: no lock (private, no shared state). JS-001: try/catch absorbs. JS-002: void return. ASCII-only.
private void SubmitReplacementStopOrder(
    Account followerAcc,
    Instrument instr,
    int qty,
    OrderAction stopAction,
    double stopPrice,
    string suffix
)
{
    try
    {
        var newStop = followerAcc.CreateOrder(
            instr,
            stopAction,
            OrderType.StopMarket,
            OrderEntry.Automated,
            TimeInForce.Day,
            qty,
            0,
            stopPrice,
            "",
            "PTT-STP-Drag-" + suffix,
            NinjaTrader.Core.Globals.MaxDate,
            (NinjaTrader.Cbi.CustomOrder)null
        );
        if (newStop == null)
        {
            StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag-" + suffix + ": CreateOrder returned null");
            return;
        }
        followerAcc.Submit(new[] { newStop });
        StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag-" + suffix + " placed @ " + stopPrice.ToString());
    }
    catch (Exception ex)
    {
        StatusUpdate?.Invoke(followerAcc.Name + ": PTT-STP-Drag error: " + ex.Message);
    }
}
```

---

## 2. Change A Checklist — CreateFollowerReplacementStop

| Required Element | Status | Evidence |
|-----------------|--------|----------|
| 6th parameter: `string suffix` | **PRESENT** | Line 4439: `string suffix` |
| Annotation comment with B171 tag | **PRESENT** | Lines 4430–4432: `// B171: suffix param added (pass-through)...` |
| StatusUpdate message: `"PTT-STP-Drag-" + suffix + " skipped: stopPrice <= 0"` | **PRESENT** | Line 4444 |
| SubmitReplacementStopOrder call with 6 params (ends with `suffix`) | **PRESENT** | Line 4447: `SubmitReplacementStopOrder(followerAcc, instr, qty, stopAction, stopPrice, suffix)` |

**Change A: ALL REQUIRED ELEMENTS PRESENT ✓**

---

## 3. Change B Checklist — SubmitReplacementStopOrder

| Required Element | Status | Evidence |
|-----------------|--------|----------|
| 6th parameter: `string suffix` | **PRESENT** | Line 4459: `string suffix` |
| Annotation comment with B171 DW-B171-02 tag | **PRESENT** | Line 4450: `// B171 DW-B171-02: suffix param added...` |
| CreateOrder name argument: `"PTT-STP-Drag-" + suffix` | **PRESENT** | Line 4474 |
| Null-check StatusUpdate: `"PTT-STP-Drag-" + suffix + ": CreateOrder returned null"` | **PRESENT** | Line 4480 |
| Placed StatusUpdate: `"PTT-STP-Drag-" + suffix + " placed @ "` | **PRESENT** | Line 4484 |
| NO unsuffixed `"PTT-STP-Drag"` string literal in this method (catch block intentionally unsuffixed) | **CONFIRMED** | Catch at line 4488 uses `"PTT-STP-Drag error: "` — intentional per plan §4 Method C; no standalone `"PTT-STP-Drag"` literal anywhere in `SubmitReplacementStopOrder` |

**Change B: ALL REQUIRED ELEMENTS PRESENT ✓**

---

## 4. Independent Scan Results (7 Scans)

### SCAN-01 — No `lock()` in CopyEngine.cs

**Command:** `(Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern 'lock\(' | Where-Object { $_.Line -notmatch '^\s*//' }).Count`

**Result: 0** ✓ — No lock() statements in production code.

---

### SCAN-02 — CRITICAL: No unsuffixed `"PTT-STP-Drag"` standalone literal in SubmitReplacementStopOrder

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern '"PTT-STP-Drag"'`

**Raw output hits:**

| Line | Content | Classification |
|------|---------|----------------|
| 2138 | `.StartsWith("PTT-STP-Drag", StringComparison.Ordinal)` | `.StartsWith` prefix check — NOT a standalone string literal |
| 2158 | `.StartsWith("PTT-STP-Drag", StringComparison.Ordinal)` | `.StartsWith` prefix check — NOT a standalone string literal |
| 2167 | Comment: `"PTT-STP-Drag" confirmed L2281` | Comment line — NOT executable code |
| 4450 | Comment: `order name changed from "PTT-STP-Drag"` | Comment in annotation — NOT executable code |
| 4777 | Comment: `"PTT-STP-Drag"` | Comment line — NOT executable code |
| 8938 | `.StartsWith("PTT-STP-Drag", StringComparison.Ordinal)` | `.StartsWith` prefix check — NOT standalone literal |
| **8968** | `0, stopPrice, string.Empty, "PTT-STP-Drag",` | **Executable string literal in `SubmitReplacementStopLeg` — DIFFERENT method, outside B171-2 scope** |
| 9442 | `order.Name == "PTT-STP-Drag"` | Comparison in different method — outside B171-2 scope |
| 9506 | `.StartsWith("PTT-STP-Drag", StringComparison.Ordinal)` | `.StartsWith` prefix check — NOT standalone literal |

**In `SubmitReplacementStopOrder` (lines 4453–4490):** **0 unsuffixed `"PTT-STP-Drag"` standalone literals.** ✓

**Notable: Line 8968** contains `"PTT-STP-Drag"` as a standalone literal in `SubmitReplacementStopLeg` — this is a pre-existing separate method, explicitly outside B171-2 scope. It was documented in the engineer's completion report and was NOT introduced by B171-2.

**SCAN-02 PASS for B171-2 scope: 0 unsuffixed literals in SubmitReplacementStopOrder** ✓

---

### SCAN-03 — Suffixed form `"PTT-STP-Drag-"` appears in expected locations

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern '"PTT-STP-Drag-"'`

**Key hits verified:**

| Line | Method | Purpose |
|------|--------|---------|
| 4474 | `SubmitReplacementStopOrder` | CreateOrder name (B171-2 Change B) ✓ |
| 4480 | `SubmitReplacementStopOrder` | null-check msg (B171-2 Change B) ✓ |
| 4484 | `SubmitReplacementStopOrder` | placed msg (B171-2 Change B) ✓ |
| 4444 | `CreateFollowerReplacementStop` | StatusUpdate skip msg (B171-2 Change A) ✓ |
| 4505 | `ExecutePhaseCStopReplacement` | cancel call (B171-1) ✓ |
| 4741 | `IsDragNameMatch` | unchanged ✓ |

**Note:** Additional hits at lines 3463, 3678, 4047, 4118, 4219, 6858, 9378, 9444, 9638 are in other methods (ResubmitOneCollateralLeg, etc.) — pre-existing, not affected by B171-2.

**SCAN-03 PASS** ✓ — All 3 B171-2 Change B occurrences confirmed at lines 4474, 4480, 4484; Change A confirmed at line 4444.

---

### SCAN-04 — Method definitions and call sites with 6-param signatures

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern 'SubmitReplacementStopOrder|CreateFollowerReplacementStop'`

**Results:**

| Line | Content |
|------|---------|
| 4432 | Comment (in annotation): `SubmitReplacementStopOrder` |
| **4433** | `private void CreateFollowerReplacementStop(` — **definition** |
| **4447** | `SubmitReplacementStopOrder(followerAcc, instr, qty, stopAction, stopPrice, suffix)` — **6-param call site** in CreateFollowerReplacementStop ✓ |
| **4453** | `private void SubmitReplacementStopOrder(` — **definition** |
| 4493 | Comment (in annotation): `CreateFollowerReplacementStop` |
| **4506** | `CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp, suffix)` — **6-param call site** in ExecutePhaseCStopReplacement ✓ |

- `CreateFollowerReplacementStop`: 1 definition (line 4433) + 1 call site (line 4506) — **6-param call confirmed** ✓
- `SubmitReplacementStopOrder`: 1 definition (line 4453) + 1 call site (line 4447) — **6-param call confirmed** ✓

**SCAN-04 PASS** ✓

---

### SCAN-05 — No `DateTime.Now` or `FontFamily` violations

**Command:** `(Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern 'DateTime\.Now[^U]|FontFamily' | Where-Object { $_.Line -notmatch '^\s*//' }).Count`

**Result: 0** ✓

**SCAN-05 PASS** ✓

---

### SCAN-06 — `apply_diff` used (write_file not called)

**Check:** ticket-2-completion.md lines 25 and 42 both state: **"Tool used: `apply_diff` (NOT write_file)"**

The completion report confirms `write_file` was NOT called on `src/PropTraderTools/CopyEngine.cs` in the engineer's session.

**SCAN-06 PASS** ✓

---

### SCAN-07 — `ptt-sync.ps1` deferred (runs after B171-3)

**Check:** ticket-2-completion.md lines 133–137 confirm:
> Per ticket dependency graph `B171-1 → B171-2 → B171-3`, `ptt-sync.ps1` is run ONCE after all three tickets are complete.

Dependency graph in 04-tickets.md states the same (lines 12–18): sequential execution, sync after ALL tickets.

**SCAN-07 PASS** ✓ — Intentionally deferred per architecture. Not a violation.

---

## 5. CYC Analysis

| Method | Decision Points | CYC | Compliant (≤8) |
|--------|----------------|-----|----------------|
| `CreateFollowerReplacementStop` | base(1) + `if (stopPrice <= 0.0)`(1) | **2** | YES ✓ |
| `SubmitReplacementStopOrder` | base(1) + try-block(1) + `if (newStop == null)`(1) + catch-block(1) | **4** | YES ✓ |

Both methods well within CYC limit.

---

## 6. Engineer's Scan Report vs Independent Results — Cross-Check

| Scan | Engineer Reported | Verifier Result | Discrepancy |
|------|------------------|-----------------|-------------|
| SCAN-01 (lock count) | 0 | **0** | NONE ✓ |
| SCAN-02 (unsuffixed literal in SubmitReplacementStopOrder) | 0 matches in target method | **0 in target method** (line 8968 in other method documented correctly) | NONE ✓ |
| SCAN-03 (suffixed form locations) | Lines 4474, 4480, 4484, 4447, 4505, 4741 | **Lines 4474, 4480, 4484, 4444, 4505, 4741** | **MINOR NOTE**: Engineer reported line 4447 for Change A skip msg; actual line is **4444**. StatusUpdate is at 4444, call site to SubmitReplacementStopOrder is at 4447. The correct suffixed StatusUpdate line is 4444, not 4447. Content is correct; line reference in engineer's report was off by 3 (likely 4447 was the `SubmitReplacementStopOrder(...)` call line, not the StatusUpdate). NOT a code violation — the suffixed literal IS present at line 4444. |
| SCAN-04 (method definitions + call sites) | Lines 4433, 4447, 4453, 4506 | **Lines 4433, 4447, 4453, 4506** | NONE ✓ |
| SCAN-05 (DateTime.Now / FontFamily) | 0 | **0** | NONE ✓ |
| SCAN-06 (apply_diff, no write_file) | apply_diff confirmed | Confirmed in completion report | NONE ✓ |
| SCAN-07 (ptt-sync deferred) | Deferred (after B171-3) | Confirmed per plan | NONE ✓ |

**One minor discrepancy:** Engineer's SCAN-03 report cited line 4447 as the Change A "StatusUpdate skip msg" location. Actual StatusUpdate skip msg (`"PTT-STP-Drag-" + suffix + " skipped: stopPrice <= 0"`) is at line **4444**. Line 4447 is the `SubmitReplacementStopOrder(...)` call. This is a documentation error in the completion report only — the actual source code is correct in both locations.

---

## 7. Jane Street DNA Rule Check

| Rule | Status | Evidence |
|------|--------|----------|
| JS-021 — No `lock()` | **PASS** | SCAN-01: 0 results |
| JS-001 — No unhandled throw in dispatch path | **PASS** | `SubmitReplacementStopOrder` wraps all execution in `try/catch`; `CreateFollowerReplacementStop` delegates to it |
| JS-002 — No `return null` where non-null expected | **PASS** | Both methods `void` return; `SubmitReplacementStopOrder` exits early with `return` (not null) |
| Immutability — no unsealed brush | **PASS** | No WPF elements in changed methods |
| NT8 — No `async/await` in init paths | **PASS** | Neither method is `OnInitialize`/`OnDestroyed`; no async |
| NT8 — `CreateOrder` name starts with `"PTT-"` | **PASS** | Line 4474: `"PTT-STP-Drag-" + suffix` — prefix confirmed |
| NT8 — No `DateTime.Now` | **PASS** | SCAN-05: 0 results |
| NT8 — No `FontFamily` | **PASS** | SCAN-05: 0 results |
| NT8 — No hex color `#RRGGBB` | **PASS** | Not introduced in either method |
| ASCII-only string literals | **PASS** | All literals use ASCII `+` concatenation with `suffix` variable |

---

## 8. Architecture Compliance

- `CreateFollowerReplacementStop` exactly matches the **AFTER** block specified in 04-tickets.md § Change A and B171-orchestrator-prompt.md § Method B.
- `SubmitReplacementStopOrder` exactly matches the **AFTER** block specified in 04-tickets.md § Change B and B171-orchestrator-prompt.md § Method C.
- `ExecutePhaseCStopReplacement` (B171-1, lines 4492–4507) is present and correct — confirms B171-1 dependency is satisfied.
- Call chain is intact: `ExecutePhaseCStopReplacement` → `CreateFollowerReplacementStop` → `SubmitReplacementStopOrder` — all with `suffix` threaded through all 3 hops.

---

## 9. Final Verdict

| Check | Result |
|-------|--------|
| Change A — all required elements present | ✓ PASS |
| Change B — all required elements present | ✓ PASS |
| SCAN-01 — no lock() | ✓ PASS |
| SCAN-02 — no unsuffixed literal in SubmitReplacementStopOrder | ✓ PASS |
| SCAN-03 — suffixed form in all expected locations | ✓ PASS |
| SCAN-04 — 6-param signatures and call sites | ✓ PASS |
| SCAN-05 — no DateTime.Now / FontFamily | ✓ PASS |
| SCAN-06 — apply_diff used, no write_file | ✓ PASS |
| SCAN-07 — ptt-sync.ps1 deferred (correct per plan) | ✓ PASS |
| CYC CreateFollowerReplacementStop = 2 (≤8) | ✓ PASS |
| CYC SubmitReplacementStopOrder = 4 (≤8) | ✓ PASS |
| No DNA rule violations | ✓ PASS |
| Engineer scan cross-check — discrepancies | Minor doc error only (line 4444 vs 4447 in SCAN-03 report); code is correct |

---

## ✅ VERIFY_PASS

All required elements of Change A and Change B are present in the actual source.
All 7 scans pass. No lock(), no DateTime.Now, no FontFamily, no unsuffixed "PTT-STP-Drag"
literal in SubmitReplacementStopOrder. CYC within bounds. apply_diff confirmed.
ptt-sync.ps1 correctly deferred pending B171-3.

One documentation-only discrepancy noted (engineer cited line 4447 for Change A StatusUpdate;
actual line is 4444). Source code is correct — not a code violation.
