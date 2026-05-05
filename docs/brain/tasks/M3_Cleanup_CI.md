# Task: M3-Post-Merge-Cleanup
## Status: 🔴 OPEN

### Overview
PR #76 has been merged, closing the Phase 4 Dispatcher Hardening mission. However, the automated CI checks (SonarCloud and DeepSource) remain in a failing state due to infrastructure gaps and pre-existing style debt. This task tracks the resolution of these items to restore baseline health.

---

### 1. SonarCloud Infrastructure Fix
**Goal**: Restore compilation visibility for SonarCloud in GitHub Actions.
**Root Cause**: The cloud runner lacks `NinjaTrader.Core` and `NinjaTrader.NinjaScript` references, causing 634+ compilation errors.

- [ ] **Action**: Create a `ci-libs/` folder in the repository root.
- [ ] **Action**: Copy the following DLLs from a local NinjaTrader 8 installation (usually `C:\Program Files (x86)\NinjaTrader 8\bin\`) to `ci-libs/`:
  - `NinjaTrader.Core.dll`
  - `NinjaTrader.Gui.dll`
  - `NinjaTrader.Custom.dll`
  - `NinjaTrader.NinjaScript.dll`
- [ ] **Action**: Update `.github/workflows/sonarcloud.yml` to include the `ci-libs/` directory in the MSBuild search path via `-p:ReferencePath="ci-libs/"`.
- [ ] **Action**: Commit and verify SonarCloud scan passes.

---

### 2. DeepSource Code Hygiene PR
**Goal**: Clear the 4 "Introduced" style violations in `AccountOrders.cs`.
**Target File**: `src/V12_002.Orders.Callbacks.AccountOrders.cs`

- [ ] **Issue CS-R1136 (Line 456)**: Replace `OldOrder.OrderId` check with `OldOrder?.OrderId` or verify nullability.
- [ ] **Issue CS-R1105 (Line 529)**: Simplify if-else logic around `followerKeys` extraction.
- [ ] **Issue CS-R1136 (Line 611)**: Add null-conditional to instrument check: `order.Instrument?.FullName`.
- [ ] **Issue CS-R1018 (Line 613)**: Specify CultureInfo in `.ToUpper()` call: `.ToUpper(System.Globalization.CultureInfo.InvariantCulture)`.

---

### Verification Criteria
- [ ] SonarCloud status shows **Passed** on the next PR.
- [ ] DeepSource status shows **0 new issues** on the cleanup branch.
- [ ] `deploy-sync.ps1` runs locally without errors.
