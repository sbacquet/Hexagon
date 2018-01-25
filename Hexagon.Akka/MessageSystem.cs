using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.DistributedData;

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

        public MessageSystem(
            string systemName, 
            Akka.Configuration.Config config, 
            IMessageFactory<M> messageFactory, 
            IMessagePatternFactory<P> patternFactory)
            : this(
                  null == config ? ActorSystem.Create(systemName) : ActorSystem.Create(systemName, config), 
                  messageFactory, 
                  patternFactory)
        {
        }

        public MessageSystem(
            ActorSystem system, 
            IMessageFactory<M> factory,
            IMessagePatternFactory<P> patternFactory)
        {
            ActorSystem = system;
            Registry = new List<MessageRegistryEntry<M, P>>();
            Actors = new List<IActorRef>();
            Mediator = DistributedPubSub.Get(ActorSystem).Mediator;
            Replicator = DistributedData.Get(ActorSystem).Replicator;
            MessageFactory = factory;
            PatternFactory = patternFactory;
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
            var actorDirectory = new ActorDirectory<M, P>(ActorSystem);
            var actorPaths = await actorDirectory.GetMatchingActorPaths(message, PatternFactory);
            if (!actorPaths.Any())
            {
                // TODO: log
                return;
            }
            var mainReceiver = new ActorPathMessageReceiver<M>(actorPaths.First(), Mediator);
            mainReceiver.Tell(message, sender);
            foreach (var secondaryActorPath in actorPaths.Where((_, i) => i > 0))
            {
                var secondaryReceiver = new ActorPathMessageReceiver<M>(secondaryActorPath, Mediator);
                secondaryReceiver.Tell(message, null);
            }
        }

        public async Task<M> SendMessageSync(M message, TimeSpan? timeout = null, System.Threading.CancellationToken? cancellationToken = null)
        {
            var actorDirectory = new ActorDirectory<M, P>(ActorSystem);
            var actorPaths = await actorDirectory.GetMatchingActorPaths(message, PatternFactory);
            if (!actorPaths.Any())
            {
                // TODO: log
                return default(M);
            }
            foreach (var secondaryActorPath in actorPaths.Where((_, i) => i > 0))
            {
                var secondaryReceiver = new ActorPathMessageReceiver<M>(secondaryActorPath, Mediator);
                secondaryReceiver.Tell(message, null);
            }
            var mainReceiver = new ActorPathMessageReceiver<M>(actorPaths.First(), Mediator);
            return await mainReceiver.Ask(message, MessageFactory, timeout, cancellationToken);
        }

        public void Start()
        {
            CreateActors();
            Registry.Clear();
            // Synchronize other actors
            System.Threading.Thread.Sleep(3000);
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

                actorProps = Props.Create(() => new Actor<M,P>(actions, asyncActions, MessageFactory));
                var actor = ActorSystem.ActorOf(actorProps, actorName);
                actor.Tell(new RegisterToGlobalDirectory<P>(
                    group
                    .Select(entry => entry.Pattern)
                    .OrderByDescending(pattern => pattern.Conjuncts.Length)));
            }
        }
    }

    public class XmlMessageSystem : MessageSystem<XmlMessage, XmlMessagePattern>
    {
        public XmlMessageSystem(Akka.Configuration.Config config) : 
            base(
                "Finastra microservices actor system using XML messages",
                config,
                new XmlMessageFactory(),
                new XmlMessagePatternFactory()
                )
        {
        }

        public XmlMessageSystem(ActorSystem system) :
            base(
                system,
                new XmlMessageFactory(),
                new XmlMessagePatternFactory()
                )
        {
        }
    }

    public class JsonMessageSystem : MessageSystem<JsonMessage, JsonMessagePattern>
    {
        public JsonMessageSystem(Akka.Configuration.Config config) : 
            base(
                "Finastra microservices actor system using JSON messages",
                config,
                new JsonMessageFactory(),
                new JsonMessagePatternFactory()
                )
        {
        }

        public JsonMessageSystem(ActorSystem system) :
            base(
                system,
                new JsonMessageFactory(),
                new JsonMessagePatternFactory()
                )
        {
        }
    }

}
