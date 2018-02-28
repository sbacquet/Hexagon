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
        readonly Logger Logger;
        readonly Lazy<IDisposable> Resource;

        public class ActionWithFilter
        {
            public Action<M, ActorRefMessageReceiver<M>, ActorRefMessageReceiver<M>, Lazy<IDisposable>, MessageSystem<M, P>, ILogger> Action;
            public Predicate<M> Filter;
        }

        public class AsyncActionWithFilter
        {
            public Func<M, ActorRefMessageReceiver<M>, ActorRefMessageReceiver<M>, Lazy<IDisposable>, MessageSystem<M, P>, ILogger, Task> Action;
            public Predicate<M> Filter;
        }

        public class ScriptWithPattern
        {
            public string Script;
            public P Pattern;
        }

        public Actor(IEnumerable<ActionWithFilter> actions, IEnumerable<AsyncActionWithFilter> asyncActions, IMessageFactory<M> factory, AkkaMessageSystem<M, P> messageSystem, Lazy<IDisposable> resource)
        {
            Logger = new Logger(Akka.Event.Logging.GetLogger(Context));
            Resource = resource;
            CreateReceivers(actions, asyncActions, factory, messageSystem);
        }

        public Actor(string processingUnitId, (EActionType Type, (string[] Conjuncts, bool IsSecondary) Pattern, string Code)[] actionCodes)
        {
            Logger = new Logger(Akka.Event.Logging.GetLogger(Context));
            var messageSystem = AkkaMessageSystem<M, P>.Instance;
            var registry = new PatternActionsRegistry<M, P>();

            foreach (var action in actionCodes)
            {
                switch (action.Type)
                {
                    case EActionType.Code:
                        if (Logger.IsDebugEnabled)
                            Logger.Debug(@"For remote processing unit ""{0}"", adding actions from assembly ""{1}""", processingUnitId, action.Code);
                        registry.AddFromAssembly(action.Code, entry => entry.CodeType == EActionType.Code);
                        break;
                    case EActionType.PowershellScript:
                        if (Logger.IsDebugEnabled)
                            Logger.Debug(@"For remote processing unit ""{0}"", adding Powershell script", processingUnitId);
                        registry.AddPowershellScript(
                            messageSystem.PatternFactory.FromConjuncts(action.Pattern.Conjuncts, action.Pattern.IsSecondary), 
                            action.Code,
                            processingUnitId);
                        break;
                }
            }
            Resource = registry.GetProcessingUnitResource(processingUnitId);
            var actorEntries = registry.LookupByProcessingUnit()[processingUnitId];
            var (actions, asyncActions) = AkkaMessageSystem<M, P>.GetActions(actorEntries);

            CreateReceivers(actions, asyncActions, messageSystem.MessageFactory, messageSystem);
        }

        void CreateReceivers(IEnumerable<ActionWithFilter> actions, IEnumerable<AsyncActionWithFilter> asyncActions, IMessageFactory<M> factory, AkkaMessageSystem<M, P> messageSystem)
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
                            Resource,
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
                            Resource,
                            messageSystem,
                            Logger
                            ),
                        asyncAction.Filter);
                }
        }

        protected override void PreRestart(Exception reason, object message)
        {
            Logger.Warning(@"Processing unit ""{0}"" will be restarted because of exception ""{1}""", Context.Self.Path.Name, reason.Message);
            base.PreRestart(reason, message);
        }

        protected override void PostStop()
        {
            if (Resource != null && Resource.IsValueCreated)
            {
                Logger.Info(@"Disposing resources of processing unit ""{0}""", Context.Self.Path.Name);
                Resource.Value.Dispose();
            }
            base.PostStop();
        }

        protected override void PostRestart(Exception reason)
        {
            base.PostRestart(reason);
            Logger.Info(@"Processing unit ""{0}"" has been restarted.", Context.Self.Path.Name);
        }
    }
}
