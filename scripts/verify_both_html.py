"""Final verification of both HTML files"""
import re, sys
sys.stdout.reconfigure(encoding='utf-8')

files = {
    'apex-trading-sop.html': r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\apex-trading-sop.html",
    'mes-atm-grid.html':     r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\mes-atm-grid.html",
}

for name, path in files.items():
    text = open(path, encoding='utf-8').read()
    print(f"\n{'='*60}")
    print(f"FILE: {name}")
    print(f"{'='*60}")

    # 1. Non-ASCII check
    non_ascii = re.findall(r'[^\x00-\x7F]', text)
    print(f"Non-ASCII chars: {len(non_ascii)} {'OK' if not non_ascii else 'FAIL'}")

    # 2. Mojibake remnants
    for pattern, label in [
        (r'&#8364;',         'euro entity (mojibake)'),
        (r'a&rdquo;',        'a+rdquo (mojibake)'),
        (r'Ã',               'latin mojibake Ã'),
        (r'â€',              'classic mojibake â€'),
    ]:
        count = len(re.findall(pattern, text))
        status = 'OK' if count == 0 else f'WARN ({count}x)'
        print(f"  {label}: {status}")

    # 3. Key HTML entities present
    present = [(e, text.count(e)) for e in ['&mdash;','&middot;','&ndash;','&rarr;','&ge;','&le;','&bull;','&times;','&divide;']]
    print("  Key entities:", ', '.join(f"{e}:{n}" for e,n in present if n > 0))

    # 4. Spot-check headings
    for marker, ctx_len in [('Iron Rule', 80), ('S-15', 100), ('2-Target', 60)]:
        idx = text.find(marker)
        if idx >= 0:
            snippet = text[idx:idx+ctx_len].replace('\n',' ')
            print(f"  [{marker}]: {snippet[:80]}")
