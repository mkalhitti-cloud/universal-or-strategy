# Universal OR Strategy: System Architecture & Assistant Guide

This guide explains how our AI system is set up to handle different tasks efficiently and how you can verify that it's working as expected.

## 1. Model Routing: Who Does What?

Our system uses different AI models depending on the complexity of the task:

| Model | Role | Best For |
| :--- | :--- | :--- |
| **Claude Opus 4.5** | **The Brain** | Complex logic, trading algorithms, critical debugging. |
| **Claude Sonnet 4.5** | **The Coordinator** | Planning, answering questions, simple code changes. |
| **Claude Haiku 4.5** | **The Assistant** | Routine tasks: file management, deployments, documentation. |
| **Gemini Flash 3.0** | **The Runner** | Automated I/O tasks via the "Delegation Bridge" (99% cheaper). |

## 2. The Delegation Bridge

Whenever an agent (like me) needs to save a file, update the changelog, or list files in a directory, we use the **Delegation Bridge**. 

- **How it works**: Instead of the expensive model (Opus/Sonnet) performing the file operation, it sends a request to a lightweight model (Gemini Flash).
- **Why we do it**: This saves over 65% on total session costs and ensures that you don't burn through "Thinking" credits on routine updates.

## 3. How to Verify Usage

You can see exactly which models have been active by checking the project's cost tracking state.

### Using code:
Check the file: [.agent/state/cost_tracking.json](file:///c:/Users/Mohammed Khalid/OneDrive/Desktop/WSGTA/Github/universal-or-strategy/.agent/state/cost_tracking.json)

### What to look for:
In the `costs` section, you will see token counts for different models:
- If `gemini_flash` or `claude_haiku` tokens are increasing, the system is correctly delegating assistant tasks.
- If only `claude_opus` or `claude_sonnet` are showing tokens for routine work, delegation might be failing.

## 4. Spawning an Agent in Claude CLI

When you start an agent in the Claude CLI using Opus:
1. **Initial Context**: The agent reads the `workspace rules` and `skills`.
2. **Instruction Recognition**: It sees the instruction in `universalorworkspacerules.md` that it MUST use the delegation bridge or Haiku for routine tasks.
3. **Execution**: When it needs to perform a routine task, it calls the `call_gemini_flash` tool (from the `delegation-bridge` skill) or spawns a Haiku sub-agent for local operations.

---
> [!TIP]
> You can ask me "Show me the cost stats" at any time to see a summary of how the assistants are being used in this session.
