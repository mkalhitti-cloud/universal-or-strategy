import os
import re

SRC_DIR = os.path.join(os.path.dirname(__file__), '..', 'src')
SRC_DIR = os.path.realpath(SRC_DIR)

# Basic regex to identify a C# method signature
# (Needs to handle access modifiers, return types, name, and parameters)
METHOD_PATTERN = re.compile(
    r'^\s*(?:(?:public|private|protected|internal|protected internal|private protected|static|virtual|override|sealed|abstract|async|unsafe)\s+)*'
    r'(?!class|struct|interface|enum|record|delegate)'  # Exclude types
    r'(?P<return_type>[A-Za-z_][A-Za-z0-9_<>, \[\]]*)\s+'
    r'(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*\('
)

# Branching keywords that increase cyclomatic complexity
BRANCH_PATTERN = re.compile(r'\b(if|while|for|foreach|case|catch|continue|break)\b|\?\?|\|\||&&|\?')

def analyze_complexity():
    methods = []
    
    for root, _, files in os.walk(SRC_DIR):
        for filename in files:
            if not filename.endswith('.cs'):
                continue
                
            filepath = os.path.join(root, filename)
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                lines = f.readlines()
                
            current_method_name = None
            current_method_complexity = 0
            current_method_lines = 0
            brace_depth = 0
            in_method = False
            
            for i, line in enumerate(lines):
                line_stripped = line.strip()
                
                # Ignore comments for complexity calculation
                if line_stripped.startswith('//'):
                    continue
                
                # Check for method signature if not already in one
                if not in_method:
                    match = METHOD_PATTERN.match(line)
                    if match and not ';' in line_stripped and not '=' in line_stripped: # Avoid fields/properties
                        current_method_name = match.group('name')
                        current_method_complexity = 1  # Base complexity is 1
                        current_method_lines = 0
                        brace_depth = 0
                        in_method = True
                
                if in_method:
                    current_method_lines += 1
                    
                    # Count branches
                    branches = len(BRANCH_PATTERN.findall(line))
                    current_method_complexity += branches
                    
                    # Track braces to know when method ends
                    brace_depth += line.count('{')
                    brace_depth -= line.count('}')
                    
                    # Method ended
                    if brace_depth <= 0 and current_method_lines > 1:
                        if current_method_name:
                            methods.append({
                                'file': filename,
                                'method': current_method_name,
                                'complexity': current_method_complexity,
                                'lines': current_method_lines,
                                'start_line': i - current_method_lines + 2
                            })
                        in_method = False
                        current_method_name = None

    # Sort by complexity descending
    methods.sort(key=lambda x: x['complexity'], reverse=True)
    
    print("\n" + "="*60)
    print("🔥 TOP 50 MOST COMPLEX C# METHODS (HOTSPOTS) 🔥")
    print("="*60)
    print(f"{'COMPLEXITY':<12} | {'LINES':<6} | {'FILE':<40} | {'METHOD'}")
    print("-" * 100)
    
    for m in methods[:50]:
        print(f"{m['complexity']:<12} | {m['lines']:<6} | {m['file']:<40} | {m['method']} (Line {m['start_line']})")

if __name__ == "__main__":
    analyze_complexity()
