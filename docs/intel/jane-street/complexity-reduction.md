---
type: KnowledgeRule
title: Complexity Reduction Patterns
description: Patterns for reducing cyclomatic complexity to CYC <= 8 (Jane Street strict standard). Primary reference for Phase 2 architecture and Phase 5 execution.
tags: [complexity, cyc, extraction, refactoring, wave7]
resource: docs/intel/jane-street/how-to-build-an-exchange.md
timestamp: 2026-06-25T00:00:00Z
---

# Complexity Reduction Patterns

**Standard**: CYC <= 8 (Jane Street strict — not Codacy's 15)
**Why**: Functions >8 are harder to reason about under microsecond latency, test exhaustively, and audit for race conditions.

## Primary Strategies

### 1. Extract Guard Clauses
Replace nested if-chains with early returns at the top of the method.
```csharp
// BEFORE (CYC adds 3 per nesting level)
if (a) { if (b) { if (c) { DoWork(); } } }

// AFTER (each guard is CYC +1, flat)
if (!a) return;
if (!b) return;
if (!c) return;
DoWork();
```

### 2. Extract to Named Helper Methods
Move cohesive logic blocks into private helper methods with descriptive names.
- Each extracted method MUST project CYC <= 8 independently
- Name reflects the single concern: `ValidateOrderBounds()`, `BuildEntrySignal()`, `ApplyAtmRules()`
- Helpers are `private` — never widen scope during extraction

### 3. Replace Switch/If-Chains with Lookup Tables or Strategy Pattern
Large switch statements (each case = +1 CYC) should become dictionary dispatches.
```csharp
// BEFORE: switch with 12 cases = CYC +12
// AFTER: Dictionary<State, Action> dispatch = CYC +1 (one lookup)
private static readonly Dictionary<OrderState, Action<Order>> _handlers = new() {
    [OrderState.Pending] = HandlePending,
    [OrderState.Filled]  = HandleFilled,
};
```

### 4. FSM Decomposition (Jane Street Exchange Pattern)
Complex state-handling methods become FSM transition tables.
- See: [how-to-build-an-exchange.md](how-to-build-an-exchange.md) — `one_in_flight` pattern
- Each state transition = one small private method (CYC <= 3 each)
- Parent method only routes: `_transitions[currentState](context)`

### 5. Extract Loop Body
A loop with a complex body: extract the body into `ProcessSingleItem(item)`.
```csharp
// BEFORE: foreach + 5 conditions inside = high CYC
// AFTER:
foreach (var item in items) ProcessSingleItem(item);
private void ProcessSingleItem(Item item) { /* CYC <= 8 */ }
```

## V12-Specific Rules

| Rule | Requirement |
|------|-------------|
| Target CYC | <= 8 for EVERY method (parent + all helpers) |
| Test coverage | xUnit [Fact] test for each extracted helper |
| Scope | ONE method per epic — never extract across methods |
| Lock blocks | ZERO — use FSM/Actor Enqueue model instead |
| String literals | ASCII-only (no Unicode, no curly quotes) |
| Encoding | UTF-8, no BOM |

## Wave 7 Decision Tree

```
CYC > 8?
  ├─ Has large switch/if-chain? → Strategy pattern or lookup table
  ├─ Has deeply nested conditions? → Extract guard clauses
  ├─ Has cohesive sub-tasks? → Extract named helper methods
  ├─ Manages state transitions? → FSM decomposition
  └─ Has loop with complex body? → Extract loop body method
```

## Cross-References
- [how-to-build-an-exchange.md](how-to-build-an-exchange.md) — FSM + sidecar patterns
- [lock-free-patterns.md](lock-free-patterns.md) — replacing lock() with Actor Enqueue
- [testing-strategies.md](testing-strategies.md) — xUnit tests for extracted helpers
