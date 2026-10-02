"""
Full fix for apex-trading-sop.html:
1. Decode the double-UTF-8 mojibake back to proper Unicode
2. Replace all non-ASCII Unicode with safe HTML entities
3. Strip corrupt \x90 bytes
"""
import re

path = r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\apex-trading-sop.html"

# Read as Latin-1 to get the raw bytes faithfully
with open(path, encoding='latin-1') as f:
    raw = f.read()

# Fix double-UTF-8: encode back to bytes as latin-1, then decode as utf-8
# Do this iteratively until stable
def fix_mojibake(text):
    try:
        fixed = text.encode('latin-1').decode('utf-8')
        return fixed
    except:
        return text

# Apply mojibake fix to the whole file
# We need to do this carefully - only fix sequences that are valid UTF-8
def fix_text(text):
    # Split on ASCII boundaries and fix non-ASCII runs
    result = []
    i = 0
    while i < len(text):
        if ord(text[i]) < 128:
            result.append(text[i])
            i += 1
        else:
            # Collect non-ASCII run
            j = i
            while j < len(text) and ord(text[j]) >= 128:
                j += 1
            chunk = text[i:j]
            try:
                fixed = chunk.encode('latin-1').decode('utf-8')
                result.append(fixed)
            except:
                # Can't fix - strip it
                result.append('')
            i = j
    return ''.join(result)

text = fix_text(raw)

# Strip any remaining \x90 garbage (corrupted box drawing)
text = re.sub(r'\x90+', '', text)
text = re.sub(r'\xad', '', text)

# Now replace all Unicode with HTML entities
replacements = [
    ('\u2014', '&mdash;'),
    ('\u2013', '&ndash;'),
    ('\u2212', '&minus;'),
    ('\u2192', '&rarr;'),
    ('\u2190', '&larr;'),
    ('\u00d7', '&times;'),
    ('\u00f7', '&divide;'),
    ('\u2265', '&ge;'),
    ('\u2264', '&le;'),
    ('\u2248', '&asymp;'),
    ('\u2260', '&ne;'),
    ('\u00b7', '&middot;'),
    ('\u00b1', '&plusmn;'),
    ('\u2022', '&bull;'),
    ('\u2605', '&#9733;'),      # ★
    ('\u2606', '&#9734;'),      # ☆
    ('\u2713', '&#10003;'),     # ✓
    ('\u2714', '&#10004;'),     # ✔
    ('\u2705', '&#9989;'),      # ✅
    ('\u274c', '&#10060;'),     # ❌
    ('\u2716', '&#10006;'),     # ✖
    ('\u26a1', '&#9889;'),      # ⚡
    ('\u2b21', '&#11041;'),     # ⬡
    ('\u25b6', '&#9654;'),      # ▶
    ('\u23f1', '&#9201;'),      # ⏱
    ('\u00a0', '&nbsp;'),
    ('\u2018', '&lsquo;'),
    ('\u2019', '&rsquo;'),
    ('\u201c', '&ldquo;'),
    ('\u201d', '&rdquo;'),
    # Box drawing -> ASCII
    ('\u2500', '-'), ('\u2502', '|'), ('\u2550', '='),
    ('\u2551', '|'), ('\u250c', '+'), ('\u2510', '+'),
    ('\u2514', '+'), ('\u2518', '+'), ('\u251c', '+'),
    ('\u2524', '+'), ('\u252c', '+'), ('\u2534', '+'),
    ('\u253c', '+'), ('\u2554', '+'), ('\u2557', '+'),
    ('\u255a', '+'), ('\u255d', '+'), ('\u2560', '+'),
    ('\u2563', '+'), ('\u2566', '+'), ('\u2569', '+'),
    ('\u256c', '+'),
    # Emoji
    ('\U0001f4f7', '[img]'),    # 📷
    # Latin extended
    ('\u00e2', 'a'), ('\u00e9', 'e'), ('\u00e8', 'e'),
    ('\u00c2', ''), ('\u00c3', ''),
    # Misc
    ('\u00b2', '&sup2;'), ('\u00b3', '&sup3;'),
    ('\u00b0', '&deg;'),
    ('\u00bc', '&frac14;'), ('\u00bd', '&frac12;'), ('\u00be', '&frac34;'),
]

for char, entity in replacements:
    text = text.replace(char, entity)

# Catch anything remaining above U+00FF
def replace_high(m):
    return f'&#{ord(m.group(0))};'
text = re.sub(r'[^\x00-\xFF]', replace_high, text)

# Also catch latin-1 range \x80-\xFF that aren't in HTML entities already
# These might be leftover mojibake fragments - strip them
def clean_latin(m):
    c = m.group(0)
    code = ord(c)
    # Known safe latin-1 HTML entities
    safe = {0xa9: '&copy;', 0xae: '&reg;', 0xb0: '&deg;',
            0xb1: '&plusmn;', 0xb7: '&middot;', 0xd7: '&times;', 0xf7: '&divide;'}
    if code in safe:
        return safe[code]
    # Everything else in \x80-\xFF that's not a known safe char: strip it
    return ''

text = re.sub(r'[\x80-\xFF]', clean_latin, text)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(text)

# Verify
remaining_non_ascii = re.findall(r'[^\x00-\x7F]', text)
print(f"Done. File length: {len(text)} chars")
print(f"Remaining non-ASCII (> 0x7F): {len(remaining_non_ascii)}")
if remaining_non_ascii[:20]:
    from collections import Counter
    print(Counter(remaining_non_ascii).most_common(10))
