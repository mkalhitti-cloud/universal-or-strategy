import subprocess
import os

def fix_with_main_baseline(path, search_replace_pairs):
    try:
        baseline = subprocess.check_output(['git', 'show', 'main:' + path], encoding='utf-8')
    except:
        print(f'File {path} not on main')
        return

    for old, new in search_replace_pairs:
        if old in baseline:
            baseline = baseline.replace(old, new)
        else:
            print(f'Warning: {old} not found in {path}')

    with open(path, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write(baseline)
    print(f'Fixed {path}')

trailing_fixes = [
    ('if (EnableSIMA) ManageTrail_RunFleetSymmetrySync(positionSnapshot);', 
     '// [LD-003] Thread-Safety: Use a fresh snapshot for fleet sync to prevent stale stop synchronization.\r\n            if (EnableSIMA)\r\n            {\r\n                var updatedSnapshot = activePositions.ToArray();\r\n                ManageTrail_RunFleetSymmetrySync(updatedSnapshot);\r\n            }'),
    ('? MANUAL BREAKEVEN', '(!) MANUAL BREAKEVEN'),
    ('// Print(string.Format("TREND E1 TRAIL', 'Print(string.Format("TREND E1 TRAIL')
]

dispatch_fixes = [
    ('DateTime.Now.Ticks', 'DateTime.UtcNow.Ticks')
]

fix_with_main_baseline('src/V12_002.Trailing.cs', trailing_fixes)
fix_with_main_baseline('src/V12_002.SIMA.Dispatch.cs', dispatch_fixes)
