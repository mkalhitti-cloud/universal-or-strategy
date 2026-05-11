import subprocess
import os
import re

def get_rev_content(rev, path):
    try:
        return subprocess.check_output(['git', 'show', rev + ':' + path], encoding='utf-8')
    except Exception as e:
        print(f"Error fetching {path} from {rev}: {e}")
        return None

def apply_fixes(path, content):
    # Normalize to LF and strip BOM if any
    content = content.replace('\ufeff', '')
    content = content.replace('\r\n', '\n')
    
    if 'Trailing.cs' in path:
        # Compliance
        content = content.replace('? MANUAL BREAKEVEN', '(!) MANUAL BREAKEVEN')
        content = content.replace('// Print(string.Format("TREND E1 TRAIL', 'Print(string.Format("TREND E1 TRAIL')
        
        # Thread Safety (Task B)
        sync_old = 'if (EnableSIMA) ManageTrail_RunFleetSymmetrySync(positionSnapshot);'
        sync_new = '// [LD-003] Thread-Safety: Use a fresh snapshot for fleet sync to prevent stale stop synchronization.\n            if (EnableSIMA)\n            {\n                var updatedSnapshot = activePositions.ToArray();\n                ManageTrail_RunFleetSymmetrySync(updatedSnapshot);\n            }'
        content = content.replace(sync_old, sync_new)
        
        # Hygiene: Ternary for extreme price
        content = re.sub(
            r'if\s*\(pos\.Direction\s*==\s*MarketPosition\.Long\)\s+pos\.ExtremePriceSinceEntry\s*=\s*Math\.Max\(pos\.ExtremePriceSinceEntry,\s*Close\[0\]\);\s+else\s+pos\.ExtremePriceSinceEntry\s*=\s*Math\.Min\(pos\.ExtremePriceSinceEntry,\s*Close\[0\]\);',
            'pos.ExtremePriceSinceEntry = pos.Direction == MarketPosition.Long ? Math.Max(pos.ExtremePriceSinceEntry, Close[0]) : Math.Min(pos.ExtremePriceSinceEntry, Close[0]);',
            content
        )

    if 'SIMA.Dispatch.cs' in path:
        content = content.replace('DateTime.Now.Ticks', 'DateTime.UtcNow.Ticks')
        # Remove unused variable
        content = re.sub(r'// V12: Followers ALWAYS use RMA multipliers for point-based trails \(User Req\)\s*bool useRmaForFollower = true;', '', content)
        content = content.replace('bool useRmaForFollower = true;', '')

    if 'Execution.cs' in path:
        # Nullable check simplification (regex to handle newlines)
        content = re.sub(
            r'pos\.ExecutingAccount\s*!=\s*null\s*&&\s*pos\.ExecutingAccount\.Name\s*==\s*flatAcctName',
            'pos.ExecutingAccount?.Name == flatAcctName',
            content
        )
        content = re.sub(
            r'kvp\.Value\.ExecutingAccount\s*!=\s*null\s*&&\s*kvp\.Value\.ExecutingAccount\.Name\s*==\s*flatAcctName',
            'kvp.Value.ExecutingAccount?.Name == flatAcctName',
            content
        )

    return content

rev = 'c95b800'
files = ['src/V12_002.Trailing.cs', 'src/V12_002.SIMA.Dispatch.cs', 'src/V12_002.Orders.Callbacks.Execution.cs']

for f_path in files:
    content = get_rev_content(rev, f_path)
    if content:
        fixed = apply_fixes(f_path, content)
        with open(f_path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(fixed)
        print(f'Restored, fixed, and LF-normalized: {f_path}')
