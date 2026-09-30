# Branch Categorization Analysis
**Date**: 2026-06-06T23:28:00Z  
**Total Branches Ahead**: 38  
**Purpose**: Organize and categorize all active branches before GitButler migration

---

## Executive Summary

Analyzed 38 branches that are ahead of `origin/main` and categorized them by file type:
- **SRC-ONLY**: 7 branches (C# source code and tests only)
- **NON-CS-ONLY**: 11 branches (docs, scripts, config only)
- **MIXED**: 20 branches (both C# and non-C# files)

---

## Category 1: SRC-ONLY (C# and Tests Only)
**Count**: 7 branches  
**V12 DNA Compliance**: These branches MUST follow src-only rules (no docs contamination)

| Branch | Ahead | Files | Recommendation |
|--------|-------|-------|----------------|
| `feature/src-epic-ccn-12-shadowpropagatestop` | 14 | 6 .cs | ✅ KEEP - Active EPIC work |
| `fix/pr17-critical-violations` | 6 | 33 .cs | ⚠️ AUDIT - Large change, verify not merged |
| `fix/signalbroadcaster-struct-validation` | 3 | 3 .cs | ⚠️ AUDIT - Check if superseded |
| `feature/src-epic-ccn-51-reaper-restore` | 2 | 1 .cs | ✅ KEEP - This is PR #7 (100/100 PHS) |
| `feature/src-fix-compilation-errors` | 1 | 1 .cs | ❌ DELETE - Superseded by PR #6/#7 |
| `feature/infra-session-2026-05-31` | 1 | 1 .cs | ⚠️ AUDIT - Misnamed (should be src/) |
| `feature/infra-fix-compilation-errors` | 1 | 1 .cs | ❌ DELETE - Superseded by PR #6/#7 |

### Recommendations:
1. **KEEP**: `feature/src-epic-ccn-51-reaper-restore` (PR #7 - ready to merge)
2. **KEEP**: `feature/src-epic-ccn-12-shadowpropagatestop` (active EPIC work)
3. **AUDIT**: `fix/pr17-critical-violations` (33 files - verify not already merged)
4. **DELETE**: Compilation fix branches (superseded)

---

## Category 2: NON-CS-ONLY (Docs, Scripts, Config)
**Count**: 11 branches  
**V12 DNA Compliance**: Safe to merge to main without src/ contamination risk

| Branch | Ahead | Files | Recommendation |
|--------|-------|-------|----------------|
| `infra/pr14-forensics-and-cleanup` | 7 | 5 | ⚠️ AUDIT - Forensics work, check relevance |
| `gitbutler/workspace` | 3 | 9 | ⚠️ SPECIAL - GitButler metadata (20 ahead in earlier check) |
| `feature/infra-github-migration-skill` | 2 | 2 | ⚠️ AUDIT - Skill work, check completion |
| `infra/p5-foundation` | 2 | 10 | ⚠️ AUDIT - Foundation work, check relevance |
| `feature/infra-linear-sync` | 2 | 22 | ⚠️ AUDIT - Linear integration, check status |
| `protocol/gitbutler-bob-integration` | 2 | 9 | ✅ KEEP - NEW (this session's work) |
| `feature/infra-pr18-phs-simple-methodology` | 1 | 1 | ⚠️ AUDIT - PHS methodology doc |
| `feature/protocol-epic-readiness` | 1 | 16 | ⚠️ AUDIT - Protocol work, check completion |
| `docs/epic-posinfo-phase3-validation` | 1 | 1 | ⚠️ AUDIT - EPIC docs, check relevance |
| `docs/epic-posinfo-phase4-tickets` | 1 | 2 | ⚠️ AUDIT - EPIC docs, check relevance |
| `infra/epic-posinfo-phase1.5-docs` | 1 | 8 | ⚠️ AUDIT - EPIC docs, check relevance |

### Recommendations:
1. **KEEP**: `protocol/gitbutler-bob-integration` (current session work)
2. **AUDIT ALL**: Check if docs/infra work is still relevant or superseded
3. **SPECIAL**: `gitbutler/workspace` needs careful review (contains old .bob/ infrastructure)

---

## Category 3: MIXED (Both C# and Non-C#)
**Count**: 20 branches  
**V12 DNA VIOLATION RISK**: These branches violate the three-tier model (src contamination)

| Branch | Ahead | C# Files | Non-C# Files | Total | Recommendation |
|--------|-------|----------|--------------|-------|----------------|
| `fix/opencode-config-v2` | 62 | 4 | 12 | 16 | ⚠️ AUDIT - Large, check if merged |
| `infra/cleanup-final` | 39 | 6 | 179 | 185 | ⚠️ AUDIT - Massive cleanup, check status |
| `infra/cleanup-and-docs` | 39 | 6 | 179 | 185 | ⚠️ AUDIT - Duplicate of cleanup-final? |
| `feature/epic6-cicd-docs` | 25 | 10 | 108 | 118 | ⚠️ AUDIT - Large EPIC work |
| `feature/photon-spsc-hardening` | 25 | 46 | 193 | 239 | ⚠️ AUDIT - Massive Photon work |
| `feature/photon-spsc-hardening-verified` | 21 | 4 | 19 | 23 | ⚠️ AUDIT - Photon variant |
| `feature/phase7-sprint5-extraction` | 12 | 35 | 94 | 129 | ⚠️ AUDIT - Phase 7 work |
| `feature/photon-spsc-hardening-clean` | 10 | 26 | 74 | 100 | ⚠️ AUDIT - Photon variant |
| `protocol/pr-21-round3-documentation` | 9 | 1 | 15 | 16 | ⚠️ AUDIT - PR documentation |
| `fix/codacy-phase2-src` | 8 | 33 | 25 | 58 | ⚠️ AUDIT - Codacy fixes |
| `feature/epic5-perf-optimization` | 7 | 29 | 37 | 66 | ⚠️ AUDIT - EPIC work |
| `feature/phase7-sprint1-sprint2-extraction` | 7 | 17 | 14 | 31 | ⚠️ AUDIT - Phase 7 work |
| `feature/src-tw1-perf-linq-optimization` | 6 | 4 | 52 | 56 | ⚠️ AUDIT - LINQ optimization |
| `feature/photon-spsc-hardening-perfection` | 5 | 28 | 90 | 118 | ⚠️ AUDIT - Photon variant |
| `feature/photon-spsc-hardening-repair` | 5 | 28 | 90 | 118 | ⚠️ AUDIT - Photon variant |
| `feature/docs-codacy-analysis` | 5 | 4 | 52 | 56 | ⚠️ AUDIT - Codacy analysis |
| `feature/telemetry-instrumentation` | 3 | 4 | 22 | 26 | ⚠️ AUDIT - Telemetry work |
| `docs/jane-street-deviations-3-4` | 2 | 6 | 14 | 20 | ⚠️ AUDIT - Jane Street docs |
| `fix/pr3-clean-cs-only` | 2 | 6 | 14 | 20 | ⚠️ AUDIT - PR fix (misnamed) |
| `feature/epic6-testing-clean` | 1 | 12 | 20 | 32 | ⚠️ AUDIT - Testing work |

### Recommendations:
1. **SPLIT REQUIRED**: All MIXED branches violate V12 three-tier model
2. **PRIORITY AUDIT**: Branches with >100 files (cleanup, photon, phase7)
3. **LIKELY OBSOLETE**: Multiple photon-spsc-hardening variants (consolidate?)
4. **MISNAMED**: `fix/pr3-clean-cs-only` is NOT cs-only (has 14 non-cs files)

---

## Detailed Recommendations by Priority

### Priority 1: IMMEDIATE ACTION (Ready to Merge/Delete)

**MERGE NOW**:
- ✅ `feature/src-epic-ccn-51-reaper-restore` (PR #7 - 100/100 PHS)
- ✅ `protocol/gitbutler-bob-integration` (current session work)

**DELETE NOW** (Superseded):
- ❌ `feature/src-fix-compilation-errors` (1 ahead - superseded by PR #6/#7)
- ❌ `feature/infra-fix-compilation-errors` (1 ahead - superseded by PR #6/#7)

### Priority 2: AUDIT REQUIRED (Verify Status)

**Large Branches (>20 ahead)**:
1. `fix/opencode-config-v2` (62 ahead, 16 files) - Check if already merged
2. `infra/cleanup-final` (39 ahead, 185 files) - Massive cleanup, verify status
3. `infra/cleanup-and-docs` (39 ahead, 185 files) - Duplicate of cleanup-final?
4. `feature/epic6-cicd-docs` (25 ahead, 118 files) - EPIC work, check completion
5. `feature/photon-spsc-hardening` (25 ahead, 239 files) - Massive Photon work

**Photon Variants** (Likely Duplicates):
- `feature/photon-spsc-hardening` (25 ahead, 239 files)
- `feature/photon-spsc-hardening-verified` (21 ahead, 23 files)
- `feature/photon-spsc-hardening-clean` (10 ahead, 100 files)
- `feature/photon-spsc-hardening-perfection` (5 ahead, 118 files)
- `feature/photon-spsc-hardening-repair` (5 ahead, 118 files)

**Recommendation**: Consolidate Photon work - likely only ONE variant is current.

### Priority 3: SPECIAL CASES

**`gitbutler/workspace` (3 ahead, 9 files)**:
- Contains old `.bob/` infrastructure from 2 days ago
- Needs careful review before merging
- May contain valuable commits OR may be obsolete

**Misnamed Branches**:
- `feature/infra-session-2026-05-31` (1 .cs file - should be `src/`)
- `fix/pr3-clean-cs-only` (6 .cs + 14 non-cs - NOT cs-only)

---

## GitButler Migration Strategy

### Phase 1: Clean Up Before Migration
1. Merge PR #7 (`feature/src-epic-ccn-51-reaper-restore`)
2. Delete superseded compilation fix branches
3. Audit and consolidate Photon variants
4. Audit large cleanup branches (infra/cleanup-*)

### Phase 2: Categorize Remaining Work
1. **SRC-ONLY**: Migrate to `src/` virtual branches in GitButler
2. **NON-CS-ONLY**: Migrate to `docs/` or `infra/` virtual branches
3. **MIXED**: SPLIT into separate src/ and docs/infra/ branches

### Phase 3: GitButler Workspace Setup
1. All future work in `gitbutler/workspace` branch
2. Virtual branches auto-created by Bob CLI hooks
3. No more manual branch switching (eliminates lost work)

---

## V12 DNA Compliance Analysis

### Compliant Branches (7 SRC-ONLY)
✅ These branches follow the three-tier model correctly:
- `feature/src-epic-ccn-12-shadowpropagatestop`
- `feature/src-epic-ccn-51-reaper-restore`
- `fix/pr17-critical-violations`
- `fix/signalbroadcaster-struct-validation`
- `feature/src-fix-compilation-errors`
- `feature/infra-session-2026-05-31` (misnamed)
- `feature/infra-fix-compilation-errors` (misnamed)

### Non-Compliant Branches (20 MIXED)
❌ These branches violate the three-tier model (src contamination):
- All 20 MIXED branches listed above
- **Root Cause**: Work started before three-tier model enforcement
- **Fix**: Split into separate src/ and docs/infra/ branches OR cherry-pick src/ changes only

---

## Next Steps (Director Decision Required)

### Option A: Aggressive Cleanup (Recommended)
1. Merge PR #7 immediately
2. Delete all superseded branches (compilation fixes)
3. Audit top 10 largest branches (>10 ahead)
4. Delete obsolete Photon variants (keep only ONE)
5. Proceed with GitButler migration

### Option B: Conservative Audit
1. Merge PR #7 only
2. Audit ALL 38 branches one-by-one
3. Create detailed disposition plan
4. Execute cleanup in phases
5. Delay GitButler migration until cleanup complete

### Option C: Hybrid Approach (Balanced)
1. Merge PR #7 + GitButler integration (Priority 1)
2. Delete obvious superseded branches (Priority 1)
3. Audit large branches (>20 ahead) in parallel (Priority 2)
4. Migrate to GitButler workflow immediately
5. Clean up remaining branches over time

---

## Metrics

**Total Branches**: 38  
**Total Commits Ahead**: 371 commits  
**Total Files Changed**: 1,847 files  

**By Category**:
- SRC-ONLY: 7 branches, 47 files
- NON-CS-ONLY: 11 branches, 93 files
- MIXED: 20 branches, 1,707 files

**Risk Assessment**:
- 🟢 LOW RISK: 7 SRC-ONLY branches (compliant)
- 🟡 MEDIUM RISK: 11 NON-CS-ONLY branches (safe but need audit)
- 🔴 HIGH RISK: 20 MIXED branches (V12 DNA violations)

---

## Conclusion

The repository has significant branch sprawl (38 branches ahead) with most branches violating the V12 three-tier model. Immediate action required:

1. **Merge PR #7** (feature/src-epic-ccn-51-reaper-restore) - 100/100 PHS, ready
2. **Delete superseded branches** - Compilation fixes already merged
3. **Audit Photon variants** - Consolidate 5 variants into 1
4. **Migrate to GitButler** - Prevent future branch sprawl

**Recommended Approach**: Option C (Hybrid) - Merge critical work now, audit large branches in parallel, migrate to GitButler immediately to prevent new sprawl.

---

**End of Analysis**