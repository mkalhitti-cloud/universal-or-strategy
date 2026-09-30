# Bob IDE Custom Modes vs Phase MCPs

## TL;DR
**Use Bob IDE custom modes locally, NOT phase MCPs.** Custom modes are faster, simpler, and more maintainable.

## Architecture Comparison

### Phase MCPs (OLD - Deprecated for Local Use)
```
Bob IDE → MCP Protocol → Phase MCP Server → Bob Shell Custom Mode
```
- Extra network layer (MCP protocol overhead)
- Tool schema serialization/deserialization
- Context passed through MCP boundary
- ~10-15k tokens per phase MCP (tool schemas)
- Slower: 2-hop architecture

### Bob IDE Custom Modes (NEW - Recommended)
```
Bob IDE → Bob Shell Custom Mode (direct)
```
- Direct invocation (no MCP overhead)
- No serialization overhead
- Context stays in same process
- Zero token overhead (modes defined in bob.config.yaml)
- Faster: 1-hop architecture

## Why Custom Modes Are Better

### 1. Performance
- **No MCP overhead**: Direct invocation vs network protocol
- **Faster context switching**: Same process vs cross-process
- **Lower latency**: 1-hop vs 2-hop architecture

### 2. Token Efficiency
- **Phase MCPs**: ~10-15k tokens (tool schemas for each phase)
- **Custom Modes**: 0 tokens (defined in bob.config.yaml, not loaded into context)
- **Savings**: 10-15k tokens per session

### 3. Maintainability
- **Single source of truth**: `.bob/custom_modes.yaml`
- **No MCP server management**: No separate processes to maintain
- **Simpler debugging**: All in one process

### 4. Flexibility
- **Easy to modify**: Edit `.bob/custom_modes.yaml` and restart
- **No MCP protocol constraints**: Direct access to all Bob features
- **Better error handling**: No MCP boundary to cross

## When to Use Each

### Use Bob IDE Custom Modes (Recommended)
- ✅ Local development and testing
- ✅ Interactive epic workflows
- ✅ Manual phase execution
- ✅ Debugging and troubleshooting
- ✅ Any work on your local machine

### Use Phase MCPs (Only If Needed)
- ⚠️ Remote orchestration (if you need to delegate to another machine)
- ⚠️ Multi-agent coordination (if you need multiple Bob instances)
- ⚠️ API-based workflows (if you need programmatic access)

**Reality**: For Wave 7 autonomous refactoring, you don't need any of these use cases. Use custom modes.

## Current Configuration

### Bob IDE Custom Modes (Defined in bob.config.yaml)
```yaml
# V12 Phase Modes (use these locally)
v12-phase0-hotspot:
  model: claude-fable-5
  apply: true
  system_prompt_prefix: "Phase 0: Hotspot Analysis"

v12-phase1-scope:
  model: claude-fable-5
  apply: true
  system_prompt_prefix: "Phase 1: Scope Definition"

v12-phase1-5-boundary:
  model: claude-fable-5
  apply: true
  system_prompt_prefix: "Phase 1.5: Scope Boundary Validation"

v12-phase2-architecture:
  model: claude-fable-5
  apply: true
  system_prompt_prefix: "Phase 2: Architecture Planning"

v12-phase3-audit:
  model: claude-fable-5
  apply: true
  system_prompt_prefix: "Phase 3: DNA & PR Audit"

v12-phase4-tickets:
  model: claude-fable-5
  apply: true
  system_prompt_prefix: "Phase 4: Ticket Generation"

v12-engineer:
  model: claude-fable-5
  apply: true
  system_prompt_prefix: "Phase 5: Surgical Refactoring"

v12-phase5-v-verify:
  model: claude-fable-5
  apply: true
  system_prompt_prefix: "Phase 5.V: Verification"

v12-phase6-review:
  model: claude-fable-5
  apply: true
  system_prompt_prefix: "Phase 6: Final Review"
```

### Phase MCPs (Disabled - Not Needed Locally)
- ❌ phase-0-hotspot
- ❌ phase-1-scope
- ❌ phase-1-5-boundary
- ❌ phase-2-architecture
- ❌ phase-3-audit
- ❌ phase-4-tickets
- ❌ phase-5-execute
- ❌ phase-5-verify
- ❌ phase-6-review

## How to Use Custom Modes Locally

### Option 1: Bob IDE Mode Selector
1. Click mode dropdown (bottom left)
2. Select phase mode (e.g., "V12 Phase 0 Hotspot Analyzer")
3. Work in that mode

### Option 2: Switch Mode Tool
```xml
<switch_mode>
<mode_slug>v12-phase0-hotspot</mode_slug>
<reason>Starting Phase 0 hotspot analysis</reason>
</switch_mode>
```

### Option 3: New Task with Mode
```xml
<new_task>
<mode>v12-phase1-scope</mode>
<message>Define scope for EPIC-CCN-001</message>
<todos>
[ ] Analyze hotspot report
[ ] Define extraction boundaries
[ ] Document scope
</todos>
</new_task>
```

## VM Execution (Unchanged)

On the VM, Bob Shell uses custom modes directly:
```bash
bob --yolo --chat-mode v12-phase0-hotspot "$(cat /tmp/phase0_msg.txt)"
```

No MCPs involved - direct custom mode invocation.

## Expected Context Savings

### Before (Phase MCPs Enabled)
- Context: ~60k/200k tokens (30%)
- Phase MCPs: ~10-15k tokens (9 phases × ~1.5k each)

### After (Phase MCPs Disabled)
- Context: ~45-50k/200k tokens (22-25%)
- **Savings: 10-15k tokens (17-25% reduction)**
- **Available: 150-155k tokens for work**

## Recommendation

**Disable all phase MCPs and use Bob IDE custom modes instead.**

### Keep Only Essential MCPs
- ✅ jcodemunch-mcp (code navigation - no alternative)
- ✅ greptile (GitHub integration - no alternative)
- ✅ sequential-thinking (reasoning - useful)

### Disable These MCPs
- ❌ All worker MCPs (worker-1, worker-2, worker-3, worker-4)
- ❌ All phase MCPs (phase-0 through phase-6)

## Verification

After disabling phase MCPs, your MCP list should show:
- jcodemunch-mcp ✅
- greptile ✅
- sequential-thinking ✅
- **Total: 3 MCPs (down from 12+)**

Context should drop to ~45-50k/200k tokens (22-25%).

## Summary

| Aspect | Phase MCPs | Custom Modes |
|--------|-----------|--------------|
| **Performance** | Slower (2-hop) | Faster (1-hop) |
| **Token Cost** | 10-15k tokens | 0 tokens |
| **Maintainability** | Complex | Simple |
| **Flexibility** | Limited | Full |
| **Use Case** | Remote orchestration | Local development |
| **Recommendation** | ❌ Disable | ✅ Use this |

**Action**: You've already disabled phase MCPs. Perfect! Use Bob IDE custom modes for all local work.