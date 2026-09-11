# BWAVE-CYC-LOGIC-01 — Ticket T2 Completion Report (Layer 2)

**Engineer:** PTT Engineer (ptt-engineer mode)
**Phase:** 4a — Implementation
**Ticket:** T2 — Group A Actions
**Epic:** BWAVE-CYC-LOGIC-01
**Target file:** `src/PropTraderTools/CopyEngine.cs`
**Date:** 2026-01-01

---

## Implementation Summary

All 11 T2 methods implemented as stub-fills in `src/PropTraderTools/CopyEngine.cs`.
No new methods inserted (all T2 methods had pre-existing stubs).
No modifications to any non-T2 method.
Hard-link sync performed via `build_readiness.ps1` (calls `deploy-sync.ps1` internally).

### Methods Implemented

| Method | Spec ID | CYC | Lines (post-T1 shift) |
|--------|---------|-----|-----------------------|
| `RegisterBeRetryIfNoTargets` | A-06 | 1 | ~L7953 |
| `RegisterPartialTargetBeRetry` | A-07 | 1 | ~L7960 |
| `CancelExistingStpDragOrders` | A-08 | 4 | ~L7968 |
| `CancelExistingTgtDragOrders` | A-09 | 4 | ~L7981 |
| `SubmitReplacementStopLeg` | A-10 | 3 | ~L7997 |
| `SubmitReplacementTargetLeg` | A-11 | 3 | ~L8013 |
| `SyncAtmFollowerStopBracket` | A-16 | 3 | ~L8094 |
| `CancelStaleTgtDragOrders` | A-17 | 4 | ~L8104 |
| `CreateAndSubmitReplacementTarget` | A-18 | 3 | ~L8115 |
| `ResubmitFollowerEntry` | A-22 | 8 | ~L8166 |
| `CancelStaleCascadeTgtDrag` | A-24 | 8 | ~L8199 |

### Implementation Notes

- **A-16 `SyncAtmFollowerStopBracket`**: Ticket text used `leaderStop.FromEntrySignalName` but
  `FromEntrySignalName` is a property on `FollowerBinding` struct (not on NT8 `Order`). The
  correct NT8 `Order` property is `FromEntrySignal` (confirmed at production call site L2786).
  Used `leaderStop.FromEntrySignal` — matching existing production code pattern.
- **A-22 `ResubmitFollowerEntry`**: Uses `DateTime.MaxValue` (static constant, not `DateTime.Now`).
  Pre-loads `_dedupCache` before `acc.Submit` to prevent re-dispatch on Working event.
- **A-24 `CancelStaleCascadeTgtDrag`**: Preserves orders that match the current leader suffix
  (the `continue` on EndsWith check skips cancellation of own drag orders).
- All NT8 API calls (`acc.Cancel`, `acc.CreateOrder`, `acc.Submit`) wrapped in `try { } catch { }`.
- All `CreateOrder` calls use "PTT-" prefixed names per SCAN-05 requirement.

---

## 7-Scan Report (Layer 2)

### SCAN-01: Build Scan
**Command:** `powershell -File .\scripts\build_readiness.ps1`
**Result:** ASCII GATE PASS, DIFF GUARD PASS, SOVEREIGN AUDIT PASS, SYNC COMPLETE.
Linting.csproj errors are pre-existing V12_002 NT8 environment errors (HashSet, NetworkStream,
Key, DependencyObject — all unrelated to PropTraderTools/CopyEngine.cs).
Zero new errors in `CopyEngine.cs`.
**SCAN-01: PASS**

### SCAN-02: Lock Scan
**Command:** `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\("`
**Result:** Zero actual `lock(` statements. All matches are comment text only
(e.g. `// JS-021: no lock()`). No lock() in any T2 method.
**SCAN-02: PASS — 0 hits**

### SCAN-03: Unicode Scan
**Command:** PowerShell non-ASCII character scan of CopyEngine.cs
**Result:** `SCAN-03 PASS: 0 non-ASCII chars`
All string literals in T2 methods are ASCII-only:
"PTT-STP-Drag", "PTT-TGT-Drag", "PTT-Copy", "PTT-TGT-Drag-"
**SCAN-03: PASS — 0 hits**

### SCAN-04: Cyclomatic Complexity
**Manual verification of all 11 T2 methods against ticket-approved CYC values:**

| Method | Approved CYC | Code CYC | <= 8? |
|--------|-------------|----------|-------|
| A-06 RegisterBeRetryIfNoTargets | 1 | 1 | Y |
| A-07 RegisterPartialTargetBeRetry | 1 | 1 | Y |
| A-08 CancelExistingStpDragOrders | 4 | 4 | Y |
| A-09 CancelExistingTgtDragOrders | 4 | 4 | Y |
| A-10 SubmitReplacementStopLeg | 3 | 3 | Y |
| A-11 SubmitReplacementTargetLeg | 3 | 3 | Y |
| A-16 SyncAtmFollowerStopBracket | 3 | 3 | Y |
| A-17 CancelStaleTgtDragOrders | 4 | 4 | Y |
| A-18 CreateAndSubmitReplacementTarget | 3 | 3 | Y |
| A-22 ResubmitFollowerEntry | 8 | 8 | Y |
| A-24 CancelStaleCascadeTgtDrag | 8 | 8 | Y |

All `// CYC=N` comments verified in code at lines ~7955, 7963, 7971, 7984, 8000, 8016,
8096, 8107, 8120, 8168, 8204.
**SCAN-04: PASS — all CYC <= 8**

### SCAN-05: Lint Scan
**Command:** `powershell -File .\scripts\lint.ps1`
**Result:** Zero CopyEngine.cs / PropTraderTools violations. 323 errors all in V12_002
files (pre-existing NT8 environment assembly-reference errors unrelated to this ticket).
**SCAN-05: PASS — 0 new PropTraderTools violations**

### SCAN-06: Test Scan
**Command:** `dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj`
**Result:** `Passed! - Failed: 0, Passed: 159, Skipped: 355, Total: 514`
**SCAN-06: PASS — Failed=0, Passed=159 (>= 159)**

### SCAN-07: RULES_CATALOG Compliance
**Checks performed:**
- `lock(` actual usage: 0 (SCAN-02 confirmed)
- `FontFamily`: 0 actual usage (comment-only matches in unrelated methods)
- `#[0-9A-Fa-f]{6}` hex color: 0 matches
- `DateTime.Now` (non-UTC): 0 matches in T2 methods. A-22 uses `DateTime.MaxValue` (constant). No `DateTime.Now` anywhere in T2 methods.
- `async/await`: 0 in any T2 method
- `throw`: 0 in any T2 method
- All NT8 API calls in `try { } catch { }`: verified for A-08, A-09, A-10, A-11, A-17, A-18, A-22, A-24
- All `CreateOrder` names start with "PTT-": A-10="PTT-STP-Drag", A-11="PTT-TGT-Drag", A-18="PTT-TGT-Drag", A-22="PTT-Copy"
- Unicode: 0 (SCAN-03 confirmed)
**SCAN-07: PASS — 0 violations**

---

## Summary

All 7 scans pass at zero violations. All 11 T2 methods implemented per spec.
Hard-link sync completed. Test suite: Failed=0, Passed=159.

**BUILD_PASS**
