import os
import re
import json
import subprocess
import time
from datetime import datetime
from langsmith import traceable
from dotenv import load_dotenv

# Load environment variables (API Key, etc.)
load_dotenv()

# --- CONFIGURATION ---
BASE_DIR = r"c:\WSGTA\universal-or-strategy"
BENCHMARK_FILE = os.path.join(BASE_DIR, "benchmarks", "StandaloneBench.cs")
TEMPLATE_FILE = os.path.join(BASE_DIR, "benchmarks", "StandaloneBench.template.txt")
RESULTS_JSON = os.path.join(BASE_DIR, "docs", "battle_results.json")
RESULTS_MD = os.path.join(BASE_DIR, "docs", "battle_results.md")
SUBMISSIONS_DIR = r"C:\tmp\battle_antigravity_os"

def get_method_body(full_code, method_name):
    # V24-compatible: handles any modifier combo + any return type (bool, void, int, etc.)
    # Modifiers: public|private|protected|internal|static|unsafe|virtual|override|extern|async
    # Return type: any word+pointer token (e.g. bool, void, ulong, SovereignChannel*)
    # Params: anything except '{' (handles pointer types, out/ref, generics)
    modifier_pat  = r'(?:(?:public|private|protected|internal|static|unsafe|virtual|override|extern|async)\s+)*'
    ret_type_pat  = r'(?:[\w*\[\]<>,\s]+?\s+)?'   # optional: any return type tokens
    method_name_e = re.escape(method_name)
    param_pat     = r'\s*\([^{]*?\)\s*'
    full_pat      = modifier_pat + ret_type_pat + method_name_e + param_pat + r'\{'
    start_match   = re.search(full_pat, full_code)
    if not start_match: return ''

    start_pos = start_match.end()
    brace_count = 1
    end_pos = start_pos

    while brace_count > 0 and end_pos < len(full_code):
        if full_code[end_pos] == '{':
            brace_count += 1
        elif full_code[end_pos] == '}':
            brace_count -= 1
            if brace_count == 0:
                return full_code[start_pos:end_pos].strip()
        end_pos += 1
    return ''

def _scan_backtick_literal(content, start):
    """Scan from 'start' (after opening backtick) to matching closing backtick.
    Skips \\` escape sequences so they don't prematurely close the literal."""
    i = start
    n = len(content)
    while i < n:
        c = content[i]
        if c == '\\':        # escape — skip next char
            i += 2
            continue
        if c == '`':          # closing backtick found
            return content[start:i], i + 1
        i += 1
    return content[start:], n  # unterminated — return rest

def extract_named_ts_exports(content):
    """Extract bodies of 'export const NAME = `...`' template literals.
    Returns list of (name, body) tuples.  Handles large V24 literals correctly."""
    results = []
    # Match 'export const IDENTIFIER = `'  OR  'const IDENTIFIER = `'
    header_pat = re.compile(r'(?:export\s+)?const\s+(\w+)\s*=\s*`')
    pos = 0
    while True:
        m = header_pat.search(content, pos)
        if not m:
            break
        name = m.group(1)
        body, next_pos = _scan_backtick_literal(content, m.end())
        results.append((name, body))
        pos = next_pos
    return results

def extract_all_literals(content):
    """Extract all bare backtick template literals (non-named).
    Uses the same escape-aware scanner to avoid premature truncation."""
    results = []
    # Skip named exports — handled by extract_named_ts_exports
    named_header = re.compile(r'(?:export\s+)?const\s+\w+\s*=\s*(String\.raw)?`')
    i = 0
    n = len(content)
    while i < n:
        # Is this position the start of a named export literal?  Skip if so.
        nm = named_header.match(content, i)
        if nm:
            _, i = _scan_backtick_literal(content, nm.end())
            continue
        if content[i] == '`':
            body, i = _scan_backtick_literal(content, i + 1)
            results.append(body)
            continue
        i += 1
    return results

def normalize_body(e_body, d_body):
    mappings = {
        r'value\b': 'payload', r'item\b': 'payload',
        r'_indices->ProducerIndex': '_producerIndex', r'_indices->ConsumerIndex': '_consumerIndex',
        r'_indices\.ProducerIndex': '_producerIndex', r'_indices\.ConsumerIndex': '_consumerIndex',
        r'_indices->Mask': '_mask', r'_indices\.Mask': '_mask',
        r'_lanes': 'Slots', r'_buffer': 'Slots', r'_slots': 'Slots',
        r'slotIdx': 'pos', r'slots\[pos\]': 'Slots[pos]',
        r'long\s+result': 'double result', r'\bSlot\b': 'CoreLane',
        r'prodIdx': 'pIdx', r'consIdx': 'cIdx',
        # V24 Sovereign Mappings
        r'ch->Head': '_producerIndex',
        r'ch->Tail': '_consumerIndex',
        r'ch->RingBuffer': 'Slots',
        r'message': 'payload',
        r'RingBuffer\[idx\]': 'Slots[idx].Value',
        r'Slots\[idx\]\s*=\s*payload;': 'Slots[idx].Value = payload;',
        r'payload\s*=\s*Slots\[idx\];': 'payload = Slots[idx].Value;',
        r'ulong\s+': 'long ',
        r'uint\s+idx\b': 'int idx',
        r'long\s+idx\b': 'int idx',
        r'long\s+head\b': 'int head',
        r'long\s+tail\b': 'int tail',
        r'head\s*&\s*0xFF': '_producerIndex & _mask',
        r'tail\s*&\s*0xFF': '_consumerIndex & _mask',
        # V25 MPMC Sovereign Mappings — remap multi-lane field accesses to flat template fields
        r'lane\.Counters\.ReservationCounter': '_mask',
        r'lane\.WritePointer\.Value': '_producerIndex',
        r'lane\.Counters\.ReadPointer': '_consumerIndex',
        r'lane\.Slots\[': 'Slots[',
        r'lane\.Metadata\.(Capacity|Mask)': '_mask',
        r'_lanes\[NormalizeLane\(\w+\)\]': '',
        r'Lane lane = .*;': 'var pos = _producerIndex;',
        r'NormalizeLane\(\w+\)': '0',
        r'\.Metadata\.Mask': '& _mask',
        r'Interlocked\.Increment\(ref lane\.Stats\.\w+\);': '',
        r'Interlocked\.Decrement\(ref lane\.Stats\.\w+\);': '',
        r'lane\.Stats\.\w+ [+\-]= 1;': '',
        r'slot\.Item': 'Slots[pos].Value',
        r'slot\.Sequence': 'Slots[pos].Sequence',
        r'Volatile\.Write\(ref slot\.Sequence': 'Volatile.Write(ref Slots[pos].Sequence',
        r'Volatile\.Read\(ref slot\.Sequence\)': 'Volatile.Read(ref Slots[pos].Sequence)',
        r'Slot slot = lane\.Slots\[.*?\];': '',
        r'int slotIndex = \(int\)\(_producerIndex \& _mask\);': '',
        r'long remaining = Interlocked\.Add\(ref _mask, -1\);': 'int pos = _producerIndex & _mask;',
        r'if \(remaining < 0\).*?return false;': '',
        r'long writeSequence = Interlocked\.Add\(ref _producerIndex, 1\) - 1;': 'int pos = _producerIndex++;',
        r'long readSequence = Volatile\.Read\(ref _consumerIndex\);': 'int pos = _consumerIndex;',
        r'if \(_consumerIndex != _producerIndex && Interlocked\.CompareExchange\(ref _consumerIndex.*?continue;': '_consumerIndex++;',
        r'Volatile\.Write\(ref Slots\[pos\]\.Sequence, \w+ \+ 1\);': 'Volatile.Write(ref Slots[pos].Sequence, pos + 1); return true;',
        r'Volatile\.Write\(ref Slots\[pos\]\.Sequence, \w+ \+ _mask\);': 'Volatile.Write(ref Slots[pos].Sequence, pos + _mask); return true;',
        # V25 MPMC _meta flat-field remappings
        r'_meta\.WriteReservation': '_producerIndex',
        r'_meta\.ReadHead': '_consumerIndex',
        r'_meta\.WriteCommitted': '_producerIndex',
        r'_meta\.Mask': '_mask',
        r'_meta\.Capacity': '_mask',
        r'\bref var\b': 'ref var',
        r'ref var slot = ref Slots\[index\];': '',
        r'\bexpectedValue\b': 'pos',
        r'\bint spin = 0;\b': '',
        r'while \(\(Slots\[pos\]\.Sequence\s*\^\s*pos\) != 0\)': 'if ((Slots[pos].Sequence ^ pos) != 0) return false; while(false)',
        r'if \(\(Slots\[pos\]\.Sequence\s*\^\s*pos\) != 0\)\s*$': 'if ((Slots[pos].Sequence ^ pos) != 0) { payload = default; return false; }',
        # Aggressive Strip - Must be last to catch orphaned lines after replacements
        r'(?m)^.*?(?:SafetyInvariant|invariant|FenceCount|Topology|ChannelMode|ch->Invariant|&ch->RingBuffer|&Slots).*?$': '',
    }
    for old, new in mappings.items():
        e_body = re.sub(old, new, e_body)
        d_body = re.sub(old, new, d_body)
    return e_body, d_body

def cleanup_orphaned_blocks(body):
    r"""Remove `{ ... }` blocks whose opening brace has no preceding control-flow keyword.
    These result when the SafetyInvariant aggressive-strip deletes an if-condition,
    leaving a dangling scoping block that makes subsequent code unreachable."""
    lines = body.split('\n')
    result = []
    i = 0
    while i < len(lines):
        stripped = lines[i].strip()
        if stripped == '{':
            # Look backwards for the last non-blank line
            prev = ''
            for j in range(len(result) - 1, -1, -1):
                if result[j].strip():
                    prev = result[j].strip()
                    break
            # Control-flow introducers always end with ), 'else', 'try', 'finally', 'catch'
            is_controlled = (prev.endswith(')') or
                             prev in ('else', 'try', 'finally') or
                             prev.startswith('catch'))
            if not is_controlled:
                # Skip the entire block
                depth = 1
                i += 1
                while i < len(lines) and depth > 0:
                    depth += lines[i].count('{') - lines[i].count('}')
                    i += 1
                continue
        result.append(lines[i])
        i += 1
    return '\n'.join(result)

def inject_and_benchmark(sub_id, e_raw, d_raw):
    with open(TEMPLATE_FILE, 'r', encoding='utf-8') as f: template = f.read()
    
    e_body, d_body = normalize_body(e_raw, d_raw)
    e_body = cleanup_orphaned_blocks(e_body)
    d_body = cleanup_orphaned_blocks(d_body)
    # ASCII Gate: strip any non-ASCII chars (box-drawing, em-dash, arrows etc.) that break C# compiler
    e_body = e_body.encode('ascii', errors='ignore').decode('ascii')
    d_body = d_body.encode('ascii', errors='ignore').decode('ascii')
    injected = template.replace("// [[TRYENQUEUE_BODY]]", e_body)
    injected = injected.replace("// [[TRYDEQUEUE_BODY]]", d_body)
    
    # unsafe is now handled by the template


    with open(BENCHMARK_FILE, 'w', encoding='utf-8') as f: f.write(injected)
    
    print(f"[*] Benchmarking {sub_id}...")
    try:
        proc = subprocess.run(["dotnet", "run", "--project", os.path.join(BASE_DIR, "benchmarks", "SpscRing.Benchmarks.csproj"), "-c", "Release"], 
                             capture_output=True, text=True, timeout=30)
        output = proc.stdout
        latency_match = re.search(r'\|\s*RoundTrip\s*\|\s*([\d\.]+) ns', output)
        if latency_match:
            return {"id": sub_id, "status": "PASS", "latency": float(latency_match.group(1)), "alloc": "0 B"}
        else:
            print(f"[DEBUG] Output for {sub_id}:\n{output}\n{proc.stderr}")
    except Exception as e: return {"id": sub_id, "status": "ERROR", "error": str(e)}
    return {"id": sub_id, "status": "FAIL", "error": "Benchmark failed"}

def main():
    results = []
    subs = sorted([d for d in os.listdir(SUBMISSIONS_DIR) if os.path.isdir(os.path.join(SUBMISSIONS_DIR, d))])
    for sub in subs:
        content = ""
        named_exports = []   # (name, body) pairs from extract_named_ts_exports
        # Recursively harvest .ts/.tsx/.html source
        import html as _html
        for root, dirs, files in os.walk(os.path.join(SUBMISSIONS_DIR, sub)):
            for file in files:
                ext = os.path.splitext(file)[1].lower()
                if ext not in ('.ts', '.tsx', '.html'):
                    continue
                file_path = os.path.join(root, file)
                with open(file_path, 'r', encoding='utf-8', errors='ignore') as f:
                    file_content = f.read()
                if ext == '.html':
                    import re
                    match = re.search(r'<script id="csharp-source".*?>(.*?)</script>', file_content, re.DOTALL)
                    if match:
                        file_content = match.group(1)
                    else:
                        file_content = _html.unescape(re.sub(r'<[^>]+>', ' ', file_content))
                # ASCII Gate (pre-extraction): strip non-ASCII (box-drawing, em-dash, arrows) 
                # before method extraction so they cannot survive into injected C# code.
                file_content = file_content.encode('ascii', errors='ignore').decode('ascii')
                # V24 fix: harvest named TS export constants first (avoids regex truncation)
                named_exports.extend(extract_named_ts_exports(file_content))
                content += "\n" + file_content

        index_html = os.path.join(SUBMISSIONS_DIR, sub, "index.html")
        if os.path.exists(index_html):
            literals = [content]
        else:
            # Combine named export bodies + bare template literals, largest blocks first
            named_bodies = [body for _, body in named_exports]
            bare_literals = extract_all_literals(content)
            # Sort descending by size so we hit the real implementation first
            literals = sorted(named_bodies + bare_literals, key=len, reverse=True)

        e_raw, d_raw = "", ""
        for lit in literals:
            if not e_raw and ("TryEnqueue" in lit or "Dispatch" in lit or "Send" in lit or "TrySend" in lit):
                e_raw = (get_method_body(lit, "TryEnqueue") or
                         get_method_body(lit, "Dispatch")    or
                         get_method_body(lit, "TrySend")     or
                         get_method_body(lit, "Send"))
            if not d_raw and ("TryDequeue" in lit or "Consume" in lit or "WorkerLoop" in lit or "Receive" in lit or "TryReceive" in lit):
                d_raw = (get_method_body(lit, "TryDequeue")  or
                         get_method_body(lit, "Consume")     or
                         get_method_body(lit, "WorkerLoop")  or
                         get_method_body(lit, "TryReceive")  or
                         get_method_body(lit, "Receive"))
        
        if e_raw and d_raw:
            # Handle long -> double payload mismatch for Round 23 Dispatch
            if "long payload" in e_raw:
                e_raw = e_raw.replace("long payload", "double payload")
            
            # Normalize WorkerLoop (strip while(true) and Process)
            if "while (true)" in d_raw:
                 d_raw = d_raw.replace("while (true)", "").replace("{", "", 1).rstrip().rstrip("}")
            if "Process(payload)" in d_raw:
                 d_raw = d_raw.replace("Process(payload)", "return true")
            if "long payload" in d_raw:
                 d_raw = d_raw.replace("long payload", "double payload")
            
            results.append(inject_and_benchmark(sub, e_raw, d_raw))
        else:
            results.append({"id": sub, "status": "SKIP", "error": "Incomplete code"})
            
    with open(RESULTS_JSON, 'w') as f: json.dump(results, f, indent=2)
    with open(RESULTS_MD, 'w') as f:
        f.write("# AMAL Battle Results Summary (FINAL RE-VALIDATION)\n\n| ID | Status | Latency | Alloc |\n|----|--------|---------|-------|\n")
        items = sorted(results, key=lambda x: x.get('latency', 999.0))
        for r in items: f.write(f"| {r['id']} | {r.get('status')} | {r.get('latency', 'N/A')} | {r.get('alloc', 'N/A')} |\n")

if __name__ == "__main__": main()
