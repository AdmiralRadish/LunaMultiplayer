using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LmpClient.VesselUtilities
{
    /// <summary>
    /// Stops Breaking Ground KAL-1000 (ModuleRoboticController) track data from being wiped.
    ///
    /// The KAL's CONTROLLEDAXES/CONTROLLEDACTIONS nodes reference their controlled parts by
    /// part persistentId. LMP vessel loads can trigger stock KSP's persistentId-collision
    /// renaming, after which the controller cannot resolve its parts, Breaking Ground silently
    /// drops every axis/action, and the next BackupVessel() persists the controller EMPTY --
    /// permanently destroying the player's sequences (verified: every KAL on the live server
    /// and in every local LMP save has empty CONTROLLEDAXES/CONTROLLEDACTIONS).
    ///
    /// This guard runs on BOTH serializer paths (outgoing proto sends and incoming wire protos):
    /// whenever a KAL is seen WITH data we cache a deep copy keyed by vessel + part uid
    /// (flightID -- stable across persistentId renames); whenever a KAL is seen EMPTIED we
    /// splice the cached copy back in, remapping any stale part persistentIds inside the
    /// restored nodes to the vessel's current ones via the uid map.
    /// </summary>
    public static class RoboticControllerGuard
    {
        private const string ModuleName = "ModuleRoboticController";
        private static readonly string[] GuardedNodeNames = { "CONTROLLEDAXES", "CONTROLLEDACTIONS" };

        private class CachedKal
        {
            public ConfigNode[] Nodes;
            public Dictionary<uint, string> UidToPersistentId;
        }

        /// <summary>Key: vesselId. Inner key: part uid (flightID) * 16 + module index.</summary>
        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<ulong, CachedKal>> Cache =
            new ConcurrentDictionary<Guid, ConcurrentDictionary<ulong, CachedKal>>();

        public static void RemoveVessel(Guid vesselId) => Cache.TryRemove(vesselId, out _);

        public static void Clear() => Cache.Clear();

        /// <summary>
        /// Caches populated KAL nodes and restores wiped ones in the given serialized vessel node.
        /// Safe to call from any thread (operates only on the ConfigNode and the cache).
        /// </summary>
        public static void ProcessVesselNode(ConfigNode vesselNode, Guid vesselId, string context)
        {
            try
            {
                var partNodes = vesselNode.GetNodes("PART");
                if (partNodes == null || partNodes.Length == 0) return;

                Dictionary<uint, string> uidToPid = null;

                foreach (var partNode in partNodes)
                {
                    var moduleIndex = -1;
                    foreach (var moduleNode in partNode.GetNodes("MODULE"))
                    {
                        if (moduleNode.GetValue("name") != ModuleName) continue;
                        moduleIndex++;

                        if (!uint.TryParse(partNode.GetValue("uid"), out var uid)) continue;
                        var key = (ulong)uid * 16 + (ulong)(moduleIndex & 15);

                        var hasData = false;
                        foreach (var nodeName in GuardedNodeNames)
                        {
                            var n = moduleNode.GetNode(nodeName);
                            if (n != null && n.CountNodes > 0) { hasData = true; break; }
                        }

                        if (uidToPid == null) uidToPid = BuildUidMap(partNodes);

                        var vesselCache = Cache.GetOrAdd(vesselId, _ => new ConcurrentDictionary<ulong, CachedKal>());
                        if (hasData)
                        {
                            var copies = new List<ConfigNode>();
                            foreach (var nodeName in GuardedNodeNames)
                            {
                                var n = moduleNode.GetNode(nodeName);
                                if (n != null) copies.Add(n.CreateCopy());
                            }
                            vesselCache[key] = new CachedKal { Nodes = copies.ToArray(), UidToPersistentId = uidToPid };
                        }
                        else if (vesselCache.TryGetValue(key, out var cached))
                        {
                            //KAL arrived/left EMPTY but we hold a populated copy -> restore it
                            var pidRemap = BuildPersistentIdRemap(cached.UidToPersistentId, uidToPid);
                            foreach (var cachedNode in cached.Nodes)
                            {
                                moduleNode.RemoveNodes(cachedNode.name);
                                var restored = cachedNode.CreateCopy();
                                RemapPersistentIds(restored, pidRemap);
                                moduleNode.AddNode(restored);
                            }
                            LunaLog.Log($"[LMP]: RoboticControllerGuard restored wiped KAL data on vessel {vesselId}, part uid {uid} ({context})");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LunaLog.LogError($"[LMP]: RoboticControllerGuard error ({context}): {e}");
            }
        }

        private static Dictionary<uint, string> BuildUidMap(ConfigNode[] partNodes)
        {
            var map = new Dictionary<uint, string>(partNodes.Length);
            foreach (var p in partNodes)
            {
                if (uint.TryParse(p.GetValue("uid"), out var uid))
                {
                    var pid = p.GetValue("persistentId");
                    if (!string.IsNullOrEmpty(pid)) map[uid] = pid;
                }
            }
            return map;
        }

        /// <summary>Old persistentId -> current persistentId for parts whose uid matches.</summary>
        private static Dictionary<string, string> BuildPersistentIdRemap(Dictionary<uint, string> oldMap, Dictionary<uint, string> newMap)
        {
            var remap = new Dictionary<string, string>();
            if (oldMap == null || newMap == null) return remap;
            foreach (var kvp in oldMap)
            {
                if (newMap.TryGetValue(kvp.Key, out var newPid) && newPid != kvp.Value)
                    remap[kvp.Value] = newPid;
            }
            return remap;
        }

        private static void RemapPersistentIds(ConfigNode node, Dictionary<string, string> remap)
        {
            if (remap.Count == 0) return;
            for (var i = 0; i < node.CountValues; i++)
            {
                var v = node.values[i];
                if (v.name.IndexOf("persistentId", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    remap.TryGetValue(v.value, out var newPid))
                {
                    v.value = newPid;
                }
            }
            for (var i = 0; i < node.CountNodes; i++)
                RemapPersistentIds(node.nodes[i], remap);
        }
    }
}
