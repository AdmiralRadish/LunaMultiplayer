using LmpCommon.Message.Data.Vessel;
using Server.Log;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;

namespace Server.System.Vessel
{
    /// <summary>
    /// We try to avoid working with protovessels as much as possible as they can be huge files.
    /// This class patches the vessel file with the information messages we receive about a position and other vessel properties.
    /// This way we send the whole vessel definition only when there are parts that have changed 
    /// </summary>
    public partial class VesselDataUpdater
    {
        /// <summary>
        /// Update the vessel files with position data max at a 2,5 seconds interval
        /// </summary>
        private const int FilePositionUpdateIntervalMs = 2500;

        /// <summary>
        /// Avoid updating the vessel files so often as otherwise the server will lag a lot!
        /// </summary>
        private static readonly ConcurrentDictionary<Guid, DateTime> LastPositionUpdateDictionary = new ConcurrentDictionary<Guid, DateTime>();

        #region Spurious orbit-capture detection (server-side)

        // Minimum game-time gap (seconds) between the server's stored EPH and the incoming update
        // that, combined with a body change and ECC < 1, indicates a spurious KSP capture.
        private const double ServerSpuriousCaptureMinTimeJump = 300.0;

        // After a vessel is flagged, reject updates for this many game-seconds.
        private const double ServerSpuriousCaptureSettleTime = 120.0;

        // Tracks game-time at which a spurious capture was detected per vessel.
        private static readonly ConcurrentDictionary<Guid, double> ServerSpuriousCaptureFlaggedAt =
            new ConcurrentDictionary<Guid, double>();

        /// <summary>
        /// Validates whether an incoming position message represents a plausible orbit update.
        /// Returns false — and logs a server warning — when a captured elliptical orbit appears
        /// in a new reference body after a large game-time gap relative to the server's stored EPH.
        /// This catches KSP PatchedConic precision errors on client vessel load or time warp,
        /// which would otherwise corrupt the authoritative server vessel state.
        /// The check is performed against the server's own stored orbit, so it is effective even
        /// on the first position update of a new session (Risk #3) and even if the client-side
        /// suppress window has elapsed (Risk #2).
        /// </summary>
        public static bool IsSpuriousCapture(VesselPositionMsgData msgData, string playerName)
        {
            int    newRef         = (int)msgData.Orbit[7];
            double newEcc         = msgData.Orbit[1];
            double newGameTime    = msgData.GameTime;  // Orbit[6] is epoch/EPH; GameTime is current UT
            double newEpoch       = msgData.Orbit[6];

            // If vessel is already flagged, keep rejecting until settle window elapses.
            if (ServerSpuriousCaptureFlaggedAt.TryGetValue(msgData.VesselId, out double flaggedAt))
            {
                if (newGameTime - flaggedAt < ServerSpuriousCaptureSettleTime)
                    return true;

                // Settle window elapsed — clear flag and re-evaluate fresh below.
                ServerSpuriousCaptureFlaggedAt.TryRemove(msgData.VesselId, out _);
            }

            // Read the server's current stored orbit for this vessel.
            if (!VesselStoreSystem.CurrentVessels.TryGetValue(msgData.VesselId, out var vessel))
                return false;  // Unknown vessel — let it through; store will create it.

            if (!int.TryParse(vessel.Orbit.GetSingle("REF")?.Value, out int storedRef))
                return false;  // No stored REF yet (vessel just created) — let through.

            if (!double.TryParse(vessel.Orbit.GetSingle("EPH")?.Value,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture, out double storedEph))
                return false;

            double gameTimeDelta = newGameTime - storedEph;
            double incomingEpochDelta = newGameTime - newEpoch;

            if (storedRef != newRef &&
                newEcc < 1.0 &&
                gameTimeDelta > ServerSpuriousCaptureMinTimeJump &&
                incomingEpochDelta > ServerSpuriousCaptureMinTimeJump)
            {
                LunaLog.Warning(
                    $"[SpuriousCapture] Vessel {msgData.VesselId} from {playerName}: " +
                    $"SOI REF {storedRef} → {newRef}, ECC={newEcc:F4}, " +
                    $"game-time jump={gameTimeDelta:F0}s, incoming epoch delta={incomingEpochDelta:F0}s " +
                    $"(threshold={ServerSpuriousCaptureMinTimeJump}s). " +
                    $"Likely KSP PatchedConic error on vessel load/warp. " +
                    $"Position update REJECTED for {ServerSpuriousCaptureSettleTime}s game-time. " +
                    "Server orbit state preserved. If this vessel genuinely performed an orbit " +
                    "insertion, send a full vessel Proto message to override.");

                ServerSpuriousCaptureFlaggedAt[msgData.VesselId] = newGameTime;
                return true;
            }

            return false;
        }

        /// <summary>Clears spurious-capture suppression state for a vessel (e.g. on vessel remove).</summary>
        public static void ClearSpuriousCaptureFlag(Guid vesselId)
        {
            ServerSpuriousCaptureFlaggedAt.TryRemove(vesselId, out _);
        }

        #endregion

        /// <summary>
        /// We received a position information from a player
        /// Then we rewrite the vesselproto with that last information so players that connect later receive an updated vesselproto
        /// </summary>
        public static void WritePositionDataToFile(VesselBaseMsgData message)
        {
            if (!(message is VesselPositionMsgData msgData)) return;
            if (VesselContext.RemovedVessels.Contains(msgData.VesselId)) return;

            if (!LastPositionUpdateDictionary.TryGetValue(msgData.VesselId, out var lastUpdated) || (DateTime.Now - lastUpdated).TotalMilliseconds > FilePositionUpdateIntervalMs)
            {
                LastPositionUpdateDictionary.AddOrUpdate(msgData.VesselId, DateTime.Now, (key, existingVal) => DateTime.Now);

                _ = Task.Run(() =>
                {
                    lock (Semaphore.GetOrAdd(msgData.VesselId, new object()))
                    {
                        if (!VesselStoreSystem.CurrentVessels.TryGetValue(msgData.VesselId, out var vessel)) return;

                        vessel.Fields.Update("lat", msgData.LatLonAlt[0].ToString(CultureInfo.InvariantCulture));
                        vessel.Fields.Update("lon", msgData.LatLonAlt[1].ToString(CultureInfo.InvariantCulture));
                        vessel.Fields.Update("alt", msgData.LatLonAlt[2].ToString(CultureInfo.InvariantCulture));

                        vessel.Fields.Update("hgt", msgData.HeightFromTerrain.ToString(CultureInfo.InvariantCulture));

                        vessel.Fields.Update("nrm", $"{msgData.NormalVector[0].ToString(CultureInfo.InvariantCulture)}," +
                                                    $"{msgData.NormalVector[1].ToString(CultureInfo.InvariantCulture)}," +
                                                    $"{msgData.NormalVector[2].ToString(CultureInfo.InvariantCulture)}");

                        vessel.Fields.Update("rot", $"{msgData.SrfRelRotation[0].ToString(CultureInfo.InvariantCulture)}," +
                                                    $"{msgData.SrfRelRotation[1].ToString(CultureInfo.InvariantCulture)}," +
                                                    $"{msgData.SrfRelRotation[2].ToString(CultureInfo.InvariantCulture)}," +
                                                    $"{msgData.SrfRelRotation[3].ToString(CultureInfo.InvariantCulture)}");

                        vessel.Orbit.Update("INC", msgData.Orbit[0].ToString(CultureInfo.InvariantCulture));
                        vessel.Orbit.Update("ECC", msgData.Orbit[1].ToString(CultureInfo.InvariantCulture));
                        vessel.Orbit.Update("SMA", msgData.Orbit[2].ToString(CultureInfo.InvariantCulture));
                        vessel.Orbit.Update("LAN", msgData.Orbit[3].ToString(CultureInfo.InvariantCulture));
                        vessel.Orbit.Update("LPE", msgData.Orbit[4].ToString(CultureInfo.InvariantCulture));
                        vessel.Orbit.Update("MNA", msgData.Orbit[5].ToString(CultureInfo.InvariantCulture));
                        vessel.Orbit.Update("EPH", msgData.Orbit[6].ToString(CultureInfo.InvariantCulture));
                        vessel.Orbit.Update("REF", msgData.Orbit[7].ToString(CultureInfo.InvariantCulture));
                        vessel.Orbit.Update("body", msgData.BodyName);
                    }
                });
            }
        }
    }
}
