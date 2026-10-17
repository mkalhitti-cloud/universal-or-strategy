# B198 Ticket 2a — Completion Report
**Engineer**: ptt-engineer (Phase 4a)
**Date**: 2026-10-13
**Ticket**: Ticket 2a — QTY Bug Instrumentation (DW-B198-QTY-01 Phase A)
**Source of truth**: docs/brain/B198/04-tickets.md §Ticket 2a
**Pre-condition**: TICKET_REVIEW_PASS confirmed (Cycle 2) in docs/brain/B198/04-ticket-review.md

---

## Implementation Summary

Added exactly 2 diagnostic log lines to `SendCopyWithAtm` in `src/PropTraderTools/CopyEngine.cs`.
No other changes made to `CopyEngine.cs`. No logic change. Zero CYC delta.

### Log Line 1 — Before CreateOrder

**File**: `src/PropTraderTools/CopyEngine.cs`
**Insertion point**: Line 5024 (immediately before `var order = follower.CreateOrder(`)
**Exact code inserted**:
```csharp
StatusUpdate?.Invoke(
    follower.Name + ": ATM-send qty=" + signal.Quantity + " tmpl=" + namedMode.TemplateName);
```

### Log Line 2 — After null guard

**File**: `src/PropTraderTools/CopyEngine.cs`
**Insertion point**: Lines 5042-5043 (immediately after `if (order == null) return false; // (2)`)
**Exact code inserted**:
```csharp
StatusUpdate?.Invoke(
    follower.Name + ": ATM-order qty=" + order.Quantity);
```

**Placement safety**: Log line 2 is after the null-guard so `order.Quantity` is guaranteed non-null. No new branch introduced.

---

## 7-Scan Results

### SCAN-01: lock() check
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "lock\s*\(" | Where-Object { $_.Line -notmatch "//" }`
**Result**: 0 matches — PASS

### SCAN-02: async void check
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "async void " | Where-Object { $_.Line -notmatch "//" }`
**Result**: 0 code matches (1 comment match excluded) — PASS

### SCAN-03: return null check
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "return null;"`
**Result**: 13 pre-existing matches, all outside T2a change scope — 0 new return null introduced — PASS

### SCAN-04: CYC delta for SendCopyWithAtm
**Method**: `SendCopyWithAtm` (private bool, line 5015)
**CYC before T2a**: 4 (documented in comment line 5013)
**CYC after T2a**: 4 (unchanged — `StatusUpdate?.Invoke(...)` null-conditional is not a McCabe branch)
**Delta**: 0 — PASS

### SCAN-05: Non-ASCII characters
**Command**: `Select-String -Path "src/PropTraderTools/CopyEngine.cs" -Pattern "[^\x00-\x7F]"`
**Result**: 0 matches — PASS

### SCAN-06: xUnit gate on B198Tests.cs
**Command**: `Select-String -Path "src/PropTraderTools/Tests/B198Tests.cs" -Pattern "NUnit|MSTest|\[TestMethod\]|\[Test\]"`
**Result**: 1 comment match (`// Framework: xUnit only. No NUnit. No MSTest.`) — 0 actual framework references — PASS

### SCAN-07: No new shared fields
**Verification**: T2a adds only two `StatusUpdate?.Invoke(...)` expression statements — no fields, no variables, no properties added to CopyEngine class.
**ATM-send/ATM-order grep**: Exactly 2 matches in SendCopyWithAtm scope only.
**Result**: 0 new fields — PASS

---

## Build Output

```
dotnet build src/PropTraderTools/PropTraderTools.Tests.csproj
Build succeeded.
    0 Error(s)
    [warnings: pre-existing CS0436 type conflicts — not introduced by T2a]
```

**Errors: 0**

---

## Test Output

```
dotnet test src/PropTraderTools/PropTraderTools.Tests.csproj --filter "B198Tests" --no-build
Passed!  - Failed: 0, Passed: 18, Skipped: 0, Total: 18, Duration: 187 ms
```

| Test | Result |
|------|--------|
| T1-01..T1-16 (SimStop N-General) | 16 PASS (regression) |
| T2a-01 SendCopyWithAtm_LogsAtmSendQtyBeforeCreateOrder | PASS |
| T2a-02 SendCopyWithAtm_LogsAtmOrderQtyAfterCreateOrder | PASS |
| **Total** | **18 PASS, 0 FAIL** |

---

## PTT Sync

```
powershell -File .\scripts\ptt-sync.ps1
ptt-sync complete: 19 linked, 0 errors
```

---

## Acceptance Criteria Checklist

1. [x] `dotnet test` passes T2a-01 and T2a-02 (2 tests GREEN)
2. [x] `Select-String ... -Pattern "ATM-send qty="` returns exactly 1 match (line 5025 in SendCopyWithAtm)
3. [x] `Select-String ... -Pattern "ATM-order qty="` returns exactly 1 match (line 5043 in SendCopyWithAtm)
4. [x] SendCopyWithAtm CYC = 4 (unchanged from before T2a)
5. [x] SCAN-01 through SCAN-07 all PASS
6. [ ] `docs/brain/B198/ticket-2a-sim-evidence.md` — **NOT YET CREATED** (requires NT8 SIM run; see notes below)

---

## SIM Evidence Note

The SIM run (NT8 SIM environment: Leader=Sim101, Followers=Sim102/103/104, MGC DEC26, MGC $400 SL4) is a runtime step that requires the NinjaTrader environment. The instrumentation is in place. The `ticket-2a-sim-evidence.md` file must be created by the engineer after deploying and running the SIM protocol defined in the ticket.

---

## Files Modified

| File | Change | Location |
|------|--------|----------|
| `src/PropTraderTools/CopyEngine.cs` | +2 log lines in SendCopyWithAtm | Lines 5024-5025, 5042-5043 |
| `src/PropTraderTools/Tests/B198Tests.cs` | +T2a-01 and T2a-02 [Fact] tests | Appended to B198Tests class |

---

## Return: BUILD_PASS
