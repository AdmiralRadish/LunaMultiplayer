using KSP.UI.Screens.Flight;
using LmpClient.Extensions;
using LmpClient.Systems.VesselPositionSys;
using System;
using System.Linq;
using Object = UnityEngine.Object;

namespace LmpClient.VesselUtilities
{
    public class VesselLoader
    {
        /// <summary>
        /// Loads/Reloads a vessel into game
        /// </summary>
        public static bool LoadVessel(ProtoVessel vesselProto, bool forceReload)
        {
            try
            {
                LogProtoVesselSummary(vesselProto, forceReload);
                return vesselProto.Validate(true) && LoadVesselIntoGame(vesselProto, forceReload);
            }
            catch (Exception e)
            {
                LunaLog.LogError($"[LMP]: Error loading vessel: {e}");
                return false;
            }
        }

        /// <summary>
        /// Logs a single-line summary of an incoming protovessel before <see cref="ProtoVessel.Load"/>
        /// runs. Provides a baseline to correlate against KSP-side <c>Vessel.UpdateCaches()</c> /
        /// <c>CommNetVessel.UpdateComm()</c> NullReferenceExceptions: the offending vessel can be
        /// matched back to the most recent load by id, name, type, and the distinct part-name set
        /// (which fingerprints which mod set the originating client expected).
        /// </summary>
        private static void LogProtoVesselSummary(ProtoVessel vesselProto, bool forceReload)
        {
            if (vesselProto == null) return;
            try
            {
                var partCount = vesselProto.protoPartSnapshots?.Count ?? 0;
                var distinctParts = vesselProto.protoPartSnapshots == null
                    ? string.Empty
                    : string.Join(",", vesselProto.protoPartSnapshots.Select(p => p.partName).Distinct());
                LunaLog.Log($"[LMP]: Loading proto vessel {vesselProto.vesselID} ({vesselProto.vesselName}) " +
                            $"type={vesselProto.vesselType} situation={vesselProto.situation} " +
                            $"forceReload={forceReload} parts={partCount} distinctParts=[{distinctParts}]");
            }
            catch (Exception e)
            {
                //Diagnostic logging must never break the load path.
                LunaLog.LogWarning($"[LMP]: LogProtoVesselSummary failed for {vesselProto.vesselID}: {e.Message}");
            }
        }

        /// <summary>
        /// One-shot sanity walk of the freshly-loaded vessel that <see cref="ProtoVessel.Load"/> just
        /// produced. Detects the exact corruption shapes that cause stock KSP to NRE on every
        /// FixedUpdate inside <c>Vessel.UpdateCaches()</c> and inside <c>CommNetVessel.UpdateComm()</c>:
        /// null entries in <c>vessel.parts</c>, parts with null <c>partInfo</c>, parts with a missing
        /// <c>vessel</c> back-reference, parts with a null <c>Modules</c> collection, modules whose
        /// runtime count diverges from the proto's module count, and null entries in
        /// <c>protoModuleCrew</c>. Pure logging — does not change which vessels survive load.
        /// </summary>
        private static void LogPostLoadVesselSanity(ProtoVessel vesselProto)
        {
            var v = vesselProto?.vesselRef;
            if (v == null) return;
            try
            {
                if (v.parts == null)
                {
                    LunaLog.LogError($"[LMP]: Post-load sanity: vessel {v.id} ({v.vesselName}) has a NULL parts list.");
                    return;
                }

                int nullParts = 0, nullPartInfo = 0, nullVesselRef = 0, nullModules = 0, moduleCountMismatch = 0, nullCrewSlot = 0;
                for (var i = 0; i < v.parts.Count; i++)
                {
                    var part = v.parts[i];
                    if (part == null)
                    {
                        nullParts++;
                        continue;
                    }

                    if (part.partInfo == null) nullPartInfo++;
                    if (!part.vessel) nullVesselRef++;
                    if (part.Modules == null)
                    {
                        nullModules++;
                    }
                    else
                    {
                        var protoPart = i < vesselProto.protoPartSnapshots.Count ? vesselProto.protoPartSnapshots[i] : null;
                        if (protoPart?.modules != null && protoPart.modules.Count != part.Modules.Count)
                            moduleCountMismatch++;
                    }
                    if (part.protoModuleCrew != null && part.protoModuleCrew.Any(c => c == null))
                        nullCrewSlot++;
                }

                if (nullParts + nullPartInfo + nullVesselRef + nullModules + moduleCountMismatch + nullCrewSlot > 0)
                {
                    LunaLog.LogError($"[LMP]: Post-load sanity: vessel {v.id} ({v.vesselName}) is CORRUPT - " +
                                     $"nullParts={nullParts} nullPartInfo={nullPartInfo} nullVesselRef={nullVesselRef} " +
                                     $"nullModules={nullModules} moduleCountMismatch={moduleCountMismatch} nullCrewSlot={nullCrewSlot}. " +
                                     $"This vessel will likely NRE in Vessel.UpdateCaches()/CommNetVessel.UpdateComm() every FixedUpdate.");
                }
            }
            catch (Exception e)
            {
                LunaLog.LogWarning($"[LMP]: LogPostLoadVesselSanity failed for {v.id}: {e.Message}");
            }
        }

        #region Private methods

        /// <summary>
        /// Loads the vessel proto into the current game
        /// </summary>
        private static bool LoadVesselIntoGame(ProtoVessel vesselProto, bool forceReload)
        {
            if (HighLogic.CurrentGame?.flightState == null)
                return false;

            var reloadingOwnVessel = FlightGlobals.ActiveVessel && vesselProto.vesselID == FlightGlobals.ActiveVessel.id;

            //In case the vessel exists, silently remove them from unity and recreate it again
            var existingVessel = FlightGlobals.FindVessel(vesselProto.vesselID);
            if (existingVessel != null)
            {
                if (!forceReload && existingVessel.Parts.Count == vesselProto.protoPartSnapshots.Count &&
                    existingVessel.GetCrewCount() == vesselProto.GetVesselCrew().Count)
                {
                    // Always keep the stored flight plan current even when skipping a full reload.
                    // Without this, maneuver node changes are discarded and the vessel's
                    // PatchedConicSolver loads stale (empty) data on the next GoOffRails.
                    existingVessel.protoVessel.flightPlan = vesselProto.flightPlan;
                    return true;
                }

                LunaLog.Log($"[LMP]: Reloading vessel {vesselProto.vesselID}");
                if (reloadingOwnVessel)
                    existingVessel.RemoveAllCrew();

                FlightGlobals.RemoveVessel(existingVessel);
                // Disable immediately so Unity stops calling FixedUpdate on this vessel before
                // Object.Destroy is processed — same deferred-destroy race that causes
                // Vessel.UpdateCaches() NullReferenceExceptions (see VesselRemoveSystem.KillVessel).
                existingVessel.gameObject.SetActive(false);
                foreach (var part in existingVessel.parts)
                {
                    Object.Destroy(part.gameObject);
                }
                Object.Destroy(existingVessel.gameObject);
            }
            else
            {
                LunaLog.Log($"[LMP]: Loading vessel {vesselProto.vesselID}");
            }

            SanitizePersistentIds(vesselProto);

            try
            {
                vesselProto.Load(HighLogic.CurrentGame.flightState);
            }
            catch (Exception loadEx)
            {
                // KSP may have created the Vessel GameObject before the exception (e.g. OrbitSnapshot.Load
                // throws when the vessel's referenceBody index is out of range because the server has extra
                // celestial bodies from a mod the client doesn't have).  Without cleanup the zombie vessel
                // stays in FlightGlobals and causes NullReferenceExceptions in Vessel.UpdateCaches() on
                // every physics tick.
                LunaLog.LogError($"[LMP]: Vessel {vesselProto.vesselID} threw during ProtoVessel.Load — removing to prevent zombie vessel. Error: {loadEx.Message}");
                if (vesselProto.vesselRef != null)
                {
                    FlightGlobals.RemoveVessel(vesselProto.vesselRef);
                    foreach (var part in vesselProto.vesselRef.parts)
                        Object.Destroy(part.gameObject);
                    Object.Destroy(vesselProto.vesselRef.gameObject);
                }
                HighLogic.CurrentGame.flightState.protoVessels.Remove(vesselProto);
                return false;
            }

            if (vesselProto.vesselRef == null)
            {
                LunaLog.Log($"[LMP]: Protovessel {vesselProto.vesselID} failed to create a vessel!");
                return false;
            }

            // Verify that every part module loaded successfully.  When the server has a mod that the
            // client lacks, KSP may instantiate a part but leave null slots in Part.Modules — these
            // cause Vessel.UpdateCaches() to throw a NullReferenceException on every physics tick.
            if (vesselProto.vesselRef.parts != null)
            {
                string badDetail = null;
                for (var pi = 0; pi < vesselProto.vesselRef.parts.Count && badDetail == null; pi++)
                {
                    var p = vesselProto.vesselRef.parts[pi];
                    if (p == null) { badDetail = $"null part at index {pi}"; break; }
                    if (p.Modules == null) continue;
                    for (var mi = 0; mi < p.Modules.Count; mi++)
                    {
                        if (p.Modules[mi] == null)
                        {
                            badDetail = $"null module at index {mi} on part '{p.partName}'";
                            break;
                        }
                    }
                }

                if (badDetail != null)
                {
                    LunaLog.LogError($"[LMP]: Vessel {vesselProto.vesselID} ({vesselProto.vesselName}) loaded with {badDetail} — removing to prevent Vessel.UpdateCaches NullReferenceException spam.");
                    FlightGlobals.RemoveVessel(vesselProto.vesselRef);
                    vesselProto.vesselRef.gameObject.SetActive(false);
                    foreach (var p in vesselProto.vesselRef.parts)
                        if (p?.gameObject != null) Object.Destroy(p.gameObject);
                    Object.Destroy(vesselProto.vesselRef.gameObject);
                    HighLogic.CurrentGame.flightState.protoVessels.Remove(vesselProto);
                    return false;
                }
            }

            // Safety-net: verify the ProtoVessel can be saved before keeping it in the flight state.
            // If ProtoVessel.Save() throws (e.g. from a null resource definition left by a server mod),
            // GamePersistence.SaveGame() would also throw, causing the UI to freeze on any menu close.
            try
            {
                vesselProto.Save(new ConfigNode());
            }
            catch (Exception saveEx)
            {
                LunaLog.LogError($"[LMP]: Vessel {vesselProto.vesselID} ({vesselProto.vesselName}) cannot be saved — removing to prevent UI freezes. Error: {saveEx.Message}");
                FlightGlobals.RemoveVessel(vesselProto.vesselRef);
                foreach (var part in vesselProto.vesselRef.parts)
                    Object.Destroy(part.gameObject);
                Object.Destroy(vesselProto.vesselRef.gameObject);
                HighLogic.CurrentGame.flightState.protoVessels.Remove(vesselProto);
                return false;
            }
            LogPostLoadVesselSanity(vesselProto);

            LogPostLoadVesselSanity(vesselProto);

            VesselPositionSystem.Singleton.ForceUpdateVesselPosition(vesselProto.vesselRef.id);

            vesselProto.vesselRef.protoVessel = vesselProto;
            if (vesselProto.vesselRef.isEVA)
            {
                var evaModule = vesselProto.vesselRef.FindPartModuleImplementing<KerbalEVA>();
                if (evaModule != null && evaModule.fsm != null && !evaModule.fsm.Started)
                {
                    evaModule.fsm?.StartFSM("Idle (Grounded)");
                }
                vesselProto.vesselRef.GoOnRails();
            }

            if (vesselProto.vesselRef.situation > Vessel.Situations.PRELAUNCH)
            {
                vesselProto.vesselRef.orbitDriver.updateFromParameters();
            }

            if (double.IsNaN(vesselProto.vesselRef.orbitDriver.pos.x))
            {
                LunaLog.Log($"[LMP]: Protovessel {vesselProto.vesselID} has an invalid orbit");
                return false;
            }

            if (reloadingOwnVessel)
            {
                vesselProto.vesselRef.Load();
                vesselProto.vesselRef.RebuildCrewList();

                //Do not do the setting of the active vessel manually, too many systems are dependant of the events triggered by KSP
                FlightGlobals.ForceSetActiveVessel(vesselProto.vesselRef);

                vesselProto.vesselRef.SpawnCrew();
                foreach (var crew in vesselProto.vesselRef.GetVesselCrew())
                {
                    ProtoCrewMember._Spawn(crew);
                    if (crew.KerbalRef)
                        crew.KerbalRef.state = Kerbal.States.ALIVE;
                }

                if (KerbalPortraitGallery.Instance.ActiveCrewItems.Count != vesselProto.vesselRef.GetCrewCount())
                {
                    KerbalPortraitGallery.Instance.StartReset(FlightGlobals.ActiveVessel);
                }
            }

            return true;
        }

        #endregion

        #region ID sanitization

        /// <summary>
        /// Proactively remaps any persistentId values in vesselProto that already exist in the
        /// running FlightGlobals registries (PersistentVesselIds, PersistentLoadedPartIds,
        /// PersistentUnloadedPartIds) before the vessel is loaded into the game.
        ///
        /// Without this, KSP's HandlePartPersistentIdCollision fires O(n) times per conflicting
        /// part on the main thread, which under concurrent LMP vessel loads can cascade into a
        /// freeze when many parts collide simultaneously.  By remapping upfront using
        /// FlightGlobals.GetUniquepersistentId() we hand KSP clean IDs and the collision handler
        /// never fires.
        ///
        /// The incoming proto IDs are transient transport values — they only need to be unique on
        /// this client.  The authoritative state is the server's save, so remapping here is safe.
        /// </summary>
        private static void SanitizePersistentIds(ProtoVessel vesselProto)
        {
            // Strip null crew slots before load — Vessel.Start() calls RebuildCrewList() which
            // iterates protoModuleCrew on every ProtoPartSnapshot; a null slot causes a
            // NullReferenceException that Unity catches internally (never reaches our catch block).
            foreach (var snapshot in vesselProto.protoPartSnapshots)
                snapshot.protoModuleCrew?.RemoveAll(c => c == null);

            // Vessel-level persistentId
            if (FlightGlobals.PersistentVesselIds.ContainsKey(vesselProto.persistentId))
            {
                var newId = FlightGlobals.GetUniquepersistentId();
                LunaLog.Log($"[LMP]: PersistentId collision — remapping vessel {vesselProto.vesselID} " +
                            $"vessel persistentId {vesselProto.persistentId} → {newId}");
                vesselProto.persistentId = newId;
            }

            // Per-part persistentId (ProtoPartSnapshot)
            foreach (var part in vesselProto.protoPartSnapshots)
            {
                if (FlightGlobals.PersistentLoadedPartIds.ContainsKey(part.persistentId) ||
                    FlightGlobals.PersistentUnloadedPartIds.ContainsKey(part.persistentId))
                {
                    var newId = FlightGlobals.GetUniquepersistentId();
                    LunaLog.Log($"[LMP]: PersistentId collision — remapping vessel {vesselProto.vesselID} " +
                                $"part {part.partName} persistentId {part.persistentId} → {newId}");
                    part.persistentId = newId;
                }
            }
        }

        #endregion
    }
}
