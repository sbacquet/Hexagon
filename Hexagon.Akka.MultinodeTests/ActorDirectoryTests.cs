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
    using XmlActor = Actor<XmlMessage, XmlMessagePattern>;

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

            CommonConfig = ConfigurationFactory.ParseString(@"
                akka.actor.provider = ""Akka.Cluster.ClusterActorRefProvider, Akka.Cluster""
                akka.loglevel = DEBUG
                akka.log-dead-letters-during-shutdown = on
            ").WithFallback(DistributedData.DefaultConfig());

            TestTransport = true;
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
                _actorDirectory = new ActorDirectory<XmlMessage, XmlMessagePattern>(Sys);
            }, from);
            EnterBarrier(from.Name + "-joined");
        }
        #endregion

        [MultiNodeFact]
        public void Tests()
        {
            Must_startup_3_nodes_cluster();
            Must_send_and_receive_string();
            Must_send_and_receive_through_directory();
        }

        public void Must_startup_3_nodes_cluster()
        {
            Within(TimeSpan.FromSeconds(15), () =>
            {
                Join(_first, _first);
                Join(_second, _first);
                Join(_third, _first);
                EnterBarrier("after-1");
            });
        }

        public void Must_send_and_receive_string()
        {
            Within(TimeSpan.FromSeconds(15), () =>
            {
                RunOn(() =>
                {
                    var cluster = Cluster.Get(Sys);
                    var key = new ORSetKey<GSet<string>>("keyA");
                    var set = ORSet.Create(cluster.SelfUniqueAddress, GSet.Create("value"));
                    var writeConsistency = WriteLocal.Instance;

                    var response = _replicator.Ask<IUpdateResponse>(Dsl.Update(key, set)).Result;
                    Assert.True(response.IsSuccessful);
                    //response.IsSuccessful.Should().BeTrue();

                    System.Threading.Thread.Sleep(5000);
                }, _first);

                EnterBarrier("2-registered");

                RunOn(() =>
                {
                    var key = new ORSetKey<GSet<string>>("keyA");
                    var readConsistency = ReadLocal.Instance;

                    var response = _replicator.Ask<IGetResponse>(Dsl.Get(key, readConsistency)).Result;
                    Assert.True(response.IsSuccessful);
                    var data = response.Get(key);
                    Assert.True(1 == data.Count);
                    Assert.True(1 == data.First().Count);
                    Assert.Equal("value", data.First().First());
                }, _second, _third);
            });
        }

        public void Must_send_and_receive_through_directory()
        {
            Within(TimeSpan.FromSeconds(30), () =>
            {
                RunOn(() =>
                {
                    _actorDirectory
                    .PublishPatterns(
                        "/user/test1",
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
                        })
                    .Wait();
                }, _first);

                RunOn(() =>
                {
                    _actorDirectory
                    .PublishPatterns(
                        "/user/test2",
                        new[]
                        {
                            new XmlMessagePattern(
                                new []
                                {
                                    @"/root/value3[. = 3]"
                                })
                        })
                    .Wait();
                }, _second);

                EnterBarrier("2-registered");
                System.Threading.Thread.Sleep(3000);

                RunOn(() =>
                {
                    var patternFactory = new XmlMessagePatternFactory();
                    string xml = @"<root><value1>1</value1><value2 attr=""b"">2</value2><value3>3</value3></root>";
                    var actorPaths = _actorDirectory.GetMatchingActorPaths(XmlMessage.FromString(xml), patternFactory).Result;
                    actorPaths.Should().BeEquivalentTo("/user/test1", "/user/test2");
                }, _first, _second, _third);
            });
        }
    }
}
