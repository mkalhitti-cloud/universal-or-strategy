# PTT-COPIER-B21-LANE-A — Ticket File
# Block:  PTT-COPIER-B21
# Lane:   A
# Defect: DW-ATR-DEFAULTS-01
# Status: TICKETS_COMPLETE
# Date:   2026-07-14

---

## Preamble

**Source plan**: `docs/brain/PTT-COPIER-B21-LANE-A/02-architecture-plan.md` (REVIEW_PASS)
**Spec requirement**: `DW-ATR-DEFAULTS-01` (P1) — ATR sizing engine field-initialiser defaults
do not match the values `StartAtrEngine` configures immediately after construction.
**xUnit baseline entering this ticket**: 120 `[Fact]` tests.
**xUnit count after ticket**: 121 `[Fact]` tests (net +1).
**Tickets in this lane**: 1 (T1 covers all three bug fixes + 1 new `[Fact]`).

---

## T1 — Align AtrSizingEngine Defaults and Add Regression Test

### Spec Requirement Satisfied
`DW-ATR-DEFAULTS-01` — three field/call-site mismatches between `AtrSizingEngine` field
initialisers and the values that `TradeCopierAddOn.StartAtrEngine` sets immediately after
construction.

### Write-Set (files engineer must modify — no other file may be touched)

| File | Absolute path in wave workspace |
|------|---------------------------------|
| `AtrSizingEngine.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\AtrSizingEngine.cs` |
| `TradeCopierAddOn.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\TradeCopierAddOn.cs` |
| `CopyEngineTests.cs` | `c:\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngineTests.cs` |

**DO NOT TOUCH**: `TradeCopierPanel.cs`, `TradeCopierWindow.cs`, `CopyEngine.cs`, any `.md` files.

---

### Edit A — AtrSizingEngine.cs line 45

**Old code (exact)**:
```csharp
        private double _maxRiskDollars  = 150.0;
```

**New code (exact)**:
```csharp
        private double _maxRiskDollars  = 200.0;
```

**Constraint**: Plain `double` field — no `volatile` keyword. NT8-003 compliant.
Single-writer UI thread; no threading model change required.

---

### Edit B — AtrSizingEngine.cs line 50

**Old code (exact)**:
```csharp
        private double _atrFraction = 1.0;
```

**New code (exact)**:
```csharp
        private double _atrFraction = 0.75;
```

**Constraint**: Plain `double` field — no `volatile` keyword. NT8-003 compliant.

---

### Edit C — TradeCopierAddOn.cs lines 201-202

**Old code (exact — lines 201-202 before fix)**:
```csharp
            engine.SetParameters(150.0, pointValue);
            _atrEngines[chart] = engine;
```

**New code (exact — lines 201-203 after fix)**:
```csharp
            engine.SetParameters(200.0, pointValue);
            engine.SetAtrFraction(0.75);
            _atrEngines[chart] = engine;
```

**Notes**:
- `SetAtrFraction` is already declared at `AtrSizingEngine.cs` line 121:
  `internal void SetAtrFraction(double fraction)` — no new method required.
- The insertion shifts `_atrEngines[chart] = engine;` from line 202 to line 203.
- `CYC` of `StartAtrEngine` after change: still `3` (the new straight-line call adds no branch).
- JS-021 compliant: no `lock()` introduced.

---

### New [Fact] — CopyEngineTests.cs

**Method signature**:
```csharp
[Fact]
public void CalcContracts_DefaultValues_Use200Risk_075Fraction()
```

**Full method body to append inside the `CopyEngineTests` class**:
```csharp
        [Fact]
        public void CalcContracts_DefaultValues_Use200Risk_075Fraction()
        {
            // Arrange: construct engine with NO SetParameters or SetAtrFraction calls.
            var engine = new AtrSizingEngine();

            // Read the actual default field values via reflection.
            // NOTE: the class-level GetField() helper (line 18-19) is hard-bound to
            // typeof(CopyEngine) -- cannot reuse. Use typeof(AtrSizingEngine) directly.
            double fraction = (double)typeof(AtrSizingEngine)
                .GetField("_atrFraction",    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(engine);
            double maxRisk = (double)typeof(AtrSizingEngine)
                .GetField("_maxRiskDollars", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(engine);

            // Act: call the pure static method with the engine's actual defaults.
            const double atrPoints  = 10.0;
            const double tickDollar = 5.0;
            int lhs = AtrSizingEngine.CalcContracts(atrPoints * fraction, maxRisk, tickDollar);

            // Baseline: explicit values that the spec mandates as the correct defaults.
            int rhs = AtrSizingEngine.CalcContracts(atrPoints * 0.75, 200.0, tickDollar);

            // Assert: defaults match spec; both sides compute 5.
            Assert.Equal(rhs, lhs);
        }
```

**Placement**: Append as the final `[Fact]` method before the closing `}` of the
`CopyEngineTests` class. Do not insert into the middle of the class.

**Math verification**:
- `atrPoints * fraction = 10.0 * 0.75 = 7.5`
- `riskPerContract = 7.5 * 5.0 = 37.5`
- `contracts = floor(200.0 / 37.5) = floor(5.333) = 5`
- Both `lhs` and `rhs` must equal `5`.

**Red-before / green-after**:
| State | `_atrFraction` | `_maxRiskDollars` | `lhs` | `rhs` | Result |
|-------|----------------|-------------------|-------|-------|--------|
| Before fix | 1.0 | 150.0 | `CalcContracts(10.0, 150.0, 5.0)` = 3 | 5 | **FAIL** |
| After fix  | 0.75 | 200.0 | `CalcContracts(7.5, 200.0, 5.0)` = 5 | 5 | **PASS** |

**CYC**: 1 (straight-line; no branches in test body).

**xUnit compliance**: Uses `[Fact]` only. No `[Test]`, `[TestMethod]`, NUnit, or MSTest.
`System.Reflection` is already imported on line 7 of `CopyEngineTests.cs`.
`AtrSizingEngine` is in the same `PropTraderTools` namespace — no new `using` required.

---

### 7-Scan Checklist (SCAN-01 through SCAN-07)

Engineer must run all 7 scans against the write-set before marking VERIFY_PASS.
All scans must return 0 violations.

**SCAN-01 — JS-021: No `lock()` usage**
```powershell
Select-String -Path "AtrSizingEngine.cs","TradeCopierAddOn.cs","CopyEngineTests.cs" -Pattern "lock\s*\("
```
Expected: **0 matches**. Rationale: lock() is banned; concurrency handled by DispatcherTimer
(UI thread) and single-writer field design.

**SCAN-02 — JS-033: No `async void`**
```powershell
Select-String -Path "AtrSizingEngine.cs","TradeCopierAddOn.cs","CopyEngineTests.cs" -Pattern "async void "
```
Expected: **0 matches** in changed code. Rationale: no async methods added by this lane.

**SCAN-03 — JS-002: No `return null`**
```powershell
Select-String -Path "AtrSizingEngine.cs","CopyEngineTests.cs" -Pattern "return null"
```
Expected: **0 matches** in `AtrSizingEngine.cs` and `CopyEngineTests.cs`.
Note: pre-existing `return null` in `TradeCopierAddOn.cs` visual-tree helpers is
unchanged and out of scope for this lane; no new `return null` introduced.

**SCAN-04 — NT8-003: No `volatile double`**
```powershell
Select-String -Path "AtrSizingEngine.cs" -Pattern "volatile double"
```
Expected: **0 matches**. Both `_maxRiskDollars` (line 45) and `_atrFraction` (line 50)
must be plain `double`, not `volatile double`.

**SCAN-05 — NT8-004: No `ImmutableDictionary` / `System.Collections.Immutable`**
```powershell
Select-String -Path "AtrSizingEngine.cs","TradeCopierAddOn.cs","CopyEngineTests.cs" -Pattern "ImmutableDictionary|System\.Collections\.Immutable"
```
Expected: **0 matches**.

**SCAN-06 — CYC Complexity: All modified methods ≤ 8**

Manual inspection required:

| Method | File | CYC Before | CYC After | Delta | Compliant |
|--------|------|-----------|----------|-------|-----------|
| `AtrSizingEngine` (field init, no ctor body change) | `AtrSizingEngine.cs` | 1 | 1 | 0 | YES |
| `StartAtrEngine` | `TradeCopierAddOn.cs` | 3 | 3 | 0 | YES |
| `CalcContracts_DefaultValues_Use200Risk_075Fraction` | `CopyEngineTests.cs` | — | 1 | +1 | YES |

All modified methods: CYC ≤ 8. No method exceeds the Jane Street strict standard.

**SCAN-07 — Test framework: No NUnit / MSTest**
```powershell
Select-String -Path "AtrSizingEngine.cs","TradeCopierAddOn.cs","CopyEngineTests.cs" -Pattern "\[Test\]|\[TestMethod\]|NUnit|MSTest"
```
Expected: **0 matches**. Only xUnit `[Fact]` attributes permitted.

---

### Success Criteria

Engineer marks VERIFY_PASS only when ALL of the following are true:

| # | Criterion | Verification method |
|---|-----------|---------------------|
| 1 | `_maxRiskDollars = 200.0` on `AtrSizingEngine.cs` line 45 | Read file — literal value `200.0` present |
| 2 | `_atrFraction = 0.75` on `AtrSizingEngine.cs` line 50 | Read file — literal value `0.75` present |
| 3 | `engine.SetParameters(200.0, pointValue);` on `TradeCopierAddOn.cs` line 201 | Read file — arg is `200.0` |
| 4 | `engine.SetAtrFraction(0.75);` on `TradeCopierAddOn.cs` line 202 | Read file — line present immediately after SetParameters |
| 5 | `[Fact]` `CalcContracts_DefaultValues_Use200Risk_075Fraction` added to `CopyEngineTests.cs` | Read file — method present |
| 6 | Total `[Fact]` count in `CopyEngineTests.cs` = **121** (baseline 120 + 1) | `Select-String -Pattern "\[Fact\]" CopyEngineTests.cs | Measure-Object` → 121 |
| 7 | All 7 scans pass (0 violations each) | Run SCAN-01 through SCAN-07 above |
| 8 | `dotnet build` passes with 0 errors | Run in `c:\WSGTA\universal-or-strategy` |

---

## TICKETS_COMPLETE
