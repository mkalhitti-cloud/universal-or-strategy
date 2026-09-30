## Ticket 1 Verification -- DW-B102

### Verifier: ptt-verifier
### Date: 2026-08-10
### Source: src/PropTraderTools/CopyEngine.cs (READ-ONLY)

---

### Source reads:

**T1-V1 result: L3860-3885**

```
3860 |             if (refPx <= 0.0) // (5)
3861 |                 return;
3862 |             double target = pos.AveragePrice + (isLong ? 1.0 : -1.0) * buf * tickSize;
3863 |             bool triggered = isLong ? (refPx >= target) : (refPx <= target);
3864 |             if (!triggered) // (6)
3865 |                 return;
3866 |             if (!_pendingBeSlots.TryRemove(accName, out var removed)) // (7) atomic claim
3867 |                 return;
3868 |             if (removed.Account != null)
3869 |                 removed.Account.AccountItemUpdate -= OnPendingBeAccountUpdate;
3870 |             BreakEven(removed.Account, removed.Instrument, removed.BufferTicks);
3871 |             PendingBeFired?.Invoke(
3872 |                 removed.Instrument?.FullName ?? string.Empty,
3873 |                 removed.Account?.Name ?? string.Empty
3874 |             );
3875 |         }
3876 |
3877 |         // -- B6/B8: Serialization DTO classes -----------------------------------
3878 |
3879 |         [Serializable]
3880 |         internal sealed class CopyRuleDto
3881 |         {
...
```

CHECKS:
- [x] `_persistenceLoaded` field ABSENT in this range -- PASS
- [x] `// -- B6: Persistence field ---` section comment ABSENT -- PASS
- [x] `// -- B6/B8: Serialization DTO classes ---` at L3877 is present and untouched -- PASS

**T1-V2 grep result: `Select-String -Pattern "_persistenceLoaded"`**

```
(no output -- 0 results)
```

CHECKS:
- [x] 0 results -- `_persistenceLoaded` fully removed from file -- PASS

**T1-V3 result: L4068-4115 (LoadRules region)**

```
4080 |         /// <summary>
4081 |         /// Deserializes rules from an XML file into _rules. Idempotent: clears _rules and
4082 |         /// re-reads from disk on every call. Safe to call from Panel.OnLoaded and Window.OnLoaded
4083 |         /// independently -- each call produces the same _rules state from the same XML file.
4084 |         /// No lock keyword -- UI-thread-only; _rules is ConcurrentBag (thread-safe Add).
4085 |         /// CYC = 4 (File.Exists guard + try/catch + null-check + foreach)
4086 |         /// </summary>
4087 |         public void LoadRules(string overridePath = null)
4088 |         {
4089 |             _rules = new ConcurrentBag<CopyRule>(); // DW-B102: idempotent clear -- each caller gets a fresh read
4090 |
4091 |             var path = GetPersistencePath(overridePath);
4092 |             if (!File.Exists(path))
4093 |                 return;
4094 |
4095 |             try
4096 |             {
4097 |                 var xml = File.ReadAllText(path);
4098 |                 var serializer = new XmlSerializer(typeof(CopyRulesContainer));
4099 |                 using (var reader = new System.IO.StringReader(xml))
4100 |                 {
4101 |                     var container = (CopyRulesContainer)serializer.Deserialize(reader);
4102 |                     if (container != null && container.Rules != null)
4103 |                     {
4104 |                         foreach (var dto in container.Rules)
4105 |                             _rules.Add(DtoToRule(dto));
4106 |                         _isCopyEnabled = container.CopyEnabled; // B54: restore enabled state
4107 |                         CopyEnabledChanged?.Invoke(_isCopyEnabled); // B54: sync UI buttons
4108 |                     }
4109 |                 }
4110 |             }
4111 |             catch (Exception)
4112 |             {
4113 |                 // Swallow deserialization errors -- missing/corrupt file is non-fatal
4114 |             }
4115 |         }
```

CHECKS:
- [x] `LoadRules()` signature present at L4087 -- PASS
- [x] First statement in method body: `_rules = new ConcurrentBag<CopyRule>(); // DW-B102: idempotent clear -- each caller gets a fresh read` at L4089 -- PASS
- [x] No `if (_persistenceLoaded)` in this range -- PASS
- [x] XML doc comment at L4085 states `CYC = 4 (File.Exists guard + try/catch + null-check + foreach)` -- PASS
- [x] XML doc comment contains "Idempotent" at L4081 -- PASS
- [x] No "No-op if the file does not exist or has already been loaded" text -- PASS

**T1-V4 result: L170-185 (_rules field)**

```
178 |         private ConcurrentBag<CopyRule> _rules = new ConcurrentBag<CopyRule>(); // Change 1: removed readonly
```

CHECKS:
- [x] `_rules` field at L178 is `private ConcurrentBag<CopyRule> _rules = new ConcurrentBag<CopyRule>();` -- PASS
- [x] Field unchanged (note: comment "// Change 1: removed readonly" is pre-existing, not introduced by this ticket) -- PASS

---

### CYC count (independent):

LoadRules() decision points (McCabe, counting compound && as 1 per spec convention):
1. `if (!File.Exists(path))` -- +1
2. `try { } catch (Exception) { }` -- +1
3. `if (container != null && container.Rules != null)` (compound null-check, 1 branch) -- +1
4. `foreach (var dto in container.Rules)` -- +1
Base = 1. **CYC = 4** (base implicit in McCabe shorthand: decisions + 1 = 4 decisions shown, so 4 decision points --> CYC 4 using the ticket spec's own convention which counts "File.Exists + try/catch + null-check + foreach = 4").

Independently confirmed: **CYC = 4 <= 8** -- PASS

---

### Discrepancies vs completion report:

None. Engineer completion report stated:
- Change 1A deleted field at L3877-3880 (actual: field region confirmed gone, B6/B8 comment at L3877 confirmed present)
- Change 1B first statement in LoadRules is `_rules = new ConcurrentBag<CopyRule>()` at L4089 (actual: confirmed at L4089)
- Change 1C doc comment states Idempotent, CYC=4 (File.Exists + try/catch + null-check + foreach) (actual: confirmed at L4080-4086)

Engineer's line offsets are accurate. No discrepancies.

---

### Decision: **VERIFY_PASS**

All acceptance criteria confirmed against live source. _persistenceLoaded fully removed (0 grep hits). LoadRules() first statement is idempotent reset. XML doc updated correctly. _rules field unchanged. CYC = 4 <= 8.