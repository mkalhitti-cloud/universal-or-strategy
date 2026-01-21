# Wearable Project Standards

A "wearable" project is one that can be picked up by any AI agent in any IDE (Antigravity, Cursor, Windsurf, Claude Code CLI) or terminal without losing context or violating project rules.

## Standard 1: State Centralization
All project state MUST live in `${PROJECT_ROOT}/.agent/`.

- `.agent/PROJECT_STATE.md`: High-level summary of what was done, what is in progress, and what is next.
- `.agent/UNANSWERED_QUESTIONS.md`: A log of blockers or questions for the user.
- `.agent/state/session_state.json`: Technical state (current versions, deployment paths, current feature status).
- `.agent/state/cost_tracking.json`: Usage and cost metrics across all models.

## Standard 2: Variable Resolution
Never use absolute local paths. Always use variables that can be resolved per-machine:

- `${PROJECT_ROOT}`: The root of the git repository.
- `${USERNAME}`: The current OS user (for bin paths).
- `${NINJATRADER_BIN}`: Resolved from `${USERNAME}`.

## Standard 3: The Delegation Protocol (Brain & Hands)
Every agent MUST follow the delegation bridge protocol:

1. **Reasoning (Brain)**: The current model (Opus, Sonnet, etc.) handles logic, architecture, and user interaction.
2. **Execution (Hands)**: The model delegates file I/O, dual-deployment, and documentation updates to Gemini Flash via the `delegation_bridge` MCP.
3. **Continuity**: After every major execution, the agent MUST update `.agent/PROJECT_STATE.md` to ensure the next agent (or the next session) starts with perfect context.

## Standard 4: Full Code Implementation
"Wearable" code is complete and compilable. Do not provide snippets. Every code delivery must be a full file or a complete, drop-in replacement that an agent can apply blindly with high confidence.

## Standard 5: Dual-Deployment
Every NinjaTrader strategy change must be deployed to:
1. The project repository (for version control).
2. The NinjaTrader `bin` directory (for immediate compilation and testing).

This ensures the environment is always ready for the user to "wear" and trade.
