# StyleCop CI Strategy - REAPER-EXPANSION Context

**Date**: 2026-05-22  
**Epic**: REAPER-EXPANSION (5-epic run)  
**Goal**: Enable full GitHub compilation by end of epic sequence

## Strategic Error Identified

**MISTAKE**: Disabled StyleCop CI workflow (commit f135505, reverted in 422a9ae)

**Why This Was Wrong**:
- REAPER-EXPANSION is a **5-epic run** with goal of enabling GitHub compilation
- Disabling CI workflows contradicts the epic's end goal
- StyleCop CI failure is a **symptom** of the larger compilation issue, not a standalone problem

## Root Cause Analysis

**Problem**: StyleCop CI workflow fails because:
1. CI environment lacks NinjaTrader DLLs at absolute paths
2. `Linting.csproj` references these DLLs for compilation
3. StyleCop analyzers require full compilation context (type resolution)

**Why Analysis-Only Mode Failed**:
- StyleCop needs type information to validate code
- Type resolution requires assembly references
- Assembly references require NinjaTrader DLLs
- **Conclusion**: StyleCop cannot run without NinjaTrader assemblies

## Correct Solution Path

### Option 1: Install NinjaTrader in CI (RECOMMENDED for Epic Goal)

**Pros**:
- Enables full compilation in CI (aligns with epic goal)
- Matches local environment exactly
- Unblocks StyleCop CI workflow
- Enables future CI enhancements (build artifacts, deployment)

**Cons**:
- Requires NinjaTrader installation automation
- Adds ~2-3 minutes to workflow runtime
- Requires NinjaTrader license handling

**Implementation** (Future Epic):
```yaml
- name: Install NinjaTrader 8
  run: |
    # Download NinjaTrader installer
    # Silent install with license key from secrets
    # Verify installation
```

### Option 2: Mock NinjaTrader Assemblies

**Pros**:
- Lightweight (no full NinjaTrader install)
- Faster CI workflow

**Cons**:
- High maintenance burden (mock all APIs)
- Fragile (breaks on NinjaTrader updates)
- Doesn't enable full compilation (only linting)

### Option 3: Accept StyleCop CI Failure (CURRENT STATE)

**Pros**:
- Zero effort
- Local pre-push hook still enforces StyleCop

**Cons**:
- CI check always fails (lowers PHS)
- Doesn't progress toward epic goal
- Creates noise in PR reviews

## Recommended Action for Current PR

**For PR #1 (REAPER-EXPANSION Phase 2.3)**:
1. **Accept StyleCop CI failure** as known issue
2. **Document in EPIC-QUALITY-DEBT** as future work item
3. **Track as separate epic** (e.g., EPIC-CI-COMPILATION)
4. **Focus on P0 bug fix** (Fleet.cs:240 counter sync)

**Rationale**:
- StyleCop CI fix requires NinjaTrader installation automation (multi-day effort)
- Current PR is focused on REAPER safety hardening
- Mixing infrastructure work with feature work violates separation of concerns
- Local StyleCop enforcement via pre-push hook is sufficient for current epic

## Future Epic: EPIC-CI-COMPILATION

**Scope**:
1. Automate NinjaTrader installation in GitHub Actions
2. Configure license key management (GitHub Secrets)
3. Update all CI workflows to use installed NinjaTrader
4. Enable full compilation + testing in CI
5. Add build artifact generation

**Dependencies**:
- NinjaTrader silent install documentation
- License key access (contact NinjaTrader support)
- GitHub Actions Windows runner configuration

**Estimated Effort**: 2-3 days

## Lessons Learned

1. **Always check epic scope before making infrastructure changes**
2. **Disabling CI checks contradicts quality goals**
3. **Temporary workarounds should be documented as technical debt**
4. **Infrastructure work should be tracked as separate epics**

## Current Status

- ✅ Reverted StyleCop disable (commit 422a9ae)
- ✅ StyleCop CI workflow re-enabled
- ❌ StyleCop CI still failing (expected, documented)
- 📋 Tracked in EPIC-QUALITY-DEBT for future resolution

**PHS Impact**: StyleCop failure keeps PHS at 80% (20/25 checks)

**Merge Decision**: Proceed with PR #1 merge if:
- P0 bug fix verified (Fleet.cs:240)
- Codacy Coverage fixed
- All other checks passing
- StyleCop failure documented as known issue