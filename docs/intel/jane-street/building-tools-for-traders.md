---
type: KnowledgeIntel
title: Building Tools for Traders (Ian Henry)
description: UI engineering principles from Jane Street. Keyboard-first design, exhaustive pattern matching, expect testing. Applied to V12 chart and ATM tooling.
tags: [ui, keyboard, pattern-matching, expect-testing, trader-tools]
timestamp: 2026-06-25T00:00:00Z
---

# Building Tools for Traders (Ian Henry)

**Source**: Jane Street engineering talk by Ian Henry on internal trading tool development

## Key Takeaways

- Trading tools require **extreme information density** and keyboard-first design over tutorials
- Bonsai compiles UI as an incremental state DAG, enabling highly optimized, granular virtual DOM patching
- OCaml isomorphic type sharing across frontend/backend eliminates API serialization boilerplate
- **Expect tests** modify themselves to embed program outputs, serving as a plain-text notebook for code review

## V12 C# Patterns

### `keyboard_first_ui`
Bypassing mouse-hover workflows in trading charts in favor of high-speed keyboard shortcuts.
```csharp
// Keyboard shortcut dispatch — fast, no mouse dependency
protected override void OnKeyUp(KeyEventArgs e) {
    if (e.Key == Key.F && e.Modifiers == ModifierKeys.Control) ToggleFleet();
    if (e.Key == Key.R) ResetSymmetryGuard();
}
```

### `exhaustive_pattern_matching`
Implementing sum-type patterns in C# via abstract hierarchies and switch expressions.
```csharp
// CORRECT — exhaustive switch expression (CYC friendly, compiler-checked)
private string DescribeState(OrderState state) => state switch {
    OrderState.Idle         => "Idle",
    OrderState.PendingNew   => "Pending",
    OrderState.Live         => "Live",
    OrderState.PendingCancel => "Cancelling",
    _ => throw new ArgumentOutOfRangeException(nameof(state))
};
```
Note: switch expressions count as 1 CYC (not N), unlike switch statements.

### `expect_testing_traces`
Serializing state machine execution paths to committed text files for differential code reviews.
```csharp
// In test: serialize execution trace and compare to committed baseline
var trace = fsm.ExecuteAndTrace(inputs);
Assert.Equal(File.ReadAllText("expected_trace.txt"), trace);
```

## Complexity Impact

`exhaustive_pattern_matching` with switch expressions reduces CYC significantly:
- Switch statement with 8 cases: CYC +8
- Switch expression with 8 arms: CYC +1 (compiler handles exhaustiveness)

## Cross-References
- [testing-strategies.md](testing-strategies.md) — expect testing → xUnit [Fact] pattern
- [complexity-reduction.md](complexity-reduction.md) — switch expression vs switch statement
