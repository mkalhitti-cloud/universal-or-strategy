name: version-manager
description: Standardized protocol for listing, switching, and comparing strategy versions using the Gemini Flash delegation bridge. Ensures correct version is active across all developmental environments.

# Version Manager Sub-Agent

## Purpose
Fast, cost-effective version management using Haiku model for simple file operations.

## When to Auto-Trigger
Automatically spawn a Haiku sub-agent when user requests:
- "Load V8_3"
- "Switch to V8_2_UI_HORIZONTAL"
- "What versions are available?"
- "Which version is in NinjaTrader?"
- "Deploy V8_2_BUGFIX to NinjaTrader"

## Sub-Agent Configuration
```
Primary Executor: Gemini 3 Flash (via delegation_bridge MCP)
Protocol: Wearable Project Standard
Tools: call_gemini_flash (read_file/deploy), grep_search
Type: Version Control Specialist
```

## Operations

### 1. List Available Versions
- Search project directory for `UniversalORStrategy*.cs` files
- Parse version numbers and suffixes
- Display in chronological order
- Show file sizes and modification dates

### 2. Load Version to NinjaTrader
- Copy specified version from project to NinjaTrader strategies folder
- Verify file copied successfully
- Report what's now loaded
- DO NOT modify the project file

### 3. Check Current Version
- Compare file in NinjaTrader vs project files
- Report which version is currently deployed
- Show last modified timestamp

### 4. Version Comparison
- Show differences between two versions (file size, dates)
- List what changed (based on naming suffix)

## Locations
- **Project**: `${PROJECT_ROOT}/`
- **NinjaTrader**: `C:/Users/${USERNAME}/Documents/NinjaTrader 8/bin/Custom/Strategies/`

## Workflow

### Loading a Version
1. Verify version exists in project
2. Copy to NinjaTrader strategies folder
3. Report: "Loaded [VERSION] to NinjaTrader. Compile (F5) to use."

### Listing Versions
1. Find all `UniversalORStrategy*.cs` files
2. Sort by version number
3. Display with metadata

### Checking Current Version
1. Read file in NinjaTrader
2. Check version string in code
3. Report which version is active

## Safety Rules
- NEVER modify files in the project directory
- NEVER delete files without explicit user request
- ALWAYS verify file exists before copying
- ALWAYS report success/failure clearly

## Example Interactions

**User**: "Load V8_2_UI_HORIZONTAL"
**Agent Response** (via Haiku sub-agent):
```
✓ Found: UniversalORStrategyV8_2_UI_HORIZONTAL.cs
✓ Copied to NinjaTrader strategies folder
✓ Size: 177,599 bytes
Ready to compile. Press F5 in NinjaTrader.
```

**User**: "What versions are available?"
**Agent Response** (via Haiku sub-agent):
```
Available versions in project:
1. UniversalORStrategyV8.cs (175,000 bytes, Jan 17)
2. UniversalORStrategyV8_2.cs (175,244 bytes, Jan 17)
3. UniversalORStrategyV8_2_UI_HORIZONTAL.cs (177,599 bytes, Jan 17)
4. UniversalORStrategyV8_3.cs (177,599 bytes, Jan 17)

Currently in NinjaTrader: V8_2 (original)
```

## Related Skills
- [version-safety](../version-safety/SKILL.md) - Version creation rules
- [delegation-bridge](../delegation-bridge/SKILL.md) - Version operation logic
- [wearable-project](../antigravity-core/wearable-project.md) - Portability standards

---
*Optimized for Gemini 3 Flash delegation via bridge (2026-01-21)*
