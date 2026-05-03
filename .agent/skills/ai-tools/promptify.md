---
name: Promptify
description: >
  Transforms user requests into detailed, precise prompts optimized for AI model consumption.
  Use this whenever the user asks to "prepare a prompt", "write a brief", or "engineer a prompt".
---

# Promptify (V12.15 Standard)

## Core Task

Rewrite user requests into specification language that guides AI models to produce desired outputs without ambiguity.

## Instructions

1.  **Structure**: Use clear headers (Objective, Context, Constraints, Expected Output).
2.  **Detail**: Include specific file paths, code block examples, and audit requirements.
3.  **A2A Focus**: Ensure the prompt is optimized for cross-agent handoffs (e.g., P3 to P4).

## Mandatory Self-Improvement Audit

After EVERY skill use, perform this audit:

1. **Instruction Clarity**: Did any step produce an unexpected or ambiguous result?
2. **Trigger Coverage**: Is the description "pushy" enough?
3. **Gap Analysis**: If a gap is found, fix it immediately. Otherwise, state: `skill(promptify): no gaps identified`.
