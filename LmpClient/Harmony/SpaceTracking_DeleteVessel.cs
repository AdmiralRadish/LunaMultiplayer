using HarmonyLib;
using KSP.UI.Screens;
using LmpClient.Base;
using LmpClient.Localization;
using LmpClient.Systems.SettingsSys;
using LmpCommon.Enums;

// ReSharper disable All

namespace LmpClient.Harmony
{
    internal static class SpaceTrackingTerminationGuard
    {
        public static bool ShouldAllowDelete()
        {
            if (MainSystem.NetworkState < ClientState.Connected) return true;

            if (!SettingsSystem.ServerSettings.AllowVesselTermination)
            {
                LunaScreenMsg.PostScreenMessage(LocalizationContainer.ScreenText.TerminationDisabledByServer, 5f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }

            return true;
        }

        public static bool ShouldAllowRecover(SpaceTracking spaceTracking)
        {
            if (MainSystem.NetworkState < ClientState.Connected) return true;

            if (!SettingsSystem.ServerSettings.AllowVesselTermination)
            {
                // Allow recovery of vessels that are landed/splashed on the home body
                if (spaceTracking.SelectedVessel != null && spaceTracking.SelectedVessel.IsRecoverable)
                    return true;

                LunaScreenMsg.PostScreenMessage(LocalizationContainer.ScreenText.TerminationDisabledByServer, 5f, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Blocks the Tracking Station "Terminate" button when AllowVesselTermination is false on the server.
    /// Physical destruction (crashes, explosions) is unaffected — only the UI button path is intercepted.
    /// </summary>
    [HarmonyPatch(typeof(SpaceTracking))]
    [HarmonyPatch("BtnOnClick_DeleteSelectedVessel")]
    public class SpaceTracking_DeleteSelectedVessel
    {
        [HarmonyPrefix]
        private static bool PrefixDeleteSelectedVessel()
        {
            return SpaceTrackingTerminationGuard.ShouldAllowDelete();
        }
    }

    /// <summary>
    /// Some KSP builds use Onclick (lowercase c) for the same button callback.
    /// Patch both names to keep behavior stable across game versions.
    /// </summary>
    [HarmonyPatch(typeof(SpaceTracking))]
    [HarmonyPatch("BtnOnclick_DeleteSelectedVessel")]
    public class SpaceTracking_DeleteSelectedVessel_Onclick
    {
        [HarmonyPrefix]
        private static bool PrefixDeleteSelectedVessel()
        {
            return SpaceTrackingTerminationGuard.ShouldAllowDelete();
        }
    }

    /// <summary>
    /// Blocks the Tracking Station "Recover" button when AllowVesselTermination is false on the server,
    /// unless the vessel is recoverable (landed or splashed on the home body). Recoverable vessels are
    /// allowed through so players can still recover missions that have safely returned home.
    /// </summary>
    [HarmonyPatch(typeof(SpaceTracking))]
    [HarmonyPatch("BtnOnclick_RecoverSelectedVessel")]
    public class SpaceTracking_RecoverSelectedVessel
    {
        [HarmonyPrefix]
        private static bool PrefixRecoverSelectedVessel(SpaceTracking __instance)
        {
            return SpaceTrackingTerminationGuard.ShouldAllowRecover(__instance);
        }
    }

    /// <summary>
    /// Some KSP builds use OnClick (uppercase C) for the recover callback.
    /// Patch both names to keep behavior stable across game versions.
    /// </summary>
    [HarmonyPatch(typeof(SpaceTracking))]
    [HarmonyPatch("BtnOnClick_RecoverSelectedVessel")]
    public class SpaceTracking_RecoverSelectedVessel_OnClick
    {
        [HarmonyPrefix]
        private static bool PrefixRecoverSelectedVessel(SpaceTracking __instance)
        {
            return SpaceTrackingTerminationGuard.ShouldAllowRecover(__instance);
        }
    }
}
