# REAPER-EXPANSION Epic — Phase 2: APPROACH (Master Index)
**Epic ID**: REAPER-EXPANSION  
**Protocol**: V12 Photon Kernel (Phase 6 Recursive)  
**Date**: 2026-05-21  
**Agent**: Plan Mode (v12-epic-planner)

---

## MODULAR APPROACH STRUCTURE

Per V12.20 Surgical Doc Limit protocol, the approach has been split into three focused modules to prevent truncation and enable parallel review.

---

## MODULE INDEX

### 1. SIMA Safety Module
**File**: [`02-approach-sima.md`](./02-approach-sima.md)

**Scope**: Fleet dispatch queue safety layer

**Blueprints**:
- `AuditFleetDispatchQueue`: Queue depth monitoring (warn @100, critical @200)
- `CheckStaleDispatch`: 3s staleness threshold
- `AuditSymmetryContext`: Cross-account position sum audit
- `AuditSimaToggleGate`: Force-reset after 5 consecutive CAS rejections

**Target**: ~180 LOC, CYC ≤ 5  
**Priority**: P1 (Critical - Queue Overflow)  
**BUILD_TAG**: `1111.008-reaper-expansion-t1`

---

### 2. IPC Safety Module
**File**: [`02-approach-ipc.md`](./02-approach-ipc.md)

**Scope**: IPC command queue safety layer

**Blueprints**:
- `AuditIpcCommandQueue`: Backpressure NACK @1600 (80% of 2000 limit)
- `CheckStaleIpcCommand`: 10s staleness threshold
- `AuditMalformedPayloadRate`: Circuit breaker @10/sec
- `AuditAllowlistBypassRate`: Security anomaly disconnect @20/min

**Target**: ~150 LOC, CYC ≤ 5  
**Priority**: P1 (Critical - DoS Attack)  
**BUILD_TAG**: `1111.008-reaper-expansion-t2`

---

### 3. Entries Safety Module
**File**: [`02-approach-entries.md`](./02-approach-entries.md)

**Scope**: Entry signal validation and duplicate suppression

**Blueprints**:
- `ValidateEntryPreconditions`: 4-check orchestrator (mode, duplicate, staleness, quantity)
- `ValidateEntryMode`: CurrentEntryMode vs calling method
- `CheckDuplicateSignal`: 500ms grace period per mode (atomic timestamp update)
- `CheckSignalStaleness`: 3-bar max lookback
- `ValidateEntryQuantity`: Clamp to PositionSize

**Target**: ~120 LOC, CYC ≤ 5  
**Priority**: P2 (High - Duplicate Signal)  
**BUILD_TAG**: `1111.008-reaper-expansion-t3`

---

## JANE STREET ATOMIC UNIFICATION

All three modules achieve **100% Jane Street Compliance**:

| Principle | SIMA | IPC | Entries | Overall |
|:---|:---:|:---:|:---:|:---:|
| **Atomic State Transitions** | ✅ | ✅ | ✅ | ✅ 100% |
| **Wait-Free Progress** | ✅ | ✅ | ✅ | ✅ 100% |
| **Bounded Queues** | ✅ | ✅ | ✅ | ✅ 100% |
| **Bounded Latency** | ✅ | ✅ | ✅ | ✅ 100% |

**Evidence**:
- **SIMA**: `Volatile.Read`, `Interlocked.Exchange`, snapshot pattern, force-reset unblocks
- **IPC**: `Volatile.Read`, client disconnect unblocks, hard limit 2000
- **Entries**: Volatile read, `AddOrUpdate` CAS, duplicate suppression prevents flooding

---

## IMPLEMENTATION SEQUENCE

### Ticket 1: SIMA Safety Module
**Details**: See [`02-approach-sima.md`](./02-approach-sima.md)  
**Agent**: Bob CLI (v12-engineer)  
**Verification**: deploy-sync.ps1 + F5 NinjaTrader + BUILD_TAG

### Ticket 2: IPC Safety Module
**Details**: See [`02-approach-ipc.md`](./02-approach-ipc.md)  
**Agent**: Bob CLI (v12-engineer)  
**Verification**: deploy-sync.ps1 + F5 NinjaTrader + BUILD_TAG

### Ticket 3: Entries Safety Module
**Details**: See [`02-approach-entries.md`](./02-approach-entries.md)  
**Agent**: Bob CLI (v12-engineer)  
**Verification**: deploy-sync.ps1 + F5 NinjaTrader + BUILD_TAG

---

## GREPTILE MCP INTEGRATION

**Pre-Implementation Scan** (Phase 2.3):
1. "What are the current safety gaps in SIMA dispatch queue management?"
2. "Find all usages of _pendingFleetDispatches and _pendingFleetDispatchCount"
3. "What are the current IPC command validation patterns?"
4. "Find all entry methods that accept quantity parameters"

**Post-Implementation Scan** (Phase 6):
- Cross-module search for lock-free compliance
- Semantic regression detection
- Integration point verification

---

## ACCEPTANCE CRITERIA SUMMARY

**Functional**:
- [ ] All 12 safety gaps addressed (4 SIMA, 4 IPC, 4 Entries)
- [ ] Jane Street Atomic Unification: 100% compliance
- [ ] Zero `lock()` statements across all modules
- [ ] ASCII-only compliance (no Unicode/emoji)

**Non-Functional**:
- [ ] Total LOC: ~450 (180 + 150 + 120)
- [ ] All methods CYC ≤ 5
- [ ] All checks complete in < 10ms (P99)

**Verification**:
- [ ] deploy-sync.ps1 PASS (all 3 tickets)
- [ ] F5 NinjaTrader verification (all 3 tickets)
- [ ] BUILD_TAGs: `1111.008-reaper-expansion-t1/t2/t3`
- [ ] Greptile semantic scan: 100/100 PHS

---

## [APPROACH-MODULAR-COMPLETE]

**Status**: Phase 2.2 complete. Approach split into 3 modular files + master index.

**File Verification**:
- ✅ `02-approach-sima.md` (329 lines)
- ✅ `02-approach-ipc.md` (249 lines)
- ✅ `02-approach-entries.md` (289 lines)
- ✅ `02-approach.md` (master index, 145 lines)

**Next Gate**: [PLAN-GATE] - Awaiting Director approval to proceed to Phase 2.3 (Greptile scan) or Phase 3 (VALIDATE).