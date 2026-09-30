# B133 LaneA — Ticket File
**Phase**: 3 (Ticket Generation)
**Status**: TICKETS_COMPLETE
**Author**: ptt-architect
**Date**: 2026-08-21
**Source Plan**: docs/brain/B133/LaneA-02-architecture-plan.md (REVIEW_PASS)

---

## Ticket 1 — DW-B142 SignalOrNameMatches null-guard fix + B133 tests

### Spec Requirement IDs Satisfied

| ID | Description | P-level |
|----|-------------|---------|
| DW-B142 | `null==null` false-positive in `SignalOrNameMatches` causes wrong follower bracket cancelled on ATM drag | P0 |
| B133-TEST | 5 new xUnit `[Fact]` tests in `B133Tests.cs` — regression guard for DW-B142 | Required |

---

### Files Modified

| File | Action | Scope |
|------|--------|-------|
| `src/PropTraderTools/CopyEngine.cs` | MODIFY — one character insertion at L2512 | null-guard on `signalName` |
| `src/PropTraderTools/Tests/B133Tests.cs` | CREATE — new file | class `B133LaneATests`, 5 `[Fact]` methods |

**No other files are touched. Changes are fully self-contained.**

---

### Method Signatures (no signature changes — existing method, one-line fix)

```csharp
// CopyEngine.cs — L2510 — signature UNCHANGED
internal static bool SignalOrNameMatches(Order order, string? signalName, string? leaderName)

// CopyEngine.cs — L2556 — testable accessor UNCHANGED (already present)
internal static bool SignalOrNameMatchesTestable(Order order, string? signalName, string? leaderName)
    => SignalOrNameMatches(order, signalName, leaderName);
```

---

### Exact Code Change — CopyEngine.cs L2512

**BEFORE** (DW-B142 bug):
```csharp
if (order.FromEntrySignal == signalName) // (1) primary: signal equality (covers null==null)
```

**AFTER** (fix):
```csharp
if (signalName != null && order.FromEntrySignal == signalName) // (1) primary: signal equality (null-guarded)
```

**Change description**: Insert `signalName != null && ` before `order.FromEntrySignal == signalName`.
Update the inline comment to remove `(covers null==null)` — replace with `(null-guarded)`.

**CYC impact**: None. The null-guard is a short-circuit within the same boolean expression, not
a new branch node in the control-flow graph. `SignalOrNameMatches` CYC remains 3.

---

### New File: src/PropTraderTools/Tests/B133Tests.cs

**Class**: `B133LaneATests`
**Namespace**: `PropTraderTools.Tests`
**Framework**: xUnit `[Fact]` only — no NUnit, no MSTest
**Testable accessor**: `CopyEngine.SignalOrNameMatchesTestable` (internal, already at L2556)
**Assembly access**: `[assembly: InternalsVisibleTo("PropTraderTools.Tests")]` already declared at
`CopyEngine.cs L46` — no new attribute needed

#### StubOrder Helper (replicate from B131Tests.cs verbatim):

```csharp
// Helper: creates an Order with Name and FromEntrySignal set.
// SignalOrNameMatches reads only order.FromEntrySignal and order.Name.
// Pattern: direct NinjaTrader.Cbi.Order instantiation (same as B131Tests.cs, B132Tests.cs).
// Do NOT use Moq or any mocking framework.
private static Order StubOrder(string name, string? fromEntrySignal)
{
    var o = new Order();
    o.Name = name;
    o.FromEntrySignal = fromEntrySignal;
    return o;
}
```

---

### Test Method Signatures — All 5 [Fact] Tests

#### Test 1 — Primary DW-B142 regression guard

```csharp
[Fact]
public void SignalOrNameMatches_NullSignal_DoesNotMatchBySignal()
```

**Setup**:
- `signalName = null`
- `order.FromEntrySignal = null`
- `leaderName = "Target3"`
- `order.Name` = any value other than `"Target3"` (e.g. `"Stop1"`)

**Expected**: `false`

**Rationale**: Before the fix, `null == null` returned `true` (false positive). After the fix,
`signalName != null` guard fires and branch (1) returns `false`. Branch (3) also returns `false`
because `order.Name` is not `"Target3"`. This is the key DW-B142 regression guard.

---

#### Test 2 — ATM name-fallback path works after null-guard

```csharp
[Fact]
public void SignalOrNameMatches_NullSignal_MatchesByName()
```

**Setup**:
- `signalName = null`
- `order.FromEntrySignal = null`
- `leaderName = "Target3"`
- `order.Name = "Target3"`

**Expected**: `true`

**Rationale**: Branch (1) null-guard fires (false). Branch (2) `leaderName != null` passes.
Branch (3) `order.Name == leaderName` matches. Confirms the ATM fallback path works end-to-end
after the fix.

---

#### Test 3 — ATM name-fallback correctly rejects wrong-name order

```csharp
[Fact]
public void SignalOrNameMatches_NullSignal_NoMatch_WrongName()
```

**Setup**:
- `signalName = null`
- `order.FromEntrySignal = null`
- `leaderName = "Target3"`
- `order.Name = "Target1"`

**Expected**: `false`

**Rationale**: Branch (1) null-guard fires (false). Branch (2) passes. Branch (3)
`"Target1" == "Target3"` is `false`. Confirms branch (3) is not a blanket true.

---

#### Test 4 — Existing strategy-order signal path is unbroken

```csharp
[Fact]
public void SignalOrNameMatches_NonNullSignal_MatchesBySignal()
```

**Setup**:
- `signalName = "ES"`
- `order.FromEntrySignal = "ES"`
- `leaderName = null`
- `order.Name = "Stop1"` (arbitrary)

**Expected**: `true`

**Rationale**: `signalName != null` guard passes. `order.FromEntrySignal == signalName`
(`"ES" == "ES"`) is `true`. Branch (1) returns `true`. This is the original working path for
strategy orders — must be unbroken by the fix.

---

#### Test 5 — Double-null produces no match

```csharp
[Fact]
public void SignalOrNameMatches_NullLeaderName_NullSignal_NoMatch()
```

**Setup**:
- `signalName = null`
- `leaderName = null`
- `order.FromEntrySignal = null`
- `order.Name = "Stop1"` (arbitrary)

**Expected**: `false`

**Rationale**: Branch (1) null-guard fires (false). Branch (2) `leaderName == null` guard fires
(false). No match possible when both signal and leader name are null.

---

### Regression Requirement

All existing test suites MUST continue to pass without modification:

| Suite | File | Test Count | Relationship to fix |
|-------|------|------------|---------------------|
| `B131Tests` | `src/PropTraderTools/Tests/B131Tests.cs` | 7 | **Critical** — directly tests `SignalOrNameMatchesTestable`. All 7 must pass with the null-guard in place. |
| `B132Tests` | `src/PropTraderTools/Tests/B132Tests.cs` | 5 | Tests `DeriveLeaderBracketIndex`, `FindLeaderStopPrice` — no overlap with `SignalOrNameMatches`. |
| `B130Tests` | `src/PropTraderTools/Tests/B130Tests.cs` | 8 | Unrelated scope. |
| `B129Tests` | `src/PropTraderTools/Tests/B129Tests.cs` | 13 | Unrelated scope. |

**Critical regression note**: B131 tests 1 and 2 pass `signalName="AtmEntrySignal"` (non-null),
so the new null-guard does not affect them. B131 tests 3-7 cover cases where the null-guard also
does not apply. All 7 B131 tests must green after the fix — verify with `dotnet test`.

---

### JS Rule Constraints (per touched file)

| Rule | Constraint | Applicability |
|------|------------|---------------|
| JS-021 | No `lock()` anywhere in touched code | N/A — `SignalOrNameMatches` is `static`, no shared state; `B133Tests.cs` is test-only |
| JS-001 | No `throw new XxxException` in hot paths | N/A — fix inserts a boolean guard, not a throw |
| JS-002 | No `return null` | N/A — method returns `bool`; already compliant; no change |
| JS-033 | No `async void` (non-event-handler) | N/A — method is synchronous; no async |
| JS-036 | No `new byte[]` heap allocation in hot path | N/A — no allocation changes |
| JS-037 | No `new T[]` without ArrayPool in hot path | N/A — no allocation changes |
| CYC | All touched methods <= 8 | `SignalOrNameMatches` CYC stays at 3. All test methods are CYC=1. |
| ASCII | ASCII-only identifiers and string literals | All new identifiers and string literals in `B133Tests.cs` are ASCII-only |
| DateTime | `DateTime.UtcNow` only (no `DateTime.Now`) | N/A — no time logic in fix or tests |
| Order naming | `CreateOrder` calls use `"PTT-"` prefix | N/A — no `CreateOrder` call introduced |

---

### 7-SCAN CHECKLIST (mandatory — engineer runs all 7 before reporting BUILD_PASS)

The engineer MUST run every scan against the post-change codebase and confirm all pass.
Record results in the BUILD_PASS report.

- [ ] **SCAN-01** — No `lock()` in src/:
  ```
  grep -r "lock(" src/ --include="*.cs"
  ```
  **Required result**: 0 results

- [ ] **SCAN-02** — No `async void` in src/:
  ```
  grep -rn "async void " src/ --include="*.cs"
  ```
  **Required result**: 0 results

- [ ] **SCAN-03** — No new `return null;` in touched files:
  ```
  grep -rn "return null;" src/ --include="*.cs"
  ```
  **Required result**: 0 new occurrences in `CopyEngine.cs` or `B133Tests.cs`
  (pre-existing occurrences in other files are acceptable)

- [ ] **SCAN-04** — No new `throw new` in touched files:
  ```
  grep -rn "throw new" src/ --include="*.cs"
  ```
  **Required result**: 0 new occurrences in `CopyEngine.cs` or `B133Tests.cs`

- [ ] **SCAN-05** — CYC <= 8 for all touched methods:
  ```
  python scripts/complexity_audit.py
  ```
  **Required result**: 0 methods > CYC 8 in `CopyEngine.cs` and `B133Tests.cs`

- [ ] **SCAN-06** — ASCII-only in touched files (PowerShell):
  ```powershell
  Select-String -Path "src\PropTraderTools\CopyEngine.cs","src\PropTraderTools\Tests\B133Tests.cs" -Pattern "[^\x00-\x7F]"
  ```
  OR (bash/grep):
  ```
  grep -Prn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs src/PropTraderTools/Tests/B133Tests.cs
  ```
  **Required result**: 0 results

- [ ] **SCAN-07** — Build green:
  ```
  dotnet build src/PropTraderTools/PropTraderTools.csproj
  ```
  **Required result**: 0 errors, 0 warnings

---

### Implementation Notes for Engineer

1. **The CopyEngine.cs change is a single-character-range insertion on L2512 only.**
   Do not modify any other line in `CopyEngine.cs`. Do not reformat, reindent, or adjust
   any surrounding code. Touch only L2512.

2. **B133Tests.cs is a NEW file.** It does not exist yet. Create it at
   `src/PropTraderTools/Tests/B133Tests.cs`.

3. **Mock pattern**: `NinjaTrader.Cbi.Order` is directly instantiable (not sealed in the NT8
   assembly available to the test project). Replicate the `StubOrder` helper exactly as used in
   `B131Tests.cs` and `B132Tests.cs` — direct instantiation with `.Name` and `.FromEntrySignal`
   property assignment. Do NOT use Moq or any mocking framework.

4. **Testable accessor**: Call `CopyEngine.SignalOrNameMatchesTestable(...)` in all 5 tests.
   This accessor is already at `CopyEngine.cs L2556`. Do not add a new accessor.

5. **InternalsVisibleTo**: Already declared at `CopyEngine.cs L46`. No new assembly attribute
   is required.

6. **Class and method name casing**: Use exactly the names specified in the Test Method Signatures
   section above. ASCII-only. No underscores in class name (`B133LaneATests` not `B133_LaneA_Tests`).

7. **usings required in B133Tests.cs**:
   ```csharp
   using NinjaTrader.Cbi;
   using Xunit;
   ```

8. **Namespace**: `PropTraderTools.Tests`

9. Run `dotnet test` after SCAN-07 to verify all 33 tests (28 existing + 5 new) pass green.

---

### Completion Criteria

The engineer reports **BUILD_PASS** when ALL of the following are true:

- [ ] `CopyEngine.cs L2512` contains the null-guard: `signalName != null && order.FromEntrySignal == signalName`
- [ ] `src/PropTraderTools/Tests/B133Tests.cs` exists and contains class `B133LaneATests` with exactly 5 `[Fact]` methods
- [ ] All 5 B133 tests pass green in `dotnet test`
- [ ] All 28 prior tests (B129×13, B130×8, B131×7, B132×5) continue to pass (0 regressions)
- [ ] SCAN-01 through SCAN-07 all pass
- [ ] `dotnet build src/PropTraderTools/PropTraderTools.csproj` → 0 errors, 0 warnings

---

*Tickets written by ptt-architect from REVIEW_PASS plan at docs/brain/B133/LaneA-02-architecture-plan.md.*
