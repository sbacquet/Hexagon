using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using Hexagon.AkkaImpl;

namespace Hexagon.Automation.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "ProcessingUnit")]
    public class GetProcessingUnit : PSCmdlet
    {
        [Parameter(Mandatory = true)]
        public AkkaMessageSystem<XmlMessage, XmlMessagePattern> System { get; set; }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public string NodeId { get; set; }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public string ProcessingUnitId { get; set; }

        List<(string nodeId, string puId)> ProcessingUnits;

        protected override void BeginProcessing()
        {
            ProcessingUnits = new List<(string nodeId, string puId)>();
        }

        protected override void ProcessRecord()
        {
            ProcessingUnits.Add((NodeId, ProcessingUnitId));
        }

        protected override void EndProcessing()
        {
            Hexagon.AkkaImpl.ActorDirectory<XmlMessage, XmlMessagePattern> directory = new AkkaImpl.ActorDirectory<XmlMessage, XmlMessagePattern>(System.ActorSystem);
            var results = directory.GetProcessingUnits(ProcessingUnits).Result;
            if (results.Any())
            {
                WriteObject(
                    results.Select(result => new
                    {
                        NodeId = result.nodeId,
                        ProcessingUnitId = result.processingUnitId,
                        MistrustFactor = result.mistrustFactor,
                        ClusterNode = result.nodeAddress,
                        ActorPath = result.actorPath,
                        Patterns = result.patterns.Select(pattern => new { Conjuncts = pattern.conjuncts, IsSecondary = pattern.isSecondary }).ToArray()
                    }),
                    true
                );
            }
        }
    }
}
