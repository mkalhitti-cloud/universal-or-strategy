# Resource Analysis - Multi-API Parallel Execution

**Date**: 2026-06-11
**Question**: Can laptop handle multiple Bob CLI sessions simultaneously?

## 🔍 Resource Requirements Analysis

### Per Bob CLI Session

Based on previous 3-worker parallel execution (Session 2):

| Resource | Per Session | 2 Sessions | 3 Sessions | 4 Sessions |
|----------|-------------|------------|------------|------------|
| **Memory** | ~500 MB | 1 GB | 1.5 GB | 2 GB |
| **CPU** | 15-25% | 30-50% | 45-75% | 60-100% |
| **Disk I/O** | Low | Low | Medium | Medium |
| **Network** | Minimal | Minimal | Minimal | Minimal |

### Previous Success: 3 Workers

**Session 2 Evidence**:
- ✅ 3 Bob CLI sessions ran successfully
- ✅ No freezing, no crashes
- ✅ Completed Phases 1-4 for 6 epics
- ✅ System remained responsive

**Conclusion**: Laptop can handle **3 concurrent Bob CLI sessions** safely.

## 🎯 Recommended Strategy: 2 Sessions at a Time

### Why 2 Instead of 4?

**Safety Margin**:
- 3 workers = proven stable
- 4 workers = untested, risky
- 2 workers = conservative, guaranteed stable

**Resource Headroom**:
- Leaves 50% CPU for system/VSCode
- Leaves 50% memory for other tasks
- Reduces thermal throttling risk
- Allows monitoring/debugging

### Revised Multi-API Plan (2 Sessions)

```
┌─────────────────────────────────────────────────┐
│  Round 1: Phase 5 Batch 1 (2 parallel)          │
├─────────────────────────────────────────────────┤
│  Terminal 1 (API Key 1, 160 BC)                 │
│  ├─ EPIC-CCN-109 (Phase 5) - 50 BC             │
│  └─ EPIC-CCN-98 (Phase 5) - 50 BC              │
│  Total: 100 BC, Time: ~1 hour                   │
├─────────────────────────────────────────────────┤
│  Terminal 2 (API Key 2, 160 BC)                 │
│  ├─ EPIC-CCN-128 (Phase 5) - 50 BC             │
│  └─ EPIC-CCN-155 (Phase 5 finish) - 25 BC      │
│  Total: 75 BC, Time: ~1 hour                    │
└─────────────────────────────────────────────────┘
Wall-clock: ~1 hour (both run simultaneously)

┌─────────────────────────────────────────────────┐
│  Round 2: Phase 5 Batch 2 (1 session)           │
├─────────────────────────────────────────────────┤
│  Terminal 3 (API Key 3, 160 BC)                 │
│  └─ EPIC-CCN-129 (Phase 5) - 50 BC             │
│  Total: 50 BC, Time: ~30 minutes                │
└─────────────────────────────────────────────────┘
Wall-clock: ~30 minutes

┌─────────────────────────────────────────────────┐
│  Round 3: Verification (2 parallel)             │
├─────────────────────────────────────────────────┤
│  Terminal 4 (API Key 4, 160 BC)                 │
│  ├─ All 6 epics: Phase 5.5 - 12 BC             │
│  └─ All 6 epics: Phase 6 - 9 BC                │
│  Total: 21 BC, Time: ~30 minutes                │
├─────────────────────────────────────────────────┤
│  Terminal 5 (API Key 5, 160 BC)                 │
│  ├─ EPIC-CCN-164 (Phases 1.5-6) - 22 BC        │
│  ├─ EPIC-CCN-107 (Phases 1.5-6) - 22 BC        │
│  └─ EPIC-CCN-108 (Phases 1.5-6) - 22 BC        │
│  Total: 66 BC, Time: ~1 hour                    │
└─────────────────────────────────────────────────┘
Wall-clock: ~1 hour (both run simultaneously)

Total Wall-Clock Time: ~2.5 hours
Total BobCoins: 312 BC (5 API keys, but only 2 concurrent)
```

## 📊 Comparison: 2 vs 4 Concurrent Sessions

| Metric | 4 Concurrent | 2 Concurrent | Winner |
|--------|--------------|--------------|--------|
| **Wall-Clock Time** | 2.5 hours | 2.5 hours | ✅ Tie |
| **CPU Usage** | 60-100% | 30-50% | ✅ 2 Sessions |
| **Memory Usage** | 2 GB | 1 GB | ✅ 2 Sessions |
| **System Stability** | Risky | Proven | ✅ 2 Sessions |
| **Monitoring Ability** | Hard | Easy | ✅ 2 Sessions |
| **BobCoins Used** | 312 BC | 312 BC | ✅ Tie |

**Verdict**: 2 concurrent sessions achieves same completion time with better stability.

## 🚀 Execution Plan (2 Sessions Max)

### Round 1: Phase 5 Batch 1 (2 parallel)

**Terminal 1** (Start first):
```powershell
cd C:\WSGTA\universal-or-strategy
$env:BOBSHELL_API_KEY="<KEY1>"
python scripts/wave2_phase5_batch1.py
# Epics: EPIC-CCN-109, 98
# Cost: 100 BC, Time: ~1 hour
```

**Terminal 2** (Start immediately):
```powershell
cd C:\WSGTA\universal-or-strategy
$env:BOBSHELL_API_KEY="<KEY2>"
python scripts/wave2_phase5_batch2.py
# Epics: EPIC-CCN-128, 155 (finish)
# Cost: 75 BC, Time: ~1 hour
```

**Wait**: ~1 hour for both to complete

### Round 2: Phase 5 Batch 2 (1 session)

**Terminal 3**:
```powershell
cd C:\WSGTA\universal-or-strategy
$env:BOBSHELL_API_KEY="<KEY3>"
python scripts/wave2_phase5_batch3.py
# Epic: EPIC-CCN-129
# Cost: 50 BC, Time: ~30 minutes
```

**Wait**: ~30 minutes

### Round 3: Verification (2 parallel)

**Terminal 4** (Start first):
```powershell
cd C:\WSGTA\universal-or-strategy
$env:BOBSHELL_API_KEY="<KEY4>"
python scripts/wave2_phases_5_5_6.py
# All 6 epics: Phases 5.5-6
# Cost: 21 BC, Time: ~30 minutes
```

**Terminal 5** (Start immediately):
```powershell
cd C:\WSGTA\universal-or-strategy
$env:BOBSHELL_API_KEY="<KEY5>"
python scripts/wave2_catchup_3epics.py
# Epics: EPIC-CCN-164, 107, 108 (Phases 1.5-6)
# Cost: 66 BC, Time: ~1 hour
```

**Wait**: ~1 hour for both to complete

## 💡 Resource Monitoring

### During Execution

**Task Manager Checks** (every 15 minutes):
- CPU: Should stay < 60%
- Memory: Should stay < 8 GB
- Disk: Should stay < 50% active time

**Warning Signs**:
- ⚠️ CPU > 80% sustained = reduce to 1 session
- ⚠️ Memory > 12 GB = reduce to 1 session
- ⚠️ System lag/freezing = reduce to 1 session

### Fallback Plan

If 2 sessions cause issues:
1. Stop both terminals (Ctrl+C)
2. Switch to sequential execution (1 session)
3. Total time: ~4 hours (still better than 6)

## 🎯 Final Recommendation

### Conservative (Recommended): 2 Sessions Max

**Pros**:
- ✅ Proven stable (3 workers succeeded)
- ✅ 50% resource headroom
- ✅ Easy to monitor
- ✅ Same completion time (2.5 hours)
- ✅ Can debug if issues arise

**Cons**:
- None (achieves same time as 4 sessions)

### Aggressive (Not Recommended): 4 Sessions

**Pros**:
- Slightly simpler script organization

**Cons**:
- ❌ Untested configuration
- ❌ 80-100% CPU usage
- ❌ Risk of thermal throttling
- ❌ Hard to debug if issues
- ❌ May cause system lag

## 📋 Updated Budget

| Round | Sessions | API Keys | BobCoins | Time |
|-------|----------|----------|----------|------|
| 1 | 2 parallel | Key 1, 2 | 175 BC | 1 hour |
| 2 | 1 session | Key 3 | 50 BC | 30 min |
| 3 | 2 parallel | Key 4, 5 | 87 BC | 1 hour |
| **Total** | **2 max** | **5 keys** | **312 BC** | **2.5 hours** |

## ✅ Action Items

1. **Generate 5 API keys** (160 BC each)
2. **Create 5 batch scripts** (modified for 2-session batching)
3. **Open 2 terminals** for Round 1
4. **Monitor resources** during execution
5. **Open 1 terminal** for Round 2
6. **Open 2 terminals** for Round 3
7. **Verify completion**

---

**Bottom Line**: Use **2 concurrent sessions maximum** for guaranteed stability while achieving the same 2.5-hour completion time. This is the sweet spot between speed and safety. 🎯