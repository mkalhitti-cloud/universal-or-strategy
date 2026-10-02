"""
Final targeted fix: replace known mojibake sequences and HTML entity garbles
with correct HTML entities. Works on the already-processed file.
"""
import re, sys
sys.stdout.reconfigure(encoding='utf-8')

path = r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\apex-trading-sop.html"

with open(path, encoding='utf-8') as f:
    text = f.read()

# These are the specific garbled forms that survived:
# a&#8364;&rdquo; = â + € + " = original em-dash â€" (UTF-8 E2 80 94 read as latin-1)
# a&#8364;&lsquo; = â + € + ' = en-dash variation
# a&#8364;&ndash; etc.

direct_fixes = [
    # Em-dash variants (â€" read as latin-1 then partially entity-encoded)
    ('a&#8364;&rdquo;',     '&mdash;'),
    ('a&#8364;&ldquo;',     '&mdash;'),
    # En-dash (â€" with different trailing)
    ('a&#8364;&ndash;',     '&ndash;'),
    # Right arrow â†' = E2 86 92
    ('a&#8224;&rsquo;',     '&rarr;'),
    # Bullet â€¢ = E2 80 A2
    ('a&#8364;&#162;',      '&bull;'),
    # Minus sign â€" lookalike
    ('a&#8364;&#8220;',     '&mdash;'),
    # Non-breaking space Â\xa0
    ('&#194;&nbsp;',        '&nbsp;'),
    # Multiply sign Ã—
    ('&#195;&#215;',        '&times;'),
    # Division sign Ã·
    ('&#195;&#247;',        '&divide;'),
    # Degree Â°
    ('&#194;&#176;',        '&deg;'),
    # Middle dot Â·
    ('&#194;&middot;',      '&middot;'),
    # Greater-equal â‰¥
    ('a&#8240;&yen;',       '&ge;'),
    # Less-equal â‰¤
    ('a&#8240;&#164;',      '&le;'),
    # Approx â‰ˆ
    ('a&#8240;&#136;',      '&asymp;'),
    # Arrow variations
    ('&#226;&#134;&#146;',  '&rarr;'),
    # Generic: any remaining a&#8364; pattern is probably an em-dash
    # (catch-all for remaining instances)
]

before = text
for old, new in direct_fixes:
    count = text.count(old)
    if count:
        print(f"  Replacing {repr(old)} -> {repr(new)} ({count}x)")
        text = text.replace(old, new)

# After direct fixes, check for remaining &#8364; (€ sign in garbled context)
# &#8364; alone in text (not in price like $400) is almost always a garbled em-dash
remaining_euro = re.findall(r'[a-z]&#8364;.{0,10}', text)
if remaining_euro:
    print(f"\nRemaining &#8364; patterns: {set(remaining_euro[:10])}")

# Final entity sweep - any &lsquo; &rsquo; &ldquo; &rdquo; surrounded by word chars
# that came from mojibake should be cleaned (they're already good HTML entities)
# Just verify the file is all ASCII now
non_ascii = re.findall(r'[^\x00-\x7F]', text)
print(f"\nNon-ASCII remaining: {len(non_ascii)}")

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(text)

# Final content check
print("\nKey patterns in fixed file:")
for label, pat in [
    ('mdash', '&mdash;'),
    ('rarr', '&rarr;'),
    ('middot', '&middot;'),
    ('S-15 heading', 'Live ATM &amp; Stop Strategy Reference &mdash; 2-Target Design'),
    ('Iron Rule', 'Iron Rule &mdash; Non-Negotiable'),
    ('Quantity split', 'T1 = T2'),
    ('euro leftover', '&#8364;'),
]:
    count = text.count(pat)
    print(f"  {label}: {count}")
