"""
Fix all non-ASCII and garbled characters in apex-trading-sop.html.
Replaces Unicode characters with safe HTML entities or ASCII equivalents.
"""
import re, sys

path = r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\apex-trading-sop.html"

with open(path, encoding='utf-8') as f:
    text = f.read()

original_len = len(text)

# ── 1. Strip any \x90 garbage bytes (corrupted box-drawing chars) ──────────
# These appear in HTML comments like <!-- ══ S-12 ══ -->
# Replace entire lines containing \x90 with clean ASCII comment equivalents
def fix_section_comment(m):
    line = m.group(0)
    # Extract section number if present
    sn = re.search(r'S-\d+', line)
    if sn:
        return f'  <!-- == {sn.group()} == -->'
    return '  <!-- ============================================================ -->'

# Fix the long ══════ style comments (all \x90 runs)
text = re.sub(r'[ \t]*<!--[^\n]*\x90[^\n]*-->', lambda m: fix_section_comment(m), text)

# Fix any remaining isolated \x90 sequences (e.g. in table cells)
text = re.sub(r'\x90+', '', text)
# Fix \xad (soft hyphen artifacts)
text = re.sub(r'\xad', '', text)

# ── 2. Replace Unicode characters with HTML entities ──────────────────────
replacements = [
    # Em-dash and en-dash
    ('\u2014', '&mdash;'),      # —
    ('\u2013', '&ndash;'),      # –
    # Arrows
    ('\u2192', '&rarr;'),       # →
    ('\u2190', '&larr;'),       # ←
    ('\u2194', '&harr;'),       # ↔
    # Math symbols
    ('\u00d7', '&times;'),      # ×
    ('\u00f7', '&divide;'),     # ÷
    ('\u2265', '&ge;'),         # ≥
    ('\u2264', '&le;'),         # ≤
    ('\u2248', '&asymp;'),      # ≈
    ('\u2260', '&ne;'),         # ≠
    ('\u2212', '&minus;'),      # − (minus sign)
    ('\u00b1', '&plusmn;'),     # ±
    # Bullets and markers
    ('\u2022', '&bull;'),       # •
    ('\u2605', '&#9733;'),      # ★
    ('\u2606', '&#9734;'),      # ☆
    ('\u2713', '&#10003;'),     # ✓
    ('\u2714', '&#10004;'),     # ✔
    ('\u274c', '&#10060;'),     # ❌
    ('\u2716', '&#10006;'),     # ✖
    ('\u26a1', '&#9889;'),      # ⚡ (keep as is, widely supported)
    ('\u2b21', '&#11041;'),     # ⬡
    # Middle dot / interpunct
    ('\u00b7', '&middot;'),     # ·
    ('\u22c5', '&sdot;'),       # ⋅
    # Spaces
    ('\u00a0', '&nbsp;'),       # non-breaking space
    # Quotes
    ('\u2018', '&lsquo;'),      # '
    ('\u2019', '&rsquo;'),      # '
    ('\u201c', '&ldquo;'),      # "
    ('\u201d', '&rdquo;'),      # "
    # Box drawing (any remaining) -> ASCII
    ('\u2500', '-'),            # ─
    ('\u2502', '|'),            # │
    ('\u250c', '+'),            # ┌
    ('\u2510', '+'),            # ┐
    ('\u2514', '+'),            # └
    ('\u2518', '+'),            # ┘
    ('\u251c', '+'),            # ├
    ('\u2524', '+'),            # ┤
    ('\u252c', '+'),            # ┬
    ('\u2534', '+'),            # ┴
    ('\u253c', '+'),            # ┼
    ('\u2550', '='),            # ═
    ('\u2551', '|'),            # ║
    ('\u2554', '+'),            # ╔
    ('\u2557', '+'),            # ╗
    ('\u255a', '+'),            # ╚
    ('\u255d', '+'),            # ╝
    ('\u2560', '+'),            # ╠
    ('\u2563', '+'),            # ╣
    ('\u2566', '+'),            # ╦
    ('\u2569', '+'),            # ╩
    ('\u256c', '+'),            # ╬
    # Camera emoji (in MCL section)
    ('\U0001f4f7', '[img]'),    # 📷
    # Clock (used in BE badge)
    ('\u23f1', '&#9201;'),      # ⏱
    # Checkmark badges
    ('\u2705', '&#9989;'),      # ✅
    ('\u274e', '&#10062;'),     # ❎
]

for char, entity in replacements:
    text = text.replace(char, entity)

# ── 3. Catch any remaining non-ASCII chars > U+00FF ───────────────────────
def replace_high_unicode(m):
    c = m.group(0)
    return f'&#{ord(c)};'

text = re.sub(r'[^\x00-\xFF]', replace_high_unicode, text)

# ── 4. Verify charset meta is present ─────────────────────────────────────
if '<meta charset="UTF-8"' not in text and "<meta charset='UTF-8'" not in text:
    text = text.replace('<head>', '<head>\n<meta charset="UTF-8" />', 1)

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(text)

print(f"Done. Original: {original_len} chars, Fixed: {len(text)} chars")

# Report remaining non-ASCII
remaining = re.findall(r'[^\x00-\xFF]', text)
if remaining:
    print(f"WARNING: {len(remaining)} non-ASCII chars remain: {set(remaining)}")
else:
    print("All non-ASCII chars resolved.")
