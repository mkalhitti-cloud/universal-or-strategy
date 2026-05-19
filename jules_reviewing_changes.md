# Jules Reviewing Code Changes Guide

This document captures the code review, diff, and execution standards for **Jules CLI** (Backup Engineer #2) operating within the **V12 Universal OR Strategy** repository.

---

## 🧭 Real-Time Activity Feed
While Jules is executing a task in the VM, you can follow its progress in the activity feed, which logs:
- Step-by-step completions and detailed execution plans.
- Rationale behind specific architectural choices.
- Output, compilation, or validation errors.
- Real-time requests for user clarification.

---

## 🔎 Code Diffs & Code Copying
- **Mini Diff**: Embedded directly in the activity log for quick inline scanning.
- **Full Diff Editor**: Located on the right-hand panel of the dashboard. Shows only modified or newly created files. Drag or expand to full screen as needed.
- **Copying Updated Code**: Click the copy icon in the top right of the diff editor. This copies *only the actual, updated file content* to your clipboard, excluding diff tags (`+`, `-`).

---

## 💬 Interactive Feedback & Adjustments
You can direct Jules in real-time by chatting in the feedback pane. 
- Refine naming conventions (e.g. pascalCase vs. camelCase).
- Direct logic overrides (e.g., "use a synchronous atomic array write instead of Enqueue").
- Request additional regression checks or code cleanup before publishing.

---

## 🌿 Finalizing & Pushing to GitHub
Upon successful completion, Jules provides a summary:
- Modified files list, total VM runtime, and lines of code changed.
- **Publish Branch**: Creates a new remote branch with Jules listed as the commit author. You are the branch owner and can open a manual PR.
- **Publish PR**: Jules creates and publishes the PR automatically. It will appear on GitHub as authored by the Jules GitHub App.
