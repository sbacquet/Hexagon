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
    public class ActorDirectory<M, P> 
        where P : IMessagePattern<M>
    {
        readonly ActorSystem ActorSystem;
        readonly ILoggingAdapter Logger;
        readonly NodeConfig NodeConfig;

        public ActorDirectory(ActorSystem actorSystem, NodeConfig nodeConfig)
        {
            ActorSystem = actorSystem;
            Logger = Logging.GetLogger(ActorSystem, this);
            NodeConfig = nodeConfig;
        }

        public struct ActorProps
        {
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
        public async Task<IEnumerable<MatchingActor>> GetMatchingActors(M message, IMessagePatternFactory<P> messagePatternFactory)
        {
            var replicator = DistributedData.Get(ActorSystem).Replicator;
            var setKey = new LWWDictionaryKey<string, ActorProps>("ActorDirectory");
            var getResponse = await replicator.Ask<IGetResponse>(Dsl.Get(setKey, ReadLocal.Instance));
            var matchingActors = new List<MatchingActor>();
            if (!getResponse.IsSuccessful)
            {
                int attemptCount = NodeConfig.GossipSynchroAttemptCount;
                for (int attempt = 1; attempt <= attemptCount; ++attempt)
                {
                    Logger.Warning("Could not read actor directory locally, trying to get it from all cluster nodes ({0}/{1})...", attempt, attemptCount);
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(NodeConfig.GossipTimeFrameInSeconds));
                    getResponse = await replicator.Ask<IGetResponse>(Dsl.Get(setKey, new ReadAll(TimeSpan.FromSeconds(5))));
                    if (getResponse.IsSuccessful)
                    {
                        Logger.Info("Actor directory successfully read from cluster nodes after {0} attempt(s)", attempt);
                        break;
                    }
                    if (attempt == attemptCount)
                    {
                        Logger.Error("Could not read actor directory from cluster nodes after {0} attempts, giving up", attemptCount);
                        return matchingActors;
                    }
                }
            }
            var actors = getResponse.Get(setKey);
            foreach (var actor in actors)
            {
                var path = actor.Key;
                var actorProps = actor.Value;
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
            return matchingActors;
        }

        public async Task PublishPatterns(string actorPath, IEnumerable<P> patterns)
        {
            if (!patterns.Any())
                throw new Exception("cannot distribute empty pattern list");

            int mistrustFactor = NodeConfig.GetMistrustFactor(actorPath);
            var cluster = Cluster.Get(ActorSystem);
            var replicator = DistributedData.Get(ActorSystem).Replicator;
            var setKey = new LWWDictionaryKey<string, ActorProps>("ActorDirectory");

            var writeConsistency = WriteLocal.Instance;
            var updateResponse = 
                await replicator.Ask<IUpdateResponse>(
                    Dsl.Update(
                        setKey, 
                        LWWDictionary<string, ActorProps>.Empty, 
                        writeConsistency, 
                        ad => ad.SetItem(
                            cluster, 
                            actorPath, 
                            new ActorProps
                            {
                                Patterns = patterns.ToArray(),
                                MistrustFactor = mistrustFactor
                            })));
            if (!updateResponse.IsSuccessful)
            {
                throw new Exception($"cannot update patterns for actor path {actorPath}");
            }
        }
    }
}
