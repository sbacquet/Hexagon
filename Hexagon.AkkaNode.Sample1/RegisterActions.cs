using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hexagon.AkkaImpl;

namespace Hexagon.AkkaNode.Sample1
{
    class RegisterActions
    {
        [PatternActionsRegistration]
        static void Register(PatternActionsRegistry<XmlMessage, XmlMessagePattern> registry)
        {
            registry.AddAction(
                new XmlMessagePattern(@"/ping"),
                (message, sender, self, resource, ms, logger) =>
                {
                    logger.Info(@"Received {0}", message);
                    var xml = message.AsPathNavigable();
                    var ping = xml.CreateNavigator().Select(@"/ping");
                    if (ping.Current.Value == "crash")
                        throw new Exception("Crash requested");
                    sender.Tell(XmlMessage.FromString($@"<pong>{self.Path}</pong>"), self);
                },
                "actor1");
        }
    }
}
