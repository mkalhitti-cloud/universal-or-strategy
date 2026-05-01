#!/usr/bin/env python3
from pathlib import Path
import sys
root=Path(sys.argv[1] if len(sys.argv)>1 else 'src')
bad=[]
for p in root.rglob('*.cs'):
    t=p.read_bytes()
    for i,line in enumerate(t.splitlines(),1):
        if any(b>127 for b in line):
            bad.append((p,i))
            break
if bad:
    for p,i in bad:
        print(f'NON_ASCII {p}:{i}')
    sys.exit(1)
print('ASCII_OK')
