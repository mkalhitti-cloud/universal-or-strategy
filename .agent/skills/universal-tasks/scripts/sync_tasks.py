import json
import os
import sys
import time
from datetime import datetime

def sync_task(task_id, status, tasks_file, agent_id=None):
    if not os.path.exists(tasks_file):
        return {"success": False, "reason": "Tasks file not found"}

    lock_file = tasks_file + ".lock"
    max_retries = 5
    
    # Simple File Locking to prevent race conditions
    for _ in range(max_retries):
        try:
            # Create a lock file
            with open(lock_file, "x") as f:
                f.write(str(os.getpid()))
            break
        except FileExistsError:
            time.sleep(0.5) # Wait and retry
            continue
    else:
        return {"success": False, "reason": "Could not acquire lock (timeout)"}

    try:
        with open(tasks_file, 'r', encoding='utf-8') as f:
            data = json.load(f)

        tasks = data.get("tasks", [])
        task = next((t for t in tasks if t.get("id") == task_id), None)

        if not task:
            return {"success": False, "reason": f"Task ID '{task_id}' not found"}

        current_time = datetime.now().isoformat()
        
        # Stale claim recovery logic (also in sync for safety)
        claimed_at = task.get("claimed_at")
        if claimed_at and task.get("status") == "in_progress":
            try:
                claimed_dt = datetime.fromisoformat(claimed_at)
                if (datetime.now() - claimed_dt).total_seconds() > 3600:
                    # Auto-release stale claim
                    task["claimed_by"] = None
                    task["claimed_at"] = None
                    task["status"] = "pending"
            except Exception:
                pass

        # Claiming logic
        if status == "in_progress":
            if not agent_id:
                return {"success": False, "reason": "Agent ID required to claim task"}
            
            if task.get("claimed_by") and task.get("claimed_by") != agent_id:
                return {"success": False, "reason": f"Task already claimed by {task.get('claimed_by')}"}
            
            task["claimed_by"] = agent_id
            task["claimed_at"] = current_time
        
        # Ownership check on completion
        if status == "completed":
            if task.get("claimed_by") and task.get("claimed_by") != agent_id:
                return {"success": False, "reason": f"Permission denied: Task is claimed by {task.get('claimed_by')}. Only the owner can complete it."}
            
            task["claimed_by"] = None # Release claim
            task["claimed_at"] = None
            task["completed_date"] = datetime.now().strftime("%Y-%m-%d")

        old_status = task.get("status")
        task["status"] = status
        task["last_updated"] = current_time
        data["last_updated"] = current_time

        with open(tasks_file, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2)
            
        return {
            "success": True, 
            "task_id": task_id,
            "old_status": old_status,
            "new_status": status,
            "agent_id": task.get("claimed_by"),
            "broadcast": f"BROADCAST: Task {task_id} updated to {status}"
        }
    except Exception as e:
        return {"success": False, "reason": f"Unexpected error: {str(e)}"}
    finally:
        # Always remove the lock file
        if os.path.exists(lock_file):
            os.remove(lock_file)

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(json.dumps({"success": False, "reason": "Usage: python sync_tasks.py <TASK_ID> <STATUS> [AGENT_ID]"}))
        sys.exit(1)

    task_id = sys.argv[1]
    status = sys.argv[2]
    agent_id = sys.argv[3] if len(sys.argv) > 3 else None
    
    # Resolve tasks file path
    tasks_file = os.path.join(os.path.dirname(__file__), "..", "..", "..", "TASKS", "MASTER_TASKS.json")
    
    result = sync_task(task_id, status, tasks_file, agent_id)
    print(json.dumps(result, indent=2))
