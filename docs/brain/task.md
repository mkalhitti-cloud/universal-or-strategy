# Project Morpheus: Hybrid Arena Roadmap (M1-M8)

## MISSION OBJECTIVE
Achieve institutional-grade deterministic execution (sub-100ns typical) within NinjaTrader 8 (.NET 4.8) using the "Hybrid Arena" (Management/Execution Split) architecture.

## ROLES & RESPONSIBILITIES
- **ARCHITECT (Claude P3)**: Strategy & Design. Writes `docs/brain/implementation_plan.md`.
- **ENGINEER (Codex P4)**: Implementation & Execution. Edits `src/` files based on the approved plan.
- **ORCHESTRATOR (Antigravity P1)**: Multi-agent coordination, forensic audits, and state management.

---

## ROADMAP

### PHASE 1: THE SOVEREIGNTY PIVOT (M1-M4)
- [ ] **M1: Blittable Substrate Refactor**
    - [ ] Eliminate `FleetDispatchSideband` (managed refs).
    - [ ] Implement index-based account/instrument lookups.
    - [ ] Define 64-byte aligned, zero-ref `struct` contracts.
- [ ] **M2: Cold-Path Freeze Logic**
    - [ ] Implement "Management Membrane" to flatten OOP objects.
    - [ ] Build the pre-trading validation/compilation engine.
- [ ] **M3: Decision Table Execution Core**
    - [ ] Implement the Hot-Path Router (Data-Oriented Scanner).
    - [ ] Zero-allocation execution loop.
- [ ] **M4: Sidecar Membrane (MMIO Mirror)**
    - [ ] Implement lock-free, padded ring buffer.
    - [ ] Ensure 64-byte cache-line separation between Producer/Consumer.

### PHASE 2: PERFORMANCE & INTEGRATION (M5-M8)
- [ ] **M5: CPU Affinity & Priority**
    - [ ] Bind execution thread to dedicated core.
- [ ] **M6: JIT Warm-up Protocol**
    - [ ] Implement pre-market synthetic execution to stabilize JIT.
- [ ] **M7: Telemetry & Observability**
    - [ ] Low-overhead performance counters.
- [ ] **M8: Final Verification**
    - [ ] Sub-100ns benchmark certification.

---
## CURRENT STATUS
**State**: Substrate v29.0 established. M1-M7 integrated.
**Next Action**: Decompose `ExecuteSmartDispatchEntry` (Phase 1).
