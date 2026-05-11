import subprocess
import re
import os

def fix_sima_dispatch():
    path = 'src/V12_002.SIMA.Dispatch.cs'
    # 1. Get Main baseline
    base = subprocess.check_output(['git', 'show', 'main:' + path], encoding='utf-8')
    # Normalize base to LF
    base = base.replace('\ufeff', '').replace('\r\n', '\n')

    # 2. Get the refactored version (HEAD)
    with open(path, 'r', encoding='utf-8') as f:
        refactored = f.read()
    
    # 3. Extract the new helpers from the refactored version
    helpers = ""
    for h in ['Dispatch_ResolveFleetSnapshot', 'Dispatch_BuildFollowerOrders', 'Dispatch_PublishMarketBracketToPhoton', 'Dispatch_PublishLimitEntryToPhoton']:
        # Find method start
        pattern = r'private void ' + h + r'\(.*?\)\s*\{'
        match = re.search(pattern, refactored)
        if match:
            # Find matching brace
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

    # 4. Extract the refactored ExecuteSmartDispatchEntry body
    match_exec = re.search(r'private void ExecuteSmartDispatchEntry\(.*?\)\s*\{', refactored)
    exec_body = ""
    if match_exec:
        start = match_exec.start()
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
            # Get just the content between braces
            full_method = refactored[start:end]
            inner_start = full_method.find('{') + 1
            inner_end = full_method.rfind('}')
            exec_body = full_method[inner_start:inner_end]

    # 5. Inject into baseline
    # Replace ExecuteSmartDispatchEntry body in baseline
    base_match = re.search(r'private void ExecuteSmartDispatchEntry\(.*?\)\s*\{', base)
    if base_match:
        b_start = base_match.start()
        brace_count = 0
        b_end = -1
        for i in range(b_start, len(base)):
            if base[i] == '{': brace_count += 1
            elif base[i] == '}':
                brace_count -= 1
                if brace_count == 0:
                    b_end = i + 1
                    break
        if b_end != -1:
            # Replace the whole method
            new_exec = base[b_start:base[b_start:].find('{')+b_start+1] + exec_body + "        }"
            base = base[:b_start] + new_exec + base[b_end:]

    # 6. Inject helpers before the last two braces (End of class)
    # Actually, NinjaTrader strategies often end with #endregion or just }}
    last_brace = base.rfind('}')
    second_to_last = base[:last_brace].rfind('}')
    base = base[:second_to_last] + helpers + "\n" + base[second_to_last:]

    # 7. Apply hygiene and logic fixes
    base = base.replace('DateTime.Now.Ticks', 'DateTime.UtcNow.Ticks')
    base = base.replace('bool useRmaForFollower = true;', '')
    
    # 8. Write back
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(base)
    print("Sima.Dispatch.cs Hardened and Cleaned.")

fix_sima_dispatch()
