import json
import os

brain_path = r"C:\Users\Mohammed Khalid\.gemini\antigravity\brain"
conv_ids = [d for d in os.listdir(brain_path) if os.path.isdir(os.path.join(brain_path, d))]

for cid in sorted(conv_ids, reverse=True):
    log_path = os.path.join(brain_path, cid, ".system_generated", "logs", "overview.txt")
    if os.path.exists(log_path):
        with open(log_path, "r", encoding="utf-8") as f:
            lines = f.readlines()
            for i, line in enumerate(lines):
                if "$what are you doing" in line:
                    print(f"Match in {cid} at line {i+1}")
                    # Print the next few lines
                    for j in range(i+1, min(i+10, len(lines))):
                        try:
                            data = json.loads(lines[j])
                            if "content" in data:
                                print(f"--- Step {data.get('step_index')} Content ---")
                                print(data["content"])
                        except:
                            pass
