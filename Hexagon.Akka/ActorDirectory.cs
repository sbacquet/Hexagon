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
        IActorRef Watcher = null;

        public ActorDirectory(ActorSystem actorSystem)
        {
            ActorSystem = actorSystem;
            Logger = Logging.GetLogger(ActorSystem, this);
        }

        public class ActorProps
        {
            public string NodeId;
            public string ProcessingUnitId;
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
            public MatchingActor WithMistrustFactor(int mf)
                => new MatchingActor { Path = this.Path, MatchingScore = this.MatchingScore, MistrustFactor = mf, IsSecondary = this.IsSecondary };
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

        public async Task PublishPatternsAsync(NodeConfig nodeConfig, params (string puId, ActorPath actorPath, P[] patterns)[] actorsToPublish)
        {
            var actorPropsList = actorsToPublish?.Select(actor => new ActorProps
            {
                NodeId = nodeConfig.NodeId,
                ProcessingUnitId = actor.puId,
                Path = actor.actorPath.ToStringWithoutAddress(),
                Patterns = actor.patterns,
                MistrustFactor = nodeConfig.GetMistrustFactor(actor.puId)
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

        public void PublishPatterns(NodeConfig nodeConfig, params (string puId, ActorPath actorPath, P[] patterns)[] actorsToPublish)
            => PublishPatternsAsync(nodeConfig, actorsToPublish).Wait();

        public async Task<bool> RemoveNodeActorsAsync(UniqueAddress node)
        {
            var cluster = Cluster.Get(ActorSystem);
            var replicator = DistributedData.Get(ActorSystem).Replicator;
            var setKey = new LWWRegisterKey<ActorProps[]>(node.ToString());
            var response = await replicator.Ask<IDeleteResponse>(Dsl.Delete(setKey, WriteLocal.Instance));
            return response.AlreadyDeleted || response.IsSuccessful;
        }

        public async Task<bool> IsReadyAsync(int attemptCount, TimeSpan timeFrame)
        {
            if (Watcher == null)
                Watcher = ActorSystem.ActorOf(
                    Props.Create(() =>
                    new PatternUnpublisherActor<M, P>(
                        this,
                        timeFrame)),
                    Constants.PatternUnpublisherName);
            bool ready = false;
            for (int i = 0; i < attemptCount && !ready; ++i)
            {
                ready = await Watcher.Ask<bool>(PatternUnpublisherActor.IsReady.Instance);
                if (ready) break;
                await Task.Delay(timeFrame);
            }
            return ready;
        }

        public bool IsReady(int attemptCount, TimeSpan timeFrame)
            => IsReadyAsync(attemptCount, timeFrame).Result;

        public void Dispose()
        {
            if (Watcher != null)
                Watcher.Tell(PoisonPill.Instance);
        }

        public async Task UpdateMistrustFactors(IEnumerable<(string nodeId, string processingUnitId, int newMistrustFactor)> mistrustFactors)
        {
            var cluster = Cluster.Get(ActorSystem);
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
                if (actorPropsList != null && actorPropsList.Any())
                {
                    var actorPropsByKey = actorPropsList.ToDictionary(actorProps => (actorProps.NodeId, actorProps.ProcessingUnitId));
                    foreach (var (nodeId, processingUnitId, newMistrustFactor) in mistrustFactors)
                    {
                        if (actorPropsByKey.TryGetValue((nodeId, processingUnitId), out ActorProps found))
                            found.MistrustFactor = newMistrustFactor;
                    }
                    var updateResponse =
                        await replicator.Ask<IUpdateResponse>(
                            Dsl.Update(
                                setKey,
                                null,
                                WriteLocal.Instance,
                                reg => new LWWRegister<ActorProps[]>(cluster.SelfUniqueAddress, actorPropsList)));
                    if (!updateResponse.IsSuccessful)
                    {
                        Logger.Error($"cannot update mistrust factor for node {node}");
                    }
                    else
                    {
                        Logger.Info($@"Mistrust factors for node {node} updated properly");
                    }
                }
            }
        }

        public async Task<IEnumerable<(
            string nodeId, 
            string processingUnitId, 
            int mistrustFactor, 
            string nodeAddress, 
            string actorPath,
            (string[] conjuncts, bool isSecondary)[] patterns)>> 
            GetProcessingUnits(IEnumerable<(string nodeId, string processingUnitId)> mistrustFactors)
        {
            var cluster = Cluster.Get(ActorSystem);
            var replicator = DistributedData.Get(ActorSystem).Replicator;
            GetKeysIdsResult keys = await replicator.Ask<GetKeysIdsResult>(Dsl.GetKeyIds);
            var matchingActors = new List<MatchingActor>();
            var factors = new List<(string nodeId, string processingUnitId, int mistrustFactor, string nodeAddress, string actorPath, (string[], bool)[] patterns)>();
            foreach (var node in keys.Keys)
            {
                var setKey = new LWWRegisterKey<ActorProps[]>(node);
                var getResponse = await replicator.Ask<IGetResponse>(Dsl.Get(setKey, ReadLocal.Instance));
                if (!getResponse.IsSuccessful)
                    throw new Exception($"cannot get message patterns for node {node}");
                var actorPropsList = getResponse.Get(setKey).Value;
                if (actorPropsList != null && actorPropsList.Any())
                {
                    if (mistrustFactors != null && mistrustFactors.Any())
                    {
                        var actorPropsByKey = actorPropsList.ToDictionary(actorProps => (actorProps.NodeId, actorProps.ProcessingUnitId));
                        foreach (var (nodeId, processingUnitId) in mistrustFactors)
                        {
                            if (actorPropsByKey.TryGetValue((nodeId, processingUnitId), out ActorProps found))
                                factors.Add(
                                    (nodeId: nodeId, 
                                    processingUnitId: processingUnitId, 
                                    mistrustFactor: found.MistrustFactor,
                                    nodeAddress: node,
                                    actorPath: found.Path,
                                    patterns: found.Patterns.Select(pattern => (pattern.Conjuncts, pattern.IsSecondary)).ToArray()
                                    )
                                );
                        }
                    }
                    else
                    {
                        // Return all data
                        factors.AddRange(
                            actorPropsList
                            .Select(
                                ap => (
                                nodeId: ap.NodeId,
                                processingUnitId: ap.ProcessingUnitId,
                                mistrustFactor: ap.MistrustFactor,
                                nodeAddress: node,
                                actorPath: ap.Path,
                                patterns: ap.Patterns.Select(pattern => (pattern.Conjuncts, pattern.IsSecondary)).ToArray()
                                )
                            )
                        );
                    }
                }
            }
            return factors;
        }
    }
}
