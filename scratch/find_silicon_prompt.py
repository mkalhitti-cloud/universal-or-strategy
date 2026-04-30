import json
import os

path = r"C:\Users\Mohammed Khalid\.gemini\antigravity\brain\733c7fbb-ef32-4a6b-b2a4-4c9bd313e6d5\.system_generated\logs\overview.txt"
if os.path.exists(path):
    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            if "Silicon-Safe Trojan Horse" in line:
                try:
                    data = json.loads(line)
                    print(data.get("content", "No content found"))
                except:
                    print(line)
else:
    print("File not found")
