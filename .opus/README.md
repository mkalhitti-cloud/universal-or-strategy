# Opus Skills & Context

This directory stores persistent context and skills for **Claude Opus** to assist with strategic planning and architecture for the UniversalORStrategy project.

## Purpose
Opus is the **strategic advisor** in the multi-AI workflow. While other assistants handle implementation details, Opus focuses on:
- High-level system design
- Long-term maintainability
- Risk assessment and edge case analysis
- Trade-off evaluation

## Structure
- `skills/`: Reusable knowledge bases and patterns.
  - `core/`: NinjaTrader 8 development patterns and best practices.
  - `trading/`: Strategy-specific trading logic (ORB, RMA, etc.).
  - `project-specific/`: Context for this specific codebase.
  - `references/`: API docs, critical bug fixes, and specifications.
  - `changelog/`: Major architectural changes and decisions.
- `context/`: Dynamic session information and strategic focus.

## Usage
Reference these files when engaging Opus for architectural discussions, planning sessions, or complex debugging that requires a systemic view.
