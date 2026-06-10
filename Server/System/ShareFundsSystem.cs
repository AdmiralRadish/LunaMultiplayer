using LmpCommon.Message.Data.ShareProgress;
using LmpCommon.Message.Server;
using Server.Client;
using Server.Log;
using Server.Server;
using Server.System.Scenario;

namespace Server.System
{
    public static class ShareFundsSystem
    {
        public static void FundsReceived(ClientStructure client, ShareProgressFundsMsgData data)
        {
            LunaLog.Debug($"Funds received: {data.Funds} Reason: {data.Reason}");

            if (ProgressionEconomyDeduplicationSystem.IsProgressionReason(data.Reason) &&
                !ProgressionEconomyDeduplicationSystem.TryAllowAward(client.PlayerName, data.Reason, ProgressionResourceType.Funds,
                    out var progressionId, out var rejectionReason))
            {
                LunaLog.Warning($"Ignoring Funds progression update from {client.PlayerName}. Reason='{data.Reason}', ProgressionId='{progressionId ?? "n/a"}', Blocked='{rejectionReason}'.");
                return;
            }

            //send the funds update to all other clients
            MessageQueuer.RelayMessage<ShareProgressSrvMsg>(client, data);
            ScenarioDataUpdater.WriteFundsDataToFile(data.Funds);
        }
    }
}
