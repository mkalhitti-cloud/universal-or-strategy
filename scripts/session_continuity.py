#!/usr/bin/env python3
"""
Session Continuity Infrastructure - Checkpoint/Restore System
Manages auto-snapshots, checkpoint restoration, and session recovery.

Usage:
    python scripts/session_continuity.py auto-snapshot <session_id> <current_tokens>
    python scripts/session_continuity.py restore <session_id> [checkpoint_number]
    python scripts/session_continuity.py list <session_id>
    python scripts/session_continuity.py merge <session_id> <checkpoint_ids...>
    python scripts/session_continuity.py prune <session_id> --keep <count>
"""

import json
import sys
import shutil
from datetime import datetime, timezone
from pathlib import Path
from typing import List, Optional, Dict, Any

# Constants
BRAIN_DIR = Path("docs/brain")
AUTO_SNAPSHOT_THRESHOLD = 50000  # Auto-snapshot every 50k tokens
MAX_CHECKPOINTS = 10  # Keep last 10 checkpoints by default

class SessionContinuity:
    """Manages session checkpoints and restoration."""
    
    def __init__(self, session_id: str):
        self.session_id = session_id
        self.session_file = BRAIN_DIR / f"session_{session_id}.json"
        self.checkpoint_pattern = f"session_{session_id}_checkpoint_*.json"
        
    def _get_checkpoint_path(self, checkpoint_num: int) -> Path:
        """Get path for checkpoint file."""
        return BRAIN_DIR / f"session_{self.session_id}_checkpoint_{checkpoint_num:03d}.json"
        
    def _get_next_checkpoint_num(self) -> int:
        """Get next available checkpoint number."""
        checkpoints = sorted(BRAIN_DIR.glob(self.checkpoint_pattern))
        if not checkpoints:
            return 1
        
        # Extract number from last checkpoint
        last_checkpoint = checkpoints[-1]
        num_str = last_checkpoint.stem.split('_')[-1]
        return int(num_str) + 1
        
    def _load_session(self) -> Dict[str, Any]:
        """Load current session data."""
        if not self.session_file.exists():
            raise FileNotFoundError(f"Session {self.session_id} not found at {self.session_file}")
        
        with open(self.session_file, 'r', encoding='utf-8') as f:
            return json.load(f)
            
    def auto_snapshot(self, current_tokens: int) -> None:
        """Create automatic checkpoint if threshold exceeded."""
        data = self._load_session()
        
        # Get last checkpoint token count
        checkpoints = sorted(BRAIN_DIR.glob(self.checkpoint_pattern))
        last_checkpoint_tokens = 0
        
        if checkpoints:
            with open(checkpoints[-1], 'r', encoding='utf-8') as f:
                last_data = json.load(f)
                last_checkpoint_tokens = last_data.get("token_budget", {}).get("consumed", 0)
        
        # Check if we need a new checkpoint
        tokens_since_last = current_tokens - last_checkpoint_tokens
        
        if tokens_since_last >= AUTO_SNAPSHOT_THRESHOLD:
            checkpoint_num = self._get_next_checkpoint_num()
            checkpoint_path = self._get_checkpoint_path(checkpoint_num)
            
            # Add checkpoint metadata
            data["checkpoint_metadata"] = {
                "checkpoint_number": checkpoint_num,
                "created_at": datetime.now(timezone.utc).isoformat(),
                "tokens_at_checkpoint": current_tokens,
                "tokens_since_last": tokens_since_last
            }
            
            # Save checkpoint
            with open(checkpoint_path, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2, ensure_ascii=False)
            
            print(f"Auto-snapshot created: checkpoint_{checkpoint_num:03d}.json")
            print(f"  Tokens: {current_tokens} (+{tokens_since_last} since last)")
            
            # Auto-prune old checkpoints
            self._auto_prune()
        else:
            print(f"No snapshot needed: {tokens_since_last} tokens since last checkpoint (threshold: {AUTO_SNAPSHOT_THRESHOLD})")
            
    def restore(self, checkpoint_num: Optional[int] = None) -> None:
        """Restore session from checkpoint."""
        checkpoints = sorted(BRAIN_DIR.glob(self.checkpoint_pattern))
        
        if not checkpoints:
            print(f"No checkpoints found for session {self.session_id}", file=sys.stderr)
            sys.exit(1)
        
        # Select checkpoint
        if checkpoint_num is None:
            # Restore from latest
            checkpoint_path = checkpoints[-1]
            checkpoint_num = int(checkpoint_path.stem.split('_')[-1])
        else:
            checkpoint_path = self._get_checkpoint_path(checkpoint_num)
            if not checkpoint_path.exists():
                print(f"Checkpoint {checkpoint_num} not found", file=sys.stderr)
                sys.exit(1)
        
        # Backup current session
        backup_path = BRAIN_DIR / f"session_{self.session_id}_backup_{datetime.now(timezone.utc).strftime('%Y%m%d_%H%M%S')}.json"
        if self.session_file.exists():
            shutil.copy2(self.session_file, backup_path)
            print(f"Current session backed up to: {backup_path.name}")
        
        # Restore checkpoint
        shutil.copy2(checkpoint_path, self.session_file)
        
        # Load restored data
        with open(checkpoint_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        
        metadata = data.get("checkpoint_metadata", {})
        tokens = metadata.get("tokens_at_checkpoint", "unknown")
        created = metadata.get("created_at", "unknown")
        
        print(f"Restored checkpoint {checkpoint_num:03d}")
        print(f"  Created: {created}")
        print(f"  Tokens: {tokens}")
        
    def list_checkpoints(self) -> None:
        """List all checkpoints for session."""
        checkpoints = sorted(BRAIN_DIR.glob(self.checkpoint_pattern))
        
        if not checkpoints:
            print(f"No checkpoints found for session {self.session_id}")
            return
        
        print(f"\n=== Checkpoints for {self.session_id} ===")
        print(f"Total: {len(checkpoints)}\n")
        
        for checkpoint_path in checkpoints:
            with open(checkpoint_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            
            metadata = data.get("checkpoint_metadata", {})
            num = metadata.get("checkpoint_number", "?")
            created = metadata.get("created_at", "unknown")
            tokens = metadata.get("tokens_at_checkpoint", "unknown")
            
            print(f"Checkpoint {num:03d}: {created} | Tokens: {tokens}")
        
        print()
        
    def merge_checkpoints(self, checkpoint_ids: List[int]) -> None:
        """Merge multiple checkpoints into current session."""
        if len(checkpoint_ids) < 2:
            print("Error: Need at least 2 checkpoints to merge", file=sys.stderr)
            sys.exit(1)
        
        # Load all checkpoints
        checkpoints_data = []
        for checkpoint_id in checkpoint_ids:
            checkpoint_path = self._get_checkpoint_path(checkpoint_id)
            if not checkpoint_path.exists():
                print(f"Error: Checkpoint {checkpoint_id} not found", file=sys.stderr)
                sys.exit(1)
            
            with open(checkpoint_path, 'r', encoding='utf-8') as f:
                checkpoints_data.append(json.load(f))
        
        # Merge strategy: union of all tracked items
        merged = checkpoints_data[0].copy()
        
        # Merge files_accessed (deduplicate by path)
        files_map = {entry["path"]: entry for entry in merged["files_accessed"]}
        for data in checkpoints_data[1:]:
            for entry in data["files_accessed"]:
                if entry["path"] not in files_map:
                    files_map[entry["path"]] = entry
        merged["files_accessed"] = list(files_map.values())
        
        # Merge symbols_explored (deduplicate by symbol_id)
        symbols_map = {entry["symbol_id"]: entry for entry in merged["symbols_explored"]}
        for data in checkpoints_data[1:]:
            for entry in data["symbols_explored"]:
                if entry["symbol_id"] not in symbols_map:
                    symbols_map[entry["symbol_id"]] = entry
        merged["symbols_explored"] = list(symbols_map.values())
        
        # Merge searches_performed (keep all, no deduplication)
        for data in checkpoints_data[1:]:
            merged["searches_performed"].extend(data["searches_performed"])
        
        # Merge negative_evidence (deduplicate by query)
        neg_map = {entry["query"]: entry for entry in merged["negative_evidence"]}
        for data in checkpoints_data[1:]:
            for entry in data["negative_evidence"]:
                if entry["query"] not in neg_map:
                    neg_map[entry["query"]] = entry
        merged["negative_evidence"] = list(neg_map.values())
        
        # Use highest token consumption
        max_tokens = max(data["token_budget"]["consumed"] for data in checkpoints_data)
        merged["token_budget"]["consumed"] = max_tokens
        merged["token_budget"]["remaining"] = merged["token_budget"]["total"] - max_tokens
        merged["token_budget"]["last_update"] = datetime.now(timezone.utc).isoformat()
        
        # Add merge metadata
        merged["merge_metadata"] = {
            "merged_checkpoints": checkpoint_ids,
            "merged_at": datetime.now(timezone.utc).isoformat(),
            "total_files": len(merged["files_accessed"]),
            "total_symbols": len(merged["symbols_explored"]),
            "total_searches": len(merged["searches_performed"])
        }
        
        # Save merged session
        with open(self.session_file, 'w', encoding='utf-8') as f:
            json.dump(merged, f, indent=2, ensure_ascii=False)
        
        print(f"Merged {len(checkpoint_ids)} checkpoints into session {self.session_id}")
        print(f"  Files: {len(merged['files_accessed'])}")
        print(f"  Symbols: {len(merged['symbols_explored'])}")
        print(f"  Searches: {len(merged['searches_performed'])}")
        print(f"  Tokens: {max_tokens}")
        
    def prune_checkpoints(self, keep_count: int = MAX_CHECKPOINTS) -> None:
        """Remove old checkpoints, keeping only the most recent N."""
        checkpoints = sorted(BRAIN_DIR.glob(self.checkpoint_pattern))
        
        if len(checkpoints) <= keep_count:
            print(f"No pruning needed: {len(checkpoints)} checkpoints (keeping {keep_count})")
            return
        
        # Delete oldest checkpoints
        to_delete = checkpoints[:-keep_count]
        for checkpoint_path in to_delete:
            checkpoint_path.unlink()
            print(f"Deleted: {checkpoint_path.name}")
        
        print(f"Pruned {len(to_delete)} checkpoints, kept {keep_count} most recent")
        
    def _auto_prune(self) -> None:
        """Automatically prune old checkpoints."""
        checkpoints = sorted(BRAIN_DIR.glob(self.checkpoint_pattern))
        
        if len(checkpoints) > MAX_CHECKPOINTS:
            to_delete = checkpoints[:-MAX_CHECKPOINTS]
            for checkpoint_path in to_delete:
                checkpoint_path.unlink()

def main():
    """CLI entry point."""
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    
    command = sys.argv[1]
    
    try:
        if command == "auto-snapshot":
            if len(sys.argv) != 4:
                print("Usage: session_continuity.py auto-snapshot <session_id> <current_tokens>")
                sys.exit(1)
            session_id, current_tokens = sys.argv[2:4]
            continuity = SessionContinuity(session_id)
            continuity.auto_snapshot(int(current_tokens))
            
        elif command == "restore":
            if len(sys.argv) < 3:
                print("Usage: session_continuity.py restore <session_id> [checkpoint_number]")
                sys.exit(1)
            session_id = sys.argv[2]
            checkpoint_num = int(sys.argv[3]) if len(sys.argv) > 3 else None
            continuity = SessionContinuity(session_id)
            continuity.restore(checkpoint_num)
            
        elif command == "list":
            if len(sys.argv) != 3:
                print("Usage: session_continuity.py list <session_id>")
                sys.exit(1)
            session_id = sys.argv[2]
            continuity = SessionContinuity(session_id)
            continuity.list_checkpoints()
            
        elif command == "merge":
            if len(sys.argv) < 4:
                print("Usage: session_continuity.py merge <session_id> <checkpoint_ids...>")
                sys.exit(1)
            session_id = sys.argv[2]
            checkpoint_ids = [int(x) for x in sys.argv[3:]]
            continuity = SessionContinuity(session_id)
            continuity.merge_checkpoints(checkpoint_ids)
            
        elif command == "prune":
            if len(sys.argv) < 3:
                print("Usage: session_continuity.py prune <session_id> [--keep N]")
                sys.exit(1)
            session_id = sys.argv[2]
            keep_count = MAX_CHECKPOINTS
            if "--keep" in sys.argv:
                keep_idx = sys.argv.index("--keep")
                keep_count = int(sys.argv[keep_idx + 1])
            continuity = SessionContinuity(session_id)
            continuity.prune_checkpoints(keep_count)
            
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
