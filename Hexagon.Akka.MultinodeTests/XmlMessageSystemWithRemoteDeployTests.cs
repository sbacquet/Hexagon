//-----------------------------------------------------------------------
// <copyright file="DistributedPubSubMediatorSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Cluster.TestKit;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.Remote.TestKit;
using FluentAssertions;
using Akka.DistributedData;

namespace Hexagon.AkkaImpl.MultinodeTests
{
    using XmlActor = Actor<XmlMessage, XmlMessagePattern>;

    public class XmlMessageSystemWithRemoteDeployTestsConfig : MultiNodeConfig
    {
        public readonly RoleName DeployTarget1;
        public readonly RoleName DeployTarget2;
        public readonly RoleName Deployer;

        public XmlMessageSystemWithRemoteDeployTestsConfig()
        {
            DeployTarget1 = Role("deployTarget1");
            DeployTarget2 = Role("deployTarget2");
            Deployer = Role("deployer");

            CommonConfig =
                ConfigurationFactory
                .ParseString(@"
                    akka.loglevel = DEBUG
                    akka.test.timefactor = 1
                    akka.log-config-on-start = on
                    akka.cluster.roles = [ ""routeHere"" ]
                ")
                .WithFallback(MultiNodeClusterSpec.ClusterConfig())
                .WithFallback(DistributedData.DefaultConfig())
                .WithFallback(DistributedPubSub.DefaultConfig());

            //var roleConfig = ConfigurationFactory.ParseString($@"akka.cluster.roles = [ ""routeHere"" ]");
            //NodeConfig(new[] { DeployTarget1, DeployTarget2 }, new[] { roleConfig });
        }
    }

    public class XmlMessageSystemWithRemoteDeployTests : MultiNodeClusterSpec
    {
        #region setup 
        private readonly RoleName _deployTarget1;
        private readonly RoleName _deployTarget2;
        private readonly RoleName _deployer;

        Hexagon.AkkaImpl.XmlMessageSystem _messageSystem;
        Hexagon.AkkaImpl.PatternActionsRegistry<XmlMessage, XmlMessagePattern> _registry;

        public XmlMessageSystemWithRemoteDeployTests() : this(new XmlMessageSystemWithRemoteDeployTestsConfig())
        {
            _registry = new PatternActionsRegistry<XmlMessage, XmlMessagePattern>();
        }

        protected XmlMessageSystemWithRemoteDeployTests(XmlMessageSystemWithRemoteDeployTestsConfig config) : base(config, typeof(XmlMessageSystemWithRemoteDeployTests))
        {
            _deployTarget1 = config.DeployTarget1;
            _deployTarget2 = config.DeployTarget2;
            _deployer = config.Deployer;
        }

        private void Join(RoleName from, RoleName to)
        {
            RunOn(() =>
            {
                Cluster.Join(Node(to).Address);
                var nodeConfig = new NodeConfig(from.Name);
                if (from.Name == "deployer")
                    nodeConfig.SetActorProps("routed", new NodeConfig.ActorProps { RouteOnRole = "routeHere", TotalMaxRoutees = 3, AllowLocalRoutee = true });
                _messageSystem = new XmlMessageSystem(this.Sys, nodeConfig);
            }, from);
            EnterBarrier(from.Name + "-joined");
        }
        #endregion

        [MultiNodeFact]
        public void Tests()
        {
            Must_startup_3_nodes_cluster();
            XmlMessageSystem_must_work();
        }

        void Must_startup_3_nodes_cluster()
        {
            Within(TimeSpan.FromSeconds(15), () =>
            {
                Join(_deployer, _deployer);
                Join(_deployTarget1, _deployer);
                Join(_deployTarget2, _deployer);
            });
        }

        [PatternActionsRegistration]
        static void Register(PatternActionsRegistry<XmlMessage, XmlMessagePattern> registry)
        {
            registry.Add(
                new XmlMessagePattern(@"/question"),
                (m, sender, self, ms) =>
                {
                    sender.Tell(XmlMessage.FromString($@"<answer>{((ActorRefMessageReceiver<XmlMessage>)self).Actor.Path}</answer>"), self);
                },
                "routed");
        }

        void XmlMessageSystem_must_work()
        {
            Within(TimeSpan.FromSeconds(30), () =>
            {
                RunOn(() =>
                {
                    _messageSystem.Start(_registry);
                }, _deployTarget1, _deployTarget2);
                EnterBarrier("2-deploy-target-started");

                RunOn(() =>
                {
                    Register(_registry);
                    _messageSystem.Start(_registry);
                    _messageSystem.SendMessage(XmlMessage.FromString(@"<question>1</question>"), new ActorRefMessageReceiver<XmlMessage>(TestActor));
                    _messageSystem.SendMessage(XmlMessage.FromString(@"<question>2</question>"), new ActorRefMessageReceiver<XmlMessage>(TestActor));
                    _messageSystem.SendMessage(XmlMessage.FromString(@"<question>3</question>"), new ActorRefMessageReceiver<XmlMessage>(TestActor));
                    var bm1 = ExpectMsg<BytesMessage>();
                    var bm2 = ExpectMsg<BytesMessage>();
                    var bm3 = ExpectMsg<BytesMessage>();
                    XmlMessage.FromBytes(bm1.Bytes).Content.Should().NotBe(XmlMessage.FromBytes(bm2.Bytes).Content);
                    XmlMessage.FromBytes(bm1.Bytes).Content.Should().NotBe(XmlMessage.FromBytes(bm3.Bytes).Content);
                    XmlMessage.FromBytes(bm2.Bytes).Content.Should().NotBe(XmlMessage.FromBytes(bm3.Bytes).Content);
                }, _deployer);

                EnterBarrier("3-done");
            });
        }
    }
}