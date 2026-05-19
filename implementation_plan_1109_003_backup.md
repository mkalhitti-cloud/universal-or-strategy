# Build 1109.003-v14.2 -- Sovereign Photon Integration Plan

## Context

**Baseline**: Build 1109.003 (freeze-proof structural repair, 9 phases).
**Objective**: Port three V14.2 "Sovereign Photon" concepts into the production baseline:

1. **Zero-Allocation FIX Proxy (ADR-012)** -- Pre-allocated struct pool + SPSC ring buffer for fleet dispatch
2. **CRC16 Integrity Verification** -- Atomic slot-level checksums to detect torn cross-thread reads
3. **Lock-Free Recycle Queue (ADR-011)** -- Zero-allocation execution ID dedup with hash-based ring

---

## 🛡️ MISSION COMMANDER MANDATE

**Protocol**: TRIPLE-AGENT ULTRATHINK
**Verification**: Mandatory P5 Audit (Codex + Jules + Gemini CLI) before execution.

---

## Component 1: SPSC Ring Buffer with CRC16

- **New File**: `src/V12_002.Photon.Ring.cs`
- **Mechanism**: Single-Producer Single-Consumer (SPSC) Ring with cache-line padding (64 bytes) on cursors to prevent false sharing.
- **Integrity**: `CRC16-CCITT` check interspersed in each ring slot. Consumer validates CRC before processing.

## Component 2: OrderProxy Pool (Zero-Allocation Dispatch)

- **New File**: `src/V12_002.Photon.Pool.cs`
- **Mechanism**: Pre-allocated `OrderProxy` pool (NUMA-aware).
- **Logic**: Uses unmanaged pointer swaps to hand off order data from the ring to the broker call.

## Component 3: Lock-Free ExecutionId Recycle Ring (ADR-011)

- **Logic**: FNV-1a 64-bit hash ring replacing `HashSet<string>` + `Queue<string>`.
- **Performance**: O(1) open-addressing lookup, circular FIFO eviction, zero string storage.

---

## Implementation Steps [ENGINEER: Codex/Jules]

### Step 1: Create `src/V12_002.Photon.Ring.cs`

Implement the `SPSCRing<T>` with `Volatile.Read/Write` barriers and `CRC16-CCITT` logic.

### Step 2: Create `src/V12_002.Photon.Pool.cs`

Implement the `PhotonOrderPool` (fixed-size pre-allocation) and the `ExecutionIdRing`.

### Step 3: Global Field Integration (`src/V12_002.cs`)

- Add `_photonPool`, `_photonDispatchRing`, and `_executionIdRing` fields.
- Initialize in `Lifecycle.cs` (State.Configure).

### Step 4: Execution Dedup Replacement (`Orders.Callbacks.Execution.cs`)

- Swap `HashSet` logic for `_executionIdRing.ContainsOrAdd(hash)`.

### Step 5: Dispatch Pipeline Integration (`SIMA.Dispatch.cs`)

- Replace `_pendingFleetDispatches` ConcurrentQueue with the `PhotonRingBuffer`.
- Implement `ComputeProxyCrc` for the `FleetDispatchSlot`.

### Step 6: Fleet Consumption (`SIMA.Fleet.cs`)

- Modify `PumpFleetDispatch` to consume from the ring first with CRC validation.

### Step 7: Telemetry & Diagnostics (`Properties.cs`)

- Update `BUILD_TAG` to `1109.003-v14.2`.
- Add `[PHOTON_HEALTH]` hooks to the REAPER cycle.

---

## Verification Plan

- [x] **Adversarial Audit**: Codex P5 + Jules P5 UltraThink Sign-off.
- [ ] **ASCII Gate**: `deploy-sync.ps1` must pass.
- [ ] **SIM Stress Test**: 12 accounts, rapid entry/flatten cycle.

**Status**: DESIGN FINALIZED -> TRIGGERING P5 AUDIT.
