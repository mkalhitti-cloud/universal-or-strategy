## Ticket 1 Completion — DW-B102

### Status: BUILD_PASS

### Changes Applied

- **Change 1A**: Deleted `// -- B6: Persistence field ---` section comment (was L3877), blank line (L3878), `private volatile bool _persistenceLoaded = false;` (was L3879), and trailing blank line (L3880). The `// -- B6/B8: Serialization DTO classes ---` comment immediately after remains untouched at what is now L3877.

- **Change 1B**: Replaced the three-line guard block inside `LoadRules()` (was L4089-4091: `if (_persistenceLoaded) / return; / _persistenceLoaded = true;`) with a single idempotent reset line: `_rules = new ConcurrentBag<CopyRule>(); // DW-B102: idempotent clear -- each caller gets a fresh read` (now L4089).

- **Change 1C**: Updated the XML doc comment above `LoadRules()` (was L4080-4086, now L4080-4086 same range). Replaced the five content lines inside `<summary>` — removed "No-op if the file does not exist or has already been loaded" and updated CYC description from `(loaded guard + File.Exists guard + try/catch + foreach)` to `(File.Exists guard + try/catch + null-check + foreach)`. New text states idempotent, UI-thread-only, CYC=4.

### Final LoadRules() state (verified at L4080-4110)

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
    // ... try/catch/foreach body unchanged
```

### Scan Results

- **SCAN-01 lock()**: `Select-String -Pattern "lock\("` — 1 hit in a comment at L1897 (pre-existing, not in changed region). 0 new `lock(` in changed regions. PASS.
- **SCAN-02 throw new**: 0 results across entire file. PASS.
- **SCAN-03 ASCII**: All new string literals (`"DW-B102: idempotent clear -- each caller gets a fresh read"`, updated doc comment text) are pure ASCII; double-dash `--` is 0x2D hyphen-minus. PASS.
- **SCAN-04 _persistenceLoaded**: 0 results — field at former L3879 and guard at former L4089-4091 both fully deleted. PASS.
- **SCAN-05 _rules reassign**: `_rules = new ConcurrentBag<CopyRule>()` present as first statement in `LoadRules()` at L4089. PASS.
- **SCAN-06 CYC LoadRules**: Decision points: `File.Exists` (+1) + `try/catch` (+1) + `if (container != null && container.Rules != null)` (+1) + `foreach` (+1) + base = **CYC 4** <= 8. PASS.
- **SCAN-07 sync**: `ptt-sync-and-verify.ps1` — 1 COPIED (CopyEngine.cs), 15 In-sync, 0 MISMATCH. All 16 files OK. PASS.

### Acceptance Criteria

- [x] `grep -n "_persistenceLoaded" src/PropTraderTools/CopyEngine.cs` -> 0 results
- [x] `LoadRules()` first executable statement is: `_rules = new ConcurrentBag<CopyRule>();`
- [x] `LoadRules()` contains no: `if (_persistenceLoaded)` or `_persistenceLoaded = true`
- [x] Doc comment above `LoadRules()` states `CYC = 4 (File.Exists guard + try/catch + null-check + foreach)`
- [x] Doc comment no longer contains: `No-op if the file does not exist or has already been loaded`
- [x] CYC of `LoadRules()` = **4** (File.Exists + try/catch + null-check + foreach)
- [x] `_rules` field at ~L178 unchanged: `private ConcurrentBag<CopyRule> _rules = new ConcurrentBag<CopyRule>();`
- [x] `// -- B6/B8: Serialization DTO classes ---` comment at L3877 (was L3881) is untouched
- [x] `ptt-sync-and-verify.ps1`: 0 MISMATCH
