---
name: version-safety
description: Enforces safe file versioning practices for NinjaTrader strategy development. Use this skill ALWAYS when creating new features, making updates, or saving any code changes. Prevents overwriting prior versions. Ensures new code is deployed to both the project and NinjaTrader for testing.
---

# Version Safety Protocol

## Core Rules

1. **NEVER overwrite existing files** - Always create a new file with a descriptive suffix
2. **NEVER auto-commit to GitHub** - Only commit when user explicitly requests
3. **ALWAYS deploy to both locations** - Project repo AND NinjaTrader strategies folder
4. **ALWAYS confirm filename** - Ask user before saving if suffix is unclear

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

1. **Project repo**: `c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy\`
2. **NinjaTrader**: `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies\`

## Workflow

### Before Making Changes

1. Identify the base file (e.g., `UniversalORStrategyV8_2.cs`)
2. Determine the new filename with descriptive suffix
3. Confirm with user: "I'll save this as `[NEW_FILENAME]`. Is that correct?"

### After Making Changes

1. Create the new file in the **project repo**
2. Update the class name inside the file to match the new filename
3. Copy the file to **NinjaTrader strategies folder**
4. Inform user: "Saved to both locations. Compile in NinjaTrader to test."

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
