using HarmonyLib;
using LmpClient.Systems.Lock;
using LmpClient.Systems.SettingsSys;
using LmpCommon.Enums;

// ReSharper disable All

namespace LmpClient.Harmony
{
    /// <summary>
    /// This harmony patch is intended to skip the spawn of an asteroid or a comet if we don't have the lock or the server doesn't allow them
    /// </summary>
    [HarmonyPatch(typeof(ScenarioDiscoverableObjects))]
    [HarmonyPatch("UpdateSpaceObjects")]
    public class ScenarioDiscoverableObjects_UpdateSpaceObjects
    {
        [HarmonyPrefix]
        private static bool PrefixUpdateSpaceObjects()
        {
            if (MainSystem.NetworkState < ClientState.Connected) return true;

            // In multiplayer, keep discoverable-object orbital state authoritative from synced proto data.
            // Local UpdateSpaceObjects simulation on reconnect can re-solve trajectories differently
            // and produce apparent apo/peri drift on asteroid/comet lineage vessels.
            return false;
        }
    }
}
