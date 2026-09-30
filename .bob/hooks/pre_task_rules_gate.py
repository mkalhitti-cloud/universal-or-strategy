#!/usr/bin/env python3
"""
RULES CATALOG GATE (V12.36) — P0 Hard Stop Hook

Runs before every task. Scans files the task will touch for P0 violations
from docs/standards/jane-street/RULES_CATALOG.md.

Exit codes:
  0  = GATE PASS — task may proceed
  1  = GATE BLOCKED — P0 violation found, work stops
  2  = HOOK ERROR — catalog unreadable or missing, work stops
"""

import sys
import os
import re
from pathlib import Path

REPO_ROOT = Path(__file__).parent.parent.parent
CATALOG_PATH = REPO_ROOT / "docs" / "standards" / "jane-street" / "RULES_CATALOG.md"

# P0 rules: (rule_id, description, regex_pattern)
P0_RULES = [
    (
        "JS-021",
        "lock() usage — STRICTLY BANNED in src/",
        re.compile(r'lock\s*\('),
    ),
    (
        "JS-033",
        "async void (non-event-handler) — BANNED",
        re.compile(r'async\s+void\s+\w+\s*\((?!.*EventHandler)'),
    ),
    (
        "JS-001",
        "throw new XxxException in business logic — use Result<T,E>",
        re.compile(r'throw\s+new\s+\w+Exception\s*\('),
    ),
    (
        "JS-002",
        "return null for missing values — use Option<T>",
        re.compile(r'return\s+null\s*;'),
    ),
]


def check_catalog_readable() -> bool:
    """Verify the rules catalog is present and UTF-8 readable."""
    if not CATALOG_PATH.exists():
        print(f"[RULES GATE] BLOCKED: Rules catalog missing at {CATALOG_PATH}", file=sys.stderr)
        return False
    try:
        # Check for UTF-16 BOM
        raw = CATALOG_PATH.read_bytes()
        if raw[:2] in (b'\xff\xfe', b'\xfe\xff'):
            print(
                f"[RULES GATE] BLOCKED: Rules catalog is UTF-16 encoded (unreadable by agents).\n"
                f"  Fix: Run the bulk-repair command in .bob/rules/05-utf8-encoding.md",
                file=sys.stderr,
            )
            return False
        # Try UTF-8 decode
        CATALOG_PATH.read_text(encoding='utf-8')
        return True
    except Exception as e:
        print(f"[RULES GATE] BLOCKED: Rules catalog unreadable: {e}", file=sys.stderr)
        return False


def scan_file_for_p0(filepath: Path) -> list[dict]:
    """Scan a single .cs file for P0 rule violations. Returns list of findings."""
    findings = []
    try:
        lines = filepath.read_text(encoding='utf-8', errors='replace').splitlines()
    except Exception:
        return findings

    for line_no, line in enumerate(lines, start=1):
        # Skip comment-only lines (don't flag commented-out code as violations)
        stripped = line.strip()
        if stripped.startswith('//') or stripped.startswith('*'):
            continue
        for rule_id, description, pattern in P0_RULES:
            if pattern.search(line):
                findings.append({
                    'rule_id': rule_id,
                    'description': description,
                    'file': str(filepath.relative_to(REPO_ROOT)),
                    'line': line_no,
                    'content': stripped[:120],
                })
    return findings


def get_src_cs_files() -> list[Path]:
    """Get all .cs files in src/ — the primary enforcement scope."""
    src_dir = REPO_ROOT / "src"
    if not src_dir.exists():
        return []
    return list(src_dir.rglob("*.cs"))


def main():
    print("[RULES GATE] V12.36 — Jane Street Rules Catalog Gate starting...", file=sys.stderr)

    # Step 1: Verify catalog is readable
    if not check_catalog_readable():
        print(
            "\n=== RULES CATALOG GATE: BLOCKED ===\n"
            "Reason: Rules catalog is unreadable (UTF-16 or missing).\n"
            "Action: Fix encoding before any work proceeds.\n"
            "Work Status: STOPPED.\n"
            "=====================================",
            file=sys.stderr,
        )
        sys.exit(2)

    print("[RULES GATE] Catalog: UTF-8 clean ✅", file=sys.stderr)

    # Step 2: Scan src/ for P0 violations
    cs_files = get_src_cs_files()
    print(f"[RULES GATE] Scanning {len(cs_files)} .cs files in src/ for P0 violations...", file=sys.stderr)

    all_findings = []
    for f in cs_files:
        all_findings.extend(scan_file_for_p0(f))

    if not all_findings:
        print("[RULES GATE] P0 scan: CLEAN ✅ — No violations found.", file=sys.stderr)
        print(
            "\n=== RULES CATALOG GATE: PASS ===\n"
            "Catalog: UTF-8 clean\n"
            "P0 violations: 0\n"
            "Work Status: PROCEED\n"
            "=================================",
            file=sys.stderr,
        )
        sys.exit(0)

    # Step 3: Report violations and block
    print(
        f"\n=== RULES CATALOG GATE: BLOCKED ===",
        file=sys.stderr,
    )
    print(f"P0 Violations found: {len(all_findings)}\n", file=sys.stderr)

    for v in all_findings:
        print(
            f"  VIOLATION: {v['rule_id']} — {v['description']}\n"
            f"  File: {v['file']}:{v['line']}\n"
            f"  Code: {v['content']}\n",
            file=sys.stderr,
        )

    print(
        "Action Required: Fix ALL P0 violations listed above before any work proceeds.\n"
        "Do NOT auto-fix. Report to Director for each violation.\n"
        "Work Status: STOPPED — zero further execution.\n"
        "=====================================",
        file=sys.stderr,
    )
    sys.exit(1)


if __name__ == "__main__":
    main()
