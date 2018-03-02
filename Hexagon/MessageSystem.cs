using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Reflection;

namespace Hexagon
{
    public abstract class MessageSystem<M, P> : IDisposable
        where P : IMessagePattern<M>
        where M : IMessage
    {
        public readonly IMessageFactory<M> MessageFactory;
        public readonly IMessagePatternFactory<P> PatternFactory;
        protected readonly ILogger Logger;

        public MessageSystem(
            IMessageFactory<M> messageFactory,
            IMessagePatternFactory<P> patternFactory,
            ILogger logger
            )
        {
            MessageFactory = messageFactory;
            PatternFactory = patternFactory;
            Logger = logger;
        }

        public abstract Task SendMessageAsync(M message, ICanReceiveMessage<M> sender);
        public void SendMessage(M message, ICanReceiveMessage<M> sender)
            => SendMessageAsync(message, sender).Wait();

        public abstract Task<M> SendMessageAndAwaitResponseAsync(M message, ICanReceiveMessage<M> sender, TimeSpan? timeout = null, System.Threading.CancellationToken? cancellationToken = null);
        public M SendMessageAndAwaitResponse(M message, ICanReceiveMessage<M> sender, TimeSpan? timeout = null, System.Threading.CancellationToken? cancellationToken = null)
            => SendMessageAndAwaitResponseAsync(message, sender, timeout, cancellationToken).Result;

        protected abstract Task DoStartAsync(NodeConfig nodeConfig, PatternActionsRegistry<M, P> registry = null);

        public async Task StartAsync(NodeConfig nodeConfig, PatternActionsRegistry<M, P> registry = null)
        {
            if (nodeConfig.AssemblyLocations != null && nodeConfig.AssemblyLocations.Any())
                InstallAssemblyResolver(nodeConfig.AssemblyLocations.Distinct());
            await DoStartAsync(nodeConfig, registry);
        }

        private void InstallAssemblyResolver(IEnumerable<string> assemblyLocations)
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += (sender, args) =>
            {
                var assemblyName = new AssemblyName(args.Name);
                foreach (var assemblyLocation in assemblyLocations)
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom($@"{assemblyLocation}\{assemblyName.Name}.dll");
                        if (Logger.IsDebugEnabled)
                            Logger.Debug(@"Loaded {0}.dll from {1}", assemblyName.Name, assemblyLocation);
                        return assembly;
                    }
                    catch
                    {
                    }
                }
                throw new System.IO.FileLoadException("cannot find assembly {0}", args.Name);
            };
        }

        public void Start(NodeConfig nodeConfig, PatternActionsRegistry<M, P> registry = null)
            => StartAsync(nodeConfig, registry).Wait();

        protected static Action<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, Lazy<IDisposable>, MessageSystem<M, P>, ILogger> PowershellScriptToAction(string script, bool respondWithOutput)
            => (message, sender, self, resource, messageSystem, logger) =>
            {
                var psResources = ((PSResources)resource?.Value)?.Resources;
                var outputs = new PowershellScriptExecutor(logger).Execute(
                    script,
                    ("message", message.ToPowershell()),
                    ("sender", sender),
                    ("self", self),
                    ("resources", psResources),
                    ("messageSystem", messageSystem));
                if (outputs != null)
                {
                    if (respondWithOutput)
                    {
                        foreach (var output in outputs)
                        {
                            if (output != null)
                            {
                                switch (output)
                                {
                                    case M outputMessage:
                                        sender.Tell(outputMessage, self);
                                        break;
                                    case string str:
                                        sender.Tell(messageSystem.MessageFactory.FromString(str), self);
                                        break;
                                    case XmlDocument xml when message is XmlMessage:
                                        sender.Tell(messageSystem.MessageFactory.FromString(xml.InnerXml), self);
                                        break;
                                    case JObject json when message is JsonMessage:
                                        sender.Tell(messageSystem.MessageFactory.FromString(json.ToString()), self);
                                        break;
                                    default:
                                        logger.Error(@"Cannot send message from PS script output (type : {0})", output.GetType());
                                        break;
                                }
                            }
                            else
                                logger.Warning("Null PS script output!");
                        }
                    }
                    else if (logger.IsInfoEnabled)
                    {
                        foreach (var output in outputs)
                            logger.Info(@"Powershell output: {0}", output);
                    }
                }
            };

        public abstract void Dispose();
    }
}
