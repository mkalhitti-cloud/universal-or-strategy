# Graphify Protocol (V12.33 — Every Task)

## Mandatory Rule — All Modes, All Agents

graphify is a CLI knowledge graph tool that gives every agent a structural view of the codebase.
It reduces token costs by 71x compared to raw file reading and surfaces god nodes, communities,
and surprising connections that jCodemunch alone does not show.

## STARTUP (First action of every task)

The `pre_task_graphify_staleness.py` hook runs automatically before every task.
It compares `git rev-parse HEAD` against the SHA stored in `.graphify/graph.json`:
- **If fresh** (SHA matches): no-op, exits in ~0.1s
- **If stale** (SHA differs or no graph exists): runs `graphify update . --no-cluster --no-description` automatically

After the hook completes (or if running manually), read `.graphify/GRAPH_REPORT.md` for:
- **God nodes** (highest-connectivity symbols — your extraction targets)
- **Communities** (logical clusters — boundaries to respect)
- **Surprising connections** (hidden coupling you must not break)

If you need to check staleness manually before the hook runs:
```bash
# Check: does graph SHA match HEAD?
python -c "import json; g=json.load(open('.graphify/graph.json')); print(g.get('metadata',g).get('git_sha','unknown'))"
git rev-parse HEAD
# If they differ — run update:
graphify update . --no-cluster --no-description
```

## SHUTDOWN (Last action of every task, after any file edits)

Run AFTER all file modifications are complete:

```bash
graphify update . --no-cluster --no-description
```

This keeps the graph fresh for the next agent that picks up the task.

## Query Commands (use INSTEAD of grepping raw files)

```bash
graphify query "<question>"          # scoped subgraph — much cheaper than GRAPH_REPORT.md
graphify path "<SymbolA>" "<SymbolB>" # shortest dependency path between two nodes
graphify explain "<concept>"          # explain a concept via graph traversal
graphify summary --graph .graphify/graph.json  # compact first-hop orientation
```

## File Locations

- Graph data:   `.graphify/graph.json`
- Report:       `.graphify/GRAPH_REPORT.md`
- Skills:       `.claude/skills/graphify/SKILL.md`

## Enforcement

- ❌ NEVER skip the startup `graphify update` — stale graph = wrong extraction targets
- ❌ NEVER skip the shutdown `graphify update` — next agent inherits stale data
- ❌ NEVER reference `graphify-out/` — that is the legacy path, now migrated to `.graphify/`
- ✅ The `--no-cluster --no-description` flags keep it fast (~19 seconds, AST-only)
- ✅ Full cluster rebuild (`graphify update .`) only needed for wave-level architectural review

## Speed Reference

| Mode | Time | When |
|------|------|------|
| `graphify update . --no-cluster --no-description` | ~19s | Every task start/end |
| `graphify update .` | ~30-60s | Wave-level review only |
