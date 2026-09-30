#!/usr/bin/env python3
"""
Pre-Task Jane Street KB Auto-Query Hook

Automatically queries Jane Street Knowledge Base (OKF local wiki) when Bob
starts a task that involves refactoring, architecture, or concurrency work.

Backend: OKF local wiki at docs/intel/jane-street/ (query_kb.py handles
Firebase-first with OKF fallback — OKF always resolves).

Triggered by: ALL wave-orch-* and v12-phase* modes (Wave 7 3-tier architecture)
plus legacy v12-engineer, v12-epic-planner, v12-phase7-lead modes.
"""

import os
import sys
import json
import subprocess
from pathlib import Path

# Keywords that trigger Jane Street KB query
TRIGGER_KEYWORDS = [
    'refactor', 'extract', 'complexity', 'cyc',
    'lock-free', 'concurrency', 'thread', 'async',
    'fsm', 'state machine', 'actor', 'channel',
    'epic', 'ticket', 'architecture', 'design',
    'performance', 'optimization', 'hotspot'
]

# Topic mapping for common refactoring patterns
TOPIC_MAP = {
    'lock-free': 'lock-free algorithms async patterns',
    'concurrency': 'lock-free state machine channels',
    'fsm': 'FSM state machine actor pattern',
    'state machine': 'FSM state machine actor pattern',
    'actor': 'actor pattern channels async',
    'complexity': 'function decomposition modularity',
    'extract': 'function extraction single responsibility',
    'refactor': 'refactoring patterns type safety',
    'performance': 'zero-allocation cache-line alignment',
    'async': 'async patterns channels cooperative',
    'thread': 'lock-free memory barriers atomic',
    'architecture': 'architecture patterns modularity',
    'epic': 'architecture patterns refactoring',
    'ticket': 'refactoring patterns function extraction'
}

# All modes used in Wave 7 3-tier architecture that require OKF KB queries
TRIGGER_MODES = {
    # Tier 1 — Top Orchestrator
    'autonomous-refactor',
    # Tier 2 — Phase Orchestrators
    'wave-orch-phase0', 'wave-orch-phase1', 'wave-orch-phase1-5',
    'wave-orch-phase2', 'wave-orch-phase3', 'wave-orch-phase4',
    'wave-orch-phase4-5', 'wave-orch-phase5', 'wave-orch-phase5v',
    'wave-orch-phase6',
    # Tier 3 — Epic Workers
    'v12-phase0-hotspot', 'v12-phase1-scope', 'v12-phase1-5-boundary',
    'v12-phase2-architecture', 'v12-phase3-audit', 'v12-phase4-tickets',
    'v12-phase4-5-review', 'v12-engineer', 'v12-phase5-v-verify',
    'v12-phase6-review',
    # Legacy modes
    'v12-epic-planner', 'v12-phase7-lead',
}

def should_trigger(task_description, mode):
    """Check if task should trigger Jane Street KB query"""
    # Always trigger for all wave-7 and v12 modes
    if mode in TRIGGER_MODES:
        return True
    
    # Check for trigger keywords in task description
    task_lower = task_description.lower()
    return any(keyword in task_lower for keyword in TRIGGER_KEYWORDS)

def extract_topics(task_description):
    """Extract relevant topics from task description"""
    task_lower = task_description.lower()
    topics = []
    
    for keyword, topic in TOPIC_MAP.items():
        if keyword in task_lower:
            topics.append(topic)
    
    # Default topic if no specific match
    if not topics:
        topics.append('refactoring patterns type safety')
    
    return topics

def query_jane_street_kb(topic):
    """Query Jane Street Knowledge Base"""
    script_path = Path(__file__).parent.parent.parent / 'scripts' / 'query_kb.py'
    
    try:
        result = subprocess.run(
            ['python', str(script_path), topic],
            capture_output=True,
            text=True,
            timeout=30
        )
        
        if result.returncode == 0:
            return result.stdout
        else:
            return f"[!] KB query failed: {result.stderr}"
    except subprocess.TimeoutExpired:
        return "[!] KB query timed out (30s)"
    except Exception as e:
        return f"[!] KB query error: {str(e)}"

def format_kb_results(results, topic):
    """Format KB results for Bob's context"""
    output = []
    output.append("=" * 80)
    output.append("JANE STREET KNOWLEDGE BASE - AUTO-QUERY RESULTS")
    output.append("=" * 80)
    output.append(f"Topic: {topic}")
    output.append("")
    output.append(results)
    output.append("")
    output.append("=" * 80)
    output.append("INSTRUCTIONS:")
    output.append("- Apply these Jane Street patterns in your implementation")
    output.append("- Cite patterns in your plan/architecture document")
    output.append("- Verify alignment with V12 DNA (lock-free, ASCII-only, CYC ≤ 8)")
    output.append("=" * 80)
    output.append("")
    
    return "\n".join(output)

def main():
    """Main hook execution"""
    # Read task context from environment or stdin
    task_description = os.environ.get('BOB_TASK_DESCRIPTION', '')
    mode = os.environ.get('BOB_MODE', '')
    
    # If not in env, read from stdin (Bob passes task via stdin)
    if not task_description:
        task_description = sys.stdin.read().strip()
    
    # Check if we should trigger
    if not should_trigger(task_description, mode):
        print("[*] Jane Street KB auto-query: Not triggered (no relevant keywords)")
        sys.exit(0)
    
    print("[*] Jane Street KB auto-query: TRIGGERED")
    print(f"[*] Mode: {mode}")
    print(f"[*] Task: {task_description[:100]}...")
    
    # Extract topics
    topics = extract_topics(task_description)
    print(f"[*] Topics: {', '.join(topics)}")
    
    # Query KB for each topic
    all_results = []
    for topic in topics[:2]:  # Limit to 2 topics to avoid token bloat
        print(f"[*] Querying Jane Street KB for: {topic}")
        results = query_jane_street_kb(topic)
        all_results.append(format_kb_results(results, topic))
    
    # Output results (Bob will see this in task context)
    print("\n".join(all_results))
    
    # Save to temp file for Bob to reference
    output_file = Path(__file__).parent.parent.parent / 'docs' / 'brain' / 'jane_street_kb_context.md'
    output_file.parent.mkdir(parents=True, exist_ok=True)
    
    with open(output_file, 'w') as f:
        f.write("\n".join(all_results))
    
    print(f"[*] Results saved to: {output_file}")
    print("[*] Jane Street KB auto-query: COMPLETE")

if __name__ == '__main__':
    main()

# Made with Bob
