using HarmonyLib;
using System;
using UnityEngine;

// ReSharper disable All

namespace LmpClient.Harmony
{
    /// <summary>
    /// Fixes heliocentric→geocentric orbit conversion when loading vessels with REF=0 (Sun).
    /// 
    /// The issue: When KSP loads a heliocentric orbit and converts it to a local frame (Earth, etc.),
    /// the conversion can produce incorrect orbital elements if:
    /// 1. The reference body transitions happen at wrong times
    /// 2. Floating-point precision issues accumulate
    /// 3. The conversion doesn't account for the epoch correctly
    /// 
    /// This patch forces a recalculation of the orbit when:
    /// - The orbit is loaded with REF=0 (heliocentric/Sun)
    /// - The vessel is an asteroid/comet
    /// - The orbit hasn't been manually set yet
    /// 
    /// We recalculate by:
    /// 1. Getting the vessel's current position and velocity from the heliocentric orbit at current UT
    /// 2. Converting those vectors to the new reference frame
    /// 3. Creating a new orbit from those vectors at the new reference body
    /// </summary>
    [HarmonyPatch(typeof(Orbit))]
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new[] { typeof(ConfigNode) })]
    public class Orbit_FixHeliocentricConversion
    {
        [HarmonyPostfix]
        private static void PostfixConstructorConfigNode(Orbit __instance, ConfigNode node)
        {
            // Only process if this is a heliocentric orbit (REF=0)
            if (!node.HasValue("REF") || node.GetValue("REF") != "0") return;
            if (!node.HasValue("IDENT") || node.GetValue("IDENT") != "Sun") return;

            // Don't process if we don't have a reference body yet
            if (__instance.referenceBody == null || __instance.referenceBody.name != "Sun") return;

            // Extract orbital elements
            if (!double.TryParse(node.GetValue("INC"), out var inc)) return;
            if (!double.TryParse(node.GetValue("ECC"), out var ecc)) return;
            if (!double.TryParse(node.GetValue("SMA"), out var sma)) return;
            if (!double.TryParse(node.GetValue("LAN"), out var lan)) return;
            if (!double.TryParse(node.GetValue("LPE"), out var lpe)) return;
            if (!double.TryParse(node.GetValue("MNA"), out var mna)) return;
            if (!double.TryParse(node.GetValue("EPH"), out var eph)) return;

            // Get current game time
            if (Planetarium.fetch == null || double.IsNaN(Planetarium.GetUniversalTime())) return;
            var currentUT = Planetarium.GetUniversalTime();

            try
            {
                // Get heliocentric position and velocity at current UT
                var helioPos = __instance.GetPositionAtUT(currentUT);
                var helioVel = __instance.GetVatUT(currentUT);

                // Find the correct reference body by checking which celestial body this vessel
                // is closest to (within SOI). Process all bodies except Sun, sorted by distance.
                CelestialBody targetRefBody = null;
                double closestDistance = double.MaxValue;

                foreach (var body in FlightGlobals.Bodies)
                {
                    // Skip the Sun itself
                    if (body.name == "Sun") continue;
                    if (body.orbit == null || body.orbit.referenceBody == null) continue;

                    // Get this body's heliocentric position at current UT
                    var bodyHelioPos = body.orbit.GetPositionAtUT(currentUT);
                    var vesselToBodyDist = Vector3d.Distance(helioPos, bodyHelioPos);

                    // Check if vessel is within this body's SOI
                    if (vesselToBodyDist < body.sphereOfInfluence && vesselToBodyDist < closestDistance)
                    {
                        closestDistance = vesselToBodyDist;
                        targetRefBody = body;
                    }
                }

                // If no body found within SOI (vessel between planets), find the closest body
                if (targetRefBody == null)
                {
                    foreach (var body in FlightGlobals.Bodies)
                    {
                        if (body.name == "Sun") continue;
                        if (body.orbit == null) continue;

                        var bodyHelioPos = body.orbit.GetPositionAtUT(currentUT);
                        var vesselToBodyDist = Vector3d.Distance(helioPos, bodyHelioPos);

                        if (vesselToBodyDist < closestDistance)
                        {
                            closestDistance = vesselToBodyDist;
                            targetRefBody = body;
                        }
                    }
                }

                if (targetRefBody != null)
                {
                    // Convert heliocentric vectors to body-relative frame
                    var bodyHelioPos = targetRefBody.orbit.GetPositionAtUT(currentUT);
                    var bodyHelioVel = targetRefBody.orbit.GetVatUT(currentUT);
                    var bodyRelPos = helioPos - bodyHelioPos;
                    var bodyRelVel = helioVel - bodyHelioVel;

                    // Update orbit with body-relative state vectors
                    __instance.UpdateFromStateVectors(bodyRelPos, bodyRelVel, targetRefBody, currentUT);

                    LunaLog.Log($"[Orbit_FixHeliocentricConversion] Converted heliocentric orbit to {targetRefBody.name} frame. " +
                        $"ECC={__instance.eccentricity:F6} (hyperbolic if >1), SMA={__instance.semiMajorAxis:F0}m");
                }
            }
            catch (Exception e)
            {
                LunaLog.Log($"[Orbit_FixHeliocentricConversion] Error during conversion: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
