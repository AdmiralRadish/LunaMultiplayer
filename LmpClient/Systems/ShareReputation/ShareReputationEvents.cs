using LmpClient.Base;
using LmpClient.Systems.ShareAchievements;

namespace LmpClient.Systems.ShareReputation
{
    public class ShareReputationEvents : SubSystem<ShareReputationSystem>
    {
        public void ReputationChanged(float reputation, TransactionReasons reason)
        {
            if (System.IgnoreEvents) return;

            var reasonText = reason == TransactionReasons.Progression
                ? ProgressionEventContext.GetProgressionReasonOrDefault(reason.ToString())
                : reason.ToString();

            LunaLog.Log($"Reputation changed to: {reputation} reason: {reasonText}");
            System.MessageSender.SendReputationMsg(reputation, reasonText);
        }

        public void RevertingDetected()
        {
            System.Reverting = true;
            System.StartIgnoringEvents();
        }

        public void RevertingToEditorDetected(EditorFacility data)
        {
            System.Reverting = true;
            System.StartIgnoringEvents();
        }

        public void LevelLoaded(GameScenes data)
        {
            if (System.Reverting)
            {
                System.Reverting = false;
                System.StopIgnoringEvents(true);
            }
        }
    }
}
