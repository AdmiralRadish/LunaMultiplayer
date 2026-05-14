using HarmonyLib;
using LmpClient.Events;
using LmpClient.VesselUtilities;
using System.Linq;

// ReSharper disable All

namespace LmpClient.Harmony
{
    /// <summary>
    /// This harmony patch is intended to trigger an event when undocking a part
    /// </summary>
    [HarmonyPatch(typeof(Part))]
    [HarmonyPatch("Undock")]
    public class Part_Undock
    {
        [HarmonyPrefix]
        private static bool PrefixUndock(Part __instance, DockedVesselInfo newVesselInfo, ref Vessel __state)
        {
            __state = null;

            var dockingNode = __instance.FindModulesImplementing<ModuleDockingNode>().FirstOrDefault();
            if (dockingNode != null && !DockingPortUtil.EnsureRecoverableForUndock(dockingNode,
                    "Part.Undock", out var failureReason))
            {
                LunaLog.LogWarning($"[LMP]: Blocking undock from Part.Undock. {failureReason}. " +
                    $"Part: {__instance.partName}, Vessel: {__instance.vessel?.id}, PartFlightId: {__instance.flightID}");
                return false;
            }

            __state = __instance.vessel;
            PartEvent.onPartUndocking.Fire(__instance, newVesselInfo);
            return true;
        }

        [HarmonyPostfix]
        private static void PostfixUndock(Part __instance, DockedVesselInfo newVesselInfo, ref Vessel __state)
        {
            if (__state == null)
                return;

            PartEvent.onPartUndocked.Fire(__instance, newVesselInfo, __state);
        }
    }
}
