# Epic 5: Performance Optimization (EPIC-5-PERF)

**Status**: ⏳ PENDING  
**Prerequisites**: Epic 4 merged

## Objective
Implement zero-allocation hot path optimizations and verify bounded latency per Jane Street architectural alignment.

## Scope

### 1. Zero-Allocation Hot Path
- [ ] Replace string interpolation in high-frequency logic (Print/Log) with non-allocating alternatives.
- [ ] Eliminate `LINQ` usage in `OnBarUpdate` and `OnMarketData` paths.
- [ ] Optimize `ConcurrentQueue` usage to minimize GC pressure.
- [ ] Verify `Allocated = 0 B` via `scripts/amal_harness.py`.

### 2. Bounded Latency Verification
- [ ] Implement microsecond-precision timing for actor dispatch.
- [ ] Audit `_photonDispatchRing` for wait-free progress guarantees.
- [ ] Profile `ProcessIpcCommands` for worst-case execution time (WCET).

### 3. Technical Debt Remediation
- [ ] Resolve 100 Codacy violations inherited from Epic 4.
- [ ] Reduce method complexity in `UpdateComplianceDisplay` (25 -> 15).
- [ ] Refactor `ExecuteOrderSync` to use parameter objects (7 params -> 1).

## Success Criteria
- [ ] AMAL Gate: `Allocated = 0 B` and `Mean Latency < Baseline`.
- [ ] PHS Score: 100/100 maintained.
- [ ] Codacy Grade: A (Targeting reduction of 306 code smells).
- [ ] Zero P0/P1 issues introduced.

## Jane Street Alignment
- **Atomic Unification**: No fragmented state transitions.
- **Deterministic Execution**: Zero garbage-collection pressure in the hot path.
- **Wait-Free Kernels**: Absolute ban on `lock()` verified.
