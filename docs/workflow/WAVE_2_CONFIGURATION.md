# Wave 2 Configuration

**Last Updated**: 2026-06-12  
**Status**: Active

## Obsidian Kanban Board

**Path**: `C:\Users\Mohammed Khalid\Documents\V12-Agent-Vault`  
**Board Name**: `WAVE_2_KANBAN`

**Usage**: All Wave 2 agents must update this Kanban board with their progress:
- Move cards between phases (Pending → Phase 0: Hotspot → Phase 1: Scope → etc.)
- Update card status on completion
- Report blockers in card notes

## API Allocation (IMMUTABLE)

**Total Available**: 1,600 bobcoins (10 APIs × 160 each)  
**Wave 2 Budget**: 1,350 bobcoins (150 per epic × 9 epics)  
**Safety Margin**: 250 bobcoins (15.6%)

| Epic ID | API File | Start Balance | Allocated | Reserve |
|---------|----------|---------------|-----------|---------|
| EPIC-CCN-107 | b (2).json | 160 | 150 | 10 |
| EPIC-CCN-108 | b.json | 160 | 150 | 10 |
| EPIC-CCN-109 | bob (1).json | 160 | 150 | 10 |
| EPIC-CCN-110 | bob (2).json | 160 | 150 | 10 |
| EPIC-CCN-111 | bob (3).json | 160 | 150 | 10 |
| EPIC-CCN-112 | bob (4).json | 160 | 150 | 10 |
| EPIC-CCN-113 | bob (5).json | 160 | 150 | 10 |
| EPIC-CCN-114 | bob (6).json | 160 | 150 | 10 |
| EPIC-CCN-115 | bob.json | 160 | 150 | 10 |
| **RESERVE** | sean.carter.jr@atomicmail.io.json | 160 | 0 | 160 |

**CRITICAL**: Each epic uses 1 unique API. No sharing. No duplicates.

## VM Configuration

**Golden Image**: `v12-bob-shell-golden-v2`  
**Active VM**: `v12-test-golden-v2`  
**Zone**: `us-central1-a`  
**Repository Path**: `/home/malhitticrypto/universal-or-strategy`

## Epic List

```
EPIC-CCN-107|ProcessIpcCommands|76
EPIC-CCN-108|ProcessOnExecutionUpdate|67
EPIC-CCN-109|HydrateFSMsFromWorkingOrders|45
EPIC-CCN-110|HandleFlatPositionUpdate|37
EPIC-CCN-111|AdoptFleetOrders|37
EPIC-CCN-112|ExtractTargetConfiguration|31
EPIC-CCN-113|SweepBrokerOrders|28
EPIC-CCN-114|FlattenSinglePosition|27
EPIC-CCN-115|ExecuteRetestEntry|26
```

## Phase Budget Estimates

| Phase | Bobcoins/Epic | Total (9 epics) |
|-------|---------------|-----------------|
| Phase 0: Hotspot | 3-5 | 27-45 |
| Phase 1: Scope | 5-10 | 45-90 |
| Phase 1.5: Boundary | 2-3 | 18-27 |
| Phase 2: Architecture | 10-15 | 90-135 |
| Phase 3: Audit | 5-10 | 45-90 |
| Phase 4: Tickets | 5-10 | 45-90 |
| **Total** | **30-53** | **270-477** |

**Note**: Phase 5 (Execution) and Phase 6 (Review) will be separate waves.

## Monitoring Commands

### Check Screen Sessions
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="screen -ls"
```

### Check Logs
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="tail -f /home/malhitticrypto/universal-or-strategy/logs/phase0/EPIC-CCN-107.log"
```

### Extract Bobcoin Usage
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="grep -A 2 'BOBCOIN REPORT' /home/malhitticrypto/universal-or-strategy/logs/phase0/*.log"
```

### Verify Files Created
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="ls -lh /home/malhitticrypto/universal-or-strategy/docs/brain/EPIC-CCN-*/00-hotspots.md"
```

## Success Criteria

### Phase 0 Success
- ✅ All 9 screen sessions complete (DONE_EXIT=0)
- ✅ Files created: `docs/brain/EPIC-CCN-{ID}/00-hotspots.md`
- ✅ Files created: `docs/brain/EPIC-CCN-{ID}/manifest.json`
- ✅ Bobcoin usage reported in logs
- ✅ All APIs remain positive (>10 bobcoins)
- ✅ Kanban board updated

### Wave 2 Complete Success
- ✅ All 9 epics complete Phases 0-4
- ✅ All output files exist on disk
- ✅ Total bobcoins used < 1,350
- ✅ No API goes negative
- ✅ Kanban board shows all epics in Phase 4: Complete

## Emergency Procedures

### Stop All Agents
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="killall screen"
```

### Check API Balances
Login to IBM Bob Shell dashboard and verify all APIs remain positive.

### Rollback
If files not created, revert manifest status to "pending" and relaunch.

---

**DO NOT MODIFY** this configuration without Director approval.  
**REFERENCE** this file in every Wave 2 session to avoid repeating setup.