"""Patch remaining &#8364; mojibake fragments in apex-trading-sop.html"""
import re, sys
sys.stdout.reconfigure(encoding='utf-8')

path = r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\apex-trading-sop.html"

with open(path, encoding='utf-8') as f:
    text = f.read()

# The remaining pattern is the 2nd/3rd em-dash in sequences like ── or ———
# After first-pass fix: &mdash; + a&rdquo;&#8364; + a&rdquo; + ...
# The fragment pattern is: (a&rdquo;&#8364;) = bytes E2 80 9D E2 82 AC = tail of prev + start of next em-dash

# Pattern: &rdquo;&#8364;a&rdquo; -> &mdash;&mdash;
# But we need to be surgical. Let's just iteratively replace the fragment
# until no more instances

before = len(re.findall(r'&#8364;', text))
print(f"&#8364; instances before: {before}")

# Replace the fragment patterns
patterns = [
    (r'a&rdquo;&#8364;(?=a&rdquo;)', '&mdash;'),  # middle of sequence
    (r'a&rdquo;&#8364;(?= )',        '&mdash; '),  # end before space
    (r'a&rdquo;&#8364;(?=<)',        '&mdash;'),   # end before tag
    (r'a&rdquo;&#8364;(?=\n)',       '&mdash;'),   # end before newline
    (r'a&rdquo;&#8364;(?=A)',        '&mdash;'),   # before 'A' (ATM, ATR etc)
    (r'a&rdquo;&#8364;(?=I)',        '&mdash;'),   # before 'I' (Image etc)
    (r'a&rdquo;&#8364;(?=F)',        '&mdash;'),   # before 'F'
    (r'a&rdquo;&#8364;(?=N)',        '&mdash;'),   # before 'N'
    (r'a&rdquo;&#8364;(?=M)',        '&mdash;'),   # before 'M'
    (r'a&rdquo;&#8364;(?=W)',        '&mdash;'),   # before 'W'
    (r'a&rdquo;&#8364;(?=T)',        '&mdash;'),   # before 'T'
    (r'a&rdquo;&#8364;(?=P)',        '&mdash;'),   # before 'P'
    (r'a&rdquo;&#8364;(?=-)',        '&mdash;-'),  # before '-'
    (r'a&rdquo;&#8364;(?=S)',        '&mdash;'),   # before 'S'
    (r'a&rdquo;&#8364;',             '&mdash;'),   # catch-all remaining
]

for pat, repl in patterns:
    n = len(re.findall(pat, text))
    if n:
        text = re.sub(pat, repl, text)
        print(f"  Fixed {repr(pat[:30])} -> {repr(repl)}: {n}x")

# Also fix lone &#8364; that appear after &mdash; (leftover middle byte)
text = re.sub(r'(&mdash;)+&#8364;', '&mdash;', text)
text = re.sub(r'&#8364;(&mdash;)+', '&mdash;', text)
text = re.sub(r'&#8364;\s*(?=[-=A-Z\s<])', ' ', text)  # isolated in dash sequences
text = re.sub(r'&#8364;', '', text)  # strip any remaining

after = len(re.findall(r'&#8364;', text))
print(f"\n&#8364; instances after: {after}")

# Fix: tree-step lines have ── which became a sequence of &mdash; - clean to single ---
text = re.sub(r'(&mdash;){3,}', '&mdash;&mdash;&mdash;', text)

# Verify
non_ascii = re.findall(r'[^\x00-\x7F]', text)
print(f"Non-ASCII remaining: {len(non_ascii)}")

# Quick sanity: show S-15 and Iron Rule headings
for marker in ['S-15</span>', 'Iron Rule']:
    idx = text.rfind(marker) if marker == 'S-15</span>' else text.find(marker)
    if idx >= 0:
        print(f"\n{marker}: {repr(text[idx:idx+100])}")

with open(path, 'w', encoding='utf-8', newline='') as f:
    f.write(text)
print("\nFile written.")
