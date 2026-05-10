import os

with open('pr_audit_findings.txt', 'r', encoding='utf-8') as f:
    content = f.read()

# Split by lines and print specifically the review/comment bodies.
lines = content.split('\n')
for line in lines:
    if 'Comment by' in line or 'Review by' in line or 'STATUS CHECKS' in line or 'FAILURE' in line or 'ACTION_REQUIRED' in line or 'P1:' in line or 'violation' in line or 'Potential issue' in line:
        print(line.strip())
    elif 'diff' in line or '```' in line:
        pass
    # keep it brief to avoid truncation
