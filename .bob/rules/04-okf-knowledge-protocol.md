# OKF Knowledge Protocol (V12.39)

## Mandatory Rule — All Modes, All Agents

The project has **two complementary Jane Street knowledge sources**. Both are MANDATORY.

### Source 1 -- OKF Wiki (pattern guide)
`docs/intel/jane-street/` -- 13 documents distilled from Jane Street engineering talks.
Distilled patterns applied to V12 C# code. Used for HOW to fix things.

### Source 2 -- Rules Catalog (the coding bible)
`docs/standards/jane-street/RULES_CATALOG.md` -- JS-001 through JS-110.
100+ numbered, enforceable rules across 7 categories. Used for WHAT to check.
The Sentinel enforcement agent (`docs/Jane Street Sentinel`) enforces this catalog on every PR.

**Status**: BOTH sources are MANDATORY constraints, not suggestions.
**Replaces**: Firebase Firestore `jane_street_knowledge_base` (credential revoked 2026-06).

## Two-Source Architecture

| Source | Location | Format | Used For |
|--------|----------|--------|----------|
| OKF Wiki | `docs/intel/jane-street/` | 13 pattern docs | Architecture decisions, HOW to implement |
| Rules Catalog | `docs/standards/jane-street/RULES_CATALOG.md` | JS-001..JS-110 | Enforcement, WHAT to scan for |
| Sentinel | `docs/Jane Street Sentinel` | PR review agent | Blocking PR violations |

**Rule**: When OKF wiki and Rules Catalog conflict, the **Catalog wins** (more specific).

## What the Rules Catalog Contains

7 categories, 100+ rules:
- **Type Safety** (JS-001..020): Result<T,E>, Option<T>, sealed hierarchies, nullable, phantom types
- **Concurrency** (JS-021..035): lock() ban, Actor pattern, Interlocked, structured concurrency
- **Performance** (JS-036..050): Span, ArrayPool, readonly struct, zero-alloc hot paths
- **Testing** (JS-051..065): property-based tests, seeded Random, BenchmarkDotNet
- **Code Review** (JS-066..080): diff < 10k chars, CYC <= 8, ASCII-only, switch expressions
- **Serialization** (JS-081..095): versioned messages, checksums, zero-copy deserialization
- **Philosophy** (JS-096..110): illegal states unrepresentable, compile-time over runtime, explicit control flow

## What the OKF Wiki Contains

13 OKF documents covering:
- CYC <= 8 reduction patterns (the primary wave 7 goal)
- Lock-free Actor/Enqueue mandate (DNA enforcement)
- xUnit-only testing standard
- FSM determinism and sidecar lifecycle
- Zero-alloc hot path, cache alignment, JIT warmup
- Data race freedom via type-system patterns
- CPU front-end DSB cache (why CYC <= 8 is also a performance win)

## MANDATORY Reading Triggers

You MUST read the relevant source before:

| Trigger | Read |
|---------|------|
| Phase 2 (Architecture Planning) | OKF: `complexity-reduction.md`, `how-to-build-an-exchange.md` |
| Phase 3 (DNA Audit) | OKF: `lock-free-patterns.md` |
| Phase 5 (Ticket Execution) | OKF: `complexity-reduction.md` + task doc |
| Phase 5.V (Verification) | OKF: `lock-free-patterns.md`, `testing-strategies.md` |
| Any `lock()` found in src/ | OKF: `lock-free-patterns.md` immediately |
| Any complexity question | OKF: `complexity-reduction.md`, `advanced-skylake-deep-dive.md` |
| PR review / violation scanning | Catalog: `docs/standards/jane-street/RULES_CATALOG.md` |
| Wave scan planning | Catalog: `RULES_CATALOG.md` + `docs/Jane Street Sentinel` |
| Any new wave register creation | Catalog: read all 7 categories for scannable rules |

## How to Read

**OKF Wiki -- pattern guide (index first)**:
```bash
read_file("docs/intel/jane-street/index.md")
read_file("docs/intel/jane-street/complexity-reduction.md")  # then specific doc
```

**Rules Catalog -- the coding bible (read for enforcement)**:
```bash
read_file("docs/standards/jane-street/RULES_CATALOG.md")
# Contains JS-001..JS-110 with DO/DON'T examples and grep patterns
```

**Sentinel -- PR review agent instructions**:
```bash
read_file("docs/Jane Street Sentinel")
# Contains enforcement workflow, labeling format [CRITICAL-JS-P0], top 10 violations
```

**Or query by keyword** (falls back to local wiki):
```bash
python scripts/query_kb.py "complexity reduction"
python scripts/query_kb.py "FSM extraction"
python scripts/query_kb.py "lock-free"
```

## Document Map

| File | Tags | Key Patterns |
|------|------|-------------|
| `complexity-reduction.md` | cyc, extraction | Extract guards, helper methods, FSM decomposition |
| `lock-free-patterns.md` | lock-free, actor | Actor/Enqueue, Interlocked, ConcurrentQueue |
| `testing-strategies.md` | xunit, testing | [Fact], Assert.Equal, never NUnit/MSTest |
| `how-to-build-an-exchange.md` | fsm, determinism | one_in_flight, sidecar_lifecycle, determinism |
| `microsecond-eternity.md` | hot-path, zero-alloc | jit_warmup, cache_alignment, inlining, zero_alloc |
| `ocaml-performance-engineering.md` | structs, cache | struct_cache_locality, ref_struct_escape_prevention |
| `concurrency-coordination.md` | cache-coherency | false sharing, Left-Right double-buffering |
| `advanced-skylake-deep-dive.md` | cpu, dsp | DSB cache fit, denormal protection, lock_free_execution |
| `building-tools-for-traders.md` | ui, keyboard | keyboard_first_ui, exhaustive_pattern_matching |
| `hardware-software-codesign.md` | resilience | defensive_initialization, infrastructure_telemetry |
| `production-engineering-billions.md` | safety | staleness_guard, independent_tracking, rate_limiting |
| `lab-to-trading-floor.md` | ux, config | serializable_configurations, keyboard_driven_execution |
| `why-testing-is-hard.md` | testing | test isolation, FSM testability |

## Enforcement

- ❌ NEVER make an architectural claim without reading the relevant OKF document first
- ❌ NEVER use `lock()` — `lock-free-patterns.md` is the authoritative reference
- ❌ NEVER use NUnit or MSTest — `testing-strategies.md` mandates xUnit only
- ❌ NEVER create a wave debt register without reading `RULES_CATALOG.md` first -- you will miss rules
- ❌ NEVER treat the OKF wiki as the complete rule set -- it is the distilled pattern guide, not the full catalog
- ✅ Query the wiki BEFORE Phase 2 and Phase 5 -- not after
- ✅ Read `RULES_CATALOG.md` before any wave scan planning or PR review
- ✅ The `pre_task_jane_street_kb.py` hook auto-queries on keyword-matching tasks
- ✅ The `inject_okf_rules.py` script embeds OKF wiki rules in all modes -- the Catalog must be read directly
