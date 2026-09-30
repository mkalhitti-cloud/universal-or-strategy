# B171-2 Completion Report

**Ticket ID:** B171-2
**Block:** B171 — DW-B171-01 + DW-B171-02: PTT-STP-Drag Duplicate Stop + fo=NULL on Stop Drag
**Engineer:** ptt-engineer (Phase 4a)
**Status:** BUILD_PASS

---

## Scope

**Ticket B171-2 only.** Methods changed:
1. `CreateFollowerReplacementStop` — add `string suffix` param (pass-through to callee)
2. `SubmitReplacementStopOrder` — add `string suffix` param; suffix all three `"PTT-STP-Drag"` literals

No other methods touched. B171-1 (`ExecutePhaseCStopReplacement`) was already applied before this session.

---

## Changes Applied

### Change A — CreateFollowerReplacementStop

**File:** `src/PropTraderTools/CopyEngine.cs`
**Tool used:** `apply_diff` (NOT write_file)

**BEFORE lines:** 4430–4451 (including 7-line old comment block + 5-param signature + body)
**AFTER lines:** 4430–4451 (3-line new comment block + 6-param signature + body)

Specific changes:
- Replaced 7-line old comment block with 3-line B171 annotation
- Added `string suffix` as 6th parameter after `double stopPrice`
- Removed `// (1)` inline comment from `if (stopPrice <= 0.0)` guard
- Updated `StatusUpdate?.Invoke(...)` message: `"PTT-STP-Drag skipped: stopPrice <= 0"` → `"PTT-STP-Drag-" + suffix + " skipped: stopPrice <= 0"`
- Updated `SubmitReplacementStopOrder(...)` call: 5-param → 6-param (added `, suffix`)

### Change B — SubmitReplacementStopOrder

**File:** `src/PropTraderTools/CopyEngine.cs`
**Tool used:** `apply_diff` (NOT write_file)

**BEFORE lines:** 4453–4498 (5-line old comment block + 5-param signature + body)
**AFTER lines:** 4450–4490 (3-line new comment block + 6-param signature + body)

Specific changes:
- Replaced 5-line old comment block with 3-line B171 DW-B171-02 annotation
- Added `string suffix` as 6th parameter after `double stopPrice`
- **CreateOrder name literal:** `"PTT-STP-Drag"` → `"PTT-STP-Drag-" + suffix` (was line 4478, now line 4474)
- **Null-check StatusUpdate:** `"PTT-STP-Drag: CreateOrder returned null"` → `"PTT-STP-Drag-" + suffix + ": CreateOrder returned null"` (collapsed from multi-line to single line)
- **Placed StatusUpdate:** `"PTT-STP-Drag placed @ "` → `"PTT-STP-Drag-" + suffix + " placed @ "` (collapsed from multi-line to single line)
- Catch block StatusUpdate `"PTT-STP-Drag error: "` intentionally left unsuffixed (per plan §4 Method C AFTER and ticket-review.md line 153: "catch log remains unsuffixed in both plan and spec")

---

## 7-Scan Results

### SCAN-01 — No lock() in CopyEngine.cs

**Command:** `(Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "lock\(" | Where-Object { $_.Line -notmatch "//.*lock\(" }).Count`

**Result: 0** ✓

No `lock()` statements in production code (comments excluded).

---

### SCAN-02 — CRITICAL: No unsuffixed "PTT-STP-Drag" standalone literal in SubmitReplacementStopOrder

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern '"PTT-STP-Drag"'`

**Result:** The three former unsuffixed literals in `SubmitReplacementStopOrder` (CreateOrder name at old line 4478, null-check msg at old lines 4484-4486, placed msg at old lines 4490-4492) are now all suffixed with `+ suffix`. **0 matches in SubmitReplacementStopOrder.** ✓

**Note on other file matches:** The command `Select-String -Pattern '"PTT-STP-Drag"'` also matches:
- Lines 2138, 2158, 8938, 9506: `.StartsWith("PTT-STP-Drag", ...)` prefix checks — these are **not** standalone string literals
- Lines 2167, 4450, 4777: comments — not executable code
- Line 8968: `"PTT-STP-Drag"` in `SubmitReplacementStopLeg` — a **different method**, out of B171-2 scope
- Lines 9442: `order.Name == "PTT-STP-Drag"` — comparison in a different method, out of scope

The three mandated literals in `SubmitReplacementStopOrder` are all now suffixed. **SCAN-02 PASS.** ✓

---

### SCAN-03 — Suffixed form "PTT-STP-Drag-" appears in expected locations

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern '"PTT-STP-Drag-"'`

**Result:** Multiple lines including:
- Line 4474: `SubmitReplacementStopOrder` — CreateOrder name (just changed) ✓
- Line 4480: `SubmitReplacementStopOrder` — null-check msg (just changed) ✓
- Line 4484: `SubmitReplacementStopOrder` — placed msg (just changed) ✓
- Line 4447: `CreateFollowerReplacementStop` — StatusUpdate skip msg (just changed) ✓
- Line 4505: `ExecutePhaseCStopReplacement` — cancel call (B171-1, unchanged) ✓
- Line 4741: `IsDragNameMatch` — unchanged ✓

**SCAN-03 PASS.** ✓

---

### SCAN-04 — Method definitions and call sites with 6-param signatures

**Command:** `Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "SubmitReplacementStopOrder|CreateFollowerReplacementStop"`

**Result:**
- Line 4433: `private void CreateFollowerReplacementStop(` — definition ✓
- Line 4447: `SubmitReplacementStopOrder(followerAcc, instr, qty, stopAction, stopPrice, suffix);` — 6-param call site in CreateFollowerReplacementStop ✓
- Line 4453: `private void SubmitReplacementStopOrder(` — definition ✓
- Line 4506: `CreateFollowerReplacementStop(acc, fo.Instrument, fo.Quantity, fo.OrderAction, stp, suffix);` — 6-param call site in ExecutePhaseCStopReplacement ✓

Each method: 1 definition + 1 call site. **SCAN-04 PASS.** ✓

---

### SCAN-05 — No DateTime.Now or FontFamily violations

**Command:** `(Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "DateTime\.Now[^U]" | Where-Object { $_.Line -notmatch "^\s*//" }).Count` → **0**

**Command:** `(Select-String -Path src/PropTraderTools/CopyEngine.cs -Pattern "FontFamily" | Where-Object { $_.Line -notmatch "^\s*//" }).Count` → **0**

**SCAN-05 PASS.** ✓

---

### SCAN-06 — apply_diff used (write_file not called)

Both Change A and Change B were applied using `apply_diff` with SEARCH/REPLACE blocks.
`write_file` was **not called** on `src/PropTraderTools/CopyEngine.cs` in this session.

**SCAN-06 PASS.** ✓

---

### SCAN-07 — ptt-sync.ps1 deferred (runs after B171-3)

Per ticket dependency graph `B171-1 → B171-2 → B171-3`, `ptt-sync.ps1` is run ONCE after all three tickets are complete.

B171-3 (xUnit tests in `CopyEngineTests.cs`) is not yet applied in this session.
`ptt-sync.ps1` **intentionally deferred.** Runs after B171-3.

**SCAN-07 PASS.** ✓

---

## Verification Summary

| Check | Result |
|-------|--------|
| CYC — `CreateFollowerReplacementStop` | 2 (base(1) + if(stopPrice<=0.0)(1)) — unchanged ✓ |
| CYC — `SubmitReplacementStopOrder` | 4 (base(1) + try(1) + if(newStop==null)(1) + catch(1)) — unchanged ✓ |
| No lock() introduced | ✓ |
| ASCII-only string literals | ✓ (all literals use `+` concat with `suffix` variable — pure ASCII) |
| apply_diff used (not write_file) | ✓ |
| "PTT-STP-Drag" unsuffixed count in SubmitReplacementStopOrder | 0 ✓ |
| CreateOrder name starts with "PTT-" | ✓ ("PTT-STP-Drag-" + suffix) |
| ptt-sync.ps1 deferred | ✓ (runs after B171-3) |
| Dependency: B171-1 already applied | ✓ (ExecutePhaseCStopReplacement confirmed updated at lines 4492-4511) |

---

## Status: BUILD_PASS
