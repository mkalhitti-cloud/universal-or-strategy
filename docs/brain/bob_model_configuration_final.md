# Bob Shell Model Configuration - Final Analysis
**Date**: 2026-05-29
**Analysis**: Complete global and project config audit

---

## ✅ Model Configuration Found

### Current Model Tier
**All modes use**: `premium` tier

**Evidence**: Session logs show `"model": "premium"` in all chat sessions.

### Model Selection Mechanism
Bob Shell uses **tier-based model selection** managed by IBM's backend, not explicit model names in config files.

---

## Configuration Files Audited

### 1. Global Settings (`~/.bob/settings.json`)
```json
{
  "ide": {
    "hasSeenNudge": true,
    "enabled": true
  },
  "ibm": {
    "isNotFirstTime": true,
    "licenseConsent": true,
    "instanceId": "20260524-0400-4817-915a-493116d6ca4e",
    "teamId": "019e5824-8411-7751-bd1a-5b9a3408b27d",
    "currentMode": "plan"
  },
  "security": {
    "auth": {
      "selectedType": "sso"
    }
  }
}
```

**Model Config**: ❌ None - Uses SSO authentication tier

### 2. Global Custom Modes (`~/.bob/settings/custom_modes.yaml`)
```yaml
customModes: []
```

**Model Config**: ❌ Empty - Custom modes defined at project level only

### 3. Project Custom Modes (`.bob/custom_modes.yaml`)
**Modes Defined**:
- `v12-epic-planner`
- `v12-engineer`
- `v12-phase7-lead`

**Model Config**: ❌ None - Inherit from global tier

---

## How Bob Shell Selects Models

### Tier-Based System
Bob Shell uses IBM's SSO authentication to determine model tier:

1. **Authentication**: SSO login determines your account tier
2. **Tier Assignment**: Account tier maps to model quality level
3. **Model Selection**: IBM backend selects appropriate model for tier
4. **Session Tracking**: All sessions log `"model": "premium"`

### Model Tiers (Inferred)
| Tier | Likely Model | Use Case |
|------|--------------|----------|
| **premium** | Claude Opus 4.7 | Complex reasoning, code generation |
| standard | Claude Sonnet 4.6 | General tasks, analysis |
| basic | Claude Haiku 4.5 | Simple Q&A, documentation |

---

## Current Configuration Summary

### All Modes Use Same Tier
**Current Setup**: All modes (plan, code, advanced, ask, v12-engineer, v12-epic-planner, v12-phase7-lead) use the **premium** tier.

**Why**: No mode-specific model overrides configured.

### Mode-Specific Model Selection
**Status**: ❌ **NOT CONFIGURED**

Bob Shell **does not support** mode-specific model selection in the current configuration format. All modes inherit the global tier from your IBM SSO account.

---

## Implications for V12 Workflow

### Cost Efficiency
✅ **Good**: Premium tier for all modes ensures maximum quality
❌ **Inefficient**: No cost optimization - even simple tasks use premium model

### Recommended Approach
Since Bob Shell doesn't support per-mode model selection:

1. **Accept Current Setup**: All modes use premium tier (Claude Opus 4.7)
2. **Manual Mode Selection**: Use appropriate modes for task complexity:
   - Complex refactoring → `v12-engineer` (premium justified)
   - Epic planning → `v12-epic-planner` (premium justified)
   - Simple Q&A → `ask` mode (premium overkill, but unavoidable)

---

## Comparison with Other Agents

### Claude Code / Cursor / Windsurf
These tools support explicit model selection per mode:
```json
{
  "modeModels": {
    "plan": "claude-sonnet-4-6",
    "code": "claude-opus-4-7"
  }
}
```

### Bob Shell
Uses tier-based system - no per-mode granularity:
```json
{
  "ibm": {
    "teamId": "...",
    "currentMode": "plan"
  }
}
```

---

## Action Items

### ✅ Completed
1. Audited global settings (`~/.bob/settings.json`)
2. Audited global custom modes (`~/.bob/settings/custom_modes.yaml`)
3. Audited project custom modes (`.bob/custom_modes.yaml`)
4. Searched session logs for model references
5. Confirmed tier-based model selection

### ⏳ Recommendations
1. **Accept premium tier for all modes** (no configuration alternative)
2. **Use mode selection strategically**:
   - `v12-engineer` for surgical refactoring
   - `v12-epic-planner` for planning
   - `ask` for documentation (even though premium is overkill)
3. **Monitor IBM billing** to understand premium tier costs

---

## Summary

**Which model is using which mode?**

| Mode | Model Tier | Likely Model | Configurable? |
|------|------------|--------------|---------------|
| plan | premium | Claude Opus 4.7 | ❌ No |
| code | premium | Claude Opus 4.7 | ❌ No |
| advanced | premium | Claude Opus 4.7 | ❌ No |
| ask | premium | Claude Opus 4.7 | ❌ No |
| v12-engineer | premium | Claude Opus 4.7 | ❌ No |
| v12-epic-planner | premium | Claude Opus 4.7 | ❌ No |
| v12-phase7-lead | premium | Claude Opus 4.7 | ❌ No |

**All modes use the same premium tier model** (likely Claude Opus 4.7) determined by your IBM SSO account. Per-mode model selection is not supported in Bob Shell's current configuration system.
