# Workspace Rules: Universal OR Strategy

## CORE INSTRUCTIONS
1. **Source of Truth**: Refer to [AGENT.md](../../AGENT.md) for all architectural context, trading rules, and version history.
2. **Terminal Commands**: ALWAYS run terminal commands instead of asking the user to perform manual file operations.
3. **Dual-Deployment**: ALWAYS deploy strategy files to both the Project Repository and the NinjaTrader bin folder using the automated deployment script.
4. **Non-Coder Protocol**: ALWAYS provide complete, compilable code ready for Mo to compile.
5. **Direct Connectivity**: NEVER build or use Excel-based RTD bridges. ALWAYS use the direct `TosRtdClient` for TOS data integration to ensure reliability.

## AUTOMATION
- Use `scripts/ninja_deploy.ps1` for all deployment tasks.
- Use `delegation_bridge` to route expensive I/O to Gemini Flash.

## ASSISTANT DELEGATION
- **Cost Optimization**: Claude Code CLI agents MUST always use `claude_haiku_4.5` or `gemini_flash_3.0` (via delegation bridge) for routine tasks (file operations, documentation, list files).
- **Core Models**: `claude_opus_4.5` and `claude_sonnet_4.5` are reserved for complex logic, architecture, and user interaction.
- **Verification**: Verify assistant usage by checking `.agent/state/cost_tracking.json` and observing the agent's reported model for sub-tasks.
