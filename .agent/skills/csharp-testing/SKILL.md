---
name: C# Testing
description: >
  Professional testing patterns and practices for NinjaTrader 8 (C# 8.0 / .NET 4.8).
  Focuses on unit testing, integration testing, and performance benchmarking (BenchmarkDotNet).
---

# C# Testing Skill

You are a **C# Testing** specialist. Use this skill to design, write, and execute tests for the V12 Universal OR Strategy.

## I. Testing Frameworks
- **Primary**: XUnit or NUnit for logic testing.
- **Mocking**: Moq for IPC and Account API abstraction.
- **Performance**: BenchmarkDotNet for hot-path latency and allocation auditing.

## II. NinjaTrader 8 Compatibility
- **Constraint**: NT8 runs on .NET Framework 4.8. Avoid modern .NET Core/5/6+ features in test projects that need to reference NT8 DLLs.
- **Abstraction**: Wrap NT8-specific objects (Instrument, Account) in interfaces for unit testing.

## III. V12 Standard Testing Patterns
1. **State Machine Verification**: Test FSM transitions (e.g., `PendingCancel` -> `CancelConfirmed`) using event-based assertion.
2. **Lock-Free Audit**: Verify concurrent safety of SPSC/MPMC queues without using `lock`.
3. **Zero-Allocation**: Use the `amal_harness.py` logic to verify that hot-path methods (e.g., `ManageTrailingStops`) perform zero Gen0/Gen1 allocations.
4. **IPC Round-Trip**: Test JSON command serialization/deserialization across the TCP boundary.

## IV. Commands
- Run Unit Tests: `dotnet test`
- Run Benchmarks: `dotnet run -c Release --project Benchmarks.csproj`
- Stress Test: `powershell -File scripts/test_stress.ps1`

---

## When to use this skill
- Before any P5 Implementation (TDD).
- During P6 Validation to verify bug fixes.
- When refactoring high-complexity methods to ensure no logic regression.

---

## Mandatory Post-Use Self-Improvement Audit (NON-NEGOTIABLE)

After completing any session using this skill, perform an audit:

1. Did any instruction produce an unexpected result or confusion?
2. Was any rule ambiguous enough that you had to make a judgment call?
3. Was a step missing that caused backtracking?
4. Is any reference file out of date?

If yes to any: **update this SKILL.md or references/ file immediately**, then commit:
skill(csharp-testing): [what was fixed]

If no gaps found: state skill(csharp-testing): no gaps identified. in your response.
No Director approval required for skill-only edits.
