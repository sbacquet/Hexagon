using System;
using Akka.Actor;
using System.Threading.Tasks;
using Hexagon;

namespace Hexagon.AkkaImpl
{
    public class ActorRefMessageReceiver<M> : ICanReceiveMessage<M>
        where M : IMessage
    {
        readonly internal IActorRef Actor;

        public ActorRefMessageReceiver(IActorRef actor)
        {
            Actor = actor;
        }
        internal ActorRefMessageReceiver(ActorRefMessageReceiver<M> receiver) : this(receiver.Actor)
        {
        }
        public virtual void Tell(M message, ICanReceiveMessage<M> sender)
        {
            if (sender != null)
                Actor.Tell(new BytesMessage(message.Bytes), (sender as ActorRefMessageReceiver<M>).Actor);
            else
                Actor.Tell(new BytesMessage(message.Bytes));
        }
        public virtual async Task<M> Ask(M message, IMessageFactory<M> factory, TimeSpan? timeout = null, System.Threading.CancellationToken? cancellationToken = null)
        {
            var bytesMessageTask = 
                cancellationToken.HasValue ?
                    Actor.Ask<BytesMessage>(new BytesMessage(message.Bytes), timeout, cancellationToken.Value)
                : 
                    Actor.Ask<BytesMessage>(new BytesMessage(message.Bytes), timeout);
            return await bytesMessageTask.ContinueWith<M>(task => factory.FromBytes(task.Result.Bytes));
        }

        public string Path => Actor.Path.ToString();
    }

    public class ReadOnlyActorRefMessageReceiver<M> : ActorRefMessageReceiver<M>
        where M : IMessage
    {
        public readonly string ActorPath;

        internal ReadOnlyActorRefMessageReceiver(ActorRefMessageReceiver<M> receiver) : base(receiver)
        {
            ActorPath = Actor.Path.ToStringWithoutAddress();
        }
        public override Task<M> Ask(M message, IMessageFactory<M> factory, TimeSpan? timeout = null, System.Threading.CancellationToken? cancellationToken = null)
        {
            throw new InvalidOperationException($"Actor {ActorPath} cannot receive messages in this context");
        }

        public override void Tell(M message, ICanReceiveMessage<M> sender)
        {
            throw new InvalidOperationException($"Actor {ActorPath} cannot receive messages in this context");
        }
    }
}
