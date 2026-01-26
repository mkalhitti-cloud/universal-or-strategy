import json
import sys
import os

def validate_task(task_id, tasks_file):
    if not os.path.exists(tasks_file):
        return {"allowed": False, "reason": f"Tasks file not found at {tasks_file}"}

    try:
        with open(tasks_file, 'r') as f:
            data = json.load(f)
    except Exception as e:
        return {"allowed": False, "reason": f"Failed to parse tasks file: {str(e)}"}

    tasks = data.get("tasks", [])
    task = next((t for t in tasks if t.get("id") == task_id), None)

    if not task:
        return {"allowed": False, "reason": f"Task ID '{task_id}' not found in {tasks_file}"}

    if task.get("status") == "completed":
        return {"allowed": False, "reason": f"Task '{task_id}' is already completed."}

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

    return {"allowed": True, "reason": "All dependencies met and no blockers found."}

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(json.dumps({"allowed": False, "reason": "No Task ID provided. Usage: python validate_task.py <TASK_ID>"}))
        sys.exit(1)

    task_id = sys.argv[1]
    # Assume default path if running from project root or .agent
    tasks_file = os.path.join(os.path.dirname(__file__), "..", "TASKS", "MASTER_TASKS.json")
    
    result = validate_task(task_id, tasks_file)
    print(json.dumps(result, indent=2))
