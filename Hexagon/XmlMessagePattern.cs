﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Hexagon
{
    public class XmlMessagePattern : IMessagePattern<XmlMessage>
    {
        readonly string[] Conjuncts;

        public int ConjunctCount => Conjuncts.Length;

        public XmlMessagePattern(string[] conjuncts)
        {
            if (conjuncts.Length == 0) { throw new System.ArgumentException("conjuncts cannot be empty"); }
            Conjuncts = conjuncts;
        }
        public bool Match(XmlMessage message)
        {
            var navigator = message.AsPathNavigable().CreateNavigator();
            return Conjuncts.All(path => navigator.SelectSingleNode(path) != null);
        }
    }
}