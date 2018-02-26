using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using Hexagon.AkkaImpl;

namespace Hexagon.Automation.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "ProcessingUnits")]
    public class GetProcessingUnits : PSCmdlet
    {
        [Parameter(Mandatory = true, ValueFromPipeline = true)]
        public AkkaMessageSystem<XmlMessage, XmlMessagePattern> System { get; set; }

        protected override void EndProcessing()
        {
            Hexagon.AkkaImpl.ActorDirectory<XmlMessage, XmlMessagePattern> directory = new AkkaImpl.ActorDirectory<XmlMessage, XmlMessagePattern>(System.ActorSystem);
            var results = directory.GetProcessingUnits(null).Result;
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
