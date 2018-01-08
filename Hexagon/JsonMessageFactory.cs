using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon
{
    public class JsonMessageFactory : IMessageFactory<JsonMessage>
    {
        public JsonMessage FromBytes(byte[] bytes) => JsonMessage.FromBytes(bytes);
    }
}
