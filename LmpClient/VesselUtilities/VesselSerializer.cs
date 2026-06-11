using LmpClient.Extensions;
using LmpClient.Systems.KerbalSys;
using LmpClient.Utilities;
using System;

namespace LmpClient.VesselUtilities
{
    public class VesselSerializer
    {
        /// <summary>
        /// Deserialize a byte array into a protovessel
        /// </summary>
        public static ProtoVessel DeserializeVessel(byte[] data, int numBytes)
        {
            try
            {
                var vesselNode = data.DeserializeToConfigNode(numBytes);
                var configGuid = vesselNode?.GetValue("pid");

                return CreateSafeProtoVesselFromConfigNode(vesselNode, new Guid(configGuid));
            }
            catch (Exception e)
            {
                LunaLog.LogError($"[LMP]: Error while deserializing vessel: {e}");
                return null;
            }
        }

        /// <summary>
        /// Serialize a protovessel into a byte array
        /// </summary>
        public static byte[] SerializeVessel(ProtoVessel protoVessel)
        {
            return PreSerializationChecks(protoVessel, out var configNode) ? configNode.Serialize() : new byte[0];
        }

        /// <summary>
        /// Serializes a vessel to a previous preallocated array (avoids garbage generation)
        /// </summary>
        public static void SerializeVesselToArray(ProtoVessel protoVessel, byte[] data, out int numBytes)
        {
            if (PreSerializationChecks(protoVessel, out var configNode))
            {
                configNode.SerializeToArray(data, out numBytes);
            }
            else
            {
                numBytes = 0;
            }
        }

        /// <summary>
        /// Creates a protovessel from a ConfigNode
        /// </summary>
        public static ProtoVessel CreateSafeProtoVesselFromConfigNode(ConfigNode inputNode, Guid protoVesselId)
        {
            try
            {
                //Cannot create a protovessel if HighLogic.CurrentGame is null as we don't have a CrewRoster
                //and the protopartsnapshot constructor needs it
                if (HighLogic.CurrentGame == null)
                    return null;

                //Make sure every crew member referenced by this vessel exists in the roster BEFORE we
                //construct the ProtoVessel. Stock ProtoPartSnapshot construction looks crew up by name and,
                //if the kerbal isn't in CurrentGame.CrewRoster yet, it silently produces a blank-named crew
                //member. Those blank crew later crash KSP.UI.Screens.AstronautComplex.CreateAssignedList and
                //Part.RegisterCrew (NullReferenceException), wiping every kerbal from the astronaut complex.
                //This is the last point where the original crew names are still available in the raw node.
                EnsureRosterForVesselCrew(inputNode);

                //Cannot reuse the Protovessel to save memory garbage as it does not have any clear method :(
                return new ProtoVessel(inputNode, HighLogic.CurrentGame);
            }
            catch (Exception e)
            {
                LunaLog.LogError($"[LMP]: Damaged vessel {protoVesselId}, exception: {e}");
                return null;
            }
        }

        #region Private methods

        /// <summary>
        /// Ensures that every crew member referenced by the raw vessel ConfigNode exists in the current
        /// game CrewRoster before the ProtoVessel is built. Crew assignments live inside PART nodes as
        /// "crew = KerbalName" lines. If a referenced kerbal is missing from the roster, stock KSP creates
        /// a blank-named crew member which later causes NullReferenceExceptions in the Astronaut Complex and
        /// Part.RegisterCrew. We first drain any pending kerbal sync (so real kerbal data is used) and then
        /// add a minimal placeholder for any crew name still missing.
        /// </summary>
        private static void EnsureRosterForVesselCrew(ConfigNode inputNode)
        {
            if (inputNode == null) return;

            var roster = HighLogic.CurrentGame?.CrewRoster;
            if (roster == null) return;

            try
            {
                //Flush any kerbals we already received from the server so their full data is in the roster
                KerbalSystem.Singleton.LoadKerbalsIntoGame();

                foreach (var partNode in inputNode.GetNodes("PART"))
                {
                    foreach (var rawCrewName in partNode.GetValues("crew"))
                    {
                        var crewName = rawCrewName?.Trim();
                        if (string.IsNullOrEmpty(crewName)) continue;
                        if (roster.Exists(crewName)) continue;

                        try
                        {
                            //Build a minimal kerbal node. ProtoCrewMember is constructed from a ConfigNode
                            //(same pattern as KerbalSystem.LoadKerbal). This is only a last-resort placeholder
                            //for a crew name that has no synced kerbal; normal kerbals are added by the drain above.
                            var crewNode = new ConfigNode();
                            crewNode.AddValue("name", crewName);
                            crewNode.AddValue("type", ProtoCrewMember.KerbalType.Crew);
                            crewNode.AddValue("trait", "Pilot");
                            crewNode.AddValue("brave", 0.5f);
                            crewNode.AddValue("dock", 0.5f);
                            crewNode.AddValue("badS", false);
                            crewNode.AddValue("state", ProtoCrewMember.RosterStatus.Assigned);

                            var pcm = new ProtoCrewMember(HighLogic.CurrentGame.Mode, crewNode)
                            {
                                rosterStatus = ProtoCrewMember.RosterStatus.Assigned
                            };
                            roster.AddCrewMember(pcm);
                            LunaLog.Log($"[LMP]: Pre-seeded missing roster crew '{crewName}' before loading vessel");
                        }
                        catch (Exception inner)
                        {
                            LunaLog.LogWarning($"[LMP]: Failed to pre-seed roster crew '{crewName}': {inner.Message}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LunaLog.LogWarning($"[LMP]: Error ensuring roster for vessel crew: {e.Message}");
            }
        }

        private static bool PreSerializationChecks(ProtoVessel protoVessel, out ConfigNode configNode)
        {
            configNode = new ConfigNode();

            if (protoVessel == null)
            {
                LunaLog.LogError("[LMP]: Cannot serialize a null protovessel");
                return false;
            }

            try
            {
                protoVessel.Save(configNode);
            }
            catch (Exception e)
            {
                LunaLog.LogError($"[LMP]: Error while saving vessel: {e}");
                return false;
            }

            var vesselId = new Guid(configNode.GetValue("pid"));

            //Defend against NaN orbits
            if (configNode.VesselHasNaNPosition())
            {
                LunaLog.LogError($"[LMP]: Vessel {vesselId} has NaN position");
                return false;
            }

            return true;
        }

        #endregion
    }
}
