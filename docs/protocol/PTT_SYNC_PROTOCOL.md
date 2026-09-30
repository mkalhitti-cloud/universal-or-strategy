# PTT Sync Protocol (V12.B95)

**Effective**: 2026-08-xx (V12.B95)  
**Supersedes**: `deploy-sync.ps1` reference in AGENTS.md (archived, non-functional)  
**Root cause fixed**: `83dcc6b0` — DW-B95/B96 dispatch+BE-ALL outage where commit was
applied but NT8 was never recompiled, leaving it running 61-minute-old code.

---

## The Problem This Protocol Solves

Committing a `.cs` fix to `src/PropTraderTools/` does **not** automatically activate that
fix in NinjaTrader 8. NT8 loads source from its own AddOns folder:

```
C:\Users\<user>\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\
```

Two separate steps are required after every commit touching `src/PropTraderTools/`:

1. **File copy** — sync the `.cs` files into NT8's AddOns folder  
2. **Recompile** — press F5 in NT8 (or Tools → Edit NinjaScript → Compile)

Missing either step means NT8 runs stale code silently. No error is shown.

---

## Mandatory Post-Commit Sequence

After every commit that touches `src/PropTraderTools/*.cs`:

```powershell
# Step 1: Sync and verify
powershell -File scripts\ptt-sync-and-verify.ps1

# Step 2: Compile in NT8
# Press F5 in NinjaTrader 8
# OR: Tools -> Edit NinjaScript -> Compile
```

The script prints a green `PASS` when all files match, and a red `FAIL` with a file list
if any file failed to copy. Always fix FAIL before compiling.

---

## Script Reference

### `scripts/ptt-sync-and-verify.ps1` (PRIMARY — use this)

**What it does**:
1. Copies all production `.cs` files from `src/PropTraderTools/` to the NT8 AddOns folder,
   skipping test files and obj/bin directories.
2. Re-hashes every file after copy (MD5) and prints OK or MISMATCH per file.
3. On success: prints green PASS + mandatory compile reminder.
4. On failure: exits with code 1 + list of mismatched files.

**When to run**: After every commit that touches `src/PropTraderTools/*.cs`.

**Excludes** (never synced to NT8):
- Files in `Tests/`, `obj/`, `bin/` subdirectories
- `*Tests.cs`, `CopyEngineTests.cs`, `*.bak`

---

### `scripts/sync-ptt-to-nt8.ps1` (LEGACY — copy only, no verify)

The original sync script. Still functional. Use `ptt-sync-and-verify.ps1` instead;
it provides the same copy logic plus the MD5 verification phase.

---

### `.bob/hooks/post_commit_sync_check.py` (POST-COMMIT HOOK)

Runs automatically after every `git commit` in Bob IDE.

**What it does**:
1. Checks which `src/PropTraderTools/*.cs` files were changed in the last commit.
2. MD5-compares each changed file against the NT8 AddOns copy.
3. If any mismatch: prints a loud warning with the exact fix command.
4. Exit 1 = warn only (non-blocking gate). Does not abort the commit.

**Note**: This hook warns but does not auto-sync. NT8 may need to be closed before
a file can be overwritten (file lock).

---

## NT8 File Locations

| Purpose | Path |
|---------|------|
| NT8 AddOns source (live) | `%USERPROFILE%\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\` |
| NT8 compiled DLL indicator | `%USERPROFILE%\Documents\NinjaTrader 8\bin\Custom\NinjaTrader.Custom.dll` |
| Repo source | `src/PropTraderTools/` |
| Sync script | `scripts/ptt-sync-and-verify.ps1` |
| Post-commit hook | `.bob/hooks/post_commit_sync_check.py` |

---

## Diagnosing Stale State

If a fix was committed but NT8 behaviour hasn't changed:

```powershell
# 1. Check whether NT8 has the latest file
Get-FileHash "src\PropTraderTools\CopyEngine.cs" -Algorithm MD5
Get-FileHash "$env:USERPROFILE\Documents\NinjaTrader 8\bin\Custom\AddOns\PropTraderTools\CopyEngine.cs" -Algorithm MD5
# If hashes differ: run ptt-sync-and-verify.ps1, then F5.

# 2. Check when NT8 last compiled
Get-Item "$env:USERPROFILE\Documents\NinjaTrader 8\bin\Custom\NinjaTrader.Custom.dll" | Select-Object LastWriteTime
# Compare to commit timestamp. If DLL is older: F5 required.
```

---

## Enforcement Chain

| Layer | Mechanism | Blocks? |
|-------|-----------|---------|
| AGENTS.md L155 | "NT8 Sync Integrity (V12.B95)" rule | Advisory — every agent must follow |
| Post-commit hook | `.bob/hooks/post_commit_sync_check.py` | Warns (exit 1, non-blocking) |
| Epic completion checklist | AGENTS.md L622 | Manual gate per epic |
| This document | Authoritative SOP | Reference |

---

## History

| Date | Event |
|------|-------|
| 2026-08-10 (approx) | DW-B95/B96 outage: `83dcc6b0` committed but NT8 never synced; 61-min-old code running |
| 2026-08-10 | Root cause diagnosed; MD5 mismatch confirmed; manual sync performed |
| 2026-08-10 | `scripts/ptt-sync-and-verify.ps1` created |
| 2026-08-10 | `.bob/hooks/post_commit_sync_check.py` created |
| 2026-08-10 | AGENTS.md: `deploy-sync.ps1` references replaced with correct script |
| 2026-08-10 | `NO-PIPELINE-REPAIRS.md` PRE-EXISTING-03: CLOSED |

---

**Author**: copier-spec session V12.B95  
**Protocol version**: 1.0
