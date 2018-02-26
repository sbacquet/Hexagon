using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using Hexagon.AkkaImpl;

namespace Hexagon.Automation.Cmdlets
{
    [Cmdlet(VerbsData.Update, "MistrustFactor")]
    public class UpdateMistrustFactor : PSCmdlet
    {
        [Parameter(Mandatory = true)]
        public AkkaMessageSystem<XmlMessage, XmlMessagePattern> System { get; set; }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public string NodeId { get; set; }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public string ProcessingUnitId { get; set; }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
        public int MistrustFactor { get; set; }

        List<(string nodeId, string puId, int factor)> Factors;

        protected override void BeginProcessing()
        {
            Factors = new List<(string nodeId, string puId, int factor)>();
        }

        protected override void ProcessRecord()
        {
            Factors.Add((NodeId, ProcessingUnitId, MistrustFactor));
        }

        protected override void EndProcessing()
        {
            Hexagon.AkkaImpl.ActorDirectory<XmlMessage, XmlMessagePattern> directory = new AkkaImpl.ActorDirectory<XmlMessage, XmlMessagePattern>(System.ActorSystem);
            directory.UpdateMistrustFactors(Factors).Wait();
        }
    }
}
