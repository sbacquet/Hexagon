using System;
using System.Collections.Generic;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Actor;
using System.Threading.Tasks;

namespace Hexagon.AkkaImpl
{
    public class Actor<M, P> : ReceiveActor 
        where P : IMessagePattern<M>
        where M : IMessage
    {
        public class ActionWithFilter
        {
            public Action<M, ActorRefMessageReceiver<M>, ActorRefMessageReceiver<M>, MessageSystem<M, P>> Action;
            public Predicate<M> Filter;
        }

        public class AsyncActionWithFilter
        {
            public Func<M, ActorRefMessageReceiver<M>, ActorRefMessageReceiver<M>, MessageSystem<M, P>, Task> Action;
            public Predicate<M> Filter;
        }

        public class ScriptWithPattern
        {
            public string Script;
            public P Pattern;
        }

        public Actor(IEnumerable<ActionWithFilter> actions, IEnumerable<AsyncActionWithFilter> asyncActions, IMessageFactory<M> factory, NodeConfig nodeConfig, MessageSystem<M, P> messageSystem)
        {
            CreateReceivers(actions, asyncActions, factory, nodeConfig, messageSystem);
        }

        public Actor(string name, string assemblyPath)
        {
            Context.System.Log.Debug(@"Creating remote actor ""{0}"" from assembly ""{1}""", name, assemblyPath);

            var registry = PatternActionsRegistry<M, P>.FromAssembly(assemblyPath);
            var actorEntries = registry.LookupByKey()[name];
            var (actions, asyncActions) = MessageSystem<M, P>.GetActions(actorEntries);

            var messageSystem = MessageSystem<M, P>.Instance;
            CreateReceivers(actions, asyncActions, messageSystem.MessageFactory, messageSystem.NodeConfig, messageSystem);

            Context.System.Log.Debug(@"Remote actor ""{0}"" created properly from assembly ""{1}""", name, assemblyPath);
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
