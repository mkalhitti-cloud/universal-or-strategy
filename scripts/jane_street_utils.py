#!/usr/bin/env python3
"""
Jane Street Violations Utility

Provides functions to load, filter, and validate Jane Street violations
for use in V12 epic workflow phases.

Usage:
    from jane_street_utils import load_violations_for_file, query_kb
    
    violations = load_violations_for_file("src/V12_002.cs")
    kb_results = query_kb("complexity reduction")
"""

import json
import os
import sys
from pathlib import Path
from typing import List, Dict, Optional, Set

# Firebase KB query
try:
    from query_kb import init_firestore, search_kb as _search_kb
except ImportError:
    # Fallback if query_kb not in path
    sys.path.insert(0, str(Path(__file__).parent))
    from query_kb import init_firestore, search_kb as _search_kb


class JaneStreetViolation:
    """Represents a single Jane Street violation"""
    
    def __init__(self, data: Dict):
        self.rule_id = data.get('rule_id', '')
        self.severity = data.get('severity', 'P0')
        self.category = data.get('category', '')
        self.file = data.get('file', '').replace('\\', '/')
        self.line = data.get('line', 0)
        self.column = data.get('column', 0)
        self.end_line = data.get('end_line', 0)
        self.message = data.get('message', '')
        self.fix_suggestion = data.get('fix_suggestion', '')
        self.code_snippet = data.get('code_snippet', '')
    
    def __repr__(self):
        return f"<Violation {self.rule_id} at {self.file}:{self.line}>"
    
    def to_dict(self) -> Dict:
        return {
            'rule_id': self.rule_id,
            'severity': self.severity,
            'category': self.category,
            'file': self.file,
            'line': self.line,
            'column': self.column,
            'end_line': self.end_line,
            'message': self.message,
            'fix_suggestion': self.fix_suggestion,
            'code_snippet': self.code_snippet
        }
    
    def in_range(self, start_line: int, end_line: int) -> bool:
        """Check if violation is within line range"""
        return start_line <= self.line <= end_line


def load_violations_file() -> List[JaneStreetViolation]:
    """
    Load all violations from jane_street_p0_violations.json
    
    Returns:
        List of JaneStreetViolation objects
    """
    violations_path = Path(__file__).parent.parent / "jane_street_p0_violations.json"
    
    if not violations_path.exists():
        print(f"WARNING: Violations file not found: {violations_path}")
        return []
    
    try:
        # UTF-16 encoded with BOM
        with open(violations_path, 'r', encoding='utf-16') as f:
            data = json.load(f)
        
        violations = []
        for v in data.get('violations', []):
            violations.append(JaneStreetViolation(v))
        
        return violations
    
    except Exception as e:
        print(f"ERROR loading violations file: {e}")
        return []


def load_violations_for_file(file_path: str) -> List[JaneStreetViolation]:
    """
    Load violations for a specific file
    
    Args:
        file_path: Relative path to file (e.g., "src/V12_002.cs")
    
    Returns:
        List of violations in that file
    """
    all_violations = load_violations_file()
    
    # Normalize path separators
    file_path = file_path.replace('\\', '/')
    
    # Filter to this file
    file_violations = [v for v in all_violations if v.file == file_path]
    
    return file_violations


def load_violations_for_files(file_paths: List[str]) -> List[JaneStreetViolation]:
    """
    Load violations for multiple files
    
    Args:
        file_paths: List of relative file paths
    
    Returns:
        List of violations across all files
    """
    all_violations = load_violations_file()
    
    # Normalize paths
    normalized_paths = {p.replace('\\', '/') for p in file_paths}
    
    # Filter to these files
    violations = [v for v in all_violations if v.file in normalized_paths]
    
    return violations


def load_violations_in_range(file_path: str, start_line: int, end_line: int) -> List[JaneStreetViolation]:
    """
    Load violations within a specific line range in a file
    
    Args:
        file_path: Relative path to file
        start_line: Start line (inclusive)
        end_line: End line (inclusive)
    
    Returns:
        List of violations in that range
    """
    file_violations = load_violations_for_file(file_path)
    
    # Filter to line range
    range_violations = [v for v in file_violations if v.in_range(start_line, end_line)]
    
    return range_violations


def get_violation_summary(violations: List[JaneStreetViolation]) -> Dict:
    """
    Get summary statistics for a list of violations
    
    Returns:
        Dict with counts by category, severity, rule
    """
    if not violations:
        return {
            'total': 0,
            'by_category': {},
            'by_severity': {},
            'by_rule': {},
            'files': set()
        }
    
    summary = {
        'total': len(violations),
        'by_category': {},
        'by_severity': {},
        'by_rule': {},
        'files': set()
    }
    
    for v in violations:
        # Count by category
        summary['by_category'][v.category] = summary['by_category'].get(v.category, 0) + 1
        
        # Count by severity
        summary['by_severity'][v.severity] = summary['by_severity'].get(v.severity, 0) + 1
        
        # Count by rule
        summary['by_rule'][v.rule_id] = summary['by_rule'].get(v.rule_id, 0) + 1
        
        # Track files
        summary['files'].add(v.file)
    
    # Convert set to list for JSON serialization
    summary['files'] = list(summary['files'])
    
    return summary


def format_violation_report(violations: List[JaneStreetViolation], title: str = "Jane Street Violations") -> str:
    """
    Format violations as a markdown report
    
    Args:
        violations: List of violations
        title: Report title
    
    Returns:
        Markdown formatted report
    """
    if not violations:
        return f"## {title}\n\n✅ No violations found\n"
    
    summary = get_violation_summary(violations)
    
    report = f"## {title}\n\n"
    report += f"**Total Violations**: {summary['total']}\n\n"
    
    # By category
    report += "**By Category**:\n"
    for category, count in sorted(summary['by_category'].items(), key=lambda x: -x[1]):
        report += f"- {category}: {count}\n"
    report += "\n"
    
    # By rule (top 10)
    report += "**Top Rules**:\n"
    top_rules = sorted(summary['by_rule'].items(), key=lambda x: -x[1])[:10]
    for rule, count in top_rules:
        report += f"- {rule}: {count}\n"
    report += "\n"
    
    # Sample violations (first 5)
    report += "**Sample Violations**:\n"
    for i, v in enumerate(violations[:5], 1):
        report += f"{i}. **{v.rule_id}** ({v.category})\n"
        report += f"   - File: `{v.file}:{v.line}`\n"
        report += f"   - Message: {v.message}\n"
        if v.fix_suggestion:
            report += f"   - Fix: {v.fix_suggestion}\n"
        report += "\n"
    
    if len(violations) > 5:
        report += f"*...and {len(violations) - 5} more violations*\n\n"
    
    return report


def query_kb(query: str) -> str:
    """
    Query Jane Street Firebase KB
    
    Args:
        query: Search term
    
    Returns:
        KB results as formatted string
    """
    try:
        # Initialize Firestore and search
        db = init_firestore()
        
        # Capture output from search_kb (it prints to stdout)
        import io
        from contextlib import redirect_stdout
        
        f = io.StringIO()
        with redirect_stdout(f):
            _search_kb(db, query)
        
        output = f.getvalue()
        
        if not output or "No results found" in output:
            return f"No results found for query: {query}"
        
        return output
    
    except Exception as e:
        return f"ERROR querying KB: {e}"


def validate_no_violations(file_paths: List[str]) -> tuple[bool, List[JaneStreetViolation]]:
    """
    Validate that files have no Jane Street violations
    
    Args:
        file_paths: List of files to check
    
    Returns:
        Tuple of (is_valid, violations_found)
    """
    violations = load_violations_for_files(file_paths)
    return (len(violations) == 0, violations)


def get_files_with_violations() -> Set[str]:
    """
    Get set of all files that have Jane Street violations
    
    Returns:
        Set of file paths
    """
    all_violations = load_violations_file()
    return {v.file for v in all_violations}


def main():
    """CLI interface for testing"""
    import argparse
    
    parser = argparse.ArgumentParser(description="Jane Street Violations Utility")
    parser.add_argument('--file', help='Check violations for specific file')
    parser.add_argument('--files', nargs='+', help='Check violations for multiple files')
    parser.add_argument('--range', nargs=3, metavar=('FILE', 'START', 'END'),
                       help='Check violations in line range')
    parser.add_argument('--summary', action='store_true', help='Show summary of all violations')
    parser.add_argument('--query', help='Query Jane Street KB')
    
    args = parser.parse_args()
    
    if args.file:
        violations = load_violations_for_file(args.file)
        print(format_violation_report(violations, f"Violations in {args.file}"))
    
    elif args.files:
        violations = load_violations_for_files(args.files)
        print(format_violation_report(violations, f"Violations in {len(args.files)} files"))
    
    elif args.range:
        file_path, start, end = args.range
        violations = load_violations_in_range(file_path, int(start), int(end))
        print(format_violation_report(violations, f"Violations in {file_path}:{start}-{end}"))
    
    elif args.summary:
        violations = load_violations_file()
        print(format_violation_report(violations, "All Jane Street Violations"))
    
    elif args.query:
        results = query_kb(args.query)
        print(results)
    
    else:
        parser.print_help()


if __name__ == '__main__':
    main()

# Made with Bob
