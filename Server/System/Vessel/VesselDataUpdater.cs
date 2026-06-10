using LunaConfigNode.CfgNode;
using Server.Log;
using Server.Settings.Structures;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace Server.System.Vessel
{
    /// <summary>
    /// We try to avoid working with protovessels as much as possible as they can be huge files.
    /// This class patches the vessel file with the information messages we receive about a position and other vessel properties.
    /// This way we send the whole vessel definition only when there are parts that have changed 
    /// </summary>
    public partial class VesselDataUpdater
    {
        #region Semaphore

        /// <summary>
        /// To not overwrite our own data we use a lock
        /// </summary>
        private static readonly ConcurrentDictionary<Guid, object> Semaphore = new ConcurrentDictionary<Guid, object>();

        #endregion

        /// <summary>
        /// Sets ORBIT IDENT from the reference body name when provided (e.g. from position or update messages).
        /// </summary>
        internal static void ApplyOrbitIdent(Classes.Vessel vessel, string bodyName)
        {
            if (string.IsNullOrEmpty(bodyName)) return;

            if (vessel.Orbit.Exists("IDENT"))
                vessel.Orbit.Update("IDENT", bodyName);
            else
                vessel.Orbit.Add(new CfgNodeValue<string, string>("IDENT", bodyName));
        }

        /// <summary>
        /// Raw updates a vessel in the dictionary and takes care of the locking in case we received another vessel message type
        /// </summary>
        public static void RawConfigNodeInsertOrUpdate(Guid vesselId, string vesselDataInConfigNodeFormat)
        {
            _ = Task.Run(() =>
            {
                var sanitizedConfig = SanitizeIncomingVesselConfig(vesselDataInConfigNodeFormat, out var removedBlankCrewLines);
                if (removedBlankCrewLines > 0)
                {
                    LunaLog.Warning($"Sanitized {removedBlankCrewLines} blank crew entries from incoming vessel definition {vesselId}.");
                }

                var vessel = new Classes.Vessel(sanitizedConfig);
                if (GeneralSettings.SettingsStore.ModControl)
                {
                    var vesselParts = vessel.Parts.GetAllValues().Select(p => p.Fields.GetSingle("name").Value);
                    var bannedParts = vesselParts.Except(ModFileSystem.ModControl.AllowedParts);
                    if (bannedParts.Any())
                    {
                        LunaLog.Warning($"Received a vessel with BANNED parts! {vesselId}");
                        return;
                    }
                }
                lock (Semaphore.GetOrAdd(vesselId, new object()))
                {
                    VesselStoreSystem.CurrentVessels.AddOrUpdate(vesselId, vessel, (key, existingVal) => vessel);
                }
            });
        }

        private static string SanitizeIncomingVesselConfig(string vesselConfig, out int removedBlankCrewLines)
        {
            removedBlankCrewLines = 0;
            if (string.IsNullOrEmpty(vesselConfig)) return vesselConfig;

            using (var reader = new StringReader(vesselConfig))
            {
                var sanitizedBuilder = new StringBuilder(vesselConfig.Length);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (IsBlankCrewLine(line))
                    {
                        removedBlankCrewLines++;
                        continue;
                    }

                    sanitizedBuilder.AppendLine(line);
                }

                return sanitizedBuilder.ToString();
            }
        }

        private static bool IsBlankCrewLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return false;

            var trimmed = line.Trim();
            if (!trimmed.StartsWith("crew", StringComparison.OrdinalIgnoreCase)) return false;

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex < 0) return false;

            var rhs = trimmed.Substring(equalsIndex + 1);
            return string.IsNullOrWhiteSpace(rhs);
        }
    }
}
