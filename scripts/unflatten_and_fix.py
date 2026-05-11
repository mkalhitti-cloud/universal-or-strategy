import os
import re

def unflatten_and_fix(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Basic unflattening
    content = content.replace('\ufeff', '')
    content = content.replace('\r\n', '\n')
    
    # If it's effectively one line (very few newlines)
    if content.count('\n') < 10:
        print(f"Unflattening {path}...")
        # Add newlines after common tokens
        content = content.replace('using ', '\nusing ')
        content = content.replace('namespace ', '\nnamespace ')
        content = content.replace('public ', '\npublic ')
        content = content.replace('private ', '\nprivate ')
        content = content.replace('{', '{\n')
        content = content.replace('}', '\n}\n')
        content = content.replace(';', ';\n')
        content = content.replace('///', '\n///')
        # Clean up excessive newlines
        content = re.sub(r'\n\s*\n', '\n', content)

    # Apply Hygiene Fixes
    if 'Trailing.cs' in path:
        # Ternary for extreme price
        content = re.sub(
            r'if\s*\(pos\.Direction\s*==\s*MarketPosition\.Long\)\s+pos\.ExtremePriceSinceEntry\s*=\s*Math\.Max\(pos\.ExtremePriceSinceEntry,\s*Close\[0\]\);\s+else\s+pos\.ExtremePriceSinceEntry\s*=\s*Math\.Min\(pos\.ExtremePriceSinceEntry,\s*Close\[0\]\);',
            'pos.ExtremePriceSinceEntry = pos.Direction == MarketPosition.Long ? Math.Max(pos.ExtremePriceSinceEntry, Close[0]) : Math.Min(pos.ExtremePriceSinceEntry, Close[0]);',
            content
        )
        # Restore ASCII marker
        content = content.replace('? MANUAL BREAKEVEN', '(!) MANUAL BREAKEVEN')
    
    if 'SIMA.Dispatch.cs' in path:
        # Remove unused variable
        content = content.replace('bool useRmaForFollower = true;', '')
        # Fix UTC Ticks
        content = content.replace('DateTime.Now.Ticks', 'DateTime.UtcNow.Ticks')

    if 'Execution.cs' in path:
        # Nullable check simplification
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

    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(content)
    print(f"Processed: {path}")

files = ['src/V12_002.Trailing.cs', 'src/V12_002.SIMA.Dispatch.cs', 'src/V12_002.Orders.Callbacks.Execution.cs']
for f_path in files:
    unflatten_and_fix(f_path)
