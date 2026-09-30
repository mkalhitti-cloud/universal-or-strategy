# PTT-COPIER-B21-LANE-B Ticket T1 — Verification Report

**Verifier**: ptt-verifier (Phase 4b)
**Block**: PTT-COPIER-B21, Lane B
**Ticket**: T1 — PopulateOrderMap_DedupGuard_B21_NameEqualityContract
**Date**: 2026-07-07
**Verdict**: **VERIFY_PASS**

---

## Files Verified (Read-Only)

| File | Role |
|------|------|
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` | Modified (new test appended) |
| `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs` | Unchanged (production code) |
| `docs/brain/PTT-COPIER-B21-LANE-B/04-tickets.md` | Ticket spec |
| `docs/brain/PTT-COPIER-B21-LANE-B/ticket-1-completion.md` | Engineer Layer 2 report |

---

## Layer 3 Scan Results (Independent — All Run by Verifier)

### Check A — New Test Presence
```powershell
Select-String -Path "...CopyEngineTests.cs" -Pattern "PopulateOrderMap_DedupGuard_B21_NameEqualityContract"
```
**Result**: 1 match at line 2101. ✅ PASS

---

### Check B — [Fact] Count
```powershell
Select-String -Path "...CopyEngineTests.cs" -Pattern "\[Fact\]" | Measure-Object | Select-Object Count
```
**Result**: 121. ✅ PASS (required: 121)

---

### Check C — B21-DEDUP- Signal Key
```powershell
Select-String -Path "...CopyEngineTests.cs" -Pattern "B21-DEDUP-"
```
**Result**: 1 match at line 2105. ✅ PASS (exactly 1 match in new test only)

---

### Check D — B20 Test Still Intact
```powershell
Select-String -Path "...CopyEngineTests.cs" -Pattern "PopulateOrderMap_DedupGuard_UsesNameEquality"
```
**Result**: 1 match at line 2038. ✅ PASS (undisturbed)

---

### Check E — Production Code UNCHANGED (CopyEngine.cs Line 665)
```powershell
Select-String -Path "...CopyEngine.cs" -Pattern "FollowerAccount\?\.Name == followerAccount\?\.Name"
```
**Result**: 1 match at line 665 — `if (!bag.Any(b => b.FollowerAccount?.Name == followerAccount?.Name))`. ✅ PASS

Name-equality predicate confirmed unchanged. Forbidden reference-equality pattern NOT present.

---

### Check F — SCAN-01: lock()
```powershell
Select-String -Path "...CopyEngineTests.cs" -Pattern "lock\s*\(" | Measure-Object | Select-Object Count
```
**Result**: 0 hits. ✅ PASS

---

### Check G — SCAN-02: Non-ASCII Characters
```powershell
Get-Content "...CopyEngineTests.cs" | Select-String -Pattern '[^\x00-\x7F]' | Select-Object LineNumber, Line
```
**Result**: 4 pre-existing hits — lines 1953, 1956, 1985, 2065 (B19/B20 blocks, not T1 code).
New T1 block (lines 2094–2131): **0 non-ASCII characters**. ✅ PASS

---

### Check H — SCAN-03: FontFamily
```powershell
Select-String -Path "...CopyEngineTests.cs" -Pattern "FontFamily" | Measure-Object | Select-Object Count
```
**Result**: 0 hits. ✅ PASS

---

### Check I — SCAN-04: Hex Colors
```powershell
Select-String -Path "...CopyEngineTests.cs" -Pattern '"#[0-9A-Fa-f]{6}"' | Measure-Object | Select-Object Count
```
**Result**: 0 hits. ✅ PASS

---

### Check J — SCAN-06: DateTime.Now (non-UTC)
```powershell
Select-String -Path "...CopyEngineTests.cs" -Pattern "DateTime\.Now[^U]" | Measure-Object | Select-Object Count
```
**Result**: 0 hits. ✅ PASS (new test uses `DateTime.UtcNow.Ticks` — compliant)

---

### Check K — SCAN-07: async void
```powershell
Select-String -Path "...CopyEngineTests.cs" -Pattern "async\s+void" | Measure-Object | Select-Object Count
```
**Result**: 0 hits. ✅ PASS

---

### Check L — Test Body Correctness (Verbatim Inspection)

New test block at lines 2094–2131 in `CopyEngineTests.cs`:

| Sub-check | Expected | Actual | Result |
|-----------|----------|--------|--------|
| Signal key prefix | `"B21-DEDUP-"` | `string signalName = "B21-DEDUP-" + DateTime.UtcNow.Ticks;` | ✅ PASS |
| Account names | `Name = "Sim101-B21"` for both a1 and a2 | Confirmed — `var a1 = new Account { Name = "Sim101-B21" };` / `var a2 = new Account { Name = "Sim101-B21" };` | ✅ PASS |
| BindingFlags for method | `NonPublic \| Instance` | `BindingFlags.NonPublic \| BindingFlags.Instance` on `GetMethod` | ✅ PASS |
| BindingFlags for field | `NonPublic \| Instance` | `BindingFlags.NonPublic \| BindingFlags.Instance` on `GetField` | ✅ PASS |
| Double invocation | `mi.Invoke(a1)` then `mi.Invoke(a2)` | Both invocations present with correct args | ✅ PASS |
| Final assertion | `Assert.Equal(1, bag.Count)` | `Assert.Equal(1, bag.Count);` — exact match | ✅ PASS |

Check L: ✅ PASS — all 6 body-correctness sub-items confirmed.

---

### Check M — Layer 2 vs Layer 3 Cross-Check

| Claim | Engineer Layer 2 | Verifier Layer 3 | Discrepancy? |
|-------|-----------------|-----------------|-------------|
| SCAN-01 lock() | 0 hits | 0 hits | None |
| SCAN-02 non-ASCII (new code) | 0 in T1; 4 pre-existing at 1953,1956,1985,2065 | 0 in T1; 4 pre-existing at exactly those lines | None |
| SCAN-03 FontFamily | 0 hits | 0 hits | None |
| SCAN-04 hex colors | 0 hits | 0 hits | None |
| SCAN-05 CreateOrder | N/A | N/A | None |
| SCAN-06 DateTime.Now | 0 hits | 0 hits | None |
| SCAN-07 async void | 0 hits | 0 hits | None |
| [Fact] count | 121 | 121 | None |
| CopyEngine.cs line 665 | Name-equality predicate | Name-equality predicate | None |

Check M: ✅ PASS — **Zero discrepancies** between engineer Layer 2 and verifier Layer 3.

---

### Check N — CYC: PopulateOrderMap (CopyEngine.cs lines 660–667)

Verbatim source (lines 660–667):
```csharp
private void PopulateOrderMap(string fromEntrySignalName, Account followerAccount)
{
    var bag = _orderMap.GetOrAdd(
        fromEntrySignalName,
        _ => new ConcurrentBag<FollowerBinding>());
    if (!bag.Any(b => b.FollowerAccount?.Name == followerAccount?.Name))   // (1) branch
        bag.Add(new FollowerBinding(followerAccount, fromEntrySignalName));
}
```

Decision points: 1 `if` + lambda predicate = **CYC = 2**. Under the Jane Street CYC ≤ 8 threshold.
Predicate uses `.Name` equality (not reference equality). Method is **UNCHANGED**.

Check N: ✅ PASS

---

## DNA Rule Summary

| Rule | Check | Result |
|------|-------|--------|
| JS-021: No `lock()` | SCAN-01 → 0 hits | ✅ PASS |
| JS-033: No `async void` | SCAN-07 → 0 hits | ✅ PASS |
| JS-006: `DateTime.UtcNow` only | SCAN-06 → 0 hits; `UtcNow.Ticks` used | ✅ PASS |
| ASCII-only (new code) | SCAN-02 → 0 in T1 block | ✅ PASS |
| CYC ≤ 8 (new test) | New test CYC = 1 (linear) | ✅ PASS |
| CYC ≤ 8 (PopulateOrderMap) | CYC = 2, unchanged | ✅ PASS |
| NT8: No FontFamily | SCAN-03 → 0 hits | ✅ PASS |
| NT8: No hex color strings | SCAN-04 → 0 hits | ✅ PASS |
| Production code UNCHANGED | CopyEngine.cs unmodified | ✅ PASS |

---

## Architecture Compliance

| Requirement | Status |
|-------------|--------|
| Only `CopyEngineTests.cs` modified | ✅ Confirmed — only test file changed |
| `CopyEngine.cs` NOT modified | ✅ Confirmed — line 665 is unchanged |
| Test uses `[Fact]` attribute (xUnit) | ✅ Confirmed |
| Method name unique (not duplicate of B20) | ✅ Confirmed — different name and different `"B21-DEDUP-"` signal prefix |
| `BindingFlags.NonPublic \| BindingFlags.Instance` for both reflection lookups | ✅ Confirmed |
| Singleton pattern respected (`_engine` field reused) | ✅ Confirmed |

---

## Verdict Summary

| Check | Result |
|-------|--------|
| A — Test presence | ✅ PASS |
| B — [Fact] count = 121 | ✅ PASS |
| C — B21-DEDUP- signal key | ✅ PASS |
| D — B20 test undisturbed | ✅ PASS |
| E — Production code unchanged | ✅ PASS |
| F — SCAN-01 lock() | ✅ PASS |
| G — SCAN-02 non-ASCII | ✅ PASS |
| H — SCAN-03 FontFamily | ✅ PASS |
| I — SCAN-04 hex colors | ✅ PASS |
| J — SCAN-06 DateTime.Now | ✅ PASS |
| K — SCAN-07 async void | ✅ PASS |
| L — Test body correctness | ✅ PASS |
| M — Layer 2 vs Layer 3 | ✅ PASS — zero discrepancies |
| N — CYC PopulateOrderMap | ✅ PASS — CYC=2, unchanged |

**ALL 14 CHECKS: PASS**

---

## VERIFY_PASS
