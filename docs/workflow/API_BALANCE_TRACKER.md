# API Balance Tracker - Wave 2 Autonomous Refactoring

**Version**: 1.0  
**Date**: 2026-06-13  
**Initial Budget**: 10 APIs × 160 bobcoins = 1,600 total

---

## Current API Status

| API Key File | Epic Assignment | Initial | Phase 0 | Phase 1 | Phase 1.5 | Phase 2 | Remaining | Status |
|--------------|-----------------|---------|---------|---------|-----------|---------|-----------|--------|
| b (2).json | EPIC-CCN-107 | 160.00 | -3.50 | -0.68 | -0.55* | TBD | ~155.27 | ✅ Healthy |
| b.json | EPIC-CCN-108 | 160.00 | -5.00 | -0.59 | -0.55* | TBD | ~153.86 | ✅ Healthy |
| bob (1).json | EPIC-CCN-109 | 160.00 | -5.00 | -0.58 | -0.55* | TBD | ~153.87 | ✅ Healthy |
| bob (2).json | EPIC-CCN-110 | 160.00 | -5.00 | -1.48 | -0.55* | TBD | ~153.97 | ✅ Healthy |
| bob (3).json | EPIC-CCN-111 | 160.00 | -5.00 | -0.99 | -0.55* | TBD | ~153.46 | ✅ Healthy |
| bob (4).json | EPIC-CCN-112 | 160.00 | -5.00 | -0.81 | -0.55* | TBD | ~153.64 | ✅ Healthy |
| bob (5).json | EPIC-CCN-113 | 160.00 | -5.00 | -0.68 | -0.55* | TBD | ~153.77 | ✅ Healthy |
| bob (6).json | EPIC-CCN-114 | 160.00 | -5.00 | -0.48 | -0.55* | TBD | ~153.97 | ✅ Healthy |
| bob.json | EPIC-CCN-115 | 160.00 | -5.00 | -0.57 | -0.55* | TBD | ~153.88 | ✅ Healthy |
| sean.carter.jr@atomicmail.io.json | RESERVE | 160.00 | 0 | 0 | 0 | TBD | 160.00 | 🔒 Reserve |

**\*Estimated** - Phase 1.5 logs not created due to permission issue

---

## Phase Summary

### Phase 0 (Hotspot Analysis)
- **Total Cost**: $45.00
- **Average per Epic**: $5.00
- **Status**: ✅ Complete (9/9)

### Phase 1 (Scope Definition)
- **Total Cost**: $6.86
- **Average per Epic**: $0.76
- **Status**: ✅ Complete (9/9)

### Phase 1.5 (Scope Boundary Validation)
- **Total Cost**: ~$5.00 (estimated)
- **Average per Epic**: ~$0.55 (estimated)
- **Status**: ✅ Complete (9/9) - logs missing

### Cumulative Usage
- **Total Spent**: ~$56.86
- **Remaining Budget**: ~$1,543.14 (96.4%)
- **Safety Margin**: ✅ Excellent

---

## Epic-to-API Assignment Map

| Epic | Method | API Key | File Path |
|------|--------|---------|-----------|
| EPIC-CCN-107 | HydrateFromOpenPositions | b (2).json | docs/API/b (2).json |
| EPIC-CCN-108 | ProcessOnExecutionUpdate | b.json | docs/API/b.json |
| EPIC-CCN-109 | ProcessOnOrderUpdate | bob (1).json | docs/API/bob (1).json |
| EPIC-CCN-110 | ProcessOnPositionUpdate | bob (2).json | docs/API/bob (2).json |
| EPIC-CCN-111 | ProcessOnAccountItemUpdate | bob (3).json | docs/API/bob (3).json |
| EPIC-CCN-112 | ProcessOnConnectionStatusUpdate | bob (4).json | docs/API/bob (4).json |
| EPIC-CCN-113 | ProcessOnFundamentalData | bob (5).json | docs/API/bob (5).json |
| EPIC-CCN-114 | ProcessOnMarketData | bob (6).json | docs/API/bob (6).json |
| EPIC-CCN-115 | ProcessOnMarketDepth | bob.json | docs/API/bob.json |

---

## Balance Monitoring Protocol

### Thresholds

| Balance Range | Status | Action Required |
|---------------|--------|-----------------|
| >100 | ✅ Healthy | Continue normal operations |
| 50-100 | ⚠️ Monitor | Track closely, prepare backup |
| 20-50 | 🔶 Caution | Reduce parallel execution |
| 10-20 | 🔴 Critical | Stop non-essential work |
| <10 | 🚨 Emergency | STOP ALL WORK, escalate |

### Monitoring Commands

**Check all balances from VM logs:**
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="grep -E 'Balance: [0-9]' /home/malhitticrypto/universal-or-strategy/logs/phase*/EPIC-CCN-*.log"
```

**Extract latest balance per API:**
```bash
# Phase 0
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="grep 'Balance:' /home/malhitticrypto/universal-or-strategy/logs/phase0/EPIC-CCN-*.log | tail -9"

# Phase 1
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="grep 'Balance:' /home/malhitticrypto/universal-or-strategy/logs/phase1/EPIC-CCN-*.log | tail -9"
```

**Alert if any API below threshold:**
```bash
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="grep 'Balance: [0-9]' /home/malhitticrypto/universal-or-strategy/logs/phase*/EPIC-CCN-*.log | grep -oP 'Balance: \K[0-9.]+' | awk '\$1 < 50 {print \"⚠️ ALERT: Balance below 50 - \" \$1}'"
```

---

## Assignment Strategy

### Current Strategy (Fixed Assignment)
- Each epic permanently assigned to one API key
- Simplifies tracking and debugging
- No load balancing needed for 9 epics

### Future Strategy (Dynamic Load Balancing)
For Wave 3+ with >10 epics:

1. **Pre-Phase Check**: Query all API balances
2. **Sort by Balance**: Highest balance first
3. **Assign Epics**: Round-robin to top N APIs
4. **Reserve Pool**: Keep 1-2 APIs as backup (>100 balance)
5. **Rebalance**: If any API <50, reassign its epics

**Implementation**: Python script `scripts/assign_epics_to_apis.py`

---

## Update Protocol

### After Each Phase Completion

1. **Extract Costs from Logs**:
   ```bash
   gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
     --command="grep -E 'Cost:.*Balance:|Cost: [0-9]' /home/malhitticrypto/universal-or-strategy/logs/phaseX/EPIC-CCN-*.log"
   ```

2. **Update This Document**:
   - Add costs to respective API rows
   - Recalculate remaining balances
   - Update phase summary section
   - Check threshold status

3. **Validate Balances**:
   - All APIs should be >100 (healthy)
   - If any <100: Flag for monitoring
   - If any <50: Consider reassignment

4. **Commit Changes**:
   ```bash
   git add docs/workflow/API_BALANCE_TRACKER.md
   git commit -m "docs: Update API balances after Phase X"
   ```

### If Balance Not Reported

Use fallback calculation:
```
Remaining = Initial - (Phase0 + Phase1 + Phase1.5 + ...)
```

Example for b (2).json:
```
Remaining = 160 - (3.50 + 0.68 + 0.55) = 155.27
```

---

## Emergency Procedures

### If API Exhausted (<10 bobcoins)

1. **STOP** all work using that API immediately
2. **Reassign** epic to reserve API (sean.carter.jr@atomicmail.io.json)
3. **Update** assignment map in this document
4. **Regenerate** phase scripts with new API key
5. **Redeploy** to VM
6. **Resume** work

### If Multiple APIs Low (<50)

1. **Pause** Wave 2 execution
2. **Audit** actual balances via Bob Shell dashboard
3. **Request** additional API keys from IBM if needed
4. **Rebalance** epic assignments
5. **Resume** with updated assignments

### If Reserve API Needed

**Current Reserve**: sean.carter.jr@atomicmail.io.json (160 bobcoins)

**Activation Protocol**:
1. Identify failed/exhausted API
2. Update epic assignment map
3. Regenerate scripts: `python scripts/wave2/generate_phaseX_scripts.py --api-override EPIC-CCN-XXX=sean.carter.jr@atomicmail.io.json`
4. Deploy and launch

---

## Reporting Format

### Required in Logs

Every Bob Shell agent MUST report:
```
Cost: X.XX | Balance: Y.YY
```

### Extraction Command

```bash
# Get all cost+balance pairs
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="grep -E 'Cost:.*Balance:' /home/malhitticrypto/universal-or-strategy/logs/phase*/EPIC-CCN-*.log"

# Parse into CSV
gcloud compute ssh v12-test-golden-v2 --zone=us-central1-a \
  --command="grep -E 'Cost:.*Balance:' /home/malhitticrypto/universal-or-strategy/logs/phase*/EPIC-CCN-*.log | sed -E 's/.*EPIC-CCN-([0-9]+).*Cost: ([0-9.]+).*Balance: ([0-9.]+).*/\1,\2,\3/'"
```

---

## Integration with Autonomous Refactor

### Pre-Phase Validation

Add to Phase 2 (Epic Execution Loop) initialization:

```markdown
### Step 0: Validate API Budget

**Switch to: Advanced mode**

Hand off:
```
TASK: Validate API Budget Before Phase Execution
PROTOCOL:
  1. Read docs/workflow/API_BALANCE_TRACKER.md
  2. Extract current balances for all 9 assigned APIs
  3. Calculate minimum balance: min(all 9 APIs)
  4. Estimate phase cost: [use previous phase average]
  5. Safety check: min_balance > (phase_cost × 2)
  6. If check fails:
     - Emit [INSUFFICIENT-BUDGET]
     - Recommend: Use reserve API or pause
  7. If check passes:
     - Emit [BUDGET-VALIDATED]
     - Proceed to epic execution
```
```

### Post-Phase Update

Add to Phase 2 (Epic Execution Loop) completion:

```markdown
### Step 5: Update API Balance Tracker

**Switch to: Advanced mode**

Hand off:
```
TASK: Update API Balance Tracker After Phase Completion
PROTOCOL:
  1. Extract all Cost+Balance from logs:
     grep -E 'Cost:.*Balance:' logs/phaseX/EPIC-CCN-*.log
  2. Parse into epic → cost → balance mapping
  3. Update docs/workflow/API_BALANCE_TRACKER.md:
     - Add costs to API rows
     - Recalculate remaining balances
     - Update phase summary
     - Check threshold status
  4. If any API <50: Emit [LOW-BALANCE-WARNING]
  5. Commit changes:
     git add docs/workflow/API_BALANCE_TRACKER.md
     git commit -m "docs: Update API balances after Phase X"
  6. Emit: [TRACKER-UPDATED]
```
```

---

## References

- **API Key Files**: `docs/API/*.json`
- **Phase Scripts**: `scripts/wave2/_pX_*.sh`
- **SOP**: `docs/workflow/WAVE_PHASE_SCRIPT_GENERATION_SOP.md`
- **Skill**: `.bob/skills/gcp-vm-wave-execution/skill.md`
- **Logs**: VM path `/home/malhitticrypto/universal-or-strategy/logs/phase*/`

---

**Last Updated**: 2026-06-13 (after Phase 1.5 completion)  
**Next Update**: After Phase 2 completion