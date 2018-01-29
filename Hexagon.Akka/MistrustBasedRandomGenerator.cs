using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon.AkkaImpl
{
    public static class MistrustBasedRandomGenerator
    {
        public static int SelectIndex(int[] mistrustFactors)
        {
            int product = mistrustFactors.Aggregate(1, (prod, factor) => prod * factor);
            int[] weights = mistrustFactors.Select(factor => product / factor).ToArray();
            var indexedWeights = weights.Select((weight, index) => (index, weight));
            var ranges =
                indexedWeights
                .Aggregate(
                    new List<(int index, int lower, int upper)>(),
                    (list, indexedWeight) =>
                    {
                        int lower = 0;
                        int upper = indexedWeight.Item2 - 1;
                        if (list.Any())
                        {
                            int shift = list.Last().upper + 1;
                            lower += shift;
                            upper += shift;
                        }
                        list.Add((indexedWeight.Item1, lower, upper));
                        return list;
                    }
                );
            int selectedIndex = Akka.Util.ThreadLocalRandom.Current.Next(weights.Sum());
            return ranges.First(ilu => selectedIndex >= ilu.lower && selectedIndex <= ilu.upper).index;
        }
    }
}
