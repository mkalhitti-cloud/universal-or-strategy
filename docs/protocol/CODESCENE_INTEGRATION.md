# CodeScene Integration Protocol (V12.24)

**Effective**: 2026-06-03  
**Status**: ACTIVE  
**Owner**: Director + All Agents

## Overview

CodeScene integration provides **multi-signal code quality analysis** combining complexity, churn, and code health metrics. This protocol governs three integration points:

1. **Epic Planning** (Integration 1): Automated hotspot analysis for refactoring prioritization
2. **Pre-Push Validation** (Integration 2): Local code health checks before commits
3. **PR Quality Gate** (Integration 3): Automated PR blocking on code health degradation

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    CodeScene Integration                     │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ Integration 1│  │ Integration 2│  │ Integration 3│      │
│  │ Epic Planner │  │  Pre-Push    │  │  PR Quality  │      │
│  │              │  │  Validation  │  │     Gate     │      │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘      │
│         │                  │                  │              │
│         ▼                  ▼                  ▼              │
│  ┌──────────────────────────────────────────────────┐      │
│  │         CodeScene CLI (cs command)                │      │
│  │  - cs review <file>  (code health scoring)       │      │
│  │  - cs delta --staged (change impact analysis)    │      │
│  └──────────────────────────────────────────────────┘      │
│                           │                                  │
│                           ▼                                  │
│  ┌──────────────────────────────────────────────────┐      │
│  │      jcodemunch-mcp (get_hotspots tool)          │      │
│  │  - Complexity × log(1 + churn) scoring           │      │
│  │  - Git history analysis (90-day window)          │      │
│  └──────────────────────────────────────────────────┘      │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

## Integration 1: Epic Planning (Automated Hotspot Analysis)

### Purpose
Replace complexity-only epic planning with **multi-signal hotspot analysis** combining:
- **Hotspot Score** (40%): Complexity × log(1 + churn)
- **Code Health** (30%): CodeScene 0-10 rating
- **Severity** (20%): P0-P3 classification
- **Churn Rate** (10%): Commits per week

### Tool
**Script**: `scripts/epic_planner.py`

### Usage
```powershell
# Analyze all high-complexity functions (CYC > 15)
python scripts/epic_planner.py

# Analyze specific file
python scripts/epic_planner.py --file src/V12_002.Entries.RMA.cs

# Custom complexity threshold
python scripts/epic_planner.py --min-complexity 20

# Custom churn window
python scripts/epic_planner.py --days 180
```

### Output
```json
{
  "epic_candidates": [
    {
      "rank": 1,
      "symbol": "MonitorRmaProximity",
      "file": "src/V12_002.Entries.RMA.cs",
      "hotspot_score": 92.49,
      "code_health": 6.82,
      "complexity": 23,
      "churn_rate": 2.1,
      "severity": "P2",
      "composite_score": 87.3,
      "recommendation": "URGENT - High complexity + high churn + low code health"
    }
  ]
}
```

### Workflow Integration
1. **Before Epic Planning**: Run `epic_planner.py` to generate ranked candidate list
2. **Review Output**: Prioritize top 5 candidates by composite score
3. **Create Epic**: Use highest-ranked candidate for next EPIC-CCN-X
4. **Archive Results**: Save output to `docs/brain/epic_planning_YYYYMMDD.json`

## Integration 2: Pre-Push Validation (Local Code Health Check)

### Purpose
Block commits that degrade code health **before** they reach GitHub.

### Tool
**Script**: `scripts/pre_push_validation.ps1` (Check #14)

### Trigger
- Automatically runs in Bob CLI before every commit
- Manually run via `powershell -File .\scripts\pre_push_validation.ps1`

### Check Logic
```powershell
cs delta --staged --output-format json
# PASS: No high-severity issues (indication < 3)
# FAIL: High-severity issues detected (indication >= 3)
```

### Thresholds
- **Blocking**: High-severity code health issues (indication ≥ 3)
- **Non-blocking**: CLI not installed or CS_ACCESS_TOKEN not set

### Output
```
[14/14] CodeScene Delta Analysis
✅ PASS: No code health degradation detected
   Results saved to: codescene_delta.json
```

### Configuration
**Environment Variable**: `CS_ACCESS_TOKEN` (set in `.env` or session)

```powershell
# Set token (PowerShell)
$env:CS_ACCESS_TOKEN = "your-token-here"

# Set token (Bash)
export CS_ACCESS_TOKEN="your-token-here"
```

## Integration 3: PR Quality Gate (Automated PR Blocking)

### Purpose
Enforce code health standards at the PR level via GitHub Actions.

### Tool
**Workflow**: `.github/workflows/codescene-quality-gate.yml`

### Trigger
- `pull_request` events: `opened`, `synchronize`, `reopened`
- Target branches: `main`, `feature/**`, `src-only/**`

### Quality Gate Logic
```yaml
FAIL if:
  - High-severity issues > 0 (indication >= 3)
  - Code health delta < -1.0 points

PASS if:
  - High-severity issues = 0
  - Code health delta >= -1.0 points
```

### Workflow Steps
1. **Checkout**: Fetch full git history for delta analysis
2. **Install CLI**: Download and install CodeScene CLI
3. **Run Delta**: Compare PR branch against base branch
4. **Parse Results**: Extract high-severity count and health delta
5. **Comment PR**: Post detailed analysis to PR comments
6. **Upload Artifact**: Save `codescene_delta.json` for 30 days
7. **Block/Pass**: Fail workflow if quality gate fails

### PR Comment Format
```markdown
## ✅ CodeScene Quality Gate: PASSED

🟢 **Code Health Delta**: +0.5 points
🟢 **High-Severity Issues**: 0

### Analysis Details

✨ **Great work!** This PR maintains or improves code health.

<details>
<summary>📊 View Full Delta Report</summary>

```json
{ ... }
```

</details>

---
*Powered by CodeScene CLI | V12 Quality Gate Protocol*
```

### Configuration
**GitHub Secret**: `CODESCENE_API_TOKEN` (set in repository settings)

```
Settings → Secrets and variables → Actions → New repository secret
Name: CODESCENE_API_TOKEN
Value: <your-token>
```

## Jane Street Alignment

### Hotspot Methodology
CodeScene's hotspot analysis aligns with Jane Street's **cognitive simplicity** principle:

- **Complexity**: Functions with CYC > 15 are harder to reason about under microsecond latency constraints
- **Churn**: High change frequency indicates unpredictable behavior (anti-pattern in HFT)
- **Coupling**: Files that change together reveal hidden dependencies (god-module risk)

### Code Health Scoring
CodeScene's 0-10 code health score maps to Jane Street's maintainability criteria:

| Score | Jane Street Interpretation | Action |
|-------|---------------------------|--------|
| 9-10  | Production-ready | Maintain |
| 7-8   | Acceptable with monitoring | Watch |
| 5-6   | Technical debt accumulating | Refactor soon |
| 3-4   | High cognitive load | Refactor now |
| 0-2   | Unmaintainable | Emergency refactor |

### Multi-Signal Fusion
Jane Street's decision-making process emphasizes **multiple independent signals**:

1. **Structural** (Complexity): AST-derived cyclomatic complexity
2. **Behavioral** (Churn): Git history analysis
3. **Cognitive** (Code Health): CodeScene's proprietary scoring
4. **Severity** (Impact): V12 P0-P3 classification

No single signal dominates; composite scoring prevents false positives.

## Validation Period (2026-06-03 to 2026-06-17)

### Integration 3 Validation
- **Duration**: 2 weeks
- **Mode**: WARNING (non-blocking)
- **Purpose**: Calibrate thresholds and validate accuracy
- **Metrics to Track**:
  - False positive rate (PRs blocked incorrectly)
  - False negative rate (PRs passed with degradation)
  - Developer friction (time to resolve issues)

### Post-Validation
- **Date**: 2026-06-17
- **Action**: Switch to BLOCKING mode if validation successful
- **Criteria**: False positive rate < 5%, developer approval

## Troubleshooting

### Issue: CodeScene CLI not found
**Solution**: Install CLI
```powershell
# Windows (PowerShell)
Invoke-WebRequest -Uri 'https://downloads.codescene.io/enterprise/cli/install-cs-tool.ps1' -OutFile install-cs-tool.ps1
.\install-cs-tool.ps1

# Linux/macOS
curl -fsSL https://downloads.codescene.io/enterprise/cli/install-cs-tool.sh | bash
```

### Issue: CS_ACCESS_TOKEN not set
**Solution**: Set environment variable
```powershell
# Load from .env file
Get-Content .env | ForEach-Object {
    if ($_ -match '^CODESCENE_API_TOKEN=(.+)$') {
        $env:CS_ACCESS_TOKEN = $matches[1]
    }
}

# Or set directly
$env:CS_ACCESS_TOKEN = "your-token-here"
```

### Issue: GitHub Actions workflow fails with 401
**Solution**: Verify GitHub secret
1. Go to repository Settings → Secrets and variables → Actions
2. Verify `CODESCENE_API_TOKEN` exists
3. Regenerate token if expired
4. Update secret value

### Issue: Delta analysis returns no results
**Solution**: Check git history
```powershell
# Verify base branch exists
git fetch origin main

# Verify staged changes exist
git diff --staged

# Run delta manually
cs delta --staged --output-format json
```

## Maintenance

### Token Rotation
**Frequency**: Every 90 days (per V12 security protocol)

**Process**:
1. Generate new token in CodeScene dashboard
2. Update `.env` file (local)
3. Update GitHub secret (CI/CD)
4. Test both integrations
5. Archive old token

### CLI Updates
**Frequency**: Check monthly for new releases

**Process**:
```powershell
# Check current version
cs --version

# Update CLI
curl -fsSL https://downloads.codescene.io/enterprise/cli/install-cs-tool.sh | bash

# Verify update
cs --version
```

### Threshold Tuning
**Frequency**: Quarterly review

**Metrics to Review**:
- Average hotspot score distribution
- Code health score distribution
- False positive/negative rates
- Developer feedback

**Adjustment Process**:
1. Analyze 90 days of data
2. Propose threshold changes
3. Get Director approval
4. Update scripts and workflows
5. Document in this protocol

## References

- **CodeScene CLI Docs**: https://codescene.com/docs/cli/
- **Jane Street Decision #14**: Multi-signal hotspot analysis (2026-06-03)
- **V12 DNA**: Correctness by Construction, Cognitive Simplicity
- **EPIC-CCN-12**: First epic using CodeScene integration (PR #22)

## Version History

| Version | Date | Changes |
|---------|------|---------|
| V12.24 | 2026-06-03 | Initial protocol - 3 integrations defined |

---

**Protocol Owner**: Director  
**Last Updated**: 2026-06-03  
**Next Review**: 2026-06-17 (post-validation)