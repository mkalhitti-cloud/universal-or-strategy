# Bob Shell Model Settings Review
**Date**: 2026-05-29
**Requested By**: Director
**Purpose**: Review model selection for Bob Shell modes and custom modes

---

## Configuration Files Reviewed

### 1. Project-Level Settings (`.bob/settings.json`)
**Location**: `C:\WSGTA\universal-or-strategy\.bob\settings.json`

**Model Configuration**: ❌ **NOT PRESENT**
- No `model` or `apiProvider` settings found
- Only contains: checkpointing, editor preferences, auto-approve list, custom tools

### 2. Custom Modes (`.bob/custom_modes.yaml`)
**Defined Modes**:
1. **v12-epic-planner** - Epic planning and ticket generation (PLAN-ONLY)
2. **v12-engineer** - Surgical refactoring and complexity extraction
3. **v12-phase7-lead** - Concurrency engineering (lock-free patterns)

**Model Configuration**: ❌ **NOT PRESENT**
- No `model`, `apiProvider`, or `modelPreference` fields in any mode
- Modes only define `roleDefinition`, `groups`, and `customRules`

### 3. MCP Configuration (`.bob/mcp.json`)
**Model Configuration**: ❌ **NOT PRESENT**
- Only defines MCP server connections (Greptile)

---

## Where Model Settings Are Stored

### Global Bob Shell Configuration
Bob Shell stores model preferences in:
- **Windows**: `C:\Users\<username>\.bob\settings.json`
- **macOS/Linux**: `~/.bob/settings.json`

**Expected Structure**:
```json
{
  "apiProvider": "anthropic",
  "model": "claude-opus-4-7",
  "modeModels": {
    "plan": "claude-sonnet-4-6",
    "code": "claude-opus-4-7",
    "advanced": "claude-opus-4-7",
    "ask": "claude-haiku-4-5",
    "v12-engineer": "claude-opus-4-7",
    "v12-epic-planner": "claude-sonnet-4-6",
    "v12-phase7-lead": "claude-opus-4-7"
  }
}
```

**Note**: Cannot access global config from workspace context.

---

## Current Session Info

**Active Model**: `premium` (per environment_details)
**Current Mode**: `plan`

This suggests Bob Shell is using a premium-tier model (likely Claude Opus 4.7 or similar).

---

## Recommended Model Assignments (V12 Optimized)

| Mode | Recommended Model | Rationale |
|------|-------------------|-----------|
| **v12-engineer** | `claude-opus-4-7` | Surgical refactoring requires maximum reasoning |
| **v12-epic-planner** | `claude-sonnet-4-6` | Planning is analysis-heavy but not code-critical |
| **v12-phase7-lead** | `claude-opus-4-7` | Concurrency engineering requires deep reasoning |
| **plan** | `claude-sonnet-4-6` | Analysis and documentation |
| **code** | `claude-opus-4-7` | Code modification requires precision |
| **advanced** | `claude-opus-4-7` | Complex tasks with MCP tools |
| **ask** | `claude-haiku-4-5` | Q&A and documentation lookup |

---

## Cost Optimization Strategy

**High-Cost Tasks** (Opus 4.7):
- Surgical refactoring (`v12-engineer`)
- Concurrency engineering (`v12-phase7-lead`)
- Complex code modifications (`code`, `advanced`)

**Medium-Cost Tasks** (Sonnet 4.6):
- Epic planning (`v12-epic-planner`)
- Analysis and design (`plan`)

**Low-Cost Tasks** (Haiku 4.5):
- Documentation lookup (`ask`)
- Simple Q&A

---

## How to Configure

To set mode-specific models:

```powershell
# Open global Bob Shell settings
code $env:USERPROFILE\.bob\settings.json
```

Add `modeModels` section with recommended assignments, then restart Bob Shell.

---

## Summary

- ✅ No model settings in project `.bob/` config (expected - uses global)
- ✅ Custom modes defined correctly (v12-engineer, v12-epic-planner, v12-phase7-lead)
- ⏳ Check global `~/.bob/settings.json` for model assignments
- ⏳ Verify `premium` model is Claude Opus 4.7
- ⏳ Add mode-specific model assignments per recommendations
