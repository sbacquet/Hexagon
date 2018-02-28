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

        [Parameter(Mandatory = false, Position = 2)]
        public PSObject[] Resources { get; set; }

        private readonly ManualResetEvent _quitEvent = new ManualResetEvent(false);

        protected override void EndProcessing()
        {
            PatternActionsRegistry<XmlMessage, XmlMessagePattern> registry = new PatternActionsRegistry<XmlMessage, XmlMessagePattern>();
            if (MessagePatterns != null && MessagePatterns.Any())
            {
                foreach (var messagePattern in MessagePatterns)
                {
                    string processingUnitId = (string)messagePattern.Properties["Id"].Value;
                    var pattern = ((object[])messagePattern.Properties["Pattern"].Value).Select(o => (string)o).ToArray();
                    var script = (ScriptBlock)messagePattern.Properties["Script"].Value;
                    var sec = messagePattern.Properties["Secondary"];
                    bool secondary = sec != null ? (bool)sec.Value : false;
                    var xmlMessagePattern = new XmlMessagePattern(secondary, pattern);
                    registry.AddPowershellScript(xmlMessagePattern, script, processingUnitId);
                }
            }
            if (Resources != null && Resources.Any())
            {
                foreach (var resource in Resources)
                {
                    string processingUnitId = (string)resource.Properties["Id"].Value;
                    ScriptBlock resourceConstructor = (ScriptBlock)resource.Properties["Constructor"].Value;
                    ScriptBlock resourceDestructor = (ScriptBlock)resource.Properties["Destructor"]?.Value;
                    registry.SetProcessingUnitResourceFactory(
                        processingUnitId,
                        logger => new Lazy<IDisposable>(() =>
                        {
                            var outputs = new PowershellScriptExecutor(logger).Execute(resourceConstructor.ToString());
                            if (outputs == null || !outputs.Any()) return null;
                            System.Collections.Hashtable resources = outputs.First() as System.Collections.Hashtable;
                            if (resources == null) return null;
                            return new PSResources(resources, logger, resourceDestructor);
                        })
                    );

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
