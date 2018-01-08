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
    internal class ActorDirectory<M, P> where P : IMessagePattern<M>
    {
        ActorSystem System { get; }
        public ActorDirectory(ActorSystem actorSystem)
        {
            System = actorSystem;
        }

        public async Task<IEnumerable<string>> GetMatchingActorPaths(M message)
        {
            var replicator = DistributedData.Get(System).Replicator;
            var keysResponse = await replicator.Ask<GetKeysIdsResult>(Dsl.GetKeyIds);
            var keys = keysResponse.Keys;
            var actorPaths = new List<string>();
            foreach (string key in keys)
            {
                var setKey = new ORSetKey<P>(key);
                var getResponse = await replicator.Ask<IGetResponse>(Dsl.Get(setKey, ReadLocal.Instance));
                if (getResponse.IsSuccessful)
                {
                    var patterns = getResponse.Get(setKey);
                    if (patterns.Any(pattern => pattern.Match(message)))
                    {
                        actorPaths.Add(key);
                    }
                }
            }
            return actorPaths;
        }

        public async void PublishPatterns(string actorPath, IEnumerable<P> patterns, double timeoutInSeconds = 5)
        {
            if (!patterns.Any())
                throw new Exception("cannot distribute empty pattern list");

            var cluster = Cluster.Get(System);
            var set = patterns.Aggregate(ORSet<P>.Empty, (s, pattern) => s.Add(cluster.SelfUniqueAddress, pattern));

            var replicator = DistributedData.Get(System).Replicator;
            var setKey = new ORSetKey<P>(actorPath);
            var writeConsistency = new WriteAll(TimeSpan.FromSeconds(timeoutInSeconds));
            var updateResponse = await replicator.Ask<IUpdateResponse>(Dsl.Update(setKey, set, writeConsistency));
            if (!updateResponse.IsSuccessful)
            {
                throw new Exception($"cannot update patterns for actor path {actorPath}");
            }
        }
    }
}
