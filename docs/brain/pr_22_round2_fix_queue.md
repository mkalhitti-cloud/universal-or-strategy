# PR #22 Round 2 Fix Queue

**Priority Order**: P0 → P1 → P2

---

## P0 CRITICAL (2 issues)

### 1. SonarCloud + CodeRabbit: Missing Assertion in Test
**File**: `tests/V12_Performance.Tests/Shadow/ShadowPropagateStopMovesTests.cs`  
**Test**: `Test_PropagateAndCacheStopPrice_NullOrder_DoesNotThrow`  
**Line**: Test method body

**Issue**: Test has no assertions - will always pass.

**Fix**:
```csharp
[Fact]
public void Test_PropagateAndCacheStopPrice_NullOrder_DoesNotThrow()
{
    // Arrange
    var cache = new ConcurrentDictionary<string, double>();
    
    // Act
    var ex = Record.Exception(() => 
    {
        // Call helper with null order scenario
        // (requires production code access via reflection or test harness)
    });
    
    // Assert
    Assert.Null(ex);
    Assert.Empty(cache);
}
```

**Estimated Effort**: 30 minutes (requires test harness setup)

---

### 2. gitar-bot: Test Architecture Flaw
**File**: `tests/V12_Performance.Tests/Shadow/ShadowPropagateStopMovesTests.cs`  
**All Tests**: Lines 77-131

**Issue**: Tests use local helper copies instead of production code. They only assert hardcoded constants, providing zero coverage.

**Root Cause**: Tests were scaffolded as "placeholders" but never connected to actual `src/V12_002.SIMA.Shadow.cs` helpers.

**Impact**: 
- False sense of security (tests always pass)
- No verification of extracted helper behavior
- Production bugs will not be caught

**Fix Options**:

**Option A: Refactor to Call Production Code (Recommended)**
```csharp
[Fact]
public void Test_ValidateLeaderPosition_NullPosition_ReturnsFalse()
{
    // Arrange
    var strategy = new V12_002(); // Requires test harness
    var stopOrders = new ConcurrentDictionary<string, Order>();
    
    // Act
    var result = strategy.ValidateLeaderPosition(
        pos: null,
        entryKey: "test",
        stopOrders: stopOrders,
        out Order leaderStop
    );
    
    // Assert
    Assert.False(result);
    Assert.Null(leaderStop);
}
```

**Option B: Document Gap Until NT8 Harness Exists**
```markdown
# Test Coverage Gap

The following helpers are currently untested due to NinjaTrader 8 runtime dependencies:
- ValidateLeaderPosition
- DetectStopPriceChange
- PropagateAndCacheStopPrice
- ValidateCachedEntry

**Mitigation**: Manual testing in NinjaTrader 8 environment required.
**Future**: Implement NT8 test harness to enable unit testing.
```

**Option C: Remove Placeholder Tests**
- Delete all 4 placeholder test methods
- Add README documenting test gap
- Prevents false confidence from always-passing tests

**Recommended**: Option C (remove placeholders) until NT8 test harness exists, then implement Option A.

**Estimated Effort**: 
- Option A: 4 hours (requires NT8 test harness)
- Option B: 15 minutes (documentation only)
- Option C: 10 minutes (delete + document)

---

## P1 HIGH (3 issues)

### 3. Codacy: Missing Braces at Lines 39, 43
**File**: `src/V12_002.SIMA.Shadow.cs`  
**Lines**: 39, 43

**Issue**: Single-line if statements missing curly braces (V12 DNA violation).

**Fix**:
```csharp
// Line 39
if (condition)
{
    return false;
}

// Line 43
if (condition)
{
    return false;
}
```

**Note**: CSharpier should have caught this. Verify `.csharpierrc.json` configuration after fix.

**Estimated Effort**: 5 minutes

---

### 4. gitar-bot: Test Name Mismatch
**File**: `tests/V12_Performance.Tests/Shadow/ShadowPropagateStopMovesTests.cs`  
**Test**: `Test_DetectStopPriceChange_ZeroTickSize_ReturnsFalse`

**Issue**: Test name says "ReturnsFalse" but assertion checks for `True`.

**Fix Option A (Rename Test)**:
```csharp
[Fact]
public void Test_DetectStopPriceChange_ZeroTickSize_ReturnsTrue()
{
    // ... existing test body
    Assert.True(result);
}
```

**Fix Option B (Fix Assertion)**:
```csharp
[Fact]
public void Test_DetectStopPriceChange_ZeroTickSize_ReturnsFalse()
{
    // ... existing test body
    Assert.False(result);
}
```

**Recommended**: Option A (rename test) - matches current assertion logic.

**Estimated Effort**: 2 minutes

---

## P2 LOW (5 issues)

### 5. Codacy: Seal Test Mock Classes
**File**: `tests/V12_Performance.Tests/Shadow/ShadowPropagateStopMovesTests.cs`  
**Classes**: `MockPositionInfo`, `MockOrder`

**Issue**: Test mock classes should be sealed (no inheritance needed).

**Fix**:
```csharp
internal sealed class MockPositionInfo
{
    // ... existing implementation
}

internal sealed class MockOrder
{
    // ... existing implementation
}
```

**Estimated Effort**: 2 minutes

---

### 6. CodeScene: Extract Test Fixture
**File**: `tests/V12_Performance.Tests/Shadow/ShadowPropagateStopMovesTests.cs`  
**Method**: `StickyState_RoundTrip_PreservesState` (70 lines)

**Issue**: Test method too long - should extract fixture setup.

**Fix**:
```csharp
private class StickyStateFixture
{
    public ConcurrentDictionary<string, double> Cache { get; }
    public string EntryKey { get; }
    public double InitialPrice { get; }
    
    public StickyStateFixture()
    {
        Cache = new ConcurrentDictionary<string, double>();
        EntryKey = "test_entry";
        InitialPrice = 100.0;
        Cache[EntryKey] = InitialPrice;
    }
}

[Fact]
public void StickyState_RoundTrip_PreservesState()
{
    // Arrange
    var fixture = new StickyStateFixture();
    
    // Act & Assert
    // ... use fixture properties
}
```

**Estimated Effort**: 15 minutes

---

### 7-9. CodeFactor: Missing Periods in XML Doc Comments
**File**: `src/V12_002.SIMA.Shadow.cs`  
**Locations**: 3 XML summary tags

**Issue**: Three XML documentation comments missing trailing periods.

**Fix**: Add periods to end of each summary tag.

**Example**:
```csharp
/// <summary>
/// Validates leader position eligibility.
/// </summary>
```

**Estimated Effort**: 3 minutes (1 minute per comment)

---

## Execution Order

**Phase 1: Critical Fixes (P0)**
1. Fix P0 #2 (Test architecture) - Choose Option C (remove placeholders)
2. Fix P0 #1 (Missing assertion) - Will be removed with Option C

**Phase 2: High Priority (P1)**
3. Fix P1 #3 (Missing braces) - Run CSharpier
4. Fix P1 #4 (Test name mismatch) - Rename test

**Phase 3: Low Priority (P2)**
5. Fix P2 #5 (Seal mock classes)
6. Fix P2 #6 (Extract fixture) - Optional if tests removed
7. Fix P2 #7-9 (XML doc periods)

---

## Estimated Total Effort

**If removing placeholder tests (Recommended)**:
- P0: 10 minutes (remove tests + document gap)
- P1: 7 minutes (braces + rename)
- P2: 5 minutes (XML docs only)
- **Total**: 22 minutes

**If keeping tests and fixing architecture**:
- P0: 4.5 hours (NT8 harness + real tests)
- P1: 7 minutes
- P2: 20 minutes (all fixes)
- **Total**: ~5 hours

---

## Verification Checklist

After fixes:
- [ ] Run `dotnet csharpier format src/`
- [ ] Run `dotnet build` (zero errors)
- [ ] Run `dotnet test` (all tests pass)
- [ ] Run `powershell -File .\scripts\pre_push_validation.ps1 -Fast`
- [ ] Verify PHS = 100/100

---

**Queue Generated**: 2026-06-02  
**Status**: READY FOR EXECUTION