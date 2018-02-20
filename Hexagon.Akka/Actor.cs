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
        Logger Logger;

        public class ActionWithFilter
        {
            public Action<M, ActorRefMessageReceiver<M>, ActorRefMessageReceiver<M>, MessageSystem<M, P>, ILogger> Action;
            public Predicate<M> Filter;
        }

        public class AsyncActionWithFilter
        {
            public Func<M, ActorRefMessageReceiver<M>, ActorRefMessageReceiver<M>, MessageSystem<M, P>, ILogger, Task> Action;
            public Predicate<M> Filter;
        }

        public class ScriptWithPattern
        {
            public string Script;
            public P Pattern;
        }

        public Actor(IEnumerable<ActionWithFilter> actions, IEnumerable<AsyncActionWithFilter> asyncActions, IMessageFactory<M> factory, NodeConfig nodeConfig, AkkaMessageSystem<M, P> messageSystem)
        {
            Logger = new Logger(Akka.Event.Logging.GetLogger(Context));
            CreateReceivers(actions, asyncActions, factory, nodeConfig, messageSystem);
        }

        public Actor((EActionType Type, (string[] Conjuncts, bool IsSecondary) Pattern, string Code)[] actionCodes)
        {
            string name = Context.Self.Path.Name;
            Logger = new Logger(Akka.Event.Logging.GetLogger(Context));
            var messageSystem = AkkaMessageSystem<M, P>.Instance;
            var registry = new PatternActionsRegistry<M, P>();

            foreach (var action in actionCodes)
            {
                switch (action.Type)
                {
                    case EActionType.Code:
                        if (Logger.IsDebugEnabled)
                            Logger.Debug(@"For remote actor ""{0}"", adding actions from assembly ""{1}""", name, action.Code);
                        registry.AddActionsFromAssembly(action.Code, entry => entry.CodeType == EActionType.Code);
                        break;
                    case EActionType.PowershellScript:
                        if (Logger.IsDebugEnabled)
                            Logger.Debug(@"For remote actor ""{0}"", adding Powershell script", name);
                        registry.AddPowershellScript(
                            messageSystem.PatternFactory.FromConjuncts(action.Pattern.Conjuncts, action.Pattern.IsSecondary), 
                            action.Code, 
                            name);
                        break;
                }
            }
            var actorEntries = registry.LookupByKey()[name];
            var (actions, asyncActions) = AkkaMessageSystem<M, P>.GetActions(actorEntries);

            CreateReceivers(actions, asyncActions, messageSystem.MessageFactory, messageSystem.NodeConfig, messageSystem);
        }

        void CreateReceivers(IEnumerable<ActionWithFilter> actions, IEnumerable<AsyncActionWithFilter> asyncActions, IMessageFactory<M> factory, NodeConfig nodeConfig, AkkaMessageSystem<M, P> messageSystem)
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
                            messageSystem,
                            Logger
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
                            messageSystem,
                            Logger
                            ),
                        asyncAction.Filter);
                }
        }

        protected override void PostRestart(Exception reason)
        {
            base.PostRestart(reason);
            Logger.Warning(@"Actor ""{0}"" has been restarted because of exception ""{1}""", Context.Self.Path.Name, reason.Message);
        }
    }
}
