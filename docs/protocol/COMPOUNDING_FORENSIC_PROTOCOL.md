# Protocol: Compounding Forensic Pipeline (CFP)

The CFP is a zero-trust multi-agent verification chain designed to prevent logic debt and race conditions in complex trading systems. It ensures diagnostics accurately "compound" as it moves through the development pipeline.

## 🏁 Phase 1: Antigravity (Strategic Architect)

- **Role**: Initial triage and environment setup.
- **Action**: Locate the fault, explain priority (P1/P2), and draft the `implementation_plan_triage.md`.
- **Handoff**: Bridge artifacts to `docs/brain/` for Codex/Claude awareness.

## 🔍 Phase 2: Codex (Forensic Auditor)

- **Role**: "Red Team" Stress Testing.
- **Action**: Perform a forensic logic audit based _only_ on the codebase and Antigravity's plan.
- **Requirement**: **Independent Verification**. Do not assume Antigravity is 100% correct. Locate technical nuances (e.g., specific dict names, broker-live states).
- **Compound**: Update the plan with forensic confirms/reclassifications.

## 🛠️ Phase 3: Claude/Sonnet (Lead Engineer)

- **Role**: Architectural Synthesis & Implementation.
- **Action**: Review all prior findings (Antigravity + Codex). Perform a third independent analysis of the source code.
- **Requirement**: **Diagnostic Synthesis**. Sonnet must confirm the logic _as an engineer_ before proposing final repairs.
- **Execution**: Implement confirmed repairs using the most architecturally sound methods.

## 🚀 Benefits

- **Accuracy**: Three independent sets of "eyes" on every P1 vulnerability.
- **Token Efficiency**: Each agent consumes the distilled "Forensic Truth" of the predecessor, reducing redundant research.
- **Safety**: Prevents architectural drift by requiring consensus at every handoff point.
