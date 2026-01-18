---
name: file-manager
description: Lightweight Haiku sub-agent for creating and deploying new strategy files. Works with version-safety protocol to save changes under new filenames and deploy to both locations. Auto-triggers when saving code changes.
---

# File Manager Sub-Agent

## Purpose
Fast, cost-effective file creation and deployment using Haiku model for routine save operations.

## When to Auto-Trigger
Automatically spawn a Haiku sub-agent when user requests:
- "Save this as V8_2_BUGFIX"
- "Create new version with UI changes"
- "Deploy to both locations"
- "Save as V8_3_COPY_TRADING"

## Sub-Agent Configuration
```
Model: haiku
Tools: Read, Write, Bash
Type: general-purpose
```

## Operations

### 1. Create New Strategy File
**Input**: Base file, new filename, code changes
**Process**:
1. Read base file (if different from current)
2. Apply code changes (if provided) OR use provided complete code
3. Update class name to match new filename
4. Update version string in code (e.g., "V8.3" → displayed in UI title bar)
5. Save to project directory
6. Copy to NinjaTrader strategies folder
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
2. Copy to NinjaTrader strategies folder
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
- **Project**: `c:\Users\Mohammed Khalid\OneDrive\Desktop\WSGTA\Github\universal-or-strategy\`
- **NinjaTrader**: `C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\Strategies\`

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
✓ Updated class name: UniversalORStrategyV8_2_BUGFIX
✓ Updated version display: "V8.2 BUGFIX"
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

## Integration with Other Skills
- **version-safety**: Provides naming rules and safety protocols
- **version-manager**: Loads saved versions
- **docs-manager**: Updates changelog after file creation
