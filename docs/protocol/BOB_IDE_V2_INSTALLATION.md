# Bob IDE v2.0 Installation Documentation

**Date**: 2026-06-24
**VM**: 34.121.187.241 (malhitticrypto@universal-or-strategy)
**Context**: Wave 7 Phase 1 Recovery (130/161 complete)

## Installation Summary

### Pre-Installation State
- **Bob Shell CLI**: v1.0.4 installed at `/home/malhitticrypto/.npm-global/bin/bob`
- **Bob IDE**: v1.0.3 (previous version)
- **Wave 7 Status**: Phase 1 at 81% completion (130/161 epics)

### Installation Steps

1. **Downloaded Bob IDE v2.0.0**
   - File: `IBM-Bob-linux-amd64-1.121.0+bob2.0.0.deb`
   - Location: `/home/malhitticrypto/Downloads/`
   - Size: 191,189,056 bytes (182 MB)

2. **Installed via dpkg**
   ```bash
   sudo dpkg -i /home/malhitticrypto/Downloads/IBM-Bob-linux-amd64-1.121.0+bob2.0.0.deb
   ```
   
   Output:
   ```
   Preparing to unpack .../IBM-Bob-linux-amd64-1.121.0+bob2.0.0.deb ...
   Unpacking bobide (2.0.0) over (1.116.0+bob1.0.3) ...
   Setting up bobide (2.0.0) ...
   ```

3. **Verified Installation**
   ```bash
   which bobide
   # Output: /usr/bin/bobide
   
   bobide --version
   # Output: 1.121.0+bob2.0.0
   ```

### Post-Installation State

#### Bob IDE v2.0.0 (NEW)
- **Binary**: `/usr/bin/bobide`
- **Version**: 1.121.0+bob2.0.0
- **Commit**: ac4b543348a8c2624286dcee536af7380629336f
- **Architecture**: x64
- **Status**: ✅ Successfully installed

#### Bob Shell CLI v1.0.4 (UNCHANGED)
- **Binary**: `/home/malhitticrypto/.npm-global/bin/bob`
- **Version**: 1.0.4
- **Status**: ✅ Unaffected by Bob IDE installation
- **Usage**: Wave 7 autonomous execution scripts

## Key Findings

### Two Separate Tools

1. **Bob IDE** (`bobide`)
   - Full IDE application (VSCode-based editor)
   - Installed at: `/usr/bin/bobide`
   - Version: 2.0.0
   - Purpose: Interactive development environment
   - API Key: Configured separately in IDE settings

2. **Bob Shell CLI** (`bob`)
   - Command-line tool for automation
   - Installed at: `/home/malhitticrypto/.npm-global/bin/bob`
   - Version: 1.0.4
   - Purpose: Autonomous wave execution
   - API Key: `BOB_SHELL_API_KEY` environment variable

### Complete Separation
- ✅ Different binaries (`bobide` vs `bob`)
- ✅ Different installation paths (`/usr/bin/` vs `~/.npm-global/bin/`)
- ✅ Different version schemes (2.0.0 vs 1.0.4)
- ✅ Different API key configurations
- ✅ No conflicts or interference

### Impact on Wave 7 Execution
- ✅ **Zero impact** on ongoing Phase 1 recovery
- ✅ Bob Shell CLI v1.0.4 remains fully operational
- ✅ Wave 7 scripts continue to use `bob` command (v1.0.4)
- ✅ No changes to environment variables or configurations
- ✅ No changes to autonomous execution workflow

## Usage

### Launching Bob IDE v2.0.0
```bash
# Launch Bob IDE GUI
bobide

# Launch Bob IDE with specific project
bobide /home/malhitticrypto/universal-or-strategy
```

### Using Bob Shell CLI v1.0.4 (Wave Execution)
```bash
# Continue using bob command as before
bob --version  # 1.0.4

# Wave 7 scripts automatically use Bob Shell CLI
bash launch_wave7_phase1_recovery.sh
```

## API Key Configuration

### Bob IDE v2.0.0
- Configure via IDE settings/preferences
- Separate from Bob Shell CLI API key
- Can use different API key if desired

### Bob Shell CLI v1.0.4
- Uses `BOB_SHELL_API_KEY` environment variable
- Current status: Exhausted (31 epics failed)
- Next step: Replace with new API key

## Next Steps

1. **API Key Replacement** (Priority)
   - Replace exhausted `BOB_SHELL_API_KEY` for Bob Shell CLI
   - Continue Wave 7 Phase 1 recovery (31 failed epics)

2. **Bob IDE Configuration** (Optional)
   - Launch Bob IDE: `bobide`
   - Configure API key in IDE settings
   - Explore v2.0 features

3. **Wave 7 Continuation**
   - Bob Shell CLI remains operational
   - No changes needed to wave execution scripts
   - Continue autonomous refactoring workflow

## Verification Commands

```bash
# Verify Bob IDE v2.0.0
which bobide          # /usr/bin/bobide
bobide --version      # 1.121.0+bob2.0.0

# Verify Bob Shell CLI v1.0.4 (unchanged)
which bob             # /home/malhitticrypto/.npm-global/bin/bob
bob --version         # 1.0.4

# Check both are accessible
type bobide           # bobide is /usr/bin/bobide
type bob              # bob is /home/malhitticrypto/.npm-global/bin/bob
```

## References

- **Bob v2.0 Release**: https://bob.ibm.com/blog/bob-v2-release-announcement
- **Bob Shell CLI**: `/home/malhitticrypto/.npm-global/bin/bob`
- **Bob IDE**: `/usr/bin/bobide`
- **Wave 7 Status**: 130/161 epics complete (81%)
- **Installation Package**: `IBM-Bob-linux-amd64-1.121.0+bob2.0.0.deb`

## Notes

- Bob IDE v2.0 is a complete IDE application (VSCode-based)
- Bob Shell CLI is a separate command-line automation tool
- Both tools coexist independently with no conflicts
- Wave 7 autonomous execution uses Bob Shell CLI only
- Bob IDE installation does not affect ongoing wave execution
- Upgrade was clean: v1.0.3 → v2.0.0 (no issues)