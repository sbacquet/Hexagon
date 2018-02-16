using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using Hexagon;

namespace Hexagon.Automation.Cmdlets
{
    [Cmdlet(VerbsLifecycle.Stop, "XmlMessageSystem")]
    public class StopXmlMessageSystem : PSCmdlet
    {
        [Parameter(Mandatory = true)]
        public MessageSystem<XmlMessage, XmlMessagePattern> System { get; set; }

        protected override void EndProcessing()
        {
            System.Dispose();
        }
    }
}
