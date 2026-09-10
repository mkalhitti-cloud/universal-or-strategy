# PTT-REPAIRS-09-OBFUSC-ATTR — Tickets
**Epic:** PTT-REPAIRS-09-OBFUSC-ATTR  
**Phase:** 3 — Ticket Generation  
**Author:** ptt-architect  
**Plan version:** `docs/brain/PTT-REPAIRS-09-OBFUSC-ATTR/02-architecture-plan.md` (REVIEW_PASS)  
**Plan review:** `docs/brain/PTT-REPAIRS-09-OBFUSC-ATTR/02-plan-review.md` (REVIEW_PASS — zero violations)

---

## T1 — Add ObfuscationAttribute to 3 CopyEngine private/internal members

### Spec Requirement IDs

| Requirement | Plan Reference |
|-------------|---------------|
| Add `[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` to private/internal members accessed by reflection in obfuscation-skipped tests | STEP 3 — Insertions 1, 2, 3 |
| Target only members that exist in `CopyEngine.cs` (do not stub or create) | STEP 2 — Members Found (3 of 73) |
| No signature changes to any decorated member | STEP 6 — "No signature changes" |
| No skip removals in this epic (deferred per DW-09-01 through DW-09-04) | STEP 4 — Zero skip removals |
| Verification: test counts unchanged at 24/0/490/514 | STEP 5 — Verification Plan |
| NT8 hard-link sync after any src/ edit | STEP 7 — SCAN-07 |

---

### File

```
src/PropTraderTools/CopyEngine.cs
```

---

### Members to Decorate (exact, from REVIEW_PASS plan)

All three insertions are in `src/PropTraderTools/CopyEngine.cs`. No other file is touched.

#### Insertion 1 — `LogBeSlotEviction` (line ~1778 pre-edit)

**Current declaration at L1778:**
```csharp
        private static void LogBeSlotEviction(string accName, bool isRejected)
```

**Insert the following line immediately above the declaration (8-space indent):**
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
```

**Result after insertion:**
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private static void LogBeSlotEviction(string accName, bool isRejected)
```

**CYC impact:** Zero.  
**Signature change:** None.

---

#### Insertion 2 — `LogDiagOrderCount` (line ~6422 pre-edit; ~6423 after Insertion 1 shifts lines)

**Current declaration at L6422:**
```csharp
        private void LogDiagOrderCount(Account acc, Instrument instrument)
```

**Insert the following line immediately above the declaration (8-space indent):**
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
```

**Result after insertion:**
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        private void LogDiagOrderCount(Account acc, Instrument instrument)
```

**CYC impact:** Zero.  
**Signature change:** None.  
**Note:** Protects the currently-passing `[Fact]` test (L6094 in `CopyEngineTests.cs`) from obfuscated-build regression. This member's test uses correct `NonPublic | Instance` binding flags and is already passing.

---

#### Insertion 3 — `GetSenderAccountName` (line ~6967 pre-edit; ~6970 after Insertions 1+2 shift lines)

**Current declaration at L6967:**
```csharp
        internal static string GetSenderAccountName(object sender)
```

**Insert the following line immediately above the declaration (8-space indent):**
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
```

**Result after insertion:**
```csharp
        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
        internal static string GetSenderAccountName(object sender)
```

**CYC impact:** Zero.  
**Signature change:** None.

---

### Method Signatures (exact — unchanged by this ticket)

```csharp
// Insertion 1
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private static void LogBeSlotEviction(string accName, bool isRejected)

// Insertion 2
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
private void LogDiagOrderCount(Account acc, Instrument instrument)

// Insertion 3
[System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]
internal static string GetSenderAccountName(object sender)
```

No method body is modified. No parameter names, return types, or access modifiers are changed.

---

### Constraint List (Jane Street / DNA Rules)

| Rule | Constraint | Status |
|------|-----------|--------|
| JS-021 | No `lock()` added anywhere | REQUIRED — Attribute lines contain no executable code; no lock possible |
| JS-001 | No `throw` statement added | REQUIRED — Attribute lines contain no executable code; no throw possible |
| JS-023 | No UI updates from off-thread | N/A — No UI code touched |
| ASCII-only | All inserted characters must be 7-bit ASCII | REQUIRED — `Feature = "rename"` and `Exclude = true` are ASCII-only |
| No `DateTime.Now` | No `DateTime.Now` reference added | N/A — No code added |
| No `FontFamily` | No FontFamily instantiation | N/A — No UI code touched |
| No hardcoded hex | No `#RRGGBB` literals | N/A — No UI code touched |
| No `CreateOrder` without PTT- prefix | No order creation | N/A — No order code touched |
| CYC <= 8 | No new branches; CYC of decorated methods unchanged | REQUIRED — Attributes are not control-flow constructs |
| No signature change | Decorated method signatures must remain byte-identical | REQUIRED — Engineer must not alter any declaration beyond inserting the attribute line above it |

---

### xUnit Tests

**No new tests are written for this ticket.** The insertions are compile-time metadata. The existing test suite is the verification vehicle.

**Tests to run:**
```
dotnet test src/PropTraderTools/ --no-build --verbosity normal
```

**Expected output (unchanged from baseline):**
```
Failed: 0, Passed: 24, Skipped: 490, Total: 514
```

Any deviation from these counts is a regression and must be investigated before the ticket is considered complete.

**Existing test coverage notes:**
- `LogDiagOrderCount` — a passing `[Fact]` test exists at `CopyEngineTests.cs` ~L6094 using `NonPublic | Instance` binding flags. Must remain passing after this edit.
- `LogBeSlotEviction` — obfuscation-skip tests exist but remain skipped (binding flag mismatch deferred to DW-09-02).
- `GetSenderAccountName` — obfuscation-skip test exists but remains skipped (binding flag mismatch deferred to DW-09-03).

---

### NT8 Hard-Link Sync Step

After modifying `CopyEngine.cs`, run:
```
powershell -File .\deploy-sync.ps1
```

This must exit `0`. Hard-link sync failure blocks completion of this ticket. Run this before executing `dotnet test`.

---

### 7-Scan Checklist (Engineer Contract — all 7 required before marking T1 done)

| Scan ID | Scan Name | Command | Pass Criterion |
|---------|-----------|---------|----------------|
| SCAN-01 | `lock()` check | `grep -n "lock(" src/PropTraderTools/CopyEngine.cs` | **0 NEW matches** relative to pre-edit baseline (no new `lock(` lines introduced) |
| SCAN-02 | `throw` check | `grep -n "\bthrow\b" src/PropTraderTools/CopyEngine.cs` | **0 NEW matches** relative to pre-edit baseline (no new `throw` lines introduced) |
| SCAN-03 | `DateTime.Now` check | `grep -n "DateTime\.Now" src/PropTraderTools/CopyEngine.cs` | **0 matches** in modified file |
| SCAN-04 | Unicode / non-ASCII check | `grep -Pn "[^\x00-\x7F]" src/PropTraderTools/CopyEngine.cs` | **0 matches** in modified file (all inserted text is 7-bit ASCII) |
| SCAN-05 | CYC baseline unchanged | Compare `dotnet test` pass count before and after edit | **Passed: 24** before and after — no test count change indicates no CYC-introduced failure |
| SCAN-06 | ObfuscationAttribute duplicate check | `grep -n "ObfuscationAttribute" src/PropTraderTools/CopyEngine.cs` | **Exactly 3 matches** — one per decorated member; no pre-existing decorations on any other member |
| SCAN-07 | deploy-sync.ps1 hard-link sync | `powershell -File .\deploy-sync.ps1` | **Exit code 0** |

All 7 scans must PASS before the ticket is closed. A single scan failure is a blocking defect.

---

### Execution Order

1. Open `src/PropTraderTools/CopyEngine.cs`.
2. Navigate to **L1778**. Confirm the line reads: `        private static void LogBeSlotEviction(string accName, bool isRejected)`. Insert `        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` on the line immediately above.
3. Navigate to **L6422** (now ~L6423 after step 2 adds one line). Confirm the line reads: `        private void LogDiagOrderCount(Account acc, Instrument instrument)`. Insert `        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` on the line immediately above.
4. Navigate to **L6967** (now ~L6970 after steps 2–3 add two lines). Confirm the line reads: `        internal static string GetSenderAccountName(object sender)`. Insert `        [System.Reflection.ObfuscationAttribute(Feature = "rename", Exclude = true)]` on the line immediately above.
5. Save `CopyEngine.cs`.
6. Run: `powershell -File .\deploy-sync.ps1` — must exit 0.
7. Run: `dotnet test src/PropTraderTools/ --no-build --verbosity normal` — must output `Failed: 0, Passed: 24, Skipped: 490, Total: 514`.
8. Execute all 7 scans in the checklist above. All must pass.

---

### Deferred Items (do NOT touch in this ticket)

| ID | Title | Prerequisite |
|----|-------|-------------|
| DW-09-01 | Implement 70 unimplemented CopyEngine helper methods referenced by obfuscation-skipped tests | New epic — large scope |
| DW-09-02 | Fix `NonPublic\|Instance` → `NonPublic\|Static` binding flags in `BwaveCycTaR3HelperTests` for `LogBeSlotEviction` + remove 2 obfuscation skips | After DW-09-01 |
| DW-09-03 | Fix `NonPublic\|Instance` → `NonPublic\|Static` binding flags in `BwaveCycTaR2HelperTests` for `GetSenderAccountName` + remove 1 obfuscation skip | After DW-09-01 |
| DW-09-04 | Remove all 137 remaining obfuscation skips after DW-09-01 + DW-09-02 + DW-09-03 are complete | After all three prior items |

**Do not modify any test file. Do not remove any `Skip` annotation. Do not implement any of the 70 missing methods. This ticket is limited to 3 attribute-line insertions only.**

---

### Ticket Summary

| Field | Value |
|-------|-------|
| Ticket ID | T1 |
| File | `src/PropTraderTools/CopyEngine.cs` |
| Change type | Pure attribute insertion (3 lines added, no lines modified or deleted) |
| Tests added | 0 |
| Skips removed | 0 |
| Signatures changed | 0 |
| Expected post-edit test counts | Failed: 0, Passed: 24, Skipped: 490, Total: 514 |
| Blocks | DW-09-02, DW-09-03, DW-09-04 (unlocks skip removal once prerequisites are met) |
| Blocked by | Nothing |

---

## RETURN VALUE

**TICKETS_COMPLETE**
