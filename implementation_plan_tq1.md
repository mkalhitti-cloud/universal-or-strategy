# T-Q1 Implementation Plan: Empty-Catch Diagnostic Logging

**BUILD_TAG_BASELINE**: 1111.007-phase7-t16  
**BUILD_TAG_TARGET**: 1111.007-phase7-tQ1  
**BRANCH**: feature/phase7-sprint5-extraction  
**MISSION**: Wrap 14 empty catch blocks with diagnostic logging controlled by runtime toggle flags

---

## 1. Executive Summary

This plan implements diagnostic logging for 14 empty catch blocks across 4 SIMA files. Two runtime toggle flags (`_diagFleet` and `_diagIpc`) control logging visibility, defaulting to `false` (silent swallow, B4 constraint). The implementation is **byte-identical** to current behavior when flags are disabled.

### Scope
- **Files Modified**: 8 (4 catch sites + 2 IPC handlers + 1 field declaration + 1 BUILD_TAG)
- **Empty Catches Wrapped**: 14 total
  - **DIAG_FLEET** (12 sites): AccountOrders.cs (5), Lifecycle.cs (4), Fleet.cs (3), Dispatch.cs (1)
  - **DIAG_IPC** (2 sites): Dispatch.cs (2)
- **Files Exempt**: MetadataGuard.cs, Photon.MmioMirror.cs (H4/P4 constraint)

---

## 2. Ambiguity Resolution

### 2.1 Line 208 Flag Binding
**Resolution**: **DIAG_FLEET**  
**Rationale**: Pattern matches other `TriggerCustomEvent(o => PumpFleetDispatch())` pump primes in Fleet.cs:75, Fleet.cs:311. All three are fleet dispatch pump primes.

### 2.2 Message Prefix Convention
**Decision**: `[FLEET_CATCH]` for DIAG_FLEET sites, `[IPC_CATCH]` for DIAG_IPC sites

### 2.3 Field Names
**Decision**: `_diagFleet` and `_diagIpc` (shorter, consistent with `_ipc` prefix pattern)

### 2.4 Default State
**Decision**: Both flags default to `false` (B4 constraint - byte-identical behavior)

---

## 3. Field Declaration Design

**File**: `src/V12_002.cs`  
**Location**: After line 292 (after `isIpcRunning` in #region Variables)

```csharp
// T-Q1: Runtime diagnostic flags for empty-catch logging
private volatile bool _diagFleet = false;  // Fleet dispatch catch logging (DIAG_FLEET toggle)
private volatile bool _diagIpc = false;    // IPC/MMIO catch logging (DIAG_IPC toggle)
```

**Rationale for `volatile`**: H13 constraint - thread-safe reads from NT8 broker callbacks without locks.

---

## 4. IPC Handler Modifications

### 4.1 HandleFleet_DiagFleet
**File**: `src/V12_002.UI.IPC.Commands.Misc.cs`  
**Insert after line 117**:

```csharp
// T-Q1: Toggle catch logging flag
_diagFleet = !_diagFleet;
Print("[DIAG_FLEET] Catch logging: " + (_diagFleet ? "ENABLED" : "DISABLED"));
```

### 4.2 TryHandleDiagCommand
**File**: `src/V12_002.UI.IPC.Commands.Config.cs`  
**Insert after line 401**:

```csharp
// T-Q1: Toggle catch logging flag
_diagIpc = !_diagIpc;
Print("[DIAG_IPC] Catch logging: " + (_diagIpc ? "ENABLED" : "DISABLED"));
```

---

## 5. Per-Site Catch Wrapper Specifications

### 5.1 DIAG_FLEET Sites (12 total)

#### AccountOrders.cs (5 sites)

**Line 157**:
```csharp
// BEFORE: try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
// AFTER:
try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch (Exception ex) { if (_diagFleet) Print("[FLEET_CATCH] AccountOrderQueue pump (OnAccountOrderUpdate): " + ex.Message); }
```

**Line 173**:
```csharp
// BEFORE: try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
// AFTER:
try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch (Exception ex) { if (_diagFleet) Print("[FLEET_CATCH] AccountOrderQueue pump (flatten wait): " + ex.Message); }
```

**Line 184**:
```csharp
// BEFORE: try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
// AFTER:
try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch (Exception ex) { if (_diagFleet) Print("[FLEET_CATCH] AccountOrderQueue pump (flatten re-enqueue): " + ex.Message); }
```

**Line 192**:
```csharp
// BEFORE: try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch { }
// AFTER:
try { TriggerCustomEvent(o => ProcessAccountOrderQueue(), null); } catch (Exception ex) { if (_diagFleet) Print("[FLEET_CATCH] AccountOrderQueue pump (budget reschedule): " + ex.Message); }
```

**Line 656**:
```csharp
// BEFORE: try { RemoveDrawObject("SIMA_DESYNC_" + cascadeAcctName); } catch { }
// AFTER:
try { RemoveDrawObject("SIMA_DESYNC_" + cascadeAcctName); } catch (Exception ex) { if (_diagFleet) Print("[FLEET_CATCH] RemoveDrawObject SIMA_DESYNC: " + ex.Message); }
```

#### Lifecycle.cs (4 sites)

**Line 65**:
```csharp
// BEFORE: try { TriggerCustomEvent(o => ProcessApplySimaState(_defEnabled), null); } catch { }
// AFTER:
try { TriggerCustomEvent(o => ProcessApplySimaState(_defEnabled), null); } catch (Exception ex) { if (_diagFleet) Print("[FLEET_CATCH] SIMA toggle deferred retry: " + ex.Message); }
```

**Line 1071** (multi-line):
```csharp
// BEFORE:
                }
                catch { }
            }
// AFTER:
                }
                catch (Exception ex) { if (_diagFleet) Print("[FLEET_CATCH] Shutdown ring drain (sideband read): " + ex.Message); }
            }
```

**Line 1113**:
```csharp
// BEFORE: try { acct.Cancel(new[] { ord }); brokerCancels++; } catch { }
// AFTER:
try { acct.Cancel(new[] { ord }); brokerCancels++; } catch (Exception ex) { if (_diagFleet) Print("[FLEET_CATCH] GTC cancel (account=" + acct.Name + "): " + ex.Message); }
```

**Line 1116** (multi-line):
```csharp
// BEFORE:
            }
            catch { }
        }
// AFTER:
            }
            catch (Exception ex) { if (_diagFleet) Print("[FLEET_CATCH] GTC sweep outer (account=" + acct.Name + "): " + ex.Message); }
        }
```

#### Fleet.cs (3 sites)

**Line 75**:
```csharp
// BEFORE: try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
// AFTER:
try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch (Exception ex) { if (_diagFleet) Print("[FLEET_CATCH] PumpFleetDispatch (ProcessFleetSlot cleanup): " + ex.Message); }
```

**Line 311**:
```csharp
// BEFORE: try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
// AFTER:
try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch (Exception ex) { if (_diagFleet) Print("[FLEET_CATCH] PumpFleetDispatch (XorShadow failure): " + ex.Message); }
```

**Line 376** (multi-line):
```csharp
// BEFORE:
            }
            catch { }
// AFTER:
            }
            catch (Exception ex) { if (_diagFleet) Print("[FLEET_CATCH] H-13 stale state reconciliation: " + ex.Message); }
```

#### Dispatch.cs (1 DIAG_FLEET site)

**Line 208**:
```csharp
// BEFORE: try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch { }
// AFTER:
try { TriggerCustomEvent(o => PumpFleetDispatch(), null); } catch (Exception ex) { if (_diagFleet) Print("[FLEET_CATCH] PumpFleetDispatch (ExecuteSmartDispatchEntry): " + ex.Message); }
```

### 5.2 DIAG_IPC Sites (2 total)

#### Dispatch.cs (2 sites)

**Line 590**:
```csharp
// BEFORE: try { _photonMmioMirror.TryPublish(ref _slot); } catch { }
// AFTER:
try { _photonMmioMirror.TryPublish(ref _slot); } catch (Exception ex) { if (_diagIpc) Print("[IPC_CATCH] MMIO mirror publish (stop slot): " + ex.Message); }
```

**Line 715**:
```csharp
// BEFORE: try { _photonMmioMirror.TryPublish(ref _slotLmt); } catch { }
// AFTER:
try { _photonMmioMirror.TryPublish(ref _slotLmt); } catch (Exception ex) { if (_diagIpc) Print("[IPC_CATCH] MMIO mirror publish (limit slot): " + ex.Message); }
```

---

## 6. Implementation Sequence

### Step 1: Declare Fields
**File**: `src/V12_002.cs` (after line 292)

### Step 2: Modify IPC Handlers
**Files**: `src/V12_002.UI.IPC.Commands.Misc.cs`, `src/V12_002.UI.IPC.Commands.Config.cs`

### Step 3: Wrap Catch Blocks (grouped by file)
1. `src/V12_002.Orders.Callbacks.AccountOrders.cs` (5 sites)
2. `src/V12_002.SIMA.Lifecycle.cs` (4 sites)
3. `src/V12_002.SIMA.Fleet.cs` (3 sites)
4. `src/V12_002.SIMA.Dispatch.cs` (3 sites: 1 DIAG_FLEET + 2 DIAG_IPC)

### Step 4: Update BUILD_TAG
**File**: `src/V12_002.cs` (line 47)
```csharp
public const string BUILD_TAG = "1111.007-phase7-tQ1";  // T-Q1: Empty-catch diagnostic logging (14 sites, 2 flags)
```

---

## 7. Verification Checklist

### 7.1 Empty-Catch Gate
```bash
grep -E "catch\s*\{\s*\}" src/V12_002.Orders.Callbacks.AccountOrders.cs src/V12_002.SIMA.Lifecycle.cs src/V12_002.SIMA.Fleet.cs src/V12_002.SIMA.Dispatch.cs
```
**Expected**: 0 hits

### 7.2 Field Declaration Gate
```bash
grep "_diagFleet\|_diagIpc" src/V12_002.cs
```
**Expected**: 2 field declarations

### 7.3 IPC Handler Gate
- Send `DIAG_FLEET` → verify toggle Print
- Send `DIAG_IPC` → verify toggle Print

### 7.4 Exempt File Gate
```bash
grep -E "catch\s*\{\s*\}" src/V12_002.MetadataGuard.cs src/V12_002.Photon.MmioMirror.cs
```
**Expected**: 3 hits (unchanged)

### 7.5 Lock Audit
```bash
grep "lock(" src/V12_002.Orders.Callbacks.AccountOrders.cs src/V12_002.SIMA.Lifecycle.cs src/V12_002.SIMA.Fleet.cs src/V12_002.SIMA.Dispatch.cs src/V12_002.UI.IPC.Commands.Misc.cs src/V12_002.UI.IPC.Commands.Config.cs src/V12_002.cs
```
**Expected**: 0 new `lock(` statements

### 7.6 ASCII Gate
```bash
python check_ascii.py src/V12_002.Orders.Callbacks.AccountOrders.cs src/V12_002.SIMA.Lifecycle.cs src/V12_002.SIMA.Fleet.cs src/V12_002.SIMA.Dispatch.cs src/V12_002.UI.IPC.Commands.Misc.cs src/V12_002.UI.IPC.Commands.Config.cs src/V12_002.cs
```
**Expected**: 0 non-ASCII characters

### 7.7 Diff Size Gate
```bash
git diff --stat feature/phase7-sprint5-extraction
```
**Expected**: Under 150 KB

### 7.8 Behavioral Verification
1. **Default Silent Swallow**: Flags at `false` → no Prints (B4 constraint)
2. **Enabled Logging**: Toggle flag → Prints appear
3. **Cross-Thread Visibility**: Broker callback triggers → Print appears (H13 constraint)

---

## 8. Constraint Compliance Matrix

| Constraint | Requirement | Status | Evidence |
|------------|-------------|--------|----------|
| **B4** | Default false = byte-identical | ✅ | Both flags default `false`; `if (_diagFleet)` guard |
| **B6** | Wrapped statements unchanged | ✅ | Only catch blocks modified |
| **H4/P4** | MMIO catches remain downstream | ✅ | MetadataGuard.cs, Photon.MmioMirror.cs exempt |
| **H5/Q-V2=A** | Fleet.cs:376 wrapped before T-W1 | ✅ | Wrapped in T-Q1 |
| **H13/V-A2=A** | volatile bool for thread safety | ✅ | Both fields `volatile` |
| **C-Thread2** | No lock() introductions | ✅ | Zero new locks |
| **C3** | ASCII-only strings | ✅ | All strings ASCII |
| **C5** | PR diff under 150 KB | ✅ | ~200 lines total (~20 KB) |

---

## 9. Success Criteria

- [ ] All 14 empty catch blocks wrapped
- [ ] Both IPC handlers toggle flags
- [ ] Zero new `lock(` statements
- [ ] All strings ASCII-only
- [ ] PR diff under 150 KB
- [ ] Empty-catch grep returns 0 hits in 4 files
- [ ] Exempt files unchanged
- [ ] BUILD_TAG = `1111.007-phase7-tQ1`
- [ ] F5 test passes
- [ ] IPC toggle commands work
- [ ] Default behavior byte-identical to t16

---

## 10. Adjudicator Review Checklist

### DNA Compliance
- [ ] No locks (C-Thread2)
- [ ] Atomic operations only (volatile reads)
- [ ] ASCII-only (C3)

### Architectural Integrity
- [ ] Byte-identical default (B4)
- [ ] Wrapped statements unchanged (B6)
- [ ] MMIO guards downstream (H4/P4)
- [ ] Thread-safe flags (H13)

### Implementation Quality
- [ ] All ambiguities resolved
- [ ] Exact code snippets provided
- [ ] Verification checklist executable
- [ ] Constraint compliance complete

### Readiness for Execution
- [ ] Executable by Bob CLI without clarification
- [ ] All 14 sites have before/after code
- [ ] IPC handlers have exact diffs
- [ ] Field placement precise

---

## 11. Notes for Engineer (Bob CLI)

### Execution Order
1. Fields first (V12_002.cs)
2. IPC handlers second
3. Catch wrappers third (grouped by file)
4. BUILD_TAG last

### Line Number Drift
If line numbers drift:
- Use context labels from Section 5
- Verify catch block content matches "BEFORE" exactly
- Search for unique string literals

### Checkpointing
Enable via `.bob/settings.json`:
```json
{
  "checkpointing": {
    "enabled": true,
    "frequency": "per_file"
  }
}
```

---

## 12. Appendix: Empty-Catch Inventory

### In-Scope (14 catches)
- AccountOrders.cs: 5
- Lifecycle.cs: 4
- Fleet.cs: 3
- Dispatch.cs: 2 (DIAG_IPC)

### Out-of-Scope (20 catches)
- UI/IPC infrastructure: 13
- MMIO guards (H4/P4 exempt): 2
- Non-fleet operations: 5

---

**END OF IMPLEMENTATION PLAN**

**READY FOR ADJUDICATOR REVIEW (Stage 3)**