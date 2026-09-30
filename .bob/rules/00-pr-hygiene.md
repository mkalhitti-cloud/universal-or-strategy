# PR Hygiene Mandate

## Mandatory Protocol
All agents MUST adhere to the following PR hygiene rules before every push:

1. **Rebase Mandate**: Your branch MUST be rebased onto the latest `origin/main`.
2. **Hygiene Script**: You MUST run `powershell -File .\scripts\verify_pr_hygiene.ps1` before every push.
3. **PHS Loop**: You MUST run `/pr-loop <PR_NUMBER>` after every PR submission and commit to drive the Project Health Score to 100/100.
4. **No Dirty Branches**: If a branch is behind `main`, you MUST fix it immediately using `git fetch origin main && git rebase origin/main`.

Failure to follow this protocol is a V12 PR Hygiene violation.
