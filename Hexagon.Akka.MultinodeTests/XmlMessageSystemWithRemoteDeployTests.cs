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
        }
    }

    public class XmlMessageSystemWithRemoteDeployTests : MultiNodeClusterSpec
    {
        #region setup 
        private readonly RoleName _deployTarget1;
        private readonly RoleName _deployTarget2;
        private readonly RoleName _deployer;

        Hexagon.AkkaImpl.XmlMessageSystem _messageSystem;

        public XmlMessageSystemWithRemoteDeployTests() : this(new XmlMessageSystemWithRemoteDeployTestsConfig())
        {
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
                {
                    nodeConfig.AddThisAssembly();
                    nodeConfig.SetActorProps("routed", new NodeConfig.ActorProps { RouteOnRole = "routeHere", TotalMaxRoutees = 3, AllowLocalRoutee = true });
                }
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
        static void RegisterActions(PatternActionsRegistry<XmlMessage, XmlMessagePattern> registry)
        {
            registry.AddAction(
                new XmlMessagePattern(@"/question1"),
                (m, sender, self, ms) =>
                {
                    sender.Tell(XmlMessage.FromString($@"<answer1>{((ActorRefMessageReceiver<XmlMessage>)self).Actor.Path}</answer1>"), self);
                },
                "routed");
            registry.AddPowershellScriptBody(
                new XmlMessagePattern(@"/question2"),
                @"'<answer2>{0}</answer2>' -f $self.Path",
                "routed");
            registry.AddPowershellScriptBody(
                new XmlMessagePattern(true, @"/question2"),
                @"$xml = [xml]$message; $xml.question2",
                "monitor");
        }

        void XmlMessageSystem_must_work()
        {
            Within(TimeSpan.FromSeconds(60), () =>
            {
                RunOn(() =>
                {
                    _messageSystem.Start().Wait();
                }, _deployTarget1, _deployTarget2);
                EnterBarrier("2-deploy-target-started");

                RunOn(() =>
                {
                    _messageSystem.Start().Wait();
                    var sender = new ActorRefMessageReceiver<XmlMessage>(TestActor);
                    _messageSystem.SendMessage(XmlMessage.FromString(@"<question1></question1>"), sender);
                    _messageSystem.SendMessage(XmlMessage.FromString(@"<question1></question1>"), sender);
                    _messageSystem.SendMessage(XmlMessage.FromString(@"<question1></question1>"), sender);
                    new[] { ExpectMsg<BytesMessage>(), ExpectMsg<BytesMessage>(), ExpectMsg<BytesMessage>() }.Select(bm => XmlMessage.FromBytes(bm.Bytes).Content)
                    .Should().OnlyHaveUniqueItems();
                    _messageSystem.SendMessage(XmlMessage.FromString(@"<question2>log1</question2>"), sender);
                    _messageSystem.SendMessage(XmlMessage.FromString(@"<question2>log2</question2>"), sender);
                    _messageSystem.SendMessage(XmlMessage.FromString(@"<question2>log3</question2>"), sender);
                    new[] { ExpectMsg<BytesMessage>(), ExpectMsg<BytesMessage>(), ExpectMsg<BytesMessage>() }.Select(bm => XmlMessage.FromBytes(bm.Bytes).Content)
                    .Should().OnlyHaveUniqueItems();
                }, _deployer);

                EnterBarrier("3-done");
            });
        }
    }
}