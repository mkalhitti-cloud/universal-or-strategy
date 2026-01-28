name: file-manager
description: Standardized protocol for file operations, version control, and dual-deployment using the Gemini Flash delegation bridge. Use for all save, copy, and deployment tasks.

# File Manager Sub-Agent

## Purpose
Fast, cost-effective file creation and deployment using **Gemini Flash 3.0 via MCP delegation bridge** for routine save operations. Falls back to Haiku if MCP unavailable.

⭐ **NEW**: Claude Code CLI now supports MCP delegation! File operations in Claude Code CLI automatically delegate to Gemini Flash (99% cheaper than Haiku).

## When to Auto-Trigger
Automatically spawn a Gemini Flash sub-agent (via MCP) when user requests:
- "Save this as V8_2_BUGFIX"
- "Create new version with UI changes"
- "Deploy to both locations"
- "Save as V8_3_COPY_TRADING"

## Sub-Agent Configuration (Multi-AI)
```
Primary Executor: Gemini 3 Flash (via delegation_bridge MCP)
Fallback Executor: Haiku (if MCP unavailable)
Tools: call_gemini_flash, read_file, write_to_file
Type: Deployment specialist ("The Hands")
```

## 🧠 HANDOFF: The "Brain & Hands" Protocol
This skill is designed to receive "Manifests" from higher-tier reasoning models (Opus/Sonnet) to handle expensive file I/O operations at a lower cost.

### How it works:
1. **The Brain (Opus/Sonnet)** provides the complete code block in the chat.
2. **The User** switches the model to **Gemini 3 Flash**.
3. **The Hands (Gemini Flash)** triggers this skill with the request: *"Apply the Brain Manifest to save and deploy."*
4. **Operations**:
   - Extraction: Pull the code from the previous turn's output.
   - Validation: Ensure the code is complete (compilable) as per Mo's rules.
   - Deployment: Save to Project + NinjaTrader folders.
   - Reporting: Provide a success report and compile instructions.

## Operations

### 1. Create New Strategy File
**Input**: Base file, new filename, code changes
**Process**:
1. Read base file (if different from current)
2. Apply code changes (if provided) OR use provided complete code
3. Update class name to match new filename
4. Update version string in code (e.g., "V8.3" → displayed in UI title bar)
5. Save to project directory
6. Run `powershell -ExecutionPolicy Bypass -File "scripts/ninja_deploy.ps1" -SourceFileName <filename>` to update class name and deploy to NinjaTrader.
7. Report success

**Example**:
```
Base: UniversalORStrategyV8_2.cs
New: UniversalORStrategyV8_2_BUGFIX.cs
Changes: [provided by Sonnet agent]

Actions:
1. Write new file to project
2. Update: class name "UniversalORStrategyV8_2_BUGFIX"
3. Update: version display "V8.2 BUGFIX"
4. Copy to NinjaTrader
5. Report: "Created V8_2_BUGFIX in both locations"
```

### 2. Deploy Existing File
**Input**: Filename to deploy
**Process**:
1. Verify file exists in project
2. Run `powershell -ExecutionPolicy Bypass -File "scripts/ninja_deploy.ps1" -SourceFileName <filename>`
3. Report success

### 3. Update Class Name
**Input**: Filename
**Process**:
1. Read file
2. Find class declaration line
3. Update class name to match filename (without .cs extension)
4. Update version display string
5. Save file

## Locations
- **Project**: `${PROJECT_ROOT}/`
- **NinjaTrader**: `C:/Users/${USERNAME}/Documents/NinjaTrader 8/bin/Custom/Strategies/`

## Workflow Integration

### Full Save Workflow (with Sonnet)
1. **Sonnet**: Makes code changes, generates complete new file content
2. **Sonnet**: Calls file-manager sub-agent with:
   - New filename
   - Complete file content
3. **Haiku sub-agent**:
   - Writes file to project
   - Updates class name/version
   - Copies to NinjaTrader
   - Reports success
4. **Sonnet**: Reports to user

### Version Naming Convention (from version-safety)
Uses descriptive suffixes:
- `_UI_[DESCRIPTION]` - UI changes
- `_BUGFIX` or `_FIX_[WHAT]` - Bug fixes
- `_[FEATURE_NAME]` - New features
- `_PERF` or `_OPTIMIZE` - Performance
- `_CLEANUP` - Code cleanup
- `_MILESTONE` - Major milestone

## Safety Rules (from version-safety)
- NEVER overwrite existing files
- ALWAYS create new file with descriptive suffix
- ALWAYS deploy to BOTH locations
- NEVER auto-commit to GitHub

## Class Name and Version Display

### Class Name
Must match filename without extension:
```
File: UniversalORStrategyV8_2_UI_HORIZONTAL.cs
Class: public class UniversalORStrategyV8_2_UI_HORIZONTAL : Strategy
```

### Version Display
Update the UI title bar version string:
```csharp
// Find and update this pattern:
Content = "═══ OR Strategy V8.2 UI HORIZONTAL ═══"
```

## Example Interactions

**Sonnet to file-manager agent**: "Create V8_2_BUGFIX with this code"
**Haiku agent**:
```
✓ Created: UniversalORStrategyV8_2_BUGFIX.cs
✓ Running deployment script...
✓ Updated class name: UniversalORStrategyV8_2_BUGFIX
✓ Saved to project (177KB)
✓ Copied to NinjaTrader
Ready to compile (F5).
```

**Sonnet to file-manager agent**: "Deploy V8_3 to both locations"
**Haiku agent**:
```
✓ Found: UniversalORStrategyV8_3.cs
✓ Already in project
✓ Copied to NinjaTrader
Both locations synced.
```

## Related Skills
- [version-safety](../version-safety/SKILL.md) - Naming and safety rules
- [delegation-bridge](../delegation-bridge/SKILL.md) - Implementation of the "Hands" protocol
- [wearable-project](../antigravity-core/wearable-project.md) - Portability standards
- [docs-manager](../docs-manager/SKILL.md) - Documentation sync

---
*Optimized for Gemini 3 Flash delegation via bridge (2026-01-21)*
