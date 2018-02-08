using System;
using System.Collections.Generic;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Actor;
using System.Threading.Tasks;

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
        public class ActionWithFilter
        {
            public ActionWithFilter(Action<M, ActorRefMessageReceiver<M>, ActorRefMessageReceiver<M>, MessageSystem<M, P>> action, Predicate<M> filter)
            {
                Action = action;
                Filter = filter;
            }
            public readonly Action<M, ActorRefMessageReceiver<M>, ActorRefMessageReceiver<M>, MessageSystem<M, P>> Action;
            public readonly Predicate<M> Filter;
        }

        public class AsyncActionWithFilter
        {
            public AsyncActionWithFilter(Func<M, ActorRefMessageReceiver<M>, ActorRefMessageReceiver<M>, MessageSystem<M, P>, Task> action, Predicate<M> filter)
            {
                Action = action;
                Filter = filter;
            }
            public readonly Func<M, ActorRefMessageReceiver<M>, ActorRefMessageReceiver<M>, MessageSystem<M, P>, Task> Action;
            public readonly Predicate<M> Filter;
        }

        public Actor(IEnumerable<ActionWithFilter> actions, IEnumerable<AsyncActionWithFilter> asyncActions, IMessageFactory<M> factory, NodeConfig nodeConfig, MessageSystem<M, P> messageSystem)
        {
            ReceiveAsync<RegisterToGlobalDirectory<P>>(mess =>
            {
                var mediator = DistributedPubSub.Get(Context.System).Mediator;
                mediator.Tell(new Put(Self));

                var actorDirectory = new ActorDirectory<M, P>(Context.System, nodeConfig);
                return actorDirectory.PublishPatterns(Self.Path.ToStringWithoutAddress(), mess.Patterns);
            });

            CreateReceivers(actions, asyncActions, factory, nodeConfig, messageSystem);
        }

        public Actor(string name, string assemblyPath)
        {
            var registry = PatternActionsRegistry<M, P>.FromAssembly(assemblyPath);
            var actorEntries = registry.LookupByKey()[name];
            var (actions, asyncActions) = MessageSystem<M, P>.GetActions(actorEntries);

            var messageSystem = MessageSystem<M, P>.Instance;
            CreateReceivers(actions, asyncActions, messageSystem.MessageFactory, messageSystem.NodeConfig, messageSystem);
        }

        void CreateReceivers(IEnumerable<ActionWithFilter> actions, IEnumerable<AsyncActionWithFilter> asyncActions, IMessageFactory<M> factory, NodeConfig nodeConfig, MessageSystem<M, P> messageSystem)
        {
            Receive<BytesMessage>(message => Self.Forward(factory.FromBytes(message.Bytes)));

            if (actions != null)
                foreach (var action in actions)
                {
                    Receive<M>(
                        message => action.Action.Invoke(
                            message, 
                            new ActorRefMessageReceiver<M>(Context.Sender),
                            new ActorRefMessageReceiver<M>(Context.Self),
                            messageSystem
                            ),
                        action.Filter);
                }

            if (asyncActions != null)
                foreach (var asyncAction in asyncActions)
                {
                    ReceiveAsync<M>(
                        message => asyncAction.Action.Invoke(
                            message,
                            new ActorRefMessageReceiver<M>(Context.Sender),
                            new ActorRefMessageReceiver<M>(Context.Self),
                            messageSystem
                            ),
                        asyncAction.Filter);
                }
        }
    }
}
