using HarmonyLib;
using LmpClient.Events;
using LmpClient.VesselUtilities;

// ReSharper disable All

namespace LmpClient.Harmony
{
    /// <summary>
    /// This harmony patch is intended to trigger an event when FINISHED unpacking a vessel.
    /// Also repairs docking port FSM states that fail to restore correctly after vessel loading.
    /// </summary>
    [HarmonyPatch(typeof(Vessel))]
    [HarmonyPatch("GoOffRails")]
    public class Vessel_GoOffRails
    {
        [HarmonyPostfix]
        private static void PostfixGoOffRails(Vessel __instance)
        {
            RailEvent.onVesselGoneOffRails.Fire(__instance);
            DockingPortUtil.FixDockingPortFsmStates(__instance);
        }
    }
}
