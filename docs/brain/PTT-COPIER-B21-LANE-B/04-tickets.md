# PTT-COPIER-B21-LANE-B Tickets

**Status**: TICKETS_COMPLETE
**Block**: PTT-COPIER-B21, Lane B
**Defect Closed**: DW-B19-02
**Source Plan**: `docs/brain/PTT-COPIER-B21-LANE-B/02-architecture-plan.md` (REVIEW_PASS)
**Author**: ptt-architect (Phase 3)
**Date**: 2026-07-07
**Ticket Count**: 1 (T1 only)

---

## T1 — PopulateOrderMap_DedupGuard_B21_NameEqualityContract

---

### Metadata

| Field | Value |
|-------|-------|
| Ticket | T1 |
| Priority | P2 |
| Spec Requirement ID | DW-B19-02 (complementary lane coverage) |
| Plan Section | §3, §4, §6, §7, §8, §10 |
| [Fact] baseline | 120 |
| [Fact] after T1 | 121 |

---

### File Modified

| File (Wave workspace) | Action |
|-----------------------|--------|
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` | Append one new `[Fact]` test method before the closing `}` of the test class |

### Files NOT Modified (engineer contract — do not touch)

| File | Status |
|------|--------|
| `CopyEngine.cs` | NO CHANGE — production fix for DW-B19-02 already applied by B20-LANE-A at line 665 |
| `AtrSizingEngine.cs` | NO CHANGE |
| `TradeCopierAddOn.cs` | NO CHANGE |
| `TradeCopierPanel.cs` | NO CHANGE |
| `TradeCopierWindow.cs` | NO CHANGE |

---

### Production Code Status

**NO PRODUCTION CODE CHANGE IS REQUIRED.**

`CopyEngine.cs` line 665 already reads:

```csharp
if (!bag.Any(b => b.FollowerAccount?.Name == followerAccount?.Name))
```

This name-equality predicate was applied by B20-LANE-A. B21-LANE-B **must not** re-apply
or modify this line. The sole deliverable of this ticket is the new test method.

---

### Method Signature

```csharp
[Fact]
public void PopulateOrderMap_DedupGuard_B21_NameEqualityContract()
```

| Attribute | Value |
|-----------|-------|
| xUnit attribute | `[Fact]` |
| Access | `public` |
| Return type | `void` |
| Parameters | none |
| CYC | 1 (linear — no branches) |

---

### Placement

Append **after** the existing B20-LANE-A test block (after line 2067 in `CopyEngineTests.cs`).

Precede the method with the following section-header comment:

```csharp
// ===================================================================
// B21-LANE-B T1: Complementary dedup guard contract verification
// ===================================================================
```

---

### Complete Method Body (engineer reference — copy verbatim)

```csharp
// ===================================================================
// B21-LANE-B T1: Complementary dedup guard contract verification
// ===================================================================

[Fact]
public void PopulateOrderMap_DedupGuard_B21_NameEqualityContract()
{
    _engine.SetEnabled(false);
    string signalName = "B21-DEDUP-" + DateTime.UtcNow.Ticks;
    var a1 = new Account { Name = "Sim101-B21" };
    var a2 = new Account { Name = "Sim101-B21" };
    var mi = typeof(CopyEngine).GetMethod(
        "PopulateOrderMap",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(mi);
    mi.Invoke(_engine, new object[] { signalName, a1 });
    mi.Invoke(_engine, new object[] { signalName, a2 });
    var mapField = typeof(CopyEngine).GetField(
        "_orderMap",
        BindingFlags.NonPublic | BindingFlags.Instance);
    Assert.NotNull(mapField);
    var map = mapField.GetValue(_engine)
        as System.Collections.Concurrent.ConcurrentDictionary<
            string,
            System.Collections.Concurrent.ConcurrentBag<FollowerBinding>>;
    Assert.NotNull(map);
    System.Collections.Concurrent.ConcurrentBag<FollowerBinding> bag;
    Assert.True(map.TryGetValue(signalName, out bag), "Signal key not found in _orderMap");
    Assert.Equal(1, bag.Count);
}
```

---

### xUnit [Fact] — What This Test Asserts

| Step | Assertion | Why |
|------|-----------|-----|
| `Assert.NotNull(mi)` | `PopulateOrderMap` exists as non-public instance method | Reflection guard — fails fast if method renamed or removed |
| `Assert.NotNull(mapField)` | `_orderMap` field exists as non-public instance field | Reflection guard — fails fast if field renamed or removed |
| `Assert.NotNull(map)` | Field value is castable to `ConcurrentDictionary<string, ConcurrentBag<FollowerBinding>>` | Type-safety guard |
| `Assert.True(map.TryGetValue(...))` | Signal key was registered after first invocation | Proves `PopulateOrderMap` wrote to `_orderMap` |
| `Assert.Equal(1, bag.Count)` | **Core assertion**: two invocations with same-Name accounts yield exactly 1 bag entry | Proves name-equality dedup guard fired and suppressed the second `bag.Add` |

Signal key uniqueness: `"B21-DEDUP-" + DateTime.UtcNow.Ticks` — distinct prefix from
B20-LANE-A test (`"B20-DEDUP-"`), preventing any cross-test contamination in the shared
`_orderMap`.

---

### Jane Street Rule Constraints

| Rule | Constraint | Compliance in T1 |
|------|-----------|------------------|
| JS-021 | No `lock()` anywhere in test code | Compliant — no lock() used |
| JS-002 | No `return null` for missing values | Compliant — void method, no return statement |
| JS-033 | No `async void` (non-event-handler) | Compliant — synchronous `void` method |
| JS-006 (project) | `DateTime.UtcNow` only, never `DateTime.Now` | Compliant — uses `DateTime.UtcNow.Ticks` |
| CYC <= 8 | All modified methods <= 8 branches | Compliant — new test CYC = 1 (linear) |
| ASCII-only | No Unicode in identifiers or string literals | Compliant — all ASCII |

---

### NT8 Compiler Rule Constraints

| Rule | Constraint | Applicability to T1 |
|------|-----------|----------------------|
| NT8-003 | No `volatile double` | Not applicable — test defines no fields |
| NT8-004 | No `ImmutableDictionary` | Not applicable — test uses `ConcurrentDictionary` (permitted) |
| NT8-006 | `ConcurrentBag.Any()` requires `using System.Linq` | Test method does not call `.Any()` directly. `PopulateOrderMap` calls it internally. Confirm `using System.Linq` is already present at the top of `CopyEngineTests.cs` (required by B20-LANE-A); no new `using` directive needed. |

---

### 7-Scan Checklist (Engineer Contract)

All 7 scans MUST be executed against the new code block before closing this ticket.
Expected outcome is listed for each scan. A non-zero result in scans 1–4, 6–7 is a
**blocking defect** — do not close the ticket until resolved.

| Scan | Pattern | Scope | Expected Outcome |
|------|---------|-------|-----------------|
| SCAN-01 | `lock\s*\(` | New test code added by T1 | **0 matches** — test uses no `lock()` |
| SCAN-02 | `[^\x00-\x7F]` (non-ASCII) | New test code added by T1 | **0 matches** — all identifiers and string literals are ASCII-only |
| SCAN-03 | `FontFamily` | Entire `CopyEngineTests.cs` | **0 matches** — not applicable to test file |
| SCAN-04 | `"#[0-9A-Fa-f]{3,6}"` (hex color strings) | New test code added by T1 | **0 matches** — no UI code in test |
| SCAN-05 | `CreateOrder` without `"PTT-"` prefix | New test code added by T1 | **NOT APPLICABLE** — test contains no `CreateOrder` call |
| SCAN-06 | `DateTime\.Now[^U]` | New test code added by T1 | **0 matches** — only `DateTime.UtcNow.Ticks` is used |
| SCAN-07 | `async\s+void\s+\w+\(` | New test code added by T1 | **0 matches** — method is synchronous `void` |

**PowerShell scan commands (engineer may use these verbatim):**

```powershell
# SCAN-01 lock()
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "lock\s*\("

# SCAN-02 non-ASCII
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "[^\x00-\x7F]"

# SCAN-03 FontFamily
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "FontFamily"

# SCAN-04 hex color strings
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern '"#[0-9A-Fa-f]{3,6}"'

# SCAN-05 not applicable (no CreateOrder)

# SCAN-06 DateTime.Now (non-UTC)
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "DateTime\.Now[^U]"

# SCAN-07 async void
Select-String -Path "src\PropTraderTools\CopyEngineTests.cs" -Pattern "async\s+void\s+\w+\("
```

---

### Success Criteria

The ticket is closed when ALL of the following conditions are met:

- [ ] `CopyEngineTests.cs` compiles without errors or warnings introduced by T1
- [ ] `Select-String -Path CopyEngineTests.cs -Pattern "\[Fact\]" | Measure-Object` returns **121**
- [ ] Test method name `PopulateOrderMap_DedupGuard_B21_NameEqualityContract` is unique (no duplicate of B20 test `PopulateOrderMap_DedupGuard_UsesNameEquality`)
- [ ] `CopyEngine.cs` has **zero edits** (diff shows no changes to that file)
- [ ] All 7 scans return expected outcomes as defined in the checklist above
- [ ] New test passes when run: `dotnet test --filter "FullyQualifiedName~PopulateOrderMap_DedupGuard_B21_NameEqualityContract"`

---

### CYC Note

`PopulateOrderMap` (the method under test) is **not modified** by T1. Its CYC remains 2.
The new test method has CYC = 1. No complexity audit action required.
