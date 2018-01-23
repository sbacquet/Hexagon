using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon
{
    public class XmlMessagePatternFactory : IMessagePatternFactory<XmlMessagePattern>
    {
        public XmlMessagePattern FromConjuncts(string[] conjuncts)
            => new XmlMessagePattern(conjuncts);
    }
}
