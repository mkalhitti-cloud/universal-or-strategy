# Codacy Custom Coding Standard Setup Guide

## Step-by-Step Instructions for Creating "V12 HFT Standard"

### Step 1: Navigate to Coding Standards

1. Go to: https://app.codacy.com/organizations/gh/malhitticrypto-debug/settings
2. Click on "Coding standards" tab (left sidebar)
3. Click blue "+ Create new standard" button (top right)

### Step 2: Basic Configuration

**Standard Name**: `V12 HFT Standard`

**Description**:
```
Jane Street-aligned HFT patterns for microsecond-latency trading systems.
Optimized for zero-allocation hot paths, FSM/Actor patterns, and lock-free concurrency.
Complexity threshold: 15 (cognitive simplicity over clever abstractions).
```

### Step 3: Select Languages

Check these languages:
- ✅ **C** (for future C extensions)
- ✅ **CPP** (for future C++ extensions)
- ✅ **CSharp** (primary language)

### Step 4: Choose Tools and Patterns

Click "Next: Tools and patterns" button.

#### For CSharp - Roslyn Analyzer Configuration

**Enable these critical patterns** (security + correctness):

| Pattern ID | Name | Why Enabled |
|------------|------|-------------|
| CA2007 | Do not directly await a Task | Prevents deadlocks in async code |
| CA2008 | Do not create tasks without TaskScheduler | Ensures proper task scheduling |
| CA1806 | Do not ignore method results | Catches ignored return values |
| CA2000 | Dispose objects before losing scope | Prevents resource leaks |
| CA2213 | Disposable fields should be disposed | Ensures cleanup |
| CA1001 | Types that own disposable fields should be disposable | Resource management |
| CA1816 | Call GC.SuppressFinalize correctly | Proper finalizer pattern |
| CA2234 | Pass System.Uri objects instead of strings | Type safety for URIs |
| CA1031 | Do not catch general exception types | Specific exception handling |
| CA1303 | Do not pass literals as localized parameters | Localization support |

**Disable these patterns** (conflict with HFT):

| Pattern ID | Name | Why Disabled |
|------------|------|-------------|
| CA1003 | Avoid specifying EventArgs | Struct events for zero allocation |
| CA1822 | Mark members as static | FSM/Actor instance coherence |
| CA1062 | Validate arguments of public methods | Hot path optimization |
| CA1305 | Specify IFormatProvider | Hot path - culture set at entry |
| CA1307 | Specify StringComparison | Hot path - ordinal default |
| CA1308 | Normalize strings to uppercase | Hot path - case handled at entry |
| CA1810 | Initialize reference type static fields inline | Explicit static constructor for clarity |
| CA1812 | Avoid uninstantiated internal classes | False positives with DI/reflection |
| CA1815 | Override equals and operator equals on value types | Not needed for internal structs |
| CA2201 | Do not raise reserved exception types | Allow ArgumentException in hot paths |

### Step 5: Complexity Configuration

**Complexity threshold**: `15`

**Rationale**: Jane Street alignment - functions with cyclomatic complexity >15 are:
- Harder to reason about under microsecond latency constraints
- Exponentially harder to test (2^N paths)
- More prone to race conditions in lock-free code

### Step 6: Duplication Configuration

**Enable duplication detection**: ✅ Yes

**Exclude paths**:
```
tests/**
benchmarks/**
sandbox/**
src/V12_002.Entries.*.cs
```

**Rationale**: Entry files have legitimate duplication (91+ clones) tracked in EPIC-ENTRY-CONSOLIDATION

### Step 7: Apply to Repositories

**Select repositories**:
- ✅ universal-or-strategy

**Make default**: ✅ Yes (apply to new repositories automatically)

### Step 8: Save and Verify

1. Click "Create standard" button
2. Verify standard appears in list
3. Check that "universal-or-strategy" shows "V12 HFT Standard" applied

---

## Gate Policy Configuration (CANNOT EDIT - READ ONLY)

**Issue discovered**: The screenshot shows "Codacy policy is a built-in gate policy. You cannot edit the quality gates defined by this policy."

**Current values** (read-only):
- New issues: 0 (minor+ severity) ✅ Good
- New security issues: 0 (minor+ severity) ✅ Good
- **Complexity: 100** ❌ Too high (should be 15)
- Duplication: 1 cloned block ✅ Good

**Workaround**: Since we cannot edit the built-in gate policy, we'll enforce complexity via:
1. `.codacy.yml` configuration (already set to 15)
2. Local pre-push validation script (already checks complexity ≤15)
3. PR review process (manual check)

**Action**: Accept that Codacy gate policy complexity=100 is read-only, rely on `.codacy.yml` + local validation instead.

---

## Alternative: Create Custom Gate Policy

If you have admin permissions, you might be able to create a NEW gate policy:

1. Go to: https://app.codacy.com/organizations/gh/malhitticrypto-debug/settings/gate
2. Click "+ Create new gate policy"
3. Name: "V12 HFT Gate Policy"
4. Configure:
   - New issues: 0 (minor+ severity)
   - New security issues: 0 (minor+ severity)
   - **Complexity: 15** (Jane Street aligned)
   - Duplication: 1 cloned block
5. Apply to: universal-or-strategy
6. Make default: Yes

**Note**: This may require organization admin permissions. If you don't have access, the `.codacy.yml` + local validation approach is sufficient.

---

## Verification Checklist

After creating the custom standard:

- [ ] Standard appears in Coding Standards list
- [ ] Standard shows "1 repository" applied
- [ ] Repository settings show "V12 HFT Standard" active
- [ ] Next Codacy analysis uses new pattern configuration
- [ ] CA1003, CA1822, CA1062 warnings disappear for hot path files
- [ ] Complexity threshold enforced at 15 (via `.codacy.yml`)

---

## Maintenance

**When to update the standard**:
- New HFT pattern identified (add suppression)
- Jane Street principle changes (update rationale)
- Performance benchmark shows different trade-off (adjust suppressions)
- New C# analyzer rules released (evaluate for HFT compatibility)

**Review frequency**: Quarterly (align with V12 architecture reviews)