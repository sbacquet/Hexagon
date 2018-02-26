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
using System.Configuration;
using Akka.Configuration.Hocon;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Hexagon.Akka.UnitTests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Hexagon.Akka.MultiNodeTests")]

namespace Hexagon.AkkaImpl
{
    public class AkkaMessageSystem<M, P> : MessageSystem<M, P>
        where P : IMessagePattern<M>
        where M : IMessage
    {
        public readonly ActorSystem ActorSystem;
        public IActorRef Mediator => DistributedPubSub.Get(ActorSystem).Mediator;
        public IActorRef Replicator => DistributedData.Get(ActorSystem).Replicator;
        readonly ActorDirectory<M,P> ActorDirectory;

        static AkkaMessageSystem<M, P> _instance = null;
        public static AkkaMessageSystem<M, P> Instance
        {
            get => _instance;
            private set
            {
                if (value != null && _instance != null)
                    throw new Exception($"MessageSystem<{typeof(M).Name},{typeof(P).Name}> singleton already set !");
                _instance = value;
            }
        }

        public AkkaMessageSystem(
            ActorSystem system, 
            IMessageFactory<M> factory,
            IMessagePatternFactory<P> patternFactory)
            : base(factory, patternFactory, new Logger(system.Log))
        {
            ActorSystem = system;
            ActorDirectory = new ActorDirectory<M, P>(system);

            Instance = this;
        }

        public override async Task SendMessageAsync(M message, ICanReceiveMessage<M> sender)
        {
            if (sender != null && !(sender is ActorRefMessageReceiver<M>))
                throw new ArgumentException("the sender must be a ActorRefMessageReceiver", "sender");

            var actorPaths = await ActorDirectory.GetMatchingActorsAsync(message, PatternFactory);
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

        public override async Task<M> SendMessageAndAwaitResponseAsync(M message, ICanReceiveMessage<M> sender, TimeSpan? timeout = null, System.Threading.CancellationToken? cancellationToken = null)
        {
            if (sender != null && !(sender is ActorRefMessageReceiver<M>))
                throw new ArgumentException("the sender must be a ActorRefMessageReceiver", "sender");

            var actorPaths = await ActorDirectory.GetMatchingActorsAsync(message, PatternFactory);
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
                return await mainReceiver.AskAsync(message, MessageFactory, timeout ?? TimeSpan.FromSeconds(10), cancellationToken);
            }
            else
            {
                Logger.Warning(@"No primary receiver found for message {0}", message);
                return default(M); // Normal ?
            }
        }

        public override async Task StartAsync(NodeConfig nodeConfig, PatternActionsRegistry<M, P> registry = null)
        {
            Logger.Info("Starting the message system...");
            // Initialize mediator
            DistributedPubSub.Get(ActorSystem);
            // Initialize replicator
            DistributedData.Get(ActorSystem);
            var actionsRegistry = new PatternActionsRegistry<M, P>();
            actionsRegistry.AddRegistry(registry);
            // Create actors from registry
            if (nodeConfig.Assemblies != null)
            {
                foreach (var assembly in nodeConfig.Assemblies)
                {
                    actionsRegistry.AddActionsFromAssembly(assembly);
                }
            }
            await CreateActorsAsync(nodeConfig, actionsRegistry);
            var timeFrame = TimeSpan.FromSeconds(nodeConfig.GossipTimeFrameInSeconds);
            await Task.Delay(timeFrame);
            bool ready = await ActorDirectory.IsReadyAsync(nodeConfig.GossipSynchroAttemptCount, timeFrame);
            if (ready)
                Logger.Info("Message system started and ready");
            else
            {
                Logger.Error("Message system did not get ready within allocated timeframe");
                throw new Exception("Message system did not get ready within allocated timeframe !");
            }
        }

        static Func<PatternActionsRegistry<M, P>.MessageRegistryEntry, Predicate<M>> FilterEntry => entry => message => entry.Pattern.Match(message);
        static Func<PatternActionsRegistry<M, P>.MessageRegistryEntry, Predicate<M>> NoFilterEntry => entry => null;

        public static (IEnumerable<Actor<M,P>.ActionWithFilter>, IEnumerable<Actor<M, P>.AsyncActionWithFilter>) GetActions(IEnumerable<PatternActionsRegistry<M, P>.MessageRegistryEntry> registryEntries)
        {
            Func<PatternActionsRegistry<M, P>.MessageRegistryEntry, Predicate<M>> filter = (registryEntries.Count() == 1 ? NoFilterEntry : FilterEntry);
            var actions =
                registryEntries
                .Where(entry => entry.CodeType == EActionType.Code && entry.Action != null)
                .Select(entry =>
                    new Actor<M, P>.ActionWithFilter
                    {
                        Action = entry.Action,
                        Filter = filter(entry)
                    });
            var powershellActions =
                registryEntries
                .Where(entry => entry.CodeType == EActionType.PowershellScript)
                .Select(entry =>
                    new Actor<M, P>.ActionWithFilter
                    {
                        Action = PowershellScriptToAction(entry.Code, !entry.Pattern.IsSecondary),
                        Filter = filter(entry)
                    });
            var asyncActions =
                registryEntries
                .Where(entry => entry.CodeType == EActionType.Code && entry.AsyncAction != null)
                .Select(entry =>
                    new Actor<M, P>.AsyncActionWithFilter
                    {
                        Action = entry.AsyncAction,
                        Filter = filter(entry)
                    });
            return (actions.Union(powershellActions), asyncActions);
        }

        private async Task CreateActorsAsync(NodeConfig nodeConfig, PatternActionsRegistry<M, P> registry)
        {
            var groups = registry.LookupByProcessingUnit();
            var actors = new List<(string puId, IActorRef actor, IEnumerable<P> patterns)>();
            foreach (var group in groups)
            {
                string processingUnitId = group.Key;
                var resource = registry.GetProcessingUnitResource(processingUnitId);
                var props = nodeConfig.GetProcessingUnitProps(processingUnitId);
                string actorName = Hexagon.Constants.GetProcessingUnitName(nodeConfig.NodeId, processingUnitId);
                string routeOnRole = props?.RouteOnRole;
                Props actorProps;
                if (routeOnRole == null)
                {
                    var (actions, asyncActions) = GetActions(group.AsEnumerable());
                    actorProps = Props.Create<Actor<M, P>>(actions, asyncActions, MessageFactory, this, resource);
                }
                else
                {
                    string router = props.Router ?? Constants.DefaultRouter;
                    Logger.Debug(@"Deploying remote processing unit ""{0}"" with router ""{1}""", actorName, router);
                    actorProps =
                        new ClusterRouterPool(
                            GetRouterPool(router),
                            new ClusterRouterPoolSettings(props.TotalMaxRoutees, props.MaxRouteesPerNode, props.AllowLocalRoutee, routeOnRole))
                        .Props(
                            Props.Create<Actor<M, P>>(
                                processingUnitId,
                                group
                                .Select(
                                    entry =>
                                    (entry.CodeType,
                                    (entry.CodeType != EActionType.Code ? entry.Pattern.ToTuple() : (new string[] { }, false)),
                                    entry.Code))
                                .Distinct()
                                .ToArray()));
                }
                var actor = ActorSystem.ActorOf(actorProps, actorName);
                Logger.Debug(@"Processing unit ""{0}"" created properly, path = {1}", actorName, actor.Path.ToStringWithoutAddress());
                actors.Add((puId: processingUnitId, actor: actor, patterns: group.Select(entry => entry.Pattern)));
            };
            await RegisterToGlobalDirectoryAsync(nodeConfig, actors);
            Logger.Debug("Processing units registered properly");
        }

        Pool GetRouterPool(string routerName)
        {
            var path = $"akka.actor.router.type-mapping.{routerName}-pool";
            var routerTypeName = ActorSystem.Settings.Config.GetString(path);
            var routerType = Type.GetType($"{routerTypeName}, Akka");
            return (Pool)Activator.CreateInstance(routerType, 0);
        }

        async Task RegisterToGlobalDirectoryAsync(NodeConfig nodeConfig, IEnumerable<(string puId, IActorRef actor, IEnumerable<P> patterns)> actors)
        {
            foreach (var actor in actors.Select(a => a.actor).Distinct())
                Mediator.Tell(new Put(actor));

            await ActorDirectory.PublishPatternsAsync(
                nodeConfig,
                actors
                .Select(a => (puId: a.puId, actorPath: a.actor.Path, patterns: a.patterns.ToArray()))
                .ToArray());
        }

        public override void Dispose()
        {
            ActorDirectory.Dispose();
            CoordinatedShutdown.Get(ActorSystem).Run().Wait();
            Instance = null;
        }
    }

    public static class AkkaMessageSystem
    {
        public static AkkaMessageSystem<M, P> Create<M, P>(
            IMessageFactory<M> messageFactory,
            IMessagePatternFactory<P> patternFactory,
            AkkaNodeConfig nodeConfig)
            where P : IMessagePattern<M>
            where M : IMessage
        {
            var actorSystem = ActorSystem.Create(nodeConfig.SystemName, DefaultAkkaConfig(nodeConfig));

            return new AkkaMessageSystem<M, P>(actorSystem, messageFactory, patternFactory);
        }

        public static Config DefaultAkkaConfig(AkkaNodeConfig nodeConfig)
        {
            var roles = string.Join(",", nodeConfig.Roles.Union(new[] { Hexagon.Constants.NodeRoleName }).Distinct().Select(item => $"\"{item}\""));
            var seeds = string.Join(",", nodeConfig.SeedNodes.Select(item => $@"""akka.tcp://{nodeConfig.SystemName}@{item}"""));
            return
                ConfigurationFactory.ParseString($@"
                    akka.actor.provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
                    akka.remote.dot-netty.tcp.hostname = ""{System.Net.Dns.GetHostName()}""
                    akka.remote.dot-netty.tcp.port = 0
                    akka.cluster.roles = [{roles}]
                    akka.cluster.seed-nodes = [{seeds}]
                    akka.cluster.auto-down-unreachable-after = 10s
                    akka.cluster.pub-sub.role = {Hexagon.Constants.NodeRoleName}
                    akka.cluster.distributed-data.role = {Hexagon.Constants.NodeRoleName}
                ")
                .WithFallback(nodeConfig.Akka)
                .WithFallback(DistributedData.DefaultConfig())
                .WithFallback(DistributedPubSub.DefaultConfig());
        }
    }

    public static class AkkaXmlMessageSystem
    {
        public static AkkaMessageSystem<XmlMessage, XmlMessagePattern> Create(AkkaNodeConfig nodeConfig)
            => AkkaMessageSystem.Create(
                new XmlMessageFactory(),
                new XmlMessagePatternFactory(),
                nodeConfig
                );

        public static AkkaMessageSystem<XmlMessage, XmlMessagePattern> Create(ActorSystem system)
            => new AkkaMessageSystem<XmlMessage, XmlMessagePattern>(
                system,
                new XmlMessageFactory(),
                new XmlMessagePatternFactory()
                );
    }

    public static class AkkaJsonMessageSystem
    {
        public static AkkaMessageSystem<JsonMessage, JsonMessagePattern> Create(AkkaNodeConfig nodeConfig)
            => AkkaMessageSystem.Create(
                new JsonMessageFactory(),
                new JsonMessagePatternFactory(),
                nodeConfig
                );

        public static AkkaMessageSystem<JsonMessage, JsonMessagePattern> Create(ActorSystem system)
            => new AkkaMessageSystem<JsonMessage, JsonMessagePattern>(
                system,
                new JsonMessageFactory(),
                new JsonMessagePatternFactory()
                );
    }
}
