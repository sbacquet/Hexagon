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
                @"/order", 
                (path, query) => XmlMessage.FromString(string.Format(@"<orderAction type=""get"" orderId=""{0}"" />", path.Length > 1 ? path[1] : "0")), 
                message => message.ToJson()));

            registry.AddConverter(XmlConverter.FromPOST(
                @"/order",
                (path, bodyJson) =>
                {
                    string xmlTemplate = @"
                        <orderAction type=""create"">
                            <requestId>{0}</requestId>
                            <order>
                                <side>{3}</side>
                                <instrument>{2}</instrument>
                                <quantity>{1}</quantity>
                            </order>
                        </orderAction>";
                    string xml = string.Format(
                        xmlTemplate,
                        System.Guid.NewGuid(),
                        bodyJson["Quantity"],
                        bodyJson["Instrument"],
                        bodyJson["Side"]);
                    return XmlMessage.FromString(xml);
                }, 
                true,
                message => message.ToJson()));
        }
    }
}
