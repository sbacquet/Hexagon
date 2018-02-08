using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.DistributedData;
using Akka.Event;
using Akka.Routing;
using Akka.Cluster.Routing;
using Akka.Configuration;
//[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Hexagon.Akka.UnitTests")]

namespace Hexagon.AkkaImpl
{
    public class MessageSystem<M, P>
        where P : IMessagePattern<M>
        where M : IMessage
    {
        readonly ActorSystem ActorSystem;
        public IActorRef Mediator => DistributedPubSub.Get(ActorSystem).Mediator;
        public IActorRef Replicator => DistributedData.Get(ActorSystem).Replicator;
        public readonly IMessageFactory<M> MessageFactory;
        public readonly IMessagePatternFactory<P> PatternFactory;
        readonly ILoggingAdapter Logger;
        public readonly NodeConfig NodeConfig;
        readonly ActorDirectory<M,P> ActorDirectory;

        static MessageSystem<M, P> _instance = null;
        public static MessageSystem<M, P> Instance
        {
            get => _instance;
            private set
            {
                if (_instance != null)
                    throw new Exception($"MessageSystem<{typeof(M).Name},{typeof(P).Name}> singleton already set !");
                _instance = value;
            }
        }

        public MessageSystem(
            string systemName, 
            Config config, 
            IMessageFactory<M> messageFactory, 
            IMessagePatternFactory<P> patternFactory,
            NodeConfig nodeConfig)
            : this(
                  null == config ? 
                  ActorSystem.Create(systemName) : 
                  ActorSystem.Create(
                      systemName, 
                      ConfigurationFactory.ParseString($@"akka.cluster.roles=""{nodeConfig.Role}""").WithFallback(config)), 
                  messageFactory, 
                  patternFactory,
                  nodeConfig)
        {
        }

        public MessageSystem(
            ActorSystem system, 
            IMessageFactory<M> factory,
            IMessagePatternFactory<P> patternFactory,
            NodeConfig nodeConfig)
        {
            ActorSystem = system;
            MessageFactory = factory;
            PatternFactory = patternFactory;
            Logger = Logging.GetLogger(system, this);
            NodeConfig = nodeConfig;
            ActorDirectory = new ActorDirectory<M, P>(system, nodeConfig);

            Instance = this;
        }

        public async void SendMessage(M message, ICanReceiveMessage<M> sender)
        {
            if (sender != null && !(sender is ActorRefMessageReceiver<M>))
                throw new ArgumentException("the sender must be a ActorRefMessageReceiver", "sender");

            var actorPaths = await ActorDirectory.GetMatchingActors(message, PatternFactory);
            if (!actorPaths.Any())
            {
                Logger.Error(@"Cannot find any receiver of message {0}", message);
                return;
            }
            var primaryReceivers = actorPaths.Where(ma => !ma.IsSecondary);
            if (primaryReceivers.Any())
            {
                if (Logger.IsDebugEnabled)
                    Logger.Debug(@"Primary receiver(s) of message {0} : {1}", message, string.Join(", ", primaryReceivers.Select(ma => ma.Path)));
                string mainReceiverPath = GetMainReceiverPath(primaryReceivers);
                if (Logger.IsDebugEnabled)
                    Logger.Debug(@"Selected primary receiver of message {0} : {1}", message, mainReceiverPath);
                var mainReceiver = new ActorPathMessageReceiver<M>(mainReceiverPath, Mediator);
                mainReceiver.Tell(message, sender);
            }
            else
            {
                Logger.Warning(@"No primary receiver found for message {0}", message);
            }
            var secondaryReceiverPaths = actorPaths.Where(ma => ma.IsSecondary).Select(ma => ma.Path);
            if (secondaryReceiverPaths.Any())
            {
                if (Logger.IsDebugEnabled)
                    Logger.Debug(@"Secondary receiver(s) of message {0} : {1}", message, string.Join(", ", secondaryReceiverPaths));
                var readonlySender = sender == null ? null : new ReadOnlyActorRefMessageReceiver<M>(sender as ActorRefMessageReceiver<M>);
                foreach (var secondaryActorPath in secondaryReceiverPaths)
                {
                    var secondaryReceiver = new ActorPathMessageReceiver<M>(secondaryActorPath, Mediator);
                    secondaryReceiver.Tell(message, readonlySender);
                }
            }
        }

        string GetMainReceiverPath(IEnumerable<ActorDirectory<M,P>.MatchingActor> primaryActors)
        {
            System.Diagnostics.Debug.Assert(primaryActors.All(actor => actor.IsSecondary == false));

            // Look for the primary actors with highest matching score
            var primaryActorsOrderdedByMatchingScore = primaryActors.OrderByDescending(ma => ma.MatchingScore);
            int highestMatchingScore = primaryActorsOrderdedByMatchingScore.First().MatchingScore;
            var mainPrimaryActors = primaryActorsOrderdedByMatchingScore.TakeWhile(ma => ma.MatchingScore == highestMatchingScore);
            if (mainPrimaryActors.Count() == 1)
            {
                // If only one, return it
                var selectedActor = mainPrimaryActors.First();
                if (Logger.IsDebugEnabled && primaryActors.Count() > 1)
                    Logger.Debug($"Primary actor {selectedActor.Path} selected by highest matching score {highestMatchingScore}");
                return selectedActor.Path;
            }
            else
            {
                Logger.Warning($"Several actors with same highest matching score {highestMatchingScore}, randomly picking one based on actor mistrust factors...");
                // If several primary actors have the same highest score, randomly pick one depending on the mistrust factor
                var mistrusts = mainPrimaryActors.Select(ma => ma.MistrustFactor).ToArray();
                var selectedActor = mainPrimaryActors.ElementAt(MistrustBasedRandomRoutingLogic.SelectIndex(mistrusts));
                if (Logger.IsDebugEnabled)
                    Logger.Debug($"Primary actor {selectedActor.Path} randomly selected (mistrust factor : {selectedActor.MistrustFactor})");
                return selectedActor.Path;
            }
        }

        public async Task<M> SendMessageAndAwaitResponse(M message, ICanReceiveMessage<M> sender, TimeSpan? timeout = null, System.Threading.CancellationToken? cancellationToken = null)
        {
            if (sender != null && !(sender is ActorRefMessageReceiver<M>))
                throw new ArgumentException("the sender must be a ActorRefMessageReceiver", "sender");

            var actorPaths = await ActorDirectory.GetMatchingActors(message, PatternFactory);
            if (!actorPaths.Any())
            {
                Logger.Error(@"Cannot find any receiver of message {0}", message);
                return default(M);
            }
            var secondaryReceiverPaths = actorPaths.Where(ma => ma.IsSecondary).Select(ma => ma.Path);
            if (secondaryReceiverPaths.Any())
            {
                Logger.Debug(@"Secondary receivers of message {0} : {1}", message, string.Join(", ", secondaryReceiverPaths));
                var readonlySender = sender == null ? null : new ReadOnlyActorRefMessageReceiver<M>(sender as ActorRefMessageReceiver<M>);
                foreach (var secondaryActorPath in secondaryReceiverPaths)
                {
                    var secondaryReceiver = new ActorPathMessageReceiver<M>(secondaryActorPath, Mediator);
                    secondaryReceiver.Tell(message, readonlySender);
                }
            }
            var primaryReceivers = actorPaths.Where(ma => !ma.IsSecondary);
            if (primaryReceivers.Any())
            {
                if (Logger.IsDebugEnabled)
                    Logger.Debug(@"Primary receiver(s) of message {0} : {1}", message, string.Join(", ", primaryReceivers.Select(ma => ma.Path)));
                string mainReceiverPath = GetMainReceiverPath(primaryReceivers);
                var mainReceiver = new ActorPathMessageReceiver<M>(mainReceiverPath, Mediator);
                return await mainReceiver.Ask(message, MessageFactory, timeout, cancellationToken);
            }
            return default(M);
        }

        public void Start(PatternActionsRegistry<M,P> registry)
        {
            // Initialize mediator
            DistributedPubSub.Get(ActorSystem);
            // Initialize replicator
            DistributedData.Get(ActorSystem);
            // Create actors from registry
            CreateActors(registry);
            // Synchronize other actors
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(NodeConfig.GossipTimeFrameInSeconds));
        }

        static Func<PatternActionsRegistry<M, P>.MessageRegistryEntry, Predicate<M>> FilterEntry => entry => message => entry.Pattern.Match(message);
        static Func<PatternActionsRegistry<M, P>.MessageRegistryEntry, Predicate<M>> NoFilterEntry => entry => null;

        public static (IEnumerable<Actor<M,P>.ActionWithFilter>, IEnumerable<Actor<M, P>.AsyncActionWithFilter>) GetActions(IEnumerable<PatternActionsRegistry<M, P>.MessageRegistryEntry> registryEntries)
        {
            Func<PatternActionsRegistry<M, P>.MessageRegistryEntry, Predicate<M>> filter = (registryEntries.Count() == 1 ? NoFilterEntry : FilterEntry);
            var actions =
                registryEntries
                .Where(entry => entry.Action != null)
                .Select(entry =>
                    new Actor<M, P>.ActionWithFilter(
                        entry.Action,
                        filter(entry)));
            var asyncActions =
                registryEntries
                .Where(entry => entry.AsyncAction != null)
                .Select(entry =>
                    new Actor<M, P>.AsyncActionWithFilter(
                        entry.AsyncAction,
                        filter(entry)));
            return (actions, asyncActions);
        }

        private void CreateActors(PatternActionsRegistry<M, P> registry)
        {
            var groups = registry.LookupByKey();
            foreach (var group in groups)
            {
                string actorName = group.Key;
                Func<PatternActionsRegistry<M, P>.MessageRegistryEntry, Predicate<M>> filter = (group.Count() == 1 ? NoFilterEntry : FilterEntry);
                var (actions, asyncActions) = GetActions(group.AsEnumerable());

                string routeOnRole = NodeConfig.GetActorProps(actorName)?.RouteOnRole;
                Props actorProps;
                if (routeOnRole == null)
                    actorProps = Props.Create(() => new Actor<M,P>(actions, asyncActions, MessageFactory, NodeConfig, this));
                else
                    actorProps = 
                        new ClusterRouterPool(
                            new RoundRobinPool(0),
                            new ClusterRouterPoolSettings(3, 1, false, routeOnRole))
                        .Props(Props.Create<Actor<M, P>>(actorName, registry.AssemblyName));
                var actor = ActorSystem.ActorOf(actorProps, actorName);
                actor.Tell(new RegisterToGlobalDirectory<P>(group.Select(entry => entry.Pattern)));
            }
        }
    }

    public class XmlMessageSystem : MessageSystem<XmlMessage, XmlMessagePattern>
    {
        public XmlMessageSystem(Akka.Configuration.Config config, NodeConfig nodeConfig) : 
            base(
                "Finastra-microservices-actor-system-using-XML-messages",
                config,
                new XmlMessageFactory(),
                new XmlMessagePatternFactory(),
                nodeConfig
                )
        {
        }

        public XmlMessageSystem(ActorSystem system, NodeConfig nodeConfig) :
            base(
                system,
                new XmlMessageFactory(),
                new XmlMessagePatternFactory(),
                nodeConfig
                )
        {
        }
    }

    public class JsonMessageSystem : MessageSystem<JsonMessage, JsonMessagePattern>
    {
        public JsonMessageSystem(Akka.Configuration.Config config, NodeConfig nodeConfig) : 
            base(
                "Finastra-microservices-actor-system-using-JSON-messages",
                config,
                new JsonMessageFactory(),
                new JsonMessagePatternFactory(),
                nodeConfig
                )
        {
        }

        public JsonMessageSystem(ActorSystem system, NodeConfig nodeConfig) :
            base(
                system,
                new JsonMessageFactory(),
                new JsonMessagePatternFactory(),
                nodeConfig
                )
        {
        }
    }

}
