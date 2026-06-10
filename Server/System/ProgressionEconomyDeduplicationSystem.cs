using LunaConfigNode.CfgNode;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.System
{
    internal enum ProgressionResourceType
    {
        Funds,
        Science,
        Reputation
    }

    internal static class ProgressionEconomyDeduplicationSystem
    {
        private const string ProgressTrackingScenario = "ProgressTracking";
        private const string ProgressNodeName = "Progress";
        private const string LedgerNodeName = "LMP_PROGRESSION_AWARDS";
        private const string AwardNodeName = "AWARD";

        private static readonly object Lock = new object();
        private static readonly Dictionary<string, DateTime> ObservedTransitionsUtc = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private static readonly TimeSpan TransitionWindow = TimeSpan.FromSeconds(30);

        public static void RecordObservedTransition(string progressionId)
        {
            if (string.IsNullOrWhiteSpace(progressionId))
                return;

            lock (Lock)
            {
                ObservedTransitionsUtc[progressionId.Trim()] = DateTime.UtcNow;
                PruneObservedTransitions(DateTime.UtcNow);
            }
        }

        public static bool TryAllowAward(string playerName, string reason, ProgressionResourceType resourceType, out string progressionId, out string rejectionReason)
        {
            progressionId = null;
            rejectionReason = null;

            if (!TryGetProgressionId(reason, out progressionId))
            {
                rejectionReason = "missing progression id";
                return false;
            }

            lock (Lock)
            {
                if (!HasRecentObservedTransition(progressionId))
                {
                    rejectionReason = "no verified transition";
                    return false;
                }

                if (!ScenarioStoreSystem.CurrentScenarios.TryGetValue(ProgressTrackingScenario, out var scenario))
                {
                    rejectionReason = "missing ProgressTracking scenario";
                    return false;
                }

                var progressRoot = scenario.GetNode(ProgressNodeName)?.Value;
                if (progressRoot == null)
                {
                    rejectionReason = "missing Progress node";
                    return false;
                }

                var ledgerNode = progressRoot.GetNode(LedgerNodeName);
                if (ledgerNode == null)
                {
                    progressRoot.AddNode(new ConfigNode(LedgerNodeName, progressRoot));
                    ledgerNode = progressRoot.GetNode(LedgerNodeName);
                }

                var ledger = ledgerNode?.Value;
                if (ledger == null)
                {
                    rejectionReason = "missing progression awards ledger";
                    return false;
                }

                var resourceKey = resourceType.ToString().ToLowerInvariant();
                var resourceNode = FindAwardNode(ledger, progressionId);
                if (resourceNode == null)
                {
                    resourceNode = new ConfigNode(AwardNodeName, ledger);
                    SetValue(resourceNode, "id", progressionId);
                    SetValue(resourceNode, resourceKey, bool.TrueString);
                    SetValue(resourceNode, "firstPlayer", playerName ?? string.Empty);
                    SetValue(resourceNode, "firstAppliedUtc", DateTime.UtcNow.ToString("o"));
                    ledger.AddNode(resourceNode);
                    return true;
                }

                if (ReadBool(resourceNode, resourceKey))
                {
                    rejectionReason = $"already applied for {resourceKey}";
                    return false;
                }

                SetValue(resourceNode, resourceKey, bool.TrueString);
                return true;
            }
        }

        public static bool IsProgressionReason(string reason)
        {
            return !string.IsNullOrWhiteSpace(reason) && reason.StartsWith("Progression", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetProgressionId(string reason, out string progressionId)
        {
            progressionId = null;
            if (string.IsNullOrWhiteSpace(reason))
                return false;

            var separatorIndex = reason.IndexOf(':');
            if (separatorIndex <= 0)
                return false;

            var reasonPrefix = reason.Substring(0, separatorIndex);
            if (!string.Equals(reasonPrefix, "Progression", StringComparison.OrdinalIgnoreCase))
                return false;

            var idCandidate = reason.Substring(separatorIndex + 1).Trim();
            if (string.IsNullOrWhiteSpace(idCandidate))
                return false;

            progressionId = idCandidate;
            return true;
        }

        private static ConfigNode FindAwardNode(ConfigNode ledgerNode, string progressionId)
        {
            return ledgerNode
                .GetNodes(AwardNodeName)
                .Select(n => n.Value)
                .FirstOrDefault(n => string.Equals(n.GetValue("id")?.Value, progressionId, StringComparison.Ordinal));
        }

        private static bool ReadBool(ConfigNode node, string key)
        {
            return bool.TryParse(node.GetValue(key)?.Value, out var parsed) && parsed;
        }

        private static void SetValue(ConfigNode node, string key, string value)
        {
            if (node.GetValue(key) == null)
            {
                node.CreateValue(new CfgNodeValue<string, string>(key, value));
            }
            else
            {
                node.UpdateValue(key, value);
            }
        }

        private static bool HasRecentObservedTransition(string progressionId)
        {
            var now = DateTime.UtcNow;
            PruneObservedTransitions(now);

            return ObservedTransitionsUtc.TryGetValue(progressionId, out var observedAt) && now - observedAt <= TransitionWindow;
        }

        private static void PruneObservedTransitions(DateTime now)
        {
            var staleKeys = ObservedTransitionsUtc
                .Where(kvp => now - kvp.Value > TransitionWindow)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in staleKeys)
                ObservedTransitionsUtc.Remove(key);
        }
    }
}