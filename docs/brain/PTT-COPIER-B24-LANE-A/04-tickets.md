# PTT-COPIER-B24-LANE-A — Tickets
# Phase: 3 (Ticket Generation)
# Author: ptt-architect
# Source plan: 02-architecture-plan.md (REVIEW_PASS)
# Defect: DW-B24-LEADER-CASTNULL-01
# Date: 2026-07-17

---

## T1 — Fix WireLeaderAccount() text-fallback for cast-null at NT8 inject time

### Identifiers

| Field | Value |
|-------|-------|
| Ticket ID | T1 |
| Defect ID | DW-B24-LEADER-CASTNULL-01 |
| Spec requirement | PTT-COPIER-B24: cold-start leader account wiring |
| Block | PTT-COPIER-B24-LANE-A |
| Priority | P0 — user-visible regression (panel non-functional on cold start) |

---

### File Path (Wave Workspace)

```
C:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierAddOn.cs
```

**Write-set: this file ONLY.** No other file may be modified.

---

### Problem Summary

At NT8 chart inject time (`DoInject` → `WireLeaderAccount`), `ComboBox.SelectedItem` is a WPF
data-binding sentinel object that has not yet materialised as a `NinjaTrader.Cbi.Account`. The cast:

```csharp
var current = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
```

returns `null` silently. `panel.SetLeaderAccount` is never called.
`ComboBox.Text` already contains the displayed account name string (e.g. `"Sim101"`) at this point
and provides a recovery path via `Account.All.FirstOrDefault`.

---

### Method Signature

```csharp
// TradeCopierAddOn.cs  —  ~lines 443-464
private static void WireLeaderAccount(ChartTrader chartTrader, TradeCopierPanel panel)
```

| Property | Value |
|----------|-------|
| Visibility | `private static` |
| Return type | `void` |
| Parameters | `ChartTrader chartTrader`, `TradeCopierPanel panel` |
| CYC before | 4 |
| CYC after  | 6 (within Jane Street ≤ 8 ceiling) |
| Change type | 3 lines added inside method body — no signature change |

---

### Exact Code Change

Locate the following two lines inside `WireLeaderAccount` (approximately lines 455-456):

**EXISTING — find this exact block:**

```csharp
var current = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
if (current != null) panel.SetLeaderAccount(current);
```

**REPLACE with (4 lines):**

```csharp
var current = accountCombo.SelectedItem as NinjaTrader.Cbi.Account;
if (current == null && accountCombo.Text != null)
    current = Account.All.FirstOrDefault(
        a => string.Equals(a.Name, accountCombo.Text,
                           StringComparison.OrdinalIgnoreCase));
if (current != null) panel.SetLeaderAccount(current);
```

**What changed:**
- Added 3 lines: the `if (current == null && accountCombo.Text != null)` guard and the
  `Account.All.FirstOrDefault(...)` lookup.
- The existing `if (current != null)` guard on the last line is **unchanged**.
- All other lines in `WireLeaderAccount` are **untouched**.

**Mandatory invariants (engineer must not deviate):**

| # | Constraint | Reason |
|---|-----------|--------|
| 1 | `StringComparison.OrdinalIgnoreCase` — NOT `==` or `InvariantCulture` | Case-sensitive match fails silently; ordinal is correct for account name comparison |
| 2 | `Account.All.FirstOrDefault` runs once, at inject time only | Never inside a loop or timer; `DoInject` fires once per chart window |
| 3 | `SelectionChanged` subscription (below the fix site) stays **unchanged** | It already handles all future account switches correctly |
| 4 | No new `Dispatcher.InvokeAsync` call inside `WireLeaderAccount` | NT8-042 constraint — see NT8 rules check below |

**NT8 Pre-condition (already satisfied, engineer must verify):**
- `using System.Linq;` is present at **line 18** of `TradeCopierAddOn.cs` (required for
  `Account.All.FirstOrDefault`). If for any reason it is absent, add it — do not add a second copy.

---

### JS Rule Constraints

| Rule ID | Rule | Applied to | Status |
|---------|------|-----------|--------|
| JS-021 (P0) | No `lock()` anywhere | `TradeCopierAddOn.cs` write-set | PASS — fix uses read-only `Account.All`, no mutation |
| JS-002 (P0) | No `return null` | `WireLeaderAccount` | PASS — method is `void`; no return value |
| JS-001 (P0) | No `throw` in hot paths | `WireLeaderAccount` | PASS — no exceptions introduced |
| JS-033 (P0) | No `async void` | `WireLeaderAccount` | PASS — method is synchronous `void` |
| ASCII-Only | No Unicode / curly quotes in string literals | All new code | PASS — all identifiers and strings are ASCII |

---

### NT8 Compiler Rules Check

| Rule ID | Description | Status |
|---------|-------------|--------|
| NT8-006 | `using System.Linq` required for `FirstOrDefault` | PASS — present at line 18 |
| NT8-021 | `Account.All` must not be accessed in constructors or field initializers | PASS — call site is inside `WireLeaderAccount`, invoked from `DoInject` lifecycle path; NT8 account infra is fully initialised |
| NT8-042 | No new `Dispatcher.InvokeAsync` introduced | PASS — fix adds none; pre-existing calls at lines ~251/293 are out of scope |
| NT8-018 | `lock()` is banned | PASS — no `lock()` in fix |
| NT8-001 | `{ get; init; }` is banned | PASS — no properties modified |
| NT8-003 | `volatile double` is banned | PASS — no double fields modified |

---

### [Fact] Delta

**Delta: 0** — no tests added, no tests removed.
**[Fact] count after ticket: 126 exactly.**

**Rationale**: `WireLeaderAccount` depends on a live `ChartTrader` WPF visual tree,
a live `ComboBox` with real `SelectedItem`/`Text`, and `Account.All` populated by the NT8 runtime.
None of these are available in the `CopyEngineTests` stub harness (no NT8 runtime, no WPF message
pump, no `ComboBox`). Adding a test that stubs all three would test the stub, not the fix.
The verification contract is the manual cold-start gate (see Verification Contract below).

---

### 7-Scan Checklist (Engineer Contract — All 7 Must Be Zero / Pass Before Marking Complete)

The engineer MUST run all 7 scans against the final diff and record results before marking T1 done.

```
SCAN-01  lock()
  Command : grep -r "lock(" src/PropTraderTools/TradeCopierAddOn.cs
  Expected: 0 matches
  Rule    : JS-021 — lock() is banned
  Action  : FAIL if any match found — remove lock() and replace with ConcurrentQueue / Interlocked

SCAN-02  async void
  Command : grep -rn "async void " TradeCopierAddOn.cs
  Expected: 0 matches
  Rule    : JS-033 — async void is banned outside event handlers
  Action  : FAIL if any match found — convert to async Task

SCAN-03  return null in changed method
  Command : grep -n "return null" TradeCopierAddOn.cs  (scope to WireLeaderAccount body only)
  Expected: 0 matches in WireLeaderAccount
  Rule    : JS-002 — method is void, no return null can exist
  Action  : FAIL if found inside WireLeaderAccount — method should return nothing

SCAN-04  DateTime.Now (file-level)
  Command : grep -n "DateTime\.Now" TradeCopierAddOn.cs
  Expected: 0 matches
  Rule    : NT8-013 (P0) — DateTime.Now is banned; use DateTime.UtcNow
  Action  : FAIL if any match found — replace DateTime.Now with DateTime.UtcNow

SCAN-05  volatile double
  Command : grep -n "volatile double" TradeCopierAddOn.cs
  Expected: 0 matches
  Rule    : NT8-003 — volatile double causes CS0677
  Action  : FAIL if found — remove volatile keyword

SCAN-06  CYC ceiling
  Command : python complexity_audit.py WireLeaderAccount
            (or: python scripts/complexity_audit.py --method WireLeaderAccount)
  Expected: CYC <= 8
  Result  : CYC = 6 after fix (was 4 before fix)
  Action  : FAIL if CYC > 8 — decompose method

SCAN-07  OrdinalIgnoreCase present (positive-presence mandate)
  Command : grep -n "OrdinalIgnoreCase" TradeCopierAddOn.cs
  Expected: 1 match (in WireLeaderAccount body)
  Rule    : Mandate — fix MUST use StringComparison.OrdinalIgnoreCase, not == or InvariantCulture
  Action  : FAIL if 0 matches — the text-fallback lookup is silently broken without OrdinalIgnoreCase

ADVISORY (non-blocking): Pre-existing InvokeAsync calls at lines ~251 and ~293 are OUT OF SCOPE.
  The engineer MUST NOT touch those lines. Confirm by reviewing the diff: no new "+ InvokeAsync"
  lines should appear. This is a diff-hygiene reminder, not a blocking scan gate.
```

---

### Verification Contract (Manual Cold-Start Gate)

After implementing the fix and building successfully, engineer MUST verify:

| Step | Action | Expected Result |
|------|--------|----------------|
| 1 | Open a MES chart with Sim101 selected in ChartTrader. **Do NOT touch the account dropdown** (cold start). | — |
| 2 | Observe panel status bar. | Must read `"Ready: MES SEP26"` (not `"No leader"`) |
| 3 | Check [Fact] count. | Must be **126** exactly (0 delta) |
| 4 | `dotnet build Linting.csproj` | 0 errors, 0 warnings |
| 5 | F5 NT8 compile | Green — 0 compiler errors |

All 5 steps must pass. If step 2 fails the fix did not take effect. Re-check the exact location of
the replaced lines; the `StringComparison.OrdinalIgnoreCase` guard must run before the
`if (current != null)` guard.

---

### Deferred Backlog

The following items from `docs/brain/PTT-COPIER-B23-LANE-C/06-deferred-backlog.md` remain OPEN
and are **not targeted** by this ticket:

| ID | Description | Status |
|----|-------------|--------|
| DW-B23-LANE-C-01 | Add short-direction `[Fact]` test for `PendingBe_Armed_FiresAtPriceTarget_Short` | OPEN — P2 |
| DW-B23-LANE-C-02 | Pre-existing `return null` at `CopyEngine.cs` lines 653, 1059, 1065, 1118 | OPEN — P2 |

This ticket closes **DW-B24-LEADER-CASTNULL-01** and no other items.

---

*End of T1*
