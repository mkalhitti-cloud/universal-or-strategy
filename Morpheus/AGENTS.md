# Morpheus Control Plane - Agent DNA

This directory (`Morpheus/`) contains the .NET 8+ backend for the Morpheus Control Plane. It is strictly isolated from the legacy .NET 4.8 NinjaTrader strategy in the repository root.

## Architecture Guidelines
- **Target Framework**: .NET 8.0 or higher.
- **Paradigm**: Lock-free asynchronous design. Use `Channel<T>` for producer/consumer workflows instead of `ConcurrentQueue` + locking.
- **Workflow**: Strict Test-Driven Development (TDD). The Validator agent MUST write failing tests defining the contract before the Worker agent implements the feature.
- **Testing**: xUnit with Moq for interfaces.

## Isolation Mandate
Do NOT reference legacy NinjaTrader assemblies (`NinjaTrader.Core`, etc.) or legacy files from `src/V12_002*` in this project. This is a standalone control plane.
