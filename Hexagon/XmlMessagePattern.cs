using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Hexagon
{
    public class XmlMessagePattern : IMessagePattern<XmlMessage>
    {
        public string[] Conjuncts { get; }

        public bool IsSecondary { get; }

        public XmlMessagePattern(params string[] conjuncts) : this(false, conjuncts)
        {
        }
        public XmlMessagePattern(bool isSecondary, params string[] conjuncts)
        {
            if (conjuncts.Length == 0) { throw new System.ArgumentException("conjuncts cannot be empty"); }
            Conjuncts = conjuncts;
            IsSecondary = isSecondary;
        }
        public bool Match(XmlMessage message)
        {
            var navigator = message.AsPathNavigable().CreateNavigator();
            return Conjuncts.All(path => navigator.SelectSingleNode(path) != null);
        }

        public override string ToString()
        {
            string conjunctsString = string.Join(" and ", Conjuncts);
            string secondaryString = IsSecondary ? " (secondary)" : "";
            return conjunctsString + secondaryString;
        }
    }
}
