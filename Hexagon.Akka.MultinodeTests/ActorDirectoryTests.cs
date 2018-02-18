using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.TestKit;
using Akka.Remote.TestKit;
using Akka.TestKit;
using Akka.Configuration;
using Akka.DistributedData;
using FluentAssertions;
using Xunit;

namespace Hexagon.AkkaImpl.MultinodeTests
{
    public class ActorDirectoryTestsConfig : MultiNodeConfig
    {
        public RoleName First { get; }
        public RoleName Second { get; }
        public RoleName Third { get; }

        public ActorDirectoryTestsConfig()
        {
            First = Role("first");
            Second = Role("second");
            Third = Role("third");

            CommonConfig = 
                ConfigurationFactory.ParseString($@"
                    akka.actor.provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
                    akka.loglevel = DEBUG
                    akka.log-dead-letters-during-shutdown = off
                    akka.test.timefactor = 1
                    akka.cluster.roles = [ {Hexagon.Constants.NodeRoleName} ]
                ")
                .WithFallback(MultiNodeClusterSpec.ClusterConfig())
                .WithFallback(DistributedData.DefaultConfig());
        }
    }

    public class ActorDirectoryTests : MultiNodeClusterSpec
    {
        #region setup 
        private readonly RoleName _first;
        private readonly RoleName _second;
        private readonly RoleName _third;
        IActorRef _replicator;
        ActorDirectory<XmlMessage, XmlMessagePattern> _actorDirectory;
        NodeConfig _nodeConfig;

        public ActorDirectoryTests() : this(new ActorDirectoryTestsConfig())
        {
        }

        protected ActorDirectoryTests(ActorDirectoryTestsConfig config) : base(config, typeof(ActorDirectoryTests))
        {
            _first = config.First;
            _second = config.Second;
            _third = config.Third;
        }

        private void Join(RoleName from, RoleName to)
        {
            RunOn(() =>
            {
                Cluster.Join(Node(to).Address);
                _replicator = DistributedData.Get(Sys).Replicator;
                _nodeConfig = new NodeConfig(from.Name);
                _actorDirectory = new ActorDirectory<XmlMessage, XmlMessagePattern>(Sys, _nodeConfig);
            }, from);
            EnterBarrier(from.Name + "-joined");
        }
        #endregion

        [MultiNodeFact]
        public void Tests()
        {
            Must_startup_3_nodes_cluster();
            ActorDirectoryMustGetInSync();
        }

        void Must_startup_3_nodes_cluster()
        {
            Within(TimeSpan.FromSeconds(15), () =>
            {
                Join(_first, _first);
                Join(_second, _first);
                Join(_third, _first);
                EnterBarrier("after-1");
            });
        }

        void ActorDirectoryMustGetInSync()
        {
            Within(TimeSpan.FromSeconds(60), () =>
            {
                RunOn(() =>
                {
                    _actorDirectory
                    .PublishPatterns(
                        (ActorPath.Parse(string.Format("akka://cluster/user/{0}", _nodeConfig.GetActorFullName("test1"))),
                        new[]
                        {
                            new XmlMessagePattern(
                                new []
                                {
                                    @"/root/value1[. = 1]",
                                    @"/root/value2[@attr = ""a""]"
                                }),
                            new XmlMessagePattern(
                                new []
                                {
                                    @"/root/value2[. = 2]"
                                })
                        }));
                }, _first);

                RunOn(() =>
                {
                    _actorDirectory
                    .PublishPatterns(
                        (ActorPath.Parse(string.Format("akka://cluster/user/{0}", _nodeConfig.GetActorFullName("test2"))),
                        new[]
                        {
                            new XmlMessagePattern(
                                new []
                                {
                                    @"/root/value3[. = 3]"
                                })
                        }));
                }, _second);

                RunOn(() =>
                {
                    _actorDirectory.PublishPatterns(null);
                }, _third);

                EnterBarrier("2-registered");

                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(_nodeConfig.GossipTimeFrameInSeconds));

                Cluster.State.Members.Where(member => member.Status == MemberStatus.Up).Should().HaveCount(3, "there should be 3 up cluster nodes");

                bool ready = _actorDirectory.IsReady();
                ready.Should().BeTrue("all nodes data must be ready");

                EnterBarrier("3-ready");

                RunOn(() =>
                {
                    var patternFactory = new XmlMessagePatternFactory();
                    string xml = @"<root><value1>1</value1><value2 attr=""b"">2</value2><value3>3</value3></root>";
                    var actorPaths = _actorDirectory.GetMatchingActorsAsync(XmlMessage.FromString(xml), patternFactory).Result.Select(ma => ma.Path);
                    actorPaths.Should().BeEquivalentTo($"/user/{_first.Name}_test1", $"/user/{_second.Name}_test2");
                }, _first, _second, _third);

                EnterBarrier("4-done");

                _actorDirectory.Dispose();

                EnterBarrier("5-exit");
            });
        }
    }
}
