# Ticket Review: B130-LaneC

**Block**: B130-LaneC
**Defect**: DW-B107
**Phase**: Phase 3.5 (Ticket Review)
**Reviewer**: ptt-ticket-reviewer
**Plan status reviewed**: REVIEW_PASS (`docs/brain/B130/LaneC-02-plan-review.md`)
**Tickets reviewed**: `docs/brain/B130/LaneC-04-tickets.md`
**Date**: 2026-09-01

---

## T3 — Append 3 DW-B107 Tests to B130Tests.cs

### Check 1 — Traceability

Every ticket item mapped to a DW-B107 acceptance criterion or architecture plan item.

| Criterion | Ticket claim | Architecture evidence | Result |
|-----------|-------------|----------------------|--------|
| **T1** — `SnapshotBeTargets` helper exists | Structural proof at `CopyEngine.cs:L3922`. Test 1 predicates mirror `L3948-3958`. | Plan Section A table + direct read confirmed. | PASS |
| **T2** — `MoveStopToBreakEven` calls `SnapshotBeTargets` | Structural proof at `CopyEngine.cs:L4019`. Noted as not runtime-testable (private method, live NT8 Account required). | Plan Section A table + Plan Section B (access constraint analysis). Agreed design decision for tests-only block. | PASS |
| **T3** — Hard-cap `while (targets.Count > 3)` present | Test 2 directly executes the algorithm on a local `List<T>` with 4-item / 3-item / 0-item boundary cases. Structural proof at `L4023-4024`. | Plan Section C, Test 2. | PASS |
| **T4** — `MoveStopToBreakEven` CYC ≤ 8 | Structural: comment at `CopyEngine.cs:L3873` `// CYC=7`. | Plan Section E table. | PASS |
| **T5** — `SnapshotBeTargets` CYC ≤ 8 | Structural: comment at `CopyEngine.cs:L3917` `// CYC=7`. | Plan Section E table. | PASS |
| **T6** — Zero `lock(` in new code | SCAN-01 grep in ticket Section 8. | No shared mutable state in any test. | PASS |
| **T7** — Zero `return null` in new code | Tests return `void`. Test 3 `Assert.NotNull(result)` documents the production null-return contract. | Ticket Section 9 mapping. | PASS |
| **T8** — All new strings/comments ASCII-only | SCAN-04 grep in ticket Section 8. | All string literals 7-bit ASCII confirmed. | PASS |

**Phantom work** (ticket items not traceable to plan/spec): None found.
**Missing work** (plan items absent from ticket): None found. All plan sections (A through K) are addressed.

Traceability: **PASS**

---

### Check 2 — 7-Scan Checklist Presence

Ticket Section 8 contains SCAN-01 through SCAN-07 as a complete table with rule citation, what to check, and expected result per scan.

| Scan | Rule Cited | What to Check | Expected Result | Present? |
|------|-----------|---------------|-----------------|----------|
| SCAN-01 | JS-021 — No `lock()` | `grep "lock(" src/PropTraderTools/Tests/B130Tests.cs` | Zero matches | ✅ |
| SCAN-02 | JS-033 — No `async void` | `grep "async void" src/PropTraderTools/Tests/B130Tests.cs` | Zero matches | ✅ |
| SCAN-03 | No `DateTime.Now` | `grep "DateTime.Now" src/PropTraderTools/Tests/B130Tests.cs` | Zero matches | ✅ |
| SCAN-04 | ASCII-only | `grep -P "[\x80-\xFF]" src/PropTraderTools/Tests/B130Tests.cs` | Zero matches | ✅ |
| SCAN-05 | CYC ≤ 8 | Manual count per method | T1:~5, T2:~4, T3:~5 | ✅ |
| SCAN-06 | NT8 API correctness | Review Test 2 body | `OrderAction.Sell` enum only; no live `Account`/`Instrument`/`CreateOrder` | ✅ |
| SCAN-07 | `dotnet test` passes | `dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "FullyQualifiedName~B130_DW107"` | 3 tests pass, exact expected output specified | ✅ |

All 7 scans present with rule, command, and expected result. **Defense-in-depth contract intact for engineer attestation (Layer 2) and verifier cross-check (Layer 3).**

Scan Checklist: **PASS**

---

### Check 3 — Test Coverage

| Test | DW-B107 behavior covered | Assertions | Adequate? |
|------|-------------------------|-----------|-----------|
| **Test 1** `B130_DW107_SnapshotBeTargetsFiltersStaleOrders` | Two-pass native-first classification (DW-B107 CHANGE A). Native names classify as native; PTT residues classify as PTT; non-targets classify as neither; when natives exist `PTT-BE-Target-4` is excluded from the result list. | 12 `Assert.*` statements including `Assert.Equal(3, result.Count)` and `Assert.DoesNotContain("PTT-BE-Target-4", result)`. | PASS |
| **Test 2** `B130_DW107_HardCapTrimsSnapshotToThreeTargets` | Hard-cap algorithm (DW-B107 CHANGE B). Three boundary cases: 4-item list trimmed to 3; 3-item list unchanged; 0-item list unchanged. | 3 `Assert.Equal` statements, one per case. | PASS |
| **Test 3** `B130_DW107_NonTargetOrdersProduceEmptySnapshot` | Empty-snapshot path; JS-002 null contract; non-target names excluded from snapshot. | `Assert.Empty(nativeTargets)`, `Assert.Empty(pttTargets)`, `Assert.Empty(result)`, `Assert.NotNull(result)`. | PASS |

Test Coverage: **PASS**

---

### Check 4 — JS Pre-Check (P0 rules in planned test code)

Reviewed all three test method bodies in ticket Section 5.

| Rule ID | Severity | Check | Result |
|---------|----------|-------|--------|
| **JS-021** | P0 | No `lock()` in any test body | PASS — no shared mutable state; all `List<T>` are method-local |
| **JS-033** | P0 | No `async void` | PASS — all 3 methods are synchronous `[Fact] public void`, no `async` keyword |
| **JS-001** | P0 | No `throw new XxxException` in test bodies | PASS — `Assert.*` is xUnit framework internal; no `throw` in test code |
| **JS-002** | P0 | No `return null` | PASS — tests return `void`; all local lists are `new List<T>()` (never null) |
| **ASCII-only** | P0 | All string literals and comments 7-bit ASCII | PASS — "Target1", "PTT-BE-Target-4", "PTT-QX-T1", "Entry", "PTT-BE-Stop-1", etc. are all 7-bit ASCII; all comments are ASCII-only |
| **No NUnit/MSTest** | project rule | xUnit-only test framework | PASS — `[Fact]` + `Assert.*` from xUnit; no `[Test]`, `[TestMethod]` |
| **No LINQ** | project rule | No `.Select()`/`.Where()`/`.Take()` in hot paths | PASS — pure `foreach`, `while`, `List.Add()` throughout |
| **No `DateTime.Now`** | project rule | No datetime usage | PASS — no date/time usage of any kind |

JS Pre-Check: **PASS**

---

### Check 5 — CYC Pre-Check

Manual verification of CYC for all 3 new test methods. Local functions (`IsNativeTarget`, `IsPttTarget`) are pure expression-body static helpers — their internal `&&`/`||` boolean short-circuit chains do not add to the enclosing method's McCabe CYC per convention (boolean connectives are not decision branches in the classical CFG model). CYC counted from `foreach`, `if`, `else if`, ternary `?:`, `while` statements in the test body.

| Method | Branch points | CYC | Limit | Status |
|--------|--------------|-----|-------|--------|
| `B130_DW107_SnapshotBeTargetsFiltersStaleOrders` | `foreach`(1) + `if IsNativeTarget`(1) + `else if IsPttTarget`(1) + ternary(1) + base(1) | **5** | 8 | PASS |
| `B130_DW107_HardCapTrimsSnapshotToThreeTargets` | `while targets4`(1) + `while targets3`(1) + `while targets0`(1) + base(1) | **4** | 8 | PASS |
| `B130_DW107_NonTargetOrdersProduceEmptySnapshot` | `foreach`(1) + `if IsNativeTarget`(1) + `else if IsPttTarget`(1) + ternary(1) + base(1) | **5** | 8 | PASS |

CYC Pre-Check: **PASS**

---

### Check 6 — NT8 Constraints

| Test | NT8 types used | Testable without NT8 harness? |
|------|----------------|-------------------------------|
| Test 1 | None — string operations and `List<string>` only | ✅ SAFE |
| Test 2 | `NinjaTrader.Cbi.OrderAction.Sell` (enum value only) — no live `Account`, `Instrument`, `CreateOrder`, or `Submit` | ✅ SAFE — enum reference is compile-time constant; `using NinjaTrader.Cbi;` already present in file |
| Test 3 | None — string operations and `List<string>` only | ✅ SAFE |

All 3 tests are pure logic tests with no NT8 runtime dependency. No NT8 constraint violations in ticket description.

NT8 Check: **PASS**

---

### Check 7 — Append-Only Check

| Requirement | Ticket evidence | Status |
|-------------|----------------|--------|
| No `CopyEngine.cs` changes | Section 3 file scope table: "NOT MODIFIED — Production fix already implemented" | ✅ |
| No `.csproj` changes | Section 3: "NOT MODIFIED — `B130Tests.cs` already in `<Compile Include="Tests\B130Tests.cs" />`" | ✅ |
| No other `.cs` file changes | Section 3: "Out of scope" | ✅ |
| Explicit BEFORE state shown | Section 1: lists 5 existing test names | ✅ |
| Explicit AFTER state shown | Section 2: lists 8 tests (5 existing + 3 new) | ✅ |
| Exact insert point specified | Section 4: shows BEFORE/AFTER code block with insertion before closing `}` of `B130Tests` class | ✅ |

Append-Only Check: **PASS**

---

### Check 8 — Completeness

| Requirement | Evidence | Status |
|-------------|----------|--------|
| Method signatures fully specified | Section 7: all 3 signatures listed with exact return type (`void`), decorator (`[Fact]`), and name | ✅ |
| Test 1 exact body | Section 5, Test 1: complete verbatim C# (local functions + 12 `Assert.*` calls + simulation loop) | ✅ |
| Test 2 exact body | Section 5, Test 2: complete verbatim C# (3 `while` boundary cases + 3 `Assert.Equal` calls) | ✅ |
| Test 3 exact body | Section 5, Test 3: complete verbatim C# (non-target array + loop + 4 `Assert.*` calls) | ✅ |
| No new `using` directives required | Section 5 preamble confirms existing header provides `NinjaTrader.Cbi` and `Xunit` | ✅ |

Engineer can copy all 3 test bodies verbatim. Completeness: **PASS**

---

### Check 9 — Predicate Accuracy

Direct comparison between `CopyEngine.cs:L3948-3958` (production source) and the `IsNativeTarget`/`IsPttTarget` local functions in Tests 1 and 3.

**Production source (L3948-3958, confirmed by direct read):**
```csharp
bool isNative =
    o.Name.Length >= 7
    && o.Name.StartsWith("Target", StringComparison.Ordinal)
    && char.IsDigit(o.Name[6])
    && o.Name[6] != '0';
bool isPtt =
    (
        o.Name.StartsWith("PTT-QX-T", StringComparison.Ordinal)
        && o.Name.Length > 8
        && char.IsDigit(o.Name[8])
    ) || o.Name.StartsWith("PTT-BE-Target-", StringComparison.Ordinal);
```

**Test local functions (verbatim from ticket):**
```csharp
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
```

**Condition-by-condition match:**

| Condition | Production L3948-3958 | Test local function | Match |
|-----------|----------------------|---------------------|-------|
| Length guard | `o.Name.Length >= 7` | `n.Length >= 7` | EXACT |
| StartsWith "Target" | `n.StartsWith("Target", StringComparison.Ordinal)` | identical | EXACT |
| IsDigit at [6] | `char.IsDigit(n[6])` | identical | EXACT |
| Not '0' at [6] | `n[6] != '0'` | identical | EXACT |
| StartsWith "PTT-QX-T" | `n.StartsWith("PTT-QX-T", StringComparison.Ordinal)` | identical | EXACT |
| Length > 8 | `n.Length > 8` | identical | EXACT |
| IsDigit at [8] | `char.IsDigit(n[8])` | identical | EXACT |
| StartsWith "PTT-BE-Target-" | `n.StartsWith("PTT-BE-Target-", StringComparison.Ordinal)` | identical | EXACT |
| `n != null` guard | Not present (production uses `string.IsNullOrEmpty` pre-filter at L3946) | Added as first conjunct | ADDITIVE (safe) |

**Reviewer note**: The production code pre-filters null/empty names at L3946 before reaching the predicate. The test local functions add an explicit `n != null` guard to make the standalone helper safe for null input. This is logically consistent — it handles a superset of inputs, not a different behavior for the non-null inputs the predicates actually receive. Non-blocking.

Predicate Accuracy: **PASS** — all 8 predicate conditions match verbatim.

---

### Check 10 — Build Command (SCAN-07)

Ticket Section 8, SCAN-07 specifies:
```powershell
dotnet test src/PropTraderTools/PropTraderTools.csproj --filter "FullyQualifiedName~B130_DW107" --verbosity normal
```

Expected output explicitly stated:
```
Passed  B130_DW107_SnapshotBeTargetsFiltersStaleOrders
Passed  B130_DW107_HardCapTrimsSnapshotToThreeTargets
Passed  B130_DW107_NonTargetOrdersProduceEmptySnapshot
Test Run Successful.
Total tests: 3
     Passed: 3
```

Exact command path, filter expression, and expected pass/fail counts all specified. Build Command: **PASS**

---

### File Routing

All file references point to the Wave workspace:
- `src/PropTraderTools/Tests/B130Tests.cs` → `c:\WSGTA\universal-or-strategy\src\PropTraderTools\Tests\B130Tests.cs`
- `src/PropTraderTools/CopyEngine.cs` (read-only reference, NOT MODIFIED)

No Director workspace paths for `.cs` files.

File Routing: **PASS**

---

### Additional Reviewer Notes (Non-Blocking)

1. **`Assert.NotNull(result)` in Test 3**: The ticket acknowledges this assertion is tautological by construction (the ternary of two `new List<string>()` references can never be null). The ticket retains it as a documentation anchor for the T7 contract. This is an informed, documented choice. Non-blocking.

2. **Scan label numbering inconsistency between plan and ticket**: The architecture plan (Section G) numbers the scans as SCAN-01=lock, SCAN-02=throw, SCAN-03=return-null, SCAN-04=async-void, SCAN-05=ASCII, SCAN-06=LINQ, SCAN-07=xUnit. The ticket (Section 8) uses SCAN-01=lock, SCAN-02=async-void, SCAN-03=DateTime.Now, SCAN-04=ASCII, SCAN-05=CYC, SCAN-06=NT8, SCAN-07=dotnet-test. The tickets are the engineer contract; the plan is advisory context. The 7 scans in the ticket are internally consistent and complete. Non-blocking.

3. **CYC note in architecture plan vs ticket**: The plan (Section E) reports test method CYC = 1 (treating foreach/while as algorithmic, not branching). The ticket (Section 5) reports CYC ~5, ~4, ~5 using the conventional McCabe per-branch count. The ticket's more conservative count (foreach + if/else if + ternary each counted) is the safer engineering contract. The engineer should use the ticket's count, which is still well within the ≤ 8 limit. Non-blocking.

---

### T3 Verdict Summary

| Check | Result |
|-------|--------|
| Traceability | ✅ PASS |
| 7-Scan Checklist Presence | ✅ PASS |
| Test Coverage | ✅ PASS |
| JS Pre-Check (P0 rules) | ✅ PASS |
| CYC Pre-Check | ✅ PASS |
| NT8 Constraints | ✅ PASS |
| Append-Only Check | ✅ PASS |
| Completeness | ✅ PASS |
| Predicate Accuracy | ✅ PASS |
| Build Command (SCAN-07) | ✅ PASS |
| File Routing | ✅ PASS |

### VERDICT: TICKET_REVIEW_PASS

---

## Overall

All checks PASS. No violations found. No items requiring architect revision.

The ticket is a complete engineering contract. The engineer can implement T3 by:
1. Reading ticket Section 4 for the exact append location.
2. Copying the 3 test bodies verbatim from ticket Section 5.
3. Running the 7 scans from ticket Section 8 before marking BUILD_PASS.

## Overall: **TICKET_REVIEW_PASS**
