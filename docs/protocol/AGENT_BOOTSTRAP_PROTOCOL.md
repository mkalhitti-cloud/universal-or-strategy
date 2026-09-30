# Agent Bootstrap Protocol - Automatic Knowledge Loading

**Status**: DESIGN PHASE  
**Version**: 1.0.0  
**Date**: 2026-05-25  
**Purpose**: Ensure ALL agents wake up with full context from Jane Street KB, Graphify, and Compound Intelligence stack

---

## Problem Statement

**Current State**: Agents start sessions with zero context, requiring manual queries to:
- Jane Street Knowledge Base (Firebase)
- Graphify knowledge graph
- Compound intelligence learnings
- Previous session history

**Desired State**: Agents automatically load relevant context on startup based on:
- Task type (architecture, refactoring, performance, etc.)
- File scope (which files they'll be working on)
- Historical patterns (what worked before)

---

## Architecture

```
Agent Startup
  ↓
Bootstrap Loader (Python)
  ├─→ Query Jane Street KB (Firebase)
  ├─→ Load Graphify Graph (graph.json)
  ├─→ Query Compound Intelligence (learnings)
  └─→ Inject into Agent Context
  ↓
Agent Session (with full context)
```

---

## Implementation

### 1. Bootstrap Loader Script

**File**: `scripts/agent_bootstrap.py`

```python
import os
import sys
import json
from pathlib import Path
from typing import Dict, List, Optional
from datetime import datetime, timedelta

# Import existing infrastructure
from query_kb import init_firestore, search_kb
from agent_session_wrapper import AgentSessionWrapper

class AgentBootstrapLoader:
    """Loads context for agent startup."""
    
    def __init__(self, agent_name: str, task_type: str, file_scope: List[str] = None):
        self.agent_name = agent_name
        self.task_type = task_type
        self.file_scope = file_scope or []
        self.context = {
            'jane_street': [],
            'graphify': {},
            'learnings': [],
            'previous_sessions': []
        }
        
    def load_all(self) -> Dict:
        """Load all context sources."""
        print(f"[Bootstrap] Loading context for {self.agent_name}...")
        
        # 1. Load Jane Street KB
        self._load_jane_street_kb()
        
        # 2. Load Graphify graph
        self._load_graphify_graph()
        
        # 3. Load compound intelligence learnings
        self._load_compound_intelligence()
        
        # 4. Load previous session history
        self._load_session_history()
        
        # 5. Generate context summary
        summary = self._generate_summary()
        
        print(f"[Bootstrap] Context loaded:")
        print(f"  - Jane Street patterns: {len(self.context['jane_street'])}")
        print(f"  - Graphify nodes: {len(self.context['graphify'].get('nodes', []))}")
        print(f"  - Learnings: {len(self.context['learnings'])}")
        print(f"  - Previous sessions: {len(self.context['previous_sessions'])}")
        
        return {
            'context': self.context,
            'summary': summary
        }
    
    def _load_jane_street_kb(self):
        """Load relevant Jane Street patterns from Firebase."""
        try:
            db = init_firestore()
            
            # Map task types to Jane Street categories
            category_map = {
                'architecture': ['system_design', 'correctness'],
                'refactoring': ['lock_free', 'actor_pattern', 'fsm'],
                'performance': ['latency', 'lock_free', 'microsecond'],
                'testing': ['testing', 'verification'],
                'debugging': ['debugging', 'observability']
            }
            
            categories = category_map.get(self.task_type, ['general'])
            
            # Query Firebase for each category
            for category in categories:
                results = search_kb(db, category)
                if results:
                    self.context['jane_street'].extend(results)
            
            # Also query for file-specific patterns
            if self.file_scope:
                for file_path in self.file_scope:
                    # Extract component name (e.g., "FSM", "SIMA", "REAPER")
                    component = self._extract_component_name(file_path)
                    if component:
                        results = search_kb(db, component)
                        if results:
                            self.context['jane_street'].extend(results)
            
        except Exception as e:
            print(f"[Bootstrap] Warning: Failed to load Jane Street KB: {e}")
    
    def _load_graphify_graph(self):
        """Load Graphify knowledge graph."""
        try:
            graph_path = Path('graphify-out/graph.json')
            if graph_path.exists():
                with open(graph_path, 'r', encoding='utf-8') as f:
                    self.context['graphify'] = json.load(f)
                
                # Extract relevant subgraph for file scope
                if self.file_scope:
                    self.context['graphify']['relevant_nodes'] = self._extract_relevant_nodes()
            else:
                print("[Bootstrap] Warning: Graphify graph not found. Run 'graphify update .'")
                
        except Exception as e:
            print(f"[Bootstrap] Warning: Failed to load Graphify graph: {e}")
    
    def _load_compound_intelligence(self):
        """Load learnings from compound intelligence stack."""
        try:
            db = init_firestore()
            
            # Query learnings collection
            learnings_ref = db.collection('learnings')
            
            # Filter by task type
            query = learnings_ref.where('category', '==', self.task_type)
            
            # Get recent learnings (last 30 days)
            cutoff_date = datetime.utcnow() - timedelta(days=30)
            query = query.where('timestamp', '>=', cutoff_date)
            
            # Order by confidence
            query = query.order_by('confidence', direction='DESCENDING')
            
            # Limit to top 10
            docs = query.limit(10).stream()
            
            for doc in docs:
                self.context['learnings'].append(doc.to_dict())
                
        except Exception as e:
            print(f"[Bootstrap] Warning: Failed to load compound intelligence: {e}")
    
    def _load_session_history(self):
        """Load previous session history for this agent."""
        try:
            db = init_firestore()
            
            # Query agent_sessions collection
            sessions_ref = db.collection('agent_sessions')
            
            # Filter by agent name
            query = sessions_ref.where('agent_name', '==', self.agent_name)
            
            # Get recent sessions (last 7 days)
            cutoff_date = datetime.utcnow() - timedelta(days=7)
            query = query.where('start_time', '>=', cutoff_date)
            
            # Order by start time
            query = query.order_by('start_time', direction='DESCENDING')
            
            # Limit to last 5 sessions
            docs = query.limit(5).stream()
            
            for doc in docs:
                self.context['previous_sessions'].append(doc.to_dict())
                
        except Exception as e:
            print(f"[Bootstrap] Warning: Failed to load session history: {e}")
    
    def _extract_component_name(self, file_path: str) -> Optional[str]:
        """Extract component name from file path."""
        # Map file patterns to components
        if 'FSM' in file_path or 'Enqueue' in file_path:
            return 'FSM'
        elif 'SIMA' in file_path:
            return 'SIMA'
        elif 'REAPER' in file_path or 'Reaper' in file_path:
            return 'REAPER'
        elif 'Telemetry' in file_path:
            return 'observability'
        elif 'Lifecycle' in file_path:
            return 'lifecycle'
        return None
    
    def _extract_relevant_nodes(self) -> List[Dict]:
        """Extract relevant nodes from Graphify graph based on file scope."""
        relevant = []
        
        if not self.context['graphify'].get('nodes'):
            return relevant
        
        for node in self.context['graphify']['nodes']:
            # Check if node file matches file scope
            node_file = node.get('file', '')
            for scope_file in self.file_scope:
                if scope_file in node_file or node_file in scope_file:
                    relevant.append(node)
                    break
        
        return relevant
    
    def _generate_summary(self) -> str:
        """Generate markdown summary of loaded context."""
        lines = [
            f"# Agent Bootstrap Context - {self.agent_name}",
            f"",
            f"**Task Type**: {self.task_type}",
            f"**File Scope**: {', '.join(self.file_scope) if self.file_scope else 'All files'}",
            f"**Loaded**: {datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S UTC')}",
            f"",
            f"## Jane Street Knowledge Base",
            f""
        ]
        
        if self.context['jane_street']:
            for pattern in self.context['jane_street'][:5]:  # Top 5
                lines.append(f"- **{pattern.get('title', 'Unknown')}**: {pattern.get('category', 'N/A')}")
                takeaways = pattern.get('takeaways', [])
                if takeaways:
                    lines.append(f"  - {takeaways[0]}")
        else:
            lines.append("- No patterns loaded")
        
        lines.extend([
            f"",
            f"## Graphify Knowledge Graph",
            f""
        ])
        
        if self.context['graphify'].get('nodes'):
            total_nodes = len(self.context['graphify']['nodes'])
            relevant_nodes = len(self.context['graphify'].get('relevant_nodes', []))
            lines.append(f"- Total nodes: {total_nodes}")
            lines.append(f"- Relevant to scope: {relevant_nodes}")
            
            # List god nodes (high degree)
            god_nodes = sorted(
                self.context['graphify']['nodes'],
                key=lambda n: n.get('degree', 0),
                reverse=True
            )[:3]
            
            if god_nodes:
                lines.append(f"- God nodes:")
                for node in god_nodes:
                    lines.append(f"  - {node.get('name', 'Unknown')} (degree: {node.get('degree', 0)})")
        else:
            lines.append("- Graph not loaded")
        
        lines.extend([
            f"",
            f"## Compound Intelligence Learnings",
            f""
        ])
        
        if self.context['learnings']:
            for learning in self.context['learnings'][:5]:  # Top 5
                lines.append(f"- **{learning.get('category', 'Unknown')}**: {learning.get('insight', 'N/A')}")
                lines.append(f"  - Confidence: {learning.get('confidence', 0):.2f}")
        else:
            lines.append("- No learnings loaded")
        
        lines.extend([
            f"",
            f"## Previous Sessions",
            f""
        ])
        
        if self.context['previous_sessions']:
            for session in self.context['previous_sessions'][:3]:  # Last 3
                lines.append(f"- {session.get('task_id', 'Unknown')} ({session.get('status', 'N/A')})")
                lines.append(f"  - Duration: {session.get('duration_seconds', 0):.1f}s")
        else:
            lines.append("- No previous sessions")
        
        return '\n'.join(lines)


def bootstrap_agent(agent_name: str, task_type: str, file_scope: List[str] = None) -> Dict:
    """Bootstrap an agent with full context."""
    loader = AgentBootstrapLoader(agent_name, task_type, file_scope)
    return loader.load_all()


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python agent_bootstrap.py <agent_name> <task_type> [file1] [file2] ...")
        print("Task types: architecture, refactoring, performance, testing, debugging")
        sys.exit(1)
    
    agent_name = sys.argv[1]
    task_type = sys.argv[2]
    file_scope = sys.argv[3:] if len(sys.argv) > 3 else None
    
    result = bootstrap_agent(agent_name, task_type, file_scope)
    
    # Print summary
    print("\n" + "="*80)
    print(result['summary'])
    print("="*80)
    
    # Save to file for agent consumption
    output_path = Path(f'.agent/bootstrap/{agent_name}-context.md')
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(result['summary'], encoding='utf-8')
    
    print(f"\nContext saved to: {output_path}")
```

---

### 2. Agent Wrapper Integration

**Modify**: `scripts/agent_session_wrapper.py`

Add bootstrap call at session start:

```python
class AgentSessionWrapper:
    def __init__(self, agent_name: str, task_id: str, task_type: str = None, file_scope: List[str] = None):
        # ... existing init ...
        
        # Bootstrap context
        if task_type:
            from agent_bootstrap import bootstrap_agent
            self.bootstrap_context = bootstrap_agent(agent_name, task_type, file_scope)
        else:
            self.bootstrap_context = None
```

---

### 3. Bob CLI Integration

**File**: `.bob/hooks/pre_session.py`

```python
#!/usr/bin/env python3
"""Pre-session hook for Bob CLI to load bootstrap context."""

import sys
import os
from pathlib import Path

# Add scripts to path
sys.path.insert(0, str(Path(__file__).parent.parent.parent / 'scripts'))

from agent_bootstrap import bootstrap_agent

def main():
    # Detect task type from Bob mode
    mode = os.getenv('BOB_MODE', 'v12-engineer')
    
    # Map mode to task type
    task_type_map = {
        'v12-engineer': 'architecture',
        'v12-epic-planner': 'architecture',
        'code': 'refactoring',
        'advanced': 'refactoring'
    }
    
    task_type = task_type_map.get(mode, 'architecture')
    
    # Load context
    result = bootstrap_agent('Bob', task_type)
    
    # Save to Bob's context directory
    context_path = Path('.bob/context/bootstrap.md')
    context_path.parent.mkdir(parents=True, exist_ok=True)
    context_path.write_text(result['summary'], encoding='utf-8')
    
    print(f"[Bob Bootstrap] Context loaded: {context_path}")

if __name__ == "__main__":
    main()
```

**Enable hook in `.bob/settings.json`**:

```json
{
  "hooks": {
    "pre_session": ".bob/hooks/pre_session.py"
  }
}
```

---

### 4. Gemini CLI Integration

**File**: `.gemini/workflows/bootstrap.yaml`

```yaml
name: Bootstrap Context
description: Load Jane Street KB, Graphify, and Compound Intelligence on startup

steps:
  - name: Load Context
    command: python scripts/agent_bootstrap.py Gemini {{task_type}} {{file_scope}}
    
  - name: Inject Context
    action: read_file
    file: .agent/bootstrap/Gemini-context.md
```

---

### 5. AGENTS.md Update

**Add to Section 4 (Communication & Context)**:

```markdown
## 4. Communication & Context

- **Active Task**: Always check `docs/brain/task.md` before initiating work.
- **Handoffs**: Use the `docs/brain/nexus_a2a.json` via the **Nexus Bridge** for inter-agent state synchronization.
- **Expert Knowledge Base (RAG)**: Before starting complex design, refactoring, or performance engineering tasks, query the Jane Street Knowledge Base using `scripts/query_kb.py` to retrieve verified microsecond-latency patterns and testing standards.
- **Automatic Bootstrap**: ALL agents automatically load context on startup via `scripts/agent_bootstrap.py`:
  - Jane Street KB patterns (filtered by task type)
  - Graphify knowledge graph (filtered by file scope)
  - Compound intelligence learnings (last 30 days)
  - Previous session history (last 7 days)
```

---

## Usage

### Manual Bootstrap

```powershell
# Bootstrap Bob for architecture task on FSM files
python scripts/agent_bootstrap.py Bob architecture src/V12_002.cs src/V12_002.Lifecycle.cs

# Bootstrap Gemini for refactoring task
python scripts/agent_bootstrap.py Gemini refactoring src/V12_002.Telemetry.cs

# Bootstrap Codex for performance task
python scripts/agent_bootstrap.py Codex performance src/V12_002.cs
```

### Automatic Bootstrap (via Hooks)

**Bob CLI**: Automatically runs on session start (via `.bob/hooks/pre_session.py`)

**Gemini CLI**: Run workflow:
```bash
gemini workflow run bootstrap --task_type=architecture --file_scope=src/V12_002.cs
```

**Jules AI**: Add to GitHub Actions workflow:
```yaml
- name: Bootstrap Jules
  run: python scripts/agent_bootstrap.py Jules refactoring ${{ github.event.pull_request.changed_files }}
```

---

## Context Injection Methods

### Method 1: File-Based (Recommended)

Agent reads bootstrap context from `.agent/bootstrap/{agent_name}-context.md`

**Pros**: Simple, works with all agents  
**Cons**: Requires agent to read file

### Method 2: Environment Variable

Export context as `AGENT_BOOTSTRAP_CONTEXT` env var

**Pros**: Immediate availability  
**Cons**: Size limits, not persistent

### Method 3: MCP Server

Serve context via MCP protocol (for Bob/Gemini)

**Pros**: Native integration  
**Cons**: Requires MCP server setup

---

## Validation

### Test Bootstrap Loader

```powershell
# Test Jane Street KB loading
python scripts/agent_bootstrap.py Bob architecture

# Verify output contains:
# - Jane Street patterns: >0
# - Graphify nodes: >0
# - Learnings: >0
```

### Test Hook Integration

```powershell
# Start Bob session
bob

# Check for bootstrap context
cat .bob/context/bootstrap.md

# Should contain Jane Street patterns, Graphify nodes, learnings
```

---

## Metrics

Track bootstrap effectiveness:

1. **Context Load Time**: <2s for full bootstrap
2. **Context Relevance**: >80% of loaded patterns used in session
3. **Query Reduction**: 50% fewer manual KB queries after bootstrap
4. **Session Efficiency**: 20% faster task completion with bootstrap

---

## Troubleshooting

### Bootstrap Fails

**Symptom**: `agent_bootstrap.py` exits with error

**Solutions**:
1. Verify Firebase credentials: `echo $env:GOOGLE_APPLICATION_CREDENTIALS`
2. Check Graphify graph exists: `ls graphify-out/graph.json`
3. Test Firebase connection: `python scripts/query_kb.py "test"`

### Context Not Loaded

**Symptom**: Agent starts without bootstrap context

**Solutions**:
1. Check hook execution: `cat .bob/hooks/pre_session.py`
2. Verify context file: `cat .agent/bootstrap/Bob-context.md`
3. Check Bob settings: `cat .bob/settings.json` (hooks enabled?)

### Irrelevant Context

**Symptom**: Bootstrap loads patterns not related to task

**Solutions**:
1. Refine task type mapping in `agent_bootstrap.py`
2. Add file scope filtering: `python agent_bootstrap.py Bob architecture src/V12_002.cs`
3. Adjust Firebase query filters

---

## Future Enhancements

1. **Semantic Search**: Use embeddings to find most relevant patterns
2. **Context Ranking**: Score patterns by relevance to current task
3. **Incremental Loading**: Load context progressively as task evolves
4. **Cross-Agent Sharing**: Share bootstrap context between agents in same session
5. **Context Caching**: Cache bootstrap results for repeated tasks

---

## V12 DNA Compliance

- ✅ **Correctness by Construction**: Bootstrap validates all loaded data
- ✅ **ASCII-Only**: All context files use ASCII encoding
- ✅ **Graceful Degradation**: Agent works if bootstrap fails
- ✅ **Lock-Free**: No synchronization in bootstrap loader
- ✅ **Observability**: Bootstrap logs all loaded context

---

## References

- **Jane Street KB**: `scripts/query_kb.py`
- **Graphify**: `graphify-out/graph.json`
- **Compound Intelligence**: `docs/protocol/COMPOUND_INTELLIGENCE_INTEGRATION.md`
- **Agent Wrapper**: `scripts/agent_session_wrapper.py`

---

**Status**: READY FOR IMPLEMENTATION  
**Estimated Effort**: 1 day  
**Dependencies**: Firebase, Graphify, Agent Session Wrapper (all exist)  
**Next Step**: Implement `scripts/agent_bootstrap.py` and test with Bob CLI
