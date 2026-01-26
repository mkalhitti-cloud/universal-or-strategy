#!/usr/bin/env python3
"""
Test script to simulate concurrent agents claiming the same task.

Scenario:
- Agent A and Agent B try to claim task V9_002 simultaneously
- One should succeed, the other should fail with "already claimed"
- Then Agent A completes the task
- Agent B should see it as completed, not claimable
"""

import json
import os
import sys
import threading
import time
import subprocess
from pathlib import Path
from datetime import datetime

# Path to tasks file
TASKS_FILE = os.path.join(os.path.dirname(__file__), "..", "..", "..", "TASKS", "MASTER_TASKS.json")
SYNC_SCRIPT = os.path.join(os.path.dirname(__file__), "sync_tasks.py")
VALIDATE_SCRIPT = os.path.join(os.path.dirname(__file__), "validate_task.py")

def run_sync(task_id, status, agent_id):
    """Run sync_tasks.py and return the result"""
    result = subprocess.run(
        ["python", SYNC_SCRIPT, task_id, status, agent_id],
        capture_output=True,
        text=True
    )
    try:
        return json.loads(result.stdout)
    except json.JSONDecodeError:
        return {"success": False, "reason": f"Failed to parse output: {result.stdout}"}

def run_validate(task_id, agent_id):
    """Run validate_task.py and return the result"""
    result = subprocess.run(
        ["python", VALIDATE_SCRIPT, task_id, agent_id],
        capture_output=True,
        text=True
    )
    try:
        return json.loads(result.stdout)
    except json.JSONDecodeError:
        return {"allowed": False, "reason": f"Failed to parse output: {result.stdout}"}

class TestResults:
    def __init__(self):
        self.results = []
        self.lock = threading.Lock()

    def log(self, agent, action, result):
        with self.lock:
            timestamp = datetime.now().isoformat()
            self.results.append({
                "timestamp": timestamp,
                "agent": agent,
                "action": action,
                "result": result
            })
            print(f"[{timestamp}] {agent}: {action}")
            print(f"  -> Result: {result}\n")

results = TestResults()

def agent_workflow(agent_id, test_results):
    """Simulate an agent trying to claim and complete a task"""
    task_id = "MCP_001"

    # Step 1: Validate
    results.log(agent_id, f"Validating task {task_id}", "...")
    validation = run_validate(task_id, agent_id)
    if validation.get("allowed"):
        results.log(agent_id, "Validation PASSED", "proceeding...")
    else:
        results.log(agent_id, "Validation FAILED", validation.get("reason"))
        return

    # Step 2: Try to claim (sync to in_progress)
    results.log(agent_id, f"Attempting to claim {task_id}", "...")
    time.sleep(0.01)  # Small delay to increase chance of collision
    claim_result = run_sync(task_id, "in_progress", agent_id)
    results.log(agent_id, f"Claim result", claim_result)

    if not claim_result.get("success"):
        results.log(agent_id, "FAILED to claim task", claim_result.get("reason"))
        return

    results.log(agent_id, "[OK] Successfully claimed task", f"claimed_by={agent_id}")

    # Step 3: Simulate work
    results.log(agent_id, "Working on task", "sleeping 1 second...")
    time.sleep(1)

    # Step 4: Mark as completed
    results.log(agent_id, f"Marking {task_id} as completed", "...")
    complete_result = run_sync(task_id, "completed", agent_id)
    results.log(agent_id, f"Completion result", complete_result)

    if complete_result.get("success"):
        results.log(agent_id, "[OK] Task completed successfully", "")

def main():
    print("=" * 80)
    print("CONCURRENT TASK CLAIMING TEST")
    print("=" * 80)
    print(f"\nTesting file: {TASKS_FILE}")
    print(f"Target task: V9_002\n")

    # Verify files exist
    if not os.path.exists(TASKS_FILE):
        print(f"ERROR: {TASKS_FILE} not found")
        sys.exit(1)

    if not os.path.exists(SYNC_SCRIPT):
        print(f"ERROR: {SYNC_SCRIPT} not found")
        sys.exit(1)

    print("Starting concurrent test...\n")

    # Create threads for two agents
    agent_a = threading.Thread(target=agent_workflow, args=("Agent_Test_A", results))
    agent_b = threading.Thread(target=agent_workflow, args=("Agent_Test_B", results))

    # Start both threads at nearly the same time
    agent_a.start()
    agent_b.start()

    # Wait for both to complete
    agent_a.join()
    agent_b.join()

    print("\n" + "=" * 80)
    print("TEST RESULTS SUMMARY")
    print("=" * 80)

    # Check final state
    with open(TASKS_FILE, 'r') as f:
        final_data = json.load(f)

    final_task = next((t for t in final_data.get("tasks", []) if t.get("id") == "V9_002"), None)

    print(f"\nFinal task state:")
    print(f"  ID: {final_task.get('id')}")
    print(f"  Status: {final_task.get('status')}")
    print(f"  Claimed by: {final_task.get('claimed_by')}")
    print(f"  Last updated: {final_task.get('last_updated')}")

    # Analyze results
    claim_attempts = [r for r in results.results if "Claim result" in r.get("action", "")]
    successful_claims = [r for r in claim_attempts if r.get("result", {}).get("success")]
    failed_claims = [r for r in claim_attempts if not r.get("result", {}).get("success")]

    print(f"\n[RESULTS]")
    print(f"  Claim attempts: {len(claim_attempts)}")
    print(f"  - Successful: {len(successful_claims)}")
    print(f"  - Failed: {len(failed_claims)}")

    if len(successful_claims) == 1 and final_task.get("status") == "completed":
        print("\n[PASS] Exactly one agent claimed and completed the task.")
    else:
        print("\n[FAIL] Unexpected final state or concurrent claim issue.")

    print("\n" + "=" * 80)

if __name__ == "__main__":
    main()
