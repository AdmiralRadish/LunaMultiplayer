# PR File Ownership Appendix (0_29_3 vs upstream/master)

Generated: June 3, 2026

## Scope and Validation

- Diff source: `git diff --name-status upstream/master...0_29_3`
- Current divergence: 68 files (`A=17`, `M=51`, `D=0`)
- Ownership audit result: 68 assigned, 0 unassigned, 0 multi-assigned

This appendix is the single-file ownership map used for review sign-off. Every file in the current divergence is assigned to one PR only.

---

## PR1 - Network Robustness (4)

- Lidgren/NetConnection.cs
- LmpCommon/RepoRetrievers/BannedIpsRetriever.cs
- LmpMasterServer/EntryPoint.cs
- Server/Server/LidgrenServer.cs

## PR2 - Scenario Integrity (10)

- LmpClient/Systems/ShareAchievements/ShareAchievementsMessageSender.cs
- LmpClient/Systems/ShareContracts/ContractPartReferenceChecker.cs
- LmpClient/Systems/ShareContracts/ContractsScenarioSanitizer.cs
- LmpClient/Systems/ShareContracts/ShareContractsEvents.cs
- LmpClient/Systems/ShareContracts/ShareContractsMessageHandler.cs
- Server/System/Scenario/ScenarioAchievementsCrewDedupe.cs
- Server/System/Scenario/ScenarioAchievementsDataUpdater.cs
- Server/System/Scenario/ScenarioContractsDataUpdater.cs
- Server/System/Scenario/ScenarioContractsMigration.cs
- ServerTest/ScenarioAchievementsCrewDedupeTest.cs

## PR3 - Vessel Sync Overhaul (15)

- LmpClient/Systems/AsteroidComet/AsteroidCometEvents.cs
- LmpClient/Systems/FlagPlant/FlagPlantEvents.cs
- LmpClient/Systems/VesselProtoSys/LocalTopologyTracker.cs
- LmpClient/Systems/VesselProtoSys/VesselProto.cs
- LmpClient/Systems/VesselProtoSys/VesselProtoEvents.cs
- LmpClient/Systems/VesselProtoSys/VesselProtoMessageHandler.cs
- LmpClient/Systems/VesselProtoSys/VesselProtoMessageSender.cs
- LmpClient/Systems/VesselProtoSys/VesselProtoSystem.cs
- LmpClient/Systems/VesselRemoveSys/VesselRemoveEvents.cs
- LmpClient/Systems/VesselRemoveSys/VesselRemoveMessageSender.cs
- LmpClient/VesselUtilities/DiscoveryInfoSanitizer.cs
- LmpClient/VesselUtilities/VesselLoader.cs
- LmpClient/VesselUtilities/VesselSerializer.cs
- LmpCommon/Message/Data/Vessel/VesselProtoMsgData.cs
- LmpCommon/Message/Data/Vessel/VesselRemoveMsgData.cs

## PR4 - Server Infrastructure (10)

- Server/Client/ClientMainThread.cs
- Server/Context/ServerContext.cs
- Server/Log/CraftCreationAndRemovalLog.cs
- Server/MainServer.cs
- Server/Message/VesselMsgReader.cs
- Server/Settings/Definition/IntervalSettingsDefinition.cs
- Server/System/ScenarioStoreSystem.cs
- Server/System/Vessel/VesselDataUpdater.cs
- Server/System/Vessel/VesselPositionDataUpdater.cs
- Server/System/WarpSystemReceiver.cs

## PR5 - Diagnostics and Profiling (4)

- LmpClient/Diagnostics/TsLoadProfiler.cs
- LmpClient/Diagnostics/VesselSyncDiagnostics.cs
- Server/Log/LunaLog.cs
- Server/System/MemoryDiagnosticsLogger.cs

## PR6 - Harmony Patches (5)

- LmpClient/Base/HarmonyPatcher.cs
- LmpClient/Harmony/DefaultDateTimeFormatter_ClampDateInputs.cs
- LmpClient/Harmony/KnowledgeBase_GetVesselCrewByAvailablePart.cs
- LmpClient/Harmony/OrbitDriver_UpdateFromParameters.cs
- LmpClient/Harmony/Part_RegisterCrew.cs

## PR7 - Build Automation (2)

- Scripts/Build-Release.ps1
- Server/StartLunaServer.bat

## PR8 - Configuration Updates (7)

- .gitignore
- LmpClient/LmpClient.csproj
- LmpClient/MainSystem.cs
- LmpClient/Systems/SettingsSys/SettingsStructures.cs
- LmpCommon/IgnoredScenarios.cs
- LmpCommon/Message/Data/Vessel/VesselPartSyncFieldMsgData.cs
- Server/Server.csproj

## PR9 - Scene and Event Systems (10)

- LmpClient/Systems/KscScene/KscSceneEvents.cs
- LmpClient/Systems/KscScene/KscSceneSystem.cs
- LmpClient/Systems/ShareFunds/ShareFundsEvents.cs
- LmpClient/Systems/ShareFunds/ShareFundsSystem.cs
- LmpClient/Systems/VesselCoupleSys/VesselCoupleEvents.cs
- LmpClient/Systems/VesselCrewSys/VesselCrewEvents.cs
- LmpClient/Systems/VesselEvaEditorSys/VesselEvaEditorEvents.cs
- LmpClient/Systems/VesselLockSys/VesselLockEvents.cs
- LmpClient/Systems/VesselPositionSys/PositionEvents.cs
- LmpClient/Systems/VesselPositionSys/VesselPositionSystem.cs

## PR10 - Warp Allotment Tracking (1)

- Server/System/WarpAllotmentTracker.cs

---

## Review Gate

Before opening any PR from this plan, re-run:

1. `git diff --name-status upstream/master...0_29_3`
2. Confirm file count remains 68 (or update this appendix if count changes).
3. Confirm no file appears in multiple PR scopes.
