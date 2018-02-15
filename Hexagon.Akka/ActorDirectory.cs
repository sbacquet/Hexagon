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
        public async Task<IEnumerable<MatchingActor>> GetMatchingActors(M message, IMessagePatternFactory<P> messagePatternFactory)
        {
            var replicator = DistributedData.Get(ActorSystem).Replicator;
            GetKeysIdsResult keys = null;
            for (int i = 0; i < 3; ++i)
            {
                keys = await replicator.Ask<GetKeysIdsResult>(Dsl.GetKeyIds);
                if (keys.Keys.Any())
                    break;
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(NodeConfig.GossipTimeFrameInSeconds));
            }
            if (!keys.Keys.Any())
                throw new Exception("Cannot get keys");
            var matchingActors = new List<MatchingActor>();
            foreach (var node in keys.Keys)
            {
                var setKey = new LWWRegisterKey<List<ActorProps>>(node);
                var getResponse = await replicator.Ask<IGetResponse>(Dsl.Get(setKey, ReadLocal.Instance));
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

        public async Task PublishPatterns(string actorPath, IEnumerable<P> patterns)
        {
            if (!patterns.Any())
                throw new Exception("cannot distribute empty pattern list");

            int mistrustFactor = NodeConfig.GetMistrustFactor(actorPath);
            var cluster = Cluster.Get(ActorSystem);
            var replicator = DistributedData.Get(ActorSystem).Replicator;
            var setKey = new LWWRegisterKey<List<ActorProps>>(cluster.SelfUniqueAddress.ToString());

            var updateResponse = 
                await replicator.Ask<IUpdateResponse>(
                    Dsl.Update(
                        setKey,
                        new LWWRegister<List<ActorProps>>(cluster.SelfUniqueAddress, new List<ActorProps>()),
                        WriteLocal.Instance,
                        reg =>
                        {
                            reg.Value.Add(new ActorProps
                            {
                                Path = actorPath,
                                Patterns = patterns.ToArray(),
                                MistrustFactor = mistrustFactor
                            });
                            return new LWWRegister<List<ActorProps>>(cluster.SelfUniqueAddress, reg.Value);
                        }));
            if (!updateResponse.IsSuccessful)
            {
                throw new Exception($"cannot update patterns for actor path {actorPath}");
            }
        }

        public async Task<bool> RemoveNodeActors(UniqueAddress node)
        {
            var cluster = Cluster.Get(ActorSystem);
            var replicator = DistributedData.Get(ActorSystem).Replicator;
            var setKey = new LWWRegisterKey<List<ActorProps>>(node.ToString());
            var response = await replicator.Ask<IDeleteResponse>(Dsl.Delete(setKey, WriteLocal.Instance));
            return response.AlreadyDeleted || response.IsSuccessful;
        }
    }
}
