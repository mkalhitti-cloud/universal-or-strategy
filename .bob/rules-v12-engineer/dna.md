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
