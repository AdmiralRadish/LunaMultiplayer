using KSP.UI.Screens.Flight;
using LmpClient.Extensions;
using LmpClient.Systems.VesselPositionSys;
using System;
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
                return vesselProto.Validate(true) && LoadVesselIntoGame(vesselProto, forceReload);
            }
            catch (Exception e)
            {
                LunaLog.LogError($"[LMP]: Error loading vessel: {e}");
                return false;
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

            vesselProto.Load(HighLogic.CurrentGame.flightState);
            if (vesselProto.vesselRef == null)
            {
                LunaLog.Log($"[LMP]: Protovessel {vesselProto.vesselID} failed to create a vessel!");
                return false;
            }

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
