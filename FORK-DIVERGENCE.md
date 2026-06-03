# Fork Divergence: 0_29_3 vs Upstream Master

**Generated:** May 27, 2026  
**Comparison:** `upstream/master` → `origin/0_29_3`

## Summary

- **Total files changed:** 68
- **Files added (unique to fork):** 17
- **Files modified:** 51
- **Files deleted:** 0
- **Net code change:** +5440 insertions, -457 deletions

---

## Files Added (Unique to Fork) — 17 Files

### Client-Side Diagnostics & Profiling (2 files)
Debugging and performance monitoring tools not present in upstream.

| File | Lines | Purpose |
|------|-------|---------|
| `LmpClient/Diagnostics/TsLoadProfiler.cs` | 194 | Performance profiling for timestamp loading |
| `LmpClient/Diagnostics/VesselSyncDiagnostics.cs` | 412 | Detailed vessel sync debugging and diagnostics |

### Harmony Patches (3 files)
Custom game patches for KSP engine behavior modifications.

| File | Lines | Purpose |
|------|-------|---------|
| `LmpClient/Harmony/DefaultDateTimeFormatter_ClampDateInputs.cs` | 145 | Date/time input clamping |
| `LmpClient/Harmony/KnowledgeBase_GetVesselCrewByAvailablePart.cs` | 95 | Crew knowledge base queries |
| `LmpClient/Harmony/Part_RegisterCrew.cs` | 83 | Part crew registration handling |

### Client Utilities (4 files)
Validation, tracking, and sanitization utilities.

| File | Lines | Purpose |
|------|-------|---------|
| `LmpClient/Systems/ShareContracts/ContractPartReferenceChecker.cs` | 47 | Contract part reference validation |
| `LmpClient/Systems/ShareContracts/ContractsScenarioSanitizer.cs` | 78 | Contract scenario data sanitization |
| `LmpClient/Systems/VesselProtoSys/LocalTopologyTracker.cs` | 170 | Vessel topology tracking for sync operations |
| `LmpClient/VesselUtilities/DiscoveryInfoSanitizer.cs` | 306 | Discovery info validation and cleanup |

### Server-Side Infrastructure (5 files)
Server logging, monitoring, and scenario management.

| File | Lines | Purpose |
|------|-------|---------|
| `Server/Log/CraftCreationAndRemovalLog.cs` | 177 | Audit log for vessel creation/removal events |
| `Server/System/MemoryDiagnosticsLogger.cs` | 112 | Memory usage monitoring and diagnostics |
| `Server/System/Scenario/ScenarioAchievementsCrewDedupe.cs` | 109 | Achievements deduplication for crew conflicts |
| `Server/System/Scenario/ScenarioContractsMigration.cs` | 155 | Contract migration and upgrade utilities |
| `Server/System/WarpAllotmentTracker.cs` | 264 | Warp time budget/allotment management system |

### Build & Deployment (2 files)
Build automation and server startup.

| File | Lines | Purpose |
|------|-------|---------|
| `Scripts/Build-Release.ps1` | 393 | Release build and packaging automation |
| `Server/StartLunaServer.bat` | 84 | Server startup batch script |

### Tests (1 file)
Unit test suite for scenario functionality.

| File | Lines | Purpose |
|------|-------|---------|
| `ServerTest/ScenarioAchievementsCrewDedupeTest.cs` | 242 | Test suite for achievements dedup logic |

---

## Files Modified (Exist in Both, But Differ) — 51 Files

### Client Harmony & Patching (2 files)
Game engine behavior modifications.

| File | Changes | Notes |
|------|---------|-------|
| `LmpClient/Harmony/OrbitDriver_UpdateFromParameters.cs` | +72 / -0 | Orbit update parameter handling |
| `LmpClient/Base/HarmonyPatcher.cs` | +13 / -0 | Patcher initialization changes |

### Client System Configuration (2 files)
Core client initialization and settings.

| File | Changes | Notes |
|------|---------|-------|
| `LmpClient/MainSystem.cs` | +78 / -0 | Main system startup modifications |
| `LmpClient/Systems/SettingsSys/SettingsStructures.cs` | +13 / -0 | Settings structure enhancements |

### Client Scenario Systems (5 files)
Achievements, contracts, and scene management.

| File | Changes | Notes |
|------|---------|-------|
| `LmpClient/Systems/KscScene/KscSceneSystem.cs` | +92 / -0 | KSC scene system expansion |
| `LmpClient/Systems/KscScene/KscSceneEvents.cs` | +24 / -0 | KSC scene event handling |
| `LmpClient/Systems/ShareAchievements/ShareAchievementsMessageSender.cs` | +53 / -0 | Achievements sync improvements |
| `LmpClient/Systems/ShareContracts/ShareContractsEvents.cs` | +24 / -11 | Contract event handlers (+13 net) |
| `LmpClient/Systems/ShareContracts/ShareContractsMessageHandler.cs` | +30 / -0 | Contract message handling |

### Client Funds & Crew (3 files)
Financial and crew management systems.

| File | Changes | Notes |
|------|---------|-------|
| `LmpClient/Systems/ShareFunds/ShareFundsEvents.cs` | +41 / -0 | Funds event handler expansion |
| `LmpClient/Systems/ShareFunds/ShareFundsSystem.cs` | +15 / -0 | Funds system improvements |
| `LmpClient/Systems/VesselCrewSys/VesselCrewEvents.cs` | +8 / -0 | Crew event handling |

### Client Vessel Systems (5 files)
Vessel state, position, locks, and eva operations.

| File | Changes | Notes |
|------|---------|-------|
| `LmpClient/Systems/VesselCoupleSys/VesselCoupleEvents.cs` | +2 / -4 | Vessel coupling event tweaks (-2 net) |
| `LmpClient/Systems/VesselLockSys/VesselLockEvents.cs` | +23 / -0 | Lock system event handlers |
| `LmpClient/Systems/VesselEvaEditorSys/VesselEvaEditorEvents.cs` | +38 / -0 | EVA editor system events |
| `LmpClient/Systems/VesselPositionSys/PositionEvents.cs` | +18 / -0 | Position event handling |
| `LmpClient/Systems/VesselPositionSys/VesselPositionSystem.cs` | +2 / -0 | Position system minor updates |

### Client Proto/Vessel Sync — Major System (6 files)
**Largest divergence — vessel synchronization refactoring.**

| File | Changes | Notes |
|------|---------|-------|
| `LmpClient/Systems/VesselProtoSys/VesselProtoSystem.cs` | +396 / -0 | **Major:** Proto system refactoring, architecture changes |
| `LmpClient/Systems/VesselProtoSys/VesselProtoEvents.cs` | +84 / -4 | Event handling expansion (+80 net) |
| `LmpClient/Systems/VesselProtoSys/VesselProtoMessageHandler.cs` | +13 / -0 | Message handler improvements |
| `LmpClient/Systems/VesselProtoSys/VesselProtoMessageSender.cs` | +18 / -6 | Message sending refinements (+12 net) |
| `LmpClient/Systems/VesselProtoSys/VesselProto.cs` | +9 / -0 | Proto data structure changes |
| `LmpClient/Systems/VesselRemoveSys/VesselRemoveEvents.cs` | +24 / -0 | Vessel removal event handling |

### Client Vessel Utilities (3 files)
**Second largest divergence — vessel loading refactoring.**

| File | Changes | Notes |
|------|---------|-------|
| `LmpClient/VesselUtilities/VesselLoader.cs` | **+872 / -0** | **Massive:** Vessel loading rewrite with new architecture |
| `LmpClient/VesselUtilities/VesselSerializer.cs` | +8 / -0 | Serialization improvements |
| `LmpClient/Systems/VesselRemoveSys/VesselRemoveMessageSender.cs` | +17 / -0 | Vessel removal message handling |

### Client Project Configuration (1 file)

| File | Changes | Notes |
|------|---------|-------|
| `LmpClient/LmpClient.csproj` | +31 / -0 | Project file updates (new dependencies/references) |

### Common Library (4 files)
Shared message definitions and utilities.

| File | Changes | Notes |
|------|---------|-------|
| `LmpCommon/IgnoredScenarios.cs` | +16 / -0 | Scenario ignore list expansion |
| `LmpCommon/Message/Data/Vessel/VesselPartSyncFieldMsgData.cs` | +3 / -6 | Part sync message tweaks (-3 net) |
| `LmpCommon/Message/Data/Vessel/VesselProtoMsgData.cs` | +18 / -0 | Proto message data changes |
| `LmpCommon/Message/Data/Vessel/VesselRemoveMsgData.cs` | +18 / -0 | Removal message data updates |
| `LmpCommon/RepoRetrievers/BannedIpsRetriever.cs` | +71 / -6 | **Upstream merge:** Prewarm, race condition fix (+65 net) |

### Networking & Server (2 files)

| File | Changes | Notes |
|------|---------|-------|
| `LmpMasterServer/EntryPoint.cs` | +8 / -8 | **Upstream merge:** Hybrid async/method pattern (+0 net) |
| `Lidgren/NetConnection.cs` | +3 / -0 | Network connection updates |
| `Server/Server/LidgrenServer.cs` | +202 / -22 | **Upstream:** Malformed packet exception handling (+180 net) |

### Server Infrastructure (9 files)
Server-side systems and utilities.

| File | Changes | Notes |
|------|---------|-------|
| `Server/MainServer.cs` | +39 / -0 | Main server initialization |
| `Server/Client/ClientMainThread.cs` | +29 / -0 | Client thread management |
| `Server/Context/ServerContext.cs` | +6 / -0 | Context improvements |
| `Server/Message/VesselMsgReader.cs` | +50 / -0 | Vessel message reading |
| `Server/Log/LunaLog.cs` | +207 / -4 | Logging system expansion (+203 net) |
| `Server/StartLunaServer.bat` | +84 / -0 | Server startup script (not in original) |
| `Server/System/ScenarioStoreSystem.cs` | +16 / -0 | Scenario storage system |
| `Server/System/WarpSystemReceiver.cs` | +10 / -0 | Warp system receiver |
| `Server/System/Vessel/VesselDataUpdater.cs` | +9 / -0 | Vessel data updates |

### Server Scenario Management (3 files)
Scenario upgrade and data migration.

| File | Changes | Notes |
|------|---------|-------|
| `Server/System/Scenario/ScenarioAchievementsDataUpdater.cs` | +6 / -0 | Achievements data sync |
| `Server/System/Scenario/ScenarioContractsDataUpdater.cs` | +169 / -0 | **Major:** Contracts data migration system |
| `Server/System/Vessel/VesselPositionDataUpdater.cs` | +27 / -0 | Position data updates |

### Server Project Configuration (1 file)

| File | Changes | Notes |
|------|---------|-------|
| `Server/Server.csproj` | +25 / -0 | Project file updates |

### Server Settings (1 file)

| File | Changes | Notes |
|------|---------|-------|
| `Server/Settings/Definition/IntervalSettingsDefinition.cs` | +14 / -0 | Settings definition updates |

### Repository Root (1 file)

| File | Changes | Notes |
|------|---------|-------|
| `.gitignore` | +2 / -0 | Git ignore rules |

---

## Key Divergence Areas

### 1. **Vessel Synchronization System** (Largest Change)
- `VesselLoader.cs`: +872 lines — Complete rewrite of vessel loading with new architecture
- `VesselProtoSystem.cs`: +396 lines — Proto system refactoring
- `LocalTopologyTracker.cs`: +170 lines (new) — Topology tracking system
- **Total impact:** ~1,400 lines of new/modified vessel sync infrastructure

### 2. **Diagnostic & Profiling Tools** (Development-focused)
- Performance profilers (TsLoadProfiler, MemoryDiagnosticsLogger)
- Sync diagnostics (VesselSyncDiagnostics)
- Likely removed by upstream as non-essential to production

### 3. **Data Integrity Systems** (Operational)
- Achievements deduplication (ScenarioAchievementsCrewDedupe)
- Contract migration (ScenarioContractsMigration)
- Discovery info sanitization (DiscoveryInfoSanitizer)
- **Purpose:** Handle edge cases and data corruption scenarios

### 4. **Network Robustness** (Upstream merge value)
- Malformed packet exception handling in `LidgrenServer.cs`
- Banned IPs caching with Prewarm optimization
- **Status:** Valuable fixes integrated from upstream

### 5. **Server-Side Logging** (Operational)
- Enhanced logging system (+207 lines in LunaLog)
- Craft creation/removal audit log
- Better observability for troubleshooting

---

## Upstream Contributions Summary

Files integrated from upstream master during merge:
- ✅ `BannedIpsRetriever.cs` — Prewarm(), race condition fix
- ✅ `LidgrenServer.cs` — Malformed packet exception handling
- ✅ `EntryPoint.cs` — Hybrid async resolution with Prewarm call

---

## Potential PR Candidates (Back to Upstream)

### High Value
- **Prewarm + BannedIps improvements** — Startup optimization, network robustness
- **Network exception handling** — Prevents crashes from malformed UDP packets
- **Data deduplication systems** — Handles real-world corruption scenarios

### Medium Value
- **Contract migration utilities** — Data integrity for long-running servers
- **Discovery info sanitization** — Prevents sync errors from bad data

### Low Value for Upstream
- **Diagnostic tools** — Fork-specific development utilities
- **Harmony patches** — May be RSS-specific
- **Warp allotment tracker** — Gameplay feature, fork-specific
