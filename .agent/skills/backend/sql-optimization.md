---
name: SQL Optimization
description: >
  High-performance database query and schema design guidelines.
---

# SQL Optimization (Professional Standard)

## Core Logic

- **Index Matching**: Ensure indexes cover the `WHERE`, `JOIN`, and `ORDER BY` clauses.
- **Selective Retrieval**: Avoid `SELECT *`. Only fetch columns needed for the execution phase.
- **Execution Plain Analysis**: Use `EXPLAIN ANALYZE` to identify nested loops or sequential scans.

## Instructions

1.  **Normalize for Hot Paths**: For high-velocity tick data, favor denormalized structures to minimize join latency.
2.  **Batch Operations**: Use bulk inserts/updates to minimize transaction overhead.
3.  **Connection Pooling**: Ensure the .NET host uses persistent connection pools to avoid handshake penalties.

## Mandatory Self-Improvement Audit

After EVERY skill use, perform this audit:

1. **Instruction Clarity**: Did any step produce an unexpected or ambiguous result?
2. **Trigger Coverage**: Is the description "pushy" enough?
3. **Gap Analysis**: If a gap is found, fix it immediately. Otherwise, state: `skill(sql-optimization): no gaps identified`.
