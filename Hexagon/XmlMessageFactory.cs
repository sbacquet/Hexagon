using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon
{
    public class XmlMessageFactory : IMessageFactory<XmlMessage>
    {
        public XmlMessage FromBytes(byte[] bytes) => XmlMessage.FromBytes(bytes);
        public XmlMessage FromString(string content) => XmlMessage.FromString(content);
    }
}
