#!/usr/bin/env python3
"""Measure total size of Jane Street Knowledge Base."""

import sys
sys.path.insert(0, 'scripts')

from query_kb import init_firestore

def main():
    db = init_firestore()
    docs = list(db.collection('jane_street_knowledge_base').stream())
    
    total_chars = 0
    for doc in docs:
        data = doc.to_dict()
        
        # Add all relevant fields
        total_chars += len(str(data.get('title', '')))
        total_chars += len(str(data.get('key_takeaways', [])))
        total_chars += len(str(data.get('v12_csharp_patterns', {})))
        total_chars += len(str(data.get('presenter', '')))
        total_chars += len(str(data.get('category', '')))
    
    estimated_tokens = total_chars // 4
    
    print(f"Jane Street KB Size Analysis:")
    print(f"  Documents: {len(docs)}")
    print(f"  Total characters: {total_chars:,}")
    print(f"  Estimated tokens: {estimated_tokens:,}")
    print(f"  Percentage of 200k context: {(estimated_tokens / 200000) * 100:.2f}%")
    
    # Show per-doc average
    if docs:
        avg_chars = total_chars // len(docs)
        avg_tokens = avg_chars // 4
        print(f"  Average per doc: {avg_tokens:,} tokens")

if __name__ == "__main__":
    main()
