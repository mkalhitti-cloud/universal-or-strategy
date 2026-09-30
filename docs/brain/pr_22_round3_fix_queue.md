# PR #22 Round 3 Fix Queue

**Target**: 100/100 PHS  
**Current**: 85/100 PHS  
**Date**: 2026-06-02

---

## P1 HIGH: Missing Braces (2 issues)

### Fix #1: Add Braces at Line 39

**File**: `src/V12_002.SIMA.Shadow.cs`  
**Line**: 39

**Current Code**:
```csharp
if (!ValidateLeaderPosition(kvp.Value, kvp.Key, stopOrders, out leaderStop))
    continue;
```

**Fixed Code**:
```csharp
if (!ValidateLeaderPosition(kvp.Value, kvp.Key, stopOrders, out leaderStop))
{
    continue;
}
```

**Estimated Time**: 2 minutes

---

### Fix #2: Add Braces at Line 43

**File**: `src/V12_002.SIMA.Shadow.cs`  
**Line**: 43-46

**Current Code**:
```csharp
if (
    !DetectStopPriceChange(kvp.Key, leaderStop.StopPrice, _leaderLastStopPrice, tickSize, out lastKnown)
)
    continue;
```

**Fixed Code**:
```csharp
if (
    !DetectStopPriceChange(kvp.Key, leaderStop.StopPrice, _leaderLastStopPrice, tickSize, out lastKnown)
)
{
    continue;
}
```

**Estimated Time**: 2 minutes

---

## P1 Verification Steps

After applying fixes:

1. **Format Check**:
   ```powershell
   dotnet csharpier check src/V12_002.SIMA.Shadow.cs
   ```
   Expected: Zero issues

2. **Build Check**:
   ```powershell
   dotnet build src/V12_002.csproj
   ```
   Expected: Zero errors

3. **Pre-Push Validation (Fast)**:
   ```powershell
   powershell -File .\scripts\pre_push_validation.ps1 -Fast
   ```
   Expected: All checks pass

---

## P2 LOW: Suppression Verification (4 issues)

### Issue #3: Codacy CYC 9 Re-verification

**Action**: Verify `.codacy.yml` suppression is working

**Steps**:
1. Check `.codacy.yml` line 63:
   ```yaml
   engines:
     metrics:
       exclude_paths:
         - "src/V12_002.SIMA.Shadow.cs"  # Decision #8: CYC 9 vs threshold 15
   ```

2. If suppression not working:
   - Option A: Fix `.codacy.yml` syntax
   - Option B: Suppress via Codacy web UI
   - Option C: Document as known limitation

**Estimated Time**: 5 minutes

---

### Issues #4-6: Static Method Suppressions

**Action**: Document Decision #11 and suppress in SonarCloud

**Methods**:
- `ValidateLeaderPosition` (line 69)
- `DetectStopPriceChange` (line 109)
- `ValidateCachedEntry` (line 154)

**Rationale**: Intentional design for DI testability (see forensics report)

**Steps**:
1. Create Decision #11 in suppression queue
2. Add SonarCloud suppressions via web UI
3. Tag with "Intentionality" + "Jane Street: Make dependencies explicit"

**Estimated Time**: 10 minutes

---

## Execution Order

1. ✅ **P1 Fix #1**: Add braces at line 39 (2 min)
2. ✅ **P1 Fix #2**: Add braces at line 43 (2 min)
3. ✅ **P1 Verify**: Run CSharpier + Build + Pre-push (3 min)
4. ⏸️ **P2 Issue #3**: Re-verify Codacy suppression (5 min)
5. ⏸️ **P2 Issues #4-6**: Document + suppress static methods (10 min)

**Total P1 Time**: ~7 minutes  
**Total P2 Time**: ~15 minutes  
**Total Time**: ~22 minutes

---

## Success Criteria

**P1 (MUST ACHIEVE)**:
- [ ] Both brace fixes applied
- [ ] CSharpier check passes
- [ ] Build succeeds
- [ ] Pre-push validation passes (fast mode)

**P2 (CONDITIONAL)**:
- [ ] Codacy CYC suppression verified
- [ ] Decision #11 documented
- [ ] SonarCloud suppressions added

**Final Target**:
- [ ] PHS reaches 100/100 after push

---

**Queue Generated**: 2026-06-02  
**Status**: READY FOR EXECUTION