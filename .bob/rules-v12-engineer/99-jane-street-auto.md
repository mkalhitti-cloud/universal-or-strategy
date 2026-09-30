# Jane Street Principles (Auto-Generated)

**Source**: Loaded from Jane Street Knowledge Base on session start
**Status**: MANDATORY - These are architectural constraints, not suggestions

## Core Principles

### Distilled Intel: Hardware-Software Codesign (Oxide at Jane Street)

**Key Takeaways**:
- Serious software engineering requires custom hardware integration and BIOS/BMC elimination.
- Hardware defects are frequently masked by software/BIOS workarounds (e.g., the 19-year double-reset NIC bug).
- Transient voltage sags (like the 12V to 8V dip on Jane Street's Sled 19) can cause selective component resets while the main processor remains active.
- Basing systems on open-source downstack components (Hubris, OpenSIL) yields faster, more debuggable development than relying on proprietary blobs.

**V12 C# Patterns**:
- **defensive_initialization**: Idempotent OnStateChange setups and state machines that survive environment resets.
- **infrastructure_telemetry**: Tracking .NET GC pauses, process memory, and thread state within trade logs for low-level diagnostic observability.

### Distilled Intel: Building Tools for Traders (Ian Henry)

**Key Takeaways**:
- Trading tools require extreme information density and keyboard-first design over tutorials.
- Bonsai compiles UI as an incremental state DAG, enabling highly optimized, granular virtual DOM patching.
- OCaml isomorphic type sharing across frontend/backend eliminates API serialization boilerplate.
- Expect tests modify themselves to embed program outputs, serving as a plain-text notebook for code review.

**V12 C# Patterns**:
- **keyboard_first_ui**: Bypassing mouse-hover workflows in trading charts in favor of high-speed keyboard shortcuts.
- **exhaustive_pattern_matching**: Implementing sum-type patterns in C# via abstract hierarchies and switch expressions.
- **expect_testing_traces**: Serializing state machine execution paths to committed text files for differential code reviews.

### Distilled Intel: How to Build an Exchange

**Key Takeaways**:
- ECN matching engines operate as deterministic single-threaded state machines on commodity x86 hardware.
- UDP multicast is utilized for simultaneous, fair distribution of market data to all participants.
- State Machine Replication (SMR) allows any component to be rebuilt rapidly by replaying the transaction log.
- Decouple core matching logic from timing-based events using helper sidecars (e.g., Cancel Fairy).
- Pointers and index dereferencing to locate order records constitute the primary memory/cache bottleneck.

**V12 C# Patterns**:
- **determinism**: Use tick timestamps instead of system clocks to ensure history replayability.
- **sidecar_lifecycle**: Segregate lifecycle and temporal order rules from core order book updates.
- **one_in_flight**: Implement a two-phase order replacement FSM to avoid ghost-order states.
- **cache_optimization**: Use fixed-size struct arrays with direct index lookups to eliminate pointer-chasing.

### Distilled Intel: Production Engineering When Trading Billions

**Key Takeaways**:
- Every order and its economic details are critical; adverse selection punishes bugs immediately.
- Banish average-based SLO alerts for core systems; implement event-based alerting for all edge cases.
- Implement orthogonal, epistemic alerts like 'Feel Too Good' (PnL exceeds expectations) to catch cross-stack issues.
- Defense in depth requires distinct enforcement gates with separate codebases, teams, and dependencies.
- Support staff must possess business context, and engineers must collaborate closely with traders using shared terminology during incidents.

**V12 C# Patterns**:
- **staleness_guard**: Track machine time vs last tick time to detect and halt on stale feeds.
- **independent_tracking**: Verify working orders and positions in-memory separately from external API states.
- **manifest_logging**: Log BUILD_TAG and parameters at startup to simplify deployment roll audits.
- **rate_limiting**: Implement a time-window circuit breaker to catch looping order placement bugs.

### Distilled Intel: Making OCaml Safe for Performance Engineering

**Key Takeaways**:
- Uniform value representation eases GC and polymorphism but forces boxing of floats/records on the heap.
- Kinds (layouts) track type shapes to specialized generics once per layout, minimizing binary bloat.
- Modes (Global vs Local) track escape behavior, enabling safe, compiler-checked stack allocation of closures.
- Statically enforcing Data Race Freedom uses Contention and Portability modes to prevent shared mutable state access.

**V12 C# Patterns**:
- **struct_cache_locality**: Favoring value types (structs) over reference types (classes) to eliminate GC scans and leverage contiguous layout caches.
- **ref_struct_escape_prevention**: Using C# ref struct definitions to enforce stack-only lifetimes and prevent heap escaping.

## Enforcement

- These principles MUST be applied to all architectural decisions
- Violations should be flagged during code review
- When in doubt, query the Jane Street KB: `python scripts/query_kb.py <term>`
