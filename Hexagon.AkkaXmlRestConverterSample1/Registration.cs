using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Hexagon.AkkaRest;

namespace Hexagon.AkkaXmlRestConverterSample1
{
    using XmlConverter = RestRequestConvertersRegistry<XmlMessage>.Converter;

    class Registrar
    {
        [RestRequestConvertersRegistration]
        static void Registration(RestRequestConvertersRegistry<XmlMessage> registry)
        {
            registry.AddConverter(XmlConverter.FromGET(
                @"/ping", 
                (path, query) => XmlMessage.FromString(string.Format(@"<ping>{0}</ping>", path.Length > 1 ? path[1] : "")), 
                message => message.ToJson()));
            registry.AddConverter(XmlConverter.FromPOST(
                @"/",
                (path, bodyJson) => XmlMessage.FromJson(bodyJson.ToString()), 
                true,
                message => message.ToJson(),
                "$.ping"));
            registry.AddConverter(XmlConverter.FromGET(
                @"/plic",
                (path, query) => XmlMessage.FromString(string.Format(@"<plic>{0}</plic>", path.Length > 1 ? path[1] : "")),
                message => message.ToJson()));
            registry.AddConverter(XmlConverter.FromPOST(
                @"/",
                (path, bodyJson) => XmlMessage.FromJson(bodyJson.ToString()),
                true,
                message => message.ToJson(),
                "$.plic"));
        }
    }
}
