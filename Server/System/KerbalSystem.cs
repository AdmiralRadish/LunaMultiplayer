using LmpCommon.Message.Data.Kerbal;
using LmpCommon.Message.Server;
using Server.Client;
using Server.Context;
using Server.Log;
using Server.Properties;
using Server.Server;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Server.System
{
    public class KerbalSystem
    {
        public static readonly string KerbalsPath = Path.Combine(ServerContext.UniverseDirectory, "Kerbals");

        public static void GenerateDefaultKerbals()
        {
            FileHandler.CreateFile(Path.Combine(KerbalsPath, "Jebediah Kerman.txt"), Resources.Jebediah_Kerman);
            FileHandler.CreateFile(Path.Combine(KerbalsPath, "Bill Kerman.txt"), Resources.Bill_Kerman);
            FileHandler.CreateFile(Path.Combine(KerbalsPath, "Bob Kerman.txt"), Resources.Bob_Kerman);
            FileHandler.CreateFile(Path.Combine(KerbalsPath, "Valentina Kerman.txt"), Resources.Valentina_Kerman);
        }

        public static void HandleKerbalProto(ClientStructure client, KerbalProtoMsgData data)
        {
            LunaLog.Debug($"Saving kerbal {data.Kerbal.KerbalName} from {client.PlayerName}");

            var path = Path.Combine(KerbalsPath, $"{data.Kerbal.KerbalName}.txt");
            var normalizedData = EnsureRosterStatusField(data.Kerbal.KerbalData, data.Kerbal.NumBytes);
            data.Kerbal.KerbalData = normalizedData;
            data.Kerbal.NumBytes = normalizedData.Length;
            FileHandler.WriteToFile(path, normalizedData, normalizedData.Length);

            MessageQueuer.RelayMessage<KerbalSrvMsg>(client, data);
        }

        public static void HandleKerbalsRequest(ClientStructure client)
        {
            var kerbalFiles = FileHandler.GetFilesInPath(KerbalsPath);
            var kerbalsData = kerbalFiles.Select(k =>
            {
                var kerbalData = EnsureRosterStatusField(FileHandler.ReadFile(k));
                return new KerbalInfo
                {
                    KerbalData = kerbalData,
                    NumBytes = kerbalData.Length,
                    KerbalName = Path.GetFileNameWithoutExtension(k)
                };
            });
            LunaLog.Debug($"Sending {client.PlayerName} {kerbalFiles.Length} kerbals...");

            var msgData = ServerContext.ServerMessageFactory.CreateNewMessageData<KerbalReplyMsgData>();
            msgData.Kerbals = kerbalsData.ToArray();
            msgData.KerbalsCount = msgData.Kerbals.Length;

            MessageQueuer.SendToClient<KerbalSrvMsg>(client, msgData);
        }

        public static void HandleKerbalRemove(ClientStructure client, KerbalRemoveMsgData message)
        {
            var kerbalToRemove = message.KerbalName;

            LunaLog.Debug($"Removing kerbal {kerbalToRemove} from {client.PlayerName}");
            FileHandler.FileDelete(Path.Combine(KerbalsPath, $"{kerbalToRemove}.txt"));

            MessageQueuer.RelayMessage<KerbalSrvMsg>(client, message);
        }

        private static byte[] EnsureRosterStatusField(byte[] kerbalData)
        {
            return EnsureRosterStatusField(kerbalData, kerbalData?.Length ?? 0);
        }

        private static byte[] EnsureRosterStatusField(byte[] kerbalData, int numBytes)
        {
            if (kerbalData == null || numBytes <= 0) return kerbalData;

            var text = Encoding.UTF8.GetString(kerbalData, 0, numBytes);
            if (text.IndexOf("rosterStatus", StringComparison.OrdinalIgnoreCase) >= 0)
                return kerbalData.Take(numBytes).ToArray();

            var newLine = text.Contains("\r\n") ? "\r\n" : "\n";
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();

            var stateIndex = -1;
            string stateValue = null;
            string indentation = string.Empty;

            for (var i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith("state", StringComparison.OrdinalIgnoreCase)) continue;

                var separator = trimmed.IndexOf('=');
                if (separator < 0) continue;

                stateIndex = i;
                stateValue = trimmed.Substring(separator + 1).Trim();
                indentation = lines[i].Substring(0, lines[i].Length - trimmed.Length);
                break;
            }

            if (stateIndex < 0 || string.IsNullOrWhiteSpace(stateValue))
                return kerbalData.Take(numBytes).ToArray();

            var normalizedRosterStatus = NormalizeRosterStatusValue(stateValue);
            if (string.IsNullOrWhiteSpace(normalizedRosterStatus))
            {
                LunaLog.Debug($"Skipping rosterStatus inject for kerbal data with non-canonical state '{stateValue}'");
                return kerbalData.Take(numBytes).ToArray();
            }

            lines.Insert(stateIndex + 1, $"{indentation}rosterStatus = {normalizedRosterStatus}");
            var normalizedText = string.Join(newLine, lines);
            return Encoding.UTF8.GetBytes(normalizedText);
        }

        private static string NormalizeRosterStatusValue(string stateValue)
        {
            if (string.IsNullOrWhiteSpace(stateValue)) return null;

            var trimmed = stateValue.Trim();

            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericState))
            {
                switch (numericState)
                {
                    case 0: return "Available";
                    case 1: return "Assigned";
                    case 2: return "Missing";
                    case 3: return "Dead";
                    default: return null;
                }
            }

            if (trimmed.Equals("Available", StringComparison.OrdinalIgnoreCase)) return "Available";
            if (trimmed.Equals("Assigned", StringComparison.OrdinalIgnoreCase)) return "Assigned";
            if (trimmed.Equals("Missing", StringComparison.OrdinalIgnoreCase)) return "Missing";
            if (trimmed.Equals("Dead", StringComparison.OrdinalIgnoreCase)) return "Dead";

            return null;
        }
    }
}
