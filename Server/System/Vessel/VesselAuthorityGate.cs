using Server.Client;
using Server.Log;
using System;
using System.Collections.Concurrent;

namespace Server.System.Vessel
{
    /// <summary>
    /// Decides whether incoming vessel data from a client should be persisted to the server's authoritative
    /// vessel state (in-memory dictionary + on-disk &lt;guid&gt;.txt). Live relay to other clients is never gated
    /// here - this only protects the stored copy.
    ///
    /// Rationale (RSS fork drift defense):
    /// In stock LMP, any client that comes within physics range of an unattended vessel will pack/unpack it
    /// in their KSP session, accumulate small floating-point error in the orbital elements, and relay that
    /// noisy state back to the server. The server stores it. Over weeks/years this produces visible drift on
    /// idle craft (eg. space stations whose Ap/Pe shift by hundreds of metres between server restarts even
    /// when no one was piloting them).
    ///
    /// This gate enforces a simple authority rule: a client may only mutate the stored copy of a vessel if it
    /// holds (or no one holds) the Control lock for that vessel. The pattern is the same one already used by
    /// <see cref="Server.Message.VesselMsgReader.HandleVesselRemove" /> - we just extend it to proto/position
    /// persistence as well.
    /// </summary>
    public static class VesselAuthorityGate
    {
        private static readonly ConcurrentDictionary<Guid, long> RejectedCounts = new ConcurrentDictionary<Guid, long>();
        private static readonly ConcurrentDictionary<Guid, DateTime> LastRejectLogUtc = new ConcurrentDictionary<Guid, DateTime>();
        private static readonly TimeSpan LogThrottle = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Returns true if the client is allowed to mutate the stored copy of the given vessel.
        /// Returns true when no client info is available, or when no Control lock currently exists for the vessel
        /// (so first-upload and ownership-less vessels keep working). The gate is hard-wired on by design; there is
        /// no runtime kill switch because allowing arbitrary clients to overwrite authoritative orbital state has
        /// no legitimate use case.
        /// Rejections are logged at debug level with per-vessel throttling so a log file is not flooded.
        /// </summary>
        /// <param name="client">The sending client.</param>
        /// <param name="vesselId">The target vessel.</param>
        /// <param name="operation">Short label for the persistence operation (eg. "Proto", "Position"). Used in log only.</param>
        public static bool CanPersist(ClientStructure client, Guid vesselId, string operation)
        {
            if (client == null) return true;

            if (!LockSystem.LockQuery.ControlLockExists(vesselId)) return true;
            if (LockSystem.LockQuery.ControlLockBelongsToPlayer(vesselId, client.PlayerName)) return true;

            var count = RejectedCounts.AddOrUpdate(vesselId, 1, (_, v) => v + 1);
            var now = DateTime.UtcNow;
            if (!LastRejectLogUtc.TryGetValue(vesselId, out var last) || now - last > LogThrottle)
            {
                LastRejectLogUtc[vesselId] = now;
                var owner = LockSystem.LockQuery.GetControlLockOwner(vesselId) ?? "<unknown>";
                LunaLog.Debug($"[AuthorityGate] Rejected {operation} for vessel {vesselId} from {client.PlayerName} " +
                              $"(rejections so far: {count}). Control lock owner: {owner}.");
            }
            return false;
        }
    }
}
