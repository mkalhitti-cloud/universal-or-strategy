# Jules Suggested Tasks Guide

This document captures the configuration and execution standards for **Jules CLI** (Backup Engineer #2) operating within the **V12 Universal OR Strategy** repository.

---

## 🔍 Overview of Suggested Tasks
Jules can autonomously scan the repository to identify structural areas for improvement and propose detailed implementation plans. These are presented on your dashboard as **Suggested Tasks**.

- **Current Scope**: The experimental version focused on scanning for `#TODO` or `//TODO` comments left in the codebase, determining if they describe a resolvable issue, and proposing an immediate fix.
- **Notification**: You will receive periodic email digests when new suggestions are discovered.

---

## ⚙️ Enabling Suggested Tasks (Proactivity)
To allow Jules to autonomously scan the codebase:
1. Navigate to the **Codebases** panel in the left sidebar of the Jules dashboard.
2. Select the `mkalhitti-cloud/universal-or-strategy` codebase.
3. Open the **Proactivity** tab.
4. Toggle Proactivity to **On**. Jules will initiate the initial codebase scan immediately.

*Note: At launch, you can enable Proactivity on a maximum of 5 repositories simultaneously.*

---

## 🚦 Reviewing and Executing Suggestions
1. **Review Suggestions**: Read the suggestion card to view the detected code context, rationale, and proposed plan.
2. **Execute**: If the plan is correct, click **Start** to trigger the automated VM worker to implement the fix.
3. **Dismissal/Feedback**: If the suggestion is irrelevant or low priority, dismiss the card. Jules records this choice to refine its future heuristic scans.
