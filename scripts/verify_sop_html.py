"""Verify apex-trading-sop.html is clean after encoding fix"""
import sys, re
sys.stdout.reconfigure(encoding='utf-8')

path = r"C:\Users\Mohammed Khalid\AppData\Local\Temp\bob-artifacts\apex-trading-sop.html"
text = open(path, encoding='utf-8').read()

# 1. No non-ASCII above 0x7F
high = re.findall(r'[^\x00-\x7F]', text)
print(f"Non-ASCII chars (>0x7F): {len(high)}")

# 2. Key entities present
for entity, label in [
    ('&mdash;', 'em-dash'),
    ('&middot;', 'middle-dot'),
    ('&rarr;', 'arrow'),
    ('&ge;', 'greater-equal'),
    ('&bull;', 'bullet'),
    ('&divide;', 'divide'),
    ('&times;', 'times'),
    ('&minus;', 'minus'),
]:
    count = text.count(entity)
    print(f"  {entity} ({label}): {count}")

# 3. Show S-15 heading
idx = text.rfind('S-15')
print("\nS-15 context (first 200 chars):")
print(text[idx:idx+200])

# 4. Show Iron Rule
idx2 = text.find('Iron Rule')
print("\nIron Rule context:")
print(text[idx2:idx2+120])

# 5. Show a section comment line
for line in text.split('\n'):
    if 'S-12' in line and '<!--' in line:
        print("\nS-12 comment:", line)
        break

# 6. Check for remaining mojibake patterns
mojibake = re.findall(r'&amp;[a-z]+;', text)
if mojibake:
    print(f"\nWARN: double-encoded entities found: {set(mojibake[:5])}")
else:
    print("\nNo double-encoded entities found.")
