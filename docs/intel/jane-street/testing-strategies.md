---
type: KnowledgeRule
title: Testing Strategies (xUnit Mandate)
description: V12 test framework mandate — xUnit ONLY. [Fact] and Assert.Equal(). NEVER NUnit or MSTest. Primary reference for Phase 5 execution and Phase 5.V verification.
tags: [testing, xunit, fact, assert, nunit-banned, mstest-banned]
resource: docs/intel/jane-street/building-tools-for-traders.md
timestamp: 2026-06-25T00:00:00Z
---

# Testing Strategies (xUnit Mandate — V12.32)

**MANDATE**: ALL tests MUST use xUnit. NEVER NUnit. NEVER MSTest.
**Violation = BLOCKER**: Phase 5.V will FAIL if NUnit/MSTest patterns are found.

## Required Pattern

```csharp
// CORRECT — xUnit
using Xunit;

public class ExtractedMethodTests
{
    [Fact]
    public void MethodName_Condition_ExpectedResult()
    {
        // Arrange
        var sut = new TargetClass();

        // Act
        var result = sut.ExtractedHelperMethod(input);

        // Assert
        Assert.Equal(expected, result);
    }
}
```

## Banned Patterns (Phase 5.V scans for these)

```csharp
// BANNED — NUnit
[TestFixture]    // ← BANNED
[Test]           // ← BANNED
[TestCase(...)]  // ← BANNED
Assert.That(...) // ← NUnit style, BANNED

// BANNED — MSTest
[TestClass]      // ← BANNED
[TestMethod]     // ← BANNED
```

## xUnit Assertion Methods

| Use | Assert Method |
|-----|--------------|
| Equality | `Assert.Equal(expected, actual)` |
| True/False | `Assert.True(condition)` / `Assert.False(condition)` |
| Null | `Assert.Null(obj)` / `Assert.NotNull(obj)` |
| Exception | `Assert.Throws<TException>(() => ...)` |
| Collection | `Assert.Contains(item, collection)` |
| Empty | `Assert.Empty(collection)` |

## Test Coverage Requirements (Wave 7)

Every extracted helper method from Phase 5 MUST have:
- Minimum 1 `[Fact]` test covering the happy path
- Test naming: `MethodName_WhenCondition_ThenExpectedBehavior`
- Test project: `tests/V12_Performance.Tests/` (existing test project)

## Expect Testing (Jane Street Pattern)

From [building-tools-for-traders.md](building-tools-for-traders.md) — expect tests:
- Serialize state machine execution paths to committed text files
- Use `Assert.Equal(File.ReadAllText("expected.txt"), actual.Serialize())`
- Enables differential code review of behavior changes

## Phase 5.V Verification Scan

```bash
# MUST find at least 1 [Fact] test
grep -r "\[Fact\]" tests/ | grep -i "<MethodName>" 

# MUST return zero (no NUnit/MSTest)
grep -r "TestFixture\|\[Test\]\|TestMethod\|TestCase" tests/ | wc -l  # → 0
```

## Cross-References
- [building-tools-for-traders.md](building-tools-for-traders.md) — expect testing pattern
- [complexity-reduction.md](complexity-reduction.md) — one test per extracted helper
