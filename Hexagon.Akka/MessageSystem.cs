using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.DistributedData;
using Akka.Event;

//[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Hexagon.Akka.UnitTests")]

namespace Hexagon.AkkaImpl
{
    internal class MessageRegistryEntry<M, P>
    {
        internal P Pattern;
        internal Action<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>> Action;
        internal Func<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, Task> AsyncAction;
        internal string Key;
    }
    public class MessageSystem<M, P> : IMessageSystem<M, P> 
        where P : IMessagePattern<M>
        where M : IMessage
    {
        readonly ActorSystem ActorSystem;
        readonly List<MessageRegistryEntry<M, P>> Registry;
        readonly List<IActorRef> Actors;
        readonly IActorRef Mediator;
        readonly IActorRef Replicator;
        readonly IMessageFactory<M> MessageFactory;
        readonly IMessagePatternFactory<P> PatternFactory;
        readonly ILoggingAdapter Logger;
        readonly NodeConfig NodeConfig;
        readonly ActorDirectory<M,P> ActorDirectory;

        public MessageSystem(
            string systemName, 
            Akka.Configuration.Config config, 
            IMessageFactory<M> messageFactory, 
            IMessagePatternFactory<P> patternFactory,
            NodeConfig nodeConfig)
            : this(
                  null == config ? ActorSystem.Create(systemName) : ActorSystem.Create(systemName, config), 
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
            Registry = new List<MessageRegistryEntry<M, P>>();
            Actors = new List<IActorRef>();
            Mediator = DistributedPubSub.Get(ActorSystem).Mediator;
            Replicator = DistributedData.Get(ActorSystem).Replicator;
            MessageFactory = factory;
            PatternFactory = patternFactory;
            Logger = Logging.GetLogger(system, this);
            NodeConfig = nodeConfig;
            ActorDirectory = new ActorDirectory<M, P>(system, nodeConfig);
        }

        public void Register(P pattern, Action<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>> action, string key)
        {
            Registry.Add(new MessageRegistryEntry<M, P> {
                Pattern = pattern,
                Action = action,
                Key = key });
        }

        public void RegisterAsync(P pattern, Func<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>, Task> action, string key)
        {
            Registry.Add(new MessageRegistryEntry<M, P>
            {
                Pattern = pattern,
                AsyncAction = action,
                Key = key
            });
        }

        public async void SendMessage(M message, ICanReceiveMessage<M> sender)
        {
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
                foreach (var secondaryActorPath in secondaryReceiverPaths)
                {
                    var secondaryReceiver = new ActorPathMessageReceiver<M>(secondaryActorPath, Mediator);
                    secondaryReceiver.Tell(message, sender);
                }
            }
        }

        string GetMainReceiverPath(IEnumerable<ActorDirectory<M,P>.MatchingActor> primaryActors)
        {
            // Look for the primary actors with highest matching score
            var primaryActorsOrderdedByMatchingScore = primaryActors.OrderByDescending(ma => ma.MatchingScore);
            int highestMatchingScore = primaryActorsOrderdedByMatchingScore.First().MatchingScore;
            var mainPrimaryActors = primaryActorsOrderdedByMatchingScore.TakeWhile(ma => ma.MatchingScore == highestMatchingScore);
            if (mainPrimaryActors.Count() == 1)
                // If only one, return it
                return mainPrimaryActors.First().Path;
            // If several primary actors have the same highest score, randomly pick one depending on the mistrust factor
            var mistrusts = mainPrimaryActors.Select(ma => ma.MistrustFactor).ToArray();
            return mainPrimaryActors.ElementAt(MistrustBasedRandomGenerator.SelectIndex(mistrusts)).Path;
        }

        public async Task<M> SendMessageAndAwaitResponse(M message, TimeSpan? timeout = null, System.Threading.CancellationToken? cancellationToken = null)
        {
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
                foreach (var secondaryActorPath in secondaryReceiverPaths)
                {
                    var secondaryReceiver = new ActorPathMessageReceiver<M>(secondaryActorPath, Mediator);
                    secondaryReceiver.Tell(message, null);
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

        public void Start()
        {
            CreateActors();
            Registry.Clear();
            // Synchronize other actors
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(NodeConfig.GossipTimeFrameInSeconds));
        }

        static Func<MessageRegistryEntry<M, P>, Predicate<M>> FilterEntry => entry => message => entry.Pattern.Match(message);
        static Func<MessageRegistryEntry<M, P>, Predicate<M>> NoFilterEntry => entry => null;

        private void CreateActors()
        {
            var groups = Registry.GroupBy(entry => entry.Key);
            foreach (var group in groups)
            {
                string actorName = group.Key;
                Props actorProps;
                Func<MessageRegistryEntry<M, P>, Predicate<M>> filter = (group.Count() == 1 ? NoFilterEntry : FilterEntry);
                var actions = 
                    group
                    .Where(entry => entry.Action != null)
                    .Select(entry => 
                        new Actor<M,P>.ActionWithFilter(
                            entry.Action,
                            filter(entry)));
                var asyncActions =
                    group
                    .Where(entry => entry.AsyncAction != null)
                    .Select(entry =>
                        new Actor<M, P>.AsyncActionWithFilter(
                            entry.AsyncAction,
                            filter(entry)));

                actorProps = Props.Create(() => new Actor<M,P>(actions, asyncActions, MessageFactory, NodeConfig));
                var actor = ActorSystem.ActorOf(actorProps, actorName);
                actor.Tell(new RegisterToGlobalDirectory<P>(group.Select(entry => entry.Pattern)));
            }
        }
    }

    public class XmlMessageSystem : MessageSystem<XmlMessage, XmlMessagePattern>
    {
        public XmlMessageSystem(Akka.Configuration.Config config, NodeConfig nodeConfig) : 
            base(
                "Finastra microservices actor system using XML messages",
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
                "Finastra microservices actor system using JSON messages",
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
