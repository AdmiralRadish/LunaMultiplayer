using System.Collections.Generic;
using System.Linq;

namespace LmpClient.VesselUtilities
{
    /// <summary>
    /// Utility for validating and recovering docking port FSM states.
    /// Inspired by DockRotate's DockingStateChecker and KML's KmlPartDock_Repair.
    /// </summary>
    public static class DockingPortUtil
    {
        /// <summary>
        /// FSM states that indicate the port is in a stable docked configuration.
        /// </summary>
        private static readonly string[] DockedStates =
        {
            "Docked (docker)",
            "Docked (dockee)",
            "Docked (same vessel)",
            "PreAttached"
        };

        /// <summary>
        /// Transient FSM states that may occur due to partial operations or proto reloads.
        /// These can potentially be recovered by resetting the FSM.
        /// </summary>
        private static readonly string[] RecoverableTransientStates =
        {
            "Disengage",
            "Acquire",
            "Acquire (dockee)"
        };

        /// <summary>
        /// Check if a docking port FSM is in a valid docked state.
        /// </summary>
        public static bool IsInDockedState(ModuleDockingNode node)
        {
            if (node?.fsm == null) return false;
            var state = node.fsm.currentStateName;
            return !string.IsNullOrEmpty(state) && DockedStates.Any(s => state == s);
        }

        /// <summary>
        /// Check if a docking port FSM is in a recoverable transient state
        /// (stuck mid-transition due to timing/proto reload issues).
        /// </summary>
        public static bool IsInRecoverableTransientState(ModuleDockingNode node)
        {
            if (node?.fsm == null) return false;
            var state = node.fsm.currentStateName;
            return !string.IsNullOrEmpty(state) && RecoverableTransientStates.Any(s => state == s);
        }

        /// <summary>
        /// Find the partner ModuleDockingNode by walking the part tree's attach nodes.
        /// Returns the partner node if this port's reference attach node connects to
        /// another part that has a ModuleDockingNode facing back at us, or null.
        /// </summary>
        public static ModuleDockingNode FindPartnerFromPartTree(ModuleDockingNode node)
        {
            if (node?.part == null) return null;

            var referenceNodeName = node.referenceAttachNode;
            if (string.IsNullOrEmpty(referenceNodeName)) return null;

            var attachNode = node.part.FindAttachNode(referenceNodeName);
            if (attachNode?.attachedPart == null) return null;

            var otherDockingNodes = attachNode.attachedPart.FindModulesImplementing<ModuleDockingNode>();
            if (otherDockingNodes == null || otherDockingNodes.Count == 0) return null;

            // Find the specific docking node on the other part that connects back to us
            foreach (var other in otherDockingNodes)
            {
                if (string.IsNullOrEmpty(other.referenceAttachNode)) continue;
                var otherAttach = other.part.FindAttachNode(other.referenceAttachNode);
                if (otherAttach?.attachedPart == node.part)
                    return other;
            }

            return null;
        }

        /// <summary>
        /// Find the partner ModuleDockingNode by searching all loaded vessels for a part
        /// matching the given dockedPartUId.
        /// </summary>
        public static ModuleDockingNode FindPartnerByUId(uint dockedPartUId)
        {
            if (dockedPartUId == 0) return null;

            foreach (var vessel in FlightGlobals.VesselsLoaded)
            {
                if (vessel?.parts == null) continue;
                foreach (var part in vessel.parts)
                {
                    if (part.flightID == dockedPartUId)
                    {
                        return part.FindModulesImplementing<ModuleDockingNode>()?.FirstOrDefault();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Determine docker vs dockee roles for a pair. The child in the part tree
        /// is typically the dockee.
        /// </summary>
        private static void InferDockerDockeeRoles(ModuleDockingNode node, ModuleDockingNode partner,
            out string nodeState, out string partnerState)
        {
            // Check serialized state first — it's often still correct even when FSM is wrong
            if (!string.IsNullOrEmpty(node.state) && node.state.StartsWith("Docked"))
            {
                nodeState = node.state;
                partnerState = (nodeState == "Docked (docker)") ? "Docked (dockee)" : "Docked (docker)";
                return;
            }
            if (!string.IsNullOrEmpty(partner.state) && partner.state.StartsWith("Docked"))
            {
                partnerState = partner.state;
                nodeState = (partnerState == "Docked (docker)") ? "Docked (dockee)" : "Docked (docker)";
                return;
            }

            // Fall back to part tree hierarchy: parent = docker, child = dockee
            if (node.part?.parent == partner.part)
            {
                nodeState = "Docked (dockee)";
                partnerState = "Docked (docker)";
            }
            else
            {
                nodeState = "Docked (docker)";
                partnerState = "Docked (dockee)";
            }
        }

        /// <summary>
        /// Set up the cross-references between two docking nodes and force their FSMs
        /// to the correct docked states. This must be done BEFORE calling StartFSM
        /// because the "Docked (docker)" OnEnter callback accesses otherNode.
        /// </summary>
        private static void RecoverDockedPair(ModuleDockingNode node, ModuleDockingNode partner,
            string nodeState, string partnerState, string reason, Vessel vessel)
        {
            LunaLog.Log($"[LMP]: Recovering docked pair on {vessel.vesselName}: " +
                $"{node.part?.partName}({node.part?.flightID}) → '{nodeState}', " +
                $"{partner.part?.partName}({partner.part?.flightID}) → '{partnerState}' " +
                $"[{reason}]");

            // Set up cross-references BEFORE forcing FSM (OnEnter accesses otherNode)
            node.otherNode = partner;
            partner.otherNode = node;

            // Ensure dockedPartUId is set on both sides
            if (node.dockedPartUId == 0 && partner.part != null)
                node.dockedPartUId = partner.part.flightID;
            if (partner.dockedPartUId == 0 && node.part != null)
                partner.dockedPartUId = node.part.flightID;

            // Force the FSM states — the docker side first since dockee's OnEnter
            // may reference the docker
            var dockerNode = (nodeState == "Docked (docker)") ? node : partner;
            var dockeeNode = (nodeState == "Docked (docker)") ? partner : node;
            var dockerState = (nodeState == "Docked (docker)") ? nodeState : partnerState;
            var dockeeState = (nodeState == "Docked (docker)") ? partnerState : nodeState;

            if (!IsInDockedState(dockerNode))
            {
                dockerNode.fsm.StartFSM(dockerState);
                LunaLog.Log($"[LMP]: Docker {dockerNode.part?.partName}({dockerNode.part?.flightID}) " +
                    $"FSM → '{dockerNode.fsm.currentStateName}'");
            }

            if (!IsInDockedState(dockeeNode))
            {
                dockeeNode.fsm.StartFSM(dockeeState);
                LunaLog.Log($"[LMP]: Dockee {dockeeNode.part?.partName}({dockeeNode.part?.flightID}) " +
                    $"FSM → '{dockeeNode.fsm.currentStateName}'");
            }
        }

        /// <summary>
        /// Determine the most likely docked state for a port that needs recovery before undocking.
        /// Used by VesselUndock for the remote-undock path only.
        /// </summary>
        public static string InferDockedStateForUndock(ModuleDockingNode node)
        {
            if (!string.IsNullOrEmpty(node.state) && node.state.StartsWith("Docked"))
                return node.state;

            if (node.otherNode?.fsm != null)
            {
                var otherState = node.otherNode.fsm.currentStateName;
                if (otherState == "Docked (dockee)" || otherState == "Docked (same vessel)")
                    return "Docked (docker)";
                if (otherState == "Docked (docker)")
                    return "Docked (dockee)";
            }

            return "Docked (docker)";
        }

        /// <summary>
        /// Attempt to recover a docking port for the remote-undock path (VesselUndock.cs).
        /// Sets up otherNode before forcing FSM to avoid NullRef.
        /// </summary>
        public static bool TryRecoverToDockedState(ModuleDockingNode node, string targetState)
        {
            if (node?.fsm == null) return false;

            var currentState = node.fsm.currentStateName;
            LunaLog.Log($"[LMP]: Attempting docking port FSM recovery: '{currentState}' → '{targetState}' " +
                $"on part {node.part?.partName} (flightID: {node.part?.flightID})");

            // Find and set otherNode before StartFSM — the OnEnter callback accesses it
            if (node.otherNode == null)
            {
                var partner = FindPartnerFromPartTree(node);
                if (partner == null && node.dockedPartUId != 0)
                    partner = FindPartnerByUId(node.dockedPartUId);
                if (partner != null)
                {
                    node.otherNode = partner;
                    partner.otherNode = node;
                }
            }

            node.fsm.StartFSM(targetState);

            var newState = node.fsm.currentStateName;
            if (newState == targetState)
            {
                LunaLog.Log($"[LMP]: Docking port FSM recovered to '{targetState}'");
                return true;
            }

            LunaLog.LogWarning($"[LMP]: Docking port FSM recovery failed — state is '{newState}' " +
                $"after StartFSM('{targetState}')");
            return false;
        }

        /// <summary>
        /// Check all docking ports on a vessel and fix any whose FSM state doesn't match
        /// their actual docked configuration. Called when a vessel goes off rails (unpacks).
        ///
        /// Detection cascade:
        ///   1. Serialized state = Docked but FSM wrong
        ///   2. dockedPartUId set but FSM not docked
        ///   3. Part tree shows physical docking partner but FSM not docked
        ///   4. Stuck in transient state with no partner → reset to Ready
        ///
        /// For cases 1-3, we recover BOTH sides of the pair together, setting up
        /// cross-references (otherNode, dockedPartUId) before forcing FSM states.
        /// </summary>
        public static void FixDockingPortFsmStates(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded) return;

            List<ModuleDockingNode> dockingNodes;
            try
            {
                dockingNodes = vessel.FindPartModulesImplementing<ModuleDockingNode>();
            }
            catch
            {
                return;
            }

            if (dockingNodes == null || dockingNodes.Count == 0) return;

            foreach (var node in dockingNodes)
            {
                if (node?.fsm == null) continue;

                // Already in a valid docked state — nothing to fix
                if (IsInDockedState(node)) continue;

                var fsmState = node.fsm.currentStateName;
                var serializedState = node.state;

                // Try to find the partner through multiple methods
                ModuleDockingNode partner = null;

                // Case 1 & 2: We have data pointing to a partner
                if (!string.IsNullOrEmpty(serializedState) && DockedStates.Any(s => serializedState == s))
                {
                    // Serialized state says docked — find partner
                    partner = FindPartnerFromPartTree(node);
                    if (partner == null && node.dockedPartUId != 0)
                        partner = FindPartnerByUId(node.dockedPartUId);
                }
                else if (node.dockedPartUId != 0)
                {
                    // Has partner UID — find partner
                    partner = FindPartnerFromPartTree(node);
                    if (partner == null)
                        partner = FindPartnerByUId(node.dockedPartUId);
                }

                // Case 3: All metadata lost — walk the part tree
                if (partner == null)
                    partner = FindPartnerFromPartTree(node);

                // If we found a partner, recover both sides together
                if (partner != null)
                {
                    string nodeState, partnerState;
                    InferDockerDockeeRoles(node, partner, out nodeState, out partnerState);
                    RecoverDockedPair(node, partner, nodeState, partnerState,
                        $"serialized='{serializedState}' fsm='{fsmState}' dockedUId={node.dockedPartUId}",
                        vessel);
                    continue;
                }

                // Case 4: Stuck in transient state with no partner anywhere — reset to Ready
                if (IsInRecoverableTransientState(node))
                {
                    LunaLog.Log($"[LMP]: Docking port stuck in transient state '{fsmState}' " +
                        $"with no partner on {vessel.vesselName} part {node.part?.partName} " +
                        $"(flightID {node.part?.flightID}) — resetting to Ready");
                    node.fsm.StartFSM("Ready");
                }
            }
        }
    }
}
