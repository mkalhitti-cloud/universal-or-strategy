# VERSIONING SYSTEM FOR V8 & V9

This document clarifies how versions are managed across the IDE, NinjaTrader, and PRODUCTION folders.

---

## The System (Simplified)

```
IDE DEVELOPMENT
├── V8_22.cs → Latest V8 being developed
├── V8_21.cs → Previous V8 iteration
└── older versions...

NinjaTrader LIVE TRADING
├── Strategy name: "UniversalORStrategyV8" (FIXED NAME - don't rename)
└── This strategy auto-loads the LATEST V8_XX.cs from IDE

PRODUCTION BACKUP
├── PRODUCTION/V8_22_STABLE/UniversalORStrategyV8_22.cs (Snapshot of tested version)
└── This is PROTECTED - don't modify
```

---

## Version Numbering

### V8 (Production Trading)
```
Format: V8_XX (where XX = version number)

Examples:
- V8_22 = Latest version, currently tested (in PRODUCTION/)
- V8_21 = Previous version (archived)
- V8_20 = Even older (archived)

Rule: Higher number = newer version
```

### V9 (Development)
```
Format: UniversalOR_V9_*.cs (multiple versions during development)

Examples:
- UniversalOR_V9_MasterHub.cs
- UniversalOR_V9_MasterHub_MILESTONE_LIVE_SUCCESS.cs

Rule: Different names represent different approaches being tested
Once V9 is ready, it becomes V8_23, V8_24, etc.
```

---

## File Locations

### Location 1: IDE Workspace (DEVELOPMENT)
```
universal-or-strategy/
├── V8_22.cs          ← Latest V8 being worked on
├── V8_21.cs          ← Previous V8 version
├── V9_ExternalRemote/
│   └── [WPF/TCP code]
├── UniversalORSlave.cs
├── UniversalORSlaveV7.cs
├── UniversalORSlaveV8.cs
├── UniversalORSlaveV8_13.cs
└── archived-versions/ ← Old stuff (V4, V5, V6, etc)
```

### Location 2: NinjaTrader (LIVE TRADING)
```
NinjaTrader Strategies Folder
└── UniversalORStrategyV8 (FIXED STRATEGY NAME - NEVER RENAME)
    └── Loads the LATEST V8_XX.cs from your development folder
```

**Important**: NinjaTrader has ONE strategy name but it auto-pulls latest code from IDE.

### Location 3: PRODUCTION (PROTECTED BACKUP)
```
universal-or-strategy/PRODUCTION/
├── V8_22_STABLE/
│   ├── UniversalORStrategyV8_22.cs (tested and working)
│   └── TESTED_DATE.txt (when it was last verified)
│
└── V9_STABLE/
    ├── UniversalOR_V9_MasterHub.cs
    └── UniversalOR_V9_MasterHub_MILESTONE_LIVE_SUCCESS.cs
```

**Important**: NEVER modify files in PRODUCTION/ - these are read-only snapshots for rollback.

---

## Workflow: Creating a New Version

### Step 1: Make Changes in IDE
```
You edit: V8_22.cs (in universal-or-strategy/ root)
Changes are auto-saved
```

### Step 2: Test in NinjaTrader
```
NinjaTrader strategy "UniversalORStrategyV8" automatically loads V8_22.cs
No manual loading needed - it's automatic
Test your changes
```

### Step 3: If Working → Create V8_23
```
Copy: V8_22.cs → V8_23.cs
This becomes the new "current" version
Edit: V8_23.cs for next iteration

(Keep V8_22.cs as backup in case V8_23 breaks)
```

### Step 4: If Major Change → Backup to PRODUCTION
```
Once V8_23 is tested and stable:
1. Copy V8_23.cs to PRODUCTION/V8_23_STABLE/UniversalORStrategyV8_23.cs
2. Create TESTED_DATE.txt in that folder
3. Commit to git with message: "prod: Promote V8_23 to production"
```

---

## Agent Instructions (CRITICAL)

### For Agents Updating V8:

```
When you make changes to V8 strategy:

1. Read current version from: V8_22.cs (in IDE root)
2. Make your changes/fixes
3. Create new version: V8_23.cs
4. Update PRODUCTION/V8_22_STABLE/ with TESTED_DATE.txt
5. Commit: "feat: V8_23 - [description of changes]"

The new V8_23.cs is AUTOMATICALLY loaded by NinjaTrader.
Don't worry about NinjaTrader sync - it happens automatically.
```

### For Agents Updating V9:

```
When you work on V9 (external app):

1. Read current code from: V9_ExternalRemote/ folder
2. Make your changes
3. Commit with appropriate message
4. If promoting to production-ready:
   - Copy to PRODUCTION/V9_STABLE/
   - Create backup with timestamp
   - Commit: "prod: Promote V9 to stable"
```

### For Agents Reading Current Version:

```
To know which version is LATEST:

1. Check PRODUCTION/V8_22_STABLE/TESTED_DATE.txt
   - Shows what was last tested

2. Look in IDE root for V8_XX.cs files
   - Highest number = current development version

3. For V9: Check .agent/SHARED_CONTEXT/V9_STATUS.json
   - Shows which V9 version is in use
```

---

## Example Workflow

### Initial State (Today, Jan 25)
```
IDE:         V8_22.cs (latest)
NinjaTrader: UniversalORStrategyV8 (uses V8_22)
PRODUCTION:  V8_22_STABLE/ (backup of Jan 23 tested version)
V9:          Two versions in PRODUCTION/V9_STABLE/
```

### After Market Open Testing (Sunday Evening)
```
V9_001 Agent tests and passes:
  ✓ V9 TOS RTD working

V9_003 Agent creates copy trading:
  ✓ Saves to V9_ExternalRemote/MainWindow.xaml.cs
  ✓ Commits changes
  ✓ Creates V9_COPY_TRADING_STATUS.json

Future: Once V9 is production-ready:
  Copy V9 logic → V8_23.cs
  Test in NinjaTrader
  Backup to PRODUCTION/V8_23_STABLE/
  New version becomes active automatically
```

---

## Key Rules

1. **IDE Development** (universal-or-strategy/ root):
   - V8_22.cs, V8_23.cs, etc. = All your working versions
   - Create new file each iteration (V8_23 if changing V8_22)
   - Higher number = newer version

2. **NinjaTrader Strategy Name** (FIXED):
   - Strategy is called "UniversalORStrategyV8" (don't rename)
   - It AUTOMATICALLY loads the latest V8_XX.cs from IDE
   - You don't manually load anything

3. **PRODUCTION Backups** (Protected):
   - PRODUCTION/V8_22_STABLE/ = Last tested snapshot
   - PRODUCTION/V9_STABLE/ = V9 development versions
   - NEVER edit these - they're read-only
   - Use for rollback if current version breaks

4. **Agent Uploads**:
   - Agents ALWAYS work on latest version
   - Agents automatically create new version numbers
   - Agents commit to git
   - PRODUCTION backups updated when version is tested

---

## Clearing Up Confusion

### Question 1: "Do I rename NinjaTrader strategy each version?"
**Answer**: NO. Strategy is ALWAYS named "UniversalORStrategyV8". It automatically loads your latest V8_XX.cs from the IDE folder. NinjaTrader stays the same - your code versions change.

### Question 2: "Do agents know to upload latest?"
**Answer**: YES. The prompts include instructions to:
- Read latest version file
- Make modifications
- Create new version number (V8_23 if V8_22 exists)
- Commit to git
- Agents coordinate through CURRENT_SESSION.md to know what's current

### Question 3: "Do we backup every version?"
**Answer**: NO. Only tested, stable versions get backed up to PRODUCTION/. Working versions stay in IDE root until they're tested and ready.

---

## File Naming Convention

### For V8 Files:
```
Format: V8_XX.cs (where XX = number)
Examples: V8_22.cs, V8_23.cs, V8_24.cs

Rule: Use ONLY in IDE root folder
NinjaTrader doesn't care about the name - it loads latest
```

### For V9 Files:
```
Format: UniversalOR_V9_*.cs (flexible during development)
Examples:
- UniversalOR_V9_MasterHub.cs
- UniversalOR_V9_MasterHub_MILESTONE_LIVE_SUCCESS.cs

Rule: Once V9 is ready, consolidate into single strategy
Then add to V8_23+
```

### For Archived Versions:
```
Location: archived-versions/
Format: Any (these are old, don't matter)
Examples: V4, V5, V6, old slave versions
```

---

## Next Steps for Agents

When V9_001 runs at market open:

1. **V9_001 Tests**: Verifies V9 TOS RTD works
   - Reads from PRODUCTION/V9_STABLE/ (two versions)
   - Tests both
   - Reports which one works

2. **V9_003 Develops**: Copy trading logic
   - Works on V9_ExternalRemote/MainWindow.xaml.cs
   - Creates new version if modifying (V9.1, V9.2, or same file with date)
   - Commits changes

3. **Integration Point**: Later merge V9 → V8
   - Once V9 is complete
   - Copy V9_ExternalRemote code → V8_23.cs (or new version)
   - Test in NinjaTrader
   - Backup to PRODUCTION/V8_23_STABLE/
   - V8_23 becomes active automatically

---

## Git Commit Examples

```bash
# When creating new V8 version:
git add V8_23.cs
git commit -m "feat: V8_23 - Add new copy trading logic"

# When promoting to production:
git add PRODUCTION/V8_23_STABLE/
git commit -m "prod: Promote V8_23 to stable after testing"

# When updating V9:
git add V9_ExternalRemote/
git commit -m "feat: V9 - Add TCP server functionality"

# When updating session status:
git add .agent/SHARED_CONTEXT/CURRENT_SESSION.md
git commit -m "docs: Update session status - V9_003 in progress"
```

---

**Summary**:
- **IDE**: Create new numbered versions (V8_22 → V8_23 → V8_24...)
- **NinjaTrader**: ONE strategy name "UniversalORStrategyV8" (auto-loads latest)
- **PRODUCTION**: Snapshots of tested versions (backup only)
- **Agents**: Always upload latest, create new versions, commit to git

Simple rule: **Highest version number in IDE root = what NinjaTrader is using.**
