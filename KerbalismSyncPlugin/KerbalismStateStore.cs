// KerbalismStateStore.cs — Server-side authoritative state for Kerbalism data.
//
// Stores kerbal health, storm state, vessel metadata, and landmark achievements
// received from clients.  The binary wire format matches SyncProtocol.cs v4 from
// the client mod identically (same BinaryWriter/BinaryReader layout).
// State is persisted to a binary file on disk for crash recovery.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Server.Context;
using Server.Log;

namespace KerbalismSyncPlugin
{
    internal class KerbalismStateStore
    {
        // ── Data structures matching the client-side protocol v4 ─

        private struct KerbalHealthData
        {
            public string Name;
            public bool Rescue;
            public bool Disabled;
            public bool EvaDead;
            public string Sickbay; // v4: string (semicolon-separated)
            public List<RuleDataEntry> Rules;
        }

        private struct RuleDataEntry
        {
            public string RuleName;
            public double Problem;
            public uint Message;
            public double TimeSince;
            public bool Lifetime;
            public double Rate;
            public double Degeneration;
        }

        private struct StormStateData
        {
            public string BodyName;
            public double StormTime;
            public double StormDuration;
            public double StormGeneration;
            public int StormState;
            public uint MsgStorm;            // v4: uint (was bool)
            public bool DisplayWarning;      // v4: added
        }

        private struct VesselStormData
        {
            public double StormTime;
            public double StormDuration;
            public double StormGeneration;
            public int StormState;
            public uint MsgStorm;
            public bool DisplayWarning;
        }

        private struct VesselDataSnapshot
        {
            public Guid VesselId;
            public double ScienceTransmitted;
            public bool DeviceTransmit;
            public bool CfgEc;
            public bool CfgSupply;
            public bool CfgSignal;
            public bool CfgMalfunction;
            public bool CfgStorm;
            public bool CfgScript;
            public bool CfgHighlights;
            public bool CfgShowLink;
            public bool CfgShow;
            public VesselStormData Storm;
        }

        private struct LandmarkSnapshot
        {
            public bool BeltCrossing;
            public bool MannedOrbit;
            public bool SpaceHarvest;
            public bool SpaceAnalysis;
            public bool HeliopauseCrossing;
        }

        // Proto field entry (v5) — mirrors client SyncProtocol structs
        private struct ProtoModuleFieldEntry
        {
            public uint   PartFlightId;
            public string ModuleName;
            public List<(string Name, string Value)> Fields;
        }

        // ── State ───────────────────────────────────────────────

        private readonly Dictionary<string, KerbalHealthData> _kerbals = new();
        private readonly Dictionary<string, StormStateData> _storms = new();
        private readonly Dictionary<Guid, VesselDataSnapshot> _vessels = new();
        private LandmarkSnapshot _landmarks;
        // Proto field store: vessel → list of module field snapshots
        // Used to restore experiment/device states on client connect (v5)
        private readonly Dictionary<Guid, List<ProtoModuleFieldEntry>> _protoFields = new();
        private readonly object _lock = new();
        private string _savePath;

        // ── Merge incoming data ─────────────────────────────────

        internal void MergeKerbalHealth(byte[] data, int numBytes)
        {
            try
            {
                using var ms = new MemoryStream(data, 0, numBytes);
                using var r = new BinaryReader(ms, Encoding.UTF8);
                r.ReadByte(); // skip message type

                int count = r.ReadInt32();
                lock (_lock)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var k = ReadKerbalHealth(r);
                        if (_kerbals.TryGetValue(k.Name, out var existing))
                        {
                            // Merge rules: for radiation (problem), take higher value
                            k = MergeKerbalRules(existing, k);
                        }
                        _kerbals[k.Name] = k;
                    }
                }
            }
            catch (Exception ex)
            {
                LunaLog.Error($"[KerbalismLMPSync] Error merging kerbal health: {ex.Message}");
            }
        }

        internal void MergeStormState(byte[] data, int numBytes)
        {
            try
            {
                using var ms = new MemoryStream(data, 0, numBytes);
                using var r = new BinaryReader(ms, Encoding.UTF8);
                r.ReadByte(); // skip message type

                int count = r.ReadInt32();
                lock (_lock)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var s = ReadStormState(r);
                        _storms[s.BodyName] = s;
                    }
                }
            }
            catch (Exception ex)
            {
                LunaLog.Error($"[KerbalismLMPSync] Error merging storm state: {ex.Message}");
            }
        }

        internal void MergeVesselData(byte[] data, int numBytes)
        {
            try
            {
                using var ms = new MemoryStream(data, 0, numBytes);
                using var r = new BinaryReader(ms, Encoding.UTF8);
                r.ReadByte(); // skip message type

                var v = ReadVesselData(r);
                lock (_lock)
                {
                    _vessels[v.VesselId] = v;
                }
            }
            catch (Exception ex)
            {
                LunaLog.Error($"[KerbalismLMPSync] Error merging vessel data: {ex.Message}");
            }
        }

        internal void MergeLandmarks(byte[] data, int numBytes)
        {
            try
            {
                using var ms = new MemoryStream(data, 0, numBytes);
                using var r = new BinaryReader(ms, Encoding.UTF8);
                r.ReadByte(); // skip message type

                var lm = ReadLandmarks(r);
                lock (_lock)
                {
                    // Landmarks are one-way achievements: merge with OR
                    _landmarks.BeltCrossing |= lm.BeltCrossing;
                    _landmarks.MannedOrbit |= lm.MannedOrbit;
                    _landmarks.SpaceHarvest |= lm.SpaceHarvest;
                    _landmarks.SpaceAnalysis |= lm.SpaceAnalysis;
                    _landmarks.HeliopauseCrossing |= lm.HeliopauseCrossing;
                }
            }
            catch (Exception ex)
            {
                LunaLog.Error($"[KerbalismLMPSync] Error merging landmarks: {ex.Message}");
            }
        }

        internal void MergeProtoFields(byte[] data, int numBytes)
        {
            try
            {
                using var ms = new MemoryStream(data, 0, numBytes);
                using var r = new BinaryReader(ms, Encoding.UTF8);
                r.ReadByte(); // skip message type (ProtoFieldBatch=8)

                var vessels = ReadProtoVesselFieldList(r);
                lock (_lock)
                {
                    foreach (var v in vessels)
                        _protoFields[v.VesselId] = v.Modules;
                }
            }
            catch (Exception ex)
            {
                LunaLog.Error($"[KerbalismLMPSync] Error merging proto fields: {ex.Message}");
            }
        }

        private static KerbalHealthData MergeKerbalRules(KerbalHealthData existing, KerbalHealthData incoming)
        {
            if (existing.Rules != null && incoming.Rules != null)
            {
                var existingByName = new Dictionary<string, RuleDataEntry>();
                foreach (var r in existing.Rules)
                    existingByName[r.RuleName] = r;

                for (int i = 0; i < incoming.Rules.Count; i++)
                {
                    var ir = incoming.Rules[i];
                    if (existingByName.TryGetValue(ir.RuleName, out var er))
                    {
                        if (er.Problem > ir.Problem)
                        {
                            ir.Problem = er.Problem;
                            incoming.Rules[i] = ir;
                        }
                    }
                }
            }
            return incoming;
        }

        // ── Build full snapshot ─────────────────────────────────
        // v5 format: [type=1][kerbals][storms][vesselDataList][landmarks][protoFields]

        internal byte[] BuildFullSnapshot()
        {
            lock (_lock)
            {
                using var ms = new MemoryStream();
                using var w = new BinaryWriter(ms, Encoding.UTF8);

                w.Write((byte)1); // SyncMessageType.FullSnapshot

                // Kerbals
                w.Write(_kerbals.Count);
                foreach (var kv in _kerbals)
                    WriteKerbalHealth(w, kv.Value);

                // Storms
                w.Write(_storms.Count);
                foreach (var kv in _storms)
                    WriteStormState(w, kv.Value);

                // Vessels (v4+)
                w.Write(_vessels.Count);
                foreach (var kv in _vessels)
                    WriteVesselData(w, kv.Value);

                // Landmarks (v4+)
                WriteLandmarks(w, _landmarks);

                // Proto fields (v5)
                w.Write(_protoFields.Count);
                foreach (var kv in _protoFields)
                {
                    w.Write(kv.Key.ToByteArray());
                    WriteProtoModuleList(w, kv.Value);
                }

                return ms.ToArray();
            }
        }

        // ── Binary read/write (identical wire format to client v4) ─

        private static KerbalHealthData ReadKerbalHealth(BinaryReader r)
        {
            var k = new KerbalHealthData
            {
                Name = r.ReadString(),
                Rescue = r.ReadBoolean(),
                Disabled = r.ReadBoolean(),
                EvaDead = r.ReadBoolean(),
                Sickbay = r.ReadString() // v4: string
            };
            int ruleCount = r.ReadInt32();
            k.Rules = new List<RuleDataEntry>(ruleCount);
            for (int j = 0; j < ruleCount; j++)
            {
                k.Rules.Add(new RuleDataEntry
                {
                    RuleName = r.ReadString(),
                    Problem = r.ReadDouble(),
                    Message = r.ReadUInt32(),
                    TimeSince = r.ReadDouble(),
                    Lifetime = r.ReadBoolean(),
                    Rate = r.ReadDouble(),
                    Degeneration = r.ReadDouble()
                });
            }
            return k;
        }

        private static StormStateData ReadStormState(BinaryReader r)
        {
            return new StormStateData
            {
                BodyName = r.ReadString(),
                StormTime = r.ReadDouble(),
                StormDuration = r.ReadDouble(),
                StormGeneration = r.ReadDouble(),
                StormState = r.ReadInt32(),
                MsgStorm = r.ReadUInt32(),        // v4: uint
                DisplayWarning = r.ReadBoolean()    // v4
            };
        }

        private static VesselDataSnapshot ReadVesselData(BinaryReader r)
        {
            var v = new VesselDataSnapshot();
            v.VesselId = new Guid(r.ReadBytes(16));
            v.ScienceTransmitted = r.ReadDouble();
            v.DeviceTransmit = r.ReadBoolean();
            v.CfgEc = r.ReadBoolean();
            v.CfgSupply = r.ReadBoolean();
            v.CfgSignal = r.ReadBoolean();
            v.CfgMalfunction = r.ReadBoolean();
            v.CfgStorm = r.ReadBoolean();
            v.CfgScript = r.ReadBoolean();
            v.CfgHighlights = r.ReadBoolean();
            v.CfgShowLink = r.ReadBoolean();
            v.CfgShow = r.ReadBoolean();
            v.Storm.StormTime = r.ReadDouble();
            v.Storm.StormDuration = r.ReadDouble();
            v.Storm.StormGeneration = r.ReadDouble();
            v.Storm.StormState = r.ReadInt32();
            v.Storm.MsgStorm = r.ReadUInt32();
            v.Storm.DisplayWarning = r.ReadBoolean();
            return v;
        }

        private static LandmarkSnapshot ReadLandmarks(BinaryReader r)
        {
            return new LandmarkSnapshot
            {
                BeltCrossing = r.ReadBoolean(),
                MannedOrbit = r.ReadBoolean(),
                SpaceHarvest = r.ReadBoolean(),
                SpaceAnalysis = r.ReadBoolean(),
                HeliopauseCrossing = r.ReadBoolean()
            };
        }

        private static void WriteKerbalHealth(BinaryWriter w, KerbalHealthData k)
        {
            w.Write(k.Name ?? string.Empty);
            w.Write(k.Rescue);
            w.Write(k.Disabled);
            w.Write(k.EvaDead);
            w.Write(k.Sickbay ?? string.Empty); // v4: string
            w.Write(k.Rules?.Count ?? 0);
            if (k.Rules != null)
            {
                foreach (var re in k.Rules)
                {
                    w.Write(re.RuleName ?? string.Empty);
                    w.Write(re.Problem);
                    w.Write(re.Message);
                    w.Write(re.TimeSince);
                    w.Write(re.Lifetime);
                    w.Write(re.Rate);
                    w.Write(re.Degeneration);
                }
            }
        }

        private static void WriteStormState(BinaryWriter w, StormStateData s)
        {
            w.Write(s.BodyName ?? string.Empty);
            w.Write(s.StormTime);
            w.Write(s.StormDuration);
            w.Write(s.StormGeneration);
            w.Write(s.StormState);
            w.Write(s.MsgStorm);           // v4: uint
            w.Write(s.DisplayWarning);      // v4
        }

        private static void WriteVesselData(BinaryWriter w, VesselDataSnapshot v)
        {
            w.Write(v.VesselId.ToByteArray());
            w.Write(v.ScienceTransmitted);
            w.Write(v.DeviceTransmit);
            w.Write(v.CfgEc);
            w.Write(v.CfgSupply);
            w.Write(v.CfgSignal);
            w.Write(v.CfgMalfunction);
            w.Write(v.CfgStorm);
            w.Write(v.CfgScript);
            w.Write(v.CfgHighlights);
            w.Write(v.CfgShowLink);
            w.Write(v.CfgShow);
            w.Write(v.Storm.StormTime);
            w.Write(v.Storm.StormDuration);
            w.Write(v.Storm.StormGeneration);
            w.Write(v.Storm.StormState);
            w.Write(v.Storm.MsgStorm);
            w.Write(v.Storm.DisplayWarning);
        }

        private static void WriteLandmarks(BinaryWriter w, LandmarkSnapshot lm)
        {
            w.Write(lm.BeltCrossing);
            w.Write(lm.MannedOrbit);
            w.Write(lm.SpaceHarvest);
            w.Write(lm.SpaceAnalysis);
            w.Write(lm.HeliopauseCrossing);
        }

        // ── Proto field read/write helpers (v5) ─────────────────

        private static void WriteProtoModuleList(BinaryWriter w, List<ProtoModuleFieldEntry> modules)
        {
            w.Write(modules?.Count ?? 0);
            if (modules == null) return;
            foreach (var m in modules)
            {
                w.Write(m.PartFlightId);
                w.Write(m.ModuleName ?? string.Empty);
                w.Write(m.Fields?.Count ?? 0);
                if (m.Fields == null) continue;
                foreach (var (name, value) in m.Fields)
                {
                    w.Write(name  ?? string.Empty);
                    w.Write(value ?? string.Empty);
                }
            }
        }

        // Returns a list of (vesselId, modules) pairs — mirrors the client's ProtoVesselFieldData
        private record ProtoVesselEntry(Guid VesselId, List<ProtoModuleFieldEntry> Modules);

        private static List<ProtoVesselEntry> ReadProtoVesselFieldList(BinaryReader r)
        {
            int vesselCount = r.ReadInt32();
            var result = new List<ProtoVesselEntry>(vesselCount);
            for (int i = 0; i < vesselCount; i++)
            {
                var vesselId = new Guid(r.ReadBytes(16));
                int modCount = r.ReadInt32();
                var modules = new List<ProtoModuleFieldEntry>(modCount);
                for (int j = 0; j < modCount; j++)
                {
                    var m = new ProtoModuleFieldEntry
                    {
                        PartFlightId = r.ReadUInt32(),
                        ModuleName   = r.ReadString(),
                        Fields       = new List<(string, string)>()
                    };
                    int fieldCount = r.ReadInt32();
                    for (int k = 0; k < fieldCount; k++)
                    {
                        string name  = r.ReadString();
                        string value = r.ReadString();
                        m.Fields.Add((name, value));
                    }
                    modules.Add(m);
                }
                result.Add(new ProtoVesselEntry(vesselId, modules));
            }
            return result;
        }

        private string GetSavePath()
        {
            if (_savePath != null) return _savePath;
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dataDir = Path.Combine(baseDir, "Plugins", "KerbalismSyncData");
            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);
            _savePath = Path.Combine(dataDir, "kerbalism_state.bin");
            return _savePath;
        }

        internal void SaveToDisk()
        {
            try
            {
                byte[] snapshot = BuildFullSnapshot();
                File.WriteAllBytes(GetSavePath(), snapshot);
            }
            catch (Exception ex)
            {
                LunaLog.Error($"[KerbalismLMPSync] Error saving state to disk: {ex.Message}");
            }
        }

        internal void LoadFromDisk()
        {
            var path = GetSavePath();
            if (!File.Exists(path))
            {
                LunaLog.Debug("[KerbalismLMPSync] No saved state found — starting fresh");
                return;
            }

            try
            {
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < 2) return;

                using var ms = new MemoryStream(data);
                using var r = new BinaryReader(ms, Encoding.UTF8);
                r.ReadByte(); // skip message type (FullSnapshot=1)

                lock (_lock)
                {
                    // v4 format: kerbals, storms, vessels, landmarks
                    // Old v3 files will fail on the sickbay string read — catch and start fresh
                    int kerbalCount = r.ReadInt32();
                    _kerbals.Clear();
                    for (int i = 0; i < kerbalCount; i++)
                    {
                        var k = ReadKerbalHealth(r);
                        _kerbals[k.Name] = k;
                    }

                    int stormCount = r.ReadInt32();
                    _storms.Clear();
                    for (int i = 0; i < stormCount; i++)
                    {
                        var s = ReadStormState(r);
                        _storms[s.BodyName] = s;
                    }

                    // v4 additions — may not exist in old files
                    if (ms.Position < ms.Length)
                    {
                        int vesselCount = r.ReadInt32();
                        _vessels.Clear();
                        for (int i = 0; i < vesselCount; i++)
                        {
                            var v = ReadVesselData(r);
                            _vessels[v.VesselId] = v;
                        }

                        if (ms.Position < ms.Length)
                        {
                            _landmarks = ReadLandmarks(r);
                        }

                        // v5: proto fields
                        if (ms.Position < ms.Length)
                        {
                            var protoEntries = ReadProtoVesselFieldList(r);
                            _protoFields.Clear();
                            foreach (var entry in protoEntries)
                                _protoFields[entry.VesselId] = entry.Modules;
                        }
                    }
                }
                LunaLog.Debug($"[KerbalismLMPSync] Loaded state from disk: {_kerbals.Count} kerbals, {_storms.Count} storms, {_vessels.Count} vessels, {_protoFields.Count} proto field sets");
            }
            catch (Exception ex)
            {
                LunaLog.Error($"[KerbalismLMPSync] Error loading state from disk (v3→v4 format change?): {ex.Message}");
                LunaLog.Debug("[KerbalismLMPSync] Starting with fresh state");
                lock (_lock)
                {
                    _kerbals.Clear();
                    _storms.Clear();
                    _vessels.Clear();
                    _landmarks = default;
                }
            }
        }
    }
}
