#!/usr/bin/env python3
"""Agent Bootstrap Loader - Automatic Knowledge Loading for V12 Agents.

This module loads context from multiple sources on agent startup:
- Jane Street Knowledge Base (Firebase)
- Graphify knowledge graph
- Compound intelligence learnings
- Previous session history

Usage:
    python agent_bootstrap.py <agent_name> <task_type> [file1] [file2] ...

Task types:
    - architecture: System design, correctness patterns
    - refactoring: Lock-free, FSM, actor patterns
    - performance: Latency, microsecond optimization
    - testing: Verification, testing patterns
    - debugging: Observability, debugging patterns
"""

import os
import sys
import json
from pathlib import Path
from typing import Dict, List, Optional
from datetime import datetime, timedelta, timezone

# Import existing infrastructure
try:
    from query_kb import init_firestore, search_kb
except ImportError:
    print("[Bootstrap] Warning: query_kb module not found. Jane Street KB will be unavailable.")
    init_firestore = None
    search_kb = None

try:
    from agent_session_wrapper import AgentSessionWrapper
except ImportError:
    print("[Bootstrap] Warning: agent_session_wrapper module not found. Session tracking unavailable.")
    AgentSessionWrapper = None


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
        print(f"[Bootstrap] Task type: {self.task_type}")
        if self.file_scope:
            print(f"[Bootstrap] File scope: {', '.join(self.file_scope[:3])}{'...' if len(self.file_scope) > 3 else ''}")
        
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
        
        print(f"\n[Bootstrap] Context loaded:")
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
        if not init_firestore or not search_kb:
            print("[Bootstrap] Skipping Jane Street KB (module unavailable)")
            return
            
        try:
            db = init_firestore()
            
            # Map task types to search keywords (search in doc ID and title)
            keyword_map = {
                'architecture': ['codesign', 'build', 'exchange', 'safe', 'production'],
                'refactoring': ['concurrency', 'coordination', 'safe', 'performance'],
                'performance': ['microsecond', 'skylake', 'performance', 'trading'],
                'testing': ['testing', 'hard', 'fix'],
                'debugging': ['tools', 'traders', 'production', 'engineering']
            }
            
            keywords = keyword_map.get(self.task_type, [])
            
            # Fetch all documents and filter by keywords
            collection_ref = db.collection('jane_street_knowledge_base')
            docs = collection_ref.stream()
            
            seen_ids = set()
            for doc in docs:
                if doc.id in seen_ids:
                    continue
                    
                data = doc.to_dict()
                
                # Build search text from doc ID and title
                search_text = " ".join([
                    str(doc.id),
                    str(data.get('title', ''))
                ]).lower()
                
                # Check if any keyword matches
                matched = False
                for keyword in keywords:
                    if keyword.lower() in search_text:
                        matched = True
                        break
                
                if matched:
                    self.context['jane_street'].append({
                        'id': doc.id,
                        'title': data.get('title', 'Unknown'),
                        'category': data.get('category', 'N/A'),
                        'key_takeaways': data.get('key_takeaways', []),
                        'v12_csharp_patterns': data.get('v12_csharp_patterns', {}),
                        'presenter': data.get('presenter', ''),
                        'source_url': data.get('source_url', '')
                    })
                    seen_ids.add(doc.id)
            
            # Also query for file-specific patterns
            if self.file_scope:
                for file_path in self.file_scope:
                    component = self._extract_component_name(file_path)
                    if component:
                        # Search for component-specific patterns
                        docs = collection_ref.stream()
                        
                        component_lower = component.lower()
                        for doc in docs:
                            if doc.id in seen_ids:
                                continue
                                
                            data = doc.to_dict()
                            search_text = " ".join([
                                str(doc.id),
                                str(data.get('title', '')),
                                " ".join(data.get('key_takeaways', [])),
                                str(data.get('v12_csharp_patterns', {}))
                            ]).lower()
                            
                            if component_lower in search_text:
                                self.context['jane_street'].append({
                                    'id': doc.id,
                                    'title': data.get('title', 'Unknown'),
                                    'category': data.get('category', 'N/A'),
                                    'key_takeaways': data.get('key_takeaways', []),
                                    'v12_csharp_patterns': data.get('v12_csharp_patterns', {}),
                                    'presenter': data.get('presenter', ''),
                                    'source_url': data.get('source_url', '')
                                })
                                seen_ids.add(doc.id)
            
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
        if not init_firestore:
            print("[Bootstrap] Skipping compound intelligence (module unavailable)")
            return
            
        try:
            db = init_firestore()
            
            # Query learnings collection
            learnings_ref = db.collection('learnings')
            
            # Get recent learnings (last 30 days)
            cutoff_date = datetime.now(timezone.utc) - timedelta(days=30)
            
            # Fetch all and filter client-side
            docs = learnings_ref.stream()
            
            for doc in docs:
                data = doc.to_dict()
                # Filter by category and date
                if data.get('category') == self.task_type:
                    timestamp = data.get('timestamp')
                    if timestamp and timestamp >= cutoff_date:
                        self.context['learnings'].append(data)
            
            # Sort by confidence
            self.context['learnings'].sort(
                key=lambda x: x.get('confidence', 0),
                reverse=True
            )
            
            # Limit to top 10
            self.context['learnings'] = self.context['learnings'][:10]
                
        except Exception as e:
            print(f"[Bootstrap] Warning: Failed to load compound intelligence: {e}")
    
    def _load_session_history(self):
        """Load previous session history for this agent."""
        if not init_firestore:
            print("[Bootstrap] Skipping session history (module unavailable)")
            return
            
        try:
            db = init_firestore()
            
            # Query agent_sessions collection
            sessions_ref = db.collection('agent_sessions')
            
            # Get recent sessions (last 7 days)
            cutoff_date = datetime.now(timezone.utc) - timedelta(days=7)
            
            # Fetch all and filter client-side
            docs = sessions_ref.stream()
            
            sessions = []
            for doc in docs:
                data = doc.to_dict()
                if data.get('agent_name') == self.agent_name:
                    start_time = data.get('start_time')
                    if start_time and start_time >= cutoff_date:
                        sessions.append(data)
            
            # Sort by start time
            sessions.sort(
                key=lambda x: x.get('start_time', datetime.min),
                reverse=True
            )
            
            # Limit to last 5 sessions
            self.context['previous_sessions'] = sessions[:5]
                
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
            f"**Loaded**: {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M:%S UTC')}",
            f"",
            f"## Jane Street Knowledge Base",
            f""
        ]
        
        if self.context['jane_street']:
            for pattern in self.context['jane_street'][:5]:  # Top 5
                lines.append(f"### {pattern.get('title', 'Unknown')}")
                lines.append(f"**Category**: {pattern.get('category', 'N/A')}")
                lines.append(f"")
                takeaways = pattern.get('key_takeaways', [])
                if takeaways:
                    lines.append(f"**Key Takeaways**:")
                    for takeaway in takeaways[:3]:  # Top 3 takeaways
                        lines.append(f"- {takeaway}")
                    lines.append(f"")
        else:
            lines.append("*No patterns loaded*")
            lines.append(f"")
        
        lines.extend([
            f"## Graphify Knowledge Graph",
            f""
        ])
        
        if self.context['graphify'].get('nodes'):
            total_nodes = len(self.context['graphify']['nodes'])
            relevant_nodes = len(self.context['graphify'].get('relevant_nodes', []))
            lines.append(f"- **Total nodes**: {total_nodes}")
            lines.append(f"- **Relevant to scope**: {relevant_nodes}")
            lines.append(f"")
            
            # List god nodes (high degree)
            god_nodes = sorted(
                self.context['graphify']['nodes'],
                key=lambda n: n.get('degree', 0),
                reverse=True
            )[:3]
            
            if god_nodes:
                lines.append(f"**God Nodes** (high coupling):")
                for node in god_nodes:
                    lines.append(f"- `{node.get('name', 'Unknown')}` (degree: {node.get('degree', 0)})")
                lines.append(f"")
        else:
            lines.append("*Graph not loaded*")
            lines.append(f"")
        
        lines.extend([
            f"## Compound Intelligence Learnings",
            f""
        ])
        
        if self.context['learnings']:
            for learning in self.context['learnings'][:5]:  # Top 5
                lines.append(f"### {learning.get('category', 'Unknown')}")
                lines.append(f"**Insight**: {learning.get('insight', 'N/A')}")
                lines.append(f"**Confidence**: {learning.get('confidence', 0):.2f}")
                lines.append(f"")
        else:
            lines.append("*No learnings loaded*")
            lines.append(f"")
        
        lines.extend([
            f"## Previous Sessions",
            f""
        ])
        
        if self.context['previous_sessions']:
            for session in self.context['previous_sessions'][:3]:  # Last 3
                lines.append(f"- **{session.get('task_id', 'Unknown')}** ({session.get('status', 'N/A')})")
                lines.append(f"  - Duration: {session.get('duration_seconds', 0):.1f}s")
                lines.append(f"")
        else:
            lines.append("*No previous sessions*")
            lines.append(f"")
        
        return '\n'.join(lines)


def bootstrap_agent(agent_name: str, task_type: str, file_scope: List[str] = None) -> Dict:
    """Bootstrap an agent with full context.
    
    Args:
        agent_name: Name of the agent (Bob, Gemini, Jules, etc.)
        task_type: Type of task (architecture, refactoring, performance, testing, debugging)
        file_scope: Optional list of file paths to focus on
        
    Returns:
        Dict with 'context' and 'summary' keys
    """
    loader = AgentBootstrapLoader(agent_name, task_type, file_scope)
    return loader.load_all()


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python agent_bootstrap.py <agent_name> <task_type> [file1] [file2] ...")
        print("")
        print("Task types:")
        print("  - architecture: System design, correctness patterns")
        print("  - refactoring: Lock-free, FSM, actor patterns")
        print("  - performance: Latency, microsecond optimization")
        print("  - testing: Verification, testing patterns")
        print("  - debugging: Observability, debugging patterns")
        print("")
        print("Examples:")
        print("  python agent_bootstrap.py Bob architecture")
        print("  python agent_bootstrap.py Gemini refactoring src/V12_002.cs")
        print("  python agent_bootstrap.py Codex performance src/V12_002.Telemetry.cs")
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
    output_dir = Path('.agent/bootstrap')
    output_dir.mkdir(parents=True, exist_ok=True)
    
    output_path = output_dir / f"{agent_name}-context.md"
    output_path.write_text(result['summary'], encoding='utf-8')
    
    print(f"\n[Bootstrap] Context saved to: {output_path}")
