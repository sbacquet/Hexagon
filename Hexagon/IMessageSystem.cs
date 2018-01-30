using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexagon
{
    public interface IMessageSystem<M, P>
    {
        void Register(P pattern, Action<M, ICanReceiveMessage<M>, ICanReceiveMessage<M>> action, string key = "default");
        void Start();
        void SendMessage(M message, ICanReceiveMessage<M> sender);
    }
}
