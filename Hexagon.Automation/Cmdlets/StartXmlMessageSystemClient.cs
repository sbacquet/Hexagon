using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using Hexagon;

namespace Hexagon.Automation.Cmdlets
{
    [Cmdlet(VerbsLifecycle.Start, "XmlMessageSystemClient")]
    public class StartXmlMessageSystemClient : PSCmdlet
    {
        public enum EImplType { Akka }

        [Parameter(Mandatory = false, DontShow = true)]
        public EImplType ImplType { get; set; } = EImplType.Akka;

        [Parameter(Mandatory = true, Position = 0)]
        public string NodeConfig { get; set; }

        protected override void EndProcessing()
        {
            var config = Hexagon.NodeConfig.FromFile(NodeConfig);
            var cleanConfig = new Hexagon.NodeConfig(config.NodeId) { SystemName = config.SystemName };
            foreach (var seedNode in config.SeedNodes)
                cleanConfig.AddSeedNode(seedNode);
            MessageSystem <XmlMessage, XmlMessagePattern> xmlMessageSystem = null;
            switch (ImplType)
            {
                case EImplType.Akka:
                    xmlMessageSystem = Hexagon.AkkaImpl.XmlMessageSystem.Create(cleanConfig);
                    break;
                default:
                    throw new ArgumentException("only Akka implementation type is handled");
            }
            xmlMessageSystem.Start();
            WriteObject(xmlMessageSystem);
        }
    }
}
