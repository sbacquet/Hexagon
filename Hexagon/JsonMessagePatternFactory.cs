using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon
{
    public class JsonMessagePatternFactory : IMessagePatternFactory<JsonMessagePattern>
    {
        public JsonMessagePattern FromConjuncts(string[] conjuncts, bool isSecondary)
            => new JsonMessagePattern(conjuncts);
    }
}
