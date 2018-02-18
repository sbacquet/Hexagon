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

        [Parameter(Mandatory = false, DontShow = true)]
        public EImplType ImplType { get; set; } = EImplType.Akka;

        [Parameter(Mandatory = true, Position = 0)]
        public string NodeConfig { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        public PSObject[] MessagePatterns { get; set; }

        protected override void EndProcessing()
        {
            PatternActionsRegistry<XmlMessage, XmlMessagePattern> registry = null;
            if (MessagePatterns != null && MessagePatterns.Any())
            {
                registry = new PatternActionsRegistry<XmlMessage, XmlMessagePattern>();
                foreach (var messagePattern in MessagePatterns)
                {
                    string key = (string)messagePattern.Properties["Key"].Value;
                    var pattern = ((object[])messagePattern.Properties["Pattern"].Value).Select(o => (string)o).ToArray();
                    var script = (ScriptBlock)messagePattern.Properties["Script"].Value;
                    var sec = messagePattern.Properties["Secondary"];
                    bool secondary = sec != null ? (bool)sec.Value : false;
                    var xmlMessagePattern = new XmlMessagePattern(secondary, pattern);
                    registry.AddPowershellScript(xmlMessagePattern, script, key);
                }
            }
            MessageSystem<XmlMessage, XmlMessagePattern> xmlMessageSystem = null;
            switch (ImplType)
            {
                case EImplType.Akka:
                    xmlMessageSystem = Hexagon.AkkaImpl.XmlMessageSystem.Create(Hexagon.NodeConfig.FromFile(NodeConfig));
                    break;
                default:
                    throw new ArgumentException("only Akka implementation type is handled");
            }
            xmlMessageSystem.Start(registry);
            WriteObject(xmlMessageSystem);
        }
    }
}
