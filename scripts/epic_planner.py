#!/usr/bin/env python3
"""
Multi-Signal Epic Planner
Combines jcodemunch hotspots with CodeScene CLI reviews for comprehensive epic prioritization.
"""

import os
import sys
import json
import subprocess
from pathlib import Path
from typing import Dict, List, Optional

# Load environment variables
def load_env():
    env_path = Path(__file__).parent.parent / '.env'
    if env_path.exists():
        with open(env_path) as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith('#') and '=' in line:
                    key, value = line.split('=', 1)
                    os.environ[key.strip()] = value.strip()

load_env()

def get_jcodemunch_hotspots(repo: str, top_n: int = 20, days: int = 90) -> List[Dict]:
    """Get hotspots from jcodemunch-mcp"""
    print(f"[*] Fetching top {top_n} hotspots from jcodemunch...")
    
    # This would normally call jcodemunch-mcp via MCP protocol
    # For now, we'll use a placeholder that reads from a cached file
    cache_file = Path(__file__).parent.parent / 'jcodemunch_hotspots.json'
    
    if cache_file.exists():
        with open(cache_file) as f:
            return json.load(f)
    
    print("[!]  No cached hotspot data. Run jcodemunch get_hotspots first.")
    return []

def get_codescene_review(file_path: str) -> Optional[Dict]:
    """Get CodeScene CLI review for a file"""
    print(f"🔍 Reviewing {file_path} with CodeScene CLI...")
    
    try:
        result = subprocess.run(
            ['cs', 'review', file_path, '--output-format', 'json'],
            capture_output=True,
            text=True,
            check=True
        )
        return json.loads(result.stdout)
    except subprocess.CalledProcessError as e:
        print(f"[X] CodeScene review failed: {e}")
        return None
    except json.JSONDecodeError as e:
        print(f"[X] Failed to parse CodeScene output: {e}")
        return None

def calculate_composite_score(hotspot: Dict, codescene: Optional[Dict]) -> float:
    """
    Calculate composite epic priority score using multiple signals:
    - Hotspot Score (40%): complexity x log(1 + churn)
    - Code Health (30%): CodeScene score (inverted: 10 - score)
    - Severity (20%): Number of high-severity issues
    - Churn (10%): Raw commit count
    """
    # Signal 1: Hotspot Score (40%)
    hotspot_score = hotspot.get('hotspot_score', 0) * 0.4
    
    # Signal 2: Code Health (30%) - inverted so lower health = higher priority
    if codescene and codescene.get('score'):
        code_health_penalty = (10 - codescene['score']) * 3.0  # Scale to match hotspot range
    else:
        code_health_penalty = 0
    
    # Signal 3: Severity (20%) - count high-severity issues
    severity_score = 0
    if codescene and codescene.get('review'):
        for issue in codescene['review']:
            if issue.get('indication', 0) >= 3:  # High severity
                severity_score += len(issue.get('functions', []))
    severity_score *= 2.0  # Scale factor
    
    # Signal 4: Churn (10%)
    churn_score = hotspot.get('churn', 0) * 0.5
    
    composite = hotspot_score + code_health_penalty + severity_score + churn_score
    return round(composite, 2)

def generate_epic_roadmap(hotspots: List[Dict], top_n: int = 10) -> List[Dict]:
    """Generate prioritized epic roadmap with multi-signal scoring"""
    print(f"\n[*] Generating epic roadmap for top {top_n} hotspots...")
    
    epics = []
    
    for idx, hotspot in enumerate(hotspots[:top_n], 1):
        file_path = hotspot.get('file', '')
        method_name = hotspot.get('name', '')
        
        # Get CodeScene review
        codescene_review = get_codescene_review(file_path)
        
        # Calculate composite score
        composite_score = calculate_composite_score(hotspot, codescene_review)
        
        epic = {
            'epic_number': f'EPIC-CCN-{12 + idx}',
            'method': method_name,
            'file': file_path,
            'line': hotspot.get('line', 0),
            'cyclomatic': hotspot.get('cyclomatic', 0),
            'churn': hotspot.get('churn', 0),
            'hotspot_score': hotspot.get('hotspot_score', 0),
            'codescene_score': codescene_review.get('score') if codescene_review else None,
            'composite_score': composite_score,
            'codescene_issues': []
        }
        
        # Extract CodeScene issues
        if codescene_review and codescene_review.get('review'):
            for issue in codescene_review['review']:
                for func in issue.get('functions', []):
                    if func.get('title') == method_name:
                        epic['codescene_issues'].append({
                            'category': issue.get('category'),
                            'details': func.get('details'),
                            'indication': issue.get('indication', 0)
                        })
        
        epics.append(epic)
    
    # Sort by composite score (descending)
    epics.sort(key=lambda x: x['composite_score'], reverse=True)
    
    return epics

def print_roadmap(epics: List[Dict]):
    """Print epic roadmap in human-readable format"""
    print("\n" + "="*80)
    print("[*] MULTI-SIGNAL EPIC ROADMAP")
    print("="*80)
    print(f"\n{'Rank':<6} {'Epic':<15} {'Method':<30} {'Composite':<12} {'Hotspot':<10} {'Health':<8}")
    print("-"*80)
    
    for idx, epic in enumerate(epics, 1):
        health = f"{epic['codescene_score']:.1f}" if epic['codescene_score'] else "N/A"
        print(f"{idx:<6} {epic['epic_number']:<15} {epic['method'][:28]:<30} "
              f"{epic['composite_score']:<12.2f} {epic['hotspot_score']:<10.2f} {health:<8}")
    
    print("\n" + "="*80)
    print("[*] SCORING BREAKDOWN")
    print("="*80)
    print("Composite Score = Hotspot(40%) + CodeHealth(30%) + Severity(20%) + Churn(10%)")
    print("- Hotspot: Complexity x log(1 + churn)")
    print("- CodeHealth: 10 - CodeScene score (inverted)")
    print("- Severity: Count of high-severity issues")
    print("- Churn: Raw commit count")

def save_roadmap(epics: List[Dict], output_file: str):
    """Save roadmap to JSON file"""
    output_path = Path(__file__).parent.parent / output_file
    with open(output_path, 'w') as f:
        json.dump(epics, f, indent=2)
    print(f"\n💾 Roadmap saved to: {output_path}")

def main():
    if len(sys.argv) < 2:
        print("Usage: python epic_planner.py <command> [args]")
        print("\nCommands:")
        print("  generate [--top-n N]  - Generate epic roadmap (default: top 10)")
        print("  review <file>         - Review single file with CodeScene")
        sys.exit(1)
    
    command = sys.argv[1]
    
    if command == "generate":
        top_n = 10
        if '--top-n' in sys.argv:
            idx = sys.argv.index('--top-n')
            top_n = int(sys.argv[idx + 1])
        
        # Get hotspots
        hotspots = get_jcodemunch_hotspots('universal-or-strategy', top_n=top_n)
        
        if not hotspots:
            print("[X] No hotspot data available")
            sys.exit(1)
        
        # Generate roadmap
        epics = generate_epic_roadmap(hotspots, top_n=top_n)
        
        # Print and save
        print_roadmap(epics)
        save_roadmap(epics, 'epic_roadmap.json')
    
    elif command == "review":
        if len(sys.argv) < 3:
            print("Usage: python epic_planner.py review <file>")
            sys.exit(1)
        
        file_path = sys.argv[2]
        review = get_codescene_review(file_path)
        
        if review:
            print(json.dumps(review, indent=2))
        else:
            print("[X] Review failed")
            sys.exit(1)
    
    else:
        print(f"Unknown command: {command}")
        sys.exit(1)

if __name__ == "__main__":
    main()

# Made with Bob
