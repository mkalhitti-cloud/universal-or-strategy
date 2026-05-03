---
name: Architecture Patterns
description: >
  Professional architecture and design pattern guidelines. Use this whenever the user
  asks to design a new component, refactor a module, or implement a standard pattern
  (Microservices, Event-Driven, Layered).
---

# Architecture Patterns (Professional Standard)

## Core Logic

- **Choose Pattern Based on Domain**:
  - **Microservices**: Use for independent scaling and failure isolation.
  - **Event-Driven**: Use for highly decoupled, asynchronous systems (Nexus standard).
  - **Layered**: Use for simple, predictable data flows.

## Instructions

1.  **Isolate Concerns**: Ensure each layer/service has a single, well-defined responsibility.
2.  **Dependency Inversion**: High-level modules should not depend on low-level modules. Both should depend on abstractions.
3.  **Audit Rationale**: For every major structural change, create an ADR (Architecture Decision Record).

## Mandatory Self-Improvement Audit

After EVERY skill use, perform this audit:

1. **Instruction Clarity**: Did any step produce an unexpected or ambiguous result?
2. **Trigger Coverage**: Is the description "pushy" enough?
3. **Gap Analysis**: If a gap is found, fix it immediately. Otherwise, state: `skill(architecture-patterns): no gaps identified`.
