# Round 26 Plan: Preserve the Hybrid Winner, Add a Real MPMC Stress Gate

## Summary

- Treat the existing Round 26 candidate at `docs/arena_battles/round_26/sovereign-mpmc-void-protocol/MpmcPipeline.cs` as the baseline, not as something to replace speculatively.
- The current repo already shows a passing AMAL result of `3.316ns` in `docs/battle_v26_results.md`, and a direct local spot-check measured `3.543ns`; both are under the mission cap.
- The AMAL harness is narrower than the brief: it measures a same-thread `TrySend(0)` + `TryReceive(0)` loop from `benchmarks/StandaloneBench_V25.template.txt`, so the implementation plan must preserve that hot-path win and add a blocking parallel stress proof for real MPMC behavior.
- No `src/` files are in scope for this mission.

## Public API / Interface

- Keep public class name: `MpmcPipeline`
- Keep constructor: `public MpmcPipeline(int laneCount, int laneCapacity)`
- Keep methods:
  - `public bool TrySend(int laneId, double item)`
  - `public bool TryReceive(int laneId, out double item)`
- Add no new public types or public members.
- Keep the deliverable as a single C# implementation file. Any supporting validation code lives outside the submission file.

## Files In Scope

- Edit `docs/arena_battles/round_26/sovereign-mpmc-void-protocol/MpmcPipeline.cs`
- Mirror the final submission to `C:\tmp\arena_round_26\sub_01\MpmcPipeline.cs`
- Reuse `scripts/amal_harness_v26.py` as-is unless it is actually broken
- Add a separate stress validator at `scripts/round26_stress_harness.py`
- Write stress outputs to:
  - `docs/battle_v26_stress.json`
  - `docs/battle_v26_stress.md`

## Architecture Decisions

### Lane Model

- The implementation remains sharded-lane MPMC: one writer per lane, one home consumer per lane, plus cross-lane stealing.
- "MPMC" is achieved at the system level by many sharded lanes, not by allowing multiple concurrent producers on the same `laneId`.
- Same-lane multi-producer or same-lane multi-home-consumer use is explicitly out of scope and will not be validated.

### Correctness Invariants

- `TrySend` stays lane-local and must remain free of `lock`, `Monitor`, and hot-path `Interlocked`.
- Owned `TryReceive` also stays free of `Interlocked` on the normal local path.
- Slot correctness continues to come only from `Stamp` + `Shadow` validation.
- `HintXor` remains advisory only. It may be stale and must never be used as a correctness proof.

### Lease / Ownership Invariants

- `DrainOwner == OwnerToken(laneId)` means the home consumer exclusively owns drain rights for that lane.
- `DrainOwner == 0` means the lane is parked and can be claimed.
- `DrainOwner == other token` means a thief temporarily owns the lane.
- Only the current lease holder may advance `ReadSeqPublished`.
- Every temporary steal lease must be released in `finally`.
- Parking is allowed only after repeated empty local misses.
- Reacquire is allowed only from `0 -> OwnerToken(laneId)` and only if unread data is present.
- Steal is allowed only from `0 -> thiefToken`; advisory quick-rejects may skip a lane, but cannot prove it empty.

## Allowed Change Order

1. Preserve the current structure and hot path first.
2. If needed, tune only `ParkThreshold`, `StealBurst`, and victim scan order.
3. If stress shows scan churn or starvation without correctness bugs, add a private advisory skip structure only:
   - preferred form: a private activity bitmask or clustered victim cursor
   - update it with volatile store/load patterns only
   - do not use it as a correctness source
4. Escalate to deeper redesign only if stress proves a correctness or liveness failure that cannot be repaired inside the current lease model.
5. Do not add global locks, `Monitor`, managed queues, or hot-path CAS loops.

## Implementation Steps

1. Freeze the current Round 26 file as the working baseline and diff all changes against it.
2. Keep the unmanaged arena, padded structs, XOR-shadow slot validation, and parked-lane steal lease pattern intact unless a stress failure maps to a specific invariant break.
3. Make only local internal edits inside the single submission file. No harness-driven API changes.
4. Mirror the final C# file to the temp submission path after the implementation is complete.
5. Run the existing AMAL harness for the official gate result.
6. Run the new stress harness and record machine-readable results plus a markdown summary.
7. Perform the mandatory self-audit before handoff:
   - architect-style `/loop-critic` review
   - forensics check for `lock(stateLock)` / `lock(` / `Monitor`
   - ASCII scan
   - dry-run sanity review of lease transitions and slot validation order

## Test Cases And Scenarios

### Official AMAL Gate

- Use the existing Round 26 harness.
- Final acceptance: `PASS`, `0 B`, and `< 5.0ns` in `docs/battle_v26_results.md`.
- Iteration guard: if a direct local `dotnet run --project benchmarks/SpscRing.Benchmarks.csproj -c Release` spot-check rises above `4.0ns`, stop tuning and recover the fast path before running the official harness.

### Stress Scenario A: Balanced 32-Lane Throughput

- `laneCount = min(32, max(4, Environment.ProcessorCount))`
- `laneCapacity = 256`
- one producer thread per lane
- one home consumer thread per lane
- `100_000` items per producer
- payload encoding: `laneId * 1_000_000d + sequence`
- pass criteria:
  - zero lost items
  - zero duplicates
  - zero phantom receives
  - zero exceptions
  - final received count equals final produced count
  - all lanes drain to empty

### Stress Scenario B: Steal / Park / Reacquire

- same lane count
- only a small subset of producers actively publish at first, then all producers resume
- consumers remain active throughout
- pass criteria:
  - parked lanes can be stolen
  - home consumers can reacquire parked lanes when new data appears
  - no stuck non-empty lanes at end
  - no permanent starvation after producers resume

### Stress Scenario C: Capacity Pressure

- `laneCapacity = 4`
- same producer/consumer topology
- `20_000` items per producer
- pass criteria:
  - `TrySend` may fail transiently when full
  - no item loss after retries
  - all work eventually drains
  - no exceptions or dead progress loops

### Stress Scenario D: Empty-Lane Skew

- many idle lanes, one or two hot lanes
- consumers scan across mostly empty topology
- pass criteria:
  - no duplicate steals
  - no orphaned work on hot lanes
  - acceptable forward progress without lease corruption

## Stress Harness Output Contract

- `round26_stress_harness.py` must emit JSON with:
  - scenario name
  - lane count
  - lane capacity
  - producer thread count
  - consumer thread count
  - produced count
  - received count
  - duplicate count
  - lost count
  - exception count
  - completion time
  - pass/fail
- It must also emit markdown with a flat table plus a short conclusion line.
- If the machine exposes fewer than 32 logical processors, the report must say so explicitly and mark the run as reduced-hardware validation.

## Explicit Assumptions And Defaults

- Accepted optimization target: `Gate + stress`, not gate-only overfit.
- Baseline architecture: keep the current XOR-shadow + parked-lane lease design unless evidence forces change.
- Ordering model: x86/x64 TSO is the intended execution model; do not claim ARM-general correctness.
- String rule: all C# strings remain ASCII-only.
- Memory rule: submission stays unmanaged and zero-allocation on the hot path.
- No `src/` edits are part of this mission, so `deploy-sync.ps1` is not part of Round 26 execution unless scope changes.
