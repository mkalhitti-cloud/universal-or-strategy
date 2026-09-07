# V12 Photon Kernel DNA
## Mandatory Architectural Constraints

> [!IMPORTANT]
> These rules are non-negotiable and override any internal LLM tendencies.

### 1. No Internal Locks
Legacy `lock(stateLock)` blocks are **STRICTLY BANNED**. All state mutations must use the FSM/Actor `Enqueue` model or atomic primitives. If you see a lock, your first priority is to refactor it out.

### 2. ASCII-Only Compliance
NEVER use Unicode, emoji, or curly quotes in C# string literals.        
- Allowed: `(!)` `--` `->` `"` (straight)
- Banned: (!) -- -> " (curly)

### 3. Surgical File Splits
All file splits MUST use the Python extractor script (`scripts/v12_split.py`). Manual copy-paste is BANNED for any split exceeding 50 lines.    

### 4. FSM-Driven Execution
Any follower order cancel+resubmit MUST use the two-phase Replace FSM (`_followerReplaceSpecs` dict). NEVER cancel and submit directly.

### 5. Post-Edit Deployment
After every `src/` edit, you MUST run:
`powershell -File .\deploy-sync.ps1`
Verify that the ASCII gate passes before notifying the Orchestrator.    

### 6. Tool Protocol Integrity
NEVER use `<<<<<<< REPLACE`, `=======`, or `>>>>>>>` markers inside `write_to_file` or `replace_file_content` calls. These tools do not support diff formats.
- Use `replace_file_content` with exact `TargetContent`.
- Use `apply_diff` only when you are absolutely certain the diff syntax is supported by the specific tool instance.
- If a tool call fails to modify the file, DO NOT report success. Immediately retry using a different surgical tool.

### 7. Complexity Extraction Standards (PTT Standard)
All extracted sub-methods must adhere to the following metrics:
- **Target Complexity**: CYC <= 8 per method (Jane Street JS-080 strict standard).
- **Extraction Floor**: LOC >= 5 lines. (Deviations require explicit justification).
- **Zero Logic Drift**: Do not optimize or "improve" logic during extraction. Pure structural movement only.

### 8. Lizard CCN Measurement — MANDATORY CORRECT COMMAND (P0)

The lizard CSV column order is: **NLOC, CCN, Token, Params, Length, ...**
Column 1 is NLOC. Column 2 is CCN. This has caused planning failures when
the header mapping was wrong (Wave 1 incident: all targets were NLOC, not CCN).

**ONLY use this command. Never modify the header order:**

```powershell
lizard src/PropTraderTools/ -x "*/bin/*" -x "*/obj/*" -x "*Tests*" --csv |
ConvertFrom-Csv -Header NLOC,CCN,Token,Params,Length,Location,File,Function,Sig,Start,End |
Where-Object {[int]$_.CCN -gt 8} |
Sort-Object {[int]$_.CCN} -Descending |
Select-Object CCN, Function, @{L="File";E={[IO.Path]::GetFileName($_.File)}} |
Format-Table -AutoSize
```

Expected when fully compliant: **zero rows**.

**Sanity check**: if any reported CCN value is > 30 for a method under 30 lines,
you are reading NLOC. Cross-check with `lizard <file>` text mode (col 2 = CCN).

Full protocol: `docs/protocol/LIZARD_CCN_PROTOCOL.md`

### 8. Empty-Catch Exemption Table (T-Q1)
When sweeping for empty `catch {}` blocks, the following sites are PERMANENTLY EXEMPT:
- `src/V12_002.MetadataGuard.cs` -- 6x `catch { return true; }` = intentional fail-open guards on validation predicates.
- `src/V12_002.Photon.MmioMirror.cs` -- 2x Dispose-pattern catches (best-effort cleanup, no re-throw needed).
All other empty catches in src/ MUST be logged. Add `catch (Exception ex) { Print("[MODULE] <context>: " + ex.Message); }` with ASCII-only module tag.
If a new Dispose-pattern site is found, document it with a one-line rationale comment and add to this table -- do NOT silently skip it.
