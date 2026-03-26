using LmpCommon.Message.Data.ShareProgress;
using LunaConfigNode.CfgNode;
using Server.Log;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Server.System.Scenario
{
    public partial class ScenarioDataUpdater
    {
        /// <summary>
        /// We received a contract message so update the scenario file accordingly
        /// </summary>
        public static void WriteContractDataToFile(ShareProgressContractsMsgData contractsMsg)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    lock (Semaphore.GetOrAdd("ContractSystem", new object()))
                    {
                        if (!ScenarioStoreSystem.CurrentScenarios.TryGetValue("ContractSystem", out var scenario)) return;

                        var scenariosParentNode = scenario.GetNode("CONTRACTS")?.Value;
                        if (scenariosParentNode == null) return;

                        var existingContracts = scenariosParentNode.GetNodes("CONTRACT").Select(c => c.Value).ToArray();

                        foreach (var contract in contractsMsg.Contracts.Select(v => ParseClientConfigNode(v.Data, v.NumBytes, "CONTRACT")))
                        {
                            var guidVal = contract.GetValue("guid");
                            if (guidVal == null)
                            {
                                LunaLog.Error("Contract update received with no guid — skipping");
                                continue;
                            }

                            var specificContractNode = existingContracts.FirstOrDefault(n =>
                            {
                                var existing = n.GetValue("guid");
                                return existing != null && existing.Value == guidVal.Value;
                            });

                            if (specificContractNode != null)
                            {
                                scenariosParentNode.ReplaceNode(specificContractNode, contract);
                            }
                            else
                            {
                                scenariosParentNode.AddNode(contract);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    LunaLog.Error($"Error updating contract scenario data: {e}");
                }
            });
        }
    }
}
