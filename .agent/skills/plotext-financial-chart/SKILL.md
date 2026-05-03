---
name: plotext-financial-chart
description: ASCII financial line charts for markdown using plotext dot marker. TRIGGERS - financial chart, line chart, plotext, price chart, trading chart, ASCII chart.
allowed-tools: Read, Bash, Write, Edit
---

# Plotext Financial Chart Skill

Create ASCII financial line charts for GitHub Flavored Markdown using plotext with dot marker (`•`). Pure text output — renders correctly on GitHub, terminals, and all monospace environments.

## When to Use This Skill

- Adding price path / line chart diagrams to markdown documentation
- Visualizing trading concepts (barriers, thresholds, entry/exit levels)
- Any GFM markdown file needing financial data visualization

## Mandatory Settings

Every chart MUST use these settings:

| Setting         | Code                 | Почему                |
| --------------- | -------------------- | --------------------- |
| Reset state     | `plt.clear_figure()` | Prevent stale data    |
| Dot marker      | `marker="dot"`       | GitHub-safe alignment |
| No color        | `plt.theme("clear")` | Clean text output     |
| Build as string | `plt.build()`        | Not `plt.show()`      |

## Quick Start

```python
import plotext as plt
plt.clear_figure()
plt.plot(x, y, marker="dot")
plt.plotsize(65, 22)
plt.theme("clear")
print(plt.build())
```

## Mandatory Checklist

- [ ] `plt.clear_figure()` — Reset state
- [ ] `marker="dot"` — Dot marker for GitHub
- [ ] `plt.theme("clear")` — No ANSI codes
- [ ] `plt.plotsize(65, 22)` — Fits 80-col code blocks
