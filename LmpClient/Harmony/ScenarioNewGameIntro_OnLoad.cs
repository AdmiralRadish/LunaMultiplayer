using HarmonyLib;

// ReSharper disable All

namespace LmpClient.Harmony
{
    /// <summary>
    /// When LMP creates a new game to join a server, ScenarioNewGameIntro initializes with
    /// all tutorial flags = False. We block receiving the server's copy (IgnoredScenarios)
    /// to prevent sync issues, so the local game thinks tutorials were never seen.
    /// This causes the "Welcome to the Space Center" message to appear every rejoin,
    /// tooltips inside buildings to keep triggering, and ClickThroughBlocker to show its
    /// first-run popup.
    ///
    /// Fix: after ScenarioNewGameIntro loads, force all completion flags to True so KSP
    /// and mods that check them (CTB) treat the game as already-introduced.
    /// </summary>
    [HarmonyPatch(typeof(ScenarioNewGameIntro))]
    [HarmonyPatch("OnLoad")]
    public class ScenarioNewGameIntro_OnLoad
    {
        [HarmonyPostfix]
        private static void PostfixOnLoad(ScenarioNewGameIntro __instance)
        {
            __instance.kscComplete = true;
            __instance.editorComplete = true;
            __instance.tsComplete = true;
        }
    }
}
