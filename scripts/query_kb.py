import os
import sys
import json
import re

COLLECTION_NAME = 'jane_street_knowledge_base'
CREDENTIALS_PATH = 'firebase-credentials.json'

# OKF local wiki path — fallback when Firebase unavailable
OKF_WIKI_PATH = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                              'docs', 'intel', 'jane-street')


def search_okf_local(term):
    """Search the local OKF wiki as fallback when Firebase is unavailable."""
    if not os.path.isdir(OKF_WIKI_PATH):
        return []

    term_lower = term.lower()
    matches = []

    for fname in os.listdir(OKF_WIKI_PATH):
        if not fname.endswith('.md') or fname == 'index.md':
            continue
        fpath = os.path.join(OKF_WIKI_PATH, fname)
        try:
            content = open(fpath, encoding='utf-8').read()
        except Exception:
            continue

        if term_lower in content.lower():
            # Extract title from frontmatter
            title_match = re.search(r'^title:\s*(.+)$', content, re.MULTILINE)
            title = title_match.group(1).strip() if title_match else fname
            # Extract description
            desc_match = re.search(r'^description:\s*(.+)$', content, re.MULTILINE)
            desc = desc_match.group(1).strip() if desc_match else ''
            matches.append({
                'id': fname.replace('.md', ''),
                'title': title,
                'description': desc,
                'source': 'local_okf',
                'path': fpath,
                'snippet': _extract_snippet(content, term_lower),
            })

    return matches


def _extract_snippet(content, term):
    """Extract a short snippet around the matching term."""
    idx = content.lower().find(term)
    if idx == -1:
        return ''
    start = max(0, idx - 80)
    end = min(len(content), idx + 200)
    return '...' + content[start:end].strip() + '...'


def init_firestore():
    """Initializes Firebase using local service account credentials."""
    import firebase_admin
    from firebase_admin import credentials as fb_creds
    from firebase_admin import firestore as fb_firestore

    root_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    cred_path = os.path.join(root_dir, CREDENTIALS_PATH)

    if not os.path.exists(cred_path):
        print(f"[-] Credentials not found at {cred_path} — using local OKF wiki")
        return None

    try:
        cred = fb_creds.Certificate(cred_path)
        if not firebase_admin._apps:
            firebase_admin.initialize_app(cred)
        return fb_firestore.client()
    except Exception as e:
        print(f"[-] Firebase init failed: {e} — using local OKF wiki")
        return None

def search_kb(db, term):
    """Fetches the collection and performs a case-insensitive RAG substring search."""
    print(f"[*] Querying Jane Street Knowledge Base for: '{term}'...")
    collection_ref = db.collection(COLLECTION_NAME)
    docs = list(collection_ref.stream())

    term_lower = term.lower()
    matches = []

    for doc in docs:
        data = doc.to_dict()
        # Search across all text fields — support both old and new field names
        pattern_values = data.get('v12_csharp_patterns', data.get('patterns', {}))
        if isinstance(pattern_values, dict):
            pattern_text = " ".join(f"{k} {v}" for k, v in pattern_values.items())
        else:
            pattern_text = " ".join(str(p) for p in pattern_values)

        search_text = " ".join([
            str(doc.id),
            str(data.get('title', '')),
            str(data.get('category', '')),
            str(data.get('description', '')),
            " ".join(data.get('key_takeaways', data.get('takeaways', []))),
            pattern_text,
        ]).lower()

        if term_lower and term_lower in search_text:
            matches.append((doc.id, data))
        elif not term_lower:
            matches.append((doc.id, data))

    if not matches:
        print(f"[-] No results found for '{term}' in Firebase.")
        print("[*] Falling back to local OKF wiki...")
        okf_results = search_okf_local(term)
        if okf_results:
            print(f"[+] Found {len(okf_results)} matching document(s) in local OKF wiki:\n")
            for r in okf_results:
                print(f"=== {r['title']} ===")
                print(f"File        : {r['path']}")
                print(f"Description : {r['description']}")
                print(f"Snippet     : {r['snippet']}")
                print("-" * 40)
        else:
            print(f"[-] No results found in Firebase or local OKF wiki for '{term}'.")
            print("[*] Available Firebase documents:")
            for doc in docs:
                print(f"  - {doc.id} ({doc.to_dict().get('title', 'No Title')})")
        return

    print(f"[+] Found {len(matches)} matching document(s):\n")
    for doc_id, data in matches:
        print(f"=== {data.get('title', 'Unknown Title')} ===")
        print(f"Document ID : {doc_id}")
        print(f"Category    : {data.get('category', 'N/A')}")
        takeaways = data.get('key_takeaways', data.get('takeaways', []))
        if takeaways:
            print("Key Takeaways:")
            for t in takeaways:
                print(f"  - {t}")
        patterns = data.get('v12_csharp_patterns', data.get('patterns', {}))
        if patterns:
            print("V12 C# Patterns:")
            if isinstance(patterns, dict):
                for k, v in patterns.items():
                    print(f"  {k}: {v}")
            else:
                for p in patterns:
                    print(f"  - {p}")
        print("-" * 40)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python query_kb.py \"<search_term>\"")
        sys.exit(1)

    query_term = sys.argv[1]
    db = init_firestore()

    if db is None:
        # Firebase unavailable — use local OKF wiki
        print(f"[*] Querying local OKF wiki for: '{query_term}'...")
        results = search_okf_local(query_term)
        if not results:
            print(f"[-] No results found for '{query_term}' in local OKF wiki.")
            print(f"[*] Wiki location: {OKF_WIKI_PATH}")
        else:
            print(f"[+] Found {len(results)} matching document(s) in local OKF wiki:\n")
            for r in results:
                print(f"=== {r['title']} ===")
                print(f"File        : {r['path']}")
                print(f"Description : {r['description']}")
                print(f"Snippet     : {r['snippet']}")
                print("-" * 40)
    else:
        search_kb(db, query_term)
