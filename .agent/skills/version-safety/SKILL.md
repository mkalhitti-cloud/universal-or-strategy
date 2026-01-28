name: version-safety
description: Enforces safe file versioning practices for NinjaTrader strategy development. Use this skill ALWAYS when creating new features, making updates, or saving any code changes. Prevents overwriting prior versions. Ensures new code is deployed to both the project and NinjaTrader for testing.

# Version Safety Protocol

**Context:** Protection of code history and operational continuity
**Platform:** NinjaTrader 8, GitHub Repo
**Universal Path:** `${PROJECT_ROOT}`
**Executors:** ${BRAIN} (Reasoning), ${HANDS} (Gemini Flash via delegation_bridge)

## Core Rules

1. **NEVER overwrite existing files** - Always create a new file with a descriptive suffix
2. **NEVER auto-commit to GitHub** - Only commit when user explicitly requests
3. **ALWAYS deploy to both locations** - Project repo AND NinjaTrader strategies folder
4. **ALWAYS use Stable Class Names for Major Series** - In V10+, keep the class name (e.g., `UniversalORStrategyV10`) stable even if the filename changes.
5. **ALWAYS archive older versions in NinjaTrader** - Rename old `.cs` files in the NT folder to `.cs.bak` before deploying the latest version.
6. **ALWAYS confirm filename** - Ask user before saving if suffix is unclear.

## File Naming Convention

Use descriptive suffixes that explain the change:

| Change Type | Naming Pattern | Example |
|-------------|----------------|---------|
| UI changes | `_UI_[DESCRIPTION]` | `V8_2_UI_HORIZONTAL.cs` |
| Bug fixes | `_BUGFIX` or `_FIX_[WHAT]` | `V8_2_FIX_STOPS.cs` |
| New feature | `_[FEATURE_NAME]` | `V8_2_COPY_TRADING.cs` |
| Performance | `_PERF` or `_OPTIMIZE` | `V8_2_PERF.cs` |
| Cleanup | `_CLEANUP` | `V8_2_CLEANUP.cs` |
| Milestone | `_MILESTONE` | `V8_2_MILESTONE.cs` |

When uncertain about the suffix, ask the user.

## Deployment Locations

For this project, always deploy to:

1. **Project repo**: `${PROJECT_ROOT}/`
2. **NinjaTrader**: `C:/Users/${USERNAME}/Documents/NinjaTrader 8/bin/Custom/Strategies/`

## Workflow

### Before Making Changes

1. Identify the base file (e.g., `UniversalORStrategyV8_2.cs`)
2. Determine the new filename with descriptive suffix
3. Confirm with user: "I'll save this as `[NEW_FILENAME]`. Is that correct?"

### After Making Changes

1. Create the new file in the **project repo**
2. **KEEP the stable class name** (e.g., `UniversalORStrategyV10`) inside the file, regardless of the filename.
3. **RENAMING OLD FILES**: Before copying to NinjaTrader, rename any existing `.cs` file with the same class name in the NT folder to `.bak`.
4. Copy the new file to **NinjaTrader strategies folder**.
5. Inform user: "Saved to both locations. Previous version archived as `.bak` in NT folder. Compile in NinjaTrader to test."

### What NOT To Do

- Do NOT use `Edit` on the original file - use `Write` to create new file
- Do NOT run `git add`, `git commit`, or `git push` unless user explicitly asks
- Do NOT delete or archive original files unless user requests
- Do NOT assume the user wants GitHub updated

## Example Interaction

**User**: "Add a new button to the UI"

**Agent response**:
1. "I'll create this as `UniversalORStrategyV8_2_UI_NEWBUTTON.cs`. Sound good?"
2. [After user confirms] Make changes
3. Save to project repo
4. Copy to NinjaTrader
5. "Done. Files saved to:
   - Project: `UniversalORStrategyV8_2_UI_NEWBUTTON.cs`
   - NinjaTrader: `UniversalORStrategyV8_2_UI_NEWBUTTON.cs`

   Open NinjaTrader and compile (F5) to test. Original V8_2 is unchanged."

## Related Skills
- [file-manager](../file-manager/SKILL.md) - File operations and deployment
- [delegation-bridge](../delegation-bridge/SKILL.md) - Safe deployment execution
- [wearable-project](../antigravity-core/wearable-project.md) - Portability standards
- [universal-or-strategy](../universal-or-strategy/SKILL.md) - Project context

