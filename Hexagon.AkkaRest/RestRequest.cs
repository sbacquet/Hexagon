using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;

namespace Hexagon.AkkaRest
{
    public class RestRequest
    {
        public enum EMethod { GET, PUT, POST, DELETE };
        public EMethod Method;
        public string Path;
        public NameValueCollection Query;
        public string Body;

        public static EMethod MethodFromString(string method)
            => (EMethod)Enum.Parse(typeof(EMethod), method);

        public static JObject BodyToJson(string body)
            => JObject.Parse(body);

        public static bool MatchBody(JObject body, params string[] jsonPathConjuncts)
            => jsonPathConjuncts.All(jsonPath => body.SelectTokens(jsonPath).Any());
    }
}
