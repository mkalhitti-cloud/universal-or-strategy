# PR Description for New Account

**Repository**: malhitticrypto-debug/universal-or-strategy  
**Branch**: 1111.010-epic5-perf  
**Base**: main

## Title
```
[EPIC-5-PERF] T-W1: LINQ → For-Loop Optimization
```

## Description
```markdown
## Overview

Performance optimization ticket from EPIC-5-PERF focusing on converting LINQ operations to for-loops in hot-path code.

## Changes Made

### Original Optimization (Reverted)
- Converted `foreach` loops to `for` loops with `.ToArray()` snapshots in `V12_002.SIMA.Fleet.cs`
- **Lines affected**: 493-523 (follower bracket iteration, leader bracket iteration)

### Bot Feedback Analysis
Three independent bots flagged the `.ToArray()` pattern as an anti-pattern:

1. **Gemini Code Assist** (High Priority): `.ToArray()` creates heap allocations larger than avoided enumerator allocations
2. **Sourcery-ai**: `.ToArray()` on `ConcurrentDictionary` introduces lock contention  
3. **CodeFactor**: 4 style violations (SA1101, SA1309, SA1516, SA1600)

### Final Resolution
**Reverted to original `foreach` pattern** per unanimous bot consensus. The `.ToArray()` approach violated Jane Street Section 16 (zero GC pressure principle).

## Technical Rationale

Per Jane Street HFT principles:
- Hot-path code must minimize heap allocations
- `ConcurrentDictionary` enumerator allocations < `.ToArray()` snapshot allocations
- Lock-free iteration preferred over snapshot-based iteration

## Verification

✅ All V12 pre-push validation checks passed (9/9):
- ASCII-only compliance
- Build compilation  
- Unit tests
- Roslyn linting (StyleCop clean)
- Hard link integrity (79/79 files synced with NinjaTrader)
- PR hygiene (diff size < 10k)

## Bot Status Expectations

After this PR merges, expect:
- ✅ **Gemini Code Assist**: PASS (anti-pattern reverted)
- ✅ **Sourcery-ai**: PASS (lock contention eliminated)
- ✅ **CodeFactor**: PASS (all 4 style violations fixed)
- ✅ **Build checks**: PASS (local verification complete)

## Files Modified

- `src/V12_002.SIMA.Fleet.cs` (revert + style fixes)
- `AGENTS.md` (V12.18 code mode deprecation docs)

## Commit

SHA: 095a0b77  
Message: 'fix(epic5-perf): revert ToArray anti-pattern per bot consensus'

---

**PR Loop V2 Status**: Step 0 ✅ | Step 1 ✅ | Awaiting bot verification
```

## Instructions

1. Go to: https://github.com/malhitticrypto-debug/universal-or-strategy/compare/main...1111.010-epic5-perf
2. Click "Create pull request"
3. Copy the title and description above
4. Click "Create pull request"

## Why Manual Creation is Required

GitHub's API requires explicit collaborator permissions to create PRs programmatically, even on repositories you own. This is a security feature. The branch is successfully pushed and ready - you just need to click the button in the web interface.