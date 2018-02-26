using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using Hexagon;
using System.Threading;
using Hexagon.AkkaImpl;

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

        private readonly ManualResetEvent _quitEvent = new ManualResetEvent(false);

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
            var config = Hexagon.NodeConfig.FromFile<AkkaNodeConfig>(NodeConfig);
            using (MessageSystem<XmlMessage, XmlMessagePattern> xmlMessageSystem = CreateSystem(config))
            {
                xmlMessageSystem.Start(config, registry);
                Console.WriteLine("Press Control-C to stop.");
                Console.CancelKeyPress += (sender, e) =>
                {
                    _quitEvent.Set();
                    e.Cancel = true;
                };
                _quitEvent.WaitOne();
            }
        }

        MessageSystem<XmlMessage, XmlMessagePattern> CreateSystem(AkkaNodeConfig config)
        {
            switch (ImplType)
            {
                case EImplType.Akka:
                    return AkkaXmlMessageSystem.Create(config);
                default:
                    throw new ArgumentException("only Akka implementation type is handled");
            }
        }
    }
}
