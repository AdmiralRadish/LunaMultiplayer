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

            if (ProgressionEconomyDeduplicationSystem.IsProgressionReason(data.Reason) &&
                !ProgressionEconomyDeduplicationSystem.TryAllowAward(client.PlayerName, data.Reason, ProgressionResourceType.Reputation,
                    out var progressionId, out var rejectionReason))
            {
                LunaLog.Warning($"Ignoring Reputation progression update from {client.PlayerName}. Reason='{data.Reason}', ProgressionId='{progressionId ?? "n/a"}', Blocked='{rejectionReason}'.");
                return;
            }

            //send the reputation update to all other clients
            MessageQueuer.RelayMessage<ShareProgressSrvMsg>(client, data);
            ScenarioDataUpdater.WriteReputationDataToFile(data.Reputation);
        }
    }
}
