# PTT-REPAIRS-04-POST-BUG-E Ticket-1 Verification

**Epic:** PTT-REPAIRS-04-POST-BUG-E
**Ticket:** TICKET-1 -- BUG-E: EvictDedup Cancelled branch clears _lastLeaderDirection
**Phase:** 4b -- Independent Verification
**Verifier:** ptt-verifier
**Date:** 2026-09-06
**Scope:** SOURCE VERIFICATION ONLY -- READ-ONLY access to src/PropTraderTools/CopyEngine.cs

---

## MANDATORY READS CONFIRMED

- [x] CopyEngine.cs lines 5845-5910 -- read independently BEFORE reading completion report
- [x] ticket-1-completion.md -- Ph4a cross-check document
- [x] 04-tickets.md -- TICKET-1 section only
- [x] 02-architecture-plan.md -- Sections 3 and 4

---

## Step 1 -- Method Signature Verification (Independent Read)

**Action:** Read CopyEngine.cs lines 5845-5910 independently.

**Verifier independent findings:**

- **Line 5845:** Blank line above comment block.
- **Line 5846-5850:** Multi-line comment block (B62 / PTT-REPAIRS-04 CYC annotation / JS-025 note).
- **Line 5851:** `internal void EvictDedup(string orderId, OrderState state)` -- method signature.
- **Lines 5853-5858:** Terminal-state guard (if/&&/&&/return).
- **Line 5860:** `_dedupCache.TryRemove(orderId, out _);`
- **Line 5862:** `if (state == OrderState.Cancelled)` -- Cancelled branch opens.
- **Line 5864-5866:** `_entryDispatchedOrders.TryRemove(orderId, out _);`
- **Lines 5872-5883:** `_entryInstrKeyByOrderId.TryRemove(...)` block (instrKey removal + live-entry guard + BUG-E fix).
- **Line 5884:** Close brace -- Cancelled branch ends.
- **Line 5886:** `if (state == OrderState.Filled)` -- Filled branch opens.
- **Lines 5892-5898:** `_entryInstrKeyByOrderId.TryRemove(...)` block (Filled instrKey removal + value-guarded live-entry remove).
- **Line 5899:** Close brace -- Filled branch ends.
- **Line 5901:** Close brace -- method ends.

**Cross-check against completion Step 1:**
- Completion cites line 5851 for signature: MATCH
- Completion cites Cancelled branch opens 5862, closes 5884: MATCH
- Completion cites Filled branch opens 5886, closes 5899: MATCH
- Completion cites method body ends 5901: MATCH

**Cross-check result: MATCH**
**Step 1: PASS**

---

## Step 2 -- TryRemove Block Location (Independent)

**Verifier independent finding:**

- **Line 5872:** `if (_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey))`
- This line is inside `if (state == OrderState.Cancelled)` which opens at line 5862.
- The TryRemove block covers lines 5872-5883 (opening if to close brace).
- Inside the TryRemove block: value-guarded live-entry removal (5874-5877) and BUG-E direction-clear (5878-5882).

**Cross-check against completion Step 2:**
- Completion cites line 5872 for TryRemove: MATCH
- Completion cites TryRemove block lines 5872-5883: MATCH
- Completion cites inside Cancelled branch (5862): MATCH

**Cross-check result: MATCH**
**Step 2: PASS**

---

## Step 3 -- Fix Lines 5878-5882 Verbatim (Independent)

**Action:** Read lines 5878-5882 directly from source.

**Verifier independent read (exact content as observed):**

| Line | Exact Content (verifier read) |
|------|-------------------------------|
| 5878 | `                    // PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.` |
| 5879 | `                    // Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.` |
| 5880 | `                    var pipeIdx = cancelledInstrKey.IndexOf('|');` |
| 5881 | `                    if (pipeIdx > 0)` |
| 5882 | `                        _lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);` |

**Structural placement (verified independently):**
- Lines 5878-5882 are INSIDE the `if (_entryInstrKeyByOrderId.TryRemove(...))` block (opens at 5872).
- Lines 5878-5882 are SIBLINGS of the live-entry guard block (5875-5877), NOT nested inside it.
- Close brace for the TryRemove block is at line 5883. CONFIRMED.

**Cross-check against completion Step 3:**

| Line | Completion Quoted | Verifier Observed | Match |
|------|-------------------|-------------------|-------|
| 5878 | `// PTT-REPAIRS-04 BUG-E: entry cancelled without fill -- direction record is stale.` | Same | YES |
| 5879 | `// Clear _lastLeaderDirection so next entry in any direction is not reversal-blocked.` | Same | YES |
| 5880 | `    var pipeIdx = cancelledInstrKey.IndexOf('|');` | Same (full indentation) | YES |
| 5881 | `    if (pipeIdx > 0)` | Same (full indentation) | YES |
| 5882 | `        _lastLeaderDirection.TryRemove(cancelledInstrKey.Substring(0, pipeIdx), out _);` | Same (full indentation) | YES |

**Scan 5 (fix presence): PASS -- all five lines present verbatim.**
**Scan 6 (comment presence): PASS -- line 5878 contains "PTT-REPAIRS-04 BUG-E".**

**Cross-check result: MATCH**
**Step 3: PASS**

---

## Step 4 -- Scan 1: lock() Scan (Independent)

**Command run independently:**
```
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "\block\s*\("
```

**Verifier output:** 63+ matches, ALL in comment lines containing text such as:
- `// JS-021: ConcurrentDictionary -- lock-free. No lock() anywhere.`
- `// ConcurrentDictionary: thread-safe without lock(). JS-021: no lock.`
- `// JS-021: no lock() -- ConcurrentDictionary TryGetValue/TryRemove.`
- `// JS-021: no lock(). ConcurrentDictionary + Interlocked only.`
- `// JS-021: no lock(). Interlocked.Decrement is atomic.`
- (etc.)

Every match is a comment documenting lock-free compliance. No match is an executable C# `lock (` or `lock(` statement.

**Non-comment lock() matches: 0**

**Cross-check against completion Step 4:**
- Completion reports: "All 63 matches are comment lines only. No C# lock( or lock( statement exists anywhere in the file."
- Verifier independently confirms: All matches are documentation comments. 0 executable lock statements.

**Cross-check result: MATCH**
**Step 4 (Scan 1): PASS**

---

## Step 5 -- Scan 2: Non-ASCII Scan (Independent)

**Command run independently:**
```
Select-String -Path "src\PropTraderTools\CopyEngine.cs" -Pattern "[^\x00-\x7F]"
```

**Verifier output:** (no output -- command completed with no matches)

**Non-ASCII matches: 0**

**Cross-check against completion Step 5:**
- Completion reports: "(no output -- command returned no matches). Non-ASCII matches: 0."
- Verifier independently confirms: 0 matches.

**Cross-check result: MATCH**
**Step 5 (Scan 2): PASS**

---

## Step 6 -- Scan 3: CYC Count (Independent)

**Action:** Independently counted all decision tokens in EvictDedup lines 5851-5901.

**Decision tokens counted (verifier independent table):**

| Line | Token | Expression | D (running) |
|------|-------|------------|-------------|
| 5853 | `if` | `state != OrderState.Filled` | D=1 |
| 5854 | `&&` | `&& state != OrderState.Cancelled` | D=2 |
| 5855 | `&&` | `&& state != OrderState.Rejected` | D=3 |
| 5862 | `if` | `state == OrderState.Cancelled` | D=4 |
| 5872 | `if` | `_entryInstrKeyByOrderId.TryRemove(orderId, out var cancelledInstrKey)` | D=5 |
| 5875 | `if` | `_liveEntryInstruments.TryGetValue(cancelledInstrKey, out storedId)` | D=6 |
| 5876 | `&&` | `&& storedId == orderId` | D=7 |
| 5881 | `if` | `pipeIdx > 0` (BUG-E addition) | D=8 |
| 5886 | `if` | `state == OrderState.Filled` | D=9 |
| 5892 | `if` | `_entryInstrKeyByOrderId.TryRemove(orderId, out var filledInstrKey)` | D=10 |
| 5895 | `if` | `_liveEntryInstruments.TryGetValue(filledInstrKey, out storedId)` | D=11 |
| 5896 | `&&` | `&& storedId == orderId` | D=12 |

**Verifier total: D = 12, CYC = 1 + 12 = 13**

**Scan notes:**
- No `||`, `for`, `foreach`, `while`, `do`, `case`, `catch`, `?` tokens found in this method.
- No unexpected decision tokens beyond the 12 enumerated.
- The only BUG-E-introduced token is `if (pipeIdx > 0)` at line 5881 (D=8 in running count).
- Architecture plan Section 4 branch table matches verifier table exactly, line-for-line.
- JS-013 (CYC <= 8): DEFERRED classification confirmed -- pre-existing violation (CYC=12 pre-fix).
  BUG-E adds +1 to pre-existing violation. Extraction planned in DEFERRED-4 (out of scope).

**Cross-check against completion Step 6:**
- Completion reports D=12, CYC=13 with identical table.
- Verifier independently confirms D=12, CYC=13 with identical table.

**Cross-check result: MATCH**
**Step 6 (Scan 3): PASS**

---

## Step 7 -- Scan 4: Build Scan (Independent)

**Command run independently:**
```
dotnet build Linting.csproj /nologo 2>&1 | Select-String "CopyEngine"
```

**Verifier output:** Command exit code 1 (PowerShell Select-String exits 1 when no matches found).
No CopyEngine lines in output. Zero CopyEngine errors.

**Note:** Exit code 1 from Select-String with zero matches is standard PowerShell behavior
(confirmed in completion report and independently observed by verifier). This is NOT a build
failure. It confirms zero CopyEngine-related error lines were present in the build output.

**CopyEngine errors: 0**

**Cross-check against completion Step 7:**
- Completion reports: "(no CopyEngine lines in output -- zero matches). Note: exit code 1 = normal PS behavior."
- Verifier independently confirms: exit code 1, zero CopyEngine lines.

**Cross-check result: MATCH**
**Step 7 (Scan 4): PASS -- BUILD_PASS confirmed.**

---

## Step 8 -- Scan 7: Hard-Link Check (Independent)

**Command run independently:**
```
fsutil hardlink list "src\PropTraderTools\CopyEngine.cs"
```

**Verifier output:**
```
\WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
\Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs
```

**Path count: 2** -- repo path and NT8 installation path.

**Cross-check against completion Step 8:**
- Completion reports exactly:
  ```
  \WSGTA\universal-or-strategy\src\PropTraderTools\CopyEngine.cs
  \Users\Mohammed Khalid\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs
  ```
  Path count: 2.
- Verifier independently confirms: identical 2 paths.

**Cross-check result: MATCH**
**Step 8 (Scan 7): PASS**

---

## DNA Rule Audit (All Rules -- Verifier Independent Check)

| Rule | Check | Verifier Finding | Status |
|------|-------|-----------------|--------|
| JS-021 no lock() | Scan 1 -- zero non-comment lock matches | CONFIRMED: 0 executable lock statements | PASS |
| JS-025 ConcurrentDictionary TryRemove | _lastLeaderDirection.TryRemove is lock-free | CONFIRMED at line 5882 | PASS |
| JS-001 no throw in dispatch | TryRemove returns false on absent key; Substring valid because pipeIdx > 0 guard | CONFIRMED: no throw possible | PASS |
| JS-002 no null return | EvictDedup returns void | CONFIRMED | PASS |
| JS-042 ASCII-only | Lines 5878-5882: all 7-bit ASCII. "--" = two hyphens U+002D. Scan 2 = 0 non-ASCII | CONFIRMED | PASS |
| JS-013 CYC <= 8 | CYC=13 post-fix. Pre-existing violation (CYC=12 pre-fix). BUG-E adds +1. | DEFERRED-4 -- pre-existing, extraction out of scope | DEFERRED |
| No FontFamily | Scan-03 not applicable (no WPF in this method) | N/A | N/A |
| No #RRGGBB hex color | Scan-04 -- no hex color strings in fix lines | CONFIRMED | N/A |
| No DateTime.Now | Scan-06 -- no DateTime usage in fix lines | CONFIRMED | N/A |
| No sealed on TradeCopierWindow | Not in scope for this ticket | N/A | N/A |

---

## Architecture Compliance

| Requirement | Verifier Finding | Status |
|-------------|-----------------|--------|
| Fix at lines 5878-5882 | Present exactly as specified | PASS |
| Inside TryRemove block (5872-5883) | Confirmed -- siblings of live-entry guard at 5875-5877 | PASS |
| NOT nested inside live-entry guard | Confirmed -- pipeIdx logic starts after close of if-block at 5877 | PASS |
| instrKey key format used for Substring | cancelledInstrKey.Substring(0, pipeIdx) extracts instrument FullName prefix | PASS |
| pipeIdx > 0 guard present | Line 5881 confirmed | PASS |
| No .cs edits made (VERIFICATION ONLY scope) | Verifier is READ-ONLY; no edits were made | CONFIRMED |
| Structure matches arch plan Section 3 "After" block | Lines 5872-5883 match spec exactly | PASS |

---

## All Steps Summary

| Step | Description | Verifier Finding | Completion Report | Cross-check |
|------|-------------|-----------------|-------------------|-------------|
| 1 | Method signature line 5851; Cancelled 5862-5884; Filled 5886-5899; method end 5901 | PASS | PASS (same lines) | MATCH |
| 2 | TryRemove at line 5872, inside Cancelled branch (5862) | PASS | PASS (line 5872) | MATCH |
| 3 | Lines 5878-5882 verbatim; sibling placement confirmed | PASS | PASS (all 5 lines) | MATCH |
| 4 (Scan 1) | lock() -- zero non-comment matches | PASS | PASS (0 non-comment) | MATCH |
| 5 (Scan 2) | Non-ASCII -- zero matches | PASS | PASS (0 matches) | MATCH |
| 6 (Scan 3) | CYC D=12, CYC=13; JS-013 DEFERRED (pre-existing) | PASS | PASS (D=12, CYC=13) | MATCH |
| 7 (Scan 4) | Build scan -- zero CopyEngine errors | PASS | PASS (0 CopyEngine errors) | MATCH |
| 8 (Scan 7) | Hard-link -- 2 paths (repo + NT8) | PASS | PASS (2 paths, same text) | MATCH |

**Scans 5 (fix presence) and 6 (comment presence) verified inline in Step 3.**
All 7 Layer 2 scans covered.

---

## Discrepancies Found

**None.** All 8 steps match the completion report exactly:
- All line numbers match.
- All scan outputs match.
- All verbatim content matches.
- CYC count D=12, CYC=13 matches in both tables, line-by-line.
- Hard-link paths are character-for-character identical.

---

## Source Edit Status

**No .cs edits observed.** The ticket scope is SOURCE VERIFICATION ONLY. CopyEngine.cs was
accessed READ-ONLY. The fix at lines 5878-5882 was applied in the prior PTT-REPAIRS-04 session
and is confirmed present and correct.

---

## BUILD_PASS

Zero CopyEngine errors from independent build scan (Step 7).

---

## VERIFY_PASS

All 8 verification steps pass. All scans return expected results. Every line number in the
completion report matches verifier independent reads. No DNA violations. No discrepancies.

**VERIFY_PASS**
