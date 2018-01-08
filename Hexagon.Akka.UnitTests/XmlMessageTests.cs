using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using Xunit;
using Hexagon.AkkaImpl;

namespace Hexagon.AkkaImpl.UnitTests
{
    public class XmlMessageTests : TestKit
    {
        bool AlwaysTrue(XmlMessage message) => true;

        [Fact]
        public void AnActorCanTellAnotherOne()
        {
            var a1 = Tuple.Create<Action<XmlMessage, ICanReceiveMessage<XmlMessage>, ICanReceiveMessage<XmlMessage>>, Predicate<XmlMessage>>(
                (message, sender, self) => TestActor.Tell(XmlMessage.FromString("<message>done</message>")), 
                AlwaysTrue);
            var actor1 = Sys.ActorOf(Props.Create(() => new Actor<XmlMessage, XmlMessagePattern>(
                new [] { a1 }, 
                new XmlMessageFactory())), "actor1");
            var a2 = Tuple.Create<Action<XmlMessage, ICanReceiveMessage<XmlMessage>, ICanReceiveMessage<XmlMessage>>, Predicate<XmlMessage>>(
                (message, sender, self) => actor1.Tell(XmlMessage.FromString("<message>OK received</message>")), 
                AlwaysTrue);
            var actor2 = Sys.ActorOf(Props.Create(() => new Actor<XmlMessage, XmlMessagePattern>(
                new [] { a2 }, 
                new XmlMessageFactory())), "actor2");
            actor2.Tell(XmlMessage.FromString("<message>OK?</message>"), TestActor);
            ExpectMsg<XmlMessage>(message => message.Content == "<message>done</message>");
        }

        [Fact]
        public void AnActorCanAskAnotherOne()
        {
            var a1 = Tuple.Create<Action<XmlMessage, ICanReceiveMessage<XmlMessage>, ICanReceiveMessage<XmlMessage>>, Predicate<XmlMessage>>(
                (message, sender, self) => sender.Tell(XmlMessage.FromString("<message>OK!</message>"), self),
                AlwaysTrue);
            var actor1 = Sys.ActorOf(Props.Create(() => new Actor<XmlMessage, XmlMessagePattern>(
                new[] { a1 },
                new XmlMessageFactory())), "actor1");
            var messageFactory = new XmlMessageFactory();
            var a2 = Tuple.Create<Action<XmlMessage, ICanReceiveMessage<XmlMessage>, ICanReceiveMessage<XmlMessage>>, Predicate<XmlMessage>>(
                (message, sender, self) =>
                {
                    var r = 
                        new ActorRefMessageReceiver<XmlMessage>(actor1)
                        .Ask(XmlMessage.FromString("<message>OK?</message>"), messageFactory)
                        .Result;
                    Assert.Equal("<message>OK!</message>", r.Content);
                    TestActor.Tell(XmlMessage.FromString("<message>done</message>"));
                },
                AlwaysTrue);
            var actor2 = Sys.ActorOf(Props.Create(() => new Actor<XmlMessage, XmlMessagePattern>(
                new[] { a2 },
                new XmlMessageFactory())), "actor2");
            actor2.Tell(XmlMessage.FromString("<message>test</message>"));
            ExpectMsg<XmlMessage>(message => message.Content == "<message>done</message>");
        }

        [Fact]
        public void AnActorCanBeAskedFromOutside()
        {
            var a1 = Tuple.Create<Action<XmlMessage, ICanReceiveMessage<XmlMessage>, ICanReceiveMessage<XmlMessage>>, Predicate<XmlMessage>>(
                (message, sender, self) => sender.Tell(XmlMessage.FromString("<message>OK!</message>"), self),
                AlwaysTrue);
            var messageFactory = new XmlMessageFactory();
            var actor1 = Sys.ActorOf(Props.Create(() => new Actor<XmlMessage, XmlMessagePattern>(
                new[] { a1 },
                messageFactory)), "actor1");
            var r = 
                new ActorRefMessageReceiver<XmlMessage>(actor1)
                .Ask(XmlMessage.FromString("<message>test</message>"), messageFactory)
                .Result;
            Assert.Equal("<message>OK!</message>", r.Content);
        }
    }
}
