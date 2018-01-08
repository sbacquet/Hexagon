using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace Hexagon
{
    public class XmlMessage : IMessage
    {
        public byte[] Bytes => Encoding.UTF8.GetBytes(Content);

        public string Content { get; private set; }

        private Lazy<IXPathNavigable> _pathNavigable;
        public IXPathNavigable AsPathNavigable() => _pathNavigable.Value;

        public T AsObject<T>()
        {
            var ser = new System.Xml.Serialization.XmlSerializer(typeof(T));
            using (var reader = new System.IO.StringReader(Content))
            {
                return (T)ser.Deserialize(reader);
            }
        }

        private XmlMessage()
        {
            _pathNavigable = new Lazy<IXPathNavigable>(() =>
            {
                using (var reader = new System.IO.StringReader(Content))
                {
                    return new XPathDocument(reader);
                }
            });
        }

        public static XmlMessage FromObject(object obj)
        {
            var ser = new System.Xml.Serialization.XmlSerializer(obj.GetType());
            using (var writer = new System.IO.StringWriter())
            {
                ser.Serialize(writer, obj);
                return FromString(writer.ToString());
            }
        }

        public static XmlMessage FromBytes(byte[] bytes)
            => new XmlMessage { Content = Encoding.UTF8.GetString(bytes) };

        public static XmlMessage FromString(string xml)
            => new XmlMessage { Content = xml };
    }
}
