using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using Hexagon;

namespace Hexagon.Automation.Cmdlets
{
    [Cmdlet(VerbsLifecycle.Start, "XmlMessageSystem")]
    public class StartXmlMessageSystem : PSCmdlet
    {
        public enum EImplType { Akka }

        [Parameter(Mandatory = false)]
        public EImplType ImplType { get; set; } = EImplType.Akka;

        [Parameter(Mandatory = true)]
        public string NodeConfig { get; set; }

        protected override void EndProcessing()
        {
            MessageSystem<XmlMessage, XmlMessagePattern> xmlMessageSystem = null;
            switch (ImplType)
            {
                case EImplType.Akka:
                    xmlMessageSystem = new Hexagon.AkkaImpl.XmlMessageSystem(Hexagon.AkkaImpl.NodeConfig.FromFile(NodeConfig));
                    break;
                default:
                    throw new ArgumentException("only Akka implementation type is handled");
            }
            xmlMessageSystem.Start();
            WriteObject(xmlMessageSystem);
        }
    }
}
