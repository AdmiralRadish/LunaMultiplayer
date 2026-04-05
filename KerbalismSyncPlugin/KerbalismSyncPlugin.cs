// KerbalismSyncPlugin.cs — LMP server plugin for Kerbalism state synchronization.
//
// Registers a ModApi handler for "KerbalismLMPSync" messages and maintains
// authoritative state for kerbal health and storm data.  When a client connects,
// it receives a full snapshot.  Client updates are stored and relayed to all
// other connected clients.
//
// Build: dotnet build KerbalismSyncPlugin.csproj -c Release
// Deploy: copy bin/Release/net6.0/KerbalismSyncPlugin.dll to <ServerDir>/Plugins/

using System;
using System.IO;
using System.Text;
using LmpCommon.Message.Data;
using LmpCommon.Message.Interface;
using LmpCommon.Message.Server;
using Server.Client;
using Server.Context;
using Server.Log;
using Server.Plugin;
using Server.Server;

namespace KerbalismSyncPlugin
{
    public class KerbalismSyncServerPlugin : LmpPlugin
    {
        private const string MOD_NAME = "KerbalismLMPSync";
        private readonly KerbalismStateStore _store = new KerbalismStateStore();
        private int _updateCounter;
        private const int SAVE_INTERVAL_TICKS = 30000; // ~5 minutes at 10ms/tick

        public override void OnServerStart()
        {
            LunaLog.Debug($"[{MOD_NAME}] Server plugin starting...");
            LmpModInterface.RegisterModHandler(MOD_NAME, OnModMessageReceived);
            _store.LoadFromDisk();
            LunaLog.Debug($"[{MOD_NAME}] Server plugin started — state loaded");
        }

        public override void OnServerStop()
        {
            LunaLog.Debug($"[{MOD_NAME}] Server plugin stopping...");
            _store.SaveToDisk();
            LmpModInterface.UnregisterModHandler(MOD_NAME);
            LunaLog.Debug($"[{MOD_NAME}] Server plugin stopped — state saved");
        }

        public override void OnUpdate()
        {
            _updateCounter++;
            if (_updateCounter >= SAVE_INTERVAL_TICKS)
            {
                _updateCounter = 0;
                _store.SaveToDisk();
            }
        }

        public override void OnClientAuthenticated(ClientStructure client)
        {
            LunaLog.Debug($"[{MOD_NAME}] Client authenticated: {client.UniqueIdentifier} — sending full snapshot");
            SendFullSnapshot(client);
        }

        private void OnModMessageReceived(ClientStructure client, byte[] data, int numBytes)
        {
            if (data == null || numBytes == 0) return;

            try
            {
                byte msgType = data[0];
                switch (msgType)
                {
                    case 0: // RequestFullSync
                        LunaLog.Debug($"[{MOD_NAME}] Full sync requested by {client.UniqueIdentifier}");
                        SendFullSnapshot(client);
                        break;

                    case 2: // KerbalHealthBatch
                        _store.MergeKerbalHealth(data, numBytes);
                        RelayToOthers(client, data, numBytes);
                        break;

                    case 3: // StormStateBatch
                        _store.MergeStormState(data, numBytes);
                        RelayToOthers(client, data, numBytes);
                        break;

                    case 4: // DriveDataBatch — relay only, no server storage
                        RelayToOthers(client, data, numBytes);
                        break;

                    case 5: // VesselDataBatch — store + relay
                        _store.MergeVesselData(data, numBytes);
                        RelayToOthers(client, data, numBytes);
                        break;

                    case 6: // ComputerScriptBatch — relay only
                        RelayToOthers(client, data, numBytes);
                        break;

                    case 7: // LandmarkBatch — store + relay
                        _store.MergeLandmarks(data, numBytes);
                        RelayToOthers(client, data, numBytes);
                        break;

                    case 8: // ProtoFieldBatch — store + relay (v5)
                        _store.MergeProtoFields(data, numBytes);
                        RelayToOthers(client, data, numBytes);
                        break;

                    default:
                        LunaLog.Debug($"[{MOD_NAME}] Unknown message type {msgType} from {client.UniqueIdentifier}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LunaLog.Error($"[{MOD_NAME}] Error handling message from {client.UniqueIdentifier}: {ex}");
            }
        }

        private void SendFullSnapshot(ClientStructure client)
        {
            byte[] snapshot = _store.BuildFullSnapshot();
            if (snapshot == null || snapshot.Length == 0) return;

            var msgData = ServerContext.ServerMessageFactory.CreateNewMessageData<ModMsgData>();
            msgData.ModName = MOD_NAME;
            msgData.Relay = false;
            msgData.Reliable = true;

            if (msgData.Data.Length < snapshot.Length)
                msgData.Data = new byte[snapshot.Length];
            Array.Copy(snapshot, msgData.Data, snapshot.Length);
            msgData.NumBytes = snapshot.Length;

            MessageQueuer.SendToClient<ModSrvMsg>(client, msgData);
            LunaLog.Debug($"[{MOD_NAME}] Sent full snapshot ({snapshot.Length} bytes) to {client.UniqueIdentifier}");
        }

        private void RelayToOthers(ClientStructure sender, byte[] data, int numBytes)
        {
            var msgData = ServerContext.ServerMessageFactory.CreateNewMessageData<ModMsgData>();
            msgData.ModName = MOD_NAME;
            msgData.Relay = false; // we handle relay ourselves
            msgData.Reliable = true;

            if (msgData.Data.Length < numBytes)
                msgData.Data = new byte[numBytes];
            Array.Copy(data, msgData.Data, numBytes);
            msgData.NumBytes = numBytes;

            MessageQueuer.RelayMessage<ModSrvMsg>(sender, msgData);
        }
    }
}
