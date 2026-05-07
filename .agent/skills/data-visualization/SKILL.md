---
name: Data Visualization
description: >
  Professional data storytelling and performance metric visualization using Python (Matplotlib, Plotly, Seaborn).
  Follows accessibility and professional design standards.
---

# Data Visualization Skill

You are a **Data Visualization** specialist. Use this skill to create dashboards and reports for the V12 strategy's performance and audit metrics.

## I. Chart Selection Principles
- **Bar Charts**: Use for comparing categories (e.g., latency by account).
- **Line Charts**: Use for time-series data (e.g., equity curve, CPU usage over time).
- **Scatter Plots**: Use for correlation (e.g., slippage vs. order size).
- **BANNED**: 3D charts, pie charts (use treemaps or bars instead).

## II. Styling & Accessibility
- **Palette**: Use colorblind-friendly palettes (e.g., Viridis, Colorcet).
- **Fonts**: Use legible, modern typography (Inter, Roboto).
- **Clutter**: Remove chartjunk (unnecessary gridlines, borders).
- **Context**: Every chart MUST have a title, axis labels, and a clear legend.

## III. Python Coding Patterns
Use the standard templates in `scripts/visualize/` for:
- Performance Heatmaps
- Latency Distribution (Histograms)
- Audit Matrix Visualization

---

## When to use this skill
- Generating P7 Sentinel reports.
- Creating "Arena Dashboards" (`arena_dashboard.html`).
- Visualizing stress test results from `test_stress.ps1`.

---

## Mandatory Post-Use Self-Improvement Audit (NON-NEGOTIABLE)

After completing any session using this skill, perform an audit:

1. Did any instruction produce an unexpected result or confusion?
2. Was any rule ambiguous enough that you had to make a judgment call?
3. Was a step missing that caused backtracking?
4. Is any reference file out of date?

If yes to any: **update this SKILL.md or references/ file immediately**, then commit:
skill(data-visualization): [what was fixed]

If no gaps found: state skill(data-visualization): no gaps identified. in your response.
No Director approval required for skill-only edits.
