import subprocess
import json

try:
    result = subprocess.run(['gh', 'pr', 'view', '--json', 'comments,reviews,statusCheckRollup'], capture_output=True, text=False, check=True)
    data = json.loads(result.stdout.decode('utf-8'))
    
    with open('pr_audit_findings.txt', 'w', encoding='utf-8') as f:
        f.write("=== STATUS CHECKS ===\n")
        if 'statusCheckRollup' in data and data['statusCheckRollup']:
            for check in data['statusCheckRollup']:
                f.write(f"- {check.get('name', check.get('context', 'Unknown'))}: {check.get('conclusion', check.get('state', 'Unknown'))}\n")
        
        f.write("\n=== COMMENTS ===\n")
        if 'comments' in data:
            for c in data['comments']:
                author = c.get('author', {}).get('login', 'Unknown')
                body = c.get('body', '')
                if 'DeepSource' in body or 'CodeRabbit' in body or 'Codacy' in body or 'Review' in body:
                    f.write(f"\n--- Comment by {author} ---\n")
                    # truncate very long bodies for sanity, but keep enough for the audit
                    f.write(body[:3000] + "\n")
                    
        f.write("\n=== REVIEWS ===\n")
        if 'reviews' in data:
            for r in data['reviews']:
                author = r.get('author', {}).get('login', 'Unknown')
                body = r.get('body', '')
                f.write(f"\n--- Review by {author} ---\n")
                f.write(body[:3000] + "\n")
                
    print("Successfully extracted PR data to pr_audit_findings.txt")
except Exception as e:
    print(f"Error: {e}")
