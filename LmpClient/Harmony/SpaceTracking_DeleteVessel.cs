using HarmonyLib;
using KSP.UI.Screens;
using LmpClient.Base;
using LmpClient.Localization;
using LmpClient.Systems.SettingsSys;
using LmpCommon.Enums;

// ReSharper disable All

namespace LmpClient.Harmony
{
    /// <summary>
    /// Shared guard + prefix methods for Tracking Station terminate/recover callbacks.
    /// Patched at runtime by HarmonyPatcher so method-name differences across KSP builds
    /// don't throw during PatchAll startup.
    /// </summary>
    internal static class SpaceTrackingTerminationGuard
    {
        public static bool PrefixDeleteSelectedVessel()
        {
            if (MainSystem.NetworkState < ClientState.Connected) return true;

            var serverSettings = SettingsSystem.ServerSettings;
            if (serverSettings == null) return true;

            if (!serverSettings.AllowVesselTermination)
            {
                LunaScreenMsg.PostScreenMessage(LocalizationContainer.ScreenText.TerminationDisabledByServer, 5f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }

            return true;
        }

        public static bool PrefixRecoverSelectedVessel(SpaceTracking spaceTracking)
        {
            if (MainSystem.NetworkState < ClientState.Connected) return true;

            var serverSettings = SettingsSystem.ServerSettings;
            if (serverSettings == null) return true;

            if (!serverSettings.AllowVesselTermination)
            {
                // Allow recovery of vessels that are landed/splashed on the home body
                if (spaceTracking?.SelectedVessel != null && spaceTracking.SelectedVessel.IsRecoverable)
                    return true;

                LunaScreenMsg.PostScreenMessage(LocalizationContainer.ScreenText.TerminationDisabledByServer, 5f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }

            return true;
        }
    }
}
