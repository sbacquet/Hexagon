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

        public MessageSystem(
            IMessageFactory<M> messageFactory,
            IMessagePatternFactory<P> patternFactory
            )
        {
            MessageFactory = messageFactory;
            PatternFactory = patternFactory;
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

        protected static Action<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, MessageSystem<M, P>> PowershellScriptToAction(string script, bool respondWithOutput)
            => (message, sender, self, messageSystem) =>
            {
                var outputs = new PowershellScriptExecutor().Execute(
                    script,
                    ("message", message.ToPowershell()),
                    ("sender", sender),
                    ("self", self),
                    ("messageSystem", messageSystem));
                if (outputs != null)
                {
                    if (respondWithOutput)
                    {
                        foreach (var output in outputs)
                            sender.Tell(messageSystem.MessageFactory.FromString(output.ToString()), self);
                    }
                }
            };
        public abstract void Dispose();
    }
}
