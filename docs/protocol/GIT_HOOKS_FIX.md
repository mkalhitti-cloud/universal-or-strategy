# Git Hooks Fix (2026-06-01)

## Issue
Bob Shell's post-commit hook was failing with error:
```
error: cannot spawn .git/hooks/post-commit: No such file or directory
```

## Root Cause
The hook script at `.git/hooks/post-commit` was missing the shebang line (`#!/bin/bash`) at the top of the file. Lines 1-2 were empty, causing Git for Windows to fail when trying to execute the script.

## Fix Applied
1. Added shebang line: `#!/bin/bash`
2. Removed leading blank lines (lines 1-2 were empty)
3. Ensured shebang is on line 1 (Git requirement)

PowerShell command used:
```powershell
$content = Get-Content .git/hooks/post-commit -Raw
$content = $content.TrimStart()
Set-Content .git/hooks/post-commit -Value $content -NoNewline
```

## Verification
- Git Bash exists at: `C:\Program Files\Git\bin\bash.exe`
- Hook script is now executable by Git for Windows
- Bob Shell notes functionality restored

## Impact
- **Before**: Hook failed silently, Bob notes not attached to commits
- **After**: Hook executes successfully, git notes synced properly

## Related
- Bob Shell Integration: `BOB.md`
- Git Notes Reference: `refs/notes/bob`
- Pending Notes File: `.bob/notes/pending-notes.txt`