using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.DistributedData;
using Akka.Cluster;
using Akka.Event;

namespace Hexagon.AkkaImpl
{
    public class ActorDirectory<M, P> : IDisposable
        where P : IMessagePattern<M>
    {
        readonly ActorSystem ActorSystem;
        readonly ILoggingAdapter Logger;
        readonly NodeConfig NodeConfig;
        IActorRef Watcher = null;

        public ActorDirectory(ActorSystem actorSystem, NodeConfig nodeConfig)
        {
            ActorSystem = actorSystem;
            Logger = Logging.GetLogger(ActorSystem, this);
            NodeConfig = nodeConfig;
        }

        public struct ActorProps
        {
            public string Path;
            public P[] Patterns;
            public int MistrustFactor;
        }
        public struct MatchingActor
        {
            public string Path;
            public int MatchingScore;
            public int MistrustFactor;
            public bool IsSecondary;
        }

        public async Task<IEnumerable<MatchingActor>> GetMatchingActorsAsync(M message, IMessagePatternFactory<P> messagePatternFactory)
        {
            var replicator = DistributedData.Get(ActorSystem).Replicator;
            GetKeysIdsResult keys = await replicator.Ask<GetKeysIdsResult>(Dsl.GetKeyIds);
            var matchingActors = new List<MatchingActor>();
            foreach (var node in keys.Keys)
            {
                var setKey = new LWWRegisterKey<ActorProps[]>(node);
                var getResponse = await replicator.Ask<IGetResponse>(Dsl.Get(setKey, ReadLocal.Instance));
                if (!getResponse.IsSuccessful)
                    throw new Exception($"cannot get message patterns for node {node}");
                var actorPropsList = getResponse.Get(setKey).Value;

                foreach (var actorProps in actorPropsList)
                {
                    var path = actorProps.Path;
                    var matchingPatterns = actorProps.Patterns.Where(pattern => pattern.Match(message));
                    int matchingPatternsCount = matchingPatterns.Count();
                    if (matchingPatternsCount > 0)
                    {
                        var matchingPattern = matchingPatterns.First();
                        if (matchingPatternsCount > 1)
                        {
                            Logger.Warning("For actor {0}, found {1} patterns matching message {2}. Taking first one = {3}", path, matchingPatternsCount, message, matchingPattern);
                        }
                        matchingActors.Add(
                            new MatchingActor
                            {
                                Path = path,
                                IsSecondary = matchingPattern.IsSecondary,
                                MatchingScore = matchingPattern.Conjuncts.Length,
                                MistrustFactor = actorProps.MistrustFactor
                            });
                    }
                }
            }
            return matchingActors;
        }

        public async Task PublishPatternsAsync(params (ActorPath actorPath, P[] patterns)[] actorsToPublish)
        {
            var actorPropsList = actorsToPublish?.Select(actor => new ActorProps
            {
                Path = actor.actorPath.ToStringWithoutAddress(),
                Patterns = actor.patterns,
                MistrustFactor = NodeConfig.GetMistrustFactor(actor.actorPath.Name)
            });
            var cluster = Cluster.Get(ActorSystem);
            var replicator = DistributedData.Get(ActorSystem).Replicator;
            var setKey = new LWWRegisterKey<ActorProps[]>(cluster.SelfUniqueAddress.ToString());

            var updateResponse =
                await replicator.Ask<IUpdateResponse>(
                    Dsl.Update(
                        setKey,
                        new LWWRegister<ActorProps[]>(cluster.SelfUniqueAddress, new ActorProps[] { }),
                        WriteLocal.Instance,
                        reg => actorPropsList == null ? reg : 
                        new LWWRegister<ActorProps[]>(
                            cluster.SelfUniqueAddress,
                            reg.Value.Concat(actorPropsList).ToArray())
                        ));
            if (!updateResponse.IsSuccessful)
            {
                throw new Exception($"cannot public actors");
            }
        }

        public void PublishPatterns(params (ActorPath actorPath, P[] patterns)[] actorsToPublish)
            => PublishPatternsAsync(actorsToPublish).Wait();

        public async Task<bool> RemoveNodeActorsAsync(UniqueAddress node)
        {
            var cluster = Cluster.Get(ActorSystem);
            var replicator = DistributedData.Get(ActorSystem).Replicator;
            var setKey = new LWWRegisterKey<ActorProps[]>(node.ToString());
            var response = await replicator.Ask<IDeleteResponse>(Dsl.Delete(setKey, WriteLocal.Instance));
            return response.AlreadyDeleted || response.IsSuccessful;
        }

        public async Task<bool> IsReadyAsync()
        {
            if (Watcher == null)
                Watcher = ActorSystem.ActorOf(
                    Props.Create(() => 
                    new PatternUnpublisherActor<M, P>(
                        this, 
                        TimeSpan.FromSeconds(NodeConfig.GossipTimeFrameInSeconds))), 
                    Constants.PatternUnpublisherName);
            bool ready = false;
            for (int i = 0; i < NodeConfig.GossipSynchroAttemptCount && !ready; ++i)
            {
                ready = await Watcher.Ask<bool>(PatternUnpublisherActor.IsReady.Instance);
                if (ready) break;
                await Task.Delay(TimeSpan.FromSeconds(NodeConfig.GossipTimeFrameInSeconds));
            }
            return ready;
        }

        public bool IsReady()
            => IsReadyAsync().Result;

        public void Dispose()
        {
            if (Watcher != null)
                Watcher.Tell(PoisonPill.Instance);
        }
    }
}
