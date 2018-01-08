using System;
using Akka.Actor;
using System.Threading.Tasks;
using Hexagon;

namespace Hexagon.AkkaImpl
{
    public class ActorRefMessageReceiver<M> : ICanReceiveMessage<M>
        where M : IMessage
    {
        public IActorRef Actor { get; private set; }
        public ActorRefMessageReceiver(IActorRef actor)
        {
            Actor = actor;
        }
        public void Tell(M message, ICanReceiveMessage<M> sender)
        {
            if (sender != null)
                Actor.Tell(new BytesMessage(message.Bytes), (sender as ActorRefMessageReceiver<M>).Actor);
            else
                Actor.Tell(new BytesMessage(message.Bytes));
        }
        public async Task<M> Ask(M message, IMessageFactory<M> factory, TimeSpan? timeout = null, System.Threading.CancellationToken? cancellationToken = null)
        {
            var bytesMessageTask = 
                cancellationToken.HasValue ?
                    Actor.Ask<BytesMessage>(new BytesMessage(message.Bytes), timeout, cancellationToken.Value)
                : 
                    Actor.Ask<BytesMessage>(new BytesMessage(message.Bytes), timeout);
            return await bytesMessageTask.ContinueWith<M>(task => factory.FromBytes(task.Result.Bytes));
        }
    }
}
