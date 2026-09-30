# Test Framework Protocol (V12.32)

**Version**: 1.0
**Date**: 2026-06-16
**Status**: MANDATORY
**Effective**: Immediately

---

## Critical Rule

**The Universal OR Strategy project uses xUnit 2.9.0+ exclusively for all test code.**

ALL agents (Bob CLI, Codex CLI, Claude, Gemini, etc.) MUST generate xUnit tests ONLY.

---

## xUnit Patterns (MANDATORY)

### Attributes
```csharp
[Fact]                    // Single test case
[Theory]                  // Parameterized test
[InlineData(1, 2, 3)]    // Test data for Theory
```

### Assertions
```csharp
Assert.Equal(expected, actual)
Assert.NotEqual(expected, actual)
Assert.NotNull(obj)
Assert.Null(obj)
Assert.True(condition)
Assert.False(condition)
Assert.Contains(substring, str)
Assert.Throws<TException>(() => action)
```

### Namespace
```csharp
using Xunit;
```

### Test Class Structure
```csharp
using Xunit;

namespace V12_Performance.Tests.Core
{
    public class MyTests  // No [TestFixture] attribute needed
    {
        [Fact]
        public void MyTest_Scenario_ExpectedBehavior()
        {
            // Arrange
            var sut = new SystemUnderTest();
            
            // Act
            var result = sut.DoSomething();
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(42, result.Value);
        }
    }
}
```

---

## NUnit Patterns (BANNED)

### Attributes (DO NOT USE)
```csharp
[Test]           // ❌ Use [Fact] instead
[TestFixture]    // ❌ Not needed in xUnit
[TestCase]       // ❌ Use [Theory] + [InlineData] instead
```

### Assertions (DO NOT USE)
```csharp
Assert.AreEqual(expected, actual)     // ❌ Use Assert.Equal()
Assert.IsNotNull(obj)                 // ❌ Use Assert.NotNull()
Assert.IsNull(obj)                    // ❌ Use Assert.Null()
Assert.IsTrue(condition)              // ❌ Use Assert.True()
Assert.IsFalse(condition)             // ❌ Use Assert.False()
```

### Namespace (DO NOT USE)
```csharp
using NUnit.Framework;  // ❌ Use 'using Xunit;' instead
```

---

## MSTest Patterns (BANNED)

### Attributes (DO NOT USE)
```csharp
[TestMethod]     // ❌ Use [Fact] instead
[TestClass]      // ❌ Not needed in xUnit
```

### Assertions (DO NOT USE)
```csharp
Assert.AreEqual(expected, actual)     // ❌ Use Assert.Equal()
Assert.IsNotNull(obj)                 // ❌ Use Assert.NotNull()
```

### Namespace (DO NOT USE)
```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;  // ❌ Use 'using Xunit;'
```

---

## Verification Protocol

### Before Generating Tests

**ALWAYS verify project test framework first**:

```bash
# Check .csproj for xUnit package
grep "xunit" tests/V12_Performance.Tests/V12_Performance.Tests.csproj

# Expected output:
# <PackageReference Include="xunit" Version="2.9.0" />
# <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
```

### After Generating Tests

**ALWAYS verify test syntax**:

```bash
# Check for NUnit patterns (should return 0 matches)
grep -r "using NUnit.Framework" tests/
grep -r "\[Test\]" tests/
grep -r "\[TestFixture\]" tests/
grep -r "Assert.AreEqual" tests/
grep -r "Assert.IsNotNull" tests/

# Check for xUnit patterns (should find matches)
grep -r "using Xunit" tests/
grep -r "\[Fact\]" tests/
grep -r "Assert.Equal" tests/
grep -r "Assert.NotNull" tests/
```

---

## Conversion Guide (NUnit → xUnit)

### Attributes
| NUnit | xUnit |
|-------|-------|
| `[Test]` | `[Fact]` |
| `[TestFixture]` | (remove - not needed) |
| `[TestCase(1, 2)]` | `[Theory]` + `[InlineData(1, 2)]` |

### Assertions
| NUnit | xUnit |
|-------|-------|
| `Assert.AreEqual(expected, actual)` | `Assert.Equal(expected, actual)` |
| `Assert.AreNotEqual(expected, actual)` | `Assert.NotEqual(expected, actual)` |
| `Assert.IsNotNull(obj)` | `Assert.NotNull(obj)` |
| `Assert.IsNull(obj)` | `Assert.Null(obj)` |
| `Assert.IsTrue(condition)` | `Assert.True(condition)` |
| `Assert.IsFalse(condition)` | `Assert.False(condition)` |
| `Assert.Contains(substring, str)` | `Assert.Contains(substring, str)` (same) |
| `Assert.Throws<T>(() => action)` | `Assert.Throws<T>(() => action)` (same) |

### Namespace
```csharp
// NUnit
using NUnit.Framework;

// xUnit
using Xunit;
```

---

## Root Cause Analysis

### EPIC-027 TICKET-1 Incident (2026-06-16)

**What Happened**:
- Bob CLI on VM generated NUnit tests for `SIMADispatchTests.cs`
- Project uses xUnit 2.9.0+
- Result: 29 compilation errors
- Resolution: Manual conversion (20 minutes)

**Why It Happened**:
- Bob CLI did not verify project test framework before generating tests
- No protocol document specifying xUnit requirement
- No validation in SOP, skill, or custom modes

**Prevention**:
- Created V12.32 Test Framework Protocol (this document)
- Updated SOP V3.2 with xUnit requirement
- Updated v12-engineer custom mode with test framework mandate
- Updated gcp-vm-wave-execution skill with validation steps

---

## Enforcement

### Pre-Generation Checklist

Before generating ANY test code:
- [ ] Verify project uses xUnit (check .csproj)
- [ ] Review xUnit patterns (attributes, assertions)
- [ ] Confirm no NUnit/MSTest patterns will be used

### Post-Generation Checklist

After generating test code:
- [ ] Verify `using Xunit;` namespace
- [ ] Verify `[Fact]` or `[Theory]` attributes
- [ ] Verify `Assert.Equal()`, `Assert.NotNull()`, etc.
- [ ] NO `using NUnit.Framework;`
- [ ] NO `[Test]`, `[TestFixture]` attributes
- [ ] NO `Assert.AreEqual()`, `Assert.IsNotNull()`, etc.
- [ ] Build passes (0 errors)

---

## Success Criteria

### Per Test File
- ✅ Uses xUnit namespace only
- ✅ Uses xUnit attributes only
- ✅ Uses xUnit assertions only
- ✅ Builds without errors
- ✅ Tests pass (if implementation exists)

### Per Epic/Wave
- ✅ All generated tests use xUnit
- ✅ No NUnit/MSTest patterns detected
- ✅ No framework conversion required
- ✅ Zero framework-related compilation errors

---

## References

- **xUnit Documentation**: https://xunit.net/
- **Project Test Framework**: `tests/V12_Performance.Tests/V12_Performance.Tests.csproj`
- **SOP V3.2**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP_V3.md`
- **v12-engineer Mode**: `.bob/custom_modes.yaml`
- **GCP VM Skill**: `.bob/skills/gcp-vm-wave-execution/skill.md`
- **EPIC-027 Incident**: `WAVE4_EPIC_027_BUILD_BLOCKER_RESOLUTION.md`

---

**MANDATORY COMPLIANCE**: All agents MUST follow this protocol for all test generation.

**Violation Consequences**: Compilation errors, manual conversion required, wasted time, delayed epic completion.

**Next Review**: After Wave 4 completion (2026-06-17).