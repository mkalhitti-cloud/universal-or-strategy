import json
import sys
import os

from datetime import datetime

def validate_task(task_id, tasks_file, agent_id=None):
    if not os.path.exists(tasks_file):
        return {"allowed": False, "reason": f"Tasks file not found at {tasks_file}"}

    try:
        with open(tasks_file, 'r', encoding='utf-8') as f:
            data = json.load(f)
    except Exception as e:
        return {"allowed": False, "reason": f"Failed to parse tasks file: {str(e)}"}

    tasks = data.get("tasks", [])
    task = next((t for t in tasks if t.get("id") == task_id), None)

    if not task:
        return {"allowed": False, "reason": f"Task ID '{task_id}' not found in {tasks_file}"}

    # Stale claim recovery logic
    claimed_at = task.get("claimed_at")
    if claimed_at and task.get("status") == "in_progress":
        try:
            claimed_dt = datetime.fromisoformat(claimed_at)
            # If claim is older than 1 hour (3600 seconds)
            if (datetime.now() - claimed_dt).total_seconds() > 3600:
                # Automatics recovery: Reset the task state
                task["claimed_by"] = None
                task["claimed_at"] = None
                task["status"] = "pending"
                # Save the recovery
                with open(tasks_file, 'w', encoding='utf-8') as f:
                    json.dump(data, f, indent=2)
        except Exception:
            pass

    if task.get("status") == "completed":
        return {"allowed": False, "reason": f"Task '{task_id}' is already completed."}

    # Claiming check
    claimed_by = task.get("claimed_by")
    if claimed_by and (agent_id is None or claimed_by != agent_id):
        return {"allowed": False, "reason": f"Task '{task_id}' is already claimed by {claimed_by}"}

    # Check Blockers
    blockers = task.get("blockers", [])
    if blockers:
        return {"allowed": False, "reason": f"Task '{task_id}' is blocked by: {', '.join(blockers)}"}

    # Check Dependencies
    dependencies = task.get("dependencies", [])
    unmet = []
    for dep_id in dependencies:
        dep_task = next((t for t in tasks if t.get("id") == dep_id), None)
        if not dep_task:
            unmet.append(f"{dep_id} (not found)")
        elif dep_task.get("status") != "completed":
            unmet.append(f"{dep_id} ({dep_task.get('status')})")

    if unmet:
        return {"allowed": False, "reason": f"Unmet dependencies: {', '.join(unmet)}"}

    return {"allowed": True, "reason": "All dependencies met, no blockers, and not claimed by others."}

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(json.dumps({"allowed": False, "reason": "No Task ID provided. Usage: python validate_task.py <TASK_ID> [AGENT_ID]"}))
        sys.exit(1)

    task_id = sys.argv[1]
    agent_id = sys.argv[2] if len(sys.argv) > 2 else None
    
    # Resolve tasks file path
    tasks_file = os.path.join(os.path.dirname(__file__), "..", "..", "..", "TASKS", "MASTER_TASKS.json")
    
    result = validate_task(task_id, tasks_file, agent_id)
    print(json.dumps(result, indent=2))
