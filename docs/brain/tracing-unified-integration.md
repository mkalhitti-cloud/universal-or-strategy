# V12 Telemetry → Unified Compound Intelligence Stack

**Status**: DESIGN PHASE (FINAL REVISION)  
**Build**: V12.18 + Existing Observability Stack  
**Date**: 2026-05-25  
**Objective**: Integrate V12 distributed tracing with existing compound intelligence infrastructure

---

## Executive Summary

**CRITICAL DISCOVERY**: A complete compound intelligence stack already exists:

1. ✅ **Phoenix** - Real-time trace visualization (http://localhost:6006)
2. ✅ **LangSmith** - Production LLM observability + SmithDB
3. ✅ **Firebase/Firestore** - Persistent knowledge base (Jane Street KB, agent sessions, learnings)
4. ✅ **Obsidian** - Human-readable vault with 11 plugins
5. ✅ **Paperclip Wiki** - LLM Wiki with compounding artifacts
6. ⏳ **Greptile** - Semantic code search (pending Skybridge adapter)

**New Goal**: Wire V12 telemetry into this existing stack using `scripts/agent_session_wrapper.py`.

---

## Unified Architecture

```
V12 Strategy (C#)
  ↓ [Telemetry Events via Print()]
Agent Session Wrapper (Python)
  ├─→ Phoenix (Real-time traces)
  ├─→ LangSmith (Production observability)
  ├─→ Firebase (Persistent KB)
  ├─→ Obsidian (Human vault)
  └─→ Paperclip Wiki (Compounding artifacts)
```

---

## Phase 1: V12 Telemetry Activation (UNCHANGED)

**See**: `docs/brain/tracing-integration-plan.md` Phase 1

Wire telemetry calls:
- `TrackFsmTransition()` in `Enqueue()`
- `TrackSimaDispatch()` in SIMA
- `TrackReaperAudit()` in REAPER
- `EmitMetricsSummary()` in `OnStateChangeTerminated()`

**Output**: Structured logs to NinjaTrader Output window

---

## Phase 2: Python Trace Capture Bridge

### 2.1 NinjaTrader Log Parser

**File**: `scripts/v12_log_parser.py`

**Purpose**: Parse NinjaTrader Output window logs and convert to structured events.

**Implementation**:

```python
import re
from datetime import datetime
from typing import Dict, List, Optional

class V12LogParser:
    """Parse V12 telemetry logs from NinjaTrader Output window."""
    
    # Log patterns
    TRACE_PATTERN = re.compile(r'\[TRACE:(\d+)\]\[([^\]]+)\]\s+(.+)')
    SPAN_PATTERN = re.compile(r'\[TRACE:(\d+)\]\[([^\]]+)\]\[SPAN\]\s+elapsed=(\d+)ms')
    METRIC_PATTERN = re.compile(r'\[V12\.18\]\s+SESSION METRICS REPORT')
    
    def parse_log_line(self, line: str) -> Optional[Dict]:
        """Parse a single log line into structured event."""
        
        # Match trace event
        trace_match = self.TRACE_PATTERN.match(line)
        if trace_match:
            trace_id, module, message = trace_match.groups()
            return {
                'type': 'trace',
                'trace_id': trace_id,
                'module': module,
                'message': message,
                'timestamp': datetime.utcnow().isoformat()
            }
        
        # Match span event
        span_match = self.SPAN_PATTERN.match(line)
        if span_match:
            trace_id, module, elapsed_ms = span_match.groups()
            return {
                'type': 'span',
                'trace_id': trace_id,
                'module': module,
                'duration_ms': int(elapsed_ms),
                'timestamp': datetime.utcnow().isoformat()
            }
        
        # Match metrics report
        if self.METRIC_PATTERN.search(line):
            return {
                'type': 'metrics_start',
                'timestamp': datetime.utcnow().isoformat()
            }
        
        return None
    
    def parse_metrics_block(self, lines: List[str]) -> Dict:
        """Parse SESSION METRICS REPORT block."""
        metrics = {}
        for line in lines:
            if 'FSM Transitions' in line:
                metrics['fsm_transitions'] = int(re.search(r':\s+(\d+)', line).group(1))
            elif 'SIMA Dispatches' in line:
                metrics['sima_dispatches'] = int(re.search(r':\s+(\d+)', line).group(1))
            elif 'Reaper Audits' in line:
                metrics['reaper_audits'] = int(re.search(r':\s+(\d+)', line).group(1))
            elif 'Order Submissions' in line:
                metrics['order_submissions'] = int(re.search(r':\s+(\d+)', line).group(1))
            elif 'IPC Commands' in line:
                metrics['ipc_commands'] = int(re.search(r':\s+(\d+)', line).group(1))
        
        return {
            'type': 'session_metrics',
            'metrics': metrics,
            'timestamp': datetime.utcnow().isoformat()
        }
```

---

### 2.2 V12 Session Wrapper

**File**: `scripts/v12_session_wrapper.py`

**Purpose**: Wrap V12 strategy execution and route telemetry to all backends.

**Implementation**:

```python
import os
import sys
from pathlib import Path
from agent_session_wrapper import AgentSessionWrapper
from v12_log_parser import V12LogParser

class V12SessionWrapper:
    """Wrapper for V12 strategy sessions with full observability."""
    
    def __init__(self, strategy_name: str = "V12_002"):
        self.strategy_name = strategy_name
        self.session_id = f"v12-{datetime.utcnow().strftime('%Y%m%d-%H%M%S')}"
        
        # Initialize agent wrapper (routes to Phoenix, LangSmith, Firebase, Obsidian)
        self.wrapper = AgentSessionWrapper(
            agent_name="V12_Strategy",
            task_id=self.session_id
        )
        
        # Initialize log parser
        self.parser = V12LogParser()
        
        # Session state
        self.traces = []
        self.spans = []
        self.metrics = {}
        
    def start_session(self):
        """Start V12 session monitoring."""
        self.wrapper.log_command(f"NinjaTrader: Start {self.strategy_name}")
        print(f"[V12 Session Wrapper] Monitoring session: {self.session_id}")
        
    def process_log_line(self, line: str):
        """Process a single log line from NinjaTrader."""
        event = self.parser.parse_log_line(line)
        
        if not event:
            return
        
        if event['type'] == 'trace':
            self.traces.append(event)
            self.wrapper.phoenix_tracer.log_event(
                name=f"v12.{event['module']}",
                attributes={
                    'trace_id': event['trace_id'],
                    'message': event['message']
                }
            )
            
        elif event['type'] == 'span':
            self.spans.append(event)
            self.wrapper.phoenix_tracer.log_event(
                name=f"v12.{event['module']}.span",
                attributes={
                    'trace_id': event['trace_id'],
                    'duration_ms': event['duration_ms']
                }
            )
            
        elif event['type'] == 'session_metrics':
            self.metrics = event['metrics']
            
    def end_session(self, status: str = "success"):
        """End V12 session and persist to all backends."""
        
        # Calculate session duration
        duration_seconds = sum(span['duration_ms'] for span in self.spans) / 1000
        
        # Extract learnings
        learnings = self._extract_learnings()
        
        # Save to all backends via wrapper
        self.wrapper.save_session(
            status=status,
            duration_seconds=duration_seconds,
            artifacts=[
                f"v12-session-{self.session_id}.json",
                f"v12-metrics-{self.session_id}.json"
            ]
        )
        
        # Save learnings
        for learning in learnings:
            self.wrapper.log_learning(
                category=learning['category'],
                insight=learning['insight'],
                context=learning['context']
            )
        
        # Export to Paperclip Wiki
        self._export_to_paperclip_wiki()
        
        print(f"[V12 Session Wrapper] Session complete: {self.session_id}")
        print(f"  - Traces: {len(self.traces)}")
        print(f"  - Spans: {len(self.spans)}")
        print(f"  - Learnings: {len(learnings)}")
        
    def _extract_learnings(self) -> List[Dict]:
        """Extract learnings from session data."""
        learnings = []
        
        # Analyze latency patterns
        if self.spans:
            avg_latency = sum(s['duration_ms'] for s in self.spans) / len(self.spans)
            max_latency = max(s['duration_ms'] for s in self.spans)
            
            if max_latency > avg_latency * 2:
                learnings.append({
                    'category': 'latency_spike',
                    'insight': f"Detected latency spike: {max_latency}ms (avg: {avg_latency:.1f}ms)",
                    'context': {
                        'session_id': self.session_id,
                        'max_latency_ms': max_latency,
                        'avg_latency_ms': avg_latency
                    }
                })
        
        # Analyze FSM transition rate
        if self.metrics.get('fsm_transitions'):
            fsm_rate = self.metrics['fsm_transitions'] / (len(self.spans) or 1)
            learnings.append({
                'category': 'fsm_transition_rate',
                'insight': f"FSM transition rate: {fsm_rate:.2f} transitions/span",
                'context': {
                    'session_id': self.session_id,
                    'fsm_transitions': self.metrics['fsm_transitions'],
                    'total_spans': len(self.spans)
                }
            })
        
        return learnings
    
    def _export_to_paperclip_wiki(self):
        """Export session data to Paperclip Wiki."""
        wiki_path = os.getenv('PAPERCLIP_WIKI_PATH', 
                              r'C:\Users\Mohammed Khalid\Documents\Paperclip\Wikis\v12-trading')
        
        traces_dir = Path(wiki_path) / 'wiki' / 'sources' / 'v12-traces'
        traces_dir.mkdir(parents=True, exist_ok=True)
        
        # Generate markdown report
        report = self._generate_markdown_report()
        
        # Save to wiki
        report_path = traces_dir / f"session-{self.session_id}.md"
        report_path.write_text(report, encoding='utf-8')
        
        print(f"[V12 Session Wrapper] Exported to Paperclip Wiki: {report_path}")
    
    def _generate_markdown_report(self) -> str:
        """Generate markdown report for Paperclip Wiki."""
        return f"""---
source_type: v12_trace
session_id: {self.session_id}
build_tag: V12.18
total_traces: {len(self.traces)}
total_spans: {len(self.spans)}
---

# V12 Trading Session - {self.session_id}

## Session Metrics

| Metric | Count |
|--------|-------|
| FSM Transitions | {self.metrics.get('fsm_transitions', 0)} |
| SIMA Dispatches | {self.metrics.get('sima_dispatches', 0)} |
| Reaper Audits | {self.metrics.get('reaper_audits', 0)} |
| Order Submissions | {self.metrics.get('order_submissions', 0)} |
| IPC Commands | {self.metrics.get('ipc_commands', 0)} |

## Top 10 Slowest Operations

| Module | Duration (ms) | Trace ID |
|--------|---------------|----------|
{self._format_top_spans()}

## Entities Referenced

- [[FSM Actors]] ({self.metrics.get('fsm_transitions', 0)} transitions)
- [[SIMA Fleet]] ({self.metrics.get('sima_dispatches', 0)} dispatches)
- [[REAPER Audit]] ({self.metrics.get('reaper_audits', 0)} cycles)

## Concepts Observed

{self._format_learnings()}
"""
    
    def _format_top_spans(self) -> str:
        """Format top 10 slowest spans as markdown table rows."""
        sorted_spans = sorted(self.spans, key=lambda s: s['duration_ms'], reverse=True)[:10]
        rows = []
        for span in sorted_spans:
            rows.append(f"| {span['module']} | {span['duration_ms']} | {span['trace_id']} |")
        return '\n'.join(rows) if rows else "| No spans recorded | - | - |"
    
    def _format_learnings(self) -> str:
        """Format learnings as markdown list."""
        learnings = self._extract_learnings()
        if not learnings:
            return "- No patterns detected"
        
        lines = []
        for learning in learnings:
            lines.append(f"- [[{learning['category']}]]: {learning['insight']}")
        return '\n'.join(lines)
```

---

### 2.3 NinjaTrader Log Tail Script

**File**: `scripts/tail_ninjatrader_log.py`

**Purpose**: Tail NinjaTrader Output window log file in real-time.

**Implementation**:

```python
import time
from pathlib import Path
from v12_session_wrapper import V12SessionWrapper

def tail_ninjatrader_log(log_path: str, wrapper: V12SessionWrapper):
    """Tail NinjaTrader log file and process lines."""
    log_file = Path(log_path)
    
    if not log_file.exists():
        print(f"Error: Log file not found: {log_path}")
        return
    
    print(f"Tailing NinjaTrader log: {log_path}")
    
    with open(log_file, 'r', encoding='utf-8') as f:
        # Seek to end of file
        f.seek(0, 2)
        
        while True:
            line = f.readline()
            if line:
                wrapper.process_log_line(line.strip())
            else:
                time.sleep(0.1)  # Wait for new lines

if __name__ == "__main__":
    # NinjaTrader log path (adjust as needed)
    log_path = r"C:\Users\Mohammed Khalid\Documents\NinjaTrader 8\log\Output.txt"
    
    # Initialize wrapper
    wrapper = V12SessionWrapper()
    wrapper.start_session()
    
    try:
        tail_ninjatrader_log(log_path, wrapper)
    except KeyboardInterrupt:
        print("\nStopping log tail...")
        wrapper.end_session(status="interrupted")
```

---

## Phase 3: Unified Backend Integration

### 3.1 Backend Routing (Already Implemented)

**File**: `scripts/agent_session_wrapper.py` (existing)

**Routes to**:
1. **Phoenix**: Real-time traces via `phoenix_tracer.log_event()`
2. **LangSmith**: Production observability via `LANGCHAIN_TRACING_V2`
3. **Firebase**: Persistent KB via `firestore.client()`
4. **Obsidian**: Human vault via markdown export

---

### 3.2 Firebase Collections

**Existing Collections** (from `COMPOUND_INTELLIGENCE_INTEGRATION.md`):
- `agent_sessions`: Every traced session
- `learnings`: Extracted patterns and insights
- `errors`: Failure modes and resolutions
- `performance`: Timing and resource metrics
- `jane_street_knowledge_base`: HFT patterns (existing)

**New Collection for V12**:
- `v12_sessions`: V12-specific session data

**Schema**:
```json
{
  "session_id": "v12-20260525-143045",
  "build_tag": "V12.18",
  "start_time": "2026-05-25T14:30:45Z",
  "end_time": "2026-05-25T20:53:45Z",
  "duration_seconds": 22980,
  "metrics": {
    "fsm_transitions": 12345,
    "sima_dispatches": 567,
    "reaper_audits": 89,
    "order_submissions": 345,
    "ipc_commands": 123
  },
  "traces": [...],
  "spans": [...],
  "learnings": [...]
}
```

---

### 3.3 Obsidian Vault Structure

**Existing Vault**: `C:\Users\Mohammed Khalid\Documents\V12-Agent-Vault`

**New Directories for V12**:
```
V12-Agent-Vault/
├── Sessions/
│   └── V12/
│       ├── 2026-05-25.md
│       └── ...
├── Learnings/
│   └── V12/
│       ├── Latency-Patterns.md
│       ├── FSM-Transition-Rate.md
│       └── ...
├── Patterns/
│   └── V12/
│       ├── SIMA-Dispatch-Optimization.md
│       └── ...
└── Agents/
    └── V12_Strategy/
        ├── Architecture.md
        ├── Telemetry.md
        └── Performance-Baseline.md
```

---

### 3.4 Paperclip Wiki Integration

**Existing Wiki**: `C:\Users\Mohammed Khalid\Documents\Paperclip\Wikis\v12-trading`

**Structure** (from `tracing-paperclip-integration.md`):
```
wiki/
├── sources/
│   └── v12-traces/
│       ├── session-2026-05-25-143045.md
│       └── ...
├── projects/
│   └── v12-trading/
│       ├── index.md
│       └── standup.md
├── entities/
│   ├── fsm-actors.md
│   ├── sima-fleet.md
│   └── reaper-audit.md
├── concepts/
│   ├── latency-patterns.md
│   └── order-submission-timing.md
└── synthesis/
    ├── session-insights-2026-05.md
    └── performance-trends.md
```

**Ingest Workflow**:
1. V12 session ends → markdown exported to `wiki/sources/v12-traces/`
2. Run `paperclip run wiki-ingest --file session-{id}.md`
3. Wiki extracts entities and concepts
4. Weekly synthesis via `paperclip run wiki-query`

---

## Phase 4: Compound Intelligence Queries

### 4.1 Cross-Backend Queries

**Query Firebase + Obsidian + Paperclip Wiki**:

```python
from scripts.agent_session_wrapper import AgentSessionWrapper
from scripts.query_kb import search_kb, init_firestore

def query_compound_intelligence(query: str):
    """Query all backends for compound intelligence."""
    
    # 1. Query Firebase (Jane Street KB + V12 sessions)
    db = init_firestore()
    firebase_results = search_kb(db, query)
    
    # 2. Query Obsidian (via Dataview)
    obsidian_results = query_obsidian_vault(query)
    
    # 3. Query Paperclip Wiki (via wiki-query skill)
    wiki_results = query_paperclip_wiki(query)
    
    # 4. Synthesize results
    return synthesize_results(firebase_results, obsidian_results, wiki_results)
```

---

### 4.2 Example Queries

**Query 1**: "What are the historical latency patterns for SIMA dispatch?"

**Sources**:
- Firebase: `v12_sessions` collection (raw metrics)
- Obsidian: `Learnings/V12/Latency-Patterns.md`
- Paperclip Wiki: `wiki/concepts/latency-patterns.md`

**Query 2**: "How many order rejections occurred in the last 30 days?"

**Sources**:
- Firebase: `v12_sessions` collection (filter by date)
- Obsidian: `Sessions/V12/*.md` (Dataview query)
- Paperclip Wiki: `wiki/sources/v12-traces/*.md` (wiki-query)

---

## Phase 5: Automated Workflows

### 5.1 Daily Session Monitoring

**Script**: `scripts/monitor_v12_session.ps1`

```powershell
# Start Phoenix
Start-Process python -ArgumentList "-m phoenix.server.main serve" -NoNewWindow

# Start log tail
Start-Process python -ArgumentList "scripts/tail_ninjatrader_log.py" -NoNewWindow

Write-Host "V12 session monitoring started"
Write-Host "Phoenix: http://localhost:6006"
```

---

### 5.2 Weekly Synthesis

**Script**: `scripts/weekly_v12_synthesis.py`

```python
from datetime import datetime, timedelta
from scripts.v12_session_wrapper import V12SessionWrapper

def run_weekly_synthesis():
    """Generate weekly synthesis report."""
    
    # Query last 7 days of sessions
    end_date = datetime.utcnow()
    start_date = end_date - timedelta(days=7)
    
    # Aggregate metrics
    sessions = query_v12_sessions(start_date, end_date)
    
    # Generate synthesis
    synthesis = generate_synthesis(sessions)
    
    # Save to Paperclip Wiki
    save_to_wiki(synthesis, f"wiki/synthesis/weekly-{end_date.strftime('%Y-%W')}.md")
    
    # Save to Obsidian
    save_to_obsidian(synthesis, f"Learnings/V12/Weekly-{end_date.strftime('%Y-%W')}.md")
    
    print(f"Weekly synthesis complete: {len(sessions)} sessions analyzed")
```

**Schedule**: Run every Sunday via Task Scheduler

---

## Phase 6: Configuration & Deployment

### 6.1 Environment Variables

**File**: `.env`

```bash
# Phoenix
PHOENIX_PORT=6006

# LangSmith
LANGCHAIN_TRACING_V2=true
LANGCHAIN_API_KEY=your_key_here
LANGCHAIN_PROJECT=v12-universal-or-strategy

# Firebase
GOOGLE_APPLICATION_CREDENTIALS=firebase-credentials.json

# Obsidian
OBSIDIAN_VAULT_PATH=C:/Users/Mohammed Khalid/Documents/V12-Agent-Vault

# Paperclip Wiki
PAPERCLIP_WIKI_PATH=C:/Users/Mohammed Khalid/Documents/Paperclip/Wikis/v12-trading

# NinjaTrader
NINJATRADER_LOG_PATH=C:/Users/Mohammed Khalid/Documents/NinjaTrader 8/log/Output.txt
```

---

### 6.2 Deployment Checklist

- [ ] Wire V12 telemetry calls (Phase 1)
- [ ] Test log parser with sample logs
- [ ] Verify Phoenix connection (http://localhost:6006)
- [ ] Verify LangSmith traces appear
- [ ] Verify Firebase writes succeed
- [ ] Verify Obsidian vault structure
- [ ] Verify Paperclip Wiki ingest
- [ ] Run first traced V12 session
- [ ] Validate compound intelligence queries
- [ ] Schedule weekly synthesis

---

## Success Metrics

1. **Trace Coverage**: 100% of V12 FSM events traced
2. **Backend Reliability**: >99% successful writes to all backends
3. **Query Latency**: Compound intelligence queries return in <5s
4. **Synthesis Quality**: >80% of insights actionable
5. **Compounding Evidence**: Each session adds to knowledge base

---

## Next Steps

1. **Immediate** (Phase 1): Wire V12 telemetry calls → Switch to `code` mode
2. **Short-term** (Phase 2): Build log parser and session wrapper
3. **Medium-term** (Phase 3): Test end-to-end pipeline
4. **Long-term** (Phase 4): Automate synthesis workflows

---

## References

- **Existing Stack**: `docs/protocol/COMPOUND_INTELLIGENCE_INTEGRATION.md`
- **Paperclip Integration**: `docs/brain/tracing-paperclip-integration.md`
- **Original Plan**: `docs/brain/tracing-integration-plan.md`
- **V12 Telemetry**: `src/V12_002.Telemetry.cs`
- **Agent Wrapper**: `scripts/agent_session_wrapper.py`
- **Firebase KB**: `scripts/query_kb.py`

---

**Plan Status**: READY FOR IMPLEMENTATION  
**Estimated Effort**: 2-3 days (leverages existing infrastructure)  
**Blocking Issues**: None  
**Dependencies**: Phoenix, LangSmith, Firebase, Obsidian, Paperclip (all already set up)
