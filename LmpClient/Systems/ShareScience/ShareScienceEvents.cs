using LmpClient.Base;
using LmpClient.Systems.ShareAchievements;

namespace LmpClient.Systems.ShareScience
{
    public class ShareScienceEvents : SubSystem<ShareScienceSystem>
    {
        public void ScienceChanged(float science, TransactionReasons reason)
        {
            if (System.IgnoreEvents) return;

            var reasonText = reason == TransactionReasons.Progression
                ? ProgressionEventContext.GetProgressionReasonOrDefault(reason.ToString())
                : reason.ToString();

            System.MessageSender.SendScienceMessage(science, reasonText);
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
