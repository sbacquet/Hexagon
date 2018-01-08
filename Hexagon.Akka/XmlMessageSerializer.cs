using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon.AkkaImpl
{
    class XmlMessageSerializer : Akka.Serialization.Serializer
    {
        public override bool IncludeManifest => false;

        public XmlMessageSerializer(Akka.Actor.ExtendedActorSystem system)
            : base(system)
        {
        }

        public override object FromBinary(byte[] bytes, Type type) => XmlMessage.FromBytes(bytes);

        public override byte[] ToBinary(object obj) => ((XmlMessage)obj).Bytes;
    }
}
