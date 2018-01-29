using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.TestKit.Xunit2;
using Xunit;

namespace Hexagon.AkkaImpl.Tests
{
    public class MistrustBasedRandomGeneratorTests
    {
        [Fact]
        public void Test()
        {
            int[] mistrustFactors = { 10, 20, 40 };
            var result = 
                Enumerable.Range(0, 1000)
                .Select(i => MistrustBasedRandomGenerator.SelectIndex(mistrustFactors))
                .GroupBy(i => i)
                .ToDictionary(group => group.Key, group => group.Count());
        }
    }
}
