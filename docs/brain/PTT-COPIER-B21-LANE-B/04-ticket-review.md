# Ticket Review: PTT-COPIER-B21-LANE-B

**Reviewer**: ptt-ticket-reviewer (Phase 3.5)
**Block**: PTT-COPIER-B21, Lane B
**Defect Closed**: DW-B19-02
**Source**: `docs/brain/PTT-COPIER-B21-LANE-B/04-tickets.md`
**Plan**: `docs/brain/PTT-COPIER-B21-LANE-B/02-architecture-plan.md` (REVIEW_PASS)
**Date**: 2026-07-07
**Ticket Count**: 1 (T1 only)

---

## T1 — PopulateOrderMap_DedupGuard_B21_NameEqualityContract

### Traceability
**PASS**

T1 maps to `DW-B19-02 (complementary lane coverage)` in the Metadata table. Plan sections
§3, §4, §6, §7, §8, §10 are all cited. Architecture plan §1 establishes DW-B19-02 as the
governing defect; §12 confirms T1 is the sole ticket implementing it. No phantom work
(every item in the ticket maps to a plan section). No missing work (the plan specifies one
ticket; the ticket file contains exactly one ticket).

---

### JS Pre-Check
**PASS**

| Rule | Check | Result |
|------|-------|--------|
| JS-021 | No `lock()` in proposed test code | PASS — `lock(` does not appear in the method body; SCAN-01 confirms expected 0 matches |
| JS-002 | No `return null` for missing values | PASS — method is `void` with no return statements |
| JS-033 | No `async void` | PASS — method signature is `public void`, no `async` keyword; SCAN-07 confirms |
| JS-006 | `DateTime.UtcNow` only, never `DateTime.Now` | PASS — signal key uses `DateTime.UtcNow.Ticks`; SCAN-06 confirms |
| CYC ≤ 8 | All modified/new methods | PASS — new test CYC=1 (linear); `PopulateOrderMap` CYC unchanged at 2 |
| ASCII-only | No Unicode in identifiers or literals | PASS — all identifiers and string literals are ASCII; SCAN-02 confirms |

---

### CYC Pre-Check
**PASS**

- `PopulateOrderMap` is **not modified** by T1. CYC remains 2. No audit action required.
- New test method `PopulateOrderMap_DedupGuard_B21_NameEqualityContract` has CYC = 1 (linear sequence, zero branches).
- Both values are well below the JS CYC ≤ 8 threshold.

---

### NT8 Check
**PASS**

| Rule | Check | Result |
|------|-------|--------|
| NT8-003 | No `volatile double` | PASS — test defines no fields of any kind |
| NT8-004 | No `ImmutableDictionary` | PASS — test uses `ConcurrentDictionary` (safe pattern) |
| NT8-006 | `ConcurrentBag.Any()` requires `using System.Linq` | PASS — test uses `bag.Count`, not `.Any()`. No new `using` directive needed. Ticket correctly notes that `PopulateOrderMap` calls `.Any()` internally and that `using System.Linq` is already present from B20-LANE-A. |
| NT8-018 | No `lock()` | PASS — same as JS-021 above |
| NT8-019 | No `async void` | PASS — same as JS-033 above |
| NT8-013 | No `DateTime.Now` in test code | PASS — uses `DateTime.UtcNow.Ticks` only |

---

### Test Coverage
**PASS**

The single public method introduced by T1 is:

```
public void PopulateOrderMap_DedupGuard_B21_NameEqualityContract()
```

This method **is itself** a `[Fact]` test. The ticket explicitly declares it with the
`[Fact]` attribute and documents five assertions:

| Assertion | What it verifies |
|-----------|-----------------|
| `Assert.NotNull(mi)` | `PopulateOrderMap` exists as non-public instance method |
| `Assert.NotNull(mapField)` | `_orderMap` exists as non-public instance field |
| `Assert.NotNull(map)` | Field casts successfully to `ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>>` |
| `Assert.True(map.TryGetValue(...))` | Signal key was registered after first invocation |
| `Assert.Equal(1, bag.Count)` | Name-equality dedup guard suppressed the second `bag.Add` |

No public or internal production method is introduced by T1 (production code is
unchanged). Test coverage requirement is fully satisfied.

---

### Scan Checklist
**PASS**

All 7 scans are present in the ticket under "7-Scan Checklist (Engineer Contract)" with
Pattern, Scope, and Expected Outcome columns, plus verbatim PowerShell scan commands.
Defense-in-depth contract is intact (Layer 1 of 3).

| Scan | Pattern | Scope | Expected | Present |
|------|---------|-------|----------|---------|
| SCAN-01 | `lock\s*\(` | New test code | 0 matches | ✓ |
| SCAN-02 | `[^\x00-\x7F]` | New test code | 0 matches | ✓ |
| SCAN-03 | `FontFamily` | Entire `CopyEngineTests.cs` | 0 matches | ✓ |
| SCAN-04 | `"#[0-9A-Fa-f]{3,6}"` | New test code | 0 matches | ✓ |
| SCAN-05 | `CreateOrder` without `"PTT-"` | New test code | NOT APPLICABLE | ✓ |
| SCAN-06 | `DateTime\.Now[^U]` | New test code | 0 matches | ✓ |
| SCAN-07 | `async\s+void\s+\w+\(` | New test code | 0 matches | ✓ |

SCAN-05 "NOT APPLICABLE" notation is acceptable: the test contains no `CreateOrder` call.
The engineer is still required to run SCAN-05 (or confirm it does not apply) before closing.

---

### File Routing
**PASS**

The single modified file is:

```
c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs
```

This is the correct Wave workspace path (`c:\WSGTA\universal-or-strategy\src\PropTraderTools\`).
No Director workspace path (`c:\WSGTA\universal-or-strategy-director\`) appears for any `.cs`
file. `CopyEngine.cs` is explicitly listed as NO CHANGE.

---

### 15-Point Specific Review (per orchestrator mandate)

| # | Check | Result | Evidence |
|---|-------|--------|----------|
| 1 | Traceability to DW-B19-02 | PASS | Metadata table; §3, §4, §6, §7, §8, §10 cited |
| 2 | Test name is `PopulateOrderMap_DedupGuard_B21_NameEqualityContract` and distinct from B20 | PASS | Method signature section; Success criteria bullet; name differs from `PopulateOrderMap_DedupGuard_UsesNameEquality` — no CS0111 risk |
| 3 | Signal key uses `"B21-DEDUP-"` prefix (not `"B20-DEDUP-"`) | PASS | Method body: `"B21-DEDUP-" + DateTime.UtcNow.Ticks`; signal key uniqueness note |
| 4 | T1 explicitly states NO changes to `CopyEngine.cs` | PASS | "Files NOT Modified" table; "Production Code Status" section; Success criteria bullet |
| 5 | T1 explicitly states only `CopyEngineTests.cs` is modified | PASS | "File Modified" table; "Files NOT Modified" table |
| 6 | All 7 scans present with expected outcomes | PASS | 7-Scan Checklist table; PowerShell commands |
| 7 | `PopulateOrderMap` CYC=2 confirmed unchanged | PASS | "CYC Note" section; architecture plan §7 |
| 8 | No `lock()` in proposed test code (JS-021) | PASS | Method body inspection; SCAN-01 |
| 9 | No `async void` in proposed test code (JS-033) | PASS | Method signature; SCAN-07 |
| 10 | No `return null` in proposed test code (JS-002) | PASS | `void` method; no return statements |
| 11 | Test uses `bag.Count` not `bag.Any()` — NT8-006 safe pattern | PASS | `Assert.Equal(1, bag.Count)` in method body |
| 12 | `[Fact]` count: baseline=120, target=121 | PASS | Metadata table; Success criteria bullet; architecture plan §1 |
| 13 | Lane isolation: 5 files explicitly listed as NOT modified | PASS | "Files NOT Modified" table: `CopyEngine.cs`, `AtrSizingEngine.cs`, `TradeCopierAddOn.cs`, `TradeCopierPanel.cs`, `TradeCopierWindow.cs` |
| 14 | Reflection uses `BindingFlags.NonPublic \| BindingFlags.Instance` for both method AND field | PASS | Method body: `GetMethod(...)` and `GetField(...)` both use these exact flags |
| 15 | Final assertion is `Assert.Equal(1, bag.Count)` | PASS | Last line of method body |

---

### VERDICT: TICKET_REVIEW_PASS

---

## Overall: TICKET_REVIEW_PASS

All 15 specific checks PASS. All 6 standard review dimensions PASS.

**This ticket is approved for engineer execution.**

The orchestrator may spawn the engineer subtask for T1.

### Engineer Summary

- **One file to edit**: `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs`
- **Action**: Append one `[Fact]` method (the method body is provided verbatim in the ticket)
- **No production code changes**
- **Run all 7 scans before closing** (SCAN-05 confirm not applicable)
- **Verify `[Fact]` count reaches 121** via `Select-String | Measure-Object`
- **Run**: `dotnet test --filter "FullyQualifiedName~PopulateOrderMap_DedupGuard_B21_NameEqualityContract"`
