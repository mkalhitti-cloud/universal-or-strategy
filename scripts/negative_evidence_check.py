#!/usr/bin/env python3
"""
Negative Evidence Cache - Prevents Redundant Failed Searches
Tracks queries that returned no results to prevent infinite search loops.

Usage:
    python scripts/negative_evidence_check.py check <query>
    python scripts/negative_evidence_check.py record <query> <verdict>
    python scripts/negative_evidence_check.py list
    python scripts/negative_evidence_check.py clear
"""

import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, List, Optional

# Constants
BRAIN_DIR = Path("docs/brain")
NEGATIVE_EVIDENCE_FILE = BRAIN_DIR / "negative_evidence.json"

class NegativeEvidenceCache:
    """Manages cache of failed searches."""
    
    def __init__(self):
        self.cache_file = NEGATIVE_EVIDENCE_FILE
        self.data: Dict[str, List[Dict]] = {"evidence": []}
        
    def load(self) -> None:
        """Load existing cache."""
        if self.cache_file.exists():
            with open(self.cache_file, 'r', encoding='utf-8') as f:
                self.data = json.load(f)
        else:
            self.data = {"evidence": []}
            
    def save(self) -> None:
        """Save cache to disk."""
        BRAIN_DIR.mkdir(parents=True, exist_ok=True)
        with open(self.cache_file, 'w', encoding='utf-8') as f:
            json.dump(self.data, f, indent=2, ensure_ascii=False)
            
    def check(self, query: str) -> Optional[Dict]:
        """Check if query has negative evidence. Returns evidence entry if found."""
        self.load()
        
        # Normalize query for comparison
        query_normalized = query.lower().strip()
        
        for entry in self.data["evidence"]:
            if entry["query"].lower().strip() == query_normalized:
                return entry
        
        return None
        
    def record(self, query: str, verdict: str, context: Optional[str] = None) -> None:
        """Record negative evidence for a query."""
        self.load()
        
        # Check for duplicate
        existing = self.check(query)
        if existing:
            print(f"Warning: Query already has negative evidence: '{query}'", file=sys.stderr)
            print(f"  Existing verdict: {existing['verdict']}", file=sys.stderr)
            return
        
        entry = {
            "query": query,
            "verdict": verdict,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "context": context
        }
        
        self.data["evidence"].append(entry)
        self.save()
        print(f"Recorded negative evidence: '{query}' -> {verdict}")
        
    def list_all(self) -> None:
        """List all negative evidence entries."""
        self.load()
        
        if not self.data["evidence"]:
            print("No negative evidence recorded")
            return
        
        print(f"\n=== Negative Evidence Cache ===")
        print(f"Total entries: {len(self.data['evidence'])}\n")
        
        for i, entry in enumerate(self.data["evidence"], 1):
            print(f"{i}. Query: '{entry['query']}'")
            print(f"   Verdict: {entry['verdict']}")
            print(f"   Timestamp: {entry['timestamp']}")
            if entry.get("context"):
                print(f"   Context: {entry['context']}")
            print()
            
    def clear(self) -> None:
        """Clear all negative evidence."""
        self.data = {"evidence": []}
        self.save()
        print("Negative evidence cache cleared")

def main():
    """CLI entry point."""
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    
    command = sys.argv[1]
    cache = NegativeEvidenceCache()
    
    try:
        if command == "check":
            if len(sys.argv) != 3:
                print("Usage: negative_evidence_check.py check <query>")
                sys.exit(1)
            query = sys.argv[2]
            evidence = cache.check(query)
            
            if evidence:
                print(f"NEGATIVE EVIDENCE FOUND for: '{query}'")
                print(f"  Verdict: {evidence['verdict']}")
                print(f"  Timestamp: {evidence['timestamp']}")
                if evidence.get("context"):
                    print(f"  Context: {evidence['context']}")
                sys.exit(0)  # Exit 0 = evidence found (don't search again)
            else:
                print(f"No negative evidence for: '{query}'")
                sys.exit(1)  # Exit 1 = no evidence (safe to search)
                
        elif command == "record":
            if len(sys.argv) < 4:
                print("Usage: negative_evidence_check.py record <query> <verdict> [context]")
                sys.exit(1)
            query = sys.argv[2]
            verdict = sys.argv[3]
            context = sys.argv[4] if len(sys.argv) > 4 else None
            cache.record(query, verdict, context)
            
        elif command == "list":
            cache.list_all()
            
        elif command == "clear":
            cache.clear()
            
        else:
            print(f"Unknown command: {command}")
            print(__doc__)
            sys.exit(1)
            
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main()

# Made with Bob
