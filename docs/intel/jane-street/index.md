---
type: KnowledgeIndex
title: Jane Street Intelligence Wiki
description: Distilled architectural patterns from Jane Street engineering talks, applied to V12 C# trading system. Used by all wave execution agents.
tags: [jane-street, architecture, hft, v12, complexity-reduction]
timestamp: 2026-06-25T00:00:00Z
version: "1.1"
format: OKF-v0.1
---

# Jane Street Intelligence Wiki

**Format**: Open Knowledge Format (OKF) v0.1
**Source**: Jane Street engineering talks, distilled into V12 C# patterns
**Status**: MANDATORY -- these are architectural constraints, not suggestions
**Replaces**: Firebase Firestore `jane_street_knowledge_base` collection (revoked credential)

> **THIS IS THE PATTERN GUIDE, NOT THE COMPLETE RULE SET.**
> The authoritative numbered rule catalog is at `docs/standards/jane-street/RULES_CATALOG.md`
> (JS-001..JS-110, 100+ rules). Read the Catalog for enforcement decisions.
> Read this wiki for HOW to implement the patterns. Both are mandatory.
> PR review agent: `docs/Jane Street Sentinel`

## Documents

| File | Topic | Key Theme |
|------|-------|-----------|
| [hardware-software-codesign.md](hardware-software-codesign.md) | Oxide at Jane Street | Defensive init, infrastructure telemetry |
| [building-tools-for-traders.md](building-tools-for-traders.md) | Ian Henry — UI Engineering | Keyboard-first, exhaustive pattern matching |
| [how-to-build-an-exchange.md](how-to-build-an-exchange.md) | ECN matching engine | FSM determinism, sidecar lifecycle |
| [production-engineering-billions.md](production-engineering-billions.md) | Production safety | Staleness guard, independent tracking |
| [ocaml-performance-engineering.md](ocaml-performance-engineering.md) | OCaml perf / type system | Struct locality, ref struct escape prevention |
| [complexity-reduction.md](complexity-reduction.md) | CYC reduction patterns | Extraction, FSM decomposition, CYC <= 8 |
| [lock-free-patterns.md](lock-free-patterns.md) | Lock-free concurrency | Actor/Enqueue model, atomic primitives |
| [testing-strategies.md](testing-strategies.md) | xUnit testing | [Fact], Assert.Equal(), never NUnit/MSTest |

## Query Protocol

Agents query this wiki by reading the relevant file directly:
```
read_file("docs/intel/jane-street/<topic>.md")
```

Or use the local query script (falls back to this wiki if Firebase unavailable):
```
python scripts/query_kb.py "complexity reduction"
```

## Cross-References

- [complexity-reduction.md](complexity-reduction.md) links to [how-to-build-an-exchange.md](how-to-build-an-exchange.md) (FSM patterns)
- [lock-free-patterns.md](lock-free-patterns.md) links to [ocaml-performance-engineering.md](ocaml-performance-engineering.md) (data race freedom)
- [testing-strategies.md](testing-strategies.md) links to [building-tools-for-traders.md](building-tools-for-traders.md) (expect tests)

## Related: Rules Catalog (enforcement layer)

| File | Contents |
|------|----------|
| [`docs/standards/jane-street/RULES_CATALOG.md`](../../standards/jane-street/RULES_CATALOG.md) | JS-001..JS-110 numbered rules with DO/DON'T + grep patterns |
| [`docs/standards/jane-street/INDEX.md`](../../standards/jane-street/INDEX.md) | Category index for all 10 standards documents |
| [`docs/Jane Street Sentinel`](../../Jane Street Sentinel) | PR enforcement agent instructions (GODMODE) |
