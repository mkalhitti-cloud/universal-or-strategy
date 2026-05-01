import os
import sys
import glob

def check_file(f):
    if not os.path.exists(f):
        print(f"{f} not found")
        return True # Not a violation if missing
    
    with open(f, 'rb') as fh:
        content = fh.read()
        non_ascii = [(i, b) for i, b in enumerate(content) if b > 127]
        if non_ascii:
            print(f"FAILED: {f} - Found {len(non_ascii)} non-ASCII bytes")
            for pos, b in non_ascii[:5]:
                # Try to show context
                start = max(0, pos - 10)
                end = min(len(content), pos + 10)
                context = content[start:end]
                print(f"  Pos {pos}: 0x{b:02X} | Context: {context}")
            return False
        else:
            print(f"PASS: {f} - All bytes are ASCII")
            return True

def main():
    files = sys.argv[1:]
    if not files:
        # Default to all C# files in src
        files = glob.glob('src/**/*.cs', recursive=True)
    
    all_pass = True
    for f in files:
        if not check_file(f):
            all_pass = False
    
    if not all_pass:
        sys.exit(1)

if __name__ == "__main__":
    main()

# CI Trigger - V12.1.2
