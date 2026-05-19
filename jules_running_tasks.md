# Running Tasks with Jules Guide

This document captures the official execution and prompt standards for **Jules CLI** (Backup Engineer #2) operating within the **V12 Universal OR Strategy** repository.

---

## 🌿 Choose Repo and Branch
- Log into the Jules portal.
- Select the `mkalhitti-cloud/universal-or-strategy` repository from the codebase dropdown.
- Select the target branch (e.g. `main` or your custom feature/hotfix branch) to base changes on.

---

## ✍️ Prompt Engineering Standards

Jules performs best when given specific, scoped, and well-structured prompts.

### 🖼️ Visual Context Uploads
- You can upload PNG or JPEG images (under 5MB total) during the initial task setup to provide visual context (e.g., UI panels, logs, or architectural diagrams).
- Note: Visuals are only ingestible during **initial task creation**, not in follow-up chat messages.

### 💡 High-Quality Prompt Examples

| Style | 🟢 Good (Specific, Scoped) | ❌ Bad (Generic, Vague) |
| :--- | :--- | :--- |
| **Logic** | "Add empty-catch block logging for `ShouldSkipFleetAccount`" | "Fix everything in the fleet logic" |
| **Optimization** | "Convert CIT_Repair method allocations from LINQ to a for-loop" | "Optimize the whole Cit component" |
| **Doc** | "Document `V12_002` expected positions with XML comments" | "Make comments better" |

---

## 👁️ Monitoring Task Execution
As Jules works on a task, you can monitor:
1. **Activity Feed**: Real-time updates as each architectural or logic sub-step completes.
2. **Inline Explanations**: A walkthrough of each code modification.
3. **Mini Diff Previews**: Inline file changes. Click to open the full side-by-side diff editor.

---

## 🔄 Mid-Task Intervention & Pausing
- **Mid-Task Chat**: You can enter instructions or request changes at any time. Jules will replan and adjust its approach dynamically.
- **Pausing**: Click the **Pause** button to halt execution. When paused, Jules holds all state and waits for explicit resume or new instructions.

---

## 🏷️ Starting Tasks from GitHub Issues
To launch Jules autonomously via GitHub:
1. Ensure the **Jules GitHub App** is authorized on the repository.
2. Apply the label `jules` (case-insensitive) to any open issue.
3. Jules will automatically comment on the issue to acknowledge, execute the task, and submit a PR back to the repository upon completion.
