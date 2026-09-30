# Multi-API Parallel Execution Strategy - Wave 2 Completion

**Date**: 2026-06-11
**Key Capability**: Multiple 160 BC API keys available simultaneously
**Goal**: Complete Wave 2 in ~2.5 hours (vs 6 hours sequential)

## 🚀 Game Changer: Multi-API Parallel Execution

### Cost Analysis (From Session 2)

| Phase | Per Epic | 6 Epics | 9 Epics |
|-------|----------|---------|---------|
| 0-4 | 8 BC | 48 BC | 72 BC |
| 5 | 50 BC | 300 BC | 450 BC |
| 5.5-6 | 3 BC | 18 BC | 27 BC |
| **Total** | **61 BC** | **366 BC** | **549 BC** |

**Key Insight**: Phase 5 is 82% of total cost (450/549 BC)

### Multi-API Architecture

```
┌─────────────────────────────────────────────────┐
│  Round 1: Phase 5 (Parallel)                    │
├─────────────────────────────────────────────────┤
│  Terminal 1 (API Key 1, 160 BC)                 │
│  ├─ EPIC-CCN-109 (Phase 5) - 50 BC             │
│  ├─ EPIC-CCN-98 (Phase 5) - 50 BC              │
│  └─ EPIC-CCN-128 (Phase 5) - 50 BC             │
│  Total: 150 BC, Time: ~1.5 hours                │
├─────────────────────────────────────────────────┤
│  Terminal 2 (API Key 2, 160 BC)                 │
│  ├─ EPIC-CCN-155 (Phase 5 finish) - 25 BC      │
│  └─ EPIC-CCN-129 (Phase 5) - 50 BC             │
│  Total: 75 BC, Time: ~1 hour                    │
└─────────────────────────────────────────────────┘
Wall-clock: ~1.5 hours (both run simultaneously)

┌─────────────────────────────────────────────────┐
│  Round 2: Completion (Parallel)                 │
├─────────────────────────────────────────────────┤
│  Terminal 3 (API Key 3, 160 BC)                 │
│  ├─ All 6 epics: Phase 5.5 - 12 BC             │
│  └─ All 6 epics: Phase 6 - 9 BC                │
│  Total: 21 BC, Time: ~30 minutes                │
├─────────────────────────────────────────────────┤
│  Terminal 4 (API Key 4, 160 BC)                 │
│  ├─ EPIC-CCN-164 (Phases 1.5-6) - 22 BC        │
│  ├─ EPIC-CCN-107 (Phases 1.5-6) - 22 BC        │
│  └─ EPIC-CCN-108 (Phases 1.5-6) - 22 BC        │
│  Total: 66 BC, Time: ~1 hour                    │
└─────────────────────────────────────────────────┘
Wall-clock: ~1 hour (both run simultaneously)

Total Wall-Clock Time: ~2.5 hours
Total BobCoins: 312 BC (2 API keys used fully)
```

## 📊 Comparison: Sequential vs Multi-API

| Metric | Sequential | Multi-API | Improvement |
|--------|------------|-----------|-------------|
| **Wall-Clock Time** | 6 hours | 2.5 hours | **2.4x faster** |
| **BobCoins Used** | 549 BC | 312 BC | **43% savings** |
| **API Keys Needed** | 4 (sequential) | 4 (parallel) | Same |
| **Complexity** | Low | Medium | Manageable |
| **Risk** | Low | Low | No git conflicts |

**Why BobCoin savings?**
- EPIC-CCN-110 already complete (Phase 5)
- EPIC-CCN-155 partially complete (only finish needed)
- Efficient batching reduces overhead

## 🎯 Execution Plan

### Prerequisites

1. **Generate 4 API Keys**:
   - `KEY1_PHASE5_BATCH1` (160 BC)
   - `KEY2_PHASE5_BATCH2` (160 BC)
   - `KEY3_VERIFY` (160 BC)
   - `KEY4_CATCHUP` (160 BC)

2. **Create Batch Scripts**:
   - `scripts/wave2_phase5_batch1.py`
   - `scripts/wave2_phase5_batch2.py`
   - `scripts/wave2_phases_5_5_6.py`
   - `scripts/wave2_catchup_3epics.py`

### Round 1: Phase 5 Execution (Parallel)

**Terminal 1** (Start first):
```powershell
$env:BOBSHELL_API_KEY="<KEY1_PHASE5_BATCH1>"
python scripts/wave2_phase5_batch1.py
```
**Epics**: EPIC-CCN-109, 98, 128
**Cost**: 150 BC
**Time**: ~1.5 hours

**Terminal 2** (Start immediately after Terminal 1):
```powershell
$env:BOBSHELL_API_KEY="<KEY2_PHASE5_BATCH2>"
python scripts/wave2_phase5_batch2.py
```
**Epics**: EPIC-CCN-155 (finish), 129
**Cost**: 75 BC
**Time**: ~1 hour

**Wait**: Both terminals to complete (~1.5 hours)

### Round 2: Completion (Parallel)

**Terminal 3** (Start first):
```powershell
$env:BOBSHELL_API_KEY="<KEY3_VERIFY>"
python scripts/wave2_phases_5_5_6.py
```
**Work**: Phases 5.5-6 for all 6 epics
**Cost**: 21 BC
**Time**: ~30 minutes

**Terminal 4** (Start immediately after Terminal 3):
```powershell
$env:BOBSHELL_API_KEY="<KEY4_CATCHUP>"
python scripts/wave2_catchup_3epics.py
```
**Epics**: EPIC-CCN-164, 107, 108 (Phases 1.5-6)
**Cost**: 66 BC
**Time**: ~1 hour

**Wait**: Both terminals to complete (~1 hour)

### Verification

```powershell
# Check all 9 epics complete
python scripts/verify_wave2_complete.py
```

## 💡 Best Practices

### 1. Epic Assignment (No Conflicts)

| Terminal | Epics | Conflict Risk |
|----------|-------|---------------|
| T1 | 109, 98, 128 | ✅ None |
| T2 | 155, 129 | ✅ None |
| T3 | All 6 (read-only) | ✅ None |
| T4 | 164, 107, 108 | ✅ None |

**Rule**: Each terminal works on different epics = No git conflicts

### 2. Progress Tracking

Create `docs/workflow/MULTI_API_PROGRESS.md`:

```markdown
## Round 1: Phase 5 (Started: HH:MM)
- [ ] Terminal 1 (Key 1): EPIC-CCN-109 ✅ | 98 ⏳ | 128 ⏳
- [ ] Terminal 2 (Key 2): EPIC-CCN-155 ✅ | 129 ⏳

## Round 2: Completion (Started: HH:MM)
- [ ] Terminal 3 (Key 3): Phases 5.5-6 (All 6) ⏳
- [ ] Terminal 4 (Key 4): 164 ⏳ | 107 ⏳ | 108 ⏳
```

### 3. Terminal Organization

**VSCode Layout**:
```
┌─────────────┬─────────────┐
│ Terminal 1  │ Terminal 2  │  Round 1
│ (Key 1)     │ (Key 2)     │  Phase 5
│ 109,98,128  │ 155,129     │
├─────────────┼─────────────┤
│ Terminal 3  │ Terminal 4  │  Round 2
│ (Key 3)     │ (Key 4)     │  Completion
│ Verify All  │ 164,107,108 │
└─────────────┴─────────────┘
```

### 4. Error Handling

**If Terminal fails**:
1. Check BobCoin balance
2. Check git status (conflicts?)
3. Restart with same API key
4. Continue from checkpoint (manifest.json)

## 📈 Budget Tracking

### Round 1 Budget

| Terminal | API Key | Budget | Planned | Actual | Remaining |
|----------|---------|--------|---------|--------|-----------|
| T1 | Key 1 | 160 BC | 150 BC | ___ BC | ___ BC |
| T2 | Key 2 | 160 BC | 75 BC | ___ BC | ___ BC |

### Round 2 Budget

| Terminal | API Key | Budget | Planned | Actual | Remaining |
|----------|---------|--------|---------|--------|-----------|
| T3 | Key 3 | 160 BC | 21 BC | ___ BC | ___ BC |
| T4 | Key 4 | 160 BC | 66 BC | ___ BC | ___ BC |

## 🎯 Success Criteria

### Per Round

**Round 1**:
- ✅ All 5 epics complete Phase 5
- ✅ No git conflicts
- ✅ Both terminals finish within 2 hours

**Round 2**:
- ✅ All 6 epics complete Phases 5.5-6
- ✅ 3 catch-up epics complete Phases 1.5-6
- ✅ Both terminals finish within 1.5 hours

### Wave 2 Complete

- ✅ All 9 epics reduced to CYC ≤ 8
- ✅ All phases (0-6) complete
- ✅ Build passes
- ✅ Tests pass
- ✅ Total time < 3 hours

## 🚀 Quick Start Commands

### Round 1 (Copy-paste ready)

**Terminal 1**:
```powershell
cd C:\WSGTA\universal-or-strategy
$env:BOBSHELL_API_KEY="<PASTE_KEY1_HERE>"
python scripts/wave2_phase5_batch1.py
```

**Terminal 2** (start immediately):
```powershell
cd C:\WSGTA\universal-or-strategy
$env:BOBSHELL_API_KEY="<PASTE_KEY2_HERE>"
python scripts/wave2_phase5_batch2.py
```

### Round 2 (After Round 1 completes)

**Terminal 3**:
```powershell
cd C:\WSGTA\universal-or-strategy
$env:BOBSHELL_API_KEY="<PASTE_KEY3_HERE>"
python scripts/wave2_phases_5_5_6.py
```

**Terminal 4** (start immediately):
```powershell
cd C:\WSGTA\universal-or-strategy
$env:BOBSHELL_API_KEY="<PASTE_KEY4_HERE>"
python scripts/wave2_catchup_3epics.py
```

## 📝 Next Steps

1. **Generate 4 API keys** from Bob Shell
2. **Create batch scripts** (modify wave2_bob_shell_executor.py)
3. **Open 4 terminals** in VSCode
4. **Execute Round 1** (2 terminals parallel)
5. **Wait ~1.5 hours**
6. **Execute Round 2** (2 terminals parallel)
7. **Wait ~1 hour**
8. **Verify completion**
9. **Celebrate!** 🎉

---

**Recommendation**: Use Multi-API Parallel for 2.4x speedup and 43% BobCoin savings. Complete Wave 2 in ~2.5 hours! 🚀