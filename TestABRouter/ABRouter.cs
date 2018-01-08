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

namespace Finastra
{
    public class ABRandomLogic : RoutingLogic
    {
        int _weightForA;
        int _weightForB;

        public ABRandomLogic(int weightForA, int weightForB)
        {
            _weightForA = weightForA;
            _weightForB = weightForB;
        }
        /// <summary>
        /// Picks a random <see cref="Routee"/> to receive the <paramref name="message"/>.
        /// </summary>
        /// <param name="message">The message that is being routed.</param>
        /// <param name="routees">A collection of routees to randomly choose from when receiving the <paramref name="message"/>.</param>
        /// <returns>A <see cref="Routee" /> that receives the <paramref name="message"/>.</returns>
        public override Routee Select(object message, Routee[] routees)
        {
            if (routees == null || routees.Length == 0)
            {
                return Routee.NoRoutee;
            }

            if (routees.Length > 2)
            {
                throw new ArgumentOutOfRangeException("routees.Length", "For A/B router, routees must be 2 max");
            }

            if (routees.Length == 1)
                return routees[0];
            if (_weightForA == 0) return routees[1];
            if (_weightForB == 0) return routees[0];

            int totalWeight = _weightForA + _weightForB;
            int weight = ThreadLocalRandom.Current.Next(totalWeight);
            return routees[System.Convert.ToInt32(weight >= _weightForA)];
        }
    }

    /// <summary>
    /// This class represents a <see cref="Group"/> router that sends messages to a random <see cref="Routee"/>.
    /// </summary>
    public sealed class ABRandomGroup : Group
    {
        IList<int> _weights = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="ABRandomGroup"/> class.
        /// </summary>
        /// <param name="config">
        /// The configuration to use to lookup paths used by the group router.
        /// 
        /// <note>
        /// If 'routees.path' is defined in the provided configuration then those paths will be used by the router.
        /// </note>
        /// </param>
        public ABRandomGroup(Config config)
            : this(
                  config.GetStringList("routees.paths"),
                  config.GetIntList("routees.weights"),
                  Dispatchers.DefaultDispatcherId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ABRandomGroup"/> class.
        /// </summary>
        /// <param name="paths">>A list of actor paths used by the group router.</param>
        public ABRandomGroup(params string[] paths)
            : base(paths, Dispatchers.DefaultDispatcherId)
        {
            if (paths.Length > 2) throw new ArgumentOutOfRangeException("paths", "For ABRandom router, there must be less than 2 routees");
            if (paths.Length == 1)
                _weights = new int[] { 100 };
            else
                _weights = new int[] { 100, 100 };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ABRandomGroup"/> class.
        /// </summary>
        /// <param name="paths">An enumeration of paths used by the group router.</param>
        public ABRandomGroup(IList<string> paths, IList<int> weights)
            : this(paths, weights, Dispatchers.DefaultDispatcherId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ABRandomGroup"/> class.
        /// </summary>
        /// <param name="paths">An enumeration of paths used by the group router.</param>
        /// <param name="routerDispatcher">The dispatcher to use when passing messages to the routees.</param>
        public ABRandomGroup(IList<string> paths, IList<int> weights, string routerDispatcher)
            : base(paths, routerDispatcher)
        {
            if (paths.Count > 2) throw new ArgumentOutOfRangeException("paths", "For ABRandom router, there must be less than 2 routees");
            if (weights.Count > 2) throw new ArgumentOutOfRangeException("weights", "For ABRandom router, there must be less than 2 routee weights");
            if (paths.Count != weights.Count) throw new ArgumentOutOfRangeException("weights", "For ABRandom router, each routee must have its corresponding weight");
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
            return Paths;
        }

        /// <summary>
        /// Creates a router that is responsible for routing messages to routees within the provided <paramref name="system" />.
        /// </summary>
        /// <param name="system">The actor system that owns this router.</param>
        /// <returns>The newly created router tied to the given system.</returns>
        public override Router CreateRouter(ActorSystem system)
        {
            return new Router(new ABRandomLogic(_weights[0], _weights.Count == 2 ? _weights[1] : 0));
        }

        /// <summary>
        /// Creates a new <see cref="ABRandomGroup" /> router with a given dispatcher id.
        /// <note>
        /// This method is immutable and returns a new instance of the router.
        /// </note>
        /// </summary>
        /// <param name="dispatcher">The dispatcher id used to configure the new router.</param>
        /// <returns>A new router with the provided dispatcher id.</returns>
        public ABRandomGroup WithDispatcher(string dispatcher)
        {
            return new ABRandomGroup(Paths.ToList<string>(), _weights, dispatcher);
        }

        /// <summary>
        /// Creates a surrogate representation of the current <see cref="ABRandomGroup"/>.
        /// </summary>
        /// <param name="system">The actor system that owns this router.</param>
        /// <returns>The surrogate representation of the current <see cref="ABRandomGroup"/>.</returns>
        public override ISurrogate ToSurrogate(ActorSystem system)
        {
            return new ABRandomGroupSurrogate
            {
                Paths = Paths,
                RouterDispatcher = RouterDispatcher
            };
        }

        /// <summary>
        /// This class represents a surrogate of a <see cref="RandomGroup"/> router.
        /// Its main use is to help during the serialization process.
        /// </summary>
        public class ABRandomGroupSurrogate : ISurrogate
        {
            IList<int> _weights = null;

            /// <summary>
            /// Creates a <see cref="RandomGroup"/> encapsulated by this surrogate.
            /// </summary>
            /// <param name="system">The actor system that owns this router.</param>
            /// <returns>The <see cref="ABRandomGroup"/> encapsulated by this surrogate.</returns>
            public ISurrogated FromSurrogate(ActorSystem system)
            {
                return new ABRandomGroup(Paths.ToList<string>(), _weights, RouterDispatcher);
            }

            /// <summary>
            /// The actor paths used by this router during routee selection.
            /// </summary>
            public IEnumerable<string> Paths { get; set; }

            /// <summary>
            /// The dispatcher to use when passing messages to the routees.
            /// </summary>
            public string RouterDispatcher { get; set; }
        }
    }
}
