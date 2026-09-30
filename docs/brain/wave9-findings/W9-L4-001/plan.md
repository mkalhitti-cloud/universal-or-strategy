# W9-L4-001 Fix Plan

## Identity

| Field | Value |
|-------|-------|
| **W9_ID** | W9-L4-001 |
| **File** | `src/V12_002.MetadataGuard.cs` |
| **Line** | 168 |
| **Priority** | P3 (non-hot-path) |
| **Fix type** | Comment-only -- no code change |

---

## Fix Rationale

Per P3 fix rule: method `MetadataGuardRepairAuthorized` is not reachable from
`OnBarUpdate`, `OnOrderUpdate`, or `OnExecutionUpdate` (confirmed by call graph in
`scan.md`). It fires at most once per REAPER repair cycle -- an infrequent deferred
event. OKF Rule 7 non-hot-path disposition: leave LINQ, add clarifying comment.

No allocation concern warrants code change at P3. A comment directly above the LINQ
call satisfies the wave audit requirement without touching executable logic.

---

## Exact Diff

### Before (lines 167-170 of `src/V12_002.MetadataGuard.cs`)

```csharp
            try
            {
                bool hasActiveFsm = _followerBrackets.Values.Any(f =>
                    f != null && f.AccountName == accountName && f.State == FollowerBracketState.Active
                );
```

### After

```csharp
            try
            {
                // not hot path -- LINQ acceptable
                bool hasActiveFsm = _followerBrackets.Values.Any(f =>
                    f != null && f.AccountName == accountName && f.State == FollowerBracketState.Active
                );
```

---

## Change Specification

| Field | Value |
|-------|-------|
| **File** | `src/V12_002.MetadataGuard.cs` |
| **Insert before line** | 168 |
| **Content to insert** | `                // not hot path -- LINQ acceptable` |
| **Lines changed** | 0 (comment insertion only, no executable change) |
| **Build impact** | none |
| **Test impact** | none |
| **Blast radius** | 0 callers affected |

---

## OKF Compliance

- Rule 7 (microsecond-eternity.md -- zero_alloc): satisfied -- comment acknowledges
  non-hot-path classification; no new allocation introduced.
- Rule 11 (ASCII only): comment text is ASCII-only. No Unicode characters.
- Rule 12 (naming): no identifier changes.
- Rule 6 (CYC <= 8): no cyclomatic complexity change.

---

## Verification Checklist

- [ ] `grep -n "not hot path" src/V12_002.MetadataGuard.cs` returns line 168
- [ ] `dotnet build` exits 0 (no compilation change)
- [ ] `grep -r "lock(" src/` returns 0 results (unchanged)
- [ ] `python scripts/wave9_ascii_check.py src/V12_002.MetadataGuard.cs` returns 0

---

## Status

`PLANNED` -- awaiting execution phase
