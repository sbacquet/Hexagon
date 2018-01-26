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
        readonly ActorSystem System;
        readonly ILoggingAdapter Logger;

        public ActorDirectory(ActorSystem actorSystem)
        {
            System = actorSystem;
            Logger = Logging.GetLogger(System, this);
        }

        public async Task<string[]> GetMatchingActorPaths(M message, IMessagePatternFactory<P> messagePatternFactory)
        {
            var replicator = DistributedData.Get(System).Replicator;
            var keysResponse = await replicator.Ask<GetKeysIdsResult>(Dsl.GetKeyIds);
            var actorPaths = keysResponse.Keys;
            var actorPathsWithMatchingScore = new List<(string Path, int MatchingScore)>();
            var readConsistency = ReadLocal.Instance; //new ReadAll(TimeSpan.FromSeconds(5));
            foreach (string path in actorPaths)
            {
                var setKey = new GSetKey<(GSet<string> Conjuncts, bool IsSecondary)>(path);
                var getResponse = await replicator.Ask<IGetResponse>(Dsl.Get(setKey, readConsistency));
                if (getResponse.IsSuccessful)
                {
                    var conjunctSets = getResponse.Get(setKey);
                    var matchingPatterns = conjunctSets
                        .Select(conjunctSet => messagePatternFactory.FromConjuncts(conjunctSet.Conjuncts.ToArray(), conjunctSet.IsSecondary))
                        .Where(pattern => pattern.Match(message));
                    int matchingPatternsCount = matchingPatterns.Count();
                    if (matchingPatternsCount > 0)
                    {
                        if (matchingPatternsCount > 1)
                        {
                            Logger.Warning("For actor {0}, found {1} handlers matching message {2}", path, matchingPatternsCount, message);
                        }
                        var matchingPattern = matchingPatterns.First();
                        actorPathsWithMatchingScore.Add((path, matchingPattern.IsSecondary ? 0 : matchingPattern.Conjuncts.Length));
                    }
                }
            }
            return actorPathsWithMatchingScore.OrderByDescending(t => t.MatchingScore).Select(t => t.Path).ToArray();
        }

        public async Task PublishPatterns(string actorPath, IEnumerable<P> patterns)
        {
            if (!patterns.Any())
                throw new Exception("cannot distribute empty pattern list");

            var cluster = Cluster.Get(System);
            var set = patterns.Aggregate(GSet<(GSet<string> Conjuncts, bool IsSecondary)>.Empty, (s, pattern) => s.Add((GSet.Create(pattern.Conjuncts), pattern.IsSecondary)));

            var replicator = DistributedData.Get(System).Replicator;
            var setKey = new GSetKey<(GSet<string> Conjuncts, bool IsSecondary)>(actorPath);
            var writeConsistency = WriteLocal.Instance; //new WriteAll(TimeSpan.FromSeconds(5));
            var updateResponse = await replicator.Ask<IUpdateResponse>(Dsl.Update(setKey, set, writeConsistency));
            if (!updateResponse.IsSuccessful)
            {
                throw new Exception($"cannot update patterns for actor path {actorPath}");
            }
        }
    }
}
