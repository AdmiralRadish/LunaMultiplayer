using ByteSizeLib;
using LmpCommon.Message.Data.Vessel;
using LmpCommon.Message.Interface;
using LmpCommon.Message.Server;
using LmpCommon.Message.Types;
using Server.Client;
using Server.Context;
using Server.Log;
using Server.Message.Base;
using Server.Server;
using Server.System;
using Server.System.Vessel;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;

namespace Server.Message
{
    public class VesselMsgReader : ReaderBase
    {
        private enum SpaceObjectClass
        {
            Normal,
            Untouched,
            Colonized
        }

        private static readonly ConcurrentDictionary<Guid, string> InitialUploaders = new ConcurrentDictionary<Guid, string>();

        public override void HandleMessage(ClientStructure client, IClientMessageBase message)
        {
            var messageData = message.Data as VesselBaseMsgData;
            switch (messageData?.VesselMessageType)
            {
                case VesselMessageType.Sync:
                    HandleVesselsSync(client, messageData);
                    message.Recycle();
                    break;
                case VesselMessageType.Proto:
                    HandleVesselProto(client, messageData);
                    break;
                case VesselMessageType.Remove:
                    HandleVesselRemove(client, messageData);
                    break;
                case VesselMessageType.Position:
                    var posVesselId = ((VesselPositionMsgData)messageData).VesselId;
                    // For untouched asteroid/comet objects we do not relay position updates.
                    // Their long-term orbit must come from authoritative proto/orbit state,
                    // not from live packed/unpacked position noise.
                    if (IsCometOrAsteroid(posVesselId))
                        break;

                    MessageQueuer.RelayMessage<VesselSrvMsg>(client, messageData);
                    // Comets/asteroids are relayed live but never persisted to disk to avoid accumulated drift.
                    if (client.Subspace == WarpContext.LatestSubspace.Id
                        && VesselAuthorityGate.CanPersist(client, posVesselId, "Position")
                        && !IsCometOrAsteroid(posVesselId))
                        VesselDataUpdater.WritePositionDataToFile(messageData);
                    break;
                case VesselMessageType.Flightstate:
                    MessageQueuer.RelayMessage<VesselSrvMsg>(client, messageData);
                    if (VesselAuthorityGate.CanPersist(client, ((VesselFlightStateMsgData)messageData).VesselId, "Flightstate"))
                        VesselDataUpdater.WriteFlightstateDataToFile(messageData);
                    break;
                case VesselMessageType.Update:
                    if (VesselAuthorityGate.CanPersist(client, ((VesselUpdateMsgData)messageData).VesselId, "Update"))
                        VesselDataUpdater.WriteUpdateDataToFile(messageData);
                    MessageQueuer.RelayMessage<VesselSrvMsg>(client, messageData);
                    break;
                case VesselMessageType.Resource:
                    if (VesselAuthorityGate.CanPersist(client, ((VesselResourceMsgData)messageData).VesselId, "Resource"))
                        VesselDataUpdater.WriteResourceDataToFile(messageData);
                    MessageQueuer.RelayMessage<VesselSrvMsg>(client, messageData);
                    break;
                case VesselMessageType.PartSyncField:
                    if (VesselAuthorityGate.CanPersist(client, ((VesselPartSyncFieldMsgData)messageData).VesselId, "PartSyncField"))
                        VesselDataUpdater.WritePartSyncFieldDataToFile(messageData);
                    MessageQueuer.RelayMessage<VesselSrvMsg>(client, messageData);
                    break;
                case VesselMessageType.PartSyncUiField:
                    if (VesselAuthorityGate.CanPersist(client, ((VesselPartSyncUiFieldMsgData)messageData).VesselId, "PartSyncUiField"))
                        VesselDataUpdater.WritePartSyncUiFieldDataToFile(messageData);
                    MessageQueuer.RelayMessage<VesselSrvMsg>(client, messageData);
                    break;
                case VesselMessageType.PartSyncCall:
                    MessageQueuer.RelayMessage<VesselSrvMsg>(client, messageData);
                    break;
                case VesselMessageType.ActionGroup:
                    if (VesselAuthorityGate.CanPersist(client, ((VesselActionGroupMsgData)messageData).VesselId, "ActionGroup"))
                        VesselDataUpdater.WriteActionGroupDataToFile(messageData);
                    MessageQueuer.RelayMessage<VesselSrvMsg>(client, messageData);
                    break;
                case VesselMessageType.Fairing:
                    if (VesselAuthorityGate.CanPersist(client, ((VesselFairingMsgData)messageData).VesselId, "Fairing"))
                        VesselDataUpdater.WriteFairingDataToFile(messageData);
                    MessageQueuer.RelayMessage<VesselSrvMsg>(client, messageData);
                    break;
                case VesselMessageType.Decouple:
                    MessageQueuer.RelayMessage<VesselSrvMsg>(client, messageData);
                    break;
                case VesselMessageType.Couple:
                    HandleVesselCouple(client, messageData);
                    break;
                case VesselMessageType.Undock:
                    MessageQueuer.RelayMessage<VesselSrvMsg>(client, messageData);
                    break;
                default:
                    throw new NotImplementedException("Vessel message type not implemented");
            }
        }

        private static void HandleVesselRemove(ClientStructure client, VesselBaseMsgData message)
        {
            var data = (VesselRemoveMsgData)message;

            if (LockSystem.LockQuery.ControlLockExists(data.VesselId) && !LockSystem.LockQuery.ControlLockBelongsToPlayer(data.VesselId, client.PlayerName))
                return;

            if (VesselStoreSystem.VesselExists(data.VesselId))
            {
                LunaLog.Debug($"Removing vessel {data.VesselId} from {client.PlayerName}");
                VesselStoreSystem.RemoveVessel(data.VesselId);
            }

            InitialUploaders.TryRemove(data.VesselId, out _);

            if (data.AddToKillList)
                VesselContext.RemovedVessels.TryAdd(data.VesselId, 0);

            //Relay the message.
            MessageQueuer.RelayMessage<VesselSrvMsg>(client, data);
        }

        private static void HandleVesselProto(ClientStructure client, VesselBaseMsgData message)
        {
            var msgData = (VesselProtoMsgData)message;

            if (VesselContext.RemovedVessels.ContainsKey(msgData.VesselId)) return;

            if (msgData.NumBytes == 0)
            {
                LunaLog.Warning($"Received a vessel with 0 bytes ({msgData.VesselId}) from {client.PlayerName}.");
                return;
            }

            var vesselAlreadyStored = VesselStoreSystem.VesselExists(msgData.VesselId);
            var initialUploader = InitialUploaders.GetOrAdd(msgData.VesselId, _ => client.PlayerName);
            var isInitialUploader = string.Equals(initialUploader, client.PlayerName, StringComparison.Ordinal);
            var vesselClass = ClassifySpaceObject(msgData, msgData.VesselId);

            // For untouched asteroids/comets that already exist in the server store,
            // ignore live proto updates entirely (no persist, no relay). This prevents
            // repeated client-side orbit presentation drift from being fanned out.
            if (vesselClass == SpaceObjectClass.Untouched && vesselAlreadyStored)
                return;

            if (!vesselAlreadyStored)
            {
                LunaLog.Debug($"Saving vessel {msgData.VesselId} ({ByteSize.FromBytes(msgData.NumBytes).KiloBytes} KB) from {client.PlayerName}.");
            }

            // Authority gate: only persist proto updates from the Control lock holder. First upload (vessel not
            // yet stored) is accepted only from the first uploader we observe for this vessel id. This closes
            // the short async insert race where concurrent proto senders could both see "not stored yet".
            // Relay to other clients always runs so live tracking is unaffected.
            var canPersist = false;
            if (vesselClass == SpaceObjectClass.Untouched)
            {
                canPersist = LockSystem.LockQuery.AsteroidCometLockBelongsToPlayer(client.PlayerName);
                if (!canPersist)
                    LunaLog.Debug($"[AuthorityGate] Rejected Proto for untouched space object {msgData.VesselId} from {client.PlayerName} (asteroid lock owner: {LockSystem.LockQuery.AsteroidCometLockOwner() ?? "<none>"}).");
            }
            else
            {
                canPersist = (!vesselAlreadyStored && isInitialUploader) ||
                             (vesselAlreadyStored && VesselAuthorityGate.CanPersist(client, msgData.VesselId, "Proto"));
            }

            if (canPersist)
                VesselDataUpdater.RawConfigNodeInsertOrUpdate(msgData.VesselId, Encoding.UTF8.GetString(msgData.Data, 0, msgData.NumBytes));
            MessageQueuer.RelayMessage<VesselSrvMsg>(client, msgData);
        }

        private static void HandleVesselsSync(ClientStructure client, VesselBaseMsgData message)
        {
            var msgData = (VesselSyncMsgData)message;

            var allVessels = VesselStoreSystem.CurrentVessels.Keys.ToList();

            //Here we only remove the vessels that the client ALREADY HAS so we only send the vessels they DON'T have
            for (var i = 0; i < msgData.VesselsCount; i++)
                allVessels.Remove(msgData.VesselIds[i]);

            var vesselsToSend = allVessels;
            foreach (var vesselId in vesselsToSend)
            {
                var vesselData = VesselStoreSystem.GetVesselInConfigNodeFormat(vesselId);
                if (vesselData.Length > 0)
                {
                    var protoMsg = ServerContext.ServerMessageFactory.CreateNewMessageData<VesselProtoMsgData>();
                    var vesselBytes = Encoding.UTF8.GetBytes(vesselData);
                    protoMsg.Data = vesselBytes;
                    protoMsg.NumBytes = vesselBytes.Length;
                    protoMsg.VesselId = vesselId;

                    MessageQueuer.SendToClient<VesselSrvMsg>(client, protoMsg);
                }
            }

            if (allVessels.Count > 0)
                LunaLog.Debug($"Sending {client.PlayerName} {vesselsToSend.Count} vessels");
        }

        private static void HandleVesselCouple(ClientStructure client, VesselBaseMsgData message)
        {
            var msgData = (VesselCoupleMsgData)message;

            LunaLog.Debug($"Coupling message received! Dominant vessel: {msgData.VesselId}");
            MessageQueuer.RelayMessage<VesselSrvMsg>(client, msgData);

            if (VesselContext.RemovedVessels.ContainsKey(msgData.CoupledVesselId)) return;

            //Now remove the weak vessel but DO NOT add to the removed vessels as they might undock!!!
            LunaLog.Debug($"Removing weak coupled vessel {msgData.CoupledVesselId}");
            VesselStoreSystem.RemoveVessel(msgData.CoupledVesselId);

            //Tell all clients to remove the weak vessel
            var removeMsgData = ServerContext.ServerMessageFactory.CreateNewMessageData<VesselRemoveMsgData>();
            removeMsgData.VesselId = msgData.CoupledVesselId;

            MessageQueuer.SendToAllClients<VesselSrvMsg>(removeMsgData);
        }

        private static bool IsCometOrAsteroid(Guid vesselId)
        {
            if (!VesselStoreSystem.CurrentVessels.TryGetValue(vesselId, out var vessel))
                return false;

            var vesselType = vessel.Fields.GetSingle("type")?.Value;
            if (string.Equals(vesselType, "SpaceObject", StringComparison.OrdinalIgnoreCase))
                return true;

            var vesselName = vessel.Fields.GetSingle("name")?.Value;
            if (!string.IsNullOrEmpty(vesselName) && vesselName.StartsWith("Ast.", StringComparison.OrdinalIgnoreCase))
                return true;

            var parts = vessel.Parts.GetAllValues().ToArray();
            if (parts.Length != 1)
                return false;

            var partName = parts[0].Fields.GetSingle("name")?.Value;
            return string.Equals(partName, "PotatoRoid", StringComparison.Ordinal) ||
                   string.Equals(partName, "PotatoComet", StringComparison.Ordinal);
        }

        private static SpaceObjectClass ClassifySpaceObject(VesselProtoMsgData msgData, Guid vesselId)
        {
            try
            {
                var vesselText = Encoding.UTF8.GetString(msgData.Data, 0, msgData.NumBytes);
                var incomingVessel = new global::Server.System.Vessel.Classes.Vessel(vesselText);
                return ClassifySpaceObject(incomingVessel);
            }
            catch
            {
                if (VesselStoreSystem.CurrentVessels.TryGetValue(vesselId, out var storedVessel))
                    return ClassifySpaceObject(storedVessel);

                return SpaceObjectClass.Normal;
            }
        }

        private static SpaceObjectClass ClassifySpaceObject(global::Server.System.Vessel.Classes.Vessel vessel)
        {
            if (vessel == null)
                return SpaceObjectClass.Normal;

            var vesselType = vessel.Fields.GetSingle("type")?.Value;
            var vesselName = vessel.Fields.GetSingle("name")?.Value;
            var isSpaceObjectType = string.Equals(vesselType, "SpaceObject", StringComparison.OrdinalIgnoreCase);
            var isLegacyAstName = !string.IsNullOrEmpty(vesselName) && vesselName.StartsWith("Ast.", StringComparison.OrdinalIgnoreCase);

            var parts = vessel.Parts.GetAllValues().ToArray();
            var partCount = parts.Length;
            var hasPotatoCore = parts.Any(p =>
            {
                var partName = p.Fields.GetSingle("name")?.Value;
                return string.Equals(partName, "PotatoRoid", StringComparison.Ordinal) ||
                       string.Equals(partName, "PotatoComet", StringComparison.Ordinal);
            });

            var hasAsteroidSignature = isSpaceObjectType || isLegacyAstName || hasPotatoCore;
            if (!hasAsteroidSignature)
                return SpaceObjectClass.Normal;

            var hasCrew = parts.Any(p => !string.IsNullOrWhiteSpace(p.Fields.GetSingle("crew")?.Value));

            // Some update paths can temporarily flip vessel type away from SpaceObject while
            // the vessel is still an untouched potato-core body. Classify by structure first.
            if (partCount <= 1 && !hasCrew)
                return SpaceObjectClass.Untouched;

            return SpaceObjectClass.Colonized;
        }
    }
}
