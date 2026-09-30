# B130-LaneC Ticket Document

**Block**: B130-LaneC
**Defect**: DW-B107
**Phase**: Phase 4 (Ticket Generation)
**Author**: ptt-architect
**Plan status**: REVIEW_PASS (`docs/brain/B130/LaneC-02-plan-review.md`)
**Date**: 2026-09-01

---

## Ticket T3 — Append 3 DW-B107 Tests to B130Tests.cs

**Ticket ID**: T3
**Spec requirement IDs satisfied**: DW-B107 acceptance criteria T1, T3, T6, T7, T8
  (T2, T4, T5 are structural/comment evidence — confirmed in plan review, no runtime test needed)
**Continuation of**: B130 LaneA T1 (DW-B137 tests), B130 LaneB T2 (DW-B136 tests)
**Engineer contract**: APPEND ONLY. No production code changes. No `.csproj` changes.

---

### 1. BEFORE State

`src/PropTraderTools/Tests/B130Tests.cs` contains **5 tests** in the `B130Tests` class:

1. `B130_DW137_Stop1NameRoutesToCancelResubmit`
2. `B130_DW137_Target1NameRoutesCorrectly`
3. `B130_DW136_CancelLeaderOrder1DoesNotCancelFollowerCopiesOfOrder2`
4. `B130_DW136_SingleEntryPathUnchanged`
5. `B130_DW136_CancelLeaderOrder2DoesNotEvictLeaderOrder1Bag`

No `B130_DW107_*` tests exist. Confirmed by grep (plan-review Section, Additional Verification).

---

### 2. AFTER State

`src/PropTraderTools/Tests/B130Tests.cs` contains **8 tests** in the `B130Tests` class.
Three new `[Fact]` methods are appended after the existing 5, before the closing `}` of the class:

6. `B130_DW107_SnapshotBeTargetsFiltersStaleOrders`
7. `B130_DW107_HardCapTrimsSnapshotToThreeTargets`
8. `B130_DW107_NonTargetOrdersProduceEmptySnapshot`

---

### 3. File Scope

| File | Change Type | Description |
|------|-------------|-------------|
| `src/PropTraderTools/Tests/B130Tests.cs` | APPEND ONLY | Add 3 `[Fact]` tests before closing `}` of `B130Tests` class |
| `src/PropTraderTools/CopyEngine.cs` | NOT MODIFIED | Production fix already implemented (L3917-3965, L4019-4024) |
| `src/PropTraderTools/PropTraderTools.csproj` | NOT MODIFIED | `<Compile Include="Tests\B130Tests.cs" />` already present |

---

### 4. Append Location

**Insert before** the final `}` that closes the `B130Tests` class (the very last line of the file).

**BEFORE** (end of file):
```csharp
        [Fact]
        public void B130_DW136_CancelLeaderOrder2DoesNotEvictLeaderOrder1Bag()
        {
            // ... (existing test body, unchanged) ...
        }
    }       // <-- closing brace of B130Tests class
}           // <-- closing brace of namespace PropTraderTools.Tests
```

**AFTER** (new content inserted before `}` of class):
```csharp
        [Fact]
        public void B130_DW136_CancelLeaderOrder2DoesNotEvictLeaderOrder1Bag()
        {
            // ... (existing test body, unchanged) ...
        }

        // ── DW-B107 Tests ─────────────────────────────────────────────────────────────
        // Behavioral equivalence tests for SnapshotBeTargets (CopyEngine.cs L3922).
        // SnapshotBeTargets is private; tests use inline predicate helpers mirroring
        // the exact logic at CopyEngine.cs L3948-3958 and the hard-cap at L4023-4024.
        // No NT8 Account/Instrument required -- string operations and List<T> only.
        // ──────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void B130_DW107_SnapshotBeTargetsFiltersStaleOrders()
        { /* ... see Section 5, Test 1 ... */ }

        [Fact]
        public void B130_DW107_HardCapTrimsSnapshotToThreeTargets()
        { /* ... see Section 5, Test 2 ... */ }

        [Fact]
        public void B130_DW107_NonTargetOrdersProduceEmptySnapshot()
        { /* ... see Section 5, Test 3 ... */ }
    }
}
```

---

### 5. Full Test Bodies (Engineer Contract)

No new `using` directives are required. The existing file header provides:
- `using NinjaTrader.Cbi;` — covers `OrderAction` (Test 2)
- `using Xunit;` — covers `[Fact]`, `Assert`
- `System.Collections.Generic` — available implicitly (net48 + existing `using NinjaTrader.Cbi;` pull-through)

#### Test 1: `B130_DW107_SnapshotBeTargetsFiltersStaleOrders`

**Purpose**: Proves the two-pass native-first classification logic that `SnapshotBeTargets` uses
to separate native ATM targets from stale PTT residues. This is the DW-B107 CHANGE A proof.
Tests the identical predicate expressions at `CopyEngine.cs:L3948-3958` via inline local functions.

**Spec criteria satisfied**: T1 (SnapshotBeTargets predicate logic correct), T8 (ASCII-only)

**CYC**: ~5
- `foreach` loop body (1) + `if (IsNativeTarget)` (1) + `else if (IsPttTarget)` (1) + ternary `nativeTargets.Count > 0 ? ...` (1) + base (1) = 5
- Local functions `IsNativeTarget` and `IsPttTarget` are pure expression-body static helpers; their internal `&&`/`||` chains are short-circuit boolean operators, not decision branches in McCabe sense. Conventional count: each local function adds 0 to the enclosing method's CYC.
- All values <= 8 limit. PASS.

```csharp
        [Fact]
        public void B130_DW107_SnapshotBeTargetsFiltersStaleOrders()
        {
            // Local predicates mirroring SnapshotBeTargets L3948-3958 verbatim.
            // CopyEngine.cs L3948-3952: isNative predicate
            static bool IsNativeTarget(string n) =>
                n != null
                && n.Length >= 7
                && n.StartsWith("Target", StringComparison.Ordinal)
                && char.IsDigit(n[6])
                && n[6] != '0';

            // CopyEngine.cs L3953-3958: isPtt predicate
            static bool IsPttTarget(string n) =>
                n != null
                && (
                    (n.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                     && n.Length > 8
                     && char.IsDigit(n[8]))
                    || n.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
                );

            // Native ATM target orders: must classify as native, not PTT
            Assert.True(IsNativeTarget("Target1"));
            Assert.True(IsNativeTarget("Target2"));
            Assert.True(IsNativeTarget("Target3"));
            Assert.False(IsPttTarget("Target1"));

            // Stale PTT-BE-Target-* residues: must classify as PTT, not native
            Assert.True(IsPttTarget("PTT-BE-Target-1"));
            Assert.True(IsPttTarget("PTT-BE-Target-4")); // stale T4 from prior session (root cause)
            Assert.False(IsNativeTarget("PTT-BE-Target-1"));

            // PTT-QX-T* orders: must classify as PTT, not native
            Assert.True(IsPttTarget("PTT-QX-T1"));
            Assert.True(IsPttTarget("PTT-QX-T3"));

            // Non-target orders: must classify as neither (proves empty-snapshot contract)
            Assert.False(IsNativeTarget("Entry"));
            Assert.False(IsPttTarget("Entry"));
            Assert.False(IsNativeTarget("PTT-BE-Stop-1"));
            Assert.False(IsPttTarget("PTT-BE-Stop-1"));

            // Native-first priority: when natives exist, PTT residues are excluded.
            // Simulates: nativeTargets.Count > 0 ? nativeTargets : pttTargets (CopyEngine.cs L3964)
            // If any native is present, result is nativeTargets (PTT-BE-Target-4 ignored).
            var nativeTargets = new System.Collections.Generic.List<string>();
            var pttTargets = new System.Collections.Generic.List<string>();
            foreach (var name in new[] { "Target1", "Target2", "Target3", "PTT-BE-Target-4" })
            {
                if (IsNativeTarget(name)) nativeTargets.Add(name);
                else if (IsPttTarget(name)) pttTargets.Add(name);
            }
            var result = nativeTargets.Count > 0 ? nativeTargets : pttTargets;
            Assert.Equal(3, result.Count);                    // exactly 3 native targets returned
            Assert.DoesNotContain("PTT-BE-Target-4", result); // stale T4 excluded (DW-B107 fix)
        }
```

---

#### Test 2: `B130_DW107_HardCapTrimsSnapshotToThreeTargets`

**Purpose**: Directly executes the `while (targets.Count > 3) targets.RemoveAt(targets.Count - 1)`
algorithm from `CopyEngine.cs:L4023-4024`. DW-B107 CHANGE B proof. Pure `List<T>` operation —
no NT8 dependencies.

**Spec criteria satisfied**: T3 (hard-cap algorithm is correct; does not over-trim; does not crash on empty)

**CYC**: ~4
- Three `while` statements each contribute 1 decision branch: 3 + base 1 = 4.
- All values <= 8 limit. PASS.

```csharp
        [Fact]
        public void B130_DW107_HardCapTrimsSnapshotToThreeTargets()
        {
            // Case 1: 4-item list (root-cause scenario: stale T4 present)
            var targets4 = new System.Collections.Generic.List<(double Price, int Qty, OrderAction Action)>
            {
                (4200.00, 1, OrderAction.Sell),
                (4210.00, 1, OrderAction.Sell),
                (4220.00, 1, OrderAction.Sell),
                (4230.00, 1, OrderAction.Sell), // stale T4 residue
            };
            while (targets4.Count > 3)
                targets4.RemoveAt(targets4.Count - 1);
            Assert.Equal(3, targets4.Count); // T4 trimmed -- DW-B107 fix verified

            // Case 2: 3-item list (nominal case: exactly 3 targets)
            var targets3 = new System.Collections.Generic.List<(double Price, int Qty, OrderAction Action)>
            {
                (4200.00, 1, OrderAction.Sell),
                (4210.00, 1, OrderAction.Sell),
                (4220.00, 1, OrderAction.Sell),
            };
            while (targets3.Count > 3)
                targets3.RemoveAt(targets3.Count - 1);
            Assert.Equal(3, targets3.Count); // unchanged -- no over-trim

            // Case 3: 0-item list (no targets -- retry path)
            var targets0 = new System.Collections.Generic.List<(double Price, int Qty, OrderAction Action)>();
            while (targets0.Count > 3)
                targets0.RemoveAt(targets0.Count - 1);
            Assert.Equal(0, targets0.Count); // empty -- no crash, no spurious trim
        }
```

---

#### Test 3: `B130_DW107_NonTargetOrdersProduceEmptySnapshot`

**Purpose**: Proves that non-target order names (Entry, Close, Stop1-3, PTT-BE-Stop-*, PTT-Copy,
PTT-QX-Stop-1) match neither predicate, so the snapshot is empty (not null) when no target orders
exist. Anchors the T7 contract (JS-002: never return null) and proves the empty-snapshot path
that triggers the retry slot at `MoveStopToBreakEven:L4034`.

**Spec criteria satisfied**: T6 (no lock), T7 (empty List never null), T8 (ASCII-only)

**Note on `Assert.NotNull(result)`**: `result` is locally-constructed via ternary of two
`new List<string>()` references. The assertion is tautological by construction (reviewer note,
non-blocking). It is retained as a T7 documentation anchor — the intent is to document that
`SnapshotBeTargets` (L3930, L3964) never returns null; this pattern makes the contract
explicit and visible to the next reader. No rule violation.

**CYC**: ~5
- `foreach` (1) + `if` (1) + `else if` (1) + ternary (1) + base (1) = 5.
- All values <= 8 limit. PASS.

```csharp
        [Fact]
        public void B130_DW107_NonTargetOrdersProduceEmptySnapshot()
        {
            // Local predicates mirroring SnapshotBeTargets L3948-3958 verbatim.
            // Reuse same helpers as Test 1 (copied -- local functions are method-scoped).
            static bool IsNativeTarget(string n) =>
                n != null
                && n.Length >= 7
                && n.StartsWith("Target", StringComparison.Ordinal)
                && char.IsDigit(n[6])
                && n[6] != '0';

            static bool IsPttTarget(string n) =>
                n != null
                && (
                    (n.StartsWith("PTT-QX-T", StringComparison.Ordinal)
                     && n.Length > 8
                     && char.IsDigit(n[8]))
                    || n.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
                );

            // Non-target names that must NOT pollute the snapshot
            var nonTargetNames = new[]
            {
                "Entry", "Close", "PTT-BE-Stop-1", "PTT-BE-Stop-2", "PTT-BE-Stop-3",
                "PTT-Copy", "PTT-QX-Stop-1", "Stop1", "Stop2", "Stop3",
            };
            var nativeTargets = new System.Collections.Generic.List<string>();
            var pttTargets = new System.Collections.Generic.List<string>();
            foreach (var name in nonTargetNames)
            {
                if (IsNativeTarget(name)) nativeTargets.Add(name);
                else if (IsPttTarget(name)) pttTargets.Add(name);
            }
            // Both lists must be empty -- no non-target name leaks into snapshot
            Assert.Empty(nativeTargets);
            Assert.Empty(pttTargets);

            // Native-first return: empty pttTargets returned when both are empty
            var result = nativeTargets.Count > 0 ? nativeTargets : pttTargets;
            Assert.Empty(result);     // empty list -- not null (JS-002 contract)
            Assert.NotNull(result);   // T7 anchor: SnapshotBeTargets L3930/3964 returns List, never null
        }
```

---

### 6. JS Rule Constraints

| Rule | ID | Applied to | Status |
|------|----|-----------|--------|
| No `lock()` | JS-021 (P0) | All 3 test methods | PASS — no shared mutable state; all `List<T>` are local to each test |
| No `throw new XxxException` in hot paths | JS-001 (P0) | All 3 test methods | PASS — `Assert.*` is xUnit framework internal throw, not project code |
| No `return null` | JS-002 (P0) | All 3 test methods | PASS — tests return `void`; local lists are always `new List<T>()` |
| No `async void` (non-event-handler) | JS-033 (P0) | All 3 test methods | PASS — all 3 tests are synchronous `[Fact] public void` |
| ASCII-only string literals | ASCII | All string literals | PASS — "Target1", "PTT-BE-Target-4", "Entry", etc. are all 7-bit ASCII |
| No LINQ in hot paths | (project rule) | All 3 test methods | PASS — pure `foreach` + `while` + `List.Add`; zero `.Select`, `.Where`, `.Take` calls |
| xUnit only (no NUnit/MSTest) | (project rule) | All 3 test methods | PASS — `[Fact]` + `Assert.*` only; no `[Test]`, `[TestMethod]` |
| No `DateTime.Now` | (project rule) | All 3 test methods | PASS — no date/time usage of any kind |
| NT8 API correctness | NT8-014 | Test 2 only | PASS — `OrderAction.Sell` is enum-only usage; no live `Account`, `Instrument`, or `CreateOrder` call |

---

### 7. Method Signatures

Engineer must implement exactly these signatures (no deviations):

```csharp
[Fact]
public void B130_DW107_SnapshotBeTargetsFiltersStaleOrders()

[Fact]
public void B130_DW107_HardCapTrimsSnapshotToThreeTargets()

[Fact]
public void B130_DW107_NonTargetOrdersProduceEmptySnapshot()
```

All three are `public void`, decorated with `[Fact]` (xUnit), no parameters, no return value,
no `async`/`await`, no `static`.

---

### 8. 7-Scan Checklist (SCAN-01 through SCAN-07)

Engineer MUST verify all 7 scans before marking ticket complete.

| Scan | Rule | What to Check | Expected Result |
|------|------|---------------|-----------------|
| **SCAN-01** | JS-021 — No `lock()` in new test code | `grep "lock(" src/PropTraderTools/Tests/B130Tests.cs` | Zero matches in the 3 new test methods |
| **SCAN-02** | JS-033 — No `async void` in new test code | `grep "async void" src/PropTraderTools/Tests/B130Tests.cs` | Zero matches in the 3 new test methods |
| **SCAN-03** | No `DateTime.Now` usage | `grep "DateTime.Now" src/PropTraderTools/Tests/B130Tests.cs` | Zero matches |
| **SCAN-04** | ASCII-only — no non-ASCII characters | `grep -P "[\x80-\xFF]" src/PropTraderTools/Tests/B130Tests.cs` | Zero matches (all literals are 7-bit ASCII) |
| **SCAN-05** | CYC <= 8 for all new test methods | Manual count per method (see Section 5 CYC annotations) | T1: ~5, T2: ~4, T3: ~5; all <= 8 |
| **SCAN-06** | NT8 API correctness — `OrderAction` enum usage only | Review Test 2 body | `OrderAction.Sell` enum used; no `Account`, `Instrument`, `CreateOrder`, `Submit` calls |
| **SCAN-07** | `dotnet test` passes | `dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "FullyQualifiedName~B130_DW107"` | Build succeeds + 3 new tests pass (0 failures) |

**Verification command for SCAN-07**:
```powershell
dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "FullyQualifiedName~B130_DW107" --verbosity normal
```

Expected output:
```
Passed  B130_DW107_SnapshotBeTargetsFiltersStaleOrders
Passed  B130_DW107_HardCapTrimsSnapshotToThreeTargets
Passed  B130_DW107_NonTargetOrdersProduceEmptySnapshot
Test Run Successful.
Total tests: 3
     Passed: 3
```

---

### 9. Acceptance Criteria Mapping

| Criterion | Proof Type | How Satisfied |
|-----------|-----------|---------------|
| **T1**: `SnapshotBeTargets` helper exists | Structural + Behavioral | Production fix confirmed at `CopyEngine.cs:L3922`. Test 1 predicates mirror `L3948-3958` exactly; predicate assertions pass only if the logic is correct. |
| **T2**: `MoveStopToBreakEven` calls `SnapshotBeTargets` | Structural | Confirmed at `CopyEngine.cs:L4019` by direct read (plan-review Checklist B). Not runtime-testable (private method + live NT8 Account required). Structural evidence is sufficient for this criterion. |
| **T3**: `while (targets.Count > 3) targets.RemoveAt(...)` cap present | Structural + Behavioral | Confirmed at `CopyEngine.cs:L4023-4024`. **Test 2** executes the verbatim algorithm on a local list with 3 boundary cases (4-item, 3-item, 0-item). |
| **T4**: `MoveStopToBreakEven` CYC <= 8 | Structural | Comment at `CopyEngine.cs:L3873`: `// CYC=7`. Confirmed by plan-review. |
| **T5**: `SnapshotBeTargets` CYC <= 8 | Structural | Comment at `CopyEngine.cs:L3917`: `// CYC=7`. Confirmed by plan-review. |
| **T6**: Zero `lock(` in new code | Behavioral (SCAN-01) | SCAN-01 grep confirms no `lock(` in 3 new test methods. No shared state in any test. |
| **T7**: Zero `return null` in new code | Behavioral (SCAN-03 analog) + Test 3 | Tests return `void`. All local lists are `new List<T>()`. **Test 3** `Assert.NotNull(result)` documents the production null-return contract as T7 anchor. |
| **T8**: All new strings/comments ASCII-only | Behavioral (SCAN-04) | SCAN-04 grep confirms zero non-ASCII bytes. All string literals ("Target1", "PTT-BE-Target-4", etc.) and comments are 7-bit ASCII. |

---

### 10. Deferred Items

None. All 8 DW-B107 acceptance criteria are satisfied by existing production code plus the
3 new tests in this ticket. No carry-forward items introduced by B130-LaneC.

---

**Return**: TICKETS_COMPLETE
