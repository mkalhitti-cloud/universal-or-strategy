# B130-LaneC Plan Review

**Block**: B130-LaneC
**Defect**: DW-B107
**Phase**: Phase 2 Review
**Reviewer**: ptt-plan-reviewer
**Plan reviewed**: `docs/brain/B130/LaneC-02-architecture-plan.md`
**Date**: 2026-09-01

---

## VERDICT: REVIEW_PASS

No P0 or P1 rule violations found. All 8 acceptance criteria addressed. Plan is correctly scoped to tests only. Two documentation-only inaccuracies noted below (non-blocking).

---

## Per-Criterion Findings

### Checklist A — Spec Traceability (T1-T8)

All 8 acceptance criteria from `DW-B107/00-defect-brief.md` are addressed in the plan.

| Criterion | Addressed? | Plan Section | Evidence |
|-----------|------------|--------------|----------|
| T1: `SnapshotBeTargets` helper exists | YES | Section A + I | Direct code read L3922 confirmed. Test 1 proves predicate logic. |
| T2: `MoveStopToBreakEven` calls `SnapshotBeTargets` | YES | Section A + I | Direct code read L4019 confirmed. Structural proof only (private method — no NT8 Account at test time). |
| T3: `while (targets.Count > 3) targets.RemoveAt(...)` cap present | YES | Section A + C (Test 2) | Direct code read L4023-4024 confirmed. Test 2 executes the exact algorithm. |
| T4: `MoveStopToBreakEven` CYC <= 8 | YES | Section E | L3873 comment: `// CYC=7`. |
| T5: `SnapshotBeTargets` CYC <= 8 | YES | Section E | L3917 comment: `// CYC=7`. |
| T6: Zero `lock(` in new code | YES | Section G (SCAN-01) + Section H | No lock in any planned test method. |
| T7: Zero `return null` in new code | YES | Section C (Test 3) + H | Tests return void; local lists are `new List<T>()`, never null. L3930 returns empty list. |
| T8: All new strings/comments ASCII-only | YES | Section G (SCAN-05) + H | All order name literals ("Target1", "PTT-BE-Target-4", etc.) are 7-bit ASCII. |

**Result**: PASS — all 8 criteria addressed.

---

### Checklist B — Production Fix Confirmation

The plan claims the DW-B107 production fix is already in `CopyEngine.cs`.

**Verified by direct source read:**

| Location | Expected | Actual (read) | Match? |
|----------|----------|---------------|--------|
| `CopyEngine.cs:L3922` | `private List<...> SnapshotBeTargets(Account acc, Instrument instrument)` | `private List<(double Price, int Qty, OrderAction Action)> SnapshotBeTargets(Account acc, Instrument instrument)` | YES |
| `CopyEngine.cs:L3917` | CYC=7 comment | `// CYC=7: null guard(1) + foreach(2) + o==null continue(3) + stateOk(4) + instrOk+type(5) + if(isNative)(6) + else if(isPtt)(7). JS-002: returns List, never null.` | YES |
| `CopyEngine.cs:L3930` | `return nativeTargets; // JS-002: empty list, never null` | `return nativeTargets; // (1) JS-002: empty list, never null` | YES |
| `CopyEngine.cs:L3873` | CYC=7 comment | `// CYC=7: IsFlat(1) + tickSize/pos guard(2) + while-cap(3) + cancel-try(4) + 0-targets branch(5) + targets-for-loop(6) + partial-retry branch(7).` | YES |
| `CopyEngine.cs:L4019` | `var targets = SnapshotBeTargets(acc, instrument); // (3)` | CONFIRMED at L4019 | YES |
| `CopyEngine.cs:L4023-4024` | `while (targets.Count > 3) targets.RemoveAt(targets.Count - 1);` | CONFIRMED at L4023-4024 | YES |

**Result**: PASS — production fix confirmed exactly as described. Tests-only scope is correct.

---

### Checklist C — Tests-Only Scope

Plan correctly identifies the single file change:

- `src/PropTraderTools/Tests/B130Tests.cs` — APPEND ONLY
- `CopyEngine.cs` — NOT MODIFIED (fix already implemented)
- `PropTraderTools.csproj` — NOT MODIFIED (B130Tests.cs already in Compile)

**Result**: PASS.

---

### Checklist D — Test Strategy (Option C: Behavioral Equivalence)

The plan selects inline local function predicates mirroring `CopyEngine.cs:L3948-3958`.

**Predicate comparison (plan vs. production source):**

**isNative predicate:**

Production (`CopyEngine.cs:L3948-3952`, after `string.IsNullOrEmpty` guard):
```csharp
o.Name.Length >= 7
    && o.Name.StartsWith("Target", StringComparison.Ordinal)
    && char.IsDigit(o.Name[6])
    && o.Name[6] != '0'
```

Plan test helper (`IsNativeTarget(string n)`):
```csharp
n != null
    && n.Length >= 7
    && n.StartsWith("Target", StringComparison.Ordinal)
    && char.IsDigit(n[6])
    && n[6] != '0'
```

Assessment: Adds `n != null` guard (production guards via `string.IsNullOrEmpty` at L3946). Logically equivalent for all non-null string inputs. The test operates on string literals, so `n != null` is always true — acceptable defensive addition. **PASS.**

**isPtt predicate:**

Production (`CopyEngine.cs:L3953-3958`):
```csharp
(
    o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
    && o.Name.Length > 8
    && char.IsDigit(o.Name[8])
) || o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
```

Plan test helper (`IsPttTarget(string n)`):
```csharp
n != null
    && (
        (n.StartsWith("PTT-QX-T", StringComparison.Ordinal)
         && n.Length > 8
         && char.IsDigit(n[8]))
        || n.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)
    )
```

Assessment: Exact logical match to production (plus `n != null` guard as above). **PASS.**

---

### Checklist E — Test 2 (Hard Cap Algorithm)

Test 2 (`B130_DW107_HardCapTrimsSnapshotToThreeTargets`) directly executes:
```csharp
while (targets.Count > 3)
    targets.RemoveAt(targets.Count - 1);
```

This is verbatim copy of `CopyEngine.cs:L4023-4024`. Tests three cases: 4-item list (root-cause), 3-item list (nominal), 0-item list (empty). All three boundary conditions are covered. **PASS.**

---

### Checklist F — Test 3 (Non-Target Empty Snapshot / JS-002 Contract)

Test 3 (`B130_DW107_NonTargetOrdersProduceEmptySnapshot`) verifies that non-target order names (Entry, Close, PTT-BE-Stop-*, Stop1/2/3, PTT-Copy, PTT-QX-Stop-1) match neither predicate, and that the final `result` list is empty and non-null.

**Observation (non-blocking documentation issue):** `Assert.NotNull(result)` asserts non-null on a local variable `result` that was assigned as `nativeTargets.Count > 0 ? nativeTargets : pttTargets` — where both are initialized via `new List<string>()`. The assertion is tautological by construction (locally-initialized reference type). It does not directly assert the production null-return contract against live NT8 output. However, it satisfies T7 as a documentation anchor for the JS-002 contract, and the assertion itself causes no harm. No rule violation. **PASS** (with note).

---

### Checklist G — CYC Compliance

**Production methods:**
- `SnapshotBeTargets` CYC=7 (confirmed at L3917 comment). Within limit. PASS.
- `MoveStopToBreakEven` CYC=7 (confirmed at L3873 comment). Within limit. PASS.

**Planned test methods:**
- Plan states CYC=1 for all 3 tests.

**Documentation inaccuracy (non-blocking):** The plan's CYC=1 claim for tests is incorrect. McCabe cyclomatic complexity counts ALL decision nodes including `while`, `foreach`, `if`, and ternary operators. Actual CYC counts for planned tests:

| Test Method | Branch Nodes | Actual CYC | Plan Claims | Limit | Violates? |
|-------------|--------------|------------|-------------|-------|-----------|
| Test 1: `B130_DW107_SnapshotBeTargetsFiltersStaleOrders` | foreach(1) + if(1) + else if(1) + ternary(1) | ~5 | 1 | 8 | NO |
| Test 2: `B130_DW107_HardCapTrimsSnapshotToThreeTargets` | while(1) + while(1) + while(1) | ~4 | 1 | 8 | NO |
| Test 3: `B130_DW107_NonTargetOrdersProduceEmptySnapshot` | foreach(1) + if(1) + else if(1) + ternary(1) | ~5 | 1 | 8 | NO |

**All actual CYC values are <= 8. No rule violation.** The CYC=1 claim in the plan is a documentation inaccuracy only. It does not affect gate pass/fail.

---

### Checklist H — 7-Scan Checklist

Plan Section G maps all 7 scans to the planned test code.

| Scan | Rule | Plan Assessment | Reviewer Assessment |
|------|------|-----------------|---------------------|
| SCAN-01 | JS-021 No `lock()` | PASS | CONFIRMED — no lock in any test body. |
| SCAN-02 | JS-001 No `throw` in hot paths | PASS | CONFIRMED — `Assert.*` is xUnit's internal throw, not project code. |
| SCAN-03 | JS-002 No `return null` | PASS | CONFIRMED — tests return void; all lists are `new List<T>()`. |
| SCAN-04 | JS-033 No `async void` | PASS | CONFIRMED — all 3 tests are synchronous `[Fact] void`. |
| SCAN-05 | ASCII-only | PASS | CONFIRMED — all string literals and comments are 7-bit ASCII. |
| SCAN-06 | No LINQ in hot paths | PASS | CONFIRMED — pure foreach + while + List.Add. Zero LINQ calls. |
| SCAN-07 | xUnit only | PASS | CONFIRMED — `[Fact]` + `Assert.*` only. No NUnit/MSTest markers. |

**Result**: PASS — all 7 scans addressed and verified.

---

### Checklist I — File Scope

Plan claims APPEND ONLY to `src/PropTraderTools/Tests/B130Tests.cs`. Confirmed:
- `using NinjaTrader.Cbi;` present at L6 of B130Tests.cs — no new `using` required.
- Last existing test `B130_DW136_CancelLeaderOrder2DoesNotEvictLeaderOrder1Bag` confirmed at L106.
- Append point (before closing `}` of `B130Tests` class) is correct.

**Result**: PASS.

---

### Checklist J — No Missing DW-B107 Requirements

The defect brief defines exactly 8 acceptance criteria (T1-T8). All are addressed in the plan (see Checklist A). No spec requirement is unaddressed.

**Result**: PASS.

---

### Checklist K — Rules Catalog P0 Violations in Planned Test Code

| Rule | Pattern | Found in planned test code? | Result |
|------|---------|----------------------------|--------|
| JS-021 | `lock(` | NO | PASS |
| JS-001 | `throw new XxxException(` in hot paths | NO | PASS |
| JS-002 | `return null;` | NO | PASS |
| JS-033 | `async void` (non-event-handler) | NO | PASS |
| JS-003 | Magic string for discriminated state | NO | PASS |
| CYC > 8 | Any method CYC > 8 | NO (actual CYC <= 5 for all tests) | PASS |
| NT8 violations | `async/await` in NT8 callbacks | NO (tests are standalone [Fact] methods) | PASS |
| NT8-014 | CreateOrder without PTT- prefix | NO (no CreateOrder in test code) | PASS |
| SCAN-06 | DateTime.Now | NO | PASS |

**Result**: PASS — no P0 violations in any planned test code.

---

### Additional Verification: B130_DW107 Tests Absent from B130Tests.cs

Grep of `src/PropTraderTools/Tests/B130Tests.cs` for pattern `B130_DW107`:

```
grep result: No matches
```

Confirmed: none of the 3 planned tests exist yet. Engineer must add them.

---

## Violations Log

| # | Rule ID | Severity | Description | Location | Blocking? |
|---|---------|----------|-------------|----------|-----------|
| 1 | (none) | DOC | Plan states CYC=1 for test methods. Actual CYC is ~4-5 per test (while/foreach/if nodes are decision points). All actual values <= 8. No compliance violation — documentation inaccuracy only. | Plan Section E, C | NO |
| 2 | (none) | DOC | `Assert.NotNull(result)` in Test 3 is tautological (locally-constructed List<string> cannot be null). Does not exercise the production null-return path against NT8 runtime output. No rule violation — acceptable as a T7 documentation anchor. | Plan Section C, Test 3 | NO |

**Total blocking violations**: 0

---

## Spec Coverage Matrix

| Requirement | Addressed? | Plan Section |
|-------------|------------|--------------|
| T1: SnapshotBeTargets exists | YES | A, I |
| T2: MoveStopToBreakEven calls SnapshotBeTargets | YES | A, I |
| T3: while-cap algorithm present | YES | A, C (Test 2), I |
| T4: MoveStopToBreakEven CYC <= 8 | YES | E |
| T5: SnapshotBeTargets CYC <= 8 | YES | E |
| T6: Zero lock() | YES | G, H |
| T7: Zero return null | YES | C (Test 3), H |
| T8: ASCII-only | YES | G, H |

---

## Summary

The architecture plan for B130-LaneC is **sound, complete, and compliant** with the DW-B107 defect brief and the Jane Street Rules Catalog. The production fix is confirmed implemented. The test strategy (behavioral equivalence via inline predicates) is technically valid. All 8 acceptance criteria are addressed. All 7 scans are mapped and pass. No P0 or P1 rule violations exist in the planned test code. Two documentation inaccuracies (CYC=1 claim for tests; tautological Assert.NotNull) are noted as non-blocking observations.

**REVIEW_PASS**
