---
name: code-formatter
description: Lightweight Haiku sub-agent for cleaning up C# code. Removes commented code, fixes indentation, removes debug Print() statements, and formats code consistently. Auto-triggers on cleanup requests.
---

# Code Formatter Sub-Agent

## Purpose
Fast code cleanup using Haiku model for routine formatting and cleanup operations.

## When to Auto-Trigger
Automatically spawn a Haiku sub-agent when user requests:
- "Clean up the code"
- "Remove commented code"
- "Remove debug statements"
- "Format the code"
- "Clean up V8_3"

## Sub-Agent Configuration
```
Model: haiku
Tools: Read, Edit
Type: general-purpose
```

## Operations

### 1. Remove Commented Code
**What to remove**:
- Single-line comments that are code: `// int oldValue = 5;`
- Multi-line comment blocks containing old code
- Commented-out method calls: `// Print("Debug message");`

**What to KEEP**:
- Documentation comments explaining logic
- TODOs and FIXMEs
- Region markers: `#region`, `#endregion`
- Comments explaining WHY (not WHAT)

**Example**:
```csharp
// REMOVE this:
// double oldStop = entry + 5;
// Print("Old stop calculation");

// KEEP this:
// Calculate stop based on ATR to adapt to volatility
double stop = entry + (currentATR * 0.5);
```

### 2. Remove Debug Print() Statements
**What to remove**:
- Temporary debug prints: `Print("DEBUG: value = " + value);`
- Verbose logging during development
- Test messages: `Print("Hit this line");`

**What to KEEP**:
- Production logging (errors, warnings)
- User-facing status messages
- Critical state information

**Example**:
```csharp
// REMOVE:
Print("DEBUG: Entering OnBarUpdate");
Print("Price: " + Close[0]);

// KEEP:
Print("⚠ STOP VALIDATION FAILED - flattening position");
Print($"OR LONG triggered at {entryPrice}");
```

### 3. Fix Indentation
**Rules**:
- Use tabs (not spaces) - NinjaTrader convention
- Proper nesting for braces
- Align property declarations
- Consistent spacing around operators

### 4. Remove Empty Lines
**Rules**:
- No more than 1 blank line between methods
- No blank lines at start/end of methods
- No blank lines before closing braces

### 5. Format Property Declarations
**Make consistent**:
```csharp
// Before:
[Range(1, 100)]
[Display(Name="Risk Per Trade", Order=1, GroupName="1. Risk Management")]
public double RiskPerTrade
{ get; set; }

// After:
[Range(1, 100)]
[Display(Name = "Risk Per Trade", Order = 1, GroupName = "1. Risk Management")]
public double RiskPerTrade { get; set; }
```

## What NOT to Change

### NEVER Modify:
1. **Trading Logic** - Any calculation, condition, or order management
2. **Variable Names** - Don't rename even if you think it's better
3. **Method Signatures** - Don't change parameter names or return types
4. **String Literals** - Don't change text in Print() statements
5. **Comments with Context** - Explanatory comments stay

### Pattern Detection:
If you see patterns like these, **DO NOT CHANGE**:
- `// CRITICAL:` - Important warning comment
- `// v8.2 change:` - Version tracking comment
- `// Prevents stop-at-market rejection` - Explanatory comment
- `#region UI Creation` - Code organization

## Workflow

### Before Cleanup
1. Read the entire file
2. Create a backup suggestion: "I'll clean up V8_3. Want me to save original as V8_3_BACKUP first?"
3. Wait for user confirmation

### During Cleanup
1. Remove commented code (verify it's not documentation)
2. Remove debug Print() statements
3. Fix indentation
4. Remove excess blank lines
5. Format property declarations

### After Cleanup
1. Report what was changed:
   - Lines of commented code removed
   - Debug statements removed
   - Formatting fixes applied
2. Suggest: "Review changes before compiling"

## Safety Rules
- ALWAYS create backup before cleanup (use version-safety protocol)
- NEVER remove comments that explain WHY
- NEVER change trading logic
- NEVER rename variables/methods
- ALWAYS preserve production logging

## Example Interactions

**User**: "Clean up V8_2_UI_HORIZONTAL"

**Haiku Agent**:
```
Found file: UniversalORStrategyV8_2_UI_HORIZONTAL.cs

Should I create a backup first? This will:
1. Save current file as V8_2_UI_HORIZONTAL_BACKUP.cs
2. Clean up the original
3. Report changes made

Proceed? (yes/no)
```

**User**: "yes"

**Haiku Agent**:
```
✓ Backup created: V8_2_UI_HORIZONTAL_BACKUP.cs

Cleaning up...
✓ Removed 23 lines of commented code
✓ Removed 8 debug Print() statements
✓ Fixed indentation in 12 blocks
✓ Removed 15 excess blank lines
✓ Formatted 34 property declarations

File cleaned. Review changes in diff before compiling.
```

## Integration with Other Skills

### Works With:
- **version-safety**: Creates backup before cleanup
- **file-manager**: Can deploy cleaned version
- **docs-manager**: Can add cleanup note to changelog

### Use Cases:
1. Before milestone release - clean up development artifacts
2. After major feature - remove debug code
3. Code review prep - ensure consistent formatting
4. Pre-production - remove all temporary code

## Pattern Recognition

### Commented Code Patterns (REMOVE):
```csharp
// double oldValue = 5;
// if (condition) { DoSomething(); }
// Print("debug message");
/* Old implementation
   that was replaced
*/
```

### Documentation Comments (KEEP):
```csharp
// This prevents stop-at-market rejection by adding buffer
// CRITICAL: Don't modify this without testing on Rithmic
// TODO: Consider making this configurable
/// <summary>
/// Validates stop price before submission
/// </summary>
```

## Example Output Format

After cleanup, report in this format:

```markdown
✓ Cleanup Complete: V8_2_UI_HORIZONTAL.cs

Changes:
- Commented code: 23 lines removed
- Debug Print(): 8 statements removed
- Indentation: 12 blocks fixed
- Blank lines: 15 removed
- Properties: 34 formatted

File size: 177KB → 175KB (2KB saved)

Next: Review diff and compile in NinjaTrader (F5)
```
