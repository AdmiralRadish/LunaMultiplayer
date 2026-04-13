using LmpClient.Base;
using LmpClient.Base.Interface;
using LmpClient.Extensions;
using LmpClient.Network;
using LmpClient.Systems.TimeSync;
using LmpClient.Systems.Warp;
using LmpClient.Utilities;
using LmpCommon.Message.Client;
using LmpCommon.Message.Data.Vessel;
using LmpCommon.Message.Interface;
using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace LmpClient.Systems.VesselPositionSys
{
    public class VesselPositionMessageSender : SubSystem<VesselPositionSystem>, IMessageSender
    {
        #region Spurious orbit-capture detection

        // Tracks (bodyIndex, gameTime) of the last position update sent per vessel, used to detect
        // anomalous SOI transitions caused by KSP PatchedConic precision errors during vessel load.
        private static readonly ConcurrentDictionary<Guid, (int BodyIndex, double GameTime)> LastKnownOrbitState =
            new ConcurrentDictionary<Guid, (int BodyIndex, double GameTime)>();

        // Vessels currently under suppression: value is the game-time at which the suspicion was raised.
        private static readonly ConcurrentDictionary<Guid, double> SpuriousCaptureSuspectedAt =
            new ConcurrentDictionary<Guid, double>();

        // A SOI transition is only flagged when this many game-seconds elapsed since the last known
        // update.  Continuous flight produces small deltas; a vessel loaded from an old server epoch
        // produces a large delta.
        private const double SpuriousCaptureMinTimeJump = 300.0;

        // After flagging, position updates for the vessel are suppressed for this many game-seconds
        // to give the operator time to notice and intervene before wrong data reaches the server.
        private const double SpuriousCaptureSettleTime = 60.0;

        /// <summary>
        /// Checks whether the orbital state in <paramref name="msgData"/> represents a plausible
        /// continuous-flight update.  Returns <c>false</c> and logs a warning when a captured
        /// elliptical orbit (ECC &lt; 1) appears in a new reference body after a large game-time
        /// gap — the signature of a KSP PatchedConic precision error on vessel load or time warp.
        /// </summary>
        private static bool ValidateOrbitPlausibility(Guid vesselId, string vesselName, VesselPositionMsgData msgData)
        {
            int    newBodyIndex    = (int)msgData.Orbit[7];
            double newEcc          = msgData.Orbit[1];
            double currentGameTime = msgData.GameTime;

            // If this vessel was previously flagged, keep suppressing until the settle window elapses.
            if (SpuriousCaptureSuspectedAt.TryGetValue(vesselId, out double suspectedAt))
            {
                if (currentGameTime - suspectedAt < SpuriousCaptureSettleTime)
                    return false;

                // Settle window elapsed — clear the flag and allow through (log if still spurious-looking).
                SpuriousCaptureSuspectedAt.TryRemove(vesselId, out _);
            }

            if (LastKnownOrbitState.TryGetValue(vesselId, out var last))
            {
                double gameTimeDelta = currentGameTime - last.GameTime;

                if (last.BodyIndex != newBodyIndex && newEcc < 1.0 && gameTimeDelta > SpuriousCaptureMinTimeJump)
                {
                    LunaLog.LogWarning(
                        $"[LMP]: SPURIOUS ORBIT CAPTURE DETECTED for '{vesselName}' ({vesselId}): " +
                        $"SOI transition body-index {last.BodyIndex} → {newBodyIndex}, " +
                        $"ECC={newEcc:F4}, game-time step={gameTimeDelta:F0}s " +
                        $"(threshold={SpuriousCaptureMinTimeJump}s). " +
                        "A captured elliptical orbit appeared without a continuous burn sequence — " +
                        "likely a KSP PatchedConic precision error on vessel load or time warp. " +
                        $"Position updates suppressed for {SpuriousCaptureSettleTime}s of game-time. " +
                        "If this vessel genuinely performed an orbit insertion, this warning is a false positive.");

                    SpuriousCaptureSuspectedAt[vesselId] = currentGameTime;
                    return false;
                }
            }

            LastKnownOrbitState[vesselId] = (newBodyIndex, currentGameTime);
            return true;
        }

        /// <summary>Remove tracking state for a vessel (call when the vessel is removed from the scene).</summary>
        public static void ClearVesselOrbitHistory(Guid vesselId)
        {
            LastKnownOrbitState.TryRemove(vesselId, out _);
            SpuriousCaptureSuspectedAt.TryRemove(vesselId, out _);
        }

        /// <summary>Remove tracking state for ALL vessels (call on system disable / scene unload).</summary>
        public static void ClearAllOrbitHistory()
        {
            LastKnownOrbitState.Clear();
            SpuriousCaptureSuspectedAt.Clear();
        }

        #endregion

        public void SendMessage(IMessageData msg)
        {
            NetworkSender.QueueOutgoingMessage(MessageFactory.CreateNew<VesselCliMsg>(msg));
        }

        /// <summary>
        /// Sends a vessel position update
        /// </summary>
        /// <param name="vessel">Vessel to send the position</param>
        /// <param name="doOrbitDriverReadyCheck">Set it to true if you want to check if the driver is ready.
        /// Avoid checking it unless is really needed as it uses reflection that's slow</param>
        public void SendVesselPositionUpdate(Vessel vessel, bool doOrbitDriverReadyCheck = false)
        {
            if (vessel == null) return;

            if (doOrbitDriverReadyCheck && !vessel.orbitDriver.Ready())
            {
                //Orbit driver is not ready so wait max 10 frames until it's ready
                CoroutineUtil.StartConditionRoutine("SendVesselPositionUpdate",
                    () => SendVesselPositionUpdate(vessel),
                    () => vessel.orbitDriver.Ready(), 10);

            }
            else
            {
                var msg = CreateMessageFromVessel(vessel);
                if (msg == null) return;

                SendMessage(msg);
            }
        }

        public static VesselPositionMsgData CreateMessageFromVessel(Vessel vessel)
        {
            if (!OrbitParametersAreOk(vessel)) return null;

            var msgData = MessageFactory.CreateNewMessageData<VesselPositionMsgData>();
            msgData.PingSec = NetworkStatistics.PingSec;
            msgData.SubspaceId = WarpSystem.Singleton.CurrentSubspace;
            msgData.GameTime = TimeSyncSystem.UniversalTime;
            try
            {
                msgData.VesselId = vessel.id;
                msgData.BodyName = vessel.mainBody.bodyName;
                msgData.BodyIndex = vessel.mainBody.flightGlobalsIndex;
                msgData.Landed = vessel.Landed;
                msgData.Splashed = vessel.Splashed;

                SetSrfRelRotation(vessel, msgData);
                SetLatLonAlt(vessel, msgData);
                SetVelocityVector(vessel, msgData);
                SetNormalVector(vessel, msgData);
                SetOrbit(vessel, msgData);

                msgData.HeightFromTerrain = vessel.heightFromTerrain;

                if (MainSystem.BodiesGees.TryGetValue(vessel.mainBody, out var bodyGee))
                    msgData.HackingGravity = Math.Abs(bodyGee - vessel.mainBody.GeeASL) > 0.0001;
                msgData.HackingGravity = false;

                if (!ValidateOrbitPlausibility(vessel.id, vessel.vesselName, msgData))
                    return null;

                return msgData;
            }
            catch (Exception e)
            {
                LunaLog.Log($"[LMP]: Failed to get vessel position update, exception: {e}");
            }

            return null;
        }

        #region Set message values

        private static void SetOrbit(Vessel vessel, VesselPositionMsgData msgData)
        {
            msgData.Orbit[0] = vessel.orbit.inclination;
            msgData.Orbit[1] = vessel.orbit.eccentricity;
            msgData.Orbit[2] = vessel.orbit.semiMajorAxis;
            msgData.Orbit[3] = vessel.orbit.LAN;
            msgData.Orbit[4] = vessel.orbit.argumentOfPeriapsis;
            msgData.Orbit[5] = vessel.orbit.meanAnomalyAtEpoch;
            msgData.Orbit[6] = vessel.orbit.epoch;
            msgData.Orbit[7] = vessel.orbit.referenceBody.flightGlobalsIndex;
        }

        private static void SetVelocityVector(Vessel vessel, VesselPositionMsgData msgData)
        {
            var velVector = Quaternion.Inverse(vessel.mainBody.bodyTransform.rotation) * vessel.srf_velocity;
            msgData.VelocityVector[0] = velVector.x;
            msgData.VelocityVector[1] = velVector.y;
            msgData.VelocityVector[2] = velVector.z;
        }

        private static void SetNormalVector(Vessel vessel, VesselPositionMsgData msgData)
        {
            msgData.NormalVector[0] = vessel.terrainNormal.x;
            msgData.NormalVector[1] = vessel.terrainNormal.y;
            msgData.NormalVector[2] = vessel.terrainNormal.z;
        }

        private static void SetLatLonAlt(Vessel vessel, VesselPositionMsgData msgData)
        {
            msgData.LatLonAlt[0] = vessel.latitude;
            msgData.LatLonAlt[1] = vessel.longitude;
            msgData.LatLonAlt[2] = vessel.altitude;
        }

        private static void SetSrfRelRotation(Vessel vessel, VesselPositionMsgData msgData)
        {
            msgData.SrfRelRotation[0] = vessel.srfRelRotation.x;
            msgData.SrfRelRotation[1] = vessel.srfRelRotation.y;
            msgData.SrfRelRotation[2] = vessel.srfRelRotation.z;
            msgData.SrfRelRotation[3] = vessel.srfRelRotation.w;
        }

        #endregion

        /// <summary>
        /// Checks if the vessel contains NaN in any orbit parameter
        /// </summary>
        private static bool OrbitParametersAreOk(Vessel vessel)
        {
            var orbitParamsAreNan = double.IsNaN(vessel.orbit.inclination) ||
                                    double.IsNaN(vessel.orbit.eccentricity) ||
                                    double.IsNaN(vessel.orbit.semiMajorAxis) ||
                                    double.IsNaN(vessel.orbit.LAN) ||
                                    double.IsNaN(vessel.orbit.argumentOfPeriapsis) ||
                                    double.IsNaN(vessel.orbit.meanAnomalyAtEpoch) ||
                                    double.IsNaN(vessel.orbit.epoch) ||
                                    double.IsNaN(vessel.orbit.referenceBody.flightGlobalsIndex);

            return !orbitParamsAreNan;
        }
    }
}
