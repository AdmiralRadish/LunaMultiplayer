using System;

namespace LmpClient.Systems.ShareAchievements
{
    /// <summary>
    /// Stores a short-lived mapping between the latest achievement event and progression economy events
    /// so the sender can attach a stable progression id for server-side dedupe.
    /// </summary>
    internal static class ProgressionEventContext
    {
        private static readonly object Lock = new object();
        private static readonly TimeSpan LinkWindow = TimeSpan.FromSeconds(15);

        private static string _lastAchievementId;
        private static DateTime _lastAchievementUtc;

        public static void RecordAchievement(string achievementId)
        {
            if (string.IsNullOrWhiteSpace(achievementId))
                return;

            lock (Lock)
            {
                _lastAchievementId = achievementId.Trim();
                _lastAchievementUtc = DateTime.UtcNow;
            }
        }

        public static string GetProgressionReasonOrDefault(string defaultReason)
        {
            lock (Lock)
            {
                if (string.IsNullOrWhiteSpace(_lastAchievementId))
                    return defaultReason;

                if (DateTime.UtcNow - _lastAchievementUtc > LinkWindow)
                    return defaultReason;

                return $"Progression:{_lastAchievementId}";
            }
        }
    }
}