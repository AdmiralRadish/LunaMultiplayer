using LmpCommon.Message.Data.ShareProgress;
using LmpCommon.Message.Server;
using Server.Client;
using Server.Log;
using Server.Server;
using Server.System.Scenario;

namespace Server.System
{
    public static class ShareScienceSystem
    {
        public static void ScienceReceived(ClientStructure client, ShareProgressScienceMsgData data)
        {
            LunaLog.Debug($"Science received: {data.Science} Reason: {data.Reason}");

            if (string.Equals(data.Reason, "Progression", global::System.StringComparison.OrdinalIgnoreCase))
            {
                LunaLog.Warning($"Ignoring Science update from {client.PlayerName} with reason '{data.Reason}' to prevent duplicated progression rewards.");
                return;
            }

            //send the science update to all other clients
            MessageQueuer.RelayMessage<ShareProgressSrvMsg>(client, data);
            ScenarioDataUpdater.WriteScienceDataToFile(data.Science);
        }
    }
}
