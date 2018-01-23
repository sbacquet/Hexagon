using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.DistributedData;
using Akka.Cluster;

namespace Hexagon.AkkaImpl
{
    public class ActorDirectory<M, P> 
        where P : IMessagePattern<M>
    {
        ActorSystem System { get; }
        public ActorDirectory(ActorSystem actorSystem)
        {
            System = actorSystem;
        }

        public async Task<IEnumerable<string>> GetMatchingActorPaths(M message, IMessagePatternFactory<P> messagePatternFactory)
        {
            var replicator = DistributedData.Get(System).Replicator;
            var keysResponse = await replicator.Ask<GetKeysIdsResult>(Dsl.GetKeyIds);
            var keys = keysResponse.Keys;
            var actorPaths = new List<string>();
            var readConsistency = ReadLocal.Instance; //new ReadAll(TimeSpan.FromSeconds(5));
            foreach (string key in keys)
            {
                var setKey = new ORSetKey<GSet<string>>(key);
                var getResponse = await replicator.Ask<IGetResponse>(Dsl.Get(setKey, readConsistency));
                if (getResponse.IsSuccessful)
                {
                    var patterns = getResponse.Get(setKey);
                    if (patterns.Any(pattern => messagePatternFactory.FromConjuncts(pattern.ToArray()).Match(message)))
                    {
                        actorPaths.Add(key);
                    }
                }
            }
            return actorPaths;
        }

        public async Task PublishPatterns(string actorPath, IEnumerable<P> patterns)
        {
            if (!patterns.Any())
                throw new Exception("cannot distribute empty pattern list");

            var cluster = Cluster.Get(System);
            var set = patterns.Aggregate(ORSet<GSet<string>>.Empty, (s, pattern) => s.Add(cluster, GSet.Create(pattern.Conjuncts)));

            var replicator = DistributedData.Get(System).Replicator;
            var setKey = new ORSetKey<GSet<string>>(actorPath);
            var writeConsistency = WriteLocal.Instance; //new WriteAll(TimeSpan.FromSeconds(5));
            var updateResponse = await replicator.Ask<IUpdateResponse>(Dsl.Update(setKey, set, writeConsistency));
            if (!updateResponse.IsSuccessful)
            {
                throw new Exception($"cannot update patterns for actor path {actorPath}");
            }
        }
    }
}
