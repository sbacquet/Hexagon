using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon
{
    public class ResourceGroup : IDisposable
    {
        readonly Dictionary<string, Lazy<IDisposable>> Resources;

        public ResourceGroup()
        {
            Resources = new Dictionary<string, Lazy<IDisposable>>();
        }

        public void AddResource(string name, Lazy<IDisposable> resource)
            => Resources[name] = resource;

        public Lazy<IDisposable> GetResource(string name)
        {
            Resources.TryGetValue(name, out Lazy<IDisposable> resource);
            return resource;
        }

        public void Dispose()
        {
            foreach (var resource in Resources.Values)
                if (resource.IsValueCreated)
                    resource.Value.Dispose();
        }

        public static Lazy<IDisposable> Create()
            => new Lazy<IDisposable>(() => new ResourceGroup());
    }
}
