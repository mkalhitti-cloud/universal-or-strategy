import json

path = r"c:\WSGTA\universal-or-strategy\scratch\last_prompt_result.txt"
with open(path, "r", encoding="utf-16") as f:
    lines = f.readlines()
    for line in lines:
        if "Content: " in line:
            json_str = line.split("Content: ")[1].strip()
            data = json.loads(json_str)
            print(data.get("content", "No content found"))
