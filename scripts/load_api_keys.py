#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Smart API Key Loader with BobCoin Budget Management

Loads API keys from docs/API/ folder and manages them to avoid negative balances.
Each key has 160 BobCoins. Leaves 5 BC buffer to avoid customer support emails.

Usage:
    python scripts/load_api_keys.py --epics 10 --phases 9

Features:
- Loads all .json files from docs/API/
- Calculates BobCoin requirements per epic
- Distributes work across keys to leave positive balance
- Warns when more keys needed
"""

import json
import glob
from pathlib import Path
from typing import List, Dict

# Constants
BOBCOINS_PER_KEY = 160
SAFETY_BUFFER = 5  # Leave 5 BC to avoid negative balance
USABLE_COINS_PER_KEY = BOBCOINS_PER_KEY - SAFETY_BUFFER  # 155 BC

# Phase costs (estimated BobCoins per phase per epic)
PHASE_COSTS = {
    "phase_0": 3,   # Hotspot analysis (fast)
    "phase_1": 3,   # Scope definition
    "phase_1.5": 2, # Scope boundary
    "phase_2": 5,   # Architecture planning
    "phase_3": 4,   # DNA & PR audit
    "phase_4": 3,   # Ticket generation
    "phase_5": 25,  # Ticket execution (expensive - Bob CLI)
    "phase_5.5": 4, # Verification
    "phase_6": 3,   # Final review
}

TOTAL_COST_PER_EPIC = sum(PHASE_COSTS.values())  # 52 BC per epic

def load_api_keys_from_folder(folder_path: str = "docs/API") -> List[str]:
    """Load all API keys from JSON files in folder."""
    keys = []
    json_files = glob.glob(f"{folder_path}/*.json")
    
    for file_path in sorted(json_files):
        try:
            with open(file_path, 'r') as f:
                data = json.load(f)
                if 'apikey' in data:
                    keys.append(data['apikey'])
        except Exception as e:
            print(f"⚠️  Error loading {file_path}: {e}")
    
    return keys

def calculate_key_distribution(num_epics: int, num_keys: int) -> Dict:
    """Calculate how to distribute epics across keys to avoid negative balances."""
    total_cost = num_epics * TOTAL_COST_PER_EPIC
    total_capacity = num_keys * USABLE_COINS_PER_KEY
    
    if total_cost > total_capacity:
        keys_needed = (total_cost + USABLE_COINS_PER_KEY - 1) // USABLE_COINS_PER_KEY
        return {
            "feasible": False,
            "total_cost": total_cost,
            "total_capacity": total_capacity,
            "keys_available": num_keys,
            "keys_needed": keys_needed,
            "shortage": keys_needed - num_keys
        }
    
    # Distribute epics evenly across keys
    epics_per_key = num_epics // num_keys
    remainder = num_epics % num_keys
    
    distribution = []
    for i in range(num_keys):
        epics_for_this_key = epics_per_key + (1 if i < remainder else 0)
        cost_for_this_key = epics_for_this_key * TOTAL_COST_PER_EPIC
        remaining_balance = USABLE_COINS_PER_KEY - cost_for_this_key
        
        distribution.append({
            "key_index": i,
            "epics_assigned": epics_for_this_key,
            "cost": cost_for_this_key,
            "remaining_balance": remaining_balance,
            "final_balance": BOBCOINS_PER_KEY - cost_for_this_key  # Including buffer
        })
    
    return {
        "feasible": True,
        "total_cost": total_cost,
        "total_capacity": total_capacity,
        "keys_available": num_keys,
        "keys_needed": (total_cost + USABLE_COINS_PER_KEY - 1) // USABLE_COINS_PER_KEY,
        "distribution": distribution
    }

def format_keys_for_executor(keys: List[str]) -> str:
    """Format keys as comma-separated string for autonomous_executor.py."""
    return ",".join(keys)

def main():
    import argparse
    
    parser = argparse.ArgumentParser(description="Load and manage API keys")
    parser.add_argument("--epics", type=int, default=10, help="Number of epics to process")
    parser.add_argument("--folder", default="docs/API", help="Folder containing API key JSON files")
    parser.add_argument("--export", action="store_true", help="Export keys as comma-separated string")
    args = parser.parse_args()
    
    print("[KEY] Loading API Keys from docs/API/")
    print()
    
    # Load keys
    keys = load_api_keys_from_folder(args.folder)
    
    if not keys:
        print("[ERROR] No API keys found in docs/API/")
        print("   Add .json files with format: {\"apikey\": \"bob_prod_...\"}")
        return 1
    
    print(f"[OK] Loaded {len(keys)} API keys")
    print(f"   Total capacity: {len(keys) * USABLE_COINS_PER_KEY} BobCoins (with {SAFETY_BUFFER} BC buffer per key)")
    print()
    
    # Calculate distribution
    print(f"[PLAN] Planning for {args.epics} epics")
    print(f"   Cost per epic: {TOTAL_COST_PER_EPIC} BC")
    print(f"   Total cost: {args.epics * TOTAL_COST_PER_EPIC} BC")
    print()
    
    distribution = calculate_key_distribution(args.epics, len(keys))
    
    if not distribution["feasible"]:
        print("[ERROR] INSUFFICIENT BOBCOINS!")
        print(f"   Need: {distribution['keys_needed']} keys")
        print(f"   Have: {distribution['keys_available']} keys")
        print(f"   Shortage: {distribution['shortage']} keys")
        print()
        print(f"[TIP] Add {distribution['shortage']} more API keys to docs/API/")
        return 1
    
    print("[OK] FEASIBLE - Keys have sufficient capacity")
    print()
    print("[PLAN] Distribution Plan:")
    print()
    
    for item in distribution["distribution"]:
        if item["epics_assigned"] > 0:
            print(f"   Key {item['key_index'] + 1}:")
            print(f"      Epics: {item['epics_assigned']}")
            print(f"      Cost: {item['cost']} BC")
            print(f"      Final Balance: {item['final_balance']} BC (positive)")
            print()
    
    print(f"[COST] Total Cost: {distribution['total_cost']} BC")
    print(f"[CAPACITY] Total Capacity: {distribution['total_capacity']} BC")
    print(f"[REMAINING] Remaining: {distribution['total_capacity'] - distribution['total_cost']} BC")
    print()
    
    if args.export:
        print("[EXPORT] Exporting keys for autonomous_executor.py:")
        print()
        keys_str = format_keys_for_executor(keys)
        print(keys_str)
        print()
        print("Copy the above line and use:")
        print(f"python3 scripts/autonomous_executor.py --api-keys \"{keys_str}\" --workers 8")
    else:
        print("[TIP] To export keys, run:")
        print("   python scripts/load_api_keys.py --epics 10 --export")
    
    return 0

if __name__ == "__main__":
    import sys
    sys.exit(main())

# Made with Bob
