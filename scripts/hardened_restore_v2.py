import subprocess
import re
import os

def hardened_restore(path, helper_names, logic_fixes_func=None):
    try:
        # 1. Get Main baseline
        base = subprocess.check_output(['git', 'show', 'main:' + path], encoding='utf-8')
        base = base.replace('\ufeff', '').replace('\r\n', '\n')

        # 2. Get current refactored version (HEAD)
        with open(path, 'r', encoding='utf-8') as f:
            refactored = f.read()
        
        # 3. Extract new helpers
        helpers = ""
        for h in helper_names:
            pattern = r'private (?:void|bool) ' + re.escape(h) + r'\(.*?\)\s*\{'
            match = re.search(pattern, refactored)
            if match:
                start = match.start()
                brace_count = 0
                end = -1
                for i in range(start, len(refactored)):
                    if refactored[i] == '{': brace_count += 1
                    elif refactored[i] == '}': 
                        brace_count -= 1
                        if brace_count == 0:
                            end = i + 1
                            break
                if end != -1:
                    helpers += "\n\n        " + refactored[start:end]

        # 4. Extract parent God Function bodies and replace in baseline
        # (This script assumes the God Function name is the one NOT in helper_names but mentioned in task)
        # Actually, let's just use manual list of pairs
        # For simplicity, I'll just use a smarter approach for bodies
        pass

    except Exception as e:
        print(f"Error restoring {path}: {e}")

# Manual Refactoring Restoration (Most Reliable)
def fix_trailing():
    path = 'src/V12_002.Trailing.cs'
    base = subprocess.check_output(['git', 'show', 'main:' + path], encoding='utf-8')
    base = base.replace('\ufeff', '').replace('\r\n', '\n')
    with open(path, 'r', encoding='utf-8') as f:
        refactored = f.read()

    # Extract ALL new helpers
    helpers = re.findall(r'private (?:void|bool) ManageTrail_\w+.*?\}', refactored, re.DOTALL)
    # Filter only the full methods (crude but effective)
    helpers_str = ""
    for h in helpers:
        if h.count('{') == h.count('}'):
            helpers_str += "\n\n        " + h

    # Replace ManageTrailingStops body
    m_match = re.search(r'private void ManageTrailingStops\(.*?\)\s*\{', refactored)
    if m_match:
        start = m_match.start()
        # Find matching brace
        brace_count = 0
        end = -1
        for i in range(start, len(refactored)):
            if refactored[i] == '{': brace_count += 1
            elif refactored[i] == '}':
                brace_count -= 1
                if brace_count == 0:
                    end = i + 1
                    break
        if end != -1:
            ref_method = refactored[start:end]
            # Replace in base
            base_match = re.search(r'private void ManageTrailingStops\(.*?\)\s*\{', base)
            if base_match:
                bs = base_match.start()
                bc = 0
                be = -1
                for j in range(bs, len(base)):
                    if base[j] == '{': bc += 1
                    elif base[j] == '}':
                        bc -= 1
                        if bc == 0:
                            be = j + 1
                            break
                if be != -1:
                    base = base[:bs] + ref_method + base[be:]

    # Inject helpers before last class closing
    last_brace = base.rfind('}')
    second_to_last = base[:last_brace].rfind('}')
    base = base[:second_to_last] + helpers_str + "\n" + base[second_to_last:]

    # Fixes
    base = base.replace('? MANUAL BREAKEVEN', '(!) MANUAL BREAKEVEN')
    base = re.sub(
        r'if\s*\(pos\.Direction\s*==\s*MarketPosition\.Long\)\s+pos\.ExtremePriceSinceEntry\s*=\s*Math\.Max\(pos\.ExtremePriceSinceEntry,\s*Close\[0\]\);\s+else\s+pos\.ExtremePriceSinceEntry\s*=\s*Math\.Min\(pos\.ExtremePriceSinceEntry,\s*Close\[0\]\);',
        'pos.ExtremePriceSinceEntry = pos.Direction == MarketPosition.Long ? Math.Max(pos.ExtremePriceSinceEntry, Close[0]) : Math.Min(pos.ExtremePriceSinceEntry, Close[0]);',
        base
    )
    
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(base)
    print("Trailing.cs Hardened.")

def fix_execution():
    path = 'src/V12_002.Orders.Callbacks.Execution.cs'
    base = subprocess.check_output(['git', 'show', 'main:' + path], encoding='utf-8')
    base = base.replace('\ufeff', '').replace('\r\n', '\n')
    with open(path, 'r', encoding='utf-8') as f:
        refactored = f.read()

    # Extract all ProcessOnExecution_ helpers
    helpers = re.findall(r'private (?:void|bool|string) ProcessOnExecution_\w+.*?\}', refactored, re.DOTALL)
    helpers_str = ""
    for h in helpers:
        if h.count('{') == h.count('}'):
            helpers_str += "\n\n        " + h

    # Replace ProcessOnExecutionUpdate body
    m_match = re.search(r'private void ProcessOnExecutionUpdate\(.*?\)\s*\{', refactored)
    if m_match:
        start = m_match.start()
        bc = 0
        end = -1
        for i in range(start, len(refactored)):
            if refactored[i] == '{': bc += 1
            elif refactored[i] == '}':
                bc -= 1
                if bc == 0:
                    end = i + 1
                    break
        if end != -1:
            ref_method = refactored[start:end]
            base_match = re.search(r'private void ProcessOnExecutionUpdate\(.*?\)\s*\{', base)
            if base_match:
                bs = base_match.start()
                bc2 = 0
                be = -1
                for j in range(bs, len(base)):
                    if base[j] == '{': bc2 += 1
                    elif base[j] == '}':
                        bc2 -= 1
                        if bc2 == 0:
                            be = j + 1
                            break
                if be != -1:
                    base = base[:bs] + ref_method + base[be:]

    # Inject helpers
    last_brace = base.rfind('}')
    second_to_last = base[:last_brace].rfind('}')
    base = base[:second_to_last] + helpers_str + "\n" + base[second_to_last:]

    # Hygiene
    base = base.replace('pos.ExecutingAccount != null && pos.ExecutingAccount.Name == flatAcctName', 'pos.ExecutingAccount?.Name == flatAcctName')
    base = base.replace('kvp.Value.ExecutingAccount != null && kvp.Value.ExecutingAccount.Name == flatAcctName', 'kvp.Value.ExecutingAccount?.Name == flatAcctName')

    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(base)
    print("Execution.cs Hardened.")

fix_trailing()
fix_execution()
