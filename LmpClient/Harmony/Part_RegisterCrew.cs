using HarmonyLib;
using System;
using System.Collections.Generic;

// ReSharper disable All

namespace LmpClient.Harmony
{
    /// <summary>
    /// Global crew integrity guard for stock activation paths.
    /// Ensures invalid proto crew entries don't crash Part.RegisterCrew when vessels are activated
    /// via non-LMP load paths (for example direct SetActiveVessel switches).
    /// </summary>
    [HarmonyPatch(typeof(Part))]
    [HarmonyPatch("RegisterCrew")]
    public class Part_RegisterCrew
    {
        [HarmonyPrefix]
        private static void PrefixRegisterCrew(Part __instance)
        {
            if (__instance?.protoModuleCrew == null || __instance.protoModuleCrew.Count == 0)
                return;

            var removedEntries = SanitizeProtoCrewEntries(__instance);
            var addedRosterEntries = EnsureRosterEntries(__instance);

            if (removedEntries > 0 || addedRosterEntries > 0)
            {
                var vesselName = __instance.vessel?.vesselName ?? "<unknown vessel>";
                LunaLog.Log($"[LMP]: RegisterCrew guard on part '{__instance.partInfo?.name ?? __instance.name}' ({vesselName}) removed {removedEntries} invalid crew entries and added {addedRosterEntries} missing roster records.");
            }
        }

        private static int SanitizeProtoCrewEntries(Part part)
        {
            var removed = 0;

            removed += part.protoModuleCrew.RemoveAll(c => c == null);

            for (var i = part.protoModuleCrew.Count - 1; i >= 0; i--)
            {
                var crew = part.protoModuleCrew[i];
                if (string.IsNullOrWhiteSpace(crew?.name))
                {
                    part.protoModuleCrew.RemoveAt(i);
                    removed++;
                }
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = part.protoModuleCrew.Count - 1; i >= 0; i--)
            {
                var crew = part.protoModuleCrew[i];
                var normalizedName = crew?.name?.Trim() ?? string.Empty;
                if (!seen.Add(normalizedName))
                {
                    part.protoModuleCrew.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        private static int EnsureRosterEntries(Part part)
        {
            var roster = HighLogic.CurrentGame?.CrewRoster;
            if (roster == null) return 0;

            var added = 0;
            foreach (var crew in part.protoModuleCrew)
            {
                if (crew == null || string.IsNullOrWhiteSpace(crew.name)) continue;

                var normalizedName = crew.name.Trim();
                if (roster.Exists(crew.name) || roster.Exists(normalizedName)) continue;

                try
                {
                    if (crew.type != ProtoCrewMember.KerbalType.Crew)
                        crew.type = ProtoCrewMember.KerbalType.Crew;

                    if (crew.rosterStatus != ProtoCrewMember.RosterStatus.Dead)
                        crew.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;

                    roster.AddCrewMember(crew);
                    added++;
                }
                catch (Exception e)
                {
                    LunaLog.LogWarning($"[LMP]: RegisterCrew guard failed adding roster entry for '{crew.name}' on part '{part.partInfo?.name ?? part.name}': {e.Message}");
                }
            }

            return added;
        }
    }
}