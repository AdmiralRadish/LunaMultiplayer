using LmpCommon.Message.Data.ShareProgress;
using LmpCommon.Message.Server;
using Server.Client;
using Server.Log;
using Server.Server;
using Server.System.Scenario;

namespace Server.System
{
    public static class ShareReputationSystem
    {
        public static void ReputationReceived(ClientStructure client, ShareProgressReputationMsgData data)
        {
            LunaLog.Debug($"Reputation received: {data.Reputation} Reason: {data.Reason}");

            if (string.Equals(data.Reason, "Progression", global::System.StringComparison.OrdinalIgnoreCase))
            {
                LunaLog.Warning($"Ignoring Reputation update from {client.PlayerName} with reason '{data.Reason}' to prevent duplicated progression rewards.");
                return;
            }

            //send the reputation update to all other clients
            MessageQueuer.RelayMessage<ShareProgressSrvMsg>(client, data);
            ScenarioDataUpdater.WriteReputationDataToFile(data.Reputation);
        }
    }
}
