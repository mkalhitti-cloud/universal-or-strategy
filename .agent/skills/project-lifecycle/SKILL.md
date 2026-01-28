---
name: project-lifecycle
description: Project management workflow for NinjaTrader strategy development. Use when starting new features/versions, creating branches, updating project documentation, or managing version transitions. Ensures proper backups, changelog updates, and documentation sync.
---

# Project Lifecycle Management

**Purpose:** Standardized workflow for managing NinjaTrader strategy versions, features, and documentation.
**Universal Path:** `${PROJECT_ROOT}`
**Executors:** ${BRAIN} (Reasoning), ${HANDS} (Gemini Flash via delegation_bridge)

---

## When to Use This Skill

- Starting a new feature or version (e.g., V7 → V7.1)
- Creating a new branch for development
- Updating project after code changes
- Managing version transitions
- Ensuring documentation stays in sync with code

---

## 1. Starting a New Feature/Version

### Pre-Flight Checklist
Before starting ANY new feature:

```markdown
- [ ] Current version is stable and tested
- [ ] All changes committed to git
- [ ] CHANGELOG.md is up to date
- [ ] README.md reflects current state
- [ ] No pending bug fixes
```

### Version Naming Convention

**Major Version (V6 → V7):**
- Breaking changes to architecture
- New strategy implementation
- Major refactor

**Minor Version (V7.0 → V7.1):**
- New feature added
- Significant enhancement
- Non-breaking improvements

**Patch Version (V7.1.0 → V7.1.1):**
- Bug fixes only
- Parameter tweaks
- Documentation updates

### Step-by-Step Process

#### Step 1: Create Backup
```powershell
# Create timestamped backup of current version
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupName = "UniversalORStrategy_V7_Backup_$timestamp.cs"
Copy-Item "Strategies\UniversalORStrategyV7.cs" "Strategies\Backups\$backupName"
```

#### Step 2: Update CHANGELOG.md
Add new section at the top:
```markdown
## [V7.1.0] - Pending
### Added
- [Feature description]

### Changed
- [What changed]

### Fixed
- [Bug fixes]

### Testing Status
- [ ] Compiles without errors
- [ ] Simulation tested
- [ ] Live tested (if applicable)
```

#### Step 3: Create New Version File
```powershell
# Copy current version to new version filename
Copy-Item "Strategies\UniversalORStrategyV10.cs" "Strategies\UniversalORStrategyV10_1.cs"

# V10+ PROTOCOL: DO NOT update class name.
# Keep: public class UniversalORStrategyV10
# This ensures the strategy stays on existing charts without re-adding.
```

#### Step 4: Update Strategy Metadata
In the new file, update version info in `OnStateChange()`:
```csharp
if (State == State.SetDefaults)
{
    // Update Name/Description to reflect the sub-version
    Name = "UniversalORStrategyV10"; // KEEP STABLE FOR NT8 UI
    Description = "Universal OR Strategy - V10.1 (New Feature Edition)";
    // ... rest of defaults
}
```

---

## 2. Branch Management

### When to Create a Branch

**Feature Branch:**
- New strategy implementation
- Major feature addition
- Experimental changes

**Hotfix Branch:**
- Critical bug in live trading
- Emergency parameter adjustment

### Branch Naming Convention
```
feature/[feature-name]     # e.g., feature/rma-trailing-stop
hotfix/[bug-name]          # e.g., hotfix/close0-bug
version/[version-number]   # e.g., version/v7.1
```

### Git Workflow
```powershell
# Create new feature branch
git checkout -b feature/new-feature-name

# Make changes, commit frequently
git add .
git commit -m "feat: Add [feature description]"

# When feature is complete and tested
git checkout main
git merge feature/new-feature-name
git tag -a v7.1.0 -m "Version 7.1.0 - [Feature name]"
```

---

## 3. Updating the Project

### After Code Changes Checklist

Every time you modify strategy code, update:

```markdown
- [ ] CHANGELOG.md (add to "Pending" section)
- [ ] README.md (if feature affects usage)
- [ ] Order_Management.xlsx (if parameters changed)
- [ ] Skills (if new patterns/lessons learned)
- [ ] Git commit with descriptive message
```

### Documentation Sync Protocol

**When to Update README.md:**
- New strategy added (ORB, RMA, etc.)
- Trading rules changed
- Performance targets updated
- File structure changed

**When to Update Order_Management.xlsx:**
- New parameters added
- Default values changed
- ATR multipliers adjusted
- Stop/target logic modified

**When to Update Skills:**
- New bug discovered and fixed
- New coding pattern established
- Lessons learned from live trading
- Apex/Rithmic compliance changes

---

## 4. Version Transition Protocol

### Moving from Development to Production

#### Phase 1: Development (Sim Account)
```markdown
1. Code new feature in V7.1 file
2. Compile and test in Strategy Analyzer
3. Run on Sim account for 1+ week
4. Document all issues in CHANGELOG
5. Fix bugs, update version to V7.1.1, V7.1.2, etc.
```

#### Phase 2: Staging (Funded Sim)
```markdown
1. Once stable, test on funded sim account
2. Monitor for 1+ week
3. Verify Apex compliance (daily loss, rate-limiting)
4. Check memory usage over 12+ hours
5. Validate execution speed < 50ms
```

#### Phase 3: Production (Live Funded)
```markdown
1. Create final backup of previous live version
2. Deploy new version to live account
3. Monitor first 3 days closely
4. Keep previous version ready for rollback
5. Update CHANGELOG with "Live" status
```

### Rollback Protocol

If new version fails in production:
```powershell
# Immediate rollback
1. Disable new strategy in NinjaTrader
2. Enable previous stable version
3. Document failure in CHANGELOG
4. Create hotfix branch to address issue
5. Do NOT deploy again until root cause fixed
```

---

## 5. Project Update Workflow

### Weekly Maintenance
```markdown
- [ ] Review CHANGELOG for completed items
- [ ] Update README if features went live
- [ ] Clean up old backup files (keep last 5)
- [ ] Verify skills reflect current best practices
- [ ] Check git tags match deployed versions
```

### Monthly Audit
```markdown
- [ ] Review all skills for outdated info
- [ ] Update performance benchmarks
- [ ] Archive old versions (>3 months)
- [ ] Update Order_Management.xlsx with live results
- [ ] Document lessons learned
```

---

## 6. Multi-Account Scaling Preparation

### When Scaling to 20 Accounts

**Phase 1: Single Account Mastery**
- V7 stable on 1 account for 3+ months
- All bugs resolved
- Consistent profitability

**Phase 2: 3-Account Test**
- Deploy to 3 accounts simultaneously
- Test account synchronization
- Verify no race conditions
- Monitor aggregate risk

**Phase 3: Full Deployment**
- Gradual rollout (5 accounts/week)
- Monitor system resources
- Ensure Rithmic connection stable
- Verify Apex compliance across all accounts

---

## 7. Emergency Procedures

### Critical Bug in Live Trading

**Immediate Actions:**
1. Disable strategy on all accounts
2. Flatten all positions manually
3. Create hotfix branch
4. Document bug in CHANGELOG
5. Fix and test on sim before re-deploying

### Data Feed Disconnect

**Recovery Protocol:**
1. Strategy should auto-flatten (per `apex-rithmic-trading` skill)
2. Verify all positions closed
3. Check for stranded orders
4. Document incident
5. Review disconnect handling code

---

## Related Skills
- [universal-or-strategy](../universal-or-strategy/SKILL.md) - Project context
- [trading-code-review](../trading-code-review/SKILL.md) - Pre-deployment checklist
- [apex-rithmic-trading](../apex-rithmic-trading/SKILL.md) - Account compliance
- [ninjatrader-strategy-dev](../ninjatrader-strategy-dev/SKILL.md) - Coding patterns
- [delegation-bridge](../delegation-bridge/SKILL.md) - Safe deployment execution
- [wearable-project](../antigravity-core/wearable-project.md) - Portability standards
