import os

files = [
    'src/V12_002.REAPER.cs',
    'src/V12_002.SIMA.cs',
    'src/V12_002.cs',
    'src/V12_002.Lifecycle.cs',
    'src/V12_002.Orders.Callbacks.cs',
    'src/V12_002.SIMA.Lifecycle.cs',
    'src/V12_002.SIMA.Flatten.cs',
    'src/V12_002.UI.IPC.Commands.Fleet.cs',
    'src/V12_002.Orders.Callbacks.Execution.cs'
]

for f in files:
    if not os.path.exists(f):
        print(f"{f} not found")
        continue
    with open(f, 'rb') as fh:
        content = fh.read()
        non_ascii = [(i, b) for i, b in enumerate(content) if b > 127]
        if non_ascii:
            print(f"{f}: Found {len(non_ascii)} non-ASCII bytes")
            for pos, b in non_ascii[:10]:
                print(f"  Pos {pos}: 0x{b:02X}")
        else:
            print(f"{f}: All bytes are ASCII (0-127)")
