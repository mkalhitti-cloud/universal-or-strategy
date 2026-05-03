---
description: Emergency procedure to nuke local environment and reinstall dependencies.
---

# Nuke & Reinstall Workflow

1.  **Backup State**: Save any uncommitted `.env` or local logs.
2.  **Nuke Folders**: Run `rm -rf node_modules package-lock.json .next bin obj`.
3.  **Clear Cache**: Run `npm cache clean --force`.
4.  **Reinstall**: Run `npm install` (or project-specific installer).
5.  **Verify**: Run the project's sanity check (e.g., `npm run test`).

---

## Post-Use Audit (NON-NEGOTIABLE)

After EVERY use of this workflow, the executing agent MUST perform a post-use audit:

1. **Did the nuke command leave any stale artifacts behind?** Update the command if so.
2. **Did the reinstall fail?** Identify missing system dependencies.
3. **Was the sanity check insufficient?** Add more rigorous verification steps.

**If no gap found, state:** `workflow(nuke-reinstall): no gaps identified.`

**Commit format:** `workflow(nuke-reinstall): [what was fixed and why]`
