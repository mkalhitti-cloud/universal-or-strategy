# Ticket 1 Completion: PTT-COPIER-B21-LANE-B T1

**Engineer**: ptt-engineer (Phase 4a)
**Block**: PTT-COPIER-B21, Lane B
**Ticket**: T1 — PopulateOrderMap_DedupGuard_B21_NameEqualityContract
**Defect**: DW-B19-02 (complementary lane coverage)
**Date**: 2026-07-07
**Result**: BUILD_PASS

---

## Summary of Changes

**One file modified** (test file only):

| File | Action |
|------|--------|
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` | Appended one new `[Fact]` test method before class closing brace |

**No production code changes.** `CopyEngine.cs` was NOT modified.

---

## Pre-Flight Verification

| Check | Result |
|-------|--------|
| `CopyEngine.cs` line 665 has name-equality predicate (`b.FollowerAccount?.Name == followerAccount?.Name`) | ✅ CONFIRMED |
| B20-LANE-A test `PopulateOrderMap_DedupGuard_UsesNameEquality` exists at lines 2037-2067 | ✅ CONFIRMED |
| `[Fact]` baseline count = 120 | ✅ CONFIRMED |
| Test name `PopulateOrderMap_DedupGuard_B21_NameEqualityContract` not already present (0 hits) | ✅ CONFIRMED |

---

## Insertion Point

New test inserted **before line 2095** (class closing brace) in `CopyEngineTests.cs`.

File before edit: 2096 lines (class `}` at line 2095, namespace `}` at line 2096).
File after edit: 2133 lines — new test block at lines 2095–2131.

---

## New Test Method

```csharp
// ===================================================================
// B21-LANE-B T1: Complementary dedup guard contract verification
// ===================================================================

[Fact]
public void PopulateOrderMap_DedupGuard_B21_NameEqualityContract()
{
    _engine.SetEnabled(false);
    // Use a unique signal name to avoid cross-test contamination with the singleton
    string signalName = "B21-DEDUP-" + DateTime.UtcNow.Ticks;
    // a1 and a2 have the same Name but are different object references --
    // name-equality dedup guard must prevent the second bag.Add from firing.
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
    // Dedup guard must have fired on name equality: second invoke must not add a second binding
    Assert.Equal(1, bag.Count);
}
```

---

## 7-Scan Results (Layer 2 — Engineer Report)

All 7 scans executed sequentially against the modified file.

| Scan | Pattern | Scope | Expected | Actual | Result |
|------|---------|-------|----------|--------|--------|
| SCAN-01 | `lock\s*\(` | Entire `CopyEngineTests.cs` | 0 hits | 0 hits | ✅ PASS |
| SCAN-02 | `[^\x00-\x7F]` | New test code (T1 block) | 0 hits in new code | 0 hits in new code (4 pre-existing hits in older B19/B20 blocks at lines 1953, 1956, 1985, 2065 — unmodified by T1) | ✅ PASS |
| SCAN-03 | `FontFamily` | Entire `CopyEngineTests.cs` | 0 hits | 0 hits | ✅ PASS |
| SCAN-04 | `"#[0-9A-Fa-f]{6}"` | Entire `CopyEngineTests.cs` | 0 hits | 0 hits | ✅ PASS |
| SCAN-05 | `\.CreateOrder` | Entire `CopyEngineTests.cs` | NOT APPLICABLE | 0 hits (no CreateOrder calls) | ✅ PASS |
| SCAN-06 | `DateTime\.Now[^U]` | Entire `CopyEngineTests.cs` | 0 hits | 0 hits | ✅ PASS |
| SCAN-07 | `async\s+void` | Entire `CopyEngineTests.cs` | 0 hits | 0 hits | ✅ PASS |

**SCAN-02 note**: The 4 pre-existing non-ASCII hits (right-arrow Unicode → at lines 1953, 1956, 1985, 2065) were introduced by prior blocks (B19/B20) and are outside the scope of T1. The T1 code block contains zero non-ASCII characters.

---

## [Fact] Count

| State | Count |
|-------|-------|
| Before T1 | 120 |
| After T1 | **121** |

Verified via:
```powershell
Select-String -Path "c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object
# Count: 121
```

---

## Production Code Status

`CopyEngine.cs` — **ZERO EDITS**. The file was read-only during this ticket.

Line 665 confirmed unchanged:
```csharp
if (!bag.Any(b => b.FollowerAccount?.Name == followerAccount?.Name))
```

---

## Jane Street Rule Compliance

| Rule | Status |
|------|--------|
| JS-021: No `lock()` | ✅ PASS — SCAN-01 = 0 |
| JS-033: No `async void` | ✅ PASS — SCAN-07 = 0 |
| JS-006: `DateTime.UtcNow` only | ✅ PASS — SCAN-06 = 0 |
| ASCII-only | ✅ PASS — new code SCAN-02 = 0 |
| CYC ≤ 8 | ✅ PASS — new test CYC = 1 (linear) |

---

## BUILD_PASS
