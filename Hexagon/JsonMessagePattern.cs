using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Hexagon
{
    public class JsonMessagePattern : IMessagePattern<JsonMessage>
    {
        readonly string[] Conjuncts;

        public int ConjunctCount => Conjuncts.Length;

        public JsonMessagePattern(string[] conjuncts)
        {
            if (conjuncts.Length == 0) { throw new System.ArgumentException("conjuncts cannot be empty"); }
            Conjuncts = conjuncts;
        }
        public bool Match(JsonMessage message)
        {
            return Conjuncts.All(path => message.AsJObject().SelectToken(path) != null);
        }
    }
}
