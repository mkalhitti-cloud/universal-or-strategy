# Tickets: DW-LB-SFB-01
# IsBracketLegStatic — Narrow PTT- Prefix Fix — Test Coverage

**Status**: READY FOR ENGINEER
**Defect ID**: DW-LB-SFB-01
**Phase**: 3 — Ticket Generation
**Plan file**: `docs/brain/DW-LB-SFB-01/02-architecture-plan.md` (REVIEW_PASS)
**Plan reviewer approval**: `docs/brain/DW-LB-SFB-01/02-plan-review.md` — REVIEW_PASS
**Author**: ptt-architect
**Date**: 2026-09-08

---

## Context (read before implementing)

The source fix for DW-LB-SFB-01 is **already merged** in commit `1086d9fd` (main, 2026-09-07).
`IsBracketLegStatic` in `src/PropTraderTools/CopyEngine.cs` (L5749-5762) was narrowed from a
broad `StartsWith("PTT-")` clause to two specific clauses
`StartsWith("PTT-STP-Drag-", StringComparison.Ordinal)` and
`StartsWith("PTT-TGT-Drag-", StringComparison.Ordinal)`.

**ptt-engineer writes exactly ONE file**: `tests/PropTraderTools.Tests/IsBracketLegStaticTests.cs`

**ptt-engineer MUST NOT modify**:
- `src/PropTraderTools/CopyEngine.cs` — the fix is already merged. Do not touch.
- Any other existing file.

---

## Ticket DW-LB-SFB-01-T1

**Title**: Add xUnit test coverage for `IsBracketLegStatic` post-fix (11 regression guards)

**Spec Requirement IDs satisfied**:
- `REQ-DW-LB-SFB-01-1`: PTT-BE-Stop must NOT be classified as a bracket leg (regression guard)
- `REQ-DW-LB-SFB-01-2`: PTT-Flatten must NOT be classified as a bracket leg (regression guard)
- `REQ-DW-LB-SFB-01-3`: PTT-Tighten-Stop must NOT be classified as a bracket leg (regression guard)
- `REQ-DW-LB-SFB-01-4`: PTT-STP-Drag-N must be classified as a bracket leg (must remain true)
- `REQ-DW-LB-SFB-01-5`: PTT-TGT-Drag-N must be classified as a bracket leg (must remain true)
- `REQ-DW-LB-SFB-01-6`: Stop1..Stop9 must be classified as a bracket leg (ATM contract)
- `REQ-DW-LB-SFB-01-7`: Target1..Target9 must be classified as a bracket leg (ATM contract)
- `REQ-DW-LB-SFB-01-8`: "Buy STP" / "Sell STP" must be classified as a bracket leg (DW-B134 path)
- `REQ-DW-LB-SFB-01-9`: Null name guard — must return false
- `REQ-DW-LB-SFB-01-10`: xUnit test coverage for all clauses of `IsBracketLegStatic`

---

### FILE TO CREATE

```
tests/PropTraderTools.Tests/IsBracketLegStaticTests.cs
```

No other files are created or modified.

---

### METHOD SIGNATURES IN THE NEW FILE

The test class contains:

1. One `private static` inline mirror method:
   ```csharp
   private static bool IsBracketLegStatic(string? name, bool hasEntrySignal)
   ```

2. Eleven `[Fact]` public void test methods (exact names listed below).

---

### CROSS-TFM CONSTRAINT AND INLINE MIRROR RATIONALE

`PropTraderTools` targets `net48` (NT8 requirement).
`PropTraderTools.Tests` targets `net8.0` (confirmed in `.csproj`).

Cross-TFM `ProjectReference` is impossible. The production method is `private static`,
so reflection would be required to call it directly — which is fragile. The established
project pattern (see `B143Tests.cs`, `CopyEngineB137Tests.cs`, `B140Tests.cs`) is the
**inline mirror**: reproduce the method's logic as a `private static` method in the test
class, decomposing NT8 objects into primitive parameters.

`IsBracketLegStatic(Order order)` in production reads exactly two fields from `Order`:
- `order.FromEntrySignal` (string?) — only null vs non-null matters
- `order.Name` (string?) — full value matters

The mirror decomposes `Order` into `(string? name, bool hasEntrySignal)`.
This is semantically identical to the production method.

---

### EXACT FILE CONTENT TO WRITE

The engineer must create the file with this exact content (character-for-character, no
additions, no removals, no whitespace mutations):

```csharp
// IsBracketLegStaticTests.cs
// xUnit regression tests for IsBracketLegStatic (DW-LB-SFB-01 defect fix).
// Source mirrored: CopyEngine.cs L5749-5762, commit 1086d9fd.
// PropTraderTools.Tests targets net8.0; PropTraderTools targets net48 (NT8 requirement).
// Direct ProjectReference is impossible across TFMs -- inline mirror is the established pattern
// (see B143Tests.cs, CopyEngineB137Tests.cs, B140Tests.cs). IsBracketLegStatic is private
// static in CopyEngine so it cannot be called directly from the test project.
// Framework: xUnit ONLY. NEVER NUnit or MSTest.
using Xunit;

namespace PropTraderTools.Tests
{
    public sealed class IsBracketLegStaticTests
    {
        // ------------------------------------------------------------------
        // Inline mirror -- exact logic of IsBracketLegStatic post-fix (commit 1086d9fd).
        // Source confirmed: CopyEngine.cs L5749-5762.
        // Decompose Order into (string? name, bool hasEntrySignal):
        //   name            = order.Name
        //   hasEntrySignal  = (order.FromEntrySignal != null)
        // If this mirror drifts from production, update it and all affected tests.
        // ------------------------------------------------------------------
        private static bool IsBracketLegStatic(string? name, bool hasEntrySignal)
        {
            if (hasEntrySignal) return true;
            if (name == null) return false;
            return name.StartsWith("Stop")
                || name.StartsWith("Target")
                || name.StartsWith("PTT-STP-Drag-", System.StringComparison.Ordinal)
                || name.StartsWith("PTT-TGT-Drag-", System.StringComparison.Ordinal)
                || name.EndsWith("STP", System.StringComparison.OrdinalIgnoreCase);
        }

        // ------------------------------------------------------------------
        // T1: PTT-STP-Drag-1 -> true
        // Drag stop replacement is a legitimate bracket leg.
        // StartsWith("PTT-STP-Drag-", Ordinal) clause fires.
        // REQ-DW-LB-SFB-01-4
        // ------------------------------------------------------------------
        [Fact]
        public void PTT_STP_Drag_1_ReturnsTrue()
        {
            Assert.True(IsBracketLegStatic("PTT-STP-Drag-1", false));
        }

        // ------------------------------------------------------------------
        // T2: PTT-TGT-Drag-1 -> true
        // Drag target replacement is a legitimate bracket leg.
        // StartsWith("PTT-TGT-Drag-", Ordinal) clause fires.
        // REQ-DW-LB-SFB-01-5
        // ------------------------------------------------------------------
        [Fact]
        public void PTT_TGT_Drag_1_ReturnsTrue()
        {
            Assert.True(IsBracketLegStatic("PTT-TGT-Drag-1", false));
        }

        // ------------------------------------------------------------------
        // T3: PTT-BE-Stop-1 -> false  [KEY REGRESSION GUARD]
        // Pre-fix: StartsWith("PTT-") matched this and returned true, causing
        // HandleBracketChange to fire on PTT-BE-Stop Working events -> PTT-Flatten storm.
        // Post-fix: no clause matches. Must return false.
        // REQ-DW-LB-SFB-01-1
        // ------------------------------------------------------------------
        [Fact]
        public void PTT_BE_Stop_1_ReturnsFalse()
        {
            Assert.False(IsBracketLegStatic("PTT-BE-Stop-1", false));
        }

        // ------------------------------------------------------------------
        // T4: PTT-Flatten -> false  [KEY REGRESSION GUARD]
        // Pre-fix: StartsWith("PTT-") matched this and returned true.
        // Post-fix: no clause matches. Must return false.
        // REQ-DW-LB-SFB-01-2
        // ------------------------------------------------------------------
        [Fact]
        public void PTT_Flatten_ReturnsFalse()
        {
            Assert.False(IsBracketLegStatic("PTT-Flatten", false));
        }

        // ------------------------------------------------------------------
        // T5: PTT-Tighten-Stop -> false  [REGRESSION GUARD]
        // Pre-fix: StartsWith("PTT-") matched this and returned true.
        // Post-fix: no clause matches. Must return false.
        // REQ-DW-LB-SFB-01-3
        // ------------------------------------------------------------------
        [Fact]
        public void PTT_Tighten_Stop_ReturnsFalse()
        {
            Assert.False(IsBracketLegStatic("PTT-Tighten-Stop", false));
        }

        // ------------------------------------------------------------------
        // T6: Stop1 -> true
        // Core NT8 ATM bracket stop name. StartsWith("Stop") clause fires.
        // REQ-DW-LB-SFB-01-6
        // ------------------------------------------------------------------
        [Fact]
        public void Stop1_ReturnsTrue()
        {
            Assert.True(IsBracketLegStatic("Stop1", false));
        }

        // ------------------------------------------------------------------
        // T7: Target1 -> true
        // Core NT8 ATM bracket target name. StartsWith("Target") clause fires.
        // REQ-DW-LB-SFB-01-7
        // ------------------------------------------------------------------
        [Fact]
        public void Target1_ReturnsTrue()
        {
            Assert.True(IsBracketLegStatic("Target1", false));
        }

        // ------------------------------------------------------------------
        // T8: Buy STP -> true
        // NT8 bracket pattern from DW-B134. EndsWith("STP", OrdinalIgnoreCase) fires.
        // REQ-DW-LB-SFB-01-8
        // ------------------------------------------------------------------
        [Fact]
        public void Buy_STP_ReturnsTrue()
        {
            Assert.True(IsBracketLegStatic("Buy STP", false));
        }

        // ------------------------------------------------------------------
        // T9: Entry -> false
        // Entry orders are never bracket legs. No clause matches.
        // REQ-DW-LB-SFB-01-10
        // ------------------------------------------------------------------
        [Fact]
        public void Entry_ReturnsFalse()
        {
            Assert.False(IsBracketLegStatic("Entry", false));
        }

        // ------------------------------------------------------------------
        // T10: null name -> false
        // Null name guard: name == null check returns false before any StartsWith.
        // REQ-DW-LB-SFB-01-9
        // ------------------------------------------------------------------
        [Fact]
        public void NullName_ReturnsFalse()
        {
            Assert.False(IsBracketLegStatic(null, false));
        }

        // ------------------------------------------------------------------
        // T11: null order analog -> false
        // Represents an Order with no FromEntrySignal and null Name.
        // Same input as T10 but documented separately to capture both scenarios
        // named in the spec (null name guard + null order guard).
        // REQ-DW-LB-SFB-01-9
        // ------------------------------------------------------------------
        [Fact]
        public void NullOrderAnalog_ReturnsFalse()
        {
            Assert.False(IsBracketLegStatic(null, false));
        }
    }
}
```

---

### WHAT MUST NOT BE CHANGED

- `src/PropTraderTools/CopyEngine.cs` — **DO NOT MODIFY**. The fix is already merged.
  - `IsBracketLegStatic` (L5749-5762) — DO NOT TOUCH.
  - `IsBracketLeg` (L5769, non-static, instance method) — DO NOT TOUCH (separate method, unrelated).
- Any existing test file — **DO NOT MODIFY**.
- `PropTraderTools.Tests.csproj` — **DO NOT MODIFY** (no new PackageReference needed; no
  ProjectReference to PropTraderTools is added — that would fail due to cross-TFM constraint).

---

### 7-SCAN CHECKLIST (ENGINEER CONTRACT)

The engineer must confirm all 7 scans pass before reporting complete.

**SCAN-01 — lock() grep** (JS-021)
```powershell
Select-String -Path "tests\PropTraderTools.Tests\IsBracketLegStaticTests.cs" -Pattern "lock\s*\("
```
Expected: zero matches. The new file has no concurrency, no state, no lock.

**SCAN-02 — CYC check**
All 11 `[Fact]` methods in the new file have CYC=1 (one `Assert.True`/`Assert.False`, no branching).
The inline mirror `IsBracketLegStatic` has CYC=7 (matches production; confirmed in architecture plan).
Expected: no method in the new file exceeds CYC=8.

**SCAN-03 — ASCII-only check** (project mandate)
```powershell
$bytes = [System.IO.File]::ReadAllBytes("tests\PropTraderTools.Tests\IsBracketLegStaticTests.cs")
($bytes | Where-Object { $_ -gt 127 }).Count
```
Expected: 0. All string literals in the test file are ASCII-only.

**SCAN-04 — NT8 API check**
```powershell
Select-String -Path "tests\PropTraderTools.Tests\IsBracketLegStaticTests.cs" -Pattern "NinjaTrader|Account\.|Order\s+\w|AtmStrategy"
```
Expected: zero matches. The test file uses only `System.StringComparison` (BCL) — no NT8 types.

**SCAN-05 — Build gate**
```powershell
dotnet build tests\PropTraderTools.Tests\PropTraderTools.Tests.csproj --configuration Debug 2>&1
```
Expected: 0 errors, 0 warnings.

**SCAN-06 — Test gate**
```powershell
dotnet test tests\PropTraderTools.Tests\PropTraderTools.Tests.csproj --configuration Debug --no-build 2>&1
```
Expected: all pre-existing tests pass PLUS all 11 new `[Fact]` methods pass (0 failures).
T3 (`PTT_BE_Stop_1_ReturnsFalse`), T4 (`PTT_Flatten_ReturnsFalse`), T5 (`PTT_Tighten_Stop_ReturnsFalse`)
must individually return `Assert.False` — these are the key regression guards.

**SCAN-07 — Sync gate**
```powershell
powershell -File scripts\ptt-sync-and-verify.ps1
```
Expected: 0 DESYNC lines, 0 MISSING lines. Test files do not sync to NT8 — this gate
verifies the existing synced set has not been disturbed.

---

### ACCEPTANCE CRITERIA

All conditions must be true before reporting `TICKET_COMPLETE`:

1. File `tests/PropTraderTools.Tests/IsBracketLegStaticTests.cs` exists and compiles.
2. `dotnet test` reports all 11 new `[Fact]` methods as passed.
3. **T3** (`PTT_BE_Stop_1_ReturnsFalse`) passes — `Assert.False` confirms the fix is in place.
4. **T4** (`PTT_Flatten_ReturnsFalse`) passes — `Assert.False` confirms the fix is in place.
5. **T5** (`PTT_Tighten_Stop_ReturnsFalse`) passes — `Assert.False` confirms fix is in place.
6. All pre-existing tests continue to pass (zero regressions).
7. All 7 scans above return their expected results.
8. `src/PropTraderTools/CopyEngine.cs` git diff is clean — the file must not have been modified.

---

### TRACEABILITY MATRIX

| Test | Req ID | Spec clause verified |
|------|--------|----------------------|
| T1 `PTT_STP_Drag_1_ReturnsTrue` | REQ-DW-LB-SFB-01-4 | StartsWith("PTT-STP-Drag-") fires |
| T2 `PTT_TGT_Drag_1_ReturnsTrue` | REQ-DW-LB-SFB-01-5 | StartsWith("PTT-TGT-Drag-") fires |
| T3 `PTT_BE_Stop_1_ReturnsFalse` | REQ-DW-LB-SFB-01-1 | KEY REGRESSION: pre-fix returned true |
| T4 `PTT_Flatten_ReturnsFalse` | REQ-DW-LB-SFB-01-2 | KEY REGRESSION: pre-fix returned true |
| T5 `PTT_Tighten_Stop_ReturnsFalse` | REQ-DW-LB-SFB-01-3 | REGRESSION: pre-fix returned true |
| T6 `Stop1_ReturnsTrue` | REQ-DW-LB-SFB-01-6 | StartsWith("Stop") fires |
| T7 `Target1_ReturnsTrue` | REQ-DW-LB-SFB-01-7 | StartsWith("Target") fires |
| T8 `Buy_STP_ReturnsTrue` | REQ-DW-LB-SFB-01-8 | EndsWith("STP", OrdinalIgnoreCase) fires |
| T9 `Entry_ReturnsFalse` | REQ-DW-LB-SFB-01-10 | No clause matches entry orders |
| T10 `NullName_ReturnsFalse` | REQ-DW-LB-SFB-01-9 | name==null guard |
| T11 `NullOrderAnalog_ReturnsFalse` | REQ-DW-LB-SFB-01-9 | null order analog (name=null, noSignal) |

---

## Summary

| Field | Value |
|-------|-------|
| Ticket count | 1 |
| Files to create | 1 (`tests/PropTraderTools.Tests/IsBracketLegStaticTests.cs`) |
| Files to modify | 0 |
| New [Fact] methods | 11 |
| Key regression guards | T3, T4, T5 |
| Framework | xUnit 2.6.2 / net8.0 |
| Pattern | Inline mirror (B143 pattern) — no ProjectReference |
| Jane Street violations | None (no lock, CYC=1 per test method, ASCII-only) |
| NT8 API usage in tests | None |
