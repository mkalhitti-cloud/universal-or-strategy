#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
UTF-8 Auto-Repair Hook (V12.35)
run_order: 1  (runs immediately after pre_task_rules_gate)

Scans ALL .md, .yaml, .json, .cs, .ps1 files for UTF-16 BOM encoding
and silently converts them to UTF-8 no-BOM before the agent reads anything.

This prevents the recurring agent warning:
  "The RULES_CATALOG.md is wide-character encoded (UTF-16)..."

Exit 0 always — this hook is non-blocking (repair or skip, never stop work).
"""

import os
import sys
import io
from pathlib import Path

# Force UTF-8 stdout so ASCII-only terminal (CP1252) doesn't crash on non-ASCII chars
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

SCAN_EXTENSIONS = {'.md', '.yaml', '.yml', '.json', '.cs', '.ps1', '.txt'}

UTF16_LE_BOM = b'\xff\xfe'
UTF16_BE_BOM = b'\xfe\xff'
UTF8_BOM = b'\xef\xbb\xbf'

def is_utf16(data: bytes) -> str | None:
    """Return 'le', 'be', or None."""
    if data[:2] == UTF16_LE_BOM:
        return 'le'
    if data[:2] == UTF16_BE_BOM:
        return 'be'
    return None


def repair_file(path: Path) -> bool:
    """Convert UTF-16 file to UTF-8 no-BOM. Returns True if repaired."""
    try:
        data = path.read_bytes()
    except (OSError, PermissionError):
        return False

    enc_type = is_utf16(data)
    if not enc_type:
        # Also strip UTF-8 BOM if present
        if data[:3] == UTF8_BOM:
            path.write_bytes(data[3:])
            return True
        return False

    # Decode from detected UTF-16 variant
    src_encoding = 'utf-16-le' if enc_type == 'le' else 'utf-16-be'
    # Strip the 2-byte BOM before decoding
    try:
        text = data[2:].decode(src_encoding)
        path.write_bytes(text.encode('utf-8'))
        return True
    except (UnicodeDecodeError, OSError):
        return False


def main():
    repo_root = Path(__file__).resolve().parent.parent.parent
    repaired = []
    skipped = []

    for ext in SCAN_EXTENSIONS:
        for fpath in repo_root.rglob(f'*{ext}'):
            # Skip .git and node_modules
            parts = fpath.parts
            if any(p in ('.git', 'node_modules', '__pycache__') for p in parts):
                continue
            try:
                if repair_file(fpath):
                    repaired.append(str(fpath.relative_to(repo_root)))
            except Exception as e:
                skipped.append(f"{fpath.name}: {e}")

    if repaired:
        print(f"[utf8-repair] Auto-fixed {len(repaired)} wide-char/BOM file(s):")
        for f in repaired:
            print(f"  + {f}")
    else:
        print("[utf8-repair] All files are UTF-8 clean -- no repairs needed.")

    if skipped:
        print(f"[utf8-repair] Skipped {len(skipped)} file(s) (permission/decode errors):")
        for s in skipped:
            print(f"  ! {s}")

    # Always exit 0 — repair hook never blocks work
    sys.exit(0)


if __name__ == '__main__':
    main()
