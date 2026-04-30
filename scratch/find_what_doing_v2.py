import json
import os

brain_path = r"C:\Users\Mohammed Khalid\.gemini\antigravity\brain"
conv_ids = [d for d in os.listdir(brain_path) if os.path.isdir(os.path.join(brain_path, d))]
current_conv = "7acbdd7b-a6e0-4d52-b672-ce3b2842619c"

for cid in sorted(conv_ids, reverse=True):
    if cid == current_conv: continue
    log_path = os.path.join(brain_path, cid, ".system_generated", "logs", "overview.txt")
    if os.path.exists(log_path):
        with open(log_path, "r", encoding="utf-8") as f:
            lines = f.readlines()
            for i, line in enumerate(lines):
                if "$what are you doing" in line:
                    print(f"Match in {cid} at line {i+1}")
                    # Print the next few steps
                    for j in range(i+1, min(i+20, len(lines))):
                        try:
                            data = json.loads(lines[j])
                            if data.get("source") == "MODEL" and "content" in data:
                                print(f"--- Conversation {cid} Step {data.get('step_index')} ---")
                                print(data["content"])
                                break # Just get the next model response
                        except:
                            pass
