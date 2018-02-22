using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public abstract Task StartAsync(PatternActionsRegistry<M, P> registry = null);

        public void Start(PatternActionsRegistry<M, P> registry = null)
            => StartAsync(registry).Wait();

        protected static Action<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, Lazy<IDisposable>, MessageSystem<M, P>, ILogger> PowershellScriptToAction(string script, bool respondWithOutput)
            => (message, sender, self, resource, messageSystem, logger) =>
            {
                var outputs = new PowershellScriptExecutor(logger).Execute(
                    script,
                    ("message", message.ToPowershell()),
                    ("sender", sender),
                    ("self", self),
                    ("resource", resource),
                    ("messageSystem", messageSystem));
                if (outputs != null)
                {
                    if (respondWithOutput)
                    {
                        foreach (var output in outputs)
                            sender.Tell(messageSystem.MessageFactory.FromString(output.ToString()), self);
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
