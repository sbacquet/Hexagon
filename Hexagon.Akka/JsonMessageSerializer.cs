using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon.AkkaImpl
{
    class JsonMessageSerializer : Akka.Serialization.Serializer
    {
        public override bool IncludeManifest => false;

        public JsonMessageSerializer(Akka.Actor.ExtendedActorSystem system)
            : base(system)
        {
        }

        public override object FromBinary(byte[] bytes, Type type) => JsonMessage.FromBytes(bytes);

        public override byte[] ToBinary(object obj) => ((JsonMessage)obj).Bytes;
    }
}
