# V12_ATLAS.md - The Unified World Model (CAG)

**Purpose**: This document provides a high-density, pre-loaded context of the V12 Universal OR Strategy. It serves as the primary source of truth for all agents to prevent context drift and ensure architectural consistency during Epic 3.

## 1. Core Architecture (The V12 DNA)
- **Execution Model**: Strictly Single-Threaded Strategy Loop via NinjaTrader 8.
- **Concurrency**: Lock-Free Actor Pattern. No `lock()` statements permitted.
- **State Mutation**: All external commands (IPC) or cross-thread events MUST be marshaled to the Strategy Thread using `.Enqueue()` or `TriggerCustomEvent`.
- **Memory Management**: Jane Street "Allocation is a Bug" philosophy. Favor `struct`, `ref`, and Object Pooling in the hot path.

## 2. Subgraph Map
- **SIMA (Single Interface Multi Account)**: The fleet management layer. Handles account-level signal replication.
- **REAPER (Naked Position Sentinel)**: The safety layer. Monitors account state and closes unhedged or orphan positions.
- **IPC (Inter-Process Communication)**: Socket-based command plane for external control.
- **STICKY STATE**: Persistence layer for cross-session parameter and position tracking.

## 3. Mandatory Jane Street Alignment (V12.17)
- **Atomic Unification**: State transitions must be complete and indivisible.
- **Deterministic Execution**: Avoid non-deterministic latency spikes from GC or thread contention.
- **Logic over Guards**: Prefer structural correctness (types/enums) over runtime checks.
- **Wait-Free Recovery**: Always include wait-free recovery patterns (e.g., Gate Resets) for lock-free kernels.

## 4. Agent Tooling & Verification
- **Greptile Sentinel**: Semantic regression testing and architectural enforcement via MCP. Mandatory use of **`/epic-scan`** in Phase 2.3. Fallback to **jCodemunch-MCP** if Greptile is unreachable.
- **Sentinel Pyramid**: Core unit test suite ensuring 100/100 logic coverage.
- **Pre-Push Validation**: Mandatory 10-point local audit before GitHub push.
- **V12.20 Surgical Doc Limit**: Documentation > 500 lines MUST be modularized into subgraph-specific files (e.g., `02-approach-sima.md`) with a Master Index. Verify buffer flush via `ls`.
- **V12.21 Sentinel-Adversary**: Independent verification runs (e.g., `/epic-scan`) are mandatory for all planning gates. Semantic tool fallback (Greptile -> jCodemunch) is permitted.

## 5. Environment Provisioning
- **API Access**: Greptile and Context7 keys are secured in `.env` (git-ignored).
- **Global Access**: All agents (Bob, Codex, Jules, etc.) share this environment.
- **MCP Config**: `.mcp.json` is configured for jCodemunch and Greptile.
