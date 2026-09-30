# B103-LaneA Tickets

**Status**: TICKETS_COMPLETE
**Source plan**: docs/brain/B103-LaneA/02-architecture-plan.md (REVIEW_PASS — cycle 2)
**Date**: 2026-08-10
**Author**: ptt-architect

---

## Ticket 1 — DW-B102: Remove `_persistenceLoaded` One-Shot Guard

### Metadata
- File: `src/PropTraderTools/CopyEngine.cs`
- Spec Requirement: DW-B102 (LoadRules one-shot guard race condition)
- Method(s) affected: `LoadRules()` + `_persistenceLoaded` field

---

### Change 1A — Delete `_persistenceLoaded` field
**Location**: `CopyEngine.cs` L3868-3871 (section comment + blank line + field + trailing blank line)

**BEFORE** (exact text to find — 4 lines including surrounding blanks):
```
        // -- B6: Persistence field -------------------------------------------

        private volatile bool _persistenceLoaded = false;

```

**AFTER** (delete these 4 lines entirely):
```
(lines deleted — blank space compiles away)
```

**Note**: The line at L3872 (`// -- B6/B8: Serialization DTO classes ---`) MUST remain untouched immediately after the deletion.

---

### Change 1B — Replace guard block in `LoadRules()` body
**Location**: `CopyEngine.cs` L4084-4086 (inside `LoadRules()` method body, first 3 statements)

**BEFORE** (exact text — 3 lines):
```csharp
            if (_persistenceLoaded)
                return;
            _persistenceLoaded = true;
```

**AFTER** (exact replacement — 1 line):
```csharp
            _rules = new ConcurrentBag<CopyRule>(); // DW-B102: idempotent clear -- each caller gets a fresh read
```

**Context**: The blank line at L4087 and the `var path = GetPersistencePath(overridePath);` at L4088 remain immediately after this replacement.

---

### Change 1C — Update XML doc comment above `LoadRules()`
**Location**: `CopyEngine.cs` L4075-4081 (7-line `<summary>` block above `LoadRules()`)

**BEFORE** (exact text — 7 lines):
```csharp
        /// <summary>
        /// Deserializes rules from an XML file and adds them to _rules via ConcurrentBag.Add().
        /// Called from TradeCopierWindow.OnInitialize() on the NT main thread.
        /// No-op if the file does not exist or has already been loaded.
        /// No lock keyword -- called once at startup; _rules is ConcurrentBag (thread-safe Add).
        /// CYC = 4 (loaded guard + File.Exists guard + try/catch + foreach)
        /// </summary>
```

**AFTER** (exact replacement — 7 lines):
```csharp
        /// <summary>
        /// Deserializes rules from an XML file into _rules. Idempotent: clears _rules and
        /// re-reads from disk on every call. Safe to call from Panel.OnLoaded and Window.OnLoaded
        /// independently -- each call produces the same _rules state from the same XML file.
        /// No lock keyword -- UI-thread-only; _rules is ConcurrentBag (thread-safe Add).
        /// CYC = 4 (File.Exists guard + try/catch + null-check + foreach)
        /// </summary>
```

---

### Full `LoadRules()` after all three changes (engineer verification reference)

```csharp
        /// <summary>
        /// Deserializes rules from an XML file into _rules. Idempotent: clears _rules and
        /// re-reads from disk on every call. Safe to call from Panel.OnLoaded and Window.OnLoaded
        /// independently -- each call produces the same _rules state from the same XML file.
        /// No lock keyword -- UI-thread-only; _rules is ConcurrentBag (thread-safe Add).
        /// CYC = 4 (File.Exists guard + try/catch + null-check + foreach)
        /// </summary>
        public void LoadRules(string overridePath = null)
        {
            _rules = new ConcurrentBag<CopyRule>(); // DW-B102: idempotent clear -- each caller gets a fresh read

            var path = GetPersistencePath(overridePath);
            if (!File.Exists(path))
                return;

            try
            {
                var xml = File.ReadAllText(path);
                var serializer = new XmlSerializer(typeof(CopyRulesContainer));
                using (var reader = new System.IO.StringReader(xml))
                {
                    var container = (CopyRulesContainer)serializer.Deserialize(reader);
                    if (container != null && container.Rules != null)
                    {
                        foreach (var dto in container.Rules)
                            _rules.Add(DtoToRule(dto));
                        _isCopyEnabled = container.CopyEnabled; // B54: restore enabled state
                        CopyEnabledChanged?.Invoke(_isCopyEnabled); // B54: sync UI buttons
                    }
                }
            }
            catch (Exception)
            {
                // Swallow deserialization errors -- missing/corrupt file is non-fatal
            }
        }
```

---

### 7-Scan Checklist (Ticket 1)

- **SCAN-01** `lock()`: `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` in changed regions → **0 new matches**
- **SCAN-02** `throw new`: `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` in changed regions → **0 new matches**
- **SCAN-03** ASCII: all new string literals (`"DW-B102: idempotent clear -- each caller gets a fresh read"`, doc comment text) are pure ASCII → **confirm visual**
- **SCAN-04** `_persistenceLoaded` fully gone: `grep -n "_persistenceLoaded" src/PropTraderTools/CopyEngine.cs` → **0 results** (field at L3870 and guard at L4084-4086 both deleted)
- **SCAN-05** `_rules` reset present: `grep -n "_rules = new ConcurrentBag" src/PropTraderTools/CopyEngine.cs` inside `LoadRules()` → **1 match** at new first statement
- **SCAN-06** CYC `LoadRules()`: manual count = `File.Exists` (+1) + `try/catch` (+1) + `if (container != null && container.Rules != null)` (+1) + `foreach` (+1) + base = **CYC 4** ≤ 8 ✓
- **SCAN-07** sync: `powershell -File scripts\ptt-sync-and-verify.ps1` → **0 MISMATCH**

---

### Acceptance Criteria (Ticket 1)

- [ ] `grep -n "_persistenceLoaded" src/PropTraderTools/CopyEngine.cs` → **0 results**
- [ ] `LoadRules()` first executable statement is: `_rules = new ConcurrentBag<CopyRule>();`
- [ ] `LoadRules()` contains no: `if (_persistenceLoaded)` or `_persistenceLoaded = true`
- [ ] Doc comment above `LoadRules()` states `CYC = 4 (File.Exists guard + try/catch + null-check + foreach)`
- [ ] Doc comment no longer contains: `No-op if the file does not exist or has already been loaded`
- [ ] CYC of `LoadRules()` = **4** (File.Exists + try/catch + null-check + foreach)
- [ ] `_rules` field at ~L178 unchanged: `private ConcurrentBag<CopyRule> _rules = new ConcurrentBag<CopyRule>();`
- [ ] `// -- B6/B8: Serialization DTO classes ---` comment at L3872 (now ~L3868) is untouched
- [ ] `ptt-sync-and-verify.ps1`: **0 MISMATCH**

---

## Ticket 2 — DW-B103: Guard PTT Exit Brackets in `TryCancelFollowerEntries`

### Metadata
- File: `src/PropTraderTools/CopyEngine.cs`
- Spec Requirement: DW-B103 (PTT exit bracket wipe on OCO cancel)
- Method(s) affected: `TryCancelFollowerEntries()` (L1506-1523)

---

### Change 2A — Replace full method with PTT exit bracket guard inserted
**Location**: `CopyEngine.cs` L1506-1523 (method block comment + method body)

**BEFORE** (exact text — 18 lines):
```csharp
        // TryCancelFollowerEntries: CYC=4. Propagates leader cancel to all follower entry orders.
        // Returns true if Cancelled state was handled (caller should return immediately).
        // HOTFIX-B63-COPY-CANCEL-01: ATM bracket cancels are skipped via IsAtmBracketName guard.
        // JS-021: no lock. JS-001: no throw.
        private bool TryCancelFollowerEntries(Order order, CopyRule rule)
        {
            if (order.OrderState != OrderState.Cancelled)
                return false;
            if (IsAtmBracketName(order.Name))
                return true; // HOTFIX-B63-COPY-CANCEL-01
            foreach (var acc in rule.FollowerAccounts)
            {
                if (acc == null)
                    continue;
                CancelOneAccount(acc, order.Instrument);
            }
            return true;
        }
```

**AFTER** (exact replacement — 24 lines):
```csharp
        // TryCancelFollowerEntries: CYC=6. Propagates leader cancel to all follower entry orders.
        // Returns true if Cancelled state was handled (caller should return immediately).
        // HOTFIX-B63-COPY-CANCEL-01: ATM bracket cancels are skipped via IsAtmBracketName guard.
        // DW-B103: PTT exit bracket OCO-cancels return false (do not wipe follower brackets).
        // JS-021: no lock. JS-001: no throw.
        private bool TryCancelFollowerEntries(Order order, CopyRule rule)
        {
            if (order.OrderState != OrderState.Cancelled)
                return false;
            if (IsAtmBracketName(order.Name))
                return true; // HOTFIX-B63-COPY-CANCEL-01
            if (
                order.Name != null
                && (
                    order.Name.StartsWith("PTT-QX-", StringComparison.Ordinal)
                    || order.Name.StartsWith("PTT-BE-", StringComparison.Ordinal)
                )
            )
                return false; // DW-B103: OCO-cancel of PTT exit bracket must not wipe follower brackets
            foreach (var acc in rule.FollowerAccounts)
            {
                if (acc == null)
                    continue;
                CancelOneAccount(acc, order.Instrument);
            }
            return true;
        }
```

---

### Protected Regions (DO NOT TOUCH)

| Region | Lines | Reason |
|--------|-------|--------|
| `IsBracketLeg()` (instance method) | L3198-3205 | B29 intentionally removed `PTT-` prefix so the Cancel button can still cancel PTT exit brackets. Correct design. UNTOUCHED. |
| `CancelOneAccount()` | L2915-2939 | User-initiated cancel path — cancelling `PTT-QX-*/PTT-BE-*` IS intentional there. UNTOUCHED. |
| `IsAtmBracketName()` | ~L669-682 | B63 hotfix: NT8 native bracket guard. Modifying breaks HOTFIX-B63-COPY-CANCEL-01. UNTOUCHED. |

---

### CYC Breakdown (Ticket 2 — for engineer verification)

| Branch | McCabe +1 |
|--------|-----------|
| `order.OrderState != OrderState.Cancelled` | +1 |
| `IsAtmBracketName(order.Name)` | +1 |
| `order.Name != null` (null guard of compound `&&`) | +1 |
| `StartsWith("PTT-QX-") \|\| StartsWith("PTT-BE-")` (OR branch) | +1 |
| `foreach (var acc in rule.FollowerAccounts)` (loop) | +1 |
| `if (acc == null)` (null guard inside loop) | +1 |
| Base | +1 |
| **Total CYC** | **6** |

CYC = **6** ≤ 8 ✓

---

### 7-Scan Checklist (Ticket 2)

- **SCAN-01** `lock()`: `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` in `TryCancelFollowerEntries` region → **0 matches**
- **SCAN-02** `throw new`: `grep -n "throw new" src/PropTraderTools/CopyEngine.cs` in `TryCancelFollowerEntries` region → **0 matches**
- **SCAN-03** ASCII: `"PTT-QX-"`, `"PTT-BE-"`, `"DW-B103: OCO-cancel..."`, `StringComparison.Ordinal` — all pure ASCII → **confirm visual**
- **SCAN-04** PTT-QX guard present: `grep -n "PTT-QX-" src/PropTraderTools/CopyEngine.cs` → **1 match** inside `TryCancelFollowerEntries`
- **SCAN-05** `StringComparison.Ordinal` used: `grep -n "StringComparison.Ordinal" src/PropTraderTools/CopyEngine.cs` in change region → **2 matches** (one per `StartsWith` call)
- **SCAN-06** CYC `TryCancelFollowerEntries()`: manual count = OrderState (+1) + IsAtmBracketName (+1) + name-null (+1) + OR-branch (+1) + foreach (+1) + acc-null (+1) + base = **CYC 6** ≤ 8 ✓
- **SCAN-07** sync: `powershell -File scripts\ptt-sync-and-verify.ps1` → **0 MISMATCH**

---

### Acceptance Criteria (Ticket 2)

- [ ] `TryCancelFollowerEntries` contains `StartsWith("PTT-QX-", StringComparison.Ordinal)` guard
- [ ] `TryCancelFollowerEntries` contains `StartsWith("PTT-BE-", StringComparison.Ordinal)` guard
- [ ] New guard `return false` (NOT `return true`) — followers are NOT cancelled for PTT exit brackets
- [ ] `StringComparison.Ordinal` used on **both** `StartsWith` calls
- [ ] New guard is placed **after** `IsAtmBracketName` guard and **before** `foreach` loop
- [ ] Block comment above method updated: `CYC=6` and `DW-B103:` annotation present
- [ ] `IsBracketLeg()` instance method at L3198-3205: **UNCHANGED**
- [ ] `CancelOneAccount()` at L2915-2939: **UNCHANGED**
- [ ] `IsAtmBracketName()` at ~L669-682: **UNCHANGED**
- [ ] CYC of `TryCancelFollowerEntries()` = **6** ≤ 8
- [ ] `ptt-sync-and-verify.ps1`: **0 MISMATCH**

---

## Application Order

Apply **Ticket 2 first** (lower line numbers: L1506-1523), then **Ticket 1** (higher line numbers: L3868-3871 and L4075-4112). This preserves line offsets during sequential editing. Either order is safe since ranges do not overlap — applying low-lines first is a defensive convention only.

---

**TICKETS_COMPLETE**
