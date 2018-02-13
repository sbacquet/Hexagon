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
                (message, sender, self, ms) => sender.Tell(XmlMessage.FromString(@"<pong>Pong</pong>"), self),
                "actor1");
        }
    }
}
