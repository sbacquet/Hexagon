using System;
using System.Collections.Generic;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Actor;

namespace Hexagon.AkkaImpl
{
    internal class RegisterToGlobalDirectory<P>
    {
        public RegisterToGlobalDirectory(IEnumerable<P> patterns)
        {
            Patterns = patterns;
        }
        public IEnumerable<P> Patterns { get; }
    }
    public class Actor<M, P> : ReceiveActor 
        where P : IMessagePattern<M>
        where M : IMessage
    {
        public Actor(IEnumerable<Tuple<Action<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>>, Predicate<M>>> actions, IMessageFactory<M> factory)
        {
            Receive<RegisterToGlobalDirectory<P>>(mess =>
            {
                var mediator = DistributedPubSub.Get(Context.System).Mediator;
                mediator.Tell(new Put(Self));

                var actorDirectory = new ActorDirectory<M, P>(Context.System);
                actorDirectory.PublishPatterns(Self.Path.Name, mess.Patterns);
            });

            Receive<BytesMessage>(message => Self.Forward(factory.FromBytes(message.Bytes)));

            foreach (var action in actions)
            {
                Receive<M>(
                    message => action.Item1.Invoke(
                        message, 
                        new ActorRefMessageReceiver<M>(Context.Sender),
                        new ActorRefMessageReceiver<M>(Context.Self)
                        ),
                    action.Item2);
            }
        }
    }
}
