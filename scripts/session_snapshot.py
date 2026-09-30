#!/usr/bin/env python3
"""
Session Snapshot Infrastructure - Jane Street Pattern Implementation
Tracks file reads, symbol exploration, searches, and token budget for agent sessions.

Usage:
    python scripts/session_snapshot.py init <session_id> <agent_name> <task_description>
    python scripts/session_snapshot.py check-read <session_id> <file_path>
    python scripts/session_snapshot.py record-read <session_id> <file_path> <read_type>
    python scripts/session_snapshot.py record-symbol <session_id> <symbol_id> <symbol_name> <file_path>
    python scripts/session_snapshot.py record-search <session_id> <query> <tool> <result_count>
    python scripts/session_snapshot.py update-budget <session_id> <tokens_consumed>
    python scripts/session_snapshot.py get <session_id> [--json]
"""

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional, Any

# Constants
BRAIN_DIR = Path("docs/brain")
DEFAULT_TOKEN_BUDGET = 200000
TOKEN_WARNING_THRESHOLD = 0.8  # Warn at 80% consumption
TOKEN_CRITICAL_THRESHOLD = 0.95  # Critical at 95% consumption

class SessionSnapshot:
    """Manages session state tracking for agent workflows."""
    
    def __init__(self, session_id: str):
        self.session_id = session_id
        self.session_file = BRAIN_DIR / f"session_{session_id}.json"
        self.data: Dict[str, Any] = {}
        
    def init(self, agent_name: str, task_description: str) -> None:
        """Initialize a new session."""
        if self.session_file.exists():
            print(f"Warning: Session {self.session_id} already exists. Overwriting.", file=sys.stderr)
        
        self.data = {
            "session_id": self.session_id,
            "agent_name": agent_name,
            "task_description": task_description,
            "start_time": datetime.now(timezone.utc).isoformat(),
            "files_accessed": [],
            "symbols_explored": [],
            "searches_performed": [],
            "token_budget": {
                "total": DEFAULT_TOKEN_BUDGET,
                "consumed": 0,
                "remaining": DEFAULT_TOKEN_BUDGET,
                "last_update": datetime.now(timezone.utc).isoformat()
            },
            "negative_evidence": [],
            "metadata": {
                "version": "1.0",
                "created_by": "session_snapshot.py"
            }
        }
        
        self._save()
        print(f"Session initialized: {self.session_id}")
        
    def load(self) -> None:
        """Load existing session data."""
        if not self.session_file.exists():
            raise FileNotFoundError(f"Session {self.session_id} not found at {self.session_file}")
        
        with open(self.session_file, 'r', encoding='utf-8') as f:
            self.data = json.load(f)
            
    def _save(self) -> None:
        """Save session data to disk."""
        BRAIN_DIR.mkdir(parents=True, exist_ok=True)
        with open(self.session_file, 'w', encoding='utf-8') as f:
            json.dump(self.data, f, indent=2, ensure_ascii=False)
            
    def check_read(self, file_path: str) -> bool:
        """Check if file has already been read. Returns True if already read."""
        self.load()
        for entry in self.data["files_accessed"]:
            if entry["path"] == file_path:
                return True
        return False
        
    def record_read(self, file_path: str, read_type: str, tokens_estimated: int = 0) -> None:
        """Record a file read operation."""
        self.load()
        
        # Check for duplicate
        for entry in self.data["files_accessed"]:
            if entry["path"] == file_path and entry["read_type"] == read_type:
                print(f"Warning: File {file_path} already recorded with read_type={read_type}", file=sys.stderr)
                return
        
        entry = {
            "path": file_path,
            "read_type": read_type,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "tokens_estimated": tokens_estimated
        }
        
        self.data["files_accessed"].append(entry)
        self._save()
        print(f"Recorded read: {file_path} ({read_type})")
        
    def record_symbol(self, symbol_id: str, symbol_name: str, file_path: str) -> None:
        """Record symbol exploration."""
        self.load()
        
        entry = {
            "symbol_id": symbol_id,
            "symbol_name": symbol_name,
            "file_path": file_path,
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
        
        self.data["symbols_explored"].append(entry)
        self._save()
        print(f"Recorded symbol: {symbol_name} in {file_path}")
        
    def record_search(self, query: str, tool: str, result_count: int) -> None:
        """Record search query."""
        self.load()
        
        entry = {
            "query": query,
            "tool": tool,
            "result_count": result_count,
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
        
        self.data["searches_performed"].append(entry)
        self._save()
        print(f"Recorded search: '{query}' via {tool} ({result_count} results)")
        
    def record_negative_evidence(self, query: str, verdict: str) -> None:
        """Record failed search (negative evidence)."""
        self.load()
        
        entry = {
            "query": query,
            "verdict": verdict,
            "timestamp": datetime.now(timezone.utc).isoformat()
        }
        
        self.data["negative_evidence"].append(entry)
        self._save()
        print(f"Recorded negative evidence: '{query}' -> {verdict}")
        
    def update_budget(self, tokens_consumed: int) -> None:
        """Update token budget consumption."""
        self.load()
        
        budget = self.data["token_budget"]
        budget["consumed"] = tokens_consumed
        budget["remaining"] = budget["total"] - tokens_consumed
        budget["last_update"] = datetime.now(timezone.utc).isoformat()
        
        # Calculate consumption percentage
        consumption_pct = tokens_consumed / budget["total"]
        
        # Emit warnings
        if consumption_pct >= TOKEN_CRITICAL_THRESHOLD:
            print(f"CRITICAL: Token budget at {consumption_pct*100:.1f}% ({budget['remaining']} remaining)", file=sys.stderr)
        elif consumption_pct >= TOKEN_WARNING_THRESHOLD:
            print(f"WARNING: Token budget at {consumption_pct*100:.1f}% ({budget['remaining']} remaining)", file=sys.stderr)
        
        self._save()
        print(f"Budget updated: {tokens_consumed}/{budget['total']} tokens ({budget['remaining']} remaining)")
        
    def get_state(self, json_output: bool = False) -> None:
        """Display current session state."""
        self.load()
        
        if json_output:
            print(json.dumps(self.data, indent=2))
        else:
            # Human-readable output
            print(f"\n=== Session: {self.data['session_id']} ===")
            print(f"Agent: {self.data['agent_name']}")
            print(f"Task: {self.data['task_description']}")
            print(f"Started: {self.data['start_time']}")
            
            print(f"\nFiles Accessed: {len(self.data['files_accessed'])}")
            for entry in self.data['files_accessed'][-5:]:  # Show last 5
                print(f"  - {entry['path']} ({entry['read_type']})")
            
            print(f"\nSymbols Explored: {len(self.data['symbols_explored'])}")
            for entry in self.data['symbols_explored'][-5:]:  # Show last 5
                print(f"  - {entry['symbol_name']} in {entry['file_path']}")
            
            print(f"\nSearches Performed: {len(self.data['searches_performed'])}")
            for entry in self.data['searches_performed'][-5:]:  # Show last 5
                print(f"  - '{entry['query']}' via {entry['tool']} ({entry['result_count']} results)")
            
            budget = self.data['token_budget']
            consumption_pct = budget['consumed'] / budget['total'] * 100
            print(f"\nToken Budget:")
            print(f"  Consumed: {budget['consumed']}/{budget['total']} ({consumption_pct:.1f}%)")
            print(f"  Remaining: {budget['remaining']}")
            print(f"  Last Update: {budget['last_update']}")
            
            if self.data['negative_evidence']:
                print(f"\nNegative Evidence: {len(self.data['negative_evidence'])}")
                for entry in self.data['negative_evidence'][-3:]:  # Show last 3
                    print(f"  - '{entry['query']}' -> {entry['verdict']}")
            
            print()

def main():
    """CLI entry point."""
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    
    command = sys.argv[1]
    
    try:
        if command == "init":
            if len(sys.argv) != 5:
                print("Usage: session_snapshot.py init <session_id> <agent_name> <task_description>")
                sys.exit(1)
            session_id, agent_name, task_description = sys.argv[2:5]
            snapshot = SessionSnapshot(session_id)
            snapshot.init(agent_name, task_description)
            
        elif command == "check-read":
            if len(sys.argv) != 4:
                print("Usage: session_snapshot.py check-read <session_id> <file_path>")
                sys.exit(1)
            session_id, file_path = sys.argv[2:4]
            snapshot = SessionSnapshot(session_id)
            already_read = snapshot.check_read(file_path)
            if already_read:
                print(f"File already read: {file_path}")
                sys.exit(0)  # Exit 0 = already read
            else:
                print(f"File not yet read: {file_path}")
                sys.exit(1)  # Exit 1 = not read
                
        elif command == "record-read":
            if len(sys.argv) < 5:
                print("Usage: session_snapshot.py record-read <session_id> <file_path> <read_type> [tokens_estimated]")
                sys.exit(1)
            session_id, file_path, read_type = sys.argv[2:5]
            tokens_estimated = int(sys.argv[5]) if len(sys.argv) > 5 else 0
            snapshot = SessionSnapshot(session_id)
            snapshot.record_read(file_path, read_type, tokens_estimated)
            
        elif command == "record-symbol":
            if len(sys.argv) != 6:
                print("Usage: session_snapshot.py record-symbol <session_id> <symbol_id> <symbol_name> <file_path>")
                sys.exit(1)
            session_id, symbol_id, symbol_name, file_path = sys.argv[2:6]
            snapshot = SessionSnapshot(session_id)
            snapshot.record_symbol(symbol_id, symbol_name, file_path)
            
        elif command == "record-search":
            if len(sys.argv) != 6:
                print("Usage: session_snapshot.py record-search <session_id> <query> <tool> <result_count>")
                sys.exit(1)
            session_id, query, tool, result_count = sys.argv[2:6]
            snapshot = SessionSnapshot(session_id)
            snapshot.record_search(query, tool, int(result_count))
            
        elif command == "record-negative":
            if len(sys.argv) != 5:
                print("Usage: session_snapshot.py record-negative <session_id> <query> <verdict>")
                sys.exit(1)
            session_id, query, verdict = sys.argv[2:5]
            snapshot = SessionSnapshot(session_id)
            snapshot.record_negative_evidence(query, verdict)
            
        elif command == "update-budget":
            if len(sys.argv) != 4:
                print("Usage: session_snapshot.py update-budget <session_id> <tokens_consumed>")
                sys.exit(1)
            session_id, tokens_consumed = sys.argv[2:4]
            snapshot = SessionSnapshot(session_id)
            snapshot.update_budget(int(tokens_consumed))
            
        elif command == "get":
            if len(sys.argv) < 3:
                print("Usage: session_snapshot.py get <session_id> [--json]")
                sys.exit(1)
            session_id = sys.argv[2]
            json_output = "--json" in sys.argv
            snapshot = SessionSnapshot(session_id)
            snapshot.get_state(json_output)
            
        else:
            print(f"Unknown command: {command}")
            print(__doc__)
            sys.exit(1)
            
    except FileNotFoundError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main()

# Made with Bob
