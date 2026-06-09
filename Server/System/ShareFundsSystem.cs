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

            // Guardrail: ignore progression-based economy packets to prevent duplicate reward storms
            // from inflating the authoritative universe state.
            if (string.Equals(data.Reason, "Progression", global::System.StringComparison.OrdinalIgnoreCase))
            {
                LunaLog.Warning($"Ignoring Funds update from {client.PlayerName} with reason '{data.Reason}' to prevent duplicated progression rewards.");
                return;
            }

            //send the funds update to all other clients
            MessageQueuer.RelayMessage<ShareProgressSrvMsg>(client, data);
            ScenarioDataUpdater.WriteFundsDataToFile(data.Funds);
        }
    }
}
