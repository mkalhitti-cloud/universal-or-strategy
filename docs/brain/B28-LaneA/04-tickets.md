# B28-LaneA Tickets

**Block**: B28-LaneA
**Defect**: DW-B28-01 (P0 CRITICAL) — BE stop price never changes on live account
**Plan status**: REVIEW_PASS
**Date**: 2026-07-16
**Total tickets**: 1 (T1)

---

## T1 — Insert Diagnostic StatusUpdate Line in MoveStopToBreakEven

### Spec Requirements Satisfied

| ID | Description |
|----|-------------|
| DW-B28-01 | P0 diagnostic hardening — insert pre-Change StatusUpdate to distinguish "reached acc.Change()" from "exception thrown by acc.Change()" on next live test |

---

### File Path

```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
```

---

### Method Signature Touched

```csharp
private void MoveStopToBreakEven(Account acc, Instrument instrument, int bufferTicks)
```

No signature change. No new methods. No new overloads.

---

### Exact Code Change

**Location**: `CopyEngine.cs` — inside the `try` block in `MoveStopToBreakEven`, at approximately line 1197.

**BEFORE:**

```csharp
order.StopPrice = newStop;
acc.Change(new Order[] { order });
StatusUpdate?.Invoke(acc.Name + ": BE moved to " + newStop);
```

**AFTER:**

```csharp
order.StopPrice = newStop;
StatusUpdate?.Invoke(acc.Name + ": BE attempting acc.Change -> " + newStop);  // DW-B28-01 diagnostic
acc.Change(new Order[] { order });
StatusUpdate?.Invoke(acc.Name + ": BE moved to " + newStop);
```

**Delta**: +1 line inserted between `order.StopPrice = newStop;` and `acc.Change(...)`. Zero lines deleted.

---

### JS Rule Constraints

| Rule | Requirement | Applies to This Change |
|------|-------------|----------------------|
| JS-021 | No `lock()` anywhere in `CopyEngine.cs` | MUST NOT introduce any `lock()` call. SCAN-01 enforces this. |
| JS-033 | No `async void` (non-event-handler) in `CopyEngine.cs` | MUST NOT introduce any `async void` method. SCAN-02 and SCAN-07 enforce this. |
| CYC <= 8 | `MoveStopToBreakEven` cyclomatic complexity must not increase | PASS — single straight-line `StatusUpdate?.Invoke(...)` adds zero branches. No `if`, `switch`, `while`, `for`, `&&`, `||`, `??` added. |
| ASCII-only | All string literals must be ASCII characters only | PASS — `"BE attempting acc.Change -> "` is pure ASCII. No Unicode, emoji, curly quotes, or non-ASCII characters permitted. |
| NT8 / DateTime | No `DateTime.Now` | N/A — no DateTime usage in this change. |
| NT8 / CreateOrder | CreateOrder arg 12 pattern | N/A — no `CreateOrder` call in this change. |

---

### xUnit [Fact] Tests

**New tests added by T1: NONE.**

| Metric | Value |
|--------|-------|
| Baseline `[Fact]` count | 135 |
| T1 new tests | 0 |
| Target `[Fact]` count after T1 | 135 |

**Rationale**: T1 is a diagnostic-only change. The inserted line is a null-conditional
`StatusUpdate?.Invoke(...)` with no branching logic. CYC is unchanged. All existing 135
`[Fact]` tests continue to cover `MoveStopToBreakEven` happy-path and error-path
behaviour. SCAN-03 and SCAN-06 enforce baseline count and full test pass.

---

### 7-Scan Checklist (Engineer Contract)

The ptt-engineer MUST run all seven scans after applying the change and before commit.
All scans must return the expected result. Any scan returning an unexpected result is a
**HARD STOP** — do not commit until all seven scans pass.

Run all scans from:
```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\
```
except SCAN-05 and SCAN-06, which run from the solution root:
```
c:\WSGTA\universal-or-strategy\
```

---

**SCAN-01** — lock() ban

```powershell
grep -n "lock(" CopyEngine.cs
```

Expected: **0 results**
Failure action: Locate and remove any `lock()` call before proceeding.

---

**SCAN-02** — async void ban

```powershell
grep -n "async void " CopyEngine.cs
```

Expected: **0 results**
Failure action: Convert any `async void` (non-event-handler) to `async Task` before proceeding.

---

**SCAN-03** — [Fact] count baseline

```powershell
Select-String -Path CopyEngineTests.cs -Pattern "\[Fact\]" | Measure-Object
```

Expected: **Count = 135**
Failure action: If count != 135, a test was added or deleted inadvertently — revert test file changes.

---

**SCAN-04** — diagnostic line present

```powershell
grep -n "BE attempting acc.Change" CopyEngine.cs
```

Expected: **exactly 1 result**
Failure action: If 0 results, the diagnostic line was not inserted — apply the change.
If > 1 result, the line was duplicated — remove the duplicate.

---

**SCAN-05** — build gate

```powershell
dotnet build
```

Run from: `c:\WSGTA\universal-or-strategy\`

Expected: **0 errors, 0 warnings**
Failure action: Fix all compiler errors before proceeding. Do not commit a broken build.

---

**SCAN-06** — test gate

```powershell
dotnet test
```

Run from: `c:\WSGTA\universal-or-strategy\`

Expected: **all 135 [Fact] tests pass**
Failure action: If any test fails, diagnose and fix before committing.

---

**SCAN-07** — async void ban (protocol duplicate of SCAN-02)

```powershell
grep -n "async void " CopyEngine.cs
```

Expected: **0 results**
Failure action: Same as SCAN-02 — convert any `async void` to `async Task`.

*(SCAN-07 is a required duplicate of SCAN-02 per the 7-scan protocol contract.)*

---

### Completion Criteria

T1 is complete when ALL of the following are satisfied:

- [ ] SCAN-01 passes (0 `lock()` results)
- [ ] SCAN-02 passes (0 `async void` results)
- [ ] SCAN-03 passes (Count = 135)
- [ ] SCAN-04 passes (exactly 1 `"BE attempting acc.Change"` hit)
- [ ] SCAN-05 passes (BUILD_PASS — 0 errors, 0 warnings)
- [ ] SCAN-06 passes (all 135 tests pass)
- [ ] SCAN-07 passes (0 `async void` results)
- [ ] Commit message includes `DW-B28-01` reference

---

### Files Changed by T1

| File | Change | Lines |
|------|--------|-------|
| `src/PropTraderTools/CopyEngine.cs` | 1 `StatusUpdate?.Invoke(...)` line inserted inside existing `try` block in `MoveStopToBreakEven` | +1 |
| `src/PropTraderTools/TradeCopierPanel.cs` | **No change** | — |
| `src/PropTraderTools/CopyEngineTests.cs` | **No change** | — |

**Total source delta**: +1 line, 0 lines deleted.

---

*End of B28-LaneA tickets. One ticket. One line. All 7 scans required before commit.*
