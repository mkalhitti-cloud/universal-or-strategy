"""
post_commit_sync_check.py
Bob IDE post-commit hook — runs automatically after every commit.

Purpose: Detect NT8 stale state when src/PropTraderTools/*.cs was changed.
         Warns loudly and prints the exact command to fix it.
         Does NOT auto-sync (NT8 may need to be closed first).

Exit codes:
  0 — no PropTraderTools .cs files changed, or all NT8 files are in sync
  1 — stale NT8 state detected (hard warning, but not a blocking gate)

Run order: This hook is non-blocking (exit 1 = warn only, not abort).
           Bob IDE registers it as run_order: 20.
"""

import hashlib
import os
import subprocess
import sys
from pathlib import Path


NT8_ADDONS_DIR = Path(os.environ.get("USERPROFILE", "C:/Users/Default")) / (
    "Documents/NinjaTrader 8/bin/Custom/AddOns/PropTraderTools"
)
SRC_DIR = Path(__file__).resolve().parents[2] / "src" / "PropTraderTools"

EXCLUDE_DIRS = {"Tests", "obj", "bin"}
EXCLUDE_PATTERNS = {"*Tests.cs", "CopyEngineTests.cs", "*.bak"}


def _md5(path: Path) -> str:
    h = hashlib.md5()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def _is_excluded(rel: str, name: str) -> bool:
    parts = Path(rel).parts
    for ex in EXCLUDE_DIRS:
        if ex in parts[:-1]:
            return True
    for pat in EXCLUDE_PATTERNS:
        if Path(name).match(pat):
            return True
    return False


def _get_changed_ptt_files() -> list[str]:
    """Return list of src/PropTraderTools/*.cs files touched in the last commit."""
    result = subprocess.run(
        ["git", "diff-tree", "--no-commit-id", "-r", "--name-only", "HEAD"],
        capture_output=True,
        text=True,
    )
    changed = []
    for line in result.stdout.splitlines():
        if line.startswith("src/PropTraderTools/") and line.endswith(".cs"):
            changed.append(line)
    return changed


def main() -> int:
    changed = _get_changed_ptt_files()
    if not changed:
        # No PropTraderTools .cs files in this commit — nothing to check.
        return 0

    if not NT8_ADDONS_DIR.exists():
        print(
            "\n[SYNC-CHECK] WARNING: NT8 AddOns folder not found at:\n"
            f"  {NT8_ADDONS_DIR}\n"
            "  Cannot verify sync state. Run manually after NT8 is installed:\n"
            "  powershell -File scripts\\ptt-sync-and-verify.ps1\n"
        )
        return 0  # Not a hard error — may be a CI environment.

    stale: list[str] = []
    for cs_path_str in changed:
        cs_path = Path(cs_path_str)
        rel = str(cs_path.relative_to("src/PropTraderTools"))
        if _is_excluded(rel, cs_path.name):
            continue
        src_file = SRC_DIR / rel
        dst_file = NT8_ADDONS_DIR / rel
        if not src_file.exists():
            continue  # deleted in this commit
        if not dst_file.exists() or _md5(src_file) != _md5(dst_file):
            stale.append(rel)

    if not stale:
        print("\n[SYNC-CHECK] NT8 in sync — all changed files verified.\n")
        return 0

    print("\n" + "=" * 60)
    print("[SYNC-CHECK] WARNING: NT8 IS STALE")
    print("=" * 60)
    print(f"  {len(stale)} file(s) differ between repo and NT8 AddOns:")
    for f in stale:
        print(f"    {f}")
    print()
    print("  FIX (run now, then press F5 in NT8):")
    print("    powershell -File scripts\\ptt-sync-and-verify.ps1")
    print()
    print("  NT8 is running OLD CODE until you sync + compile.")
    print("=" * 60 + "\n")
    return 1


if __name__ == "__main__":
    sys.exit(main())
