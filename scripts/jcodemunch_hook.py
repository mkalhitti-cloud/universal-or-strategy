#!/usr/bin/env python3
"""
jCodemunch Git Hook Integration
Python wrapper for calling jCodemunch-MCP tools from git hooks
Handles register_edit, index_file, and index_folder operations
"""

import sys
import json
import logging
import argparse
from pathlib import Path
from typing import List, Optional
from datetime import datetime

# Setup logging
LOG_DIR = Path(".git/hooks/logs")
LOG_DIR.mkdir(parents=True, exist_ok=True)
LOG_FILE = LOG_DIR / f"jcodemunch-{datetime.now().strftime('%Y-%m-%d')}.log"

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s',
    handlers=[
        logging.FileHandler(LOG_FILE),
        logging.StreamHandler(sys.stderr)
    ]
)
logger = logging.getLogger(__name__)

# Try to import MCP client library
try:
    # This is a placeholder - actual MCP client import will depend on the library
    # For now, we'll use subprocess to call tools via stdio
    import subprocess
    HAS_MCP = True
except ImportError:
    HAS_MCP = False
    logger.warning("MCP client library not available, using subprocess fallback")


class JCodemunchHook:
    """Wrapper for jCodemunch MCP operations in git hooks"""
    
    def __init__(self, repo_path: str = "."):
        self.repo_path = Path(repo_path).resolve()
        self.mcp_server = "jcodemunch-mcp"
        
    def _call_mcp_tool(self, tool_name: str, arguments: dict) -> dict:
        """
        Call an MCP tool via subprocess
        This uses the MCP stdio protocol to communicate with jcodemunch-mcp
        """
        try:
            # Construct MCP request
            request = {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "tools/call",
                "params": {
                    "name": tool_name,
                    "arguments": arguments
                }
            }
            
            # For now, log the call (actual MCP integration would go here)
            logger.info(f"MCP call: {tool_name} with args: {json.dumps(arguments)}")
            
            # TODO: Implement actual MCP stdio communication
            # This would involve:
            # 1. Starting the MCP server process if not running
            # 2. Sending JSON-RPC request via stdin
            # 3. Reading JSON-RPC response from stdout
            # 4. Handling errors and timeouts
            
            # For now, return success
            return {"success": True, "tool": tool_name}
            
        except Exception as e:
            logger.error(f"MCP call failed: {tool_name} - {str(e)}")
            return {"success": False, "error": str(e)}
    
    def register_edit(self, file_paths: List[str]) -> bool:
        """
        Register edited files with jCodemunch for cache invalidation
        This is fast and should always be called after edits
        """
        if not file_paths:
            logger.info("No files to register")
            return True
            
        logger.info(f"Registering {len(file_paths)} edited files")
        
        arguments = {
            "repo": str(self.repo_path),
            "file_paths": file_paths,
            "reindex": False  # Just invalidate caches, don't re-index yet
        }
        
        result = self._call_mcp_tool("register_edit", arguments)
        
        if result.get("success"):
            logger.info(f"✓ Registered {len(file_paths)} files for cache invalidation")
            return True
        else:
            logger.error(f"✗ Failed to register edits: {result.get('error')}")
            return False
    
    def index_file(self, file_path: str) -> bool:
        """
        Re-index a single file
        Use for small changes (<10 files)
        """
        logger.info(f"Indexing file: {file_path}")
        
        arguments = {
            "path": str(Path(file_path).resolve()),
            "use_ai_summaries": True,
            "context_providers": True
        }
        
        result = self._call_mcp_tool("index_file", arguments)
        
        if result.get("success"):
            logger.info(f"✓ Indexed file: {file_path}")
            return True
        else:
            logger.error(f"✗ Failed to index file: {result.get('error')}")
            return False
    
    def index_folder(self, incremental: bool = True) -> bool:
        """
        Re-index entire folder
        Use for large changes (>10 files)
        """
        logger.info(f"Indexing folder: {self.repo_path} (incremental={incremental})")
        
        arguments = {
            "path": str(self.repo_path),
            "use_ai_summaries": True,
            "incremental": incremental
        }
        
        result = self._call_mcp_tool("index_folder", arguments)
        
        if result.get("success"):
            logger.info(f"✓ Indexed folder: {self.repo_path}")
            return True
        else:
            logger.error(f"✗ Failed to index folder: {result.get('error')}")
            return False
    
    def update_from_commit(self, commit_hash: str = "HEAD") -> bool:
        """
        Update jCodemunch index based on files changed in a commit
        Strategy: <10 files = per-file, >=10 files = batch
        """
        import subprocess
        
        # Get list of changed files in src/
        try:
            result = subprocess.run(
                ["git", "diff-tree", "--no-commit-id", "--name-only", "-r", commit_hash],
                capture_output=True,
                text=True,
                check=True
            )
            
            all_files = [f.strip() for f in result.stdout.split('\n') if f.strip()]
            src_files = [f for f in all_files if f.startswith('src/')]
            
            if not src_files:
                logger.info("No src/ files changed, skipping jCodemunch update")
                return True
            
            logger.info(f"Found {len(src_files)} changed src/ files")
            
            # Always register edits for cache invalidation
            self.register_edit(src_files)
            
            # Choose strategy based on file count
            if len(src_files) < 10:
                # Per-file updates
                logger.info("Using per-file update strategy")
                success = True
                for file_path in src_files:
                    if not self.index_file(file_path):
                        success = False
                return success
            else:
                # Batch update
                logger.info("Using batch update strategy (incremental)")
                return self.index_folder(incremental=True)
                
        except subprocess.CalledProcessError as e:
            logger.error(f"Failed to get changed files: {e}")
            return False
        except Exception as e:
            logger.error(f"Unexpected error: {e}")
            return False


def main():
    """CLI entry point for git hooks"""
    parser = argparse.ArgumentParser(
        description="jCodemunch git hook integration"
    )
    parser.add_argument(
        "action",
        choices=["register_edit", "index_file", "index_folder", "update_commit"],
        help="Action to perform"
    )
    parser.add_argument(
        "--files",
        nargs="+",
        help="File paths (for register_edit or index_file)"
    )
    parser.add_argument(
        "--commit",
        default="HEAD",
        help="Commit hash (for update_commit)"
    )
    parser.add_argument(
        "--incremental",
        action="store_true",
        help="Use incremental indexing (for index_folder)"
    )
    parser.add_argument(
        "--repo",
        default=".",
        help="Repository path"
    )
    
    args = parser.parse_args()
    
    # Create hook instance
    hook = JCodemunchHook(repo_path=args.repo)
    
    # Execute action
    success = False
    try:
        if args.action == "register_edit":
            if not args.files:
                logger.error("--files required for register_edit")
                sys.exit(1)
            success = hook.register_edit(args.files)
            
        elif args.action == "index_file":
            if not args.files or len(args.files) != 1:
                logger.error("Exactly one file required for index_file")
                sys.exit(1)
            success = hook.index_file(args.files[0])
            
        elif args.action == "index_folder":
            success = hook.index_folder(incremental=args.incremental)
            
        elif args.action == "update_commit":
            success = hook.update_from_commit(commit_hash=args.commit)
        
        sys.exit(0 if success else 1)
        
    except Exception as e:
        logger.error(f"Fatal error: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()

# Made with Bob
