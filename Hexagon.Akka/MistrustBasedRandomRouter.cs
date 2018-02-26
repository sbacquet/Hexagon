using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Routing;
using Akka.Util;
using Akka.Configuration;
using Akka.Dispatch;
using Akka.Actor;

namespace Hexagon.AkkaImpl
{
    public class MistrustBasedRandomRoutingLogic : RoutingLogic
    {
        readonly int[] _mistrustFactors;

        public MistrustBasedRandomRoutingLogic(int[] mistrustFactors)
        {
            _mistrustFactors = mistrustFactors;
        }

        public override Routee Select(object message, Routee[] routees)
        {
            if (routees == null || routees.Length == 0)
            {
                return Routee.NoRoutee;
            }

            if (routees.Length != _mistrustFactors.Length)
            {
                throw new ArgumentOutOfRangeException("routees.Length", "Routing candidates must have the same cardinality as mistrustFactors");
            }

            if (routees.Length == 1)
                return routees[0];

            return routees[SelectIndex(_mistrustFactors)];
        }

        public static int SelectIndex(int[] mistrustFactors)
        {
            if (mistrustFactors.Length > 1)
                mistrustFactors = mistrustFactors.Where(factor => factor > 0).ToArray();
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

    /// <summary>
    /// This class represents a <see cref="Group"/> router that sends messages to a random <see cref="Routee"/> based on their mistrust factor.
    /// </summary>
    public sealed class MistrustBasedRandomGroup : Group
    {
        readonly int[] _weights;

        /// <summary>
        /// Initializes a new instance of the <see cref="MistrustBasedRandomGroup"/> class.
        /// </summary>
        /// <param name="config">
        /// The configuration to use to lookup paths used by the group router.
        /// 
        /// <note>
        /// If 'routees.path' is defined in the provided configuration then those paths will be used by the router.
        /// </note>
        /// </param>
        public MistrustBasedRandomGroup(Config config)
            : this(
                  config.GetStringList("routees.paths").ToArray(),
                  config.GetIntList("routees.weights").ToArray(),
                  Dispatchers.DefaultDispatcherId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MistrustBasedRandomGroup"/> class.
        /// </summary>
        /// <param name="paths">>A list of actor paths used by the group router.</param>
        public MistrustBasedRandomGroup(params string[] paths)
            : base(paths, Dispatchers.DefaultDispatcherId)
        {
            _weights = Enumerable.Repeat(1, paths.Length).ToArray();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MistrustBasedRandomGroup"/> class.
        /// </summary>
        /// <param name="paths">An enumeration of paths used by the group router.</param>
        public MistrustBasedRandomGroup(string[] paths, int[] weights)
            : this(paths, weights, Dispatchers.DefaultDispatcherId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MistrustBasedRandomGroup"/> class.
        /// </summary>
        /// <param name="paths">An enumeration of paths used by the group router.</param>
        /// <param name="routerDispatcher">The dispatcher to use when passing messages to the routees.</param>
        public MistrustBasedRandomGroup(string[] paths, int[] weights, string routerDispatcher)
            : base(paths, routerDispatcher)
        {
            if (paths.Length != weights.Length) throw new ArgumentOutOfRangeException("weights", "For MistrustBasedRandomGroup router, each routee must have its corresponding weight");
            if (weights.Sum() <= 0) throw new ArgumentOutOfRangeException("weights", "Sum of weights must be > 0");

            _weights = weights;
        }

        /// <summary>
        /// Retrieves the actor paths used by this router during routee selection.
        /// </summary>
        /// <param name="system">The actor system that owns this router.</param>
        /// <returns>An enumeration of actor paths used during routee selection</returns>
        public override IEnumerable<string> GetPaths(ActorSystem system)
        {
            return InternalPaths;
        }

        /// <summary>
        /// Creates a router that is responsible for routing messages to routees within the provided <paramref name="system" />.
        /// </summary>
        /// <param name="system">The actor system that owns this router.</param>
        /// <returns>The newly created router tied to the given system.</returns>
        public override Router CreateRouter(ActorSystem system)
        {
            return new Router(new MistrustBasedRandomRoutingLogic(_weights.ToArray()));
        }

        /// <summary>
        /// Creates a new <see cref="MistrustBasedRandomGroup" /> router with a given dispatcher id.
        /// <note>
        /// This method is immutable and returns a new instance of the router.
        /// </note>
        /// </summary>
        /// <param name="dispatcher">The dispatcher id used to configure the new router.</param>
        /// <returns>A new router with the provided dispatcher id.</returns>
        public MistrustBasedRandomGroup WithDispatcher(string dispatcher)
        {
            return new MistrustBasedRandomGroup(InternalPaths, _weights, dispatcher);
        }

        /// <summary>
        /// Creates a surrogate representation of the current <see cref="MistrustBasedRandomGroup"/>.
        /// </summary>
        /// <param name="system">The actor system that owns this router.</param>
        /// <returns>The surrogate representation of the current <see cref="MistrustBasedRandomGroup"/>.</returns>
        public override ISurrogate ToSurrogate(ActorSystem system)
        {
            return new MistrustBasedRandomGroupSurrogate
            {
                Paths = InternalPaths,
                Weights = _weights,
                RouterDispatcher = RouterDispatcher
            };
        }

        /// <summary>
        /// This class represents a surrogate of a <see cref="RandomGroup"/> router.
        /// Its main use is to help during the serialization process.
        /// </summary>
        class MistrustBasedRandomGroupSurrogate : ISurrogate
        {
            /// <summary>
            /// Creates a <see cref="RandomGroup"/> encapsulated by this surrogate.
            /// </summary>
            /// <param name="system">The actor system that owns this router.</param>
            /// <returns>The <see cref="MistrustBasedRandomGroup"/> encapsulated by this surrogate.</returns>
            public ISurrogated FromSurrogate(ActorSystem system)
            {
                return new MistrustBasedRandomGroup(Paths, Weights, RouterDispatcher);
            }

            /// <summary>
            /// The actor paths used by this router during routee selection.
            /// </summary>
            public string[] Paths { get; set; }

            public int[] Weights { get; set; }

            /// <summary>
            /// The dispatcher to use when passing messages to the routees.
            /// </summary>
            public string RouterDispatcher { get; set; }
        }
    }
}
